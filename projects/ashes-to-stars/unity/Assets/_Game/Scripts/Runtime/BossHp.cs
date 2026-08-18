using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-11 보스 HP 기준선. 5인 파티 총 DPS × 목표 시간.
    /// 옛 길은 T1 Lv1 고정 100이라 1층과 30층 필요 DPS가 같았다.
    /// 기대 파티는 §18-10 권장 전투력(1층=100, 1~49 +5.5%/층).
    /// 장비 칸은 이 슬라이스에서 권장 전투력에 흡수한다. QA_NO면 옛 100.
    /// W3Party는 안 읽는다.
    /// </summary>
    public static class BossHp
    {
        public const string EnvShow = "QA_BOSS_HP";
        public const string EnvNo = "QA_NO_BOSS_HP";
        public const float BaselineDps = 100f;
        public const float RiseLow = 0.055f;
        public const float RiseHigh = 0.07f;
        public const int RiseSplit = 49;
        public const float TwoMul = 0.65f;
        public const float ThreeMul = 0.45f;

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

        /// <summary>그 층에 도착했을 때 기대하는 레벨. 1층=1 · 30층=30.</summary>
        public static int ExpectedLevel(int floor) =>
            Mathf.Clamp(floor, 1, LifeSystem.MaxLevel);

        /// <summary>§18-10 권장 전투력 배수. 1층=1, 30층≈4.71, 49층≈13.1.</summary>
        public static float FloorMul(int floor)
        {
            if (Blocked) return 1f;
            floor = Mathf.Max(1, floor);
            if (floor <= 1) return 1f;
            if (floor <= RiseSplit)
                return Mathf.Pow(1f + RiseLow, floor - 1);
            float atSplit = Mathf.Pow(1f + RiseLow, RiseSplit - 1);
            return atSplit * Mathf.Pow(1f + RiseHigh, floor - RiseSplit);
        }

        public static float PartyDps(int floor) =>
            BaselineDps * FloorMul(floor);

        public static float CountMul(int bossCount)
        {
            if (bossCount == 2) return TwoMul;
            if (bossCount >= 3) return ThreeMul;
            return 1f;
        }

        public static float Hp(int floor, float targetSec, int bossCount = 1)
        {
            float t = Mathf.Max(0f, targetSec);
            return PartyDps(floor) * t * CountMul(bossCount);
        }

        public static string Line(int floor)
        {
            if (Blocked) return "보스 HP는 고정 DPS 100";
            return $"보스 HP는 기대 파티 {PartyDps(floor):0} DPS(§18-11)";
        }

        public static string Line() => Line(Mathf.Max(1, GameState.TowerFloor));

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.SetTowerFloorForTest(30);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
