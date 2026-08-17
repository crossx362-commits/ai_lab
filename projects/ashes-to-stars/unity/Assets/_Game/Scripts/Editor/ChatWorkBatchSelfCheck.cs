using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>대화 세션 전투 수리(3택·겹침·시드)를 한 번에 돌리고 보드용 JSON을 남긴다.</summary>
    public static class ChatWorkBatchSelfCheck
    {
        struct Row
        {
            public string Name;
            public bool Ok;
            public string Note;
        }

        [MenuItem("Ashes to Stars/QA/Chat Work Batch Self Check")]
        public static void Run()
        {
            var rows = new List<Row>();
            var errors = new List<string>();
            Application.LogCallback hook = (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                    errors.Add(msg);
            };
            Application.logMessageReceived += hook;
            try
            {
                One("사냥 강화 3택", HuntBoonSelfCheck.Run, rows, errors);
                One("캐릭터 겹침", UnitSeparationSelfCheck.Run, rows, errors);
                One("집·나무 겹침", FieldDecorOverlapSelfCheck.Run, rows, errors);
                One("집 돌아나가기", ArenaLayoutSelfCheck.Run, rows, errors);
                One("길 한가운데 금지", FieldDecorRoadSelfCheck.Run, rows, errors);
                One("로컬 테스트 시드", LocalPlayKitSelfCheck.Run, rows, errors);
                One("사냥 경험치", HuntExpSelfCheck.Run, rows, errors);
            }
            finally
            {
                Application.logMessageReceived -= hook;
                HuntBoon.End();
            }

            int fail = 0;
            for (int i = 0; i < rows.Count; i++)
                if (!rows[i].Ok) fail++;
            WriteReport(rows, fail);
            if (fail == 0) Debug.Log($"[ChatWorkBatchSelfCheck] PASS {rows.Count}/{rows.Count}");
            else Debug.LogError($"[ChatWorkBatchSelfCheck] FAIL {fail}/{rows.Count}");
        }

        static void One(string name, Action run, List<Row> rows, List<string> errors)
        {
            errors.Clear();
            try
            {
                run();
            }
            catch (Exception e)
            {
                errors.Add(e.Message);
            }
            bool ok = true;
            string note = "통과";
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i].IndexOf("FAIL", StringComparison.OrdinalIgnoreCase) >= 0
                    || errors[i].IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
                    || errors[i].IndexOf("Assert", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ok = false;
                    note = errors[i];
                    if (note.Length > 160) note = note.Substring(0, 160);
                    break;
                }
            }
            rows.Add(new Row { Name = name, Ok = ok, Note = note });
        }

        static void WriteReport(List<Row> rows, int fail)
        {
            string root = FindRoot();
            if (string.IsNullOrEmpty(root))
            {
                Debug.LogWarning("[ChatWorkBatchSelfCheck] 저장소 루트를 못 찾아 JSON을 못 씀");
                return;
            }
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"at\": \"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\",\n");
            sb.Append("  \"ok\": ").Append(fail == 0 ? "true" : "false").Append(",\n");
            sb.Append("  \"summary\": \"").Append(fail == 0
                ? rows.Count + "개 전부 통과"
                : fail + "개 실패 / " + rows.Count).Append("\",\n");
            sb.Append("  \"items\": [\n");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                sb.Append("    {\"name\":\"").Append(Esc(r.Name))
                  .Append("\",\"ok\":").Append(r.Ok ? "true" : "false")
                  .Append(",\"note\":\"").Append(Esc(r.Note)).Append("\"}");
                sb.Append(i + 1 < rows.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            string path = Path.Combine(root, "loop", "last_test_report.json");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log("[ChatWorkBatchSelfCheck] 기록 " + path);
        }

        static string FindRoot()
        {
            var d = new DirectoryInfo(Application.dataPath);
            while (d != null)
            {
                if (File.Exists(Path.Combine(d.FullName, "loop", "board.py")))
                    return d.FullName;
                d = d.Parent;
            }
            return null;
        }

        static string Esc(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
