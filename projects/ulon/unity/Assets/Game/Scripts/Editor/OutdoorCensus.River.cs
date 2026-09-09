using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **강이 화면에서 강으로 읽히나 — 먼저 무엇으로 잴지 정하고 센다**(검수 지시 2026-09-09).
        ///
        /// 조사해 보니 호수·바다는 자가 여럿 보는데(`AssertShoreBand`·`AssertRidgeAndShore`)
        /// **강을 무는 자는 하나도 없다** — 셈 `RunRiverMouth`가 이어짐만 셀 뿐이다. 축을 셋으로 잡는다:
        ///   ① **이어짐** — 중심선을 따라 수면 아래가 끊기지 않는가(기존 셈이 보는 축).
        ///   ② **폭과 굽이** — 젖은 폭이 얼마이고 얼마나 고른가, 중심선이 굽는가(곧은 도랑은 수로다).
        ///   ③ **물가** — 강변에 모래·자갈 전이대가 있는가(호수·바다에는 있는 그것이 강에도 있나).
        /// 그리고 ④ **어느 샷에 실제로 보이나** — 화각 안이기만 하면 안 되고 **가림도 본다**
        /// (첫 판은 가림을 안 봐서 `15`를 「13점」이라 했는데 그 화면에 강은 없었다 — 언덕 뒤였다).
        ///
        /// **이 자가 못 보는 것**: ①물 재질·반짝임은 안 본다(지형 높이와 도포만 읽는다)
        /// ②④는 가림을 걷어도 여전히 **「보일 자리인가」이지 「화면에서 강으로 읽히나」가 아니다** —
        /// 멀면 몇 픽셀짜리 실개천이어도 점은 센다. 실제로 `15`는 가림을 본 뒤에도 10점인데
        /// 그 화면에서 눈에 들어오는 물은 호수·바다다. **화면 판정은 여전히 눈이 한다.**
        ///
        /// **실측(2026-09-09)**: ①끊김 0 ②젖은 폭 평균 22.2m(최소 15.5·최대 36.5, 원장 반폭 6m이니
        /// 12m가 설계값이다 — 하구 쪽 35m는 바다와 합쳐진 것) ③**강변 모래·자갈 0%**(26곳 전부 풀)
        /// ④조망 넷에만 들고 근접 샷이 하나도 없다. 호수·바다에는 전이대 자가 있는데 강에는 없다.
        /// </summary>
        public static void RunRiverRead()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            float sea = WorldTerrain.SeaLevel;
            int samples = 0, dryGaps = 0;
            float wMin = 999f, wMax = 0f, wSum = 0f;
            float prevCz = float.NaN, bendSum = 0f;
            float sandSum = 0f, grassSum = 0f;
            int bankSamples = 0;
            string widths = "";
            for (float x = WorldTerrain.RiverFromX; x >= WorldTerrain.RiverToX; x -= 5f)
            {
                float cz = WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f;
                if (!float.IsNaN(prevCz)) bendSum += Mathf.Abs(cz - prevCz);
                prevCz = cz;

                // ② 젖은 폭 — 중심선 양옆으로 훑어 수면 아래 구간의 길이.
                float wet = 0f;
                for (float dz = -WorldTerrain.RiverHalfWidth * 3f; dz <= WorldTerrain.RiverHalfWidth * 3f; dz += 0.5f)
                    if (WorldTerrain.HeightAt(x, cz + dz) < sea) wet += 0.5f;
                samples++;
                if (wet < 1f) dryGaps++;
                wSum += wet;
                if (wet < wMin) wMin = wet;
                if (wet > wMax) wMax = wet;
                if (samples % 3 == 1) widths += " " + x.ToString("0") + ":" + wet.ToString("0.0");

                // ③ 물가 — 물 끝에서 3m 바깥의 지표가 모래·자갈인가 풀인가.
                for (int side = -1; side <= 1; side += 2)
                {
                    float bank = cz + side * (wet * 0.5f + 3f);
                    int layer = WorldSplat.CoverAt(x, bank, out float w);
                    bool sandy = layer == WorldSplat.Sand || layer == WorldSplat.Gravel;
                    bool grassy = layer < 0 || layer == WorldSplat.Grass || layer == WorldSplat.DryGrass;
                    if (w >= 0.3f && sandy) sandSum++;
                    else if (grassy || w < 0.3f) grassSum++;
                    bankSamples++;
                }
            }
            Debug.Log("[강] ① 이어짐 — 표본 " + samples + "개 중 물 없는 자리 " + dryGaps + "개");
            Debug.Log("[강] ② 폭 — 평균 " + (wSum / Mathf.Max(1, samples)).ToString("0.0") + "m · 최소 " +
                      wMin.ToString("0.0") + " · 최대 " + wMax.ToString("0.0") + "m (원장 반폭 " +
                      WorldTerrain.RiverHalfWidth + "m) · 굽이 총 " + bendSum.ToString("0.0") + "m ·" + widths);
            Debug.Log("[강] ③ 물가 — 표본 " + bankSamples + "곳 중 모래·자갈 " + sandSum + "(" +
                      (100f * sandSum / Mathf.Max(1, bankSamples)).ToString("0") + "%) · 풀·맨땅 " + grassSum);

            // ④ 어느 샷에 보이나 — 화각 안 + 가림 없음. 「읽히나」까지는 못 묻는다(위 머리말 참조).
            var shots = QaShots.BuildShots();
            string frames = "";
            for (int i = 0; i < shots.Length; i++)
            {
                QaShots.EyeOf(shots[i], out Vector3 eye, out Vector3 look);
                int inFrame = 0;
                for (float x = WorldTerrain.RiverFromX; x >= WorldTerrain.RiverToX; x -= 5f)
                {
                    float cz = WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f;
                    var pt = new Vector3(x, sea, cz);
                    if (!InFrame(eye, look, pt)) continue;
                    // **프레임 안 ≠ 화면에 보임** — 첫 판에 가림을 안 봐서 `15`가 「13점」이었는데
                    // 실제 화면에는 강이 없었다(언덕 뒤였다). 눈에서 광선을 쏴 지형에 먼저 막히면 뺀다.
                    var dir = pt - eye;
                    float dist = dir.magnitude;
                    if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, dist - 0.6f) &&
                        hit.distance < dist - 0.6f)
                        continue;
                    inFrame++;
                }
                if (inFrame > 0)
                    frames += " · " + QaShots.NameOf(shots[i]) + " " + inFrame + "점";
            }
            Debug.Log("[강] ④ 담기는 샷 —" + (frames.Length > 0 ? frames : " 없음(어느 화면에도 안 들어온다)"));
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        /// <summary>이 점이 그 카메라의 화각(55°, 16:9) 안인가 — 가림은 안 본다(들어올 자리인지만).</summary>
        static bool InFrame(Vector3 eye, Vector3 look, Vector3 p)
        {
            var fwd = (look - eye).normalized;
            var v = p - eye;
            float depth = Vector3.Dot(v, fwd);
            if (depth < 0.5f) return false;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up = Vector3.Cross(fwd, right);
            float tanY = Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * 16f / 9f;
            return Mathf.Abs(Vector3.Dot(v, right)) <= depth * tanX &&
                   Mathf.Abs(Vector3.Dot(v, up)) <= depth * tanY;
        }
    }
}
