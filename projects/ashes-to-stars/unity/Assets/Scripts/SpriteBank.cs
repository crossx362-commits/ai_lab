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
    public enum Job { Tank = 0, Dps = 1, Healer = 2, Buffer = 3 }

    /// <summary>
    /// 프레임 순서는 JOB_FRAMES와 **반드시 같아야 한다** — 인덱스로 짝지어 로드한다.
    /// 대시 4프레임은 §5 이동기(무적 0.3초)용이고, Invuln은 무적 구간 표시다.
    /// </summary>
    public enum Frame
    {
        Idle = 0, WalkA = 1, WalkB = 2, AttackA = 3, AttackB = 4,
        Special = 5, Hurt = 6, Death = 7,
        DashA = 8, DashB = 9, DashC = 10, DashD = 11, Invuln = 12,
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
    static readonly string[] MOB_DIRS =
        { "mob01", "mob_chaser", "mob_charger", "mob_ranged", "mob_swarmer" };
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
            case Motion.Walk: i = (int)MobFrame.Walk0 + (int)(t / 0.11f) % 6; break;
            case Motion.Attack: i = (int)MobFrame.Atk0 + Mathf.Clamp((int)(t / 0.09f), 0, 3); break;
            case Motion.Hurt: i = (int)MobFrame.Hurt1; break;   // 붉게 물든 프레임이 피격 표시다
            case Motion.Death: i = (int)MobFrame.Death0 + Mathf.Clamp((int)(t / 0.1f), 0, 3); break;
            default: i = (int)MobFrame.Idle0 + (int)(t / 0.16f) % 4; break;
        }
        return row[Mathf.Clamp(i, 0, row.Length - 1)] ?? Mob(kind);
    }

    static readonly string[] JOB_DIRS = { "tank", "dps", "healer", "buffer" };
    static readonly string[] JOB_FRAMES =
    {
        "idle_00", "walk_00", "walk_01", "attack_00", "attack_01",
        "special_00", "hurt_00", "death_00",
        "dash_00", "dash_01", "dash_02", "dash_03", "invuln_00",
    };

    Sprite[][] _job;   // [직업][프레임]

    /// <summary>직업·프레임별 픽셀아트. 로드 실패 시 플레이스홀더로 폴백한다.</summary>
    public Sprite Char(Job j, Frame f = Frame.Idle)
    {
        var row = _job[(int)j];
        return row[(int)f] ?? row[0] ?? Player;
    }

    /// <summary>캐릭터가 지금 무엇을 하고 있는가. 우선순위가 높은 것이 이긴다.</summary>
    public enum Motion { Idle, Walk, Attack, Special, Hurt, Death, Dash }

    /// <summary>
    /// 상태와 경과 시간으로 프레임을 고른다.
    /// 호출부는 "지금 무엇을 하는가"만 알면 되고 프레임 번호를 몰라도 된다 —
    /// 프레임 구성이 바뀌어도 호출부를 안 고치게 하려는 것이다.
    /// </summary>
    public Sprite CharAnim(Job j, Motion m, float t)
    {
        switch (m)
        {
            case Motion.Walk:
                return Char(j, (t % 0.36f) < 0.18f ? Frame.WalkA : Frame.WalkB);
            case Motion.Attack:
                // 앞 절반은 준비, 뒷 절반은 타격 — 짧아도 두 장이면 동작으로 읽힌다
                return Char(j, (t % 0.24f) < 0.12f ? Frame.AttackA : Frame.AttackB);
            case Motion.Dash:
                return Char(j, (Frame)((int)Frame.DashA + Mathf.Clamp((int)(t / 0.06f), 0, 3)));
            case Motion.Special: return Char(j, Frame.Special);
            case Motion.Hurt: return Char(j, Frame.Hurt);
            case Motion.Death: return Char(j, Frame.Death);
            default: return Char(j, Frame.Idle);
        }
    }

    // Resources 아래 이름 (확장자 없음)
    static readonly string[] MOB_KEYS = { "mob_chaser_0", "mob_swarmer_0", "mob_ranged_0" };

    /// <summary>보스 실루엣 4종. 층·계열로 골라 쓴다(§0-B 변주 원칙).</summary>
    public static readonly string[] BOSS_KEYS =
        { "boss_brute", "boss_serpent", "boss_wraith", "boss_construct" };

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
            "boss_brute", "boss_serpent", "boss_wraith", "boss_construct"
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

        var texes = new Texture2D[srcNames.Length + 1];
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
        atlas.filterMode = FilterMode.Point;
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
        const float U_CHAR = 2.0f;    // 파티원·플레이어
        const float U_MOB = 1.5f;     // 잡몹 — 캐릭터보다 작아야 물량이 위협으로 읽힌다
        const float U_ELITE = 2.2f;   // 정예
        const float U_BOSS = 3.4f;    // 보스
        const float U_PROJ = 0.4f;

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

        // 직업마다 **idle 한 장**으로 배율과 발밑을 정하고 그 직업의 전 프레임에 같은 값을 쓴다.
        //  ① 프레임마다 재면 공격처럼 몸을 숙이는 동작에서 확대율이 달라져 캐릭터가 커졌다 작아졌다 한다
        //  ② 원본 시트는 직업마다 캐릭터를 다른 크기로 그려놨다(힐러가 탱커보다 크다).
        //     idle 실측 높이를 U_CHAR로 맞추므로 **직업 간 키가 통일**된다(오너 지적 "높이도 맞춰야지").
        // 프레임 사이 정렬은 이미지 단계에서 공통 캔버스·발밑 정렬로 끝냈으므로 여기서 또 재지 않는다.
        b._job = new Sprite[JOB_DIRS.Length][];
        for (int j = 0; j < JOB_DIRS.Length; j++)
        {
            int idle = CHAR0 + j * JOB_FRAMES.Length;      // 0번이 idle
            var rc = PxRect(idle);
            var (bx0, by0, bx1, by1) = Bounds(idle);
            float ppu = Mathf.Max(1, by1 - by0 + 1) / U_CHAR;
            var pivot = new Vector2(
                Mathf.Clamp01(((bx0 + bx1) * 0.5f - rc.x) / rc.width),
                Mathf.Clamp01((by0 - rc.y) / rc.height));

            b._job[j] = new Sprite[JOB_FRAMES.Length];
            for (int f = 0; f < JOB_FRAMES.Length; f++)
                b._job[j][f] = MakeWith(CHAR0 + j * JOB_FRAMES.Length + f, ppu, pivot);
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
        b.Projectile = b.White;

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
