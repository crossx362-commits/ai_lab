using System;
using System.Globalization;

namespace AshesToStars
{
    /// <summary>
    /// 탑 도크 레이드·하위 레이드 부제. 옛 줄은 설명·비용·재입장을 이어 붙여 두 줄로 잘렸다.
    /// QA_NO면 옛 긴 줄. TowerScreen이 읽는다.
    /// </summary>
    public static class TowerDockCap
    {
        public const string EnvShow = "QA_TOWER_DOCK";
        public const string EnvNo = "QA_NO_TOWER_DOCK";
        /// <summary>탑 도크 한 칸. 슬림 카드에서 한 줄.</summary>
        public const int CaptionMaxRunes = 18;
        public const string OldRaidMid = "5층마다 보스, 10층 단위는 대보스(§9)";
        public const string RaidTrain = "비살상 · HP 1 귀환";
        public const string RaidMid = "5층 ×1.5";
        public const string RaidMega = "대보스 ×2.2";

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

        public static string Line() => Blocked
            ? "부제가 두 줄이다"
            : "레이드 부제는 한 줄이다(§16)";

        public static int RuneCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsLowSurrogate(text[i])) n++;
            }
            return n;
        }

        public static bool CaptionFits(string text) =>
            RuneCount(text) <= CaptionMaxRunes;

        /// <summary>옛 소비처 — 훈련 줄 또는 대보스 비용+§9 설명을 이어 붙였다.</summary>
        public static string OldRaid(int floor)
        {
            if (DeathTraining.IsTraining) return DeathTraining.Line();
            string mega = RaidCost.FormatLine(floor);
            return string.IsNullOrEmpty(mega) ? OldRaidMid : mega + " · " + OldRaidMid;
        }

        /// <summary>제목이 레이드라 훈련·벽만. 18자 이하.</summary>
        public static string Raid(int floor)
        {
            if (Blocked) return OldRaid(floor);
            if (DeathTraining.IsTraining) return RaidTrain;
            float w = BossHp.WallMul(floor);
            string wall = w.ToString("0.0", CultureInfo.InvariantCulture);
            if (RaidCost.IsMega(floor))
                return w > 1.01f ? $"대보스 ×{wall}" : "대보스";
            return w > 1.01f ? $"5층 ×{wall}" : "5층 보스";
        }

        /// <summary>옛 소비처 — FormatLine 세 줄을 이어 붙였다.</summary>
        public static string OldLower(int floor) =>
            RaidReroll.FormatLine(floor) + " · " + RaidBossPool.Line()
            + " · " + RaidScale.FormatLine(floor);

        /// <summary>제목이 층이므로 배수·풀·스케일만. 18자 이하.</summary>
        public static string Lower(int floor)
        {
            if (Blocked) return OldLower(floor);
            int mul = RaidReroll.Applies(floor) ? RaidReroll.Multiplier() : 1;
            int n = RaidBossPool.PoolCount;
            int p = RaidScale.ScalePercent();
            string pct = (p / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
            if (n > 0 && p > 0) return $"×{mul} · {n}종 · {pct}";
            if (n > 0) return $"×{mul} · {n}종";
            if (p > 0) return $"×{mul} · {pct}";
            return $"재입장 ×{mul}";
        }

        /// <summary>시각 QA. 51층·T5·2회차라 하위 5층이 ×2 · 10종 · 0.65.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(4);
            if (RaidReroll.NextAttempt() < 2)
                RaidReroll.Record(RaidScale.LowerRaidFloor);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
