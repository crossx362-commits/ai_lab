using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 수직 슬라이스 셋째 — 방어 건물 4종(§13-2·§18-12).
    /// 화살탑→마법탑→성벽→함정. 20층부터. 비용·시간은 본성의 40%.
    /// 수비대 0명이면 효율이 절반(§13-5). 단축은 EstateRush. 격자는 다음.
    /// </summary>
    public static class EstateDefense
    {
        public enum Kind { 화살탑, 마법탑, 성벽, 함정 }

        public const int UnlockFloor = 20;
        public const int CostShare = 40;
        public const int EmptyEfficiency = 50;
        public const int CutPerLevel = 5;
        public const int CutCap = 40;

        public static readonly Kind[] All =
        {
            Kind.화살탑, Kind.마법탑, Kind.성벽, Kind.함정,
        };

        const string K_LV = "ats.estate.def.lv.";
        const string K_BUSY = "ats.estate.def.busy";
        const string K_TO = "ats.estate.def.to";
        const string K_DONE = "ats.estate.def.done";
        const string K_ORIG = "ats.estate.def.orig";
        const string K_JOB = "ats.estate.def.job";

        static bool _loaded;
        static bool _qaSeeded;
        static readonly int[] _lv = new int[4];
        static int _busy = -1;
        static int _to;
        static long _doneUnix;
        static long _origSec;
        static long _jobCost;

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static Func<int> GarrisonCount = () => DefenseState.Count;

        public static bool Busy { get { Load(); Tick(); return _busy >= 0; } }
        public static Kind? BusyKind
        {
            get
            {
                Load();
                Tick();
                return _busy >= 0 ? (Kind)_busy : (Kind?)null;
            }
        }

        public static int Level(Kind k)
        {
            Load();
            Tick();
            return _lv[(int)k];
        }

        public static int TotalLevel()
        {
            Load();
            Tick();
            int n = 0;
            for (int i = 0; i < _lv.Length; i++) n += _lv[i];
            return n;
        }

        public static long UpgradeCost(int fromLevel)
        {
            return EstateBuild.UpgradeCost(Math.Max(1, fromLevel)) * CostShare / 100;
        }

        public static double UpgradeSeconds(int fromLevel)
        {
            return EstateBuild.UpgradeSeconds(Math.Max(1, fromLevel)) * CostShare / 100.0;
        }

        public static long RemainingSeconds()
        {
            Load();
            Tick();
            if (_busy < 0) return 0;
            long left = _doneUnix - NowUnix();
            return left > 0 ? left : 0;
        }

        public static string RemainingText()
        {
            long s = RemainingSeconds();
            if (s <= 0) return "";
            if (s >= 3600) return $"{s / 3600}시간 {(s % 3600) / 60}분";
            if (s >= 60) return $"{s / 60}분 {s % 60}초";
            return $"{s}초";
        }

        public static int EfficiencyPercent()
        {
            if (Disabled()) return 0;
            return GarrisonCount() > 0 ? 100 : EmptyEfficiency;
        }

        public static int CutPercent()
        {
            if (Disabled()) return 0;
            int cut = TotalLevel() * CutPerLevel;
            if (GarrisonCount() <= 0) cut /= 2;
            if (cut > CutCap) cut = CutCap;
            return cut;
        }

        public static long ApplyToLoot(long loot)
        {
            if (loot <= 0) return loot;
            int cut = CutPercent();
            if (cut <= 0) return loot;
            return loot * (100 - cut) / 100;
        }

        public static string WhyCannotStart(Kind k)
        {
            Load();
            Tick();
            if (Disabled()) return "방어 건물이 꺼져 있다";
            if (GameState.TowerFloor < UnlockFloor)
                return $"탑 {UnlockFloor}층부터 순차 해금(현재 {GameState.TowerFloor}층)(§13-2)";
            if ((int)k > 0 && _lv[(int)k - 1] < 1)
                return $"먼저 {All[(int)k - 1]}을(를) 세운다(§13-2 순차)";
            int lv = _lv[(int)k];
            if (lv >= EstateBuild.KeepLevel)
                return $"본성 Lv{EstateBuild.KeepLevel}이 상한이다(§18-12)";
            if (_busy >= 0) return "방어 공사가 끝나지 않았다";
            long cost = UpgradeCost(lv);
            if (GameState.Wallet.Copper < cost)
                return $"골드가 부족하다 — {Economy.FormatCurrency(cost)}";
            return null;
        }

        public static bool TryStart(Kind k)
        {
            Load();
            Tick();
            if (WhyCannotStart(k) != null) return false;
            int lv = _lv[(int)k];
            long cost = UpgradeCost(lv);
            if (!GameState.Pay(cost)) return false;
            _busy = (int)k;
            _to = lv + 1;
            double wait = UpgradeSeconds(lv);
            if (FastQa()) wait = 1;
            _origSec = (long)Math.Ceiling(wait);
            _jobCost = cost;
            _doneUnix = NowUnix() + _origSec;
            Save();
            return true;
        }

        public static long RushableSeconds()
        {
            Load();
            Tick();
            return EstateRush.Rushable(RemainingSeconds(), OriginalSeconds());
        }

        public static long GoldCostToFloor()
        {
            return EstateRush.GoldCost(RushableSeconds(), JobCost());
        }

        public static string WhyCannotRushGold()
        {
            Load();
            Tick();
            if (EstateRush.Disabled()) return "단축이 꺼져 있다";
            if (_busy < 0) return "공사가 없다";
            if (RushableSeconds() <= 0) return "남은 시간의 50%가 바닥이다(§13-2)";
            long pay = GoldCostToFloor();
            if (GameState.Wallet.Copper < pay)
                return $"골드가 부족하다 — {Economy.FormatCurrency(pay)}";
            return null;
        }

        public static string WhyCannotRushMaterial(Economy.LifeItem item)
        {
            Load();
            Tick();
            if (EstateRush.Disabled()) return "단축이 꺼져 있다";
            if (_busy < 0) return "공사가 없다";
            if (RushableSeconds() <= 0) return "남은 시간의 50%가 바닥이다(§13-2)";
            if (EstateRush.IsForbidden(item)) return "목숨 아이템·강화석은 단축에 못 쓴다(§13-2)";
            if (GameState.Bag.GetCount(item) < 1) return $"{GameState.Label(item)}이(가) 없다";
            return null;
        }

        public static bool TryRushGold()
        {
            Load();
            Tick();
            if (WhyCannotRushGold() != null) return false;
            long cut = RushableSeconds();
            long pay = EstateRush.GoldCost(cut, JobCost());
            if (!GameState.Pay(pay)) return false;
            _doneUnix -= cut;
            Save();
            return true;
        }

        public static bool TryRushMaterial(Economy.LifeItem item, int count)
        {
            Load();
            Tick();
            if (count <= 0) return false;
            if (WhyCannotRushMaterial(item) != null) return false;
            long rem = RemainingSeconds();
            long cut = EstateRush.MaterialCut(rem, count);
            long cap = RushableSeconds();
            if (cut > cap) cut = cap;
            if (cut <= 0) return false;
            if (!GameState.Consume(item, count)) return false;
            _doneUnix -= cut;
            Save();
            return true;
        }

        public static void Tick()
        {
            Load();
            if (_busy < 0) return;
            if (NowUnix() < _doneUnix) return;
            _lv[_busy] = _to;
            _busy = -1;
            _to = 0;
            _doneUnix = 0;
            _origSec = 0;
            _jobCost = 0;
            Save();
        }

        static long OriginalSeconds()
        {
            Load();
            if (_origSec > 0) return _origSec;
            if (_busy < 0) return 0;
            return RemainingSeconds();
        }

        static long JobCost()
        {
            Load();
            if (_jobCost > 0) return _jobCost;
            int lv = _busy >= 0 ? _lv[_busy] : 0;
            return UpgradeCost(lv);
        }

        public static void SeedQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable("QA_ESTATE_DEFENSE");
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            if (_qaSeeded) return;
            ResetForTest();
            _qaSeeded = true;
            GameState.SetTowerFloorForTest(UnlockFloor);
            _loaded = true;
            _lv[0] = 1;
            Save();
            Tick();
        }

        static bool FastQa()
        {
            string raw = Environment.GetEnvironmentVariable("QA_ESTATE_DEFENSE_FAST");
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        static bool Disabled()
        {
            string raw = Environment.GetEnvironmentVariable("QA_NO_DEFENSE");
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < _lv.Length; i++)
                _lv[i] = Mathf.Max(0, PlayerPrefs.GetInt(K_LV + i, 0));
            _busy = PlayerPrefs.GetInt(K_BUSY, -1);
            _to = PlayerPrefs.GetInt(K_TO, 0);
            long.TryParse(PlayerPrefs.GetString(K_DONE, "0"), out _doneUnix);
            long.TryParse(PlayerPrefs.GetString(K_ORIG, "0"), out _origSec);
            long.TryParse(PlayerPrefs.GetString(K_JOB, "0"), out _jobCost);
        }

        static void Save()
        {
            for (int i = 0; i < _lv.Length; i++)
                PlayerPrefs.SetInt(K_LV + i, _lv[i]);
            PlayerPrefs.SetInt(K_BUSY, _busy);
            PlayerPrefs.SetInt(K_TO, _to);
            PlayerPrefs.SetString(K_DONE, _doneUnix.ToString());
            PlayerPrefs.SetString(K_ORIG, _origSec.ToString());
            PlayerPrefs.SetString(K_JOB, _jobCost.ToString());
            PlayerPrefs.Save();
        }

        public static void ResetForTest()
        {
            for (int i = 0; i < 4; i++)
                PlayerPrefs.DeleteKey(K_LV + i);
            PlayerPrefs.DeleteKey(K_BUSY);
            PlayerPrefs.DeleteKey(K_TO);
            PlayerPrefs.DeleteKey(K_DONE);
            PlayerPrefs.DeleteKey(K_ORIG);
            PlayerPrefs.DeleteKey(K_JOB);
            PlayerPrefs.Save();
            for (int i = 0; i < _lv.Length; i++) _lv[i] = 0;
            _busy = -1;
            _to = 0;
            _doneUnix = 0;
            _origSec = 0;
            _jobCost = 0;
            _loaded = false;
            _qaSeeded = false;
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            GarrisonCount = () => DefenseState.Count;
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
        }

        public static void SetLevelForTest(Kind k, int lv)
        {
            Load();
            _lv[(int)k] = Mathf.Max(0, lv);
            Save();
        }
    }
}
