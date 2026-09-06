using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 기획서 §6.1 지역(초원/농경지·숲·광산)이 **문서에만 있고 화면은 빈 초록**이면 §8.2 위반이다
        /// (검수 2026-09-06 반려 4). 그래서 「막을 것」이 아니라 **보여야 할 것의 하한**을 잰다:
        /// 지역 반경 안 소품 수와, 마을 밖 평지에서 가까운 곳에 소품이 있는 표본 비율.
        ///
        /// 이 판정을 통과하면서 화면이 빌 수 있나? — 소품이 지역 한구석에 몰리면 그렇다.
        /// 그래서 지역은 **사분면마다** 최소 수를 함께 요구한다.
        /// </summary>
        const float PlainCoverRadius = 12f;    // 표본에서 이 거리 안에 소품이 있으면 「채워진 평지」로 센다
        // 실측으로 못 박은 값: 산포 후 91.0% / 산포 0개(네거티브 컨트롤) 36.6%.
        // 하한은 「걸어가면 대체로 뭔가 있다」가 화면에서 읽히는 수준인 4곳 중 3곳으로 잡는다.
        const float PlainCoverMin = 0.75f;
        const int RegionQuadrantMin = 4;

        static void AssertWorldRegions()
        {
            var props = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var pts = new System.Collections.Generic.List<Vector2>(props.Length);
            for (int i = 0; i < props.Length; i++)
            {
                var t = props[i].transform;
                // 지형·물·던전 구조물은 「소품」이 아니다 — 이것들을 세면 빈 들판도 통과한다.
                if (props[i] is TerrainCollider || props[i].GetComponent<Terrain>() != null)
                    continue;
                string n = AncestorNames(t);
                if (n.IndexOf("Dungeon", StringComparison.Ordinal) >= 0 || n.IndexOf("SeaWater", StringComparison.Ordinal) >= 0)
                    continue;
                var p = t.position;
                pts.Add(new Vector2(p.x, p.z));
            }

            var regions = WorldRegions.All;
            for (int k = 0; k < regions.Length; k++)
            {
                var r = regions[k];
                if (GameObject.Find(r.Object) == null)
                    throw new InvalidOperationException("§6.1 " + r.Name + " 지역 오브젝트(" + r.Object + ")가 씬에 없습니다 — 기획서에만 있는 지역입니다.");
                int total = 0;
                var quad = new int[4];
                for (int i = 0; i < pts.Count; i++)
                {
                    float dx = pts[i].x - r.X;
                    float dz = pts[i].y - r.Z;
                    if (dx * dx + dz * dz > r.Radius * r.Radius)
                        continue;
                    total++;
                    quad[(dx >= 0f ? 1 : 0) + (dz >= 0f ? 2 : 0)]++;
                }
                if (total < r.PropMin)
                    throw new InvalidOperationException("§6.1 " + r.Name + " 반경 " + r.Radius + "m 안 소품이 " + total + "개입니다 — 최소 " + r.PropMin + "개. 이름만 있고 화면은 빈 초록입니다(§8.2).");
                for (int q = 0; q < 4; q++)
                {
                    if (quad[q] < RegionQuadrantMin)
                        throw new InvalidOperationException("§6.1 " + r.Name + " 사분면 " + q + "에 소품이 " + quad[q] + "개입니다 — 최소 " + RegionQuadrantMin + "개. 한구석에만 몰려 지역으로 안 읽힙니다.");
                }
                Debug.Log("[Ulon] §6.1 " + r.Name + " 소품 " + total + "개 (사분면 " + quad[0] + "/" + quad[1] + "/" + quad[2] + "/" + quad[3] + ")");
            }

            // 마을 밖 평지 표본 — 8m 격자.
            int samples = 0, covered = 0;
            for (float x = -80f; x <= 80f; x += 8f)
            {
                for (float z = -80f; z <= 80f; z += 8f)
                {
                    if (Mathf.Sqrt(x * x + z * z) < 26f)
                        continue;                                       // 마을·광장
                    float h = WorldTerrain.HeightAt(x, z);
                    if (h < WorldTerrain.SeaLevel + 1f || h > WorldTerrain.LandBase + 6f)
                        continue;                                       // 물가·산비탈은 원래 비어 있는 게 맞다
                    samples++;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        if ((pts[i] - new Vector2(x, z)).sqrMagnitude <= PlainCoverRadius * PlainCoverRadius)
                        {
                            covered++;
                            break;
                        }
                    }
                }
            }
            float share = samples == 0 ? 0f : covered / (float)samples;
            if (share < PlainCoverMin)
                throw new InvalidOperationException("마을 밖 평지 표본 " + samples + "곳 중 " + PlainCoverRadius + "m 안에 소품이 있는 곳이 " +
                    (share * 100f).ToString("0.0") + "%입니다 — 최소 " + (PlainCoverMin * 100f) + "%. 걸어가면 아무것도 없는 초록 벌판입니다(§8.2).");

            Debug.Log("[Ulon] §6.1 지역 통과 — 평지 표본 " + samples + "곳 중 소품 " + PlainCoverRadius + "m 안 " + (share * 100f).ToString("0.0") + "% (하한 " + (PlainCoverMin * 100f) + "%)");
        }
    }
}
