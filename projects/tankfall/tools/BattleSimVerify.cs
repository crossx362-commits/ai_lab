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
    // 매치업 셀당 판 수. 10 은 승률 표준오차 ±16%p 라 미러(네거티브 컨트롤)를 읽을 수 없었다. 20 = ±11%p.
    const uint MatchesPerCell = 20;
    // ── 단일 행 실험 (TANKFALL_ROW=IonAttacker TANKFALL_VAR=CraterRadius TANKFALL_VALUES=12,9.5,8) ──
    // 한 번에 한 변수만 바꿔 그 행만 재측정한다. Sim 수치는 손대지 않고 하네스가 그 기종의 필드 하나를 덮는다.
    // TANKFALL_CRATER=a,b,c 는 VAR=CraterRadius 의 준말. 덮을 수 있는 필드는 SetVar 의 switch 에 있는 것뿐 — 없는 이름은 즉시 종료.
    static TankKind? OnlyRow;
    static string VarName;
    static float VarValue;
    static bool HasVar;
    static float GetVar(in TankStats t) => VarName switch
    {
        "CraterRadius" => t.CraterRadius, "BlastRadius" => t.BlastRadius, "BaseDamage" => t.BaseDamage, "DirectDamage" => t.DirectDamage,
        "Defense" => t.Defense, "Delay" => t.Delay, "Hp" => t.Hp,
        "MinPitch" => t.MinPitch, "MaxPitch" => t.MaxPitch, "GravityScale" => t.GravityScale, "WindScale" => t.WindScale, "PowerScale" => t.PowerScale, "MoveSpeed" => t.MoveSpeed,
        "SpBase" => t.SpBase, "SpBlast" => t.SpBlast, "SpCrater" => t.SpCrater, "SpDirect" => t.SpDirect,
        _ => throw new ArgumentException($"TANKFALL_VAR={VarName}: 덮을 수 없는 필드"),
    };
    static void SetVar(ref TankStats t, float v)
    {
        switch (VarName)
        {
            case "CraterRadius": t.CraterRadius = v; break;  case "BlastRadius": t.BlastRadius = v; break;
            case "BaseDamage": t.BaseDamage = v; break;      case "DirectDamage": t.DirectDamage = v; break;
            case "Defense": t.Defense = v; break;            case "Delay": t.Delay = (int)v; break;
            case "Hp": t.Hp = (int)v; break;
            case "MinPitch": t.MinPitch = v; break;          case "MaxPitch": t.MaxPitch = v; break;
            case "GravityScale": t.GravityScale = v; break;  case "WindScale": t.WindScale = v; break;
            case "PowerScale": t.PowerScale = v; break;      case "MoveSpeed": t.MoveSpeed = v; break;
            case "SpBase": t.SpBase = v; break;              case "SpBlast": t.SpBlast = v; break;
            case "SpCrater": t.SpCrater = v; break;          case "SpDirect": t.SpDirect = v; break;
            default: throw new ArgumentException($"TANKFALL_VAR={VarName}: 덮을 수 없는 필드");
        }
    }
    /// <summary>실험 값은 **기본 스탯(WithShell 전)** 에 덮고 나서 탄종·조건 보정을 거친다 — 2번탄 배율이 곱해지는 순서가 게임과 같아야 한다.</summary>
    static TankStats Stats(TankKind k, ShellKind shell, float hpFrac, Weather w)
    {
        if (OnlyRow != k || !HasVar) return TankStats.For(k, shell, hpFrac, w);
        var b = TankStats.Get(k);
        SetVar(ref b, VarValue);
        return b.WithShell(shell).WithCondition(hpFrac, w);
    }
    const float TankRadius = 2.0f;
    const float TurnSeconds = 18f;        // §2-1 2페이즈 턴의 현실적 평균

    // §2-5 레버 2 — 실측 후 유일하게 살아남은 레버. 되먹임 나선(§2-5-1)이 폭주하기 전에 판을 끝낸다.
    // 3v3 이므로 1라운드 = 6턴. 12라운드 = 72턴.
    // ⚠️ §2-5 초안의 12라운드(72턴)는 **너무 늦다**. [4] 실측상 맵은 40턴(약 7라운드)에
    //    명중률 4.2% 로 죽는다. 임계값을 그 앞에 둬야 나선을 끊는다.
    const float SuddenDeathPct = 0.05f;   // 매 턴 전원 최대HP의 5%
    const int SuddenDeathTurn = 36;       // 6라운드 × 6명. BattleDemo.SuddenDeathRound 와 같은 값이어야 한다

    static float _specPerMatch;

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
        public int Id, Team, Hp, MaxHp;
        public TankKind Kind;
        public Weather W;
        public SkillGauge Skill;   // 나이스샷 포인트 → SS (원작: 제한이 걸린 건 SS 뿐)
        // ⚠️ 세크윈드(저체력 격노)·포세이돈(눈)은 **상황에 따라 수치가 변한다.**
        //    그래서 St 는 상수가 아니라 매번 현재 체력·날씨로 다시 만든다.
        public float HpFrac => MaxHp > 0 ? Hp / (float)MaxHp : 1f;
        public TankStats St => Stats(Kind, ShellKind.Normal, HpFrac, W);
        public float X, Y, Z;
        public bool Alive => Hp > 0;
        public Vec3 Center => new Vec3(X, Y + 1.2f, Z);
        public Vec3 Muzzle => new Vec3(X, Y + 2.4f, Z);
    }

    struct MatchResult
    {
        public int Winner, Shots, Hits, Turns;
        public int FallDamage, FallDealt;   // 낙하 피해 총량 / 그중 적에게 준 몫(§28 보상 측정)
        public int SsUsed, DotDealt, MineDealt, SatelliteShots, SubShells;   // 원작 시스템이 실제로 도는지(네거티브 컨트롤)
        public int ShotsA, HitsA, BlastDealtA;   // A팀만: 사격 수, 적에게 피해 준 사격 수, 폭발(직격 포함) 피해 합 — 낙하·지속·설치물 제외
        public int[] BucketShots, BucketHits;   // 턴 구간별(0-19,20-39,...) — 나선 정량화
        public int SpecialUsed;
        public int Landed;        // 조준 대상 근처(3R 이내)에 떨어진 사격 — 탄착오차의 분모
        public int Blocked;       // 지형에 막혀 중간에 박힌 사격
        public float TotalMiss;   // Landed 사격만 누적
        public bool Timeout;
    }

    static MatchResult RunMatch(uint seed, float errorRatio, int hp = MaxHp, int maxTurns = 400,
                                int suddenDeathTurn = 0, float craterRadius = BlastRadius,
                                TankKind? teamA = null, TankKind? teamB = null,
                                Weather weather = Weather.Clear,
                                int firstTeam = 0, bool swapSpawn = false, bool alternate = true)
    {
        var rng = new Rng(seed);
        var vol = new SdfVolume(Voxel, ChunkN, -20f, Hills, (int)(MapSize / Voxel));
        var units = new List<U>();
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < 3; i++)
            {
                int side = swapSpawn ? 1 - t : t;      // 스폰 교환 — 지형 비대칭 판별용
                float x = side == 0 ? 55f + i * 14f : 150f + i * 12f;
                float z = side == 0 ? 40f + i * 8f : 155f - i * 9f;
                float g = TankGroundProbe.GroundBelow(vol, x, z, 60f);
                var kind = t == 0 ? (teamA ?? TankKind.Carrot) : (teamB ?? TankKind.Carrot);
                // 종류를 지정한 실험에서는 그 탱크의 HP 를 쓴다(hp 인자는 HP 스윕 전용)
                int startHp = (teamA.HasValue || teamB.HasValue) ? TankStats.Get(kind).Hp : hp;
                units.Add(new U { Id = t * 3 + i, Team = t, Kind = kind, Hp = startHp, MaxHp = startHp, W = weather,
                                  X = x, Z = z, Y = float.IsNegativeInfinity(g) ? 10f : g });
            }

        // ⚠️ 하네스 버그였다. 유닛이 A0 A1 A2 B3 B4 B5 순으로 리스트에 들어가고 턴 루프가 그 순서를
        //    그대로 돌아서 **A팀이 세 발을 연속으로 쏜 뒤에야 B팀 차례**가 왔다. 게임(BattleDemo 195행)은
        //    팀 교대인데 하네스만 달랐다. 특수탄이 죽어 있을 땐 미러 50% 로 안 보였고, 낙하 피해가
        //    들어오자 미러 80~90% 로 터졌다 — 세 발 연속 굴착이 상대 발밑을 통째로 끊는다.
        //    게임과 같은 교대 순서로 맞춘다. 판별용으로 옛 순서(alternate=false)도 남긴다.
        if (alternate) units.Sort((a, b) => (a.Id % 3) * 2 + a.Team - ((b.Id % 3) * 2 + b.Team));

        // 원작 딜레이 턴제(TurnOrder). 동률은 등록 순서이므로 교대 리스트를 firstTeam 부터 돌려 등록한다.
        var order = new TurnOrder();
        for (int i = 0; i < units.Count; i++)
        {
            var uu = units[(i + firstTeam) % units.Count];
            order.Add(uu.Id, uu.St.Delay);
        }
        var byId = new Dictionary<int, U>();
        foreach (var uu in units) byId[uu.Id] = uu;
        Func<int, bool> alive = id => byId[id].Alive;

        var res = new MatchResult { Winner = -1, BucketShots = new int[8], BucketHits = new int[8] };
        var wind = new Vec3(0, 0, 0);
        var status = new StatusEffects();   // 독·화상·속박
        var hazards = new HazardField();    // 지뢰·지속불

        for (int step = 0; step < maxTurns; step++)
        {
            // 라운드마다 바람 갱신(§15)
            if (step % 6 == 0)
            {
                float a = rng.Range(0f, MathF.PI * 2f), s = rng.Range(0f, 10f);
                wind = new Vec3(MathF.Cos(a) * s, 0f, MathF.Sin(a) * s);
            }

            // 다음 행동자 = 누적 딜레이 최소(원작 규칙). 전멸이면 끝.
            bool anyAlive = false;
            foreach (var o in units) if (o.Alive) { anyAlive = true; break; }
            if (!anyAlive) break;
            var u = byId[order.Next(alive)];
            // ⚠️ 여기서 바로 Consume 한다. 아래에 continue 경로가 셋이라 끝에서 하면 하나는 반드시 빠진다.
            order.Consume(u.Id, ShellKind.Normal, false);
            res.Turns++;

            // 턴 시작: 독·화상 tick, 지속불 수명, 서 있는 자리의 설치물(불 속에 서 있으면 매 턴)
            hazards.TickStartOfTurn();
            {
                int dot = Damage.AfterDefense(status.TickStartOfTurn(u.Id), u.St.Defense);
                if (dot > 0) { u.Hp = Math.Max(0, u.Hp - dot); if (u.Team == 1) res.DotDealt += dot; }   // A 가 B 에 준 것만
                int hz0 = Damage.AfterDefense(hazards.OnUnitAt(u.Id, u.Kind, u.Center), u.St.Defense);
                if (hz0 > 0) { u.Hp = Math.Max(0, u.Hp - hz0); if (u.Team == 1) res.MineDealt += hz0; }
                if (!u.Alive) continue;
            }
            // 간단 이동 [추정]: 40% 턴에 임의 방향 최대 8m. 지뢰·지속불은 움직여야 밟는다.
            //   실제 게임의 이동 페이즈를 대신하는 최소 모델이다 — 없으면 마인랜더 2번탄이 하네스에서 영원히 0 이다.
            if (status.CanMove(u.Id) && rng.Float01() < 0.40f)
            {
                float hd = rng.Range(0f, MathF.PI * 2f), dx = MathF.Cos(hd), dz = MathF.Sin(hd);
                float gy = u.Y; bool moved = false;
                for (int k = 0; k < 8; k++)
                {
                    float nx = u.X + dx, nz = u.Z + dz;
                    if (nx < 5f || nx > MapSize - 5f || nz < 5f || nz > MapSize - 5f) break;
                    if (TankGroundProbe.CanStepTo(vol, gy, nx, nz, out float ny) != TankGroundProbe.MoveResult.Ok) break;
                    u.X = nx; u.Z = nz; gy = ny; moved = true;
                    int hz = Damage.AfterDefense(hazards.OnUnitAt(u.Id, u.Kind, u.Center), u.St.Defense);
                    if (hz > 0) { u.Hp = Math.Max(0, u.Hp - hz); if (u.Team == 1) res.MineDealt += hz; break; }
                }
                if (moved) { u.Y = gy; order.AddExtra(u.Id, ShellKind.Normal, true); }
                if (!u.Alive) continue;
            }

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
            foreach (var o in units) if (o.Alive && o.Team != u.Team) enemies.Add(new AiGunner.Target { Id = o.Id, Center = o.Center, Defense = o.St.Defense });
            if (enemies.Count == 0) break;

            var st = u.St;
            var plan = AiGunner.Decide(vol, u.Muzzle, enemies, wind, errorRatio, ref rng, MapSize, st);
            if (!plan.Valid) continue;

            float speed = st.SpeedAt(plan.Power);
            var accel = st.AccelWith(wind.X, wind.Z);
            var boxes = new List<TankHitbox>();
            foreach (var o in units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });

            // 1) 기준 탄도(패턴 중앙) 한 발로 착탄점을 본다
            var shot = ProjectileSimulator.Simulate(vol, u.Muzzle, Ballistics.VelocityFrom(plan.YawDeg, plan.PitchDeg, speed), accel, boxes, u.Id, MapSize);
            res.Shots++;
            if (u.Team == 0) res.ShotsA++;
            int bk = Math.Min(7, res.Turns / 20);
            res.BucketShots[bk]++;
            // 나이스샷 판정(원작: 게이지를 표시 지점에 정확히 멈추면 포인트 +1) — AI 는 확률 [추정]
            if (NiceShot.AiJudge(errorRatio, ref rng)) u.Skill.OnNiceShot();
            if (!shot.Hit) continue;

            // ⚠️ 탄착오차는 **목표 근처에 떨어진 사격만** 평균 내야 한다.
            //    지형에 막혀 산허리에 박힌 탄(수십~백 m 단위)을 같이 넣으면 평균이 조준 정확도가 아니라
            //    "차폐 비율"을 재는 숫자가 된다. 실제로 그래서 오차 0 대조군이 24.9m 로 나왔다.
            foreach (var e in enemies)
                if (e.Id == plan.TargetId)
                {
                    float d = (shot.Impact - e.Center).Length;
                    if (d <= st.BlastRadius * 3f) { res.TotalMiss += d; res.Landed++; }
                    else res.Blocked++;
                }

            // 2) 착탄점을 본 뒤 탄종을 고른다(§8). 위성탄은 착탄점이 달라지므로 먼저 풀어서 넘긴다.
            var fxSpecial = ShellEffects.Of(u.Kind, ShellKind.Special);
            Vec3? satImpact = null; int satDirect = -1;
            if (fxSpecial.Type == ShellEffects.EffectType.SatelliteStrike)
            {
                satImpact = SatelliteStrike.Resolve(vol, shot.Impact.X, shot.Impact.Z, shot.Impact.Y + 60f, boxes, out satDirect);
            }
            var normalHits = AiGunner.SimulatePattern(vol, u.Muzzle, plan.YawDeg, plan.PitchDeg, speed, accel, boxes, u.Id, MapSize,
                                                      Spread.Pattern(u.Kind, ShellKind.Normal), shot);
            var specialHits = satImpact.HasValue ? null
                : AiGunner.SimulatePattern(vol, u.Muzzle, plan.YawDeg, plan.PitchDeg, speed, accel, boxes, u.Id, MapSize,
                                           Spread.Pattern(u.Kind, ShellKind.Special), shot);
            var shell = AiGunner.PickShell(st, normalHits, specialHits, enemies, satImpact, satDirect);
            bool ss = false;
            if (shell == ShellKind.Special)
            {
                if (u.Team == 0) res.SpecialUsed++;
                st = Stats(u.Kind, shell, u.HpFrac, weather);
                order.AddExtra(u.Id, shell, false);     // 특수탄 추가 딜레이 [추정]
                if (u.Skill.CanSs()) { st = NiceShot.ApplySs(st); u.Skill.SpendSs(); ss = true; if (u.Team == 0) res.SsUsed++; }
            }
            var fx = ShellEffects.Of(u.Kind, shell);

            // 3) 다탄두: 고른 탄종의 패턴대로 전부 날린다(중앙 탄 = 위 기준 탄도)
            var pattern = Spread.Pattern(u.Kind, shell);
            bool anyHit = false;
            float craterEach = ((teamA.HasValue || teamB.HasValue) ? st.CraterRadius : craterRadius)
                               * (pattern.Count > 1 ? 0.65f : 1f);   // 다탄두는 발당 굴착을 줄인다 [추정] — 아니면 3배로 판다
            for (int pi = 0; pi < pattern.Count; pi++)
            {
                var pt = pattern[pi];
                var sub = pattern.Count == 1 ? shot
                    : ProjectileSimulator.Simulate(vol, u.Muzzle,
                        Ballistics.VelocityFrom(plan.YawDeg + pt.YawOffsetDeg, plan.PitchDeg + pt.PitchOffsetDeg, speed),
                        accel, boxes, u.Id, MapSize);
                if (pattern.Count > 1 && u.Team == 0) res.SubShells++;
                if (!sub.Hit) continue;

                Vec3 impact = sub.Impact; int directId = sub.DirectHitTankId;
                if (fx.Type == ShellEffects.EffectType.Homing)
                {
                    int uid = u.Id;
                    impact = ShellEffects.HomingCorrect(impact, boxes, u.Id, 0, id => byId[id].Team != byId[uid].Team, out int hd);
                    if (hd >= 0) directId = hd;
                }
                if (fx.Type == ShellEffects.EffectType.SatelliteStrike)
                {
                    impact = SatelliteStrike.Resolve(vol, sub.Impact.X, sub.Impact.Z, sub.Impact.Y + 60f, boxes, out directId);
                    if (u.Team == 0) res.SatelliteShots++;
                }

                var blast = SdfDeformer.SubtractSphere(vol, new BlastRequest(impact.X, impact.Y, impact.Z, craterEach));
                CeilingCollapse.Apply(vol, blast);

                foreach (var o in units)
                {
                    if (!o.Alive) continue;
                    float dist = (o.Center - impact).Length;
                    bool direct = o.Id == directId;
                    if (dist > st.BlastRadius && !direct) continue;
                    int dmg = Damage.AfterDefense(
                        Damage.Compute(dist, st.BlastRadius, st.BaseDamage * pt.DamageScale, st.DirectDamage * pt.DamageScale, direct),
                        o.St.Defense);
                    if (dmg <= 0) continue;
                    o.Hp = Math.Max(0, o.Hp - dmg);
                    if (o.Team != u.Team) { anyHit = true; if (u.Team == 0) res.BlastDealtA += dmg; }
                    // 맞은 유닛에 붙는 효과
                    if (fx.Type == ShellEffects.EffectType.Poison && o.Id != u.Id) status.Poison(o.Id, fx.Param1, fx.Param2, o.Kind);
                    if (fx.Type == ShellEffects.EffectType.Root && o.Team != u.Team) status.Root(o.Id, fx.Param1);
                }
                // 자리에 남는 효과
                if (fx.Type == ShellEffects.EffectType.Burn || fx.Type == ShellEffects.EffectType.PoisonCloud)
                    hazards.PlaceFire(impact.X, impact.Y, impact.Z, st.BlastRadius, fx.Param1, fx.Param2);   // 지속불·독구름은 같은 장판 구조
                if (fx.Type == ShellEffects.EffectType.Mine) hazards.PlaceMine(impact.X, impact.Y, impact.Z, 4f, fx.Param1, u.Id);   // 반경 4m [추정]
            }
            if (anyHit) { res.Hits++; res.BucketHits[bk]++; if (u.Team == 0) res.HitsA++; }

            // 지형이 꺼졌을 수 있으니 재접지 — **떨어진 만큼 피해**(§28)
            foreach (var o in units)
                if (o.Alive)
                {
                    float g = TankGroundProbe.GroundBelow(vol, o.X, o.Z, o.Y + 2f);
                    if (float.IsNegativeInfinity(g)) continue;
                    int fall = Damage.AfterDefense(Damage.FromFall(o.Y - g), o.St.Defense);
                    o.Y = g;
                    if (fall <= 0) continue;
                    o.Hp = Math.Max(0, o.Hp - fall);
                    res.FallDamage += fall;
                    if (u.Team == 0 && o.Team == 1) res.FallDealt += fall;   // A 가 B 에 준 것만 — 양 팀 합산이면 상대 몫이 섞인다
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
        var rowEnv = Environment.GetEnvironmentVariable("TANKFALL_ROW");
        if (!string.IsNullOrEmpty(rowEnv))
        {
            if (!Enum.TryParse<TankKind>(rowEnv, true, out var rk)) { Console.WriteLine($"❌ TANKFALL_ROW={rowEnv}: 모르는 기종"); Environment.Exit(2); }
            OnlyRow = rk;
            VarName = Environment.GetEnvironmentVariable("TANKFALL_VAR");
            var valEnv = Environment.GetEnvironmentVariable("TANKFALL_VALUES");
            var cEnv = Environment.GetEnvironmentVariable("TANKFALL_CRATER");
            if (!string.IsNullOrEmpty(cEnv)) { VarName = "CraterRadius"; valEnv = cEnv; }
            var values = new List<float>();
            if (!string.IsNullOrEmpty(VarName))
            {
                var baseT = TankStats.Get(rk);
                float baseV = GetVar(baseT);   // 모르는 필드면 여기서 즉시 죽는다
                if (string.IsNullOrEmpty(valEnv)) values.Add(baseV);
                else foreach (var tok in valEnv.Split(',')) values.Add(float.Parse(tok.Trim(), System.Globalization.CultureInfo.InvariantCulture));
                HasVar = true;
                Console.WriteLine($"=== 단일 행 실험: {baseT.Name} · {VarName} 기본 {baseV:F2} → [{string.Join(", ", values)}] (셀당 {MatchesPerCell * 2}판) ===");
            }
            else
            {
                values.Add(0f); HasVar = false;
                Console.WriteLine($"=== 단일 행 측정: {TankStats.Get(rk).Name} (변수 없음, 셀당 {MatchesPerCell * 2}판) ===");
            }
            uint per = MatchesPerCell * 2;   // 12셀뿐이라 판 수를 두 배로 — 행 평균 표준오차 ≈ ±2.5%p
            foreach (var v in values)
            {
                VarValue = v;
                Console.WriteLine("");
                Console.WriteLine(HasVar ? $"--- {VarName} = {v:F2} ---" : "--- 현행 ---");
                Matchup(rk, per);
            }
            return;
        }
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

        // [6] 탱크 3종 매치업(§9-3). 생김새만 다르고 성능이 같던 걸 갈랐으니 **승률로 확인**한다.
        //     읽는 법: 행=A팀, 열=B팀, 숫자=A팀 승률. 미러(대각선)는 50% 근처여야 정상(네거티브 컨트롤).
        //     어느 한 종이 모든 열에서 이기면 그건 선택지가 아니라 정답이다 — 다시 짜야 한다.
        // ⚠️ 매치업을 재기 전에 **참가 자격부터 본다.** 사거리가 교전 거리보다 짧으면
        //    그 탱크는 밸런스가 나쁜 게 아니라 게임에 못 들어온다 — 승률표를 읽어도 의미가 없다.
        //    실제로 박격포가 사거리 109m(교전 134m)라 40판 전패했고, 그걸 밸런스 문제로 오독할 뻔했다.
        // [6-1] 미러가 50% 를 벗어난 원인 판별 — 원인 단정 전 판별 테스트(CLAUDE.md 가드레일).
        //     같은 탱크(캐롯)끼리 붙여 놓고 조건만 바꾼다. 어느 조건에서 50% 로 돌아오는지가 답이다.
        Console.WriteLine("");
        Console.WriteLine("[6-1] 미러 이탈 원인 판별 — 캐롯 vs 캐롯, 각 20판, A팀 승률");
        {
            (string name, int first, bool swap, bool alt)[] conds =
            {
                ("순차 AAA BBB (구 하네스)", 0, false, false),
                ("교대 A선공 (게임과 동일)", 0, false, true),
                ("교대 B선공",              1, false, true),
                ("교대 A선공 + 스폰 교환",  0, true,  true),
            };
            foreach (var c in conds)
            {
                int winA = 0, decided = 0;
                for (uint m = 0; m < 20u; m++)
                {
                    var r = RunMatch(1000 + m * 77, 0.025f, MaxHp, 400, SuddenDeathTurn, BlastRadius,
                                     TankKind.Carrot, TankKind.Carrot, Weather.Clear, c.first, c.swap, c.alt);
                    if (r.Winner >= 0) { decided++; if (r.Winner == 0) winA++; }
                }
                float pct = decided > 0 ? winA * 100f / decided : -1f;
                string read = pct > 65f ? "  ← A 유리" : pct < 35f ? "  ← B 유리" : "  ← 대칭";
                Console.WriteLine($"    {c.name,-26} {pct,5:F0}%{read}");
            }
            Console.WriteLine("    ※ 읽는 법: 'B선공'에서 뒤집히면 선공 이점, '스폰 교환'에서 뒤집히면 지형 비대칭.");
            // 지형 비대칭을 숫자로: 각 스폰 지점의 지면 높이
            var probe = new SdfVolume(Voxel, ChunkN, -20f, Hills, (int)(MapSize / Voxel));
            string ha = "", hb = "";
            for (int i = 0; i < 3; i++)
            {
                float ga = TankGroundProbe.GroundBelow(probe, 55f + i * 14f, 40f + i * 8f, 60f);
                float gb = TankGroundProbe.GroundBelow(probe, 150f + i * 12f, 155f - i * 9f, 60f);
                ha += $"{ga,5:F1} "; hb += $"{gb,5:F1} ";
            }
            Console.WriteLine($"    스폰 지면 높이  A팀 [{ha}]  B팀 [{hb}]  (높은 쪽이 사거리·시야 유리)");
        }

        Console.WriteLine("");
        Console.WriteLine("[6-0] 참가 자격 — 최대 사거리 vs 교전 거리");
        {
            float engage = MathF.Sqrt((150f - 55f) * (150f - 55f) + (155f - 40f) * (155f - 40f));
            Console.WriteLine($"    스폰 간 교전 거리 {engage:F0}m");
            bool allOk = true;
            foreach (var k in TankStats.Selectable())
            {
                var t = TankStats.Get(k);
                bool ok = t.MaxRange > engage * 1.1f;      // 10% 여유가 없으면 사실상 못 싸운다
                allOk &= ok;
                Console.WriteLine($"    {TankStats.EraName(t.Era)} {t.Name,-12} 최대사거리 {t.MaxRange,6:F0}m  각도 {t.MinPitch,4:F0}~{t.MaxPitch,-3:F0}°  {(ok ? "OK" : "❌ 교전 거리 미달")}");
            }
            if (!allOk) Console.WriteLine("    ❌ 아래 승률표는 신뢰할 수 없다 — 사거리부터 고쳐라.");
        }

        Console.WriteLine("");
        Matchup(null, MatchesPerCell);
        Console.WriteLine("");

        Console.WriteLine("※ 탄착오차 = 목표 21m(3R) 이내 탄만 평균. 차폐 = 지형에 막혀 그 밖에 박힌 비율.");
        Console.WriteLine("\n※ 명중률 = 적에게 피해를 준 사격 비율. 오차0 대조군이 가장 높아야 정상.");
        Console.WriteLine("※ 예상시간 = 평균턴 × 18초(§2-1 2페이즈 턴 평균). 기획서 §55 목표는 8~15분.");
    }

    /// <summary>[6] 매치업. only 가 있으면 그 기종 행만(단일 변수 실험용).</summary>
    static void Matchup(TankKind? only, uint perCell)
    {
        Console.WriteLine($"[6] 포트리스 12종 매치업 승률 (A팀 기준, 각 {perCell}판, 오차 2.5%, 서든데스 6R)");
        var kinds = TankStats.Selectable();      // 슈퍼탱크는 원작대로 선택 불가라 빠진다
        int N = kinds.Length;
        var win = new float[N, N];
        var spUse = new float[N];
        var turnAvg = new float[N];
        var fallAvg = new float[N];   // 네거티브 컨트롤: 0 이면 §28 보상이 또 죽어 있다는 뜻
        var ssAvg = new float[N]; var dotAvg = new float[N]; var mineAvg = new float[N]; var satAvg = new float[N];
        var hitPct = new float[N]; var dmgPerShot = new float[N];   // A팀 명중률 · 명중당 폭발 피해

        // 단일 행 모드: 그 기종이 A 팀인 행만 돈다(12셀). 나머지 행은 계산도 출력도 안 한다.
        int[] rows;
        if (only.HasValue) rows = new[] { Array.IndexOf(kinds, only.Value) };
        else { rows = new int[N]; for (int i = 0; i < N; i++) rows[i] = i; }
        if (only.HasValue && rows[0] < 0) { Console.WriteLine($"    ❌ {only.Value} 는 선택 가능 기종이 아니다"); return; }
        foreach (int ai in rows)
        {
            float spSum = 0f, tSum = 0f, fSum = 0f, ssSum = 0f, dotSum = 0f, mineSum = 0f, satSum = 0f;
            long shotsA = 0, hitsA = 0, blastA = 0;
            for (int bi = 0; bi < N; bi++)
            {
                int winA = 0, decided = 0, turns = 0, spec = 0, fall = 0, ssN = 0, dotN = 0, mineN = 0, satN = 0;
                for (uint m = 0; m < perCell; m++)
                {
                    // [6-1] 실측: B 스폰 자리가 지형상 유리(+20%p). 홀짝 판마다 자리를 바꿔 탱크 비교에서 상쇄한다.
                    var r = RunMatch(1000 + m * 77, 0.025f, MaxHp, 400, SuddenDeathTurn, BlastRadius, kinds[ai], kinds[bi],
                                     Weather.Clear, 0, (m & 1) == 1, true);
                    turns += r.Turns; spec += r.SpecialUsed; fall += r.FallDealt;
                    ssN += r.SsUsed; dotN += r.DotDealt; mineN += r.MineDealt; satN += r.SatelliteShots;
                    shotsA += r.ShotsA; hitsA += r.HitsA; blastA += r.BlastDealtA;
                    if (r.Winner >= 0) { decided++; if (r.Winner == 0) winA++; }
                }
                win[ai, bi] = decided > 0 ? winA * 100f / decided : -1f;
                spSum += spec / (float)perCell; tSum += turns / (float)perCell; fSum += fall / (float)perCell;
                ssSum += ssN / (float)perCell; dotSum += dotN / (float)perCell; mineSum += mineN / (float)perCell; satSum += satN / (float)perCell;
            }
            spUse[ai] = spSum / N; turnAvg[ai] = tSum / N; fallAvg[ai] = fSum / N;
            hitPct[ai] = shotsA > 0 ? hitsA * 100f / shotsA : 0f; dmgPerShot[ai] = hitsA > 0 ? blastA / (float)hitsA : 0f;
            ssAvg[ai] = ssSum / N; dotAvg[ai] = dotSum / N; mineAvg[ai] = mineSum / N; satAvg[ai] = satSum / N;
        }

        // 12종 이름을 가로로 늘어놓으면 표가 화면을 넘는다 — 번호로 찍고 아래에 범례를 단다.
        Console.Write($"    {"A팀 \\ B팀",-16}");
        for (int i = 0; i < N; i++) Console.Write($"{i + 1,4}");
        Console.WriteLine($" | {"평균",5} {"미러",5}");
        foreach (int ai in rows)
        {
            var t = TankStats.Get(kinds[ai]);
            Console.Write($" {ai + 1,2} {t.Name,-12}");
            float sum = 0f; int n = 0;
            for (int bi = 0; bi < N; bi++)
            {
                Console.Write(win[ai, bi] < 0 ? "   -" : $"{win[ai, bi],4:F0}");
                if (win[ai, bi] >= 0) { sum += win[ai, bi]; n++; }
            }
            float avg = n > 0 ? sum / n : 0f;
            string flag = avg >= 62f ? " ⬆" : avg <= 38f ? " ⬇" : "";
            Console.WriteLine($" | {avg,4:F0}% {win[ai, ai],4:F0}%{flag}");
        }
        Console.Write("    범례:");
        for (int i = 0; i < N; i++) Console.Write($" {i + 1}={TankStats.Get(kinds[i]).Name}");
        Console.WriteLine("");
        Console.WriteLine("");
        Console.WriteLine("    계열·특수탄·판길이");
        Console.Write($"    {"탱크",-14}{"계열",-6}{"체력",5}{"사거리",7}{"폭발",6}{"굴착",6}{"직격",6}{"2번탄/판",9}{"SS/판",6}{"평균턴",7}{"명중%",6}{"피해/명중",9}{"낙하",6}{"지속",6}{"설치물",7}{"위성",5}");
        Console.WriteLine("");
        foreach (int i in rows)
        {
            var t = Stats(kinds[i], ShellKind.Normal, 1f, Weather.Clear);   // 단일 행 실험의 굴착 배율이 표에도 보이게
            // ⚠️ 특수탄/판이 0 에 가까우면 밸런스가 아니라 **선택지가 죽어 있다**는 신호다.
            string dead = spUse[i] < 0.5f ? "  ❌사장" : "";
            Console.WriteLine($"    {t.Name,-14}{TankStats.EraName(t.Era),-6}{t.Hp,5}{t.MaxRange,6:F0}m{t.BlastRadius,5:F1}m{t.CraterRadius,5:F1}m{t.DirectDamage,6:F0}{spUse[i],9:F1}{ssAvg[i],6:F1}{turnAvg[i],7:F0}{hitPct[i],6:F0}{dmgPerShot[i],9:F0}{fallAvg[i],6:F0}{dotAvg[i],6:F0}{mineAvg[i],7:F0}{satAvg[i],5:F1}{dead}");
        }
        Console.WriteLine("    ※ 미러(대각선)가 50%에서 크게 벗어나면 진영 유불리(스폰·지형·선공)가 섞인 것이다.");
        Console.WriteLine("    ※ 명중% = A팀 사격 중 적에게 폭발 피해를 준 비율, 피해/명중 = 그 사격 한 번의 폭발 피해 합(방어 적용 후, 낙하·지속·설치물 제외).");
        Console.WriteLine("    ※ 낙하 = 지형을 끊어 적에게 입힌 한 판 평균 피해(§28). 지속 = 독·화상 tick, 설치물 = 지뢰(마인랜더)+지속불(캐터펄트)+독구름(듀크) 장판 피해, 위성 = 위성탄 발수. 해당 탱크에서 0 이면 그 시스템이 죽은 것.");
        Console.WriteLine("    ※ ⬆/⬇ = 평균 승률이 62% 이상 / 38% 이하 — 선택지가 아니라 정답 또는 함정이라는 뜻.");
    }
}
