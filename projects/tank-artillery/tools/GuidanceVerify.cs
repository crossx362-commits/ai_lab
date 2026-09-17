// 초기 유도·자세 제어 단계(Sim/Guidance.cs)가 실제로 그 모습을 내는지 재는 하네스.
//
// 핵심 질문 셋:
//   (1) 발사 직후 **노즈가 속도 방향과 어긋나는가**(= 수직으로 선다). 안 어긋나면 그냥 포탄이다.
//   (2) 방향 전환이 끝나면 **속도 방향으로 되돌아오는가**. 안 돌아오면 옆으로 누워 날아간다.
//   (3) 연소가 끝나면 **TVC 가 0 이 되는가**. 추력 없는 노즐 편향은 거짓말이다.
//
// ⚠️ 네거티브 컨트롤: 캐논(추진 없음)에서도 노즈가 기울면 측정이 고장 난 것이다.

using System;
using Tankfall.Sim;

static class GuidanceVerify
{
    static int _fail;

    static void Ok(bool cond, string what, string detail = "")
    {
        Console.WriteLine($"  {(cond ? "✅" : "❌")} {what}{(detail.Length > 0 ? "   " + detail : "")}");
        if (!cond) _fail++;
    }

    /// <summary>
    /// 두 방향의 사잇각(도). **acos 를 쓰면 안 된다** — 0° 근처에서 acos 는 조건수가 폭발해
    /// 완전히 같은 벡터도 0.03° 로 나온다(이 하네스가 처음에 네거티브 컨트롤을 거짓 실패시킨 원인).
    /// 현(chord) 길이로 2·asin(|a−b|/2) 를 쓰면 작은 각에서 정확하다.
    /// </summary>
    static float Deg(Vec3 a, Vec3 b)
    {
        Vec3 x = a.Normalized, y = b.Normalized;
        float half = (x - y).Length * 0.5f;
        if (half > 1f) half = 1f;
        return 2f * MathF.Asin(half) * (180f / MathF.PI);
    }

    static float PitchDeg(Vec3 v) => MathF.Asin(Math.Clamp(v.Normalized.Y, -1f, 1f)) * (180f / MathF.PI);

    const float Dt = 1f / 120f;

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 미사일 초기 유도 · 자세 제어 단계 검증 ===\n");

        var accel = Ballistics.Accel(0f, 0f);
        float speed = Ballistics.PowerToSpeed(0.8f);

        foreach (var kind in new[] { TankKind.Missile, TankKind.SuperTank, TankKind.MultiMissile })
        {
            var fp = FlightProfile.Of(kind, ShellKind.Normal, 1f);
            var g = GuidanceProfile.Of(kind, fp);
            var v0 = Ballistics.VelocityFrom(0f, 45f, speed * fp.Mul);
            Vec3 launch = v0.Normalized;

            Console.WriteLine($"[{kind}] 정렬 {g.AlignSec:F2}s · 상승 {g.RiseSec:F2}s · 전환 {g.PitchSec:F2}s · 연소 {g.BoostSec:F2}s");
            Ok(g.Has, "유도 단계가 있다");
            Ok(g.PitchOverEnd <= g.BoostSec + 1e-3f, "자세 정렬이 연소 안에 끝난다",
               $"{g.PitchOverEnd:F2}s ≤ {g.BoostSec:F2}s");

            float maxMismatch = 0f, maxMismatchT = 0f, maxTvc = 0f, maxPitch = -90f;
            int insFix = 0, tvcAfterBurn = 0;
            float endMismatch = -1f;

            for (float t = 0f; t <= 3.0f; t += Dt)
            {
                Vec3 vel = Ballistics.VelocityAt(v0, accel, t, fp);
                var a = Guidance.At(t, Dt, vel, launch, g);

                float mism = Deg(a.Nose, vel);
                if (mism > maxMismatch) { maxMismatch = mism; maxMismatchT = t; }
                if (a.TvcDeg > maxTvc) maxTvc = a.TvcDeg;
                float np = PitchDeg(a.Nose);
                if (np > maxPitch) maxPitch = np;
                if (a.InsFix) insFix++;
                if (t > g.BoostSec + Dt && a.TvcDeg > 1e-4f) tvcAfterBurn++;
                if (t >= g.PitchOverEnd + 0.25f && endMismatch < 0f) endMismatch = mism;

                if (a.Nose.Length < 0.99f || a.Nose.Length > 1.01f) { Ok(false, "노즈가 단위벡터가 아니다", $"t={t:F2} |n|={a.Nose.Length:F3}"); break; }
                if (np > 90.5f) { Ok(false, "노즈가 수직을 넘었다(뒤집힘)", $"t={t:F2} pitch={np:F1}°"); break; }
            }

            Ok(maxMismatch > 12f, "(1) 발사 직후 노즈가 속도 방향과 어긋난다",
               $"최대 {maxMismatch:F1}° @ {maxMismatchT:F2}s");
            Ok(maxPitch > 45f + 8f, "    노즈가 발사각보다 하늘 쪽으로 선다", $"최대 피치 {maxPitch:F1}° (발사 45°)");
            Ok(endMismatch >= 0f && endMismatch < 4f, "(2) 전환 뒤 속도 방향으로 되돌아온다",
               $"전환+0.25s 에서 {endMismatch:F2}°");
            Ok(maxTvc > 1f && maxTvc <= g.MaxTvcDeg + 1e-3f, "    TVC 가 작동하고 한계를 안 넘는다",
               $"최대 {maxTvc:F1}° / 한계 {g.MaxTvcDeg:F0}°");
            Ok(tvcAfterBurn == 0, "(3) 연소 종료 뒤 TVC 가 0 이다", $"위반 {tvcAfterBurn} 프레임");
            Ok(insFix == 2, "    INS/GPS 보정이 정렬 완료·중간유도 진입 두 번만 뜬다", $"{insFix}회");
            Console.WriteLine();
        }

        Console.WriteLine("[네거티브 컨트롤] 추진 없는 기종은 자세 제어가 없어야 한다");
        foreach (var kind in new[] { TankKind.Cannon, TankKind.Laser, TankKind.CrossBow })
        {
            var fp = FlightProfile.Of(kind, ShellKind.Normal, 1f);
            var g = GuidanceProfile.Of(kind, fp);
            var v0 = Ballistics.VelocityFrom(0f, 45f, speed * fp.Mul);
            float worst = 0f, tvc = 0f, dacs = 0f;
            for (float t = 0f; t <= 2f; t += Dt)
            {
                Vec3 vel = Ballistics.VelocityAt(v0, accel, t, fp);
                var a = Guidance.At(t, Dt, vel, v0.Normalized, g);
                worst = MathF.Max(worst, Deg(a.Nose, vel));
                tvc = MathF.Max(tvc, a.TvcDeg); dacs = MathF.Max(dacs, a.Dacs);
            }
            Ok(!g.Has && worst < 1e-2f && tvc == 0f && dacs == 0f,
               $"{kind}: 노즈 = 속도 방향, 제어 장치 정지", $"어긋남 {worst:F3}°");
        }
        Console.WriteLine();

        Console.WriteLine("[결정론] 같은 t 는 언제 물어도 같은 자세");
        {
            var fp = FlightProfile.Of(TankKind.Missile, ShellKind.Normal, 1f);
            var g = GuidanceProfile.Of(TankKind.Missile, fp);
            var v0 = Ballistics.VelocityFrom(20f, 52f, speed * fp.Mul);
            bool same = true;
            for (float t = 0f; t <= 2f; t += 0.05f)
            {
                Vec3 vel = Ballistics.VelocityAt(v0, accel, t, fp);
                var a = Guidance.At(t, Dt, vel, v0.Normalized, g);
                var b = Guidance.At(t, Dt, vel, v0.Normalized, g);
                if (Deg(a.Nose, b.Nose) > 1e-4f || a.TvcDeg != b.TvcDeg || a.Dacs != b.Dacs) { same = false; break; }
            }
            Ok(same, "두 번 물어도 같은 값");
        }

        Console.WriteLine("\n[선회(중간 유도)] 유도 구간에서 날개·측추력기가 물린다");
        {
            var fp = FlightProfile.Of(TankKind.Missile, ShellKind.Special, 1f);
            var g = GuidanceProfile.Of(TankKind.Missile, fp);
            var v0 = Ballistics.VelocityFrom(0f, 45f, speed * fp.Mul);
            float t = g.BoostSec + 0.8f;
            Vec3 vel = Ballistics.VelocityAt(v0, accel, t, fp);
            var straight = Guidance.At(t, Dt, vel, v0.Normalized, g, 0f);
            var turning = Guidance.At(t, Dt, vel, v0.Normalized, g, 40f);
            Ok(straight.Stage == Guidance.StageAero && turning.Stage == Guidance.StageMidcourse,
               "선회율이 있으면 중간유도 단계로 바뀐다");
            Ok(MathF.Abs(turning.Fin) > MathF.Abs(straight.Fin) && turning.TvcDeg == 0f,
               "날개만 물리고 TVC 는 안 켜진다(연소 끝남)", $"fin {turning.Fin:F2}");
        }

        Console.WriteLine("\n[탄도 불변] 자세는 궤적에 손대지 않는다");
        {
            // Guidance 는 위치를 만들지 않는다 — 같은 입력에서 PositionAt 이 자세 호출 전후로 동일한지 본다.
            var fp = FlightProfile.Of(TankKind.Missile, ShellKind.Normal, 1f);
            var g = GuidanceProfile.Of(TankKind.Missile, fp);
            var p0 = new Vec3(100f, 12f, 20f);
            var v0 = Ballistics.VelocityFrom(0f, 45f, speed * fp.Mul);
            bool same = true;
            for (float t = 0f; t <= 3f; t += 0.05f)
            {
                var before = Ballistics.PositionAt(p0, v0, accel, t, fp);
                Guidance.At(t, Dt, Ballistics.VelocityAt(v0, accel, t, fp), v0.Normalized, g, 12f);
                var after = Ballistics.PositionAt(p0, v0, accel, t, fp);
                if ((after - before).Length > 1e-6f) { same = false; break; }
            }
            Ok(same, "자세를 계산해도 궤적이 그대로다");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "=== 통과 ===" : $"=== 실패 {_fail}건 ===");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
