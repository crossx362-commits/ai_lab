using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 침략자가 진입 면을 고른다(§13-3 ✅).
    /// 옛 경로는 EstateGrid.InvaderSide — 가장 짧은 면을 시스템이 골랐다.
    /// 한 면만 요새화하면 반대가 뚫리려면 **공격자가** 면을 고를 수 있어야 한다.
    /// 경로 전투 시뮬은 💡라 안 넣는다. QA_NO면 옛 최단 자동.
    /// </summary>
    public static class InvasionApproach
    {
        public const string EnvShow = "QA_INVASION_APPROACH";
        public const string EnvNo = "QA_NO_INVASION_APPROACH";

        static bool _has;
        static EstateGrid.Side _side;
        static bool _picking;
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

        public static bool HasPick => !Blocked && _has;

        public static bool Picking
        {
            get => !Blocked && _picking;
            set { if (!Blocked) _picking = value; }
        }

        /// <summary>고른 면. 안 골랐거나 QA_NO면 최단 자동.</summary>
        public static EstateGrid.Side Side =>
            HasPick ? _side : EstateGrid.InvaderSide();

        public static int Path() => EstateGrid.PathLength(Side);

        public static int PathOf(EstateGrid.Side side) => EstateGrid.PathLength(side);

        public static bool CanPick(EstateGrid.Side side) => PathOf(side) >= 0;

        public static void Pick(EstateGrid.Side side)
        {
            if (Blocked) return;
            if (!CanPick(side)) return;
            _has = true;
            _side = side;
            _picking = false;
        }

        public static string Line()
        {
            if (Blocked) return "진입 면은 최단 자동(§13-3)";
            if (Picking) return "진입 면을 고른다(§13-3)";
            return $"진입 면 {Side} {Path()}칸(§13-3)";
        }

        public static string CardTitle(EstateGrid.Side side) => side.ToString();

        public static string CardBody(EstateGrid.Side side)
        {
            int n = PathOf(side);
            if (n < 0) return "막힘 — 이 면으로는 창고에 못 간다";
            return $"{n}칸 — 이 면으로 들어간다(§13-3)";
        }

        /// <summary>시각 QA. QA_INVASION_APPROACH=1이면 30층·남면 고르기 화면.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            if (GameState.Wallet.Copper < 10_000)
                GameState.Grant(10_000);
            InvasionState.ResetForTest();
            _has = true;
            _side = EstateGrid.Side.남;
            _picking = true;
        }

        public static void ResetForTest()
        {
            _has = false;
            _side = EstateGrid.Side.북;
            _picking = false;
            _qaSeeded = false;
        }
    }
}
