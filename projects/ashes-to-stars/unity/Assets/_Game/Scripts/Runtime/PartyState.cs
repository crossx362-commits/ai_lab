using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 출전 파티 편성 (명세 S3 · 기획서 §3·§9 "최대 5인").
    ///
    /// 왜 지금 만드나: §21-1i에서 **구성이 실제로 결과를 가른다**는 것이 5회 중앙값으로 확정됐다
    /// (딜특화 0.72배 · 힐러없음 0.67배 · 도발OFF 0.86배). 구성이 결과를 가르는데
    /// 플레이어가 그 구성을 못 고르면, 그 설계는 게임 안에 존재하지 않는 것과 같다.
    ///
    /// 슬롯은 **로스터 인덱스**로 들고 있는다 — 캐릭터 객체를 들고 있으면
    /// 삭제(§4 3회 사망)된 뒤에도 참조가 남아 유령이 출전한다.
    /// </summary>
    public static class PartyState
    {
        public sealed class SortieCombatant
        {
            public string Job;
            public AdvancementTier Advancement;
            public int SkillCount => Advancement == AdvancementTier.Basic ? 2 : 4;
        }

        public const int MaxSlots = 5;      // ✅ §9 최대 5인
        const string K_SLOTS = "ats.party";

        static readonly List<int> _slots = new List<int>();
        static bool _loaded;

        public static IReadOnlyList<int> Slots { get { Load(); return _slots; } }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            string raw = PlayerPrefs.GetString(K_SLOTS, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var part in raw.Split(','))
                    if (int.TryParse(part, out int i)) _slots.Add(i);

            Prune();
            if (_slots.Count == 0) AutoFill();
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_SLOTS, string.Join(",", _slots));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 출전 불가가 된 슬롯을 떼어낸다(삭제·회복 중·인덱스 이탈).
        /// 편성 화면을 열지 않고 바로 전투로 가는 경로가 있으므로 **읽을 때마다** 검사한다 —
        /// 화면에서만 걸러내면 그 화면을 안 거친 경로로 유령이 출전한다.
        /// </summary>
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

        /// <summary>비어 있으면 출전 가능한 캐릭터로 채운다 — 빈 파티로 전투에 들어가는 일은 없어야 한다.</summary>
        static void AutoFill()
        {
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count && _slots.Count < MaxSlots; i++)
                if (LifeSystem.IsAvailable(roster[i])) _slots.Add(i);
            Save();
        }

        public static bool Contains(int rosterIndex) { Load(); return _slots.Contains(rosterIndex); }

        /// <summary>슬롯에 넣거나 뺀다. 넣을 자리가 없거나 출전 불가면 false.</summary>
        public static bool Toggle(int rosterIndex)
        {
            Load();
            if (_slots.Contains(rosterIndex)) { _slots.Remove(rosterIndex); Save(); return true; }

            var roster = LifeSystem.GetCharacters();
            if (rosterIndex < 0 || rosterIndex >= roster.Count) return false;
            if (!LifeSystem.IsAvailable(roster[rosterIndex])) return false;   // 회복 중·삭제는 못 넣는다
            if (_slots.Count >= MaxSlots) return false;

            _slots.Add(rosterIndex);
            Save();
            return true;
        }

        /// <summary>지금 편성이 출전 가능한가. 한 명이라도 있어야 한다.</summary>
        public static bool CanSortie { get { Load(); Prune(); return _slots.Count > 0; } }

        /// <summary>전투가 읽는 직업 목록. 순서가 곧 슬롯 1~5다(1번이 탱 자리, §10-4 진형).</summary>
        public static List<string> SortieJobs()
        {
            var jobs = new List<string>();
            foreach (var combatant in SortieCombatants()) jobs.Add(combatant.Job);
            return jobs;
        }

        /// <summary>
        /// 전투가 읽는 캐릭터 계약. 직업명만 넘기면 기본직업을 1차 아키타입으로 어댑트하는 순간
        /// 전직 단계가 사라져 기본 2스킬과 1차 4스킬을 가를 수 없다.
        /// </summary>
        public static List<SortieCombatant> SortieCombatants()
        {
            Load(); Prune();
            var roster = LifeSystem.GetCharacters();
            var result = new List<SortieCombatant>();
            foreach (int i in _slots)
            {
                if (i < 0 || i >= roster.Count) continue;
                var character = roster[i];
                result.Add(new SortieCombatant
                {
                    Job = CombatJob(character),
                    Advancement = character.Advancement,
                });
            }
            return result;
        }

        // W3Party는 기존 1차 Job enum으로 전투한다. 기본직업 전용 스킬이
        // 들어오기 전까지는 같은 역할의 프로토타입 아키타입을 사용한다.
        static string CombatJob(CharacterRecord character) => character.Advancement != AdvancementTier.Basic
            ? character.Job
            : character.Job switch
            {
                "탱" => "수호기사",
                "딜" => "검사",
                "힐" => "사제",
                "버퍼" => "음유시인",
                _ => character.Job,
            };

        /// <summary>전투 결과를 로스터에 반영한 뒤 호출 — 죽은 캐릭터가 슬롯에 남지 않게.</summary>
        public static void Refresh() { Load(); Prune(); Save(); }

        /// <summary>테스트·디버그용.</summary>
        public static void ResetForTest()
        {
            _slots.Clear(); _loaded = false;
            PlayerPrefs.DeleteKey(K_SLOTS);
        }
    }
}
