using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 게임 전체 점검 — *SelfCheck의 public static Run()을 전부 한 세션에서 실행한다.
    /// 256개 파일을 개별 -executeMethod로 돌리면 유니티 기동 256번이라 사실상 불가능했다.
    ///
    /// 실패는 예외로 세고 다음 검사로 넘어간다(대부분의 SelfCheck는 FAIL 시 throw).
    /// EditorApplication.Exit를 직접 부르는 넷(UiAtlasUv·PlayableScenesBuilder·
    /// V4ExternalPlaytest·TowerClimbCurveMeasure)과 스윕 본체(GameSweepSelfCheck —
    /// 등록부가 전수와 같아 중첩)는 제외한다. 그 다섯은 각자 따로 돈다.
    ///
    ///   Unity -batchmode -quit -nographics -projectPath &lt;unity_meas&gt; \
    ///         -executeMethod AshesToStars.GameFullCheck.Run
    /// </summary>
    public static class GameFullCheck
    {
        static readonly string[] Skip =
        {
            "UiAtlasUvSelfCheck",        // EditorApplication.Exit — 단독 실행 전용
            "PlayableScenesBuilder",     // 빌더지 검사가 아니다
            "V4ExternalPlaytest",        // 별도 러너(loop/v4_playtest.py)가 돈다
            "TowerClimbCurveMeasure",    // 관문 측정 — 결과 파일을 따로 남긴다
            "GameSweepSelfCheck",        // 스윕 본체 — 등록부가 전수와 같아져(20260826 채택 1)
                                         // 중첩 실행은 모든 검사를 두 번 도는 것과 같다.
                                         // 스윕은 매 바퀴 batch로 이미 돈다.
        };

        [MenuItem("Ashes to Stars/QA/Game Full Check (all SelfChecks)")]
        public static void Run()
        {
            var targets = typeof(GameFullCheck).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed)   // static class
                .Where(t => t.Name.EndsWith("SelfCheck", StringComparison.Ordinal))
                .Where(t => !Skip.Contains(t.Name))
                .Select(t => new { Type = t, Method = t.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null) })
                .Where(x => x.Method != null)
                .OrderBy(x => x.Type.Name, StringComparer.Ordinal)
                .ToList();

            var failed = new List<string>();
            var log = new StringBuilder();
            int done = 0;
            foreach (var t in targets)
            {
                try
                {
                    t.Method.Invoke(null, null);
                    log.AppendLine("  PASS  " + t.Type.Name);
                }
                catch (Exception e)
                {
                    var why = e.InnerException ?? e;
                    failed.Add(t.Type.Name);
                    log.AppendLine("  FAIL  " + t.Type.Name + " — " + Truncate(why.Message, 160));
                    // InnerException.StackTrace 병기(회의 20260826-095813 채택 2) —
                    // BossBattleDps NRE 스택 오독(W3Party.cs:809 오독 → NextStyle:1126 정정) 재발 차단.
                    var stack = (why.StackTrace ?? "스택 없음").Replace("\n", " | ").Replace("\r", "");
                    log.AppendLine("        at " + Truncate(stack, 240));
                }
                done++;
            }

            string head = failed.Count == 0
                ? $"[GameFullCheck] PASS {done}/{done}"
                : $"[GameFullCheck] FAIL {failed.Count}/{done} — " + string.Join(", ", failed.Take(8));
            Debug.Log(head + "\n" + log);

            WriteReport(done, failed, log.ToString());
            if (failed.Count > 0) EditorApplication.Exit(1);
        }

        static string Truncate(string s, int n)
            => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n);

        static void WriteReport(int total, List<string> failed, string body)
        {
            try
            {
                var dir = new DirectoryInfo(Application.dataPath);
                for (int i = 0; i < 4 && dir?.Parent != null; i++) dir = dir.Parent;
                string outDir = Path.Combine(dir.FullName, "output", "qa", "ashes-to-stars", "full_check");
                Directory.CreateDirectory(outDir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                File.WriteAllText(Path.Combine(outDir, $"full_check_{stamp}.log"),
                    $"total {total} · fail {failed.Count}\n{string.Join("\n", failed)}\n\n{body}",
                    new UTF8Encoding(false));
                // 보드 「검증 결과」 칸이 읽는 파일도 갱신한다(loop/last_test_report.json).
                string loopDir = Path.Combine(dir.FullName, "loop");
                if (Directory.Exists(loopDir))
                {
                    var items = new StringBuilder();
                    items.Append("{\"name\":\"SelfCheck 전수\",\"ok\":").Append(failed.Count == 0 ? "true" : "false")
                         .Append(",\"note\":\"").Append(failed.Count == 0 ? $"{total}개 전부 통과" : $"{failed.Count}개 실패: {Truncate(string.Join(" ", failed), 120)}")
                         .Append("\"}");
                    File.WriteAllText(Path.Combine(loopDir, "last_test_report.json"),
                        "{\"at\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\",\"ok\":" + (failed.Count == 0 ? "true" : "false")
                        + ",\"summary\":\"SelfCheck 전수 " + (total - failed.Count) + "/" + total + " 통과\",\"items\":[" + items + "]}",
                        new UTF8Encoding(false));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameFullCheck] 보고서 기록 실패: " + e.Message);
            }
        }
    }
}
