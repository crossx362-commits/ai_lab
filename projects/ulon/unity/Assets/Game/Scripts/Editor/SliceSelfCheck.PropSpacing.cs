using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **실내 소품이 서로 파고들어 있지 않은가**(검수 관찰 2026-09-08, `12_d3_interior_playcam`:
    /// 「오른쪽 통 두 개가 겹쳐 있고, 상자·항아리 무더기도 서로 박혀 보인다」).
    ///
    /// 분포 게이트(`AssertPropDistribution`)는 **어디에 있나**만 본다 — 같은 자리에 둘을 겹쳐 놔도
    /// 사분면 수는 그대로다. 「사람 눈으로 매번 세는 일이 아니다」(검수)라서 자를 하나 더 세운다.
    ///
    /// 재는 것: 소품 쌍의 월드 바운드가 **세 축 모두** 겹칠 때의 파고든 깊이(가장 얕은 축).
    /// 바운드는 실물보다 크므로(통은 원기둥, 잔해는 울퉁불퉁) **스치는 정도는 봐준다** —
    /// 판정은 「깊이가 작은 쪽 소품 두께의 절반을 넘는가」다. 절반을 넘으면 화면에서 한 덩어리로 읽힌다.
    /// 벽걸이(횃불·등불)는 벽에 박아 다는 것이라 대상에서 빼고, 그 사유를 여기 적는다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>파고든 깊이가 작은 쪽 소품 두께의 이 비율을 넘으면 「박혔다」.</summary>
        public const float PropOverlapFrac = 0.5f;

        static bool WallMountedProp(Transform t) =>
            t.name.StartsWith("DungeonFurnTorch", StringComparison.Ordinal)
            || t.name.StartsWith("DungeonFurnLantern", StringComparison.Ordinal);

        /// <summary>겹친 깊이(m) — 안 겹치면 0. 세 축 중 **가장 얕은 축**이 파고든 깊이다.</summary>
        static float Penetration(Bounds a, Bounds b)
        {
            float dx = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
            float dy = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
            float dz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
            if (dx <= 0f || dy <= 0f || dz <= 0f)
                return 0f;
            return Mathf.Min(dx, Mathf.Min(dy, dz));
        }

        /// <summary>이 소품의 「두께」 — 가장 얇은 쪽. 얇은 널빤지를 두꺼운 통 기준으로 재면 봐주게 된다.</summary>
        static float Thickness(Bounds b) => Mathf.Min(b.size.x, Mathf.Min(b.size.y, b.size.z));

        static void CheckPropSpacing(string label, string interiorObject, List<string> report)
        {
            var interior = GameObject.Find(interiorObject);
            if (interior == null)
                throw new InvalidOperationException(interiorObject + "이(가) 없습니다 — 못 잰 것을 통과로 적지 않는다.");

            var names = new List<string>();
            var boxes = new List<Bounds>();
            foreach (var t in PropNodes(interior))
            {
                if (WallMountedProp(t))
                    continue;
                if (!GroundFit.WorldBounds(t, out Bounds wb))
                    continue;
                names.Add(t.name);
                boxes.Add(wb);
            }
            if (boxes.Count == 0)
                throw new InvalidOperationException(label + "의 소품을 하나도 못 읽었습니다 — 잰 것이 없습니다(0이면 실패).");

            float worst = 0f;
            string worstPair = "";
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    float pen = Penetration(boxes[i], boxes[j]);
                    if (pen <= 0f)
                        continue;
                    float thin = Mathf.Min(Thickness(boxes[i]), Thickness(boxes[j]));
                    float frac = thin > 0.01f ? pen / thin : 1f;
                    if (frac > worst)
                    {
                        worst = frac;
                        worstPair = names[i] + "↔" + names[j] + " " + pen.ToString("0.00") + "m(얇은 쪽 " +
                                    thin.ToString("0.00") + "m의 " + (frac * 100f).ToString("0") + "%)";
                    }
                }
            report.Add(label + " 소품 " + boxes.Count + "개 · 최대 파고듦 " +
                       (worst > 0f ? worstPair : "없음"));
            if (worst > PropOverlapFrac)
                throw new InvalidOperationException(label + "의 소품이 서로 파고들었습니다: " + worstPair +
                    " — 허용 " + (PropOverlapFrac * 100f).ToString("0") + "%. 화면에서 한 덩어리로 읽힙니다(§8.2).");
        }

        static void AssertPropsNotOverlapping()
        {
            var report = new List<string>();
            CheckPropSpacing("던전 1", Dungeon1.InteriorObject, report);
            CheckPropSpacing("던전 2", Dungeon2.InteriorObject, report);
            CheckPropSpacing("던전 3", Dungeon3.InteriorObject, report);
            Debug.Log("[Ulon] 실내 소품 파고듦 — " + string.Join(" · ", report));
        }

        /// <summary>NC — 소품 하나를 **실제로 이웃 위로 옮겨** 겹쳐 놓으면 빨간불이어야 한다.</summary>
        static void AssertPropsNotOverlappingNegativeControl()
        {
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            if (interior == null)
                throw new InvalidOperationException("던전 1 실내가 없어 파고듦 네거티브 컨트롤을 할 수 없습니다.");
            Transform a = null, b = null;
            foreach (var t in PropNodes(interior))
            {
                if (WallMountedProp(t))
                    continue;
                if (a == null) a = t;
                else if (b == null) { b = t; break; }
            }
            if (a == null || b == null)
                throw new InvalidOperationException("파고듦 NC 대상이 없습니다 — 소품이 둘 미만입니다(0이면 실패).");
            var keep = b.position;
            bool red = false;
            try
            {
                b.position = a.position;              // 두 소품을 같은 자리에 겹쳐 놓는다
                Physics.SyncTransforms();
                try { AssertPropsNotOverlapping(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                b.position = keep;
                Physics.SyncTransforms();
            }
            if (!red)
                throw new InvalidOperationException("소품 파고듦 네거티브 컨트롤 실패 — " + b.name + "을 " + a.name +
                    " 자리에 겹쳐 놨는데 통과했습니다.");
            Debug.Log("[Ulon] 실내 소품 파고듦 네거티브 컨트롤 통과 — 둘을 같은 자리에 놓으면 FAIL");
        }
    }
}
