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
        /// <summary>
        /// **이 조각이 사람·짐승의 일부인가**(제외 판정의 단일 원장).
        ///
        /// 예전엔 `t.root.GetComponentInChildren&lt;WorldBody&gt;()`로 물었다 — 뿌리에 몸이 하나라도
        /// 매달려 있으면 **그 뿌리 아래 전부**를 「사람·짐승」으로 보고 뺐다. 그래서 마구간지기가
        /// 마구간의 자식이라는 이유만으로 **마구간 전체(울타리·기둥·지붕·사슴)가 자 밖으로 사라졌고**,
        /// 사슴이 마구간지기 몸에 16% 박혀 있는데도 「낀 사람 0명」이 나왔다.
        /// **자를 느슨하게 하는 규칙이 곧 구멍이다** — 제외는 그 조각 **자신의 조상**에게만 묻는다.
        /// </summary>
        static bool IsBodyPart(Transform t)
        {
            return t != null && t.GetComponentInParent<Ulon.Server.WorldBody>() != null;
        }

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
                if (IsBodyPart(hits[i].transform))
                    continue;                                   // 사람·짐승 겹침은 다른 축(선언 제외)
                blockers.Add(GroundFit.NodePath(hits[i].transform));
            }
            return blockers;
        }

        /// <summary>
        /// **사람 몸에 조각이 겹쳐 있는가** — 콜라이더가 아니라 **보이는 것**으로 잰다(검수 지시 2026-09-08).
        ///
        /// 왜 넓혔나: 훈련사 머리에 지붕 조각(`roof-gable-end`)이 박혀 있었는데 이 게이트는
        /// 「낀 사람 0명」이라고 답했다. 캡슐이 짧아서가 아니라(0.4~1.6m+반지름이라 머리까지 잰다)
        /// **그 지붕 조각에 콜라이더가 없어서**다 — 물리로 재는 자는 장식을 못 본다.
        /// 그래서 몸 바운드와 **렌더러 바운드**가 겹치는지 따로 본다. 사람·짐승은 뺀다(다른 축).
        /// </summary>
        static List<string> PiecesInBody(Transform person)
        {
            var found = new List<string>();
            if (!GroundFit.BodyBounds(person, out Bounds body))
                return found;
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < rends.Length; i++)
            {
                var t = rends[i].transform;
                if (!rends[i].enabled || rends[i] is ParticleSystemRenderer)
                    continue;
                if (t.IsChildOf(person) || t.GetComponent<Terrain>() != null)
                    continue;
                if (IsBodyPart(t))
                    continue;                                   // 사람·짐승 겹침은 다른 축
                var b = rends[i].bounds;
                if (b.size.x > 20f || b.size.z > 20f)
                    continue;                                   // 월드 규모 판(지형·수면)은 겹침이 무의미
                if (!b.Intersects(body))
                    continue;
                // 스치는 것이 아니라 **몸 안에 박힌 것**만 — 겹침 부피가 몸의 5% 이상.
                var min = Vector3.Max(b.min, body.min);
                var max = Vector3.Min(b.max, body.max);
                var overlap = Vector3.Max(max - min, Vector3.zero);
                float vol = overlap.x * overlap.y * overlap.z;
                float bodyVol = Mathf.Max(0.0001f, body.size.x * body.size.y * body.size.z);
                float share = vol / bodyVol;
                if (share >= 0.01f && share < 0.05f)
                    Debug.Log("[Ulon] 몸에 스친 조각(판정 아래) — " + person.name + " ← " +
                              GroundFit.NodePath(t) + " " + (share * 100f).ToString("0.0") + "%");
                if (share < 0.05f)
                    continue;
                found.Add(GroundFit.NodePath(t) + "(" + (vol / bodyVol * 100f).ToString("0") + "%)");
            }
            return found;
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
                blockers.AddRange(PiecesInBody(people[i]));      // 콜라이더 없는 장식까지 본다
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
                        if (IsBodyPart(hits[h].transform))
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
                if (IsBodyPart(all[i].transform))
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
