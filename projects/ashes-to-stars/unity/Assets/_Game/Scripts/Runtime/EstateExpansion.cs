using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-12 부지 확장. 원장 확정: 초기 격자 8×8 → 부지 확장 최대 16×16,
    /// 해금은 20층 / 50층 / 80층. 중간 크기(1·2단계)는 원장 미확정이라
    /// 두 끝점(8·16) 사이를 계단으로 잡았다(11·13) — Tooltip·주석에 명시.
    ///
    /// 소비처: EstateGrid.Size(논리 격자 폭)가 CurrentSize()를 읽는다.
    /// 해금 층은 GameState.TowerFloor(단조 증가·최고 기록) 기준.
    /// QA_NO_ESTATE_EXPAND면 확장 없음(항상 8×8) — 네거티브 컨트롤.
    /// 수치 튜닝(§21-3) 방어: 해금 층이 0 이하거나 비단조면 원장 기본(20/50/80)으로 폴백.
    /// </summary>
    public static class EstateExpansion
    {
        public const string EnvNo = "QA_NO_ESTATE_EXPAND";

        public const int DefaultFloor1 = 20;
        public const int DefaultFloor2 = 50;
        public const int DefaultFloor3 = 80;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

        /// <summary>테스트가 격자 폭을 고정할 때만(&gt;0이면 층 무시). EstateGrid.ResetForTest가 기본으로 앉힌다.</summary>
        public static int ForceSizeForTest;

        static bool _gatesCached;
        static int _g1 = DefaultFloor1, _g2 = DefaultFloor2, _g3 = DefaultFloor3;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>해금 층 3단계(20/50/80 기본). 비정상 수치는 원장 기본으로 폴백(§21-3).</summary>
        public static (int, int, int) Gates()
        {
            if (ForceConfig != null)
                return SanitizeGates(ForceConfig.부지확장해금층1, ForceConfig.부지확장해금층2, ForceConfig.부지확장해금층3);
            if (_gatesCached)
                return (_g1, _g2, _g3);
            int a = DefaultFloor1, b = DefaultFloor2, c = DefaultFloor3;
            try
            {
                var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                if (cfg != null)
                {
                    (a, b, c) = SanitizeGates(cfg.부지확장해금층1, cfg.부지확장해금층2, cfg.부지확장해금층3);
                    UnityEngine.Object.DestroyImmediate(cfg);
                }
            }
            catch
            {
                a = DefaultFloor1; b = DefaultFloor2; c = DefaultFloor3;
            }
            _g1 = a; _g2 = b; _g3 = c;
            _gatesCached = true;
            return (_g1, _g2, _g3);
        }

        static (int, int, int) SanitizeGates(int a, int b, int c)
        {
            // 0 이하·비단조(오름차순 아님)면 원장 기본으로 통째 폴백 — 부분 신뢰 금지.
            if (a <= 0 || b <= 0 || c <= 0 || !(a < b && b < c))
                return (DefaultFloor1, DefaultFloor2, DefaultFloor3);
            return (a, b, c);
        }

        /// <summary>최고 기록 층으로 해금된 확장 단계(0~3).</summary>
        public static int UnlockedTiers(int floor)
        {
            var (g1, g2, g3) = Gates();
            int t = 0;
            if (floor >= g1) t = 1;
            if (floor >= g2) t = 2;
            if (floor >= g3) t = 3;
            return t;
        }

        /// <summary>확장 단계별 격자 폭. 8(초기) → 16(최대). 중간(11·13) 원장 미확정.</summary>
        public static int SizeForTier(int tier)
        {
            switch (tier)
            {
                case 0: return EstateGrid.BaseSize;   // 8
                case 1: return 11;
                case 2: return 13;
                default: return EstateGrid.MaxSize;   // 16
            }
        }

        /// <summary>지금 쓸 수 있는 논리 격자 폭. 소비처: EstateGrid.Size.</summary>
        public static int CurrentSize()
        {
            if (ForceSizeForTest > 0)
                return Mathf.Clamp(ForceSizeForTest, EstateGrid.BaseSize, EstateGrid.MaxSize);
            if (Blocked)
                return EstateGrid.BaseSize;
            int floor = SafeFloor();
            return SizeForTier(UnlockedTiers(floor));
        }

        static int SafeFloor()
        {
            try { return Mathf.Max(1, GameState.TowerFloor); }
            catch { return 1; }
        }

        public static void ResetForTest()
        {
            ForceConfig = null;
            ForceSizeForTest = 0;
            _gatesCached = false;
            _g1 = DefaultFloor1;
            _g2 = DefaultFloor2;
            _g3 = DefaultFloor3;
        }
    }
}
