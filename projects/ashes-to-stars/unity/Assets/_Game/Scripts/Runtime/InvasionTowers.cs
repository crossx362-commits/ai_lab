using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 침략 전투에서 화살탑·마법탑이 쏜다. 수비는 내 건물, 출정은 더미 적 탑.
    /// 성벽은 받는 피해, 함정은 스폰 폭발. QA_NO_INVASION_TOWERS면 옛 잡몹 웨이브.
    /// </summary>
    public static class InvasionTowers
    {
        public const string EnvNo = "QA_NO_INVASION_TOWERS";
        public const float ArrowDmgPerLevel = 6f;
        public const float MagicDmgPerLevel = 4f;
        public const float TrapDmgPerLevel = 8f;
        public const float WallCutPerLevel = 0.04f;
        public const float WallCutCap = 0.28f;
        public const float ArrowInterval = 0.9f;
        public const float MagicInterval = 1.6f;
        public const float ArrowRange = 10f;
        public const float MagicRange = 8.5f;
        public const float MagicRadius = 2.2f;
        public const int MagicMaxTargets = 3;

        public static Func<bool> FriendlyNow = () => InboundRaid.Fighting;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool InPlay =>
            !Blocked && GameFlow.Kind == GameFlow.BattleKind.침략;

        public static int DummyLevel => 1 + GameState.TowerFloor / 15;

        public static float Efficiency
        {
            get
            {
                if (!InPlay) return 0f;
                if (!FriendlyNow()) return 1f;
                return EstateDefense.EfficiencyPercent() / 100f;
            }
        }

        public static int ArrowLv =>
            !InPlay ? 0 : FriendlyNow() ? EstateDefense.Level(EstateDefense.Kind.화살탑) : DummyLevel;

        public static int MagicLv =>
            !InPlay ? 0 : FriendlyNow() ? EstateDefense.Level(EstateDefense.Kind.마법탑) : DummyLevel;

        public static int WallLv =>
            !InPlay || !FriendlyNow() ? 0 : EstateDefense.Level(EstateDefense.Kind.성벽);

        public static int TrapLv =>
            !InPlay || !FriendlyNow() ? 0 : EstateDefense.Level(EstateDefense.Kind.함정);

        public static float ArrowDmg => ArrowLv * ArrowDmgPerLevel * Efficiency;
        public static float MagicDmg => MagicLv * MagicDmgPerLevel * Efficiency;
        public static float TrapBurst => TrapLv * TrapDmgPerLevel * Efficiency;

        public static float WallTakenMul
        {
            get
            {
                if (WallLv <= 0) return 1f;
                float cut = WallLv * WallCutPerLevel * Efficiency;
                if (cut > WallCutCap) cut = WallCutCap;
                return 1f - cut;
            }
        }

        public static Vector2 ArrowPos(int slot, bool friendly)
        {
            float y = friendly ? -6f : 6f;
            return new Vector2(slot == 0 ? -5f : 5f, y);
        }

        public static Vector2 MagicPos(bool friendly) =>
            new Vector2(0f, friendly ? -6.5f : 6.5f);

        public static void ResetForTest()
        {
            FriendlyNow = () => InboundRaid.Fighting;
        }
    }
}
