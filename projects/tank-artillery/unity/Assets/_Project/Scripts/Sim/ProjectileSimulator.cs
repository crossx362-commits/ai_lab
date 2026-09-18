// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §5-5, §6
//
// 궤적은 닫힌 해지만(§5-4) **충돌은 이산 검사**가 필요하다.
// 서버가 이걸 돌리고, 클라는 결과(path)를 재생만 한다 — 기획서 §78.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>충돌 대상 탱크. SIM은 유니티를 모르므로 좌표만 받는다.</summary>
    public struct TankHitbox
    {
        public int Id;
        public Vec3 Center;      // 차체 중심(지면 + 높이 절반)
        public float Radius;     // 캡슐 근사 반경
    }

    public struct ShotResult
    {
        public bool Hit;
        public Vec3 Impact;
        public float FlightTime;
        public int DirectHitTankId;      // 직격이면 탱크 id, 아니면 -1
        public List<Vec3> Path;          // 연출용 궤적 샘플
        /// <summary>증폭벽(§2-9-15)을 지났으면 1.5, 아니면 1. 피해 계산이 곱한다.</summary>
        public float DamageScale;
        /// <summary>회오리(§2-9-15)에 휘말렸는가. 연출·로그용.</summary>
        public bool Tornadoed;
    }

    public static class ProjectileSimulator
    {
        /// <summary>
        /// 포구가 지형에 박혔을 때 "빠져나갈 수 있다"고 봐 주는 거리(m) [추정].
        /// 포신 길이가 기종별로 2.0~5.0m 이고(ProceduralTank), 크레이터 벽·언덕은 그보다 두껍다 —
        /// 그래서 이 값 안에서 공기가 나오면 **턱에 살짝 걸린 것**, 안 나오면 **진짜로 묻힌 것**으로 가른다.
        /// 키우면 벽에 대고 쏴도 뚫고 나가고, 줄이면 평범한 사격이 자폭이 된다.
        /// </summary>
        public const float MuzzleEscape = 3.0f;

        /// <summary>
        /// 발사 → 착탄. 지형·탱크 충돌을 모두 본다.
        ///
        /// ⚠️ 지형 판정은 SDF **부호 변화**만 쓴다. 오버행·동굴이 있으므로
        ///    "포탄이 지면 아래로 내려갔다"는 판정은 성립하지 않는다(§5-5).
        ///    부수 효과로 동굴 안으로 쏘아 넣는 사격이 자동으로 된다.
        /// </summary>
        public static ShotResult Simulate(SdfVolume vol, Vec3 p0, Vec3 v0, Vec3 accel,
                                          IReadOnlyList<TankHitbox> tanks, int shooterId,
                                          float mapSize, AirField air = null)
            => Simulate(vol, p0, v0, accel, tanks, shooterId, mapSize, default, null, air);

        /// <summary>
        /// 프로파일(추진·유도) + 기후 포함. 유도는 **정점을 지난 뒤** homingOk 인 탱크 중 HomingRange 안·전방의 가장 가까운 것을 향해
        /// 초당 HomingTurnDeg 만큼 속도를 꺾는다(고정 스텝이라 결정론). 표적이 없으면 프로파일 궤적 그대로.
        ///
        /// ⚠️ 두 기능(추진·유도 / 증폭벽·회오리)이 **같은 루프에서 만난다**. 합칠 때 정한 것:
        ///    · 회오리에 빨려 올라가면 **추진은 끝난 것으로 본다**(fp 의 추진만 지운다). 꼭대기에서 tBase 를 t 로 옮기는데,
        ///      프로파일을 그대로 두면 `t-tBase=0` 이라 **연소가 처음부터 다시 시작**해 회오리가 사거리 증폭기가 된다.
        ///    · 유도는 회오리 뒤에도 살린다(추진과 달리 연료가 아니라 제어다) — 단 진행 중이던 유도는 끊고 다시 잡게 한다.
        /// </summary>
        public static ShotResult Simulate(SdfVolume vol, Vec3 p0, Vec3 v0, Vec3 accel,
                                          IReadOnlyList<TankHitbox> tanks, int shooterId,
                                          float mapSize, FlightProfile fp, Func<int, bool> homingOk,
                                          AirField air = null)
        {
            var path = new List<Vec3>(256) { p0 };
            var res = new ShotResult { DirectHitTankId = -1, Path = path, DamageScale = 1f };

            // ⚠️ 포구가 **지형 안에 박힌 채** 쏘는 경우(구덩이 안, 비탈에 포신이 박힘 — §7-6 은 깊은 구덩이를
            //    의도된 설계로 둔다). 아래 지형 판정은 **공기→지형 부호 전환**(`sdf<=0 && prevSdf>0`)만 본다.
            //    시작점이 이미 지형 속이면 그 전환이 영영 안 와서 **탄이 땅속을 그대로 지나간다** —
            //    오너 보고 "발사체가 지형을 그냥 통과한다"(2026-09-18). 실측: 묻힌 포구에서 쏜 432발 전부 관통.
            //
            //    조사한 표준 두 가지가 같은 답을 준다:
            //      · PhysX GeometryQueries — 레이 원점이 solid 안이면 **거리 0·원점에 맞은 것**으로 보고한다.
            //        https://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/3.3.4/Manual/GeometryQueries.html
            //      · 포격 게임 관례(웜즈류) — 탄은 지형에 닿으면 터지므로 벽에 대고 쏘면 그 자리에서 터지고
            //        자기도 맞는다. https://worms.fandom.com/wiki/Shotgun
            //
            //    다만 "묻혔으면 무조건 즉시 폭발"로 두면 포구가 턱에 **살짝** 걸린 평범한 사격까지 자폭이 된다.
            //    그래서 포신 길이만큼(MuzzleEscape) 앞을 훑어 공기가 나오면 거기서부터 날리고,
            //    그 안에서 못 벗어나면 진짜로 묻힌 것이라 포구에서 터뜨린다.
            Vec3 start = p0;
            if (vol.SampleWorld(p0.X, p0.Y, p0.Z) <= 0f)
            {
                Vec3 dir = v0.Normalized;
                bool escaped = false;
                for (float d = vol.Voxel; d <= MuzzleEscape && !escaped; d += vol.Voxel)
                {
                    Vec3 q = p0 + dir * d;
                    if (vol.SampleWorld(q.X, q.Y, q.Z) > 0f) { start = q; escaped = true; }
                }
                if (!escaped)
                {
                    res.Hit = true; res.Impact = p0; res.FlightTime = 0f;
                    return res;                       // 포구에서 터진다 — 폭발 피해는 호출부가 준다(자해 포함)
                }
                path.Add(start);
            }

            Vec3 prev = start;
            float prevSdf = vol.SampleWorld(start.X, start.Y, start.Z);
            // 회오리는 궤적을 한 번 끊고 다시 잇는다(머리말 참조). 무한 반복을 막으려 한 판에 한 번만.
            Vec3 org = start, vel = v0;
            float tBase = 0f;
            int lifts = 0;
            bool homing = false; Vec3 hv = default; int homeTarget = -1;

            for (float t = Ballistics.SimStep; t <= Ballistics.MaxFlightSec; t += Ballistics.SimStep)
            {
                float tf = t - tBase;                       // 이번 비행 구간의 경과(회오리 뒤엔 다시 0부터)
                Vec3 cur;
                if (!homing)
                {
                    cur = Ballistics.PositionAt(org, vel, accel, tf, fp);
                    if (fp.HasHoming && tanks != null && Ballistics.VelocityAt(vel, accel, tf, fp).Y < 0f)
                    {
                        float best = fp.HomingRange * fp.HomingRange;
                        Vec3 fwd = (cur - prev).Normalized;
                        for (int i = 0; i < tanks.Count; i++)
                        {
                            var tk = tanks[i];
                            if (tk.Id == shooterId || (homingOk != null && !homingOk(tk.Id))) continue;
                            Vec3 d = tk.Center - cur;
                            if (Vec3.Dot(d, fwd) <= 0f) continue;                 // 뒤에 있는 건 못 쫓는다
                            float d2 = d.LengthSq;
                            if (d2 < best) { best = d2; homeTarget = i; }
                        }
                        if (homeTarget >= 0) { homing = true; hv = Ballistics.VelocityAt(vel, accel, tf, fp); }
                    }
                }
                else
                {
                    float dt = Ballistics.SimStep;
                    Vec3 want = (tanks[homeTarget].Center - prev).Normalized;
                    float spd = hv.Length; Vec3 dir = spd > 1e-6f ? hv * (1f / spd) : want;
                    float maxRad = fp.HomingTurnDeg * (MathF.PI / 180f) * dt;
                    float c = MathF.Max(-1f, MathF.Min(1f, Vec3.Dot(dir, want)));
                    float ang = MathF.Acos(c);
                    if (ang > maxRad && ang > 1e-4f)
                    {
                        // dir 을 want 쪽으로 maxRad 만큼 회전(두 벡터 평면 안에서)
                        Vec3 perp = (want - dir * c).Normalized;
                        dir = dir * MathF.Cos(maxRad) + perp * MathF.Sin(maxRad);
                    }
                    else dir = want;
                    hv = dir * spd + accel * dt;
                    cur = prev + hv * dt;
                }

                // --- 기후(§2-9-15) ---
                if (air != null)
                {
                    // 증폭벽: 지나가면 피해가 증폭된다(원작 "대미지가 50% 증폭된다"). 한 번만 곱한다.
                    if (res.DamageScale == 1f && air.CrossesWall(prev, cur)) res.DamageScale = AirField.AmpScale;
                    // 회오리: 빨려 올라갔다가 떨어진다. 꼭대기에서 거의 수직으로 다시 쏜 것처럼 잇는다.
                    if (lifts == 0 && air.EntersTornado(prev, cur, out var tor))
                    {
                        lifts++;
                        res.Tornadoed = true;
                        org = new Vec3(tor.X, tor.TopY, tor.Z);
                        vel = new Vec3(vel.X * 0.12f, 0f, vel.Z * 0.12f);   // 위로 빨린 뒤 거의 멈춘다 [추정]
                        tBase = t;
                        // ⚠️ 추진을 안 지우면 tf 가 0 으로 돌아가며 **연소가 다시 시작**한다 — 회오리가 사거리 증폭기가 된다.
                        //    유도는 남긴다(연료가 아니라 제어다). 진행 중이던 유도는 끊고 낙하 중에 다시 잡게 한다.
                        fp.Thrust = 0f; fp.BoostSec = 0f;
                        homing = false; homeTarget = -1;
                        path.Add(new Vec3(tor.X, tor.TopY, tor.Z));
                        prev = org;
                        prevSdf = vol.SampleWorld(org.X, org.Y, org.Z);
                        continue;
                    }
                }

                // --- 탱크 직격 ---
                if (tanks != null)
                    for (int i = 0; i < tanks.Count; i++)
                    {
                        var tk = tanks[i];
                        if (tk.Id == shooterId) continue;
                        if (SegmentPointDistSq(prev, cur, tk.Center) <= tk.Radius * tk.Radius)
                        {
                            res.Hit = true; res.Impact = tk.Center; res.FlightTime = t;
                            res.DirectHitTankId = tk.Id;
                            path.Add(tk.Center);
                            return res;
                        }
                    }

                // --- 지형: 세그먼트를 복셀 크기로 재분할해 부호 변화를 찾는다 ---
                // 세그먼트(최대 0.68m)가 복셀(0.5m)보다 길어 얇은 능선·처마를 뛰어넘을 수 있다(§5-5).
                float segLen = (cur - prev).Length;
                int sub = Math.Max(1, (int)MathF.Ceiling(segLen / vol.Voxel));
                for (int s = 1; s <= sub; s++)
                {
                    float f = s / (float)sub;
                    Vec3 q = prev + (cur - prev) * f;
                    float sdf = vol.SampleWorld(q.X, q.Y, q.Z);
                    if (sdf <= 0f && prevSdf > 0f)
                    {
                        Vec3 a = prev + (cur - prev) * ((s - 1) / (float)sub);
                        res.Impact = Refine(vol, a, q);
                        res.Hit = true;
                        res.FlightTime = t;
                        path.Add(res.Impact);
                        return res;
                    }
                    prevSdf = sdf;
                }

                path.Add(cur);
                prev = cur;

                // 맵 밖으로 나가면 불발
                if (cur.Y < vol.OriginY || cur.X < -20f || cur.Z < -20f ||
                    cur.X > mapSize + 20f || cur.Z > mapSize + 20f)
                    return res;
            }
            return res;
        }

        static Vec3 Refine(SdfVolume vol, Vec3 air, Vec3 solid)
        {
            for (int i = 0; i < 8; i++)
            {
                Vec3 mid = (air + solid) * 0.5f;
                if (vol.SampleWorld(mid.X, mid.Y, mid.Z) > 0f) air = mid; else solid = mid;
            }
            return solid;
        }

        static float SegmentPointDistSq(Vec3 a, Vec3 b, Vec3 p)
        {
            Vec3 ab = b - a, ap = p - a;
            float len2 = ab.LengthSq;
            if (len2 < 1e-9f) return ap.LengthSq;
            float t = Vec3.Dot(ap, ab) / len2;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return (ap - ab * t).LengthSq;
        }
    }

    /// <summary>§6 피해 계산.</summary>
    public static class Damage
    {
        public const float FalloffInnerRatio = 0.21f;
        public const float FalloffExponent = 1.3f;

        /// <summary>
        /// 거리 감쇠(§6-1). 기획서 §39 표(300/250/150/40)를 한 줄 공식으로 재현한 것 —
        /// R=7·Base=300 에서 계산값이 300/263/135/32 로 맞는다.
        /// 무기별로 바꾸지 마라. 곡선이 무기마다 다르면 플레이어가 학습할 수 없다.
        /// </summary>
        public static float Falloff(float distance, float radius)
        {
            float inner = radius * FalloffInnerRatio;
            if (distance <= inner) return 1f;
            if (distance >= radius) return 0f;
            float t = (distance - inner) / (radius - inner);
            return MathF.Pow(1f - t, FalloffExponent);
        }

        // ── 낙하 피해(§28) ──────────────────────────────────────────────────────
        // ⚠️ **왜 이게 있어야 하는가 — 12종 매치업 실측이 요구했다.**
        //    굴착 반경이 큰 탱크가 예외 없이 바닥에 깔렸다:
        //      이온어태커 굴착 12.0m → 10%   캐터펄트 9.0m → 28%   마인랜더 8.5m → 38%
        //      ↔ 듀크 6.5m → 94%   캐롯 7.0m → 85%
        //    지형은 양 팀이 같이 쓰는 자원이라, 크게 파면 §2-5-1 의 되먹임 나선이
        //    **먼저 자기 명중률을 무너뜨린다.** 그런데 §28 "발밑을 도려내 떨어뜨린다"의
        //    **보상이 코드에 아예 없었다** — 떨어져도 재접지만 하고 피해는 0이었다.
        //    즉 굴착은 순수 자해였고, 이건 수치를 아무리 만져도 못 고친다. 빠진 기능이었다.
        //
        // ⚠️ 무료 구간이 필요하다. 착탄마다 지면이 몇 cm 씩 꺼지는데 그걸 전부 피해로 세면
        //    아무도 안 판 판에서도 체력이 줄어든다(원인 못 찾는 버그가 된다).
        //
        // ⚠️ 2026-09-17 재조정 — **AI 이동이 들어오고 나서** 낙하 피해가 판을 지배하고 있었다(§AiMover 배선).
        //    실측: 멀티미사일은 한 판 낙하 피해 635 인데 직사(폭발) 피해는 한 사격당 178 이었다.
        //    즉 **쏘는 것보다 파는 것이 이겼다.** 크로스보우는 명중률 75%(1위)에 사격당 피해 215(상위)인데도
        //    굴착 4.5m 라 낙하 피해가 81 뿐이라 승률 17% 로 무너졌다 — 잘 쏘는 것이 보상받지 못하는 판이었다.
        //    26→17, 상한 420→300 으로 낮춰 "파는 것은 보상이지 승리 조건이 아니다"로 되돌린다.
        //    ⚠️ 0 으로 만들지 마라. §28 보상이 없으면 굴착이 순수 자해가 되어 반대쪽으로 무너진다
        //       (이온 10% · 캐터펄트 28% 였던 상태 — 이 상수가 생긴 이유다).
        public const float FallFreeMeters = 3f;
        public const float FallDamagePerMeter = 17f;
        public const float FallDamageMax = 300f;

        /// <summary>발밑이 사라져 떨어진 높이(m)에 대한 피해. 3m 까지는 무피해.</summary>
        public static int FromFall(float dropMeters)
        {
            float over = dropMeters - FallFreeMeters;
            if (over <= 0f) return 0;
            return (int)MathF.Round(MathF.Min(over * FallDamagePerMeter, FallDamageMax));
        }

        /// <summary>
        /// 방어력 적용(원작 스탯). 받는 피해 = 원피해 × 100 / 방어력.
        /// 검증: "슈퍼탱크 전 속성 0.8배" = 100/125. 피해를 주는 모든 경로가 이걸 통과해야 한다.
        /// </summary>
        public static int AfterDefense(int raw, float defense)
            => defense <= 0f ? raw : (int)MathF.Round(raw * 100f / defense);

        /// <summary>폭발 피해 + 직격 보너스(§6-2).</summary>
        public static int Compute(float distance, float radius, float baseDamage,
                                  float directDamage, bool isDirect)
        {
            float d = baseDamage * Falloff(distance, radius);
            if (isDirect) d += directDamage;
            return (int)MathF.Round(d);
        }
    }
}
