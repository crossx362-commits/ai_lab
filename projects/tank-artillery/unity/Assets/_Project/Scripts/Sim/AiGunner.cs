// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §5-7, §10(AI), §67(난이도 오차)
//
// AI 조준을 VIEW 가 아니라 여기 둔 이유:
//   1) 유니티 없이 자동 대전을 돌려 명중률·한 판 길이를 **측정**할 수 있다
//   2) 서버 권위(§4-2)에서 AI 도 같은 코드를 쓴다
//   3) 시드 기반 난수라 재현된다(리플레이)

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>결정론적 난수. UnityEngine.Random 을 쓰면 리플레이·서버 재현이 깨진다.</summary>
    public struct Rng
    {
        uint _s;
        public Rng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }
        public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
        public float Float01() => (Next() & 0xFFFFFF) / (float)0x1000000;
        public float Range(float a, float b) => a + (b - a) * Float01();
    }

    public struct AimPlan
    {
        public bool Valid;
        /// <summary>
        /// AI 가 **스스로 예상한** 착탄 오차(m). 해를 못 찾아 대충 쏘는 폴백이면 `float.MaxValue`.
        ///
        /// 이 값이 있는 이유: AI 의 **믿음을 밖에서 검사할 수 있게** 하려고. 2026-09-19 까지
        /// 조준 검증이 기후 없는 하늘에서 돌아서, AI 는 "1m 안에 맞는다"고 믿고 쏜 탄이 회오리에
        /// 먹혀 60m 밖에 떨어져도 **아무도 그 어긋남을 못 봤다.** 이제 자체검사가
        /// `PredictedMiss` 와 실제 착탄을 대조한다 — 믿음과 현실이 갈리면 즉시 빨간불이다.
        /// </summary>
        public float PredictedMiss;
        public float YawDeg;      // 월드 야우
        public float PitchDeg;
        public float Power;       // 0~1
        public int TargetId;
    }

    public static class AiGunner
    {
        /// <summary>
        /// 난이도별 조준 오차(§67). **목표점을 이만큼 흔든 뒤 그 지점을 정확히 조준**한다.
        ///
        /// ⚠️ 파워·각도에 각각 퍼센트를 곱하면 안 된다. 두 오차가 겹치는 데다
        ///    45°에서 각도 8%는 3.6°라 20m 넘게 빗나간다 — 폭발 반경(7m)의 세 배다.
        ///    "탄착 반경"으로 정의해야 난이도가 예측 가능하고 튜닝이 된다.
        ///
        /// 값은 사거리 대비 비율. **자동 대전 40판 × 수십 조합 실측으로 확정** (`verify.sh battle`).
        ///
        /// HP 1000 · 굴착 반경 7m · 서든데스 6라운드 기준:
        ///
        /// | 오차 | 명중률 | 예상 시간 |
        /// |------|--------|-----------|
        /// | 0%(대조) | 100% | 4.8분 |
        /// | 0.75% | 100% | 4.8분 |
        /// | 1.5%  |  99% | 4.9분 |
        /// | 2.5%  |  84% | 7.3분 |
        /// | 4.0%  |  68% | 11.9분 ✅ |
        ///
        /// ⚠️ **기획서 §67 의 ±15/8/4/2% 를 그대로 쓰면 안 된다.** 6% 면 명중률 8%,
        ///    한 판 95.8분, 무승부 28/40 이다. 오차가 폭발 반경(7m)을 넘는 순간
        ///    피해가 0 으로 죽고 판이 안 끝난다 — 그리고 길어진 판이 지형을 갈아
        ///    명중률을 더 떨어뜨리는 되먹임 나선에 들어간다(§2-5-1).
        ///
        /// ⚠️ **이 값들을 올려 판을 길게 만들려 하지 마라.** 판을 늘리는 모든 손잡이는
        ///    지형을 갈아 **실력 변별력을 지운다**(HP 1800 에서 에임봇과 1.5% AI 가 둘 다 80%).
        ///    그건 §1 "포격 감각이 실력을 결정한다" 를 깨는 것이다. 판 길이는
        ///    서든데스(상한)로 자르고, 이 값은 **위험도**만 정한다.
        /// </summary>
        // 아래 실측치는 **게임이 쓰는 TwinHills 기준**(2026-09-16 재측정, §2-9-8).
        // 옛 하네스 언덕에서 잰 값과 크게 다르니 맵을 바꾸면 반드시 다시 재라.
        // 라운드로빈(서로 다른 난이도끼리 붙이기)에서 강한 쪽이 일관되게 이긴다 = 사다리 성립.
        public const float ErrorNovice = 0.04f;    // 명중 75% · 9.8분 — 사람이 이기는 난이도
        public const float ErrorNormal = 0.025f;   // 명중 83% · 6.2분 — 기본값
        public const float ErrorExpert = 0.015f;   // 명중 94% · 4.1분
        public const float ErrorAce = 0.0075f;     // 명중 97% · 3.5분 — 사실상 빗나가지 않는다

        /// <summary>
        /// 탄종 선택(§8). **특수탄은 탄도를 바꾸지 않으므로** 궤적을 한 번만 계산하고
        /// 같은 착탄점에 두 프로필을 대 보면 된다 — 조준을 두 번 풀 필요가 없다.
        ///
        /// ⚠️ 피해량만으로 고르면 **굴착탄은 영원히 안 쓰인다**(피해가 절반이다).
        ///    굴착탄의 값어치는 피해가 아니라 지형이다. 그래서 두 갈래로 판단한다:
        ///      · 굴착형(굴착 반경이 커진 탄) → "때려도 안 아플 때" 쓴다. 발밑을 파서 떨어뜨린다(§28)
        ///      · 그 외                      → 기대 피해가 뚜렷이 클 때만 쓴다
        ///    탄이 3발뿐이라 "조금 이득"에 쓰면 정작 필요할 때 없다. 그래서 문턱을 둔다.
        /// </summary>
        /// <summary>
        /// 1번탄 vs 2번탄. **원작대로 둘 다 무한**이라 탄수 계산이 없다 — 남는 비용은 2번탄의 추가 딜레이[추정]뿐이다.
        /// 그래서 문턱은 낮다(1.10배). 폭발 피해로 안 보이는 효과(독·불·속박·지뢰)는 <see cref="EffectValue"/> 로 셈한다.
        /// 위성탄은 착탄점이 다르므로 호출자가 SatelliteStrike 로 푼 착탄점을 <paramref name="specialImpact"/> 로 준다.
        /// </summary>
        /// <summary>다탄두 한 발의 착탄. <see cref="SimulatePattern"/> 이 만든다.</summary>
        public struct SubImpact { public Vec3 Impact; public int DirectId; public float Scale; }

        /// <summary>
        /// 패턴의 전 발을 실제로 날려 착탄점을 모은다(빗나간 발은 뺀다). 중앙 탄(편차 0)은 이미 쏜 결과를 재사용.
        /// 조준(Decide)이 시뮬 검증인 것과 같은 원칙 — 탄종 선택도 추정식이 아니라 시뮬 결과로 한다.
        /// </summary>
        public static List<SubImpact> SimulatePattern(SdfVolume vol, Vec3 muzzle, float yawDeg, float pitchDeg, float speed, Vec3 accel,
                                                      IReadOnlyList<TankHitbox> boxes, int shooterId, float mapSize,
                                                      IReadOnlyList<Spread.ShotPattern> pattern, ShotResult? center = null,
                                                      FlightProfile fp = default, Func<int, bool> homingOk = null,
                                                      AirField air = null)
        {
            var list = new List<SubImpact>(pattern.Count);
            for (int i = 0; i < pattern.Count; i++)
            {
                var pt = pattern[i];
                ShotResult sub;
                if (center.HasValue && pt.YawOffsetDeg == 0f && pt.PitchOffsetDeg == 0f) sub = center.Value;
                else sub = ProjectileSimulator.Simulate(vol, muzzle,
                        Ballistics.VelocityFrom(yawDeg + pt.YawOffsetDeg, pitchDeg + pt.PitchOffsetDeg, speed), accel, boxes, shooterId, mapSize, fp, homingOk, air);
                if (sub.Hit) list.Add(new SubImpact { Impact = sub.Impact, DirectId = sub.DirectHitTankId, Scale = pt.DamageScale });
            }
            return list;
        }

        /// <summary>
        /// 1번탄 vs 2번탄 (§8). 양쪽 패턴의 **실제 착탄점 합**으로 기대 피해를 비교한다.
        ///
        /// ⚠️ 예전엔 중앙 탄 한 발의 피해 × 발수^0.85 로 어림했다. 그 식으로는 멀티미사일 2번(60×9)이
        ///    1번(175×3)을 어떤 상황에서도 못 넘어 120판 동안 0회 — 선택지가 식 때문에 죽어 있었다.
        ///    부채꼴이 실제로 몇 발이나 폭발 반경 안에 떨어지는지는 지형·거리에 달렸으니 쏴 봐야 안다.
        /// </summary>
        /// <param name="normal">1번탄 패턴 착탄점들(SimulatePattern).</param>
        /// <param name="special">2번탄 패턴 착탄점들. 위성탄처럼 착탄점이 따로 계산되는 탄은 null 로 두고 specialImpact 로 넘긴다.</param>
        /// <param name="status">
        /// **지금 판의 실제 상태 효과.** 독·화상이 이미 걸린 적에게 다시 쏘는 값을 깎는 데 쓴다.
        /// ⚠️ **일부러 필수 인자로 뒀다.** 기본값을 주면 호출부가 안 넘겨도 컴파일이 되고, 그러면
        ///    게임만 고쳐지고 하네스는 옛 값으로 재는 어긋남이 생긴다 — 이 프로젝트가 걸음 크기에서
        ///    이미 한 번 당한 실패다(§2-9-11). 빈 인스턴스를 넘기는 것도 같은 결과이니 넘기지 마라.
        /// </param>
        public static ShellKind PickShell(TankStats baseSt, IReadOnlyList<SubImpact> normal, IReadOnlyList<SubImpact> special,
                                          IReadOnlyList<Target> enemies, StatusEffects status,
                                          Vec3? specialImpact = null, int specialDirectId = -1)
        {
            if (enemies == null || enemies.Count == 0 || normal == null) return ShellKind.Normal;
            var sp = baseSt.WithShell(ShellKind.Special);
            var fx = ShellEffects.Of(baseSt.Kind, ShellKind.Special);

            float vNormal = 0f;
            for (int i = 0; i < normal.Count; i++)
                vNormal += ExpectedDamage(baseSt, normal[i].Impact, normal[i].DirectId, enemies) * normal[i].Scale;

            float vSpecial = 0f;
            Vec3 spCenter; bool haveCenter = false;
            if (specialImpact.HasValue)
            {
                // 위성탄: 착탄 X·Z 의 하늘에서 수직 낙하 — 호출부가 먼저 풀어서 넘긴다
                spCenter = specialImpact.Value; haveCenter = true;
                vSpecial = ExpectedDamage(sp, spCenter, specialDirectId, enemies);
            }
            else
            {
                spCenter = default;
                if (special != null)
                    for (int i = 0; i < special.Count; i++)
                    {
                        Vec3 imp = special[i].Impact; int dir = special[i].DirectId;
                        if (fx.Type == ShellEffects.EffectType.Homing)
                        {
                            // 유도탄: 착탄점이 가까운 적에게 끌린다 → 그 적 중심에서의 피해로 평가
                            float best = ShellEffects.HomingRange; int who = -1;
                            for (int e = 0; e < enemies.Count; e++)
                            {
                                float d = (enemies[e].Center - imp).Length;
                                if (d < best) { best = d; who = e; }
                            }
                            if (who >= 0) { imp = enemies[who].Center; dir = enemies[who].Id; }
                        }
                        vSpecial += ExpectedDamage(sp, imp, dir, enemies) * special[i].Scale;
                        if (i == special.Count / 2) { spCenter = imp; haveCenter = true; }   // 부채꼴 중앙 발
                    }
            }
            if (haveCenter) vSpecial += EffectValue(baseSt.Kind, sp, spCenter, enemies, status);

            // 굴착 특화 판단(발밑 끊기)은 유지 — 낙하 피해가 있으니 굴착이 곧 화력이다
            if (haveCenter && sp.CraterRadius > baseSt.CraterRadius * 1.3f)
            {
                float near = float.MaxValue;
                for (int i = 0; i < enemies.Count; i++) near = MathF.Min(near, (enemies[i].Center - spCenter).Length);
                if (vNormal < 60f && near < sp.CraterRadius) return ShellKind.Special;
            }
            // 원작대로 2번탄이 무한이라 비용은 추가 딜레이[추정 +20%]뿐 — 조금이라도 나으면 쓴다
            return vSpecial > vNormal * 1.02f + 10f ? ShellKind.Special : ShellKind.Normal;
        }

        /// <summary>
        /// 폭발 피해에 안 잡히는 2번탄 효과의 기대 가치 [추정]. 전부 "맞으면"이 전제라 폭발 반경 안에 적이 있어야 한다.
        ///
        /// ⚠️ **"크로스보우 2번탄이 판당 1.7회밖에 안 나가는 건 이 함수가 지속 피해를 과소평가해서"가 아니다.**
        ///    2026-09-17 에 그 가설로 여기를 고치려다 실측으로 뒤집혔다. 재확인하지 말고 이 표를 봐라
        ///    (적 1명·방어 100·정타 기준, 지금 수치):
        ///
        ///      크로스보우 1번탄 670  vs  2번탄 269 + 독 60 = 329     → 2번탄이 **절반 이하**
        ///
        ///    독을 할인 없이(×1.0) 쳐도 344 다. **2배 차이는 이 함수로 못 메운다.**
        ///    거리를 훑으면 2번탄이 이기는 구간은 **3.5~4.05m 폭 0.5m 고리 하나**뿐이다
        ///    (1번탄 피해가 60 아래로 떨어지는 지점과 2번탄 폭발 반경 4.05m 사이).
        ///    판당 1.7회는 그 고리에 적이 들어온 횟수다 — **AI 는 정확히 옳게 고르고 있다.**
        ///
        ///    원인은 조준이 아니라 수치다: `SpBase 0.30 · SpDirect 0.50` 이라 2번탄이 1번탄의 절반인데
        ///    보상이 독 75 뿐이다. 이 비율은 DirectDamage 를 올려도 안 변한다(둘 다 같이 스케일된다 —
        ///    200→340 재밸런싱 전후로 비율이 2.05 → 2.04 로 그대로였다).
        ///    고칠 곳은 **TankStats 의 SpBase/SpDirect 또는 독 수치**지 여기가 아니다.
        ///
        /// ⚠️ 반대로 여기엔 **과대평가** 쪽 오차가 둘 있다(둘 다 지속탄을 덜 쓰게 만드는 방향이라 아직 안 고쳤다.
        ///    고치려면 `PickShell` 에 `StatusEffects` 를 넘겨야 해서 시그니처가 바뀐다):
        ///      · 독·화상은 `_poison[id] = ...` **덮어쓰기라 안 쌓인다.** 이미 중독된 적에게 다시 쏴도
        ///        남은 턴이 갱신될 뿐인데, 여기서는 매번 총량을 그대로 쳐 준다.
        ///      · 포세이돈은 독 저항 50%(`StatusEffects.Poison`)인데 여기서는 안 깎는다.
        /// </summary>
        static float EffectValue(TankKind kind, TankStats sp, Vec3 impact, IReadOnlyList<Target> enemies,
                                 StatusEffects status)
        {
            var fx = ShellEffects.Of(kind, ShellKind.Special);
            float near = float.MaxValue; int inBlast = 0;

            // 독·화상은 **표적마다** 값이 다르다(이미 걸려 있으면 이득이 적다) — 대상별로 센다.
            // ⚠️ `status` 가 null 이면 옛 동작(늘 총량)으로 떨어진다. 호출부가 실제 상태를 안 넘기면
            //    게임만 고쳐지고 하네스는 옛 값으로 재는 어긋남이 생긴다 — 둘 다 같은 걸 넘겨야 한다.
            float dotGain = 0f;
            for (int i = 0; i < enemies.Count; i++)
            {
                float d = (enemies[i].Center - impact).Length;
                near = MathF.Min(near, d);
                if (d > sp.BlastRadius) continue;
                inBlast++;

                if (fx.Type == ShellEffects.EffectType.Poison)
                    dotGain += status != null
                        ? status.PoisonGain(enemies[i].Id, fx.Param1, fx.Param2, enemies[i].Kind)
                        : fx.Param1 * fx.Param2;
                else if (fx.Type == ShellEffects.EffectType.Burn)
                    dotGain += status != null
                        ? status.BurnGain(enemies[i].Id, fx.Param1, fx.Param2)
                        : fx.Param1 * fx.Param2;
            }
            switch (fx.Type)
            {
                // 지속피해 총량의 80%(적이 나가면 끊기지만 나가려면 이동을 써야 한다).
                // ⚠️ 이제 총량이 아니라 **증분**이다 — 독·화상은 덮어쓰기라 재중독으로 얻는 게 적다
                //    (StatusEffects.PoisonGain). 예전엔 이미 중독된 적에게도 총량을 그대로 쳐 줘서
                //    AI 가 같은 적을 재중독시키며 턴을 버렸다.
                case ShellEffects.EffectType.Poison:
                case ShellEffects.EffectType.Burn:
                    return dotGain * 0.8f;

                // 독구름은 **자리에 남는 장판**(HazardField)이라 유닛 상태를 덮어쓰지 않는다 —
                // 여러 겹 깔면 실제로 겹쳐 들어가므로 증분 할인을 하면 안 된다.
                case ShellEffects.EffectType.PoisonCloud:
                    return inBlast > 0 ? fx.Param1 * fx.Param2 * 0.8f * inBlast : 0f;

                case ShellEffects.EffectType.Root:
                    return inBlast > 0 ? 40f : 0f;                                        // 움직임 봉쇄 [추정 40]
                case ShellEffects.EffectType.Mine:
                    return fx.Param1 * 0.5f * MathF.Max(0f, 1f - near / 20f);             // 적 가까이 깔수록 가치 [추정]
                default:
                    return 0f;
            }
        }

        static float ExpectedDamage(TankStats st, Vec3 impact, int directHitId, IReadOnlyList<Target> enemies)
        {
            float sum = 0f;
            for (int i = 0; i < enemies.Count; i++)
            {
                float d = (enemies[i].Center - impact).Length;
                bool direct = enemies[i].Id == directHitId;
                if (d > st.BlastRadius && !direct) continue;
                sum += Damage.AfterDefense(Damage.Compute(d, st.BlastRadius, st.BaseDamage, st.DirectDamage, direct),
                                           enemies[i].Defense > 0f ? enemies[i].Defense : 100f);
            }
            return sum;
        }

        public struct Target
        {
            public int Id;
            public Vec3 Center;
            public float Defense;   // 표적 방어력 — 없으면 100(원피해)

            /// <summary>표적 기종. 독 저항(포세이돈 50%)을 <see cref="EffectValue"/> 가 보려면 필요하다.
            /// ⚠️ 안 넣으면 기본값이 캐터펄트(0)라 **포세이돈 저항만 조용히 빠진다.** 호출부에서 반드시 채워라
            /// (재중독 판정은 Id 만 쓰므로 이 값이 비어도 동작한다 — 그래서 더 눈에 안 띈다).</summary>
            public TankKind Kind;
        }

        /// <summary>
        /// 가장 가까운 적을 골라 조준한다. 파워를 낮은 쪽부터 훑으며 각 후보를 실제로 시뮬레이션해
        /// 조준점에 가장 가까이 떨어지는 것을 채택한다(낮은 파워 = 짧은 비행 = 바람 영향 적음).
        /// </summary>
        /// <param name="air">
        /// 기후(증폭벽·회오리). 🚨 **반드시 넘겨라.**
        /// 2026-09-19 까지 이 인자 자체가 없어서 AI 는 **기후가 없는 하늘에서 조준을 검증**하고
        /// 실제 발사는 기후가 있는 하늘로 나갔다 — 회오리에 먹히는 해를 "검증통과"로 골라 헛발을 쐈다.
        /// 연습장 역산(`TrySolvePractice`)이 거짓말하던 것과 **똑같은 결함**이고, 거기서 이 자리를 찾았다.
        /// **조준 검증과 실제 발사는 같은 하늘을 봐야 한다.**
        /// </param>
        public static AimPlan Decide(SdfVolume vol, Vec3 from, IReadOnlyList<Target> enemies, Vec3 wind,
                                     float errorRatio, ref Rng rng, float mapSize = 200f,
                                     TankStats? tank = null, AirField air = null)
        {
            // 탱크 종류가 사거리·탄도·사각을 바꾼다(§9-3). 안 주면 밸런스 기준값.
            var st = tank ?? TankStats.Get(TankKind.Carrot);   // 기준 탱크(원작 초심자용 평준화)
            float minPitch = st.MinPitch, maxPitch = st.MaxPitch;
            var plan = new AimPlan { TargetId = -1 };
            if (enemies == null || enemies.Count == 0) return plan;

            // 목표 선택: 가장 가까운 적
            Vec3 aim = default; float best = float.MaxValue; int bestId = -1;
            for (int i = 0; i < enemies.Count; i++)
            {
                float d = (enemies[i].Center - from).LengthSq;
                if (d < best) { best = d; aim = enemies[i].Center; bestId = enemies[i].Id; }
            }
            plan.TargetId = bestId;

            // 난이도 오차: 목표점을 수평 원판 안에서 흔든 뒤 **그 자리 지면 위로 되올린다**.
            //
            // ⚠️ 높이를 목표의 Y 그대로 두면 안 된다. 비탈에 선 적을 7m 옆으로 흔들면
            //    그 점이 산 **속**이라 어떤 탄도로도 닿지 않는다. 그러면 검증 탐색이
            //    "그나마 덜 빗나간 차폐 사격"을 고르고, 오차를 조금 올렸을 뿐인데
            //    한 판이 12분 → 106분으로 무너진다(실측: 차폐율 26% → 73%).
            //    사람은 바위 속을 조준하지 않는다. 지면 위의 엉뚱한 지점을 조준한다.
            float dist = MathF.Sqrt(best);
            float r = dist * errorRatio;
            if (r > 0f)
            {
                float ang = rng.Range(0f, MathF.PI * 2f);
                float rad = r * MathF.Sqrt(rng.Float01());       // 원판 균일 분포
                float ax = aim.X + MathF.Cos(ang) * rad, az = aim.Z + MathF.Sin(ang) * rad;
                float ay = aim.Y;
                if (vol != null)
                {
                    float g = TankGroundProbe.GroundBelow(vol, ax, az, aim.Y + rad + 30f);
                    if (!float.IsNegativeInfinity(g)) ay = g + 1.2f;   // 차체 중심 높이
                }
                aim = new Vec3(ax, ay, az);
            }

            var accel = st.AccelWith(wind.X, wind.Z);

            // 후보 중 **실제로 쏴 보고** 조준점에 가장 가까이 떨어지는 것을 고른다.
            //
            // ⚠️ "해가 존재하는 첫 파워"를 그냥 쓰면 안 된다. 그건 최소에너지 해(≈45°)라
            //    사이에 언덕이 있으면 산허리에 그대로 박는다. 해석 해는 지형을 모른다.
            //    실측: 이 검증을 안 하면 오차 0 인데도 평균 탄착오차가 24.9m 였다(폭발 반경의 3.5배).
            //    vol 이 null 이면 검증 없이 옛 동작(해석 해 우선)으로 떨어진다.
            float bestMiss = float.MaxValue;
            for (float pw = 0.30f; pw <= 1.0001f; pw += 0.025f)
            {
                float speed = st.SpeedAt(pw);
                if (!Ballistics.SolveLaunchAngles(from, aim, speed, accel, st.Flight, out var lo, out var hi)) continue;   // 추진 탄은 등가 포물선으로 정확히

                // 고각 우선 — 언덕을 넘긴다(§5-7). 범위를 벗어나면 저각.
                foreach (var sol in new[] { hi, lo })
                {
                    if (sol.PitchDeg < minPitch || sol.PitchDeg > maxPitch) continue;

                    if (vol == null)
                    {
                        plan.Valid = true;
                        plan.PredictedMiss = float.MaxValue;   // vol 이 없으면 검증 자체를 못 한다
                        plan.YawDeg = sol.YawDeg; plan.PitchDeg = sol.PitchDeg; plan.Power = pw;
                        return plan;
                    }

                    var vel = Ballistics.VelocityFrom(sol.YawDeg, sol.PitchDeg, speed);
                    var sr = ProjectileSimulator.Simulate(vol, from, vel, accel, null, -1, mapSize, st.Flight, null, air);
                    float miss = sr.Hit ? (sr.Impact - aim).Length : float.MaxValue;
                    if (miss >= bestMiss) continue;

                    bestMiss = miss;
                    plan.Valid = true;
                    plan.PredictedMiss = miss;
                    plan.YawDeg = sol.YawDeg; plan.PitchDeg = sol.PitchDeg; plan.Power = pw;
                    if (miss < 1.5f) return plan;       // 충분히 정확하면 더 안 찾는다
                }
            }
            if (plan.Valid) return plan;

            // 해가 없으면 최대 파워로 대충 쏜다(사거리 부족)
            Vec3 d2 = aim - from;
            plan.Valid = true;
            plan.PredictedMiss = float.MaxValue;   // 검증을 통과한 해가 아니다 — 맞는다고 믿지 않는다
            plan.Power = 1f;
            plan.PitchDeg = MathF.Min(55f, maxPitch);
            plan.YawDeg = MathF.Atan2(d2.X, d2.Z) * (180f / MathF.PI);
            return plan;
        }
    }
}
