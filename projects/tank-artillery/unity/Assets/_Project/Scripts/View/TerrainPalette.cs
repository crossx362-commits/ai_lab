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
            // 경계값은 실제 맵 높이 범위(0~40m)에 맞췄다.
            // ⚠️ 경계값은 **실제 맵 높이 분포**에 맞춰야 한다. 예전엔 3/8/24/36m 로 뒀는데
            //    TwinHills 언덕이 20~22m 라 지형 대부분이 한 구간(Mid)에 몰려 **전부 같은 초록**으로 보였다.
            //    지금 값은 0~24m 를 세 구간으로 고르게 가른다.
            Color ground =
                y < 1.5f ? t.Low :
                y < 7f ? Color.Lerp(t.Low, t.Mid, Mathf.InverseLerp(1.5f, 7f, y)) :
                y < 15f ? t.Mid :
                          Color.Lerp(t.Mid, t.High, Mathf.InverseLerp(15f, 26f, y));

            // 지면보다 낮게 파인 자리는 더 어둡게 — 크레이터가 눈에 띄어야 한다.
            if (y < 1f) ground = Color.Lerp(ground, t.RockDark, 0.35f);

            return Color.Lerp(ground, y > 26f ? t.RockDark : t.Rock, slope);
        }
    }
}
