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
        /// <summary>
        /// p = 월드 위치, upness = 면 법선의 y(1=평지, 0=수직 절벽).
        ///
        /// 🚨 **지면에 질감이 없었다**(2026-09-19 오너 지적 "그래픽 좀더 디테일하게").
        ///    높이·경사만 보고 칠하니 완만한 곳이 **거대한 단색 면**이 되고, 구간 경계가
        ///    **높이가 같은 선**을 따라가 지도 등고선처럼 칼로 자른 듯 드러났다.
        ///    → 위치로 두 가지를 흔든다: ① 구간 경계가 지나가는 자리 ② 밝기.
        ///    ①만으로는 단색 면이 그대로라 부족하다(실제로 ①만 넣었다가 화면이 안 바뀌어 되돌린 적이 있다).
        ///
        /// ⚠️ **비싼 노이즈를 쓰지 마라.** 폭발마다 더러워진 청크의 정점을 전부 다시 칠한다(§7-6-3).
        ///    Perlin 대신 사인 해시 두 번으로 끝낸다.
        /// </summary>
        public static Color Of(in MapTheme t, Vector3 p, float upness)
        {
            // 위치 노이즈 — 큰 얼룩(약 11m)과 잔 얼룩(약 3m)을 겹친다.
            float n1 = Hash(p.x * 0.09f, p.z * 0.09f);        // -1~1
            // ⚠️ 잔 얼룩의 파수를 정점 간격(0.5m)에 가깝게 올리지 마라 — 정점마다 값이 튀어
            //    **질감이 아니라 모래알 노이즈**가 된다. 0.20 은 약 5m 주기다.
            float n2 = Hash(p.x * 0.20f + 7.7f, p.z * 0.20f - 3.1f);
            float blotch = n1 * 0.70f + n2 * 0.30f;

            // ① 구간 경계를 흔든다 — 등고선 한 줄로 드러나지 않게
            float y = p.y + blotch * 2.6f;

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
            var col = Color.Lerp(ground, Color.Lerp(t.Rock, t.RockDark, Band(y, 20f, 30f)), slope);

            // ② 밝기를 흔든다 — 이게 **질감**이다. ①만으로는 경계만 움직이고 면은 여전히 단색이다.
            //    ±7% 면 얼룩이 보이되 색이 더러워지지는 않는다(더 키우면 지면이 지저분해진다).
            float shade = 1f + blotch * 0.075f;
            return new Color(col.r * shade, col.g * shade, col.b * shade, col.a);
        }

        /// <summary>
        /// 값 노이즈(-1~1). 사인 해시 하나 — 정점마다 도는 자리라 **싸야 한다**.
        /// 매끄럽지 않아도 된다(정점 색은 삼각형 안에서 보간되므로 화면에서는 부드럽게 퍼진다).
        /// </summary>
        static float Hash(float x, float z)
        {
            float v = Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f;
            return (v - Mathf.Floor(v)) * 2f - 1f;
        }

        /// <summary>구간 사이를 부드럽게 잇는다. 하드 컷오프가 만드는 계단진 등고선을 없애는 게 목적이다.</summary>
        static float Band(float v, float a, float b)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(a, b, v));
            return t * t * (3f - 2f * t);
        }
    }
}
