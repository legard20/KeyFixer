using System;
using System.Collections.Generic;
using KeyFixer;

public static class ConverterTests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void AssertEqual(string name, string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            _passed++;
            Console.WriteLine("[PASS] " + name);
        }
        else
        {
            _failed++;
            Console.WriteLine("[FAIL] " + name);
            Console.WriteLine("  Expected: " + expected);
            Console.WriteLine("  Actual  : " + actual);
        }
    }

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

    public static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 1. Arabic-layout gibberish -> English
        AssertEqual("Ar->En: hello world",
            "hello world",
            LayoutConverter.ConvertSmart("اثممخ صخقمي", true).ConvertedText);

        AssertEqual("Ar->En: Capitalized Hello World!",
            "Hello World!",
            LayoutConverter.ConvertSmart("أثممخ ًخقمي!", true).ConvertedText);

        AssertEqual("Ar->En: Capitalized Shift symbols (File Data Love Code Video Zoom)",
            "File Data Love Code Video Zoom",
            LayoutConverter.ConvertSmart("[همث ]شفش /خرث }خيث {هيثخ ~خخة", true).ConvertedText);

        AssertEqual("Ar->En: Ligatures b/B/G/T (book Big Good Time)",
            "book Big Good Time",
            LayoutConverter.ConvertSmart("لاخخن لآهل لأخخي لإهةث", true).ConvertedText);

        AssertEqual("Ar->En: Context-aware gh vs b (right night thought enough high bright github about)",
            "right night thought enough high bright github about",
            LayoutConverter.ConvertSmart("قهلاف ىهلاف فاخعلاف ثىخعلا اهلا لاقهلاف لهفاعلا شلاخعف", true).ConvertedText);

        AssertEqual("Ar->En: Exact 'right' (قهلاف)",
            "right",
            LayoutConverter.ConvertSmart("قهلاف", true).ConvertedText);

        AssertEqual("Ar->En: Punctuation (hello, how are you?)",
            "hello, how are you?",
            LayoutConverter.ConvertSmart("اثممخو اخص شقث غخع؟", true).ConvertedText);

        // 2. English-layout gibberish -> Arabic
        AssertEqual("En->Ar: السلام عليكم ورحمة الله وبركاته",
            "السلام عليكم ورحمة الله وبركاته",
            LayoutConverter.ConvertSmart("hgsghl ugd;l ,vplm hggi ,fv;hji", true).ConvertedText);

        AssertEqual("En->Ar: يوسف (d,st)",
            "يوسف",
            LayoutConverter.ConvertSmart("d,st", true).ConvertedText);

        AssertEqual("En->Ar: أحمد (Hpl])",
            "أحمد",
            LayoutConverter.ConvertSmart("Hpl]", true).ConvertedText);

        AssertEqual("En->Ar: لأن وإلا وآمين (Gk ,Yb ,Nldk)",
            "لأن وإلا وآمين",
            LayoutConverter.ConvertSmart("Gk ,Yb ,Nldk", true).ConvertedText);

        AssertEqual("En->Ar: CapsLock accidental (HGSGHL UGD;L)",
            "السلام عليكم",
            LayoutConverter.ConvertSmart("HGSGHL UGD;L", true).ConvertedText);

        AssertEqual("En->Ar: Numbers with decimals preserved (hgsuf 12.5 [kdi)",
            "السعف 12.5 جنيه",
            LayoutConverter.ConvertSmart("hgsut 12.5 [kdi", true).ConvertedText);

        // 3. Round-trip tests (En -> Ar -> En and Ar -> En -> Ar)
        string sampleEn = "Good Morning! Check File Data, Code & Video at 10.5.";
        string typedInAr = LayoutConverter.ConvertEnglishToArabic(sampleEn);
        string recoveredEn = LayoutConverter.ConvertArabicToEnglish(typedInAr);
        AssertEqual("Round-trip En->Ar->En full sentence", sampleEn, recoveredEn);

        string sampleAr = "السلام عليكم يا يوسف، ازيك النهاردة؟";
        string typedInEn = LayoutConverter.ConvertArabicToEnglish(sampleAr);
        string recoveredAr = LayoutConverter.ConvertEnglishToArabic(typedInEn);
        AssertEqual("Round-trip Ar->En->Ar full sentence", sampleAr, recoveredAr);

        // 4. Smart Mixed-Text Detection (only fix gibberish, keep valid words!)
        AssertEqual("Mixed smart: Arabic + English-gibberish -> Arabic",
            "انا نسيت اغير اللغة وكتبت السلام عليكم",
            LayoutConverter.ConvertSmart("انا نسيت اغير اللغة وكتبت hgsghl ugd;l", true).ConvertedText);

        AssertEqual("Mixed smart: English + Arabic-gibberish -> English",
            "I forgot to switch and typed hello world",
            LayoutConverter.ConvertSmart("I forgot to switch and typed اثممخ صخقمي", true).ConvertedText);

        AssertEqual("Mixed smart: Preserves URLs and emails",
            "زور موقع https://example.com او ابعت على user@test.com السلام عليكم",
            LayoutConverter.ConvertSmart("زور موقع https://example.com او ابعت على user@test.com hgsghl ugd;l", true).ConvertedText);

        AssertEqual("Mixed smart: Egyptian Arabic + English gibberish paragraph",
            "يا يوسف انا كنت بكتب رسالة طويلة شوية بالانجليزي",
            LayoutConverter.ConvertSmart("يا يوسف انا كنت بكتب vshgm ',dgm a,dm fhghk[gd.d", true).ConvertedText);

        AssertEqual("Mixed smart: English sentence + Arabic gibberish phrase",
            "Please send me the computer password right now",
            LayoutConverter.ConvertSmart("Please send me the ؤخةحعفثق حشسسصخقي قهلاف ىخص", true).ConvertedText);

        AssertEqual("Long Arabic-layout gibberish paragraph -> English",
            "this is a long sentence typed by mistake while the keyboard was in arabic mode",
            LayoutConverter.ConvertSmart("فاهس هس ش مخىل سثىفثىؤث فغحثي لاغ ةهسفشنث صاهمث فاث نثغلاخشقي صشس هى شقشلاهؤ ةخيث", true).ConvertedText);

        // 5. Edge cases: empty, whitespace, multiline
        AssertEqual("Edge: empty string", "", LayoutConverter.ConvertSmart("", true).ConvertedText);
        AssertEqual("Edge: whitespace only", "   \r\n\t ", LayoutConverter.ConvertSmart("   \r\n\t ", true).ConvertedText);
        AssertEqual("Edge: multiline preserved",
            "hello\r\nworld",
            LayoutConverter.ConvertSmart("اثممخ\r\nصخقمي", true).ConvertedText);

        // 6. Direction metadata check
        ConversionResult dir1 = LayoutConverter.ConvertSmart("اثممخ صخقمي", true);
        AssertTrue("Direction Ar->En target is English",
            dir1.Direction == ConversionDirection.ArabicToEnglish && dir1.TargetLanguage == TargetLanguage.English,
            dir1.Direction + "/" + dir1.TargetLanguage);

        ConversionResult dir2 = LayoutConverter.ConvertSmart("hgsghl ugd;l", true);
        AssertTrue("Direction En->Ar target is Arabic",
            dir2.Direction == ConversionDirection.EnglishToArabic && dir2.TargetLanguage == TargetLanguage.Arabic,
            dir2.Direction + "/" + dir2.TargetLanguage);

        Console.WriteLine(string.Format("\nSummary: {0} passed, {1} failed.", _passed, _failed));
        return _failed == 0 ? 0 : 1;
    }
}
