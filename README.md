# KeyFixer — Arabic ⇄ English Keyboard Layout Fixer (v1.1.0)

Lightweight open-source Windows background tray utility built with C# / `.NET Framework 4.8` (~57 KB standalone `.exe`, zero external dependencies) — automatically detects and fixes text typed in the wrong Arabic (101) / US English (QWERTY) keyboard layout with a single customizable hotkey.

## English

### Features
- **Automatic Bidirectional Conversion (Arabic ⇄ English):**
  - Typed English while the keyboard was on Arabic (e.g. `اثممخ صخقمي`) → instantly converts to `hello world`.
  - Typed Arabic while the keyboard was on English (e.g. `hgsghl ugd;l`) → instantly converts to `السلام عليكم`.
- **Smart Mixed-Text Gibberish Detection:**
  - In mixed sentences (e.g. `انا كنت بكتب hgsghl ugd;l` or `Please send me ؤخةحعفثق حشسسصخقي`), KeyFixer detects consecutive wrongly-typed segments and fixes only the gibberish while leaving valid words, URLs, and email addresses untouched.
- **Full Windows Arabic (101) Layout Accuracy:**
  - Exact 1-to-1 mapping for all lowercase and `Shift`-uppercase keys, 2-character ligatures (`لا`, `لأ`, `لإ`, `لآ`), context-aware `لا` disambiguation (`b` vs `gh` in words like `book`, `right`, `thought`, `enough`, `high`), and accidental `CapsLock` recovery (`HGSGHL UGD;L` → `السلام عليكم`).
- **Customizable Global Hotkey (Default: `PgDn` / `PageDown`):**
  - Select the wrongly-typed text in any Windows application (browsers, WhatsApp, Telegram, Word, Notepad, VS Code) and press `PgDn`.
  - Customize the hotkey anytime from the Settings dialog (`PgDn`, `Pause`, `F9`, or combinations like `Ctrl + Shift + Space`).
  - **Smart Key Pass-Through:** Pressing `PgDn` with no text selected passes the key through normally so page scrolling keeps working.
- **Non-Intrusive Background Tray App:**
  - Preserves and restores your previous clipboard contents automatically.
  - Optional automatic Windows keyboard layout switching after conversion.
  - Optional **Start with Windows** toggle (`HKCU\...\Run`).
  - Settings persist in `%APPDATA%\KeyFixer\settings.json`.

### Usage
1. Download **`KeyFixer.exe`** (or **`KeyFixer-v1.0.0.zip`**) from the [Releases](../../releases) page.
2. Double-click `KeyFixer.exe` — it runs silently in the background with a blue `Aع` icon in the Windows System Tray.
3. Highlight any wrongly-typed text and press **`PgDn`** (`PageDown`).
4. Double-click the tray icon (or right-click → **الإعدادات وتخصيص الزر...**) to customize your hotkey, toggle options, or test conversions.

> **Note:** If you type inside an application running as Administrator, run `KeyFixer.exe` as Administrator as well so Windows UIPI permits text replacement inside elevated windows.

### Building from Source & Running Tests
No SDK or external packages required — compiles directly with the built-in Windows `.NET Framework 4.8` compiler (`csc.exe`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build.ps1" -RunTests
```

### What's Fixed in v1.1.0
- Fixed mixed Arabic and English sentences so valid words are not broken or reordered.
- Fixed URLs, emails, domains, file paths, IPs, and `@handles` so they stay untouched.
- Fixed PgDn and other navigation keys so scrolling works normally and keys are never blocked.
- Added a focus check before pasting so text is never pasted into the wrong window.
- Added a safer wait before restoring the clipboard so slow apps paste the right text.
- Fixed small cleanup and safety bugs (see `CHANGELOG.md`).

---

## العربية

أداة ويندوز خفيفة جداً ومفتوحة المصدر (~57 كيلوبايت فقط، ملف `KeyFixer.exe` واحد بدون أي متطلبات خارجية) تعمل في الخلفية بجانب الساعة (System Tray) للتعرف التلقائي على الكلام المكتوب بلغة الكيبورد الخاطئة (عربي ⇄ إنجليزي) وتصحيحه فوراً بضغطة زر واحدة.

### المميزات
- **تحويل تلقائي ذكي بالاتجاهين:**
  - لو كتبت كلام إنجليزي والكيبورد عربي (مثل `اثممخ صخقمي`) ← يتحول فوراً إلى `hello world`.
  - لو كتبت كلام عربي والكيبورد إنجليزي (مثل `hgsghl ugd;l`) ← يتحول فوراً إلى `السلام عليكم`.
- **التعرف الذكي للنص المختلط:** لو حددت جملة فيها كلام صحيح مع كلام غير مفهوم (مثل `انا كنت بكتب hgsghl ugd;l`)، تقوم الأداة بإصلاح الكلام الخاطئ فقط وتترك الكلمات الصحيحة والروابط والإيميلات كما هي.
- **دقة كاملة لكيبورد ويندوز العربي (101):**
  - يدعم الحروف الكبيرة (`Shift`)، والاختصارات الثنائية (`لا`، `لأ`، `لإ`، `لآ`)، وتمييز `لا` بين `b` و `gh` حسب السياق (`book` مقابل `right` و `thought`)، ومعالجة `CapsLock` بالخطأ.
- **اختصار افتراضي `PgDn (PageDown)` قابل للتخصيص:**
  - حدّد النص الخاطئ في أي برنامج (متصفح، واتساب، تيليجرام، وورد، نوتباد...) واضغط `PgDn`.
  - يمكنك تغيير الزر من نافذة الإعدادات إلى أي زر آخر (`Pause`، `F9`، `PgUp`، أو اختصار مثل `Ctrl + Shift + Space`).
- **لا يعطل زر `PgDn` العادي:** عند الضغط على `PgDn` بدون تحديد نص، يتم تمرير الزر طبيعياً للتمرير في الصفحات.
- **يحافظ على الحافظة (Clipboard):** يعيد محتوى النسخ السابق بعد التحويل تلقائياً.
- **تبديل لغة الكيبورد تلقائياً (اختياري)** + **خيار التشغيل التلقائي مع بدء الويندوز**.

### طريقة التشغيل والاستخدام
1. حمّل ملف **`KeyFixer.exe`** أو الأرشيف **`KeyFixer-v1.0.0.zip`** من صفحة [الإصدارات (Releases)](../../releases).
2. شغّل `KeyFixer.exe` — سيعمل فوراً في الخلفية وتظهر أيقونته الزرقاء `Aع` بجانب الساعة.
3. حدّد أي نص مكتوب بالغلط واضغط **`PgDn`**.
4. اضغط **مرتين** على الأيقونة بجانب الساعة (أو كليك يمين ← **الإعدادات وتخصيص الزر...**) لتغيير الزر أو ضبط الإعدادات.

> **ملاحظة:** تُحفظ الإعدادات تلقائياً في `%APPDATA%\KeyFixer\settings.json`. وإذا كنت تستخدم برامج بصلاحيات المدير (Run as Administrator)، شغّل `KeyFixer.exe` كمدير أيضاً.

### بناء ملف التشغيل (EXE) وتشغيل الاختبارات
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build.ps1" -RunTests
```
