using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **마을 소품이 건물을 파고들지 않았는가**(검수 관찰 2026-09-09, `01_village_square`:
    /// 「울타리 한 줄이 집 벽을 관통하고, 가로등이 처마와 겹쳐 바싹 붙는다」).
    ///
    /// 새 자를 만들지 않는다 — 던전 소품에만 걸려 있던 `PropOverlapFrac`(얇은 쪽 두께의 절반)을
    /// **마을 소품↔건물**로 넓힌다. 재는 함수(`Penetration`·`Thickness`)는 그대로 재사용한다.
    /// 배율(랩 B)로 두께·간격 관계가 바뀐 자리를 잡는 것이 목적이다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 바닥에 깔린 것(길 타일·바닥판)은 대상이 아니다 — 건물은 그 위에 서는 것이지 파고드는 게 아니다.
        /// 이름이 아니라 **자리·모양으로** 가른다: 높이가 이 값보다 낮으면 깔개로 본다.
        /// </summary>
        const float VillageMatHeight = 0.35f;

        static bool IsVillageMat(Bounds b) => b.size.y < VillageMatHeight;

        /// <summary>
        /// 마을 소품·건물을 모은다. **묶음은 소품이 아니다** — `VillageDecor`·`PlainScatter`처럼
        /// 자기 렌더러 없이 여럿을 담기만 하는 마디는 그 바운드가 마을 전체를 덮어, 그대로 재면
        /// 「모든 집을 파고든 소품 하나」가 나온다(첫 판 실측: 30쌍 전부 묶음이었다).
        /// 그래서 **자기 렌더러가 없고 자식이 둘 이상이면 묶음으로 보고 한 겹 내려간다**.
        /// 이름 목록이 아니라 성질이다(「출처가 뿌리에 없으면 한 겹 아래 있다」).
        /// </summary>
        internal static void CollectVillage(List<Transform> propNodes, List<Bounds> propBoxes,
                                   List<string> bldNames, List<Bounds> bldBoxes)
        {
            var stack = new Stack<Transform>();
            // 꺼진 것도 훑는다 — **부지 집은 계약 전까지 꺼져 있을 뿐 그 자리는 이미 예약돼 있다**.
            // (굽는 쪽이 못 보고 지나쳐 덤불이 그 집에 박힌 채 남았다, 2026-09-09.) 소품은 켜진 것만 센다.
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go != null && go.scene.IsValid() && go.transform.parent == null)
                    stack.Push(go.transform);
            }

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t.name == "Ground" || t.name.StartsWith("Terrain", StringComparison.Ordinal))
                    continue;
                if (t.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                    continue;                                   // 사람·짐승은 지나다니는 것이지 박히는 게 아니다
                if (t.GetComponentInChildren<Renderer>(true) == null)
                    continue;
                if (VisualSliceBuilder.IsBuildingObject(t.name))
                {
                    if (GroundFit.WorldBounds(t, out Bounds bb) && InVillage(bb))
                    {
                        bldNames.Add(t.name);
                        bldBoxes.Add(bb);
                    }
                    continue;
                }
                if (t.GetComponent<Renderer>() == null && t.childCount > 1)
                {
                    for (int c = 0; c < t.childCount; c++)
                        stack.Push(t.GetChild(c));
                    continue;
                }
                if (!t.gameObject.activeInHierarchy)
                    continue;
                if (!GroundFit.WorldBounds(t, out Bounds wb) || !InVillage(wb) || IsVillageMat(wb))
                    continue;
                propNodes.Add(t);
                propBoxes.Add(wb);
            }
        }

        static bool InVillage(Bounds b) =>
            Mathf.Abs(b.center.x) <= VisualSliceBuilder.VillageFadeRadius &&
            Mathf.Abs(b.center.z) <= VisualSliceBuilder.VillageFadeRadius;

        static string VillagePropClashReason(bool log)
        {
            var propNodes = new List<Transform>();
            var propBoxes = new List<Bounds>();
            var bldNames = new List<string>();
            var bldBoxes = new List<Bounds>();
            CollectVillage(propNodes, propBoxes, bldNames, bldBoxes);
            if (bldBoxes.Count == 0)
                throw new InvalidOperationException("마을 건물을 하나도 못 읽었습니다 — 잰 것이 없습니다(0이면 실패).");
            if (propBoxes.Count == 0)
                throw new InvalidOperationException("마을 소품을 하나도 못 읽었습니다 — 잰 것이 없습니다(0이면 실패).");

            float worst = 0f;
            string worstPair = "";
            var bad = new List<string>();
            for (int p = 0; p < propBoxes.Count; p++)
                for (int b = 0; b < bldBoxes.Count; b++)
                {
                    float pen = Penetration(propBoxes[p], bldBoxes[b]);
                    if (pen <= 0f)
                        continue;
                    float thin = Mathf.Min(Thickness(propBoxes[p]), Thickness(bldBoxes[b]));
                    float frac = thin > 0.01f ? pen / thin : 1f;
                    // 자리까지 적는다 — 「어느 울타리냐」를 사람이 다시 찾게 만들면 자가 반쪽이다.
                    string what = propNodes[p].name + "(" + propBoxes[p].center.x.ToString("0.0") + "," +
                                  propBoxes[p].center.z.ToString("0.0") + ")↔" + bldNames[b] + "(" +
                                  bldBoxes[b].center.x.ToString("0.0") + "," + bldBoxes[b].center.z.ToString("0.0") + ") " +
                                  pen.ToString("0.00") + "m(얇은 쪽 " + thin.ToString("0.00") + "m의 " +
                                  (frac * 100f).ToString("0") + "%)";
                    if (frac > PropOverlapFrac)
                        bad.Add(what);
                    if (frac > worst)
                    {
                        worst = frac;
                        worstPair = what;
                    }
                }
            if (log)
                Debug.Log("[Ulon] 마을 소품↔건물 — 소품 " + propBoxes.Count + "개 · 건물 " + bldBoxes.Count +
                          "채 · 파고든 쌍 " + bad.Count + "개 · 최대 " + (worst > 0f ? worstPair : "없음") +
                          " (허용 " + (PropOverlapFrac * 100f).ToString("0") + "%)");
            if (bad.Count == 0)
                return null;
            bad.Sort(StringComparer.Ordinal);
            return "마을 소품이 건물을 파고들었습니다 — " + bad.Count + "쌍:\n  " +
                   string.Join("\n  ", bad.GetRange(0, Mathf.Min(bad.Count, 8))) +
                   (bad.Count > 8 ? "\n  … 외 " + (bad.Count - 8) + "쌍" : "") +
                   "\n허용 " + (PropOverlapFrac * 100f).ToString("0") +
                   "%. 울타리가 벽을 뚫고 등이 처마에 박히면 배치가 아니라 사고로 읽힙니다.";
        }

        static void AssertVillagePropsClearOfBuildings()
        {
            string reason = VillagePropClashReason(true);
            if (reason != null)
                throw new InvalidOperationException(reason);
        }

        /// <summary>
        /// 양방향 NC — ① 소품 하나를 **실제로 집 안으로 밀어 넣으면** 빨간불, ② 되돌리면 다시 초록.
        /// 한쪽만 보면 「늘 초록인 자」와 「늘 빨간 자」를 구별하지 못한다.
        /// </summary>
        static void AssertVillagePropsClearOfBuildingsNegativeControl()
        {
            var propNodes = new List<Transform>();
            var propBoxes = new List<Bounds>();
            var bldNames = new List<string>();
            var bldBoxes = new List<Bounds>();
            CollectVillage(propNodes, propBoxes, bldNames, bldBoxes);
            if (propBoxes.Count == 0 || bldBoxes.Count == 0)
                throw new InvalidOperationException("마을 소품↔건물 NC 대상이 없습니다(0이면 실패).");

            Transform victim = propNodes[0];

            var keep = victim.position;
            bool red = false;
            try
            {
                victim.position = bldBoxes[0].center;             // 소품을 건물 한가운데로 밀어 넣는다
                Physics.SyncTransforms();
                red = VillagePropClashReason(false) != null;
            }
            finally
            {
                victim.position = keep;
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("마을 소품↔건물 네거티브 컨트롤 실패 — " + victim.name +
                    "을(를) " + bldNames[0] + " 한가운데에 넣었는데 통과했습니다.");
            string back = VillagePropClashReason(true);
            if (back != null)
                throw new InvalidOperationException("마을 소품↔건물 네거티브 컨트롤 실패 — 되돌렸는데도 빨간불입니다: " + back);
            Debug.Log("[Ulon] 마을 소품↔건물 양방향 NC 통과 — 집 안으로 밀면 FAIL · 되돌리면 다시 통과");
        }
    }
}
