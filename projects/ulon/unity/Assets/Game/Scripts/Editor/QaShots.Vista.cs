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
