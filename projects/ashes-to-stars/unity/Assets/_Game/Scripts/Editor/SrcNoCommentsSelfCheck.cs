using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// SrcNoComments Editor 공용 — 주석만의 옛 문자열이 소스 계약 Contains를 FALSE-FAIL
    /// 시키지 않는지. 토큰이 주석에만 있으면 스트립 후 부재, 코드·문자열 리터럴이면 존재.
    /// QA_NO_SRC_NO_COMMENTS=1이면 원문(옛 FALSE-FAIL 경로).
    /// </summary>
    public static class SrcNoCommentsSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        const string Token = "QA_STRIP_PROBE";

        static readonly string[] ConsumerFiles =
        {
            "GroundHollowSelfCheck.cs",
            "BossHpSelfCheck.cs",
            "CharacterRosterSelfCheck.cs",
        };

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Src No Comments Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string old = Environment.GetEnvironmentVariable(SrcNoComments.EnvNo);
            string tmp = null;
            try
            {
                Environment.SetEnvironmentVariable(SrcNoComments.EnvNo, null);

                string editor = Path.Combine(Application.dataPath, "_Game/Scripts/Editor");
                string helperPath = Path.Combine(editor, "SrcNoComments.cs");
                Check(File.Exists(helperPath), "SrcNoComments.cs 헬퍼가 있다");
                if (File.Exists(helperPath))
                {
                    string helper = File.ReadAllText(helperPath);
                    Check(helper.IndexOf("public static string Read(string path)", StringComparison.Ordinal) >= 0,
                        "Read(path) 시그니처가 있다");
                    Check(helper.IndexOf("public static string Strip(string src)", StringComparison.Ordinal) >= 0,
                        "Strip(src) 시그니처가 있다");
                    Check(helper.IndexOf("QA_NO_SRC_NO_COMMENTS", StringComparison.Ordinal) >= 0,
                        "헬퍼가 QA_NO_SRC_NO_COMMENTS 게이트를 갖는다");
                }

                foreach (string file in ConsumerFiles)
                {
                    string path = Path.Combine(editor, file);
                    Check(File.Exists(path), $"{file} 소스 발견");
                    if (!File.Exists(path)) continue;
                    string src = File.ReadAllText(path);
                    Check(src.IndexOf("SrcNoComments.Read", StringComparison.Ordinal) >= 0,
                        $"{file}이 SrcNoComments.Read를 부른다");
                }

                string sweepPath = Path.Combine(editor, "GameSweepSelfCheck.cs");
                Check(File.Exists(sweepPath), "GameSweepSelfCheck.cs 소스 발견");
                if (File.Exists(sweepPath))
                {
                    string sweep = File.ReadAllText(sweepPath);
                    Check(sweep.IndexOf("SrcNoCommentsSelfCheck.Run", StringComparison.Ordinal) >= 0,
                        "GameSweep 등록부에 SrcNoComments 행이 있다");
                }

                Check(!SrcNoComments.Blocked, "기본은 QA_NO_SRC_NO_COMMENTS가 꺼져 있다");

                // ── Strip: 주석만의 토큰은 사라진다 ──
                string lineOnly = "class C {\n    // " + Token + " lives only here\n    void M() { int x = 1; }\n}\n";
                Check(!SrcNoComments.Strip(lineOnly).Contains(Token),
                    "토큰이 // 줄 주석에만 있으면 스트립 후 Contains false");

                string xmlOnly = "/// <summary>" + Token + " in xml doc</summary>\nclass C { int x = 1; }\n";
                Check(!SrcNoComments.Strip(xmlOnly).Contains(Token),
                    "토큰이 /// xml doc에만 있으면 스트립 후 Contains false");

                string blockOnly = "class C {\n    /* " + Token + " in block */\n    void M() {}\n}\n";
                Check(!SrcNoComments.Strip(blockOnly).Contains(Token),
                    "토큰이 /* */ 블록에만 있으면 스트립 후 Contains false");

                // ── Strip: 코드·문자열 리터럴의 토큰은 남는다 ──
                string inCode = "class C {\n    // no token here\n    void " + Token + "() { }\n}\n";
                Check(SrcNoComments.Strip(inCode).Contains(Token),
                    "토큰이 코드에 있으면 스트립 후 Contains true");

                string inString = "class C {\n    string s = \"" + Token + "\";\n}\n";
                Check(SrcNoComments.Strip(inString).Contains(Token),
                    "토큰이 문자열 리터럴에 있으면 스트립 후 Contains true");

                string inVerbatim = "class C {\n    string s = @\"" + Token + "\";\n}\n";
                Check(SrcNoComments.Strip(inVerbatim).Contains(Token),
                    "토큰이 verbatim 문자열에 있으면 스트립 후 Contains true");

                string urlInString = "class C {\n    string u = \"http://example.com/" + Token + "\";\n}\n";
                string strippedUrl = SrcNoComments.Strip(urlInString);
                Check(strippedUrl.Contains("http://example.com/" + Token),
                    "문자열 안의 // 는 주석이 아니라 그대로 남는다");

                string codeThenComment = "int " + Token + " = 1; // drop me\n";
                string mixed = SrcNoComments.Strip(codeThenComment);
                Check(mixed.Contains(Token) && mixed.IndexOf("drop me", StringComparison.Ordinal) < 0,
                    "같은 줄 코드 토큰은 남고 // 뒤는 빠진다");

                // ── Read + QA_NO 네거티브(옛 FALSE-FAIL 경로) ──
                tmp = Path.Combine(Application.temporaryCachePath, "src_no_comments_probe.cs");
                File.WriteAllText(tmp, "// " + Token + " comment only\nclass Probe { int x = 1; }\n");
                Check(!SrcNoComments.Read(tmp).Contains(Token),
                    "Read: 주석만의 토큰은 스트립 후 부재");

                Environment.SetEnvironmentVariable(SrcNoComments.EnvNo, "1");
                Check(SrcNoComments.Blocked, "QA_NO_SRC_NO_COMMENTS=1이면 차단");
                Check(SrcNoComments.Read(tmp).Contains(Token),
                    "QA_NO면 원문(옛 FALSE-FAIL 경로) — 주석 토큰이 Contains true");
                Environment.SetEnvironmentVariable(SrcNoComments.EnvNo, null);
                Check(!SrcNoComments.Blocked, "차단을 풀면 다시 스트립한다");
                Check(!SrcNoComments.Read(tmp).Contains(Token),
                    "차단 해제 후 주석 토큰은 다시 부재");

                _ = nameof(SrcNoComments.Read);
                _ = nameof(SrcNoComments.Strip);
                _ = nameof(SrcNoComments.Blocked);
            }
            finally
            {
                if (tmp != null && File.Exists(tmp)) File.Delete(tmp);
                Environment.SetEnvironmentVariable(SrcNoComments.EnvNo, old);
            }

            if (_fail == 0) Debug.Log("[SrcNoCommentsSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SrcNoCommentsSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[SrcNoCommentsSelfCheck] FAIL {_fail}건");
        }
    }
}
