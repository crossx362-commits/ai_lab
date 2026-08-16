using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 수직 슬라이스 첫 조각 — 본성 레벨과 건설 시간(§13-2·§18-12).
    /// 완료는 수령 버튼 없이 시각이 되면 적용된다(숙제 금지).
    /// </summary>
    public static class EstateBuild
    {
        public const int MaxKeep = 13;
        public const double BaseSeconds = 5.0 * 60.0;
        public const double TimeMul = 1.6;
        public const double CapSeconds = 24.0 * 3600.0;
        public const double CostGoldPerHour = 8.0;
        public const double CostMul = 1.5;

        const string K_LV = "ats.estate.keep";
        const string K_TO = "ats.estate.keep_to";
        const string K_DONE = "ats.estate.keep_done";
        const string K_ORIG = "ats.estate.keep_orig";
        const string K_JOB = "ats.estate.keep_job";

        static bool _loaded;
        static bool _qaRushSeeded;
        static int _level = 1;
        static int _to;
        static long _doneUnix;
        static long _origSec;
        static long _jobCost;

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static int KeepLevel { get { Load(); Tick(); return _level; } }
        public static bool KeepBusy { get { Load(); Tick(); return _to > _level; } }
        public static int KeepTarget { get { Load(); Tick(); return _to > _level ? _to : _level; } }

        public static double UpgradeSeconds(int fromLevel)
        {
            if (fromLevel < 1) fromLevel = 1;
            double sec = BaseSeconds * Math.Pow(TimeMul, fromLevel - 1);
            if (sec > CapSeconds) sec = CapSeconds;
            return sec;
        }

        public static long UpgradeCost(int fromLevel)
        {
            if (fromLevel < 1) fromLevel = 1;
            double goldHours = CostGoldPerHour * Math.Pow(CostMul, fromLevel);
            return (long)(goldHours * Economy.COPPER_PER_GOLD);
        }

        public static long WarehouseCapCopper()
        {
            Load();
            Tick();
            return _level * 12L * Economy.COPPER_PER_GOLD;
        }

        public static long RemainingSeconds()
        {
            Load();
            Tick();
            if (_to <= _level) return 0;
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

        public static string WhyCannotUpgrade()
        {
            Load();
            Tick();
            if (_level >= MaxKeep) return "본성 상한이다(§13-2)";
            if (_to > _level) return "본성 공사가 끝나지 않았다";
            long cost = UpgradeCost(_level);
            if (GameState.Wallet.Copper < cost)
                return $"골드가 부족하다 — {Economy.FormatCurrency(cost)}";
            return null;
        }

        public static bool TryStartKeep()
        {
            Load();
            Tick();
            if (WhyCannotUpgrade() != null) return false;
            long cost = UpgradeCost(_level);
            if (!GameState.Pay(cost)) return false;
            _to = _level + 1;
            double wait = UpgradeSeconds(_level);
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
            if (_to <= _level) return "공사가 없다";
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
            if (_to <= _level) return "공사가 없다";
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

        public static void SeedRushQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable("QA_ESTATE_RUSH");
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            if (_qaRushSeeded) return;
            ResetForTest();
            _qaRushSeeded = true;
            long now = 1_700_000_000;
            NowUnix = () => now;
            _loaded = true;
            _level = 1;
            long cost = UpgradeCost(1);
            GameState.Earn(cost + EstateRush.GoldCost(150, cost) + 1);
            TryStartKeep();
            GameState.Gain(Economy.LifeItem.CraftHide, 3);
        }

        public static void Tick()
        {
            Load();
            if (_to <= _level) return;
            if (NowUnix() < _doneUnix) return;
            _level = _to;
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
            if (_to <= _level) return 0;
            return RemainingSeconds();
        }

        static long JobCost()
        {
            Load();
            if (_jobCost > 0) return _jobCost;
            return UpgradeCost(Math.Max(1, _level));
        }

        static bool FastQa()
        {
            string raw = Environment.GetEnvironmentVariable("QA_ESTATE_KEEP_FAST");
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _level = Mathf.Max(1, PlayerPrefs.GetInt(K_LV, 1));
            _to = PlayerPrefs.GetInt(K_TO, 0);
            long.TryParse(PlayerPrefs.GetString(K_DONE, "0"), out _doneUnix);
            long.TryParse(PlayerPrefs.GetString(K_ORIG, "0"), out _origSec);
            long.TryParse(PlayerPrefs.GetString(K_JOB, "0"), out _jobCost);
            if (_level > MaxKeep) _level = MaxKeep;
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_LV, _level);
            PlayerPrefs.SetInt(K_TO, _to);
            PlayerPrefs.SetString(K_DONE, _doneUnix.ToString());
            PlayerPrefs.SetString(K_ORIG, _origSec.ToString());
            PlayerPrefs.SetString(K_JOB, _jobCost.ToString());
            PlayerPrefs.Save();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_LV);
            PlayerPrefs.DeleteKey(K_TO);
            PlayerPrefs.DeleteKey(K_DONE);
            PlayerPrefs.DeleteKey(K_ORIG);
            PlayerPrefs.DeleteKey(K_JOB);
            PlayerPrefs.Save();
            _level = 1;
            _to = 0;
            _doneUnix = 0;
            _origSec = 0;
            _jobCost = 0;
            _loaded = false;
            _qaRushSeeded = false;
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
        }
    }
}
