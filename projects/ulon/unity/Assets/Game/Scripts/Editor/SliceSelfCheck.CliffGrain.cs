using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **암벽에 결이 남아 있는가**(검수 랩 ㉢ 반려).
        ///
        /// 랩 ㉡에서 마스크 투영을 고쳐 **톤은 갈렸는데** `16`의 앞 절벽이 **점토처럼 매끈해졌다.**
        /// 여기 있던 자들은 전부 「가중치가 어떻게 갈렸나」를 묻고, **「표면에 결이 남았나」를 묻는
        /// 자가 없었다** — 그래서 EXIT=0인데 벽이 뭉개졌다(랩 ⑪의 「자는 초록인데 그 겹이 없는
        /// 세계」와 같은 자리다).
        ///
        /// 재는 것: 급경사 자리에서 **그 자리를 덮는 겹들의 결 세기를 가중 평균**한다. 결 세기는
        /// 겹 텍스처의 **밝기 표준편차**다(화면은 못 재도 — 배치모드에 렌더가 없다 — 화면에 깔리는
        /// 것이 이 텍스처들이므로 밋밋한 겹이 벽을 덮으면 여기서 먼저 떨어진다).
        /// 밋밋한 겹으로 갈아 끼우거나, 밋밋한 겹이 벽을 더 차지하면 값이 내려간다.
        /// </summary>
        // 하한은 **앞 판을 실제로 재서** 잡았다(같은 자로 두 번 굽고 로그를 읽었다):
        // 반려된 판(어두운 겹 진폭 0.20) 0.051 · 폭을 넓힌 수리판 0.061 · 어두운 겹이 밋밋하면 0.041.
        // 0.055는 그 사이다 — **반려된 판이 이 자를 못 지나간다**(그게 이 자를 세운 이유다).
        const float CliffGrainMin = 0.055f;

        static void AssertCliffGrain()
        {
            AssertCliffGrainNegativeControl();

            float grain = CliffGrain(false, out int samples, out float darkStd, out float lightStd);
            if (samples < 100)
                throw new InvalidOperationException("급경사 표본이 " + samples + "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");

            Debug.Log("[Ulon] §8.2 암벽 결 — 급경사 표본 " + samples + "곳 · 결 세기 " + grain.ToString("0.000") +
                      " (하한 " + CliffGrainMin.ToString("0.000") + ") · 밝은 겹 " + lightStd.ToString("0.000") +
                      " · 어두운 겹 " + darkStd.ToString("0.000") +
                      " · 어두운 겹이 밋밋하면 " + CliffGrain(true, out _, out _, out _).ToString("0.000"));

            string verdict = CliffGrainVerdict(grain);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        /// <summary>판정만 하는 자리 — 표본과 떼어 놓아야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string CliffGrainVerdict(float grain)
        {
            if (grain < CliffGrainMin)
                return "암벽 결 세기가 " + grain.ToString("0.000") + "입니다(하한 " + CliffGrainMin.ToString("0.000") +
                       ") — 벽을 덮는 겹이 밋밋해 화면에서는 점토 면으로 읽힙니다.";
            return null;
        }

        /// <summary>
        /// 급경사 자리를 덮는 겹들의 **결 세기 가중 평균**. `flatDark`면 어두운 겹을 밋밋한 것으로
        /// 가정하고 다시 잰다(반대쪽 한계 — 밋밋한 겹이 벽을 덮으면 값이 내려가야 이 자가 산다).
        /// </summary>
        static float CliffGrain(bool flatDark, out int samples, out float darkStd, out float lightStd)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("지형이 없습니다 — 암벽 결을 검사할 수 없습니다.");
            var data = terrain.terrainData;
            int ar = data.alphamapResolution;
            var alpha = data.GetAlphamaps(0, 0, ar, ar);

            var layers = data.terrainLayers;
            var std = new float[WorldSplat.LayerCount];
            for (int i = 0; i < WorldSplat.LayerCount && i < layers.Length; i++)
                std[i] = LayerGrain(layers[i]);
            lightStd = std[WorldSplat.Rock];
            darkStd = std[WorldSplat.CliffDark];
            if (flatDark)
                std[WorldSplat.CliffDark] = 0f;

            samples = 0;
            float sum = 0f;
            for (float x = -145f; x <= 145f; x += 2.5f)
                for (float z = -145f; z <= 145f; z += 2.5f)
                {
                    if (WorldSplat.MacroSlopeTan(x, z) < 1.0f)      // 큰 경사 45° 아래는 벽이 아니다
                        continue;
                    float here = 0f, w = 0f;
                    for (int i = 0; i < WorldSplat.LayerCount; i++)
                    {
                        float a = Sample(alpha, ar, x, z, i);
                        here += a * std[i];
                        w += a;
                    }
                    if (w < 0.5f)
                        continue;
                    sum += here / w;
                    samples++;
                }
            return samples > 0 ? sum / samples : 0f;
        }

        /// <summary>
        /// 겹 텍스처의 **밝기 표준편차** — 이 겹이 화면에 깔릴 때 눈에 보이는 결의 세기다.
        ///
        /// 임포트된 텍스처는 CPU 사본이 없어 `GetPixels`가 죽는다. **임포트 설정을 읽기 가능으로
        /// 바꾸지 않는다** — 재는 자가 세계를 바꾸면 안 된다(랩 ③에서 밟았다). 디스크의 PNG를
        /// 그대로 읽어 임시 텍스처에 얹는다.
        /// </summary>
        static float LayerGrain(TerrainLayer layer)
        {
            var tex = layer != null ? layer.diffuseTexture : null;
            if (tex == null)
                return 0f;
            string path = UnityEditor.AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                throw new InvalidOperationException("겹 텍스처의 파일을 못 찾았습니다: " + tex.name + " — 결을 잴 수 없습니다.");
            var probe = new Texture2D(2, 2);
            if (!probe.LoadImage(System.IO.File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(probe);
                // 못 읽은 것을 0으로 넘기면 「밋밋하다」와 구별이 안 된다 — 조용히 넘어가지 않는다.
                throw new InvalidOperationException("겹 텍스처를 읽을 수 없습니다: " + path + " — 결을 잴 수 없습니다.");
            }
            var px = probe.GetPixels();
            UnityEngine.Object.DestroyImmediate(probe);
            if (px.Length == 0)
                return 0f;
            double mean = 0.0;
            for (int i = 0; i < px.Length; i++)
                mean += px[i].grayscale;
            mean /= px.Length;
            double var2 = 0.0;
            for (int i = 0; i < px.Length; i++)
            {
                double d = px[i].grayscale - mean;
                var2 += d * d;
            }
            return (float)Math.Sqrt(var2 / px.Length);
        }

        static void AssertCliffGrainNegativeControl()
        {
            if (CliffGrainVerdict(0.03f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 벽이 매끈한데도 통과했습니다.");
            if (CliffGrainVerdict(0.12f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 결이 살아 있는데 자가 걸렸습니다.");
            // **실제 파이프라인 반대쪽 한계**: 어두운 겹을 밋밋한 것으로 갈아 끼우면 빨간불이어야 한다.
            float flat = CliffGrain(true, out int n, out _, out _);
            if (n < 100)
                throw new InvalidOperationException("반대쪽 한계 실패 — 표본이 " + n + "곳뿐입니다.");
            if (CliffGrainVerdict(flat) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 어두운 겹이 밋밋해도(" + flat.ToString("0.000") +
                                                   ") 이 자를 통과합니다. 그러면 이 자는 결을 재는 것이 아닙니다.");
        }
    }
}
