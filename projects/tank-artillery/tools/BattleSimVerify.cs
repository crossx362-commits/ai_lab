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
    const float Voxel = 0.5f, MapSize = MapHeightFunction.MapSize;   // 단일 소스를 따른다
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
    // 1라운드 = 팀인원 × 2 턴. 라운드 수로만 생각하고 턴 수는 인원에서 유도해라.
    // ⚠️ §2-5 초안의 12라운드(72턴)는 **너무 늦다**. [4] 실측상 맵은 40턴(약 7라운드)에
    //    명중률 4.2% 로 죽는다. 임계값을 그 앞에 둬야 나선을 끊는다.
    const float SuddenDeathPct = 0.05f;   // 매 턴 전원 최대HP의 5%
    // ⚠️ **턴 수를 숫자로 박지 마라.** 36 = 6라운드 × 6명이었는데 4:4 로 늘리자 하네스만 4.5라운드에
    //    서든데스를 걸고 게임은 6라운드에 걸었다 — 승률·판 길이 표가 **게임과 다른 것을 재면서도 통과**한다.
    //    라운드 수(게임의 BattleDemo.SuddenDeathRound)가 단일 소스이고, 턴 수는 인원에서 유도한다.
    const int SuddenDeathRound = 6;
    // ⚠️ `const` 가 아니라 프로퍼티다 — `TANKFALL_TEAM` 으로 인원을 흔들면 서든데스 턴도 같이 움직여야 한다.
    //    `const` 로 굳혀 두면 3:3 측정이 **4:4 기준 서든데스(48턴)** 를 쓰면서 "3:3 을 쟀다"고 말한다.
    static int SuddenDeathTurn => SuddenDeathRound * MapHeightFunction.TeamSize * 2;

    static float _specPerMatch;

    /// <summary>
    /// 화상(2026-09-19 배선)이 **실제로 걸린 횟수**. 0 이면 배선이 죽은 것이다.
    /// 원장 규칙 그대로다 — 「새 시스템은 표에 **그 시스템만 만드는 숫자**를 열로 넣어 0 이면 죽은 것으로 읽는다」
    /// (지뢰가 120판 동안 피해 0 이었는데 열이 없어서 아무도 몰랐던 사고에서 나온 규칙).
    /// </summary>
    static int BurnApplied;

    /// <summary>
    /// 화상 배선이 살아 있는지 — **실행이 전부 끝난 뒤**에 판정한다.
    ///
    /// 🚨 처음에 이걸 `[1]` 난이도 표 **직후**에 뒀다가 헛짚었다. `[1]` 은 **캐롯 자기전**이라
    ///    캐터펄트가 아예 안 나온다 — 배선이 멀쩡해도 항상 0 이다. 더 나쁜 것은
    ///    **그 자리에서 배선을 끊는 네거티브 컨트롤을 돌렸더니 ❌ 가 떠서 "게이트가 작동한다"고 착각**한 것이다.
    ///    빨간불이 뜬 이유가 내가 끊어서가 아니라 **원래 0 이라서**였다.
    ///    → 교훈: **네거티브 컨트롤이 빨간불을 냈다고 끝이 아니다. 대조군(안 끊은 쪽)이 초록인지도 봐야 한다.**
    /// ⚠️ 눈이면 **0 이 정답이다**(원작: 눈이 캐터펄트의 불을 끈다). 날씨를 안 보고 0 을 실패로 처리하면
    ///    `TANKFALL_WEATHER=Snow` 실행이 애먼 빨간불을 낸다 — 거짓 빨간불도 거짓 초록불만큼 위험하다.
    /// ⚠️ 캐터펄트가 안 도는 실행(단일 행 실험 등)에서는 판정하지 않는다 — `sawCatapult` 가 그걸 가른다.
    /// </summary>
    static bool SawCatapult;

    static void BurnWiringGate()
    {
        if (Wx == Weather.Snow)
        {
            Console.WriteLine($"\n❄ 화상 {BurnApplied}회 — 눈에서는 0 이 정답이다(원작: 눈이 캐터펄트의 불을 끈다)");
            if (BurnApplied > 0) { Console.WriteLine("❌ 눈인데 화상이 걸렸다 — 날씨 게이트가 샌다"); RcFail = true; }
            return;
        }
        if (!SawCatapult)
        { Console.WriteLine($"\n· 화상 게이트 건너뜀 — 이번 실행에 캐터펄트가 안 나왔다(적용 {BurnApplied}회)"); return; }
        if (BurnApplied > 0) Console.WriteLine($"\n✅ 화상 배선 살아 있다 — {BurnApplied}회 적용됨 (캐터펄트 2번탄)");
        else { Console.WriteLine("\n❌ 캐터펄트가 돌았는데 화상이 한 번도 안 걸렸다 — 배선이 죽었다"); RcFail = true; }
    }

    // ── 맵 ──  게임(BattleDemo)과 **같은 함수·같은 스폰**을 써야 승률이 게임의 승률이다(교대 순서 버그의 교훈, §2-9-1).
    //   TANKFALL_MAP=TwinHills(기본)|Crater|Terrace|Valley|Ridge|Badlands — MapHeightFunction(§맵 6종). Legacy = 옛 언덕(22m·20m, §2-9-4 까지 전부 이 맵에서 잰 값) — 비교·회귀용.
    // ── 날씨 ──  TANKFALL_WEATHER=Clear(기본)|Snow. 눈이면 포세이돈만 세진다(SnowBonus) —
    //   게임은 판 시작에 25% 확률로 눈이 오는데(BattleDemo.SnowChance [추정]) 하네스가 늘 맑음으로만 재면
    //   포세이돈의 고유 능력은 **한 번도 측정되지 않는다**(= 살아 있다고 말할 수 없다).
    static Weather Wx = Weather.Clear;
    /// <summary>유닛당 아이템 슬롯 수(§2-9-10). `TANKFALL_ITEMS=0` 으로 끄면 아이템 이전 수치와 비교할 수 있다(회귀용).
    /// 기본 2 는 내가 정한 값 [추정] — 기획서에 획득 규칙이 없다.</summary>
    static int ItemSlots = 2;

        /// <summary>
        /// 원인 판별용 스위치(하네스 전용, 2026-09-17).
        ///   TANKFALL_CRATERSHAPE=0 → 탄종별 굴착 모양을 끄고 전부 구로 판다
        ///   TANKFALL_ULT=0         → 궁극기를 끈다
        /// 캐논 승률이 33~35% 에서 24% 로 내려간 원인을 **단일 변수로** 가르기 위해 둔다(§2-9-5 방식).
        /// ⚠️ 게임(BattleDemo)에는 이런 스위치가 없다 — 하네스에서만 대조군을 만든다.
        /// </summary>
        /// <summary>TANKFALL_ONLY=mirror 로 미러 게이트까지만 돌린다(반복 측정용).</summary>
        static readonly string OnlyStage = Environment.GetEnvironmentVariable("TANKFALL_ONLY") ?? "";

        /// <summary>
        /// 미러 게이트의 조준 오차. 기본은 중급(0.025).
        /// TANKFALL_MIRROR_ERR=0 으로 두면 **완벽 조준 대조군**이 된다 —
        /// 운을 제거하고 구조적 편향만 남기는 판별(§2-9-8 이 쓴 방법).
        /// </summary>
        static readonly float MirrorErr =
            float.TryParse(Environment.GetEnvironmentVariable("TANKFALL_MIRROR_ERR"), out var me) ? me : 0.025f;

        /// <summary>TANKFALL_NOHEAL=1 이면 회복 계열 아이템을 쓰지 않는다(미러 편향 판별용).</summary>
        static readonly bool NoHeal = Environment.GetEnvironmentVariable("TANKFALL_NOHEAL") == "1";

        /// <summary>TANKFALL_NOWIND=1 이면 바람을 0 으로 고정한다(미러 편향 판별용).</summary>
        static readonly bool NoWind = Environment.GetEnvironmentVariable("TANKFALL_NOWIND") == "1";

        static readonly bool UseCraterShape = Environment.GetEnvironmentVariable("TANKFALL_CRATERSHAPE") != "0";
        static readonly bool UseUltimate = Environment.GetEnvironmentVariable("TANKFALL_ULT") != "0";
    /// <summary>
    /// 사격 산포(오너 지시 2026-09-18 "발사체 궤적 랜덤"). `TANKFALL_SPREAD=0` 으로 끄면 도입 전과 비교할 수 있다.
    /// ⚠️ 게임(`BattleDemo.FireFrom`)과 **같은 `Spread.AimJitter`** 를 부른다 — 한쪽만 흔들면
    ///    표가 다른 게임을 재면서도 통과한다(이 저장소에서 제일 자주 난 사고).
    /// </summary>
    static readonly bool UseSpread = Environment.GetEnvironmentVariable("TANKFALL_SPREAD") != "0";

        /// <summary>보급(§2-9-11) 스위치. `TANKFALL_SUPPLY=0` 으로 끄면 아이템만 켠 상태와 비교할 수 있다(판별용).</summary>
        static bool SupplyOn = true;
        /// <summary>미러 게이트 시드 오프셋. `TANKFALL_SEED=&lt;n&gt;` — 같은 결론이 다른 시드에서도 나오는지 보는 용도.</summary>
        static uint SeedOffset = 0;
        /// <summary>기후(§2-9-15) 스위치. `TANKFALL_CLIMATE=0` 으로 끄면 도입 전과 비교할 수 있다.</summary>
        static bool ClimateOn = true;
    /// <summary>AI 가 보급 상자를 주우러 걸어갈 최대 거리 [추정]. 한 턴 이동 8m 라 여러 턴에 걸쳐 간다.</summary>
    const float SupplySeekRange = 45f;
    /// <summary>[6-2-1] 미러 게이트 표본. 20판(±11%p)으로는 편향과 운을 못 가른다 — 실측으로 배운 값.</summary>
    const uint MirrorGateN = 60u;
    static bool RcFail;
    static string MapName = "TwinHills";
    static bool LegacyMap => MapName == "Legacy";
    static MapKind Map = MapKind.TwinHills;
    static SdfVolume NewVolume()
        => LegacyMap ? new SdfVolume(Voxel, ChunkN, -20f, Hills, (int)(MapSize / Voxel))
                     : new SdfVolume(Voxel, ChunkN, -20f, MapHeightFunction.Fn(Map), (int)(MapSize / Voxel), MapHeightFunction.Grad(Map));
    static void SpawnAt(int side, int slot, out float x, out float z)
    {
        if (LegacyMap) { x = side == 0 ? 55f + slot * 14f : 150f + slot * 12f; z = side == 0 ? 40f + slot * 8f : 155f - slot * 9f; return; }
        MapHeightFunction.Spawn(Map, side, slot, out x, out z);
    }

    /// <summary>옛 언덕. §2-9-4 까지의 매치업 전부는 이 맵에서 잰 것이다(회귀 비교용으로 남긴다).</summary>
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
        public SkillGauge Skill;
        public float LastPitch = 45f;   // 각도고정탄이 묶을 기준 각도   // 나이스샷 포인트 → SS (원작: 제한이 걸린 건 SS 뿐)
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
            public int UltUsed;        // 궁극기(§49) 발동 횟수 — 0 이면 문턱(UltimateCost)이 너무 높다는 신호다
        public int ItemsUsed, ItemsShielded, ItemsDoubleFired;   // 아이템(§2-9-10) 네거티브 컨트롤 — 0 이면 죽은 것
        public int SupplyDropped, SupplyPicked, SupplyDestroyed;
        public int PickedA, PickedB, SeekA, SeekB;   // 팀별 분해 — 미러가 기울면 여기가 먼저 답한다
        public int SeekTurns, SeekBlocked;
        /// <summary>A팀 이동 사유별 턴 수(MoveReason 순). **네거티브 컨트롤**: CraterEscape 가 0 이면
        /// 구덩이 탈출이 죽은 것이고, Supply 가 0 이면 헬기가 다시 장식이 된 것이다.</summary>
        public int[] MoveWhyA;
        public int ImpairHitA;
        public int AmpShots, TornadoShots;   // 기후(§2-9-15) 네거티브 컨트롤 — 0 이면 죽은 시스템   // 방해탄(§2-9-14) — 각도·파워 고정만. 화면 방해는 AI 에 효과 0 이라 안 쓴다   // 궁극기(§2-9-12) 네거티브 컨트롤 — 0 이면 죽은 시스템   // 헬기 보급(§2-9-11) 네거티브 컨트롤
        public int ShotsA, HitsA, BlastDealtA;   // A팀만: 사격 수, 적에게 피해 준 사격 수, 폭발(직격 포함) 피해 합 — 낙하·지속·설치물 제외
        public int[] BucketShots, BucketHits;   // 턴 구간별(0-19,20-39,...) — 나선 정량화
        public int SpecialUsed;
        public int Landed;        // 조준 대상 근처(3R 이내)에 떨어진 사격 — 탄착오차의 분모
        public int Blocked;       // 지형에 막혀 중간에 박힌 사격
        public float TotalMiss;   // Landed 사격만 누적
        public bool Timeout;
    }

    /// <param name="errorRatioB">B팀만 다른 조준 오차를 줄 때. null 이면 양 팀 같다(기존 호출 전부 이 경로).
    /// 난이도 사다리([7])는 이게 없으면 잴 수 없다 — 같은 난이도끼리 붙이면 당연히 50% 라 사다리인지 알 수 없다.</param>
    static MatchResult RunMatch(uint seed, float errorRatio, int hp = MaxHp, int maxTurns = 400,
                                int suddenDeathTurn = 0, float craterRadius = BlastRadius,
                                TankKind? teamA = null, TankKind? teamB = null,
                                Weather weather = Weather.Clear,
                                int firstTeam = 0, bool swapSpawn = false, bool alternate = true,
                                float? errorRatioB = null, bool fairItems = false)
    {
        var rng = new Rng(seed);
        var vol = NewVolume();
        var units = new List<U>();
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < MapHeightFunction.TeamSize; i++)
            {
                int side = swapSpawn ? 1 - t : t;      // 스폰 교환 — 지형 비대칭 판별용
                SpawnAt(side, i, out float x, out float z);
                float g = TankGroundProbe.GroundBelow(vol, x, z, 60f);
                var kind = t == 0 ? (teamA ?? TankKind.Carrot) : (teamB ?? TankKind.Carrot);
                if (kind == TankKind.Catapult) SawCatapult = true;   // 화상 게이트가 "이번 실행에 캐터펄트가 있었나"를 알아야 한다
                // 종류를 지정한 실험에서는 그 탱크의 HP 를 쓴다(hp 인자는 HP 스윕 전용)
                int startHp = (teamA.HasValue || teamB.HasValue) ? TankStats.Get(kind).Hp : hp;
                units.Add(new U { Id = t * MapHeightFunction.TeamSize + i, Team = t, Kind = kind, Hp = startHp, MaxHp = startHp, W = weather,
                                  X = x, Z = z, Y = float.IsNegativeInfinity(g) ? 10f : g });
            }

        // ⚠️ 하네스 버그였다. 유닛이 A0 A1 A2 B3 B4 B5 순으로 리스트에 들어가고 턴 루프가 그 순서를
        //    그대로 돌아서 **A팀이 세 발을 연속으로 쏜 뒤에야 B팀 차례**가 왔다. 게임(BattleDemo 195행)은
        //    팀 교대인데 하네스만 달랐다. 특수탄이 죽어 있을 땐 미러 50% 로 안 보였고, 낙하 피해가
        //    들어오자 미러 80~90% 로 터졌다 — 세 발 연속 굴착이 상대 발밑을 통째로 끊는다.
        //    게임과 같은 교대 순서로 맞춘다. 판별용으로 옛 순서(alternate=false)도 남긴다.
        // ⚠️ **두 번째 하네스 버그**(2026-09-16 발견). 예전엔 교대 리스트를 만든 뒤 등록 인덱스를
        //    `(i + firstTeam) % 6` 으로 **한 칸 회전**시켜 "B선공"을 만들었다. 그런데 이 맵의 조준 AI 는
        //    가장 가까운 적을 쏘므로 같은 슬롯끼리 1:1 결투 3쌍이 된다(A0↔B0, A1↔B1, A2↔B2).
        //    한 칸 회전은 **첫 쌍의 선공만 뒤집고 나머지 두 쌍은 그대로 A 가 먼저 쏜다** — 즉 "B선공"이
        //    3쌍 중 1쌍에만 걸린 반쪽짜리 대조군이었다. 오차 0% 자기전에서 B선공인데도 A 가 68% 이기는
        //    것으로 들통났다(구조적 편향이 아니라 대조군이 고장난 것이었다).
        //    이제 **팀 순서 자체를 뒤집어** 세 쌍 전부에서 선공이 넘어가게 한다.
        if (alternate)
        {
            int ft = firstTeam;
            units.Sort((a, b) => (a.Id % MapHeightFunction.TeamSize) * 2 + (ft == 1 ? 1 - a.Team : a.Team)
                               - ((b.Id % MapHeightFunction.TeamSize) * 2 + (ft == 1 ? 1 - b.Team : b.Team)));
        }

        // 원작 딜레이 턴제(TurnOrder). 동률은 등록 순서이므로 리스트 순서 그대로 등록한다.
        // (alternate=false 인 옛 순차 순서에서는 firstTeam 이 옛 회전 의미 그대로다 — 판별용으로만 쓴다.)
        var order = new TurnOrder();
        for (int i = 0; i < units.Count; i++)
        {
            var uu = alternate ? units[i] : units[(i + firstTeam * 3) % units.Count];
            order.Add(uu.Id, uu.St.Delay);
        }
        var byId = new Dictionary<int, U>();
        foreach (var uu in units) byId[uu.Id] = uu;
        Func<int, bool> alive = id => byId[id].Alive;

        var res = new MatchResult { Winner = -1, BucketShots = new int[8], BucketHits = new int[8], MoveWhyA = new int[7] };
        var wind = new Vec3(0, 0, 0);
        var status = new StatusEffects();   // 독·화상·속박
        var hazards = new HazardField();    // 지뢰·지속불
        // 아이템(§2-9-10). 게임과 **같은 ItemState** 를 쓴다 — 한쪽만 아이템을 쓰면 승률이 게임의 승률이 아니다.
        var items = new ItemState();
        var impair = new ImpairState();     // 방해탄(§2-9-14)
        var air = new AirField();           // 기후 — 증폭벽·회오리(§2-9-15)
        if (ClimateOn) air.Roll(ref rng, MapSize);
        var supply = new SupplyDrop();      // 헬기 보급(§2-9-11)
        var hazBuf = new List<HazardField.HazardView>();      // AiMover 에 넘길 장판 스냅샷(매 턴 재사용)
        var aiFoes = new List<AiGunner.Target>();             // AiMover 에 넘길 적 목록(매 턴 재사용)
        if (ItemSlots > 0)
        {
            var roll = new List<ItemKind>();
            // ⚠️ `fairItems` 는 **미러 게이트 전용**이다(게임에는 없다).
            //    아이템은 유닛 Id 로 배분되므로 스폰을 교환해도 따라가지 않는다 —
            //    그래서 거울 조건에서 승률이 보존되지 않고(기본+교환 합이 100 이 아니게 되고) 미러 평균이 기운다.
            //    실측: 아이템 OFF 면 합 98.4(대칭 성립), ON 이면 85.5(깨짐).
            //    미러 게이트가 재려는 건 **탱크 수치만의 대칭**이지 아이템 운이 아니므로, 여기서만 양 팀에 같은 세트를 준다.
            if (fairItems)
            {
                var perSlot = new List<ItemKind>[MapHeightFunction.TeamSize];
                for (int i = 0; i < MapHeightFunction.TeamSize; i++) { var r0 = new List<ItemKind>(); Items.Roll(ref rng, ItemSlots, r0); perSlot[i] = r0; }
                foreach (var o in units) items.Bag(o.Id).AddRange(perSlot[o.Id % MapHeightFunction.TeamSize]);
            }
            else
                foreach (var o in units) { Items.Roll(ref rng, ItemSlots, roll); items.Bag(o.Id).AddRange(roll); }
        }

        for (int step = 0; step < maxTurns; step++)
        {
            // 라운드마다 바람 갱신(§15)
            if (step % 6 == 0)
            {
                float a = rng.Range(0f, MathF.PI * 2f), s = rng.Range(0f, 10f);
                // ⚠️ 무풍 대조군(TANKFALL_NOWIND=1). 미러가 기울 때 **바람 때문인지**를 가르는 판별용이다.
                //    난수는 그대로 소비한다 — 안 그러면 뒤따르는 모든 난수가 밀려 다른 판이 된다.
                wind = NoWind ? new Vec3(0f, 0f, 0f) : new Vec3(MathF.Cos(a) * s, 0f, MathF.Sin(a) * s);
                // 헬기 보급(§2-9-11) — 아이템을 끈 실험에서는 보급도 끈다(비교가 오염되면 안 된다).
                if (ItemSlots > 0 && SupplyOn)
                {
                    // 살아 있는 탱크를 앵커로 넘긴다 — 닿을 수 있는 자리에 떨어져야 보급이다.
                    // 팀도 같이 넘긴다 — 안 넘기면 유닛이 많은(이기는) 쪽에 상자가 몰려 우위가 증폭된다
                    // (미러 평균을 50%→44.9% 로 끌어내린 원인, SupplyDrop.RollDrop 머리말).
                    var anchors = new List<Vec3>();
                    var anchorTeams = new List<int>();
                    foreach (var o in units) if (o.Alive) { anchors.Add(new Vec3(o.X, o.Y, o.Z)); anchorTeams.Add(o.Team); }
                    res.SupplyDropped += supply.RollDrop(ref rng, MapSize, (x, z) => TankGroundProbe.GroundBelow(vol, x, z, 80f), anchors, anchorTeams);
                }
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
            // 보급 상자를 밟고 서 있으면 먼저 줍는다(이동 안 해도 그 자리에 떨어진 경우).
            if (ItemSlots > 0 && SupplyOn)
            {
                var got0 = supply.TryPickup(u.X, u.Y, u.Z);
                if (got0 != ItemKind.None) { items.Bag(u.Id).Add(got0); res.SupplyPicked++; if (u.Team == 0) res.PickedA++; else res.PickedB++; }
            }
            // ⚠️ 원작에서 보급은 "이동으로 줍는" 것이다 — AI 가 상자를 무시하면 헬기가 장식이 된다.
            //    처음엔 탐색 반경 10m·이동확률 40% 로 뒀더니 **판당 0.1회**밖에 안 주웠다(상자는 맵 전역에
            //    떨어지는데 탱크는 양쪽 끝에 몰려 있다). 한 턴에 8m 밖에 못 가므로 **여러 턴에 걸쳐 걸어갈**
            //    의지가 있어야 한다 — 반경을 넓히고, 상자를 노리는 턴은 반드시 움직이게 했다.
            // ⚠️ 2026-09-17: 여기 있던 **하네스 전용 이동 정책**(40% 임의 방향 + 상자 탐색)을 걷어냈다.
            //    게임의 AI 는 그때 이동 페이즈를 통째로 건너뛰고 있었으므로 **하네스가 게임보다 잘 움직였고**,
            //    보급·지뢰·지속불 승률이 게임의 승률이 아니었다(§2-9-1: 게임과 하네스는 같은 규칙을 써야 한다).
            //    이제 둘 다 Sim/AiMover.Decide 를 부른다. 정책을 여기서 또 쓰지 마라.
            //
            // ⚠️ 걷는 것만 여기 남긴다 — AiMover.Walk 가 아니라 이 루프를 쓰는 이유는
            //    **걸음마다** 상자를 줍고 지뢰를 밟아야 하기 때문이다(게임의 DriveUnit 도 같은 구조).
            //    걸음 크기는 양쪽 다 TankGroundProbe.WalkStep 이라 1m vs 0.25m 사고는 재발하지 않는다.
            hazards.Snapshot(hazBuf);
            aiFoes.Clear();
            foreach (var o in units)
                if (o.Alive && o.Team != u.Team) aiFoes.Add(new AiGunner.Target { Id = o.Id, Kind = o.Kind, Center = o.Center, Defense = o.St.Defense });
            var mplan = AiMover.Decide(vol, u.X, u.Y, u.Z, status.CanMove(u.Id), u.St.MaxRange, MapSize,
                                       hazBuf, ItemSlots > 0 ? supply : null, aiFoes, ref rng);
            bool seeking = mplan.Why == MoveReason.Supply;
            if (seeking) { res.SeekTurns++; if (u.Team == 0) res.SeekA++; else res.SeekB++; }
            if (u.Team == 0) res.MoveWhyA[(int)mplan.Why]++;
            if (mplan.Move)
            {
                float dx = mplan.DirX, dz = mplan.DirZ;
                // ⚠️ **하네스가 게임보다 훨씬 덜 움직이던 버그**(2026-09-16). 게임(BattleDemo.MoveUnit)은
                //    이동을 `TankGroundProbe.WalkStep`(0.25m)으로 쪼개 걷는다 — 크레이터 턱은 가장자리 ε 구간의
                //    상승량이 √(2Rε) 라 걸음이 크면 "너무 가파름"으로 막히기 때문이다(TankGroundProbe 머리말).
                //    하네스는 1m 걸음이라 같은 지형에서 게임이라면 넘었을 턱에 걸렸다.
                //    진단: 상자를 주우러 간 턴 4.29 중 2.92 가 지형에 막힘 → 헬기 보급이 죽어 있었다.
                const float Walk = TankGroundProbe.WalkStep;
                int steps = (int)(mplan.Distance / Walk);
                float gy = u.Y; bool moved = false;
                for (int k = 0; k < steps; k++)
                {
                    // 막히면 그냥 포기하던 게 보급이 죽은 진짜 원인이었다(진단: 주우러 간 턴 4.62 중 3.61 이 막힘).
                    // 직선으로만 걸으면 언덕 턱에 걸려 상자 앞에서 멈춘다. 옆으로 돌아가게 한다 [추정].
                    bool stepped = false;
                    for (int t = 0; t < 3; t++)
                    {
                        float ang = t == 0 ? 0f : (t == 1 ? 0.6f : -0.6f);
                        float ca = MathF.Cos(ang), sa = MathF.Sin(ang);
                        float sx = (dx * ca - dz * sa) * Walk, sz = (dx * sa + dz * ca) * Walk;
                        float tx = u.X + sx, tz = u.Z + sz;
                        if (tx < 5f || tx > MapSize - 5f || tz < 5f || tz > MapSize - 5f) continue;
                        if (TankGroundProbe.CanStepTo(vol, gy, tx, tz, out float ty) != TankGroundProbe.MoveResult.Ok) continue;
                        u.X = tx; u.Z = tz; gy = ty; stepped = true; break;
                    }
                    if (!stepped) { if (seeking) res.SeekBlocked++; break; }
                    moved = true;
                    float ny = gy;
                    if (ItemSlots > 0 && SupplyOn)
                    {
                        var got = supply.TryPickup(u.X, ny, u.Z);
                        if (got != ItemKind.None) { items.Bag(u.Id).Add(got); res.SupplyPicked++; if (u.Team == 0) res.PickedA++; else res.PickedB++; }
                    }
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

            // ── 아이템 사용(AI) ──
            // 정책 [추정]: 턴을 안 먹는 아이템은 **쓸 수 있으면 쓴다**(안 쓰면 그냥 손해다).
            //   턴을 먹는 것(에너지2)은 정말 급할 때만. 텔레포트탄·바람반대는 AI 가 이득을 판단할
            //   방법이 아직 없어서 안 쓴다 — **안 쓰는 걸 숨기지 말고 카운터로 드러낸다**(0 이면 사장).
            items.TickStartOfTurn(u.Id);
            impair.TickStartOfTurn(u.Id);
            bool turnSpentOnItem = false;
            if (ItemSlots > 0)
            {
                bool lowHp = u.Hp < u.MaxHp / 2;
                bool critical = u.Hp < u.MaxHp / 4;
                // 눈내리기는 **팀에 포세이돈이 있을 때만** 값을 한다(SnowBonus, §2-9-7).
                bool wantSnow = weather != Weather.Snow;
                if (wantSnow)
                {
                    bool hasPoseidon = false;
                    foreach (var t in units) if (t.Team == u.Team && t.Alive && t.Kind == TankKind.Poseidon) hasPoseidon = true;
                    wantSnow = hasPoseidon;
                }
                foreach (var k in new[] { ItemKind.Shield, ItemKind.MoveUp, ItemKind.PowerUp, ItemKind.SnowFall, ItemKind.TeamEnergy, ItemKind.AddEnergy1, ItemKind.DoubleFire, ItemKind.AddEnergy2, ItemKind.LockAngleShell, ItemKind.LockPowerShell })
                {
                    if (!items.Has(u.Id, k)) continue;
                    // 판별용(TANKFALL_NOHEAL=1): 회복 계열만 끈다 — 미러 편향이 회복 때문인지 가른다.
                    if (NoHeal && (k == ItemKind.AddEnergy1 || k == ItemKind.AddEnergy2 || k == ItemKind.TeamEnergy)) continue;
                    if (k == ItemKind.AddEnergy1 && !lowHp) continue;
                    if (k == ItemKind.AddEnergy2 && !critical) continue;
                    if (k == ItemKind.Shield && items.HasShield(u.Id)) continue;
                    if (k == ItemKind.SnowFall && !wantSnow) continue;
                    if (k == ItemKind.TeamEnergy && !lowHp) continue;
                    var capturedWeather = weather;
                    var r = items.Use(u.Id, u.Team, k,
                        (id, frac) => { var t = units.Find(x => x.Id == id); if (t != null) t.Hp = Math.Min(t.MaxHp, t.Hp + (int)(t.MaxHp * frac)); },
                        (team, frac) => { foreach (var t in units) if (t.Team == team && t.Alive) t.Hp = Math.Min(t.MaxHp, t.Hp + (int)(t.MaxHp * frac)); },
                        () => { weather = Weather.Snow; foreach (var t in units) t.W = Weather.Snow; hazards.ClearFires(); },   // 눈이면 불·독가스는 사라진다(원작)
                        () => { wind = new Vec3(-wind.X, 0f, -wind.Z); });
                    if (r == ItemState.UseResult.NotHeld) continue;
                    res.ItemsUsed++;
                    // 에너지2 는 턴을 먹는다 — 쓰면 이번 턴은 사격 없이 끝난다.
                    if (r == ItemState.UseResult.AppliedEndsTurn && k == ItemKind.AddEnergy2) { turnSpentOnItem = true; break; }
                }
            }
            if (turnSpentOnItem) continue;

            var enemies = new List<AiGunner.Target>();
            foreach (var o in units) if (o.Alive && o.Team != u.Team) enemies.Add(new AiGunner.Target { Id = o.Id, Kind = o.Kind, Center = o.Center, Defense = o.St.Defense });
            if (enemies.Count == 0) break;

            var st = u.St;
            float err = (u.Team == 1 && errorRatioB.HasValue) ? errorRatioB.Value : errorRatio;
            // ⚠️ 게임(`BattleDemo.AiShoot`)과 **같은 하늘**을 넘긴다 — 한쪽만 기후를 보면 표가 다른 게임을 잰다.
            var plan = AiGunner.Decide(vol, u.Muzzle, enemies, wind, err, ref rng, MapSize, st, air);
            if (!plan.Valid) continue;

            // 방해탄(§2-9-14) — 각도고정·파워고정은 **AI 에게도 진짜 효과가 있다**(조준을 묶는다).
            //   각도고정: 원작 "각도를 조절할 수 없다" → 직전 각도에 묶인다.
            //   파워고정: 원작 "50% 이하의 힘으로 발사할 수 없게 된다" → 하한이 생긴다.
            if (impair.AngleLocked(u.Id)) plan.PitchDeg = u.LastPitch;
            plan.Power = impair.ClampPower(u.Id, plan.Power);
            u.LastPitch = plan.PitchDeg;

            float speed = st.SpeedAt(plan.Power);
            var accel = st.AccelWith(wind.X, wind.Z);
            var boxes = new List<TankHitbox>();
            foreach (var o in units) if (o.Alive) boxes.Add(new TankHitbox { Id = o.Id, Center = o.Center, Radius = TankRadius });

            // 사격 산포 — 조준이 끝난 뒤, 쏘기 직전에 흔든다(게임과 같은 자리·같은 함수).
            //   AI 는 정확히 조준하고 **세계가** 흔든다. 그래서 조준 계산(AiGunner)에는 안 들어간다.
            if (UseSpread)
            {
                Spread.AimJitter(ref rng, u.Kind, out float jy, out float jp);
                plan.YawDeg += jy;
                plan.PitchDeg += jp;
            }
            // 1) 기준 탄도(패턴 중앙) 한 발로 착탄점을 본다
            var shot = ProjectileSimulator.Simulate(vol, u.Muzzle, Ballistics.VelocityFrom(plan.YawDeg, plan.PitchDeg, speed), accel, boxes, u.Id, MapSize, st.Flight, null, air);
            res.Shots++;
            if (u.Team == 0) res.ShotsA++;
            int bk = Math.Min(7, res.Turns / 20);
            res.BucketShots[bk]++;
            // 나이스샷 판정(원작: 게이지를 표시 지점에 정확히 멈추면 포인트 +1) — AI 는 확률 [추정]
            if (NiceShot.AiJudge(err, ref rng)) u.Skill.OnNiceShot();
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
                                                      Spread.Pattern(u.Kind, ShellKind.Normal), shot, st.Flight, null, air);
            var specialHits = satImpact.HasValue ? null
                : AiGunner.SimulatePattern(vol, u.Muzzle, plan.YawDeg, plan.PitchDeg, speed, accel, boxes, u.Id, MapSize,
                                           Spread.Pattern(u.Kind, ShellKind.Special), shot, st.Flight, null, air);
            var shell = AiGunner.PickShell(st, normalHits, specialHits, enemies, status, satImpact, satDirect);
            bool ss = false, ultShell = false;   // 궁극기(§49) — 2번탄을 핵급으로 키운다
            if (shell == ShellKind.Special)
            {
                if (u.Team == 0) res.SpecialUsed++;
                st = Stats(u.Kind, shell, u.HpFrac, weather);
                order.AddExtra(u.Id, shell, false);     // 특수탄 추가 딜레이 [추정]
                // ⚠️ 궁극기를 먼저 본다 — SS 를 먼저 쓰면 포인트가 계속 깎여 궁극기가 영원히 안 나온다(게임 쪽과 같은 규칙).
                if (UseUltimate && u.Skill.CanUltimate()) { st = NiceShot.ApplyUltimate(st, u.Kind); u.Skill.SpendUltimate(); ss = true; ultShell = true; if (u.Team == 0) res.UltUsed++; }
                // ⚠️ 아끼지 않으면 2점에서 SS 로 다 써버려 궁극기가 **영원히 안 나온다**(NiceShot.AiSaveForUltimate 머리말).
                else if (u.Skill.CanSs() && !(UseUltimate && NiceShot.AiSaveForUltimate(u.Skill.Points, ref rng)))
                { st = NiceShot.ApplySs(st); u.Skill.SpendSs(); ss = true; if (u.Team == 0) res.SsUsed++; }
            }
            var fx = ShellEffects.Of(u.Kind, shell);

            // 파워업(§2-9-10): 원작 분류가 "능력 아이템(공격력 강화)"이라 **피해**를 올린다.
            // ⚠️ 속도를 올리면 안 된다 — AI·플레이어가 이미 조준을 마친 뒤에 거는 아이템이라
            //    속도가 바뀌면 그 조준이 통째로 빗나간다(아이템이 손해가 된다).
            if (items.HasPowerUp(u.Id))
            {
                st.BaseDamage *= Items.PowerUpScale;
                st.DirectDamage *= Items.PowerUpScale;
            }

            // 3) 다탄두: 고른 탄종의 패턴대로 전부 날린다(중앙 탄 = 위 기준 탄도)
            var pattern = Spread.Pattern(u.Kind, shell, ultShell);   // 궁극기는 연사형만 탄두가 는다(게임과 같은 규칙)
            bool anyHit = false;
            // 발당 굴착은 게임과 **같은 함수**를 쓴다(CraterShape.PerShotCrater 머리말: 0.65 고정은 9연에서 5.9배로 팠다).
            float craterEach = CraterShape.PerShotCrater(
                (teamA.HasValue || teamB.HasValue) ? st.CraterRadius : craterRadius, pattern.Count);
            int volleys = items.HasDoubleFire(u.Id) ? 2 : 1;   // 더블파이어: 같은 각도·파워로 한 발 더(원작)
            for (int v = 0; v < volleys; v++)
            {
            if (v > 0 && u.Team == 0) res.ItemsDoubleFired++;
            for (int pi = 0; pi < pattern.Count; pi++)
            {
                var pt = pattern[pi];
                // ⚠️ 두 번째 발은 **다시 푼다**. 첫 발이 지형을 깎아 놓아서 같은 궤적이라도 착탄점이 다르다.
                // 유도탄(2번탄)은 정점 뒤에 적 쪽으로 휘므로 기준 탄도를 재사용하지 않고 유도 포함으로 다시 푼다(게임 FireFrom 과 같은 규칙).
                int uidH = u.Id;
                var sub = (pattern.Count == 1 && v == 0 && !st.Flight.HasHoming) ? shot
                    : ProjectileSimulator.Simulate(vol, u.Muzzle,
                        Ballistics.VelocityFrom(plan.YawDeg + pt.YawOffsetDeg, plan.PitchDeg + pt.PitchOffsetDeg, speed),
                        accel, boxes, u.Id, MapSize, st.Flight, id => byId[id].Team != byId[uidH].Team, air);
                if (pattern.Count > 1 && u.Team == 0) res.SubShells++;
                if (!sub.Hit) continue;

                Vec3 impact = sub.Impact; int directId = sub.DirectHitTankId;
                // 기후(§2-9-15): 증폭벽을 지난 탄은 피해가 1.5배(원작), 회오리에 걸린 탄은 엉뚱한 데 떨어진다.
                float ampScale = sub.DamageScale;
                if (ampScale > 1f && u.Team == 0) res.AmpShots++;
                if (sub.Tornadoed && u.Team == 0) res.TornadoShots++;
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

                // ⚠️ 굴착 모양은 게임(BattleDemo)과 **같은 표**를 써야 한다 — 한쪽만 쓰면 이 하네스의 승률이
                //    게임의 승률이 아니게 된다(§2-9-1 교대 순서 버그의 교훈).
                var blast = SdfDeformer.SubtractSphere(vol,
                    new BlastRequest(impact.X, impact.Y, impact.Z, craterEach, UseCraterShape ? CraterShape.VScaleOf(u.Kind, shell) : 1f));
                CeilingCollapse.Apply(vol, blast);
                // 원작: 상자를 부수면 그 안의 아이템도 사라진다(적이 못 줍게 부수는 것도 전술).
                if (ItemSlots > 0 && SupplyOn) res.SupplyDestroyed += supply.DestroyNear(impact.X, impact.Y, impact.Z, st.BlastRadius);

                foreach (var o in units)
                {
                    if (!o.Alive) continue;
                    float dist = (o.Center - impact).Length;
                    bool direct = o.Id == directId;
                    if (dist > st.BlastRadius && !direct) continue;
                    int dmg = Damage.AfterDefense(
                        Damage.Compute(dist, st.BlastRadius, st.BaseDamage * pt.DamageScale * ampScale, st.DirectDamage * pt.DamageScale * ampScale, direct),
                        o.St.Defense);
                    if (dmg <= 0) continue;
                    // 실드: 들어오는 공격 1회를 통째로 막는다(원작). 피해 계산 **뒤·적용 앞**에서 소모한다.
                    if (items.ConsumeShield(o.Id)) { res.ItemsShielded++; continue; }
                    o.Hp = Math.Max(0, o.Hp - dmg);
                    if (o.Team != u.Team) { anyHit = true; if (u.Team == 0) res.BlastDealtA += dmg; }
                    // 맞은 유닛에 붙는 효과
                    if (fx.Type == ShellEffects.EffectType.Poison && o.Id != u.Id) status.Poison(o.Id, fx.Param1, fx.Param2, o.Kind);
                    if (fx.Type == ShellEffects.EffectType.Root && o.Team != u.Team) status.Root(o.Id, fx.Param1);
                    // 화상(2026-09-19 배선) — **게임(BattleDemo)과 같은 조건이어야 한다.** 맞은 유닛 전부, 눈이면 안 붙는다.
                    if (fx.Type == ShellEffects.EffectType.Burn && weather != Weather.Snow)
                    { status.Burn(o.Id, fx.Param1, fx.Param2); BurnApplied++; }
                    // 방해탄(§2-9-14): 맞은 적에게 건다.
                    var imk = items.ImpairShot(u.Id);
                    if (imk != ImpairKind.None && o.Team != u.Team)
                    { impair.Apply(o.Id, imk); if (u.Team == 0) res.ImpairHitA++; }
                }
                // 자리에 남는 효과
                // 눈이면 불·독가스는 안 남는다 — 원작 규칙(https://namu.wiki/w/포트리스2 · 2026-09-17).
                // 게임(BattleDemo)과 같은 규칙이어야 승률이 게임의 승률이다.
                if ((fx.Type == ShellEffects.EffectType.Burn || fx.Type == ShellEffects.EffectType.PoisonCloud)
                    && weather != Weather.Snow)
                    hazards.PlaceFire(impact.X, impact.Y, impact.Z, st.BlastRadius, fx.Param1, fx.Param2);   // 지속불·독구름은 같은 장판 구조
                if (fx.Type == ShellEffects.EffectType.Mine) hazards.PlaceMine(impact.X, impact.Y, impact.Z, 4f, fx.Param1, u.Id);   // 반경 4m [추정]
            }
            }
            items.ClearShotFlags(u.Id);   // 이번 사격용 효과(파워업·더블파이어·텔레포트)는 여기서 반드시 지운다
            // 지형이 깎였으니 상자도 내려앉힌다 — 공중에 뜬 상자는 주울 수 없다.
            if (ItemSlots > 0 && SupplyOn) supply.Settle((x, z) => TankGroundProbe.GroundBelow(vol, x, z, 80f));
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

        // ── 팀 인원 대조군 (TANKFALL_TEAM=3) ──
        // 게임 기본은 4:4 다. "4:4 라서 판이 길어졌다"를 말하려면 **같은 맵·같은 시드로 3:3 을 나란히**
        // 재야 한다 — §2-5 의 옛 10.1분은 맵이 달라(옛 하네스 언덕) 대조군이 못 된다.
        // ⚠️ 판을 만들기 **전에** 설정해야 한다. 유닛 Id 가 `team * TeamSize + i` 로 굳기 때문이다.
        var teamEnv = Environment.GetEnvironmentVariable("TANKFALL_TEAM");
        if (!string.IsNullOrWhiteSpace(teamEnv))
        {
            if (!int.TryParse(teamEnv.Trim(), out int tn))
            { Console.WriteLine($"❌ TANKFALL_TEAM={teamEnv}: 정수가 아니다"); Environment.Exit(2); }
            else
            {
                try { MapHeightFunction.SetForHarness(tn); }
                catch (ArgumentOutOfRangeException e) { Console.WriteLine($"❌ TANKFALL_TEAM={teamEnv}: {e.Message}"); Environment.Exit(2); }
                Console.WriteLine($"⚠️ 팀 인원 대조군: {tn}:{tn} (게임 기본 4:4 아님 — 이 표를 밸런스 근거로 쓰지 마라)");
            }
        }

        var mapEnv = Environment.GetEnvironmentVariable("TANKFALL_MAP");
        if (!string.IsNullOrEmpty(mapEnv))
        {
            if (mapEnv.Trim().Equals("Legacy", StringComparison.OrdinalIgnoreCase)) MapName = "Legacy";
            else if (MapHeightFunction.TryParse(mapEnv, out Map)) MapName = MapHeightFunction.Name(Map);
            else { Console.WriteLine($"❌ TANKFALL_MAP={mapEnv}: 모르는 맵(TwinHills|Crater|Terrace|Valley|Ridge|Badlands|Legacy)"); Environment.Exit(2); }
        }
        Console.WriteLine($"맵: {MapName}" + (LegacyMap ? " (옛 언덕 — §2-9-4 회귀 비교용)" : " (MapHeightFunction — 게임과 동일)"));
        var clEnv = Environment.GetEnvironmentVariable("TANKFALL_CLIMATE");
        if (!string.IsNullOrWhiteSpace(clEnv)) ClimateOn = clEnv.Trim() != "0";
        var sdEnv = Environment.GetEnvironmentVariable("TANKFALL_SEED");
        if (!string.IsNullOrWhiteSpace(sdEnv)) uint.TryParse(sdEnv.Trim(), out SeedOffset);
        var supEnv = Environment.GetEnvironmentVariable("TANKFALL_SUPPLY");
        if (!string.IsNullOrWhiteSpace(supEnv)) SupplyOn = supEnv.Trim() != "0";
        var itEnv = Environment.GetEnvironmentVariable("TANKFALL_ITEMS");
        if (!string.IsNullOrEmpty(itEnv))
        {
            if (!int.TryParse(itEnv.Trim(), out ItemSlots) || ItemSlots < 0) { Console.WriteLine($"❌ TANKFALL_ITEMS={itEnv}: 0 이상 정수여야 한다"); Environment.Exit(2); }
        }
        Console.WriteLine($"아이템 슬롯: {ItemSlots}" + (ItemSlots == 0 ? " (끔 — 아이템 도입 전 수치와 비교용)" : ""));
        var wEnv = Environment.GetEnvironmentVariable("TANKFALL_WEATHER");
        if (!string.IsNullOrEmpty(wEnv))
        {
            if (!Enum.TryParse(wEnv.Trim(), true, out Wx)) { Console.WriteLine($"❌ TANKFALL_WEATHER={wEnv}: 모르는 날씨(Clear|Snow)"); Environment.Exit(2); }
            Console.WriteLine($"날씨: {Wx} (포세이돈 SnowBonus 측정용)");
        }
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
            BurnWiringGate();
            if (RcFail) Environment.Exit(1);
            return;
        }
        Console.WriteLine($"=== AI 자동 대전 ({MapHeightFunction.TeamSize}v{MapHeightFunction.TeamSize}) ===\n");
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
            var dvol = NewVolume();
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
                var tl = new List<AiGunner.Target> { new AiGunner.Target { Id = 9, Kind = TankKind.Cannon, Center = tc } };
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
                                     TankKind.Carrot, TankKind.Carrot, Wx, c.first, c.swap, c.alt);
                    if (r.Winner >= 0) { decided++; if (r.Winner == 0) winA++; }
                }
                float pct = decided > 0 ? winA * 100f / decided : -1f;
                string read = pct > 65f ? "  ← A 유리" : pct < 35f ? "  ← B 유리" : "  ← 대칭";
                Console.WriteLine($"    {c.name,-26} {pct,5:F0}%{read}");
            }
            Console.WriteLine("    ※ 읽는 법: 'B선공'에서 뒤집히면 선공 이점, '스폰 교환'에서 뒤집히면 지형 비대칭.");
            // 지형 비대칭을 숫자로: 각 스폰 지점의 지면 높이
            var probe = NewVolume();
            string ha = "", hb = "";
            for (int i = 0; i < MapHeightFunction.TeamSize; i++)
            {
                SpawnAt(0, i, out float sax, out float saz); SpawnAt(1, i, out float sbx, out float sbz);
                float ga = TankGroundProbe.GroundBelow(probe, sax, saz, 60f);
                float gb = TankGroundProbe.GroundBelow(probe, sbx, sbz, 60f);
                ha += $"{ga,5:F1} "; hb += $"{gb,5:F1} ";
            }
            Console.WriteLine($"    스폰 지면 높이  A팀 [{ha}]  B팀 [{hb}]  (높은 쪽이 사거리·시야 유리)");
        }

        // [6-2] TwinHills v5 재측정에서 미러가 크게 벌어진 기종(레이저 90%, 듀크 74%, 캐터펄트 74%)을
        //       [6-1]과 같은 방식으로 각각 판별한다 — SESSION_HANDOFF.md 의 "하지 말 것"(수치부터 만지지 말 것) 지시에 따른
        //       원인 분리 작업. 캐롯([6-1])은 이미 "대칭"으로 나왔으니(범용 판별로는 못 잡음) 여기서 재확인 안 함.
        Console.WriteLine("");
        Console.WriteLine($"[6-2] 미러 이탈 원인 판별 — 맵={MapName}, 기종별 자기전(自己戰) 각 20판, A팀 승률");
        foreach (var kind in new[] { TankKind.Laser, TankKind.Duke, TankKind.Catapult })
        {
            Console.WriteLine($"  · {kind}");
            (string name, int first, bool swap, bool alt)[] conds =
            {
                ("순차 AAA BBB (구 하네스)", 0, false, false),
                ("교대 A선공 (게임과 동일)", 0, false, true),
                ("교대 B선공",              1, false, true),
                ("교대 A선공 + 스폰 교환",  0, true,  true),
            };
            foreach (var c in conds)
            {
                int winA = 0, decided = 0, turnsSum = 0;
                for (uint m = 0; m < 20u; m++)
                {
                    var r = RunMatch(1000 + m * 77, 0.025f, MaxHp, 400, SuddenDeathTurn, BlastRadius,
                                     kind, kind, Wx, c.first, c.swap, c.alt);
                    turnsSum += r.Turns;
                    if (r.Winner >= 0) { decided++; if (r.Winner == 0) winA++; }
                }
                float pct = decided > 0 ? winA * 100f / decided : -1f;
                float avgTurn = decided > 0 ? turnsSum / 20f : -1f;
                string read = pct > 65f ? "  ← A 유리" : pct < 35f ? "  ← B 유리" : "  ← 대칭";
                Console.WriteLine($"      {c.name,-26} {pct,5:F0}%{read}  (평균 {avgTurn:F0}턴)");
            }
        }
        Console.WriteLine("    ※ 읽는 법: 'B선공'에서 뒤집히면 선공 이점, '스폰 교환'에서 뒤집히면 지형 비대칭.");
        Console.WriteLine("    ※ 판이 짧을수록(레이저 등) 선공 이점이 상쇄되기 전에 끝난다는 가설 — 평균 턴 열로 대조.");

        // [6-2-1] 매치업표 미러 열의 **게이트**. 미러 칸은 셀당 20판이라 ±11%p 로 흔들려 한 칸만 보면
        //         "편향"과 "운"을 못 가른다(실제로 그래서 두 번이나 오진했다). 12종 전부를 매치업과
        //         **똑같은 조건 패턴**(선공·스폰 2판마다 교차)으로 표본만 늘려 재고 평균을 본다.
        //         12종 평균이 50% 에서 벗어나면 그건 탱크가 아니라 하네스가 기운 것이다.
        Console.WriteLine("");
        Console.WriteLine($"[6-2-1] 미러 게이트 — 12종 자기전, 각 {MirrorGateN}판, 매치업과 같은 교차 패턴");
        // TANKFALL_ONLY=mirror 면 여기까지만 돌고 끝낸다 — 미러를 반복 측정할 때 전체 표를 기다리지 않기 위해서다.
        {
            var kinds = TankStats.Selectable();
            float sum = 0f; int n = 0; string worstName = "-"; float worst = 50f;   // worst=50 에서 시작해야 첫 기종부터 비교된다
            // 조합별 분해(§2-9-6 [6-2] 와 같은 판별): 스폰 교환 × 선공 4칸을 따로 세면
            // **어느 축이 기울었는지**가 보인다. 평균만 보면 "기울었다"까지만 알고 원인을 못 짚는다.
            var comboWin = new int[4]; var comboDec = new int[4];
            foreach (var k in kinds)
            {
                int winA = 0, decided = 0;
                // 🚨 **세 번째 표본 사고**(2026-09-17). 예전엔 12종이 전부 `1000 + m*77` 로 **같은 시드열**을 썼다.
                //    같은 바람·같은 나이스샷·같은 투하가 12종에 통째로 걸려 판들이 서로 독립이 아닌데,
                //    아래 표준오차는 720판이 독립인 양 계산한다 → ±1.9%p 라고 말하면서 실제로는 시드마다
                //    **44.9 / 54.2 / 45.7 / 49.6 (SD 약 4%p)** 로 흔들렸다. 그 흔들림을 "보급이 대칭을 깼다"로
                //    오독할 뻔했다(이 프로젝트에서 표본을 과신한 게 이번이 세 번째다).
                //    기종마다 시드열을 떼어 놓으면 12개가 진짜 독립 표본이 되고 평균의 오차가 식과 맞는다.
                uint kseed = (uint)((int)k + 1) * 9973u;
                for (uint m = 0; m < MirrorGateN; m++)
                {
                    int swap = (int)((m >> 1) & 1);
                    bool bFirst = (m & 1) == 1;
                    int combo = swap * 2 + (bFirst ? 1 : 0);
                    var r = RunMatch(1000 + SeedOffset + kseed + m * 77, MirrorErr, MaxHp, 400, SuddenDeathTurn, BlastRadius,
                                     k, k, Wx, swap, bFirst, true, null, fairItems: true);
                    if (r.Winner >= 0)
                    {
                        decided++; if (r.Winner == 0) winA++;
                        comboDec[combo]++; if (r.Winner == 0) comboWin[combo]++;
                    }
                }
                float pct = decided > 0 ? winA * 100f / decided : -1f;
                sum += pct; n++;
                if (MathF.Abs(pct - 50f) > MathF.Abs(worst - 50f)) { worst = pct; worstName = TankStats.Get(k).Name; }
                Console.WriteLine($"    {TankStats.Get(k).Name,-12} {pct,5:F0}%");
            }
            float mean = n > 0 ? sum / n : -1f;
            float se = 100f / MathF.Sqrt(MirrorGateN * (float)n) / 2f;
            Console.WriteLine($"    12종 평균 {mean:F1}%  (표준오차 ±{se:F1}%p)  최대 이탈 {worstName} {worst:F0}%");
            // 조합별 A팀 승률. 스폰·선공이 대칭이라면 네 칸이 모두 50% 근처여야 한다.
            string[] comboName = { "스폰기본·A선공", "스폰기본·B선공", "스폰교환·A선공", "스폰교환·B선공" };
            Console.Write("    [분해] ");
            for (int c = 0; c < 4; c++)
                Console.Write($"{comboName[c]} {(comboDec[c] > 0 ? comboWin[c] * 100f / comboDec[c] : -1f),5:F1}%   ");
            Console.WriteLine();
            // 선공만 묶어 본다 — §2-9-8 에서 선공 가치가 중급 ±10%p 로 측정됐다. 그 값이 그대로 나오면 정상이다.
            int aFirstW = comboWin[0] + comboWin[2], aFirstD = comboDec[0] + comboDec[2];
            int bFirstW = comboWin[1] + comboWin[3], bFirstD = comboDec[1] + comboDec[3];
            Console.WriteLine($"    [분해] A선공일 때 A승률 {(aFirstD > 0 ? aFirstW * 100f / aFirstD : -1f):F1}%   " +
                              $"B선공일 때 A승률 {(bFirstD > 0 ? bFirstW * 100f / bFirstD : -1f):F1}%   " +
                              $"→ 선공 가치 {(aFirstD > 0 && bFirstD > 0 ? aFirstW * 100f / aFirstD - bFirstW * 100f / bFirstD : 0f):F1}%p");
            if (MathF.Abs(mean - 50f) > 3f * se)
            { Console.WriteLine("    ❌ 미러 평균이 50% 에서 유의하게 벗어났다 — 하네스가 한쪽으로 기울어 있다"); RcFail = true; }
            else Console.WriteLine("    ✅ 미러 평균이 50% 안(3σ) — 매치업표를 탱크 비교로 읽어도 된다");
        }

        if (OnlyStage == "mirror")
        {
            BurnWiringGate();
            if (RcFail) { Console.WriteLine("❌ 미러 게이트 실패"); Environment.Exit(1); }
            Console.WriteLine("=== 미러 게이트만 실행 완료 ==="); Environment.Exit(0);
        }

        // [6-3] 난이도 사다리가 **진짜 사다리인가**. 지금까지 난이도는 같은 값끼리만 재서(AI vs AI 대칭)
        //       "오차가 크면 판이 길어진다"만 알았지 **강한 난이도가 약한 난이도를 실제로 이기는지**는 잰 적이 없다.
        //       같은 난이도끼리는 당연히 50% 라 사다리 여부를 증명하지 못한다 — 서로 다른 난이도를 붙여야 한다.
        //       읽는 법: 행(A)이 더 강한 난이도면 승률이 50% 를 **뚜렷하게** 넘어야 사다리다. 안 넘으면 손잡이가 죽은 것이다.
        Console.WriteLine("");
        Console.WriteLine($"[6-3] 난이도 사다리 검증 — 맵={MapName}, 캐롯 자기전, 각 20판, A팀 승률");
        {
            (string name, float err)[] tiers =
            {
                ("대조 0%",      0f),          // 네거티브 컨트롤: 오차가 없으면 난수가 조준에 안 쓰인다.
                                               //   여기서도 미러가 50% 를 벗어나면 원인은 난수가 아니라 구조다.
                ("에이스 0.75%", AiGunner.ErrorAce),
                ("상급 1.5%",   AiGunner.ErrorExpert),
                ("중급 2.5%",   AiGunner.ErrorNormal),
                ("초급 4.0%",   AiGunner.ErrorNovice),
            };
            Console.Write($"    {"A vs B",-12}");
            foreach (var t in tiers) Console.Write($"{t.name,12}");
            Console.WriteLine();
            bool ladderOk = true;
            var diag = new float[tiers.Length];
            for (int a = 0; a < tiers.Length; a++)
            {
                Console.Write($"    {tiers[a].name,-12}");
                for (int b = 0; b < tiers.Length; b++)
                {
                    int winA = 0, decided = 0;
                    for (uint m = 0; m < 20u; m++)
                    {
                        // 선공·스폰 둘 다 교차해 진영 효과를 뺀다([6-2] 교훈) — 남는 차이가 난이도뿐이어야 한다.
                        var r = RunMatch(2000 + m * 77, tiers[a].err, MaxHp, 400, SuddenDeathTurn, BlastRadius,
                                         TankKind.Carrot, TankKind.Carrot, Wx,
                                         (int)((m >> 1) & 1), (m & 1) == 1, true, tiers[b].err);
                        if (r.Winner >= 0) { decided++; if (r.Winner == 0) winA++; }
                    }
                    float pct = decided > 0 ? winA * 100f / decided : -1f;
                    Console.Write($"{pct,11:F0}%");
                    // 한 칸 위(더 강한 난이도)가 아래를 못 이기면 사다리가 아니다. 인접 칸만 본다(a<b = A 가 더 강함).
                    if (b == a + 1 && pct <= 55f) ladderOk = false;
                    if (a == b) diag[a] = pct;
                }
                Console.WriteLine();
            }
            Console.WriteLine(ladderOk
                ? "    ✅ 인접 난이도끼리 강한 쪽이 이긴다 — 사다리가 성립한다"
                : "    ❌ 인접 난이도 차이가 안 난다 — 난이도 손잡이가 죽어 있다(§2-5 의 '창이 0.7%p' 문제)");

            // 대각선(같은 난이도끼리)은 50% 여야 한다 — 벗어나면 진영 효과가 남은 것이다.
            // 원인을 바로 가르기 위해 선공만 따로 떼어 다시 잰다(§2-9-6 과 같은 판별).
            Console.WriteLine("    — 대각선(같은 난이도) 이 50% 를 벗어난 칸의 선공별 분해 —");
            for (int a = 0; a < tiers.Length; a++)
            {
                _ = diag[a];   // 전 티어를 다 잰다 — n=20 으로는 "편향"과 "시드 운"을 못 가른다는 걸 실측으로 배웠다.
                // ⚠️ 표본을 20판에서 **120판**으로 올리고 시드 계열도 바꾼다(2000→5000 계열).
                //    20판 ±11%p 로는 "진짜 편향"과 "이 시드 20개가 우연히 그랬다"를 가를 수 없다 —
                //    실제로 저오차 구간은 판이 거의 결정론적이라 시드에 심하게 끌려간다.
                for (int ft = 0; ft < 2; ft++)
                    for (int sp = 0; sp < 2; sp++)
                    {
                        int winA = 0, decided = 0, turns = 0;
                        const uint N = 120u;
                        for (uint m = 0; m < N; m++)
                        {
                            var r = RunMatch(5000 + m * 131, tiers[a].err, MaxHp, 400, SuddenDeathTurn, BlastRadius,
                                             TankKind.Carrot, TankKind.Carrot, Wx, ft, sp == 1, true, tiers[a].err);
                            turns += r.Turns;
                            if (r.Winner >= 0) { decided++; if (r.Winner == 0) winA++; }
                        }
                        float pct = decided > 0 ? winA * 100f / decided : -1f;
                        Console.WriteLine($"      {tiers[a].name,-12} {(ft == 0 ? "A선공" : "B선공")} {(sp == 1 ? "스폰교환" : "스폰기본")}  A승 {pct,5:F0}%  (n={N}, ±{100f / MathF.Sqrt(N) / 2f:F0}%p, 평균 {turns / (float)N:F0}턴)");
                    }
            }
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
        // 미러 게이트가 떨어지면 종료 코드로 알린다 — 출력만 빨갛고 verify.sh 는 통과하던 구멍을 막는다.
        BurnWiringGate();
        if (RcFail) { Console.WriteLine("❌ 미러 게이트 실패 — 위 [6-2-1] 참조"); Environment.Exit(1); }
    }

    /// <summary>[6] 매치업. only 가 있으면 그 기종 행만(단일 변수 실험용).</summary>
    static void Matchup(TankKind? only, uint perCell)
    {
        int totalDecided = 0, totalCells = 0;
        Console.WriteLine($"[6] 포트리스 12종 매치업 승률 (A팀 기준, 각 {perCell}판, 오차 2.5%, 서든데스 6R)");
        var kinds = TankStats.Selectable();      // 슈퍼탱크는 원작대로 선택 불가라 빠진다
        int N = kinds.Length;
        var win = new float[N, N];
        var spUse = new float[N];
        var turnAvg = new float[N];
        var fallAvg = new float[N];   // 네거티브 컨트롤: 0 이면 §28 보상이 또 죽어 있다는 뜻
        var ssAvg = new float[N]; var ultAvg = new float[N]; var dotAvg = new float[N]; var mineAvg = new float[N]; var satAvg = new float[N];
        var itemAvg = new float[N];   // 아이템 사용/판 — 네거티브 컨트롤(0 이면 아이템이 죽은 것)
        var supplyAvg = new float[N]; // 보급 줍기/판 — 네거티브 컨트롤(0 이면 헬기가 장식인 것)
        var impAvg = new float[N];
        var ampAvg = new float[N];    // 증폭벽 통과/판, 회오리 휘말림/판 — 기후(§2-9-15) 네거티브 컨트롤
        var torAvg = new float[N];    // 방해탄 적중/판 — 각도·파워 고정만(화면 방해는 AI 상대로 안 쓴다, §2-9-14)    // 궁극기 발동/판 — 네거티브 컨트롤(0 이면 궁극기가 죽은 것)
        float supDropAll = 0f, supPickAll = 0f, supDestAll = 0f; int supCells = 0;
        float seekAll = 0f, seekBlkAll = 0f, pickAall = 0f, pickBall = 0f, seekAall = 0f, seekBall = 0f;   // 투하/획득/파괴 대조
        var whyAll = new float[7]; float whyCells = 0f;   // AI 이동 사유(§AiMover) 네거티브 컨트롤
        var hitPct = new float[N]; var dmgPerShot = new float[N];   // A팀 명중률 · 명중당 폭발 피해

        // 단일 행 모드: 그 기종이 A 팀인 행만 돈다(12셀). 나머지 행은 계산도 출력도 안 한다.
        int[] rows;
        if (only.HasValue) rows = new[] { Array.IndexOf(kinds, only.Value) };
        else { rows = new int[N]; for (int i = 0; i < N; i++) rows[i] = i; }
        if (only.HasValue && rows[0] < 0) { Console.WriteLine($"    ❌ {only.Value} 는 선택 가능 기종이 아니다"); return; }
        foreach (int ai in rows)
        {
            int rowMinDecided = int.MaxValue; string rowMinCell = "-";
            float spSum = 0f, tSum = 0f, fSum = 0f, ssSum = 0f, ultSum = 0f, dotSum = 0f, mineSum = 0f, satSum = 0f, itemSum = 0f, supSum = 0f, impSum = 0f, ampSum = 0f, torSum = 0f;
            long shotsA = 0, hitsA = 0, blastA = 0;
            for (int bi = 0; bi < N; bi++)
            {
                int winA = 0, decided = 0, turns = 0, spec = 0, fall = 0, ssN = 0, ultN = 0, dotN = 0, mineN = 0, satN = 0, itemN = 0, supN = 0, supDropN = 0, supDestN = 0, seekN = 0, seekBlkN = 0, impN = 0, ampN = 0, torN = 0, pkA = 0, pkB = 0, skA = 0, skB = 0;
                var whyN = new int[7];
                for (uint m = 0; m < perCell; m++)
                {
                    // [6-1] 실측: B 스폰 자리가 지형상 유리(+20%p). 홀짝 판마다 자리를 바꿔 탱크 비교에서 상쇄한다.
                    // [6-2] 실측(2026-09-16): 실제 게임(§52)은 팀A가 등록순 동률에서 항상 먼저 쏜다 —
                    //   설계상 의도된 동작(플레이어=A팀 고정 선공)이라 게임 코드는 안 건드린다. 하지만 이
                    //   "미러=50% 여야 정상"이라는 매치업표의 전제는 **탱크 수치 대칭**만 봐야 하는데,
                    //   선공권이 항상 A로 고정돼 있으면 판이 짧은 기종(레이저 6턴 등)일수록 선공 승률 편중이
                    //   그대로 새어 들어와 탱크 자체의 좌우 대칭성과 뒤섞인다. 그래서 스폰 교환과 별도로
                    //   선공도 2판마다 교차해 "탱크 수치만의" 대칭성을 분리해 잰다(선공 자체의 효과는 [6-2]가 따로 잰다).
                    var r = RunMatch(1000 + m * 77, 0.025f, MaxHp, 400, SuddenDeathTurn, BlastRadius, kinds[ai], kinds[bi],
                                     Wx, (int)((m >> 1) & 1), (m & 1) == 1, true);
                    turns += r.Turns; spec += r.SpecialUsed; fall += r.FallDealt;
                    ssN += r.SsUsed; ultN += r.UltUsed; dotN += r.DotDealt; mineN += r.MineDealt; satN += r.SatelliteShots; itemN += r.ItemsUsed; supN += r.SupplyPicked;
                    impN += r.ImpairHitA; ampN += r.AmpShots; torN += r.TornadoShots;
                    supDropN += r.SupplyDropped; supDestN += r.SupplyDestroyed; seekN += r.SeekTurns; seekBlkN += r.SeekBlocked; pkA += r.PickedA; pkB += r.PickedB; skA += r.SeekA; skB += r.SeekB;
                    if (r.MoveWhyA != null) for (int w = 0; w < whyN.Length; w++) whyN[w] += r.MoveWhyA[w];
                    shotsA += r.ShotsA; hitsA += r.HitsA; blastA += r.BlastDealtA;
                    if (r.Winner >= 0) { decided++; if (r.Winner == 0) winA++; }
                }
                win[ai, bi] = decided > 0 ? winA * 100f / decided : -1f;
                // ⚠️ 결판난 판이 적으면 0%/100% 가 쉽게 나온다 — 그건 밸런스가 아니라 **표본이 무너진 것**이다.
                //    행별로 가장 적게 결판난 칸을 기억해 표 아래에 띄운다(진단이 없으면 또 오진한다).
                if (decided < rowMinDecided) { rowMinDecided = decided; rowMinCell = $"{TankStats.Get(kinds[ai]).Name} vs {TankStats.Get(kinds[bi]).Name}"; }
                totalDecided += decided; totalCells++;
                spSum += spec / (float)perCell; tSum += turns / (float)perCell; fSum += fall / (float)perCell;
                ssSum += ssN / (float)perCell; ultSum += ultN / (float)perCell; dotSum += dotN / (float)perCell; mineSum += mineN / (float)perCell; satSum += satN / (float)perCell;
                itemSum += itemN / (float)perCell; supSum += supN / (float)perCell;
                impSum += impN / (float)perCell; ampSum += ampN / (float)perCell; torSum += torN / (float)perCell;
                supDropAll += supDropN / (float)perCell; supPickAll += supN / (float)perCell; supDestAll += supDestN / (float)perCell; supCells++;
                seekAll += seekN / (float)perCell; seekBlkAll += seekBlkN / (float)perCell;
                for (int w = 0; w < whyAll.Length; w++) whyAll[w] += whyN[w] / (float)perCell;
                whyCells++;
                pickAall += pkA / (float)perCell; pickBall += pkB / (float)perCell; seekAall += skA / (float)perCell; seekBall += skB / (float)perCell;
            }
            spUse[ai] = spSum / N; turnAvg[ai] = tSum / N; fallAvg[ai] = fSum / N;
            hitPct[ai] = shotsA > 0 ? hitsA * 100f / shotsA : 0f; dmgPerShot[ai] = hitsA > 0 ? blastA / (float)hitsA : 0f;
            ssAvg[ai] = ssSum / N; ultAvg[ai] = ultSum / N; dotAvg[ai] = dotSum / N; mineAvg[ai] = mineSum / N; satAvg[ai] = satSum / N;
            itemAvg[ai] = itemSum / N; supplyAvg[ai] = supSum / N; impAvg[ai] = impSum / N; ampAvg[ai] = ampSum / N; torAvg[ai] = torSum / N;
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
        Console.Write($"    {"탱크",-14}{"계열",-6}{"체력",5}{"사거리",7}{"폭발",6}{"굴착",6}{"직격",6}{"2번탄/판",9}{"SS/판",6}{"궁극/판",8}{"평균턴",7}{"명중%",6}{"피해/명중",9}{"낙하",6}{"지속",6}{"설치물",7}{"위성",5}{"아이템",7}{"보급",6}{"방해",6}{"증폭",6}{"회오리",7}");
        Console.WriteLine("");
        if (totalCells > 0)
        {
            float avgDecided = totalDecided / (float)totalCells;
            Console.WriteLine($"    [표본] 칸당 평균 결판 {avgDecided:F1}/{perCell}판" +
                              (avgDecided < perCell * 0.6f
                                  ? $"   ⚠️ 무승부가 많다 — 0%/100% 는 밸런스가 아니라 표본 붕괴다"
                                  : "   (충분)"));
        }
        foreach (int i in rows)
        {
            var t = Stats(kinds[i], ShellKind.Normal, 1f, Wx);   // 단일 행 실험의 굴착 배율이 표에도 보이게
            // ⚠️ 특수탄/판이 0 에 가까우면 밸런스가 아니라 **선택지가 죽어 있다**는 신호다.
            string dead = spUse[i] < 0.5f ? "  ❌사장" : "";
            Console.WriteLine($"    {t.Name,-14}{TankStats.EraName(t.Era),-6}{t.Hp,5}{t.MaxRange,6:F0}m{t.BlastRadius,5:F1}m{t.CraterRadius,5:F1}m{t.DirectDamage,6:F0}{spUse[i],9:F1}{ssAvg[i],6:F1}{ultAvg[i],8:F2}{turnAvg[i],7:F0}{hitPct[i],6:F0}{dmgPerShot[i],9:F0}{fallAvg[i],6:F0}{dotAvg[i],6:F0}{mineAvg[i],7:F0}{satAvg[i],5:F1}{itemAvg[i],7:F1}{supplyAvg[i],6:F1}{impAvg[i],6:F1}{ampAvg[i],6:F1}{torAvg[i],7:F1}{dead}");
        }
        if (supCells > 0)
            Console.WriteLine($"    헬기 보급(§2-9-11) 한 판 평균 — 투하 {supDropAll / supCells:F2}  획득 {supPickAll / supCells:F2}  파괴 {supDestAll / supCells:F2}" +
                              (supPickAll / supCells < 0.3f ? "   ⚠️ 획득이 거의 0 — 헬기가 장식이다" : ""));
        if (supCells > 0)
            Console.WriteLine($"      (진단) 주우러 간 턴 {seekAll / supCells:F2}  지형에 막힘 {seekBlkAll / supCells:F2}"
                              + $"  |  팀별 획득 A {pickAall / supCells:F2} : B {pickBall / supCells:F2}"
                              + $"  주우러 간 턴 A {seekAall / supCells:F2} : B {seekBall / supCells:F2}");
        // AI 이동(§AiMover) 네거티브 컨트롤 — **게임과 하네스가 같은 함수를 쓴다**는 걸 숫자로 확인하는 자리다.
        // 구덩이 탈출이 0 이면 "자기가 판 구덩이에 갇힌 AI" 가 돌아온 것이고, 보급이 0 이면 헬기가 다시 장식이다.
        if (whyCells > 0)
        {
            string[] wn = { "안움직임", "속박", "구덩이탈출", "장판탈출", "보급", "사거리", "자리옮김" };
            var sb = new System.Text.StringBuilder("    AI 이동 사유(A팀, 한 판 평균) —");
            for (int w = 1; w < wn.Length; w++) sb.Append($"  {wn[w]} {whyAll[w] / whyCells:F2}");
            Console.WriteLine(sb.ToString());
            if (whyAll[2] / whyCells < 0.01f) Console.WriteLine("      ⚠️ 구덩이 탈출이 0 — AI 가 자기가 판 구덩이에 갇혀 있다");
        }
        Console.WriteLine("    ※ 미러(대각선)가 50%에서 크게 벗어나면 진영 유불리(스폰·지형·선공)가 섞인 것이다.");
        Console.WriteLine("    ※ 명중% = A팀 사격 중 적에게 폭발 피해를 준 비율, 피해/명중 = 그 사격 한 번의 폭발 피해 합(방어 적용 후, 낙하·지속·설치물 제외).");
        Console.WriteLine("    ※ 낙하 = 지형을 끊어 적에게 입힌 한 판 평균 피해(§28). 지속 = 독·화상 tick, 설치물 = 지뢰(마인랜더)+지속불(캐터펄트)+독구름(듀크) 장판 피해, 위성 = 위성탄 발수, 아이템 = 한 판 평균 아이템 사용 수(§2-9-10), 보급 = 한 판 평균 헬기 상자 획득 수(§2-9-11). 해당 탱크에서 0 이면 그 시스템이 죽은 것.");
        Console.WriteLine("    ※ ⬆/⬇ = 평균 승률이 62% 이상 / 38% 이하 — 선택지가 아니라 정답 또는 함정이라는 뜻.");
    }
}
