using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 월드 지형(산·바다·강·호수)이 화면에서 읽히는 성질을 잰다 — 오브젝트 존재 여부가 아니라
        /// **기복·재질·물에 잠기지 않은 콘텐츠 좌표**를 본다(검수 2026-09-06 「게이트를 존재 여부로 만들지 마라」).
        /// 기획서 §6.1(광산/산지) · §8.2(단조로운 화면·Default-Material 금지) · §8.1(멀리서 읽히는 실루엣).
        /// </summary>
        const float MountainReliefMin = 18f;    // 산 띠 최고점 - 평지 기준(m)
        const float MountainSpreadMin = 4f;     // 산 띠 높이 표준편차(m) — 평평한 고원은 산이 아니다
        const float WaterAreaMin = 0.06f;       // 지형 표본 중 수면 아래 비율(바다+강+호수)
        const float LayerShareMin = 0.03f;
        const float PassHeightMax = 12f;        // 고개(안부)는 평지 대비 이 정도까지 낮아야 한다
        const float PassShareMin = 0.12f;       // 띠 둘레에서 고개가 차지하는 비율 하한(산괴+고개 구조)
        const float MassifSpreadMin = 25f;      // 띠 안 최고-최저 차 — 균일한 톱니 방지
        // 산 중턱에서 풀·바위가 각각 이만큼은 있어야 한다. 0.10은 「중턱 풀 0.19」를 통과시켜 재반려됐다 —
        // 화면에서 읽히는 수준으로 0.35(검수 지시). 고친 뒤 실측 풀 0.66·바위 0.81.
        const float MixedBandShareMin = 0.35f;
        const float ShoreSandShareMin = 0.60f;  // 물가 표본 중 모래가 우세해야 하는 비율      // 풀·바위·모래 각 도포 비율 하한

        static void AssertWorldTerrain()
        {
            AssertDungeon3Leftover();

            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("씬에 Terrain이 없습니다.");
            var data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;

            if (Mathf.Abs(data.size.x - WorldTerrain.Span) > 0.5f)
                throw new InvalidOperationException("지형 한 변이 " + data.size.x + "m입니다 — 원장(WorldTerrain.Span) " + WorldTerrain.Span + "m와 다릅니다.");

            // ① 산 — 실제 기복. 하이트맵을 직접 읽는다(오브젝트가 아니라 지형이어야 §6.1 광산/산지다).
            float peak = 0f;
            float sum = 0f;
            float sumSq = 0f;
            int n = 0;
            for (int i = 0; i < 720; i++)
            {
                float ang = i / 720f * Mathf.PI * 2f;
                float r = WorldTerrain.MountainPeak;
                float wx = Mathf.Cos(ang) * r;
                float wz = Mathf.Sin(ang) * r;
                // 체비셰프 띠라 대각선은 더 멀다 — 띠 위로 투영한다.
                float scale = r / Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz));
                wx *= scale;
                wz *= scale;
                float h = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;
                peak = Mathf.Max(peak, h);
                sum += h;
                sumSq += h * h;
                n++;
            }
            float mean = sum / n;
            float spread = Mathf.Sqrt(Mathf.Max(0f, sumSq / n - mean * mean));
            Debug.Log("[Ulon] 지형 계측 산 최고 " + peak.ToString("0.0") + "m 평균 " + mean.ToString("0.0") + "m 표준편차 " + spread.ToString("0.0") + "m");
            if (peak - WorldTerrain.LandBase < MountainReliefMin)
                throw new InvalidOperationException("산 띠 최고점이 평지 대비 " + (peak - WorldTerrain.LandBase).ToString("0.0") + "m입니다 — 최소 " + MountainReliefMin + "m(§6.1 산지·§8.1 멀리서 읽히는 실루엣).");
            if (spread < MountainSpreadMin)
                throw new InvalidOperationException("산 띠 높이 표준편차가 " + spread.ToString("0.00") + "m입니다 — 최소 " + MountainSpreadMin + "m. 봉우리 없이 평평한 고원은 산으로 안 읽힙니다.");

            // ② 물 — 수면 재질이 텍스처를 가진 전용 재질이고, 지형이 실제로 수면 아래로 파여 있는가.
            var water = GameObject.Find(VisualSliceBuilder.WaterObject);
            if (water == null)
                throw new InvalidOperationException("수면 오브젝트가 없습니다: " + VisualSliceBuilder.WaterObject);
            var rend = water.GetComponent<Renderer>();
            if (rend == null || rend.sharedMaterial == null)
                throw new InvalidOperationException("수면에 재질이 없습니다.");
            var mat = rend.sharedMaterial;
            if (mat.name.IndexOf("Default", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("수면이 기본 재질(" + mat.name + ")입니다 — §8.2 Default-Material 프리미티브 금지.");
            if (mat.mainTexture == null)
                throw new InvalidOperationException("수면 재질에 텍스처가 없습니다 — 단색 파란 판은 §8.2 위반(단색 초록 금지와 같은 취지).");
            if (Mathf.Abs(water.transform.position.y - WorldTerrain.SeaLevel) > 0.01f)
                throw new InvalidOperationException("수면 높이가 " + water.transform.position.y + "입니다 — 원장 SeaLevel " + WorldTerrain.SeaLevel + "와 다릅니다.");

            int under = 0;
            int samples = 0;
            float halfSpan = WorldTerrain.Span * 0.5f;
            for (int gz = 0; gz < 60; gz++)
            {
                for (int gx = 0; gx < 60; gx++)
                {
                    float wx = -halfSpan + (gx + 0.5f) / 60f * WorldTerrain.Span;
                    float wz = -halfSpan + (gz + 0.5f) / 60f * WorldTerrain.Span;
                    float h = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;
                    samples++;
                    if (h < WorldTerrain.SeaLevel)
                        under++;
                }
            }
            float waterShare = under / (float)samples;
            Debug.Log("[Ulon] 지형 계측 수면 아래 비율 " + waterShare.ToString("0.00"));
            if (waterShare < WaterAreaMin)
                throw new InvalidOperationException("수면 아래 지형이 " + (waterShare * 100f).ToString("0") + "%뿐입니다 — 최소 " + (WaterAreaMin * 100f).ToString("0") + "%. 바다·강·호수가 실제로 파여 있지 않습니다.");

            // 호수와 강은 각각 따로 확인한다 — 바다만 넓어도 위 비율은 통과한다.
            if (terrain.SampleHeight(new Vector3(WorldTerrain.LakeX, 0f, WorldTerrain.LakeZ)) + origin.y >= WorldTerrain.SeaLevel)
                throw new InvalidOperationException("호수 중심(" + WorldTerrain.LakeX + ", " + WorldTerrain.LakeZ + ")이 수면 위입니다 — 호수가 파이지 않았습니다.");
            float riverMidX = (WorldTerrain.RiverFromX + WorldTerrain.RiverToX) * 0.5f;
            float riverMidZ = WorldTerrain.RiverZ + Mathf.Sin((riverMidX - WorldTerrain.RiverFromX) * 0.06f) * 6f;
            if (terrain.SampleHeight(new Vector3(riverMidX, 0f, riverMidZ)) + origin.y >= WorldTerrain.SeaLevel)
                throw new InvalidOperationException("강 중앙(" + riverMidX.ToString("0") + ", " + riverMidZ.ToString("0") + ")이 수면 위입니다 — 물길이 이어지지 않았습니다.");

            // ②-2 산괴와 고개 — 균일한 링은 담장으로 읽힌다(§8.1 검수 반려).
            //    띠를 한 바퀴 돌며 최고/최저를 본다: 고개(낮은 안부)와 큰 봉우리가 둘 다 있어야 산맥이다.
            // 방향마다 산 띠를 가로질러 **그 방향의 최고봉**을 구한다. 고정 반경 한 줄만 훑으면
            // 해안선 노이즈 때문에 띠를 빗나가 엉뚱한 값이 나온다(실측에서 겪었다).
            int dirs = 360;
            float bandMin = float.MaxValue;
            int passCount = 0;
            for (int i = 0; i < dirs; i++)
            {
                float ang = i / (float)dirs * Mathf.PI * 2f;
                float ux = Mathf.Cos(ang);
                float uz = Mathf.Sin(ang);
                float norm = Mathf.Max(Mathf.Abs(ux), Mathf.Abs(uz));
                float dirPeak = 0f;
                for (float m = WorldTerrain.MountainStart - 12f; m <= WorldTerrain.CoastEnd; m += 2f)
                {
                    float sc = m / norm;
                    float hh = terrain.SampleHeight(new Vector3(ux * sc, 0f, uz * sc)) + origin.y;
                    dirPeak = Mathf.Max(dirPeak, hh);
                }
                peak = Mathf.Max(peak, dirPeak);
                bandMin = Mathf.Min(bandMin, dirPeak);
                if (dirPeak < WorldTerrain.LandBase + PassHeightMax)
                    passCount++;
            }
            float passShare = passCount / (float)dirs;
            Debug.Log("[Ulon] 지형 계측 방향별 최고봉 최대 " + peak.ToString("0.0") + "m 최소 " + bandMin.ToString("0.0") + "m 고개 비율 " + passShare.ToString("0.00"));
            if (passShare < PassShareMin)
                throw new InvalidOperationException("산 띠에서 고개(평지+" + PassHeightMax + "m 이하)가 " + (passShare * 100f).ToString("0") + "%뿐입니다 — 최소 " + (PassShareMin * 100f).ToString("0") + "%. 봉우리가 균일하게 이어지면 산맥이 아니라 톱니 담장으로 읽힙니다(§8.1).");
            if (peak - bandMin < MassifSpreadMin)
                throw new InvalidOperationException("방향별 최고봉의 최대 " + peak.ToString("0.0") + "m와 최소 " + bandMin.ToString("0.0") + "m의 차가 " + (peak - bandMin).ToString("0.0") + "m입니다 — 최소 " + MassifSpreadMin + "m. 봉우리 크기가 균일하면 톱니 울타리로 보입니다.");

            // ③ 도포 — 초록 한 장으로 덮으면 §8.2 위반. 풀·바위·모래가 각각 실제로 칠해져 있는가.
            if (data.terrainLayers == null || data.terrainLayers.Length < 3)
                throw new InvalidOperationException("지형 레이어가 " + (data.terrainLayers == null ? 0 : data.terrainLayers.Length) + "장입니다 — 풀·바위·모래 3장이 필요합니다(§8.2).");
            int ar = data.alphamapResolution;
            var maps = data.GetAlphamaps(0, 0, ar, ar);
            var share = new float[3];
            for (int z = 0; z < ar; z += 4)
            {
                for (int x = 0; x < ar; x += 4)
                {
                    for (int l = 0; l < 3; l++)
                    {
                        if (maps[z, x, l] > 0.5f)
                            share[l] += 1f;
                    }
                }
            }
            float cells = Mathf.Ceil(ar / 4f) * Mathf.Ceil(ar / 4f);
            Debug.Log("[Ulon] 지형 계측 도포 풀 " + (share[0] / cells).ToString("0.00") + " 바위 " + (share[1] / cells).ToString("0.00") + " 모래 " + (share[2] / cells).ToString("0.00"));
            string[] names = { "풀", "바위", "모래" };
            for (int l = 0; l < 3; l++)
            {
                if (share[l] / cells < LayerShareMin)
                    throw new InvalidOperationException("지형 도포 " + names[l] + "가 " + (share[l] / cells * 100f).ToString("0.0") + "%뿐입니다 — 최소 " + (LayerShareMin * 100f).ToString("0") + "%(§8.2 단조로운 한 가지 색 금지).");
            }

            // ③-2 「어디에 칠했는지」 — 전체 비율만 보면 산 전체가 회색 한 장이어도 통과한다(검수 반려).
            //     산 중턱 구간에 풀과 바위가 **섞여** 있는지, 물가에 모래가 있는지 위치로 본다.
            int midG = 0, midR = 0, midN = 0, shoreN = 0, shoreSand = 0;
            for (int gz = 0; gz < 120; gz++)
            {
                for (int gx = 0; gx < 120; gx++)
                {
                    float wx = -halfSpan + (gx + 0.5f) / 120f * WorldTerrain.Span;
                    float wz = -halfSpan + (gz + 0.5f) / 120f * WorldTerrain.Span;
                    float h = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;
                    int ax = Mathf.Clamp(Mathf.RoundToInt((wx + halfSpan) / WorldTerrain.Span * (ar - 1)), 0, ar - 1);
                    int az = Mathf.Clamp(Mathf.RoundToInt((wz + halfSpan) / WorldTerrain.Span * (ar - 1)), 0, ar - 1);
                    // **풀은 두 겹이다**(짙은 풀 + 마른 풀, 검수 랩 ⑤). 이 자가 묻는 것은 「중턱에 풀이
                    // 섞여 있나」이므로 **풀 계열의 합**을 본다 — 한 겹만 보면 마른 풀로 간 몫이 사라져
                    // 멀쩡한 중턱이 「바위로 쏠렸다」로 읽힌다(실측 0.33으로 울었다).
                    float g = maps[az, ax, Ulon.Shared.WorldSplat.Grass] + maps[az, ax, Ulon.Shared.WorldSplat.DryGrass];
                    float r = maps[az, ax, Ulon.Shared.WorldSplat.Rock], sd = maps[az, ax, Ulon.Shared.WorldSplat.Sand];
                    if (h > WorldTerrain.LandBase + 6f && h < WorldTerrain.LandBase + 18f)
                    {
                        midN++;
                        if (g > 0.35f) midG++;
                        if (r > 0.35f) midR++;
                    }
                    if (Mathf.Abs(h - WorldTerrain.SeaLevel) < 0.8f)
                    {
                        shoreN++;
                        if (sd >= g && sd >= r) shoreSand++;
                    }
                }
            }
            Debug.Log("[Ulon] 지형 계측 중턱 풀 " + (midN > 0 ? midG / (float)midN : 0f).ToString("0.00") + " 바위 " + (midN > 0 ? midR / (float)midN : 0f).ToString("0.00") + " / 물가 모래 " + (shoreN > 0 ? shoreSand / (float)shoreN : 0f).ToString("0.00") + " (표본 " + midN + "·" + shoreN + ")");
            if (midN < 20)
                throw new InvalidOperationException("산 중턱 표본이 " + midN + "개뿐입니다 — 산비탈이 사실상 없습니다.");
            if (midG / (float)midN < MixedBandShareMin || midR / (float)midN < MixedBandShareMin)
                throw new InvalidOperationException("산 중턱 도포가 한쪽으로 쏠렸습니다(풀 " + (midG / (float)midN).ToString("0.00") + " · 바위 " + (midR / (float)midN).ToString("0.00") + ") — 각각 " + MixedBandShareMin + " 이상이어야 합니다. 산이 무채색 한 장으로 읽히고 밑동이 칼로 자른 듯 끊깁니다(§8.2).");
            if (shoreN < 20)
                throw new InvalidOperationException("물가 표본이 " + shoreN + "개뿐입니다 — 물가 경사가 절벽입니다.");
            if (shoreSand / (float)shoreN < ShoreSandShareMin)
                throw new InvalidOperationException("물가 도포 중 모래가 " + (shoreSand / (float)shoreN).ToString("0.00") + "입니다 — 최소 " + ShoreSandShareMin + ". 잔디가 물에 수직으로 잘립니다(§8.2).");

            // ④ 콘텐츠 좌표가 전부 뭍에 있는가 — 지형을 넓히거나 파면 여기서 P0가 재발한다.
            CheckOnLand(terrain, origin, "마을 광장", 0f, 0f);
            CheckOnLand(terrain, origin, "필드 보스", FieldBoss.X, FieldBoss.Z);
            CheckOnLand(terrain, origin, "던전 1 입구", Dungeon1.EntranceX, Dungeon1.EntranceZ);
            CheckOnLand(terrain, origin, "던전 2 입구", Dungeon2.EntranceX, Dungeon2.EntranceZ);
            CheckOnLand(terrain, origin, "던전 3 입구", Dungeon3.EntranceX, Dungeon3.EntranceZ);
            CheckOnLand(terrain, origin, "던전 1 내부", Dungeon1.InteriorX, Dungeon1.InteriorZ);
            CheckOnLand(terrain, origin, "던전 2 내부", Dungeon2.InteriorX, Dungeon2.InteriorZ);
            CheckOnLand(terrain, origin, "던전 3 내부", Dungeon3.InteriorX, Dungeon3.InteriorZ);

            Debug.Log("[Ulon] 월드 지형 통과 — 산 기복 " + MountainReliefMin + "m↑·수면 아래 " + (WaterAreaMin * 100f).ToString("0") + "%↑(바다·강·호수)·도포 3종·콘텐츠 전부 뭍");
        }

        /// <summary>이 좌표가 지형 안이고, 물에 잠기지 않고, 방(지하 4.5m)을 파도 물 아래로 안 내려가는가.</summary>
        static void CheckOnLand(Terrain terrain, Vector3 origin, string what, float wx, float wz)
        {
            float half = WorldTerrain.Span * 0.5f;
            if (Mathf.Abs(wx) > half - 4f || Mathf.Abs(wz) > half - 4f)
                throw new InvalidOperationException(what + " 좌표(" + wx + ", " + wz + ")가 지형 밖입니다(지형 반경 " + half + "m).");
            float h = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + origin.y;
            if (h < WorldTerrain.SeaLevel + 0.5f)
                throw new InvalidOperationException(what + " 좌표(" + wx + ", " + wz + ")의 지면이 " + h.ToString("0.0") + "m로 수면(" + WorldTerrain.SeaLevel + "m) 근처입니다 — 물에 잠깁니다.");
            if (h > WorldTerrain.LandBase + 8f)
                throw new InvalidOperationException(what + " 좌표(" + wx + ", " + wz + ")의 지면이 " + h.ToString("0.0") + "m입니다 — 산비탈에 얹혀 있습니다(평지 " + WorldTerrain.LandBase + "m).");
        }
    }
}
