// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §5
//
// === 적분하지 않는다 ===
// 기획서 §13이 공기저항을 빼기로 확정했으므로 가속도는 (중력 + 바람)의 **상수 벡터**이고
// 궤적에는 닫힌 해가 있다:  P(t) = P₀ + V₀t + ½at²
//
// 오일러 적분을 쓰면 프레임레이트에 따라 탄착점이 달라져 서버/클라가 어긋나고,
// AI 조준·궤적 프리뷰·리플레이가 각자 다른 코드가 된다. 이 함수 하나를 전부가 공유한다.

using System;

namespace Tankfall.Sim
{
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public float LengthSq => X * X + Y * Y + Z * Z;
        public Vec3 Normalized { get { float l = Length; return l < 1e-6f ? default : this * (1f / l); } }
        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    /// <summary>
    /// 비행 프로파일(오너 지시 2026-09-17 "미사일이 다 포물선으로 날아가지 말자").
    ///
    /// 두 축만 둔다. 둘 다 **결정론**이고 하나는 **닫힌 해**를 유지한다:
    ///   · 추진(Thrust·BoostSec): 발사 방향(dir₀, 상수)으로 BoostSec 동안 Thrust 가속. 가속도가 구간별 상수라
    ///     P(t) 는 여전히 닫힌 해다 — 추진 뒤 궤적은 "속도 s+Th·Tb 로 p₀−dir₀·½Th·Tb² 에서 쏜 포물선"과 **정확히 같다**.
    ///     그래서 조준 역산(SolveLaunchAngles 오버로드)이 근사가 아니라 등가 포물선으로 정확히 풀린다.
    ///   · 유도(HomingTurnDeg): 정점을 지난 뒤 사거리 안의 적 쪽으로 초당 N° 씩 꺾는다. 이 구간만 고정 스텝 적분이며
    ///     조준은 유도 없이 푼다(유도는 맞히는 쪽으로만 돕는다).
    /// SpeedMul 은 추진이 더한 사거리를 도로 깎아 **최대 사거리를 원래 값에 맞춘다**(= [6-0] 참가 자격·매치업 유지).
    /// 수치는 전부 [추정] — 원작은 2D 포물선뿐이라 근거가 없다. 매치업으로 조정하라.
    /// </summary>
    public struct FlightProfile
    {
        public float Thrust;          // m/s², 발사 방향
        public float BoostSec;        // 추진 시간
        public float HomingTurnDeg;   // 초당 선회각(0=유도 없음)
        public float HomingRange;     // 유도 표적 탐색 반경
        public float SpeedMul;        // 초기속도 배율(0 이면 1)

        public bool HasThrust => Thrust > 0f && BoostSec > 0f;
        public bool HasHoming => HomingTurnDeg > 0f;
        public float Mul => SpeedMul <= 0f ? 1f : SpeedMul;

        /// <summary>추진이 더하는 등가 속도만큼 초기속도를 깎아, 최대 파워 총속도를 원래 최대와 같게 맞춘다.</summary>
        static FlightProfile Boost(float thrust, float boostSec, float maxSpeed, float homing = 0f)
        {
            float add = thrust * boostSec;
            return new FlightProfile { Thrust = thrust, BoostSec = boostSec, HomingTurnDeg = homing, HomingRange = 70f,
                                       SpeedMul = MathF.Max(0.3f, 1f - add / maxSpeed) };
        }

        public static FlightProfile Of(TankKind kind, ShellKind shell, float powerScale)
        {
            float vmax = Ballistics.VelocityMax * powerScale;
            bool sp = shell == ShellKind.Special;
            switch (kind)
            {
                // ⚠️ 추진(Thrust·Boost)은 **탄종과 무관**하게 같다. AI 는 1번탄 궤적을 보고 탄종을 고르므로(§8 "탄도가 같으니
                //    조준을 다시 풀 필요가 없다") 2번탄이 다른 추진을 가지면 고른 근거가 거짓이 된다. 2번탄은 유도만 얹는다.
                case TankKind.Missile:      return Boost(22f, 1.2f, vmax, sp ? 55f : 0f);     // 로켓: 직진 후 낙하 · 2번 유도탄
                case TankKind.MultiMissile: return Boost(12f, 0.8f, vmax);
                case TankKind.SuperTank:    return Boost(26f, 1.1f, vmax, sp ? 75f : 0f);     // "핫도그" 강추진 · 9연 유도
                case TankKind.Laser:        return Boost(34f, 0.7f, vmax);                                          // 빔: 앞부분이 거의 직선
                case TankKind.SecWind:      return Boost(14f, 0.7f, vmax);
                case TankKind.CrossBow:     return Boost(9f, 0.5f, vmax);                                           // 볼트: 처음만 곧게
                default:                    return default;                                                          // 포탄·돌·물·지뢰·결정: 순수 포물선
            }
        }
    }

    public static class Ballistics
    {
        // --- §5-1 확정 상수 ---
        public const float Gravity = 30.0f;        // 현실 3배. 탄도를 굽히고 비행을 3~4초로 줄인다
        public const float VelocityMin = 25.0f;    // 파워 0
        public const float VelocityMax = 81.2f;    // 파워 100 → 45°에서 사거리 220m
        public const float WindCoeff = 0.35f;      // 바람 1당 0.35 m/s² (최대사거리에서 ≈2.6m 편차)
        public const float MaxFlightSec = 15.0f;
        public const float SimStep = 1f / 120f;

        /// <summary>
        /// 파워(0~1) → 초기속도. **제곱근 매핑**을 쓴다(§5-2).
        /// 사거리는 v₀²에 비례하므로 선형 매핑이면 "파워 50 = 사거리 절반"이 성립하지 않아 학습이 어렵다.
        /// 이렇게 하면 사거리가 파워에 정확히 선형이 된다.
        /// </summary>
        public static float PowerToSpeed(float power01)
        {
            float p = power01 < 0f ? 0f : (power01 > 1f ? 1f : power01);
            return MathF.Sqrt(VelocityMin * VelocityMin +
                              (VelocityMax * VelocityMax - VelocityMin * VelocityMin) * p);
        }

        /// <summary>중력 + 바람. 바람은 **수평 전용**(Y=0) — 수직 바람은 직관을 파괴한다(§5-3).</summary>
        public static Vec3 Accel(float windX, float windZ, float windScale = 1f)
            => new Vec3(windX * WindCoeff * windScale, -Gravity, windZ * WindCoeff * windScale);

        /// <summary>닫힌 해. 서버·AI·프리뷰·리플레이·킬캠이 전부 이 함수를 쓴다.</summary>
        public static Vec3 PositionAt(Vec3 p0, Vec3 v0, Vec3 accel, float t)
            => p0 + v0 * t + accel * (0.5f * t * t);

        /// <summary>프로파일 포함 닫힌 해. 추진 구간은 dir₀ 방향 등가속, 그 뒤는 추진이 준 속도로 포물선.</summary>
        public static Vec3 PositionAt(Vec3 p0, Vec3 v0, Vec3 accel, float t, in FlightProfile fp)
        {
            if (!fp.HasThrust) return PositionAt(p0, v0, accel, t);
            Vec3 dir = v0.Normalized;
            float tb = MathF.Min(t, fp.BoostSec);
            float boost = 0.5f * fp.Thrust * tb * tb + fp.Thrust * fp.BoostSec * MathF.Max(0f, t - fp.BoostSec);
            return p0 + v0 * t + dir * boost + accel * (0.5f * t * t);
        }

        /// <summary>프로파일 포함 속도(유도 구간 진입 시 초기값으로 쓴다).</summary>
        public static Vec3 VelocityAt(Vec3 v0, Vec3 accel, float t, in FlightProfile fp)
        {
            Vec3 v = v0 + accel * t;
            if (fp.HasThrust) v = v + v0.Normalized * (fp.Thrust * MathF.Min(t, fp.BoostSec));
            return v;
        }

        /// <summary>
        /// 프로파일을 아는 조준 역산. 추진은 **등가 포물선**(속도 s+Th·Tb, 원점 p₀−dir₀·½Th·Tb²)으로 정확히 바뀌는데
        /// dir₀ 가 해에 의존하므로 4회 고정점 반복한다(방향 변화가 작아 바로 수렴). 유도는 조준에 안 넣는다.
        /// </summary>
        public static bool SolveLaunchAngles(Vec3 from, Vec3 to, float speed, Vec3 accel, in FlightProfile fp,
                                             out AimSolution low, out AimSolution high)
        {
            if (!fp.HasThrust) return SolveLaunchAngles(from, to, speed, accel, out low, out high);
            float add = fp.Thrust * fp.BoostSec;
            float shift = 0.5f * fp.Thrust * fp.BoostSec * fp.BoostSec;
            low = default; high = default;
            Vec3 dirLo = (to - from).Normalized, dirHi = dirLo;
            for (int i = 0; i < 4; i++)
            {
                bool okLo = SolveLaunchAngles(from - dirLo * shift, to, speed + add, accel, out var lo, out _);
                bool okHi = SolveLaunchAngles(from - dirHi * shift, to, speed + add, accel, out _, out var hi);
                if (!okLo || !okHi) return false;
                low = lo; high = hi;
                dirLo = VelocityFrom(lo.YawDeg, lo.PitchDeg, 1f);
                dirHi = VelocityFrom(hi.YawDeg, hi.PitchDeg, 1f);
            }
            return true;
        }

        /// <summary>야우/피치(도) + 속도 → 초기 속도 벡터. 유니티 좌표계(+Z 전방, +Y 위).</summary>
        public static Vec3 VelocityFrom(float yawDeg, float pitchDeg, float speed)
        {
            float y = yawDeg * (MathF.PI / 180f), p = pitchDeg * (MathF.PI / 180f);
            float cp = MathF.Cos(p);
            return new Vec3(MathF.Sin(y) * cp, MathF.Sin(p), MathF.Cos(y) * cp) * speed;
        }

        /// <summary>발사 해 하나. 바람이 있으면 야우도 해마다 달라진다(바람 반대로 틀어 쏴야 한다).</summary>
        public struct AimSolution
        {
            public float YawDeg, PitchDeg;
        }

        /// <summary>
        /// 조준 역산(§5-7) — 목표를 맞히는 발사각을 구한다. **바람이 있어도 해석적으로 풀린다.**
        ///
        /// 핵심: 가속도 a 는 상수 벡터다. a 방향을 "아래"로 삼는 회전 좌표계에서는 바람이 사라지고
        ///       중력 G=|a| 하나만 있는 표준 포물선이 된다. 그 좌표계에서 각을 풀고 **벡터로 역회전**한다.
        ///
        /// ⚠️ 각도만 보정하고 야우를 목표 방향으로 두면 안 된다.
        ///    옆바람일 때 바람 반대로 틀어 쏘는 성분이 통째로 빠져 7~28m 빗나간다(실측으로 잡은 버그).
        ///
        /// 반환 false = 사거리 부족(파워를 올려야 한다).
        /// low = 저각 해, high = 고각 해. 지형에 막히지 않는 쪽을 호출자가 고른다.
        /// </summary>
        public static bool SolveLaunchAngles(Vec3 from, Vec3 to, float speed, Vec3 accel,
                                             out AimSolution low, out AimSolution high)
        {
            low = default; high = default;

            Vec3 d = to - from;
            float G = accel.Length;
            if (G < 1e-4f) return false;
            Vec3 down = accel * (1f / G);
            Vec3 up = down * -1f;

            // 회전 좌표계: y = 유효 "위" 성분, x = 유효 수평 거리
            float dDotDown = Vec3.Dot(d, down);
            float y = -dDotDown;
            Vec3 horizVec = d - down * dDotDown;
            float x = horizVec.Length;
            if (x < 1e-3f) return false;
            Vec3 horizDir = horizVec * (1f / x);

            float v2 = speed * speed;
            float disc = v2 * v2 - G * (G * x * x + 2f * y * v2);
            if (disc < 0f) return false;                       // 사거리 부족

            float root = MathF.Sqrt(disc);
            low = ToWorld(horizDir, up, MathF.Atan((v2 - root) / (G * x)));
            high = ToWorld(horizDir, up, MathF.Atan((v2 + root) / (G * x)));
            return true;
        }

        /// <summary>유효 좌표계의 각 θ → 월드 발사 방향 → 월드 야우/피치.</summary>
        static AimSolution ToWorld(Vec3 horizDir, Vec3 up, float theta)
        {
            Vec3 dir = horizDir * MathF.Cos(theta) + up * MathF.Sin(theta);
            float len = dir.Length;
            if (len > 1e-6f) dir = dir * (1f / len);
            return new AimSolution
            {
                PitchDeg = MathF.Asin(MathF.Max(-1f, MathF.Min(1f, dir.Y))) * (180f / MathF.PI),
                YawDeg = MathF.Atan2(dir.X, dir.Z) * (180f / MathF.PI),
            };
        }
    }
}
