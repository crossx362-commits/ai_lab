// Sim/TerrainDeformer.cs 가 tools/crater_harness.py(참조 구현)와 같은 수치를 내는지 대조한다.
// 유니티 없이 돈다 — SIM 레이어 규칙(§4-1)이 지켜지고 있다는 증거이기도 하다.
//
// 빌드·실행: tools/verify.sh 참조.

using System;
using Tankfall.Sim;

static class CraterVerify
{
    const int N = 256;
    const float MapSize = 200f;
    const float TankClimbLimitDeg = 42f;

    static HeightField Make(Func<int, int, float> baseFn = null)
    {
        var f = new HeightField(N, MapSize);
        if (baseFn != null)
            for (int j = 0; j < f.Dim; j++)
                for (int i = 0; i < f.Dim; i++)
                    f.SetAt(i, j, baseFn(i, j));
        return f;
    }

    static void Shot(HeightField f, float cx, float cz, float rc)
        => TerrainDeformer.ApplyCrater(f, new CraterRequest(cx, cz, rc));

    static (float slope, float lo, float hi) Stats(HeightField f, float cx, float cz, float r,
                                                   Func<int, int, float> baseFn = null)
    {
        float cs = f.CellSize;
        float ms = 0f, lo = 1e9f, hi = -1e9f;
        int j0 = Math.Max(1, (int)((cz - r) / cs)), j1 = Math.Min(f.Dim - 1, (int)((cz + r) / cs) + 1);
        int i0 = Math.Max(1, (int)((cx - r) / cs)), i1 = Math.Min(f.Dim - 1, (int)((cx + r) / cs) + 1);
        for (int j = j0; j < j1; j++)
            for (int i = i0; i < i1; i++)
            {
                ms = MathF.Max(ms, f.SlopeDegAt(i, j));
                float b = baseFn != null ? baseFn(i, j) : 0f;
                lo = MathF.Min(lo, f.GetAt(i, j) - b);
                hi = MathF.Max(hi, f.GetAt(i, j) - b);
            }
        return (ms, lo, hi);
    }

    static string Flag(float slope) => slope < TankClimbLimitDeg ? "OK" : "갇힘!";

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"=== C# 포팅 검증 · 안식각 {TerrainDeformer.ReposeAngleDeg}° · {TerrainDeformer.ReposeIterations}회 ===\n");

        Console.WriteLine("[1] Rc=7m 평지 1발 (고정 40회)");
        {
            var f = Make(); Shot(f, 100, 100, 7f);
            var (s, lo, hi) = Stats(f, 100, 100, 22);
            Console.WriteLine($"     최대경사 {s,5:F1}°  깊이 {-lo,4:F1}m  둔덕 {hi,4:F2}m  [{Flag(s)}]");
        }

        Console.WriteLine("\n[2] 같은 지점 10발 연타");
        {
            var f = Make();
            for (int n = 1; n <= 10; n++)
            {
                Shot(f, 100, 100, 7f);
                if (n == 1 || n == 3 || n == 5 || n == 10)
                {
                    var (s, lo, hi) = Stats(f, 100, 100, 25);
                    Console.WriteLine($"    {n,2}발: 최대경사 {s,5:F1}°  깊이 {-lo,4:F1}m  둔덕 {hi,4:F2}m  [{Flag(s)}]");
                }
            }
        }

        Console.WriteLine("\n[3] 25° 비탈 타격");
        {
            float cs = MapSize / N;
            Func<int, int, float> slope = (i, j) => i * cs * MathF.Tan(25f * MathF.PI / 180f);
            var f = Make(slope); Shot(f, 100, 100, 7f);
            var (s, lo, hi) = Stats(f, 100, 100, 22, slope);
            Console.WriteLine($"    비탈 깎임 {-lo,4:F1}m  하단 퇴적 {hi,4:F2}m  최대경사 {s,5:F1}°  [{Flag(s)}]");
        }

        Console.WriteLine("\n[4] 폭발 1회 실측 비용 (C#, 릴랙세이션 포함)");
        foreach (var (rc, name) in new[] { (3f, "소형"), (7f, "일반"), (10f, "대형"), (16.1f, "굴착탄") })
        {
            var f = Make();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Shot(f, 100, 100, rc);
            sw.Stop();
            Console.WriteLine($"    {name,-4}(Rc={rc,4:F1}m): {sw.Elapsed.TotalMilliseconds,6:F2} ms");
        }
    }
}
