// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7
//
// SIM이 만든 MeshData를 유니티 Mesh로 올린다. 여기가 VIEW의 전부다 —
// 지형 로직(파괴·붕괴·샘플)은 한 줄도 없다. 계산은 Sim이 하고 이 클래스는 그리기만 한다(§4-1).
//
// ⚠️ MeshCollider를 붙이지 않는다(§7-5). 지형 충돌은 전부 SDF 샘플로 한다.

using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public sealed class TerrainView : MonoBehaviour
    {
        public SdfVolume Volume { get; private set; }

        int _chunkN;
        Material _material;
        readonly Dictionary<Vector3Int, MeshFilter> _chunks = new Dictionary<Vector3Int, MeshFilter>();

        /// ⚠️ 청크별 삼각형 수를 **직접 들고 있는다**. `mesh.triangles` 는 값을 읽는 게 아니라
        ///    인덱스 배열 전체를 새로 할당해 복사하는 프로퍼티다 — 개수 하나 때문에 그걸 부르면
        ///    폭발 한 번에 더티 청크 수만큼 수십 KB 를 복사하고 GC 에 던진다(실측: 메시 재생성 4~12 ms 의 일부).
        readonly Dictionary<Vector3Int, int> _triCount = new Dictionary<Vector3Int, int>();
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Color> _c = new List<Color>();

        /// <summary>맵 테마(색 팔레트). 판 시작에 한 번 정해지고 그 뒤 모든 청크가 이 값으로 칠해진다.</summary>
        public MapTheme Theme = MapTheme.Of(Tankfall.Sim.MapKind.TwinHills, false);

        readonly List<int> _t = new List<int>();

        // 메시 재생성 내부 분해 계측(§7-7). 어디가 비싼지 추측하지 않는다.
        public long UploadTicks, NormalTicks, BoundsTicks;
        public void ResetTicks() { UploadTicks = NormalTicks = BoundsTicks = 0; }
        public static float Ms(long ticks) => (float)ticks * 1000f / Stopwatch.Frequency;

        public int ChunkCount => _chunks.Count;
        public int TriangleCount { get; private set; }

        /// <summary>성능 분해 측정용(§7-7). 지형 그림자/렌더러를 통째로 끄고 비용을 가른다.</summary>
        public void SetShadows(bool on)
        {
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On
                          : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var kv in _chunks)
                if (kv.Value != null)
                {
                    var mr = kv.Value.GetComponent<MeshRenderer>();
                    if (mr != null) mr.shadowCastingMode = mode;
                }
        }

        public void SetVisible(bool on)
        {
            foreach (var kv in _chunks)
                if (kv.Value != null)
                {
                    var mr = kv.Value.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = on;
                }
        }

        public void Init(SdfVolume vol, int chunkN, Material mat)
        {
            Volume = vol; _chunkN = chunkN; _material = mat;
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);

        /// <summary>표면이 지날 수 있는 청크를 한 번에 만든다(최초 1회).</summary>
        public void BuildRegion(int cx0, int cx1, int cy0, int cy1, int cz0, int cz1)
        {
            for (int cy = cy0; cy < cy1; cy++)
                for (int cz = cz0; cz < cz1; cz++)
                    for (int cx = cx0; cx < cx1; cx++)
                        RebuildChunk(new Vector3Int(cx, cy, cz));
        }

        /// <summary>폭발·붕괴가 건드린 범위의 청크만 다시 만든다.</summary>
        public void ApplyDirty(in DirtyBounds b)
        {
            if (b.Empty) return;
            int cx0 = FloorDiv(b.I0, _chunkN), cx1 = FloorDiv(b.I1 - 1, _chunkN) + 1;
            int cy0 = FloorDiv(b.J0, _chunkN), cy1 = FloorDiv(b.J1 - 1, _chunkN) + 1;
            int cz0 = FloorDiv(b.K0, _chunkN), cz1 = FloorDiv(b.K1 - 1, _chunkN) + 1;
            for (int cy = cy0; cy < cy1; cy++)
                for (int cz = cz0; cz < cz1; cz++)
                    for (int cx = cx0; cx < cx1; cx++)
                        RebuildChunk(new Vector3Int(cx, cy, cz));
        }

        void RebuildChunk(Vector3Int c)
        {
            int i0 = c.x * _chunkN, j0 = c.y * _chunkN, k0 = c.z * _chunkN;
            // 1셀 겹쳐 만들면 청크 사이 이음매가 생기지 않는다(§7-2)
            var data = SurfaceNets.Build(Volume, i0, i0 + _chunkN + 1,
                                                 j0, j0 + _chunkN + 1,
                                                 k0, k0 + _chunkN + 1);

            if (data.TriangleCount == 0)
            {
                if (_chunks.TryGetValue(c, out var dead) && dead != null)
                {
                    if (_triCount.TryGetValue(c, out int oldN)) TriangleCount -= oldN;
                    Destroy(dead.gameObject);
                    _chunks.Remove(c);
                    _triCount.Remove(c);
                }
                return;
            }

            if (!_chunks.TryGetValue(c, out var mf) || mf == null)
            {
                var go = new GameObject($"Chunk_{c.x}_{c.y}_{c.z}");
                go.transform.SetParent(transform, false);
                mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var m = new Mesh { name = go.name };
                m.MarkDynamic();                       // 정점 버퍼 재할당을 피한다
                mf.sharedMesh = m;
                _chunks[c] = mf;
            }
            else if (_triCount.TryGetValue(c, out int prevN)) TriangleCount -= prevN;

            _v.Clear(); _t.Clear();
            var verts = data.Verts;
            for (int n = 0; n < verts.Count; n += 3)
                _v.Add(new Vector3(verts[n], verts[n + 1], verts[n + 2]));
            _t.AddRange(data.Tris);

            var mesh = mf.sharedMesh;
            mesh.Clear();
            mesh.indexFormat = _v.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            long t0 = Stopwatch.GetTimestamp();
            mesh.SetVertices(_v);
            // calculateBounds:false — 바로 아래에서 RecalculateBounds 를 부르므로 두 번 할 필요가 없다
            mesh.SetTriangles(_t, 0, false);
            long t1 = Stopwatch.GetTimestamp();
            mesh.RecalculateNormals();
            // 정점 색(§ TerrainPalette) — 법선이 나온 뒤라야 경사를 안다.
            // ⚠️ 여기서 비용이 늘면 폭발 프레임(§7-6-3)이 흔들린다. `-perf` 로 재고 나서 손대라.
            mesh.GetNormals(_n);
            _c.Clear();
            for (int i = 0; i < _v.Count; i++) _c.Add(TerrainPalette.Of(Theme, _v[i].y, _n[i].y));
            mesh.SetColors(_c);
            long t2 = Stopwatch.GetTimestamp();
            mesh.RecalculateBounds();
            long t3 = Stopwatch.GetTimestamp();

            UploadTicks += t1 - t0; NormalTicks += t2 - t1; BoundsTicks += t3 - t2;
            TriangleCount += data.TriangleCount;
            _triCount[c] = data.TriangleCount;
        }
    }
}
