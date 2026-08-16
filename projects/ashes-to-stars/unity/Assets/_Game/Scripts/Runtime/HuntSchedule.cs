using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 필드 자동사냥 일정(§6 ✅). 유저가 출전 편성을 보내 두면 허브에 있어도 돈다.
    /// 일과표 UI·조건부 지시·오프라인 60%는 💡라 안 넣는다.
    /// 사망 카운트는 안 오른다(§6 오프라인 정산과 같은 경계). 상한 12시간.
    /// QA_NO면 시작·정산이 0.
    /// </summary>
    public static class HuntSchedule
    {
        public const string EnvShow = "QA_HUNT_SCHEDULE";
        public const string EnvNo = "QA_NO_HUNT_SCHEDULE";
        public const float CapSeconds = 12f * 3600f;

        const string K_ON = "ats.hunt.sched.on";
        const string K_SEC = "ats.hunt.sched.sec";
        const string K_WHO = "ats.hunt.sched.who";

        static bool _loaded;
        static bool _qaSeeded;
        static bool _on;
        static float _sec;
        static readonly List<int> _who = new List<int>();

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Running { get { Load(); return _on && !Blocked; } }
        public static float Elapsed { get { Load(); return _sec; } }
        public static int Count { get { Load(); return _who.Count; } }

        public static bool Contains(int rosterIndex)
        {
            Load();
            if (!_on || Blocked) return false;
            return _who.Contains(rosterIndex);
        }

        public static bool TryStart()
        {
            Load();
            if (Blocked || _on) return false;
            var slots = PartyState.Slots;
            if (slots.Count == 0) return false;
            _who.Clear();
            for (int i = 0; i < slots.Count; i++)
                if (!_who.Contains(slots[i])) _who.Add(slots[i]);
            if (_who.Count == 0) return false;
            _on = true;
            _sec = 0f;
            Save();
            return true;
        }

        public static bool Stop()
        {
            Load();
            if (!_on) return false;
            Settle();
            _on = false;
            _sec = 0f;
            _who.Clear();
            Save();
            return true;
        }

        public static void Tick(float dt)
        {
            Load();
            if (!_on || Blocked) return;
            if (dt <= 0f) return;
            int before = (int)_sec;
            _sec += dt;
            if (_sec > CapSeconds) _sec = CapSeconds;
            if ((int)_sec != before) Save();
        }

        public static long PendingGold()
        {
            Load();
            if (!_on || Blocked || _sec <= 0f) return 0;
            return Economy.WaveHuntGold(GameState.Tier, _sec);
        }

        public static long PendingExp()
        {
            Load();
            if (!_on || Blocked || _sec <= 0f) return 0;
            return Economy.WaveHuntExp(GameState.Tier, _sec);
        }

        public static string Line()
        {
            if (Blocked) return "일정 사냥 없음";
            if (!Running) return "일정 사냥 꺼짐";
            return $"일정 사냥 {Count}명 · {FormatTime(_sec)} · {Economy.FormatCurrency(PendingGold())}(§6)";
        }

        public static string CardTitle() =>
            Running ? "일정 사냥 중" : "일정 사냥";

        public static string CardBody()
        {
            if (Blocked) return "일정 사냥 없음";
            if (Running)
                return $"{Line()} — 탭하면 정산. 사망 없음(§6)";
            return "편성을 보내 두면 영지에서도 돈다. 사망 없음 · 상한 12시간(§6)";
        }

        static void Settle()
        {
            if (Blocked || _sec <= 0f || _who.Count == 0) return;
            long gold = Economy.WaveHuntGold(GameState.Tier, _sec);
            if (gold > 0) GameState.Earn(gold);
            AwardExp(Economy.WaveHuntExp(GameState.Tier, _sec));
        }

        static void AwardExp(long total)
        {
            if (total <= 0) return;
            var roster = LifeSystem.GetCharacters();
            var live = new List<CharacterRecord>();
            long levelSum = 0;
            for (int i = 0; i < _who.Count; i++)
            {
                int idx = _who[i];
                if (idx < 0 || idx >= roster.Count) continue;
                var c = roster[idx];
                if (c == null || c.IsDeleted) continue;
                live.Add(c);
                levelSum += c.Level;
            }
            if (live.Count == 0 || levelSum <= 0) return;
            long given = 0;
            for (int i = 0; i < live.Count; i++)
            {
                long share = i == live.Count - 1
                    ? total - given
                    : total * live[i].Level / levelSum;
                given += share;
                LifeSystem.AddExp(live[i], share);
            }
            LifeSystem.PersistRoster();
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, (int)seconds);
            if (s >= 3600) return $"{s / 3600}시간 {(s % 3600) / 60}분";
            if (s >= 60) return $"{s / 60}분 {s % 60}초";
            return $"{s}초";
        }

        public static void SeedQaIfRequested()
        {
            if (Blocked) return;
            string raw = Environment.GetEnvironmentVariable(EnvShow);
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            _ = LifeSystem.GetCharacters();
            _ = PartyState.Slots;
            if (!_on)
            {
                if (PartyState.Slots.Count == 0) return;
                TryStart();
            }
            if (_on && _sec < 1f) Tick(3600f);
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_ON);
            PlayerPrefs.DeleteKey(K_SEC);
            PlayerPrefs.DeleteKey(K_WHO);
            PlayerPrefs.Save();
            _on = false;
            _sec = 0f;
            _who.Clear();
            _loaded = true;
            _qaSeeded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
            _qaSeeded = false;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _on = PlayerPrefs.GetInt(K_ON, 0) != 0;
            _sec = PlayerPrefs.GetFloat(K_SEC, 0f);
            _who.Clear();
            string raw = PlayerPrefs.GetString(K_WHO, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (string part in raw.Split(','))
                    if (int.TryParse(part, out int i)) _who.Add(i);
            if (_sec > CapSeconds) _sec = CapSeconds;
            if (_who.Count == 0) _on = false;
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_ON, _on ? 1 : 0);
            PlayerPrefs.SetFloat(K_SEC, _sec);
            PlayerPrefs.SetString(K_WHO, string.Join(",", _who));
            PlayerPrefs.Save();
        }
    }
}
