using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 보스 레이드 1인 최초 클리어 특별 보상(§8).
    /// 5층마다 한 보스. 그 보스를 출전 1명으로 처음 깨면 칭호·외형만 준다.
    /// 두 번째부터는 없다. 골드·레벨·목숨 아이템·전투력은 안 건드린다.
    /// 희귀 장비는 💡라 이 슬라이스에 없다.
    /// </summary>
    public static class SoloRaidClear
    {
        public const string LookName = "홀로 선 별";

        const string K_FLOORS = "ats.solo_raid_floors";
        const string K_PENDING = "ats.solo_raid_pending";
        const string K_LAST = "ats.solo_raid_last";

        static bool _loaded;
        static readonly List<int> _floors = new List<int>();
        static bool _pending;
        static int _last;

        public static bool HasAny { get { EnsureLoaded(); return _floors.Count > 0; } }
        public static int Count { get { EnsureLoaded(); return _floors.Count; } }
        public static int LastFloor { get { EnsureLoaded(); return _last; } }
        public static bool PendingBanner { get { EnsureLoaded(); return _pending; } }
        public static bool HasLook { get { return HasAny; } }

        public static string LastTitle
        {
            get
            {
                EnsureLoaded();
                return _last > 0 ? TitleOf(_last) : "";
            }
        }

        public static string BannerText
        {
            get
            {
                EnsureLoaded();
                if (!_pending || _last <= 0) return "";
                return $"{TitleOf(_last)} — 1인 최초 클리어(§8)";
            }
        }

        public static string TitleOf(int floor) => $"{floor}층을 홀로 깬 자";

        public static bool IsRaidFloor(int floor)
            => floor >= 5 && floor <= 100 && floor % 5 == 0;

        public static bool HasClear(int floor)
        {
            EnsureLoaded();
            return _floors.Contains(floor);
        }

        /// <summary>
        /// 탑 레이드 보스를 출전 1명으로 처음 깼을 때만 연다.
        /// 같은 층 재도전·2명 이상·비레이드 층은 false. QA_NO_SOLO_CLEAR=1이면 거부.
        /// </summary>
        public static bool TryGrant(int floor, int partySize)
        {
            if (QaForcedOff()) return false;
            if (!IsRaidFloor(floor)) return false;
            if (partySize != 1) return false;
            EnsureLoaded();
            if (_floors.Contains(floor))
            {
                _pending = false;
                Save();
                return false;
            }
            _floors.Add(floor);
            _floors.Sort();
            _last = floor;
            _pending = true;
            Save();
            return true;
        }

        public static void AckBanner()
        {
            EnsureLoaded();
            if (!_pending) return;
            _pending = false;
            Save();
        }

        /// <summary>시각 QA. QA_SOLO_CLEAR=1이면 5층 1인 칭호·배너를 심는다.</summary>
        public static void SeedQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_SOLO_CLEAR");
            if (raw != "1" && raw != "true") return;
            EnsureLoaded();
            if (!_floors.Contains(5)) _floors.Add(5);
            _floors.Sort();
            _last = 5;
            _pending = true;
            Save();
            if (string.IsNullOrEmpty(GameFlow.LastBattleSummary))
                GameFlow.LastBattleSummary = "보스 격파 — 5층 · 5층을 홀로 깬 자";
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_FLOORS);
            PlayerPrefs.DeleteKey(K_PENDING);
            PlayerPrefs.DeleteKey(K_LAST);
            _loaded = false;
            _floors.Clear();
            _pending = false;
            _last = 0;
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
            _floors.Clear();
            _pending = false;
            _last = 0;
        }

        static bool QaForcedOff()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_NO_SOLO_CLEAR");
            return raw == "1" || raw == "true";
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _floors.Clear();
            string raw = PlayerPrefs.GetString(K_FLOORS, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var part in raw.Split(','))
                    if (int.TryParse(part, out int floor) && !_floors.Contains(floor))
                        _floors.Add(floor);
            _floors.Sort();
            _pending = PlayerPrefs.GetInt(K_PENDING, 0) == 1;
            _last = PlayerPrefs.GetInt(K_LAST, 0);
            if (_last <= 0 && _floors.Count > 0)
                _last = _floors[_floors.Count - 1];
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_FLOORS, string.Join(",", _floors));
            PlayerPrefs.SetInt(K_PENDING, _pending ? 1 : 0);
            PlayerPrefs.SetInt(K_LAST, _last);
            PlayerPrefs.Save();
        }
    }
}
