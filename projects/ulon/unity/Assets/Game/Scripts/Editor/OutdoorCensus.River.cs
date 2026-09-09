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
        /// **실측(2026-09-10, 호수 봉합 뒤)**: ①끊김 0 ②젖은 폭은 아래 로그가 찍는다(**호수 구간과
        /// 하구는 뺀다** — 남의 물이다) ③모래 띠 강·호수·바다 나란히 ④조망 다섯에 들고 근접 샷이 없다.
        ///
        /// **③은 첫 판에 「강변 모래 0%」라고 틀리게 보고했다 — 세계가 아니라 자가 틀렸다.**
        /// 물가 모래는 지역 도포(`CoverAt`)가 아니라 **다른 채널**(`ShoreSandAt`·`ShoreScreeAt`)인데
        /// `CoverAt`의 1등 층을 물었고, 그것도 물 끝 3m **한 점**만 봤다(띠가 4m면 3m는 이미 끝자락이다).
        /// 교훈은 원장에 이미 있던 것이다 — **판별이 「아니오」라고 하면 자를 먼저 의심하라.**
        /// 여기서는 「0%」라는 **깨끗한 극단값**이 신호였다: 세계는 좀처럼 0을 주지 않는다.
        /// </summary>
        public static void RunRiverRead()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            float sea = WorldTerrain.SeaLevel;
            int samples = 0, dryGaps = 0, mouthSkipped = 0;
            float wMin = 999f, wMax = 0f, wSum = 0f;
            float prevCz = float.NaN, bendSum = 0f;
            string widths = "";
            for (float x = WorldTerrain.RiverFromX; x >= WorldTerrain.RiverToX; x -= 5f)
            {
                float cz = WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f;
                if (!float.IsNaN(prevCz)) bendSum += Mathf.Abs(cz - prevCz);
                prevCz = cz;

                // ② 젖은 폭 — 중심선 양옆으로 훑어 수면 아래 구간의 길이.
                // **양 끝을 뺀다**: 해안 언저리는 강이 아니라 **바다**고(35m), 강이 시작하는
                // `RiverFromX`는 아직 **호수 안**이라 호수 폭을 잰다(x=−80에서 36.5m가 나왔다).
                // 남의 물을 섞으면 평균이 강을 과장한다 — 봉합 전후를 같은 자로 비교하려면 더욱 그렇다.
                if (new Vector2(x, cz).magnitude > WorldTerrain.CoastEnd - 12f ||
                    new Vector2(x - WorldTerrain.LakeX, cz - WorldTerrain.LakeZ).magnitude
                        < WorldTerrain.LakeRadius + 2f)
                {
                    mouthSkipped++;
                    continue;
                }
                float wet = 0f;
                for (float dz = -WorldTerrain.RiverHalfWidth * 3f; dz <= WorldTerrain.RiverHalfWidth * 3f; dz += 0.5f)
                    if (WorldTerrain.HeightAt(x, cz + dz) < sea) wet += 0.5f;
                samples++;
                if (wet < 1f) dryGaps++;
                wSum += wet;
                if (wet < wMin) wMin = wet;
                if (wet > wMax) wMax = wet;
                if (samples % 3 == 1) widths += " " + x.ToString("0") + ":" + wet.ToString("0.0");
            }
            Debug.Log("[강] ① 이어짐 — 표본 " + samples + "개 중 물 없는 자리 " + dryGaps +
                      "개 · 호수·하구라 뺀 표본 " + mouthSkipped + "곳");
            // 「뺀 수를 찍고 0이면 죽은 예외로 실패」 — 예외가 조용히 죽으면 자가 딴 세계를 잰다.
            if (mouthSkipped == 0)
                throw new System.InvalidOperationException("호수·하구로 뺀 표본이 0곳입니다 — 예외가 죽었습니다.");
            Debug.Log("[강] ② 폭 — 평균 " + (wSum / Mathf.Max(1, samples)).ToString("0.0") + "m · 최소 " +
                      wMin.ToString("0.0") + " · 최대 " + wMax.ToString("0.0") + "m (원장 반폭 " +
                      WorldTerrain.RiverHalfWidth + "m) · 굽이 총 " + bendSum.ToString("0.0") + "m ·" + widths);
            // ③ 물가 — **셋을 같은 자로 나란히 잰다.** 「강이 0%」만으로는 결함인지 알 수 없다:
            // 다른 물가도 0이면 세계 전체의 규칙이고, 다른 물가만 모래면 **강만 빠진 것**이다.
            Debug.Log("[강] ③ 물가(모래 채널) — 강 " + BankSand(RiverBank()));
            Debug.Log("[강] ③-나 견줌 — 호수 " + BankSand(LakeBank()) + " · 바다 " + BankSand(SeaBank()));

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

        /// <summary>
        /// 물 끝에서 **뭍 쪽으로 훑으며** 모래·자갈을 잰다 — 첫 판(물 끝 3m 한 점의 1등 층)은 자가 틀렸다.
        /// 띠가 2~4m면 3m는 이미 띠 밖이고, `CoverAt`은 **최상위 층 하나**만 돌려주므로 모래 40%·잔디 60%도
        /// 「잔디」로 찍힌다. **판별이 「아니오」라고 하면 자부터 의심한다**(원장). 그래서 두 가지를 찍는다:
        /// **가중치 최대**(1등이 아니어도 얼마나 섞였나)와 **1등인 구간의 폭**.
        /// </summary>
        static string BankSand(System.Collections.Generic.List<(Vector2 water, Vector2 inland)> pts)
        {
            float peakSum = 0f, widthSum = 0f, peakMax = 0f;
            foreach (var p in pts)
            {
                var dir = (p.inland - p.water).normalized;
                float peak = 0f, width = 0f;
                for (float d = 0.5f; d <= 8f; d += 0.5f)
                {
                    var q = p.water + dir * d;
                    // **물가 모래는 `CoverAt`에 없다** — 지역 도포와 다른 채널(`ShoreSandAt`·`ShoreScreeAt`)이다.
                    // 첫 판이 `CoverAt`으로 「0%」를 찍은 것은 세계가 아니라 **자가 틀린 것**이었다.
                    float sandW = WorldSplat.ShoreSandAt(q.x, q.y) + WorldSplat.ShoreScreeAt(q.x, q.y);
                    if (sandW > peak) peak = sandW;
                    if (sandW >= 0.5f) width += 0.5f;      // 반 넘게 모래면 화면에서 모래로 읽힌다
                }
                peakSum += peak;
                widthSum += width;
                if (peak > peakMax) peakMax = peak;
            }
            int n = Mathf.Max(1, pts.Count);
            return pts.Count + "곳 · 모래·자갈 가중치 평균 최대 " + (peakSum / n).ToString("0.00") +
                   "(가장 센 곳 " + peakMax.ToString("0.00") + ") · 1등인 구간 평균 폭 " +
                   (widthSum / n).ToString("0.0") + "m";
        }

        /// <summary>호수 물가 — 열두 방위로 나가 물이 끝나는 자리와 그 바깥 방향.</summary>
        static System.Collections.Generic.List<(Vector2, Vector2)> LakeBank()
        {
            float sea = WorldTerrain.SeaLevel;
            var pts = new System.Collections.Generic.List<(Vector2, Vector2)>();
            for (float a = 0f; a < 360f; a += 30f)
            {
                float dx = Mathf.Cos(a * Mathf.Deg2Rad), dz = Mathf.Sin(a * Mathf.Deg2Rad);
                for (float d = 1f; d <= 60f; d += 0.5f)
                    if (WorldTerrain.HeightAt(WorldTerrain.LakeX + dx * d, WorldTerrain.LakeZ + dz * d) >= sea)
                    {
                        pts.Add((new Vector2(WorldTerrain.LakeX + dx * d, WorldTerrain.LakeZ + dz * d),
                                 new Vector2(WorldTerrain.LakeX + dx * (d + 1f), WorldTerrain.LakeZ + dz * (d + 1f))));
                        break;
                    }
            }
            return pts;
        }

        /// <summary>바다 물가 — 원점에서 스물네 방위로 나가 물이 시작하는 자리와 뭍 쪽 방향.</summary>
        static System.Collections.Generic.List<(Vector2, Vector2)> SeaBank()
        {
            float sea = WorldTerrain.SeaLevel;
            var pts = new System.Collections.Generic.List<(Vector2, Vector2)>();
            for (float a = 0f; a < 360f; a += 15f)
            {
                float dx = Mathf.Cos(a * Mathf.Deg2Rad), dz = Mathf.Sin(a * Mathf.Deg2Rad);
                for (float d = 60f; d <= 150f; d += 0.5f)
                    if (WorldTerrain.HeightAt(dx * d, dz * d) < sea)
                    {
                        pts.Add((new Vector2(dx * d, dz * d), new Vector2(dx * (d - 1f), dz * (d - 1f))));
                        break;
                    }
            }
            return pts;
        }

        /// <summary>강 물가 — 중심선을 따라 양옆으로, 물이 끝나는 자리와 바깥 방향.</summary>
        static System.Collections.Generic.List<(Vector2, Vector2)> RiverBank()
        {
            float sea = WorldTerrain.SeaLevel;
            var pts = new System.Collections.Generic.List<(Vector2, Vector2)>();
            for (float x = WorldTerrain.RiverFromX; x >= WorldTerrain.RiverToX; x -= 5f)
            {
                float cz = WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f;
                if (WorldTerrain.HeightAt(x, cz) >= sea) continue;      // 마른 자리엔 물가가 없다
                for (int side = -1; side <= 1; side += 2)
                    for (float d = 0.5f; d <= 40f; d += 0.5f)
                        if (WorldTerrain.HeightAt(x, cz + side * d) >= sea)
                        {
                            pts.Add((new Vector2(x, cz + side * d), new Vector2(x, cz + side * (d + 1f))));
                            break;
                        }
            }
            return pts;
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
