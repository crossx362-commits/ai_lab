using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 층 클리어 보상(오너 2026-08-16 21:06).
    /// 탑 층을 처음 깨면 기본직업 1종을 고른다.
    /// 레이드(5·10·…·100)는 같은 클리어에서 기본직업 2종을 고르고,
    /// 일정 확률로 특수 직업 캐릭터 1명의 역할도 고른다.
    /// 직업명(사신 등)은 💡라 플래그만. 던전·필드·재도전은 안 준다.
    /// 50층 드랍표 증표는 이 경로가 아니다.
    /// </summary>
    public static class FloorRecruit
    {
        public const float RaidSpecialChance = 0.01f;
        public const int RaidJobSlots = 2;
        public const int NormalJobSlots = 1;

        const string K_JOB_FLOORS = "ats.floor_recruit_jobs";
        const string K_SPEC_FLOORS = "ats.floor_recruit_spec";
        const string K_PENDING = "ats.floor_recruit_pending";
        const string K_PENDING_PICKS = "ats.floor_recruit_picks";
        const string K_PENDING_FLOOR = "ats.floor_recruit_pfloor";
        const string K_LAST_NAME = "ats.floor_recruit_name";
        const string K_LAST_JOB = "ats.floor_recruit_job";
        const string K_SPEC_PENDING = "ats.floor_recruit_spec_pending";
        const string K_SPEC_PICK = "ats.floor_recruit_spec_pick";
        const string K_SPEC_GOT = "ats.floor_recruit_spec_got";
        const string K_LAST_SPEC_NAME = "ats.floor_recruit_spec_name";
        const string K_LAST_SPEC_JOB = "ats.floor_recruit_spec_job";

        static bool _loaded;
        static readonly List<int> _jobFloors = new List<int>();
        static readonly List<int> _specFloors = new List<int>();
        static int _pendingPicks;
        static int _pendingFloor;
        static string _lastName = "";
        static string _lastJob = "";
        static bool _pendingSpecialPick;
        static bool _pendingSpecialBanner;
        static int _lastSpecialGot;
        static string _lastSpecialName = "";
        static string _lastSpecialJob = "";

        public static bool PendingJob { get { EnsureLoaded(); return _pendingPicks > 0; } }
        public static bool PendingSpecialPick { get { EnsureLoaded(); return _pendingSpecialPick; } }
        public static bool AwaitingPick { get { EnsureLoaded(); return _pendingPicks > 0 || _pendingSpecialPick; } }
        public static int PendingPicks { get { EnsureLoaded(); return _pendingPicks; } }
        public static int PendingFloor { get { EnsureLoaded(); return _pendingFloor; } }
        public static string LastGrantedName { get { EnsureLoaded(); return _lastName; } }
        public static string LastGrantedJob { get { EnsureLoaded(); return _lastJob; } }
        public static bool PendingSpecialBanner { get { EnsureLoaded(); return _pendingSpecialBanner; } }
        public static int LastSpecialGot { get { EnsureLoaded(); return _lastSpecialGot; } }
        public static string LastSpecialName { get { EnsureLoaded(); return _lastSpecialName; } }
        public static string LastSpecialJob { get { EnsureLoaded(); return _lastSpecialJob; } }
        public static int OfferedJobCount { get { EnsureLoaded(); return _jobFloors.Count; } }

        public static bool OfferedJob(int floor)
        {
            EnsureLoaded();
            return _jobFloors.Contains(floor);
        }

        public static bool RolledSpecial(int floor)
        {
            EnsureLoaded();
            return _specFloors.Contains(floor);
        }

        public static bool IsRaidFloor(int floor) => SoloRaidClear.IsRaidFloor(floor);

        public static int JobSlotsFor(int floor) => IsRaidFloor(floor) ? RaidJobSlots : NormalJobSlots;

        public static string PickTitle()
        {
            EnsureLoaded();
            if (_pendingPicks > 0)
            {
                int total = JobSlotsFor(_pendingFloor);
                int next = total - _pendingPicks + 1;
                if (total <= 1)
                    return $"{_pendingFloor}층 돌파 — 기본 직업 1종을 고른다";
                return $"{_pendingFloor}층 레이드 — 기본 직업 {total}종을 고른다 ({next}/{total})";
            }
            if (_pendingSpecialPick)
                return $"{_pendingFloor}층 레이드 — 특수 직업 캐릭터 역할을 고른다";
            return "";
        }

        public static string PickSubtitle()
        {
            EnsureLoaded();
            if (_pendingPicks <= 0 && _pendingSpecialPick)
                return "Lv1 특수 직업 · 목숨 1 · 부활초·환생석 불가";
            return "Lv1 기본직업 · 명부에 들어온다";
        }

        public static string SpecialHint()
        {
            EnsureLoaded();
            if (_pendingSpecialPick && _pendingPicks > 0)
                return "당첨 — 2종을 고른 뒤 특수 직업 역할을 고른다";
            if (_pendingSpecialBanner && !string.IsNullOrEmpty(_lastSpecialName))
                return $"특수 직업 {_lastSpecialName} — 레이드 확률(§3)";
            if (_pendingSpecialBanner)
                return "특수 직업 캐릭터 — 레이드 확률(§3)";
            return "";
        }

        /// <summary>
        /// 탑 층을 처음 깼을 때만 연다. 같은 층 재도전·던전·범위 밖은 false.
        /// QA_NO_FLOOR_REWARD=1이면 거부.
        /// </summary>
        public static bool OnCleared(int floor)
        {
            if (QaForcedOff()) return false;
            if (DungeonRun.Active) return false;
            if (floor < 1 || floor > 100) return false;
            EnsureLoaded();
            bool changed = false;
            if (!_jobFloors.Contains(floor))
            {
                _jobFloors.Add(floor);
                _jobFloors.Sort();
                _pendingPicks = JobSlotsFor(floor);
                _pendingFloor = floor;
                changed = true;
            }
            if (IsRaidFloor(floor) && !_specFloors.Contains(floor))
            {
                _specFloors.Add(floor);
                _specFloors.Sort();
                if (RollSpecial())
                    _pendingSpecialPick = true;
                changed = true;
            }
            if (changed) Save();
            return changed;
        }

        /// <summary>
        /// 대기 중인 층 보상으로 1명을 명부에 넣는다.
        /// 기본 선택이 남아 있으면 기본직업, 다 고른 뒤엔 특수 직업.
        /// 잘못된 직업·대기 없음은 false.
        /// </summary>
        public static bool TryClaim(string job)
        {
            if (QaForcedOff()) return false;
            EnsureLoaded();
            if (_pendingPicks > 0)
            {
                var ch = LifeSystem.AddBasicRecruit(job);
                if (ch == null) return false;
                _pendingPicks--;
                _lastName = ch.Name;
                _lastJob = ch.Job;
                Save();
                return true;
            }
            if (_pendingSpecialPick)
            {
                if (!LifeSystem.IsNamedSpecialJob(job) && !LifeSystem.IsBasicJob(job))
                    return false;
                var ch = LifeSystem.AddSpecialRecruit(job);
                if (ch == null) return false;
                _pendingSpecialPick = false;
                _pendingSpecialBanner = true;
                _lastSpecialGot = 1;
                _lastSpecialName = ch.Name;
                _lastSpecialJob = ch.Job;
                _lastName = ch.Name;
                _lastJob = ch.Job;
                Save();
                return true;
            }
            return false;
        }

        public static void AckSpecialBanner()
        {
            EnsureLoaded();
            if (!_pendingSpecialBanner) return;
            _pendingSpecialBanner = false;
            Save();
        }

        /// <summary>시각 QA. QA_FLOOR_REWARD=1이면 5층 2종 선택과 특수 직업 역할 대기를 심는다.</summary>
        public static void SeedQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_FLOOR_REWARD");
            if (raw != "1" && raw != "true") return;
            EnsureLoaded();
            if (!_jobFloors.Contains(5)) _jobFloors.Add(5);
            _jobFloors.Sort();
            _pendingPicks = RaidJobSlots;
            _pendingFloor = 5;
            if (!_specFloors.Contains(5)) _specFloors.Add(5);
            _specFloors.Sort();
            _pendingSpecialPick = true;
            _pendingSpecialBanner = false;
            _lastSpecialGot = 0;
            if (string.IsNullOrEmpty(GameFlow.LastBattleSummary))
                GameFlow.LastBattleSummary = "5층 레이드 — 기본 직업 2종을 고른다";
            Save();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_JOB_FLOORS);
            PlayerPrefs.DeleteKey(K_SPEC_FLOORS);
            PlayerPrefs.DeleteKey(K_PENDING);
            PlayerPrefs.DeleteKey(K_PENDING_PICKS);
            PlayerPrefs.DeleteKey(K_PENDING_FLOOR);
            PlayerPrefs.DeleteKey(K_LAST_NAME);
            PlayerPrefs.DeleteKey(K_LAST_JOB);
            PlayerPrefs.DeleteKey(K_SPEC_PENDING);
            PlayerPrefs.DeleteKey(K_SPEC_PICK);
            PlayerPrefs.DeleteKey(K_SPEC_GOT);
            PlayerPrefs.DeleteKey(K_LAST_SPEC_NAME);
            PlayerPrefs.DeleteKey(K_LAST_SPEC_JOB);
            _loaded = false;
            ClearMemory();
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
            ClearMemory();
        }

        static void ClearMemory()
        {
            _jobFloors.Clear();
            _specFloors.Clear();
            _pendingPicks = 0;
            _pendingFloor = 0;
            _lastName = "";
            _lastJob = "";
            _pendingSpecialPick = false;
            _pendingSpecialBanner = false;
            _lastSpecialGot = 0;
            _lastSpecialName = "";
            _lastSpecialJob = "";
        }

        static bool RollSpecial()
        {
            if (QaForceSpecialOn()) return true;
            if (QaForceSpecialOff()) return false;
            return Random.value < RaidSpecialChance;
        }

        static bool QaForcedOff()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_NO_FLOOR_REWARD");
            return raw == "1" || raw == "true";
        }

        static bool QaForceSpecialOn()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_RAID_SPECIAL");
            return raw == "1" || raw == "true";
        }

        static bool QaForceSpecialOff()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_NO_RAID_SPECIAL");
            return raw == "1" || raw == "true";
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _jobFloors.Clear();
            _specFloors.Clear();
            ReadFloors(PlayerPrefs.GetString(K_JOB_FLOORS, ""), _jobFloors);
            ReadFloors(PlayerPrefs.GetString(K_SPEC_FLOORS, ""), _specFloors);
            _pendingPicks = PlayerPrefs.GetInt(K_PENDING_PICKS, -1);
            if (_pendingPicks < 0)
                _pendingPicks = PlayerPrefs.GetInt(K_PENDING, 0) == 1 ? 1 : 0;
            _pendingFloor = PlayerPrefs.GetInt(K_PENDING_FLOOR, 0);
            _lastName = PlayerPrefs.GetString(K_LAST_NAME, "");
            _lastJob = PlayerPrefs.GetString(K_LAST_JOB, "");
            _pendingSpecialPick = PlayerPrefs.GetInt(K_SPEC_PICK, 0) == 1;
            _pendingSpecialBanner = PlayerPrefs.GetInt(K_SPEC_PENDING, 0) == 1;
            _lastSpecialGot = PlayerPrefs.GetInt(K_SPEC_GOT, 0);
            _lastSpecialName = PlayerPrefs.GetString(K_LAST_SPEC_NAME, "");
            _lastSpecialJob = PlayerPrefs.GetString(K_LAST_SPEC_JOB, "");
        }

        static void ReadFloors(string raw, List<int> dest)
        {
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var part in raw.Split(','))
                if (int.TryParse(part, out int floor) && !dest.Contains(floor))
                    dest.Add(floor);
            dest.Sort();
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_JOB_FLOORS, string.Join(",", _jobFloors));
            PlayerPrefs.SetString(K_SPEC_FLOORS, string.Join(",", _specFloors));
            PlayerPrefs.SetInt(K_PENDING, _pendingPicks > 0 ? 1 : 0);
            PlayerPrefs.SetInt(K_PENDING_PICKS, _pendingPicks);
            PlayerPrefs.SetInt(K_PENDING_FLOOR, _pendingFloor);
            PlayerPrefs.SetString(K_LAST_NAME, _lastName ?? "");
            PlayerPrefs.SetString(K_LAST_JOB, _lastJob ?? "");
            PlayerPrefs.SetInt(K_SPEC_PICK, _pendingSpecialPick ? 1 : 0);
            PlayerPrefs.SetInt(K_SPEC_PENDING, _pendingSpecialBanner ? 1 : 0);
            PlayerPrefs.SetInt(K_SPEC_GOT, _lastSpecialGot);
            PlayerPrefs.SetString(K_LAST_SPEC_NAME, _lastSpecialName ?? "");
            PlayerPrefs.SetString(K_LAST_SPEC_JOB, _lastSpecialJob ?? "");
            PlayerPrefs.Save();
        }
    }
}
