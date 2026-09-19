// SDF 지형이 실제로 성립하는지 재는 하네스. 유니티 없이 돈다(SIM 레이어 규칙의 증거이기도 하다).
using System;
using System.Diagnostics;
using Tankfall.Sim;

static class SdfVerify
{
    const float MapSize = MapHeightFunction.MapSize;   // 단일 소스(MapHeightFunction)를 따른다
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
        // ══════════════════════════════════════════════════════════════
        //  🚨 이 파일에는 **판정이 하나도 없었다** (2026-09-19 발견).
        //     ✅/❌ 가 0개라 `verify.sh sdf` 는 **무슨 일이 있어도 rc=0** 이었다 —
        //     그런데 여기서 재는 것이 §7-4 실측(M2 게이트)과 §7-5 콜라이더 판단의 근거다.
        //     **이 프로젝트에서 하중이 제일 큰 검사가 판정을 안 하고 있었다.**
        //
        //  물리는 기준(검수 2026-09-19): **rc 에 물릴 자격 = «쌍으로 확인할 수 있는가».**
        //     대조군이 없는 단정은 물려 봐야 «항상 초록»인지 «진짜 통과»인지 구별이 안 된다.
        //     쌍이 안 되는 절(시간·메모리 측정)은 **[정보]** 로 찍어 ❌ 와 눈으로 구별되게 한다.
        // ══════════════════════════════════════════════════════════════
        int fail = 0;
        void Assert(bool ok, string what, string detail)
        {
            Console.WriteLine($"    {(ok ? "✅" : "❌")} {what}   {detail}");
            if (!ok) fail++;
        }
        Console.WriteLine($"=== SDF 지형 검증 · 복셀 {Voxel}m · 청크 {ChunkN}³({ChunkN * Voxel}m) · 맵 {MapSize}m ===\n");
        var vol = NewVolume();
        var (v, t, c, ms) = BuildAll(vol);
        Console.WriteLine("[1] 초기 지형 메시 (파괴 전 — 복셀 할당 0)");
        Console.WriteLine($"    [정보] 청크 {c}개  정점 {v:N0}  삼각형 {t:N0}  생성 {ms:F0} ms");
        // §7-1 의 핵심 주장: **파괴 전 지형은 메모리 0**(높이 함수로 즉석 계산한다).
        // 쌍의 한쪽이다 — 다른 쪽은 [5] 에서 «폭발하면 실제로 할당된다»를 잰다.
        // 이 둘이 같이 있어야 «0 이 맞다»와 «0 밖에 못 나온다»가 갈린다.
        long bytes0 = vol.ChunkBytes;
        Assert(bytes0 == 0, "파괴 전 SDF 메모리 0 (§7-1 — 높이 함수로 즉석 계산)", $"{bytes0} B");
        Console.WriteLine("");
        Console.WriteLine("[2] 폭발 1회");
        foreach (var (rc, name) in new[] { (3f, "소형"), (7f, "일반"), (10f, "대형"), (16.1f, "굴착탄") })
        {
            var vv = NewVolume();
            float px = 100f, pz = 60f, py = Height(px, pz);
            var sw = Stopwatch.StartNew();
            var b = SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py, pz, rc));
            sw.Stop();
            var (rt, rc2, rms) = Rebuild(vv, b);
            // [정보] — 시간은 기계마다 다르고 문턱을 정할 근거가 없다. 쌍이 안 되므로 rc 에 안 물린다.
            Console.WriteLine($"    [정보] {name} Rc={rc:F1} SDF {sw.Elapsed.TotalMilliseconds:F2}ms 메시 {rms:F2}ms");
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
            // §7-4 의 핵심 주장: **SDF 면 오버행이 만들어진다**(Heightfield 로는 불가능했다, §2-2).
            // 쌍이 이미 갖춰져 있다 — 폭발 **전**에는 지표 하나뿐이어야 하고, **후**에 둘 이상이어야 한다.
            // ⚠️ 후만 재면 «원래부터 둘이었다»와 구별이 안 된다. 전을 같이 재는 게 대조군이다.
            Console.WriteLine($"    [정보] 표면 교차 — 전 {nBefore} 후 {nAfter}");
            Assert(nBefore == 1, "폭발 전에는 지표 하나뿐이다 (대조군)", $"교차 {nBefore}개");
            Assert(nAfter >= 2, "폭발 후 오버행이 생긴다 (§7-4 · §2-2 — Heightfield 로는 불가능)", $"교차 {nAfter}개");
        }
        Console.WriteLine("\n[4] 발밑 도려내기");
        {
            var vv = NewVolume();
            float px = 100f, pz = 40f;
            float surf = Height(px, pz);
            float py = surf - 8f;
            Span<float> ys = stackalloc float[8];
            // 🚨 여기는 **전을 세어 놓고 버리고 있었다**(`Surfaces(...)` 반환값을 안 받았다) —
            //    그래서 «후 2개»가 폭발 때문인지 원래 그랬는지 알 수 없었다. 전을 받아 대조군으로 쓴다.
            int nBefore = Surfaces(vv, px, pz, ys);
            SdfDeformer.SubtractSphere(vv, new BlastRequest(px, py, pz, 7f));
            int nAfter = Surfaces(vv, px, pz, ys);
            Console.WriteLine($"    [정보] 표면 교차 — 전 {nBefore} 후 {nAfter}");
            Assert(nBefore == 1, "도려내기 전에는 지표 하나뿐이다 (대조군)", $"교차 {nBefore}개");
            // §28: 적 «발밑»을 도려내 떨어뜨린다 — 지표 아래를 파면 지면이 끊겨 교차가 늘어난다.
            Assert(nAfter >= 2, "발밑을 도려내면 지면이 끊긴다 (§28 — 기획서 원본이 그대로 성립)", $"교차 {nAfter}개");
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
            Console.WriteLine($"    [정보] SDF {vv.ChunkBytes / 1024.0 / 1024.0:F1} MB ({vv.ChunkCount}청크)");
            // 쌍의 다른 쪽 — 폭발하면 **실제로 할당된다**. 이게 없으면 [1] 의 «0» 은
            // «메모리를 아예 못 쓰는 상태»여도 통과한다(항상 초록).
            Assert(vv.ChunkBytes > 0 && vv.ChunkCount > 0,
                   "폭발 100회 뒤에는 실제로 할당된다 (위 «0» 의 대조군)",
                   $"{vv.ChunkCount}청크 {vv.ChunkBytes / 1024.0 / 1024.0:F1} MB");
        }

        Console.WriteLine(fail == 0 ? "\n✅ SDF 게이트 통과" : $"\n❌ SDF 게이트 실패 {fail}건");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
