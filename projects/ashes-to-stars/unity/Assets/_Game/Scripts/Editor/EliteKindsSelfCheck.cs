using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>정예 유형 1~2종이 던전 지도에 보인다. QA_NO면 옛 퍼센트 줄(§10-2).</summary>
    public static class EliteKindsSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Elite Kinds Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EliteKinds.EnvShow);
            string no = Environment.GetEnvironmentVariable(EliteKinds.EnvNo);
            Environment.SetEnvironmentVariable(EliteKinds.EnvShow, null);
            Environment.SetEnvironmentVariable(EliteKinds.EnvNo, null);

            GameState.ResetAll();
            EliteKinds.ResetForTest();
            DungeonRun.End();

            Check(!EliteKinds.Blocked, "기본은 켜짐");
            Check(EliteKinds.Format(EliteKind.수호자, EliteKind.주술사) == "수호자 · 주술사",
                $"Format 둘 (실제 {EliteKinds.Format(EliteKind.수호자, EliteKind.주술사)})");
            Check(EliteKinds.Format(EliteKind.없음) == "", "없음은 빈 줄");
            Check(EliteKinds.Format((EliteKind[])null) == "", "null은 빈 줄");
            Check(EliteKinds.Line(new WavePlan { EliteKinds = EliteKinds.QaKinds })
                    .IndexOf("수호자", StringComparison.Ordinal) >= 0
                  && EliteKinds.Line(new WavePlan { EliteKinds = EliteKinds.QaKinds })
                    .IndexOf("§10-2", StringComparison.Ordinal) >= 0,
                $"Line (실제 {EliteKinds.Line(new WavePlan { EliteKinds = EliteKinds.QaKinds })})");

            var dummy = new DungeonNode
            {
                Kind = NodeKind.정예,
                Template = ArenaTemplate.open_ring,
                Wave = new WavePlan
                {
                    TargetCount = 80,
                    ElitePercent = 12f,
                    EliteKinds = EliteKinds.QaKinds,
                },
            };
            string cap = EliteKinds.Caption(dummy);
            Check(cap.IndexOf("수호자", StringComparison.Ordinal) >= 0
                  && cap.IndexOf("주술사", StringComparison.Ordinal) >= 0
                  && cap.IndexOf("§10-2", StringComparison.Ordinal) >= 0
                  && cap.IndexOf("%", StringComparison.Ordinal) < 0,
                $"Caption 유형 (실제 {cap})");
            // §522 ✅ 거울 역할이 카드에 읽힌다 — RoleTip을 빼면(빈 문자열) 이름만 남아 FAIL.
            Check(cap.IndexOf("탱", StringComparison.Ordinal) >= 0
                  && cap.IndexOf("최우선 처치", StringComparison.Ordinal) >= 0,
                $"Caption 거울 역할 (실제 {cap})");
            Check(EliteKinds.RoleTip(EliteKind.소환술사) == "소환사 · 본체 끊기"
                  && EliteKinds.RoleTip(EliteKind.없음) == "",
                $"RoleTip 표 (실제 {EliteKinds.RoleTip(EliteKind.소환술사)})");
            Check(EliteKinds.FormatWithRole(EliteKind.주술사).IndexOf("힐 · 최우선 처치", StringComparison.Ordinal) >= 0,
                $"FormatWithRole (실제 {EliteKinds.FormatWithRole(EliteKind.주술사)})");
            string old = EliteKinds.OldCaption(dummy);
            Check(old.IndexOf("12%", StringComparison.Ordinal) >= 0
                  && old.IndexOf("80체", StringComparison.Ordinal) >= 0,
                $"OldCaption 퍼센트 (실제 {old})");

            Environment.SetEnvironmentVariable(EliteKinds.EnvNo, "1");
            Check(EliteKinds.Blocked, "QA_NO");
            Check(EliteKinds.Format(EliteKind.수호자) == "", "차단 Format 빈 줄");
            Check(EliteKinds.Caption(dummy) == old, "차단하면 옛 퍼센트 줄");
            Environment.SetEnvironmentVariable(EliteKinds.EnvNo, null);

            int elites = 0;
            int bad = 0;
            for (uint seed = 1; seed <= 32; seed++)
            {
                var plan = DungeonGenerator.Generate(seed, 0, DungeonKind.일반);
                for (int i = 0; i < plan.Nodes.Length; i++)
                {
                    var n = plan.Nodes[i];
                    if (n.Kind != NodeKind.정예) continue;
                    elites++;
                    int c = EliteKinds.CountOf(n.Wave);
                    if (c < 1 || c > 2) bad++;
                }
            }
            Check(elites > 0, $"생성기 정예 노드 (실제 {elites})");
            Check(bad == 0, $"정예는 1~2종 (불량 {bad}/{elites})");

            Environment.SetEnvironmentVariable(EliteKinds.EnvShow, "1");
            EliteKinds.ResetForTest();
            DungeonRun.End();
            EliteKinds.SeedQaIfRequested();
            Check(EliteKinds.ShowQa, "시드 ShowQa");
            Check(DungeonRun.Active, "시드 던전");
            Check(EliteKinds.Line().IndexOf("수호자", StringComparison.Ordinal) >= 0
                  && EliteKinds.Line().IndexOf("주술사", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {EliteKinds.Line()})");
            bool found = false;
            if (DungeonRun.Active)
            {
                for (int i = 0; i < DungeonRun.Plan.Nodes.Length; i++)
                {
                    var n = DungeonRun.Plan.Nodes[i];
                    if (n.Kind != NodeKind.정예) continue;
                    Check(EliteKinds.Caption(n).IndexOf("수호자", StringComparison.Ordinal) >= 0,
                        $"시드 카드 (실제 {EliteKinds.Caption(n)})");
                    found = true;
                    break;
                }
            }
            Check(found, "시드 정예 노드");
            Environment.SetEnvironmentVariable(EliteKinds.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(mapSrc.IndexOf("EliteKinds.Caption", StringComparison.Ordinal) >= 0,
                "던전 지도가 Caption을 읽는다");
            Check(mapSrc.IndexOf("EliteKinds.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "던전 지도가 시드를 읽는다");
            Check(mapSrc.IndexOf("EliteKinds.Line", StringComparison.Ordinal) >= 0,
                "던전 지도가 Line을 읽는다");

            _ = nameof(EliteKinds.Format);
            _ = nameof(EliteKinds.Caption);
            _ = nameof(EliteKinds.Line);
            _ = nameof(EliteKinds.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(EliteKinds.EnvShow, show);
            Environment.SetEnvironmentVariable(EliteKinds.EnvNo, no);
            EliteKinds.ResetForTest();
            DungeonRun.End();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[EliteKindsSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EliteKindsSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EliteKindsSelfCheck] FAIL {_fail}건");
        }
    }
}
