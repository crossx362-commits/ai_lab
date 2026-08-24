using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 월드맵 전장의 안개(§14) + 엘프 탐험 범위 +30%(§18-9).
    /// 반경 공식은 이미 확정된 SenseBase(층) — 새 숫자를 만들지 않는다.
    /// 엘프는 그 반경에 ×1.30. 로컬 더미 별 3개가 반경 안이면 밝혀진다.
    /// 서버 별은 없다. QA_NO면 옛 「로컬 허브만」·안개 없음.
    /// </summary>
    public static class WorldExplore
    {
        public const string EnvShow = "QA_EXPLORE_FOG";
        public const string EnvNo = "QA_NO_EXPLORE_FOG";
        public const int HumanPercent = 100;
        public const int ElfPercent = 130;
        public const int NearFloor = 1;
        public const int MidFloor = 30;

        public struct Neighbor
        {
            public string Name;
            public float Dist;
            public float Angle;
            public Neighbor(string name, float dist, float angle)
            {
                Name = name; Dist = dist; Angle = angle;
            }
        }

        static bool _qaSeeded;
        static Texture2D _disc;
        static GUIStyle _cap;

        /// <summary>SelfCheck가 종족 배율을 고정할 때만. 0이면 RaceDef·계정 종족을 본다.</summary>
        public static float ForceMul;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>가까운=1층 영공, 경계=30층 인간 영공, 안개=30층 엘프 영공(×1.30).</summary>
        public static Neighbor[] Neighbors()
        {
            float near = WorldStar.SenseBase(NearFloor);
            float mid = WorldStar.SenseBase(MidFloor);
            float far = mid * ElfPercent / 100f;
            return new[]
            {
                new Neighbor("가까운 별", near, 0.55f),
                new Neighbor("경계 별", mid, 2.20f),
                new Neighbor("안개 별", far, 4.05f),
            };
        }

        public static int Percent()
        {
            if (Blocked) return HumanPercent;
            if (ForceMul > 0f) return Math.Max(1, (int)Math.Round(ForceMul * 100f));
            var d = RaceInfo.For(RacePrefs.Get());
            if (d != null && d.탐험범위배율 > 0f)
                return Math.Max(1, (int)Math.Round(d.탐험범위배율 * 100f));
            return RacePrefs.Get() == RaceId.엘프 ? ElfPercent : HumanPercent;
        }

        public static float Radius(int floor) =>
            WorldStar.SenseBase(floor) * Percent() / 100f;

        public static bool Revealed(float dist, int floor) =>
            dist <= Radius(floor) + 0.0001f;

        public static int RevealedCount(int floor)
        {
            var stars = Neighbors();
            int n = 0;
            for (int i = 0; i < stars.Length; i++)
                if (Revealed(stars[i].Dist, floor)) n++;
            return n;
        }

        public static string Line(int floor)
        {
            if (Blocked) return "안개 없음";
            int n = RevealedCount(floor);
            int tot = Neighbors().Length;
            if (Percent() == ElfPercent)
                return $"엘프 탐험 +30% · 별 {n}/{tot}(§18-9)";
            return $"밝혀진 별 {n}/{tot}(§14)";
        }

        public static string Line() =>
            Line(Mathf.Max(1, GameState.TowerFloor));

        /// <summary>성계 카드 한 줄. 도크 CaptionMaxRunes=18 안.</summary>
        public static string Caption()
        {
            if (Blocked) return WorldMapDockCap.StarCap;
            int floor = Mathf.Max(1, GameState.TowerFloor);
            return $"탐험 {RevealedCount(floor)}/{Neighbors().Length}";
        }

        public static void Draw(Rect field)
        {
            if (Blocked) return;
            if (field.width < 48f || field.height < 48f) return;

            int floor = Mathf.Max(1, GameState.TowerFloor);
            float radius = Radius(floor);
            var c = new Vector2(field.x + field.width * 0.5f,
                                field.y + field.height * 0.5f);
            float maxR = WorldStar.SenseMul(WorldStar.MaxFloor);
            if (maxR < 0.01f) maxR = 11f;
            float pxPer = Mathf.Min(field.width, field.height) * 0.42f / maxR;
            float rPx = Mathf.Max(10f, radius * pxPer);

            var prev = GUI.color;
            GUI.color = new Color(0.02f, 0.04f, 0.10f, 0.55f);
            GUI.DrawTexture(field, Texture2D.whiteTexture);

            GUI.color = new Color(0.40f, 0.62f, 1f, 0.32f);
            GUI.DrawTexture(new Rect(c.x - rPx, c.y - rPx, rPx * 2f, rPx * 2f), Disc());

            float home = Mathf.Clamp(WorldStar.SizePx(floor) * 0.42f, 18f, 36f);
            GUI.color = Color.white;
            UiAtlas.DrawFit(new Rect(c.x - home * 0.5f, c.y - home * 0.5f, home, home),
                "worldmap");

            CapStyle();
            var stars = Neighbors();
            for (int i = 0; i < stars.Length; i++)
            {
                bool seen = Revealed(stars[i].Dist, floor);
                float dPx = stars[i].Dist * pxPer;
                float x = c.x + Mathf.Cos(stars[i].Angle) * dPx;
                float y = c.y + Mathf.Sin(stars[i].Angle) * dPx;
                float s = seen ? 22f : 14f;
                var ir = new Rect(x - s * 0.5f, y - s * 0.5f, s, s);
                if (ir.xMax < field.x || ir.x > field.xMax
                    || ir.yMax < field.y || ir.y > field.yMax)
                    continue;
                GUI.color = seen ? Color.white : new Color(1f, 1f, 1f, 0.16f);
                UiAtlas.DrawFit(ir, "worldmap");
                if (!seen) continue;
                var lab = new Rect(ir.x - 28f, ir.yMax, s + 56f, 16f);
                lab.x = Mathf.Clamp(lab.x, field.x, field.xMax - lab.width);
                lab.y = Mathf.Min(lab.y, field.yMax - lab.height);
                UiPages.LabelFit(lab, stars[i].Name, _cap);
            }

            GUI.color = prev;
            var head = new Rect(field.x + 8f, field.y + 4f, field.width - 16f, 18f);
            UiPages.LabelFit(head, Line(floor), _cap);
        }

        static Texture2D Disc()
        {
            if (_disc != null) return _disc;
            const int n = 64;
            _disc = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[n * n];
            float mid = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - mid, dy = y - mid;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (mid + 0.01f);
                float a = d >= 1f ? 0f : d <= 0.82f ? 1f : (1f - d) / 0.18f;
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                px[y * n + x] = new Color32(255, 255, 255, b);
            }
            _disc.SetPixels32(px);
            _disc.Apply(false, true);
            return _disc;
        }

        static void CapStyle()
        {
            if (_cap != null) return;
            _cap = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            _cap.normal.textColor = new Color(0.82f, 0.90f, 1f, 0.95f);
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            RacePrefs.Set(RaceId.엘프);
            if (GameState.TowerFloor < MidFloor)
                GameState.SetTowerFloorForTest(MidFloor);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            ForceMul = 0f;
        }
    }
}
