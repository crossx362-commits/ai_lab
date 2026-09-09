using System;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **얼룩이 낙하선으로 늘어나지 않는가**(검수 랩 ㉡).
        ///
        /// 랩 ⑧에서 **무늬**는 삼면 투영으로 고쳤는데 **무늬를 섞는 마스크**는 여전히 XZ 평면
        /// 좌표로 뽑고 있었다. 급경사에서는 XZ로 1m 가는 동안 표면이 7m를 가므로 마스크가
        /// **정확히 낙하선 방향으로 늘어난다** — 무늬는 안 늘어나는데 규칙만 늘어나 화면에는
        /// 정수리에서 흘러내리는 **회색 세로 띠(물때)**로 남았다. 빨강 판별 테스트로 봤다.
        ///
        /// 그래서 이 자는 「얼룩이 있나」가 아니라 **「얼룩이 방향을 타나」**를 묻는다:
        /// 같은 자리에서 **표면 위 같은 거리**만큼 ㉠낙하선 방향과 ㉡등고선 방향으로 가서
        /// 마스크가 얼마나 바뀌는지 잰다. 늘어난 마스크는 낙하선 쪽으로 **거의 안 바뀐다**.
        /// 비율이 1에 가까울수록 방향을 안 탄다(= 덩이).
        /// </summary>
        const float MaskFallRatioMin = 0.60f;   // 낙하선 변화 / 등고선 변화. 평면 마스크는 실측 0.2대다.
        const float MaskStep = 3f;              // 표면 위로 이만큼 간다(덩이 주기 15~31m보다 충분히 짧다)

        static void AssertCliffMaskFall()
        {
            AssertCliffMaskFallNegativeControl();

            float ratio = MaskFallRatio(false, out int samples, out float fall, out float contour);
            if (samples < 100)
                throw new InvalidOperationException("급경사 표본이 " + samples + "곳뿐입니다 — 이 자는 아무것도 재지 않았습니다.");

            Debug.Log("[Ulon] §8.2 얼룩 방향 — 급경사 표본 " + samples + "곳 · 낙하선 변화 " +
                      fall.ToString("0.000") + " · 등고선 변화 " + contour.ToString("0.000") +
                      " · 비율 " + ratio.ToString("0.00") + " (하한 " + MaskFallRatioMin.ToString("0.00") +
                      ") · 같은 자리 평면 마스크면 " + MaskFallRatio(true, out _, out _, out _).ToString("0.00"));

            string verdict = MaskFallVerdict(ratio);
            if (verdict != null)
                throw new InvalidOperationException(verdict);
        }

        /// <summary>판정만 하는 자리 — 표본과 떼어 놓아야 반대쪽 한계를 그냥 부를 수 있다.</summary>
        static string MaskFallVerdict(float ratio)
        {
            if (ratio < MaskFallRatioMin)
                return "암면 얼룩이 낙하선 방향으로 " + ratio.ToString("0.00") + "배만 바뀝니다(하한 " +
                       MaskFallRatioMin.ToString("0.00") + ") — 얼룩이 경사를 따라 늘어나 화면에서는 세로 물때로 읽힙니다.";
            return null;
        }

        /// <summary>
        /// 급경사 자리에서 마스크의 **방향별 변화량**을 잰다. `planar`면 옛 평면 마스크를 같은 자리로 잰다
        /// (반대쪽 한계 — 지어낸 수치가 아니라 안 고친 것을 실제로 부른다).
        /// </summary>
        static float MaskFallRatio(bool planar, out int samples, out float fallAvg, out float contourAvg)
        {
            samples = 0;
            float fallSum = 0f, contourSum = 0f;
            for (float x = -145f; x <= 145f; x += 2.5f)
                for (float z = -145f; z <= 145f; z += 2.5f)
                {
                    float tan = WorldSplat.MacroSlopeTan(x, z);
                    if (tan < 1.0f)          // 큰 경사 45° 아래는 늘어남이 눈에 안 띈다
                        continue;
                    // 낙하선(수평 투영) — 경사가 가장 급한 쪽. 등고선은 그에 수직이다.
                    const float d = 2f;
                    float hx = WorldTerrain.HeightAt(x + d, z) - WorldTerrain.HeightAt(x - d, z);
                    float hz = WorldTerrain.HeightAt(x, z + d) - WorldTerrain.HeightAt(x, z - d);
                    var down = new Vector2(hx, hz);
                    if (down.sqrMagnitude < 1e-6f)
                        continue;
                    down.Normalize();
                    var across = new Vector2(-down.y, down.x);
                    // **표면 위 거리를 맞춘다** — 낙하선 쪽은 수평으로 조금만 가도 표면은 많이 간다.
                    // 이걸 안 맞추면 「늘어남」이 아니라 「내가 덜 걸은 것」을 재게 된다.
                    float horiz = MaskStep / Mathf.Sqrt(1f + tan * tan);
                    float m0 = Mask(planar, x, z);
                    float mFall = Mask(planar, x + down.x * horiz, z + down.y * horiz);
                    float mCross = Mask(planar, x + across.x * MaskStep, z + across.y * MaskStep);
                    fallSum += Mathf.Abs(mFall - m0);
                    contourSum += Mathf.Abs(mCross - m0);
                    samples++;
                }
            fallAvg = samples > 0 ? fallSum / samples : 0f;
            contourAvg = samples > 0 ? contourSum / samples : 0f;
            return contourAvg > 1e-5f ? fallAvg / contourAvg : 0f;
        }

        static float Mask(bool planar, float wx, float wz)
        {
            return planar ? WorldSplat.DarkCliffPlanarAt(wx, wz) : WorldSplat.DarkCliffAt(wx, wz);
        }

        static void AssertCliffMaskFallNegativeControl()
        {
            if (MaskFallVerdict(0.25f) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 얼룩이 낙하선으로 늘어났는데도 통과했습니다.");
            if (MaskFallVerdict(0.9f) != null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 방향을 안 타는 얼룩인데 자가 걸렸습니다.");
            // **실제 파이프라인 반대쪽 한계**: 안 고친 마스크를 같은 표본으로 재면 빨간불이어야 한다.
            float planar = MaskFallRatio(true, out int n, out _, out _);
            if (n < 100)
                throw new InvalidOperationException("반대쪽 한계 실패 — 평면 마스크 표본이 " + n + "곳뿐입니다.");
            if (MaskFallVerdict(planar) == null)
                throw new InvalidOperationException("반대쪽 한계 실패 — 옛 평면 마스크(" + planar.ToString("0.00") +
                                                   ")가 이 자를 통과합니다. 그러면 이 자는 투영을 재는 것이 아닙니다.");
        }
    }
}
