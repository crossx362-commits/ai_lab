using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// §8.2 — 실내 소품이 **등록 CC0 모델**인지 본다(검수 2026-09-06 반려: 색칠 큐브로 채웠다).
        /// 맨바닥 비율 게이트(`AssertRoomFurnished`)는 「덮기만 하면」 통과하므로 이 결함을 못 잡는다.
        /// 두 게이트는 서로 다른 것을 잰다 — **분포**는 맨바닥 비율이, **자격**은 이 게이트가.
        /// </summary>
        static void AssertRoomPropsQualified()
        {
            int total = 0;
            total += CheckProps("던전 1", Dungeon1.InteriorObject);
            total += CheckProps("던전 2", Dungeon2.InteriorObject);
            total += CheckProps("던전 3", Dungeon3.InteriorObject);
            if (total == 0)
                throw new InvalidOperationException("실내 소품(DungeonFurn*)이 한 개도 없습니다 — 방이 빈 바닥입니다(§6.1).");
            Debug.Log("[Ulon] 실내 소품 자격 통과 — " + total + "개 전부 PropArt 등록 CC0 모델(프리미티브 0개)");
        }

        static int CheckProps(string label, string interiorObject)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(interiorObject + "이(가) 없습니다.");
            int n = 0;
            foreach (var t in PropNodes(interior))
            {
                string why = PropArt.ReasonUnqualified(t.gameObject);
                if (why != "")
                    throw new InvalidOperationException(label + "의 소품 " + t.name + "이(가) 자격 미달입니다 — " + why +
                        ". 등록된 CC0 소품 프리팹만 쓴다(자격 원장 Editor/PropArt.cs).");
                n++;
            }
            return n;
        }

        static List<Transform> PropNodes(GameObject interior)
        {
            var list = new List<Transform>();
            for (int c = 0; c < interior.transform.childCount; c++)
            {
                var child = interior.transform.GetChild(c);
                if (child.name.StartsWith("DungeonFurn", StringComparison.Ordinal))
                    list.Add(child);
            }
            return list;
        }

        /// <summary>네거티브 컨트롤 — 방에 색칠 큐브 소품을 실제로 하나 넣으면 빨간불이어야 한다.</summary>
        static void AssertRoomPropsNegativeControl()
        {
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 1 실내가 없어 소품 자격 네거티브 컨트롤을 할 수 없습니다.");
            var fake = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fake.name = "DungeonFurnFakeCube";
            fake.transform.SetParent(interior.transform, true);
            fake.transform.position = interior.transform.position;
            bool red = false;
            try
            {
                try { AssertRoomPropsQualified(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fake);
            }
            if (!red)
                throw new InvalidOperationException("소품 자격 네거티브 컨트롤 실패 — 색칠 큐브를 소품으로 넣었는데도 통과했습니다.");
            Debug.Log("[Ulon] 실내 소품 자격 네거티브 컨트롤 통과 — 프리미티브 큐브를 넣으면 FAIL");
        }
    }
}
