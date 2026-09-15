// SDF 지형이 실제로 성립하는지 재는 하네스. 유니티 없이 돈다(SIM 레이어 규칙의 증거이기도 하다).
using System;
using System.Diagnostics;
using Tankfall.Sim;

static class SdfVerify
{
    const float MapSize = 200f;
    const float Voxel = 0.5f;
    const int ChunkN = 16;
    const float OriginY = -20f;

    static float Height(float x, float z) => MapHeightFunction.TwinHills(x, z);

    static SdfVolume NewVolume() => new SdfVolume(Voxel, ChunkN, OriginY, Height, (int)(MapSize / Voxel),
                                                 MapHeightFunction.Grad(MapKind.TwinHills));

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
        var vol = NewVolume();
        var (v, t, c, ms) = BuildAll(vol);
        Console.WriteLine("[1] 초기 지형 메시 (파괴 전 — 복셀 할당 0)");
        Console.WriteLine($"    청크 {c}개  정점 {v:N0}  삼각형 {t:N0}  생성 {ms:F0} ms");
        Console.WriteLine($"    SDF 메모리 {vol.ChunkBytes / 1024.0:F0} KB\n");
        Console.WriteLine("[2] 폭발 1회");
        foreach (var (rc, name) in new[] { (3f, "소형"), (7f, "일반"), (10f, "대형"), (16.1f, "굴착탄") })
        {
            var vv = NewVolume();
            float px = 100f, pz = 60f, py = Height(px, pz);
            var sw = Stopwatch.StartNew();
            var b = SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py, pz, rc));
            sw.Stop();
            var (rt, rc2, rms) = Rebuild(vv, b);
            Console.WriteLine($"    {name} Rc={rc:F1} SDF {sw.Elapsed.TotalMilliseconds:F2}ms 메시 {rms:F2}ms");
        }
        Console.WriteLine("\n[3] 오버행");
        {
            var vv = NewVolume();
            float px = MapHeightFunction.TwinWestHillX + 9f, pz = MapHeightFunction.TwinWestHillZ;
            float surf = Height(px, pz);
            float py = surf - 7.5f;
            Span<float> ys = stackalloc float[8];
            int nBefore = Surfaces(vv, px, pz, ys);
            SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py, pz, 6f));
            int nAfter = Surfaces(vv, px, pz, ys);
            Console.WriteLine($"    전 {nBefore} 후 {nAfter}");
            if (nAfter >= 2) Console.WriteLine("    오버행 성립");
            else Console.WriteLine("    오버행 없음");
        }
        Console.WriteLine("\n[4] 발밑 도려내기");
        {
            var vv = NewVolume();
            float px = 100f, pz = 40f;
            float surf = Height(px, pz);
            float py = surf - 8f;
            Span<float> ys = stackalloc float[8];
            Surfaces(vv, px, pz, ys);
            SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py, pz, 7f));
            int nAfter = Surfaces(vv, px, pz, ys);
            Console.WriteLine($"    후 {nAfter}");
        }
        Console.WriteLine("\n[5] 폭발 100회");
        {
            var vv = NewVolume();
            var rnd = new Random(12345);
            for (int n = 0; n < 100; n++)
            {
                float px = 20f + (float)rnd.NextDouble() * 160f;
                float pz = 20f + (float)rnd.NextDouble() * 160f;
                var b = SdfDeformer.SubtractSphere(vv, new BlastRequest(px, Height(px, pz), pz, 7f));
                Rebuild(vv, b);
            }
            Console.WriteLine($"    SDF {vv.ChunkBytes / 1024.0 / 1024.0:F1} MB ({vv.ChunkCount}청크)");
        }
    }
}
