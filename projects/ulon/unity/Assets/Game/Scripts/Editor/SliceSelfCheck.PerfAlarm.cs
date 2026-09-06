using System;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **성능 회귀 경보다 — 성능 합격이 아니다.**(검수 2026-09-07)
    /// 프레임 시간을 아직 못 재므로 지금 값이 좋다는 근거는 **어디에도 없다**. 이 게이트가 재는 것은
    /// 「어제보다 갑자기 무거워졌는가」뿐이다. 통과를 「성능이 괜찮다」로 읽으면 그게 거짓 증거다.
    ///
    /// 상한 유도: 2026-09-07 실측(`docs/PERF_BASELINE.md`) **× 1.5**(정상 작업으로 늘 수 있는 여유).
    ///   방 1개   렌더러 105 → 160,  삼각형 28,482 → 43,000,  머티리얼 23 → 35
    ///   마을 40m 렌더러 730 → 1,100, 삼각형 127,608 → 191,000, 머티리얼 55 → 83
    ///   월드 200m 렌더러 2,426 → 3,600, 삼각형 337,738 → 507,000, 머티리얼 73 → 110
    /// 축이 셋인 이유: 머티리얼(드로우콜 대리)을 빼면 **재질만 잔뜩 늘어도 통과한다**.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        struct PerfLimit
        {
            public int Renderers;
            public int Triangles;
            public int Materials;
        }

        static readonly PerfLimit[] PerfLimits =
        {
            new PerfLimit { Renderers = 160,   Triangles = 43000,  Materials = 35 },    // 던전 방 1개
            new PerfLimit { Renderers = 1100,  Triangles = 191000, Materials = 83 },    // 마을 40m
            new PerfLimit { Renderers = 3600,  Triangles = 507000, Materials = 110 },   // 월드 200m
        };

        static void AssertPerfRegressionAlarm()
        {
            var spots = PerfReport.Spots();
            if (spots.Length != PerfLimits.Length)
                throw new InvalidOperationException("성능 회귀 경보 — 지점 수(" + spots.Length +
                    ")와 상한 수(" + PerfLimits.Length + ")가 다릅니다. 지점을 늘렸으면 상한도 실측에서 유도해 넣어라.");

            for (int i = 0; i < spots.Length; i++)
            {
                var c = PerfReport.Measure(spots[i]);
                var lim = PerfLimits[i];
                Debug.Log("[Ulon] 성능 회귀 경보 " + spots[i].Name + " — 렌더러 " + c.Renderers + "/" + lim.Renderers +
                          ", 삼각형 " + c.Triangles + "/" + lim.Triangles + ", 머티리얼 " + c.Materials + "/" + lim.Materials);
                if (c.Renderers == 0 || c.Triangles == 0)
                    throw new InvalidOperationException("성능 회귀 경보 — " + spots[i].Name +
                        "에서 아무것도 못 셌습니다(렌더러 " + c.Renderers + ", 삼각형 " + c.Triangles +
                        "). 아무것도 안 재고 통과시키지 않는다.");
                Alarm(spots[i].Name, "렌더러", c.Renderers, lim.Renderers);
                Alarm(spots[i].Name, "삼각형", (int)c.Triangles, lim.Triangles);
                Alarm(spots[i].Name, "머티리얼 종류", c.Materials, lim.Materials);
            }
            Debug.Log("[Ulon] 성능 회귀 경보 통과 — **성능 합격이 아니다**(프레임 시간을 아직 못 잰다). " +
                      "지금 값이 2026-09-07 실측의 1.5배 안이라는 뜻일 뿐이다.");
        }

        static void Alarm(string spot, string axis, int value, int limit)
        {
            if (value <= limit)
                return;
            throw new InvalidOperationException("성능 **회귀 경보**(성능 합격/불합격이 아니다) — " + spot + "의 " +
                axis + "이(가) " + value + "로 기준선(2026-09-07 실측)의 1.5배 상한 " + limit + "을 넘었습니다. " +
                "무거워진 원인을 찾거나, 의도한 증가라면 `docs/PERF_BASELINE.md`를 다시 재고 상한을 유도해 갱신하라.");
        }
    }
}
