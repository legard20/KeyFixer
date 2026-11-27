using System;
using System.Collections.Generic;
using System.Text;

namespace KeyFixer
{
    public enum ConversionDirection
    {
        None = 0,
        ArabicToEnglish = 1,
        EnglishToArabic = 2,
        MixedSmart = 3
    }

    public enum TargetLanguage
    {
        None = 0,
        English = 1,
        Arabic = 2
    }

    public sealed class ConversionResult
    {
        public string OriginalText { get; set; }
        public string ConvertedText { get; set; }
        public ConversionDirection Direction { get; set; }
        public TargetLanguage TargetLanguage { get; set; }
        public bool Changed
        {
            get { return !string.Equals(OriginalText, ConvertedText, StringComparison.Ordinal); }
        }
    }

    /// <summary>
    /// Bidirectional Windows Arabic (101) <-> US English (QWERTY) keyboard layout converter
    /// with automatic direction detection and mixed-text gibberish recognition.
    /// </summary>
    public static class LayoutConverter
    {
        private static readonly Dictionary<char, string> EnToAr = new Dictionary<char, string>
        {
            // Number row
            { '`', "\u0630" }, // ذ
            { '~', "\u0651" }, // ّ (Shadda)
            { '(', ")" },
            { ')', "(" },

            // Top letter row
            { 'q', "\u0636" }, // ض
            { 'Q', "\u064E" }, // َ (Fatha)
            { 'w', "\u0635" }, // ص
            { 'W', "\u064B" }, // ً (Fathatan)
            { 'e', "\u062B" }, // ث
            { 'E', "\u064F" }, // ُ (Damma)
            { 'r', "\u0642" }, // ق
            { 'R', "\u064C" }, // ٌ (Dammatan)
            { 't', "\u0641" }, // ف
            { 'T', "\u0644\u0625" }, // لإ
            { 'y', "\u063A" }, // غ
            { 'Y', "\u0625" }, // إ
            { 'u', "\u0639" }, // ع
            { 'U', "\u2018" }, // ‘
            { 'i', "\u0647" }, // ه
            { 'I', "\u00F7" }, // ÷
            { 'o', "\u062E" }, // خ
            { 'O', "\u00D7" }, // ×
            { 'p', "\u062D" }, // ح
            { 'P', "\u061B" }, // ؛
            { '[', "\u062C" }, // ج
            { '{', "<" },
            { ']', "\u062F" }, // د
            { '}', ">" },

            // Home row
            { 'a', "\u0634" }, // ش
            { 'A', "\u0650" }, // ِ (Kasra)
            { 's', "\u0633" }, // س
            { 'S', "\u064D" }, // ٍ (Kasratan)
            { 'd', "\u064A" }, // ي
            { 'D', "]" },
            { 'f', "\u0628" }, // ب
            { 'F', "[" },
            { 'g', "\u0644" }, // ل
            { 'G', "\u0644\u0623" }, // لأ
            { 'h', "\u0627" }, // ا
            { 'H', "\u0623" }, // أ
            { 'j', "\u062A" }, // ت
            { 'J', "\u0640" }, // ـ (Tatweel)
            { 'k', "\u0646" }, // ن
            { 'K', "\u060C" }, // ، (Arabic comma)
            { 'l', "\u0645" }, // م
            { 'L', "/" },
            { ';', "\u0643" }, // ك
            { '\'', "\u0637" }, // ط

            // Bottom row
            { 'z', "\u0626" }, // ئ
            { 'Z', "~" },
            { 'x', "\u0621" }, // ء
            { 'X', "\u0652" }, // ْ (Sukun)
            { 'c', "\u0624" }, // ؤ
            { 'C', "}" },
            { 'v', "\u0631" }, // ر
            { 'V', "{" },
            { 'b', "\u0644\u0627" }, // لا
            { 'B', "\u0644\u0622" }, // لآ
            { 'n', "\u0649" }, // ى
            { 'N', "\u0622" }, // آ
            { 'm', "\u0629" }, // ة
            { 'M', "\u2019" }, // ’
            { ',', "\u0648" }, // و
            { '<', "," },
            { '.', "\u0632" }, // ز
            { '>', "." },
            { '/', "\u0638" }, // ظ
            { '?', "\u061F" }  // ؟
        };

        private static readonly Dictionary<char, char> ArToEn = new Dictionary<char, char>
        {
            // Number row
            { '\u0630', '`' }, // ذ
            { '\u0651', '~' }, // ّ
            { ')', '(' },
            { '(', ')' },

            // Top letter row
            { '\u0636', 'q' }, // ض
            { '\u064E', 'Q' }, // َ
            { '\u0635', 'w' }, // ص
            { '\u064B', 'W' }, // ً
            { '\u062B', 'e' }, // ث
            { '\u064F', 'E' }, // ُ
            { '\u0642', 'r' }, // ق
            { '\u064C', 'R' }, // ٌ
            { '\u0641', 't' }, // ف
            { '\u063A', 'y' }, // غ
            { '\u0625', 'Y' }, // إ
            { '\u0639', 'u' }, // ع
            { '\u2018', 'U' }, // ‘
            { '\u0647', 'i' }, // ه
            { '\u00F7', 'I' }, // ÷
            { '\u062E', 'o' }, // خ
            { '\u00D7', 'O' }, // ×
            { '\u062D', 'p' }, // ح
            { '\u061B', 'P' }, // ؛
            { '\u062C', '[' }, // ج
            { '<', '{' },
            { '\u062F', ']' }, // د
            { '>', '}' },

            // Home row
            { '\u0634', 'a' }, // ش
            { '\u0650', 'A' }, // ِ
            { '\u0633', 's' }, // س
            { '\u064D', 'S' }, // ٍ
            { '\u064A', 'd' }, // ي
            { ']', 'D' },
            { '\u0628', 'f' }, // ب
            { '[', 'F' },
            { '\u0644', 'g' }, // ل
            { '\u0627', 'h' }, // ا
            { '\u0623', 'H' }, // أ
            { '\u062A', 'j' }, // ت
            { '\u0640', 'J' }, // ـ
            { '\u0646', 'k' }, // ن
            { '\u060C', 'K' }, // ،
            { '\u0645', 'l' }, // م
            { '/', 'L' },
            { '\u0643', ';' }, // ك
            { '\u0637', '\'' }, // ط

            // Bottom row
            { '\u0626', 'z' }, // ئ
            { '~', 'Z' },
            { '\u0621', 'x' }, // ء
            { '\u0652', 'X' }, // ْ
            { '\u0624', 'c' }, // ؤ
            { '}', 'C' },
            { '\u0631', 'v' }, // ر
            { '{', 'V' },
            { '\u0649', 'n' }, // ى
            { '\u0622', 'N' }, // آ
            { '\u0629', 'm' }, // ة
            { '\u2019', 'M' }, // ’
            { '\u0648', ',' }, // و
            { ',', '<' },
            { '\u0632', '.' }, // ز
            { '.', '>' },
            { '\u0638', '/' }, // ظ
            { '\u061F', '?' }  // ؟
        };

        private static readonly HashSet<string> CommonEnglishBigrams = new HashSet<string>
        {
            "th","he","in","er","an","re","on","at","en","nd","ti","es","or","te","of","ed",
            "is","it","al","ar","st","to","nt","ng","se","ha","as","ou","io","le","ve","co",
            "me","de","hi","ri","ro","ic","ne","ea","ra","ce","li","ch","ll","be","ma","si",
            "om","ur","el","lo","wo","rl","ld","pr","tr","sh","wh","gr","pl","fr","cr","dr",
            "br","sp","cl","fl","gl","sl","sw","qu","ck","gh","ph","ee","oo","ss","tt","ff",
            "pp","rr","mm","nn","dd","gg","bb","cc","ay","ey","oy","ow","aw","ew","ai","ei",
            "oi","ui","ie","ue","oa","au","eu","ca","la","ta","da","pa","wa","go","do","no",
            "so","bo","fo","mo","po","ho","yo","my","by","ty","ry","ly","ke","ge","pe","fe",
            "we","ye","ze","ki","mi","ni","pi","wi","di","fi","gi","vi","bi","su","bu","cu",
            "du","fu","gu","hu","ju","lu","mu","nu","pu","ru","tu","up","us","un","um","ul",
            "ut","ac","ad","af","ag","ak","am","ap","av","ab","ec","ef","eg","ek","em","ep",
            "et","ev","ex","ib","id","if","ig","ik","il","im","ip","ir","iv","ob","oc","od",
            "og","ok","ol","op","os","ot","ov","ox","ck","nk","sk","rk","lk","mp","mb","ct",
            "ft","lt","pt","rt","xt","ns","rs","ls","ts","ds","gs","ks","ms","ps","ws","ys"
        };

        private static readonly HashSet<string> RareImpossibleEnglishBigrams = new HashSet<string>
        {
            "hf","uj","fj","jf","kj","jk","qj","vj","xj","zj","cj","dj","gj","hj","lj","mj","nj","pj","rj","sj","tj","wj","yj",
            "dh","fh","jh","kh","lh","mh","nh","qh","vh","zh","hg","sg","dg","fg","kg","mg","pg","tg","vg","wg","zg",
            "bk","dk","fk","gk","hk","mk","pk","qk","tk","vk","wk","xk","zk",
            "bv","cv","dv","fv","gv","hv","jv","kv","lv","mv","nv","pv","qv","sv","tv","wv","xv","zv",
            "bx","cx","dx","fx","gx","hx","jx","kx","lx","mx","nx","px","qx","rx","sx","tx","vx","wx","zx",
            "bq","cq","dq","fq","gq","hq","jq","kq","lq","mq","nq","pq","rq","sq","tq","vq","wq","xq","yq","zq",
            "aj","ej","ij","oj","jb","jc","jd","jg","jh","jl","jm","jn","jp","jq","jr","js","jt","jv","jw","jx","jy","jz",
            "qb","qc","qd","qf","qg","qh","qk","ql","qm","qn","qp","qr","qs","qt","qv","qw","qx","qy","qz",
            "vb","vc","vd","vf","vg","vh","vk","vl","vm","vn","vp","vq","vr","vs","vt","vw","vx","vz",
            "zb","zc","zd","zf","zg","zh","zk","zm","zn","zp","zq","zr","zs","zt","zv","zw","zx",
            "fw","gw","hw","jw","kw","mw","pw","qw","vw","xw","zw","wf","wg","wj","wk","wm","wp","wq","wv","wx","wz"
        };

        private static readonly HashSet<string> CommonEnglishShortWords = new HashSet<string>
        {
            "a","i","am","an","as","at","be","by","do","go","he","hi","if","in","is","it",
            "me","my","no","of","ok","on","or","so","to","up","us","we","all","and","any",
            "are","bad","big","boy","but","bye","can","car","cat","day","did","dog","end",
            "far","few","for","fun","get","god","got","had","has","her","hey","him","his",
            "hot","how","its","job","key","let","lot","man","may","men","new","not","now",
            "off","old","one","our","out","own","pay","put","ran","red","run","sad","sat",
            "saw","say","see","set","she","sir","sit","six","son","sun","ten","the","too",
            "top","try","two","use","war","was","way","who","why","win","yes","yet","you"
        };

        private static readonly HashSet<string> CommonArabicBigrams = new HashSet<string>
        {
            "ال","لا","ان","من","في","عل","لي","ما","ها","وا","نا","ين","ون","ات","ية","هذ",
            "كا","عم","لل","با","تا","را","سا","عا","فا","قا","مح","مس","مع","نت","هم","كم",
            "لم","يا","يك","لو","دي","ده","بس","مش","زي","كل","بع","قد","عن","إل","أو","أن",
            "إن","ثم","حي","صب","خي","سل","حم","رح","بر","شك","وس","يو","يف","مو","نو","هو",
            "بي","تي","سي","عي","في","قي","كي","مي","ني","هي","وي","ري","ار","ور","ير","تر",
            "مر","كر","فر","قر","سر","شر","حر","جر","در","بر","عر","غر","ضر","صر","طر","ظر",
            "ام","يم","تم","سم","حم","جم","دم","رم","عم","غم","قم","كم","نم","وم","كت","تب",
            "بش","شغ","غل","حب","بن","نت","تغ","غي","ير","عا","يز","زك","كذ","دا","اح","حد",
            "هل","اه","لا","جد","دا","تم","ام","صا","اح","حس","سن","يم","كن","نت","تع","مل"
        };

        private static readonly HashSet<string> CommonArabicShortWords = new HashSet<string>
        {
            "في","من","على","علي","الى","إلى","عن","مع","هذا","هذه","كان","انا","أنا","انت",
            "أنت","هو","هي","هم","نحن","ما","لا","لم","لن","قد","كل","بس","مش","ده","دي",
            "كده","زي","عشان","عايز","جدا","او","أو","ثم","ان","أن","إن","الله","السلام",
            "عليكم","مرحبا","شكرا","اهلا","أهلا","كيف","اليوم","مصر","خير","اي","أي","لو",
            "يا","ده","ليه","فين","امتى","هنا","هناك","بعد","قبل","تحت","فوق","بين","عند",
            "انتا","احنا","هم","انتو","عادي","تمام","صح","غلط","كويس","حلو","ممكن","لازم"
        };

        /// <summary>
        /// Automatically detects direction and converts mistyped Arabic/English text.
        /// For pure single-script text, always flips the whole text deterministically.
        /// For mixed Arabic + English text (when smartMixedMode is true), detects and fixes
        /// only the meaningless/gibberish segments while leaving real words intact.
        /// </summary>
        public static ConversionResult ConvertSmart(string text, bool smartMixedMode)
        {
            ConversionResult result = new ConversionResult
            {
                OriginalText = text ?? string.Empty,
                ConvertedText = text ?? string.Empty,
                Direction = ConversionDirection.None,
                TargetLanguage = TargetLanguage.None
            };

            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            List<Token> tokens = Tokenize(text);
            int arabicWordTokens = 0;
            int latinWordTokens = 0;
            int mixedWordTokens = 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                Token t = tokens[i];
                if (t.IsWhitespace) continue;
                if (IsProtectedToken(t.Text))
                {
                    t.IsProtected = true;
                    t.Script = ScriptKind.Neutral;
                    continue;
                }
                ScriptKind kind = ClassifyToken(t.Text);
                t.Script = kind;
                if (kind == ScriptKind.Arabic) arabicWordTokens++;
                else if (kind == ScriptKind.Latin) latinWordTokens++;
                else if (kind == ScriptKind.Mixed) mixedWordTokens++;
            }

            // Case 1: Pure Arabic-script selection (excluding protected URLs/emails/paths/numbers) -> English
            if (arabicWordTokens > 0 && latinWordTokens == 0 && mixedWordTokens == 0)
            {
                result.Direction = ConversionDirection.ArabicToEnglish;
                result.TargetLanguage = TargetLanguage.English;
                result.ConvertedText = ConvertArabicToEnglish(text);
                return result;
            }

            // Case 2: Pure Latin-script selection (excluding protected URLs/emails/paths/numbers) -> Arabic
            if (latinWordTokens > 0 && arabicWordTokens == 0 && mixedWordTokens == 0)
            {
                result.Direction = ConversionDirection.EnglishToArabic;
                result.TargetLanguage = TargetLanguage.Arabic;
                result.ConvertedText = ConvertEnglishToArabic(text);
                return result;
            }

            // Case 3: No clear letters (e.g., pure punctuation like "؛،؟" or ";,./[]'", or only protected tokens)
            if (arabicWordTokens == 0 && latinWordTokens == 0 && mixedWordTokens == 0)
            {
                // If every non-whitespace token is protected (e.g. a lone URL or email), leave untouched
                bool hasUnprotectedNonWs = false;
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (!tokens[i].IsWhitespace && !tokens[i].IsProtected)
                    {
                        hasUnprotectedNonWs = true;
                        break;
                    }
                }
                if (!hasUnprotectedNonWs)
                {
                    return result;
                }

                int arChars = CountArabicMappableChars(text);
                int enChars = CountEnglishMappableChars(text);
                if (arChars > enChars)
                {
                    result.Direction = ConversionDirection.ArabicToEnglish;
                    result.TargetLanguage = TargetLanguage.English;
                    result.ConvertedText = ConvertArabicToEnglish(text);
                }
                else if (enChars > 0)
                {
                    result.Direction = ConversionDirection.EnglishToArabic;
                    result.TargetLanguage = TargetLanguage.Arabic;
                    result.ConvertedText = ConvertEnglishToArabic(text);
                }
                return result;
            }

            // Case 4: Truly mixed text (contains BOTH Arabic-script and Latin-script tokens, or mixed-script tokens)
            // Group contiguous non-protected tokens of the same script into runs (phrases)
            // so even short/ambiguous words inside a mistyped phrase convert cleanly together,
            // while valid Arabic and valid English words stay intact and in exact original order.
            if (smartMixedMode)
            {
                StringBuilder sb = new StringBuilder(text.Length);
                int convertedToEn = 0;
                int convertedToAr = 0;
                int idx = 0;

                while (idx < tokens.Count)
                {
                    Token t = tokens[idx];
                    if (t.IsWhitespace || t.IsProtected || t.Script == ScriptKind.Neutral)
                    {
                        sb.Append(t.Text);
                        idx++;
                        continue;
                    }

                    if (t.Script == ScriptKind.Mixed)
                    {
                        int subEn, subAr;
                        sb.Append(ConvertMixedTokenSmart(t.Text, out subEn, out subAr));
                        convertedToEn += subEn;
                        convertedToAr += subAr;
                        idx++;
                        continue;
                    }

                    ScriptKind runScript = t.Script;
                    int lastScriptToken = idx;

                    // Extend run across whitespace and neutral punctuation as long as the next script token matches runScript
                    int scan = idx + 1;
                    while (scan < tokens.Count)
                    {
                        Token cur = tokens[scan];
                        if (cur.IsWhitespace)
                        {
                            scan++;
                            continue;
                        }
                        if (cur.IsProtected)
                        {
                            break;
                        }
                        if (cur.Script == runScript)
                        {
                            lastScriptToken = scan;
                            scan++;
                            continue;
                        }
                        if (cur.Script == ScriptKind.Neutral)
                        {
                            scan++;
                            continue;
                        }
                        break;
                    }
                    int runEnd = lastScriptToken;

                    // Build the run text and compute per-word & aggregate source vs converted scores
                    StringBuilder runBuilder = new StringBuilder();
                    double totalSourceScore = 0.0;
                    double totalTargetScore = 0.0;
                    int wordCount = 0;

                    int runLen = runEnd - idx + 1;
                    double[] tokSource = new double[runLen];
                    double[] tokTarget = new double[runLen];
                    bool[] tokIsScript = new bool[runLen];
                    string[] tokConverted = new string[runLen];

                    for (int k = idx; k <= runEnd; k++)
                    {
                        int rel = k - idx;
                        runBuilder.Append(tokens[k].Text);
                        if (!tokens[k].IsWhitespace && tokens[k].Script == runScript && !tokens[k].IsProtected)
                        {
                            tokIsScript[rel] = true;
                            wordCount++;
                            if (runScript == ScriptKind.Arabic)
                            {
                                string cand = ConvertArabicToEnglish(tokens[k].Text);
                                tokConverted[rel] = cand;
                                tokSource[rel] = ScoreArabicWord(tokens[k].Text);
                                tokTarget[rel] = ScoreEnglishWord(cand);
                            }
                            else
                            {
                                string cand = ConvertEnglishToArabic(tokens[k].Text);
                                tokConverted[rel] = cand;
                                tokSource[rel] = ScoreEnglishWord(tokens[k].Text);
                                tokTarget[rel] = ScoreArabicWord(cand);
                            }
                            totalSourceScore += tokSource[rel];
                            totalTargetScore += tokTarget[rel];
                        }
                    }

                    string runText = runBuilder.ToString();
                    double avgSource = wordCount > 0 ? totalSourceScore / wordCount : 0.0;
                    double avgTarget = wordCount > 0 ? totalTargetScore / wordCount : 0.0;
                    double minTarget = (runScript == ScriptKind.Arabic) ? 1.2 : 1.0;

                    if (avgTarget >= minTarget && avgTarget > avgSource + 1.8)
                    {
                        // Whole run is mistyped gibberish
                        if (runScript == ScriptKind.Arabic)
                        {
                            sb.Append(ConvertArabicToEnglish(runText));
                            convertedToEn += wordCount;
                        }
                        else
                        {
                            sb.Append(ConvertEnglishToArabic(runText));
                            convertedToAr += wordCount;
                        }
                    }
                    else
                    {
                        // Run may mix valid words and gibberish words of the same script
                        // (e.g., "اثممخ صخقمي ثم احفظ"). Convert only tokens that are clearly gibberish.
                        for (int rel = 0; rel < runLen; rel++)
                        {
                            int k = idx + rel;
                            if (tokIsScript[rel] && tokTarget[rel] >= 1.5 && (tokTarget[rel] - tokSource[rel]) >= 3.0)
                            {
                                sb.Append(tokConverted[rel]);
                                if (runScript == ScriptKind.Arabic) convertedToEn++;
                                else convertedToAr++;
                            }
                            else
                            {
                                sb.Append(tokens[k].Text);
                            }
                        }
                    }

                    idx = runEnd + 1;
                }

                if (convertedToEn > 0 || convertedToAr > 0)
                {
                    result.Direction = ConversionDirection.MixedSmart;
                    result.ConvertedText = sb.ToString();
                    if (convertedToEn > convertedToAr)
                        result.TargetLanguage = TargetLanguage.English;
                    else if (convertedToAr > convertedToEn)
                        result.TargetLanguage = TargetLanguage.Arabic;
                    else
                        result.TargetLanguage = TargetLanguage.None;
                    return result;
                }

                // All Arabic and English runs in the mixed sentence are already valid:
                // do NOT wreck the sentence by flipping everything!
                return result;
            }

            // When smartMixedMode is OFF on mixed text: convert each token according to its own script
            // while leaving protected tokens (URLs, emails, paths, numbers) and whitespace untouched.
            StringBuilder manualSb = new StringBuilder(text.Length);
            for (int i = 0; i < tokens.Count; i++)
            {
                Token t = tokens[i];
                if (t.IsWhitespace || t.IsProtected || t.Script == ScriptKind.Neutral)
                {
                    manualSb.Append(t.Text);
                }
                else if (t.Script == ScriptKind.Arabic)
                {
                    manualSb.Append(ConvertArabicToEnglish(t.Text));
                }
                else if (t.Script == ScriptKind.Latin)
                {
                    manualSb.Append(ConvertEnglishToArabic(t.Text));
                }
                else
                {
                    int dummyEn, dummyAr;
                    manualSb.Append(ConvertMixedTokenSmart(t.Text, out dummyEn, out dummyAr));
                }
            }

            result.Direction = ConversionDirection.MixedSmart;
            result.TargetLanguage = (arabicWordTokens >= latinWordTokens) ? TargetLanguage.English : TargetLanguage.Arabic;
            result.ConvertedText = manualSb.ToString();
            return result;
        }

        /// <summary>
        /// Converts text typed on a US English keyboard into Windows Arabic (101) characters.
        /// Handles accidental CapsLock and leaves protected tokens (URLs, emails, paths, IPs, handles) untouched.
        /// </summary>
        public static string ConvertEnglishToArabic(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

            // Process whitespace-delimited tokens so protected tokens and CapsLock detection work cleanly
            List<Token> tokens = Tokenize(input);

            // Check global CapsLock only across non-protected tokens
            StringBuilder unprotectedText = new StringBuilder(input.Length);
            for (int tIdx = 0; tIdx < tokens.Count; tIdx++)
            {
                if (!tokens[tIdx].IsWhitespace && !IsProtectedToken(tokens[tIdx].Text))
                {
                    unprotectedText.Append(tokens[tIdx].Text);
                    unprotectedText.Append(' ');
                }
            }
            bool globalCapsLock = LooksLikeCapsLock(unprotectedText.ToString());
            StringBuilder sb = new StringBuilder(input.Length * 2);

            for (int tIdx = 0; tIdx < tokens.Count; tIdx++)
            {
                Token tok = tokens[tIdx];
                if (tok.IsWhitespace || IsProtectedToken(tok.Text))
                {
                    sb.Append(tok.Text);
                    continue;
                }

                string s = tok.Text;
                bool tokenCaps = globalCapsLock || LooksLikeCapsLock(s);

                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];

                    // Preserve decimal point or comma strictly between digits (e.g., 3.14, 1,000)
                    if ((c == '.' || c == ',') && i > 0 && i + 1 < s.Length &&
                        char.IsDigit(s[i - 1]) && char.IsDigit(s[i + 1]))
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (tokenCaps && c >= 'A' && c <= 'Z')
                    {
                        c = (char)(c + 32);
                    }

                    string mapped;
                    if (EnToAr.TryGetValue(c, out mapped))
                    {
                        sb.Append(mapped);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts text typed on a Windows Arabic (101) keyboard into US English characters.
        /// Handles 2-character ligatures (لا, لأ, لإ, لآ) with context-aware disambiguation
        /// for لا ('b' vs 'gh' in English words like right/night/thought/enough/high),
        /// and leaves protected tokens (URLs, emails, paths, IPs, handles) untouched.
        /// </summary>
        public static string ConvertArabicToEnglish(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

            List<Token> tokens = Tokenize(input);
            StringBuilder sb = new StringBuilder(input.Length);

            for (int tIdx = 0; tIdx < tokens.Count; tIdx++)
            {
                Token tok = tokens[tIdx];
                if (tok.IsWhitespace || IsProtectedToken(tok.Text))
                {
                    sb.Append(tok.Text);
                    continue;
                }

                ConvertArabicTokenToEnglishInto(tok.Text, sb);
            }

            return sb.ToString();
        }

        private static void ConvertArabicTokenToEnglishInto(string input, StringBuilder sb)
        {
            int len = input.Length;
            for (int i = 0; i < len; i++)
            {
                char c = input[i];

                // Check 2-char Arabic-101 ligatures starting with Lam (ل = \u0644)
                if (c == '\u0644' && i + 1 < len)
                {
                    char next = input[i + 1];
                    if (next == '\u0622') // لآ -> B
                    {
                        sb.Append('B');
                        i++;
                        continue;
                    }
                    if (next == '\u0623') // لأ -> G
                    {
                        sb.Append('G');
                        i++;
                        continue;
                    }
                    if (next == '\u0625') // لإ -> T
                    {
                        sb.Append('T');
                        i++;
                        continue;
                    }
                    if (next == '\u0627') // لا -> 'b' or 'gh'
                    {
                        if (ShouldDecodeLamAlefAsGh(sb, input, i + 2))
                        {
                            sb.Append("gh");
                        }
                        else
                        {
                            sb.Append('b');
                        }
                        i++;
                        continue;
                    }
                }

                // Preserve '.' or ',' strictly between digits (e.g., 3.14)
                if ((c == '.' || c == ',') && i > 0 && i + 1 < len &&
                    char.IsDigit(input[i - 1]) && char.IsDigit(input[i + 1]))
                {
                    sb.Append(c);
                    continue;
                }

                char mapped;
                if (ArToEn.TryGetValue(c, out mapped))
                {
                    sb.Append(mapped);
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        private static string ConvertMixedTokenSmart(string token, out int convertedToEn, out int convertedToAr)
        {
            convertedToEn = 0;
            convertedToAr = 0;

            // Split a single mixed-script token into contiguous Arabic / Latin / Neutral spans
            StringBuilder sb = new StringBuilder(token.Length);
            int i = 0;
            while (i < token.Length)
            {
                ScriptKind curKind = ClassifyChar(token[i]);
                int start = i;
                while (i < token.Length && ClassifyChar(token[i]) == curKind)
                {
                    i++;
                }
                string span = token.Substring(start, i - start);
                if (curKind == ScriptKind.Arabic)
                {
                    string cand = ConvertArabicToEnglish(span);
                    double sAr = ScoreArabicWord(span);
                    double sEn = ScoreEnglishWord(cand);
                    if (sEn >= 1.2 && sEn > sAr + 1.8)
                    {
                        sb.Append(cand);
                        convertedToEn++;
                    }
                    else
                    {
                        sb.Append(span);
                    }
                }
                else if (curKind == ScriptKind.Latin)
                {
                    string cand = ConvertEnglishToArabic(span);
                    double sEn = ScoreEnglishWord(span);
                    double sAr = ScoreArabicWord(cand);
                    if (sAr >= 1.0 && sAr > sEn + 1.8)
                    {
                        sb.Append(cand);
                        convertedToAr++;
                    }
                    else
                    {
                        sb.Append(span);
                    }
                }
                else
                {
                    sb.Append(span);
                }
            }
            return sb.ToString();
        }

        private static ScriptKind ClassifyChar(char c)
        {
            if (IsArabicLetter(c) || IsArabicDiacritic(c)) return ScriptKind.Arabic;
            if (IsAsciiLetter(c)) return ScriptKind.Latin;
            return ScriptKind.Neutral;
        }

        private static bool ShouldDecodeLamAlefAsGh(StringBuilder emittedSoFar, string source, int afterIndex)
        {
            // Inspect previous 1..2 emitted English letters (within same word)
            char prev1 = '\0';
            char prev2 = '\0';
            int n = emittedSoFar.Length;
            if (n >= 1 && IsAsciiLetter(emittedSoFar[n - 1]))
            {
                prev1 = char.ToLowerInvariant(emittedSoFar[n - 1]);
                if (n >= 2 && IsAsciiLetter(emittedSoFar[n - 2]))
                {
                    prev2 = char.ToLowerInvariant(emittedSoFar[n - 2]);
                }
            }

            // Inspect next English letter (peek what source[afterIndex] maps to)
            char nextEn = '\0';
            bool atWordEnd = true;
            if (afterIndex < source.Length)
            {
                char srcNext = source[afterIndex];
                char mappedNext;
                if (ArToEn.TryGetValue(srcNext, out mappedNext) && IsAsciiLetter(mappedNext))
                {
                    nextEn = char.ToLowerInvariant(mappedNext);
                    atWordEnd = false;
                }
                else if (IsAsciiLetter(srcNext))
                {
                    nextEn = char.ToLowerInvariant(srcNext);
                    atWordEnd = false;
                }
            }

            // 1. Word-initial "gho..." (e.g., ghost)
            if (prev1 == '\0' && nextEn == 'o')
            {
                // Check next-next is 's' (ghost) so we don't break "book", "boy", "both", "box", "born", "body"
                if (afterIndex + 1 < source.Length)
                {
                    char mappedNext2;
                    if (ArToEn.TryGetValue(source[afterIndex + 1], out mappedNext2) &&
                        char.ToLowerInvariant(mappedNext2) == 's')
                    {
                        return true;
                    }
                }
                return false;
            }

            // 2. "...ight" (right, night, light, might, sight, fight, bright, flight, eight, weight)
            //    or "...igh" at word end (high, sigh, thigh)
            if (prev1 == 'i' && (nextEn == 't' || atWordEnd))
            {
                // Avoid turning "lib" or "rib" or "fib" or "nib" into "ligh"/"righ" when prev2 is r/l/f/n and atWordEnd
                if (atWordEnd && (prev2 == 'r' || prev2 == 'l' || prev2 == 'f' || prev2 == 'n' || prev2 == 'b'))
                {
                    return false;
                }
                return true;
            }

            // 3. "...ough..." (though, through, thought, enough, rough, cough, tough, bought, brought, ought)
            if (prev2 == 'o' && prev1 == 'u' && (nextEn == 't' || nextEn == 's' || nextEn == 'e' || nextEn == 'l' || nextEn == 'n' || atWordEnd))
            {
                return true;
            }

            // 4. "...augh..." (laugh, caught, taught, daughter, naughty, laughter)
            if (prev2 == 'a' && prev1 == 'u' && (nextEn == 't' || nextEn == 's' || nextEn == 'e' || nextEn == 'i' || atWordEnd))
            {
                return true;
            }

            return false;
        }

        private static bool LooksLikeCapsLock(string text)
        {
            int upper = 0;
            int lower = 0;
            int nonArabicUpper = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'A' && c <= 'Z')
                {
                    upper++;
                    // Letters whose Arabic Shift mapping is a diacritic/punctuation rather than Hamza/Lam-Alef
                    // (Normal Arabic typing only uses Shift+H/Y/N/G/T/B for أ/إ/آ/لأ/لإ/لآ)
                    if (c != 'H' && c != 'Y' && c != 'N' && c != 'G' && c != 'T' && c != 'B')
                    {
                        nonArabicUpper++;
                    }
                }
                else if (c >= 'a' && c <= 'z')
                {
                    lower++;
                }
            }

            int total = upper + lower;
            if (total < 3) return false;
            return (upper * 100 / total) >= 75 && nonArabicUpper >= 2;
        }

        public static double ScoreEnglishWord(string token)
        {
            if (string.IsNullOrEmpty(token)) return -10.0;

            string trimmed = TrimEdgePunctuation(token);
            if (string.IsNullOrEmpty(trimmed)) return 0.0;

            double score = 0.0;
            int letters = 0;
            int vowels = 0;
            int maxConsonantRun = 0;
            int currentConsonantRun = 0;
            int embeddedArPunct = 0;

            StringBuilder cleanLetters = new StringBuilder(trimmed.Length);

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (IsAsciiLetter(c))
                {
                    char low = char.ToLowerInvariant(c);
                    cleanLetters.Append(low);
                    letters++;
                    if (IsEnglishVowel(low))
                    {
                        vowels++;
                        currentConsonantRun = 0;
                    }
                    else
                    {
                        currentConsonantRun++;
                        if (currentConsonantRun > maxConsonantRun)
                            maxConsonantRun = currentConsonantRun;
                    }
                }
                else if (c == ';' || c == ',' || c == '.' || c == '[' || c == ']' || c == '/' || c == '`')
                {
                    // Punctuation inside an English word is a huge indicator of Arabic typed on English layout
                    // (e.g., ugd;l = عليكم, ,vplm = ورحمة, hpl] = أحمد, [lhg = جمال)
                    embeddedArPunct++;
                }
                else if (c == '\'')
                {
                    // Allow normal English contractions (don't, it's, I'm, you're, we'll, can't, they've, he'd)
                    bool validApostrophe = (i > 0 && i + 1 < trimmed.Length && IsAsciiLetter(trimmed[i - 1]) && IsAsciiLetter(trimmed[i + 1]));
                    if (validApostrophe)
                    {
                        string suffix = trimmed.Substring(i + 1).ToLowerInvariant();
                        if (suffix != "s" && suffix != "t" && suffix != "m" && suffix != "re" &&
                            suffix != "ll" && suffix != "ve" && suffix != "d")
                        {
                            embeddedArPunct++;
                        }
                    }
                    else
                    {
                        embeddedArPunct++;
                    }
                }
                else if (IsArabicChar(c))
                {
                    score -= 4.0;
                }
            }

            // Also check leading/trailing Arabic-mapped punctuation attached to letters (like ,vplm or hpl])
            if (token.Length >= 2)
            {
                char first = token[0];
                char last = token[token.Length - 1];
                if ((first == ',' || first == ';' || first == '[' || first == ']' || first == '/' || first == '\'' || first == '`') &&
                    IsAsciiLetter(token[1]))
                {
                    embeddedArPunct++;
                }
                if ((last == ';' || last == '[' || last == ']' || last == '/' || last == '`') &&
                    IsAsciiLetter(token[token.Length - 2]))
                {
                    embeddedArPunct++;
                }
            }

            score -= embeddedArPunct * 6.0;

            if (letters == 0) return score - 5.0;

            string word = cleanLetters.ToString();

            // Common English short word bonus
            if (CommonEnglishShortWords.Contains(word))
            {
                score += 6.0;
            }

            // Vowel presence & ratio
            if (letters >= 3 && vowels == 0)
            {
                score -= 7.0;
            }
            else if (letters >= 2)
            {
                double vowelRatio = (double)vowels / letters;
                if (vowelRatio >= 0.20 && vowelRatio <= 0.72)
                    score += 2.5;
                else if (vowelRatio < 0.14)
                    score -= 4.0;
            }

            // Consonant clusters
            if (maxConsonantRun >= 5) score -= 6.0;
            else if (maxConsonantRun == 4) score -= 2.5;

            // Bigram plausibility
            if (word.Length >= 2)
            {
                int goodBigrams = 0;
                int badBigrams = 0;
                int totalBigrams = word.Length - 1;
                for (int i = 0; i < totalBigrams; i++)
                {
                    string bg = word.Substring(i, 2);
                    if (CommonEnglishBigrams.Contains(bg))
                        goodBigrams++;
                    else if (RareImpossibleEnglishBigrams.Contains(bg))
                        badBigrams++;
                }
                double bigramRatio = (double)goodBigrams / totalBigrams;
                score += (bigramRatio - 0.35) * 8.0;
                score -= badBigrams * 3.5;
            }

            return score;
        }

        public static double ScoreArabicWord(string token)
        {
            if (string.IsNullOrEmpty(token)) return -10.0;

            string trimmed = TrimEdgePunctuation(token);
            if (string.IsNullOrEmpty(trimmed)) return 0.0;

            double score = 0.0;
            int arLetters = 0;
            int diacritics = 0;
            int strangeSymbols = 0;
            int rareVowelProxyCount = 0; // ث(e), خ(o), ش(a), ه(i), ص(w), ض(q), ظ(/)

            StringBuilder cleanAr = new StringBuilder(trimmed.Length);

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (IsArabicDiacritic(c))
                {
                    diacritics++;
                    // Diacritic at index 0 is impossible in Arabic (caused by Shift+Q/W/E/R/A/S/X/~ in Arabic layout!)
                    if (i == 0) score -= 8.0;
                }
                else if (IsArabicLetter(c))
                {
                    cleanAr.Append(c);
                    arLetters++;
                    if (c == '\u062B' || c == '\u062E' || c == '\u0635' || c == '\u0636' || c == '\u0638')
                    {
                        rareVowelProxyCount++;
                    }
                }
                else if (c == '[' || c == ']' || c == '{' || c == '}' || c == '/' || c == '~' ||
                         c == '\u00F7' || c == '\u00D7' || c == '\u2018' || c == '\u2019' ||
                         c == '\u061B' || c == '\u060C' || c == '\u0640')
                {
                    // Symbols inside Arabic words caused by Shift+English letters (F,D,V,C,L,Z,I,O,U,M,P,K,J)
                    strangeSymbols++;
                }
            }

            // Also check leading shifted-English symbols attached to Arabic letters (e.g., [همث = File, ]شفش = Data, /خرث = Love)
            if (token.Length >= 2)
            {
                char first = token[0];
                if ((first == '[' || first == ']' || first == '{' || first == '}' || first == '/' || first == '~' ||
                     first == '\u00F7' || first == '\u00D7' || first == '\u2018' || first == '\u2019' ||
                     first == '\u061B' || first == '\u060C' || first == '\u0640' || IsArabicDiacritic(first)) &&
                    IsArabicLetter(token[1]))
                {
                    strangeSymbols++;
                }
            }

            score -= strangeSymbols * 6.0;
            if (diacritics >= 2) score -= diacritics * 2.5;

            if (arLetters == 0) return score - 5.0;

            string word = cleanAr.ToString();

            // Common Arabic word bonus
            if (CommonArabicShortWords.Contains(word))
            {
                score += 6.5;
            }

            // Positional impossibilities in real Arabic (massive signals for English typed on Arabic layout!)
            bool hasAnomaly = (strangeSymbols > 0 || diacritics >= 2);
            for (int i = 0; i < word.Length; i++)
            {
                char c = word[i];
                bool isLast = (i == word.Length - 1);

                // ة (m) NEVER appears in non-final position in real Arabic!
                if (c == '\u0629' && !isLast)
                {
                    score -= 7.0;
                    hasAnomaly = true;
                }
                // ى (n) NEVER appears in non-final position in real Arabic!
                if (c == '\u0649' && !isLast)
                {
                    score -= 7.0;
                    hasAnomaly = true;
                }
                // ء (x), ئ (z), ؤ (c) NEVER start a real Arabic word!
                if (i == 0 && (c == '\u0621' || c == '\u0626' || c == '\u0624'))
                {
                    score -= 6.5;
                    hasAnomaly = true;
                }
            }

            // High density of rare Arabic letters that correspond to frequent English letters (ث=e, خ=o, ص=w, ض=q, ظ=/)
            if (arLetters >= 3)
            {
                double rareRatio = (double)rareVowelProxyCount / arLetters;
                if (rareRatio >= 0.45)
                {
                    score -= 5.0;
                    hasAnomaly = true;
                }
                else if (rareRatio >= 0.30)
                {
                    score -= 2.0;
                }
            }

            // Clean Arabic structure baseline bonus (parallel to English vowel-ratio baseline)
            if (!hasAnomaly && arLetters >= 2)
            {
                score += 2.2;
            }

            // Definite article "ال" bonus
            if (word.Length >= 4 && word[0] == '\u0627' && word[1] == '\u0644')
            {
                score += 3.0;
            }

            // Bigram plausibility
            if (word.Length >= 2)
            {
                int goodBigrams = 0;
                int totalBigrams = word.Length - 1;
                for (int i = 0; i < totalBigrams; i++)
                {
                    string bg = word.Substring(i, 2);
                    if (CommonArabicBigrams.Contains(bg))
                        goodBigrams++;
                }
                double bigramRatio = (double)goodBigrams / totalBigrams;
                score += (bigramRatio - 0.25) * 7.0;
            }

            return score;
        }

        private static readonly HashSet<string> KnownTlds = new HashSet<string>
        {
            "com","net","org","io","dev","app","gov","edu","co","eg","uk","de","fr","sa","ae",
            "me","ai","info","biz","tv","gg","xyz","site","online","store","tech","cloud","pro",
            "link","ly","be","it","es","ru","jp","cn","in","ca","au","nl","se","ch","cc","sh"
        };

        /// <summary>
        /// Returns true if a token is a URL, domain, email, file path, IP address, version tag,
        /// social handle/hashtag, hex literal, or pure number/symbol that must never be converted.
        /// </summary>
        public static bool IsProtectedToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return true;

            string core = TrimTokenWrappers(token);
            if (string.IsNullOrEmpty(core)) return true;

            string low = core.ToLowerInvariant();

            // 1. Explicit URL schemes & www.
            if (low.StartsWith("http://") || low.StartsWith("https://") ||
                low.StartsWith("ftp://") || low.StartsWith("mailto:") ||
                low.StartsWith("file://") || low.StartsWith("www."))
            {
                return true;
            }

            // 2. Localhost with port/path
            if (low == "localhost" || low.StartsWith("localhost:") || low.StartsWith("localhost/"))
            {
                return true;
            }

            // 3. Email address (local@domain.tld)
            int atIdx = low.IndexOf('@');
            if (atIdx > 0 && atIdx == low.LastIndexOf('@') && atIdx < low.Length - 3)
            {
                int dotAfterAt = low.IndexOf('.', atIdx + 2);
                if (dotAfterAt > atIdx + 1 && dotAfterAt < low.Length - 1)
                {
                    string tldPart = low.Substring(low.LastIndexOf('.') + 1);
                    if (tldPart.Length >= 2 && IsAllAsciiLetters(tldPart))
                    {
                        return true;
                    }
                }
            }

            // 4. Bare domain / URL without scheme (e.g. github.com/user/repo, docs.google.com, site.co.uk)
            if (LooksLikeDomainOrUrl(low))
            {
                return true;
            }

            // 5. Windows / UNC / Unix file paths
            if (core.Length >= 3 && IsAsciiLetter(core[0]) && core[1] == ':' && (core[2] == '\\' || core[2] == '/'))
            {
                return true;
            }
            if (core.StartsWith(@"\\") && core.Length >= 4)
            {
                return true;
            }
            if (core.StartsWith("/") && core.IndexOf('/', 1) > 1 && !ContainsArabic(core))
            {
                return true;
            }

            // 6. IPv4 address (with optional :port)
            if (LooksLikeIpv4(low))
            {
                return true;
            }

            // 7. Version strings (v1.0, v1.0.0, v2.3.1-beta)
            if (low.Length >= 4 && low[0] == 'v' && char.IsDigit(low[1]) && low.IndexOf('.') >= 2)
            {
                return true;
            }

            // 8. Hex literals (0x4B46)
            if (low.StartsWith("0x") && low.Length >= 3 && IsHexDigits(low, 2))
            {
                return true;
            }

            // 9. Social @handles (e.g. @legard20)
            if (core.Length >= 3 && core[0] == '@' && IsAsciiLetter(core[1]) && !ContainsArabic(core))
            {
                return true;
            }

            // 10. Pure digits/symbols (no letters at all: 2026, 3.14, +20100)
            bool hasLetter = false;
            for (int i = 0; i < core.Length; i++)
            {
                if (IsAsciiLetter(core[i]) || IsArabicLetter(core[i]))
                {
                    hasLetter = true;
                    break;
                }
            }
            return !hasLetter;
        }

        private static string TrimTokenWrappers(string s)
        {
            int start = 0;
            int end = s.Length - 1;
            while (start <= end && (s[start] == '(' || s[start] == '[' || s[start] == '{' ||
                                    s[start] == '<' || s[start] == '"' || s[start] == '\''))
            {
                start++;
            }
            while (end >= start && (s[end] == ')' || s[end] == ']' || s[end] == '}' ||
                                    s[end] == '>' || s[end] == '"' || s[end] == '\'' ||
                                    s[end] == ',' || s[end] == '.' || s[end] == ';' ||
                                    s[end] == ':' || s[end] == '!' || s[end] == '?'))
            {
                end--;
            }
            if (start > end) return string.Empty;
            return s.Substring(start, end - start + 1);
        }

        private static bool LooksLikeDomainOrUrl(string low)
        {
            if (ContainsArabic(low)) return false;

            // Isolate host part before '/', '?', '#', ':'
            int hostEnd = low.Length;
            for (int i = 0; i < low.Length; i++)
            {
                char c = low[i];
                if (c == '/' || c == '?' || c == '#' || c == ':')
                {
                    hostEnd = i;
                    break;
                }
            }
            if (hostEnd < 4) return false;
            string host = low.Substring(0, hostEnd);
            int lastDot = host.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= host.Length - 2) return false;

            // Validate host chars (letters, digits, hyphens, dots)
            for (int i = 0; i < host.Length; i++)
            {
                char c = host[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '.'))
                    return false;
            }

            string tld = host.Substring(lastDot + 1);
            return KnownTlds.Contains(tld);
        }

        private static bool LooksLikeIpv4(string s)
        {
            int colon = s.IndexOf(':');
            string ip = (colon > 0) ? s.Substring(0, colon) : s;
            string[] parts = ip.Split('.');
            if (parts.Length != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                int val;
                if (!int.TryParse(parts[i], out val) || val < 0 || val > 255)
                    return false;
            }
            return true;
        }

        private static bool IsAllAsciiLetters(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (!IsAsciiLetter(s[i])) return false;
            }
            return s.Length > 0;
        }

        private static bool IsHexDigits(string s, int startIndex)
        {
            for (int i = startIndex; i < s.Length; i++)
            {
                char c = s[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        private static bool ContainsArabic(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (IsArabicChar(s[i])) return true;
            }
            return false;
        }

        private static string TrimEdgePunctuation(string s)
        {
            int start = 0;
            int end = s.Length - 1;
            while (start <= end && IsNeutralEdgePunct(s[start])) start++;
            while (end >= start && IsNeutralEdgePunct(s[end])) end--;
            if (start > end) return string.Empty;
            return s.Substring(start, end - start + 1);
        }

        private static bool IsNeutralEdgePunct(char c)
        {
            return c == '!' || c == '?' || c == '\u061F' || c == ':' || c == '"' ||
                   c == '(' || c == ')' || c == '-' || c == '_' || c == '*' || c == '#';
        }

        private static ScriptKind ClassifyToken(string token)
        {
            int ar = 0;
            int en = 0;
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (IsArabicLetter(c) || IsArabicDiacritic(c)) ar++;
                else if (IsAsciiLetter(c)) en++;
            }
            if (ar > 0 && en > 0) return ScriptKind.Mixed;
            if (ar > 0) return ScriptKind.Arabic;
            if (en > 0) return ScriptKind.Latin;
            return ScriptKind.Neutral;
        }

        private static int CountArabicMappableChars(string s)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (IsArabicChar(c) || c == '\u061F' || c == '\u061B' || c == '\u060C')
                    count++;
            }
            return count;
        }

        private static int CountEnglishMappableChars(string s)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (EnToAr.ContainsKey(c) && !char.IsDigit(c))
                    count++;
            }
            return count;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static bool IsEnglishVowel(char c)
        {
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' || c == 'y';
        }

        private static bool IsArabicLetter(char c)
        {
            return (c >= '\u0621' && c <= '\u063A') || (c >= '\u0641' && c <= '\u064A');
        }

        private static bool IsArabicDiacritic(char c)
        {
            return c >= '\u064B' && c <= '\u0652';
        }

        private static bool IsArabicChar(char c)
        {
            return c >= '\u0600' && c <= '\u06FF';
        }

        private enum ScriptKind
        {
            Neutral = 0,
            Arabic = 1,
            Latin = 2,
            Mixed = 3
        }

        private sealed class Token
        {
            public string Text;
            public bool IsWhitespace;
            public bool IsProtected;
            public ScriptKind Script;
        }

        private static List<Token> Tokenize(string text)
        {
            List<Token> tokens = new List<Token>();
            int i = 0;
            int len = text.Length;
            while (i < len)
            {
                bool ws = char.IsWhiteSpace(text[i]);
                int start = i;
                while (i < len && char.IsWhiteSpace(text[i]) == ws)
                {
                    i++;
                }
                tokens.Add(new Token
                {
                    Text = text.Substring(start, i - start),
                    IsWhitespace = ws,
                    Script = ScriptKind.Neutral
                });
            }
            return tokens;
        }
    }
}
