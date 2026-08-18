using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 보스 스킬 수(§10-5 ✅). 기본 2 + 페이즈마다 1.
    /// 옛 길은 층 ≤5 / ≤10 / 나머지라 15층이 4페이즈(2→3→4→5)였다.
    /// 표: 중간(5·15·…) 2→3 · 대보스(10·20·40) 2→3→4 · 50층+ 2→3→4→5.
    /// 탑 자막·HP 바가 PhaseCount를 읽는다. CreateBosses가 PhaseCount를 읽는다.
    /// QA_NO면 옛 ≤5/≤10. W3Party는 안 읽는다.
    /// </summary>
    public static class BossSkills
    {
        public const string EnvShow = "QA_BOSS_SKILLS";
        public const string EnvNo = "QA_NO_BOSS_SKILLS";
        public const int StartSkills = 2;
        public const int QaFloor = 15;

        static bool _qaSeeded;

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

        /// <summary>옛 구간. 15층이 4페이즈가 된다.</summary>
        public static int OldPhaseCount(int floor)
        {
            if (floor <= 5) return 2;
            if (floor <= 10) return 3;
            return 4;
        }

        /// <summary>§10-5 표. 50층+ = 4 · 대보스 = 3 · 중간·그 외 = 2. QA_NO면 옛.</summary>
        public static int PhaseCount(int floor)
        {
            if (Blocked) return OldPhaseCount(floor);
            if (floor >= 50) return 4;
            if (RaidCost.IsMega(floor)) return 3;
            return 2;
        }

        /// <summary>페이즈 0부터. 기본 2 + 페이즈.</summary>
        public static int SkillsAt(int phase)
        {
            if (phase < 0) phase = 0;
            return StartSkills + phase;
        }

        public static int FinalSkills(int floor) =>
            SkillsAt(PhaseCount(floor) - 1);

        public static string Chain(int phases)
        {
            int n = Mathf.Max(1, phases);
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append('→');
                sb.Append(SkillsAt(i));
            }
            return sb.ToString();
        }

        public static string OldChain(int floor) => Chain(OldPhaseCount(floor));

        public static string Label(int floor)
        {
            if (floor >= 50) return "50층+";
            if (RaidCost.IsMega(floor)) return "대보스";
            return "중간";
        }

        public static string OldLine(int floor) =>
            $"{Label(floor)} {OldChain(floor)}(옛)";

        public static string FormatLine(int floor)
        {
            if (Blocked) return OldLine(floor);
            return $"{Label(floor)} {Chain(PhaseCount(floor))}(§10-5)";
        }

        public static string Line() => FormatLine(RaidCost.CurrentRaidFloor());

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.SetTowerFloorForTest(QaFloor);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
