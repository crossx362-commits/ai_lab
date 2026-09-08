using System;
using System.Collections.Generic;
using Ulon.Server;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **배우 안에 배우가 없는가**(2026-09-09, 재드레싱 사슬을 재다 드러났다).
    ///
    /// 사냥터 기사를 찾는 게이트가 `GameObject.Find("Knight")`로 재는데, 씬에는 **기사가 둘**이었다:
    /// 진짜(루트 `Knight`, `CharacterController` 있음, 사냥 구역 z 13.2)와, 그 **밑에 자식으로 매달린**
    /// 또 하나(`Knight/Knight`, 캡슐 없음, z 31.2로 사냥 구역 밖). 이름이 같으니 `Find`는 둘 중 아무나
    /// 집었고, 마을을 다시 드레싱해 이름이 흔들리자 그제서야 몸통 없는 쪽을 집어 빨간불이 났다 —
    /// **게이트는 내내 엉뚱한 것을 재고 통과하고 있었다.**
    ///
    /// 그래서 이름이 아니라 **자리**로 재는 자를 하나 더 세운다: 배우(`WorldBody`)는 다른 배우의 **자손일
    /// 수 없다.** 몹이 여럿인 것은 정상이고(던전 사본), 몹 **안에** 몹이 있는 것은 정상이 아니다.
    /// 같은 성질을 `VisualSliceBuilder.EnsureNoNestedActors`가 고치고 이 게이트가 지킨다.
    ///
    /// 양방향 NC: 탐침 배우를 다른 배우 밑에 매달면 빨간불, 루트로 빼면 다시 초록.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const string NestedActorProbeName = "SelfcheckNestedActorProbe";

        static void AssertNoNestedActors()
        {
            string reason = NestedActorReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>빨간불 사유(없으면 빈 문자열) — 게이트와 NC가 같은 자를 쓴다.</summary>
        static string NestedActorReason(bool log)
        {
            var bodies = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bodies.Length == 0)
                return "배우를 한 명도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).";
            var nested = new List<string>();
            for (int i = 0; i < bodies.Length; i++)
            {
                var host = NestedActorHost(bodies[i]);
                if (host != null)
                    nested.Add(bodies[i].name + "(" + bodies[i].MobId + ") ⊂ " + host.name +
                               " " + bodies[i].transform.position.ToString("F1"));
            }
            if (log)
                Debug.Log("[Ulon] 배우 겹침 — 배우 " + bodies.Length + "명 중 다른 배우 밑에 매달린 " + nested.Count + "명");
            if (nested.Count > 0)
                return "다른 배우 밑에 매달린 배우 " + nested.Count + "명: " + string.Join(", ", nested) +
                       " — 이름으로 찾는 게이트가 둘 중 아무나 집습니다(진짜 몸통 대신 껍데기를 재고 통과합니다). " +
                       "`VisualSliceBuilder.EnsureNoNestedActors`가 전수로 풀어야 합니다.";
            return "";
        }

        /// <summary>이 배우를 품고 있는 **다른 배우**(없으면 null).</summary>
        static WorldBody NestedActorHost(WorldBody body)
        {
            for (var p = body.transform.parent; p != null; p = p.parent)
            {
                var host = p.GetComponent<WorldBody>();
                if (host != null)
                    return host;
            }
            return null;
        }

        /// <summary>양방향 NC — 탐침을 배우 밑에 매달면 빨간불, 루트로 빼면 다시 초록.</summary>
        static void AssertNoNestedActorsNegativeControl()
        {
            var anchor = UnityEngine.Object.FindFirstObjectByType<WorldBody>(FindObjectsInactive.Include);
            if (anchor == null)
                throw new InvalidOperationException("NC 대상 배우가 없습니다 — 잰 것이 없습니다(0이면 실패).");
            string before = NestedActorReason(true);
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("배우 겹침 NC 실패 — 손대기 전부터 빨간불입니다: " + before);

            var probe = new GameObject(NestedActorProbeName);
            try
            {
                probe.AddComponent<WorldBody>();
                probe.transform.SetParent(anchor.transform, true);
                if (string.IsNullOrEmpty(NestedActorReason(false)))
                    throw new InvalidOperationException("배우 겹침 NC 실패 — 배우 밑에 배우를 매달았는데 통과했습니다. 빈 통과입니다.");
                probe.transform.SetParent(null, true);
                if (!string.IsNullOrEmpty(NestedActorReason(false)))
                    throw new InvalidOperationException("배우 겹침 NC 실패 — 루트로 뺐는데 빨간불이 남았습니다(자가 아무나 잡습니다).");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            if (!string.IsNullOrEmpty(NestedActorReason(false)))
                throw new InvalidOperationException("배우 겹침 NC 실패 — 탐침을 지웠는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            Debug.Log("[Ulon] 배우 겹침 양방향 NC 통과 — 배우 밑에 매달면 FAIL · 루트로 빼면 다시 통과");
        }
    }
}
