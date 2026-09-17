// `Sim/AiMover.cs` 가 **실제로 일하는지** 재는 하네스.
//
// ⚠️ 검사마다 네거티브 컨트롤을 붙였다 — 고치기 **전에도** 통과가 나오면 측정이 고장 난 것이다.
//    이 프로젝트가 같은 함정에 세 번 빠졌다: "기능을 넣었다 ≠ 발동한다"(궁극기가 한 번도 발동
//    안 했는데 ON/OFF 승률이 숫자 하나까지 같게 나와서야 들통났다), "응답 성공 ≠ 지시 전달 성공",
//    "Popen 성공 ≠ 스크립트 실행 성공". 그래서 여기서는 **빨간불부터 확인한다.**
//
// 도는 법:  ./tools/verify.sh aimove
//   (verify.sh 에 한 줄:  aimove)  run_console AiMoveVerify $SIM/*.cs tools/AiMoveVerify.cs ;;

using System;
using System.Collections.Generic;
using Tankfall.Sim;

static class AiMoveVerify
{
    const float Voxel = 0.5f;
    const int ChunkN = 16;
    const float MapSize = 280f;                       // 밸런스 세션이 확정한 맵 크기
    const float Flat = 10f;

    static SdfVolume NewFlat() => new SdfVolume(Voxel, ChunkN, -20f, (x, z) => Flat, (int)(MapSize / Voxel));

    /// <summary>비탈. 사발 판정이 **비탈을 사발로 오인하지 않는지** 보는 대조군.</summary>
    static SdfVolume NewSlope() => new SdfVolume(Voxel, ChunkN, -20f, (x, z) => Flat + (x - 140f) * 0.35f, (int)(MapSize / Voxel));

    static int _fail;
    static void Test(string name, bool ok, string detail = "")
    {
        Console.WriteLine($"  {(ok ? "✅" : "❌")} {name}{(detail.Length > 0 ? "   " + detail : "")}");
        if (!ok) _fail++;
    }

    /// <summary>지정한 자리에 구덩이를 판다. 반환 = 바닥 높이.</summary>
    static float DigPit(SdfVolume v, float cx, float cz, float radius)
    {
        SdfDeformer.SubtractSphere(v, new BlastRequest(cx, Flat, cz, radius));
        return TankGroundProbe.GroundBelow(v, cx, cz, Flat + 40f);
    }

    /// <summary>한 턴씩 계획 → 걷기 를 반복한다. 반환 = 시작점에서 멀어진 거리.</summary>
    /// <param name="firstWhy">**첫** 판단 이유. 마지막 이유를 보면 이미 목적을 이룬 뒤라 늘 None/Wander 로 나와
    /// "무엇이 움직이게 했나"를 못 본다.</param>
    static float RunTurns(SdfVolume v, ref float x, ref float z, ref float y, int turns, ref Rng rng,
                          IReadOnlyList<HazardField.HazardView> hz, SupplyDrop sup,
                          IReadOnlyList<AiGunner.Target> foes, float maxRange, out MoveReason firstWhy)
    {
        float x0 = x, z0 = z;
        firstWhy = MoveReason.None;
        for (int t = 0; t < turns; t++)
        {
            var p = AiMover.Decide(v, x, y, z, true, maxRange, MapSize, hz, sup, foes, ref rng);
            if (firstWhy == MoveReason.None) firstWhy = p.Why;
            if (!p.Move) continue;
            AiMover.Walk(v, ref x, ref z, ref y, p.DirX, p.DirZ, p.Distance, MapSize);
        }
        return MathF.Sqrt((x - x0) * (x - x0) + (z - z0) * (z - z0));
    }

    /// <summary>
    /// **대조 정책**: 하네스가 그동안 쓰던 임의걸음(40% 턴에 아무 방향 8m). 게임의 AI 는 아예 안 움직였으니
    /// 이게 "이 파일이 없을 때 가장 잘 움직이는 쪽"이다 — 이걸 못 이기면 새 정책은 값이 없다.
    /// </summary>
    static bool WanderEscapes(SdfVolume v, float cx, float cz, float startY, int turns, ref Rng rng, out float endY)
    {
        float x = cx, z = cz; endY = startY;
        for (int t = 0; t < turns; t++)
        {
            if (rng.Float01() >= AiMover.WanderChance) continue;
            float a = rng.Range(0f, MathF.PI * 2f);
            AiMover.Walk(v, ref x, ref z, ref endY, MathF.Cos(a), MathF.Sin(a), AiMover.TurnDistance, MapSize);
        }
        return endY >= Flat - TankGroundProbe.StepHeight;
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== AI 이동(Sim/AiMover) 검증 ===\n");

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("[1] 사발 판정 — 구덩이만 사발이라고 해야 한다");
        {
            var flat = NewFlat();
            Test("평지는 사발이 아니다 (네거티브 컨트롤)",
                 !AiMover.InPit(flat, 140f, 140f, Flat, out _, out _));

            var slope = NewSlope();
            float sg = TankGroundProbe.GroundBelow(slope, 140f, 140f, Flat + 40f);
            Test("비탈은 사발이 아니다 (오르막만 높고 내리막이 있다)",
                 !AiMover.InPit(slope, 140f, 140f, sg, out _, out _));

            // 구덩이는 반경 8m 링이 전부 위에 있어야 하므로 그보다 크게 판다.
            var pit = NewFlat();
            float py = DigPit(pit, 140f, 140f, 11f);
            bool inPit = AiMover.InPit(pit, 140f, 140f, py, out _, out _);
            Test("구덩이 바닥은 사발이다", inPit, $"바닥 {py:F2}m (지표 {Flat:F0}m, 깊이 {Flat - py:F2}m)");
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[2] 구덩이 탈출 — **이게 이 파일의 본론**");
        Console.WriteLine("    (밸런스 세션이 이온어태커에서 본 '크게 팔수록 적이 자기 구덩이에 숨어 안 보인다'의 원인 후보)");
        {
            // ⚠️ "안 움직이면 못 나온다"를 대조군으로 쓰면 **0 == 0 인 동어반복**이다 — 그건 검사가 아니다.
            //    진짜 대조군은 **이 파일이 없을 때 가장 잘 움직이는 쪽**, 즉 하네스의 임의걸음이다.
            //    그걸 못 이기면 새 정책은 값이 없다. 시드 20개로 양쪽을 같은 지형에서 돌린다.
            const int Seeds = 20, Turns = 12;
            Console.WriteLine($"    {"반경",5} {"깊이",7} │ {"임의걸음 탈출",14} {"AiMover 탈출",14}");
            foreach (float r in new[] { 9f, 11f, 14f })
            {
                int wOut = 0, mOut = 0; float depth = 0f; var firstWhy = MoveReason.None;
                for (int s = 0; s < Seeds; s++)
                {
                    var a = NewFlat();
                    float ay = DigPit(a, 140f, 140f, r); depth = Flat - ay;
                    var r1 = new Rng((uint)(1000 + s));
                    if (WanderEscapes(a, 140f, 140f, ay, Turns, ref r1, out _)) wOut++;

                    var b = NewFlat();
                    float by = DigPit(b, 140f, 140f, r);
                    float bx = 140f, bz = 140f;
                    var r2 = new Rng((uint)(1000 + s));
                    RunTurns(b, ref bx, ref bz, ref by, Turns, ref r2, null, null, null, 220f, out var w);
                    if (s == 0) firstWhy = w;
                    if (by >= Flat - TankGroundProbe.StepHeight) mOut++;
                }
                Console.WriteLine($"    {r,4:F0}m {depth,6:F2}m │ {wOut,8}/{Seeds,-5} {mOut,8}/{Seeds,-5}  (첫 판단: {firstWhy})");
                Test($"  반경 {r:F0}m — 첫 판단이 CraterEscape 다", firstWhy == MoveReason.CraterEscape, $"{firstWhy}");
                // ⚠️ **20/20 을 요구하면 안 된다.** §7-6 은 "자동 구제(텔레포트·무료 이동·지형 복구)를 넣지 마라,
                //    깊은 구덩이는 전술적 위험으로 남긴다 — '적을 구덩이에 가두기'가 SDF 가 준 새 전술이다" 라고 못박는다.
                //    항상 나온다면 그 전술을 내가 지운 것이다. 재는 것은 "확실히 나온다"가 아니라
                //    **"이 파일이 없을 때(임의걸음)보다 뚜렷이 낫다"** 이다.
                Test($"  반경 {r:F0}m — 임의걸음보다 뚜렷이 낫다 (대조군)", mOut > wOut, $"AiMover {mOut} vs 임의걸음 {wOut}");
                Test($"  반경 {r:F0}m — 그래도 대부분 나온다 (70% 이상)", mOut * 100 >= Seeds * 70, $"{mOut}/{Seeds}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[3] 장판 탈출 — 불·독은 벗어나고, 지뢰는 장판이 아니다");
        {
            var v = NewFlat();
            var hz = new List<HazardField.HazardView>
            {
                new HazardField.HazardView { Kind = 1, X = 140f, Y = Flat, Z = 140f, Radius = 8f, TurnsLeft = 3 },
            };
            float x = 142f, z = 140f, y = Flat;     // 장판 안
            var rng = new Rng(7u);
            var p = AiMover.Decide(v, x, y, z, true, 220f, MapSize, hz, null, null, ref rng);
            Test("장판 안이면 HazardEscape 를 고른다", p.Why == MoveReason.HazardEscape, $"{p.Why}");
            Test("나가는 방향이 중심 반대쪽이다", p.DirX > 0.9f, $"dir=({p.DirX:F2},{p.DirZ:F2})");

            AiMover.Walk(v, ref x, ref z, ref y, p.DirX, p.DirZ, p.Distance, MapSize);
            float d = MathF.Sqrt((x - 140f) * (x - 140f) + (z - 140f) * (z - 140f));
            Test("한 턴에 장판 밖으로 나간다", d > 8f, $"중심에서 {d:F1}m (반경 8m)");

            // 네거티브 컨트롤 — 장판 밖이면 장판 때문에 움직이면 안 된다.
            float ox = 160f, oz = 140f;
            var rng2 = new Rng(7u);
            var p2 = AiMover.Decide(v, ox, Flat, oz, true, 220f, MapSize, hz, null, null, ref rng2);
            Test("장판 밖에서는 HazardEscape 가 안 뜬다 (네거티브 컨트롤)", p2.Why != MoveReason.HazardEscape, $"{p2.Why}");

            // 지뢰는 장판이 아니다 — 밟고 서 있어도 도망가지 않는다(다가가면 터지는 것이지 서 있으면 아니다).
            var mines = new List<HazardField.HazardView>
            {
                new HazardField.HazardView { Kind = 0, X = 140f, Y = Flat, Z = 140f, Radius = 8f },
            };
            var rng3 = new Rng(7u);
            var p3 = AiMover.Decide(v, 142f, Flat, 140f, true, 220f, MapSize, mines, null, null, ref rng3);
            Test("지뢰는 장판으로 안 센다 (Kind 0)", p3.Why != MoveReason.HazardEscape, $"{p3.Why}");
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[4] 보급 상자 — 원작에서 보급은 '이동으로 줍는' 것이다");
        {
            var v = NewFlat();
            var sup = new SupplyDrop();
            var rngD = new Rng(99u);
            // 실제 투하 경로를 그대로 쓴다(앵커에서 8~26m). 투하는 확률이라 나올 때까지 굴린다.
            var anchors = new List<Vec3> { new Vec3(140f, Flat, 140f) };
            for (int i = 0; i < 40 && sup.Count == 0; i++) sup.RollDrop(ref rngD, MapSize, (a, b) => Flat, anchors);
            Test("상자가 놓였다 (선행 조건)", sup.Count > 0, $"{sup.Count}개");

            if (sup.Count > 0)
            {
                sup.Nearest(140f, 140f, out float cx, out float cz, out float cd);
                float x = 140f, z = 140f, y = Flat;
                var rng = new Rng(3u);
                float before = cd;
                RunTurns(v, ref x, ref z, ref y, 8, ref rng, null, sup, null, 220f, out var why);
                sup.Nearest(x, z, out _, out _, out float after);
                Console.WriteLine($"    상자까지 {before:F1}m → {after:F1}m  ({why})");
                Test("상자 쪽으로 다가간다", after < before - 5f, $"{before:F1}m → {after:F1}m");

                // 네거티브 컨트롤 — 상자가 없으면 Supply 가 뜨면 안 된다.
                var empty = new SupplyDrop();
                var rng2 = new Rng(3u);
                var p = AiMover.Decide(v, 140f, Flat, 140f, true, 220f, MapSize, null, empty, null, ref rng2);
                Test("상자가 없으면 Supply 가 안 뜬다 (네거티브 컨트롤)", p.Why != MoveReason.Supply, $"{p.Why}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[5] 사거리 확보 — 못 닿는 적에게는 붙고, 닿으면 안 붙는다");
        {
            var v = NewFlat();
            var far = new List<AiGunner.Target> { new AiGunner.Target { Id = 9, Kind = TankKind.Cannon, Center = new Vec3(260f, Flat, 140f), Defense = 100f } };
            var rng = new Rng(5u);
            var p = AiMover.Decide(v, 40f, Flat, 140f, true, 150f, MapSize, null, null, far, ref rng);
            Test("사거리 밖 적에게 붙는다", p.Why == MoveReason.Range && p.DirX > 0.9f, $"{p.Why} dir=({p.DirX:F2},{p.DirZ:F2})");

            // 네거티브 컨트롤 — 이미 닿으면 Range 가 뜨면 안 된다.
            var rng2 = new Rng(5u);
            var p2 = AiMover.Decide(v, 200f, Flat, 140f, true, 150f, MapSize, null, null, far, ref rng2);
            Test("이미 사거리 안이면 Range 가 안 뜬다 (네거티브 컨트롤)", p2.Why != MoveReason.Range, $"{p2.Why}");
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[6] 결정론 · 속박 — 같은 시드면 같은 결과여야 한다");
        {
            var v = NewFlat();
            float ax = 140f, az = 140f, ay = Flat; var r1 = new Rng(4242u);
            float bx = 140f, bz = 140f, by = Flat; var r2 = new Rng(4242u);
            RunTurns(v, ref ax, ref az, ref ay, 20, ref r1, null, null, null, 220f, out _);
            RunTurns(v, ref bx, ref bz, ref by, 20, ref r2, null, null, null, 220f, out _);
            Test("같은 시드 → 같은 자리", MathF.Abs(ax - bx) < 1e-5f && MathF.Abs(az - bz) < 1e-5f, $"({ax:F3},{az:F3}) vs ({bx:F3},{bz:F3})");

            float cx = 140f, cz = 140f, cy = Flat; var r3 = new Rng(777u);
            RunTurns(v, ref cx, ref cz, ref cy, 20, ref r3, null, null, null, 220f, out _);
            Test("다른 시드 → 다른 자리 (난수가 실제로 도는가)",
                 MathF.Abs(ax - cx) > 1e-3f || MathF.Abs(az - cz) > 1e-3f, $"({ax:F1},{az:F1}) vs ({cx:F1},{cz:F1})");

            var rng = new Rng(1u);
            var pr = AiMover.Decide(v, 140f, Flat, 140f, false, 220f, MapSize, null, null, null, ref rng);
            Test("속박(CanMove=false)이면 안 움직인다", !pr.Move && pr.Why == MoveReason.CannotMove, $"{pr.Why}");
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[7] 걸음 크기 — 게임과 하네스가 갈라졌던 그 자리");
        {
            // 2026-09-16 사고: 하네스는 1m, 게임은 0.25m 로 걸어 하네스가 같은 턱에 더 자주 막혔다.
            // Walk 가 단일 소스가 됐으니 상수 자체를 검사해 재발을 막는다.
            Test("Walk 는 TankGroundProbe.WalkStep 을 쓴다", MathF.Abs(TankGroundProbe.WalkStep - 0.25f) < 1e-6f,
                 $"WalkStep={TankGroundProbe.WalkStep}m");

            // 큰 걸음이면 실제로 더 막히는가.
            //
            // ⚠️ 처음엔 반경 11m 구덩이 하나로만 쟀다. 0.25m 도 1m 도 **직선 한 번으로는 0/8** 이라
            //    `okSmall >= okBig` 가 `0 >= 0` 으로 통과했다 — **판별력 0 인 가짜 초록불**이다.
            //    (반경 11m 은 [2]에서 턴마다 방향을 다시 고르기 때문에 나오는 것이지 직선으로는 못 나온다.)
            //    TankGroundProbe 머리말의 실측이 말하는 판별 구간은 **작은 크레이터**다:
            //    가장자리 ε 구간 상승량이 √(2Rε) 라 R 이 작을수록 걸음 크기가 갈린다.
            //    그래서 여러 반경을 훑고, **어느 반경에서도 안 갈리면 실패로 찍는다.**
            var v0 = NewFlat();
            int discriminating = 0;
            Console.WriteLine($"    {"반경",5} │ {"0.25m 걸음",11} {"1m 걸음",9}");
            foreach (float r in new[] { 2.5f, 3f, 4f, 5f, 6f })
            {
                var v = NewFlat();
                float py = DigPit(v, 140f, 140f, r);
                int okSmall = 0, okBig = 0;
                for (int d = 0; d < 8; d++)
                {
                    float a = d * (MathF.PI * 2f / 8f), dx = MathF.Cos(a), dz = MathF.Sin(a);
                    float x1 = 140f, z1 = 140f, y1 = py;
                    AiMover.Walk(v, ref x1, ref z1, ref y1, dx, dz, r * 3f + 6f, MapSize);
                    if (y1 >= Flat - TankGroundProbe.StepHeight) okSmall++;
                    // 옛 하네스의 1m 걸음을 그대로 흉내 낸다.
                    float x2 = 140f, z2 = 140f, y2 = py;
                    for (int k = 0; k < (int)(r * 3f + 6f); k++)
                    {
                        if (TankGroundProbe.CanStepTo(v, y2, x2 + dx, z2 + dz, out float ny) != TankGroundProbe.MoveResult.Ok) break;
                        x2 += dx; z2 += dz; y2 = ny;
                    }
                    if (y2 >= Flat - TankGroundProbe.StepHeight) okBig++;
                }
                Console.WriteLine($"    {r,4:F1}m │ {okSmall,7}/8   {okBig,5}/8{(okSmall > okBig ? "   ← 갈린다" : "")}");
                if (okSmall > okBig) discriminating++;
                Test($"  반경 {r:F1}m — 큰 걸음이 작은 걸음을 못 이긴다", okBig <= okSmall, $"{okSmall} vs {okBig}");
            }
            Test("걸음 크기가 실제로 결과를 가르는 반경이 있다 (판별력 검사)", discriminating > 0,
                 discriminating > 0 ? $"{discriminating}개 반경에서 갈림" : "어느 반경에서도 안 갈림 — 이 검사는 가짜 초록불이다");
        }

        Console.WriteLine($"\n=== {(_fail == 0 ? "전부 통과" : $"실패 {_fail}건")} ===");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
