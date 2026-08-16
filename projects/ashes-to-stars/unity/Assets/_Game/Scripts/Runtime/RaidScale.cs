using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 하위 레이드 스케일링 계수 0.65(§9·§18-10).
    /// 실난이도·일반 보상 = 원래 + (선택 티어 − 원래) × 0.65.
    /// 최고 기록으로 저층을 강제 상승시키지 않는다. 환생석·증표는 원래 층.
    /// BalanceConfig.스케일링계수를 읽는다. QA_NO면 원래 층만.
    /// </summary>
    public static class RaidScale
    {
        public const string EnvShow = "QA_RAID_SCALE";
        public const string EnvNo = "QA_NO_RAID_SCALE";
        public const int DefaultPercent = 65;
        public const int LowerRaidFloor = 5;
        public const float RewardHourFraction = 0.25f;

        /// <summary>SelfCheck가 계수를 고정할 때만. -1이면 에셋·기본값.</summary>
        public static int ForceScalePercent = -1;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>§18-10 앵커. 에셋 기본값 0.65. 차단이면 0이라 격차를 안 따라간다.</summary>
        public static int ScalePercent()
        {
            if (Blocked) return 0;
            if (ForceScalePercent >= 0) return ForceScalePercent;
            float raw = DefaultPercent / 100f;
            try
            {
                BalanceConfig cfg = ForceConfig;
                bool owned = false;
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                    owned = cfg != null;
                }
                if (cfg != null && cfg.스케일링계수 > 0f)
                    raw = cfg.스케일링계수;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = DefaultPercent / 100f;
            }
            return Mathf.Max(1, Mathf.RoundToInt(raw * 100f));
        }

        /// <summary>세계 티어와 같은 층 매핑. 1~10=T1.</summary>
        public static int NativeTier(int floor) =>
            Mathf.Clamp((Mathf.Max(1, floor) - 1) / 10, 0, 9);

        /// <summary>첫 클리어 보상이 쓰던 층/10. 하위가 아닐 때 숫자를 바꾸지 않는다.</summary>
        public static int LegacyRewardTier(int floor) =>
            Mathf.Clamp(Mathf.Max(0, floor / 10), 0, 9);

        public static bool IsRaidFloor(int floor) => floor >= 5 && floor % 5 == 0 && floor <= 100;

        /// <summary>이미 깬 탑 레이드만. 첫 도전·던전은 원래 층.</summary>
        public static bool Applies(int floor) =>
            !Blocked && IsRaidFloor(floor) && floor < GameState.TowerFloor
            && !(DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon);

        /// <summary>탑에서 여는 저층. 11층부터 5층이 하위가 된다.</summary>
        public static int LowerFloor =>
            GameState.TowerFloor > 10 ? LowerRaidFloor : 0;

        public static long Blend(long original, long current)
        {
            int p = ScalePercent();
            if (p <= 0) return original;
            return original + (current - original) * p / 100;
        }

        public static float Blend(float original, float current)
        {
            int p = ScalePercent();
            if (p <= 0) return original;
            return original + (current - original) * p / 100f;
        }

        public static long RevenueCopper(int tier)
        {
            var mul = Economy.TierRevenueMultiplier;
            if (mul == null || mul.Length == 0) return Economy.COPPER_PER_GOLD;
            if (tier < 0) tier = 0;
            if (tier >= mul.Length) tier = mul.Length - 1;
            return (long)(mul[tier] * Economy.COPPER_PER_GOLD);
        }

        public static long GoldFromTier(int tier) =>
            (long)(RevenueCopper(tier) * RewardHourFraction);

        public static long LegacyGold(int floor) => GoldFromTier(LegacyRewardTier(floor));

        public static long Gold(int floor)
        {
            if (!Applies(floor)) return LegacyGold(floor);
            return Blend(GoldFromTier(NativeTier(floor)), GoldFromTier(GameState.Tier));
        }

        public static long ExpFromTier(int tier)
        {
            var mul = Economy.TierRevenueMultiplier;
            if (mul == null || mul.Length == 0) return 100;
            if (tier < 0) tier = 0;
            if (tier >= mul.Length) tier = mul.Length - 1;
            long exp = (long)(mul[tier] * 100f);
            return exp < 1 ? 1 : exp;
        }

        public static long LegacyExp(int floor) => ExpFromTier(LegacyRewardTier(floor));

        public static long Exp(int floor)
        {
            if (!Applies(floor)) return LegacyExp(floor);
            return Blend(ExpFromTier(NativeTier(floor)), ExpFromTier(GameState.Tier));
        }

        public static float TimeForFloor(int floor)
        {
            if (floor <= 5) return 90f;
            if (floor <= 10) return 180f;
            return 300f;
        }

        public static float TimeForTier(int tier)
        {
            if (tier <= 0) return 90f;
            if (tier == 1) return 180f;
            return 300f;
        }

        /// <summary>BossBattle.Begin 덮어쓰기. 0이면 층 기본값.</summary>
        public static float TargetSeconds(int floor)
        {
            if (!Applies(floor)) return 0f;
            float scaled = Blend(TimeForFloor(floor), TimeForTier(GameState.Tier));
            return scaled > 0f ? scaled : 0f;
        }

        public static string Line(int floor) => FormatLine(floor);

        public static string Line()
        {
            int floor = LowerFloor;
            if (floor <= 0) return "";
            return FormatLine(floor);
        }

        public static string FormatLine(int floor)
        {
            if (Blocked) return "하위 레이드 스케일 없음";
            int from = NativeTier(floor) + 1;
            int to = GameState.Tier + 1;
            string pct = (ScalePercent() / 100.0).ToString("0.00",
                System.Globalization.CultureInfo.InvariantCulture);
            return $"하위 레이드 스케일 {pct}(§18-10) · {floor}층 T{from}→T{to} · "
                + Economy.FormatCurrency(Gold(floor));
        }

        /// <summary>시각 QA. 51층·선택 T5면 5층 골드가 1골드 15실버 23쿠퍼.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(4);
        }

        public static void ResetForTest()
        {
            ForceScalePercent = -1;
            ForceConfig = null;
            _qaSeeded = false;
        }
    }
}
