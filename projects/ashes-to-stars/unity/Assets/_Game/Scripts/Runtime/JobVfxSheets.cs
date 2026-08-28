using UnityEngine;

namespace AshesToStars
{
    /// <summary>직업별 4×2 공격 이펙트 시트에서 지정 프레임을 런타임 스프라이트로 만든다.</summary>
    public static class JobVfxSheets
    {
        public const int FrameCount = 8;
        static readonly string[] Keys = { "tank_slash_sheet", "dps_slash_sheet", "mage_fire_sheet", "priest_heal_sheet", "tank_barrier_sheet", "bard_aura_sheet" };
        public static readonly string[] RequiredKeys = Keys;
        static Texture2D[] _textures;

        static void Load()
        {
            if (_textures != null) return;
            _textures = new Texture2D[Keys.Length];
            for (int i = 0; i < Keys.Length; i++) _textures[i] = Resources.Load<Texture2D>("fx/" + Keys[i]);
        }

        public static int SourceCount => Keys.Length;
        public static bool IsReady
        {
            get
            {
                Load();
                for (int i = 0; i < _textures.Length; i++) if (_textures[i] == null) return false;
                return true;
            }
        }

        public static Sprite Frame(int style, int frame)
        {
            Load();
            if (style < 0 || style >= _textures.Length || _textures[style] == null) return null;
            var texture = _textures[style];
            int column = Mathf.Clamp(frame, 0, FrameCount - 1) % 4;
            int row = Mathf.Clamp(frame, 0, FrameCount - 1) / 4;
            float w = texture.width / 4f, h = texture.height / 2f;
            return Sprite.Create(texture, new Rect(column * w, texture.height - (row + 1) * h, w, h),
                new Vector2(.5f, .5f), h / 2f, 0, SpriteMeshType.FullRect);
        }
    }
}
