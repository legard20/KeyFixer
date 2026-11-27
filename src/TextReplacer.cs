using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace KeyFixer
{
    public enum ReplaceStatus
    {
        NoSelectionPassedThrough = 0,
        NoSelectionIgnored = 1,
        NoChange = 2,
        CancelledFocusChanged = 3,
        ClipboardFailed = 4,
        Replaced = 5
    }

    public sealed class ReplaceOutcome
    {
        public ReplaceStatus Status { get; set; }
        public ConversionResult Conversion { get; set; }
        public bool PassedThrough { get; set; }
        public bool ClipboardRestored { get; set; }
        public int RestoreWaitMs { get; set; }
    }

    /// <summary>
    /// Abstraction over Win32 window/input/clipboard operations so TextReplacer's
    /// focus checks, navigation-key pass-through, and clipboard-restore timing can be tested directly.
    /// </summary>
    public interface IReplacerPlatform
    {
        IntPtr GetForegroundWindow();
        IntPtr GetFocusedControl(IntPtr foregroundWindow);
        string GetWindowClassName(IntPtr hWnd);
        bool? TryCheckEditControlHasSelection(IntPtr focusedControl);
        uint GetClipboardSequenceNumber();
        IntPtr GetOpenClipboardWindow();
        bool IsKeyDown(int vk);
        bool SendCtrlCombo(ushort vkKey);
        bool SendPassThroughKey(uint modifiers, Keys key);
        string ReadClipboardText();
        bool WriteClipboardText(string text);
        object CaptureClipboardSnapshot();
        bool RestoreClipboardSnapshot(object snapshot);
        void DisposeClipboardSnapshot(object snapshot);
        void SwitchForegroundKeyboardLayout(IntPtr foregroundWindow, TargetLanguage target);
        void Sleep(int ms);
    }

    /// <summary>
    /// Handles copying selected text via SendInput (Ctrl+C), converting it with LayoutConverter,
    /// verifying window focus before pasting (Ctrl+V), waiting safely before restoring the user's
    /// clipboard, and ensuring navigation keys like PgDn are never swallowed when scrolling.
    /// </summary>
    public sealed class TextReplacer
    {
        public const int ClipboardRestoreMinDelayMs = 450;
        public const int ClipboardRestoreMaxExtraWaitMs = 350;
        public const int ClipboardCopyTimeoutMs = 140;

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
        private const uint EM_GETSEL = 0x00B0;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

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

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
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
        private static extern IntPtr GetOpenClipboardWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] lpList);

        private readonly IReplacerPlatform _platform;
        private readonly int _restoreMinDelayMs;
        private readonly int _restoreMaxExtraWaitMs;
        private bool _busy;

        public static int InputStructSize
        {
            get { return Marshal.SizeOf(typeof(INPUT)); }
        }

        public TextReplacer()
            : this(new Win32ReplacerPlatform(), ClipboardRestoreMinDelayMs, ClipboardRestoreMaxExtraWaitMs)
        {
        }

        public TextReplacer(IReplacerPlatform platform, int restoreMinDelayMs, int restoreMaxExtraWaitMs)
        {
            _platform = platform ?? new Win32ReplacerPlatform();
            _restoreMinDelayMs = restoreMinDelayMs;
            _restoreMaxExtraWaitMs = restoreMaxExtraWaitMs;
        }

        public ConversionResult ProcessSelectedText(AppSettings settings, HotkeyManager hotkeyManager)
        {
            ReplaceOutcome outcome = ProcessSelectedTextDetailed(settings, hotkeyManager);
            return outcome != null ? outcome.Conversion : null;
        }

        public ReplaceOutcome ProcessSelectedTextDetailed(AppSettings settings, HotkeyManager hotkeyManager)
        {
            if (_busy || settings == null) return null;
            _busy = true;

            ReplaceOutcome outcome = new ReplaceOutcome { Status = ReplaceStatus.NoSelectionIgnored };
            object backup = null;

            try
            {
                Keys hotkey = settings.GetKeys();
                bool isNavKey = AppSettings.IsNavigationKey(hotkey);
                bool shouldPassThrough = isNavKey || settings.PassThroughWhenNoSelection;

                // 1. Snapshot the active window and focused control BEFORE doing anything
                IntPtr initialForeground = _platform.GetForegroundWindow();
                if (initialForeground == IntPtr.Zero)
                {
                    if (shouldPassThrough)
                    {
                        outcome.PassedThrough = PassThroughHotkey(settings, hotkeyManager);
                        outcome.Status = ReplaceStatus.NoSelectionPassedThrough;
                    }
                    return outcome;
                }
                IntPtr initialFocus = _platform.GetFocusedControl(initialForeground);

                // 2. For bare navigation keys (e.g. PgDn):
                //    - Never send Ctrl+C to Console/Terminal windows (would send SIGINT and kill commands).
                //    - If the focused Win32 Edit/RichEdit control clearly has no selection, pass through immediately (~1ms).
                if (isNavKey && settings.HotkeyModifiers == AppSettings.MOD_NONE)
                {
                    string fgClass = _platform.GetWindowClassName(initialForeground);
                    if (IsTerminalWindowClass(fgClass))
                    {
                        outcome.PassedThrough = PassThroughHotkey(settings, hotkeyManager);
                        outcome.Status = ReplaceStatus.NoSelectionPassedThrough;
                        return outcome;
                    }

                    bool? hasEditSelection = _platform.TryCheckEditControlHasSelection(initialFocus);
                    if (hasEditSelection.HasValue && !hasEditSelection.Value)
                    {
                        outcome.PassedThrough = PassThroughHotkey(settings, hotkeyManager);
                        outcome.Status = ReplaceStatus.NoSelectionPassedThrough;
                        return outcome;
                    }
                }

                // 3. Wait only for modifier keys (Ctrl/Alt/Shift/Win) to be released so they don't collide with Ctrl+C.
                //    Do NOT block waiting for a bare navigation key like PgDn to be released (keeps scrolling responsive).
                if (settings.HotkeyModifiers != AppSettings.MOD_NONE || !isNavKey)
                {
                    WaitForModifiersReleased(settings.HotkeyModifiers, isNavKey ? Keys.None : hotkey, 300);
                }

                // 4. Back up user's current clipboard
                backup = _platform.CaptureClipboardSnapshot();

                // 5. Send Ctrl+C and check if clipboard changed
                uint seqBefore = _platform.GetClipboardSequenceNumber();
                if (!_platform.SendCtrlCombo(VK_C))
                {
                    if (shouldPassThrough)
                    {
                        outcome.PassedThrough = PassThroughHotkey(settings, hotkeyManager);
                        outcome.Status = ReplaceStatus.NoSelectionPassedThrough;
                    }
                    return outcome;
                }

                bool copied = WaitForClipboardChange(seqBefore, ClipboardCopyTimeoutMs);
                if (!copied)
                {
                    if (shouldPassThrough)
                    {
                        outcome.PassedThrough = PassThroughHotkey(settings, hotkeyManager);
                        outcome.Status = ReplaceStatus.NoSelectionPassedThrough;
                    }
                    return outcome;
                }

                // 6. Read selected text from clipboard
                string selected = _platform.ReadClipboardText();
                if (string.IsNullOrEmpty(selected) || selected.Trim().Length == 0)
                {
                    outcome.ClipboardRestored = _platform.RestoreClipboardSnapshot(backup);
                    if (isNavKey)
                    {
                        outcome.PassedThrough = PassThroughHotkey(settings, hotkeyManager);
                        outcome.Status = ReplaceStatus.NoSelectionPassedThrough;
                    }
                    return outcome;
                }

                // 7. Convert text
                ConversionResult result = LayoutConverter.ConvertSmart(selected, settings.SmartMixedMode);
                outcome.Conversion = result;
                if (!result.Changed)
                {
                    outcome.ClipboardRestored = _platform.RestoreClipboardSnapshot(backup);
                    outcome.Status = ReplaceStatus.NoChange;
                    if (isNavKey)
                    {
                        outcome.PassedThrough = PassThroughHotkey(settings, hotkeyManager);
                    }
                    return outcome;
                }

                // 8. Verify the EXACT same window (and focused control) is still focused BEFORE pasting!
                if (!IsSameWindowFocused(initialForeground, initialFocus))
                {
                    outcome.ClipboardRestored = _platform.RestoreClipboardSnapshot(backup);
                    outcome.Status = ReplaceStatus.CancelledFocusChanged;
                    return outcome;
                }

                // 9. Write converted text to clipboard
                if (!_platform.WriteClipboardText(result.ConvertedText))
                {
                    outcome.ClipboardRestored = _platform.RestoreClipboardSnapshot(backup);
                    outcome.Status = ReplaceStatus.ClipboardFailed;
                    return outcome;
                }

                uint seqAfterWrite = _platform.GetClipboardSequenceNumber();
                _platform.Sleep(15);

                // Re-verify focus right before sending Ctrl+V
                if (!IsSameWindowFocused(initialForeground, initialFocus))
                {
                    outcome.ClipboardRestored = _platform.RestoreClipboardSnapshot(backup);
                    outcome.Status = ReplaceStatus.CancelledFocusChanged;
                    return outcome;
                }

                if (!_platform.SendCtrlCombo(VK_V))
                {
                    outcome.ClipboardRestored = _platform.RestoreClipboardSnapshot(backup);
                    outcome.Status = ReplaceStatus.ClipboardFailed;
                    return outcome;
                }

                // 10. Optionally switch the verified original window's keyboard language
                if (settings.AutoSwitchKeyboardLayout && result.TargetLanguage != TargetLanguage.None)
                {
                    _platform.SwitchForegroundKeyboardLayout(initialForeground, result.TargetLanguage);
                }

                // 11. Wait safely for slow apps (Chrome/Electron/Word/Telegram) to finish reading clipboard
                //     AND confirm the clipboard was not changed by the user before restoring the backup.
                int waitedMs;
                bool safeToRestore = WaitForPasteToFinish(seqAfterWrite, out waitedMs);
                outcome.RestoreWaitMs = waitedMs;
                if (safeToRestore)
                {
                    outcome.ClipboardRestored = _platform.RestoreClipboardSnapshot(backup);
                }

                outcome.Status = ReplaceStatus.Replaced;
                return outcome;
            }
            finally
            {
                if (backup != null)
                {
                    _platform.DisposeClipboardSnapshot(backup);
                }
                _busy = false;
            }
        }

        private bool IsSameWindowFocused(IntPtr initialForeground, IntPtr initialFocus)
        {
            IntPtr currentForeground = _platform.GetForegroundWindow();
            if (currentForeground == IntPtr.Zero || currentForeground != initialForeground)
            {
                return false;
            }

            if (initialFocus != IntPtr.Zero)
            {
                IntPtr currentFocus = _platform.GetFocusedControl(currentForeground);
                if (currentFocus != IntPtr.Zero && currentFocus != initialFocus)
                {
                    return false;
                }
            }

            return true;
        }

        private bool WaitForPasteToFinish(uint seqAfterWrite, out int totalWaitedMs)
        {
            totalWaitedMs = 0;
            const int stepMs = 25;

            // Phase 1: Wait minimum safe window for async paste handling in slow apps (Electron/Chrome/Word)
            while (totalWaitedMs < _restoreMinDelayMs)
            {
                if (_platform.GetClipboardSequenceNumber() != seqAfterWrite)
                {
                    // User or another app placed new data on the clipboard; do NOT clobber it!
                    return false;
                }
                _platform.Sleep(stepMs);
                totalWaitedMs += stepMs;
            }

            // Phase 2: If the target app currently has the clipboard open/locked, wait until it closes
            int extraWaited = 0;
            while (_platform.GetOpenClipboardWindow() != IntPtr.Zero && extraWaited < _restoreMaxExtraWaitMs)
            {
                if (_platform.GetClipboardSequenceNumber() != seqAfterWrite)
                {
                    return false;
                }
                _platform.Sleep(stepMs);
                extraWaited += stepMs;
                totalWaitedMs += stepMs;
            }

            // Final sequence check right before restore
            return _platform.GetClipboardSequenceNumber() == seqAfterWrite;
        }

        private bool PassThroughHotkey(AppSettings settings, HotkeyManager hotkeyManager)
        {
            string ignored;
            bool wasRegistered = (hotkeyManager != null && hotkeyManager.IsRegistered);
            if (wasRegistered)
            {
                hotkeyManager.Unregister();
            }

            try
            {
                bool sent = _platform.SendPassThroughKey(settings.HotkeyModifiers, settings.GetKeys());
                _platform.Sleep(50);
                return sent;
            }
            finally
            {
                if (wasRegistered)
                {
                    hotkeyManager.Register(settings.HotkeyModifiers, settings.GetKeys(), out ignored);
                }
            }
        }

        private void WaitForModifiersReleased(uint modifiers, Keys keyToWait, int maxMs)
        {
            int elapsed = 0;
            int vk = (int)(keyToWait & Keys.KeyCode);
            while (elapsed < maxMs)
            {
                bool down = (vk != 0 && _platform.IsKeyDown(vk)) ||
                            _platform.IsKeyDown(VK_SHIFT) ||
                            _platform.IsKeyDown(VK_MENU) ||
                            _platform.IsKeyDown(VK_LWIN) ||
                            _platform.IsKeyDown(VK_RWIN) ||
                            _platform.IsKeyDown(VK_CONTROL);
                if (!down) break;
                _platform.Sleep(15);
                elapsed += 15;
            }
        }

        private bool WaitForClipboardChange(uint seqBefore, int timeoutMs)
        {
            int elapsed = 0;
            while (elapsed < timeoutMs)
            {
                if (_platform.GetClipboardSequenceNumber() != seqBefore)
                {
                    return true;
                }
                _platform.Sleep(10);
                elapsed += 10;
            }
            return _platform.GetClipboardSequenceNumber() != seqBefore;
        }

        private static bool IsTerminalWindowClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return false;
            return string.Equals(className, "ConsoleWindowClass", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "CASCADIA_HOSTING_WINDOW_CLASS", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExtendedKey(ushort vk)
        {
            return vk == 0x21 || vk == 0x22 || vk == 0x23 || vk == 0x24 ||
                   vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28 ||
                   vk == 0x2D || vk == 0x2E || vk == 0x2C || vk == 0x5D;
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

        private sealed class Win32ReplacerPlatform : IReplacerPlatform
        {
            public IntPtr GetForegroundWindow()
            {
                return TextReplacer.GetForegroundWindow();
            }

            public IntPtr GetFocusedControl(IntPtr foregroundWindow)
            {
                if (foregroundWindow == IntPtr.Zero) return IntPtr.Zero;
                uint pid;
                uint threadId = GetWindowThreadProcessId(foregroundWindow, out pid);
                if (threadId == 0) return IntPtr.Zero;

                GUITHREADINFO gti = new GUITHREADINFO();
                gti.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
                if (GetGUIThreadInfo(threadId, ref gti))
                {
                    return gti.hwndFocus;
                }
                return IntPtr.Zero;
            }

            public string GetWindowClassName(IntPtr hWnd)
            {
                if (hWnd == IntPtr.Zero) return string.Empty;
                StringBuilder sb = new StringBuilder(128);
                int len = GetClassName(hWnd, sb, sb.Capacity);
                return len > 0 ? sb.ToString(0, len) : string.Empty;
            }

            public bool? TryCheckEditControlHasSelection(IntPtr focusedControl)
            {
                if (focusedControl == IntPtr.Zero) return null;
                string cls = GetWindowClassName(focusedControl);
                if (string.IsNullOrEmpty(cls)) return null;

                bool isStandardEdit =
                    string.Equals(cls, "Edit", StringComparison.OrdinalIgnoreCase) ||
                    cls.IndexOf("RichEdit", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isStandardEdit) return null;

                IntPtr res;
                IntPtr ok = SendMessageTimeout(focusedControl, EM_GETSEL, IntPtr.Zero, IntPtr.Zero,
                    SMTO_ABORTIFHUNG, 50, out res);
                if (ok == IntPtr.Zero) return null;

                long packed = res.ToInt64();
                int start = (int)(packed & 0xFFFF);
                int end = (int)((packed >> 16) & 0xFFFF);
                return start != end;
            }

            public uint GetClipboardSequenceNumber()
            {
                return TextReplacer.GetClipboardSequenceNumber();
            }

            public IntPtr GetOpenClipboardWindow()
            {
                return TextReplacer.GetOpenClipboardWindow();
            }

            public bool IsKeyDown(int vk)
            {
                return (GetAsyncKeyState(vk) & 0x8000) != 0;
            }

            public bool SendCtrlCombo(ushort vkKey)
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

            public bool SendPassThroughKey(uint modifiers, Keys key)
            {
                ushort vk = (ushort)(key & Keys.KeyCode);
                ushort scan = (ushort)MapVirtualKey(vk, 0);
                uint extFlag = IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0;

                // Build down/up sequence including any modifiers
                System.Collections.Generic.List<INPUT> list = new System.Collections.Generic.List<INPUT>();
                AddModifierInputs(list, modifiers, false);

                INPUT keyDown = new INPUT();
                keyDown.type = INPUT_KEYBOARD;
                keyDown.u.ki.wVk = vk;
                keyDown.u.ki.wScan = scan;
                keyDown.u.ki.dwFlags = extFlag;
                list.Add(keyDown);

                INPUT keyUp = new INPUT();
                keyUp.type = INPUT_KEYBOARD;
                keyUp.u.ki.wVk = vk;
                keyUp.u.ki.wScan = scan;
                keyUp.u.ki.dwFlags = extFlag | KEYEVENTF_KEYUP;
                list.Add(keyUp);

                AddModifierInputs(list, modifiers, true);

                INPUT[] arr = list.ToArray();
                uint sent = SendInput((uint)arr.Length, arr, InputStructSize);
                return sent == (uint)arr.Length;
            }

            private static void AddModifierInputs(System.Collections.Generic.List<INPUT> list, uint modifiers, bool keyUp)
            {
                uint flag = keyUp ? KEYEVENTF_KEYUP : 0;
                if ((modifiers & AppSettings.MOD_CONTROL) != 0) AddSimpleKey(list, VK_CONTROL, flag);
                if ((modifiers & AppSettings.MOD_ALT) != 0) AddSimpleKey(list, VK_MENU, flag);
                if ((modifiers & AppSettings.MOD_SHIFT) != 0) AddSimpleKey(list, VK_SHIFT, flag);
                if ((modifiers & AppSettings.MOD_WIN) != 0) AddSimpleKey(list, VK_LWIN, flag | KEYEVENTF_EXTENDEDKEY);
            }

            private static void AddSimpleKey(System.Collections.Generic.List<INPUT> list, ushort vk, uint flags)
            {
                INPUT inp = new INPUT();
                inp.type = INPUT_KEYBOARD;
                inp.u.ki.wVk = vk;
                inp.u.ki.wScan = (ushort)MapVirtualKey(vk, 0);
                inp.u.ki.dwFlags = flags;
                list.Add(inp);
            }

            public string ReadClipboardText()
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

            public bool WriteClipboardText(string text)
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

            public object CaptureClipboardSnapshot()
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

            public bool RestoreClipboardSnapshot(object snapshotObj)
            {
                ClipboardSnapshot snap = snapshotObj as ClipboardSnapshot;
                if (snap == null) return false;
                if (!snap.HasText && !snap.HasImage && !snap.HasFiles) return true;

                for (int i = 0; i < 8; i++)
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
                        }
                        else if (snap.HasFiles && snap.Files != null && snap.Files.Count > 0)
                        {
                            Clipboard.SetFileDropList(snap.Files);
                        }
                        return true;
                    }
                    catch
                    {
                        Thread.Sleep(25);
                    }
                }
                return false;
            }

            public void DisposeClipboardSnapshot(object snapshotObj)
            {
                ClipboardSnapshot snap = snapshotObj as ClipboardSnapshot;
                if (snap != null)
                {
                    snap.Dispose();
                }
            }

            public void SwitchForegroundKeyboardLayout(IntPtr foregroundWindow, TargetLanguage target)
            {
                try
                {
                    if (foregroundWindow == IntPtr.Zero) return;

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
                            PostMessage(foregroundWindow, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layouts[i]);
                            return;
                        }
                    }
                }
                catch
                {
                    // Ignore if foreground app rejects WM_INPUTLANGCHANGEREQUEST
                }
            }

            public void Sleep(int ms)
            {
                if (ms > 0) Thread.Sleep(ms);
            }
        }
    }
}
