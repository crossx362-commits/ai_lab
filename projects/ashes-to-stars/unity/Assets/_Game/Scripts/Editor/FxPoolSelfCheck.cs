using System.IO;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>전투 이펙트의 전면 정렬·런타임 심볼·실소비 크기 견본을 검증한다.</summary>
    public static class FxPoolSelfCheck
    {
        static readonly (string name, string role)[] Symbols =
        {
            ("fx_hit", "충격"), ("fx_slash", "베기"), ("fx_heal", "치유 고리"), ("fx_fire", "불꽃"),
            ("fx_shield", "보호 고리"), ("fx_taunt", "동심 충격파"),
            ("fx_summon", "룬 마법진"), ("fx_death", "연기 확산"),
        };

        const int SampleSize = 64;
        const int CellSize = 96;

        public static void Run()
        {
            Debug.Assert(FxPool.FrontSortingOrder > 1100,
                "[FxPool] 전면 이펙트 정렬값은 몬스터·보스 깊이 정렬(최대 1100)보다 커야 한다");
            var textures = new Texture2D[Symbols.Length];
            for (int i = 0; i < Symbols.Length; i++)
                textures[i] = AssertRuntimeSymbol(Symbols[i].name, Symbols[i].role);
            WriteRuntimeContactSheet(textures);
            Debug.Log("[FxPoolSelfCheck] PASS");
        }

        static Texture2D AssertRuntimeSymbol(string name, string role)
        {
            var texture = Resources.Load<Texture2D>("fx/" + name);
            Debug.Assert(texture != null, $"[FxPool] {name} 런타임 리소스가 있어야 한다");
            Debug.Assert(texture != null && texture.width == 512 && texture.height == 512,
                $"[FxPool] {name}는 투명 512×512 런타임 심볼이어야 한다");
            if (texture == null || !texture.isReadable) return texture;
            var pixels = texture.GetPixels32();
            int clear = 0, visible = 0;
            for (int i = 0; i < pixels.Length; i += 31)
            {
                if (pixels[i].a < 16) clear++;
                if (pixels[i].a > 64) visible++;
            }
            Debug.Assert(clear > 1000, $"[FxPool] {name} 배경은 투명해야 한다");
            Debug.Assert(visible > 500, $"[FxPool] {name} {role} 실루엣이 비어 있지 않아야 한다");
            return texture;
        }

        static void WriteRuntimeContactSheet(Texture2D[] textures)
        {
            const int columns = 4, rows = 2;
            var sheet = new Texture2D(columns * CellSize, rows * CellSize, TextureFormat.RGBA32, false);
            var background = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < background.Length; i++) background[i] = new Color32(8, 9, 13, 255);
            sheet.SetPixels32(background);
            for (int i = 0; i < textures.Length; i++)
            {
                var source = textures[i];
                if (source == null || !source.isReadable) continue;
                int cellX = (i % columns) * CellSize + (CellSize - SampleSize) / 2;
                int cellY = (rows - 1 - i / columns) * CellSize + (CellSize - SampleSize) / 2;
                for (int y = 0; y < SampleSize; y++)
                for (int x = 0; x < SampleSize; x++)
                {
                    Color fg = source.GetPixelBilinear((x + .5f) / SampleSize, (y + .5f) / SampleSize);
                    Color bg = sheet.GetPixel(cellX + x, cellY + y);
                    sheet.SetPixel(cellX + x, cellY + y, Color.Lerp(bg, fg, fg.a));
                }
            }
            sheet.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", "results"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "fx_pool_runtime_symbols.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log($"[FxPool] 실소비 64px 견본(hit/slash/heal/fire | shield/taunt/summon/death): {path}");
        }
    }
}
