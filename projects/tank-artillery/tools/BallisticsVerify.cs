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
        // 🚨 2026-09-19: 이 파일은 **[3-1] 추진 프로파일 하나만** rc 를 냈다. §5 조준 역산 24발이
        //    전부 「해 없음 ❌」 이어도 `verify.sh ball` 은 **0 을 반환**했다(거짓 초록불).
        //    물리는 기준은 «쌍으로 확인할 수 있는가» — [1] 사거리 표처럼 문턱의 근거가 없는 절은
        //    `[정보]` 로 찍고 rc 에 안 물린다.
        int Fail = 0;
        Console.WriteLine("=== §5 탄도 · 조준 역산 검증 ===\n");

        // [정보] — 사거리 표는 «얼마나 나가는가»의 기록이지 단정이 아니다. 문턱의 근거가 없다.
        Console.WriteLine("[1] 사거리 표 (평지, 45°, 바람 0)  [정보]");
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

        // ── [1-1] 기종별 최대 사거리 ──────────────────────────────────────────────
        // 🚨 왜 있나(2026-09-19, 오너 지시 「발사체 궤적 다양하게」). 궤적을 기종별로 가르면
        //    **모양만 바뀌고 사거리는 안 바뀌어야** 한다 — `FlightProfile.Boost()` 가
        //    `SpeedMul = 1 − (thrust·boostSec)/vmax` 로 최대 파워 총속도를 원래 값에 맞추는 게 그 약속이다.
        //    그 약속이 지켜지는지는 **말이 아니라 이 표**로 본다. 프로파일을 건드린 커밋은
        //    이 표의 전/후를 같이 붙여야 한다.
        // ⚠️ 「추진 없음」과 완전히 같지는 않다 — 추진은 발사 방향으로 거리를 «미리» 벌어 놓으므로
        //    등가 포물선의 원점이 p₀ 뒤로 ½·Th·Tb² 만큼 밀린다(구조체 머리말). 그래서 **차이의 절댓값이
        //    아니라 «전 커밋 대비 변화»가 판정 기준**이다. 여기서 문턱을 단정하지 않는다. [정보]
        Console.WriteLine("[1-1] 기종별 최대 사거리 (평지, 파워 100, 45°, 바람 0)  [정보]");
        Console.WriteLine($"    {"기종",-12} {"추진",16} {"v0",9} {"사거리",9} {"추진끔",9} {"차이",8}");
        var volK = Vol(Flat);
        foreach (TankKind k in Enum.GetValues(typeof(TankKind)))
        {
            var st = TankStats.For(k);   // Get() 은 원본 행이라 Flight 가 안 채워진다 — For() 가 FlightProfile.Of 를 물린다
            float v = st.SpeedAt(1f);
            var pk = new Vec3(100f, 12f, 20f);
            var acck = Ballistics.Accel(0f, 0f) * st.GravityScale;
            var fpk = st.Flight;
            // 같은 기종을 «프로파일 켜고» / «끄고» 나란히 — 네거티브 컨트롤은 쌍으로 본다.
            var on  = ProjectileSimulator.Simulate(volK, pk, Ballistics.VelocityFrom(0f, 45f, v), acck, null, -1, MapSize, fpk, null, null);
            float vOff = Ballistics.PowerToSpeed(1f) * st.PowerScale;      // SpeedMul 을 뺀 원래 초기속도
            var off = ProjectileSimulator.Simulate(volK, pk, Ballistics.VelocityFrom(0f, 45f, vOff), acck, null, -1, MapSize, default, null, null);
            float rOn  = on.Hit  ? (on.Impact  - pk).Length : -1f;
            float rOff = off.Hit ? (off.Impact - pk).Length : -1f;
            string thr = fpk.HasThrust ? $"{fpk.Thrust,5:F0}m/s² ×{fpk.BoostSec,4:F1}s" : "         없음   ";
            string dif = (rOn >= 0f && rOff >= 0f) ? $"{rOn - rOff,6:F1}m" : "   맵밖";
            Console.WriteLine($"    {st.Name,-12} {thr,16} {v,7:F1}m/s {(rOn >= 0f ? $"{rOn,7:F0}m" : "   맵밖"),9} {(rOff >= 0f ? $"{rOff,7:F0}m" : "   맵밖"),9} {dif,8}");
        }
        Console.WriteLine();

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
                    // 🚨 예전엔 여기서 「해 없음 ❌」 를 **찍기만** 했다 — 24발이 전부 실패해도 rc=0 이었다.
                    //    §5 조준 역산은 이 게임의 1순위(포격 감각)를 떠받치는 식이다. 물린다.
                    if (!ok) { Console.WriteLine($"    {name,5} {dist,6:F0}m {wn,6}   해 없음 ❌"); Fail++; }
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
            float right = -1f, worst = -1f;
            foreach (var (pitch, tag) in new[] { (lo.PitchDeg, "정답(저각)"), (lo.PitchDeg + 5f, "+5° 틀림"), (lo.PitchDeg - 5f, "−5° 틀림") })
            {
                var vel = Ballistics.VelocityFrom(lo.YawDeg, pitch, sp);
                var r = ProjectileSimulator.Simulate(v, from, vel, acc, null, -1, MapSize);
                float miss = r.Hit ? (r.Impact - to).Length : -1f;
                Console.WriteLine($"    {tag,-12} 각 {pitch,5:F1}° → 오차 {miss,7:F1}m");
                if (tag.StartsWith("정답")) right = miss; else worst = MathF.Min(worst < 0f ? miss : worst, miss);
            }
            // 🚨 예전엔 「※ 정답이 작고 ±5°가 크게 빗나가야 정상」이라고 **적어만** 뒀다 — 사람이 읽고
            //    판단하라는 뜻인데 아무도 안 읽는다. 그 문장을 그대로 단정으로 옮긴다.
            //    ⚠️ 이 절 자체가 «역산이 상수를 내놓는 것이 아님»을 보이는 대조군이다 —
            //       역산이 고장나 아무 각이나 내놓아도 [2] 는 통과할 수 있지만 여기는 못 지나간다.
            bool aimOk = right >= 0f && worst > 0f && right < 5f && worst > right + 10f;
            Console.WriteLine($"    {(aimOk ? "✅" : "❌")} 정답 {right:F1}m 이 ±5° 최소 오차 {worst:F1}m 보다 뚜렷이 작다");
            if (!aimOk) Fail++;
        }

        // ── [1-2] 궤적 덤프 ────────────────────────────────────────────────────────
        // 오너 지시(「궤적 다양하게」)의 **확인 수단**이다. 13종을 **같은 자리·같은 파워·같은 각도**로 쏜
        // 궤적을 CSV 로 뱉는다 — 겹쳐 그리면 갈렸는지 1초에 보인다.
        // ⚠️ 화면이 아니라 여기서 뽑는 게 맞다: `BattleDemo.FlyStep` 은 Sim 이 계산한 `_shotPath` 를
        //    **재생만** 하므로 **이 경로가 곧 화면에 보이는 경로**다(카메라·연출이 안 끼어든다).
        // `TANKFALL_PATHS=<파일>` 이 있을 때만 쓴다(평소 출력엔 안 낀다).
        {
            string dump = Environment.GetEnvironmentVariable("TANKFALL_PATHS");
            if (!string.IsNullOrEmpty(dump))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("kind,t,x,y");
                var volD = Vol(Flat);
                var pD = new Vec3(100f, 12f, 20f);
                foreach (TankKind k in Enum.GetValues(typeof(TankKind)))
                {
                    var st = TankStats.For(k, ShellKind.Normal);
                    float v = st.SpeedAt(0.85f);                     // 같은 파워
                    var acc = st.AccelWith(0f, 0f);
                    var v0 = Ballistics.VelocityFrom(0f, 45f, v);    // 같은 각도
                    var r = ProjectileSimulator.Simulate(volD, pD, v0, acc, null, -1, MapSize, st.Flight, null, null);
                    for (int i = 0; i < r.Path.Count; i++)
                        sb.AppendLine($"{st.Name},{i * Ballistics.SimStep:F3},{r.Path[i].Z - pD.Z:F3},{r.Path[i].Y - pD.Y:F3}");
                }
                System.IO.File.WriteAllText(dump, sb.ToString());
                Console.WriteLine($"[1-2] 궤적 덤프 → {dump}  (13종 · 같은 자리·파워 85·45°)\n");
            }
        }

        Console.WriteLine("\n[3-1] ★ 추진 프로파일 조준 역산 — 등가 포물선이 정확하면 오차가 순수 포물선과 같아야 한다");
        {
            var v = Vol(Flat);
            var from = new Vec3(100f, 12f, 20f);
            bool allOk = true;
            // 🚨 **기종 목록을 손으로 적어 두면 새 프로파일이 게이트를 안 탄다**(2026-09-19).
            //    오너 지시로 7종에 프로파일이 생겼는데 이 절은 넷만 보고 있었다 — 특히 **음수 추진(감속)** 은
            //    완전히 새 경로인데 **검증 0회**로 통과할 뻔했다. 목록 대신 **`HasThrust` 인 전부**를 돈다.
            // ⚠️ 네거티브 컨트롤(`worstNaive > worstProfile + 3f`)은 여기서 **두 가지를 동시에** 본다:
            //      ① 역산이 프로파일을 실제로 반영하는가 ② **그 프로파일이 궤적을 눈에 띄게 바꾸는가.**
            //    ②가 깨지면(=무시해도 3m 안 틀림) 「사람 눈에도 안 보이는 프로파일」이라는 뜻이다 —
            //    그건 게이트 실패로 다루는 게 맞다. 궤적 다양화의 목적 자체가 «보이는 것»이니까.
            foreach (TankKind kind in Enum.GetValues(typeof(TankKind)))
            {
                var st = TankStats.For(kind, ShellKind.Normal);
                if (!st.Flight.HasThrust) continue;      // 캐논 등 순수 포물선은 여기 대상이 아니다
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
            if (!allOk) { Console.WriteLine("    ❌ 추진 프로파일 역산 실패"); Fail++; }
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
                // 🚨 예전엔 측정과 예상을 나란히 **찍기만** 했다. 사람이 눈으로 비교하라는 뜻인데 안 읽는다.
                //    §5-3 의 주장은 「바람은 ½·a·t² 로 편차를 만든다」 — 측정이 그 식과 맞는지가 단정이다.
                //    ⚠️ 여기는 **이론값이 내장 대조군**이다. 식이 틀리면 둘이 갈라진다.
                bool wOk = MathF.Abs(drift - expect) < MathF.Max(0.5f, expect * 0.08f);
                Console.WriteLine($"    {(wOk ? "✅" : "❌")} 바람 {w,4:F0} → 편차 {drift,6:F1}m   (½·a·t² 예상 {expect,5:F1}m)");
                if (!wOk) Fail++;
            }
        }
    
        Console.WriteLine(Fail == 0 ? "\n✅ 탄도 게이트 통과" : $"\n❌ 탄도 게이트 실패 {Fail}건");
        Environment.Exit(Fail == 0 ? 0 : 1);
    }
}
