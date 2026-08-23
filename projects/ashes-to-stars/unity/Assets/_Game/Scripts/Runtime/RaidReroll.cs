using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 하위 레이드 재입장 누진 비용(§18-2).
    /// 1회차 ×1 → 2회차 ×2 → 3회차 ×4 → 4회차+ ×8. 24시간 리셋.
    /// Economy.GetRerollCostMultiplier를 읽는다. 첫 클리어·던전은 1배.
    /// QA_NO면 매번 1배.
    /// </summary>
    public static class RaidReroll
    {
        public const string EnvShow = "QA_RAID_REROLL";
        public const string EnvNo = "QA_NO_RAID_REROLL";
        public const long WindowSeconds = 24 * 3600;
        public const string ActionKey = "Tower5BossRaid";

        const string K_HITS = "ats.raid_reroll.hits";
        const string K_LAST = "ats.raid_reroll.last";

        static bool _loaded;
        static bool _qaSeeded;
        static int _hits;
        static long _last;

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static bool Blocked
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
            int.TryParse(PlayerPrefs.GetString(K_HITS, "0"), out _hits);
            long.TryParse(PlayerPrefs.GetString(K_LAST, "0"), out _last);
            if (_hits < 0) _hits = 0;
            if (_last < 0) _last = 0;
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_HITS, _hits.ToString());
            PlayerPrefs.SetString(K_LAST, _last.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>이미 깬 탑 레이드만. 첫 도전·던전은 누진 없음.</summary>
        public static bool Applies(int floor) =>
            !Blocked && RaidScale.IsRaidFloor(floor) && floor < GameState.TowerFloor
            && !(DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon);

        /// <summary>24시간 창 안의 다음 회차. 창 밖이면 1.</summary>
        public static int NextAttempt()
        {
            if (Blocked) return 1;
            Load();
            if (_last > 0 && NowUnix() - _last > WindowSeconds) return 1;
            return _hits + 1;
        }

        /// <summary>§18-2 배수. Economy.GetRerollCostMultiplier(이전 횟수)를 읽는다.</summary>
        public static int Multiplier()
        {
            int n = NextAttempt();
            if (n < 1) n = 1;
            return Mathf.Max(1, Mathf.RoundToInt(Economy.GetRerollCostMultiplier(n - 1)));
        }

        public static long Apply(long copper)
        {
            if (Blocked) return copper;
            if (copper <= 0) return 0;
            return copper * Multiplier();
        }

        public static long BaseCost() =>
            Economy.GetActionCost(ActionKey, GameState.UnlockedTier);

        /// <summary>하위면 누진, 아니면 기준값. 10층은 RaidCost가 0.15를 읽는다.</summary>
        public static long Cost(int floor)
        {
            long raw = RaidCost.Copper(floor);
            if (!Applies(floor)) return raw;
            return Apply(raw);
        }

        public static string Line()
        {
            int floor = RaidScale.LowerFloor;
            if (floor <= 0) return "";
            return FormatLine(floor);
        }

        public static string FormatLine(int floor)
        {
            if (Blocked) return "재입장 누진 없음";
            if (!Applies(floor)) return "";
            int mul = Multiplier();
            return $"재입장 ×{mul}(§18-2) · {EstateStatusHud.ShortCopper(Cost(floor))}";
        }

        /// <summary>입장 골드가 빠진 뒤에만. 하위 카드만. 미리보기는 그 전의 NextAttempt를 본다.</summary>
        public static void Record(int floor)
        {
            if (Blocked || floor != RaidScale.LowerFloor || !Applies(floor)) return;
            Load();
            if (_last > 0 && NowUnix() - _last > WindowSeconds)
                _hits = 0;
            _hits++;
            _last = NowUnix();
            Save();
        }

        /// <summary>시각 QA. 11층·2회차라 하위 5층이 32실버.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            RacePrefs.Set(RaceId.인간);
            if (GameState.TowerFloor <= 10)
                GameState.SetTowerFloorForTest(11);
            ResetHitsOnly();
            Record(RaidScale.LowerRaidFloor);
            long need = Cost(RaidScale.LowerRaidFloor);
            if (GameState.Wallet.Copper < need)
                GameState.Grant(need);
        }

        static void ResetHitsOnly()
        {
            PlayerPrefs.DeleteKey(K_HITS);
            PlayerPrefs.DeleteKey(K_LAST);
            PlayerPrefs.Save();
            _hits = 0;
            _last = 0;
            _loaded = true;
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_HITS);
            PlayerPrefs.DeleteKey(K_LAST);
            PlayerPrefs.Save();
            _hits = 0;
            _last = 0;
            _qaSeeded = false;
            _loaded = false;
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static void ForgetInMemoryForTest()
        {
            _hits = 0;
            _last = 0;
            _loaded = false;
        }
    }
}
