using UnityEngine;

/// <summary>
/// 블렌더에서 렌더한 플레이스홀더 스프라이트를 로드해 한 장의 아틀라스로 합친다.
/// 아틀라스 + 단일 머티리얼이어야 SpriteRenderer가 배칭된다 — W1 성능의 전제.
/// </summary>
public class SpriteBank
{
    public static SpriteBank Cached;

    public Material Mat;
    public Sprite Player, Summon, Projectile, Ground;
    /// <summary>보스 실루엣 4종(§0-B). 층·계열로 골라 크기·색을 변주해 쓴다.</summary>
    public Sprite[] Boss;
    /// <summary>체력바 등 단색 사각형용 흰 1유닛 스프라이트. 색은 SpriteRenderer.color로 준다.</summary>
    public Sprite White;
    Sprite[] _mobs;

    public Sprite Mob(int i) => _mobs[Mathf.Clamp(i, 0, _mobs.Length - 1)];

    // ── 오너 픽셀아트 (2026-08-13) ────────────────────────
    // 직업 4종 × 프레임 4종. 블렌더 플레이스홀더와 달리 **실제 아트**다.
    // ⚠️ 파일이 Resources 밖(_Game/Art/Sprites)에 있으면 Resources.Load가 못 찾아
    //    화면엔 플레이스홀더만 나온다 — 실제로 그 상태로 W1~W3을 돌렸었다.
    /// <summary>
    /// 아트 세트. ⚠️ **역할(Role)이 아니라 직업 단위**다 — 역할로 고르면 딜러 둘
    /// (검사·마법사)이 **같은 그림**이 된다(오너 지적 2026-08-15 「딜러 마법사 검사
    /// 같은 모양인데」). 순서는 `JOB_DIRS`와 인덱스로 짝지어지므로 **바꾸지 마라.**
    /// </summary>
    public enum Job { Tank = 0, Dps = 1, Healer = 2, Buffer = 3, Mage = 4 }

    /// <summary>
    /// 프레임 순서는 JOB_FRAMES와 **반드시 같아야 한다** — 인덱스로 짝지어 로드한다.
    /// 대시 4프레임은 §5 이동기(무적 0.3초)용이고, Invuln은 무적 구간 표시다.
    /// </summary>
    /// <summary>
    /// ⚠️ 순서는 `JOB_FRAMES`와 **반드시 같다** — 인덱스로 짝지어 로드한다.
    ///
    /// 걷기·공격이 2장뿐이었다(WalkA/B, AttackA/B). 오너가 준 원본 시트는 동작마다
    /// **6프레임**인데 계약이 2장이라 나머지 4장을 통째로 버리고 있었다
    /// (오너 지적 2026-08-18 "캐릭터 스프라이트 애니메이션 제대로 적용 안 된 거 같음").
    /// 6장으로 늘린다. 옛 자산처럼 뒤 프레임이 없으면 로더가 건너뛰고 `CharAnim`이
    /// 있는 장 안에서만 돈다 — 그래서 전직 13장 자산도 그대로 동작한다.
    /// </summary>
    public enum Frame
    {
        // 2026-08-21 6→8칸 확장(오너 "모션마다 이미지 수가 왜 달라") — 시트가 전
        // 모션 8장 균일이라 2장씩 버리고 있었다. 값은 JOB_FRAMES 순서와 같아야 한다.
        Idle0 = 0, Idle1 = 1, Idle2 = 2, Idle3 = 3, Idle4 = 4, Idle5 = 5, Idle6 = 6, Idle7 = 7,
        Walk0 = 8, Walk1 = 9, Walk2 = 10, Walk3 = 11, Walk4 = 12, Walk5 = 13, Walk6 = 14, Walk7 = 15,
        Atk0 = 16, Atk1 = 17, Atk2 = 18, Atk3 = 19, Atk4 = 20, Atk5 = 21, Atk6 = 22, Atk7 = 23,
        Sp0 = 24, Sp1 = 25, Sp2 = 26, Sp3 = 27, Sp4 = 28, Sp5 = 29, Sp6 = 30, Sp7 = 31,
        Hurt = 32,
        Death0 = 33, Death1 = 34, Death2 = 35, Death3 = 36,
        Death4 = 37, Death5 = 38, Death6 = 39, Death7 = 40,
        DashA = 41, DashB = 42, DashC = 43, DashD = 44, Invuln = 45,
        Death = Death0,   // 옛 이름 — 단일 장 호출부용(시체는 마지막 장이 아니라 첫 장을 든다)
        Special = Sp0,
        Idle = Idle0,
        // 옛 이름 — 호출부가 아직 쓴다. 6프레임의 첫 두 장을 가리킨다.
        WalkA = Walk0, WalkB = Walk1, AttackA = Atk0, AttackB = Atk1,
    }

    // ── 오너 몬스터 아트 (2026-08-13) ────────────────────
    // 종이 늘면 MOB_DIRS에 폴더명을 추가한다. 아틀라스·피벗·애니메이션은 배열 길이를
    // 따라가므로 다른 곳은 손댈 필요가 없다(2026-08-15에 1종 → 5종으로 늘리며 확인).
    public enum MobFrame
    {
        Idle0 = 0, Idle1, Idle2, Idle3,
        Walk0, Walk1, Walk2, Walk3, Walk4, Walk5,
        Atk0, Atk1, Atk2, Atk3,
        Hurt0, Hurt1, Hurt2, Hurt3,
        Death0, Death1, Death2, Death3,
    }

    // 계열 순서가 곧 `MobAnim(kind, ...)`의 인덱스다 — 바꾸면 화면의 몹 그림이 통째로 어긋난다.
    // 0번(mob01)은 기존 기본형이라 자리를 지킨다. 1~4는 §10-2 계열 구분용 P2 아트.
    public const int MobKindBasic = 0, MobKindChaser = 1, MobKindCharger = 2,
                     MobKindRanged = 3, MobKindSwarmer = 4;
    // 5번부터는 힉스필드로 만든 인간형 할로우 나이트 그림이다(오너 지시 2026-08-18
    // "지금까지 힉스필드에서 만들어진 캐릭터는 몬스터에 활용하자"). 오너가 준 픽셀아트가
    // 기본 5직업을 맡으면서 이 10종이 갈 곳을 잃었는데, 버리지 않고 몹 계열로 돌린다.
    // ⚠️ **뒤에만 덧붙인다.** 0~4는 기존 인덱스라 하나라도 밀면 화면의 몹이 통째로 어긋난다.
    public const int MobKindGuardian = 5, MobKindBerserker = 6, MobKindSwordsman = 7,
                     MobKindArcher = 8, MobKindSummoner = 9, MobKindPriest = 10,
                     MobKindDruid = 11, MobKindBard = 12, MobKindShaman = 13,
                     MobKindElemental = 14;
    static readonly string[] MOB_DIRS =
    {
        "mob01", "mob_chaser", "mob_charger", "mob_ranged", "mob_swarmer",
        "mob_guardian", "mob_berserker", "mob_swordsman", "mob_archer", "mob_summoner",
        "mob_priest", "mob_druid", "mob_bard", "mob_shaman", "mob_elemental",
    };
    static readonly string[] MOB_FRAMES =
    {
        "idle_00", "idle_01", "idle_02", "idle_03",
        "walk_00", "walk_01", "walk_02", "walk_03", "walk_04", "walk_05",
        "attack_00", "attack_01", "attack_02", "attack_03",
        "hurt_00", "hurt_01", "hurt_02", "hurt_03",
        "death_00", "death_01", "death_02", "death_03",
    };

    Sprite[][] _mobAnim;   // [몹종][프레임]

    /// <summary>몹 애니메이션. 아트가 없으면 블렌더 플레이스홀더로 폴백한다.</summary>
    public Sprite MobAnim(int kind, Motion m, float t)
    {
        if (_mobAnim == null || _mobAnim.Length == 0) return Mob(kind);
        var row = _mobAnim[kind % _mobAnim.Length];
        int i;
        switch (m)
        {
            case Motion.Walk: i = (int)MobFrame.Walk0 + (int)(t / (WalkCycle / 6f)) % 6; break;
            case Motion.Attack: i = (int)MobFrame.Atk0 + Mathf.Clamp((int)(t / (0.50f / 4f)), 0, 3); break;
            case Motion.Hurt: i = (int)MobFrame.Hurt1; break;   // 붉게 물든 프레임이 피격 표시다
            case Motion.Death: i = (int)MobFrame.Death0 + Mathf.Clamp((int)(t / 0.1f), 0, 3); break;
            default: i = (int)MobFrame.Idle0 + (int)(t / (IdleCycle / 4f)) % 4; break;
        }
        return row[Mathf.Clamp(i, 0, row.Length - 1)] ?? Mob(kind);
    }

    static readonly string[] JOB_DIRS = { "tank", "dps", "healer", "buffer", "mage" };
    /// <summary>1차 전직 전용 폴더. 아틀라스 밖 솔로 텍스처 — 장수가 많아  squish 방지.</summary>
    public static readonly string[] ExtraDirs =
    {
        "guardian", "berserker", "swordsman", "archer", "summoner",
        "priest", "druid", "bard", "shaman", "elemental",
    };
    Sprite[][] _extra;
    static readonly string[] JOB_FRAMES =
    {
        "idle_00", "idle_01", "idle_02", "idle_03", "idle_04", "idle_05", "idle_06", "idle_07",
        "walk_00", "walk_01", "walk_02", "walk_03", "walk_04", "walk_05", "walk_06", "walk_07",
        "attack_00", "attack_01", "attack_02", "attack_03", "attack_04", "attack_05", "attack_06", "attack_07",
        "special_00", "special_01", "special_02", "special_03", "special_04", "special_05", "special_06", "special_07",
        "hurt_00",
        "death_00", "death_01", "death_02", "death_03",
        "death_04", "death_05", "death_06", "death_07",
        "dash_00", "dash_01", "dash_02", "dash_03", "invuln_00",
    };

    Sprite[][] _job;   // [직업][프레임]

    /// <summary>직업·프레임별 픽셀아트. 로드 실패 시 플레이스홀더로 폴백한다.</summary>
    /// <summary>아트가 아직 없는 직업. 그 자리에 붉은 사각형을 그리지 않고 딜러 아트로 대신한다.</summary>
    bool[] _jobMissing;

    public Sprite Char(Job j, Frame f = Frame.Idle)
    {
        int idx = (int)j;
        // ⚠️ 아트가 없는 직업을 그대로 그리면 **붉은 단색 사각형**이 된다(로드 실패 대체물).
        //    직업 단위 아트로 바꾸면서 `mage` 폴더가 아직 비어 있을 수 있는데, 그때
        //    화면이 더 나빠지면 안 된다 — 도착 전까지는 딜러 아트로 버틴다.
        //    아트가 들어오면 이 분기는 저절로 꺼진다(경고는 로드 때 한 번 남는다).
        if (_jobMissing != null && idx < _jobMissing.Length && _jobMissing[idx]) idx = (int)Job.Dps;
        var row = _job[idx];
        return row[(int)f] ?? row[0] ?? Player;
    }

    /// <summary>캐릭터가 지금 무엇을 하고 있는가. 우선순위가 높은 것이 이긴다.</summary>
    public enum Motion { Idle, Walk, Attack, Special, Hurt, Death, Dash }

    /// <summary>
    /// 상태와 경과 시간으로 프레임을 고른다.
    /// 호출부는 "지금 무엇을 하는가"만 알면 되고 프레임 번호를 몰라도 된다 —
    /// 프레임 구성이 바뀌어도 호출부를 안 고치게 하려는 것이다.
    /// </summary>
    /// <summary>이 직업이 실제로 가진 연속 프레임 수를 센다(뒤가 비면 거기서 끊는다).</summary>
    int Have(Job j, Frame first, int max)
    {
        int idx = (int)j;
        if (_jobMissing != null && idx < _jobMissing.Length && _jobMissing[idx]) idx = (int)Job.Dps;
        var row = _job[idx];
        int n = 0;
        for (int k = 0; k < max; k++)
        {
            int f = (int)first + k;
            if (f >= row.Length || row[f] == null) break;
            n++;
        }
        return Mathf.Max(1, n);
    }

    // 한 동작이 도는 데 걸리는 시간(초). 오너 지적 2026-08-18 "스프라이트 애니 너무
    // 빨라서 안 보인다" — 걷기 0.72s(6장이면 8fps)·공격 0.06s/장(16fps)은 눈이 못 따라간다.
    // ⚠️ 공격은 **동작 길이(W3Party.AttackT)보다 짧아야** 전 프레임이 보인다.
    //    옛 값은 6×0.06=0.36s인데 AttackT가 0.26s라 뒤 2장이 화면에 뜨지도 못했다.
    public const float WalkCycle = 1.10f;   // 8장 → 0.14s/장 (사이클 길이는 유지)
    public const float IdleCycle = 1.50f;   // 8장 → 0.19s/장 (사이클 길이는 유지)
    public const float AtkFrame = 0.0625f;  // 8장 → 총 0.50s (오너 지정 총길이 유지)
    public const float SpFrame = 0.125f;    // 8장 → 총 1.00s (오너 지정 총길이 유지)
    public const float DashFrame = 0.06f;   // 대시는 짧아야 이동기로 읽힌다 — 유지
    public const float DeathFrame = 0.1f;   // 8장 → 0.8s 쓰러진 뒤 마지막 장(시체) 유지 — 몹 death와 같은 박자

    public Sprite CharAnim(Job j, Motion m, float t)
    {
        switch (m)
        {
            // 걷기·공격은 **가진 만큼** 돈다. 6장이면 6장, 옛 자산처럼 2장이면 2장 —
            // 프레임 수를 코드에 못 박으면 자산이 바뀔 때마다 여기도 고쳐야 한다.
            case Motion.Walk:
            {
                int n = Have(j, Frame.Walk0, 8);
                return Char(j, (Frame)((int)Frame.Walk0 + (int)(t / (WalkCycle / n)) % n));
            }
            case Motion.Attack:
            {
                int n = Have(j, Frame.Atk0, 8);
                // 공격은 한 번 재생하고 마지막 장에서 멈춘다(루프하면 계속 휘두른다)
                return Char(j, (Frame)((int)Frame.Atk0 + Mathf.Clamp((int)(t / AtkFrame), 0, n - 1)));
            }
            case Motion.Dash:
                return Char(j, (Frame)((int)Frame.DashA + Mathf.Clamp((int)(t / DashFrame), 0, 3)));
            case Motion.Special:
            {
                int n = Have(j, Frame.Sp0, 8);
                return Char(j, (Frame)((int)Frame.Sp0 + Mathf.Clamp((int)(t / SpFrame), 0, n - 1)));
            }
            case Motion.Hurt: return Char(j, Frame.Hurt);
            case Motion.Death:
            {
                // 쓰러지는 8장을 한 번 재생하고 마지막 장(시체)에서 멈춘다 — 몹 death와 같은 구조.
                int n = Have(j, Frame.Death0, 8);
                return Char(j, (Frame)((int)Frame.Death0 + Mathf.Clamp((int)(t / DeathFrame), 0, n - 1)));
            }
            default:
            {
                int n = Have(j, Frame.Idle0, 8);
                return Char(j, (Frame)((int)Frame.Idle0 + (int)(t / (IdleCycle / n)) % n));
            }
        }
    }

    static Job JobFromDir(string dir) => dir switch
    {
        "guardian" or "berserker" or "tank" => Job.Tank,
        "priest" or "druid" or "healer" => Job.Healer,
        "bard" or "shaman" or "elemental" or "buffer" => Job.Buffer,
        "summoner" or "mage" => Job.Mage,
        _ => Job.Dps,
    };

    int ExtraIndex(string dir)
    {
        if (string.IsNullOrEmpty(dir) || _extra == null) return -1;
        for (int i = 0; i < ExtraDirs.Length; i++)
            if (ExtraDirs[i] == dir) return i;
        return -1;
    }

    /// <summary>전직 폴더 애니. 걷기 장이 없으면 기본 5직업으로 돌아간다.</summary>
    public Sprite CharDir(string dir, Frame f = Frame.Idle)
    {
        int e = ExtraIndex(dir);
        if (e < 0 || _extra[e] == null)
            return Char(JobFromDir(dir), f);
        var row = _extra[e];
        return row[(int)f] ?? row[0] ?? Char(JobFromDir(dir), f);
    }

    /// <summary>전직 폴더가 실제로 가진 연속 프레임 수.</summary>
    int HaveDir(int e, Frame first, int max)
    {
        var row = _extra[e];
        int n = 0;
        for (int k = 0; k < max; k++)
        {
            int f = (int)first + k;
            if (f >= row.Length || row[f] == null) break;
            n++;
        }
        return Mathf.Max(1, n);
    }

    public Sprite CharAnimDir(string dir, Motion m, float t)
    {
        int e = ExtraIndex(dir);
        if (e < 0 || _extra[e] == null || _extra[e][(int)Frame.WalkA] == null)
            return CharAnim(JobFromDir(dir), m, t);
        switch (m)
        {
            // 전직 폴더도 가진 만큼 돈다 — 지금 자산은 걷기 2장뿐이라 2장으로 돌지만,
            // 6장짜리가 들어오면 코드를 안 고쳐도 6장으로 돈다.
            case Motion.Walk:
            {
                int n = HaveDir(e, Frame.Walk0, 8);
                return CharDir(dir, (Frame)((int)Frame.Walk0 + (int)(t / (WalkCycle / n)) % n));
            }
            case Motion.Attack:
            {
                int n = HaveDir(e, Frame.Atk0, 8);
                return CharDir(dir, (Frame)((int)Frame.Atk0 + Mathf.Clamp((int)(t / AtkFrame), 0, n - 1)));
            }
            case Motion.Dash:
                return CharDir(dir, (Frame)((int)Frame.DashA + Mathf.Clamp((int)(t / DashFrame), 0, 3)));
            case Motion.Special:
            {
                int n = HaveDir(e, Frame.Sp0, 8);
                return CharDir(dir, (Frame)((int)Frame.Sp0 + Mathf.Clamp((int)(t / SpFrame), 0, n - 1)));
            }
            case Motion.Hurt: return CharDir(dir, Frame.Hurt);
            case Motion.Death:
            {
                int n = HaveDir(e, Frame.Death0, 8);
                return CharDir(dir, (Frame)((int)Frame.Death0 + Mathf.Clamp((int)(t / DeathFrame), 0, n - 1)));
            }
            default:
            {
                int n = HaveDir(e, Frame.Idle0, 8);
                return CharDir(dir, (Frame)((int)Frame.Idle0 + (int)(t / (IdleCycle / n)) % n));
            }
        }
    }

    // Resources 아래 이름 (확장자 없음)
    static readonly string[] MOB_KEYS = { "mob_chaser_0", "mob_swarmer_0", "mob_ranged_0" };

    /// <summary>보스 실루엣 4종. 층·계열로 골라 쓴다(§0-B 변주 원칙).</summary>
    public static readonly string[] BOSS_KEYS =
        { "boss_brute", "boss_serpent", "boss_wraith", "boss_construct", "boss_saint" };
    static readonly string[] BOSS_DIRS =
        { "boss_brute", "boss_serpent", "boss_wraith", "boss_construct", "boss_saint" };
    static readonly string[] BOSS_FRAMES =
    {
        "idle_00", "idle_01", "idle_02", "idle_03",
        "attack_00", "attack_01", "attack_02", "attack_03",
        "hurt_00", "hurt_01", "hurt_02", "hurt_03",
        "death_00", "death_01", "death_02", "death_03",
    };

    Sprite[][] _bossAnim;

    /// <summary>보스 애니. 폴더가 없으면 정지 실루엣으로 폴백한다(아틀라스에 안 넣는다 — 과밀 방지).</summary>
    public Sprite BossAnim(int kind, Motion m, float t)
    {
        int k = Mathf.Abs(kind) % BOSS_KEYS.Length;
        Sprite fallback = Boss != null && k < Boss.Length ? Boss[k] : null;
        if (_bossAnim == null || k >= _bossAnim.Length || _bossAnim[k] == null)
            return fallback;
        var row = _bossAnim[k];
        int i;
        switch (m)
        {
            case Motion.Attack: i = 4 + Mathf.Clamp((int)(t / 0.10f), 0, 3); break;
            case Motion.Hurt: i = 8 + Mathf.Clamp((int)(t / 0.08f), 0, 3); break;
            case Motion.Death: i = 12 + Mathf.Clamp((int)(t / 0.12f), 0, 3); break;
            default: i = (int)(t / 0.18f) % 4; break;
        }
        return row[Mathf.Clamp(i, 0, row.Length - 1)] ?? fallback;
    }

    /// <summary>가장자리를 깎은 둥근 점. 투사체처럼 **작게 그려지는 것**에 쓴다.</summary>
    static Texture2D Dot(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        float c = (n - 1) * 0.5f, r = c + 0.2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                // 안쪽은 꽉 차고 가장자리 한 겹만 반투명 — 픽셀아트라 계단이 보이는 편이 낫다
                t.SetPixel(x, y, new Color(1f, 1f, 1f, d > 1f ? 0f : (d > 0.8f ? 0.55f : 1f)));
            }
        t.Apply();
        return t;
    }

    public static SpriteBank Load()
    {
        if (Cached != null) return Cached;
        var b = new SpriteBank();

        var baseNames = new[]
        {
            "player_knight_0", MOB_KEYS[0], MOB_KEYS[1], MOB_KEYS[2],
            "elite_healer_0", "boss_0",
            // 보스 실루엣 4종(§0-B: 보스 20종에 전용 모델을 만들지 않고 **실루엣 4종을
            // 크기·색·장식으로 변주**). 예전엔 `boss_0` 한 장뿐이라 모든 보스가 같은 그림이었다.
            "boss_brute", "boss_serpent", "boss_wraith", "boss_construct", "boss_saint"
        };

        // 캐릭터 픽셀아트를 같은 아틀라스에 싣는다 — 배칭이 깨지면 W1 성능 전제가 무너진다
        var charNames = new System.Collections.Generic.List<string>();
        foreach (var d in JOB_DIRS)
            foreach (var f in JOB_FRAMES)
                charNames.Add($"{d}/{d}_{f}");

        var mobNames = new System.Collections.Generic.List<string>();
        foreach (var d in MOB_DIRS)
            foreach (var f in MOB_FRAMES)
                mobNames.Add($"{d}/{d}_{f}");

        var srcNames = new string[baseNames.Length + charNames.Count + mobNames.Count];
        baseNames.CopyTo(srcNames, 0);
        charNames.CopyTo(srcNames, baseNames.Length);
        mobNames.CopyTo(srcNames, baseNames.Length + charNames.Count);
        int CHAR0 = baseNames.Length;                      // 캐릭터 구간 시작
        int MOB0 = baseNames.Length + charNames.Count;     // 몹 구간 시작
        int WHITE = srcNames.Length;    // 체력바용 흰 칸 — 파일이 아니라 코드로 만든다
        int DOT = WHITE + 1;            // 투사체용 둥근 점 — 아래 주석 참고

        var texes = new Texture2D[srcNames.Length + 2];
        int charMissing = 0;
        for (int i = 0; i < srcNames.Length; i++)
        {
            texes[i] = Resources.Load<Texture2D>("sprites/" + srcNames[i]);
            if (texes[i] == null)
            {
                Debug.LogWarning($"[SpriteBank] 누락: sprites/{srcNames[i]} — 단색 대체");
                texes[i] = Solid(new Color(0.8f, 0.3f, 0.3f, 1f), 64);
                if (i >= CHAR0) charMissing++;
            }
        }

        texes[WHITE] = Solid(Color.white, 8);
        // 투사체 점.
        // ⚠️ **흰 칸(1×1 단색)을 투사체로 쓰지 마라.** 키우면 언제나 **사각형**이라
        //    화면에 분홍 네모가 떠 있는 그림이 된다(실측 2026-08-15, 내가 낸 회귀).
        //    가장자리를 깎은 둥근 점이라야 "날아가는 것"으로 읽힌다.
        texes[DOT] = Dot(12);

        // 캐릭터 아트가 통째로 안 잡히면 조용히 플레이스홀더로 굴러가지 않게 막는다.
        // 실제로 파일이 Resources 밖에 있어 "적용했는데 화면엔 안 나오는" 상태로 검증을 돌렸다.
        if (charMissing == srcNames.Length - CHAR0)
            Debug.LogError("[SpriteBank] 캐릭터 픽셀아트를 하나도 못 찾았다 — " +
                           "Assets/Resources/sprites/<직업>/ 아래에 있는지 확인할 것");
        else if (charMissing > 0)
            Debug.LogWarning($"[SpriteBank] 캐릭터 프레임 {charMissing}장 누락");

        // 런타임 아틀라스 — 모든 스프라이트가 한 텍스처를 공유하게
        foreach (var t in texes)
            if (!t.isReadable)
                Debug.LogError($"[SpriteBank] 읽기 불가 텍스처: {t.name} — 아틀라스가 실패한다. " +
                               "Editor/TextureImportRules 확인할 것");

        // 4096 — 캐릭터 4직업×13프레임 + 몹 22 + 플레이스홀더가 한 장에 들어가야 한다.
        // 아틀라스가 넘치면 PackTextures가 **조용히 축소**해 스프라이트가 뭉개진다.
        //
        // ⚠️ 그 "조용히"가 이 프로젝트에서 가장 위험한 실패 방식이다(§21-1b 계열).
        //    넘쳐도 예외가 안 나고 화면도 그럴듯해서, 도트가 뭉개진 채 몇 주를 갈 수 있다.
        //    그래서 담기 전에 면적을 재서 **먼저 소리를 지른다**.
        //    실측 2026-08-14: 87장 9.77Mpx = 58%. 여유는 캐릭터 크기로 약 56장뿐이었다.
        //    → 새로 넣는 아트는 높이 128px로 정규화할 것(아트 계획 §6 C2).
        long usedPx = 0;
        foreach (var t in texes) usedPx += (long)t.width * t.height;
        const long ATLAS_PX = 4096L * 4096L;
        float fill = usedPx / (float)ATLAS_PX;
        if (fill > 0.85f)
            Debug.LogError($"[SpriteBank] 아틀라스 과밀 {fill:P0} ({texes.Length}장) — " +
                           "PackTextures가 조용히 축소해 도트가 뭉개진다. " +
                           "입력을 128px로 정규화하거나 플레이스홀더를 정리할 것");
        else if (fill > 0.70f)
            Debug.LogWarning($"[SpriteBank] 아틀라스 {fill:P0} 찼다 ({texes.Length}장) — 곧 한계다");

        var atlas = new Texture2D(4096, 4096, TextureFormat.RGBA32, false);
        var rects = atlas.PackTextures(texes, 2, 4096, false);
        // ✅ 픽셀아트라 Point — Bilinear면 도트가 뭉개진다(§17 아트 방향).
        //    아틀라스 한도를 2048로 올린 것은 캐릭터 16프레임이 추가됐기 때문이다.
        // 화풍 전환(2026-08-18, 오너 지시): 픽셀아트 → 할로우 나이트 손그림.
        // Point는 도트 보존용이었다 — 손그림을 비정수 배율로 Point에 그리면 계단이 생긴다.
        atlas.filterMode = FilterMode.Bilinear;
        atlas.Apply(false, false);

        // 아틀라스가 정말로 채워졌는지 확인 — 실패 시 전부 투명이라 화면이 텅 빈다
        var probe = atlas.GetPixels32();
        int opaque = 0;
        for (int i = 0; i < probe.Length; i += 97) if (probe[i].a > 8) opaque++;
        if (opaque < 10)
            Debug.LogError("[SpriteBank] 아틀라스가 비었다 — 스프라이트가 그려지지 않는다");
        else
            Debug.Log($"[SpriteBank] 아틀라스 OK — 불투명 표본 {opaque}");

        // ── 크기·발밑 정렬을 스프라이트마다 **실측**해서 맞춘다 ────────────────
        // 2026-08-13 오너 지적 "캐릭터가 몬스터에 비해 엄청 큼"의 원인:
        //   몹 텍스처는 128×128인데 실제 몹은 26×30px뿐이고(블렌더 렌더가 여백투성이),
        //   캐릭터는 294×239에 155px가 들어차 있다. 여기에 스케일까지 손으로 곱하니
        //   화면상 캐릭터 3.17유닛 vs 몹 0.52유닛 — 6배가 됐다.
        // 그래서 PPU를 손으로 정하지 않고, **불투명 영역의 실제 높이**를 재서
        //   "화면에서 몇 유닛으로 보일지"(targetUnits)로 PPU를 역산한다.
        //   pivot도 같은 bbox의 바닥·중앙으로 잡으므로 캔버스 여백이 얼마든 발이 땅에 붙는다.
        var atlasPx = atlas.GetPixels32();
        int aw = atlas.width;

        Rect PxRect(int i)
        {
            var r = rects[i];
            return new Rect(Mathf.Round(r.x * atlas.width), Mathf.Round(r.y * atlas.height),
                            Mathf.Round(r.width * atlas.width), Mathf.Round(r.height * atlas.height));
        }

        /// 칸 안 불투명 픽셀의 경계(알파 40 이하는 그림자·잔광으로 보고 무시)
        (int minX, int minY, int maxX, int maxY) Bounds(int i)
        {
            var px = PxRect(i);
            int x0 = (int)px.x, y0 = (int)px.y, x1 = (int)px.xMax, y1 = (int)px.yMax;
            int minX = x1, minY = y1, maxX = x0, maxY = y0;
            bool any = false;
            for (int y = y0; y < y1; y++)
            {
                int row = y * aw;
                for (int x = x0; x < x1; x++)
                {
                    if (atlasPx[row + x].a <= 40) continue;
                    any = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (!any) { minX = x0; minY = y0; maxX = x1 - 1; maxY = y1 - 1; }
            return (minX, minY, maxX, maxY);
        }

        /// <summary>칸마다 내용을 실측해 크기·발밑을 맞춘다. 여백이 제각각인 몹·보스용.</summary>
        Sprite MakeAt(int i, float targetUnits)
        {
            var px = PxRect(i);
            var (minX, minY, maxX, maxY) = Bounds(i);
            float ppu = Mathf.Max(1, maxY - minY + 1) / Mathf.Max(0.01f, targetUnits);
            var pivot = new Vector2(
                Mathf.Clamp01(((minX + maxX) * 0.5f - px.x) / px.width),
                Mathf.Clamp01((minY - px.y) / px.height));
            return Sprite.Create(atlas, px, pivot, ppu, 0, SpriteMeshType.FullRect);
        }

        /// <summary>
        /// PPU와 pivot을 **밖에서 정해** 만든다. 캐릭터 프레임용.
        /// 프레임마다 내용을 재면 공격처럼 몸을 숙이는 동작에서 확대율이 달라져
        /// 캐릭터가 커졌다 작아졌다 한다 — 한 직업은 한 배율로 묶어야 한다.
        /// </summary>
        Sprite MakeWith(int i, float ppu, Vector2 pivot)
            => Sprite.Create(atlas, PxRect(i), pivot, ppu, 0, SpriteMeshType.FullRect);

        // 화면상 목표 크기(유닛). 여기 숫자만 고치면 전체 비율이 바뀐다.
        // ⚠️ 이 숫자들은 **여기서 정하지 않는다.** 단일 소스는 `art/prop_scale.json`이다.
        //    그 표는 첫 줄에 `_reference.character_units`를 선언하고 프랍 53종의 값을
        //    전부 그 기준의 상대 크기로 적어 뒀는데, 정작 캐릭터 크기는 여기 C# 상수로
        //    따로 살아 있었다 — 두 개의 진실. 2026-08-18에 "캐릭터가 작다"는 지적을
        //    여기 상수만 2.0→2.6으로 올려 막으려다, 프랍 53종의 상대비를 통째로
        //    무효화한다는 걸 발견했다(집이 캐릭터의 1.8배 → 1.4배로 조용히 찌그러진다).
        //    아래 값은 표를 못 읽었을 때의 **폴백일 뿐**이다. 크기를 바꾸려면 표를 고쳐라.
        float U(string key, float fallback) => AshesToStars.FieldDecor.Units(key, fallback);
        float U_CHAR = U("unit_char", 2.0f);    // 파티원·플레이어 — 크기표의 기준값
        float U_MOB = U("unit_mob", 1.5f);      // 잡몹 — 캐릭터보다 작아야 물량이 위협으로 읽힌다
        float U_ELITE = U("unit_elite", 1.9f);  // 정예 — 캐릭터보다 **작아야** 주인공이 읽힌다
        float U_BOSS = U("unit_boss", 3.4f);    // 보스 — 여기서만 캐릭터를 넘는다
        float U_PROJ = U("unit_proj", 0.4f);

        b.Player = MakeAt(0, U_CHAR);
        b._mobs = new[] { MakeAt(1, U_MOB), MakeAt(2, U_MOB), MakeAt(3, U_MOB) };
        b.Summon = MakeAt(4, U_ELITE);
        // ⚠️ 예전엔 `MakeAt(5, U_PROJ)`였다 — 인덱스 5는 `boss_0`이라 **투사체가 보스
        //    플레이스홀더 그림(회색 덩어리 + 원뿔)으로 그려지고 있었다**(2026-08-15 발견).
        //    작게 그려져 아무도 눈치채지 못했다. 전용 아트가 생기기 전까지는 단색 점이
        //    낫다 — 최소한 무엇인지 헷갈리지 않는다.
        // 보스 실루엣 4종(§0-B). `U_BOSS`는 선언만 되고 **쓰이는 곳이 없었다** —
        // 즉 보스 그림이 아틀라스에 실려도 보스 크기로 만들어진 적이 없다.
        b.Boss = new Sprite[BOSS_KEYS.Length];
        for (int i = 0; i < BOSS_KEYS.Length; i++)
            b.Boss[i] = MakeAt(6 + i, U_BOSS);      // 6부터가 BOSS_KEYS 구간이다

        // 보스 애니는 아틀라스 밖에 둔다. 4종×16장을  squish하면 도트가 뭉갠다.
        b._bossAnim = new Sprite[BOSS_DIRS.Length][];
        float bossPpu = 160f / U_BOSS;
        for (int k = 0; k < BOSS_DIRS.Length; k++)
        {
            b._bossAnim[k] = new Sprite[BOSS_FRAMES.Length];
            for (int f = 0; f < BOSS_FRAMES.Length; f++)
            {
                var tex = Resources.Load<Texture2D>(
                    $"sprites/{BOSS_DIRS[k]}/{BOSS_DIRS[k]}_{BOSS_FRAMES[f]}");
                if (tex == null) continue;
                b._bossAnim[k][f] = Sprite.Create(
                    tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.06f), bossPpu, 0, SpriteMeshType.FullRect);
            }
        }

        // 직업마다 **idle 한 장**으로 배율과 발밑을 정하고 그 직업의 전 프레임에 같은 값을 쓴다.
        //  ① 프레임마다 재면 공격처럼 몸을 숙이는 동작에서 확대율이 달라져 캐릭터가 커졌다 작아졌다 한다
        //  ② 원본 시트는 직업마다 캐릭터를 다른 크기로 그려놨다(힐러가 탱커보다 크다).
        //     idle 실측 높이를 U_CHAR로 맞추므로 **직업 간 키가 통일**된다(오너 지적 "높이도 맞춰야지").
        // 프레임 사이 정렬은 이미지 단계에서 공통 캔버스·발밑 정렬로 끝냈으므로 여기서 또 재지 않는다.
        b._job = new Sprite[JOB_DIRS.Length][];
        b._jobMissing = new bool[JOB_DIRS.Length];
        for (int j = 0; j < JOB_DIRS.Length; j++)
        {
            int idle = CHAR0 + j * JOB_FRAMES.Length;      // 0번이 idle
            // 이 직업의 아트가 통째로 없으면(파일 로드 실패로 단색 대체된 상태)
            // 그리지 말고 딜러 아트로 대신한다 — `Char()`가 그 판정을 쓴다.
            if (Resources.Load<Texture2D>($"sprites/{JOB_DIRS[j]}/{JOB_DIRS[j]}_{JOB_FRAMES[0]}") == null)
            {
                b._jobMissing[j] = true;
                Debug.LogWarning($"[SpriteBank] 직업 아트 없음: {JOB_DIRS[j]} — 딜러 아트로 대체한다");
            }
            var rc = PxRect(idle);
            var (bx0, by0, bx1, by1) = Bounds(idle);
            float ppu = Mathf.Max(1, by1 - by0 + 1) / U_CHAR;
            var pivot = new Vector2(
                Mathf.Clamp01(((bx0 + bx1) * 0.5f - rc.x) / rc.width),
                Mathf.Clamp01((by0 - rc.y) / rc.height));

            b._job[j] = new Sprite[JOB_FRAMES.Length];
            for (int f = 0; f < JOB_FRAMES.Length; f++)
                b._job[j][f] = MakeWith(CHAR0 + j * JOB_FRAMES.Length + f, ppu, pivot);
            // Sprite.Create는 rect가 텍스처 밖이거나 pivot이 NaN이면 **null을 돌려준다**.
            // 그러면 Char()가 조용히 placeholder(player_knight_0)로 폴백해 "새 그림을
            // 넣었는데 옛 그림이 나온다"가 된다 — 경고가 없어 원인을 못 찾는다(2026-08-18).
            if (b._job[j][0] == null)
                Debug.LogError($"[SpriteBank] {JOB_DIRS[j]} 스프라이트 생성 실패 — " +
                               $"칸={rc} ppu={ppu:F1} pivot={pivot} 내용높이={by1 - by0 + 1}");
        }

        // 몹도 종별로 idle 한 장으로 배율을 정한다 — 캐릭터와 같은 이유(프레임마다 재면 튄다)
        b._mobAnim = new Sprite[MOB_DIRS.Length][];
        for (int k = 0; k < MOB_DIRS.Length; k++)
        {
            int idle = MOB0 + k * MOB_FRAMES.Length;
            var rc = PxRect(idle);
            var (bx0, by0, bx1, by1) = Bounds(idle);
            float ppu = Mathf.Max(1, by1 - by0 + 1) / U_MOB;
            var pivot = new Vector2(
                Mathf.Clamp01(((bx0 + bx1) * 0.5f - rc.x) / rc.width),
                Mathf.Clamp01((by0 - rc.y) / rc.height));

            b._mobAnim[k] = new Sprite[MOB_FRAMES.Length];
            for (int f = 0; f < MOB_FRAMES.Length; f++)
                b._mobAnim[k][f] = MakeWith(MOB0 + k * MOB_FRAMES.Length + f, ppu, pivot);
        }

        // 체력바용 흰 사각형 — 아틀라스 마지막 칸이라 같은 머티리얼을 쓴다(배칭 유지)
        b.White = MakeAt(WHITE, 1f);

        // ⚠️ **`b.White` 할당 뒤에 와야 한다.** 위쪽(다른 base 스프라이트와 같은 자리)에
        //    뒀다가 null을 넣을 뻔했다 — 이 함수는 아래로 갈수록 채워지는 구조다.
        // ⚠️⚠️ `b.White`를 **그대로 대입하지 마라.** White는 1유닛짜리라 투사체가
        //    화면에 **큰 사각형**으로 그려진다(실측 2026-08-15: 분홍 덩어리 두 개가
        //    화면 한복판에 떴다 — 내가 boss 커밋에서 낸 회귀다).
        //    같은 아틀라스 칸을 **투사체 크기(U_PROJ)로 다시 만든다.**
        b.Projectile = MakeAt(DOT, U_PROJ);

        // 1차 전직 폴더는 아틀라스 밖. idle만 있으면 메뉴용, walk가 오면 전투 애니.
        b._extra = new Sprite[ExtraDirs.Length][];
        for (int e = 0; e < ExtraDirs.Length; e++)
        {
            string d = ExtraDirs[e];
            var idleTex = Resources.Load<Texture2D>($"sprites/{d}/{d}_idle_00");
            if (idleTex == null) continue;
            // ⚠️ PPU를 idle **한 장의 캔버스 높이**로 정하고 나머지 프레임에 그대로 쓴다.
            //    그래서 idle만 캔버스가 다르면 **전 프레임이 통째로 어긋난다**. 실제로
            //    2026-08-18에 전직 10종 전부 idle만 원본 생성물(1400~1900px)이고 나머지는
            //    128px이라, ppu=1734/2.8=619가 128px 프레임에 걸려 캐릭터가 0.21유닛
            //    (거의 안 보임)으로 그려지고 있었다. 경고가 없어 아무도 몰랐다.
            //    이제 캔버스가 어긋나면 **소리를 지른다** — 조용한 실패가 이 프로젝트에서
            //    가장 비싼 실패 방식이다(§21-1b 계열).
            float ppu = Mathf.Max(8, idleTex.height) / U_CHAR;
            var pivot = new Vector2(0.5f, 0.06f);
            b._extra[e] = new Sprite[JOB_FRAMES.Length];
            for (int f = 0; f < JOB_FRAMES.Length; f++)
            {
                var tex = Resources.Load<Texture2D>($"sprites/{d}/{d}_{JOB_FRAMES[f]}");
                if (tex == null) continue;
                if (tex.height != idleTex.height)
                    Debug.LogError($"[SpriteBank] {d}: 캔버스 높이 불일치 — " +
                                   $"idle {idleTex.height}px vs {JOB_FRAMES[f]} {tex.height}px. " +
                                   "한 직업의 프레임은 같은 캔버스여야 배율이 맞는다 " +
                                   "(art/align_frames.py로 정렬할 것)");
                b._extra[e][f] = Sprite.Create(
                    tex, new Rect(0, 0, tex.width, tex.height),
                    pivot, ppu, 0, SpriteMeshType.FullRect);
            }
        }

        // 조작 캐릭터도 플레이스홀더가 아니라 실제 아트로 — W2도 이 한 줄로 같이 반영된다
        b.Player = b.Char(Job.Tank);

        // 스프라이트 전용 머티리얼 1장 — 이걸 모두가 공유한다
        var sh = Shader.Find("Sprites/Default");
        b.Mat = new Material(sh) { enableInstancing = true };

        // 바닥 텍스처 (노이즈맵 베이크본)
        var g = Resources.Load<Texture2D>("ground/field_plain_albedo");
        if (g != null)
        {
            g.wrapMode = TextureWrapMode.Repeat;
            b.Ground = Sprite.Create(g, new Rect(0, 0, g.width, g.height), new Vector2(0.5f, 0.5f), 64f);
        }

        Cached = b;
        return b;
    }

    static Texture2D Solid(Color c, int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        t.SetPixels(px); t.Apply();
        return t;
    }
}
