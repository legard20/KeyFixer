using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeyFixer
{
    /// <summary>
    /// Handles copying selected text via SendInput (Ctrl+C), converting it with LayoutConverter,
    /// pasting it back (Ctrl+V), preserving the user's clipboard, passing through bare hotkeys
    /// when nothing is selected, and optionally switching the foreground window's keyboard layout.
    /// </summary>
    public sealed class TextReplacer
    {
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_MENU = 0x12;
        private const ushort VK_LWIN = 0x5B;
        private const ushort VK_RWIN = 0x5C;
        private const ushort VK_C = 0x43;
        private const ushort VK_V = 0x56;

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] lpList);

        private bool _busy;

        public static int InputStructSize
        {
            get { return Marshal.SizeOf(typeof(INPUT)); }
        }

        public ConversionResult ProcessSelectedText(AppSettings settings, HotkeyManager hotkeyManager)
        {
            if (_busy) return null;
            _busy = true;

            try
            {
                // 1. Wait briefly for the user to release the hotkey & modifier keys
                WaitForKeysReleased(settings.HotkeyModifiers, settings.GetKeys(), 450);

                // 2. Back up user's current clipboard
                using (ClipboardSnapshot backup = CaptureClipboard())
                {
                    // 3. Send Ctrl+C and check if clipboard changed
                    uint seqBefore = GetClipboardSequenceNumber();
                    if (!SendCtrlCombo(VK_C))
                    {
                        return null;
                    }

                    bool copied = WaitForClipboardChange(seqBefore, 180);
                    if (!copied)
                    {
                        // No text was selected. If bare single key (e.g. PgDn) and passthrough enabled, pass it through!
                        if (settings.PassThroughWhenNoSelection && settings.HotkeyModifiers == AppSettings.MOD_NONE && hotkeyManager != null)
                        {
                            PassThroughSingleKey(settings, hotkeyManager);
                        }
                        return null;
                    }

                    // 4. Read selected text from clipboard
                    string selected = ReadClipboardTextWithRetry();
                    if (string.IsNullOrEmpty(selected))
                    {
                        RestoreClipboard(backup);
                        return null;
                    }

                    // 5. Convert text
                    ConversionResult result = LayoutConverter.ConvertSmart(selected, settings.SmartMixedMode);
                    if (!result.Changed)
                    {
                        RestoreClipboard(backup);
                        return result;
                    }

                    // 6. Write converted text and paste via Ctrl+V
                    if (!WriteClipboardTextWithRetry(result.ConvertedText))
                    {
                        RestoreClipboard(backup);
                        return null;
                    }

                    Thread.Sleep(15);
                    SendCtrlCombo(VK_V);

                    // 7. Optionally switch foreground window's keyboard language
                    if (settings.AutoSwitchKeyboardLayout && result.TargetLanguage != TargetLanguage.None)
                    {
                        TrySwitchForegroundKeyboardLayout(result.TargetLanguage);
                    }

                    // 8. Wait briefly for slow apps (Chrome/Word/Telegram) to finish reading clipboard, then restore
                    Thread.Sleep(140);
                    RestoreClipboard(backup);

                    return result;
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private static void PassThroughSingleKey(AppSettings settings, HotkeyManager hotkeyManager)
        {
            string ignored;
            hotkeyManager.Unregister();
            try
            {
                ushort vk = (ushort)(settings.GetKeys() & Keys.KeyCode);
                ushort scan = (ushort)MapVirtualKey(vk, 0);
                uint extFlag = IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0;

                INPUT[] inputs = new INPUT[2];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = vk;
                inputs[0].u.ki.wScan = scan;
                inputs[0].u.ki.dwFlags = extFlag;

                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = vk;
                inputs[1].u.ki.wScan = scan;
                inputs[1].u.ki.dwFlags = extFlag | KEYEVENTF_KEYUP;

                SendInput((uint)inputs.Length, inputs, InputStructSize);
                Thread.Sleep(25);
            }
            finally
            {
                hotkeyManager.Register(settings.HotkeyModifiers, settings.GetKeys(), out ignored);
            }
        }

        private static bool IsExtendedKey(ushort vk)
        {
            return vk == 0x21 || vk == 0x22 || vk == 0x23 || vk == 0x24 ||
                   vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28 ||
                   vk == 0x2D || vk == 0x2E || vk == 0x2C || vk == 0x5D;
        }

        private static void WaitForKeysReleased(uint modifiers, Keys key, int maxMs)
        {
            int elapsed = 0;
            int vk = (int)(key & Keys.KeyCode);
            while (elapsed < maxMs)
            {
                bool down = IsKeyDown(vk) || IsKeyDown(VK_SHIFT) || IsKeyDown(VK_MENU) ||
                            IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN) || IsKeyDown(VK_CONTROL);
                if (!down) break;
                Thread.Sleep(15);
                elapsed += 15;
            }
        }

        private static bool IsKeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private static bool SendCtrlCombo(ushort vkKey)
        {
            ushort scanCtrl = (ushort)MapVirtualKey(VK_CONTROL, 0);
            ushort scanKey = (ushort)MapVirtualKey(vkKey, 0);

            INPUT[] inputs = new INPUT[4];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = VK_CONTROL;
            inputs[0].u.ki.wScan = scanCtrl;
            inputs[0].u.ki.dwFlags = 0;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = vkKey;
            inputs[1].u.ki.wScan = scanKey;
            inputs[1].u.ki.dwFlags = 0;

            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].u.ki.wVk = vkKey;
            inputs[2].u.ki.wScan = scanKey;
            inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;

            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].u.ki.wVk = VK_CONTROL;
            inputs[3].u.ki.wScan = scanCtrl;
            inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;

            uint sent = SendInput((uint)inputs.Length, inputs, InputStructSize);
            return sent == (uint)inputs.Length;
        }

        private static bool WaitForClipboardChange(uint seqBefore, int timeoutMs)
        {
            int elapsed = 0;
            while (elapsed < timeoutMs)
            {
                if (GetClipboardSequenceNumber() != seqBefore)
                {
                    return true;
                }
                Thread.Sleep(10);
                elapsed += 10;
            }
            return GetClipboardSequenceNumber() != seqBefore;
        }

        private static string ReadClipboardTextWithRetry()
        {
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                    {
                        return Clipboard.GetText(TextDataFormat.UnicodeText);
                    }
                    if (Clipboard.ContainsText())
                    {
                        return Clipboard.GetText();
                    }
                    return null;
                }
                catch
                {
                    Thread.Sleep(15);
                }
            }
            return null;
        }

        private static bool WriteClipboardTextWithRetry(string text)
        {
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    return true;
                }
                catch
                {
                    Thread.Sleep(15);
                }
            }
            return false;
        }

        private static void TrySwitchForegroundKeyboardLayout(TargetLanguage target)
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return;

                int count = GetKeyboardLayoutList(0, null);
                if (count <= 0) return;

                IntPtr[] layouts = new IntPtr[count];
                GetKeyboardLayoutList(count, layouts);

                uint desiredPrimaryLang = (target == TargetLanguage.Arabic) ? 0x01u : 0x09u;
                for (int i = 0; i < layouts.Length; i++)
                {
                    uint langId = (uint)((long)layouts[i] & 0xFFFF);
                    uint primaryLang = langId & 0x3FF;
                    if (primaryLang == desiredPrimaryLang)
                    {
                        PostMessage(fg, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layouts[i]);
                        return;
                    }
                }
            }
            catch
            {
                // Ignore if foreground app rejects WM_INPUTLANGCHANGEREQUEST
            }
        }

        private sealed class ClipboardSnapshot : IDisposable
        {
            public bool HasText;
            public string UnicodeText;
            public bool HasImage;
            public Image ImageData;
            public bool HasFiles;
            public StringCollection Files;

            public void Dispose()
            {
                if (ImageData != null)
                {
                    ImageData.Dispose();
                    ImageData = null;
                }
            }
        }

        private static ClipboardSnapshot CaptureClipboard()
        {
            ClipboardSnapshot snap = new ClipboardSnapshot();
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                    {
                        snap.HasText = true;
                        snap.UnicodeText = Clipboard.GetText(TextDataFormat.UnicodeText);
                    }
                    else if (Clipboard.ContainsImage())
                    {
                        Image img = Clipboard.GetImage();
                        if (img != null)
                        {
                            snap.HasImage = true;
                            snap.ImageData = (Image)img.Clone();
                            img.Dispose();
                        }
                    }
                    else if (Clipboard.ContainsFileDropList())
                    {
                        StringCollection files = Clipboard.GetFileDropList();
                        if (files != null && files.Count > 0)
                        {
                            snap.HasFiles = true;
                            snap.Files = new StringCollection();
                            foreach (string f in files) snap.Files.Add(f);
                        }
                    }
                    return snap;
                }
                catch
                {
                    Thread.Sleep(15);
                }
            }
            return snap;
        }

        private static void RestoreClipboard(ClipboardSnapshot snap)
        {
            if (snap == null) return;
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    if (snap.HasText && snap.UnicodeText != null)
                    {
                        Clipboard.SetText(snap.UnicodeText, TextDataFormat.UnicodeText);
                    }
                    else if (snap.HasImage && snap.ImageData != null)
                    {
                        Clipboard.SetImage(snap.ImageData);
                        snap.ImageData.Dispose();
                        snap.ImageData = null;
                    }
                    else if (snap.HasFiles && snap.Files != null && snap.Files.Count > 0)
                    {
                        Clipboard.SetFileDropList(snap.Files);
                    }
                    return;
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }
            if (snap.ImageData != null)
            {
                snap.ImageData.Dispose();
                snap.ImageData = null;
            }
        }
    }
}
