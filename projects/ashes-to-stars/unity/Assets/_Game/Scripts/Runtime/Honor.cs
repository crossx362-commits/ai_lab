using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 침략 명예(§18-13). 거래 불가 통화. 승리 +30 / 패배 0.
    /// 상대 방어(EstateDefense.CutPercent)에 ±50% — Cut 0=15 · 20=30 · 40=45.
    /// 수비 성공 +20(§18-13). InboundRaid가 로컬 인바운드를 정산할 때 ApplyGuard가 적립하고
    /// 월드맵 수비대 카드가 GuardCap을 읽는다. QA_NO_HONOR_GUARD면 옛 「침략 없음」.
    /// QA_NO_HONOR면 불변. QA_NO_HONOR_DEFENSE면 옛 고정 +30.
    /// </summary>
    public static class Honor
    {
        public const int Win = 30;
        public const int Lose = 0;
        public const int Guard = 20;
        public const int ScaleFloor = 50;
        public const string EnvShow = "QA_HONOR";
        public const string EnvNo = "QA_NO_HONOR";
        public const string EnvShowDefense = "QA_HONOR_DEFENSE";
        public const string EnvNoDefense = "QA_NO_HONOR_DEFENSE";
        public const string EnvShowGuard = "QA_HONOR_GUARD";
        public const string EnvNoGuard = "QA_NO_HONOR_GUARD";
        public const string GuardCap = "수비 +20";

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

        public static bool ScaleBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoDefense);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool GuardBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoGuard);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowGuardQa
        {
            get
            {
                if (Blocked || GuardBlocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShowGuard);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static int Points { get { Load(); return _points; } }
        public static int LastGain { get { Load(); return _last; } }

        /// <summary>Cut 0=15 · 20=30 · 40=45. QA_NO_HONOR_DEFENSE면 30.</summary>
        public static int WinForCut(int cut)
        {
            if (ScaleBlocked) return Win;
            if (cut < 0) cut = 0;
            if (cut > EstateDefense.CutCap) cut = EstateDefense.CutCap;
            int scale = ScaleFloor + cut * 100 / EstateDefense.CutCap;
            return Win * scale / 100;
        }

        public static int WinNow() => WinForCut(EstateDefense.CutPercent());

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

        /// <summary>승리 WinNow × 반복 배율, 패배 0. QA_NO면 0을 주고 잔액은 안 바꾼다.</summary>
        public static int ApplyInvasion(bool won)
        {
            Load();
            if (Blocked)
            {
                _last = 0;
                Save();
                return 0;
            }
            int add = won ? WinNow() * InvasionState.RepeatPercent() / 100 : Lose;
            _points += add;
            _last = add;
            Save();
            return add;
        }

        public static string WinLine()
        {
            if (Blocked) return "명예 없음";
            if (ScaleBlocked) return $"명예 +{Win}(§18-13)";
            return $"명예 +{WinNow()}(방어 비례 §18-13)";
        }

        public static string BalanceLine()
        {
            if (Blocked) return "명예 없음";
            return $"명예 {Points}";
        }

        /// <summary>수비 성공 Guard, 실패 0. QA_NO·QA_NO_HONOR_GUARD면 0을 주고 잔액은 안 바꾼다.</summary>
        public static int ApplyGuard(bool held)
        {
            Load();
            if (Blocked || GuardBlocked)
            {
                _last = 0;
                Save();
                return 0;
            }
            int add = held ? Guard : Lose;
            _points += add;
            _last = add;
            Save();
            return add;
        }

        public static string GuardLine()
        {
            if (Blocked || GuardBlocked) return "수비 명예 없음";
            return $"수비 성공 +{Guard}(§18-13)";
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

        /// <summary>시각 QA. QA_HONOR_DEFENSE=1이면 방어 Cut 40 · 명예 +45.</summary>
        public static void SeedDefenseQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowDefense) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            Load();
            RacePrefs.Set(RaceId.인간);
            WorldStar.EnemyDebuff = false;
            WorldStar.AllyBuff = false;
            EstateDefense.ResetForTest();
            EstateDefense.SetLevelForTest(EstateDefense.Kind.화살탑, 16);
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            if (GameState.Wallet.Copper < InvasionState.SortieCost())
                GameState.Earn(InvasionState.SortieCost());
            InvasionState.ResetPendingForHonorQa();
        }

        /// <summary>시각 QA. QA_HONOR_GUARD=1이면 수비 1명·인바운드 성공 정산·명예 +20.</summary>
        public static void SeedGuardQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowGuard) != "1") return;
            if (Blocked || GuardBlocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            Load();
            RacePrefs.Set(RaceId.인간);
            WorldStar.EnemyDebuff = false;
            WorldStar.AllyBuff = false;
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            InboundRaid.SeedHeldForQa();
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
