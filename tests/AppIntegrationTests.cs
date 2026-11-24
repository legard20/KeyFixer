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

        Console.WriteLine(string.Format("\nSummary: {0} passed, {1} failed.", _passed, _failed));
        return _failed == 0 ? 0 : 1;
    }
}
