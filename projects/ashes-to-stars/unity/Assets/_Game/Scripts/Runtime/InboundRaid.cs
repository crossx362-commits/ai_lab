using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 로컬 인바운드 침략(§15 비동기 공성 · §18-13 수비 +20).
    /// 서버 없이 월드맵에 들어온 습격을 수비대 유무로 정산한다. W3Party는 안 연다.
    /// 수비 1명 이상이면 성공(Honor.ApplyGuard), 비어 있으면 실패·12시간 회복·보호막.
    /// QA_NO_HONOR_GUARD면 대기·정산 모두 거부(옛 화면 = 침략 없음).
    /// </summary>
    public static class InboundRaid
    {
        const string K_PENDING = "ats.inbound.pending";
        const string K_LAST = "ats.inbound.last";

        static bool _loaded;
        static bool _pending;
        static long _lastUnix;
        static bool _qaSeeded;

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

        /// <summary>대기 중인 습격을 정산. 성공이면 +20, 실패면 0·수비 회복·보호막.</summary>
        public static int Settle()
        {
            Load();
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
