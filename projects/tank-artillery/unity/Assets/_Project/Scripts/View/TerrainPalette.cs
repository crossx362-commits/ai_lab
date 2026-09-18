// 지형 색 — 높이와 경사로 칠한다. 텍스처가 없으니(런타임 SDF 메시, §7-2) 정점 색이 유일한 통로다.
//
// 왜 필요한가: 단색 베이지 언덕은 **파괴가 안 보인다.** 쏴서 판 자리와 원래 지면이 같은 색이라
// 크레이터가 명암으로만 읽혔다 — 이 게임의 핵심(§7 지형 파괴)이 화면에서 가장 안 보이는 상태였다.
// 경사면을 바위색으로 칠하면 새로 판 구덩이의 벽이 즉시 드러난다.
//
// 색 값 자체는 맵 테마(MapTheme)가 들고 있다 — 원작이 맵마다 지형 색을 갈랐기 때문이다(조사 근거는 MapTheme.cs).
// ⚠️ 색을 여기나 청크에 직접 적지 마라. 한 곳(MapTheme)에서만 정해야 경계에서 색이 안 튄다.

using UnityEngine;

namespace Tankfall.View
{
    public static class TerrainPalette
    {
        /// <summary>y = 월드 높이, upness = 면 법선의 y(1=평지, 0=수직 절벽).</summary>
        public static Color Of(in MapTheme t, float y, float upness)
        {
            // 경사: 가파를수록 바위. 폭발로 판 벽이 여기서 드러난다.
            float slope = Mathf.Clamp01(Mathf.InverseLerp(0.86f, 0.42f, upness));

            // 고도 3구간(제한 팔레트 — 조사: 색을 늘리면 형태가 안 읽힌다).
            // ⚠️ 경계값은 **실제 맵 높이 분포**에 맞춰야 한다. 예전엔 3/8/24/36m 로 뒀는데
            //    TwinHills 언덕이 20~22m 라 지형 대부분이 한 구간(Mid)에 몰려 **전부 같은 초록**으로 보였다.
            //    지금 값은 0~26m 를 세 구간으로 고르게 가른다.
            //
            // 🚨 구간 경계를 **하드 컷오프로 두지 마라**(2026-09-18 수리). `y < 1.5f ? Low : …` 처럼
            //    끊어 놓으면 평지에서 그 높이선을 따라 색이 탁 바뀌는데, 정점 색이 복셀(0.5m) 해상도라
            //    그 경계가 **계단진 등고선**으로 드러난다 — 화면에 각진 얼룩으로 보였다.
            //    아래는 전부 smoothstep 으로 잇는다. 색을 바꾸려면 경계값만 옮기고 이 구조는 두어라.
            Color ground = Color.Lerp(Color.Lerp(t.Low, t.Mid, Band(y, 0.5f, 7f)),
                                      t.High, Band(y, 13f, 26f));

            // 지면보다 낮게 파인 자리는 더 어둡게 — 크레이터가 눈에 띄어야 한다(경계 없이 서서히).
            ground = Color.Lerp(ground, t.RockDark, (1f - Band(y, -2f, 2f)) * 0.35f);

            // 높은 절벽일수록 짙은 바위 — 이것도 이어서 준다(예전엔 26m 에서 탁 끊겼다).
            return Color.Lerp(ground, Color.Lerp(t.Rock, t.RockDark, Band(y, 20f, 30f)), slope);
        }

        /// <summary>구간 사이를 부드럽게 잇는다. 하드 컷오프가 만드는 계단진 등고선을 없애는 게 목적이다.</summary>
        static float Band(float v, float a, float b)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(a, b, v));
            return t * t * (3f - 2f * t);
        }
    }
}
