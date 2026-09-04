using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 10층 대보스 입장 0.15 G/h(§18-2). 5층 중간은 0.10.
    /// Economy["Tower10Boss"]를 읽는다. QA_NO면 5층 요금.
    /// </summary>
    public static class RaidCost
    {
        public const string EnvShow = "QA_RAID_MEGA";
        public const string EnvNo = "QA_NO_RAID_MEGA";
        public const string MidKey = "Tower5BossRaid";
        public const string MegaKey = "Tower10Boss";

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsMega(int floor) =>
            floor > 0 && floor % 10 == 0 && floor <= 100;

        /// <summary>차단이면 5층 키. 10·20·…·100만 대보스 키.</summary>
        public static string ActionKey(int floor)
        {
            if (Blocked) return MidKey;
            return IsMega(floor) ? MegaKey : MidKey;
        }

        /// <summary>탑 비용은 해금 티어. 종족 80%는 GetActionCost가 먼저.</summary>
        public static long Copper(int floor) =>
            Economy.GetActionCost(ActionKey(floor), GameState.UnlockedTier);

        public static int CurrentRaidFloor()
        {
            int f = GameState.TowerFloor;
            if (f < 5) return 5;
            return (f / 5) * 5;
        }

        public static string FormatLine(int floor)
        {
            if (!IsMega(floor)) return "";
            if (Blocked) return "대보스 가산 없음";
            return $"대보스 {Economy.FormatCurrency(Copper(floor))}(§18-2)";
        }

        public static string Line() => FormatLine(CurrentRaidFloor());

        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(10);
            long need = Copper(10);
            if (GameState.Wallet.Copper < need)
                GameState.Grant(need);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
