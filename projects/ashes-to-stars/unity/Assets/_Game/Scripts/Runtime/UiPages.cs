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
        public const float EquipSlotSize = 48f;
        public const float EquipLabelH = 20f;
        public const float EquipLookPad = 64f;

        /// <summary>링 반지름을 패널 안에 맞게 줄인다. 안 줄이면 왼쪽 칸이 박스 밖으로 나간다.</summary>
        public static void EquipRingFit(Rect stage, Rect face, out float ringX, out float ringY)
        {
            float half = EquipSlotSize * 0.5f;
            const float pad = 8f;
            float cx = face.center.x;
            float cy = face.center.y;
            ringX = Mathf.Min(face.width * 0.50f + 16f,
                cx - stage.x - half - pad,
                stage.xMax - cx - half - pad);
            ringY = Mathf.Min(face.height * 0.42f + 16f,
                cy - stage.y - half - pad - 52f,
                stage.yMax - cy - half - pad - EquipLabelH);
            ringX = Mathf.Max(24f, ringX);
            ringY = Mathf.Max(24f, ringY);
        }

        public static Rect ClampIn(Rect box, Rect inner)
        {
            if (inner.width > box.width) inner.width = box.width;
            if (inner.height > box.height) inner.height = box.height;
            float x = Mathf.Clamp(inner.x, box.x, box.xMax - inner.width);
            float y = Mathf.Clamp(inner.y, box.y, box.yMax - inner.height);
            return new Rect(x, y, inner.width, inner.height);
        }

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
            var inner = UiAtlas.ContentRect(cell, "panel", 2f);
            float plate = Mathf.Min(RosterPlate, Mathf.Max(44f, inner.height * 0.48f));
            float faceW = Mathf.Min(72f, inner.width,
                Mathf.Max(28f, inner.height - plate));
            face = new Rect(inner.center.x - faceW * 0.5f, inner.y, faceW, faceW);
            name = new Rect(inner.x, face.yMax + 2f, inner.width, 18f);
            job = new Rect(inner.x, face.yMax + 20f, inner.width, 16f);
            hearts = new Rect(inner.center.x - 36f, inner.yMax - 20f, 72f, 20f);
            if (job.yMax > hearts.y)
                job.height = Mathf.Max(0f, hearts.y - job.y);
            if (name.yMax > job.y && job.height > 1f)
                name.height = Mathf.Max(0f, job.y - name.y);
        }

        /// <summary>
        /// 편성·사냥 선택 카드. 가로로 넓은 칸에 초상을 가운데 두면
        /// 이름이 초상 한가운데로 들어간다(오너 08:37).
        /// </summary>
        public static void PartyCardLayout(Rect cell, out Rect face, out Rect name, out Rect marks)
        {
            const float markW = 80f;
            var inner = UiAtlas.ContentRect(cell, "panel", 2f);
            if (IsWideCard(cell, 1.35f))
            {
                float faceS = Mathf.Min(inner.height, 88f);
                face = new Rect(inner.x, inner.y + (inner.height - faceS) * 0.5f, faceS, faceS);
                marks = new Rect(inner.xMax - markW,
                    inner.y + (inner.height - 22f) * 0.5f, markW, 22f);
                float tx = face.xMax + 10f;
                float tw = marks.x - tx - 8f;
                float nameH = Mathf.Min(44f, inner.height);
                if (tw < 48f)
                {
                    marks = new Rect(tx, inner.yMax - 20f, inner.xMax - tx, 20f);
                    name = new Rect(tx, inner.y, inner.xMax - tx,
                        Mathf.Max(16f, marks.y - inner.y - 2f));
                    return;
                }
                name = new Rect(tx, inner.y + (inner.height - nameH) * 0.5f, tw, nameH);
                return;
            }

            float faceS2 = Mathf.Min(inner.width, Mathf.Max(28f, inner.height - 48f));
            face = new Rect(inner.center.x - faceS2 * 0.5f, inner.y, faceS2, faceS2);
            name = new Rect(inner.x, face.yMax + 2f, inner.width, 18f);
            marks = new Rect(inner.center.x - 40f, inner.yMax - 20f, markW, 20f);
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
            return new Rect(stage.x + EquipLookPad, y, w, h);
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
        /// 허브 카드 안 아이콘·제목 좌표. 높은(세로) 카드는 아이콘이 위를 채우고,
        /// 넓은 카드는 왼쪽에 붙인 뒤 글씨를 세로 가운데에 둔다.
        /// 높이만 보면 필드 2×3(≈596×169)이 세로로 분류돼 제목이 아래 테두리에 붙었다(오너 08:37).
        /// </summary>
        public const float CardMinIcon = 72f;
        /// <summary>한글 제목은 fontSize보다 칸이 커야 획이 안 잘린다.</summary>
        public const float CardTitleH = 36f;
        /// <summary>현황·필드 도크처럼 높이 110 미만인 가로 칸. 제목 36이면 부제가 0이 된다.</summary>
        public const float SlimCardH = 110f;
        public const float SlimTitleH = 22f;
        public const float SlimSubMin = 18f;
        /// <summary>이 비율 이상이면 가로 카드 — 높이 168을 넘어도 세로 배치하지 않는다.</summary>
        public const float CardWideAspect = 1.45f;

        public static bool IsWideCard(Rect card, float aspect = CardWideAspect) =>
            card.height > 1f && card.width >= card.height * aspect;

        public static bool IsSlimCard(Rect card) =>
            IsWideCard(card) && card.height < SlimCardH;

        public static float TitleHOf(Rect card) =>
            IsSlimCard(card) ? SlimTitleH : CardTitleH;

        /// <summary>슬림 도크는 두꺼운 panel 금테가 본문을 먹는다. 버튼 크롬이 더 얇다.</summary>
        public static string CardChrome(Rect card) =>
            IsSlimCard(card) ? "button_normal" : "panel";

        public static void CardLayout(Rect card, bool hasIcon, out Rect icon, out Rect title, out Rect sub)
        {
            icon = default;
            var inner = UiAtlas.ContentRect(card, CardChrome(card));
            bool wide = IsWideCard(card);
            bool tall = hasIcon && !wide && card.height >= 168f;
            float titleH = TitleHOf(card);
            if (tall)
            {
                float plateH = Mathf.Clamp(inner.height * 0.36f, 56f, 110f);
                float maxIcon = Mathf.Max(24f, Mathf.Min(inner.width, inner.height - plateH - 6f));
                float size = Mathf.Min(Mathf.Max(Mathf.Min(CardMinIcon, maxIcon), 24f), maxIcon);
                float gap = 4f;
                float subMin = 16f;
                float stackH = size + gap + CardTitleH + subMin;
                if (stackH > inner.height)
                    stackH = inner.height;
                float y0 = inner.y + Mathf.Max(0f, (inner.height - stackH) * 0.5f);
                icon = new Rect(inner.center.x - size * 0.5f, y0, size, size);
                title = new Rect(inner.x, icon.yMax + gap, inner.width, CardTitleH);
                if (title.yMax > inner.yMax)
                    title.height = Mathf.Max(18f, inner.yMax - title.y);
                sub = new Rect(inner.x, title.yMax, inner.width,
                    Mathf.Max(0f, inner.yMax - title.yMax));
                return;
            }

            float side = 0f;
            if (hasIcon)
            {
                side = Mathf.Min(inner.height, 96f,
                    Mathf.Max(Mathf.Min(CardMinIcon, inner.height), inner.width * 0.18f));
                icon = new Rect(inner.x, inner.y + (inner.height - side) * 0.5f, side, side);
            }
            float tx = inner.x + (hasIcon ? side + 10f : 0f);
            float tw = Mathf.Max(12f, inner.xMax - tx);
            float minSub = IsSlimCard(card) ? SlimSubMin : 16f;
            float subH = Mathf.Min(48f, Mathf.Max(minSub, inner.height - titleH - 4f));
            float blockH = titleH + 2f + subH;
            if (blockH > inner.height)
            {
                float th = Mathf.Min(titleH, Mathf.Max(18f, inner.height * 0.55f));
                title = new Rect(tx, inner.y, tw, th);
                sub = new Rect(tx, title.yMax, tw, Mathf.Max(0f, inner.yMax - title.yMax));
                return;
            }
            float ty = inner.y + (inner.height - blockH) * 0.5f;
            title = new Rect(tx, ty, tw, titleH);
            sub = new Rect(tx, ty + titleH + 2f, tw, subH);
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
