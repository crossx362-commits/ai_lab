using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 월드맵 픽셀 원장. HUD와 게이트가 같은 색 규칙을 쓴다 — 두 곳에 칠하면 화면과 자가 갈린다.
    /// 북쪽(+Z)이 위. 원작 울온의 종이 지도처럼 지형·물·마을만 읽히게 칠한다(원작 그래픽 복제 아님).
    /// </summary>
    public static class WorldMapBake
    {
        public const int Size = 256;

        public static void Fill(Color32[] pixels, int size)
        {
            if (pixels == null || pixels.Length < size * size)
                throw new System.ArgumentException("월드맵 픽셀 배열이 모자랍니다.");
            for (int y = 0; y < size; y++)
            {
                // Texture2D 행 0 = 아래 = 남쪽(−Z). GUI.DrawTexture가 위를 북쪽으로 보여 준다.
                float wz = (y / (float)(size - 1)) * WorldTerrain.Span - WorldTerrain.Half;
                for (int x = 0; x < size; x++)
                {
                    float wx = (x / (float)(size - 1)) * WorldTerrain.Span - WorldTerrain.Half;
                    pixels[y * size + x] = ColorOf(wx, wz);
                }
            }
        }

        public static Color32 ColorOf(float wx, float wz)
        {
            float h = WorldTerrain.HeightAt(wx, wz);
            float sea = WorldTerrain.SeaLevel;
            if (h < sea)
            {
                float t = Mathf.Clamp01((sea - h) / 2.6f);
                byte r = (byte)Mathf.Lerp(70f, 28f, t);
                byte g = (byte)Mathf.Lerp(110f, 48f, t);
                byte b = (byte)Mathf.Lerp(150f, 90f, t);
                return new Color32(r, g, b, 255);
            }

            float cheb = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz));
            if (cheb >= WorldTerrain.BeachTopM)
                return new Color32(196, 178, 130, 255);
            if (cheb >= WorldTerrain.MountainStart)
            {
                float t = Mathf.InverseLerp(WorldTerrain.MountainStart, WorldTerrain.MountainPeak, cheb);
                byte v = (byte)Mathf.Lerp(120f, 168f, Mathf.Clamp01(t));
                return new Color32(v, (byte)(v - 8), (byte)(v - 16), 255);
            }

            float forest = DistSq(wx, wz, WorldRegions.Forest.X, WorldRegions.Forest.Z);
            if (forest < WorldRegions.Forest.Radius * WorldRegions.Forest.Radius)
                return new Color32(52, 92, 48, 255);
            float meadow = DistSq(wx, wz, WorldRegions.Meadow.X, WorldRegions.Meadow.Z);
            if (meadow < WorldRegions.Meadow.Radius * WorldRegions.Meadow.Radius)
                return new Color32(150, 140, 70, 255);
            float mine = DistSq(wx, wz, WorldRegions.Mine.X, WorldRegions.Mine.Z);
            if (mine < WorldRegions.Mine.Radius * WorldRegions.Mine.Radius)
                return new Color32(130, 118, 96, 255);
            return new Color32(86, 124, 64, 255);
        }

        public static bool IsWater(Color32 c) => c.b > c.g && c.b > c.r;

        public static Vector2 WorldToGui(Rect map, float wx, float wz)
        {
            float u = (wx + WorldTerrain.Half) / WorldTerrain.Span;
            float v = 1f - (wz + WorldTerrain.Half) / WorldTerrain.Span;
            return new Vector2(map.x + u * map.width, map.y + v * map.height);
        }

        static float DistSq(float x, float z, float cx, float cz)
        {
            float dx = x - cx, dz = z - cz;
            return dx * dx + dz * dz;
        }
    }
}
