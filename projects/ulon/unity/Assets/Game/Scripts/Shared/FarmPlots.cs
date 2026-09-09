using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// **농경지 뙈기 원장** — 어디에 몇 m짜리 밭을 두나, 그리고 **그 자리가 비탈이면 어디로 비키나**.
    ///
    /// 왜 원장인가: 뙈기 좌표는 그동안 빌더 안에만 있었고, 지형이 비탈이든 말든 그 자리에 앉았다.
    /// 셈(`OutdoorCensus.RunFieldFlatness`) 실측: 넷 중 뙈기 4가 고저차 6.2m·평균 경사 14.3°,
    /// 뙈기 3이 4.3m — `18_meadow`에서 울타리와 이랑이 언덕을 타고 넘어 「경작지」가 아니라
    /// 「비탈에 친 울타리」로 읽혔다. 사람은 밭을 비탈에 만들지 않는다(계단을 내기 전에는).
    ///
    /// 규칙: **지형이 밭 자리를 고른다**(`Flatten`). 먼저 뙈기를 평평한 곳으로 밀어 보았지만
    /// 이 지역에는 고저차 2.5m짜리 자리가 넷 나오지 않았고(밀어도 뙈기 3이 5.3m), 억지로 밀자
    /// 지역 밖 산포 덤불이 울타리 1.1m 앞에 서서 침입 자가 물었다. 자리를 옮기는 대신 **땅을 고른다** —
    /// 사람이 밭을 낼 때 하는 일이고, 규칙도 하나로 줄어든다.
    /// </summary>
    public static class FarmPlots
    {
        public const float SearchRadius = 12f;     // 원래 자리에서 이만큼 안에서만 비킨다(밭이 지역 밖으로 나가면 안 된다)
        public const float PlotGap = 2.5f;
        // 지역 **가장자리까지 붙이지 않는다** — 붙이면 지역 밖 산포 소품이 울타리 코앞에 선다
        // (실측: 뙈기를 민 첫 판에 `PlainScatter/plant_bush`가 밭 1.1m 앞이라 지역 침입 자가 물었다).
        public const float RegionMargin = 3f;        // 뙈기 사이 최소 틈(울타리끼리 붙지 않게)
        public const float SpreadMax = 2.5f;      // 자가 묻는 고저차 상한 — 이보다 크면 비탈밭이다

        public static readonly float[] Halves = { 7.5f, 5.5f, 6.0f, 8.0f };

        /// <summary>원래(설계) 자리 — 지역 중심 기준.</summary>
        public static Vector2[] Base(WorldRegions.Region r)
        {
            return new[]
            {
                new Vector2(r.X - 11f, r.Z - 9f), new Vector2(r.X + 10f, r.Z - 10f),
                new Vector2(r.X - 10f, r.Z + 10f), new Vector2(r.X + 11f, r.Z + 9f),
            };
        }

        public const float FlattenFade = 5f;      // 뙈기 바깥으로 이만큼에 걸쳐 원지형으로 되돌아간다

        static bool flattening;

        /// <summary>네거티브 컨트롤 전용 — 켜면 밭 자리를 고르지 않는다(자가 이 규칙을 재는지 확인용).</summary>
        public static bool FlattenDisabled;

        /// <summary>
        /// **밭 자리는 지형이 받아 준다** — 사람이 밭을 낼 때는 땅을 고른다.
        /// 뙈기를 아무리 밀어도 지역 안에 고저차 2.5m짜리 평지가 넷 나오지 않았다(실측: 뙈기 3이 5.3m).
        /// 그래서 뙈기 사각형을 그 중심 높이로 끌어당긴다. 재귀 가드는 중심 높이를 「평탄화 이전」으로
        /// 읽기 위한 것이다 — 없으면 지형이 자기 자신을 물어 무한히 돈다.
        /// </summary>
        public static float Flatten(float wx, float wz, float h, WorldRegions.Region r)
        {
            if (flattening || FlattenDisabled)
                return h;
            var basePlots = Base(r);
            for (int i = 0; i < basePlots.Length; i++)
            {
                float half = Halves[i] + 1.5f;      // 울타리 바깥 한 걸음까지 평평하게
                float dx = Mathf.Abs(wx - basePlots[i].x) - half;
                float dz = Mathf.Abs(wz - basePlots[i].y) - half;
                float d = Mathf.Max(dx, dz);
                if (d > FlattenFade)
                    continue;
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / FlattenFade));
                float centerH;
                flattening = true;
                try { centerH = WorldTerrain.HeightAt(basePlots[i].x, basePlots[i].y); }
                finally { flattening = false; }
                h = Mathf.Lerp(h, centerH, w);
            }
            return h;
        }

        /// <summary>이 뙈기 자리의 고저차(최고−최저, m).</summary>
        public static float Spread(float cx, float cz, float half)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (float x = cx - half; x <= cx + half; x += 1f)
            for (float z = cz - half; z <= cz + half; z += 1f)
            {
                float h = WorldTerrain.HeightAt(x, z);
                if (h < lo) lo = h;
                if (h > hi) hi = h;
            }
            return hi - lo;
        }
    }
}
