using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyFixer
{
    /// <summary>
    /// Clean Arabic RTL Settings & Hotkey Customization dialog with a live conversion tester.
    /// </summary>
    public sealed class SettingsForm : Form
    {
        private readonly AppSettings _workingSettings;
        private readonly Action _onSuspendHotkey;
        private readonly Action _onResumeHotkey;

        private bool _capturingHotkey;
        private TextBox _txtHotkey;
        private Button _btnChangeHotkey;
        private Button _btnResetHotkey;
        private Label _lblHotkeyHint;

        private CheckBox _chkSmartMixed;
        private CheckBox _chkPassThrough;
        private CheckBox _chkAutoSwitchLang;
        private CheckBox _chkStartWithWindows;

        private TextBox _txtTestInput;
        private Label _lblTestStatus;

        public AppSettings ResultSettings { get { return _workingSettings; } }

        public SettingsForm(AppSettings currentSettings, Action onSuspendHotkey, Action onResumeHotkey)
        {
            _workingSettings = currentSettings.Clone();
            _onSuspendHotkey = onSuspendHotkey;
            _onResumeHotkey = onResumeHotkey;

            BuildUi();
            SyncUiFromSettings();
        }

        private void BuildUi()
        {
            Text = "إعدادات KeyFixer — مصحح لغة الكتابة";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(490, 520);
            BackColor = Color.FromArgb(247, 249, 252);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            KeyPreview = true;

            try
            {
                Icon = TrayIconFactory.CreateTrayIcon(true);
            }
            catch
            {
                // Non-fatal if icon handle fails
            }

            // Header panel
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(21, 101, 192),
                Padding = new Padding(16, 10, 16, 10)
            };

            Label lblTitle = new Label
            {
                Text = "KeyFixer — التحويل الفوري بين العربي والإنجليزي",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label lblSubtitle = new Label
            {
                Text = "حدد النص المكتوب بالخطأ في أي برنامج واضغط زر الاختصار ليتم تصحيحه فوراً",
                ForeColor = Color.FromArgb(220, 235, 255),
                Font = new Font("Segoe UI", 8.8f, FontStyle.Regular),
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft
            };

            header.Controls.Add(lblSubtitle);
            header.Controls.Add(lblTitle);
            Controls.Add(header);

            // Group 1: Hotkey customization
            GroupBox grpHotkey = new GroupBox
            {
                Text = "زر الاختصار (Hotkey)",
                Location = new Point(16, 82),
                Size = new Size(458, 106),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            _txtHotkey = new TextBox
            {
                Location = new Point(14, 30),
                Size = new Size(216, 28),
                ReadOnly = true,
                BackColor = Color.FromArgb(240, 244, 250),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                TabStop = false
            };

            _btnChangeHotkey = new Button
            {
                Text = "تغيير الزر",
                Location = new Point(238, 28),
                Size = new Size(100, 31),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(25, 118, 210),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnChangeHotkey.FlatAppearance.BorderSize = 0;
            _btnChangeHotkey.Click += OnToggleCaptureClick;

            _btnResetHotkey = new Button
            {
                Text = "الافتراضي (PgDn)",
                Location = new Point(344, 28),
                Size = new Size(100, 31),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(236, 240, 246),
                ForeColor = Color.FromArgb(40, 50, 65),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnResetHotkey.FlatAppearance.BorderColor = Color.FromArgb(200, 210, 224);
            _btnResetHotkey.Click += OnResetHotkeyClick;

            _lblHotkeyHint = new Label
            {
                Text = "اضغط «تغيير الزر» ثم اضغط أي زر مثل PgDn أو Pause أو F9 أو Ctrl+Shift+Space",
                Location = new Point(14, 68),
                Size = new Size(430, 28),
                Font = new Font("Segoe UI", 8.3f, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 100, 115)
            };

            grpHotkey.Controls.Add(_txtHotkey);
            grpHotkey.Controls.Add(_btnChangeHotkey);
            grpHotkey.Controls.Add(_btnResetHotkey);
            grpHotkey.Controls.Add(_lblHotkeyHint);
            Controls.Add(grpHotkey);

            // Group 2: Smart options
            GroupBox grpOptions = new GroupBox
            {
                Text = "خيارات ذكية",
                Location = new Point(16, 198),
                Size = new Size(458, 136),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            _chkSmartMixed = new CheckBox
            {
                Text = "التعرف الذكي للنص المختلط (إصلاح الكلام الغلط فقط وترك الكلمات الصحيحة)",
                Location = new Point(14, 28),
                Size = new Size(430, 24),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _chkPassThrough = new CheckBox
            {
                Text = "تمرير الزر طبيعياً عند عدم تحديد نص (لا يعطل التمرير بـ PgDn)",
                Location = new Point(14, 54),
                Size = new Size(430, 24),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _chkAutoSwitchLang = new CheckBox
            {
                Text = "تبديل لغة الكيبورد تلقائياً إلى اللغة الصحيحة بعد التحويل",
                Location = new Point(14, 80),
                Size = new Size(430, 24),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _chkStartWithWindows = new CheckBox
            {
                Text = "تشغيل الأداة تلقائياً في الخلفية مع بدء الويندوز",
                Location = new Point(14, 106),
                Size = new Size(430, 24),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            grpOptions.Controls.Add(_chkSmartMixed);
            grpOptions.Controls.Add(_chkPassThrough);
            grpOptions.Controls.Add(_chkAutoSwitchLang);
            grpOptions.Controls.Add(_chkStartWithWindows);
            Controls.Add(grpOptions);

            // Group 3: Live conversion tester
            GroupBox grpTest = new GroupBox
            {
                Text = "تجربة سريعة للتحويل",
                Location = new Point(16, 344),
                Size = new Size(458, 114),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            _txtTestInput = new TextBox
            {
                Location = new Point(14, 28),
                Size = new Size(340, 48),
                Multiline = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Text = "اثممخ صخقمي"
            };

            Button btnConvertNow = new Button
            {
                Text = "تحويل الآن",
                Location = new Point(362, 28),
                Size = new Size(82, 48),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnConvertNow.FlatAppearance.BorderSize = 0;
            btnConvertNow.Click += OnTestConvertClick;

            _lblTestStatus = new Label
            {
                Text = "جرّب كتابة: اثممخ صخقمي  أو  hgsghl ugd;l  ثم اضغط «تحويل الآن»",
                Location = new Point(14, 82),
                Size = new Size(430, 22),
                Font = new Font("Segoe UI", 8.3f, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 100, 115)
            };

            grpTest.Controls.Add(_txtTestInput);
            grpTest.Controls.Add(btnConvertNow);
            grpTest.Controls.Add(_lblTestStatus);
            Controls.Add(grpTest);

            // Footer buttons
            Button btnSave = new Button
            {
                Text = "حفظ وتطبيق",
                Location = new Point(16, 470),
                Size = new Size(125, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(21, 101, 192),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += OnSaveClick;

            Button btnCancel = new Button
            {
                Text = "إلغاء",
                Location = new Point(150, 470),
                Size = new Size(90, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(236, 240, 246),
                ForeColor = Color.FromArgb(40, 50, 65),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 210, 224);
            btnCancel.Click += delegate
            {
                StopCapture();
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(btnSave);
            Controls.Add(btnCancel);

            KeyDown += OnFormKeyDown;
            FormClosing += delegate { StopCapture(); };
        }

        private void SyncUiFromSettings()
        {
            _txtHotkey.Text = AppSettings.FormatHotkey(_workingSettings.HotkeyModifiers, _workingSettings.GetKeys());
            _chkSmartMixed.Checked = _workingSettings.SmartMixedMode;
            _chkPassThrough.Checked = _workingSettings.PassThroughWhenNoSelection;
            _chkAutoSwitchLang.Checked = _workingSettings.AutoSwitchKeyboardLayout;
            _chkStartWithWindows.Checked = _workingSettings.StartWithWindows;
        }

        private void OnToggleCaptureClick(object sender, EventArgs e)
        {
            if (_capturingHotkey)
            {
                StopCapture();
                SyncUiFromSettings();
            }
            else
            {
                StartCapture();
            }
        }

        private void StartCapture()
        {
            _capturingHotkey = true;
            if (_onSuspendHotkey != null) _onSuspendHotkey();

            _txtHotkey.Text = "اضغط الزر المطلوب الآن...";
            _txtHotkey.BackColor = Color.FromArgb(255, 243, 205);
            _btnChangeHotkey.Text = "إلغاء الالتقاط";
            _btnChangeHotkey.BackColor = Color.FromArgb(211, 47, 47);
            _lblHotkeyHint.Text = "اضغط أي زر (مثل PgDn أو Pause أو F9) أو اختصار (مثل Ctrl+Shift+Space) — Esc للإلغاء";
            _lblHotkeyHint.ForeColor = Color.FromArgb(165, 90, 0);
        }

        private void StopCapture()
        {
            if (!_capturingHotkey) return;
            _capturingHotkey = false;
            if (_onResumeHotkey != null) _onResumeHotkey();

            _txtHotkey.BackColor = Color.FromArgb(240, 244, 250);
            _btnChangeHotkey.Text = "تغيير الزر";
            _btnChangeHotkey.BackColor = Color.FromArgb(25, 118, 210);
            _lblHotkeyHint.ForeColor = Color.FromArgb(90, 100, 115);
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturingHotkey) return;

            e.SuppressKeyPress = true;
            e.Handled = true;

            Keys keyCode = e.KeyCode;
            if (keyCode == Keys.Escape)
            {
                StopCapture();
                SyncUiFromSettings();
                _lblHotkeyHint.Text = "تم إلغاء تغيير الزر.";
                return;
            }

            // Wait until the user presses a non-modifier key
            if (keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu ||
                keyCode == Keys.LWin || keyCode == Keys.RWin)
            {
                return;
            }

            uint mods = AppSettings.MOD_NONE;
            if (e.Control) mods |= AppSettings.MOD_CONTROL;
            if (e.Alt) mods |= AppSettings.MOD_ALT;
            if (e.Shift) mods |= AppSettings.MOD_SHIFT;

            if (!AppSettings.IsAllowedHotkey(mods, keyCode))
            {
                _lblHotkeyHint.Text = "لا يمكن تعيين الحروف أو المسافة بمفردها حتى لا تتعطل الكتابة؛ استخدم Ctrl/Alt معها أو اختر PgDn / Pause / F1-F12.";
                _lblHotkeyHint.ForeColor = Color.FromArgb(198, 40, 40);
                return;
            }

            _workingSettings.SetHotkey(mods, keyCode);
            StopCapture();
            _txtHotkey.Text = AppSettings.FormatHotkey(_workingSettings.HotkeyModifiers, _workingSettings.GetKeys());
            _lblHotkeyHint.Text = "تم اختيار الاختصار الجديد: " + _txtHotkey.Text + " (اضغط «حفظ وتطبيق» لتفعيله)";
            _lblHotkeyHint.ForeColor = Color.FromArgb(46, 125, 50);
        }

        private void OnResetHotkeyClick(object sender, EventArgs e)
        {
            StopCapture();
            _workingSettings.SetHotkey(AppSettings.MOD_NONE, Keys.Next);
            _txtHotkey.Text = AppSettings.FormatHotkey(_workingSettings.HotkeyModifiers, _workingSettings.GetKeys());
            _lblHotkeyHint.Text = "تمت الاستعادة إلى الافتراضي: PgDn (PageDown)";
            _lblHotkeyHint.ForeColor = Color.FromArgb(46, 125, 50);
        }

        private void OnTestConvertClick(object sender, EventArgs e)
        {
            ConversionResult res = LayoutConverter.ConvertSmart(_txtTestInput.Text, _chkSmartMixed.Checked);
            _txtTestInput.Text = res.ConvertedText;
            if (!res.Changed)
            {
                _lblTestStatus.Text = "لا يوجد نص قابل للتحويل.";
                _lblTestStatus.ForeColor = Color.FromArgb(120, 120, 120);
            }
            else if (res.Direction == ConversionDirection.ArabicToEnglish)
            {
                _lblTestStatus.Text = "تم التحويل تلقائياً: عربي غير مفهوم ← إنجليزي";
                _lblTestStatus.ForeColor = Color.FromArgb(21, 101, 192);
            }
            else if (res.Direction == ConversionDirection.EnglishToArabic)
            {
                _lblTestStatus.Text = "تم التحويل تلقائياً: إنجليزي غير مفهوم ← عربي";
                _lblTestStatus.ForeColor = Color.FromArgb(46, 125, 50);
            }
            else
            {
                _lblTestStatus.Text = "تم الإصلاح الذكي للنص المختلط بنجاح!";
                _lblTestStatus.ForeColor = Color.FromArgb(106, 27, 154);
            }
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            StopCapture();
            _workingSettings.SmartMixedMode = _chkSmartMixed.Checked;
            _workingSettings.PassThroughWhenNoSelection = _chkPassThrough.Checked;
            _workingSettings.AutoSwitchKeyboardLayout = _chkAutoSwitchLang.Checked;
            _workingSettings.StartWithWindows = _chkStartWithWindows.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
