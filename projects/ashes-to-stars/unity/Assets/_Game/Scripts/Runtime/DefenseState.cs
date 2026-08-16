using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 수비대 배치(§13-5). 로스터 인덱스 최대 5.
    ///
    /// 침략 본게임(비동기 공성)은 열지 않는다. 이 목록의 소비처는
    /// 「출전 파티에서 빠진다」와 「화면에 수비 N/5가 보인다」뿐이다.
    /// 배치만 저장하고 출전이 그대로면 정의만 있는 것과 같다.
    /// </summary>
    public static class DefenseState
    {
        public const int MaxSlots = 5;
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
                if (idx < 0 || idx >= roster.Count || !LifeSystem.IsAvailable(roster[idx]))
                    _slots.RemoveAt(i);
            }
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
