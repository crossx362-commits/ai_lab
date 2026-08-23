using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 핵심 건물(IsCore)별 레벨·공사(GAME_SPEC_ESTATE_BUILD §2-3·§13-2·§18-12).
    /// 본성 API는 보존하고 Cell.Keep으로 위임한다. 방어 건물은 EstateDefense.
    /// 완료는 수령 버튼 없이 시각이 되면 적용된다(숙제 금지). 칸마다 병렬 공사 OK.
    /// </summary>
    public static class EstateBuild
    {
        public const int MaxKeep = 13;
        public const double BaseSeconds = 5.0 * 60.0;
        public const double TimeMul = 1.6;
        public const double CapSeconds = 24.0 * 3600.0;
        public const double CostGoldPerHour = 8.0;
        public const double CostMul = 1.5;

        const string PrefPrefix = "ats.estate.b.";
        const string OldK_LV = "ats.estate.keep";
        const string OldK_TO = "ats.estate.keep_to";
        const string OldK_DONE = "ats.estate.keep_done";
        const string OldK_ORIG = "ats.estate.keep_orig";
        const string OldK_JOB = "ats.estate.keep_job";

        static readonly EstateGrid.Cell[] Cores =
        {
            EstateGrid.Cell.Keep,
            EstateGrid.Cell.Mine,
            EstateGrid.Cell.Warehouse,
            EstateGrid.Cell.Smith,
            EstateGrid.Cell.Auction,
            EstateGrid.Cell.Mausoleum,
            EstateGrid.Cell.Barracks,
        };

        static bool _loaded;
        static bool _qaRushSeeded;
        static readonly int[] _level = new int[Cores.Length];
        static readonly int[] _to = new int[Cores.Length];
        static readonly long[] _doneUnix = new long[Cores.Length];
        static readonly long[] _origSec = new long[Cores.Length];
        static readonly long[] _jobCost = new long[Cores.Length];

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static int KeepLevel => Level(EstateGrid.Cell.Keep);
        public static bool KeepBusy => Busy(EstateGrid.Cell.Keep);
        public static int KeepTarget => Target(EstateGrid.Cell.Keep);

        public static int Level(EstateGrid.Cell c)
        {
            Load();
            Tick();
            int i = IndexOf(c);
            return i < 0 ? 0 : _level[i];
        }

        public static bool Busy(EstateGrid.Cell c)
        {
            Load();
            Tick();
            int i = IndexOf(c);
            return i >= 0 && _to[i] > _level[i];
        }

        public static int Target(EstateGrid.Cell c)
        {
            Load();
            Tick();
            int i = IndexOf(c);
            if (i < 0) return 0;
            return _to[i] > _level[i] ? _to[i] : _level[i];
        }

        public static double UpgradeSeconds(int fromLevel)
        {
            if (fromLevel < 1) fromLevel = 1;
            double sec = BaseSeconds * Math.Pow(TimeMul, fromLevel - 1);
            if (sec > CapSeconds) sec = CapSeconds;
            return sec;
        }

        public static double UpgradeSeconds(EstateGrid.Cell c, int fromLevel)
        {
            _ = c;
            return UpgradeSeconds(fromLevel);
        }

        public static long UpgradeCost(int fromLevel)
        {
            if (fromLevel < 1) fromLevel = 1;
            double goldHours = CostGoldPerHour * Math.Pow(CostMul, fromLevel);
            return (long)(goldHours * Economy.COPPER_PER_GOLD);
        }

        public static long UpgradeCost(EstateGrid.Cell c, int fromLevel)
        {
            _ = c;
            return UpgradeCost(fromLevel);
        }

        public static long WarehouseCapCopper()
        {
            Load();
            Tick();
            int keep = Level(EstateGrid.Cell.Keep);
            return keep * 12L * Economy.COPPER_PER_GOLD;
        }

        public static long RemainingSeconds() => RemainingSeconds(EstateGrid.Cell.Keep);

        public static long RemainingSeconds(EstateGrid.Cell c)
        {
            Load();
            Tick();
            int i = IndexOf(c);
            if (i < 0 || _to[i] <= _level[i]) return 0;
            long left = _doneUnix[i] - NowUnix();
            return left > 0 ? left : 0;
        }

        public static string RemainingText() => RemainingText(EstateGrid.Cell.Keep);

        public static string RemainingText(EstateGrid.Cell c)
        {
            long s = RemainingSeconds(c);
            if (s <= 0) return "";
            if (s >= 3600) return $"{s / 3600}시간 {(s % 3600) / 60}분";
            if (s >= 60) return $"{s / 60}분 {s % 60}초";
            return $"{s}초";
        }

        public static string WhyCannotUpgrade() => WhyCannotUpgrade(EstateGrid.Cell.Keep);

        public static string WhyCannotUpgrade(EstateGrid.Cell c)
        {
            Load();
            Tick();
            int i = IndexOf(c);
            if (i < 0) return "핵심 건물이 아니다";
            int lv = _level[i];
            if (c == EstateGrid.Cell.Keep)
            {
                if (lv >= MaxKeep) return "본성 상한이다(§13-2)";
            }
            else
            {
                int keep = _level[IndexOf(EstateGrid.Cell.Keep)];
                if (lv >= keep)
                    return $"본성 Lv{keep}이 상한이다(§13-2)";
            }
            if (_to[i] > _level[i])
                return c == EstateGrid.Cell.Keep ? "본성 공사가 끝나지 않았다" : "공사가 끝나지 않았다";
            long cost = UpgradeCost(c, lv);
            if (GameState.Wallet.Copper < cost)
                return $"골드가 부족하다 — {EstateStatusHud.ShortCopper(cost)}";
            return null;
        }

        public static bool TryStartKeep() => TryStartUpgrade(EstateGrid.Cell.Keep);

        public static bool TryStartUpgrade(EstateGrid.Cell c)
        {
            Load();
            Tick();
            if (WhyCannotUpgrade(c) != null) return false;
            int i = IndexOf(c);
            long cost = UpgradeCost(c, _level[i]);
            if (!GameState.Pay(cost)) return false;
            _to[i] = _level[i] + 1;
            double wait = UpgradeSeconds(c, _level[i]);
            if (FastQa()) wait = 1;
            _origSec[i] = (long)Math.Ceiling(wait);
            _jobCost[i] = cost;
            _doneUnix[i] = NowUnix() + _origSec[i];
            Save();
            return true;
        }

        public static long RushableSeconds() => RushableSeconds(EstateGrid.Cell.Keep);

        public static long RushableSeconds(EstateGrid.Cell c)
        {
            Load();
            Tick();
            return EstateRush.Rushable(RemainingSeconds(c), OriginalSeconds(c));
        }

        public static long GoldCostToFloor() => GoldCostToFloor(EstateGrid.Cell.Keep);

        public static long GoldCostToFloor(EstateGrid.Cell c)
        {
            return EstateRush.GoldCost(RushableSeconds(c), JobCost(c));
        }

        public static string WhyCannotRushGold() => WhyCannotRushGold(EstateGrid.Cell.Keep);

        public static string WhyCannotRushGold(EstateGrid.Cell c)
        {
            Load();
            Tick();
            int i = IndexOf(c);
            if (i < 0) return "핵심 건물이 아니다";
            if (EstateRush.Disabled()) return "단축이 꺼져 있다";
            if (_to[i] <= _level[i]) return "공사가 없다";
            if (RushableSeconds(c) <= 0) return "남은 시간의 50%가 바닥이다(§13-2)";
            long pay = GoldCostToFloor(c);
            if (GameState.Wallet.Copper < pay)
                return $"골드가 부족하다 — {EstateStatusHud.ShortCopper(pay)}";
            return null;
        }

        public static string WhyCannotRushMaterial(Economy.LifeItem item) =>
            WhyCannotRushMaterial(EstateGrid.Cell.Keep, item);

        public static string WhyCannotRushMaterial(EstateGrid.Cell c, Economy.LifeItem item)
        {
            Load();
            Tick();
            int i = IndexOf(c);
            if (i < 0) return "핵심 건물이 아니다";
            if (EstateRush.Disabled()) return "단축이 꺼져 있다";
            if (_to[i] <= _level[i]) return "공사가 없다";
            if (RushableSeconds(c) <= 0) return "남은 시간의 50%가 바닥이다(§13-2)";
            if (EstateRush.IsForbidden(item)) return "목숨 아이템·강화석은 단축에 못 쓴다(§13-2)";
            if (GameState.Bag.GetCount(item) < 1) return $"{GameState.Label(item)}이(가) 없다";
            return null;
        }

        public static bool TryRushGold() => TryRushGold(EstateGrid.Cell.Keep);

        public static bool TryRushGold(EstateGrid.Cell c)
        {
            Load();
            Tick();
            if (WhyCannotRushGold(c) != null) return false;
            int i = IndexOf(c);
            long cut = RushableSeconds(c);
            long pay = EstateRush.GoldCost(cut, JobCost(c));
            if (!GameState.Pay(pay)) return false;
            _doneUnix[i] -= cut;
            Save();
            return true;
        }

        public static bool TryRushMaterial(Economy.LifeItem item, int count) =>
            TryRushMaterial(EstateGrid.Cell.Keep, item, count);

        public static bool TryRushMaterial(EstateGrid.Cell c, Economy.LifeItem item, int count)
        {
            Load();
            Tick();
            if (count <= 0) return false;
            if (WhyCannotRushMaterial(c, item) != null) return false;
            int i = IndexOf(c);
            long rem = RemainingSeconds(c);
            long cut = EstateRush.MaterialCut(rem, count);
            long cap = RushableSeconds(c);
            if (cut > cap) cut = cap;
            if (cut <= 0) return false;
            if (!GameState.Consume(item, count)) return false;
            _doneUnix[i] -= cut;
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
            for (int i = 0; i < Cores.Length; i++) _level[i] = 1;
            long cost = UpgradeCost(1);
            GameState.Grant(cost + EstateRush.GoldCost(150, cost) + 1);
            TryStartKeep();
            GameState.Gain(Economy.LifeItem.CraftHide, 3);
        }

        public static void Tick()
        {
            Load();
            bool changed = false;
            long now = NowUnix();
            for (int i = 0; i < Cores.Length; i++)
            {
                if (_to[i] <= _level[i]) continue;
                if (now < _doneUnix[i]) continue;
                _level[i] = _to[i];
                _to[i] = 0;
                _doneUnix[i] = 0;
                _origSec[i] = 0;
                _jobCost[i] = 0;
                changed = true;
            }
            if (changed) Save();
        }

        static long OriginalSeconds(EstateGrid.Cell c)
        {
            Load();
            int i = IndexOf(c);
            if (i < 0) return 0;
            if (_origSec[i] > 0) return _origSec[i];
            if (_to[i] <= _level[i]) return 0;
            return RemainingSeconds(c);
        }

        static long JobCost(EstateGrid.Cell c)
        {
            Load();
            int i = IndexOf(c);
            if (i < 0) return 0;
            if (_jobCost[i] > 0) return _jobCost[i];
            return UpgradeCost(c, Math.Max(1, _level[i]));
        }

        static bool FastQa()
        {
            string raw = Environment.GetEnvironmentVariable("QA_ESTATE_KEEP_FAST");
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        static int IndexOf(EstateGrid.Cell c)
        {
            for (int i = 0; i < Cores.Length; i++)
                if (Cores[i] == c) return i;
            return -1;
        }

        static string Key(EstateGrid.Cell c, string suffix) => PrefPrefix + c + "." + suffix;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < Cores.Length; i++)
            {
                var c = Cores[i];
                string kLv = Key(c, "lv");
                if (c == EstateGrid.Cell.Keep && !PlayerPrefs.HasKey(kLv)
                    && PlayerPrefs.HasKey(OldK_LV))
                {
                    _level[i] = Mathf.Max(1, PlayerPrefs.GetInt(OldK_LV, 1));
                    _to[i] = PlayerPrefs.GetInt(OldK_TO, 0);
                    long.TryParse(PlayerPrefs.GetString(OldK_DONE, "0"), out _doneUnix[i]);
                    long.TryParse(PlayerPrefs.GetString(OldK_ORIG, "0"), out _origSec[i]);
                    long.TryParse(PlayerPrefs.GetString(OldK_JOB, "0"), out _jobCost[i]);
                }
                else
                {
                    _level[i] = Mathf.Max(1, PlayerPrefs.GetInt(kLv, 1));
                    _to[i] = PlayerPrefs.GetInt(Key(c, "to"), 0);
                    long.TryParse(PlayerPrefs.GetString(Key(c, "done"), "0"), out _doneUnix[i]);
                    long.TryParse(PlayerPrefs.GetString(Key(c, "orig"), "0"), out _origSec[i]);
                    long.TryParse(PlayerPrefs.GetString(Key(c, "job"), "0"), out _jobCost[i]);
                }
                if (c == EstateGrid.Cell.Keep && _level[i] > MaxKeep)
                    _level[i] = MaxKeep;
            }
        }

        static void Save()
        {
            for (int i = 0; i < Cores.Length; i++)
            {
                var c = Cores[i];
                PlayerPrefs.SetInt(Key(c, "lv"), _level[i]);
                PlayerPrefs.SetInt(Key(c, "to"), _to[i]);
                PlayerPrefs.SetString(Key(c, "done"), _doneUnix[i].ToString());
                PlayerPrefs.SetString(Key(c, "orig"), _origSec[i].ToString());
                PlayerPrefs.SetString(Key(c, "job"), _jobCost[i].ToString());
            }
            PlayerPrefs.Save();
        }

        /// <summary>파산 강등(§18-5). 공사 중이면 취소하고, 본성은 1 아래로 안 내린다.</summary>
        public static bool DowngradeOne()
        {
            Load();
            Tick();
            int i = IndexOf(EstateGrid.Cell.Keep);
            if (_to[i] > _level[i])
            {
                _to[i] = 0;
                _doneUnix[i] = 0;
                _origSec[i] = 0;
                _jobCost[i] = 0;
            }
            if (_level[i] <= 1)
            {
                Save();
                return false;
            }
            _level[i]--;
            Save();
            return true;
        }

        public static void SetLevelForTest(int lv) => SetLevelForTest(EstateGrid.Cell.Keep, lv);

        public static void SetLevelForTest(EstateGrid.Cell c, int lv)
        {
            Load();
            int i = IndexOf(c);
            if (i < 0) return;
            _level[i] = Mathf.Clamp(lv, 1, MaxKeep);
            _to[i] = 0;
            _doneUnix[i] = 0;
            _origSec[i] = 0;
            _jobCost[i] = 0;
            Save();
        }

        /// <summary>티어 아트 시각 QA. Keep=3(_0), Mine=7(_1), Warehouse=12(_2), Barracks Busy+scaffold.</summary>
        public static void SeedArtTierQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable("QA_ESTATE_ART_TIERS");
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            EstateGrid.EnsureHubBuildings();
            SetLevelForTest(EstateGrid.Cell.Keep, 3);
            SetLevelForTest(EstateGrid.Cell.Mine, 7);
            SetLevelForTest(EstateGrid.Cell.Warehouse, 12);
            SetLevelForTest(EstateGrid.Cell.Barracks, 6);
            // Busy 시드: 수비대 공사 중 → scaffold 겹침
            int i = IndexOf(EstateGrid.Cell.Barracks);
            if (i >= 0)
            {
                _to[i] = _level[i] + 1;
                _doneUnix[i] = NowUnix() + 3600;
                _origSec[i] = 3600;
                _jobCost[i] = 1;
                Save();
            }
        }

        public static void ResetForTest()
        {
            for (int i = 0; i < Cores.Length; i++)
            {
                var c = Cores[i];
                PlayerPrefs.DeleteKey(Key(c, "lv"));
                PlayerPrefs.DeleteKey(Key(c, "to"));
                PlayerPrefs.DeleteKey(Key(c, "done"));
                PlayerPrefs.DeleteKey(Key(c, "orig"));
                PlayerPrefs.DeleteKey(Key(c, "job"));
                _level[i] = 1;
                _to[i] = 0;
                _doneUnix[i] = 0;
                _origSec[i] = 0;
                _jobCost[i] = 0;
            }
            PlayerPrefs.DeleteKey(OldK_LV);
            PlayerPrefs.DeleteKey(OldK_TO);
            PlayerPrefs.DeleteKey(OldK_DONE);
            PlayerPrefs.DeleteKey(OldK_ORIG);
            PlayerPrefs.DeleteKey(OldK_JOB);
            PlayerPrefs.Save();
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
