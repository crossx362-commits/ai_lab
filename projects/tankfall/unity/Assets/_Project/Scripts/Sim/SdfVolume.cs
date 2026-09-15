// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7
//
// 오너 지시(2026-09-15): "힉스필드 쓰지말어 코드로 그려"
//   → Heightfield(컬럼당 높이 1개) 폐기. 지형을 SDF 볼륨으로 잡고 메시를 코드로 생성한다.
//   → 기획서 §21(구형 폭발)·§28(발밑 도려내 떨어뜨리기)이 타협 없이 성립한다.
//
// ⚠️ SIM 레이어 규칙(§4-1): UnityEngine 참조 없음. 서버·AI·리플레이가 이 코드를 그대로 쓴다.
//
// === 핵심 설계: 하이브리드 희소 ===
// 지형의 99%는 한 번도 파괴되지 않는다. 그래서 복셀을 미리 깔지 않는다.
//   - 파괴되지 않은 곳: 기본 지형 함수 h(x,z)로 즉석 평가 → 메모리 0
//   - 파괴된 곳만: 해당 청크를 함수값으로 채워 할당(copy-on-write) 후 수정
// 전체를 복셀화하면 200×100×200m / 0.5m = 32M 복셀 = 128MB다. 하이브리드는 실측 §4 참조.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>
    /// 부호거리장 지형. 규약: <b>양수 = 공기, 음수 = 땅, 0 = 표면</b>.
    /// 격자 좌표는 전역 정수 (ix,iy,iz), 월드 = 격자 × Voxel + Origin.
    /// </summary>
    public sealed class SdfVolume
    {
        public readonly float Voxel;        // 복셀 크기 (m)
        public readonly int ChunkN;         // 청크 한 변의 복셀 수
        public readonly float OriginY;      // iy=0 에 해당하는 월드 Y

        readonly Dictionary<long, float[]> _chunks = new Dictionary<long, float[]>();
        readonly Func<float, float, float> _baseHeight;   // 기본 지형 h(x,z)

        public int ChunkCount => _chunks.Count;
        public long ChunkBytes => (long)_chunks.Count * ChunkN * ChunkN * ChunkN * sizeof(float);

        // (x,z) 격자에 h 와 경사계수를 미리 구워 둔다.
        // 경사 보정을 넣자 baseHeight 호출이 복셀당 5배로 늘어 굴착탄 폭발이 6ms→39ms 로 퇴행했다(실측).
        // 지형 함수는 파괴돼도 바뀌지 않으므로 한 번만 계산하면 된다. 401² 기준 1.3MB.
        readonly float[] _hTab, _sTab;
        readonly int _tabDim;

        public SdfVolume(float voxel, int chunkN, float originY, Func<float, float, float> baseHeight,
                         int gridCellsXZ = 0)
        {
            Voxel = voxel;
            ChunkN = chunkN;
            OriginY = originY;
            _baseHeight = baseHeight;

            if (gridCellsXZ > 0)
            {
                _tabDim = gridCellsXZ + 1;
                _hTab = new float[_tabDim * _tabDim];
                _sTab = new float[_tabDim * _tabDim];
                for (int iz = 0; iz < _tabDim; iz++)
                    for (int ix = 0; ix < _tabDim; ix++)
                    {
                        float x = ix * voxel, z = iz * voxel;
                        _hTab[iz * _tabDim + ix] = baseHeight(x, z);
                        _sTab[iz * _tabDim + ix] = ComputeSlopeScale(x, z);
                    }
            }
        }

        bool InTable(int ix, int iz) => _hTab != null && ix >= 0 && iz >= 0 && ix < _tabDim && iz < _tabDim;

        public float WorldX(int ix) => ix * Voxel;
        public float WorldY(int iy) => OriginY + iy * Voxel;
        public float WorldZ(int iz) => iz * Voxel;

        public int GridX(float x) => (int)MathF.Floor(x / Voxel);
        public int GridY(float y) => (int)MathF.Floor((y - OriginY) / Voxel);
        public int GridZ(float z) => (int)MathF.Floor(z / Voxel);

        /// <summary>
        /// 파괴 전 지형의 SDF.
        ///
        /// ⚠️ 그냥 (y − h)를 쓰면 안 된다. 그건 <b>수직</b> 거리라 비탈에서 실제 거리의 1/cosθ 배다.
        ///    폭발은 <b>반경</b> 거리(R − |p−c|)를 써넣으므로 두 거리장의 스케일이 어긋나고,
        ///    섞이는 경계에서 보간이 틀어져 크레이터 표면에 동심원 계단이 생긴다(실제 빌드에서 확인).
        ///    경사로 나눠 실제 거리에 맞춘다: d ≈ (y − h) / √(1 + |∇h|²)
        /// </summary>
        float BaseSdf(int ix, int iy, int iz)
        {
            if (InTable(ix, iz))
            {
                int t = iz * _tabDim + ix;
                return (WorldY(iy) - _hTab[t]) * _sTab[t];
            }
            float x = WorldX(ix), z = WorldZ(iz);
            return (WorldY(iy) - _baseHeight(x, z)) * ComputeSlopeScale(x, z);
        }

        /// <summary>격자 위면 구워둔 값, 아니면 즉석 계산.</summary>
        void SampleHS(int ix, int iz, out float h, out float scale)
        {
            if (InTable(ix, iz))
            {
                int t = iz * _tabDim + ix;
                h = _hTab[t]; scale = _sTab[t]; return;
            }
            float x = WorldX(ix), z = WorldZ(iz);
            h = _baseHeight(x, z); scale = ComputeSlopeScale(x, z);
        }

        /// <summary>1/√(1+|∇h|²) — 수직 거리를 실제 거리로 바꾸는 계수.</summary>
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

        /// <summary>SDF 샘플. 할당된 청크가 없으면 기본 지형 함수로 평가한다(메모리 0).</summary>
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

        /// <summary>SDF 기록. 청크가 없으면 기본 지형 값으로 채워 할당한다(copy-on-write).</summary>
        public void Set(int ix, int iy, int iz, float v)
        {
            int cx = FloorDiv(ix, ChunkN), cy = FloorDiv(iy, ChunkN), cz = FloorDiv(iz, ChunkN);
            long k = Key(cx, cy, cz);
            if (!_chunks.TryGetValue(k, out var data))
            {
                data = new float[ChunkN * ChunkN * ChunkN];
                int bx = cx * ChunkN, by = cy * ChunkN, bz = cz * ChunkN;
                // (lx,lz) 평면에 h/경사를 한 번만 — y 에 무관하므로 ChunkN 배 절약
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

        /// <summary>
        /// 블록 [i0,i0+ni) × [j0,j0+nj) × [k0,k0+nk) 의 SDF를 한 번에 채운다. 레이아웃 dst[(j*nk+k)*ni+i].
        /// 반환 = (최솟값, 최댓값) — 호출자가 "전부 땅 / 전부 공기" 청크를 즉시 버리는 데 쓴다.
        ///
        /// ⚠️ Sample()을 복셀마다 부르는 것보다 두 가지가 빠르다:
        ///   1) h(x,z)는 y에 무관하다 → (i,k) 평면에 한 번만 계산. 실측으로 18³→18² (18배)
        ///   2) 청크 딕셔너리 조회를 직전 키와 비교해 건너뛴다(블록은 대개 한두 청크 안에 있다)
        /// </summary>
        public (float min, float max) FillBlock(int i0, int j0, int k0, int ni, int nj, int nk,
                                                float[] dst, float[] hScratch)
        {
            // h 와 경사계수를 (i,k) 평면에 함께 캐시한다. 둘 다 y 에 무관하다.
            // hScratch 는 [0..ni*nk) = h, [ni*nk..2*ni*nk) = 경사계수 로 나눠 쓴다.
            int plane = ni * nk;
            for (int k = 0; k < nk; k++)
            {
                for (int i = 0; i < ni; i++)
                    SampleHS(i0 + i, k0 + k, out hScratch[k * ni + i], out hScratch[plane + k * ni + i]);
            }

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

        /// <summary>월드 좌표 삼선형 샘플. 탱크 접지·포탄 충돌·조준이 전부 이 함수를 쓴다(콜라이더 없음).</summary>
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

        /// <summary>
        /// (x,z)에서 위→아래로 내려오며 표면 교차 Y들을 모은다.
        /// 교차가 2개 이상이면 그 기둥에 <b>오버행 또는 동굴</b>이 있다는 뜻 — Heightfield로는 표현 불가능했던 것.
        /// 탱크 접지는 이 목록의 첫 번째(가장 위) 표면을 쓴다.
        /// </summary>
        public int SurfacesAlongColumn(float x, float z, float yTop, float yBottom, float step, Span<float> outY)
        {
            int n = 0;
            float prevY = yTop, prev = SampleWorld(x, yTop, z);
            for (float y = yTop - step; y >= yBottom && n < outY.Length; y -= step)
            {
                float cur = SampleWorld(x, y, z);
                if ((prev > 0f) != (cur > 0f))
                {
                    float t = prev / (prev - cur);              // 선형보간 교차
                    outY[n++] = prevY + (y - prevY) * t;
                }
                prev = cur; prevY = y;
            }
            return n;
        }
    }
}
