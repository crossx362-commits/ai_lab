using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// §6.1·§8.2 — 지역은 **바닥으로도** 구분돼야 한다. 소품만 얹고 지표가 전부 같은 초록이면
        /// 「밭·숲·광산」이 아니라 「초원에 놓인 물건들」로 읽힌다(검수 2026-09-06 관찰).
        /// 그리고 지역끼리 잇는 **길**이 없으면 각 지역이 평지에 뚝 떨어져 있다.
        ///
        /// 판정은 씬이 아니라 **실제 알파맵**(칠해진 결과)을 읽는다 — 빌더가 호출됐는지가 아니라
        /// 화면에 무엇이 칠해졌는지가 기준이다.
        /// </summary>
        const float RegionCoverMin = 0.50f;    // 지역 반경 안 표본의 해당 레이어 평균 가중치
        const float PlainGrassMin = 0.80f;     // 지역·길 밖 평지는 풀이어야 한다(온 세상이 흙이면 안 된다)
        const float RoadCoverMin = 0.50f;      // 길 중심선 표본의 길 레이어 가중치

        static void AssertRegionSplat()
        {
            AssertGrassToneNegativeControl();
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("지형이 없습니다 — 지역 도포를 검사할 수 없습니다.");
            var data = terrain.terrainData;
            if (data.terrainLayers.Length < WorldSplat.LayerCount)
                throw new InvalidOperationException("지형 레이어가 " + data.terrainLayers.Length + "종입니다 — 지역 지표·길까지 " + WorldSplat.LayerCount + "종이어야 합니다(§6.1).");

            int ar = data.alphamapResolution;
            var alpha = data.GetAlphamaps(0, 0, ar, ar);

            var regions = WorldRegions.All;
            int entranceSkipped = 0;
            for (int k = 0; k < regions.Length; k++)
            {
                var r = regions[k];
                int layerIdx = WorldSplat.LayerOf(r.Object);
                float sum = 0f;
                int n = 0;
                for (float a = 0f; a < 360f; a += 30f)
                {
                    for (float rad = 0f; rad <= r.Radius * 0.5f; rad += r.Radius * 0.25f)
                    {
                        float wx = r.X + Mathf.Sin(a * Mathf.Deg2Rad) * rad;
                        float wz = r.Z + Mathf.Cos(a * Mathf.Deg2Rad) * rad;
                        // **던전 문 앞은 표본에서 뺀다** — 사람은 던전 입구를 갈아엎지 않는다
                        // (`WorldSplat.EntranceClearAt`, 굽는 쪽과 **같은 함수**). 자를 무르게 하는 것과
                        // 표본을 고치는 것은 다르다: 하한 0.5는 그대로 두고, 밭이 아닌 자리를 밭으로 세지 않는다.
                        // 랩 ⑨에서 배운 그대로다 — **뺀 수를 찍고, 한 자리도 안 빼면 죽은 예외로 실패**한다.
                        if (WorldSplat.EntranceClearAt(wx, wz) < 0.5f)
                        {
                            entranceSkipped++;
                            continue;
                        }
                        sum += Sample(alpha, ar, wx, wz, layerIdx);
                        n++;
                    }
                }
                float avg = n == 0 ? 0f : sum / n;
                Debug.Log("[Ulon] §6.1 지역 지표 " + r.Name + " " + data.terrainLayers[layerIdx].name + " " + avg.ToString("0.00") + " (하한 " + RegionCoverMin + ")");
                if (avg < RegionCoverMin)
                    throw new InvalidOperationException("§6.1 " + r.Name + " 바닥의 " + data.terrainLayers[layerIdx].name + " 도포가 " + avg.ToString("0.00") +
                        "입니다 — 최소 " + RegionCoverMin + ". 지역 바닥이 초원과 같아 소품만 얹힌 모양입니다(§8.2).");
            }

            Debug.Log("[Ulon] §6.1 지역 지표 — 던전 문 앞이라 뺀 표본 " + entranceSkipped + "곳");
            if (entranceSkipped == 0)
                throw new InvalidOperationException("지역 지표 표본에서 던전 문 앞을 한 자리도 빼지 않았습니다 — " +
                    "예외가 죽었거나(WorldSplat.EntranceClearAt) 입구가 지역 밖으로 옮겨진 것입니다. 둘 다 확인하십시오.");

            // 길 — 마을에서 각 지역까지 실제로 이어져 있나. 중간 지점을 따라 훑는다.
            var routes = WorldSplat.Routes;
            for (int i = 0; i < routes.Length; i++)
            {
                float worst = 1f;
                float worstT = 0f;
                for (float t = 0.12f; t <= 0.88f; t += 0.04f)
                {
                    float wx = Mathf.Lerp(routes[i].x, routes[i].z, t);
                    float wz = Mathf.Lerp(routes[i].y, routes[i].w, t);
                    // **광장 돌포장도 길이다** — 길이 마을에서 시작하니 첫 구간은 광장 바닥이다.
                    // 이 자가 묻는 것은 「걸어갈 바닥이 이어지는가」이지 「무슨 재질인가」가 아니다
                    // (2026-09-09: 광장을 지형 돌포장으로 깔자 t=0.12에서 길 도포 0.00으로 울었다).
                    float v = Mathf.Max(Sample(alpha, ar, wx, wz, WorldSplat.Road),
                                        Sample(alpha, ar, wx, wz, WorldSplat.Cobble));
                    if (v < worst) { worst = v; worstT = t; }
                }
                Debug.Log("[Ulon] §6.1 길 " + i + " 최약 지점 " + worst.ToString("0.00") + " (t=" + worstT.ToString("0.00") + ", 하한 " + RoadCoverMin + ")");
                if (worst < RoadCoverMin)
                    throw new InvalidOperationException("§6.1 길 " + i + "이 t=" + worstT.ToString("0.00") + " 지점에서 끊깁니다(길 도포 " + worst.ToString("0.00") +
                        ", 최소 " + RoadCoverMin + ") — 지역이 평지에 떠 있습니다.");
            }

            // 반대쪽 한계 — 온 세상을 흙으로 칠해도 위 판정은 통과한다. 지역·길 밖 평지는 풀이어야 한다.
            // **풀은 이제 두 겹이다**(짙은 풀 + 마른 풀, 검수 랩 ⑤). 이 자가 묻는 것은 「흙이 아니라
            // 풀인가」이므로 **풀 계열의 합**을 잰다 — 하한 0.80은 그대로다(검수 지시: 건드리지 마라).
            float gsum = 0f;
            float drySum = 0f;
            int gn = 0;
            for (float x = -80f; x <= 80f; x += 8f)
            {
                for (float z = -80f; z <= 80f; z += 8f)
                {
                    float h = WorldTerrain.HeightAt(x, z);
                    if (h < WorldTerrain.SeaLevel + 2f || h > WorldTerrain.LandBase + 6f)
                        continue;                                   // 물가·산비탈은 모래·바위가 맞다
                    if (WorldSplat.CoverAt(x, z, out float cover) >= 0 && cover > 0.05f)
                        continue;                                   // 지역·길은 흙이 맞다
                    float lush = Sample(alpha, ar, x, z, WorldSplat.Grass);
                    float dry = Sample(alpha, ar, x, z, WorldSplat.DryGrass);
                    gsum += lush + dry;
                    drySum += dry;
                    gn++;
                }
            }
            if (gn == 0)
                throw new InvalidOperationException("지역 밖 평지 표본이 0곳입니다 — 이 자는 아무것도 재지 않았습니다.");
            float grassAvg = gn == 0 ? 0f : gsum / gn;
            Debug.Log("[Ulon] §6.1 지역 밖 평지 풀 " + grassAvg.ToString("0.00") + " (표본 " + gn + "곳, 하한 " + PlainGrassMin + ")");
            if (grassAvg < PlainGrassMin)
                throw new InvalidOperationException("지역·길 밖 평지의 풀 도포가 " + grassAvg.ToString("0.00") + "입니다 — 최소 " + PlainGrassMin +
                    ". 온 세상이 흙바닥이면 지역 구분이 사라집니다(§8.2).");

            // **초록이 몇 가지인가**(검수 랩 ⑤ — 「흙이 적은 것이 아니라 초록이 한 톤인 것」).
            // 풀 총량은 위 자가 지키고, 이 자는 그 풀이 **한 색인지**를 묻는다. 양쪽으로 잰다:
            // 갈리지 않으면 당구대이고, 마른 풀이 다 먹으면 초원이 마른 들판이 된다.
            float dryAvg = drySum / gn;
            Debug.Log("[Ulon] §8.2 평지 잔디 톤 — 마른 풀 몫 " + dryAvg.ToString("0.00") +
                      " (허용 " + DryToneMin + "~" + DryToneMax + ", 표본 " + gn + "곳)");
            string tone = GrassToneVerdict(dryAvg);
            if (tone != null)
                throw new InvalidOperationException(tone);

            Debug.Log("[Ulon] §6.1 지역 지표·길 통과 — 밭/숲/광산 도포 + 길 3갈래, 지역 밖 평지 풀 " + grassAvg.ToString("0.00"));
        }

        const float DryToneMin = 0.12f;    // 이보다 적으면 갈린 티가 안 난다(=한 톤)
        const float DryToneMax = 0.55f;    // 이보다 많으면 초원이 마른 들판으로 넘어간다

        /// <summary>마른 풀 몫에 대한 판정만 한다 — 표본과 분리해 두어야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string GrassToneVerdict(float dryAvg)
        {
            if (dryAvg < DryToneMin)
                return "평지 잔디의 마른 풀 몫이 " + dryAvg.ToString("0.00") + "입니다 — 최소 " + DryToneMin +
                       ". 초록이 한 톤이면 들판이 당구대로 읽힙니다(§8.2).";
            if (dryAvg > DryToneMax)
                return "평지 잔디의 마른 풀 몫이 " + dryAvg.ToString("0.00") + "입니다 — 최대 " + DryToneMax +
                       ". 초원이 통째로 마른 들판이 됐습니다(§8.2).";
            return null;
        }

        /// <summary>반대쪽 한계 — 갈리지 않은 값과 다 마른 값에서 **반드시** 빨간불이 나야 한다.</summary>
        static void AssertGrassToneNegativeControl()
        {
            if (GrassToneVerdict(0f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 마른 풀이 0인데도 잔디 톤 자가 통과했습니다.");
            if (GrassToneVerdict(1f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 온 들판이 마른 풀인데도 잔디 톤 자가 통과했습니다.");
            if (GrassToneVerdict((DryToneMin + DryToneMax) * 0.5f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 허용 범위 한가운데인데 잔디 톤 자가 걸렸습니다.");
        }

        static float Sample(float[,,] alpha, int ar, float wx, float wz, int layer)
        {
            float half = WorldTerrain.Span * 0.5f;
            int x = Mathf.Clamp(Mathf.RoundToInt((wx + half) / WorldTerrain.Span * (ar - 1)), 0, ar - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((wz + half) / WorldTerrain.Span * (ar - 1)), 0, ar - 1);
            return alpha[z, x, layer];
        }
    }
}
