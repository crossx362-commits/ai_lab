// 미사일 **초기 유도 및 자세 제어 단계**(Initial Guidance and Attitude Control Phase)
// — 오너 지시 2026-09-17 "발사체에 미사일 초기 유도 및 자세제어 같은 모습도 추가해".
//
// === 왜 SIM 에 있는가 ===
// 이건 **연출 전용**이다(탄도 §5·판정에 영향 0). 그런데 View 에 두면 검증할 방법이 없다 —
// verify.sh 는 유니티 없이 Sim 만 컴파일해 돌린다. 자세는 순수 함수 At(t,…) 이므로
// Sim 에 두면 "수직으로 섰다가 속도 방향으로 눕는가"를 하네스로 실측할 수 있다(tools/GuidanceVerify.cs).
//
// ⚠️ 여기서 위치·속도를 만들지 마라. 궤적은 Ballistics 의 닫힌 해 + ProjectileSimulator 뿐이다.
//    이 파일은 "탄이 **어디를 보고 있는가**"와 "제어 장치가 **지금 몇 % 로 작동 중인가**"만 답한다.
//    실제로 몸체를 기울여도 탄착점이 안 변하는 건, 기획서 §13 이 공기저항을 뺐기 때문이다
//    (자세가 궤적에 개입할 물리 경로 자체가 없다 — 그래서 조준 역산이 그대로 정확하다).
//
// === 실제 미사일의 4단계를 그대로 시간축에 올린다 ===
//   1. 사전 데이터 입력·INS 정렬  : t < AlignSec       — 노즈가 아직 발사관 방향. 제어면 정지
//   2. 수직 상승 (VLS)            : RiseSec            — 노즈만 하늘로 세운다(속도는 그대로 = 자세/속도 불일치)
//   3. 방향 전환 (Pitch-over)     : PitchSec           — TVC + 측추력기로 노즈를 속도 방향까지 꺾는다
//   4. 중간 유도 진입             : t ≥ BoostSec       — 연소 종료 → TVC 사라지고 날개(카나드)만 남는다. INS/GPS 보정 1회
// 유도탄은 정점 뒤 선회하는데(ProjectileSimulator 의 homing), 그 구간은 추력이 없으므로
// **날개와 측추력기만** 작동한다 — TvcDeg 가 0 으로 떨어지는 게 그 표현이다.
//
// 수치는 전부 [추정]. 원작(포트리스)은 2D 포물선뿐이라 근거가 없다 — 눈에 읽히는 값으로 잡았다.

using System;

namespace Tankfall.Sim
{
    /// <summary>기종별 초기 유도 단계 길이. 셋을 더한 값이 BoostSec 을 넘으면 안 된다(연소 중에 자세를 끝내야 한다).</summary>
    public struct GuidanceProfile
    {
        public float AlignSec;     // INS 정렬 — 노즈 고정
        public float RiseSec;      // 수직 상승
        public float PitchSec;     // 방향 전환
        public float RiseDeg;      // 발사 방향에서 하늘 쪽으로 얼마나 세우는가
        public float BoostSec;     // 연소 시간(이후 TVC 없음) — FlightProfile 에서 받아온다
        public float MaxTvcDeg;    // 노즐 최대 편향각
        public float DacsHz;       // 측추력기 펄스 주파수
        public float FinDeg;       // 공력 구간 날개 미세 보정 진폭

        public bool Has => PitchSec > 0f;
        public float PitchOverEnd => AlignSec + RiseSec + PitchSec;

        /// <summary>
        /// 추진체를 가진 기종 중 **미사일**만 이 단계를 갖는다.
        /// 레이저(빔)·세크윈드(바람칼)·크로스보우(볼트)는 추진이 있어도 자세 제어 장치가 없는 물건이라 제외한다 —
        /// 빔이 수직으로 섰다가 눕는 건 정체성이 깨진다.
        /// </summary>
        public static GuidanceProfile Of(TankKind kind, in FlightProfile fp)
        {
            switch (kind)
            {
                case TankKind.Missile:      // 3연발 로켓 — 가장 길고 확실하게 선다
                    return Mk(0.10f, 0.30f, 0.55f, 34f, fp.BoostSec, 14f, 22f, 2.2f);
                case TankKind.SuperTank:    // 핫도그·9연 유도탄 — 강추진이라 전환이 날카롭다
                    return Mk(0.08f, 0.26f, 0.50f, 30f, fp.BoostSec, 17f, 26f, 2.6f);
                case TankKind.MultiMissile: // 폭죽 다발 — 추진이 짧아 단계도 짧다
                    return Mk(0.06f, 0.18f, 0.38f, 24f, fp.BoostSec, 11f, 20f, 1.8f);
                default:
                    return default;
            }
        }

        static GuidanceProfile Mk(float align, float rise, float pitch, float riseDeg,
                                  float boost, float tvc, float hz, float fin)
        {
            // 연소보다 자세가 늦게 끝나면 "추력 없는 TVC" 가 되어 거짓말이 된다 — 남는 시간에 맞춰 줄인다.
            float total = align + rise + pitch;
            float room = boost > 0f ? boost * 0.92f : total;
            float k = total > room ? room / total : 1f;
            return new GuidanceProfile
            {
                AlignSec = align * k, RiseSec = rise * k, PitchSec = pitch * k,
                RiseDeg = riseDeg, BoostSec = boost, MaxTvcDeg = tvc, DacsHz = hz, FinDeg = fin,
            };
        }
    }

    /// <summary>한 시점의 자세 + 제어 장치 상태. View 는 이걸 그대로 그리기만 한다.</summary>
    public struct Attitude
    {
        public Vec3 Nose;        // 몸체가 가리키는 방향(정규화). 전환 중에는 **속도 방향과 다르다**
        public Vec3 Exhaust;     // 노즐이 실제로 뿜는 방향(TVC 편향 포함)
        public Vec3 DacsDir;     // 측추력기 분사 방향(노즈가 가는 쪽의 반대 — 반작용으로 꺾는다)
        public float TvcDeg;     // 노즐 편향각(연소 중에만 > 0)
        public float Dacs;       // 0~1 측추력기 분사 세기(펄스)
        public float Fin;        // -1~1 날개(그리드핀·카나드) 편향
        public bool InsFix;      // 이 프레임에 INS/GPS 보정이 있었는가(중간 유도 진입)
        public int Stage;        // 0 정렬 · 1 수직상승 · 2 방향전환 · 3 공력비행 · 4 중간유도(선회)
    }

    public static class Guidance
    {
        public const int StageAlign = 0, StageRise = 1, StagePitchOver = 2, StageAero = 3, StageMidcourse = 4;

        /// <summary>선회로 보지 않는 잡음 하한(초당 도). 궤적 샘플의 수치 오차로 날개가 떨리는 걸 막는다.</summary>
        public const float TurnDeadzoneDeg = 3f;

        /// <summary>
        /// 비행시간 t 에서의 자세. **t 만의 함수**라 결정론이고, 리플레이·킬캠이 같은 그림을 낸다.
        /// </summary>
        /// <param name="t">발사 후 경과(초)</param>
        /// <param name="dt">직전 프레임과의 간격 — InsFix 를 "이 프레임에 통과했는가"로 판정하는 데만 쓴다</param>
        /// <param name="velDir">현재 속도 방향(정규화 전이어도 된다)</param>
        /// <param name="launchDir">발사 순간의 속도 방향</param>
        /// <param name="turnRateDeg">현재 선회율(초당 도) — 유도 구간 표현용. 없으면 0</param>
        public static Attitude At(float t, float dt, Vec3 velDir, Vec3 launchDir,
                                  in GuidanceProfile g, float turnRateDeg = 0f)
        {
            Vec3 v = velDir.Normalized;
            if (v.LengthSq < 0.5f) v = launchDir.Normalized;
            var a = new Attitude { Nose = v, Exhaust = v * -1f, DacsDir = default, Stage = StageAero };
            if (!g.Has) return a;

            Vec3 l = launchDir.Normalized;
            if (l.LengthSq < 0.5f) l = v;

            float t1 = g.AlignSec, t2 = t1 + g.RiseSec, t3 = t2 + g.PitchSec;
            Vec3 riseDir = TiltToVertical(l, g.RiseDeg);

            Vec3 from, to;
            float u, rateNorm;          // rateNorm = 0~1 로 정규화한 각속도(스무스스텝 미분 최대가 1.5)
            if (t < t1)
            {
                // 1단계 — 표적 좌표 입력·INS 영점. 노즈는 아직 발사관을 본다.
                from = l; to = l; u = 0f; rateNorm = 0f; a.Stage = StageAlign;
            }
            else if (t < t2)
            {
                from = l; to = riseDir; a.Stage = StageRise;
                float p = Norm(t - t1, g.RiseSec);
                u = Smooth(p); rateNorm = SmoothRate(p);
            }
            else if (t < t3)
            {
                // 3단계 — Pitch-over. 여기가 TVC·측추력기가 제일 크게 작동하는 구간이다.
                from = riseDir; to = v; a.Stage = StagePitchOver;
                float p = Norm(t - t2, g.PitchSec);
                u = Smooth(p); rateNorm = SmoothRate(p);
            }
            else
            {
                from = v; to = v; u = 1f; rateNorm = 0f;
                a.Stage = turnRateDeg > TurnDeadzoneDeg ? StageMidcourse : StageAero;
            }

            Vec3 nose = Slerp(from, to, u);
            float sweep = AngleRad(from, to);

            // 노즈가 **가고 있는 쪽**(노즈에 수직인 단위벡터). TVC·측추력기 방향이 전부 여기서 나온다.
            Vec3 turnDir = Perp(nose, to);

            // 4단계 — 공력 비행: 날개로 미세 보정. 전환 직후 진동이 남았다가 잦아든다.
            if (a.Stage >= StageAero)
            {
                float since = t - t3;
                float settle = MathF.Exp(-since * 1.7f);
                float fin = MathF.Sin(since * 9.0f) * settle;
                if (a.Stage == StageMidcourse)
                {
                    // 유도 선회 중 — 카나드가 선회 쪽으로 물린 채 유지된다(진동이 아니라 편향).
                    fin = Clamp(turnRateDeg / 60f, -1f, 1f);
                    turnDir = Perp(nose, v);
                }
                a.Fin = fin;
                nose = Tilt(nose, turnDir, a.Fin * g.FinDeg * (MathF.PI / 180f));
            }

            a.Nose = nose;

            // ── TVC: 노즐을 꺾어 추력 방향을 바꾼다. **연소 중에만** 가능하다 ──
            // 노즈를 turnDir 쪽으로 돌리려면 꼬리를 반대로 밀어야 하고, 그러려면 배기가 turnDir 쪽으로 나가야 한다.
            bool burning = g.BoostSec <= 0f || t < g.BoostSec;
            float tvcRad = 0f;
            if (burning && a.Stage >= StageRise && a.Stage <= StagePitchOver)
            {
                float span = a.Stage == StageRise ? g.RiseSec : g.PitchSec;
                float degPerSec = sweep * (180f / MathF.PI) * rateNorm / MathF.Max(1e-3f, span);
                a.TvcDeg = MathF.Min(g.MaxTvcDeg, degPerSec * 0.22f);
                tvcRad = a.TvcDeg * (MathF.PI / 180f);
            }
            a.Exhaust = Tilt(nose * -1f, turnDir, tvcRad);

            // ── 측추력기(DACS): 몸통 옆 소형 로켓이 **펄스**로 터진다. 가스는 노즈가 가는 쪽의 반대로 뿜는다 ──
            float demand = a.Stage == StageMidcourse
                ? Clamp(MathF.Abs(turnRateDeg) / 45f, 0f, 1f)
                : Clamp(rateNorm, 0f, 1f) * (a.Stage == StagePitchOver ? 1f : 0.65f);
            if (a.Stage == StageAlign || a.Stage == StageAero) demand = 0f;
            a.Dacs = demand * Pulse(t, g.DacsHz);
            a.DacsDir = turnDir * -1f;

            // ── INS/GPS 보정: 연소 종료 = 중간 유도 진입. 한 프레임만 true ──
            if (g.BoostSec > 0f && t >= g.BoostSec && t - dt < g.BoostSec) a.InsFix = true;
            // 발사 직후 정렬 완료(1단계 종료)도 한 번 알린다 — 유도 컴퓨터가 궤도를 받아든 순간.
            else if (t >= t1 && t - dt < t1) a.InsFix = true;

            return a;
        }

        // ── 수학 도우미 ────────────────────────────────────────
        static float Norm(float x, float span) => span <= 1e-6f ? 1f : Clamp(x / span, 0f, 1f);
        static float Smooth(float p) => p * p * (3f - 2f * p);
        /// <summary>스무스스텝 미분을 최댓값 1.5 로 나눠 0~1 로 만든 것.</summary>
        static float SmoothRate(float p) => 6f * p * (1f - p) / 1.5f;

        static float Clamp(float x, float lo, float hi) => x < lo ? lo : (x > hi ? hi : x);

        /// <summary>펄스 열 — 듀티 35%. 난수가 아니라 시간의 함수라 결정론이다.</summary>
        static float Pulse(float t, float hz)
        {
            if (hz <= 0f) return 1f;
            float ph = t * hz;
            ph -= MathF.Floor(ph);
            return ph < 0.35f ? 1f : 0f;
        }

        static float AngleRad(Vec3 a, Vec3 b) => MathF.Acos(Clamp(Vec3.Dot(a, b), -1f, 1f));

        /// <summary>a 에 수직이면서 b 쪽을 가리키는 단위벡터. a∥b 면 0 벡터.</summary>
        static Vec3 Perp(Vec3 a, Vec3 b)
        {
            Vec3 p = b - a * Vec3.Dot(a, b);
            return p.LengthSq < 1e-10f ? default : p.Normalized;
        }

        /// <summary>dir 을 axisDir(dir 에 수직) 쪽으로 rad 만큼 기울인다.</summary>
        static Vec3 Tilt(Vec3 dir, Vec3 towards, float rad)
        {
            if (MathF.Abs(rad) < 1e-5f || towards.LengthSq < 0.5f) return dir;
            return (dir * MathF.Cos(rad) + towards * MathF.Sin(rad)).Normalized;
        }

        /// <summary>발사 방향을 하늘 쪽으로 deg 만큼 세운다. 수직을 넘지는 않는다(뒤집힌 로켓 방지).</summary>
        static Vec3 TiltToVertical(Vec3 dir, float deg)
        {
            Vec3 up = new Vec3(0f, 1f, 0f);
            float toVert = AngleRad(dir, up);
            float want = deg * (MathF.PI / 180f);
            float use = MathF.Min(want, MathF.Max(0f, toVert - 2f * (MathF.PI / 180f)));
            if (use <= 1e-4f) return dir;
            Vec3 towards = Perp(dir, up);
            return towards.LengthSq < 0.5f ? dir : Tilt(dir, towards, use);
        }

        static Vec3 Slerp(Vec3 a, Vec3 b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            float ang = AngleRad(a, b);
            if (ang < 1e-4f) return b;
            Vec3 towards = Perp(a, b);
            return towards.LengthSq < 0.5f ? b : Tilt(a, towards, ang * t);
        }
    }
}
