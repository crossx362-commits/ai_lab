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
        internal static bool IsRunPiece(string name) =>
            name.StartsWith("fence", System.StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("hedge", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 울타리가 벽을 뚫고 수레가 대장간에 박힌 자리를 푼다(검수 관찰 2026-09-09 `01_village_square`).
        /// 울타리는 집보다 **먼저** 놓이므로 배치 시점에는 집이 없다 — 그래서 마지막에 한 번 훑는다.
        /// </summary>
        public static void ClearPropsFromBuildings()
        {
            // 꺼진 건물은 렌더러가 꺼져 있어 **바운드를 못 잰다** — 잠깐 켜서 재고 되돌린다.
            // (부지 집은 계약 전까지 꺼져 있다. 못 재면 그 자리는 빈 땅으로 보이고 덤불이 그대로 박힌다.)
            var woken = new List<GameObject>();
            var everything = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < everything.Length; i++)
            {
                var go = everything[i];
                if (go != null && go.scene.IsValid() && !go.activeSelf && IsBuildingObject(go.name))
                {
                    go.SetActive(true);
                    woken.Add(go);
                }
            }
            try
            {
                ClearPropsFromBuildingsPass();
            }
            finally
            {
                for (int i = 0; i < woken.Count; i++)
                    if (woken[i] != null)
                        woken[i].SetActive(false);
                Physics.SyncTransforms();
            }
        }

        static void ClearPropsFromBuildingsPass()
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
                            props[p].position += PushOut(boxes[p], bldBoxes[b]);
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
            ClearPropsFromDoorFronts();
            var p2 = new List<Transform>(); var b2 = new List<Bounds>();
            var n2 = new List<string>(); var bb2 = new List<Bounds>();
            SliceSelfCheck.CollectVillage(p2, b2, n2, bb2);
            Debug.Log("[Ulon] 마을 소품↔건물 정리 — 줄 조각 " + removed + "개 제거 · 소품 " + moved +
                      "개 밀어냄 (남은 소품 " + p2.Count + "개 · 건물 " + n2.Count + "채: " + string.Join(",", n2) + ")");
        }

        /// <summary>
        /// **소품끼리 파고든 것을 비켜 세운다**(검수 승인 랩 2026-09-09 — 장작이 울타리를 관통).
        /// 무엇이 결함인지는 자(`SliceSelfCheck.CollectPropClashes`)가 정하고 여기서는 **비키기만** 한다:
        /// 같은 물건끼리(울타리 줄)·자연물끼리(수관)는 자가 애초에 안 준다.
        /// 누가 비키나: **울타리는 안 비킨다**(마당 경계를 정하는 것이라 옮기면 마당이 어긋난다).
        /// 그 밖에는 **작은 쪽**이 비킨다 — 화덕 옆 장작이 화덕을 밀어내면 그게 더 이상하다.
        /// </summary>
        public static void ClearPropsFromProps()
        {
            int moved = 0;
            for (int pass = 0; pass < 8; pass++)
            {
                var ia = new List<int>();
                var ib = new List<int>();
                var deep = new List<float>();
                var props = new List<Transform>();
                var boxes = new List<Bounds>();
                SliceSelfCheck.CollectPropClashes(ia, ib, deep, props, boxes);
                if (ia.Count == 0)
                    break;
                for (int i = 0; i < ia.Count; i++)
                {
                    Transform a = props[ia[i]], b = props[ib[i]];
                    if (a == null || b == null)
                        continue;
                    Bounds ba = boxes[ia[i]], bb2 = boxes[ib[i]];
                    bool aRun = IsRunPiece(a.name), bRun = IsRunPiece(b.name);
                    Transform mover;
                    Bounds moverBox, hostBox;
                    if (aRun != bRun)
                    {
                        mover = aRun ? b : a;
                        moverBox = aRun ? bb2 : ba;
                        hostBox = aRun ? ba : bb2;
                    }
                    else
                    {
                        bool aSmall = ba.size.x * ba.size.y * ba.size.z <= bb2.size.x * bb2.size.y * bb2.size.z;
                        mover = aSmall ? a : b;
                        moverBox = aSmall ? ba : bb2;
                        hostBox = aSmall ? bb2 : ba;
                    }
                    // **수평으로만, 상대에게서 멀어지는 쪽으로 민다.** 축 하나만 보고 밀면 울타리 두
                    // 조각 사이에 낀 수레가 판마다 좌우로 오가며 안 빠졌다(실측 2026-09-09: `cart-high`).
                    // 지표 스냅은 부르지 않는다 — 불꽃 파티클까지 바운드로 재는 화덕이 20m 위로 튄다.
                    var away = new Vector3(moverBox.center.x - hostBox.center.x, 0f,
                                           moverBox.center.z - hostBox.center.z);
                    if (away.sqrMagnitude < 0.0001f)
                        away = Vector3.right;
                    mover.position += away.normalized * (deep[i] + 0.30f);
                    moved++;
                }
                Physics.SyncTransforms();
            }
            Debug.Log("[Ulon] 마을 소품끼리 정리 — " + moved + "개를 비켜 세웠습니다");
        }

        /// <summary>
        /// 수평 두 축 중 **덜 물린 쪽**의 가까운 방향으로 빼낸다. 미는 거리는 그 축이 물린 만큼이다 —
        /// 첫 판은 가장 얕은 축(대개 높이)의 깊이만큼만 밀어 네 번을 돌고도 여전히 박혀 있었다.
        /// y로는 안 민다: 소품을 공중에 띄우거나 땅에 묻는 것은 수리가 아니다.
        /// </summary>
        static Vector3 PushOut(Bounds prop, Bounds bld)
        {
            float dx = Mathf.Min(prop.max.x, bld.max.x) - Mathf.Max(prop.min.x, bld.min.x);
            float dz = Mathf.Min(prop.max.z, bld.max.z) - Mathf.Max(prop.min.z, bld.min.z);
            if (dx <= dz)
                return new Vector3((prop.center.x >= bld.center.x ? 1f : -1f) * (dx + 0.08f), 0f, 0f);
            return new Vector3(0f, 0f, (prop.center.z >= bld.center.z ? 1f : -1f) * (dz + 0.08f));
        }

        /// <summary>
        /// **문 앞 한 몸 자리를 비운다** — 가로등·수레가 집 문 정면에 서 있으면 「들어가는 곳」으로 안 읽힌다
        /// (검수 관찰 2026-09-09). 통로 상자는 게이트(`SliceSelfCheck.CollectDoorFrontZones`)가 잰
        /// **그 상자 그대로**를 받아 쓴다 — 자를 두 벌 두지 않는다. 미는 방식은 건물 정리와 같다.
        /// </summary>
        static void ClearPropsFromDoorFronts()
        {
            var zones = new List<Bounds>();
            SliceSelfCheck.CollectDoorFrontZones(zones);
            if (zones.Count == 0)
                return;
            int moved = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                var props = new List<Transform>();
                var boxes = new List<Bounds>();
                var names = new List<string>();
                var bboxes = new List<Bounds>();
                SliceSelfCheck.CollectVillage(props, boxes, names, bboxes);
                bool touched = false;
                for (int p = 0; p < props.Count; p++)
                {
                    if (props[p] == null || SliceSelfCheck.IsFlatMat(boxes[p]))
                        continue;
                    for (int z = 0; z < zones.Count; z++)
                    {
                        if (!zones[z].Intersects(boxes[p]))
                            continue;
                        props[p].position += PushOut(boxes[p], zones[z]);
                        moved++;
                        touched = true;
                        break;
                    }
                }
                Physics.SyncTransforms();
                if (!touched)
                    break;
            }
            if (moved > 0)
                Debug.Log("[Ulon] 문 앞 통로 정리 — 소품 " + moved + "개를 문 정면에서 비켜세웠다(문 " + zones.Count + "개).");
        }

    }
}
