using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KeyFixer;

public static class AppIntegrationTests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void AssertTrue(string name, bool condition, string detail)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine("[PASS] " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("[FAIL] " + name + " (" + detail + ")");
        }
    }

    private static void AssertEqual(string name, string expected, string actual)
    {
        AssertTrue(name, string.Equals(expected, actual, StringComparison.Ordinal),
            "expected='" + expected + "' actual='" + actual + "'");
    }

    [STAThread]
    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 1. Default AppSettings
        AppSettings def = new AppSettings();
        AssertTrue("Default hotkey is PageDown (Keys.Next)", def.GetKeys() == Keys.Next && def.HotkeyModifiers == 0,
            "mods=" + def.HotkeyModifiers + " key=" + def.GetKeys());
        AssertEqual("Default hotkey label", "PgDn (PageDown)", AppSettings.FormatHotkey(def.HotkeyModifiers, def.GetKeys()));
        AssertTrue("Default SmartMixedMode is true", def.SmartMixedMode, "false");
        AssertTrue("Default PassThroughWhenNoSelection is true", def.PassThroughWhenNoSelection, "false");
        AssertTrue("Default AutoSwitchKeyboardLayout is true", def.AutoSwitchKeyboardLayout, "false");
        AssertTrue("Default StartWithWindows is false", !def.StartWithWindows, "true");

        // 2. Hotkey validation rules
        AssertTrue("Allow bare PgDn", AppSettings.IsAllowedHotkey(0, Keys.Next), "rejected");
        AssertTrue("Allow bare Pause", AppSettings.IsAllowedHotkey(0, Keys.Pause), "rejected");
        AssertTrue("Allow bare F9", AppSettings.IsAllowedHotkey(0, Keys.F9), "rejected");
        AssertTrue("Allow Ctrl+Shift+Space", AppSettings.IsAllowedHotkey(AppSettings.MOD_CONTROL | AppSettings.MOD_SHIFT, Keys.Space), "rejected");
        AssertTrue("Allow Alt+Q", AppSettings.IsAllowedHotkey(AppSettings.MOD_ALT, Keys.Q), "rejected");
        AssertTrue("Block bare A", !AppSettings.IsAllowedHotkey(0, Keys.A), "allowed");
        AssertTrue("Block bare Space", !AppSettings.IsAllowedHotkey(0, Keys.Space), "allowed");
        AssertTrue("Block bare Enter", !AppSettings.IsAllowedHotkey(0, Keys.Enter), "allowed");
        AssertTrue("Block bare Escape", !AppSettings.IsAllowedHotkey(0, Keys.Escape), "allowed");
        AssertTrue("Block bare ControlKey", !AppSettings.IsAllowedHotkey(AppSettings.MOD_CONTROL, Keys.ControlKey), "allowed");

        AssertEqual("Format Ctrl+Shift+Space",
            "Ctrl + Shift + Space",
            AppSettings.FormatHotkey(AppSettings.MOD_CONTROL | AppSettings.MOD_SHIFT, Keys.Space));

        // 3. Settings Save / Load persistence + corruption resilience
        string tempDir = Path.Combine(Path.GetTempPath(), "KeyFixerTest_" + Guid.NewGuid().ToString("N"));
        string tempFile = Path.Combine(tempDir, "settings.json");
        try
        {
            AppSettings custom = new AppSettings();
            custom.SetHotkey(AppSettings.MOD_CONTROL | AppSettings.MOD_ALT, Keys.K);
            custom.SmartMixedMode = false;
            custom.PassThroughWhenNoSelection = false;
            custom.AutoSwitchKeyboardLayout = false;
            custom.Save(tempFile);

            AppSettings reloaded = AppSettings.Load(tempFile);
            AssertTrue("Settings round-trip hotkey",
                reloaded.HotkeyModifiers == (AppSettings.MOD_CONTROL | AppSettings.MOD_ALT) && reloaded.GetKeys() == Keys.K,
                "mods=" + reloaded.HotkeyModifiers + " key=" + reloaded.GetKeys());
            AssertTrue("Settings round-trip flags",
                !reloaded.SmartMixedMode && !reloaded.PassThroughWhenNoSelection && !reloaded.AutoSwitchKeyboardLayout,
                "flags mismatch");

            // Corrupted JSON falls back cleanly to PgDn defaults
            File.WriteAllText(tempFile, "{ not valid json!!!");
            AppSettings recovered = AppSettings.Load(tempFile);
            AssertTrue("Corrupted settings recover to PgDn default",
                recovered.GetKeys() == Keys.Next && recovered.HotkeyModifiers == 0,
                "mods=" + recovered.HotkeyModifiers + " key=" + recovered.GetKeys());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        // 4. Win32 SendInput INPUT struct size check (must be 40 on x64, 28 on x86)
        int expectedInputSize = IntPtr.Size == 8 ? 40 : 28;
        AssertTrue("Win32 SendInput INPUT struct size is exact (" + expectedInputSize + ")",
            TextReplacer.InputStructSize == expectedInputSize,
            "actual=" + TextReplacer.InputStructSize);

        // 5. Real Win32 HotkeyManager register / re-register / unregister
        using (HotkeyManager hm = new HotkeyManager())
        {
            string err;
            bool okPgDn = hm.Register(AppSettings.MOD_NONE, Keys.Next, out err);
            AssertTrue("Win32 RegisterHotKey(PageDown)", okPgDn && hm.IsRegistered, err ?? "failed");

            bool okCustom = hm.Register(AppSettings.MOD_CONTROL | AppSettings.MOD_SHIFT, Keys.F11, out err);
            AssertTrue("Win32 RegisterHotKey(Ctrl+Shift+F11)", okCustom && hm.IsRegistered && hm.CurrentKey == Keys.F11, err ?? "failed");

            hm.Unregister();
            AssertTrue("Win32 UnregisterHotKey clears state", !hm.IsRegistered, "still registered");
        }

        // 6. TrayIconFactory active & paused icons
        using (Icon icActive = TrayIconFactory.CreateTrayIcon(true))
        using (Icon icPaused = TrayIconFactory.CreateTrayIcon(false))
        {
            AssertTrue("TrayIcon active is 32x32", icActive != null && icActive.Width == 32 && icActive.Height == 32, "bad active icon");
            AssertTrue("TrayIcon paused is 32x32", icPaused != null && icPaused.Width == 32 && icPaused.Height == 32, "bad paused icon");
        }

        // 7. SettingsForm instantiation & RTL verification
        using (SettingsForm form = new SettingsForm(new AppSettings(), null, null))
        {
            AssertTrue("SettingsForm is RTL Arabic",
                form.RightToLeft == RightToLeft.Yes && form.RightToLeftLayout,
                "RTL not set");
        }

        // 8. Problem 3: Navigation keys (PgDn, PgUp, Home, End, Arrows) never swallowed
        AssertTrue("P3 IsNavigationKey recognizes PgDn/PgUp/Home/End/Arrows",
            AppSettings.IsNavigationKey(Keys.Next) && AppSettings.IsNavigationKey(Keys.Prior) &&
            AppSettings.IsNavigationKey(Keys.Home) && AppSettings.IsNavigationKey(Keys.End) &&
            AppSettings.IsNavigationKey(Keys.Down) && !AppSettings.IsNavigationKey(Keys.Pause) &&
            !AppSettings.IsNavigationKey(Keys.F9),
            "navigation key classification mismatch");

        // P3a: PgDn with no selection -> passes through PgDn
        {
            FakePlatform env = new FakePlatform();
            env.CopyProducesSelection = false;
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            AppSettings s = new AppSettings(); // Default PgDn
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(s, null);
            AssertTrue("P3a PgDn with no selection passes through",
                o.Status == ReplaceStatus.NoSelectionPassedThrough && o.PassedThrough && env.LastPassThroughKey == Keys.Next,
                "status=" + o.Status + " passed=" + o.PassedThrough);
        }

        // P3b: PgDn in standard Win32 Edit control with no selection -> skips Ctrl+C and passes through immediately
        {
            FakePlatform env = new FakePlatform();
            env.EditHasSelection = false;
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(new AppSettings(), null);
            AssertTrue("P3b PgDn in Edit control with no selection skips Ctrl+C and passes through",
                o.PassedThrough && env.CtrlCCount == 0 && env.LastPassThroughKey == Keys.Next,
                "ctrlC=" + env.CtrlCCount + " passed=" + o.PassedThrough);
        }

        // P3c: PgDn in Windows Terminal -> skips Ctrl+C (never sends SIGINT) and passes through
        {
            FakePlatform env = new FakePlatform();
            env.WindowClassName = "CASCADIA_HOSTING_WINDOW_CLASS";
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(new AppSettings(), null);
            AssertTrue("P3c PgDn in Terminal skips Ctrl+C and passes through",
                o.PassedThrough && env.CtrlCCount == 0 && env.LastPassThroughKey == Keys.Next,
                "ctrlC=" + env.CtrlCCount + " passed=" + o.PassedThrough);
        }

        // P3d: PgDn when Ctrl+C fails -> still passes through (never swallowed)
        {
            FakePlatform env = new FakePlatform();
            env.FailCtrlC = true;
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(new AppSettings(), null);
            AssertTrue("P3d PgDn when Ctrl+C fails still passes through",
                o.PassedThrough && env.LastPassThroughKey == Keys.Next,
                "passed=" + o.PassedThrough);
        }

        // P3e: PgUp when selected text needs no change (e.g. valid URL) -> restores clipboard and passes through
        {
            FakePlatform env = new FakePlatform();
            env.SelectedTextToCopy = "https://example.com";
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            AppSettings s = new AppSettings();
            s.SetHotkey(AppSettings.MOD_NONE, Keys.Prior);
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(s, null);
            AssertTrue("P3e PgUp with unchanged URL passes through and restores clipboard",
                o.Status == ReplaceStatus.NoChange && o.PassedThrough && o.ClipboardRestored && env.LastPassThroughKey == Keys.Prior,
                "status=" + o.Status + " passed=" + o.PassedThrough + " restored=" + o.ClipboardRestored);
        }

        // 9. Problem 4: Window focus check before pasting — cancel safely if focus changed
        {
            FakePlatform env = new FakePlatform();
            env.SelectedTextToCopy = "اثممخ صخقمي";
            env.SwitchForegroundAfterRead = new IntPtr(0x2222);
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(new AppSettings(), null);
            AssertTrue("P4a Focus changed to another window cancels paste and restores clipboard",
                o.Status == ReplaceStatus.CancelledFocusChanged && env.CtrlVCount == 0 &&
                env.LayoutSwitchCount == 0 && o.ClipboardRestored && env.ClipboardText == "ORIGINAL_USER_CLIP",
                "status=" + o.Status + " ctrlV=" + env.CtrlVCount + " clip=" + env.ClipboardText);
        }

        {
            FakePlatform env = new FakePlatform();
            env.SelectedTextToCopy = "hgsghl ugd;l";
            env.SwitchFocusedControlAfterWrite = new IntPtr(0xB002);
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(new AppSettings(), null);
            AssertTrue("P4b Focused control changed right before Ctrl+V cancels paste and restores clipboard",
                o.Status == ReplaceStatus.CancelledFocusChanged && env.CtrlVCount == 0 &&
                o.ClipboardRestored && env.ClipboardText == "ORIGINAL_USER_CLIP",
                "status=" + o.Status + " ctrlV=" + env.CtrlVCount + " clip=" + env.ClipboardText);
        }

        // 10. Problem 5: Safe clipboard restore timing & race protection with slow apps
        {
            FakePlatform env = new FakePlatform();
            env.SelectedTextToCopy = "اثممخ صخقمي";
            env.OpenClipboardPollsRemaining = 4; // Simulates slow app holding clipboard open for 100ms past min delay
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(new AppSettings(), null);
            AssertTrue("P5a Slow app waits >= 450ms + extra clipboard-open time before restoring",
                o.Status == ReplaceStatus.Replaced && env.CtrlVCount == 1 &&
                env.PastedText == "hello world" && o.RestoreWaitMs >= 550 &&
                o.ClipboardRestored && env.ClipboardText == "ORIGINAL_USER_CLIP",
                "status=" + o.Status + " waitMs=" + o.RestoreWaitMs + " pasted=" + env.PastedText + " clip=" + env.ClipboardText);
        }

        {
            FakePlatform env = new FakePlatform();
            env.SelectedTextToCopy = "hgsghl ugd;l";
            env.ExternalClipboardChangeOnSleepStep = 3; // Simulates user copying new text during wait
            TextReplacer replacer = new TextReplacer(env, 450, 350);
            ReplaceOutcome o = replacer.ProcessSelectedTextDetailed(new AppSettings(), null);
            AssertTrue("P5b External clipboard change during wait skips stale restore (never clobbers new copy)",
                o.Status == ReplaceStatus.Replaced && !o.ClipboardRestored && env.ClipboardText == "NEW_USER_COPY",
                "restored=" + o.ClipboardRestored + " clip=" + env.ClipboardText);
        }

        Console.WriteLine(string.Format("\nSummary: {0} passed, {1} failed.", _passed, _failed));
        return _failed == 0 ? 0 : 1;
    }

    private sealed class FakePlatform : IReplacerPlatform
    {
        public IntPtr ForegroundWindow = new IntPtr(0x1111);
        public IntPtr FocusedControl = new IntPtr(0xA001);
        public string WindowClassName = "Chrome_WidgetWin_1";
        public bool? EditHasSelection = null;
        public bool CopyProducesSelection = true;
        public bool FailCtrlC = false;
        public string SelectedTextToCopy = "اثممخ صخقمي";
        public string ClipboardText = "ORIGINAL_USER_CLIP";
        public string PastedText = null;
        public uint ClipSeq = 10;

        public IntPtr SwitchForegroundAfterRead = IntPtr.Zero;
        public IntPtr SwitchFocusedControlAfterWrite = IntPtr.Zero;
        public int OpenClipboardPollsRemaining = 0;
        public int ExternalClipboardChangeOnSleepStep = -1;

        public int CtrlCCount = 0;
        public int CtrlVCount = 0;
        public int LayoutSwitchCount = 0;
        public Keys LastPassThroughKey = Keys.None;
        private int _sleepCallsAfterPaste = 0;
        private bool _pasteSent = false;

        public IntPtr GetForegroundWindow() { return ForegroundWindow; }
        public IntPtr GetFocusedControl(IntPtr fg) { return FocusedControl; }
        public string GetWindowClassName(IntPtr hWnd) { return WindowClassName; }
        public bool? TryCheckEditControlHasSelection(IntPtr focusedControl) { return EditHasSelection; }
        public uint GetClipboardSequenceNumber() { return ClipSeq; }

        public IntPtr GetOpenClipboardWindow()
        {
            if (OpenClipboardPollsRemaining > 0)
            {
                OpenClipboardPollsRemaining--;
                return ForegroundWindow;
            }
            return IntPtr.Zero;
        }

        public bool IsKeyDown(int vk) { return false; }

        public bool SendCtrlCombo(ushort vkKey)
        {
            if (vkKey == 0x43) // C
            {
                CtrlCCount++;
                if (FailCtrlC) return false;
                if (CopyProducesSelection)
                {
                    ClipboardText = SelectedTextToCopy;
                    ClipSeq++;
                }
                return true;
            }
            if (vkKey == 0x56) // V
            {
                CtrlVCount++;
                PastedText = ClipboardText;
                _pasteSent = true;
                return true;
            }
            return true;
        }

        public bool SendPassThroughKey(uint modifiers, Keys key)
        {
            LastPassThroughKey = key;
            return true;
        }

        public string ReadClipboardText()
        {
            string val = ClipboardText;
            if (SwitchForegroundAfterRead != IntPtr.Zero)
            {
                ForegroundWindow = SwitchForegroundAfterRead;
            }
            return val;
        }

        public bool WriteClipboardText(string text)
        {
            ClipboardText = text;
            ClipSeq++;
            if (SwitchFocusedControlAfterWrite != IntPtr.Zero)
            {
                FocusedControl = SwitchFocusedControlAfterWrite;
            }
            return true;
        }

        public object CaptureClipboardSnapshot() { return ClipboardText; }

        public bool RestoreClipboardSnapshot(object snapshot)
        {
            ClipboardText = snapshot as string;
            ClipSeq++;
            return true;
        }

        public void DisposeClipboardSnapshot(object snapshot) { }

        public void SwitchForegroundKeyboardLayout(IntPtr fg, TargetLanguage target)
        {
            LayoutSwitchCount++;
        }

        public void Sleep(int ms)
        {
            if (_pasteSent)
            {
                _sleepCallsAfterPaste++;
                if (ExternalClipboardChangeOnSleepStep > 0 && _sleepCallsAfterPaste == ExternalClipboardChangeOnSleepStep)
                {
                    ClipboardText = "NEW_USER_COPY";
                    ClipSeq++;
                }
            }
        }
    }
}
