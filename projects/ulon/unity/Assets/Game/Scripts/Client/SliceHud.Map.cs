using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
    {
        bool mapOpen;
        Texture2D mapTex;
        int mapTexSpan = -1;

        static Rect MiniMapRect => new Rect(Screen.width - 176f, 12f, 160f, 176f);
        static Rect FullMapRect
        {
            get
            {
                float side = Mathf.Min(300f, Screen.width * 0.24f);
                return new Rect(12f, 168f, side, side + 28f);
            }
        }

        void DrawWorldMap(OfflineWorld world, WorldBody me)
        {
            EnsureMapTex();
            DrawMapPanel(MiniMapRect, me, true);
            if (mapOpen)
                DrawMapPanel(FullMapRect, me, false);
        }

        void EnsureMapTex()
        {
            int spanKey = (int)WorldTerrain.Span;
            if (mapTex != null && mapTexSpan == spanKey)
                return;
            if (mapTex != null)
                Destroy(mapTex);
            int n = WorldMapBake.Size;
            mapTex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "UlonWorldMap"
            };
            var px = new Color32[n * n];
            WorldMapBake.Fill(px, n);
            mapTex.SetPixels32(px);
            mapTex.Apply(false, false);
            mapTexSpan = spanKey;
        }

        void DrawMapPanel(Rect r, WorldBody me, bool compact)
        {
            RegisterArea(r, this);
            var prev = GUI.color;
            GUI.color = new Color(0.10f, 0.09f, 0.07f, 0.94f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Box(r, GUIContent.none);

            float pad = 6f;
            float labelH = 20f;
            float inner = Mathf.Min(r.width, r.height - labelH) - pad * 2f;
            var map = new Rect(r.x + pad, r.y + labelH, inner, inner);
            if (mapTex != null)
                GUI.DrawTexture(map, mapTex, ScaleMode.StretchToFill, false);

            GUI.Label(new Rect(r.x + pad, r.y + 2f, r.width - pad * 2f, labelH),
                      compact ? "지도  (M)" : "세계 지도  ·  M 닫기");

            Mark(map, 0f, 0f, new Color(0.95f, 0.85f, 0.25f), compact ? 4f : 6f);
            Mark(map, WorldTerrain.LakeX, WorldTerrain.LakeZ, new Color(0.35f, 0.55f, 0.85f), compact ? 3f : 5f);
            Mark(map, Dungeon1.EntranceX, Dungeon1.EntranceZ, new Color(0.45f, 0.28f, 0.22f), 4f);
            Mark(map, Dungeon2.EntranceX, Dungeon2.EntranceZ, new Color(0.45f, 0.28f, 0.22f), 4f);
            Mark(map, Dungeon3.EntranceX, Dungeon3.EntranceZ, new Color(0.45f, 0.28f, 0.22f), 4f);
            if (me != null)
                Mark(map, me.transform.position.x, me.transform.position.z, Color.white, compact ? 5f : 7f);
            var corpse = NearestCorpse(me);
            if (corpse != null)
                Mark(map, corpse.transform.position.x, corpse.transform.position.z, new Color(0.85f, 0.85f, 0.85f), 5f);

            if (!compact)
            {
                GUI.Label(new Rect(map.x, map.yMax + 2f, map.width, 18f),
                          "노랑 마을 · 흰점 나 · 갈점 던전 · " +
                          ((int)WorldTerrain.Span).ToString() + "m");
            }
        }

        static void Mark(Rect map, float wx, float wz, Color color, float size)
        {
            var p = WorldMapBake.WorldToGui(map, wx, wz);
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
