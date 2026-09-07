using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **「몸에 박혔다」를 삼각형으로 판정한다**(검수 랩 2026-09-08).
    ///
    /// 왜 필요한가: 겹침을 **상자 교차**로만 재면 **속이 빈 것**이 걸린다 —
    /// 차양(`stall-red`)의 바운드는 지붕 아래 빈 공간까지 포함하므로, 차양 밑에 선 상인이
    /// 「8% 박혔다」로 잡혔다. 그건 §18.19가 맞게 구현된 모습이지 결함이 아니다.
    /// 반대로 **자를 느슨하게 하면 구멍이 된다** — 「바운드가 크면 봐준다」 같은 예외는 금지다.
    ///
    /// 그래서 봐주지 않고 **더 정확하게** 잰다: 조각의 삼각형이 몸 상자와 실제로 만나는가
    /// (Akenine-Möller 삼각형-상자 SAT). 지붕만 있는 차양은 삼각형이 머리 위를 지나가므로 안 걸리고,
    /// 몸을 관통한 사슴·궤짝은 그대로 걸린다. 판정을 못 하는 경우(메시를 못 읽는 조각)는
    /// **겹친 것으로 본다** — 못 재는 것을 통과시키면 그게 구멍이다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        internal static bool MeshHitsBox(Renderer r, Bounds box)
        {
            var filter = r != null ? r.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            // `isReadable`은 보지 않는다 — 이건 에디터 전용 어셈블리라 임포트 메시의 정점을 그냥 읽는다.
            // (처음엔 `!isReadable`이면 「겹친 것」으로 돌렸는데, 저장소 FBX가 전부 Read/Write 꺼짐이라
            //  **모든 조각이 보수적 통과**로 빠져 이 자가 아무 일도 안 했다 — 실측으로 발견.)
            if (mesh == null)
                return true;                                  // 못 재면 겹친 것으로 본다(보수적)
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var l2w = r.transform.localToWorldMatrix;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                var a = l2w.MultiplyPoint3x4(verts[tris[i]]);
                var b = l2w.MultiplyPoint3x4(verts[tris[i + 1]]);
                var c = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);
                if (TriBoxOverlap(box.center, box.extents, a, b, c))
                    return true;
            }
            return false;
        }

        static bool TriBoxOverlap(Vector3 center, Vector3 half, Vector3 a, Vector3 b, Vector3 c)
        {
            a -= center; b -= center; c -= center;
            var e0 = b - a; var e1 = c - b; var e2 = a - c;
            // 9개 축(상자 축 × 삼각형 변)
            if (!AxisTest(a, b, c, half, e0)) return false;
            if (!AxisTest(a, b, c, half, e1)) return false;
            if (!AxisTest(a, b, c, half, e2)) return false;
            // 상자 자신의 3축
            if (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > half.x || Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < -half.x) return false;
            if (Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > half.y || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < -half.y) return false;
            if (Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > half.z || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < -half.z) return false;
            // 삼각형 평면
            var normal = Vector3.Cross(e0, e1);
            float d = Vector3.Dot(normal, a);
            float radius = half.x * Mathf.Abs(normal.x) + half.y * Mathf.Abs(normal.y) + half.z * Mathf.Abs(normal.z);
            return Mathf.Abs(d) <= radius;
        }

        static bool AxisTest(Vector3 a, Vector3 b, Vector3 c, Vector3 half, Vector3 edge)
        {
            var axes = new[]
            {
                new Vector3(0f, -edge.z, edge.y),
                new Vector3(edge.z, 0f, -edge.x),
                new Vector3(-edge.y, edge.x, 0f),
            };
            for (int i = 0; i < axes.Length; i++)
            {
                var ax = axes[i];
                if (ax.sqrMagnitude < 1e-12f)
                    continue;
                float pa = Vector3.Dot(ax, a), pb = Vector3.Dot(ax, b), pc = Vector3.Dot(ax, c);
                float radius = half.x * Mathf.Abs(ax.x) + half.y * Mathf.Abs(ax.y) + half.z * Mathf.Abs(ax.z);
                if (Mathf.Min(pa, Mathf.Min(pb, pc)) > radius || Mathf.Max(pa, Mathf.Max(pb, pc)) < -radius)
                    return false;
            }
            return true;
        }
    }
}
