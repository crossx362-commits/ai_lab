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
    }

    public static class ProjectileSimulator
    {
        /// <summary>
        /// 발사 → 착탄. 지형·탱크 충돌을 모두 본다.
        ///
        /// ⚠️ 지형 판정은 SDF **부호 변화**만 쓴다. 오버행·동굴이 있으므로
        ///    "포탄이 지면 아래로 내려갔다"는 판정은 성립하지 않는다(§5-5).
        ///    부수 효과로 동굴 안으로 쏘아 넣는 사격이 자동으로 된다.
        /// </summary>
        public static ShotResult Simulate(SdfVolume vol, Vec3 p0, Vec3 v0, Vec3 accel,
                                          IReadOnlyList<TankHitbox> tanks, int shooterId,
                                          float mapSize)
            => Simulate(vol, p0, v0, accel, tanks, shooterId, mapSize, default, null);

        /// <summary>
        /// 프로파일(추진·유도) 포함. 유도는 **정점을 지난 뒤** homingOk 인 탱크 중 HomingRange 안·전방의 가장 가까운 것을 향해
        /// 초당 HomingTurnDeg 만큼 속도를 꺾는다(고정 스텝이라 결정론). 표적이 없으면 프로파일 궤적 그대로.
        /// </summary>
        public static ShotResult Simulate(SdfVolume vol, Vec3 p0, Vec3 v0, Vec3 accel,
                                          IReadOnlyList<TankHitbox> tanks, int shooterId,
                                          float mapSize, FlightProfile fp, Func<int, bool> homingOk)
        {
            var path = new List<Vec3>(256) { p0 };
            var res = new ShotResult { DirectHitTankId = -1, Path = path };

            Vec3 prev = p0;
            float prevSdf = vol.SampleWorld(p0.X, p0.Y, p0.Z);
            bool homing = false; Vec3 hv = default; int homeTarget = -1;

            for (float t = Ballistics.SimStep; t <= Ballistics.MaxFlightSec; t += Ballistics.SimStep)
            {
                Vec3 cur;
                if (!homing)
                {
                    cur = Ballistics.PositionAt(p0, v0, accel, t, fp);
                    if (fp.HasHoming && tanks != null && Ballistics.VelocityAt(v0, accel, t, fp).Y < 0f)
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
                        if (homeTarget >= 0) { homing = true; hv = Ballistics.VelocityAt(v0, accel, t, fp); }
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
        public const float FallFreeMeters = 3f;
        public const float FallDamagePerMeter = 26f;
        public const float FallDamageMax = 420f;

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
