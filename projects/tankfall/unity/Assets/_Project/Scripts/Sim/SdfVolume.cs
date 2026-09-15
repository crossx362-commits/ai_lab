// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7
//
// 오너 지시(2026-09-15): "힙스필드 쓰지말어 코드로 그려"
//   → Heightfield(컬럼당 높이 1개) 폐기. 지형을 SDF 볼륨으로 잡고 메시를 코드로 생성한다.
//   → 기획서 §21(구형 폭발)·§28(발밑 도려내 떨어뜨리기)이 타협 없이 성립한다.
//
// ⚠️ SIM 레이어 규칙(§4-1): UnityEngine 참조 없음. 서버·AI·리플레이가 이 코드를 그대로 쓴다.
//
using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    public sealed class SdfVolume
    {
        public readonly float Voxel;
        public readonly int ChunkN;
        public readonly float OriginY;

        readonly Dictionary<long, float[]> _chunks = new Dictionary<long, float[]>();
        readonly Func<float, float, float> _baseHeight;
        readonly HeightGrad _grad;

        public int ChunkCount => _chunks.Count;
        public long ChunkBytes => (long)_chunks.Count * ChunkN * ChunkN * ChunkN * sizeof(float);

        readonly float[] _hTab, _sTab;
        readonly int _tabDim;

        public SdfVolume(float voxel, int chunkN, float originY, Func<float, float, float> baseHeight,
                         int gridCellsXZ = 0, HeightGrad grad = null)
        {
            Voxel = voxel;
            ChunkN = chunkN;
            OriginY = originY;
            _baseHeight = baseHeight;
            _grad = grad;
            if (gridCellsXZ > 0)
            {
                _tabDim = gridCellsXZ + 1;
                _hTab = new float[_tabDim * _tabDim];
                _sTab = new float[_tabDim * _tabDim];
                BakeTable(voxel);
            }
        }

        void BakeTable(float voxel)
        {
            bool mirror = _grad != null;
            int xLast = mirror ? _tabDim / 2 : _tabDim - 1;
            for (int iz = 0; iz < _tabDim; iz++)
            {
                float z = iz * voxel;
                for (int ix = 0; ix <= xLast; ix++)
                {
                    SampleHeightSlope(ix * voxel, z, out float h, out float s);
                    _hTab[iz * _tabDim + ix] = h;
                    _sTab[iz * _tabDim + ix] = s;
                    if (mirror)
                    {
                        int mx = _tabDim - 1 - ix;
                        if (mx != ix)
                        {
                            _hTab[iz * _tabDim + mx] = h;
                            _sTab[iz * _tabDim + mx] = s;
                        }
                    }
                }
            }
        }

        void SampleHeightSlope(float x, float z, out float h, out float scale)
        {
            if (_grad != null)
            {
                _grad(x, z, out h, out float hx, out float hz);
                scale = 1f / MathF.Sqrt(1f + hx * hx + hz * hz);
                return;
            }
            h = _baseHeight(x, z);
            scale = ComputeSlopeScale(x, z);
        }

        public void ClearDestroyed() => _chunks.Clear();

        bool InTable(int ix, int iz) => _hTab != null && ix >= 0 && iz >= 0 && ix < _tabDim && iz < _tabDim;
        public float WorldX(int ix) => ix * Voxel;
        public float WorldY(int iy) => OriginY + iy * Voxel;
        public float WorldZ(int iz) => iz * Voxel;
        public int GridX(float x) => (int)MathF.Floor(x / Voxel);
        public int GridY(float y) => (int)MathF.Floor((y - OriginY) / Voxel);
        public int GridZ(float z) => (int)MathF.Floor(z / Voxel);

        float BaseSdf(int ix, int iy, int iz)
        {
            if (InTable(ix, iz))
            {
                int t = iz * _tabDim + ix;
                return (WorldY(iy) - _hTab[t]) * _sTab[t];
            }
            SampleHeightSlope(WorldX(ix), WorldZ(iz), out float h, out float scale);
            return (WorldY(iy) - h) * scale;
        }

        void SampleHS(int ix, int iz, out float h, out float scale)
        {
            if (InTable(ix, iz))
            {
                int t = iz * _tabDim + ix;
                h = _hTab[t]; scale = _sTab[t]; return;
            }
            SampleHeightSlope(WorldX(ix), WorldZ(iz), out h, out scale);
        }

        float ComputeSlopeScale(float x, float z)
        {
            float e = Voxel;
            float dhx = (_baseHeight(x + e, z) - _baseHeight(x - e, z)) / (2f * e);
            float dhz = (_baseHeight(x, z + e) - _baseHeight(x, z - e)) / (2f * e);
            return 1f / MathF.Sqrt(1f + dhx * dhx + dhz * dhz);
        }

        static long Key(int cx, int cy, int cz)
            => ((long)(cx & 0x1FFFFF) << 42) | ((long)(cy & 0x1FFFFF) << 21) | (long)(cz & 0x1FFFFF);
        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);
        static int Mod(int a, int b) { int m = a % b; return m < 0 ? m + b : m; }

        public float Sample(int ix, int iy, int iz)
        {
            int cx = FloorDiv(ix, ChunkN), cy = FloorDiv(iy, ChunkN), cz = FloorDiv(iz, ChunkN);
            if (_chunks.TryGetValue(Key(cx, cy, cz), out var data))
            {
                int lx = Mod(ix, ChunkN), ly = Mod(iy, ChunkN), lz = Mod(iz, ChunkN);
                return data[(ly * ChunkN + lz) * ChunkN + lx];
            }
            return BaseSdf(ix, iy, iz);
        }

        public void Set(int ix, int iy, int iz, float v)
        {
            int cx = FloorDiv(ix, ChunkN), cy = FloorDiv(iy, ChunkN), cz = FloorDiv(iz, ChunkN);
            long k = Key(cx, cy, cz);
            if (!_chunks.TryGetValue(k, out var data))
            {
                data = new float[ChunkN * ChunkN * ChunkN];
                int bx = cx * ChunkN, by = cy * ChunkN, bz = cz * ChunkN;
                var ph = new float[ChunkN * ChunkN];
                var ps = new float[ChunkN * ChunkN];
                for (int lz = 0; lz < ChunkN; lz++)
                    for (int lx = 0; lx < ChunkN; lx++)
                        SampleHS(bx + lx, bz + lz, out ph[lz * ChunkN + lx], out ps[lz * ChunkN + lx]);
                for (int ly = 0; ly < ChunkN; ly++)
                {
                    float wy = WorldY(by + ly);
                    for (int lz = 0; lz < ChunkN; lz++)
                        for (int lx = 0; lx < ChunkN; lx++)
                        {
                            int t = lz * ChunkN + lx;
                            data[(ly * ChunkN + lz) * ChunkN + lx] = (wy - ph[t]) * ps[t];
                        }
                }
                _chunks[k] = data;
            }
            data[(Mod(iy, ChunkN) * ChunkN + Mod(iz, ChunkN)) * ChunkN + Mod(ix, ChunkN)] = v;
        }

        public (float min, float max) FillBlock(int i0, int j0, int k0, int ni, int nj, int nk,
                                                float[] dst, float[] hScratch)
        {
            int plane = ni * nk;
            for (int k = 0; k < nk; k++)
                for (int i = 0; i < ni; i++)
                    SampleHS(i0 + i, k0 + k, out hScratch[k * ni + i], out hScratch[plane + k * ni + i]);
            long lastKey = long.MinValue;
            float[] lastData = null;
            bool lastHas = false;
            float mn = float.MaxValue, mx = float.MinValue;
            for (int j = 0; j < nj; j++)
            {
                int iy = j0 + j;
                float wy = WorldY(iy);
                int cy = FloorDiv(iy, ChunkN), ly = Mod(iy, ChunkN);
                for (int k = 0; k < nk; k++)
                {
                    int iz = k0 + k;
                    int cz = FloorDiv(iz, ChunkN), lz = Mod(iz, ChunkN);
                    for (int i = 0; i < ni; i++)
                    {
                        int ix = i0 + i;
                        long key = Key(FloorDiv(ix, ChunkN), cy, cz);
                        if (key != lastKey) { lastHas = _chunks.TryGetValue(key, out lastData); lastKey = key; }
                        float v = lastHas
                            ? lastData[(ly * ChunkN + lz) * ChunkN + Mod(ix, ChunkN)]
                            : (wy - hScratch[k * ni + i]) * hScratch[plane + k * ni + i];
                        dst[(j * nk + k) * ni + i] = v;
                        if (v < mn) mn = v;
                        if (v > mx) mx = v;
                    }
                }
            }
            return (mn, mx);
        }

        public float SampleWorld(float x, float y, float z)
        {
            float fx = x / Voxel, fy = (y - OriginY) / Voxel, fz = z / Voxel;
            int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy), iz = (int)MathF.Floor(fz);
            float tx = fx - ix, ty = fy - iy, tz = fz - iz;
            float c000 = Sample(ix, iy, iz), c100 = Sample(ix + 1, iy, iz);
            float c010 = Sample(ix, iy + 1, iz), c110 = Sample(ix + 1, iy + 1, iz);
            float c001 = Sample(ix, iy, iz + 1), c101 = Sample(ix + 1, iy, iz + 1);
            float c011 = Sample(ix, iy + 1, iz + 1), c111 = Sample(ix + 1, iy + 1, iz + 1);
            float x00 = c000 + (c100 - c000) * tx, x10 = c010 + (c110 - c010) * tx;
            float x01 = c001 + (c101 - c001) * tx, x11 = c011 + (c111 - c011) * tx;
            float y0 = x00 + (x10 - x00) * ty, y1 = x01 + (x11 - x01) * ty;
            return y0 + (y1 - y0) * tz;
        }

        public int SurfacesAlongColumn(float x, float z, float yTop, float yBottom, float step, Span<float> outY)
        {
            int n = 0;
            float prevY = yTop, prev = SampleWorld(x, yTop, z);
            for (float y = yTop - step; y >= yBottom && n < outY.Length; y -= step)
            {
                float cur = SampleWorld(x, y, z);
                if ((prev > 0f) != (cur > 0f))
                {
                    float t = prev / (prev - cur);
                    outY[n++] = prevY + (y - prevY) * t;
                }
                prev = cur; prevY = y;
            }
            return n;
        }
    }
}
