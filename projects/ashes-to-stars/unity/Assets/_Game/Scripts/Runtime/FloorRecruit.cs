using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 층 클리어 보상(오너 2026-08-16 20:47).
    /// 탑 층을 처음 깨면 기본직업 4종 중 1명을 고른다.
    /// 레이드(5·10·…·100)는 같은 클리어에서 특수 직업 증표 2장을 확률로 더 준다.
    /// 직업명(사신 등)은 💡라 증표만. 던전·필드·재도전은 안 준다.
    /// </summary>
    public static class FloorRecruit
    {
        public const float RaidSpecialChance = 0.20f;
        public const int RaidSpecialTokens = 2;

        const string K_JOB_FLOORS = "ats.floor_recruit_jobs";
        const string K_SPEC_FLOORS = "ats.floor_recruit_spec";
        const string K_PENDING = "ats.floor_recruit_pending";
        const string K_PENDING_FLOOR = "ats.floor_recruit_pfloor";
        const string K_LAST_NAME = "ats.floor_recruit_name";
        const string K_LAST_JOB = "ats.floor_recruit_job";
        const string K_SPEC_PENDING = "ats.floor_recruit_spec_pending";
        const string K_SPEC_GOT = "ats.floor_recruit_spec_got";

        static bool _loaded;
        static readonly List<int> _jobFloors = new List<int>();
        static readonly List<int> _specFloors = new List<int>();
        static bool _pendingJob;
        static int _pendingFloor;
        static string _lastName = "";
        static string _lastJob = "";
        static bool _pendingSpecial;
        static int _lastSpecialGot;

        public static bool PendingJob { get { EnsureLoaded(); return _pendingJob; } }
        public static int PendingFloor { get { EnsureLoaded(); return _pendingFloor; } }
        public static string LastGrantedName { get { EnsureLoaded(); return _lastName; } }
        public static string LastGrantedJob { get { EnsureLoaded(); return _lastJob; } }
        public static bool PendingSpecialBanner { get { EnsureLoaded(); return _pendingSpecial; } }
        public static int LastSpecialTokensGot { get { EnsureLoaded(); return _lastSpecialGot; } }
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
                _pendingJob = true;
                _pendingFloor = floor;
                changed = true;
            }
            if (IsRaidFloor(floor) && !_specFloors.Contains(floor))
            {
                _specFloors.Add(floor);
                _specFloors.Sort();
                if (RollSpecial())
                {
                    int got = 0;
                    for (int i = 0; i < RaidSpecialTokens; i++)
                        if (GameState.Gain(Economy.LifeItem.SpecialJobToken))
                            got++;
                    _lastSpecialGot = got;
                    _pendingSpecial = got > 0;
                }
                changed = true;
            }
            if (changed) Save();
            return changed;
        }

        /// <summary>대기 중인 층 보상으로 기본직업 1명을 명부에 넣는다. 잘못된 직업·대기 없음은 false.</summary>
        public static bool TryClaim(string job)
        {
            if (QaForcedOff()) return false;
            EnsureLoaded();
            if (!_pendingJob) return false;
            var ch = LifeSystem.AddBasicRecruit(job);
            if (ch == null) return false;
            _pendingJob = false;
            _lastName = ch.Name;
            _lastJob = ch.Job;
            Save();
            return true;
        }

        public static void AckSpecialBanner()
        {
            EnsureLoaded();
            if (!_pendingSpecial) return;
            _pendingSpecial = false;
            Save();
        }

        /// <summary>시각 QA. QA_FLOOR_REWARD=1이면 1층 직업 선택과 증표 2장 배너를 심는다.</summary>
        public static void SeedQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_FLOOR_REWARD");
            if (raw != "1" && raw != "true") return;
            EnsureLoaded();
            if (!_jobFloors.Contains(1)) _jobFloors.Add(1);
            _jobFloors.Sort();
            _pendingJob = true;
            _pendingFloor = 1;
            if (!_specFloors.Contains(5)) _specFloors.Add(5);
            _specFloors.Sort();
            _lastSpecialGot = RaidSpecialTokens;
            _pendingSpecial = true;
            int have = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);
            for (int i = have; i < RaidSpecialTokens; i++)
                GameState.Gain(Economy.LifeItem.SpecialJobToken);
            Save();
            if (string.IsNullOrEmpty(GameFlow.LastBattleSummary))
                GameFlow.LastBattleSummary = "1층 돌파 — 기본 직업을 고른다";
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_JOB_FLOORS);
            PlayerPrefs.DeleteKey(K_SPEC_FLOORS);
            PlayerPrefs.DeleteKey(K_PENDING);
            PlayerPrefs.DeleteKey(K_PENDING_FLOOR);
            PlayerPrefs.DeleteKey(K_LAST_NAME);
            PlayerPrefs.DeleteKey(K_LAST_JOB);
            PlayerPrefs.DeleteKey(K_SPEC_PENDING);
            PlayerPrefs.DeleteKey(K_SPEC_GOT);
            _loaded = false;
            _jobFloors.Clear();
            _specFloors.Clear();
            _pendingJob = false;
            _pendingFloor = 0;
            _lastName = "";
            _lastJob = "";
            _pendingSpecial = false;
            _lastSpecialGot = 0;
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
            _jobFloors.Clear();
            _specFloors.Clear();
            _pendingJob = false;
            _pendingFloor = 0;
            _lastName = "";
            _lastJob = "";
            _pendingSpecial = false;
            _lastSpecialGot = 0;
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
            _pendingJob = PlayerPrefs.GetInt(K_PENDING, 0) == 1;
            _pendingFloor = PlayerPrefs.GetInt(K_PENDING_FLOOR, 0);
            _lastName = PlayerPrefs.GetString(K_LAST_NAME, "");
            _lastJob = PlayerPrefs.GetString(K_LAST_JOB, "");
            _pendingSpecial = PlayerPrefs.GetInt(K_SPEC_PENDING, 0) == 1;
            _lastSpecialGot = PlayerPrefs.GetInt(K_SPEC_GOT, 0);
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
            PlayerPrefs.SetInt(K_PENDING, _pendingJob ? 1 : 0);
            PlayerPrefs.SetInt(K_PENDING_FLOOR, _pendingFloor);
            PlayerPrefs.SetString(K_LAST_NAME, _lastName ?? "");
            PlayerPrefs.SetString(K_LAST_JOB, _lastJob ?? "");
            PlayerPrefs.SetInt(K_SPEC_PENDING, _pendingSpecial ? 1 : 0);
            PlayerPrefs.SetInt(K_SPEC_GOT, _lastSpecialGot);
            PlayerPrefs.Save();
        }
    }
}
