using System;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **자갈은 모래와 갈려야 한다**(2026-09-09, `63_pier_cutface`).
        ///
        /// 호수 절개면은 셈으로 자갈 77~94%인데 화면은 크림색(186)이라 옆 모래톱(238)과 한 톤이었다.
        /// 대낮 직사광이 톤을 통째로 올리므로, **겹 자체의 밝기 차**가 충분히 벌어져 있지 않으면
        /// 화면에서 두 겹이 하나로 읽힌다. 그래서 구운 텍스처를 직접 읽어 두 가지를 묻는다:
        /// ① 자갈이 모래보다 충분히 어두운가(비율 상한) ② 알갱이가 보이는가(결 = 표준편차 하한).
        ///
        /// 결 하한은 밋밋해지는 쪽 회귀를 막는다 — 어둡게만 하면 「검은 점토 면」이 된다(암벽 랩의 교훈).
        /// NC는 옛 값(0.28~0.55)을 그 자리에서 계산해 이 자가 물어야 함을 보인다.
        /// </summary>
        // 상한은 **화면이 정한다**: 옛 값(0.53)이 대낮 절개면에서 모래톱과 한 톤(186 대 238)으로 읽혔다.
        // 그래서 그보다 확실히 낮은 자리에 선을 긋는다 — 새 값은 0.39다.
        const float GravelOverSandMax = 0.45f;   // 자갈 평균 ÷ 모래 평균 상한
        const float GravelGrainMin = 0.035f;     // 자갈 텍스처 표준편차 하한(0~1)

        static void AssertGravelTone()
        {
            float gravel = TextureTone("Assets/Game/Art/Env/MineGravel.png", out float grain);
            float sand = TextureTone("Assets/Game/Art/Env/ShoreSand.png", out float _);
            float ratio = sand > 0f ? gravel / sand : 1f;
            Debug.Log("[Ulon] 자갈 톤 — 자갈 " + gravel.ToString("0.000") + " ÷ 모래 " + sand.ToString("0.000") +
                      " = " + ratio.ToString("0.00") + "(상한 " + GravelOverSandMax + ") · 결 " +
                      grain.ToString("0.000") + "(하한 " + GravelGrainMin + ")");
            if (ratio > GravelOverSandMax)
                throw new InvalidOperationException("자갈이 모래 밝기의 " + ratio.ToString("0.00") +
                    "배입니다(상한 " + GravelOverSandMax + ") — 대낮 화면에서 두 겹이 한 톤으로 읽힙니다.");
            if (grain < GravelGrainMin)
                throw new InvalidOperationException("자갈 결이 " + grain.ToString("0.000") + "입니다(하한 " +
                    GravelGrainMin + ") — 어둡기만 하고 알갱이가 없으면 젖은 점토 면입니다.");

            // NC — 옛 값(0.28~0.55)이었다면 이 자가 물었어야 한다. 텍스처를 다시 굽지 않고
            // **같은 자리에서 계산**한다: 결(무늬)은 그대로고 톤만 옛 범위로 되돌린 경우의 평균.
            float oldMean = ToneOf(new Color(0.28f, 0.26f, 0.24f), new Color(0.55f, 0.52f, 0.47f));
            float oldRatio = sand > 0f ? oldMean / sand : 1f;
            if (oldRatio <= GravelOverSandMax)
                throw new InvalidOperationException("자갈 톤 네거티브 컨트롤 실패 — 옛 값으로도 " +
                    oldRatio.ToString("0.00") + "이라 통과합니다. 이 자는 아무것도 가르지 않습니다.");
            Debug.Log("[Ulon] 자갈 톤 NC 통과 — 옛 값(0.28~0.55)이면 " + oldRatio.ToString("0.00") + "로 FAIL");
        }

        /// <summary>구운 텍스처의 평균 밝기(0~1)와 표준편차.</summary>
        static float TextureTone(string path, out float grain)
        {
            // **에셋으로 불러온 텍스처는 읽을 수 없다**(임포터가 Read/Write를 끈다) — 파일을 직접 디코드한다.
            string full = System.IO.Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            if (!System.IO.File.Exists(full))
                throw new InvalidOperationException("지형 겹 텍스처가 없습니다: " + path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!UnityEngine.ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(full)))
                throw new InvalidOperationException("지형 겹 텍스처를 못 읽었습니다: " + path);
            var px = tex.GetPixels();
            UnityEngine.Object.DestroyImmediate(tex);
            if (px.Length == 0)
                throw new InvalidOperationException("지형 겹 텍스처가 비었습니다: " + path);
            double sum = 0d, sum2 = 0d;
            for (int i = 0; i < px.Length; i++)
            {
                float v = px[i].grayscale;
                sum += v;
                sum2 += v * v;
            }
            double mean = sum / px.Length;
            grain = (float)Math.Sqrt(Math.Max(0d, sum2 / px.Length - mean * mean));
            return (float)mean;
        }

        /// <summary>두 색을 고르게 섞었을 때의 평균 밝기 — 무늬는 0~1을 고르게 훑는다고 본다.</summary>
        static float ToneOf(Color a, Color b)
        {
            return (a.grayscale + b.grayscale) * 0.5f;
        }
    }
}
