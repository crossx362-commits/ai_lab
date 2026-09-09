using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class QaShots
    {
        /// <summary>
        /// **`15_lake_river`의 자리는 대상에서 유도한다**(검수 2026-09-10: 「화면에 호수도 강도 없다」).
        ///
        /// 손으로 박은 눈·타깃이 이름이 약속한 둘을 다 놓쳤다. 이 장이 「못 잼」으로 자에서 빠져 있어
        /// 오래 조용했다 — **재지 못하는 것과 아무것도 안 담긴 것은 자 앞에서 똑같이 조용하다**(원장).
        ///
        /// 그래서 `06`에서 쓴 방식을 그대로 쓴다: **대상 점을 만들고, 후보 자리를 재서 가장 많이
        /// 담기는 자리를 고른다.** 대상은 호수 둘레와 강 중심선이고, 판정은 화각 + **가림**이다
        /// (프레임 안이라도 언덕 뒤면 화면에 없다 — 강 셈에서 값을 치른 교훈).
        /// 고른 자리는 로그로 남긴다: 세계가 바뀌면 자리도 바뀌므로 상수로 박지 않는다.
        /// </summary>
        static Shot LakeRiverShot(string name)
        {
            float sea = WorldTerrain.SeaLevel;
            var targets = new System.Collections.Generic.List<Vector3>();
            // 호수는 **파인 그릇**이라 먼 쪽 물가는 제 둔덕에 가린다 — 0.8R로 잡았더니 8점 중 1점만
            // 보였다(첫 판). 수면 한가운데와 0.5R을 대상으로 삼는다: 「호수가 화면에 있나」는
            // 물이 보이는가이지 물가 여덟 곳이 다 보이는가가 아니다.
            targets.Add(new Vector3(WorldTerrain.LakeX, sea, WorldTerrain.LakeZ));
            for (float a = 0f; a < 360f; a += 60f)
                targets.Add(new Vector3(
                    WorldTerrain.LakeX + Mathf.Cos(a * Mathf.Deg2Rad) * WorldTerrain.LakeRadius * 0.5f, sea,
                    WorldTerrain.LakeZ + Mathf.Sin(a * Mathf.Deg2Rad) * WorldTerrain.LakeRadius * 0.5f));
            int lakeCount = targets.Count;
            for (float x = WorldTerrain.RiverFromX; x >= WorldTerrain.RiverToX; x -= 10f)   // 강 중심선
                targets.Add(new Vector3(x, sea,
                    WorldTerrain.RiverZ + Mathf.Sin((x - WorldTerrain.RiverFromX) * 0.06f) * 6f));

            // 대상 무리의 중심을 본다 — 호수와 강은 한 줄로 늘어서 있으므로 그 줄의 가운데다.
            var center = Vector3.zero;
            foreach (var t in targets) center += t;
            center /= targets.Count;

            float bestScore = -1f;
            Vector3 bestEye = new Vector3(WorldTerrain.LakeX + 46f, sea + 30f, WorldTerrain.LakeZ + 46f);
            string bestWhere = "(후보 없음)";
            foreach (float yaw in new[] { 0f, 30f, 60f, 90f, 120f, 150f, 180f, 210f, 240f, 270f, 300f, 330f })
            foreach (float dist in new[] { 60f, 80f, 100f, 130f })
            foreach (float high in new[] { 30f, 45f, 60f, 80f, 100f })
            {
                var eye = center + new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad) * dist, high,
                                               Mathf.Cos(yaw * Mathf.Deg2Rad) * dist);
                int lake = 0, river = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (!PointVisible(eye, center, targets[i])) continue;
                    if (i < lakeCount) lake++; else river++;
                }
                // **둘 다 담겨야 한다** — 한쪽만 많이 담긴 자리를 고르면 이름이 다시 절반만 지켜진다.
                // 그래서 점수는 합이 아니라 **적은 쪽**을 크게 본다.
                float score = Mathf.Min(lake / (float)lakeCount, river / (float)(targets.Count - lakeCount)) * 100f
                              + (lake + river) * 0.1f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestEye = eye;
                    bestWhere = "요 " + yaw.ToString("0") + "° · 거리 " + dist.ToString("0") + "m · 높이 " +
                                high.ToString("0") + "m → 호수 " + lake + "/" + lakeCount + " · 강 " + river + "/" +
                                (targets.Count - lakeCount);
                }
            }
            Debug.Log("[샷] 15 자리 유도 — " + bestWhere);
            return new Shot { Name = name, Eye = bestEye, Target = center };
        }


        /// <summary>
        /// **강 근접 샷의 자리도 강에서 유도한다**(검수 승인 ⓒ, 2026-09-10).
        ///
        /// 강을 무는 자가 하나도 없었고 **근접 샷도 하나도 없었다** — 같은 뿌리다: 조망에만 실려
        /// 있으면 「강이 강으로 읽히나」를 눈이 판정할 자리가 없다. `15`와 같은 방식이되 묻는 것이 다르다.
        /// 조망은 「둘 다 담겼나」였고 여기는 **「굽이가 굽이로 보이나」**다 — 곧은 도랑은 수로다.
        /// 점수는 셋을 곱한다: 중심선이 보이는 비율 × **화면 가로 퍼짐** × **양안이 보이는 비율**.
        ///
        /// **양안이 결정적이다.** 첫 판은 중심선과 퍼짐만 봤는데 「7/7 · 퍼짐 0.84」짜리 자리를
        /// 골라 놓고 화면은 **물만 가득**했다 — 카메라가 강을 **따라** 서서 물가가 프레임 밖이었고,
        /// 그러면 강인지 바다인지 화면으로 가릴 수 없다. **물은 물가가 있어야 강이 된다.**
        ///
        /// 양안을 「보이나」로만 물은 둘째 판도 죽은 장이었다(「양안 7/7·7/7」인데 화면은 여전히
        /// 물 천지). **보이는 것과 크게 보이는 것은 다르다** — 물가가 화면 구석에 1px로 걸려도
        /// 「보임」이다. 그래서 **양안이 화면에서 벌어진 폭**을 인자로 넣었다.
        ///
        /// 셋째 판도 반려였다(검수: 「강으로 안 읽힌다 — 굽이가 굽이로 안 보이고 하늘도 없다」).
        /// **크기 인자를 최댓값으로 좇으면 카메라가 코앞까지 온다** — 12m에서 물가 접사가 됐다.
        /// 원장 문장(`00c165e0`)대로 셋을 고쳤다:
        ///   ① 화면 강폭은 **목표 구간**(0.25~0.45)에서 최대고 그 밖은 감점 — 상한 없는 크기는 코앞을 부른다.
        ///   ② **굽이 인자 신설** — 중심선을 화면에 투영해 **방향이 꺾인 각도의 합**을 잰다.
        ///      이 샷의 질문이 「굽이가 굽이로 보이나」인데 **묻는 인자가 하나도 없었다**(곧은 자락도 만점).
        ///   ③ **하늘이 프레임에 들 것** — 근접이라도 물만 가득하면 규모를 못 읽는다.
        ///
        /// 대상 구간은 **굽이 정점 언저리**다. 중심선이 `sin((x−RiverFromX)·0.06)`이므로 극값은
        /// 위상 −π/2, 곧 x ≈ −106이다(원장에서 유도한다 — 상수로 박으면 원장이 바뀔 때 어긋난다).
        /// </summary>
        static Shot RiverBendShot(string name)
        {
            float sea = WorldTerrain.SeaLevel;
            // 굽이 정점 — 위상 −π/2가 되는 x. 그 둘레 ±15m를 대상으로 잡는다.
            float peakX = WorldTerrain.RiverFromX - (Mathf.PI * 0.5f) / 0.06f;
            var targets = new System.Collections.Generic.List<Vector3>();
            for (float x = peakX + 15f; x >= peakX - 15f; x -= 5f)
            {
                float cx = Mathf.Clamp(x, WorldTerrain.RiverToX, WorldTerrain.RiverFromX);
                targets.Add(new Vector3(cx, sea,
                    WorldTerrain.RiverZ + Mathf.Sin((cx - WorldTerrain.RiverFromX) * 0.06f) * 6f));
            }
            // 양안 — 중심선 각 점에서 좌우로 나가 물이 끝나는 자리.
            var left = new System.Collections.Generic.List<Vector3>();
            var right = new System.Collections.Generic.List<Vector3>();
            foreach (var t in targets)
                for (int side = -1; side <= 1; side += 2)
                    for (float d = 1f; d <= 30f; d += 0.5f)
                        if (WorldTerrain.HeightAt(t.x, t.z + side * d) >= sea)
                        {
                            var p = new Vector3(t.x, sea, t.z + side * d);
                            if (side < 0) left.Add(p); else right.Add(p);
                            break;
                        }

            var center = Vector3.zero;
            foreach (var t in targets) center += t;
            center /= targets.Count;

            float bestScore = -1f;
            Vector3 bestEye = center + new Vector3(0f, 12f, 30f);
            string bestWhere = "(후보 없음)";
            foreach (float yaw in new[] { 0f, 30f, 60f, 90f, 120f, 150f, 180f, 210f, 240f, 270f, 300f, 330f })
            foreach (float dist in new[] { 12f, 18f, 25f, 35f })
            foreach (float high in new[] { 10f, 16f, 24f, 32f })
            {
                var eye = center + new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad) * dist, high,
                                               Mathf.Cos(yaw * Mathf.Deg2Rad) * dist);
                int seen = 0;
                float uMin = 1f, uMax = 0f;
                foreach (var t in targets)
                {
                    if (!PointVisible(eye, center, t)) continue;
                    seen++;
                    float u = ScreenU(eye, center, t);
                    if (u < uMin) uMin = u;
                    if (u > uMax) uMax = u;
                }
                if (seen < 2) continue;
                float spread = Mathf.Clamp01(uMax - uMin);
                int lSeen = 0, rSeen = 0;
                foreach (var p in left) if (PointVisible(eye, center, p)) lSeen++;
                foreach (var p in right) if (PointVisible(eye, center, p)) rSeen++;
                float banks = Mathf.Min(lSeen / Mathf.Max(1f, left.Count), rSeen / Mathf.Max(1f, right.Count));
                // 양안이 화면에서 얼마나 벌어지나 — 「보임」이 아니라 「읽힘」을 재는 인자다.
                float widthOnScreen = 0f;
                int pairs = Mathf.Min(left.Count, right.Count);
                for (int i = 0; i < pairs; i++)
                    widthOnScreen += Mathf.Abs(ScreenU(eye, center, right[i]) - ScreenU(eye, center, left[i]));
                widthOnScreen = pairs > 0 ? widthOnScreen / pairs : 0f;
                // ① 목표 구간 — 0.25~0.45에서 1, 밖으로 나갈수록 깎는다.
                float widthScore = widthOnScreen < 0.25f
                    ? widthOnScreen / 0.25f
                    : (widthOnScreen <= 0.45f ? 1f : Mathf.Max(0f, 1f - (widthOnScreen - 0.45f) / 0.35f));
                // ② 굽이 — 화면에 투영한 중심선이 얼마나 꺾이나(각도 합, 라디안).
                float bend = ScreenBend(eye, center, targets);
                float bendScore = Mathf.Clamp01(bend / 1.0f);
                // ③ 하늘 — 카메라가 지평선 위를 조금이라도 담나(피치가 너무 가파르면 물만 남는다).
                float pitch = Mathf.Asin(Mathf.Clamp01((eye.y - center.y) / Mathf.Max(0.001f, (center - eye).magnitude)));
                bool skyInFrame = pitch < (55f * 0.5f * Mathf.Deg2Rad);
                if (!skyInFrame) continue;
                float score = seen / (float)targets.Count * spread * banks * widthScore * bendScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestEye = eye;
                    bestWhere = "요 " + yaw.ToString("0") + "° · 거리 " + dist.ToString("0") + "m · 높이 " +
                                high.ToString("0") + "m → 중심선 " + seen + "/" + targets.Count +
                                " · 가로 퍼짐 " + spread.ToString("0.00") + " · 양안 " + lSeen + "/" + left.Count +
                                "·" + rSeen + "/" + right.Count + " · 화면 강폭 " + widthOnScreen.ToString("0.00") +
                                " · 굽이 " + bend.ToString("0.00") + "rad · 피치 " +
                                (pitch * Mathf.Rad2Deg).ToString("0") + "°";
                }
            }
            Debug.Log("[샷] 강 굽이 자리 유도 — 정점 x=" + peakX.ToString("0") + " · " + bestWhere);
            return new Shot { Name = name, Eye = bestEye, Target = center };
        }

        /// <summary>
        /// 화면에 투영한 중심선이 **얼마나 꺾이나** — 이웃한 세 점이 이루는 방향 변화의 합(라디안).
        /// 곧은 자락은 0에 가깝다. 「굽이가 굽이로 보이나」를 묻는 유일한 인자다.
        /// </summary>
        static float ScreenBend(Vector3 eye, Vector3 look, System.Collections.Generic.List<Vector3> pts)
        {
            var uv = new System.Collections.Generic.List<Vector2>();
            foreach (var p in pts)
            {
                if (!PointVisible(eye, look, p)) continue;
                uv.Add(new Vector2(ScreenU(eye, look, p), ScreenV(eye, look, p)));
            }
            float sum = 0f;
            for (int i = 2; i < uv.Count; i++)
            {
                var a = uv[i - 1] - uv[i - 2];
                var b = uv[i] - uv[i - 1];
                if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) continue;
                sum += Mathf.Abs(Vector2.SignedAngle(a, b)) * Mathf.Deg2Rad;
            }
            return sum;
        }

        /// <summary>이 점이 화면 세로 어디에 찍히나(0 위 ~ 1 아래).</summary>
        static float ScreenV(Vector3 eye, Vector3 look, Vector3 p)
        {
            var fwd = (look - eye).normalized;
            var v = p - eye;
            float depth = Vector3.Dot(v, fwd);
            if (depth < 0.5f) return 0.5f;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up = Vector3.Cross(fwd, right);
            float tanY = Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad);
            return Mathf.Clamp01(0.5f - Vector3.Dot(v, up) / (depth * tanY) * 0.5f);
        }

        /// <summary>이 점이 화면 가로 어디에 찍히나(0 왼쪽 ~ 1 오른쪽) — 굽이가 퍼졌는지 보려고 쓴다.</summary>
        static float ScreenU(Vector3 eye, Vector3 look, Vector3 p)
        {
            var fwd = (look - eye).normalized;
            var v = p - eye;
            float depth = Vector3.Dot(v, fwd);
            if (depth < 0.5f) return 0.5f;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            float tanX = Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad) * 16f / 9f;
            return Mathf.Clamp01(0.5f + Vector3.Dot(v, right) / (depth * tanX) * 0.5f);
        }

        /// <summary>화각(55°, 16:9) 안이고 지형에 안 막히는가 — 「프레임 안 ≠ 화면에 보임」.</summary>
        static bool PointVisible(Vector3 eye, Vector3 look, Vector3 p)
        {
            var fwd = (look - eye).normalized;
            var v = p - eye;
            float depth = Vector3.Dot(v, fwd);
            if (depth < 0.5f) return false;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var up = Vector3.Cross(fwd, right);
            float tanY = Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad);
            if (Mathf.Abs(Vector3.Dot(v, right)) > depth * tanY * 16f / 9f) return false;
            if (Mathf.Abs(Vector3.Dot(v, up)) > depth * tanY) return false;
            float dist = v.magnitude;
            return !(Physics.Raycast(eye, v.normalized, out RaycastHit hit, dist - 0.6f) && hit.distance < dist - 0.6f);
        }
    }
}
