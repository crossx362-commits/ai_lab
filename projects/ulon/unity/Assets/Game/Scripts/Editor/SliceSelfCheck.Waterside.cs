using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **낚시터에 물이 있는가 · 화덕에 불이 있는가**(검수 반려 2026-09-07).
    ///
    /// 둘 다 「그 자리에 무엇이 있다」가 아니라 **화면에서 그 기능이 읽히는 조건**을 잰다 —
    /// 낚시터는 발판이 아니라 **물**이 있어야 낚시터고, 화덕은 돌이 아니라 **불**이 있어야 화덕이다.
    /// 역할↔외형 게이트(`RoleLook`)는 「부속 메시가 붙었는가」까지만 보므로, 잔디 위 발판과
    /// 불 꺼진 돌무더기를 통과시켰다. 그 사각지대를 이 두 축이 메운다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>낚시터에서 물까지 허용 거리 — 이 거리 안에 수면이 없으면 화면에 물이 안 들어온다.</summary>
        const float FishingWaterMax = 8f;

        /// <summary>수면과의 높이 차 상한 — 물 옆이어도 벼랑 위면 화면에 물이 안 들어온다(실측 6.2m 사례).</summary>
        const float FishingWaterDropMax = 1.5f;

        /// <summary>(x,z) 둘레에서 가장 가까운 수면까지 거리. 없으면 +∞.</summary>
        static float DistanceToWater(Vector3 at, out Vector3 where)
        {
            where = Vector3.zero;
            float best = float.PositiveInfinity;
            // 수면 판정은 **지형 높이**로 한다 — 물 오브젝트 이름에 기대면 개명 한 번에 눈이 먼다
            // (지표 분류에서 겪은 그 함정). 지표가 수면보다 낮은 자리가 곧 물이다.
            for (float dx = -FishingWaterMax * 1.5f; dx <= FishingWaterMax * 1.5f; dx += 1f)
                for (float dz = -FishingWaterMax * 1.5f; dz <= FishingWaterMax * 1.5f; dz += 1f)
                {
                    float x = at.x + dx, z = at.z + dz;
                    if (WorldTerrain.HeightAt(x, z) >= WorldTerrain.SeaLevel)
                        continue;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < best) { best = d; where = new Vector3(x, WorldTerrain.SeaLevel, z); }
                }
            return best;
        }

        static void AssertFishingAtWater()
        {
            var spot = GameObject.Find("FishingSpot");
            if (spot == null)
                throw new InvalidOperationException("낚시터(FishingSpot)를 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            float d = DistanceToWater(spot.transform.position, out Vector3 water);
            Debug.Log("[Ulon] 낚시터 물가 — 좌표 " + spot.transform.position.ToString("0.0") +
                      ", 가장 가까운 수면 " + (float.IsInfinity(d) ? "없음" : d.ToString("0.0") + "m " + water.ToString("0.0")) +
                      " (상한 " + FishingWaterMax + "m)");
            // **수평만 재면 둑 위가 통과한다** — 첫 배치가 실제로 수면보다 6.2m 높은 벼랑 위였고
            // 「물까지 2.0m」로 초록불이었다. 높이 차도 같이 잰다(한 축만 재면 다른 축으로 샌다).
            float drop = spot.transform.position.y - WorldTerrain.SeaLevel;
            Debug.Log("[Ulon] 낚시터 높이 — 수면 대비 " + drop.ToString("0.0") + "m (상한 " + FishingWaterDropMax + "m)");
            if (Mathf.Abs(drop) > FishingWaterDropMax)
                throw new InvalidOperationException("낚시터가 수면보다 " + drop.ToString("0.0") +
                    "m 높습니다(상한 " + FishingWaterDropMax + "m) — 둑 위 발판은 물가로 안 읽힌다(§8.1).");
            if (!(d <= FishingWaterMax))
                throw new InvalidOperationException("낚시터에서 물까지 " +
                    (float.IsInfinity(d) ? "물이 아예 없습니다" : d.ToString("0.0") + "m입니다") +
                    " — 상한 " + FishingWaterMax + "m. 잔디 위 발판은 낚시터로 안 읽힌다(§8.1).");
        }

        /// <summary>네거티브 컨트롤 — 낚시터를 실제로 마을 잔디로 옮겨 빨간불을 본 뒤 되돌린다.</summary>
        static void AssertFishingAtWaterNegativeControl()
        {
            var spot = GameObject.Find("FishingSpot");
            if (spot == null)
                throw new InvalidOperationException("낚시터를 못 찾았습니다 — 네거티브 컨트롤을 돌릴 수 없습니다.");
            var had = spot.transform.position;
            spot.transform.position = new Vector3(-11.5f, had.y, -8.5f);   // 예전 자리(마을 잔디)
            float d = DistanceToWater(spot.transform.position, out _);
            spot.transform.position = had;
            Debug.Log("[Ulon] 낚시터 물가 NC — 마을 잔디로 옮겼을 때 물까지 " +
                      (float.IsInfinity(d) ? "없음" : d.ToString("0.0") + "m"));
            if (d <= FishingWaterMax)
                throw new InvalidOperationException("낚시터 물가 네거티브 컨트롤 실패 — 마을 잔디에서도 물이 " +
                    d.ToString("0.0") + "m로 읽힙니다(게이트가 아무것도 안 재고 있다).");
        }

        static void AssertCampfireHasFire()
        {
            var go = GameObject.Find("Campfire");
            if (go == null)
                throw new InvalidOperationException("화덕(Campfire)을 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var systems = go.GetComponentsInChildren<ParticleSystem>(false);
            int emitting = 0;
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                var em = systems[i].emission;
                // 「달려 있다」가 아니라 **계속 난다**를 잰다 — 꺼 둔 템플릿이 붙어 있어도 화면엔 불이 없다.
                if (main.loop && main.playOnAwake && em.enabled && em.rateOverTime.constant > 0f)
                    emitting++;
            }
            var lights = go.GetComponentsInChildren<Light>(false);
            Debug.Log("[Ulon] 화덕 불 — 계속 나는 파티클 " + emitting + "개 · 불빛 " + lights.Length + "개");
            if (emitting < 1)
                throw new InvalidOperationException("화덕에 불이 없습니다 — 돌만 놓인 자리는 화덕으로 안 읽힌다(§8.1). " +
                    "불 메시가 없어도 등록 CC0 파티클로 붙일 수 있다.");
        }

        /// <summary>네거티브 컨트롤 — 불을 실제로 꺼서 빨간불을 본 뒤 되돌린다.</summary>
        static void AssertCampfireFireNegativeControl()
        {
            var go = GameObject.Find("Campfire");
            var flame = go != null ? go.transform.Find(VisualSliceBuilder.CampfireFlameObject) : null;
            if (flame == null)
                throw new InvalidOperationException("화덕 불꽃 오브젝트를 못 찾았습니다 — 네거티브 컨트롤을 돌릴 수 없습니다.");
            flame.gameObject.SetActive(false);
            bool red = false;
            try { AssertCampfireHasFire(); }
            catch (InvalidOperationException) { red = true; }
            flame.gameObject.SetActive(true);
            if (!red)
                throw new InvalidOperationException("화덕 불 네거티브 컨트롤 실패 — 불을 껐는데 통과했습니다.");
            Debug.Log("[Ulon] 화덕 불 NC — 불을 끄자 빨간불, 되돌린 뒤 통과");
        }
    }
}
