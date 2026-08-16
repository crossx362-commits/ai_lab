using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 수비대 배치(§13-5). 로스터 인덱스 최대 5.
    ///
    /// 침략 본게임(비동기 공성)은 열지 않는다. 이 목록의 소비처는
    /// 「출전 파티에서 빠진다」와 「전멸 뒤 12시간 출전 불가」와
    /// 「화면에 수비 N/5가 보인다」다. 보호막과 같은 시계를 써야
    /// 보호막이 끝났는데 수비대가 비는 무방비 창이 안 생긴다(§15).
    /// </summary>
    public static class DefenseState
    {
        public const int MaxSlots = 5;
        public const string EnvShow = "QA_DEFENSE_RECOVER";
        const string K_SLOTS = "ats.defense";

        static readonly List<int> _slots = new List<int>();
        static bool _loaded;

        public static IReadOnlyList<int> Slots { get { Load(); return _slots; } }
        public static int Count { get { Load(); Prune(); return _slots.Count; } }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _slots.Clear();
            string raw = PlayerPrefs.GetString(K_SLOTS, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var part in raw.Split(','))
                    if (int.TryParse(part, out int i)) _slots.Add(i);
            Prune();
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_SLOTS, string.Join(",", _slots));
            PlayerPrefs.Save();
        }

        static void Prune()
        {
            var roster = LifeSystem.GetCharacters();
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                int idx = _slots[i];
                // 회복 중인 수비는 남긴다 — 빼면 보호막이 끝날 때 수비가 비어
                // 무방비 창이 생긴다(§15·§19-C1). 삭제만 거른다.
                if (idx < 0 || idx >= roster.Count || roster[idx].IsDeleted)
                    _slots.RemoveAt(i);
            }
        }

        /// <summary>수비대가 전멸하면 전원 12시간 출전 불가. 목숨은 안 깎는다(§13-5·§15).</summary>
        public static int ApplyPvpRecover()
        {
            Load();
            var roster = LifeSystem.GetCharacters();
            var copy = new List<int>(_slots);
            int n = 0;
            for (int i = 0; i < copy.Count; i++)
            {
                int idx = copy[i];
                if (idx < 0 || idx >= roster.Count) continue;
                var ch = roster[idx];
                if (ch == null || ch.IsDeleted) continue;
                LifeSystem.RegisterDeath(ch, isPvp: true);
                if (LifeSystem.GetRecoveryTimeRemaining(ch) > 0) n++;
            }
            return n;
        }

        /// <summary>QA_DEFENSE_RECOVER=1이면 첫 생존 캐릭터를 수비에 세우고 12시간 회복을 건다.</summary>
        public static void SeedQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable(EnvShow);
            if (raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return;
            if (LifeSystem.RecoverBlocked) return;
            Load();
            var roster = LifeSystem.GetCharacters();
            int idx = -1;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] != null && !roster[i].IsDeleted) { idx = i; break; }
            }
            if (idx < 0) return;
            if (Contains(idx) && LifeSystem.GetRecoveryTimeRemaining(roster[idx]) > 0)
                return;
            if (!Contains(idx)) Toggle(idx);
            ApplyPvpRecover();
        }

        /// <summary>로스터에서 한 명을 지운 뒤 인덱스를 당긴다. 파티와 같은 유령 슬롯을 막는다.</summary>
        public static void NotifyRosterRemoved(int index)
        {
            Load();
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i] == index) _slots.RemoveAt(i);
                else if (_slots[i] > index) _slots[i]--;
            }
            Save();
        }

        public static bool Contains(int rosterIndex)
        {
            Load();
            Prune();
            return _slots.Contains(rosterIndex);
        }

        /// <summary>넣거나 뺀다. 넣으면 출전 편성에서 내린다. 출전 불가면 false.</summary>
        public static bool Toggle(int rosterIndex)
        {
            Load();
            Prune();
            if (_slots.Contains(rosterIndex))
            {
                _slots.Remove(rosterIndex);
                Save();
                return true;
            }

            var roster = LifeSystem.GetCharacters();
            if (rosterIndex < 0 || rosterIndex >= roster.Count) return false;
            if (!LifeSystem.IsAvailable(roster[rosterIndex])) return false;
            if (_slots.Count >= MaxSlots) return false;

            if (PartyState.Contains(rosterIndex))
                PartyState.Toggle(rosterIndex);

            _slots.Add(rosterIndex);
            Save();
            return true;
        }

        public static void ResetForTest()
        {
            _slots.Clear();
            _loaded = false;
            PlayerPrefs.DeleteKey(K_SLOTS);
        }

        public static void ForgetInMemoryForTest()
        {
            _slots.Clear();
            _loaded = false;
        }
    }
}
