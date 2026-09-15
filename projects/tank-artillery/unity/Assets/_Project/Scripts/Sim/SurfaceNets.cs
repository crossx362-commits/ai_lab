// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7
//
// "코드로 그린다" — SDF에서 지형 메시를 직접 생성한다. 유니티 Terrain도, 미리 만든 메시 에셋도 없다.
//
// Surface Nets(듀얼 컨투어링의 단순형)를 쓴다. 마칭 큐브 대신 고른 이유:
//   - 셀당 정점 1개 → 삼각형 수가 마칭 큐브의 약 1/2
//   - 256가지 케이스 테이블이 없다(구현·검증이 짧다)
//   - 쿼드 격자라 메시가 균일해서 변형 후에도 셰이딩이 튀지 않는다
//
// ⚠️ SIM 레이어(§4-1): UnityEngine 참조 없음. 결과 배열을 VIEW에서 Mesh로 올린다.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    public sealed class MeshData
    {
        public readonly List<float> Verts = new List<float>();   // x,y,z 반복
        public readonly List<int> Tris = new List<int>();
        public int VertexCount => Verts.Count / 3;
        public int TriangleCount => Tris.Count / 3;
    }

    public static class SurfaceNets
    {
        // 셀의 12 엣지 = (코너a, 코너b), 코너는 0..7 = (x) | (y<<1) | (z<<2)
        static readonly int[,] Edges =
        {
            {0,1},{2,3},{4,5},{6,7},   // x 방향
            {0,2},{1,3},{4,6},{5,7},   // y 방향
            {0,4},{1,5},{2,6},{3,7},   // z 방향
        };

        /// <summary>
        /// 셀 영역 [i0,i1) × [j0,j1) × [k0,k1) 을 메시로 만든다.
        /// 청크 단위로 호출할 땐 범위를 1셀씩 겹쳐 호출하면 이음매가 생기지 않는다(SDF 샘플이 전역이라 가능).
        /// </summary>
        public static MeshData Build(SdfVolume vol, int i0, int i1, int j0, int j1, int k0, int k1)
        {
            int di = i1 - i0, dj = j1 - j0, dk = k1 - k0;
            var mesh = new MeshData();
            var vertIndex = new int[di * dj * dk];
            for (int n = 0; n < vertIndex.Length; n++) vertIndex[n] = -1;

            // SDF를 로컬 배열에 한 번만 캐시한다.
            // 캐시 없이 vol.Sample을 직접 부르면 같은 코너를 최대 8번 조회하고 매번
            // 청크 딕셔너리 탐색 + FloorDiv/Mod를 거친다 — 실측으로 청크 빌드가 9배 느려진다.
            int ci = di + 1, cj = dj + 1, ck = dk + 1;
            var sdf = new float[ci * cj * ck];
            var (mn, mx) = vol.FillBlock(i0, j0, k0, ci, cj, ck, sdf, new float[ci * ck * 2]);   // h + 경사계수

            // 표면이 지나지 않는 청크는 여기서 끝낸다.
            // 폭발의 dirty 범위는 육면체인데 실제로 깎이는 건 그 안에 내접한 구라,
            // 모서리 청크들은 전부 땅이거나 전부 공기다 — 굴착탄에서 특히 많이 걸린다.
            if (mx <= 0f || mn > 0f) return mesh;

            float S(int i, int j, int k) => sdf[((j - j0) * ck + (k - k0)) * ci + (i - i0)];

            var corner = new float[8];
            float v = vol.Voxel;

            // --- 1단계: 표면이 지나는 셀마다 정점 1개 ---
            for (int j = j0; j < j1; j++)
                for (int k = k0; k < k1; k++)
                    for (int i = i0; i < i1; i++)
                    {
                        int mask = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            float s = S(i + (c & 1), j + ((c >> 1) & 1), k + ((c >> 2) & 1));
                            corner[c] = s;
                            if (s > 0f) mask |= 1 << c;   // 양수 = 공기
                        }
                        if (mask == 0 || mask == 255) continue;   // 전부 땅 또는 전부 공기

                        // 부호가 바뀌는 엣지들의 교차점 평균 = 셀 정점
                        float sx = 0f, sy = 0f, sz = 0f;
                        int hits = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            int a = Edges[e, 0], b = Edges[e, 1];
                            if ((corner[a] > 0f) == (corner[b] > 0f)) continue;
                            float t = corner[a] / (corner[a] - corner[b]);
                            sx += (a & 1) + t * ((b & 1) - (a & 1));
                            sy += ((a >> 1) & 1) + t * (((b >> 1) & 1) - ((a >> 1) & 1));
                            sz += ((a >> 2) & 1) + t * (((b >> 2) & 1) - ((a >> 2) & 1));
                            hits++;
                        }
                        float inv = 1f / hits;
                        vertIndex[((j - j0) * dk + (k - k0)) * di + (i - i0)] = mesh.VertexCount;
                        mesh.Verts.Add(vol.WorldX(i) + sx * inv * v);
                        mesh.Verts.Add(vol.WorldY(j) + sy * inv * v);
                        mesh.Verts.Add(vol.WorldZ(k) + sz * inv * v);
                    }

            int Idx(int i, int j, int k)
            {
                if (i < i0 || i >= i1 || j < j0 || j >= j1 || k < k0 || k >= k1) return -1;
                return vertIndex[((j - j0) * dk + (k - k0)) * di + (i - i0)];
            }

            // --- 2단계: 부호가 바뀌는 격자 엣지마다 인접 4셀 정점을 이어 쿼드 ---
            for (int j = j0; j < j1; j++)
                for (int k = k0; k < k1; k++)
                    for (int i = i0; i < i1; i++)
                    {
                        bool s0 = S(i, j, k) > 0f;

                        if (i > i0 && S(i, j, k + 1) > 0f != s0 && j > j0)
                            Quad(mesh, Idx(i - 1, j - 1, k), Idx(i, j - 1, k), Idx(i, j, k), Idx(i - 1, j, k), s0);

                        if (j > j0 && S(i + 1, j, k) > 0f != s0 && k > k0)
                            Quad(mesh, Idx(i, j - 1, k - 1), Idx(i, j, k - 1), Idx(i, j, k), Idx(i, j - 1, k), s0);

                        if (k > k0 && S(i, j + 1, k) > 0f != s0 && i > i0)
                            Quad(mesh, Idx(i - 1, j, k - 1), Idx(i - 1, j, k), Idx(i, j, k), Idx(i, j, k - 1), s0);
                    }

            return mesh;
        }

        static void Quad(MeshData m, int a, int b, int c, int d, bool flip)
        {
            if (a < 0 || b < 0 || c < 0 || d < 0) return;   // 영역 경계 — 이웃 청크가 이어 만든다
            if (flip) { (a, c) = (c, a); }
            m.Tris.Add(a); m.Tris.Add(b); m.Tris.Add(c);
            m.Tris.Add(a); m.Tris.Add(c); m.Tris.Add(d);
        }
    }
}
