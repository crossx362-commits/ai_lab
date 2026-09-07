using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **사냥터가 마을에서 떨어져 있는가**(검수 지시 2026-09-07, 랩 ⑦).
    ///
    /// 근거는 `56_mob_lineup`이다 — 잡몹이 마을 담장에 붙어 서 있었다. 마을은 가드존이라
    /// 그 안에서는 싸움이 성립하지 않는데, 몹이 담장에 붙어 있으면 화면은 「마을에 몹이 산다」로
    /// 읽힌다(§8.1).
    ///
    /// **한 축으로 재지 않는다**(검수 원문: 돌길만 재면 담장·좌판 옆이 통과한다).
    /// 마을 구조물 **전체**에서의 최단 거리를 잰다:
    ///   · 마을의 정의는 코드에 이미 있다 — `GuardZone.Radius`(16m). 그 반경 +6m 안에 선
    ///     **액터가 아닌 렌더러 전부**를 마을 구조물로 모은다(이름 목록이 아니라 규칙).
    ///   · 거리는 렌더러 **바운드까지의 수평 최단 거리** — 중심으로 재면 큰 건물이 가깝게 안 읽힌다.
    /// 그리고 **옮긴 뒤에도 사냥터가 사냥터인지** 같이 잰다(너무 밀면 산·물로 간다).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>몹 발과 마을 구조물 사이 최소 수평 거리.</summary>
        const float HuntVillageGapMin = 8f;

        /// <summary>마을 구조물로 볼 반경 — 가드존(마을의 코드상 정의) 바깥 여유.</summary>
        const float VillageStructureRadius = GuardZone.Radius + 6f;

        static List<Renderer> VillageStructures()
        {
            var found = new List<Renderer>();
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].GetComponent<Terrain>() != null)
                    continue;                                   // 지형은 구조물이 아니다
                if (all[i].GetComponentInParent<CharacterController>() != null)
                    continue;                                   // 사람·몹은 구조물이 아니다
                // **월드 규모의 판은 구조물이 아니다** — 바다 수면(`SeaWater`)이 중심 (0,0)에
                // 세계만 한 바운드를 갖고 있어서, 이걸 안 빼면 모든 몹의 「최단 거리」가 0m로 나온다
                // (첫 판이 그렇게 찍혔다). 규칙은 **공용 함수 한 곳**에만 둔다(검수 지시 2026-09-07).
                if (GroundFit.IsWorldScalePlane(all[i].bounds))
                    continue;
                var c = all[i].bounds.center;
                if ((c.x * c.x) + (c.z * c.z) > VillageStructureRadius * VillageStructureRadius)
                    continue;
                found.Add(all[i]);
            }
            return found;
        }

        /// <summary>수평 최단 거리 — 바운드를 xz 평면으로 눌러서 잰다.</summary>
        static float FlatGap(Vector3 p, Bounds b)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x - b.center.x) - b.extents.x, 0f);
            float dz = Mathf.Max(Mathf.Abs(p.z - b.center.z) - b.extents.z, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void AssertHuntMobsAwayFromVillage()
        {
            var structures = VillageStructures();
            if (structures.Count == 0)
                throw new InvalidOperationException("마을 구조물을 하나도 못 모았습니다 — 잰 것이 없습니다(0이면 실패).");

            var spots = VisualSliceBuilder.HuntSpots;
            var lines = new List<string>();
            var bad = new List<string>();
            int measured = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                var go = GameObject.Find(spots[i].Name);
                if (go == null)
                {
                    bad.Add(spots[i].Name + "(씬에 없음)");
                    continue;
                }
                measured++;
                var p = go.transform.position;
                float best = float.MaxValue;
                string nearest = "-";
                for (int s = 0; s < structures.Count; s++)
                {
                    float d = FlatGap(p, structures[s].bounds);
                    if (d < best) { best = d; nearest = GroundFit.NodePath(structures[s].transform); }
                }
                lines.Add(spots[i].Name + " " + best.ToString("0.0") + "m(" + nearest + ")");
                if (best < HuntVillageGapMin)
                    bad.Add(spots[i].Name + " " + best.ToString("0.0") + "m — " + nearest);

                // **옮긴 뒤에도 사냥터가 사냥터인가** — 물속·산비탈로 밀려나면 이격은 성공이 아니다.
                float g = VisualSliceBuilder.GroundHeightAt(p.x, p.z);
                if (g < WorldTerrain.SeaLevel + 1f)
                    bad.Add(spots[i].Name + "이 물가/물속입니다(지표 " + g.ToString("0.0") + "m, 수면 " +
                            WorldTerrain.SeaLevel.ToString("0.0") + "m)");
                float slope = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float ox = (k == 0 ? 2f : k == 1 ? -2f : 0f), oz = (k == 2 ? 2f : k == 3 ? -2f : 0f);
                    slope = Mathf.Max(slope, Mathf.Abs(VisualSliceBuilder.GroundHeightAt(p.x + ox, p.z + oz) - g));
                }
                if (slope > 2.5f)
                    bad.Add(spots[i].Name + "이 비탈에 섰습니다(2m 안에서 " + slope.ToString("0.0") + "m 낙차)");
            }
            Debug.Log("[Ulon] 사냥터 이격 — 잡몹 " + measured + "/" + spots.Length + "체, 마을 구조물 " +
                      structures.Count + "개 중 최단 거리(하한 " + HuntVillageGapMin.ToString("0") + "m): " +
                      string.Join(", ", lines));
            if (bad.Count > 0)
                throw new InvalidOperationException("사냥터가 마을에 붙어 있습니다 — " + string.Join(", ", bad) +
                    ". 마을은 가드존이라 싸움이 성립하지 않는데 몹이 담장 옆에 서 있으면 화면은 " +
                    "「마을에 몹이 산다」로 읽힙니다(§8.1). `VisualSliceBuilder.HuntSpots`를 옮기십시오.");
        }

        /// <summary>NC — 한 마리를 **실제로 마을 한복판에 놓으면** 빨간불이어야 한다.</summary>
        static void AssertHuntMobsAwayFromVillageNegativeControl()
        {
            var spots = VisualSliceBuilder.HuntSpots;
            GameObject victim = null;
            for (int i = 0; i < spots.Length && victim == null; i++)
                victim = GameObject.Find(spots[i].Name);
            if (victim == null)
                throw new InvalidOperationException("사냥터 이격 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var keep = victim.transform.position;
            bool red = false;
            string message = "";
            try
            {
                victim.transform.position = new Vector3(0f, VisualSliceBuilder.GroundHeightAt(0f, 0f), 0f);
                try { AssertHuntMobsAwayFromVillage(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally { victim.transform.position = keep; }
            if (!red)
                throw new InvalidOperationException("사냥터 이격 네거티브 컨트롤 실패 — " + victim.name +
                    "을 마을 한복판(0,0)에 놓았는데 통과했습니다.");
            Debug.Log("[Ulon] 사냥터 이격 네거티브 컨트롤 통과 — 한 마리를 마을 한복판에 놓으면 FAIL: " + message);
        }
    }
}
