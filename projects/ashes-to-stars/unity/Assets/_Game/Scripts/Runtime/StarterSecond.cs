using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 시작 로스터 2명(§3 ✅). 첫 캐릭터는 타이틀에서 고르고,
    /// 튜토리얼 5분이 지나면 두 번째를 고른다. 무료 영입 3회(💡)는 안 넣는다.
    /// </summary>
    public static class StarterSecond
    {
        public const float UnlockSeconds = 300f;
        public const string EnvShow = "QA_STARTER_SECOND";
        public const string EnvNo = "QA_NO_STARTER_SECOND";

        const string K_STARTED = "ats.starter_second_started";
        const string K_PLAYED = "ats.starter_second_play";
        const string K_PENDING = "ats.starter_second_pending";
        const string K_CLAIMED = "ats.starter_second_claimed";
        const string K_NAME = "ats.starter_second_name";
        const string K_JOB = "ats.starter_second_job";

        static bool _loaded;
        static bool _started;
        static bool _pending;
        static bool _claimed;
        static float _played;
        static string _lastName = "";
        static string _lastJob = "";

        public static bool Pending { get { EnsureLoaded(); return _pending && !_claimed && !Blocked; } }
        public static bool Claimed { get { EnsureLoaded(); return _claimed; } }
        public static bool Started { get { EnsureLoaded(); return _started; } }
        public static float PlayedSeconds { get { EnsureLoaded(); return _played; } }
        public static string LastName { get { EnsureLoaded(); return _lastName; } }
        public static string LastJob { get { EnsureLoaded(); return _lastJob; } }
        public static string PickTitle => "두 번째 동료를 고른다 — 시작 로스터 2명(§3)";
        public static string PickSubtitle => "5분이 지났다. Lv10 기본직업 · 같은 역할도 된다";

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void OnNewGame()
        {
            _loaded = true;
            _started = true;
            _pending = false;
            _claimed = false;
            _played = 0f;
            _lastName = "";
            _lastJob = "";
            Save();
        }

        /// <summary>플레이 화면에서만 쌓인다. 5분이 되면 대기만 연다 — 자동으로 넣지 않는다.</summary>
        public static void Tick(float dt)
        {
            if (Blocked) return;
            if (StarterPick.Open) return;
            EnsureLoaded();
            if (!_started || _claimed) return;
            _played += Mathf.Max(0f, dt);
            if (!_pending && _played + 0.0001f >= UnlockSeconds)
            {
                _pending = true;
                Save();
            }
        }

        public static bool TryClaim(string job)
        {
            if (Blocked) return false;
            EnsureLoaded();
            if (!_pending || _claimed) return false;
            var ch = LifeSystem.AddStarterCompanion(job);
            if (ch == null) return false;
            _pending = false;
            _claimed = true;
            _lastName = ch.Name;
            _lastJob = ch.Job;
            Save();
            var roster = LifeSystem.GetCharacters();
            int idx = roster.IndexOf(ch);
            if (idx >= 0 && !PartyState.Contains(idx))
                PartyState.Toggle(idx);
            return true;
        }

        public static void SeedQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable(EnvShow);
            if (string.IsNullOrEmpty(raw)) return;
            if (Blocked) return;
            bool claim = raw == "2" || string.Equals(raw, "claim", StringComparison.OrdinalIgnoreCase);
            bool show = claim || raw == "1"
                || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            if (!show) return;
            if (!LifeSystem.HasSavedRoster() || LifeSystem.GetCharacters().Count != 1)
                LifeSystem.BeginNewGame("힐");
            EnsureLoaded();
            _started = true;
            _played = UnlockSeconds;
            _pending = !_claimed;
            Save();
            if (claim && _pending)
                TryClaim("탱");
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_STARTED);
            PlayerPrefs.DeleteKey(K_PLAYED);
            PlayerPrefs.DeleteKey(K_PENDING);
            PlayerPrefs.DeleteKey(K_CLAIMED);
            PlayerPrefs.DeleteKey(K_NAME);
            PlayerPrefs.DeleteKey(K_JOB);
            PlayerPrefs.Save();
            ForgetInMemoryForTest();
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
            _started = false;
            _pending = false;
            _claimed = false;
            _played = 0f;
            _lastName = "";
            _lastJob = "";
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _started = PlayerPrefs.GetInt(K_STARTED, 0) == 1;
            _pending = PlayerPrefs.GetInt(K_PENDING, 0) == 1;
            _claimed = PlayerPrefs.GetInt(K_CLAIMED, 0) == 1;
            _played = PlayerPrefs.GetFloat(K_PLAYED, 0f);
            _lastName = PlayerPrefs.GetString(K_NAME, "");
            _lastJob = PlayerPrefs.GetString(K_JOB, "");
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_STARTED, _started ? 1 : 0);
            PlayerPrefs.SetInt(K_PENDING, _pending ? 1 : 0);
            PlayerPrefs.SetInt(K_CLAIMED, _claimed ? 1 : 0);
            PlayerPrefs.SetFloat(K_PLAYED, _played);
            PlayerPrefs.SetString(K_NAME, _lastName ?? "");
            PlayerPrefs.SetString(K_JOB, _lastJob ?? "");
            PlayerPrefs.Save();
        }
    }
}
