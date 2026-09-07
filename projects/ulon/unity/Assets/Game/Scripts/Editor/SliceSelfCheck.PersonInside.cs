using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **사람이 구조물 안에 서 있지 않은가**(검수 지시 2026-09-07, 마을 배치 랩).
    ///
    /// 발단: `50_villagers` 1·3번 타일에서 NPC가 유리벽 안·지붕 뒤에 서 있는 것처럼 보였다.
    /// 「보인다」로 고치지 않는다 — **몇 명이 실제로 겹쳐 있는지부터 센다**(화면과 계측이 어긋나면
    /// 무엇이 거기 있는지부터 센다, 원장).
    ///
    /// 대상은 **사람 전수**(`CharacterController`를 단 액터). 판정은 발 위치의 캡슐이 구조물
    /// 콜라이더와 겹치는가 — 격자 시선 게이트의 `CanStand`와 같은 기하다.
    /// **제외(선언)**: 지형(설 바닥), 자기 자신, 그리고 다른 사람·짐승(사람끼리 겹침은 다른 축이고
    /// 여기서 섞으면 「구조물 안에 섰다」는 판정이 흐려진다 — 필요하면 별도 게이트로).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static List<Transform> People()
        {
            var found = new List<Transform>();
            var all = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                found.Add(all[i].transform);
            return found;
        }

        static List<string> StructuresAround(Transform person)
        {
            var blockers = new List<string>();
            var feet = person.position;
            var hits = Physics.OverlapCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.6f, 0.32f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsTerrainCollider(hits[i]) || hits[i].transform.IsChildOf(person))
                    continue;
                if (hits[i].transform.root.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                    continue;                                   // 사람·짐승 겹침은 다른 축(선언 제외)
                blockers.Add(GroundFit.NodePath(hits[i].transform));
            }
            return blockers;
        }

        static void AssertNobodyInsideStructure()
        {
            var people = People();
            if (people.Count == 0)
                throw new InvalidOperationException("사람을 하나도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var bad = new List<string>();
            for (int i = 0; i < people.Count; i++)
            {
                var blockers = StructuresAround(people[i]);
                if (blockers.Count > 0)
                    bad.Add(people[i].name + " ← " + string.Join("+", blockers.GetRange(0, Mathf.Min(3, blockers.Count))));
            }
            // **「낀 사람」과 「갇힌 사람」은 다른 축이다** — 콜라이더와 겹치지 않아도 사방이 벽이면
            // 화면에서는 「유리벽 안에 선 NPC」로 읽힌다(검수 관찰 `50_villagers` 1·3번 타일).
            // 여기서는 세기만 한다(판정은 배치 결정이라 검수 몫).
            var boxed = new List<string>();
            for (int i = 0; i < people.Count; i++)
            {
                int walls = 0;
                var from = people[i].position + Vector3.up * 1.2f;
                for (int a = 0; a < 8; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;
                    var hits = Physics.RaycastAll(from, dir, 3f, ~0, QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < hits.Length; h++)
                    {
                        if (IsTerrainCollider(hits[h].collider) || hits[h].transform.IsChildOf(people[i]))
                            continue;
                        if (hits[h].transform.root.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                            continue;
                        walls++;
                        break;
                    }
                }
                if (walls >= 5)
                    boxed.Add(people[i].name + " " + walls + "/8면");
            }
            Debug.Log("[Ulon] 사람 겹침 — 사람 " + people.Count + "명 중 구조물에 낀 사람 " + bad.Count + "명" +
                      (bad.Count > 0 ? ": " + string.Join(", ", bad) : "") +
                      " · 사방이 막힌(3m 안 5면 이상) 사람 " + boxed.Count + "명" +
                      (boxed.Count > 0 ? ": " + string.Join(", ", boxed) : ""));
            if (bad.Count > 0)
                throw new InvalidOperationException("구조물 안에 선 사람 " + bad.Count + "명: " +
                    string.Join(", ", bad) + " — 벽 안에 선 사람은 화면에서 유리벽 뒤에 갇힌 것으로 읽힙니다(§8.1). " +
                    "개별로 밀지 말고 **세우는 자리 규칙**을 고치십시오.");
        }

        /// <summary>NC — 한 명을 **실제로 벽 안으로 밀어 넣으면** 빨간불이어야 한다.</summary>
        static void AssertNobodyInsideStructureNegativeControl()
        {
            var people = People();
            if (people.Count == 0)
                throw new InvalidOperationException("사람 겹침 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            // 벽을 하나 찾아 그 안으로 옮긴다 — 「가상의 결함」이 아니라 실제 겹침을 만든다.
            Transform wall = null;
            var all = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length && wall == null; i++)
            {
                if (IsTerrainCollider(all[i]))
                    continue;
                if (all[i].transform.root.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                    continue;
                if (all[i].bounds.size.y > 1.2f && all[i].bounds.size.y < 20f)
                    wall = all[i].transform;
            }
            if (wall == null)
                throw new InvalidOperationException("사람 겹침 NC용 구조물을 못 찾았습니다.");
            var victim = people[0];
            var saved = victim.position;
            var cc = victim.GetComponent<CharacterController>();
            bool red = false;
            try
            {
                if (cc != null) cc.enabled = false;
                var b = wall.GetComponent<Collider>().bounds;
                victim.position = new Vector3(b.center.x, b.min.y, b.center.z);
                try { AssertNobodyInsideStructure(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                victim.position = saved;
                if (cc != null) cc.enabled = true;
            }
            if (!red)
                throw new InvalidOperationException("사람 겹침 네거티브 컨트롤 실패 — " + victim.name +
                    "을 " + wall.name + " 안으로 밀어 넣었는데 통과했습니다.");
            Debug.Log("[Ulon] 사람 겹침 네거티브 컨트롤 통과 — 한 명을 " + wall.name + " 안에 넣으면 FAIL");
        }
    }
}
