using System;

namespace AshesToStars
{
    /// <summary>
    /// 몬스터 계열 직업 상성 ×1.3 / ×0.7(§10-3·§18-11).
    /// 옛 던전 제목은 계열 이름만 보여 배율 소비처가 0곳이었다.
    /// 전투 수치는 W3Party라 이 칸 아님. QA_NO면 옛 `계열` 제목.
    /// </summary>
    public static class FamilyAdv
    {
        public const string EnvShow = "QA_FAMILY_ADV";
        public const string EnvNo = "QA_NO_FAMILY_ADV";
        public const float Strong = 1.3f;
        public const float Weak = 0.7f;
        public const uint QaSeed = 7;

        static readonly (MobFamily Fam, string[] Strong, string[] Weak)[] Table =
        {
            (MobFamily.야수, new[] { "마법사", "정령사" }, new[] { "궁수" }),
            (MobFamily.언데드, new[] { "사제", "드루이드" }, new[] { "검사" }),
            (MobFamily.마족, new[] { "수호기사", "주술사" }, new[] { "마법사" }),
            (MobFamily.기계, new[] { "마법사", "소환사" }, new[] { "검사", "광전사" }),
            (MobFamily.정령, new[] { "궁수", "음유시인" }, new[] { "수호기사", "광전사", "검사" }),
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

        public static float Mul(string job, MobFamily family)
        {
            if (Blocked || string.IsNullOrEmpty(job)) return 1f;
            bool strong = false;
            bool weak = false;
            var names = Expand(job);
            for (int i = 0; i < names.Length; i++)
            {
                if (IsListed(family, names[i], true)) strong = true;
                if (IsListed(family, names[i], false)) weak = true;
            }
            if (strong) return Strong;
            if (weak) return Weak;
            return 1f;
        }

        public static float PartyMul(MobFamily family)
        {
            if (Blocked) return 1f;
            var slots = PartyState.Slots;
            var roster = LifeSystem.GetCharacters();
            bool strong = false;
            bool weak = false;
            for (int i = 0; i < slots.Count; i++)
            {
                int idx = slots[i];
                if (idx < 0 || idx >= roster.Count) continue;
                float m = Mul(roster[idx].Job, family);
                if (m > 1f) strong = true;
                if (m < 1f) weak = true;
            }
            if (strong) return Strong;
            if (weak) return Weak;
            return 1f;
        }

        public static string StrongLabel(MobFamily family)
        {
            var row = RowOf(family);
            return row.Strong == null || row.Strong.Length == 0
                ? ""
                : string.Join("·", row.Strong);
        }

        public static string WeakLabel(MobFamily family)
        {
            var row = RowOf(family);
            return row.Weak == null || row.Weak.Length == 0
                ? ""
                : string.Join("·", row.Weak);
        }

        public static string OldTitle(MobFamily family) => $"던전 · {family} 계열";

        public static string Title(MobFamily family)
        {
            if (Blocked) return OldTitle(family);
            string strong = StrongLabel(family);
            if (string.IsNullOrEmpty(strong)) return OldTitle(family);
            _lastLine = Line(family);
            return $"던전 · {family} · {strong} ×{Strong:0.0}";
        }

        public static string Title() =>
            DungeonRun.Active ? Title(DungeonRun.Plan.Family) : "던전";

        public static string Line(MobFamily family)
        {
            if (Blocked) return $"{family} 계열";
            string strong = StrongLabel(family);
            if (string.IsNullOrEmpty(strong)) return $"{family} 계열";
            return $"{family} · {strong} ×{Strong:0.0}(§10-3)";
        }

        public static string Line() =>
            DungeonRun.Active ? Line(DungeonRun.Plan.Family)
                : (string.IsNullOrEmpty(_lastLine) ? "계열 상성 ×1.3(§10-3)" : _lastLine);

        static (MobFamily Fam, string[] Strong, string[] Weak) RowOf(MobFamily family)
        {
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].Fam == family) return Table[i];
            return default;
        }

        static bool IsListed(MobFamily family, string job, bool strong)
        {
            var row = RowOf(family);
            var list = strong ? row.Strong : row.Weak;
            if (list == null) return false;
            for (int i = 0; i < list.Length; i++)
                if (list[i] == job) return true;
            return false;
        }

        static string[] Expand(string job)
        {
            switch (job)
            {
                case "탱": return new[] { "수호기사", "광전사" };
                case "딜": return new[] { "검사", "궁수" };
                case "마딜": return new[] { "마법사", "소환사" };
                case "힐": return new[] { "사제", "드루이드" };
                case "버퍼": return new[] { "음유시인", "주술사", "정령사" };
                default: return new[] { job };
            }
        }

        /// <summary>시각 QA. T1은 야수만 열린다(§10-3).</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            if (DungeonRun.Active)
            {
                _lastLine = Line(DungeonRun.Plan.Family);
                return;
            }
            DungeonRun.Begin(QaSeed, 0, DungeonKind.일반, GameFlow.Field);
            if (DungeonRun.Active)
                _lastLine = Line(DungeonRun.Plan.Family);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _lastLine = "";
        }
    }
}
