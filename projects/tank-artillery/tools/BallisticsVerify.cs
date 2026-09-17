// §5 탄도·조준 역산이 실제로 맞는지 재는 하네스.
//
// 핵심 질문: AI 가 빗나가는 게 **의도한 난이도 오차** 때문인가, **공식 버그** 때문인가.
// 오차를 0으로 두고 쏠서 목표에 얼마나 붙는지 보면 갈린다.
//
// ⚠️ 네거티브 컨트롤: 일부러 5° 틀어 쏌 탄이 잘 맞으면 측정이 고장 난 것이다.

using System;
using System.Collections.Generic;
using Tankfall.Sim;

static class BallisticsVerify
{
    const float Voxel = 0.5f, MapSize = MapHeightFunction.MapSize;   // 단일 소스를 따른다
    const int ChunkN = 16;

    static float Flat(float x, float z) => 10f;
    static float Hills(float x, float z) => MapHeightFunction.TwinHills(x, z);

    static SdfVolume Vol(Func<float, float, float> h)
        => new SdfVolume(Voxel, ChunkN, -20f, h, (int)(MapSize / Voxel));

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== §5 탄도 · 조준 역산 검증 ===\n");

        Console.WriteLine("[1] 사거리 표 (평지, 45°, 바람 0)");
        Console.WriteLine($"    {"파워",5} {"v0",8} {"이론사거리",11} {"실측사거리",11} {"비행",7}");
        var vol = Vol(Flat);
        foreach (float p in new[] { 0.25f, 0.5f, 0.75f, 1.0f })
        {
            float v = Ballistics.PowerToSpeed(p);
            float theory = v * v / Ballistics.Gravity;
            var p0 = new Vec3(100f, 12f, 20f);
            var v0 = Ballistics.VelocityFrom(0f, 45f, v);
            var acc = Ballistics.Accel(0f, 0f);
            var r = ProjectileSimulator.Simulate(vol, p0, v0, acc, null, -1, MapSize);
            float actual = r.Hit ? (r.Impact - p0).Length : 0f;
            string note = r.Hit ? "" : "  ← 맵(200m) 밖으로 나감. 최대사거리 220m 설계상 정상";
            Console.WriteLine($"    {p * 100,4:F0} {v,7:F1}m/s {theory,10:F0}m {actual,10:F0}m {r.FlightTime,6:F2}s{note}");
        }
        Console.WriteLine("    ※ 실측이 이론보다 약간 큰 것은 발사점이 지면보다 높기 때문 (정상)\n");

        Console.WriteLine("[2] ★ 조준 역산 정확도 (오차 0). 이게 크면 공식 버그다");
        Console.WriteLine($"    {"지형",6} {"거리",7} {"바람",7} {"해",5} {"파워",6} {"각도",7} {"목표와 거리",12}");
        int solved = 0, total = 0, good = 0;
        foreach (var (name, hf) in new (string, Func<float, float, float>)[] { ("평지", Flat), ("언덕", Hills) })
        {
            var v = Vol(hf);
            foreach (float dist in new[] { 40f, 80f, 120f, 160f })
                foreach (var (wx, wz, wn) in new[] { (0f, 0f, "없음"), (6f, 0f, "→6"), (-4f, 5f, "↖6.4") })
                {
                    total++;
                    float sx = 100f, sz = 25f;
                    float tx = 100f, tz = sz + dist;
                    var from = new Vec3(sx, hf(sx, sz) + 2.5f, sz);
                    var to = new Vec3(tx, hf(tx, tz) + 1.2f, tz);
                    var acc = Ballistics.Accel(wx, wz);
                    bool ok = false;
                    for (float pw = 0.3f; pw <= 1.0f; pw += 0.05f)
                    {
                        float sp = Ballistics.PowerToSpeed(pw);
                        if (!Ballistics.SolveLaunchAngles(from, to, sp, acc, out var lo, out var hi))
                            continue;
                        foreach (var sol in new[] { hi, lo })
                        {
                            float pitch = sol.PitchDeg;
                            if (pitch < -5f || pitch > 80f) continue;
                            var vel = Ballistics.VelocityFrom(sol.YawDeg, pitch, sp);
                            var r = ProjectileSimulator.Simulate(v, from, vel, acc, null, -1, MapSize);
                            if (!r.Hit) continue;
                            float miss = (r.Impact - to).Length;
                            solved++;
                            if (miss < 3f) good++;
                            Console.WriteLine($"    {name,5} {dist,6:F0}m {wn,6} {(pitch == hi.PitchDeg ? "고각" : "저각"),5} {pw * 100,5:F0} {pitch,6:F1}° {miss,10:F2}m {(miss < 3f ? "✅" : "⚠️")}");
                            ok = true; break;
                        }
                        if (ok) break;
                    }
                    if (!ok) Console.WriteLine($"    {name,5} {dist,6:F0}m {wn,6}   해 없음 ❌");
                }
        }
        Console.WriteLine($"    → {total}개 중 {solved}개 해 찾음, {good}개가 3m 이내\n");

        Console.WriteLine("[3] 네거티브 컨트롤 (같은 상황에서 각도만 5° 틀어 쏌다)");
        {
            var v = Vol(Flat);
            var from = new Vec3(100f, 12.5f, 25f);
            var to = new Vec3(100f, 11.2f, 145f);
            var acc = Ballistics.Accel(0f, 0f);
            float sp = Ballistics.PowerToSpeed(0.8f);
            Ballistics.SolveLaunchAngles(from, to, sp, acc, out var lo, out var hi);
            foreach (var (pitch, tag) in new[] { (lo.PitchDeg, "정답(저각)"), (lo.PitchDeg + 5f, "+5° 틀림"), (lo.PitchDeg - 5f, "−5° 틀림") })
            {
                var vel = Ballistics.VelocityFrom(lo.YawDeg, pitch, sp);
                var r = ProjectileSimulator.Simulate(v, from, vel, acc, null, -1, MapSize);
                float miss = r.Hit ? (r.Impact - to).Length : -1f;
                Console.WriteLine($"    {tag,-12} 각 {pitch,5:F1}° → 오차 {miss,7:F1}m");
            }
            Console.WriteLine("    ※ 정답이 작고 ±5°가 크게 빗나가야 정상");
        }

        Console.WriteLine("\n[3-1] ★ 추진 프로파일 조준 역산 — 등가 포물선이 정확하면 오차가 순수 포물선과 같아야 한다");
        {
            var v = Vol(Flat);
            var from = new Vec3(100f, 12f, 20f);
            bool allOk = true;
            foreach (var kind in new[] { TankKind.Missile, TankKind.SuperTank, TankKind.Laser, TankKind.CrossBow })
            {
                var st = TankStats.For(kind, ShellKind.Normal);
                var acc = st.AccelWith(3f, 0f);
                var fp = st.Flight;
                // 네거티브 컨트롤: 프로파일을 **무시하고** 풀면 크게 빗나가야 한다(추진이 실제로 궤적을 바꾼다는 증거)
                float worstNaive = 0f, worstProfile = 0f;
                foreach (float dist in new[] { 80f, 120f, 160f })
                {
                    // ⚠️ 표적은 **지면 높이(Flat=10)** 에 둔다. 발사점 높이(12)에 두면 탄은 2m 아래 지면에 먼저 닿아
                    //    저각 해일수록 수 m 더 나간 자리에 떨어진다 — 그건 역산 오차가 아니라 표적 정의 오류다(처음 그렇게 10m 가 나왔다).
                    var to = new Vec3(100f, 10.05f, 20f + dist);
                    float sp = st.SpeedAt(0.85f);
                    if (Ballistics.SolveLaunchAngles(from, to, sp, acc, out var nlo, out _))
                    {
                        var r = ProjectileSimulator.Simulate(v, from, Ballistics.VelocityFrom(nlo.YawDeg, nlo.PitchDeg, sp), acc, null, -1, MapSize, fp, null);
                        if (r.Hit) worstNaive = MathF.Max(worstNaive, (r.Impact - to).Length);
                    }
                    if (!Ballistics.SolveLaunchAngles(from, to, sp, acc, fp, out var lo, out _)) { Console.WriteLine($"    {st.Name} {dist}m: 해 없음"); allOk = false; continue; }
                    var rp = ProjectileSimulator.Simulate(v, from, Ballistics.VelocityFrom(lo.YawDeg, lo.PitchDeg, sp), acc, null, -1, MapSize, fp, null);
                    float miss = rp.Hit ? (rp.Impact - to).Length : 999f;
                    worstProfile = MathF.Max(worstProfile, miss);
                }
                bool ok = worstProfile < 2.5f && worstNaive > worstProfile + 3f;
                allOk &= ok;
                Console.WriteLine($"    {(ok ? "✅" : "❌")} {st.Name,-8} 추진 {fp.Thrust:F0}m/s²×{fp.BoostSec:F1}s  프로파일 역산 오차 {worstProfile,5:F1}m   (무시하면 {worstNaive,5:F1}m)");
            }
            if (!allOk) { Console.WriteLine("    ❌ 추진 프로파일 역산 실패"); Environment.Exit(1); }
        }

        Console.WriteLine("\n[4] 바람 편차 — 파워 70(사거리 ~172m). 파워 100 은 맵 밖이라 쟀 수 없다");
        {
            var v = Vol(Flat);
            var from = new Vec3(100f, 12f, 20f);
            float sp = Ballistics.PowerToSpeed(0.7f);
            var baseVel = Ballistics.VelocityFrom(0f, 45f, sp);
            var r0 = ProjectileSimulator.Simulate(v, from, baseVel, Ballistics.Accel(0f, 0f), null, -1, MapSize);
            foreach (float w in new[] { 1f, 5f, 10f })
            {
                var r = ProjectileSimulator.Simulate(v, from, baseVel, Ballistics.Accel(w, 0f), null, -1, MapSize);
                if (!r.Hit || !r0.Hit) { Console.WriteLine($"    바람 {w,4:F0} → 측정 불가(맵 밖)"); continue; }
                float drift = (r.Impact - r0.Impact).Length;
                float expect = 0.5f * Ballistics.WindCoeff * w * r0.FlightTime * r0.FlightTime;
                Console.WriteLine($"    바람 {w,4:F0} → 편차 {drift,6:F1}m   (½·a·t² 예상 {expect,5:F1}m)");
            }
        }
    }
}
