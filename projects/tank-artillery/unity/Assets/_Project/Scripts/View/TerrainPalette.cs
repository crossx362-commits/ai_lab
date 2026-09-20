// 지형 색 — 높이와 경사로 칠한다. 텍스처가 없으니(런타임 SDF 메시, §7-2) 정점 색이 유일한 통로다.
//
// 왜 필요한가: 단색 베이지 언덕은 **파괴가 안 보인다.** 쏴서 판 자리와 원래 지면이 같은 색이라
// 크레이터가 명암으로만 읽혔다 — 이 게임의 핵심(§7 지형 파괴)이 화면에서 가장 안 보이는 상태였다.
// 경사면을 바위색으로 칠하면 새로 판 구덩이의 벽이 즉시 드러난다.
//
// 색 값 자체는 맵 테마(MapTheme)가 들고 있다 — 원작이 맵마다 지형 색을 갈랐기 때문이다(조사 근거는 MapTheme.cs).
// ⚠️ 색을 여기나 청크에 직접 적지 마라. 한 곳(MapTheme)에서만 정해야 경계에서 색이 안 튄다.

using UnityEngine;
using Tankfall.Sim;

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

            // ══════════════════════════════════════════════════════════════
            //  ③ **파낸 자리는 흙이다** (2026-09-19)
            //
            //  이 게임의 축은 지형 파괴(§2-2 · M2)인데 **크레이터가 화면에서 안 읽혔다.** 눈 맵에서는
            //  하얀 원반, 초원에서는 연두 원반으로 보였다 — 깊이도, 판 자리라는 것도 전달이 안 됐다.
            //
            //  원인은 이 함수의 **입력**이었다. 높이(y)와 경사만 봤기 때문에:
            //    · 위의 "낮게 파인 자리는 어둡게"는 **절대 높이 -2~2m** 를 본다. TwinHills 언덕(20m)에
            //      판 구덩이는 y=15m 라 그 구간에 아예 안 들어온다 — 언덕 위 크레이터는 한 번도 안 어두워졌다.
            //    · 경사(slope)는 **벽**만 잡는다. 크레이터 **바닥**은 평평해서 지표면과 같은 색으로 남는다.
            //
            //  그래서 **"원래 지면보다 얼마나 아래인가"** 를 새 입력으로 넣는다. 파괴 전 지형은
            //  `MapHeightFunction.Height` 가 해석적으로 돌려주므로(메모리 0, §7 머리말) 비교가 가능하다.
            //  오버행·동굴 천장 아래도 자연히 흙이 된다 — 거기도 **지표 아래**가 맞다.
            //
            //  ⚠️ 정점마다 높이 함수를 한 번 더 돈다. 폭발 프레임 비용(§7-6-3)을 **재고 나서** 넣었다.
            //  ⚠️ 눈 날씨에도 흙색 그대로다(`MapTheme.Of` 의 주석) — 흰 지면에 흰 구덩이가 문제였다.
            //  ⚠️ 이건 되돌렸던 "지형 색 얼룩" 가설과 **다른 것**이다. 그건 평평한 면의 얼룩이었고
            //     실측으로 음영임이 반증됐다. 이건 새 입력(굴착 여부)이지 같은 가설의 재시도가 아니다.
            // ══════════════════════════════════════════════════════════════
            float below = H0(t.Map, p.x, p.z) - p.y;
            // 0.8m 아래부터 흙이 비치기 시작해 4m 에서 완전히 흙이다.
            // ⚠️ 문턱을 0 으로 두지 마라 — 복셀(0.5m) 해상도의 표면 정점이 원래 높이보다 살짝 아래에
            //    앉는 일이 흔해서, **파지도 않은 평지가 통째로 흙색**이 된다.
            float dug = Mathf.Clamp01((below - 0.8f) / 3.2f);
            // 계측 — "넣었는데 화면이 안 바뀐다"를 눈으로 판정하지 않기 위해서다(색 노이즈 때의 교훈).
            DugVerts += dug > 0f ? 1 : 0; TotalVerts++; if (below > MaxBelow) MaxBelow = below;
            if (dug > 0f)
                col = Color.Lerp(col, Color.Lerp(t.Dirt, t.DirtDeep, dug), dug * 0.9f);

            // ② 밝기를 흔든다 — 이게 **질감**이다. ①만으로는 경계만 움직이고 면은 여전히 단색이다.
            //    ±7% 면 얼룩이 보이되 색이 더러워지지는 않는다(더 키우면 지면이 지저분해진다).
            float shade = 1f + blotch * 0.075f;
            return new Color(col.r * shade, col.g * shade, col.b * shade, col.a);
        }

        // ══════════════════════════════════════════════════════════════
        //  원래 지면 높이 캐시 (2026-09-19)
        //
        //  `MapHeightFunction.Height` 를 **정점마다** 부르면 비싸다 — 실측으로 24청크 재생성의
        //  법선+색이 1.49ms → 3.04ms 로 **두 배**가 됐고, 다탄두는 **한 프레임에 세 번** 돌아
        //  최악 5.93ms 까지 갔다(`-autoshot -roster MultiMissile,...` 로그).
        //  → 1m 격자로 한 번 구워 두고 이중선형으로 읽는다. 사인·코사인 여러 번 대신 덧셈 네 번이다.
        //
        //  ⚠️ **View 전용 캐시다. Sim 에 상태를 더하지 마라** — 결정론(§1 2순위)과 §10 델타 계약이 걸린다.
        //  ⚠️ 1m 격자의 보간 오차는 cm 단위라 문턱 0.8m 에 안 닿는다. 격자를 **성기게** 만들지 마라
        //     (4m 쯤 되면 언덕 꼭대기를 깎아 평지가 흙색이 된다).
        //  ⚠️ 맵이 바뀌면 다시 굽는다. 맵 종류로 판별하므로 같은 맵이면 재사용된다.
        // ══════════════════════════════════════════════════════════════
        const float HStep = 1f;
        static MapKind _hMap;
        static float[] _hGrid;
        static int _hN;

        static float H0(MapKind map, float x, float z)
        {
            if (_hGrid == null || _hMap != map) BuildHeightCache(map);
            float fx = Mathf.Clamp(x / HStep, 0f, _hN - 1.001f);
            float fz = Mathf.Clamp(z / HStep, 0f, _hN - 1.001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            int r0 = iz * _hN, r1 = r0 + _hN;
            float a = Mathf.Lerp(_hGrid[r0 + ix], _hGrid[r0 + ix + 1], tx);
            float b = Mathf.Lerp(_hGrid[r1 + ix], _hGrid[r1 + ix + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        static void BuildHeightCache(MapKind map)
        {
            _hN = Mathf.CeilToInt(MapHeightFunction.MapSize / HStep) + 2;
            _hGrid = new float[_hN * _hN];
            for (int j = 0; j < _hN; j++)
                for (int i = 0; i < _hN; i++)
                    _hGrid[j * _hN + i] = MapHeightFunction.Height(map, i * HStep, j * HStep);
            _hMap = map;
        }

        /// <summary>파낸 것으로 판정된 정점 수 / 전체 / 원래 지면 아래 최대 깊이. `TerrainView` 가 찍는다.</summary>
        public static int DugVerts, TotalVerts;
        public static float MaxBelow;
        public static void ResetStats() { DugVerts = TotalVerts = 0; MaxBelow = 0f; }

        /// <summary>
        /// 값 노이즈(-1~1). 사인 해시 하나 — 정점마다 도는 자리라 **싸야 한다**.
        /// 매끄럽지 않아도 된다(정점 색은 삼각형 안에서 보간되므로 화면에서는 부드럽게 퍼진다).
        /// </summary>
        static float Hash(float x, float z)
        {
            // 연속적인 넓은 색 번짐. 정점마다 독립 해시를 쓰면 모래알처럼 보인다.
            return Mathf.Sin(x * 1.7f + Mathf.Sin(z * .9f)) * Mathf.Cos(z * 1.3f + x * .4f);
        }

        /// <summary>구간 사이를 부드럽게 잇는다. 하드 컷오프가 만드는 계단진 등고선을 없애는 게 목적이다.</summary>
        static float Band(float v, float a, float b)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(a, b, v));
            return t * t * (3f - 2f * t);
        }
    }
}
