using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 수직 슬라이스 둘째 — 광산 생산을 창고에 자동 적립(§13-2·§18-12).
    /// 필드 수익의 25%(T1=25실버/h). 창고 용량을 넘긴 생산분은 소멸.
    /// 수령 버튼 없음 — 숙제 금지. 사냥 수입은 한도에 안 걸린다.
    /// </summary>
    public static class EstateMine
    {
        public const double FieldShare = 0.25;

        const string K_LAST = "ats.estate.mine_last";
        const string K_WASTE = "ats.estate.mine_waste";
        const string K_OWED = "ats.estate.mine_owed";

        static bool _loaded;
        static bool _qaSeeded;
        static long _lastUnix;
        static long _wasted;
        static long _owed;

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static long CopperPerHour()
        {
            int t = GameState.Tier;
            if (t < 0) t = 0;
            var mul = Economy.TierRevenueMultiplier;
            if (t >= mul.Length) t = mul.Length - 1;
            return (long)(FieldShare * mul[t] * Economy.COPPER_PER_GOLD);
        }

        public static long RoomCopper()
        {
            long cap = EstateBuild.WarehouseCapCopper();
            long have = GameState.Wallet.Copper;
            return have >= cap ? 0 : cap - have;
        }

        public static long WastedCopper
        {
            get { Load(); Tick(); return _wasted; }
        }

        public static long Tick()
        {
            Load();
            long now = NowUnix();
            if (_lastUnix <= 0)
            {
                _lastUnix = now;
                Save();
                return 0;
            }
            if (Disabled() || now <= _lastUnix)
            {
                _lastUnix = now;
                Save();
                return 0;
            }

            long elapsed = now - _lastUnix;
            _lastUnix = now;
            _owed += CopperPerHour() * elapsed;
            long produced = _owed / 3600;
            _owed %= 3600;
            if (produced <= 0)
            {
                Save();
                return 0;
            }

            long room = RoomCopper();
            long add = produced < room ? produced : room;
            long waste = produced - add;
            if (add > 0) GameState.Earn(add);
            if (waste > 0) _wasted += waste;
            Save();
            return add;
        }

        public static void SeedQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable("QA_ESTATE_MINE");
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            ResetForTest();
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long have = GameState.Wallet.Copper;
            if (have > 0) GameState.Pay(have);
            _loaded = true;
            _lastUnix = NowUnix() - 3600;
            Save();
            Tick();
        }

        static bool Disabled()
        {
            string raw = Environment.GetEnvironmentVariable("QA_NO_MINE");
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            long.TryParse(PlayerPrefs.GetString(K_LAST, "0"), out _lastUnix);
            long.TryParse(PlayerPrefs.GetString(K_WASTE, "0"), out _wasted);
            long.TryParse(PlayerPrefs.GetString(K_OWED, "0"), out _owed);
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_LAST, _lastUnix.ToString());
            PlayerPrefs.SetString(K_WASTE, _wasted.ToString());
            PlayerPrefs.SetString(K_OWED, _owed.ToString());
            PlayerPrefs.Save();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_LAST);
            PlayerPrefs.DeleteKey(K_WASTE);
            PlayerPrefs.DeleteKey(K_OWED);
            PlayerPrefs.Save();
            _lastUnix = 0;
            _wasted = 0;
            _owed = 0;
            _loaded = false;
            _qaSeeded = false;
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
        }
    }
}
