using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 월드맵 전장의 안개(§14) + 엘프 탐험 범위 +30%(§18-9).
    /// 반경 공식은 이미 확정된 SenseBase(층) — 새 숫자를 만들지 않는다.
    /// 엘프는 그 반경에 ×1.30. 로컬 더미 별 3개가 반경 안이면 밝혀진다.
    /// 서버 별은 없다. QA_NO면 옛 「로컬 허브만」·안개 없음.
    /// 별 위 흰 캡션은 헤더 부제와 같은 Line()을 두 번 그리지 않는다
    /// (실측 worldmap_hud_nav_shots/after). QA_NO_EXPLORE_DUP면 옛 중복.
    /// </summary>
    public static class WorldExplore
    {
        public const string EnvShow = "QA_EXPLORE_FOG";
        public const string EnvNo = "QA_NO_EXPLORE_FOG";
        public const string EnvNoDup = "QA_NO_EXPLORE_DUP";
        public const string EnvNoLabelPlate = "QA_NO_EXPLORE_LABEL_PLATE";
        public const string EnvNoLabelSpread = "QA_NO_EXPLORE_LABEL_SPREAD";
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

        public static bool DupBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoDup);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool LabelPlateBlocked =>
            Environment.GetEnvironmentVariable(EnvNoLabelPlate) == "1";

        public static bool LabelSpreadBlocked =>
            Environment.GetEnvironmentVariable(EnvNoLabelSpread) == "1";

        /// <summary>별 이름을 성운 위에 띄우지 않고 작은 금테 표찰 안에 가둔다.</summary>
        public static Rect LabelPlate(Rect field, Rect icon)
        {
            var plate = new Rect(icon.x - 32f, icon.yMax + 2f, icon.width + 64f, 24f);
            plate.x = Mathf.Clamp(plate.x, field.x + 2f, field.xMax - plate.width - 2f);
            plate.y = Mathf.Min(plate.y, field.yMax - plate.height - 2f);
            return plate;
        }

        /// <summary>가까운 별 세 개의 표찰을 아이콘 사방으로 펼쳐 서로 가리지 않게 한다.</summary>
        public static Rect LabelPlate(Rect field, Rect icon, int index)
        {
            if (LabelSpreadBlocked) return LabelPlate(field, icon);
            const float width = 86f;
            const float height = 24f;
            Rect plate;
            if (index == 0)
                plate = new Rect(icon.xMax + 6f, icon.center.y - height * 0.5f, width, height);
            else if (index == 1)
                plate = new Rect(icon.x - width - 6f, icon.center.y + 5f, width, height);
            else
                plate = new Rect(icon.x - width - 6f, icon.center.y - height - 5f, width, height);
            plate.x = Mathf.Clamp(plate.x, field.x + 2f, field.xMax - plate.width - 2f);
            plate.y = Mathf.Clamp(plate.y, field.y + 2f, field.yMax - plate.height - 2f);
            return plate;
        }

        /// <summary>
        /// 헤더 부제가 이미 Line()이다(QA_EXPLORE_FOG 또는 엘프 탐험 조각).
        /// 별 위까지 같은 문장이면 두 번 겹친다.
        /// </summary>
        public static bool HeaderOwnsLine()
        {
            if (ShowQa) return true;
            return Percent() == ElfPercent;
        }

        /// <summary>별 필드 캡션. QA_NO면 옛 중복. 헤더가 가진 문장은 빈 문자열.</summary>
        public static string FieldCaption(int floor)
        {
            if (DupBlocked) return Line(floor);
            if (HeaderOwnsLine()) return "";
            return Line(floor);
        }

        public static string FieldCaption() =>
            FieldCaption(Mathf.Max(1, GameState.TowerFloor));

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
                return $"탐험 반경 +30% · 밝힌 별 {n}/{tot}";
            return $"밝혀진 별 {n}/{tot}";
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

        public static void Draw(Rect field, GUIStyle hubLabel)
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
            if (!UiAtlas.DrawSliced(field, "panel", 8f, new Color(1f, 1f, 1f, 0.88f)))
                UiAtlas.Draw(field, "panel", new Color(1f, 1f, 1f, 0.88f));

            var disc = new Rect(c.x - rPx, c.y - rPx, rPx * 2f, rPx * 2f);
            var discTint = new Color(0.40f, 0.62f, 1f, 0.32f);
            if (!UiAtlas.DrawFit(disc, "panel", discTint))
                UiAtlas.DrawSliced(disc, "panel", 8f, discTint);

            float home = Mathf.Clamp(WorldStar.SizePx(floor) * 0.42f, 18f, 36f);
            GUI.color = Color.white;
            UiAtlas.DrawFit(new Rect(c.x - home * 0.5f, c.y - home * 0.5f, home, home),
                "worldmap");

            var capStyle = HubCap(hubLabel);
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
                var lab = LabelPlate(field, ir, i);
                if (!LabelPlateBlocked)
                {
                    if (!UiAtlas.DrawSliced(lab, "panel", 8f,
                            new Color(0.80f, 0.88f, 1f, 0.82f)))
                        UiAtlas.Draw(lab, "panel", new Color(0.80f, 0.88f, 1f, 0.82f));
                    lab = new Rect(lab.x + 2f, lab.y + 2f, lab.width - 4f, lab.height - 4f);
                }
                UiPages.LabelFit(lab, stars[i].Name, capStyle);
            }

            GUI.color = prev;
            string cap = FieldCaption(floor);
            if (string.IsNullOrEmpty(cap)) return;
            var head = new Rect(field.x + 8f, field.y + 4f, field.width - 16f, 18f);
            UiPages.LabelFit(head, cap, capStyle);
        }

        static GUIStyle HubCap(GUIStyle hub)
        {
            var s = new GUIStyle(hub != null ? hub : GUIStyle.none)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            return s;
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
