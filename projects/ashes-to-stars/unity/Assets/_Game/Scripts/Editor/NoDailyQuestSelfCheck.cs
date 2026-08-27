using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// GAME_DESIGN §1 숙제·일일입장 금지. 플레이어 UI 경로에 일일퀘·출석·매일N회
    /// 문구가 0건인지, 오프라인 정산 소비처가 광산 Tick인지, 레이드·경매를
    /// 오늘 의무로 포장하지 않는지. QA_NO_NO_DAILY=1이면 옛 스캔 없음(통과).
    /// </summary>
    public static class NoDailyQuestSelfCheck
    {
        public const string EnvNo = "QA_NO_NO_DAILY";

        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static readonly string[] DailyNeedles =
        {
            "일일퀘",
            "일일 퀘",
            "출석",
            "매일N회",
            "매일 입장",
            "일일 입장",
            "일일 숙제",
            "오늘 의무",
            "매일 해야",
            "daily quest",
            "attendance check-in",
            "attendance checkin",
        };

        static readonly string[] HomeworkNeedles =
        {
            "오늘 의무",
            "매일 해야",
            "일일 숙제",
        };

        static readonly string[] RaidAuctionFiles =
        {
            "FieldBoss.cs",
            "RaidSpawn.cs",
            "AuctionHud.cs",
            "AuctionTrade.cs",
            "TokenPrice.cs",
            "FieldScreen.cs",
        };

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        [MenuItem("Ashes to Stars/QA/No Daily Quest Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string old = Environment.GetEnvironmentVariable(EnvNo);
            string tmpHit = null;
            string tmpComment = null;
            string tmpDir = null;
            try
            {
                Environment.SetEnvironmentVariable(EnvNo, null);
                Check(!Blocked, "기본은 QA_NO_NO_DAILY가 꺼져 있다");

                string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
                Check(Directory.Exists(runtime), "Runtime 폴더가 있다");

                var hits = ScanDir(runtime);
                Check(hits.Count == 0,
                    hits.Count == 0
                        ? $"Runtime UI cs 일일 문구 0 (파일 {CountCs(runtime)})"
                        : "Runtime UI cs 일일 문구 " + hits.Count + " — " + hits[0]);

                foreach (string file in RaidAuctionFiles)
                {
                    string path = Path.Combine(runtime, file);
                    Check(File.Exists(path), file + " 있음(시스템 존재는 OK)");
                    if (!File.Exists(path)) continue;
                    string src = SrcNoComments.Read(path);
                    var hw = FindNeedles(src, HomeworkNeedles);
                    Check(hw.Count == 0,
                        hw.Count == 0
                            ? file + "에 오늘 의무·매일 해야·일일 숙제 프레이밍 없음"
                            : file + " 숙제 프레이밍 " + hw[0]);
                }

                tmpHit = Path.Combine(Application.temporaryCachePath, "no_daily_quest_hit.cs");
                File.WriteAllText(tmpHit,
                    "class Probe {\n    string s = \"일일퀘\";\n    string t = \"출석\";\n    string u = \"매일N회\";\n    string v = \"매일 3회\";\n    string w = \"daily quest\";\n}\n");
                var caught = FindDaily(SrcNoComments.Read(tmpHit));
                Check(caught.Count >= 1 && caught.Exists(h => h.Contains("일일퀘")),
                    "픽스처 일일퀘를 잡는다");
                Check(caught.Exists(h => h.Contains("출석")), "픽스처 출석을 잡는다");
                Check(caught.Exists(h => h.Contains("매일N회") || h.Contains("매일 3회")),
                    "픽스처 매일N회·매일 3회를 잡는다");
                Check(caught.Exists(h => h.IndexOf("daily quest", StringComparison.OrdinalIgnoreCase) >= 0),
                    "픽스처 daily quest를 잡는다");

                tmpComment = Path.Combine(Application.temporaryCachePath, "no_daily_quest_comment.cs");
                File.WriteAllText(tmpComment,
                    "// 일일퀘 출석 매일N회 daily quest — 주석만\nclass Probe { int x = 1; }\n");
                var commentHits = FindDaily(SrcNoComments.Read(tmpComment));
                Check(commentHits.Count == 0,
                    "주석만의 일일퀘는 SrcNoComments 뒤 0 (FALSE-FAIL 차단)");

                Check(FindDaily("string s = \"매일 3회\";").Count >= 1, "매일 3회 패턴을 잡는다");
                Check(FindDaily("string s = \"매일 입장\";").Count >= 1, "매일 입장을 잡는다");
                Check(FindDaily("int x = 1;").Count == 0, "무관한 코드는 0");

                tmpDir = Path.Combine(Application.temporaryCachePath, "no_daily_quest_dir");
                Directory.CreateDirectory(tmpDir);
                File.Copy(tmpHit, Path.Combine(tmpDir, "Hit.cs"), true);
                Check(ScanDir(tmpDir).Count >= 1, "픽스처 폴더를 가리키면 잡는다");

                Environment.SetEnvironmentVariable(EnvNo, "1");
                Check(Blocked, "QA_NO_NO_DAILY=1이면 차단");
                Check(ScanDir(tmpDir).Count == 0, "QA_NO면 같은 픽스처 폴더도 스캔 안 함(옛 통과)");
                Check(ScanDir(runtime).Count == 0, "QA_NO면 프로덕션 스캔 없이 0");
                Environment.SetEnvironmentVariable(EnvNo, null);
                Check(!Blocked, "차단을 풀면 다시 스캔한다");

                string minePath = Path.Combine(runtime, "EstateMine.cs");
                Check(File.Exists(minePath), "EstateMine.cs 소스 발견");
                if (File.Exists(minePath))
                {
                    string mine = SrcNoComments.Read(minePath);
                    int tickAt = mine.IndexOf("static long Tick(", StringComparison.Ordinal);
                    Check(tickAt >= 0, "EstateMine.Tick 정의가 있다");
                    int nextFn = mine.IndexOf("static void SeedQaIfRequested", tickAt, StringComparison.Ordinal);
                    string tickBody = (tickAt >= 0 && nextFn > tickAt)
                        ? mine.Substring(tickAt, nextFn - tickAt)
                        : "";
                    Check(tickBody.IndexOf("OfflineSettle.EffectiveSeconds", StringComparison.Ordinal) >= 0,
                        "EstateMine.Tick이 OfflineSettle.EffectiveSeconds를 부른다");
                    Check(tickBody.IndexOf("EffectiveSeconds(elapsed)", StringComparison.Ordinal) >= 0,
                        "Tick이 경과 초를 EffectiveSeconds에 넘긴다");
                }

                _ = nameof(EstateMine.Tick);
                _ = nameof(OfflineSettle.EffectiveSeconds);
                _ = nameof(SrcNoComments.Read);
            }
            finally
            {
                if (tmpHit != null && File.Exists(tmpHit)) File.Delete(tmpHit);
                if (tmpComment != null && File.Exists(tmpComment)) File.Delete(tmpComment);
                if (tmpDir != null && Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                Environment.SetEnvironmentVariable(EnvNo, old);
            }

            if (_fail == 0) Debug.Log("[NoDailyQuestSelfCheck] PASS\n" + _log);
            else Debug.LogError("[NoDailyQuestSelfCheck] FAIL " + _fail + "건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException("[NoDailyQuestSelfCheck] FAIL " + _fail + "건");
        }

        static int CountCs(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            return Directory.GetFiles(dir, "*.cs").Length;
        }

        /// <summary>QA_NO면 빈 목록(옛 스캔 없음). 아니면 Runtime cs를 SrcNoComments로 본다.</summary>
        internal static List<string> ScanDir(string dir)
        {
            var found = new List<string>();
            if (Blocked || !Directory.Exists(dir)) return found;
            foreach (string path in Directory.GetFiles(dir, "*.cs"))
            {
                if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                string src = SrcNoComments.Read(path);
                foreach (string hit in FindDaily(src))
                    found.Add(Path.GetFileName(path) + ": " + hit);
            }
            return found;
        }

        internal static List<string> FindDaily(string src)
        {
            var found = FindNeedles(src, DailyNeedles);
            if (HasDailyNTimes(src) && !found.Exists(h => h.IndexOf("매일", StringComparison.Ordinal) >= 0))
                found.Add("매일N회");
            return found;
        }

        static List<string> FindNeedles(string src, string[] needles)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(src)) return found;
            for (int i = 0; i < needles.Length; i++)
            {
                string n = needles[i];
                bool en = n.Length > 0 && n[0] < 128;
                if (src.IndexOf(n, en ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0)
                    found.Add(n);
            }
            return found;
        }

        static bool HasDailyNTimes(string src)
        {
            if (string.IsNullOrEmpty(src)) return false;
            if (src.IndexOf("매일N회", StringComparison.Ordinal) >= 0) return true;
            int i = 0;
            while (true)
            {
                int at = src.IndexOf("매일", i, StringComparison.Ordinal);
                if (at < 0) return false;
                int j = at + 2;
                while (j < src.Length && char.IsWhiteSpace(src[j])) j++;
                if (j < src.Length && src[j] >= '0' && src[j] <= '9')
                {
                    while (j < src.Length && src[j] >= '0' && src[j] <= '9') j++;
                    while (j < src.Length && char.IsWhiteSpace(src[j])) j++;
                    if (j < src.Length && src[j] == '회') return true;
                }
                i = at + 2;
            }
        }
    }
}
