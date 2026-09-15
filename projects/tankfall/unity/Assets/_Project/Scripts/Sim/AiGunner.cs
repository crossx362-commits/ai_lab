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
        public const float ErrorNovice = 0.04f;    // 실측 68% 명중 · 11.9분 — 사람이 이기는 난이도
        public const float ErrorNormal = 0.025f;   // 실측 84% · 7.3분 — 기본값
        public const float ErrorExpert = 0.015f;   // 실측 99%
        public const float ErrorAce = 0.0075f;     // 실측 100% — 사실상 빗나가지 않는다

        public struct Target
        {
            public int Id;
            public Vec3 Center;
        }

        /// <summary>
        /// 가장 가까운 적을 골라 조준한다. 파워를 낮은 쪽부터 훑으며 각 후보를 실제로 시뮬레이션해
        /// 조준점에 가장 가까이 떨어지는 것을 채택한다(낮은 파워 = 짧은 비행 = 바람 영향 적음).
        /// </summary>
        public static AimPlan Decide(SdfVolume vol, Vec3 from, IReadOnlyList<Target> enemies, Vec3 wind,
                                     float errorRatio, ref Rng rng, float mapSize = 200f,
                                     float minPitch = -5f, float maxPitch = 80f)
        {
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

            var accel = Ballistics.Accel(wind.X, wind.Z);

            // 후보 중 **실제로 쏴 보고** 조준점에 가장 가까이 떨어지는 것을 고른다.
            //
            // ⚠️ "해가 존재하는 첫 파워"를 그냥 쓰면 안 된다. 그건 최소에너지 해(≈45°)라
            //    사이에 언덕이 있으면 산허리에 그대로 박는다. 해석 해는 지형을 모른다.
            //    실측: 이 검증을 안 하면 오차 0 인데도 평균 탄착오차가 24.9m 였다(폭발 반경의 3.5배).
            //    vol 이 null 이면 검증 없이 옛 동작(해석 해 우선)으로 떨어진다.
            float bestMiss = float.MaxValue;
            for (float pw = 0.30f; pw <= 1.0001f; pw += 0.025f)
            {
                float speed = Ballistics.PowerToSpeed(pw);
                if (!Ballistics.SolveLaunchAngles(from, aim, speed, accel, out var lo, out var hi)) continue;

                // 고각 우선 — 언덕을 넘긴다(§5-7). 범위를 벗어나면 저각.
                foreach (var sol in new[] { hi, lo })
                {
                    if (sol.PitchDeg < minPitch || sol.PitchDeg > maxPitch) continue;

                    if (vol == null)
                    {
                        plan.Valid = true;
                        plan.YawDeg = sol.YawDeg; plan.PitchDeg = sol.PitchDeg; plan.Power = pw;
                        return plan;
                    }

                    var vel = Ballistics.VelocityFrom(sol.YawDeg, sol.PitchDeg, speed);
                    var sr = ProjectileSimulator.Simulate(vol, from, vel, accel, null, -1, mapSize);
                    float miss = sr.Hit ? (sr.Impact - aim).Length : float.MaxValue;
                    if (miss >= bestMiss) continue;

                    bestMiss = miss;
                    plan.Valid = true;
                    plan.YawDeg = sol.YawDeg; plan.PitchDeg = sol.PitchDeg; plan.Power = pw;
                    if (miss < 1.5f) return plan;       // 충분히 정확하면 더 안 찾는다
                }
            }
            if (plan.Valid) return plan;

            // 해가 없으면 최대 파워로 대충 쏜다(사거리 부족)
            Vec3 d2 = aim - from;
            plan.Valid = true;
            plan.Power = 1f;
            plan.PitchDeg = 55f;
            plan.YawDeg = MathF.Atan2(d2.X, d2.Z) * (180f / MathF.PI);
            return plan;
        }
    }
}
