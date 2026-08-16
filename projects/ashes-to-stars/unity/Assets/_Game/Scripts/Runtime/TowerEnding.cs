using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 100층 최초 클리어 결말(§8). 칭호·별 외형은 계정 귀속이고 전투력에 안 더한다.
    /// 뉴게임+·101층은 열지 않는다. 저장은 유지한 채 100층을 다시 오를 수 있다.
    /// </summary>
    public static class TowerEnding
    {
        public const int FinaleFloor = 100;
        public const string TitleName = "별에 닿은 자";
        public const string LookName = "별 외형";
        public const string EpilogueBody =
            "탑의 꼭대기에 별이 내려앉는다. 전투력은 그대로다. 저장은 유지되고 100층을 다시 오를 수 있다.";

        const string K_TITLE = "ats.star_title";
        const string K_LOOK = "ats.star_look";
        const string K_EPI = "ats.star_epilogue";

        static bool _loaded;
        static bool _title;
        static bool _look;
        static bool _pending;

        public static bool HasTitle { get { EnsureLoaded(); return _title; } }
        public static bool HasStarLook { get { EnsureLoaded(); return _look; } }
        public static bool PendingEpilogue { get { EnsureLoaded(); return _pending; } }

        /// <summary>
        /// 100층 보스 격파에서만 연다. 이미 있으면 false(재지급 없음).
        /// QA_NO_TOWER_END=1이면 지급을 거부한다.
        /// </summary>
        public static bool TryGrant(int clearedFloor)
        {
            if (QaForcedOff()) return false;
            if (clearedFloor != FinaleFloor) return false;
            EnsureLoaded();
            if (_title && _look)
            {
                _pending = false;
                Save();
                return false;
            }
            _title = true;
            _look = true;
            _pending = true;
            Save();
            return true;
        }

        public static void SkipEpilogue()
        {
            EnsureLoaded();
            if (!_pending) return;
            _pending = false;
            Save();
        }

        /// <summary>시각 QA. QA_TOWER_END=1이면 100층 직후 에필로그 대기 상태를 심는다.</summary>
        public static void SeedQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_TOWER_END");
            if (raw != "1" && raw != "true") return;
            GameState.SetTowerFloorForTest(FinaleFloor);
            EnsureLoaded();
            _title = true;
            _look = true;
            _pending = true;
            Save();
            if (string.IsNullOrEmpty(GameFlow.LastBattleSummary))
                GameFlow.LastBattleSummary = "보스 격파 — 100층 · 별에 닿은 자";
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_TITLE);
            PlayerPrefs.DeleteKey(K_LOOK);
            PlayerPrefs.DeleteKey(K_EPI);
            _loaded = false;
            _title = _look = _pending = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
        }

        static bool QaForcedOff()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_NO_TOWER_END");
            return raw == "1" || raw == "true";
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _title = PlayerPrefs.GetInt(K_TITLE, 0) == 1;
            _look = PlayerPrefs.GetInt(K_LOOK, 0) == 1;
            _pending = PlayerPrefs.GetInt(K_EPI, 0) == 1;
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_TITLE, _title ? 1 : 0);
            PlayerPrefs.SetInt(K_LOOK, _look ? 1 : 0);
            PlayerPrefs.SetInt(K_EPI, _pending ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
