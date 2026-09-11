using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **바다 해안이 얼마나 완만한가** — 대장 판정 ⓐ(2026-09-11 「44°로 꺼지는 해안은 상식
        /// 모순, 실제 백사장은 1~5°」)가 연 랩의 자.
        ///
        /// 재는 것은 **모래의 폭이 아니라 물속의 폭**이다. 기존 `AssertShoreBand`는 물 **바깥**
        /// 모래 도포 폭을 재고, 이 자는 물 **안쪽** — 물가에서 **수심 0.5m·1.0m까지의 가로 거리** —
        /// 를 잰다. 화면에서 「얕은 물 띠」로 읽히는 것은 후자다(깊이 색과 거품이 거기 산다).
        ///
        /// 원장(`WorldTerrain.HeightAt`)만 읽는다 — 구운 지형이 아니라 **규칙**을 잰다.
        /// **이 자가 못 보는 것**: 화면 픽셀이다. 200m 밖에서 몇 px로 보이는지는 `15`·`14`가 판정한다.
        /// </summary>
        internal static void CoastRampStats(out float minHalf, out float medHalf, out float medOne,
                                            out float medDeg, out int n, out int excluded, out string detail)
        {
            const int Bearings = 24;
            const float Step = 0.25f;
            var half = new System.Collections.Generic.List<float>();
            var one = new System.Collections.Generic.List<float>();
            var deg = new System.Collections.Generic.List<float>();
            excluded = 0;

            for (int b = 0; b < Bearings; b++)
            {
                float a = b * Mathf.PI * 2f / Bearings;
                float dx = Mathf.Cos(a), dz = Mathf.Sin(a);
                float rWater = -1f, rHalf = -1f, rOne = -1f;
                for (float r = 95f; r <= 215f; r += Step)
                {
                    float h = WorldTerrain.HeightAt(dx * r, dz * r);
                    float d = WorldTerrain.SeaLevel - h;
                    if (rWater < 0f && d > 0f) rWater = r;
                    if (rWater >= 0f && rHalf < 0f && d >= 0.5f) rHalf = r;
                    if (rWater >= 0f && rOne < 0f && d >= 1.0f) { rOne = r; break; }
                }
                if (rWater < 0f || rHalf < 0f) continue;         // 그 방위엔 바다가 없다(못 재는 자리)
                // **강 하구는 바다 해안이 아니다** — 강바닥은 물가에서 바로 1.4m로 떨어지므로
                // 백사장 자를 거기에 대면 「해안이 가파르다」가 아니라 **다른 물을 잰 것**이다
                // (호수 봉합 랩에서 표본 양 끝이 남의 물이었던 것과 같은 함정). 뺀 수를 센다.
                float px = dx * rWater, pz = dz * rWater;
                if (px < 0f && Mathf.Abs(pz - WorldTerrain.RiverZ) < WorldTerrain.RiverHalfWidth + 8f)
                { excluded++; continue; }
                half.Add(rHalf - rWater);
                one.Add(rOne > 0f ? rOne - rWater : 999f);
                // 물가의 기울기 — 수면 위아래 1m 구간의 평균
                float hIn = WorldTerrain.HeightAt(dx * (rWater + 2f), dz * (rWater + 2f));
                float hOut = WorldTerrain.HeightAt(dx * (rWater - 2f), dz * (rWater - 2f));
                deg.Add(Mathf.Atan(Mathf.Abs(hOut - hIn) / 4f) * Mathf.Rad2Deg);
            }

            n = half.Count;
            if (n == 0) { minHalf = medHalf = medOne = medDeg = -1f; detail = "바다를 만난 방위가 없습니다"; return; }
            half.Sort(); one.Sort(); deg.Sort();
            minHalf = half[0];
            medHalf = half[n / 2];
            medOne = one[n / 2];
            medDeg = deg[n / 2];
            var all = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++) all.Append(half[i].ToString("0.0")).Append(i < n - 1 ? " " : "");
            detail = "방위 " + n + "곳(강 하구 " + excluded + "곳 뺌) — 수심 0.5m까지 가로 최소 " + minHalf.ToString("0.0") +
                     "m·중앙 " + medHalf.ToString("0.0") + "m · 수심 1.0m까지 중앙 " +
                     medOne.ToString("0.0") + "m · 물가 기울기 중앙 " + medDeg.ToString("0.0") + "° · 0.5m 폭 전부[" + all + "]";
        }

        public static void RunCoastRamp()
        {
            CoastRampStats(out _, out _, out _, out _, out _, out _, out string det);
            Debug.Log("[해안램프] " + det);
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        }
    }
}
