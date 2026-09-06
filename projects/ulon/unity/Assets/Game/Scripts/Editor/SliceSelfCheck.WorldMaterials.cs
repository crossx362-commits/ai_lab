using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEditor;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// §8.2 「단조로운 화면·Default-Material 금지」를 **마을 밖까지** 확장한다(검수 2026-09-06, 열 번째 지적).
        /// 지역 게이트는 소품 **개수**만 세서 「흰 무텍스처 Kenney 바위 28개」가 만점으로 통과했다 —
        /// 개수는 맞고 화면은 틀린 부류다. 그래서 화면에 남는 렌더러의 **재질**을 직접 본다.
        ///
        /// 또 하나: 던전 암반 뚜껑이 지표 위로 솟으면 조망에서 회색 판으로 읽힌다. 뚜껑 타일마다
        /// **그 자리의 지형 높이**와 비교해 묻혀 있는지 잰다(경사에서 솟는 것을 잡는다).
        /// </summary>
        const float CapBuriedMargin = 0.15f;

        static void AssertWorldMaterials()
        {
            var untextured = new List<string>();
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int checkedCount = 0;
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled)
                    continue;
                var t = rends[i].transform;
                if (VisualSliceBuilder.IsCharacterArtPublic(t))
                    continue;
                if (rends[i].GetComponent<Terrain>() != null || rends[i] is ParticleSystemRenderer)
                    continue;
                var mats = rends[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null)
                    {
                        untextured.Add(Ancestry(t) + ":null-mat");
                        continue;
                    }
                    checkedCount++;
                    // 물(투명 노이즈 셰이더)과 눈 발광(Glow)은 텍스처 없이 색으로 읽히는 것이 정상이다 —
                    // 마을 게이트(AssertVillageVisuals)도 Water를 같은 이유로 뺀다.
                    if (mat.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0
                        || mat.name.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (mat.HasProperty("_MainTex") && mat.mainTexture == null)
                        untextured.Add(Ancestry(t) + ":" + mat.name);
                }
            }
            // 텍스처 유무만으로는 이번 결함을 못 잡는다 — Kenney 바위의 `colormap` 아틀라스는 텍스처가 **있고**,
            // 그 바위가 샘플하는 자리가 흰색이라 잔디 위에서 스티로폼으로 보였다(네거티브 컨트롤로 확인).
            // 그래서 바위류는 **우리 암석 재질을 쓰는지**를 직접 못 박는다. 흰 Kenney 바위는 세 번 반려된 단골이다.
            var rawRock = new List<string>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || VisualSliceBuilder.IsCharacterArtPublic(rends[i].transform))
                    continue;
                string anc = Ancestry(rends[i].transform);
                bool isRock = anc.IndexOf("rock-", StringComparison.OrdinalIgnoreCase) >= 0
                    || anc.IndexOf("Vein", StringComparison.OrdinalIgnoreCase) >= 0
                    || anc.IndexOf("Outcrop", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isRock)
                    continue;
                var mats2 = rends[i].sharedMaterials;
                for (int m = 0; m < mats2.Length; m++)
                {
                    var mat = mats2[m];
                    string n = mat != null ? mat.name : "null";
                    if (n.StartsWith("MountainRock", StringComparison.Ordinal) || n.StartsWith("IronVein", StringComparison.Ordinal)
                        || n.StartsWith("Dungeon", StringComparison.Ordinal) || n.StartsWith("Kenney", StringComparison.Ordinal))
                        continue;
                    rawRock.Add(anc + ":" + n);
                }
            }
            if (rawRock.Count > 0)
            {
                rawRock.Sort();
                throw new InvalidOperationException("바위 소품 " + rawRock.Count + "개가 Kenney 기본 재질 그대로입니다 — 잔디 위 흰 덩어리로 읽힙니다(§8.2). 예: " +
                    string.Join("; ", rawRock.GetRange(0, Mathf.Min(5, rawRock.Count))) + ". EnsureWorldPropMaterials의 암석/광맥 재질로 칠하세요.");
            }

            if (untextured.Count > 0)
            {
                untextured.Sort();
                int show = Mathf.Min(8, untextured.Count);
                throw new InvalidOperationException("무텍스처 재질 렌더러 " + untextured.Count + "개 — §8.2 위반(단색 덩어리로 보인다). 예: " +
                    string.Join("; ", untextured.GetRange(0, show)) + ". 노이즈 텍스처 재질(MakeNoiseMat)로 칠하세요.");
            }

            // 뚜껑이 지표 위로 솟았는가.
            var worst = 0f;
            string worstName = "";
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int caps = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name != "DungeonCap")
                    continue;
                var rend = all[i].GetComponent<Renderer>();
                if (rend == null)
                    continue;
                caps++;
                var b = rend.bounds;
                float ground = GroundHeightAt(b.center.x, b.center.z);
                float over = b.max.y - (ground - CapBuriedMargin);
                if (over > worst)
                {
                    worst = over;
                    worstName = b.center.x.ToString("0.0") + "," + b.center.z.ToString("0.0");
                }
            }
            if (caps == 0)
                throw new InvalidOperationException("던전 암반 뚜껑이 하나도 없습니다 — 하늘·주광이 방으로 샙니다.");
            if (worst > 0f)
                throw new InvalidOperationException("던전 뚜껑 타일이 지표 위로 " + worst.ToString("0.00") + "m 솟았습니다(" + worstName +
                    ") — 조망에서 회색 판으로 읽힙니다(§8.2). 타일 윗면을 그 자리 지형 아래로 내리세요.");

            AssertTerrainLayerContrast();

            Debug.Log("[Ulon] 월드 재질 통과 — 렌더러 재질 " + checkedCount + "개 전부 텍스처 있음·바위 소품 전부 암석 재질, 뚜껑 타일 " + caps + "장 전부 지표 아래(여유 " + CapBuriedMargin + "m)");
        }

        // 바위 텍스처는 풀과 **다른 무늬·다른 타일링·넓은 명암 폭**이어야 한다(검수 재반려).
        // 실측: 바위 표준편차 21.6 / 풀 4.9 / 모래 6.4 (0~255 휘도).
        const float RockValueStdMin = 14f;

        static void AssertTerrainLayerContrast()
        {
            var rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Game/Art/Env/MountainRock.terrainlayer");
            var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Game/Art/Env/VillageGrass.terrainlayer");
            if (rock == null || grass == null)
                throw new InvalidOperationException("지형 레이어 에셋이 없습니다(MountainRock/VillageGrass).");
            if (Mathf.Abs(rock.tileSize.x - grass.tileSize.x) < 0.5f)
                throw new InvalidOperationException("바위와 풀의 타일링이 같습니다(" + rock.tileSize.x + ") — 같은 스케일의 같은 잡음이면 산이 「초록/회색 두 색」으로만 읽힙니다(§8.2).");

            float std = LuminanceStd("Assets/Game/Art/Env/MountainRock.png");
            float grassStd = LuminanceStd("Assets/Game/Art/Env/KenneyGrass.png");
            if (std < RockValueStdMin)
                throw new InvalidOperationException("바위 텍스처의 명암 편차가 " + std.ToString("0.0") + "입니다 — 최소 " + RockValueStdMin +
                    "(풀 " + grassStd.ToString("0.0") + "). 단조로운 회색 한 장으로 보입니다(§8.2).");
            Debug.Log("[Ulon] 지형 레이어 대비 통과 — 바위 명암 편차 " + std.ToString("0.0") + "(풀 " + grassStd.ToString("0.0") + ")·타일 바위 " + rock.tileSize.x + " 풀 " + grass.tileSize.x);
        }

        /// <summary>PNG를 디스크에서 읽어 휘도 표준편차를 잰다(임포트된 텍스처는 isReadable이 아니라 GetPixels가 막힌다).</summary>
        static float LuminanceStd(string assetPath)
        {
            string full = System.IO.Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            if (!System.IO.File.Exists(full))
                throw new InvalidOperationException("텍스처 파일이 없습니다: " + assetPath);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(full)))
                throw new InvalidOperationException("텍스처를 읽지 못했습니다: " + assetPath);
            var px = tex.GetPixels();
            float mean = 0f;
            for (int i = 0; i < px.Length; i++)
                mean += (0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b) * 255f;
            mean /= px.Length;
            float var2 = 0f;
            for (int i = 0; i < px.Length; i++)
            {
                float v = (0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b) * 255f - mean;
                var2 += v * v;
            }
            return Mathf.Sqrt(var2 / px.Length);
        }

        static float GroundHeightAt(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return WorldTerrain.HeightAt(x, z);
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        static string Ancestry(Transform t)
        {
            var sb = new System.Text.StringBuilder();
            for (var cur = t; cur != null; cur = cur.parent)
                sb.Insert(0, cur.name + "/");
            return sb.ToString();
        }
    }
}
