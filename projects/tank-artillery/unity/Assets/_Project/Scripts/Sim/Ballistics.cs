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
    /// SpeedMul 은 추진이 더한 **속도**를 도로 깎아 **최대 파워 총속도를 원래 값에 맞춘다**.
    ///
    /// 🚨 **여기 「최대 사거리를 원래 값에 맞춘다」고 적혀 있었다 — 정확하지 않다**(2026-09-19 실측,
    ///    `BallisticsVerify` [1-1] 표). 맞춰지는 것은 **속도**지 **사거리**가 아니다.
    ///    추진은 발사 방향으로 거리를 «미리» 벌어 놓으므로 등가 포물선의 원점이 p₀ 뒤로 ½·Th·Tb² 밀리고,
    ///    그만큼 p₀ 에서 잰 사거리가 **줄어든다**(실측 손실 ≈ 0.7·Th·Tb²):
    ///      크로스보우 −1.6m(0.7%) · 세크윈드 −4.8m · 멀티미사일 −5.4m · 레이저 −11.8m · **미사일 −22.7m(9%)**
    ///    즉 **추진을 주면 사거리를 «조금 판다».** 프로파일을 새로 넣거나 고칠 땐 [1-1] 표의 전/후를 같이 봐라.
    ///
    /// 🔑 **보이는 탄 = 시뮬한 탄.** 화면(`BattleDemo.FlyStep`)은 자기 물리를 돌리지 않고 Sim 이 미리 계산한
    ///    `_shotPath` 를 **재생만** 한다. 그래서 여기만 고치면 화면·AI·역산·하네스가 전부 같이 움직인다.
    ///    ⚠️ **View 에 물리를 더하지 마라** — 그 순간 「믿음과 현실이 다른 하늘」이 다섯 번째로 생긴다.
    /// 수치는 전부 [추정] — 원작은 2D 포물선뿐이라 근거가 없다. 매치업으로 조정하라.
    /// </summary>
    public struct FlightProfile
    {
        public float Thrust;          // m/s², 발사 방향
        public float BoostSec;        // 추진 시간
        public float HomingTurnDeg;   // 초당 선회각(0=유도 없음)
        public float HomingRange;     // 유도 표적 탐색 반경
        public float SpeedMul;        // 초기속도 배율(0 이면 1)
        /// <summary>
        /// **전 비행 구간**에 걸리는 수평 상수 가속(m/s²). +는 «앞으로 미는», −는 «끄는» 힘이다.
        ///
        /// 🔑 **이게 궤적 «모양»을 가르는 유일한 축이다**(2026-09-19). `Thrust` 는 `dir₀` 방향이라
        ///    결과가 언제나 **등가 포물선**이고 정점이 항상 사거리의 절반에 온다 — 사거리만 바뀐다.
        ///    `GravityScale` 도 마찬가지다(실측: 중력 ×0.72~×1.35 에서 **정점위치 0.500 불변**).
        ///    **수평 성분이 들어가야 대칭이 깨진다.** 실측 도달 폭 정점위치 **0.420~0.719**.
        /// 🔑 **새 수학이 필요 없다.** 상수 벡터라 «중력+바람»과 완전히 같은 취급이고,
        ///    역산기는 이미 「가속도 a 는 상수 벡터」를 해석적으로 푼다(그게 바람이 풀리는 원리다).
        ///    그래서 **닫힌 해·결정론이 그대로**다(§2-3 「매 프레임 적분은 해롭다」를 안 건드린다).
        /// ⚠️ 방향은 «수평 전방». 아래 `AeroAccel` 이 **fp 를 받는 함수 안에서** 더한다 —
        ///    호출부는 아무것도 안 바꾼다. 호출부에서 더하게 만들면 한 곳이 빠져 「다른 하늘」이 된다.
        /// </summary>
        public float AeroFwd;

        public bool HasAero => AeroFwd != 0f;

        /// 🚨 **`> 0` 이 아니라 `!= 0` 이다**(2026-09-19). 음수 추진(감속)을 열면서 바꿨다.
        /// `> 0` 으로 두면 음수 프로파일에서 **`SpeedMul` 만 걸리고 감속은 안 걸려 사거리가 늘어난다** —
        /// 초기속도는 Mul>1 로 키워 놓고 되돌릴 감속을 건너뛰기 때문이다. 이 설계의 유일한 함정이라 여기 적는다.
        public bool HasThrust => Thrust != 0f && BoostSec > 0f;
        public bool HasHoming => HomingTurnDeg > 0f;
        public float Mul => SpeedMul <= 0f ? 1f : SpeedMul;

        /// <summary>추진이 더하는 등가 속도만큼 초기속도를 깎아, 최대 파워 총속도를 원래 최대와 같게 맞춘다.</summary>
        static FlightProfile Boost(float thrust, float boostSec, float maxSpeed, float homing = 0f)
        {
            float add = thrust * boostSec;
            return new FlightProfile { Thrust = thrust, BoostSec = boostSec, HomingTurnDeg = homing, HomingRange = 70f,
                                       SpeedMul = MathF.Max(0.3f, 1f - add / maxSpeed) };
        }

        /// <summary>
        /// 수평 상수 가속이 바꾼 사거리를 초기속도로 되돌리는 배율.
        /// **유도한 식이 아니라 실측에 맞춘 식**이다(45°·파워85·평지에서 사거리를 기준과 같게 만드는 배율):
        ///   앞 +6 → 0.913 · +10 → 0.866 · +14 → 0.826 · 뒤 −6 → 1.118 · −10 → 1.225 · −14 → 1.369
        /// `1/(1+0.0165·a)` 가 이 여섯 점을 ±2% 로 지난다.
        /// ⚠️ `Boost()` 와 같은 한계를 공유한다 — **한 지점(최대 파워·45°)에서만 정확**하다.
        ///    그래서 「사거리 불변」이 아니라 「사거리를 되돌리려는 보정」이다. 실제 잔차는 [1-1] 표로 본다.
        /// </summary>
        static float AeroMul(float fwd) => 1f / (1f + 0.0165f * fwd);

        /// <summary>수평 상수 가속만 주는 프로파일(추진 없음). 사거리는 `AeroMul` 로 되돌린다.</summary>
        static FlightProfile Aero(float fwd)
            => new FlightProfile { AeroFwd = fwd, SpeedMul = AeroMul(fwd) };

        /// <summary>기존 `dir₀` 추진에 수평 상수 가속을 얹는다 — 배율은 둘을 곱한다.</summary>
        static FlightProfile Plus(FlightProfile fp, float fwd)
        {
            fp.AeroFwd = fwd;
            fp.SpeedMul = fp.Mul * AeroMul(fwd);
            return fp;
        }

        public static FlightProfile Of(TankKind kind, ShellKind shell, float powerScale)
        {
            float vmax = Ballistics.VelocityMax * powerScale;
            bool sp = shell == ShellKind.Special;
            switch (kind)
            {
                // ⚠️ 추진(Thrust·Boost)은 **탄종과 무관**하게 같다. AI 는 1번탄 궤적을 보고 탄종을 고르므로(§8 "탄도가 같으니
                //    조준을 다시 풀 필요가 없다") 2번탄이 다른 추진을 가지면 고른 근거가 거짓이 된다. 2번탄은 유도만 얹는다.
                // 아래 6종은 `dir₀` 추진(사거리)에 **수평 상수 가속(모양)**을 얹었다.
                // ⚠️ 추진만 있을 땐 「직진 후 낙하」·「앞부분이 거의 직선」이 **주석뿐이었다** — 실제론 전부
                //    정점위치 0.495 의 대칭 포물선이었다(2026-09-19 실측). 이제 그 서술이 참이 된다.
                // ⚠️ **여기 수평 가속은 작다 — 일부러 그렇다.** `Boost` 와 `AeroMul` 의 보정은
                //    각자 «혼자 있을 때» 맞춘 것이라 **곱해도 안 맞는다.** 처음에 레이저를 +12 로 줬더니
                //    사거리가 맵 밖으로 나갔다(측정 불가). 추진이 없는 7종은 전 범위를 써도 되지만
                //    여기 6종은 **사거리 잔차를 [1-1] 로 보면서** 작게 둔다. 모양은 그만큼 덜 갈린다.
                case TankKind.Missile:      return Plus(Boost(22f, 1.2f, vmax, sp ? 55f : 0f), 2f);   // 로켓: 앞으로 밀려 평평하게
                case TankKind.MultiMissile: return Plus(Boost(12f, 0.8f, vmax), 2f);
                case TankKind.SuperTank:    return Plus(Boost(26f, 1.1f, vmax, sp ? 75f : 0f), 1f);   // "핫도그" 강추진
                case TankKind.Laser:        return Plus(Boost(34f, 0.7f, vmax), 2f);                  // 빔: 추진 6종 중 가장 평평
                case TankKind.SecWind:      return Plus(Boost(14f, 0.7f, vmax), -2f);                 // 바람 타듯 살짝 떠서 늦게 떨어짐
                case TankKind.CrossBow:     return Plus(Boost(9f, 0.5f, vmax), 3f);                   // 볼트: 낮고 곧게

                // ── 오너 지시(2026-09-19) 「발사체 날아가는 궤적 다양하게 하랬는데?」 ────────────────
                // 여기 아래 7종은 **전부 `default` = 완전히 같은 포물선**이었다. ⑦ 에서 자취 «색»은 갈렸는데
                // «움직임»은 하나도 안 갈렸던 것이다. 26발(13종×2)이 사실상 6가지 모양이었다.
                //
                // 🔑 **모양만 바꾸고 수치는 안 건드린다.** 피해·폭발·굴착·각도범위·AI 평가식 전부 그대로다.
                //    레버는 이 구조체의 두 축(Thrust·BoostSec) 하나뿐이고, `Boost()` 가 총속도를 맞춘다.
                // 🔑 **음수 추진 = 감속.** 발사 방향으로 «느려지는» 탄이다. 대수적으로 안전하다 —
                //    `PositionAt`·`VelocityAt`·`SolveLaunchAngles` 가 전부 부호에 무관한 식이라
                //    닫힌 해와 정확한 역산이 그대로 유지된다. (`HasThrust` 의 `!= 0` 주석을 같이 봐라.)
                // ⚠️ **|Thrust·BoostSec| 은 12 이하로.** 최소 파워(v≈25)에서 `speed + add` 가 0 에 가까워지면
                //    역산이 깨진다. 「좀 더 세게」를 누르고 싶으면 `verify.sh ball` [2] 의 24발부터 확인해라.
                // ⚠️ 추진을 주면 사거리를 **조금 판다**(구조체 머리말의 −0.7·Th·Tb² 참조). 여기를 고쳤으면
                //    [1-1] 표의 전/후를 커밋에 붙여라.
                //
                // 배정 근거는 **지어내지 않고 `TankStats` 의 실제 값**에서 뽑았다 — Era · MinPitch~MaxPitch ·
                // GravityScale · MoveSpeed · 2번탄. 「이 기종이 왜 이렇게 나는가」가 표에 이미 적혀 있었다.

                // ── 오너 지시(2026-09-19) 「발사체 날아가는 궤적 다양하게 하랬는데?」 ──────────────
                // 🛑 **먼저 실패한 시도를 적어 둔다.** `dir₀` 방향 추진으로 갈랐더니 게이트는 전부 통과했는데
                //    **모양이 안 갈렸다** — 13종 전부 정점위치 0.494~0.496 의 **완전 대칭 포물선**이고
                //    갈린 건 사거리(179~242m)뿐이었다. 원인은 머리말에 있었다: 추진이 `dir₀` 로만 걸리면
                //    결과는 **언제나 등가 포물선**이라, **역산이 정확한 이유가 곧 모양이 안 변하는 이유**다.
                //    ⚠️ `GravityScale` 도 못 가른다 — 중력 ×0.72~×1.35 에서 **정점위치 0.500 불변**(실측).
                //    ⇒ 모양을 가르는 건 **수평 성분**뿐이다. 그래서 `AeroFwd`(전 비행 상수 가속)를 쓴다.
                //
                // 🛑 **캐논은 일부러 비워 둔다.** 25~55° · grav 1.00 · 「가장 넓고 약한」 기준 탱크다.
                //    **기준선이 없으면 열셋이 다 움직여도 아무것도 «달라» 보이지 않는다.** 여기를 채우지 마라.
                case TankKind.Cannon:       return default;

                // 캐터펄트: **0~90°** 전 기종 최대 고각 + **GravityScale 1.15** 최대. 투석기가 던진 돌 —
                //   뒤로 끌려 **높이 뜨고 늦게, 가파르게** 떨어진다. 정점이 사거리 중간보다 «뒤»로 간다.
                case TankKind.Catapult:     return Aero(-8f);
                // 마인랜더: 5~40° 저각 + **HP 1150 전 탱크 최고** = 둔중함이 정체성. 약하게 끌린다.
                case TankKind.MineLander:   return Aero(-4f);
                // 포세이돈: 미래 · 물. 무겁게 얹혀 가는 느낌 — 약한 뒤끌림.
                case TankKind.Poseidon:     return Aero(-5f);
                // 캐롯: **0~40°** 저각 + 삼연포탄. 앞으로 밀려 **낮고 빠르게** — 저각이 눈에 읽힌다.
                case TankKind.Carrot:       return Aero(5f);
                // 듀크: 0~55° + **GravityScale 0.95 전 기종 최저**. 길고 완만하게 뻗는다.
                case TankKind.Duke:         return Aero(3f);
                // 이온어태커: 미래 + **MoveSpeed 1.27 전 기종 최고**(가장 기민). 가장 날카롭게 평평하다.
                case TankKind.IonAttacker:  return Aero(10f);

                default:                    return default;                                                          // 남는 건 없다(13종 전부 위에 있다)
            }
        }
    }

    public static class Ballistics
    {
        // --- §5-1 확정 상수 ---
        /// <summary>
        /// 중력. 현실 3배 — 탄도를 굽히고 비행을 3~4초로 줄인다. **기본값 30 은 게임의 값이다.**
        ///
        /// 🚨 **`const` 를 뗀 이유는 «수치 변경»이 아니라 «대조군»이다** (2026-09-19).
        ///    §11 머리말: 「각 게이트는 "통과"가 아니라 **"빨간불을 먼저 봤는가"**로 판정한다.
        ///    네거티브 컨트롤이 없는 통과는 통과가 아니다.」
        ///    그리고 **M1 게이트 본문이 그 대조군을 명시한다** — 「`g` 를 30→15 로 바꿨을 때 체감이
        ///    명확히 달라지는가(안 달라지면 파워/각도가 결과를 지배하지 않는다는 뜻 → 상수 재설계)」.
        ///    M1 은 2026-09-19 에 오너 판정으로 통과했지만 **그 대조군은 실시되지 않았다**(§12-1 · §11 M1 배너).
        ///    그걸 사람이 1분 안에 해 볼 수 있게 여는 것이 이 스위치의 전부다.
        ///
        /// ⚠️ **기본값은 30 그대로다.** 게임 코드는 <see cref="SetForHarness"/> 를 **부르지 않는다**.
        /// 🛑 이 스위치로 **밸런스 표를 다시 뜨지 마라.** 허가된 것은 «사람이 체감을 비교하는 것»뿐이다.
        /// </summary>
        public static float Gravity { get; private set; } = 30.0f;

        /// <summary>
        /// **대조군 전용.** 판이 만들어지기 **전에만** 부를 수 있다(`-g 15` · `TANKFALL_G=15`).
        ///
        /// ⚠️ 판이 도는 중에 바꾸면 이미 날아간 탄과 새 탄이 **다른 하늘**을 보게 된다 —
        ///    이 프로젝트가 네 번 겪은 어긋남(역산·유도탄·AI 조준·연습장)과 같은 계열이다.
        ///    그래서 <see cref="MapHeightFunction.BattleStarted"/> 를 보고 **던진다**(`TeamSize` 와 같은 가드).
        /// ⚠️ 범위 밖은 던진다 — 조용히 무시하면 "g=15 로 쟀다"면서 30 으로 재는 거짓말이 된다.
        /// </summary>
        public static void SetForHarness(float g)
        {
            if (!(g > 0f) || g > 200f)
                throw new ArgumentOutOfRangeException(nameof(g), g, "중력은 0 초과 200 이하 (대조군 범위)");
            if (MapHeightFunction.BattleStarted)
                throw new InvalidOperationException(
                    "Ballistics.SetForHarness 는 판이 만들어지기 전에만 부를 수 있다 — 이미 Spawn 이 돌았다. " +
                    "판 도중에 중력을 바꾸면 날아가던 탄과 새 탄이 다른 하늘을 본다.");
            Gravity = g;
        }

        /// <summary>기본값과 다른가 — 화면에 «대조군 중»을 알리려고 쓴다(사람이 모르고 재면 안 된다).</summary>
        public static bool GravityIsDefault => Gravity == 30.0f;
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

        /// <summary>
        /// 프로파일의 수평 가속을 «가속도 벡터»에 합친다. **여기 한 곳에서만 더한다.**
        /// `horizRef` 는 수평 전방 기준 벡터(속도 또는 목표 방향) — Y 는 무시한다.
        /// ⚠️ 호출부에서 각자 더하게 두면 한 곳이 빠져 「믿음과 현실이 다른 하늘」이 된다.
        ///    그래서 **fp 를 받는 함수들이 내부에서** 이걸 부른다.
        /// </summary>
        public static Vec3 AeroAccel(Vec3 accel, Vec3 horizRef, in FlightProfile fp)
        {
            if (!fp.HasAero) return accel;
            var h = new Vec3(horizRef.X, 0f, horizRef.Z);
            float l = h.Length;
            if (l < 1e-6f) return accel;
            return accel + h * (fp.AeroFwd / l);
        }

        /// <summary>닫힌 해. 서버·AI·프리뷰·리플레이·킬캠이 전부 이 함수를 쓴다.</summary>
        public static Vec3 PositionAt(Vec3 p0, Vec3 v0, Vec3 accel, float t)
            => p0 + v0 * t + accel * (0.5f * t * t);

        /// <summary>프로파일 포함 닫힌 해. 추진 구간은 dir₀ 방향 등가속, 그 뒤는 추진이 준 속도로 포물선.</summary>
        public static Vec3 PositionAt(Vec3 p0, Vec3 v0, Vec3 accel, float t, in FlightProfile fp)
        {
            accel = AeroAccel(accel, v0, fp);            // 수평 상수 가속 = 중력·바람과 같은 취급
            if (!fp.HasThrust) return PositionAt(p0, v0, accel, t);
            Vec3 dir = v0.Normalized;
            float tb = MathF.Min(t, fp.BoostSec);
            float boost = 0.5f * fp.Thrust * tb * tb + fp.Thrust * fp.BoostSec * MathF.Max(0f, t - fp.BoostSec);
            return p0 + v0 * t + dir * boost + accel * (0.5f * t * t);
        }

        /// <summary>프로파일 포함 속도(유도 구간 진입 시 초기값으로 쓴다).</summary>
        public static Vec3 VelocityAt(Vec3 v0, Vec3 accel, float t, in FlightProfile fp)
        {
            Vec3 v = v0 + AeroAccel(accel, v0, fp) * t;
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
            // 수평 기준은 **해에 의존하지 않는** 「쏘는 쪽 → 목표」 방향으로 잡는다 — 그래야 결합이 없어
            // 반복 없이 정확히 풀린다(바람이 있으면 해의 야우는 조금 달라지지만, 그 차이는 [2] 24발이 잡는다).
            accel = AeroAccel(accel, to - from, fp);
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
