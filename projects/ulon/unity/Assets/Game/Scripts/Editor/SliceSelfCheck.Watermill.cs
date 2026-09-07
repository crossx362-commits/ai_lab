using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **역할 없는 물레방아가 씬에 없는가**(검수 절차 판정 2026-09-07).
    ///
    /// 절차: 역할 원장(`RoleLook.Facilities`)에 대응 역할이 있으면 물가로 옮기고, 없으면 지운다.
    /// 확인 결과 **없다** — 그래서 지웠고, 이 게이트는 **다시 서지 않는지**를 지킨다.
    /// 「치웠다」가 「안 보이는 데로 옮겼다」였던 전례가 있어(지표 −10m), 판정은 **씬에 존재하는가**로 한다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static List<Transform> RolelessWatermills()
        {
            var found = new List<Transform>();
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (VisualSliceBuilder.IsWatermillName(all[i].name))
                    found.Add(all[i]);
            return found;
        }

        static void AssertNoRolelessWatermill()
        {
            var left = RolelessWatermills();
            if (left.Count > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < left.Count && i < 6; i++)
                    names.Add(GroundFit.NodePath(left[i]) + left[i].position.ToString("F1"));
                throw new InvalidOperationException("역할 없는 물레방아가 " + left.Count + "채 남아 있습니다: " +
                    string.Join(", ", names) + " — 역할 원장(`RoleLook.Facilities`)에 물레방아 역할이 없습니다. " +
                    "역할이 생기면 물가로 옮기고 원장에 행을 추가하십시오(그때 이 게이트를 고치십시오).");
            }
            Debug.Log("[Ulon] 물레방아 — 씬에 0채(역할 원장에 대응 역할 없음, 장식은 지웠다)");
        }

        /// <summary>NC — 물레방아를 **실제로 다시 세우면** 빨간불이어야 한다.</summary>
        static void AssertNoRolelessWatermillNegativeControl()
        {
            var ghost = new GameObject("watermill");
            bool red = false;
            try
            {
                try { AssertNoRolelessWatermill(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { UnityEngine.Object.DestroyImmediate(ghost); }
            if (!red)
                throw new InvalidOperationException("물레방아 네거티브 컨트롤 실패 — 다시 세웠는데 통과했습니다.");
            Debug.Log("[Ulon] 물레방아 네거티브 컨트롤 통과 — 다시 세우면 FAIL");
        }
    }
}
