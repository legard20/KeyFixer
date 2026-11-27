using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace KeyFixer
{
    public static class Program
    {
        private const string MutexName = @"Local\KeyFixer_SingleInstance_Mutex_v1";

        [STAThread]
        public static int Main(string[] args)
        {
            // Optional lightweight CLI mode for automated verification / scripting
            if (args != null && args.Length > 0)
            {
                if (args[0] == "--version")
                {
                    Console.WriteLine("KeyFixer 1.1.0");
                    return 0;
                }
                if (args[0] == "--convert" && args.Length >= 2)
                {
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
                    ConversionResult res = LayoutConverter.ConvertSmart(args[1], true);
                    Console.WriteLine(res.ConvertedText);
                    return 0;
                }
            }

            bool createdNew;
            using (Mutex singleInstance = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "أداة KeyFixer تعمل بالفعل في الخلفية بجانب الساعة (System Tray).",
                        "KeyFixer",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button1,
                        MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (KeyFixerTrayContext ctx = new KeyFixerTrayContext())
                {
                    Application.Run(ctx);
                }
            }

            return 0;
        }
    }

    internal sealed class KeyFixerTrayContext : ApplicationContext
    {
        private AppSettings _settings;
        private readonly HotkeyManager _hotkeyManager;
        private readonly TextReplacer _replacer;
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly Icon _activeIcon;
        private readonly Icon _pausedIcon;

        private readonly ToolStripMenuItem _mnuHeader;
        private readonly ToolStripMenuItem _mnuTogglePause;
        private readonly ToolStripMenuItem _mnuStartup;

        private bool _paused;
        private SettingsForm _openSettingsForm;

        public KeyFixerTrayContext()
        {
            _settings = AppSettings.Load();
            _settings.StartWithWindows = AppSettings.IsStartupEnabled();

            _activeIcon = TrayIconFactory.CreateTrayIcon(true);
            _pausedIcon = TrayIconFactory.CreateTrayIcon(false);

            _replacer = new TextReplacer();
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            _trayMenu = new ContextMenuStrip();
            _trayMenu.RightToLeft = RightToLeft.Yes;
            _trayMenu.Font = new Font("Segoe UI", 9.5f);

            _mnuHeader = new ToolStripMenuItem("KeyFixer")
            {
                Enabled = false,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            _mnuTogglePause = new ToolStripMenuItem("إيقاف مؤقت", null, OnTogglePauseClick);
            ToolStripMenuItem mnuSettings = new ToolStripMenuItem("الإعدادات وتخصيص الزر...", null, OnOpenSettingsClick);
            _mnuStartup = new ToolStripMenuItem("تشغيل مع بدء الويندوز", null, OnToggleStartupClick)
            {
                Checked = _settings.StartWithWindows
            };
            ToolStripMenuItem mnuExit = new ToolStripMenuItem("خروج", null, OnExitClick);

            _trayMenu.Items.Add(_mnuHeader);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_mnuTogglePause);
            _trayMenu.Items.Add(mnuSettings);
            _trayMenu.Items.Add(_mnuStartup);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(mnuExit);

            _trayIcon = new NotifyIcon
            {
                Icon = _activeIcon,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += OnOpenSettingsClick;

            string err;
            bool ok = _hotkeyManager.Register(_settings.HotkeyModifiers, _settings.GetKeys(), out err);
            UpdateTrayStatus();

            if (!ok)
            {
                _trayIcon.ShowBalloonTip(
                    4000,
                    "KeyFixer — تنبيه الاختصار",
                    err + "\nاضغط مرتين على الأيقونة لاختيار زر آخر.",
                    ToolTipIcon.Warning);
            }
            else
            {
                _trayIcon.ShowBalloonTip(
                    2500,
                    "KeyFixer يعمل في الخلفية",
                    "حدد النص المكتوب بالخطأ واضغط " + AppSettings.FormatHotkey(_settings.HotkeyModifiers, _settings.GetKeys()) + " لتصحيحه فوراً.",
                    ToolTipIcon.Info);
            }
        }

        private void OnHotkeyPressed(object sender, EventArgs e)
        {
            if (_paused) return;
            try
            {
                _replacer.ProcessSelectedText(_settings, _hotkeyManager);
            }
            catch
            {
                // Keep tray app alive if a foreground app or clipboard call throws unexpectedly
            }
        }

        private void OnTogglePauseClick(object sender, EventArgs e)
        {
            _paused = !_paused;
            if (_paused)
            {
                _hotkeyManager.Unregister();
            }
            else
            {
                string err;
                if (!_hotkeyManager.Register(_settings.HotkeyModifiers, _settings.GetKeys(), out err))
                {
                    _paused = true;
                    _trayIcon.ShowBalloonTip(3500, "KeyFixer", err, ToolTipIcon.Warning);
                }
            }
            UpdateTrayStatus();
        }

        private void OnToggleStartupClick(object sender, EventArgs e)
        {
            bool next = !_settings.StartWithWindows;
            try
            {
                AppSettings.ApplyWindowsStartup(next, Application.ExecutablePath);
                _settings.StartWithWindows = next;
                _settings.Save();
                _mnuStartup.Checked = next;
            }
            catch (Exception ex)
            {
                MessageBox.Show("تعذر تعديل التشغيل التلقائي:\n" + ex.Message, "KeyFixer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnOpenSettingsClick(object sender, EventArgs e)
        {
            if (_openSettingsForm != null && !_openSettingsForm.IsDisposed)
            {
                _openSettingsForm.Activate();
                return;
            }

            Action suspend = delegate
            {
                _hotkeyManager.Unregister();
            };
            Action resume = delegate
            {
                if (!_paused)
                {
                    string ignored;
                    _hotkeyManager.Register(_settings.HotkeyModifiers, _settings.GetKeys(), out ignored);
                }
            };

            using (SettingsForm form = new SettingsForm(_settings, suspend, resume))
            {
                _openSettingsForm = form;
                DialogResult res = form.ShowDialog();
                _openSettingsForm = null;

                if (res == DialogResult.OK)
                {
                    ApplyNewSettings(form.ResultSettings);
                }
            }
        }

        private void ApplyNewSettings(AppSettings updated)
        {
            string err = null;
            bool ok = true;

            if (!_paused)
            {
                ok = _hotkeyManager.Register(updated.HotkeyModifiers, updated.GetKeys(), out err);
                if (!ok)
                {
                    // Restore previous hotkey if new one failed to register
                    string ignored;
                    _hotkeyManager.Register(_settings.HotkeyModifiers, _settings.GetKeys(), out ignored);
                    MessageBox.Show(err, "KeyFixer — تعذر حفظ الاختصار", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _settings = updated;
            try
            {
                AppSettings.ApplyWindowsStartup(_settings.StartWithWindows, Application.ExecutablePath);
            }
            catch
            {
                // Keep app running even if registry is restricted
            }

            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("تم تطبيق الإعدادات لكن تعذر حفظ الملف:\n" + ex.Message, "KeyFixer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            UpdateTrayStatus();
        }

        private void UpdateTrayStatus()
        {
            string hk = AppSettings.FormatHotkey(_settings.HotkeyModifiers, _settings.GetKeys());
            if (_paused)
            {
                _trayIcon.Icon = _pausedIcon;
                _mnuHeader.Text = "KeyFixer — متوقف مؤقتاً";
                _mnuTogglePause.Text = "تفعيل الأداة";
                SetTrayTooltip("KeyFixer (متوقف مؤقتاً)");
            }
            else
            {
                _trayIcon.Icon = _activeIcon;
                _mnuHeader.Text = "KeyFixer — نشط (" + hk + ")";
                _mnuTogglePause.Text = "إيقاف مؤقت";
                SetTrayTooltip("KeyFixer — " + hk);
            }
            _mnuStartup.Checked = _settings.StartWithWindows;
        }

        private void SetTrayTooltip(string text)
        {
            // NotifyIcon.Text has a hard 63-character limit in .NET Framework WinForms
            if (text.Length > 63) text = text.Substring(0, 63);
            _trayIcon.Text = text;
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hotkeyManager.Dispose();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayMenu.Dispose();
                _activeIcon.Dispose();
                _pausedIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
