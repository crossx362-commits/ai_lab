using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **성능 회귀 경보다 — 성능 합격이 아니다.**(검수 2026-09-07)
    /// 프레임 시간을 아직 못 재므로 지금 값이 좋다는 근거는 **어디에도 없다**. 이 게이트가 재는 것은
    /// 「어제보다 갑자기 무거워졌는가」뿐이다. 통과를 「성능이 괜찮다」로 읽으면 그게 거짓 증거다.
    ///
    /// 상한 유도: 실측(`docs/PERF_BASELINE.md`) **× 1.5**(정상 작업으로 늘 수 있는 여유).
    ///   방 1개   렌더러 105 → 160,  삼각형 28,482 → 43,000,  머티리얼 10 → 15   (재질은 2026-09-08 새 자)
    ///   마을 40m 렌더러 852 → 1,278, 삼각형 127,608 → 191,000, 머티리얼 36 → 54
    ///   월드 200m 렌더러 2,426 → 3,600, 삼각형 337,738 → 507,000, 머티리얼 39 → 59
    /// 축이 셋인 이유: 머티리얼(드로우콜 대리)을 빼면 **재질만 잔뜩 늘어도 통과한다**.
    /// 2026-09-08 갱신 둘: ① 머티리얼 축의 자를 인스턴스 수 → **그림의 가짓수**로 바꿨다(아래 `PerfLimits` 주석)
    /// ② 마을 렌더러 기준선을 730 → **852**(오늘 실측)로 갱신했다 — 자를 바꾼 값이 아니라 물건이 는 것이므로
    /// 별도 축으로 계속 본다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        struct PerfLimit
        {
            public int Renderers;
            public int Triangles;
            public int Materials;
        }

        /// <summary>
        /// **머티리얼 축의 자를 바꿨다**(검수 판정 2026-09-08). 옛 자는 씬 안의 `Material` **인스턴스**를 셌고,
        /// 마을 광장에서 84가 나와 상한 83에 걸렸다. 그런데 그 84의 내역을 찍어 보니
        /// `colormap` 34개 · `grass` 7개 · `dirt` 3개 — **Kenney 프롭 34개가 각자 품고 있는 같은 재질**이었다.
        /// 프롭을 하나 더 놓을 때마다 자동으로 오르는 숫자는 「무거워졌다」의 신호가 아니다.
        /// 그래서 **상한을 올리는 대신** 자를 「그리는 그림의 가짓수」로 바꿨다(`PerfReport.MaterialKey`:
        /// 셰이더 + 메인 텍스처 에셋 + 색). 새 자의 오늘 실측: 방 10 · 마을 36 · 월드 39(돌길 도포를 고친 뒤 값 — 고치기 전엔 39·42였다).
        /// **렌더러 수는 그대로 별도 축**으로 남긴다(재질이 뭉쳐도 물건 수는 따로 봐야 한다) —
        /// 마을 기준선만 2026-09-07의 730에서 **오늘 실측 852**로 갱신했다(검수 지시).
        /// 상한은 전부 실측 × 1.5.
        /// </summary>
        static readonly PerfLimit[] PerfLimits =
        {
            new PerfLimit { Renderers = 160,   Triangles = 43000,  Materials = 15 },    // 던전 방 1개 (재질 10×1.5)
            new PerfLimit { Renderers = 1278,  Triangles = 191000, Materials = 54 },    // 마을 40m (렌더러 852×1.5, 재질 36×1.5)
            new PerfLimit { Renderers = 3600,  Triangles = 507000, Materials = 59 },    // 월드 200m (재질 39×1.5)
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
                if (c.Materials > lim.Materials)
                    Debug.Log("[Ulon] 성능 회귀 경보 — " + spots[i].Name + "의 머티리얼 목록: " +
                              string.Join(", ", PerfReport.LastMaterialNames));
                Alarm(spots[i].Name, "머티리얼 종류", c.Materials, lim.Materials);
            }
            Debug.Log("[Ulon] 성능 회귀 경보 통과 — **성능 합격이 아니다**(프레임 시간을 아직 못 잰다). " +
                      "지금 값이 2026-09-07 실측의 1.5배 안이라는 뜻일 뿐이다.");
        }

        /// <summary>
        /// **양방향 NC**(검수 지시 2026-09-08) — 자를 바꿨으니 자가 무엇에 반응하고 무엇에 안 반응하는지
        /// 둘 다 실물로 보인다. ㉠ **같은 그림을 한 번 더 물린다** → 종류 수가 늘면 안 된다(초록).
        /// ㉡ **지금 없는 다른 그림을 물린다** → 종류 수가 늘고, 그 값으로 판정하면 빨간불이어야 한다.
        /// ㉡이 없으면 「아무것도 안 세는 자」가 조용히 통과한다(0이면 실패와 같은 이유다).
        /// </summary>
        static void AssertPerfMaterialRulerNegativeControl()
        {
            var spot = PerfReport.Spots()[1];           // 마을 광장 — 자재가 가장 섞인 자리
            int before = PerfReport.Measure(spot).Materials;
            if (before == 0)
                throw new InvalidOperationException("머티리얼 자 NC — 잰 것이 0입니다(0이면 실패).");

            Renderer victim = null, donor = null;
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < rends.Length && (victim == null || donor == null); i++)
            {
                if (!rends[i].enabled || rends[i].sharedMaterial == null)
                    continue;
                if ((rends[i].transform.position - spot.Center).magnitude > spot.Radius)
                    continue;
                if (donor == null) { donor = rends[i]; continue; }
                if (PerfReport.MaterialKey(rends[i].sharedMaterial) != PerfReport.MaterialKey(donor.sharedMaterial))
                    victim = rends[i];
            }
            if (victim == null || donor == null)
                throw new InvalidOperationException("머티리얼 자 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");

            var saved = victim.sharedMaterial;
            try
            {
                // ㉠ 같은 그림의 **사본**을 물린다 — 에셋이 아니라 메모리 복제라 옛 자였다면 +1이 됐다.
                var copy = new Material(donor.sharedMaterial) { name = donor.sharedMaterial.name + " (사본)" };
                victim.sharedMaterial = copy;
                int dup = PerfReport.Measure(spot).Materials;
                UnityEngine.Object.DestroyImmediate(copy);
                if (dup > before)
                    throw new InvalidOperationException("머티리얼 자 NC ㉠ 실패 — 같은 그림의 사본을 물렸는데 종류가 " +
                        before + "→" + dup + "로 늘었습니다. 자가 아직 인스턴스를 세고 있습니다.");

                // ㉡ **다른 그림**을 물린다 — 씬에 없던 재질 에셋을 찾아 꽂는다.
                Material foreign = null;
                var guids = UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { "Assets/Game/Art/Env" });
                var here = new HashSet<string>(PerfReport.LastMaterialNames);
                for (int i = 0; i < guids.Length && foreign == null; i++)
                {
                    var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (m != null && !here.Contains(PerfReport.MaterialKey(m)))
                        foreign = m;
                }
                if (foreign == null)
                    throw new InvalidOperationException("머티리얼 자 NC ㉡ 대상이 없습니다 — 씬에 없는 재질 에셋을 못 찾았습니다.");
                victim.sharedMaterial = foreign;
                int added = PerfReport.Measure(spot).Materials;
                if (added <= before)
                    throw new InvalidOperationException("머티리얼 자 NC ㉡ 실패 — 다른 그림(" + foreign.name +
                        ")을 물렸는데 종류가 " + before + "→" + added + "입니다. 자가 새 재질을 못 봅니다.");
                bool red = false;
                try { Alarm(spot.Name, "머티리얼 종류", added, before); }
                catch (InvalidOperationException) { red = true; }
                if (!red)
                    throw new InvalidOperationException("머티리얼 자 NC ㉡ 실패 — 상한을 넘겼는데 경보가 안 울렸습니다.");
                Debug.Log("[Ulon] 머티리얼 자 양방향 NC 통과 — 사본 " + before + "→" + dup +
                          "(안 늚), 다른 그림 " + before + "→" + added + "(늚·경보)");
            }
            finally { victim.sharedMaterial = saved; }
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
