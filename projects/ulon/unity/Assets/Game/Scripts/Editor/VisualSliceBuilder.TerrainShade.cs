using System.IO;
using Ulon.Shared;
using UnityEditor;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        /// <summary>
        /// **지형을 삼면 투영으로 칠한다**(검수·대장 승인 2026-09-09).
        ///
        /// 기본 지형 셰이더는 도포를 XZ 평면으로 투영해서 경사가 설수록 무늬가 세로로 늘어난다.
        /// 이 산은 최대 82°다. 무늬를 두 겹으로 가르고(랩 ⑥) 바위를 104개 박아도(랩 ⑦) 줄은
        /// 안 끊겼다 — 늘어남은 **투영의 성질**이라 결과를 손봐서는 안 없어진다.
        ///
        /// **컨트롤은 유니티 자동 바인딩에 기대지 않는다.** 5겹부터 add pass로 갈라져 커스텀
        /// 재질에서 조용히 어긋나기 때문이다. 알파맵에서 직접 구워 꽂고, **그 일치를 자가 잰다**
        /// (자는 알파맵을 읽고 화면은 컨트롤 텍스처를 읽는다 — 둘이 어긋나면 「자는 초록, 화면은 딴것」).
        /// </summary>
        public const string TriplanarShader = "Ulon/TerrainTriplanar";
        const string CtrlPathFmt = "Assets/Game/Art/Env/TerrainCtrl{0}.asset";
        const string TerrainMatPath = "Assets/Game/Art/Env/TerrainTriplanar.mat";

        public static void ApplyTriplanarTerrain(Terrain terrain, TerrainData data, float[,,] alpha)
        {
            var shader = Shader.Find(TriplanarShader);
            if (shader == null)
            {
                Debug.LogWarning("[Ulon] 지형 셰이더를 못 찾았습니다: " + TriplanarShader);
                return;
            }
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/Env"));

            int ar = data.alphamapResolution;
            int layers = data.alphamapLayers;
            var ctrl = new Texture2D[3];
            for (int t = 0; t < 3; t++)
            {
                string path = string.Format(CtrlPathFmt, t);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null || tex.width != ar || tex.height != ar || tex.mipmapCount < 2)
                {
                    // 밉 체인을 만든다. 먼 지형은 화면 한 픽셀에 컨트롤 텍셀 여럿이 들어오므로
                    // 밉이 없으면 움직일 때 도포 경계가 지글거린다(유니티 기본 지형이 먼 데서
                    // basemap을 쓰는 이유). **다만 정지 QA 샷에서는 밉 전후 그림이 같았다** —
                    // 「능선 계단이 밉 탓」이라는 내 가설은 화면으로 확인 못 했다. 표준이라 남긴다.
                    tex = new Texture2D(ar, ar, TextureFormat.RGBA32, true, true);
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(tex, path);
                }
                var px = new Color[ar * ar];
                for (int z = 0; z < ar; z++)
                    for (int x = 0; x < ar; x++)
                        px[z * ar + x] = new Color(Weight(alpha, layers, z, x, t * 4 + 0),
                                                   Weight(alpha, layers, z, x, t * 4 + 1),
                                                   Weight(alpha, layers, z, x, t * 4 + 2),
                                                   Weight(alpha, layers, z, x, t * 4 + 3));
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.SetPixels(px);
                tex.Apply(true, false);   // true = 밉 체인을 다시 만든다
                EditorUtility.SetDirty(tex);
                ctrl[t] = tex;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, TerrainMatPath);
            }
            mat.shader = shader;
            mat.SetTexture("_Ctrl0", ctrl[0]);
            mat.SetTexture("_Ctrl1", ctrl[1]);
            mat.SetTexture("_Ctrl2", ctrl[2]);
            var tiles = new Vector4[3];
            for (int i = 0; i < 12; i++)
            {
                float tile = 8f;
                if (i < data.terrainLayers.Length && data.terrainLayers[i] != null)
                {
                    mat.SetTexture("_L" + i, data.terrainLayers[i].diffuseTexture);
                    tile = Mathf.Max(0.1f, data.terrainLayers[i].tileSize.x);
                }
                tiles[i / 4][i % 4] = tile;
            }
            mat.SetVector("_Tiles0", tiles[0]);
            mat.SetVector("_Tiles1", tiles[1]);
            mat.SetVector("_Tiles2", tiles[2]);
            mat.SetFloat("_WorldSpan", WorldTerrain.Span);
            mat.SetFloat("_TriSharp", 4f);
            mat.SetFloat("_PlanarOnly", 0f);
            EditorUtility.SetDirty(mat);

            terrain.materialTemplate = mat;
            // **생성물이 에셋이면 저장까지가 수리다** — 안 부르면 자는 초록인데 QA 샷은 옛 값을 찍는다.
            AssetDatabase.SaveAssets();
            Debug.Log("[Ulon] 지형 삼면 투영 — 도포 " + layers + "겹을 컨트롤 3장으로 구웠습니다(" + ar + "×" + ar + ").");
        }

        static float Weight(float[,,] alpha, int layers, int z, int x, int layer)
        {
            return layer < layers ? alpha[z, x, layer] : 0f;
        }
    }
}
