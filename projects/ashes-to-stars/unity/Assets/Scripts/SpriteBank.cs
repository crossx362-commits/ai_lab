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

    // Resources 아래 이름 (확장자 없음)
    static readonly string[] MOB_KEYS = { "mob_chaser_0", "mob_swarmer_0", "mob_ranged_0" };

    public static SpriteBank Load()
    {
        if (Cached != null) return Cached;
        var b = new SpriteBank();

        var srcNames = new[]
        {
            "player_knight_0", MOB_KEYS[0], MOB_KEYS[1], MOB_KEYS[2],
            "elite_healer_0", "boss_0"
        };

        var texes = new Texture2D[srcNames.Length];
        for (int i = 0; i < srcNames.Length; i++)
        {
            texes[i] = Resources.Load<Texture2D>("sprites/" + srcNames[i]);
            if (texes[i] == null)
            {
                Debug.LogWarning($"[SpriteBank] 누락: sprites/{srcNames[i]} — 단색 대체");
                texes[i] = Solid(new Color(0.8f, 0.3f, 0.3f, 1f), 64);
            }
        }

        // 런타임 아틀라스 — 모든 스프라이트가 한 텍스처를 공유하게
        foreach (var t in texes)
            if (!t.isReadable)
                Debug.LogError($"[SpriteBank] 읽기 불가 텍스처: {t.name} — 아틀라스가 실패한다. " +
                               "Editor/TextureImportRules 확인할 것");

        var atlas = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        var rects = atlas.PackTextures(texes, 2, 1024, false);
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
