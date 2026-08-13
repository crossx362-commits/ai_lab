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
    Sprite[] _mobs;

    public Sprite Mob(int i) => _mobs[Mathf.Clamp(i, 0, _mobs.Length - 1)];

    // ── 오너 픽셀아트 (2026-08-13) ────────────────────────
    // 직업 4종 × 프레임 4종. 블렌더 플레이스홀더와 달리 **실제 아트**다.
    // ⚠️ 파일이 Resources 밖(_Game/Art/Sprites)에 있으면 Resources.Load가 못 찾아
    //    화면엔 플레이스홀더만 나온다 — 실제로 그 상태로 W1~W3을 돌렸었다.
    public enum Job { Tank = 0, Dps = 1, Healer = 2, Buffer = 3 }
    public enum Frame { Idle = 0, WalkA = 1, WalkB = 2, Attack = 3 }

    static readonly string[] JOB_DIRS = { "tank", "dps", "healer", "buffer" };
    static readonly string[] JOB_FRAMES = { "idle_00", "walk_00", "walk_01", "attack_00" };

    Sprite[][] _job;   // [직업][프레임]

    /// <summary>직업·프레임별 픽셀아트. 로드 실패 시 플레이스홀더로 폴백한다.</summary>
    public Sprite Char(Job j, Frame f = Frame.Idle)
    {
        var row = _job[(int)j];
        return row[(int)f] ?? row[0] ?? Player;
    }

    /// <summary>걷는 두 프레임을 시간으로 토글한다. 멈춰 있으면 대기 프레임.</summary>
    public Sprite CharAnim(Job j, bool moving, float t)
    {
        if (!moving) return Char(j, Frame.Idle);
        return Char(j, (t % 0.36f) < 0.18f ? Frame.WalkA : Frame.WalkB);
    }

    // Resources 아래 이름 (확장자 없음)
    static readonly string[] MOB_KEYS = { "mob_chaser_0", "mob_swarmer_0", "mob_ranged_0" };

    public static SpriteBank Load()
    {
        if (Cached != null) return Cached;
        var b = new SpriteBank();

        var baseNames = new[]
        {
            "player_knight_0", MOB_KEYS[0], MOB_KEYS[1], MOB_KEYS[2],
            "elite_healer_0", "boss_0"
        };

        // 캐릭터 픽셀아트를 같은 아틀라스에 싣는다 — 배칭이 깨지면 W1 성능 전제가 무너진다
        var charNames = new System.Collections.Generic.List<string>();
        foreach (var d in JOB_DIRS)
            foreach (var f in JOB_FRAMES)
                charNames.Add($"{d}/{d}_{f}");

        var srcNames = new string[baseNames.Length + charNames.Count];
        baseNames.CopyTo(srcNames, 0);
        charNames.CopyTo(srcNames, baseNames.Length);
        int CHAR0 = baseNames.Length;   // 캐릭터 구간 시작 인덱스

        var texes = new Texture2D[srcNames.Length];
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

        var atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
        var rects = atlas.PackTextures(texes, 2, 2048, false);
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

        Sprite MakeAt(int i, float ppu)
        {
            var r = rects[i];
            var px = new Rect(r.x * atlas.width, r.y * atlas.height,
                              r.width * atlas.width, r.height * atlas.height);
            return Sprite.Create(atlas, px, new Vector2(0.5f, 0.12f), ppu, 0,
                                 SpriteMeshType.FullRect);
        }

        b.Player = MakeAt(0, 96f);
        b._mobs = new[] { MakeAt(1, 128f), MakeAt(2, 128f), MakeAt(3, 128f) };
        b.Summon = MakeAt(4, 150f);
        b.Projectile = MakeAt(5, 300f);

        // 오너 픽셀아트는 원본 크기가 제각각이다(129~154 × 157~184).
        // PPU를 고정하면 키가 들쭉날쭉해지므로 **높이 기준으로 정규화**해 화면상 같은 키로 세운다.
        const float CHAR_UNITS = 1.75f;
        b._job = new Sprite[JOB_DIRS.Length][];
        for (int j = 0; j < JOB_DIRS.Length; j++)
        {
            b._job[j] = new Sprite[JOB_FRAMES.Length];
            for (int f = 0; f < JOB_FRAMES.Length; f++)
            {
                int idx = CHAR0 + j * JOB_FRAMES.Length + f;
                b._job[j][f] = MakeAt(idx, texes[idx].height / CHAR_UNITS);
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
