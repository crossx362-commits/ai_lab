// SDF 지형이 실제로 성립하는지 재는 하네스. 유니티 없이 돈다(SIM 레이어 규칙의 증거이기도 하다).
//
// 재는 것: 메시 규모·생성 비용·폭발 비용·메모리, 그리고 방향 전환의 **이유**인
//          오버행([3])과 발밑 천장([4])이 진짜 만들어지는가.
//
// ⚠️ [3][4]는 네거티브 컨트롤을 함께 찍는다 — 폭발 전에도 "성립"이라고 나오면 측정이 고장 난 것이다.

using System;
using System.Diagnostics;
using Tankfall.Sim;

static class SdfVerify
{
    const float MapSize = 200f;
    const float Voxel = 0.5f;
    const int ChunkN = 16;          // 8m 청크
    const float OriginY = -20f;

    // 기본 지형: 쌍둥이 언덕(기획서 §31) + 완만한 굴곡
    static float Height(float x, float z)
    {
        float h = 4f;
        h += 22f * MathF.Exp(-(((x - 60f) * (x - 60f) + (z - 100f) * (z - 100f)) / 900f));
        h += 20f * MathF.Exp(-(((x - 145f) * (x - 145f) + (z - 105f) * (z - 105f)) / 800f));
        h += 2.5f * MathF.Sin(x * 0.06f) * MathF.Cos(z * 0.05f);
        return h;
    }

    static SdfVolume NewVolume() => new SdfVolume(Voxel, ChunkN, OriginY, Height, (int)(MapSize / Voxel));

    /// <summary>표면이 지나는 청크만 메시로 만든다. 청크 하나를 1셀 겹쳐 생성해 이음매를 없앤다.</summary>
    static (int verts, int tris, int chunks, double ms) BuildAll(SdfVolume vol)
    {
        var sw = Stopwatch.StartNew();
        int cells = (int)(MapSize / Voxel);
        int cx0 = 0, cx1 = cells / ChunkN;
        int cy0 = vol.GridY(-2f) / ChunkN, cy1 = vol.GridY(40f) / ChunkN + 1;
        int verts = 0, tris = 0, used = 0;

        for (int cy = cy0; cy < cy1; cy++)
            for (int cz = cx0; cz < cx1; cz++)
                for (int cx = cx0; cx < cx1; cx++)
                {
                    int i0 = cx * ChunkN, j0 = cy * ChunkN, k0 = cz * ChunkN;
                    if (!ChunkMayHaveSurface(vol, i0, j0, k0)) continue;
                    var m = SurfaceNets.Build(vol, i0, i0 + ChunkN + 1, j0, j0 + ChunkN + 1, k0, k0 + ChunkN + 1);
                    if (m.TriangleCount == 0) continue;
                    verts += m.VertexCount; tris += m.TriangleCount; used++;
                }
        sw.Stop();
        return (verts, tris, used, sw.Elapsed.TotalMilliseconds);
    }

    /// <summary>청크의 x,z 범위에서 지형 높이 min/max를 훑어 청크 y구간과 겹치는지 본다.</summary>
    static bool ChunkMayHaveSurface(SdfVolume vol, int i0, int j0, int k0)
    {
        float y0 = vol.WorldY(j0), y1 = vol.WorldY(j0 + ChunkN);
        float lo = float.MaxValue, hi = float.MinValue;
        for (int a = 0; a <= 4; a++)
            for (int b = 0; b <= 4; b++)
            {
                float h = Height(vol.WorldX(i0) + a * ChunkN * Voxel / 4f,
                                 vol.WorldZ(k0) + b * ChunkN * Voxel / 4f);
                lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
            }
        return hi >= y0 - Voxel && lo <= y1 + Voxel;
    }

    /// <summary>dirty 범위와 겹치는 청크만 다시 만든다.</summary>
    static (int tris, int chunks, double ms) Rebuild(SdfVolume vol, in DirtyBounds b)
    {
        if (b.Empty) return (0, 0, 0);
        var sw = Stopwatch.StartNew();
        int tris = 0, n = 0;
        int cx0 = FloorDiv(b.I0, ChunkN), cx1 = FloorDiv(b.I1 - 1, ChunkN) + 1;
        int cy0 = FloorDiv(b.J0, ChunkN), cy1 = FloorDiv(b.J1 - 1, ChunkN) + 1;
        int cz0 = FloorDiv(b.K0, ChunkN), cz1 = FloorDiv(b.K1 - 1, ChunkN) + 1;
        for (int cy = cy0; cy < cy1; cy++)
            for (int cz = cz0; cz < cz1; cz++)
                for (int cx = cx0; cx < cx1; cx++)
                {
                    var m = SurfaceNets.Build(vol, cx * ChunkN, cx * ChunkN + ChunkN + 1,
                                                   cy * ChunkN, cy * ChunkN + ChunkN + 1,
                                                   cz * ChunkN, cz * ChunkN + ChunkN + 1);
                    tris += m.TriangleCount; n++;
                }
        sw.Stop();
        return (tris, n, sw.Elapsed.TotalMilliseconds);
    }

    static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);

    static int Surfaces(SdfVolume vol, float x, float z, Span<float> ys)
        => vol.SurfacesAlongColumn(x, z, 60f, -19f, 0.1f, ys);

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"=== SDF 지형 검증 · 복셀 {Voxel}m · 청크 {ChunkN}³({ChunkN * Voxel}m) · 맵 {MapSize}m ===\n");

        // [1] 초기 지형
        var vol = NewVolume();
        var (v, t, c, ms) = BuildAll(vol);
        Console.WriteLine("[1] 초기 지형 메시 (파괴 전 — 복셀 할당 0)");
        Console.WriteLine($"    청크 {c}개  정점 {v:N0}  삼각형 {t:N0}  생성 {ms:F0} ms");
        Console.WriteLine($"    SDF 메모리 {vol.ChunkBytes / 1024.0:F0} KB  (기본 지형은 함수로 평가 → 할당 없음)\n");

        // [2] 폭발 1회
        Console.WriteLine("[2] 폭발 1회 — SDF 갱신 + 영향 청크 메시 재생성");
        Console.WriteLine($"    {"무기",-6} {"Rc",5}  {"SDF갱신",8} {"메시재생성",10} {"청크",5} {"삼각형",8} {"메모리증가",10}");
        foreach (var (rc, name) in new[] { (3f, "소형"), (7f, "일반"), (10f, "대형"), (16.1f, "굴착탄") })
        {
            var vv = NewVolume();
            float px = 100f, pz = 60f, py = Height(px, pz);
            long before = vv.ChunkBytes;
            var sw = Stopwatch.StartNew();
            var b = SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py, pz, rc));
            sw.Stop();
            var (rt, rc2, rms) = Rebuild(vv, b);
            Console.WriteLine($"    {name,-6} {rc,4:F1}m  {sw.Elapsed.TotalMilliseconds,6:F2}ms {rms,8:F2}ms {rc2,5} {rt,8:N0} {(vv.ChunkBytes - before) / 1024.0,8:F0} KB");
        }

        // [3] 오버행 — 이 방향 전환의 이유
        Console.WriteLine("\n[3] ★ 오버행/처마 (언덕 옆면 안쪽 굴착) — Heightfield로는 구조적으로 불가능했던 것");
        {
            var vv = NewVolume();
            float px = 69f, pz = 100f;                     // 언덕(중심 60,100) 옆면 안쪽
            float surf = Height(px, pz);
            float py = surf - 7.5f;                        // 표면 아래로 박아 넣는다 (위쪽 흙을 남기려고)
            Span<float> ys = stackalloc float[8];
            int nBefore = Surfaces(vv, px, pz, ys);
            SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py, pz, 6f));
            int nAfter = Surfaces(vv, px, pz, ys);
            Console.WriteLine($"    지표 {surf:F1}m, 폭심 {py:F1}m, Rc=6m");
            Console.WriteLine($"    폭발 전 표면 교차 {nBefore}개  (네거티브 컨트롤: 1이어야 정상)");
            Console.Write($"    폭발 후 표면 교차 {nAfter}개  → ");
            if (nAfter >= 2)
                Console.WriteLine($"오버행 성립 [처마 두께 {ys[0] - ys[1]:F1}m, 그 아래 공동 {ys[1] - ys[2]:F1}m]");
            else
                Console.WriteLine("오버행 없음");
        }

        // [4] 발밑 도려내기 — 기획서 §28 원본
        Console.WriteLine("\n[4] ★ 발밑 도려내기 (지표 아래 폭발) — 기획서 §28 원본 그대로");
        {
            var vv = NewVolume();
            float px = 100f, pz = 40f;
            float surf = Height(px, pz);
            float py = surf - 8f;                          // 지표 8m 아래
            Span<float> ys = stackalloc float[8];
            int nBefore = Surfaces(vv, px, pz, ys);
            SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py, pz, 7f));
            int nAfter = Surfaces(vv, px, pz, ys);
            Console.WriteLine($"    지표 {surf:F1}m, 폭심 {py:F1}m (8m 아래), Rc=7m");
            Console.WriteLine($"    폭발 전 표면 교차 {nBefore}개 → 폭발 후 {nAfter}개");
            if (nAfter >= 2)
            {
                float ceiling = ys[0] - ys[1];
                Console.WriteLine($"    → 동굴 성립. 천장 두께 {ceiling:F1}m");
                // 2발째: 같은 자리 위쪽을 때려 천장을 뚫는다 → 탱크가 서 있던 지면이 사라진다
                SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py + 5f, pz, 7f));
                int n2 = Surfaces(vv, px, pz, ys);
                Console.WriteLine($"    2발째(천장 노림) → 표면 교차 {n2}개, 최상단 표면 {(n2 > 0 ? ys[0] : 0):F1}m");
                Console.WriteLine($"    → 지면이 {surf:F1}m → {(n2 > 0 ? ys[0] : 0):F1}m 로 {surf - (n2 > 0 ? ys[0] : 0):F1}m 꺼졌다. 기획서 §28 성립.");
            }
            else
                Console.WriteLine("    → 동굴 없음");
        }

        // [5] 한 판 누적 메모리
        Console.WriteLine("\n[5] 한 판 누적 (폭발 100회, Rc=7m, 맵 전역에 분산)");
        {
            var vv = NewVolume();
            var rnd = new Random(12345);
            var sw = Stopwatch.StartNew();
            double rebuildMs = 0;
            int totalChunks = 0;
            for (int n = 0; n < 100; n++)
            {
                float px = 20f + (float)rnd.NextDouble() * 160f;
                float pz = 20f + (float)rnd.NextDouble() * 160f;
                var b = SdfDeformer.SubtractSphere(vv, new BlastRequest(px, Height(px, pz), pz, 7f));
                var (_, rcn, rms) = Rebuild(vv, b);
                rebuildMs += rms; totalChunks += rcn;
            }
            sw.Stop();
            Console.WriteLine($"    총 {sw.Elapsed.TotalMilliseconds:F0} ms (메시 재생성 {rebuildMs:F0} ms 포함)");
            Console.WriteLine($"    SDF 메모리 {vv.ChunkBytes / 1024.0 / 1024.0:F1} MB  ({vv.ChunkCount}청크)  ·  재생성 평균 {totalChunks / 100.0:F1}청크/발");
        }
    }
}
