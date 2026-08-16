using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 허브 페이지 좌표. 화면마다 줄을 쌓지 않고 같은 격자·탭 높이를 쓴다.
    /// </summary>
    public static class UiPages
    {
        public const float TabH = 50f;
        public const float TabGap = 10f;

        /// <summary>
        /// §16 하단 5칸. 전체 폭을 5등분하면 229×72 알약이 된다(오너 21:45).
        /// 아이콘+짧은 라벨의 타일을 가운데 모은다 — AFK·세븐나이츠 도크와 같다.
        /// </summary>
        public const float NavTileW = 88f;
        public const float NavTileH = 70f;
        public const float NavTileGap = 8f;
        public const float NavMaxAspect = 1.45f;
        /// <summary>본문이 비워 둘 아래 여백. 옛 BarH+28=100보다 작아야 공간이 늘어난다.</summary>
        public const float NavReserve = 80f;

        public static Rect[] NavDock(int count, float screenW = 1280f, float screenH = 720f)
        {
            if (count < 1) count = 1;
            float used = count * NavTileW + (count - 1) * NavTileGap;
            float x0 = (screenW - used) * 0.5f;
            float y = screenH - NavTileH - 8f;
            var tiles = new Rect[count];
            for (int i = 0; i < count; i++)
                tiles[i] = new Rect(x0 + i * (NavTileW + NavTileGap), y, NavTileW, NavTileH);
            return tiles;
        }

        public static string NavIcon(string scene) => scene switch
        {
            "Estate" => "territory",
            "Field" => "field",
            "Tower" => "tower",
            "WorldMap" => "worldmap",
            "Character" => "characters",
            _ => null,
        };

        public static Rect AfterTabs(Rect r, float extra = 12f) =>
            new Rect(r.x, r.y + TabH + extra, r.width, Mathf.Max(40f, r.height - TabH - extra));

        /// <summary>초상 둘레 장비 칸. 각도는 12시가 -90도.</summary>
        public static Rect SlotOnRing(Vector2 center, float radiusX, float radiusY,
                                      float degrees, float size)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float x = center.x + Mathf.Cos(rad) * radiusX - size * 0.5f;
            float y = center.y + Mathf.Sin(rad) * radiusY - size * 0.5f;
            return new Rect(x, y, size, size);
        }

        public static readonly float[] EquipRingDegrees = { -90f, -20f, 50f, 125f, 180f, -145f };

        /// <summary>명부 왼쪽 바둑판 · 오른쪽 대형 모습. 비율을 뒤집으면 오너 지시와 반대다.</summary>
        public const float RosterListRatio = 0.36f;
        public const int RosterCols = 3;
        public const float RosterCellH = 132f;
        public const float RosterRowH = 64f;
        public const float RosterRowGap = 8f;
        public const float LargeLookW = 240f;
        public const float LargeLookH = 300f;

        public static void RosterSplit(Rect r, out Rect list, out Rect stage)
        {
            const float gap = 10f;
            float listW = Mathf.Max(240f, r.width * RosterListRatio);
            if (listW > r.width - 140f) listW = Mathf.Max(180f, r.width * 0.34f);
            list = new Rect(r.x, r.y, listW, r.height);
            stage = new Rect(r.x + listW + gap, r.y, Mathf.Max(80f, r.width - listW - gap), r.height);
        }

        public static Rect RosterCell(Rect list, int index, int cols = RosterCols)
        {
            if (cols < 1) cols = 1;
            float gap = RosterRowGap;
            float cw = (list.width - gap * (cols - 1)) / cols;
            int x = index % cols;
            int y = index / cols;
            return new Rect(list.x + x * (cw + gap), list.y + y * (RosterCellH + gap),
                cw, RosterCellH);
        }

        /// <summary>
        /// 명부 칸. 초상·이름·직업·목숨이 한 칸 안에서 서로 겹치지 않는다.
        /// 초상을 72로 고정하면 118칸에서 직업과 하트가 겹친다.
        /// </summary>
        public const float RosterPlate = 58f;

        public static void RosterCellLayout(Rect cell, out Rect face, out Rect name, out Rect job,
                                            out Rect hearts)
        {
            float faceW = Mathf.Min(72f, cell.width - 12f,
                Mathf.Max(32f, cell.height - RosterPlate - 8f));
            face = new Rect(cell.center.x - faceW * 0.5f, cell.y + 6f, faceW, faceW);
            name = new Rect(cell.x + 4f, face.yMax + 2f, cell.width - 8f, 18f);
            job = new Rect(cell.x + 4f, face.yMax + 20f, cell.width - 8f, 16f);
            hearts = new Rect(cell.center.x - 36f, cell.yMax - 22f, 72f, 22f);
        }

        /// <summary>편성 카드. 초상 아래 이름, 맨 아래 목숨 — 두 줄이 겹치면 이름이 잘린다.</summary>
        public static void PartyCardLayout(Rect cell, out Rect face, out Rect name, out Rect marks)
        {
            float faceS = Mathf.Min(cell.width - 16f, Mathf.Max(32f, cell.height - 52f));
            face = new Rect(cell.center.x - faceS * 0.5f, cell.y + 6f, faceS, faceS);
            name = new Rect(cell.x + 6f, face.yMax + 2f, cell.width - 12f, 18f);
            marks = new Rect(cell.center.x - 40f, cell.yMax - 22f, 80f, 22f);
            if (name.yMax > marks.y)
                name.height = Mathf.Max(0f, marks.y - name.y);
        }

        public static Rect RosterRow(Rect list, int index) => RosterCell(list, index, 1);

        public static Rect LargeLook(Rect stage)
        {
            float maxH = Mathf.Max(160f, stage.height - 96f);
            float h = Mathf.Min(LargeLookH, maxH);
            float w = Mathf.Min(LargeLookW, stage.width * 0.46f, h * 0.88f);
            h = Mathf.Min(h, w / 0.72f);
            float y = stage.y + 70f;
            if (y + h > stage.yMax - 8f) y = Mathf.Max(stage.y + 64f, stage.yMax - h - 8f);
            return new Rect(stage.x + 16f, y, w, h);
        }

        /// <summary>전투 idle 스프라이트 폴더. 초상이 아니라 전신 모습을 그릴 때 쓴다.</summary>
        public static string LookDir(string job) => job switch
        {
            "탱" or "수호기사" or "광전사" => "tank",
            "힐" or "사제" or "드루이드" => "healer",
            "버퍼" or "음유시인" or "주술사" or "정령사" => "buffer",
            "마딜" or "마법사" or "소환사" => "mage",
            _ => "dps",
        };

        public static string LookPath(string job, string frame)
        {
            string dir = LookDir(job);
            return $"sprites/{dir}/{dir}_{frame}";
        }

        public const string IdleFrame = "idle_00";
        /// <summary>시작 직업 카드는 idle(오너 21:52). 걷기로 바꾸면 SelfCheck가 실패한다.</summary>
        public const bool StarterLookWalks = false;

        public static string WalkFrame() =>
            (Time.unscaledTime % 0.36f) < 0.18f ? "walk_00" : "walk_01";

        public static string JobLookFrame(bool walk) => walk ? WalkFrame() : IdleFrame;

        /// <summary>idle 시트 실측(tank 142×128). 칸이 가로로 넓어도 이 비율을 지킨다.</summary>
        public const float LookSrcW = 142f;
        public const float LookSrcH = 128f;
        public const float StarterLabelH = 36f;

        /// <summary>모습 칸. 가로로 넓은 카드에 프레임을 늘리지 않는다(오너 21:50).</summary>
        public static Rect LookDest(Rect target, float srcW = 0f, float srcH = 0f)
        {
            float w = srcW > 0f ? srcW : LookSrcW;
            float h = srcH > 0f ? srcH : LookSrcH;
            var inner = new Rect(target.x + 4f, target.y + 4f, target.width - 8f, target.height - 8f);
            return UiAtlas.FitInside(inner, w, h);
        }

        /// <summary>전신. 스프라이트 비율로만 그린다. 빈 베이지 판을 넓게 깔지 않는다.</summary>
        public static void DrawJobLook(Rect target, string job, bool walk, Color? tint = null)
        {
            var saved = GUI.color;
            string frame = JobLookFrame(walk);
            var tex = Resources.Load<Texture2D>(LookPath(job, frame));
            if (tex == null && walk)
                tex = Resources.Load<Texture2D>(LookPath(job, IdleFrame));
            float sw = tex != null ? tex.width : LookSrcW;
            float sh = tex != null ? tex.height : LookSrcH;
            var dest = LookDest(target, sw, sh);
            UiAtlas.DrawRosterFrame(dest);
            var inner = new Rect(dest.x + 4f, dest.y + 4f, dest.width - 8f, dest.height - 8f);
            if (tex != null)
            {
                GUI.color = tint ?? Color.white;
                GUI.DrawTexture(inner, tex, ScaleMode.ScaleToFit, true);
                GUI.color = saved;
                return;
            }
            PortraitAtlas.Draw(inner, PortraitAtlas.KeyForJob(job), tint);
        }

        /// <summary>
        /// 같은 크기 카드를 앞에서부터 채운다. 마지막 줄이 모자라면 가운데.
        /// 3×2에 5장을 넣으면 빈 6번째 칸이 생긴다(오너 21:45).
        /// </summary>
        public static Rect[] PackedCards(Rect r, int count, int cols = 3, float gap = 12f)
        {
            if (count < 1) count = 1;
            if (cols < 1) cols = 1;
            int rows = (count + cols - 1) / cols;
            float ch = (r.height - gap * (rows - 1)) / rows;
            float cw = (r.width - gap * (cols - 1)) / cols;
            var cells = new Rect[count];
            int i = 0;
            for (int y = 0; y < rows; y++)
            {
                int n = Mathf.Min(cols, count - i);
                float used = n * cw + (n - 1) * gap;
                float x0 = r.x + (r.width - used) * 0.5f;
                for (int x = 0; x < n; x++, i++)
                    cells[i] = new Rect(x0 + x * (cw + gap), r.y + y * (ch + gap), cw, ch);
            }
            return cells;
        }

        public static Rect[] JobPickCards(Rect r, int count) => PackedCards(r, count, 3, 12f);

        public static Rect[] StarterPickCards(Rect r) => JobPickCards(r, 5);

        public static Rect[] Grid(Rect r, int cols, int rows, float gap = 16f)
        {
            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;
            var cells = new Rect[cols * rows];
            float cw = (r.width - gap * (cols - 1)) / cols;
            float ch = (r.height - gap * (rows - 1)) / rows;
            for (int i = 0; i < cells.Length; i++)
            {
                int x = i % cols;
                int y = i / cols;
                cells[i] = new Rect(r.x + x * (cw + gap), r.y + y * (ch + gap), cw, ch);
            }
            return cells;
        }

        /// <summary>
        /// 허브 카드 안 아이콘·제목 좌표. 높은 카드는 아이콘이 위를 채우고,
        /// 넓은 카드는 왼쪽에 붙는다 — 64px 아이콘을 위에만 두면 카드가 비어 보인다.
        /// </summary>
        public const float CardMinIcon = 72f;
        /// <summary>한글 제목은 fontSize보다 칸이 커야 획이 안 잘린다.</summary>
        public const float CardTitleH = 36f;

        public static void CardLayout(Rect card, bool hasIcon, out Rect icon, out Rect title, out Rect sub)
        {
            icon = default;
            bool tall = hasIcon && card.height >= 168f;
            if (tall)
            {
                float plateH = Mathf.Clamp(card.height * 0.38f, 80f, 120f);
                float maxIcon = Mathf.Max(24f, Mathf.Min(card.width - 28f, card.height - plateH - 16f));
                float size = Mathf.Min(Mathf.Max(CardMinIcon, maxIcon), maxIcon);
                icon = new Rect(card.center.x - size * 0.5f, card.y + 8f, size, size);
                if (icon.yMax > card.yMax - plateH)
                    icon.height = Mathf.Max(24f, card.yMax - plateH - icon.y);
                title = new Rect(card.x + 14f, card.yMax - plateH + 8f, card.width - 28f, CardTitleH);
                sub = new Rect(card.x + 14f, card.yMax - plateH + 8f + CardTitleH,
                    card.width - 28f, Mathf.Max(20f, plateH - CardTitleH - 16f));
                return;
            }

            float side = hasIcon
                ? Mathf.Min(card.height - 16f, 96f, Mathf.Max(CardMinIcon, card.width * 0.22f))
                : 0f;
            if (hasIcon)
                icon = new Rect(card.x + 12f, card.y + (card.height - side) * 0.5f, side, side);
            float tx = card.x + (hasIcon ? side + 22f : 16f);
            float tw = card.xMax - tx - 14f;
            title = new Rect(tx, card.y + 10f, tw, CardTitleH);
            sub = new Rect(tx, card.y + 12f + CardTitleH, tw,
                Mathf.Max(20f, card.height - CardTitleH - 22f));
        }

        /// <summary>칸 밖으로 글자가 새거나 옆 카드와 겹치지 않게 자른다.</summary>
        public static void LabelClip(Rect r, string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text) || r.width < 2f || r.height < 2f || style == null)
                return;
            var clip = new GUIStyle(style) { clipping = TextClipping.Clip };
            GUI.BeginGroup(r);
            GUI.Label(new Rect(0f, 0f, r.width, r.height), text, clip);
            GUI.EndGroup();
        }

        public static bool LayoutOverlaps(Rect a, Rect b) =>
            a.width > 1f && b.width > 1f && a.Overlaps(b);
    }
}
