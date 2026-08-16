using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 침략 명예(§18-13). 거래 불가 통화. 승리 +30 / 패배 0.
    /// 방어력 비례 ±50%는 침략 전투 시뮬이 없어 고정 +30만.
    /// 수비 성공 +20은 수비 시뮬이 없어 안 넣는다. QA_NO면 불변.
    /// </summary>
    public static class Honor
    {
        public const int Win = 30;
        public const int Lose = 0;
        public const string EnvShow = "QA_HONOR";
        public const string EnvNo = "QA_NO_HONOR";

        const string K_POINTS = "ats.honor.points";
        const string K_LAST = "ats.honor.last";

        static bool _loaded;
        static bool _qaSeeded;
        static int _points;
        static int _last;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static int Points { get { Load(); return _points; } }
        public static int LastGain { get { Load(); return _last; } }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            int.TryParse(PlayerPrefs.GetString(K_POINTS, "0"), out _points);
            int.TryParse(PlayerPrefs.GetString(K_LAST, "0"), out _last);
            if (_points < 0) _points = 0;
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_POINTS, _points.ToString());
            PlayerPrefs.SetString(K_LAST, _last.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>승리 +30 × 반복 배율, 패배 0. QA_NO면 0을 주고 잔액은 안 바꾼다.</summary>
        public static int ApplyInvasion(bool won)
        {
            Load();
            if (Blocked)
            {
                _last = 0;
                Save();
                return 0;
            }
            int add = won ? Win * InvasionState.RepeatPercent() / 100 : Lose;
            _points += add;
            _last = add;
            Save();
            return add;
        }

        public static string WinLine()
        {
            if (Blocked) return "명예 없음";
            return $"명예 +{Win}(§18-13)";
        }

        public static string BalanceLine()
        {
            if (Blocked) return "명예 없음";
            return $"명예 {Points}";
        }

        /// <summary>시각 QA. QA_HONOR=1이면 30층·보호막 없음으로 침략 카드를 연다.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            Load();
            RacePrefs.Set(RaceId.인간);
            WorldStar.EnemyDebuff = false;
            WorldStar.AllyBuff = false;
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            if (GameState.Wallet.Copper < InvasionState.SortieCost())
                GameState.Earn(InvasionState.SortieCost());
            InvasionState.ResetPendingForHonorQa();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_POINTS);
            PlayerPrefs.DeleteKey(K_LAST);
            PlayerPrefs.Save();
            _points = 0;
            _last = 0;
            _qaSeeded = false;
            _loaded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _points = 0;
            _last = 0;
            _loaded = false;
        }
    }
}
