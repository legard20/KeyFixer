using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyFixer
{
    /// <summary>
    /// Registers a single system-wide Win32 global hotkey on a hidden NativeWindow.
    /// </summary>
    public sealed class HotkeyManager : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0x4B46; // 'KF'
        private const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private bool _disposed;
        private bool _registered;
        private uint _currentModifiers;
        private Keys _currentKey;

        public event EventHandler HotkeyPressed;

        public bool IsRegistered { get { return _registered; } }
        public uint CurrentModifiers { get { return _currentModifiers; } }
        public Keys CurrentKey { get { return _currentKey; } }

        public HotkeyManager()
        {
            CreateHandle(new CreateParams());
        }

        public bool Register(uint modifiers, Keys key, out string errorMessage)
        {
            errorMessage = null;
            Unregister();

            Keys keyCode = key & Keys.KeyCode;
            if (!AppSettings.IsAllowedHotkey(modifiers, keyCode))
            {
                errorMessage = "الاختصار المحدد غير مدعوم. اختر زراً مثل PgDn أو Pause أو اختصاراً مع Ctrl/Alt.";
                return false;
            }

            bool ok = RegisterHotKey(Handle, HOTKEY_ID, modifiers | MOD_NOREPEAT, (uint)keyCode);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = "تعذر تسجيل الاختصار " + AppSettings.FormatHotkey(modifiers, keyCode) +
                               " (قد يكون مستخدماً من برنامج آخر، كود الخطأ: " + err + ").";
                return false;
            }

            _registered = true;
            _currentModifiers = modifiers;
            _currentKey = keyCode;
            return true;
        }

        public void Unregister()
        {
            if (_registered && Handle != IntPtr.Zero)
            {
                UnregisterHotKey(Handle, HOTKEY_ID);
                _registered = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                EventHandler handler = HotkeyPressed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unregister();
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }
}
