// 맵 테마 — 지형 색·오브젝트·하늘을 맵마다 다르게 준다.
//
// 출처(조사 2026-09-17, 나무위키 「포트리스2/맵」): 원작 맵은 **테마가 각각 뚜렷하다** —
// 이집트(The Sphinx, 계단식 지형 + 멀리 보이는 스핑크스), 해적선(The Cave), 무덤(Grave yard, 곳곳의 비석),
// 공장(The Factory, 움직이는 용광로), 밤의 시골(The Night, "노란색의 나무 줄기와 풀" + 거대한 달).
// 즉 원작은 **지형 색까지 맵마다 갈랐고**(보라색 지형, 분홍 지형), 맵마다 큰 배경 구조물을 세웠다.
// 우리 맵 6종도 같은 원리로 갈라 준다 — 이름만 다르고 전부 같은 베이지 언덕이면 맵을 나눈 게 화면에서 안 보인다.
//
// 색 구성 원칙(조사: 로우폴리 캐주얼): **제한된 팔레트**를 쓴다.
// 테마당 지면 3색 + 바위 2색까지만 — 더 늘리면 화면이 시끄러워지고 형태가 안 읽힌다.

using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public enum TreeKind { Broadleaf, Pine, Cactus }

    public struct MapTheme
    {
        public Color Low, Mid, High;      // 고도별 지면 3색 (제한 팔레트)
        public Color Rock, RockDark;      // 경사면
        public Color Dirt, DirtDeep;      // 파낸 흙(2026-09-19) — 지표 아래가 드러난 자리
        public MapKind Map;               // 팔레트가 "원래 지면 높이"를 되물으려면 맵을 알아야 한다
        public Color SkyTop, SkyBottom;   // 하늘 그라디언트
        public Color Sun;                 // 태양광 색 — 시간대 느낌을 만든다
        public Color Fog;
        public TreeKind Tree;
        public bool Snowy;                // 눈 날씨(§2-9-7). 나무·덤불 색도 이걸 본다
        public float TreeRatio;           // 나무 : 바위 비율(0=전부 바위)
        public int ScatterCount;

        public static MapTheme Of(MapKind k, bool snowy)
        {
            MapTheme t;
            switch (k)
            {
                // 초원 — 원작 The Single Log Bridge 의 "따뜻한 숲지대" 계열.
                default:
                case MapKind.TwinHills:
                    t = new MapTheme
                    {
                        // ⚠️ 채도를 더 올리지 마라. 한 번 올렸다가 조명·앰비언트와 겹쳐 **형광 연두**가 됐다.
                        //    로우폴리의 "제한 팔레트"는 색 수를 줄이라는 뜻이지 쨍하게 하라는 뜻이 아니다.
                        Low  = new Color(0.72f, 0.66f, 0.46f),   // 물가 모래
                        Mid  = new Color(0.38f, 0.55f, 0.29f),   // 풀
                        High = new Color(0.27f, 0.42f, 0.23f),   // 짙은 풀
                        Rock = new Color(0.52f, 0.48f, 0.42f),
                        RockDark = new Color(0.38f, 0.35f, 0.31f),
                        SkyTop = new Color(0.33f, 0.58f, 0.86f),
                        SkyBottom = new Color(0.76f, 0.87f, 0.95f),
                        Sun = new Color(1.00f, 0.97f, 0.88f),
                        Fog = new Color(0.74f, 0.84f, 0.92f),
                        Tree = TreeKind.Broadleaf, TreeRatio = 0.62f, ScatterCount = 280,
                    };
                    break;

                // 사막 — 원작 The Sphinx 의 고대 이집트 풍(계단식 + 저녁노을).
                case MapKind.Crater:
                    t = new MapTheme
                    {
                        Low  = new Color(0.86f, 0.74f, 0.50f),
                        Mid  = new Color(0.80f, 0.64f, 0.40f),
                        High = new Color(0.68f, 0.52f, 0.34f),
                        Rock = new Color(0.62f, 0.47f, 0.34f),
                        RockDark = new Color(0.46f, 0.34f, 0.25f),
                        SkyTop = new Color(0.36f, 0.48f, 0.78f),
                        SkyBottom = new Color(0.97f, 0.76f, 0.51f),   // 노을
                        Sun = new Color(1.00f, 0.88f, 0.70f),
                        Fog = new Color(0.93f, 0.80f, 0.62f),
                        Tree = TreeKind.Cactus, TreeRatio = 0.22f, ScatterCount = 210,
                    };
                    break;

                // 설산 계단식 — 원작에 눈 맵은 없지만 날씨(눈, §2-9-7)가 게임에 있으므로 그 축을 맵으로도 세운다.
                case MapKind.Terrace:
                    t = new MapTheme
                    {
                        Low  = new Color(0.60f, 0.62f, 0.58f),
                        Mid  = new Color(0.74f, 0.78f, 0.76f),
                        High = new Color(0.92f, 0.95f, 0.97f),
                        Rock = new Color(0.46f, 0.47f, 0.50f),
                        RockDark = new Color(0.33f, 0.34f, 0.38f),
                        SkyTop = new Color(0.26f, 0.42f, 0.68f),
                        SkyBottom = new Color(0.82f, 0.88f, 0.94f),
                        Sun = new Color(0.94f, 0.96f, 1.00f),
                        Fog = new Color(0.84f, 0.89f, 0.94f),
                        Tree = TreeKind.Pine, TreeRatio = 0.48f, ScatterCount = 240,
                    };
                    break;

                // 강 계곡 — 도랑 지형이니 물가·이끼 낀 초록 협곡으로. TwinHills(초원)와 갈리도록 채도를 눌러 이끼 낀 느낌을 준다.
                case MapKind.Valley:
                    t = new MapTheme
                    {
                        Low  = new Color(0.42f, 0.50f, 0.36f),
                        Mid  = new Color(0.30f, 0.44f, 0.30f),
                        High = new Color(0.22f, 0.34f, 0.26f),
                        Rock = new Color(0.40f, 0.42f, 0.38f),
                        RockDark = new Color(0.28f, 0.30f, 0.28f),
                        SkyTop = new Color(0.24f, 0.44f, 0.62f),
                        SkyBottom = new Color(0.66f, 0.78f, 0.80f),
                        Sun = new Color(0.92f, 0.95f, 0.90f),
                        Fog = new Color(0.62f, 0.72f, 0.70f),
                        Tree = TreeKind.Broadleaf, TreeRatio = 0.58f, ScatterCount = 260,
                    };
                    break;

                // 가을 능선 — 가운데 벽이 서는 지형이니 단풍 든 산등성이로. Terrace(설산)와 겹치지 않게 따뜻한 색으로 간다.
                case MapKind.Ridge:
                    t = new MapTheme
                    {
                        Low  = new Color(0.62f, 0.46f, 0.28f),
                        Mid  = new Color(0.74f, 0.42f, 0.20f),
                        High = new Color(0.58f, 0.30f, 0.16f),
                        Rock = new Color(0.50f, 0.44f, 0.38f),
                        RockDark = new Color(0.36f, 0.30f, 0.26f),
                        SkyTop = new Color(0.40f, 0.52f, 0.72f),
                        SkyBottom = new Color(0.92f, 0.80f, 0.62f),
                        Sun = new Color(1.00f, 0.86f, 0.62f),
                        Fog = new Color(0.86f, 0.76f, 0.62f),
                        Tree = TreeKind.Broadleaf, TreeRatio = 0.50f, ScatterCount = 250,
                    };
                    break;

                // 황무지 — 반복 능선이니 화산재 덮인 용암지대로. Crater(사막)와 갈리도록 채도를 죽인 어두운 팔레트.
                case MapKind.Badlands:
                    t = new MapTheme
                    {
                        Low  = new Color(0.30f, 0.27f, 0.25f),
                        Mid  = new Color(0.40f, 0.32f, 0.27f),
                        High = new Color(0.52f, 0.34f, 0.24f),
                        Rock = new Color(0.24f, 0.22f, 0.21f),
                        RockDark = new Color(0.14f, 0.13f, 0.13f),
                        SkyTop = new Color(0.34f, 0.24f, 0.26f),
                        SkyBottom = new Color(0.78f, 0.46f, 0.30f),
                        Sun = new Color(1.00f, 0.62f, 0.38f),
                        Fog = new Color(0.62f, 0.42f, 0.34f),
                        Tree = TreeKind.Cactus, TreeRatio = 0.08f, ScatterCount = 160,
                    };
                    break;
            }

            t.Map = k;

            // ── 파낸 흙 (2026-09-19) ──
            // 맵마다 따로 짓지 않고 **그 맵의 바위색에서 유도한다.** 6개 팔레트를 새로 지어내면
            // 서로 안 맞는 색이 섞이고, 맵이 늘 때마다 빠뜨린다(맵 3→6 때 깨졌던 종류의 자리).
            // 맵 고유색이 필요하면 위 switch 안에서 덮어써라 — 여기서는 안 덮어쓴다.
            if (t.Dirt.a <= 0f) t.Dirt = Color.Lerp(t.RockDark, new Color(0.44f, 0.31f, 0.20f), 0.75f);
            if (t.DirtDeep.a <= 0f) t.DirtDeep = Color.Lerp(t.RockDark, new Color(0.25f, 0.17f, 0.11f), 0.80f);

            // 눈 날씨는 맵 위에 덧씌운다 — 맵 테마를 지우지 않고 밝기만 올린다(포세이돈 조건이 보여야 하므로, §2-9-7).
            t.Snowy = snowy;
            if (snowy)
            {
                t.Mid = Color.Lerp(t.Mid, Color.white, 0.55f);
                t.High = Color.Lerp(t.High, Color.white, 0.70f);
                t.Low = Color.Lerp(t.Low, Color.white, 0.35f);
                t.SkyTop = Color.Lerp(t.SkyTop, new Color(0.62f, 0.68f, 0.76f), 0.55f);
                t.SkyBottom = Color.Lerp(t.SkyBottom, new Color(0.88f, 0.90f, 0.93f), 0.55f);
                t.Sun = Color.Lerp(t.Sun, new Color(0.90f, 0.93f, 1.00f), 0.6f);
                t.Fog = Color.Lerp(t.Fog, new Color(0.88f, 0.91f, 0.95f), 0.6f);
                // ⚠️ **파낸 흙은 눈으로 덮지 마라.** 눈 맵에서 지표가 온통 흰색이라,
                //    판 자리가 흙색으로 남아야 "여기가 뚫렸다"가 가장 크게 읽힌다.
                //    (흰 지면에 흰 구덩이는 명암으로만 보인다 — 고치려는 문제가 그거다.)
            }
            return t;
        }
    }
}
