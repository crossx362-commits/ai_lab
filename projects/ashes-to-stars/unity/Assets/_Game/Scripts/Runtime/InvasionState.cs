using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 침략 본게임 한 슬라이스(§15). 다른 유저 서버가 아니라 로컬 별 수비대와 싸운다.
    /// 출정 비용·승리 약탈·패배 추가 소모·PvP 목숨 예외가 생산 경계다.
    /// 진입 면은 EstateGrid가 고른 최단 4면이다. 랭킹·동맹·경로 전투는 여기서 열지 않는다.
    /// </summary>
    public static class InvasionState
    {
        /// <summary>
        /// 침략당한 직후 보호막 = 수비대 회복(§15 ✅). 한쪽만 바꾸면
        /// "보호막은 끝났는데 수비대는 아직"인 무방비 창이 생긴다.
        /// </summary>
        public const long GuardHours = 12;
        public const long GuardSeconds = GuardHours * 3600;
        public const long DefenseRecoverSeconds = GuardSeconds;
        public const string EnvShow = "QA_INVASION_SHIELD";
        public const string EnvNo = "QA_NO_INVASION_SHIELD";

        const string K_PENDING = "ats.invasion.pending";
        const string K_PAID = "ats.invasion.paid";
        const string K_LAST = "ats.invasion.last_loot";
        const string K_SIDE = "ats.invasion.side";
        const string K_SHIELD = "ats.invasion.shield_until";

        static bool _loaded;
        static bool _pending;
        static long _paid;
        static long _lastLoot;
        static long _shieldUntil;
        static EstateGrid.Side _approach;

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static bool Pending { get { Load(); return _pending; } }
        public static long LastLoot { get { Load(); return _lastLoot; } }
        public static EstateGrid.Side ApproachSide { get { Load(); return _approach; } }
        public static long ShieldUntil { get { Load(); return _shieldUntil; } }
        public static bool ShieldActive => RemainingSeconds() > 0;

        public static bool ShieldBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _pending = PlayerPrefs.GetInt(K_PENDING, 0) == 1;
            long.TryParse(PlayerPrefs.GetString(K_PAID, "0"), out _paid);
            long.TryParse(PlayerPrefs.GetString(K_LAST, "0"), out _lastLoot);
            long.TryParse(PlayerPrefs.GetString(K_SHIELD, "0"), out _shieldUntil);
            int side = PlayerPrefs.GetInt(K_SIDE, (int)EstateGrid.Side.북);
            _approach = (EstateGrid.Side)Mathf.Clamp(side, 0, 3);
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_PENDING, _pending ? 1 : 0);
            PlayerPrefs.SetString(K_PAID, _paid.ToString());
            PlayerPrefs.SetString(K_LAST, _lastLoot.ToString());
            PlayerPrefs.SetString(K_SHIELD, _shieldUntil.ToString());
            PlayerPrefs.SetInt(K_SIDE, (int)_approach);
            PlayerPrefs.Save();
        }

        public static long RemainingSeconds()
        {
            Load();
            long left = _shieldUntil - NowUnix();
            return left > 0 ? left : 0;
        }

        public static string ShieldText()
        {
            long s = RemainingSeconds();
            if (s <= 0) return "";
            if (s >= 3600) return $"{s / 3600}시간 {(s % 3600) / 60}분";
            if (s >= 60) return $"{s / 60}분";
            return $"{s}초";
        }

        public static string ShieldBlockReason()
        {
            if (!ShieldActive) return "";
            return $"보호막 {ShieldText()} — 수비대 회복과 같은 12시간(§15)";
        }

        public static void ArmShield()
        {
            if (ShieldBlocked) return;
            _shieldUntil = NowUnix() + GuardSeconds;
        }

        public static long SortieCost() =>
            Economy.GetActionCost("InvasionAttack", GameState.Tier);

        public static long DefeatCost() =>
            Economy.GetActionCost("InvasionAttackDefeat", GameState.Tier);

        /// <summary>승자 보상은 상대 영지 레벨(여기선 내 탑 층) 기준. 창고를 비워도 준다(§15 1-b).</summary>
        public static long LootCopper()
        {
            long baseLoot = Economy.GetActionCost("InvasionAttack", GameState.Tier) * 3;
            if (baseLoot < 1000) baseLoot = 1000;
            int empty = DefenseState.MaxSlots - DefenseState.Count;
            return EstateDefense.ApplyToLoot(baseLoot + baseLoot * empty / 10);
        }

        public static bool TryBegin()
        {
            Load();
            if (_pending) return false;
            if (ShieldActive) return false;
            if (!GameState.CanInvade()) return false;
            long cost = SortieCost();
            if (!GameState.Pay(cost)) return false;
            _pending = true;
            _paid = cost;
            _lastLoot = 0;
            _approach = EstateGrid.InvaderSide();
            Save();
            return true;
        }

        public static long Settle(bool won)
        {
            Load();
            if (!_pending) return 0;
            _pending = false;
            long loot = 0;
            if (won)
            {
                loot = LootCopper();
                GameState.Earn(loot);
                _lastLoot = loot;
            }
            else
            {
                GameState.Pay(DefeatCost());
                _lastLoot = 0;
            }
            _paid = 0;
            ArmShield();
            Save();
            return loot;
        }

        public static void SeedQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable(EnvShow);
            if (string.IsNullOrEmpty(raw)) return;
            if (ShieldBlocked) return;
            bool show = raw == "1"
                || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            if (!show) return;
            Load();
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            _pending = false;
            ArmShield();
            Save();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_PENDING);
            PlayerPrefs.DeleteKey(K_PAID);
            PlayerPrefs.DeleteKey(K_LAST);
            PlayerPrefs.DeleteKey(K_SIDE);
            PlayerPrefs.DeleteKey(K_SHIELD);
            PlayerPrefs.Save();
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _pending = false;
            _paid = 0;
            _lastLoot = 0;
            _shieldUntil = 0;
            _approach = EstateGrid.Side.북;
            _loaded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _pending = false;
            _paid = _lastLoot = _shieldUntil = 0;
            _approach = EstateGrid.Side.북;
            _loaded = false;
        }
    }
}
