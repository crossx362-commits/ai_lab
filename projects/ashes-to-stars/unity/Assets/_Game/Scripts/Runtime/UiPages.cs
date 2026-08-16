using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 허브 페이지 좌표. 화면마다 줄을 쌓지 않고 같은 격자·탭 높이를 쓴다.
    /// </summary>
    public static class UiPages
    {
        public const float TabH = 42f;
        public const float TabGap = 10f;

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
        public const float RosterCellH = 118f;
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

        public static string WalkFrame() =>
            (Time.unscaledTime % 0.36f) < 0.18f ? "walk_00" : "walk_01";

        /// <summary>전신. walk면 idle/walk 두 장으로 걷는다. 어두운 판 위에서는 초상을 크게 깐다.</summary>
        public static void DrawJobLook(Rect target, string job, bool walk, Color? tint = null)
        {
            var saved = GUI.color;
            GUI.color = new Color(0.86f, 0.82f, 0.74f, 1f);
            if (Texture2D.whiteTexture != null)
                GUI.DrawTexture(target, Texture2D.whiteTexture);
            GUI.color = saved;
            UiAtlas.DrawRosterFrame(target);
            var inner = new Rect(target.x + 6f, target.y + 6f, target.width - 12f, target.height - 12f);
            string frame = walk ? WalkFrame() : "idle_00";
            var tex = Resources.Load<Texture2D>(LookPath(job, frame));
            if (tex == null && walk)
                tex = Resources.Load<Texture2D>(LookPath(job, "idle_00"));
            if (walk && tex != null)
            {
                GUI.color = tint ?? Color.white;
                GUI.DrawTexture(inner, tex, ScaleMode.ScaleToFit, true);
                GUI.color = saved;
                return;
            }
            if (!PortraitAtlas.Draw(inner, PortraitAtlas.KeyForJob(job), tint) && tex != null)
            {
                GUI.color = tint ?? Color.white;
                GUI.DrawTexture(inner, tex, ScaleMode.ScaleToFit, true);
                GUI.color = saved;
            }
        }

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

        public static void CardLayout(Rect card, bool hasIcon, out Rect icon, out Rect title, out Rect sub)
        {
            icon = default;
            bool tall = hasIcon && card.height >= 150f;
            if (tall)
            {
                float plateH = Mathf.Clamp(card.height * 0.34f, 72f, 108f);
                float maxIcon = Mathf.Min(card.width - 28f, card.height - plateH - 14f);
                float size = Mathf.Max(CardMinIcon, maxIcon);
                icon = new Rect(card.center.x - size * 0.5f, card.y + 10f, size, size);
                title = new Rect(card.x + 16f, card.yMax - plateH + 8f, card.width - 32f, 30f);
                sub = new Rect(card.x + 16f, card.yMax - plateH + 38f, card.width - 32f, plateH - 46f);
                return;
            }

            float side = hasIcon
                ? Mathf.Min(card.height - 16f, 96f, Mathf.Max(CardMinIcon, card.width * 0.22f))
                : 0f;
            if (hasIcon)
                icon = new Rect(card.x + 12f, card.y + (card.height - side) * 0.5f, side, side);
            float tx = card.x + (hasIcon ? side + 22f : 16f);
            float tw = card.xMax - tx - 14f;
            title = new Rect(tx, card.y + 10f, tw, 30f);
            sub = new Rect(tx, card.y + 42f, tw, Mathf.Max(20f, card.height - 54f));
        }
    }
}
