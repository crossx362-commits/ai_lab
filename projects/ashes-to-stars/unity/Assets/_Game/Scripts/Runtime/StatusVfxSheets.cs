using UnityEngine;

namespace AshesToStars
{
    /// <summary>독·빙결과 보스 장판 예고의 4x2, 8프레임 시트를 읽는다.</summary>
    public static class StatusVfxSheets
    {
        static readonly string[] Files = { "boss_aoe_warning_sheet", "poison_status_sheet", "freeze_status_sheet" };
        static readonly Texture2D[] Textures = new Texture2D[Files.Length];
        static bool _loaded;

        public static int SourceCount => Files.Length;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < Files.Length; i++) Textures[i] = Resources.Load<Texture2D>("fx/" + Files[i]);
        }

        public static Sprite Frame(int style, int frame)
        {
            Load();
            if (style < 0 || style >= Textures.Length || frame < 0 || frame > 7) return null;
            var texture = Textures[style];
            if (texture == null) return null;
            float w = texture.width / 4f, h = texture.height / 2f;
            int column = frame % 4, row = frame / 4;
            return Sprite.Create(texture, new Rect(column * w, texture.height - (row + 1) * h, w, h),
                Vector2.one * .5f, h / 2f, 0, SpriteMeshType.FullRect);
        }
    }
}
