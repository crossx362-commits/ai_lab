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
        public const string EnvShowRace = "QA_RACE_MINE";
        public const string EnvNoRace = "QA_NO_RACE_MINE";
        public const string EnvShowSeize = "QA_MINE_SEIZE";
        public const string EnvNoSeize = "QA_NO_MINE_SEIZE";
        public const int HumanPercent = 100;
        public const int DwarfPercent = 120;
        public const int BeastPercent = 80;
        public const int SeizeOverdue = 2;
        public const int SeizePercent = 100;

        const string K_LAST = "ats.estate.mine_last";
        const string K_WASTE = "ats.estate.mine_waste";
        const string K_OWED = "ats.estate.mine_owed";

        static bool _loaded;
        static bool _qaSeeded;
        static bool _raceQaSeeded;
        static bool _seizeQaSeeded;
        static long _lastSeized;
        static long _lastUnix;
        static long _wasted;
        static long _owed;

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>SelfCheck가 종족 배율을 고정할 때만. 0이면 RaceDef·계정 종족을 본다.</summary>
        public static float ForceRaceMul;

        public static long CopperPerHour()
        {
            int t = GameState.Tier;
            if (t < 0) t = 0;
            var mul = Economy.TierRevenueMultiplier;
            if (t >= mul.Length) t = mul.Length - 1;
            return (long)(FieldShare * mul[t] * Economy.COPPER_PER_GOLD);
        }

        public static bool RaceBlocked =>
            Environment.GetEnvironmentVariable(EnvNoRace) == "1";

        public static bool SeizeBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoSeize);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>§18-5 연체 2회면 영지 생산 100%가 빚으로 간다. 사냥 Earn의 50%와 다르다.</summary>
        public static bool Seized =>
            !SeizeBlocked && GameState.OverdueCount >= SeizeOverdue && GameState.Debt > 0;

        public static long LastSeized { get { Load(); return _lastSeized; } }

        public static string SeizeLine()
        {
            if (SeizeBlocked || !Seized) return "생산 압류 없음";
            return "생산 압류 100%(§18-5)";
        }

        /// <summary>§18-9 드워프 영지 생산 +20% · 수인 −20%. 에셋이 없으면 표로 폴백한다.</summary>
        public static int RacePercent()
        {
            if (RaceBlocked) return HumanPercent;
            if (ForceRaceMul > 0f) return Math.Max(1, (int)Math.Round(ForceRaceMul * 100f));
            try
            {
                var races = Resources.LoadAll<RaceDef>("races");
                RaceId id = RacePrefs.Get();
                for (int i = 0; i < races.Length; i++)
                {
                    if (races[i] != null && races[i].Id == id && races[i].영지생산배율 > 0f)
                        return Math.Max(1, (int)Math.Round(races[i].영지생산배율 * 100f));
                }
            }
            catch
            {
                // 배치 검사 중 에셋 DB가 비면 표로 간다.
            }
            RaceId fallback = RacePrefs.Get();
            if (fallback == RaceId.드워프) return DwarfPercent;
            if (fallback == RaceId.수인) return BeastPercent;
            return HumanPercent;
        }

        public static long ApplyRace(long copper) => copper * RacePercent() / 100;

        public static string RaceLine()
        {
            int p = RacePercent();
            if (p == DwarfPercent) return "드워프 생산 +20%(§18-9)";
            if (p == BeastPercent) return "수인 생산 −20%(§18-9)";
            return "종족 생산 배율 없음";
        }

        public static long RoomCopper()
        {
            long cap = EstateBuild.WarehouseCapCopper();
            long have = GameState.Wallet.Copper;
            return have >= cap ? 0 : cap - have;
        }

        /// <summary>종족 생산 배율 × 영공 아군 버프가 붙은 시간당 생산. 기준 25%는 `CopperPerHour`.</summary>
        public static long CopperPerHourEffective() =>
            (long)(ApplyRace(CopperPerHour()) * WorldStar.AllyMul);

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
            _owed += CopperPerHourEffective() * elapsed;
            long produced = _owed / 3600;
            _owed %= 3600;
            if (produced <= 0)
            {
                _lastSeized = 0;
                Save();
                return 0;
            }

            GameState.RefreshSanctions(now);
            long seized = 0;
            if (Seized)
            {
                seized = GameState.RepayFromIncome(produced);
                produced -= seized;
            }
            _lastSeized = seized;

            long room = RoomCopper();
            long add = produced < room ? produced : room;
            long waste = produced - add;
            if (add > 0) GameState.Earn(add);
            if (waste > 0) _wasted += waste;
            Save();
            return seized + add;
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

        /// <summary>시각 QA. QA_MINE_SEIZE=1이면 연체 2회·빚 1만·1시간분 압류.</summary>
        public static void SeedSeizeQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowSeize) != "1") return;
            if (_seizeQaSeeded) return;
            RacePrefs.Set(RaceId.인간);
            WorldStar.ResetForTest();
            ResetForTest();
            _seizeQaSeeded = true;
            GameState.SeedMineSeizeLoan();
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _loaded = true;
            _lastUnix = NowUnix() - 3600;
            Save();
            Tick();
        }

        /// <summary>시각 QA. QA_RACE_MINE=1이면 드워프·1시간분으로 현황을 연다.</summary>
        public static void SeedRaceQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowRace) != "1") return;
            if (_raceQaSeeded) return;
            RacePrefs.Set(RaceId.드워프);
            WorldStar.ResetForTest();
            ResetForTest();
            _raceQaSeeded = true;
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
            _lastSeized = 0;
            _loaded = false;
            _qaSeeded = false;
            _raceQaSeeded = false;
            _seizeQaSeeded = false;
            ForceRaceMul = 0f;
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
        }
    }
}
