// §7-6 의 세 문제가 실제로 해결됐는지 재는 하네스.
//
// ⚠️ 모든 검사에 네거티브 컨트롤을 붙였다 — 고치기 전에도 "통과"가 나오면 측정이 고장 난 것이다.

using System;
using System.Diagnostics;
using Tankfall.Sim;

static class GameplayVerify
{
    const float Voxel = 0.5f;
    const int ChunkN = 16;

    static float Flat(float x, float z) => 10f;                       // 평지 10m
    static float Hill(float x, float z) => 10f + 18f * MathF.Exp(-(((x - 60f) * (x - 60f) + (z - 100f) * (z - 100f)) / 900f));

    static SdfVolume NewVol(Func<float, float, float> h) => new SdfVolume(Voxel, ChunkN, -20f, h, (int)(200f / Voxel));

    static int Surfaces(SdfVolume v, float x, float z, Span<float> ys)
        => v.SurfacesAlongColumn(x, z, 60f, -19f, 0.05f, ys);

    static string YN(bool b) => b ? "✅" : "❌";

    /// <summary>크레이터 중심에서 8방향으로 한 걸음씩 연쇄 이동해 반경 밖까지 나갈 수 있는가.
    /// 한 걸음만 보는 EscapeRoutes 로는 "바닥에서 옆 바닥으로"도 탈출로 세어버린다 — 실측으로 확인한 함정.</summary>
    static bool WalksOut(SdfVolume v, float cx, float cz, float startY, float radius)
    {
        float step = TankGroundProbe.WalkStep;     // 연속 이동을 흉내낸다
        int maxSteps = (int)(radius * 3f / step) + 20;
        for (int d = 0; d < 8; d++)
        {
            float a = d * (MathF.PI * 2f / 8f);
            float dx = MathF.Cos(a) * step, dz = MathF.Sin(a) * step;
            float x = cx, z = cz, y = startY;
            for (int n = 0; n < maxSteps; n++)
            {
                if (TankGroundProbe.CanStepTo(v, y, x + dx, z + dz, out float ny) != TankGroundProbe.MoveResult.Ok)
                    break;
                x += dx; z += dz; y = ny;
                float dist = MathF.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                if (dist > radius + step && y >= startY) return true;   // 반경 밖 + 원래 높이 회복
            }
        }
        return false;
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== §7-6 세 문제 검증 ===\n");

        // ---------------------------------------------------------------
        Console.WriteLine("[1] 부유 덩어리 — 얇은 천장 붕괴 (임계 1.0m)");
        Console.WriteLine($"    {"천장두께",10} {"붕괴전 교차",12} {"붕괴후 교차",12}  판정");
        foreach (float depth in new[] { 7.3f, 7.6f, 7.9f, 8.3f, 9.0f, 11.0f })
        {
            var v = NewVol(Flat);
            float px = 100f, pz = 100f, surf = 10f;
            var b = SdfDeformer.SubtractSphere(v, new BlastRequest(px, surf - depth, pz, 7f));
            Span<float> ys = stackalloc float[8];
            int nBefore = Surfaces(v, px, pz, ys);
            float thick = nBefore >= 2 ? ys[0] - ys[1] : float.NaN;

            var cb = CeilingCollapse.Apply(v, b);
            int nAfter = Surfaces(v, px, pz, ys);

            bool thin = thick < CeilingCollapse.MinThickness;
            bool collapsed = nAfter < nBefore;
            bool ok = thin == collapsed;
            Console.WriteLine($"    {thick,9:F2}m {nBefore,12} {nAfter,12}  {YN(ok)} {(thin ? "얇음→붕괴 기대" : "두꺼움→유지 기대")}");
        }

        // ---------------------------------------------------------------
        Console.WriteLine("\n[2] 부유 덩어리 — 옆으로 파서 뜬 흙 만들기 (실전 시나리오)");
        {
            var v = NewVol(Flat);
            float pz = 100f, surf = 10f;
            // 지하를 옆으로 세 발 이어 파 긴 터널을 만든다 → 그 위 천장이 얇아진다
            var acc = new DirtyBounds { Empty = true };
            for (int n = 0; n < 3; n++)
            {
                var b = SdfDeformer.SubtractSphere(v, new BlastRequest(96f + n * 4f, surf - 7.6f, pz, 7f));
                acc = CeilingCollapse.Union(acc, b);
            }
            Span<float> ys = stackalloc float[8];
            int before = Surfaces(v, 100f, pz, ys);
            float thick = before >= 2 ? ys[0] - ys[1] : float.NaN;

            var sw = Stopwatch.StartNew();
            var cb = CeilingCollapse.Apply(v, acc);
            sw.Stop();
            int after = Surfaces(v, 100f, pz, ys);

            bool expectCollapse = thick < CeilingCollapse.MinThickness;
            Console.WriteLine($"    터널 3발 → 천장 {thick:F2}m ({(expectCollapse ? "얇음→붕괴 기대" : "두꺼움→유지 기대")}), 표면 교차 {before}개");
            Console.WriteLine($"    붕괴 패스 {sw.Elapsed.TotalMilliseconds:F2} ms → 표면 교차 {after}개  {YN(expectCollapse == (after < before))}");
            Console.WriteLine($"    붕괴가 넓힌 dirty 범위: {(cb.Empty ? "없음" : $"{cb.I1 - cb.I0}×{cb.J1 - cb.J0}×{cb.K1 - cb.K0} 복셀")}");
        }

        // ---------------------------------------------------------------
        Console.WriteLine("\n[3] 탱크 갇힘 — 크레이터에서 나올 수 있는가 (턱 넘기 1.2m)");
        Console.WriteLine($"    {"Rc",5} {"깊이",8} {"한걸음",8} {"밖까지",8}  판정");
        foreach (float rc in new[] { 2f, 3f, 5f, 7f, 10f, 14f })
        {
            var v = NewVol(Flat);
            float px = 100f, pz = 100f;
            SdfDeformer.SubtractSphere(v, new BlastRequest(px, 10f, pz, rc));
            var b2 = new DirtyBounds { I0 = v.GridX(px - rc - 1), I1 = v.GridX(px + rc + 1),
                                       J0 = v.GridY(10f - rc - 1), J1 = v.GridY(10f + rc + 1),
                                       K0 = v.GridZ(pz - rc - 1), K1 = v.GridZ(pz + rc + 1), Empty = false };
            CeilingCollapse.Apply(v, b2);

            float g = TankGroundProbe.GroundBelow(v, px, pz, 12f);
            int oneStep = TankGroundProbe.EscapeRoutes(v, px, pz, g);
            bool out_ = WalksOut(v, px, pz, g, rc);
            Console.WriteLine($"    {rc,4:F0}m {10f - g,7:F2}m {oneStep,6}/8 {(out_ ? "탈출" : "갇힘"),8}  {(out_ ? "✅" : "⚠️ HUD로 알릴 것")}");
        }

        // ---------------------------------------------------------------
        Console.WriteLine("\n[4] 네거티브 컨트롤 — 턱 넘기를 끄면(0m) 얕은 구덩이도 갇혀야 한다");
        {
            var v = NewVol(Flat);
            SdfDeformer.SubtractSphere(v, new BlastRequest(100f, 10f, 100f, 3f));
            float g = TankGroundProbe.GroundBelow(v, 100f, 100f, 12f);
            int withStep = TankGroundProbe.EscapeRoutes(v, 100f, 100f, g, stepHeight: TankGroundProbe.StepHeight);
            int noStep = TankGroundProbe.EscapeRoutes(v, 100f, 100f, g, stepHeight: 0.05f);
            Console.WriteLine($"    Rc=3m 구덩이:  턱1.2m → {withStep}/8 경로   턱0.05m → {noStep}/8 경로");
            Console.WriteLine($"    {YN(noStep < withStep)} 턱 넘기가 실제로 탈출을 만들어내고 있다 (같으면 측정이 고장 난 것)");
        }

        // ---------------------------------------------------------------
        Console.WriteLine("\n[5] 오버행 아래 탱크 — 위에서 찾으면 처마로 순간이동한다");
        {
            var v = NewVol(Hill);
            float px = 69f, pz = 100f;
            float surf = Hill(px, pz);
            SdfDeformer.SubtractSphere(v, new BlastRequest(px, surf - 7.5f, pz, 6f));
            Span<float> ys = stackalloc float[8];
            int n = Surfaces(v, px, pz, ys);
            if (n >= 3)
            {
                float insideY = (ys[1] + ys[2]) * 0.5f;              // 공동 한가운데 = 탱크가 있는 곳
                float below = TankGroundProbe.GroundBelow(v, px, pz, insideY);
                Console.WriteLine($"    처마 {ys[0]:F1}m / 공동 {ys[1]:F1}~{ys[2]:F1}m, 탱크 {insideY:F1}m");
                Console.WriteLine($"    GroundBelow → {below:F1}m  {YN(MathF.Abs(below - ys[2]) < 0.3f)} 공동 바닥을 찾았다 (처마 {ys[0]:F1}m 로 튀지 않음)");
            }
            else Console.WriteLine($"    오버행 생성 실패 (교차 {n}개) — 테스트 전제 불성립");
        }

        // ═══════════════════════════════════════════════════════════════
        //  [6] 등반 — **무엇이 막는가**  (§11 M0 게이트, 2026-09-19)
        //
        //  🚨 §11 M0 는 「**42° 경사에서 등반이 막히는가**」를 사람이 눈으로 볼 게이트로 적어 뒀다.
        //     그런데 그 문장은 **Heightfield 시절 기준**이다 — 그때는 안식각 릴랙세이션으로 벽을 42° 아래로
        //     눕혀서 «경사각»이 등반을 막았다. **SDF 로 바꾸면서 그 수법을 못 쓰게 됐고**(`TankGroundProbe` 머리말),
        //     지금 등반을 막는 것은 **경사각이 아니라 수직 턱 높이**(`StepHeight` 1.2m)다.
        //
        //  그래서 여기서는 **게이트 문장이 아니라 실제 규칙**을 쌍으로 잰다. 없는 규칙을 있다고 재면
        //  통과하든 실패하든 거짓말이다. 42° 가 실제로 어떻게 되는지도 같이 찍어 기록을 닫는다.
        // ═══════════════════════════════════════════════════════════════
        // ⚠️ `GameplayVerify` 는 원래 **전부 정보성**이라 ❌ 를 찍어도 rc 가 0 이었다(2026-09-19 발견).
        //    여기서 전체 계약을 바꾸지는 않는다 — 대신 **이 절의 단정만** 진짜 게이트로 만든다.
        //    (전체를 rc 에 물리는 건 기존 ❌ 유무를 먼저 재고 나서 할 일이다. 검수에 보고했다.)
        int hardFail = 0;
        Console.WriteLine("\n[6] 등반 — 경사각이 아니라 «수직 턱»이 막는다 (§11 M0)");
        {
            // 한 걸음(0.25m)에 오르는 높이 = WalkStep · tanθ. 턱(1.2m)을 넘으면 막힌다.
            Console.WriteLine($"    규칙: 한 걸음 {TankGroundProbe.WalkStep:F2}m · 넘는 턱 {TankGroundProbe.StepHeight:F2}m");
            Console.WriteLine($"    {"경사",6} {"걸음당 상승",12} {"판정",8}");
            foreach (float deg in new[] { 20f, 42f, 60f, 78f, 85f })
            {
                float rise = TankGroundProbe.WalkStep * MathF.Tan(deg * MathF.PI / 180f);
                var vol = NewVol((x, z) => MathF.Max(0f, (x - 100f)) * MathF.Tan(deg * MathF.PI / 180f));
                float g0 = TankGroundProbe.GroundBelow(vol, 100f, 100f, 60f);
                var r = TankGroundProbe.CanStepTo(vol, g0, 100f + TankGroundProbe.WalkStep, 100f, out _);
                Console.WriteLine($"    {deg,5:F0}° {rise,11:F2}m {(r == TankGroundProbe.MoveResult.Ok ? "오른다" : "막힌다"),8}");
            }

            // ── 쌍으로: 턱을 기준으로 **넘는 쪽과 막히는 쪽이 둘 다 나와야** 검사가 의미 있다 ──
            // 수직 벽 하나를 세우고 높이만 바꾼다(경사가 아니라 턱이 변수라는 걸 그대로 드러낸다).
            foreach (var (wall, expectOk) in new[] { (TankGroundProbe.StepHeight - 0.3f, true),
                                                     (TankGroundProbe.StepHeight + 0.3f, false) })
            {
                var vol = NewVol((x, z) => x > 100f ? wall : 0f);
                float g0 = TankGroundProbe.GroundBelow(vol, 99f, 100f, 60f);
                var r = TankGroundProbe.CanStepTo(vol, g0, 101f, 100f, out float g1);
                bool ok = (r == TankGroundProbe.MoveResult.Ok) == expectOk;
                if (!ok) hardFail++;
                Console.WriteLine($"    수직 턱 {wall:F1}m → {(r == TankGroundProbe.MoveResult.Ok ? "넘는다" : "막힌다"),6}"
                                + $"  {YN(ok)} {(expectOk ? "턱 이하라 넘어야 한다" : "턱 초과라 막혀야 한다")} (지면 {g0:F1}→{g1:F1}m)");
            }
        }
        if (hardFail > 0) { Console.WriteLine($"\n❌ [6] 등반 게이트 실패 {hardFail}건"); Environment.Exit(1); }
    }
}
