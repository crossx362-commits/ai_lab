using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 침략 본게임 한 슬라이스(§15). 다른 유저 서버가 아니라 로컬 별 수비대와 싸운다.
    /// 출정 비용·승리 약탈·패배 추가 소모·PvP 목숨 예외가 생산 경계다.
    /// 4면 공성·랭킹·동맹은 여기서 열지 않는다.
    /// </summary>
    public static class InvasionState
    {
        const string K_PENDING = "ats.invasion.pending";
        const string K_PAID = "ats.invasion.paid";
        const string K_LAST = "ats.invasion.last_loot";

        static bool _loaded;
        static bool _pending;
        static long _paid;
        static long _lastLoot;

        public static bool Pending { get { Load(); return _pending; } }
        public static long LastLoot { get { Load(); return _lastLoot; } }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _pending = PlayerPrefs.GetInt(K_PENDING, 0) == 1;
            long.TryParse(PlayerPrefs.GetString(K_PAID, "0"), out _paid);
            long.TryParse(PlayerPrefs.GetString(K_LAST, "0"), out _lastLoot);
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_PENDING, _pending ? 1 : 0);
            PlayerPrefs.SetString(K_PAID, _paid.ToString());
            PlayerPrefs.SetString(K_LAST, _lastLoot.ToString());
            PlayerPrefs.Save();
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
            if (!GameState.CanInvade()) return false;
            long cost = SortieCost();
            if (!GameState.Pay(cost)) return false;
            _pending = true;
            _paid = cost;
            _lastLoot = 0;
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
            Save();
            return loot;
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_PENDING);
            PlayerPrefs.DeleteKey(K_PAID);
            PlayerPrefs.DeleteKey(K_LAST);
            PlayerPrefs.Save();
            _pending = false;
            _paid = 0;
            _lastLoot = 0;
            _loaded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _pending = false;
            _paid = _lastLoot = 0;
            _loaded = false;
        }
    }
}
