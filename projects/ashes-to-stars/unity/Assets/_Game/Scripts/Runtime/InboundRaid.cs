using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 로컬 인바운드 침략(§15 비동기 공성 · §18-13 수비 +20).
    /// 수비가 있으면 전투로 막고, 비어 있으면 즉시 실패·12시간 회복·보호막.
    /// QA_NO_INBOUND_FIGHT면 옛 즉시 정산(수비 1명이면 전투 없이 성공).
    /// QA_NO_HONOR_GUARD면 대기·정산 모두 거부(옛 화면 = 침략 없음).
    /// </summary>
    public static class InboundRaid
    {
        const string K_PENDING = "ats.inbound.pending";
        const string K_LAST = "ats.inbound.last";

        public const string EnvNoFight = "QA_NO_INBOUND_FIGHT";
        static bool _loaded;
        static bool _pending;
        static bool _fighting;
        static long _lastUnix;
        static bool _qaSeeded;

        public static bool FightBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoFight);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Fighting => _fighting;

        public static bool Pending { get { Load(); return _pending; } }
        public static long LastUnix { get { Load(); return _lastUnix; } }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _pending = PlayerPrefs.GetInt(K_PENDING, 0) == 1;
            long.TryParse(PlayerPrefs.GetString(K_LAST, "0"), out _lastUnix);
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_PENDING, _pending ? 1 : 0);
            PlayerPrefs.SetString(K_LAST, _lastUnix.ToString());
            PlayerPrefs.Save();
        }

        public static bool HeldNow() => DefenseState.Count > 0;

        public static bool Queue()
        {
            Load();
            if (Honor.Blocked || Honor.GuardBlocked) return false;
            if (!DefenseState.Unlocked) return false;
            if (InvasionState.ShieldActive) return false;
            if (_pending) return false;
            _pending = true;
            Save();
            return true;
        }

        /// <summary>해금된 영지에 보호막·대기가 없으면 습격을 한 건 건다. 12시간 창.</summary>
        public static bool OfferIfDue()
        {
            Load();
            if (Honor.Blocked || Honor.GuardBlocked) return false;
            if (!DefenseState.Unlocked) return false;
            if (InvasionState.ShieldActive) return false;
            if (_pending) return true;
            long now = InvasionState.NowUnix();
            if (_lastUnix > 0 && now - _lastUnix < InvasionState.GuardSeconds)
                return false;
            return Queue();
        }

        /// <summary>
        /// 수비가 있으면 전투로 들어간다. 비었거나 QA_NO면 false — 호출부가 Settle한다.
        /// </summary>
        public static bool TryFight()
        {
            Load();
            if (!_pending || FightBlocked || !HeldNow()) return false;
            _fighting = true;
            GameFlow.GoBattle(GameFlow.WorldMap, GameFlow.BattleKind.침략, GameState.TowerFloor);
            return true;
        }

        /// <summary>전투 결과로 정산. 이기면 수비 성공, 지면 실패·회복·보호막.</summary>
        public static int SettleFromBattle(bool survived)
        {
            Load();
            _fighting = false;
            if (!_pending) return 0;
            bool held = survived;
            int pts = Honor.ApplyGuard(held);
            if (!held)
            {
                DefenseState.ApplyPvpRecover();
                InvasionState.ArmShield();
            }
            _pending = false;
            _lastUnix = InvasionState.NowUnix();
            Save();
            return pts;
        }

        /// <summary>대기 중인 습격을 정산. 성공이면 +20, 실패면 0·수비 회복·보호막.</summary>
        public static int Settle()
        {
            Load();
            _fighting = false;
            if (!_pending) return 0;
            bool held = HeldNow();
            int pts = Honor.ApplyGuard(held);
            if (!held)
            {
                DefenseState.ApplyPvpRecover();
                InvasionState.ArmShield();
            }
            _pending = false;
            _lastUnix = InvasionState.NowUnix();
            Save();
            return pts;
        }

        /// <summary>QA_HONOR_GUARD 시드. 수비 1명을 세우고 성공 정산까지 끝낸다.</summary>
        public static void SeedHeldForQa()
        {
            if (_qaSeeded) return;
            if (Honor.Blocked || Honor.GuardBlocked) return;
            _qaSeeded = true;
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            _pending = false;
            _lastUnix = 0;
            _loaded = true;
            Save();
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0)
            {
                LifeSystem.ResetAll();
                roster = LifeSystem.GetCharacters();
            }
            if (roster.Count > 0 && !DefenseState.Contains(0))
                DefenseState.Toggle(0);
            Queue();
            Settle();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_PENDING);
            PlayerPrefs.DeleteKey(K_LAST);
            PlayerPrefs.Save();
            _pending = false;
            _fighting = false;
            _lastUnix = 0;
            _qaSeeded = false;
            _loaded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _pending = false;
            _lastUnix = 0;
            _loaded = false;
        }
    }
}
