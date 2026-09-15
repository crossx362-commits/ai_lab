// AI vs AI 자동 대전. 유니티 없이 돌려 **명중률·한 판 길이·승률**을 잰다.
//
// 재려는 것 (명세 §2-5, §11 M3/M4 게이트):
//   1) 난이도 오차가 실제로 얼마나 빗나가게 만드는가 (§67 튜닝)
//   2) 한 판이 끝나긴 하는가, 몇 턴 걸리는가 (기획서 §55: 8~15분)
//   3) 지형 파괴가 누적돼도 전투가 성립하는가
//
// ⚠️ 네거티브 컨트롤: 오차 0 이면 명중률이 크게 올라가야 한다. 안 오르면 측정이 고장 난 것이다.

using System;
using System.Collections.Generic;
using Tankfall.Sim;

static class BattleSimVerify
{
    const float Voxel = 0.5f, MapSize = 200f;
    const int ChunkN = 16;
    const int MaxHp = 1000;          // 기본값. RunMatch 가 판마다 덮어쓴다(HP 스윕용)
    const float BlastRadius = 7f, BaseDamage = 300f, DirectDamage = 100f;
    const float TankRadius = 2.0f;
    const float TurnSeconds = 18f;        // §2-1 2페이즈 턴의 현실적 평균

    // §2-5 레버 2 — 실측 후 유일하게 살아남은 레버. 되먹임 나선(§2-5-1)이 폭주하기 전에 판을 끝낸다.
    // 3v3 이므로 1라운드 = 6턴. 12라운드 = 72턴.
    // ⚠️ §2-5 초안의 12라운드(72턴)는 **너무 늦다**. [4] 실측상 맵은 40턴(약 7라운드)에
    //    명중률 4.2% 로 죽는다. 임계값을 그 앞에 둬야 나선을 끊는다.
    const float SuddenDeathPct = 0.05f;   // 매 턴 전원 최대HP의 5%
    const int SuddenDeathTurn = 36;       // 6라운드 × 6명. BattleDemo.SuddenDeathRound 와 같은 값이어야 한다

    static float Hills(float x, float z)
    {
        float h = 4f;
        h += 22f * MathF.Exp(-(((x - 60f) * (x - 60f) + (z - 100f) * (z - 100f)) / 900f));
        h += 20f * MathF.Exp(-(((x - 145f) * (x - 145f) + (z - 105f) * (z - 105f)) / 800f));
        h += 2.5f * MathF.Sin(x * 0.06f) * MathF.Cos(z * 0.05f);
        return h;
    }

    sealed class U
    {
        public int Id, Team, Hp;
        public float X, Y, Z;
        public bool Alive => Hp > 0;
        public Vec3 Center => new Vec3(X, Y + 1.2f, Z);
        public Vec3 Muzzle => new Vec3(X, Y + 2.4f, Z);
    }

    struct MatchResult
    {
        public int Winner, Shots, Hits, Turns;
        public int[] BucketShots, BucketHits;   // 턴 구간별(0-19,20-39,...) — 나선 정량화
        public int Landed;        // 조준 대상 근처(3R 이내)에 떨어진 사격 — 탄착오차의 분모
        public int Blocked;       // 지형에 막혀 중간에 박힌 사격
        public float TotalMiss;   // Landed 사격만 누적
        public bool Timeout;
    }

    static MatchResult RunMatch(uint seed, float errorRatio, int hp = MaxHp, int maxTurns = 400,
                                int suddenDeathTurn = 0, float craterRadius = BlastRadius)
    {
        var rng = new Rng(seed);
        var vol = new SdfVolume(Voxel, ChunkN, -20f, Hills, (int)(MapSize / Voxel));
        var units = new List<U>();
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < 3; i++)
            {
                float x = t == 0 ? 55f + i * 14f : 150f + i * 12f;
                float z = t == 0 ? 40f + i * 8f : 155f - i * 9f;
                float g = TankGroundProbe.GroundBelow(vol, x, z, 60f);
                units.Add(new U { Id = t * 3 + i, Team = t, Hp = hp, X = x, Z = z, Y = float.IsNegativeInfinity(g) ? 10f : g });
            }

        var res = new MatchResult { Winner = -1, BucketShots = new int[8], BucketHits = new int[8] };
        var wind = new Vec3(0, 0, 0);
        int turn = 0;

        for (int step = 0; step < maxTurns; step++)
        {
            // 라운드마다 바람 갱신(§15)
            if (step % 6 == 0)
            {
                float a = rng.Range(0f, MathF.PI * 2f), s = rng.Range(0f, 10f);
                wind = new Vec3(MathF.Cos(a) * s, 0f, MathF.Sin(a) * s);
            }

            // 다음 생존자
            int guard = 0;
            while (!units[turn % units.Count].Alive && guard++ < 12) turn++;
            var u = units[turn % units.Count];
            turn++;
            if (!u.Alive) break;
            res.Turns++;

            // 서든데스: 상한 턴을 넘기면 매 턴 전원이 깎인다. 나선이 폭주해도 판은 끝난다.
            if (suddenDeathTurn > 0 && res.Turns > suddenDeathTurn)
            {
                int tick = (int)(hp * SuddenDeathPct);
                foreach (var o in units) if (o.Alive) o.Hp = Math.Max(0, o.Hp - tick);
                int sa = 0, sb = 0;
                foreach (var o in units) { if (!o.Alive) continue; if (o.Team == 0) sa++; else sb++; }
                if (sa == 0 || sb == 0) { res.Winner = sa > 0 ? 0 : (sb > 0 ? 1 : -1); return res; }
            }

            var enemies = new List<AiGunner.Target>();
            foreach (var o in units) if (o.Alive && o.Team != u.Team) enemies.Add(new AiGunner.Target { Id = o.Id, Center = o.Center });
            if (enemies.Count == 0) break;

            var plan = AiGunner.Decide(vol, u.Muzzle, enemies, wind, errorRatio, ref rng, MapSize);
            if (!plan.Valid) continue;

            float speed = Ballistics.PowerToSpeed(plan.Power);
            var v0 = Ballistics.VelocityFrom(plan.YawDeg, plan.PitchDeg, speed);
            var accel = Ballistics.Accel(wind.X, wind.Z);

            var boxes = new List<TankHitbox>();
            foreach (var o in units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });

            var shot = ProjectileSimulator.Simulate(vol, u.Muzzle, v0, accel, boxes, u.Id, MapSize);
            res.Shots++;
            int bk = Math.Min(7, res.Turns / 20);
            res.BucketShots[bk]++;
            if (!shot.Hit) continue;

            // ⚠️ 탄착오차는 **목표 근처에 떨어진 사격만** 평균 내야 한다.
            //    지형에 막혀 산허리에 박힌 탄(수십~백 m 단위)을 같이 넣으면 평균이 조준 정확도가 아니라
            //    "차폐 비율"을 재는 숫자가 된다. 실제로 그래서 오차 0 대조군이 24.9m 로 나왔다.
            foreach (var e in enemies)
                if (e.Id == plan.TargetId)
                {
                    float d = (shot.Impact - e.Center).Length;
                    if (d <= BlastRadius * 3f) { res.TotalMiss += d; res.Landed++; }
                    else res.Blocked++;
                }

            // 지형 파괴 + 천장 붕괴
            var blast = SdfDeformer.SubtractSphere(vol, new BlastRequest(shot.Impact.X, shot.Impact.Y, shot.Impact.Z, craterRadius));
            CeilingCollapse.Apply(vol, blast);

            bool anyHit = false;
            foreach (var o in units)
            {
                if (!o.Alive) continue;
                float dist = (o.Center - shot.Impact).Length;
                bool direct = o.Id == shot.DirectHitTankId;
                if (dist > BlastRadius && !direct) continue;
                int dmg = Damage.Compute(dist, BlastRadius, BaseDamage, DirectDamage, direct);
                if (dmg <= 0) continue;
                o.Hp = Math.Max(0, o.Hp - dmg);
                if (o.Team != u.Team) anyHit = true;
            }
            if (anyHit) { res.Hits++; res.BucketHits[bk]++; }

            // 지형이 꺼졌을 수 있으니 재접지
            foreach (var o in units)
                if (o.Alive)
                {
                    float g = TankGroundProbe.GroundBelow(vol, o.X, o.Z, o.Y + 2f);
                    if (!float.IsNegativeInfinity(g)) o.Y = g;
                }

            int a0 = 0, b0 = 0;
            foreach (var o in units) { if (!o.Alive) continue; if (o.Team == 0) a0++; else b0++; }
            if (a0 == 0 || b0 == 0) { res.Winner = a0 > 0 ? 0 : 1; return res; }
        }
        res.Timeout = true;
        return res;
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== AI 자동 대전 (3v3) ===\n");
        Console.WriteLine($"{"난이도",-8} {"오차",6} {"명중률",8} {"탄착오차",10} {"차폐",6} {"평균턴",7} {"예상시간",9} {"무승부",7}");

        var levels = new (string, float)[]
        {
            ("오차0(대조)", 0f),
            ("최상급", AiGunner.ErrorAce),
            ("상급", AiGunner.ErrorExpert),
            ("중급", AiGunner.ErrorNormal),
            ("초급", AiGunner.ErrorNovice),
        };

        foreach (var (name, err) in levels)
        {
            int matches = 40, shots = 0, hits = 0, turns = 0, timeouts = 0, landed = 0, blocked = 0;
            float miss = 0f;
            for (uint m = 0; m < matches; m++)
            {
                var r = RunMatch(1000 + m * 77, err, MaxHp, 400, SuddenDeathTurn);   // 출하 설정과 동일
                shots += r.Shots; hits += r.Hits; turns += r.Turns;
                landed += r.Landed; blocked += r.Blocked; miss += r.TotalMiss;
                if (r.Timeout) timeouts++;
            }
            float hitRate = shots > 0 ? hits * 100f / shots : 0f;
            float avgTurns = turns / (float)matches;
            float minutes = avgTurns * TurnSeconds / 60f;
            float blockRate = (landed + blocked) > 0 ? blocked * 100f / (landed + blocked) : 0f;
            Console.WriteLine($"{name,-8} {err * 100,5:F1}% {hitRate,7:F1}% {(landed > 0 ? miss / landed : 0),8:F1}m {blockRate,5:F0}% {avgTurns,6:F1} {minutes,7:F1}분 {timeouts,6}/{matches}");
        }

        // [2] 진단 — 파괴되지 않은 지형에서 단발 사격만. 전투 누적 효과를 뺀 순수 조준 정확도.
        //     [1]의 "차폐" 가 조준 탓인지, 300턴 쌓인 지형 파괴 탓인지 가른다.
        Console.WriteLine("");
        Console.WriteLine("[2] 단발 진단 (무손상 지형, 언덕 너머 사격 24발)");
        Console.WriteLine($"    {"오차",6} {"중앙값",9} {"90분위",9} {"최대",9} {"21m초과",8}");
        foreach (var (name, err) in levels)
        {
            var dvol = new SdfVolume(Voxel, ChunkN, -20f, Hills, (int)(MapSize / Voxel));
            var drng = new Rng(4242);
            var misses = new List<float>();
            for (int i = 0; i < 24; i++)
            {
                float sx = 55f + (i % 4) * 10f, sz = 40f + (i / 4) * 4f;
                float tx = 150f - (i % 3) * 9f, tz = 150f - (i / 6) * 7f;
                float sg = TankGroundProbe.GroundBelow(dvol, sx, sz, 60f);
                float tg = TankGroundProbe.GroundBelow(dvol, tx, tz, 60f);
                if (float.IsNegativeInfinity(sg) || float.IsNegativeInfinity(tg)) continue;
                var from = new Vec3(sx, sg + 2.4f, sz);
                var tc = new Vec3(tx, tg + 1.2f, tz);
                var tl = new List<AiGunner.Target> { new AiGunner.Target { Id = 9, Center = tc } };
                var plan = AiGunner.Decide(dvol, from, tl, new Vec3(0, 0, 0), err, ref drng, MapSize);
                if (!plan.Valid) { misses.Add(999f); continue; }
                var dv0 = Ballistics.VelocityFrom(plan.YawDeg, plan.PitchDeg, Ballistics.PowerToSpeed(plan.Power));
                var sh = ProjectileSimulator.Simulate(dvol, from, dv0, Ballistics.Accel(0f, 0f), null, -1, MapSize);
                misses.Add(sh.Hit ? (sh.Impact - tc).Length : 999f);
            }
            misses.Sort();
            int over = 0; foreach (var m in misses) if (m > BlastRadius * 3f) over++;
            Console.WriteLine($"    {err * 100,5:F1}% {misses[misses.Count / 2],8:F1}m {misses[(int)(misses.Count * 0.9f)],8:F1}m {misses[misses.Count - 1],8:F1}m {over,6}/{misses.Count}");
        }

        // [3] HP 스윕 — 판 길이의 **선형** 손잡이. 조준 오차는 쓸 수 있는 폭이 0.7%p 뿐이라
        //     난이도로 판 길이를 맞출 수 없다는 걸 [1]이 보여줬다. HP 는 선형이라 맞출 수 있다.
        Console.WriteLine("");
        Console.WriteLine("[3] HP 스윕 (§55 목표 8~15분 안에 드는 HP 찾기)");
        Console.WriteLine($"    {"HP",6} {"상급1.5%",20} {"중급3%",20}");
        foreach (int hp in new[] { 1000, 1400, 1800, 2200, 2600 })
        {
            var cells = new string[2];
            for (int li = 0; li < 2; li++)
            {
                float err = li == 0 ? 0.015f : 0.03f;
                int matches = 24, turns = 0, timeouts = 0, shots = 0, hits = 0;
                for (uint m = 0; m < matches; m++)
                {
                    var r = RunMatch(1000 + m * 77, err, hp);
                    turns += r.Turns; shots += r.Shots; hits += r.Hits;
                    if (r.Timeout) timeouts++;
                }
                float at = turns / (float)matches, mins = at * TurnSeconds / 60f;
                cells[li] = $"{mins,6:F1}분 {hits * 100f / Math.Max(1, shots),5:F0}% {(mins >= 8f && mins <= 15f && timeouts == 0 ? "OK" : "  "),-2}";
            }
            Console.WriteLine($"    {hp,6} {cells[0],20} {cells[1],20}");
        }
        Console.WriteLine("");

        // [4] 되먹임 나선 정량화 — 같은 AI·같은 오차인데 턴이 갈수록 명중률이 떨어지는가.
        //     떨어진다면 원인은 조준이 아니라 **누적된 지형 파괴**다. 판을 늘리는 모든 손잡이가 이걸 건드린다.
        Console.WriteLine("");
        Console.WriteLine("[4] 턴이 갈수록 명중률이 떨어지는가 (HP 1800, 오차 3%, 40판)");
        Console.WriteLine($"    {"턴 구간",10} {"사격",7} {"명중률",8}");
        {
            var bs = new int[8]; var bh = new int[8];
            for (uint m = 0; m < 40; m++)
            {
                var r = RunMatch(1000 + m * 77, 0.03f, 1800);
                for (int i = 0; i < 8; i++) { bs[i] += r.BucketShots[i]; bh[i] += r.BucketHits[i]; }
            }
            for (int i = 0; i < 8; i++)
            {
                if (bs[i] == 0) continue;
                string lbl = i < 7 ? $"{i * 20}-{i * 20 + 19}" : "140+";
                Console.WriteLine($"    {lbl,10} {bs[i],7} {bh[i] * 100f / bs[i],7:F1}%");
            }
        }
        Console.WriteLine("");

        // [5] 최종 조합 — 피해 반경(7m)은 고정하고 **지형을 파는 반경만** 줄여본다.
        //     나선의 연료는 크레이터 부피다. 부피는 r³ 이라 반경을 조금만 줄여도 크게 준다
        //     (7→5 m = 부피 36%). §21 구형 크레이터·§28 발밑 끊기는 그대로 유지된다.
        //     판 길이와 **실력 변별력**(오차별 명중률 격차)을 동시에 보는 게 핵심이다.
        Console.WriteLine("");
        Console.WriteLine($"[5] 지형 크레이터 반경 분리 (피해 반경은 {BlastRadius:F0}m 고정, HP {MaxHp}, 서든데스 6R)");
        Console.Write($"    {"오차",6}");
        foreach (float cr in new[] { 7f, 5.5f, 4.5f }) Console.Write($" {"굴착 " + cr.ToString("F1") + "m",17}");
        Console.WriteLine();
        foreach (float err in new[] { 0f, 0.015f, 0.025f, 0.04f })
        {
            Console.Write($"    {err * 100,5:F2}%");
            foreach (float cr in new[] { 7f, 5.5f, 4.5f })
            {
                int turns = 0, timeouts = 0, shots = 0, hits = 0;
                for (uint m = 0; m < 40; m++)
                {
                    var r = RunMatch(1000 + m * 77, err, MaxHp, 400, 36, cr);
                    turns += r.Turns; shots += r.Shots; hits += r.Hits;
                    if (r.Timeout) timeouts++;
                }
                float mins = turns / 40f * TurnSeconds / 60f;
                bool ok = mins >= 8f && mins <= 15f && timeouts == 0;
                Console.Write($" {mins,6:F1}분 {hits * 100f / Math.Max(1, shots),4:F0}% {(ok ? "OK" : "  "),-2}");
            }
            Console.WriteLine();
        }
        Console.WriteLine("    ※ 위아래 명중률 격차가 클수록 실력이 변별된다(§1). 격차가 없으면 지형이 실력을 덮은 것.");
        Console.WriteLine("");

        Console.WriteLine("※ 탄착오차 = 목표 21m(3R) 이내 탄만 평균. 차폐 = 지형에 막혀 그 밖에 박힌 비율.");
        Console.WriteLine("\n※ 명중률 = 적에게 피해를 준 사격 비율. 오차0 대조군이 가장 높아야 정상.");
        Console.WriteLine("※ 예상시간 = 평균턴 × 18초(§2-1 2페이즈 턴 평균). 기획서 §55 목표는 8~15분.");
    }
}
