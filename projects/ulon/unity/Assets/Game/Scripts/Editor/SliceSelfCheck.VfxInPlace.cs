using System;
using Ulon.Client;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **효과가 실제 플레이 프레임에서 읽히는가**(검수 랩 D, 사각지대 표 ④).
    ///
    /// 기존 `AssertActionVfxOnScreen`은 **검은 배경·허공(y=800)**에서 효과 하나만 렌더한다.
    /// 그건 「효과가 렌더되긴 한다」만 증명한다 — 실제 화면은 대낮의 밝은 땅이거나 어두운 던전이고,
    /// 카메라는 쿼터뷰로 멀리 있다. 검은 배경에서 잘 보이던 것이 마을 대낮에는 안 보일 수 있다.
    ///
    /// 그래서 **플레이 카메라 그대로** 같은 프레임을 두 번 찍는다(효과 없이 / 효과 있게) —
    /// 판정은 두 프레임의 **차이**다: ① 바뀐 픽셀 비율 ② 바뀐 자리의 색 대비.
    /// 배경이 무엇이든 「눈에 띄는 변화가 생겼는가」를 재므로 배경색에 안 흔들린다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const int PlayVfxW = 640, PlayVfxH = 360;
        const float PlayVfxPixelDelta = 0.08f;       // 이 이상 달라진 픽셀을 「바뀐 픽셀」로 센다
        // 임계는 **두 밴드를 실측해** 그 사이로 잡는다(처음 쓴 0.4%는 아무것도 재기 전의 짐작이었다 — 공개).
        //   결함(고치기 전 크기 0.30~0.42m): 야외 0.02~0.07%
        //   정상(고친 크기 0.85~1.35m):     야외 0.34~0.61% · 지하 2.18~3.70%
        // 0.25%는 결함 최댓값의 3.5배, 정상 최솟값의 0.74배다. NC(6% 축소)가 매 실행 이 선을 못 넘는 것도 확인한다.
        const float PlayVfxChangedMin = 0.0025f;     // 화면의 0.25%
        // **밝기 차가 아니라 색 차로 잰다**(규칙 변경, 공개): 주황 불티는 모래 바닥과 밝기가 거의 같아
        // (루마 0.53 vs 0.6) 밝기 축으로는 「안 읽힘」으로 나오지만 화면에서는 색으로 또렷이 읽힌다.
        // 사람 눈이 보는 것은 밝기만이 아니다 — 채널 최대 차의 평균을 쓴다.
        const float PlayVfxContrastMin = 0.18f;      // 바뀐 자리의 평균 색 차(채널 최대)
        // **양쪽 한계**(검수 지시) — 하한만 두면 다음 사람이 「잘 보이게」 키우다 전투 화면을 덮는다.
        // 상한은 「효과가 대상을 가리지 않는 최대」다. 실측: 정상 최대 지하 Hit 3.2%대,
        // 결함(2.5배로 키움) 지하 45.7% → 그 사이인 6%.
        const float PlayVfxChangedMax = 0.06f;       // 화면의 6%
        // 여유 메모: 야외 Craft가 하한 바로 위다(0.32% vs 0.25%). 야외 효과를 더 줄이면 바로 빨간불이다.

        struct PlayVfxMeasure
        {
            public float Changed;      // 바뀐 픽셀 / 전체
            public float Contrast;     // 바뀐 픽셀의 평균 색 차(채널 최대)
        }

        struct PlayVfxSpot
        {
            public string Label;
            public Vector3 Player;
            public float Distance;
        }

        static PlayVfxSpot[] PlayVfxSpots()
        {
            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float outdoor = qv != null ? qv.Distance : 12f;
            float indoor = qv != null ? Mathf.Min(qv.Distance, qv.IndoorDistance) : 5.8f;
            float villageY = TerrainHeight(0f, 0f);
            float roomY = TerrainHeight(Dungeon3.InteriorX, Dungeon3.InteriorZ) - VisualSliceBuilder.DungeonDepth;
            return new[]
            {
                // 대낮의 밝은 마을(야외 줌 18m) — 밝은 효과가 배경에 묻히는 자리
                new PlayVfxSpot { Label = "마을 광장(야외 대낮)", Player = new Vector3(0f, villageY + 1.0f, 0f), Distance = outdoor },
                // 어두운 지하 방 — 반대쪽 극단
                new PlayVfxSpot { Label = "던전 3 방(지하)", Player = new Vector3(Dungeon3.InteriorX, roomY + 1.0f, Dungeon3.InteriorZ), Distance = indoor },
            };
        }

        static float TerrainHeight(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            return terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y
                                   : WorldTerrain.LandBase;
        }

        public static void AssertActionVfxInPlayFrame()
        {
            var spots = PlayVfxSpots();
            var kinds = new[] { ActionVfx.Kind.Hit, ActionVfx.Kind.Heal, ActionVfx.Kind.Craft };
            int measured = 0;
            // **전부 재고 나서 판정한다** — 첫 실패에서 멈추면 나머지 다섯 자리의 수치를 못 본다.
            var failures = new System.Collections.Generic.List<string>();
            for (int s = 0; s < spots.Length; s++)
                for (int k = 0; k < kinds.Length; k++)
                {
                    var m = MeasureVfxInPlace(spots[s], kinds[k], 1f);
                    measured++;
                    Debug.Log("[Ulon] VFX 실화면 " + spots[s].Label + " · " + kinds[k] +
                              " — 바뀐 픽셀 " + (m.Changed * 100f).ToString("F2") + "%, 대비 " + m.Contrast.ToString("F2"));
                    if (m.Changed < PlayVfxChangedMin || m.Contrast < PlayVfxContrastMin)
                        failures.Add(kinds[k] + "가 " + spots[s].Label + "에서 안 읽힘 — 바뀐 픽셀 " +
                            (m.Changed * 100f).ToString("F2") + "%(하한 " + (PlayVfxChangedMin * 100f).ToString("F2") +
                            "%), 대비 " + m.Contrast.ToString("F2") + "(하한 " + PlayVfxContrastMin + ")");
                    if (m.Changed > PlayVfxChangedMax)
                        failures.Add(kinds[k] + "가 " + spots[s].Label + "에서 화면을 너무 덮는다 — 바뀐 픽셀 " +
                            (m.Changed * 100f).ToString("F2") + "%(상한 " + (PlayVfxChangedMax * 100f).ToString("F2") +
                            "%). 효과가 플레이어·대상을 가리면 회복 대상을 보면서 쓸 수 없다.");
                }
            if (measured == 0)
                throw new Exception("VFX 실화면 측정을 한 번도 못 했습니다 — 잰 것이 없습니다(0이면 실패).");
            if (failures.Count > 0)
                throw new Exception("VFX가 실제 플레이 프레임에서 안 읽히는 자리 " + failures.Count + "곳:\n  " +
                    string.Join("\n  ", failures) +
                    "\n검은 배경 게이트를 통과해도 실제 화면에서 묻히면 없는 것과 같습니다.");
            Debug.Log("[Ulon] VFX 실화면 — 지점 " + spots.Length + "곳 × 3종 전부 플레이 프레임에서 읽힘");
        }

        /// <summary>
        /// 네거티브 컨트롤 — 효과를 **실제로 작게 만들어** 같은 자리에서 재면 빨간불이어야 한다.
        /// (템플릿 삭제는 기존 NC가 이미 본다. 여기서 봐야 할 결함은 「있긴 한데 안 읽힘」이다.)
        /// </summary>
        public static void AssertActionVfxInPlayFrameNegativeControl()
        {
            var spot = PlayVfxSpots()[0];
            var weak = MeasureVfxInPlace(spot, ActionVfx.Kind.Hit, 0.06f);
            Debug.Log("[Ulon] VFX 실화면 네거티브 컨트롤 — 6% 크기: 바뀐 픽셀 " +
                      (weak.Changed * 100f).ToString("F2") + "%, 대비 " + weak.Contrast.ToString("F2"));
            if (weak.Changed >= PlayVfxChangedMin && weak.Contrast >= PlayVfxContrastMin)
                throw new Exception("VFX 실화면 네거티브 컨트롤 실패 — 효과를 6% 크기로 줄였는데도 임계를 넘었습니다. " +
                    "임계가 너무 낮아 「안 읽히는 효과」를 통과시킵니다.");

            // **반대쪽 네거티브 컨트롤** — 2.5배로 키우면 상한을 넘어야 한다. 상한이 살아 있다는 증거다
            // (하한만 검사하면 「화면을 덮는 효과」로 빠져나간다).
            var loud = MeasureVfxInPlace(PlayVfxSpots()[1], ActionVfx.Kind.Hit, 2.5f);
            Debug.Log("[Ulon] VFX 실화면 네거티브 컨트롤 — 250% 크기(지하): 바뀐 픽셀 " +
                      (loud.Changed * 100f).ToString("F2") + "%, 상한 " + (PlayVfxChangedMax * 100f).ToString("F2") + "%");
            if (loud.Changed <= PlayVfxChangedMax)
                throw new Exception("VFX 실화면 상한 네거티브 컨트롤 실패 — 효과를 2.5배로 키웠는데도 상한 안이었습니다(" +
                    (loud.Changed * 100f).ToString("F2") + "%). 상한이 너무 높아 화면을 덮는 효과를 통과시킵니다.");
        }

        static PlayVfxMeasure MeasureVfxInPlace(PlayVfxSpot spot, ActionVfx.Kind kind, float scale)
        {
            var template = ActionVfx.Template(kind);
            if (template == null)
                throw new Exception("VFX 템플릿이 없습니다: " + ActionVfx.ObjectFor(kind));

            var qv = UnityEngine.Object.FindFirstObjectByType<QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 eye = spot.Player - rot * Vector3.forward * spot.Distance;

            var camGo = new GameObject("VfxPlayFrameCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            camGo.transform.position = eye;
            camGo.transform.LookAt(spot.Player);

            var rt = new RenderTexture(PlayVfxW, PlayVfxH, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(PlayVfxW, PlayVfxH, TextureFormat.RGB24, false);
            GameObject effect = null;
            var faded = new System.Collections.Generic.List<Renderer>();
            try
            {
                cam.targetTexture = rt;
                // 런타임과 같은 차폐 페이드 — 이걸 빼면 벽에 가려 안 보이는 것을 「보인다」고 잰다.
                DungeonSightFade.Hide(eye, spot.Player, DungeonSightFade.DefaultRadius, faded);

                Color[] before = Shoot(cam, rt, tex);

                effect = UnityEngine.Object.Instantiate(template.gameObject,
                    spot.Player + Vector3.up * 1.0f, template.transform.rotation);
                effect.SetActive(true);
                effect.transform.localScale = template.transform.localScale * scale;
                var ps = effect.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    if (scale != 1f)
                    {
                        var main = ps.main;
                        main.startSizeMultiplier *= scale;   // 파티클 크기는 트랜스폼 스케일만으로 안 줄어든다
                    }
                    ps.Simulate(0.30f, true, true);
                }

                Color[] after = Shoot(cam, rt, tex);
                return Diff(before, after);
            }
            finally
            {
                if (effect != null) UnityEngine.Object.DestroyImmediate(effect);
                DungeonSightFade.Restore(faded);
                cam.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static Color[] Shoot(Camera cam, RenderTexture rt, Texture2D tex)
        {
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, PlayVfxW, PlayVfxH), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            return tex.GetPixels();
        }

        static PlayVfxMeasure Diff(Color[] a, Color[] b)
        {
            int changed = 0;
            float sum = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                float d = Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                if (d < PlayVfxPixelDelta)
                    continue;
                changed++;
                sum += d;
            }
            return new PlayVfxMeasure
            {
                Changed = (float)changed / a.Length,
                Contrast = changed == 0 ? 0f : sum / changed,
            };
        }

    }
}
