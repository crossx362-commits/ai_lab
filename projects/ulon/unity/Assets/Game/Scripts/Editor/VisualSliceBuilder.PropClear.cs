using System.Collections.Generic;
using UnityEngine;

// 마을 소품이 건물을 파고든 자리를 **굽는 쪽에서** 없앤다 — 게이트(`SliceSelfCheck.VillageSpacing`)와
// 같은 자(`Penetration`/`Thickness`/`PropOverlapFrac`)를 그대로 부른다. 자를 두 벌 두지 않는다.
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        /// <summary>
        /// 울타리·산울타리는 **줄**이라 걸린 조각을 빼면 집이 그 자리를 대신 막는다(마을에서 자연스럽다).
        /// 나머지 소품은 하나뿐이라 빼면 없어진 것이 되므로 **가장 얕은 축으로 밀어낸다**.
        /// 이름을 쓰는 곳은 여기뿐이고(프리팹 이름), **게이트는 이름을 안 본다** — 자와 수리를 갈라 둔다.
        /// </summary>
        static bool IsRunPiece(string name) =>
            name.StartsWith("fence", System.StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("hedge", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 울타리가 벽을 뚫고 수레가 대장간에 박힌 자리를 푼다(검수 관찰 2026-09-09 `01_village_square`).
        /// 울타리는 집보다 **먼저** 놓이므로 배치 시점에는 집이 없다 — 그래서 마지막에 한 번 훑는다.
        /// </summary>
        public static void ClearPropsFromBuildings()
        {
            int removed = 0, moved = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                var props = new List<Transform>();
                var boxes = new List<Bounds>();
                var bldNames = new List<string>();
                var bldBoxes = new List<Bounds>();
                SliceSelfCheck.CollectVillage(props, boxes, bldNames, bldBoxes);

                bool touched = false;
                for (int p = 0; p < props.Count; p++)
                {
                    if (props[p] == null)
                        continue;
                    for (int b = 0; b < bldBoxes.Count; b++)
                    {
                        float pen = SliceSelfCheck.Penetration(boxes[p], bldBoxes[b]);
                        if (pen <= 0f)
                            continue;
                        float thin = Mathf.Min(SliceSelfCheck.Thickness(boxes[p]), SliceSelfCheck.Thickness(bldBoxes[b]));
                        if (thin <= 0.01f || pen / thin <= SliceSelfCheck.PropOverlapFrac)
                            continue;
                        if (IsRunPiece(props[p].name))
                        {
                            UnityEngine.Object.DestroyImmediate(props[p].gameObject);
                            removed++;
                        }
                        else
                        {
                            props[p].position += PushOut(boxes[p], bldBoxes[b], pen);
                            moved++;
                        }
                        touched = true;
                        break;
                    }
                }
                Physics.SyncTransforms();
                if (!touched)
                    break;
            }
            Debug.Log("[Ulon] 마을 소품↔건물 정리 — 줄 조각 " + removed + "개 제거 · 소품 " + moved + "개 밀어냄");
        }

        /// <summary>가장 얕게 물린 축의 **가까운 쪽**으로 빼낸다 — 반대편으로 밀면 건물을 통과한다.</summary>
        static Vector3 PushOut(Bounds prop, Bounds bld, float pen)
        {
            float dx = Mathf.Min(prop.max.x, bld.max.x) - Mathf.Max(prop.min.x, bld.min.x);
            float dz = Mathf.Min(prop.max.z, bld.max.z) - Mathf.Max(prop.min.z, bld.min.z);
            // y로는 안 민다 — 소품을 공중에 띄우거나 땅에 묻는 수리는 수리가 아니다.
            float margin = pen + 0.08f;
            if (dx <= dz)
                return new Vector3(prop.center.x >= bld.center.x ? margin : -margin, 0f, 0f);
            return new Vector3(0f, 0f, prop.center.z >= bld.center.z ? margin : -margin);
        }
    }
}
