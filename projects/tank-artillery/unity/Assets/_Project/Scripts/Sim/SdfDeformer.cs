// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7
//
// 폭발로 지형을 깎는다. Heightfield 때와 달리 **진짜 구형**으로 파인다 —
// 언덕 옆면을 때리면 위쪽 흙이 남아 오버행이 되고, 발밑을 파면 천장이 생긴다(기획서 §21·§28).

using System;

namespace Tankfall.Sim
{
    public struct BlastRequest
    {
        public float X, Y, Z;     // 착탄 월드 좌표
        public float Radius;      // Rc = weapon.ExplosionRadius * weapon.TerrainDamage

        public BlastRequest(float x, float y, float z, float radius) { X = x; Y = y; Z = z; Radius = radius; }
    }

    /// <summary>폭발이 건드린 격자 범위. 이 범위와 겹치는 청크만 메시를 다시 만든다.</summary>
    public struct DirtyBounds
    {
        public int I0, I1, J0, J1, K0, K1;
        public bool Empty;
    }

    public static class SdfDeformer
    {
        /// <summary>
        /// 구를 빼낸다(CSG subtract): sdf' = max(sdf, R − |p−c|).
        /// 구 안쪽은 R−|p−c| > 0 이 되어 공기로 바뀐다.
        /// </summary>
        public static DirtyBounds SubtractSphere(SdfVolume vol, in BlastRequest req)
        {
            float r = req.Radius, vx = vol.Voxel;
            // SDF가 표면 근처에서만 의미가 있으면 되므로, 구 반경 + 여유 2복셀만 훑는다.
            int i0 = vol.GridX(req.X - r) - 2, i1 = vol.GridX(req.X + r) + 3;
            int j0 = vol.GridY(req.Y - r) - 2, j1 = vol.GridY(req.Y + r) + 3;
            int k0 = vol.GridZ(req.Z - r) - 2, k1 = vol.GridZ(req.Z + r) + 3;

            var b = new DirtyBounds { I0 = int.MaxValue, J0 = int.MaxValue, K0 = int.MaxValue,
                                      I1 = int.MinValue, J1 = int.MinValue, K1 = int.MinValue, Empty = true };

            for (int j = j0; j < j1; j++)
                for (int k = k0; k < k1; k++)
                    for (int i = i0; i < i1; i++)
                    {
                        float dx = vol.WorldX(i) - req.X;
                        float dy = vol.WorldY(j) - req.Y;
                        float dz = vol.WorldZ(k) - req.Z;
                        float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                        float carved = r - dist;            // 구 안쪽에서 양수
                        if (carved <= 0f) continue;         // 구 밖 — 건드릴 필요 없음

                        float old = vol.Sample(i, j, k);
                        if (carved <= old) continue;        // 이미 공기이거나 더 비어 있음

                        vol.Set(i, j, k, carved);
                        if (i < b.I0) b.I0 = i; if (i + 1 > b.I1) b.I1 = i + 1;
                        if (j < b.J0) b.J0 = j; if (j + 1 > b.J1) b.J1 = j + 1;
                        if (k < b.K0) b.K0 = k; if (k + 1 > b.K1) b.K1 = k + 1;
                        b.Empty = false;
                    }
            return b;
        }
    }
}
