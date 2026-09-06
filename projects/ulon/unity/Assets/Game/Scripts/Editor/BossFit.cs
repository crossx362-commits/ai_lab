using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 보스 장식(왕관·무기)이 **화면에서 몸에 붙어 보이는가**를 재는 기하 계산. 빌더와 게이트가 같은 함수를 쓴다
    /// (판정이 두 곳에 살면 갈라진다 — 하네스 수칙).
    ///
    /// 왜 렌더러 bounds를 안 쓰나:
    ///  - 스킨드 메시의 `Renderer.bounds`는 에디터 포즈에서 부풀어 있어 정수리가 실제보다 높게 나온다
    ///    (왕관이 머리 위 한 뼘에 뜬 원인, 검수 2026-09-06 반려 1).
    ///  - 긴 칼의 AABB는 몸을 통째로 삼켜서 「손이 무기 bounds 안」 같은 판정을 늘 통과시킨다(반려 2).
    /// 그래서 스킨드 메시를 **베이크해 실제 정점**을 보고, 무기는 **장축과 그립 끝점**을 뽑는다.
    /// </summary>
    public static class BossFit
    {
        /// <summary>정수리 슬랩 두께(m) — 이 두께 안의 정점으로 머리 폭을 잰다.</summary>
        public const float HeadSlab = 0.22f;

        static bool IsDecor(Transform t, GameObject actor)
        {
            for (var p = t; p != null && p.gameObject != actor; p = p.parent)
            {
                string n = p.name;
                if (n.StartsWith("BossCrown", System.StringComparison.Ordinal) ||
                    n.StartsWith("BossAura", System.StringComparison.Ordinal) ||
                    n.StartsWith(VisualSliceBuilder.BossWeaponPrefix, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>몸(장식 제외)의 실제 정점을 월드 좌표로 모은다.</summary>
        public static bool BodyPoints(GameObject actor, List<Vector3> outPoints)
        {
            outPoints.Clear();
            var skinned = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var baked = new Mesh();
            for (int i = 0; i < skinned.Length; i++)
            {
                if (!skinned[i].enabled || !skinned[i].gameObject.activeInHierarchy || IsDecor(skinned[i].transform, actor))
                    continue;
                skinned[i].BakeMesh(baked, false);   // 스케일은 localToWorldMatrix가 적용한다 — true면 두 번 곱해져 머리가 몸통만큼 커진다
                var verts = baked.vertices;
                var m = skinned[i].transform.localToWorldMatrix;
                for (int v = 0; v < verts.Length; v++)
                    outPoints.Add(m.MultiplyPoint3x4(verts[v]));
            }
            Object.DestroyImmediate(baked);

            var filters = actor.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                var rend = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || rend == null || !rend.enabled || !mf.gameObject.activeInHierarchy || IsDecor(mf.transform, actor))
                    continue;
                var verts = mf.sharedMesh.vertices;
                var m = mf.transform.localToWorldMatrix;
                for (int v = 0; v < verts.Length; v++)
                    outPoints.Add(m.MultiplyPoint3x4(verts[v]));
            }
            return outPoints.Count > 0;
        }

        /// <summary>
        /// 정수리 높이·머리 폭·머리 중심축(수평). 왕관은 이 값 위에 얹고, 게이트는 같은 값으로 잰다.
        /// </summary>
        public static bool HeadMetrics(GameObject actor, out float topY, out float width, out Vector3 center)
        {
            topY = 0f;
            width = 0f;
            center = Vector3.zero;
            var pts = new List<Vector3>();
            if (!BodyPoints(actor, pts))
                return false;

            // 「가장 높은 정점 = 정수리」가 아니다 — 본워든의 뿔투구는 두개골보다 0.50m 위, 좌우로 1.14m 벌어져 있다.
            // 그 위에 얹으면 관이 공중에 뜨고 크기도 뿔에 맞춰 몸보다 커진다(검수 2026-09-06 반려 1의 진짜 원인).
            // 그래서 **머리 본 주변 기둥**만 머리로 본다 — 뿔·망토·어깨는 반경 밖이다.
            var headBone = FindBone(actor, "head");
            float bodyH = 2f;
            var ccH = actor.GetComponent<CharacterController>();
            if (ccH != null && ccH.height > 0.01f)
                bodyH = ccH.height;
            float floorY = headBone != null ? headBone.position.y - 0.05f : float.MinValue;
            // 반경은 **머리 본에 스킨된 정점**(두개골·머리카락)에서 잰다. 몸 높이 비율이나 목 위 띠로 잡으면
            // 어깨·망토가 섞여 반경이 커지고, 그 틈으로 뿔 끝이 다시 들어와 머리 폭이 두 배로 나왔다(실측 0.86 vs 실제 0.50).
            float rMax = float.MaxValue;
            Vector3 skullC = headBone != null ? headBone.position : Vector3.zero;
            if (headBone != null)
            {
                var skull = new List<Vector3>();
                HeadSkinPoints(actor, headBone, skull);
                if (skull.Count > 0)
                {
                    // 최대 폭이 아니라 **80퍼센타일 반경**을 쓴다 — 뿔·귀·깃털은 정점 수가 적어 꼬리에 몰리는데,
                    // 최대값으로 잡으면 그것들이 머리 폭이 되고(기사 실측 1.10m vs 두개골 0.47m) 관이 머리보다 커진다.
                    var c = Vector3.zero;
                    for (int i = 0; i < skull.Count; i++)
                        c += skull[i];
                    c /= skull.Count;
                    var radii = new List<float>(skull.Count);
                    for (int i = 0; i < skull.Count; i++)
                        radii.Add(new Vector2(skull[i].x - c.x, skull[i].z - c.z).magnitude);
                    radii.Sort();
                    float r80 = radii[Mathf.Clamp(Mathf.RoundToInt(radii.Count * 0.80f), 0, radii.Count - 1)];
                    skullC = new Vector3(c.x, headBone.position.y, c.z);
                    rMax = Mathf.Max(0.10f, r80 * 1.20f);
                }
                else
                {
                    rMax = 0.25f * bodyH;
                }
            }

            // 정수리도 최고점이 아니라 **90퍼센타일**로 잡는다 — 투구 볏·머리카락 몇 가닥의 끝에 얹으면
            // 관 밑으로 공기가 보여 「떠 있다」로 읽힌다(검수 반려 1의 화면 인상).
            var ys = new List<float>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (headBone != null)
                {
                    if (pts[i].y < floorY)
                        continue;
                    if (new Vector2(pts[i].x - skullC.x, pts[i].z - skullC.z).magnitude > rMax)
                        continue;
                }
                ys.Add(pts[i].y);
            }
            if (ys.Count == 0)
                return false;
            ys.Sort();
            topY = ys[Mathf.Clamp(Mathf.RoundToInt(ys.Count * 0.90f), 0, ys.Count - 1)];

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            int n = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i].y < topY - HeadSlab)
                    continue;
                if (headBone != null &&
                    new Vector2(pts[i].x - skullC.x, pts[i].z - skullC.z).magnitude > rMax)
                    continue;
                n++;
                if (pts[i].x < minX) minX = pts[i].x;
                if (pts[i].x > maxX) maxX = pts[i].x;
                if (pts[i].z < minZ) minZ = pts[i].z;
                if (pts[i].z > maxZ) maxZ = pts[i].z;
            }
            if (n == 0)
                return false;
            width = Mathf.Max(maxX - minX, maxZ - minZ);
            center = new Vector3((minX + maxX) * 0.5f, topY, (minZ + maxZ) * 0.5f);
            return width > 0.01f;
        }

        /// <summary>정수리 슬랩을 머리 덩어리로 본다 — 무기가 얼굴 옆에 떠 있는지 판정할 때 쓴다.</summary>
        public static bool HeadBounds(GameObject actor, out Bounds head)
        {
            head = new Bounds();
            if (!HeadMetrics(actor, out float topY, out float width, out Vector3 c))
                return false;
            head = new Bounds(new Vector3(c.x, topY - HeadSlab * 0.5f, c.z), new Vector3(width, HeadSlab, width));
            return true;
        }

        /// <summary>
        /// 무기의 **장축**과 양 끝점(월드). 그립 끝은 단면이 얇은 쪽이다 — 손잡이는 가늘고 날은 넓다.
        /// </summary>
        public static bool WeaponAxis(Transform weapon, out Vector3 grip, out Vector3 tip)
        {
            grip = Vector3.zero;
            tip = Vector3.zero;
            var pts = new List<Vector3>();
            var filters = weapon.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                var rend = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || rend == null || !rend.enabled || !mf.gameObject.activeInHierarchy)
                    continue;
                var verts = mf.sharedMesh.vertices;
                var m = mf.transform.localToWorldMatrix;
                for (int v = 0; v < verts.Length; v++)
                    pts.Add(m.MultiplyPoint3x4(verts[v]));
            }
            var skinned = weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length > 0)
            {
                var baked = new Mesh();
                for (int i = 0; i < skinned.Length; i++)
                {
                    if (!skinned[i].enabled || !skinned[i].gameObject.activeInHierarchy)
                        continue;
                    skinned[i].BakeMesh(baked, false);   // 스케일은 localToWorldMatrix가 적용한다 — true면 두 번 곱해져 머리가 몸통만큼 커진다
                    var verts = baked.vertices;
                    var m = skinned[i].transform.localToWorldMatrix;
                    for (int v = 0; v < verts.Length; v++)
                        pts.Add(m.MultiplyPoint3x4(verts[v]));
                }
                Object.DestroyImmediate(baked);
            }
            if (pts.Count < 8)
                return false;

            // 장축 = 무기 로컬 축 중 가장 긴 것(무기 메시는 축 정렬이라 이걸로 충분하다).
            var inv = weapon.worldToLocalMatrix;
            var lb = new Bounds(inv.MultiplyPoint3x4(pts[0]), Vector3.zero);
            for (int i = 1; i < pts.Count; i++)
                lb.Encapsulate(inv.MultiplyPoint3x4(pts[i]));
            int axis = lb.size.x >= lb.size.y && lb.size.x >= lb.size.z ? 0 : (lb.size.y >= lb.size.z ? 1 : 2);
            float lo = axis == 0 ? lb.min.x : axis == 1 ? lb.min.y : lb.min.z;
            float hi = axis == 0 ? lb.max.x : axis == 1 ? lb.max.y : lb.max.z;
            float len = hi - lo;
            if (len < 0.01f)
                return false;

            // 양 끝 10% 슬랩의 단면 폭 — 가는 쪽이 손잡이다.
            float slab = len * 0.10f;
            float loW = 0f, hiW = 0f;
            var loC = Vector3.zero;
            var hiC = Vector3.zero;
            int loN = 0, hiN = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var lp = inv.MultiplyPoint3x4(pts[i]);
                float a = axis == 0 ? lp.x : axis == 1 ? lp.y : lp.z;
                float r = Mathf.Sqrt(
                    (axis == 0 ? 0f : lp.x - lb.center.x) * (axis == 0 ? 0f : lp.x - lb.center.x) +
                    (axis == 1 ? 0f : lp.y - lb.center.y) * (axis == 1 ? 0f : lp.y - lb.center.y) +
                    (axis == 2 ? 0f : lp.z - lb.center.z) * (axis == 2 ? 0f : lp.z - lb.center.z));
                if (a <= lo + slab) { loW = Mathf.Max(loW, r); loC += pts[i]; loN++; }
                else if (a >= hi - slab) { hiW = Mathf.Max(hiW, r); hiC += pts[i]; hiN++; }
            }
            if (loN == 0 || hiN == 0)
                return false;
            loC /= loN;
            hiC /= hiN;
            if (loW <= hiW) { grip = loC; tip = hiC; }
            else { grip = hiC; tip = loC; }
            return true;
        }

        /// <summary>머리 본(또는 그 자손 본)에 **스킨된** 정점만 모은다 — 두개골·머리카락. 투구 뿔 같은
        /// 별도 메시(머리 본에 부모로 매달린 MeshFilter)는 여기 안 들어온다.</summary>
        public static void HeadSkinPoints(GameObject actor, Transform headBone, List<Vector3> outPoints)
        {
            outPoints.Clear();
            var skinned = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var baked = new Mesh();
            for (int i = 0; i < skinned.Length; i++)
            {
                var smr = skinned[i];
                if (!smr.enabled || !smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                    continue;
                var bones = smr.bones;
                var weights = smr.sharedMesh.boneWeights;
                if (bones == null || weights == null || weights.Length == 0)
                    continue;
                var isHead = new bool[bones.Length];
                for (int b = 0; b < bones.Length; b++)
                {
                    for (var t = bones[b]; t != null; t = t.parent)
                    {
                        if (t == headBone) { isHead[b] = true; break; }
                        if (t.gameObject == actor) break;
                    }
                }
                smr.BakeMesh(baked, false);
                var verts = baked.vertices;
                var m = smr.transform.localToWorldMatrix;
                int n = Mathf.Min(verts.Length, weights.Length);
                for (int v = 0; v < n; v++)
                {
                    int bi = weights[v].boneIndex0;
                    if (bi < 0 || bi >= isHead.Length || !isHead[bi] || weights[v].weight0 < 0.5f)
                        continue;
                    outPoints.Add(m.MultiplyPoint3x4(verts[v]));
                }
            }
            Object.DestroyImmediate(baked);
        }

        /// <summary>이름에 키워드가 든 본을 찾는다(정확히 일치하는 것 우선).</summary>
        public static Transform FindBone(GameObject actor, string keyword)
        {
            Transform best = null;
            var all = actor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (n == keyword)
                    return all[i];
                if (best == null && n.Contains(keyword))
                    best = all[i];
            }
            return best;
        }

        /// <summary>손 본의 **팔뚝 방향**(팔꿈치→손). 무기 장축은 이 방향으로 뻗어야 「쥔 것」으로 읽힌다.</summary>
        public static Vector3 ForearmDir(Transform hand, Transform fallbackRoot)
        {
            if (hand != null && hand.parent != null)
            {
                var d = hand.position - hand.parent.position;
                if (d.sqrMagnitude > 1e-6f)
                    return d.normalized;
            }
            return fallbackRoot != null ? fallbackRoot.forward : Vector3.forward;
        }
    }
}
