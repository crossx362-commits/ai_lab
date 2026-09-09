using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **길은 문 뒤 언덕을 넘지 않는다**(2026-09-09).
        ///
        /// 언덕을 올리자 마을 스포크 길이 그 위를 그대로 타고 정상까지 올라갔다(`07`·`09`·`11` 상단 띠).
        /// 자는 언덕 반경 안을 1m 격자로 훑어 **언덕이 `RoadMoundCut` 넘게 솟은 자리에 길 도포가 있는가**를
        /// 센다 — 하나도 없어야 한다.
        ///
        /// NC는 규칙을 끄고 같은 자리를 다시 센다(`RoadMoundFade` 없이 옛 세기로): 길 칸이 나와야 한다.
        /// 안 나오면 이 자는 언덕 위를 재고 있지 않은 것이다(셈에서 D2 170칸을 봤다).
        /// </summary>
        static void AssertRoadOffMound()
        {
            int onMound = 0, wouldBe = 0;
            string where = "";
            foreach (var d in EntranceGeom.All)
            {
                var front = EntranceGeom.Front(d.X, d.Z, d.Yaw);
                float cx = d.X - front.x * EntranceGeom.MoundOffset, cz = d.Z - front.y * EntranceGeom.MoundOffset;
                for (float x = cx - EntranceGeom.MoundRadius; x <= cx + EntranceGeom.MoundRadius; x += 1f)
                for (float z = cz - EntranceGeom.MoundRadius; z <= cz + EntranceGeom.MoundRadius; z += 1f)
                {
                    if (EntranceGeom.MoundRise(x, z) <= WorldSplat.RoadMoundCut)
                        continue;
                    int layer = WorldSplat.CoverAt(x, z, out float w);
                    if (w >= 0.5f && layer == WorldSplat.Road)
                    {
                        onMound++;
                        if (where.Length == 0)
                            where = d.Root + " (" + x.ToString("0") + "," + z.ToString("0") + ")";
                    }
                    // NC — 규칙(언덕 페이드)이 없었다면 여기가 길이었나.
                    float dist = float.MaxValue;
                    var routes = WorldSplat.Routes;
                    for (int i = 0; i < routes.Length; i++)
                        dist = Mathf.Min(dist, WorldSplat.DistToSegment(x, z, routes[i].x, routes[i].y, routes[i].z, routes[i].w));
                    if (1f - Mathf.Clamp01((dist - WorldSplat.RoadHalfWidth) / WorldSplat.RoadFade) >= 0.5f)
                        wouldBe++;
                }
            }
            if (onMound > 0)
                throw new InvalidOperationException("문 뒤 언덕 위에 길 도포가 " + onMound + "칸 있습니다(" + where +
                    ") — 길이 산꼭대기로 올라갑니다.");
            if (wouldBe == 0)
                throw new InvalidOperationException("길-언덕 네거티브 컨트롤 실패 — 규칙을 빼도 언덕 위에 길이 없습니다. " +
                    "이 자는 아무것도 재고 있지 않습니다.");
            Debug.Log("[Ulon] 길은 언덕을 넘지 않는다 — 언덕 위 길 0칸(규칙이 없었다면 " + wouldBe + "칸)");
        }
    }
}
