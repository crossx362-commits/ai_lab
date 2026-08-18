using System;
using System.Collections.Generic;

namespace AshesToStars
{
    /// <summary>
    /// 정예 유형 1~2종(§10-2 ✅). 생성기는 WavePlan.EliteKinds에 넣는데
    /// 지도가 퍼센트만 보여 소비처가 0곳이었다. QA_NO면 옛 퍼센트 줄.
    /// 전투 기믹은 W3Party라 이 칸 아님.
    /// </summary>
    public static class EliteKinds
    {
        public const string EnvShow = "QA_ELITE_KINDS";
        public const string EnvNo = "QA_NO_ELITE_KINDS";
        public const uint QaSeed = 7;
        public static readonly EliteKind[] QaKinds =
        {
            EliteKind.수호자, EliteKind.주술사,
        };

        static bool _qaSeeded;
        static string _lastLine = "";

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string LastLine => _lastLine ?? "";

        public static int CountOf(WavePlan wave)
        {
            if (wave == null || wave.EliteKinds == null) return 0;
            int n = 0;
            for (int i = 0; i < wave.EliteKinds.Length; i++)
                if (wave.EliteKinds[i] != EliteKind.없음) n++;
            return n;
        }

        public static string Format(params EliteKind[] kinds)
        {
            if (Blocked || kinds == null || kinds.Length == 0) return "";
            var parts = new List<string>(kinds.Length);
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i] == EliteKind.없음) continue;
                parts.Add(kinds[i].ToString());
            }
            return parts.Count == 0 ? "" : string.Join(" · ", parts);
        }

        public static string Format(WavePlan wave) =>
            wave == null ? "" : Format(wave.EliteKinds);

        public static string Line(WavePlan wave)
        {
            string kinds = Format(wave);
            if (string.IsNullOrEmpty(kinds)) return Blocked ? "정예 유형 숨김" : "";
            return kinds + "(§10-2)";
        }

        public static string Line() =>
            Blocked ? "정예 유형 숨김" : (string.IsNullOrEmpty(_lastLine) ? "정예 유형 1~2종(§10-2)" : _lastLine);

        public static string OldCaption(DungeonNode n)
        {
            int count = n?.Wave?.TargetCount ?? 0;
            float pct = n?.Wave?.ElitePercent ?? 0f;
            var tmpl = n == null ? ArenaTemplate.open_ring : n.Template;
            return $"정예 아레나 · 동시 {count}체 · 정예 {pct:F0}% · {tmpl}";
        }

        public static string Caption(DungeonNode n)
        {
            if (n == null || Blocked) return OldCaption(n);
            string kinds = Format(n.Wave);
            if (string.IsNullOrEmpty(kinds)) return OldCaption(n);
            _lastLine = Line(n.Wave);
            return $"정예 아레나 · {kinds}(§10-2)";
        }

        public static bool ApplyQa(DungeonPlan plan)
        {
            if (plan == null || plan.Nodes == null) return false;
            for (int i = 0; i < plan.Nodes.Length; i++)
            {
                var n = plan.Nodes[i];
                if (n == null || n.Kind != NodeKind.정예 || n.Wave == null) continue;
                n.Wave.EliteKinds = (EliteKind[])QaKinds.Clone();
                _lastLine = Line(n.Wave);
                return true;
            }
            return false;
        }

        /// <summary>시각 QA. QA_ELITE_KINDS=1이면 정예 노드에 수호자·주술사를 심는다.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            if (DungeonRun.Active && ApplyQa(DungeonRun.Plan)) return;
            for (uint seed = QaSeed; seed < QaSeed + 48; seed++)
            {
                DungeonRun.End();
                DungeonRun.Begin(seed, 0, DungeonKind.일반, GameFlow.Field);
                if (ApplyQa(DungeonRun.Plan)) return;
            }
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _lastLine = "";
        }
    }
}
