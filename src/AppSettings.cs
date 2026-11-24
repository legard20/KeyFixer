using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KeyFixer
{
    public sealed class AppSettings
    {
        public const uint MOD_NONE = 0x0000;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        private const string StartupRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupValueName = "KeyFixer";

        public uint HotkeyModifiers { get; set; }
        public int HotkeyKey { get; set; }
        public bool SmartMixedMode { get; set; }
        public bool PassThroughWhenNoSelection { get; set; }
        public bool AutoSwitchKeyboardLayout { get; set; }
        public bool StartWithWindows { get; set; }

        public AppSettings()
        {
            HotkeyModifiers = MOD_NONE;
            HotkeyKey = (int)Keys.Next; // PageDown (PgDn)
            SmartMixedMode = true;
            PassThroughWhenNoSelection = true;
            AutoSwitchKeyboardLayout = true;
            StartWithWindows = false;
        }

        public Keys GetKeys()
        {
            return (Keys)HotkeyKey;
        }

        public void SetHotkey(uint modifiers, Keys key)
        {
            HotkeyModifiers = modifiers & (MOD_ALT | MOD_CONTROL | MOD_SHIFT | MOD_WIN);
            HotkeyKey = (int)(key & Keys.KeyCode);
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                HotkeyModifiers = HotkeyModifiers,
                HotkeyKey = HotkeyKey,
                SmartMixedMode = SmartMixedMode,
                PassThroughWhenNoSelection = PassThroughWhenNoSelection,
                AutoSwitchKeyboardLayout = AutoSwitchKeyboardLayout,
                StartWithWindows = StartWithWindows
            };
        }

        public static string DefaultSettingsPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(Path.Combine(appData, "KeyFixer"), "settings.json");
            }
        }

        public static AppSettings Load()
        {
            return Load(DefaultSettingsPath);
        }

        public static AppSettings Load(string path)
        {
            AppSettings defaults = new AppSettings();
            try
            {
                if (!File.Exists(path))
                {
                    return defaults;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                AppSettings loaded = ser.Deserialize<AppSettings>(json);
                if (loaded == null)
                {
                    return defaults;
                }

                // Validate hotkey; fall back to default PgDn if corrupted
                Keys k = (Keys)(loaded.HotkeyKey & (int)Keys.KeyCode);
                uint mods = loaded.HotkeyModifiers & (MOD_ALT | MOD_CONTROL | MOD_SHIFT | MOD_WIN);
                if (!IsAllowedHotkey(mods, k))
                {
                    loaded.HotkeyModifiers = MOD_NONE;
                    loaded.HotkeyKey = (int)Keys.Next;
                }
                else
                {
                    loaded.HotkeyModifiers = mods;
                    loaded.HotkeyKey = (int)k;
                }

                return loaded;
            }
            catch
            {
                return defaults;
            }
        }

        public void Save()
        {
            Save(DefaultSettingsPath);
        }

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            JavaScriptSerializer ser = new JavaScriptSerializer();
            string json = ser.Serialize(this);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        public static bool IsAllowedHotkey(uint modifiers, Keys key)
        {
            Keys code = key & Keys.KeyCode;
            if (code == Keys.None) return false;

            // Disallow bare modifier keys themselves as the main key
            if (code == Keys.ControlKey || code == Keys.LControlKey || code == Keys.RControlKey ||
                code == Keys.ShiftKey || code == Keys.LShiftKey || code == Keys.RShiftKey ||
                code == Keys.Menu || code == Keys.LMenu || code == Keys.RMenu ||
                code == Keys.LWin || code == Keys.RWin || code == Keys.Escape)
            {
                return false;
            }

            bool hasCtrlOrAltOrWin = (modifiers & (MOD_CONTROL | MOD_ALT | MOD_WIN)) != 0;
            if (hasCtrlOrAltOrWin)
            {
                return true;
            }

            // Without Ctrl/Alt/Win, only allow safe non-typing function/navigation/special keys
            // so the user cannot accidentally break normal typing with bare letters or Space/Enter.
            if (code >= Keys.F1 && code <= Keys.F24) return true;
            if (code == Keys.Next || code == Keys.Prior || code == Keys.Pause ||
                code == Keys.Scroll || code == Keys.Insert || code == Keys.Home ||
                code == Keys.End || code == Keys.PrintScreen || code == Keys.Apps ||
                code == Keys.Oemtilde)
            {
                return true;
            }

            return false;
        }

        public static string FormatHotkey(uint modifiers, Keys key)
        {
            List<string> parts = new List<string>();
            if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
            parts.Add(FormatKeyName(key & Keys.KeyCode));
            return string.Join(" + ", parts.ToArray());
        }

        public static string FormatKeyName(Keys key)
        {
            switch (key)
            {
                case Keys.Next: return "PgDn (PageDown)";
                case Keys.Prior: return "PgUp (PageUp)";
                case Keys.Oemtilde: return "~ (`)";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.OemPipe: return "\\";
                case Keys.PrintScreen: return "PrtSc";
                case Keys.Scroll: return "ScrollLock";
                case Keys.Capital: return "CapsLock";
                default:
                    if (key >= Keys.D0 && key <= Keys.D9)
                    {
                        return ((char)('0' + (key - Keys.D0))).ToString();
                    }
                    return key.ToString();
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(StartupRegPath, false))
                {
                    if (rk == null) return false;
                    object val = rk.GetValue(StartupValueName);
                    return val != null && !string.IsNullOrEmpty(val.ToString());
                }
            }
            catch
            {
                return false;
            }
        }

        public static void ApplyWindowsStartup(bool enable, string exePath)
        {
            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(StartupRegPath, true))
            {
                if (rk == null) return;
                if (enable)
                {
                    rk.SetValue(StartupValueName, "\"" + exePath + "\"");
                }
                else
                {
                    if (rk.GetValue(StartupValueName) != null)
                    {
                        rk.DeleteValue(StartupValueName, false);
                    }
                }
            }
        }
    }
}
