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
            // 패널 보더는 칸 높이 비례(0.24)라 칸을 키워도 안쪽이 안 늘어난다 — 상한 22로 묶는다.
            var inner = UiAtlas.ContentRect(cell, "panel", 2f, 22f);
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
            // 최후 방어: job이 눌려 소멸한 칸에서도 이름이 하트 행을 침범하지 않게 한다.
            // 하트는 Clip 없이 나중에 그려져 이름 끝글자를 덮는다(실측 2026-08-24).
            if (name.yMax > hearts.y - 1f)
                name.height = Mathf.Max(0f, hearts.y - 1f - name.y);
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

        /// <summary>1차 전직 전용 폴더. 파일이 없으면 기본 5직업으로 돌아간다.</summary>
        public static string DedicatedLookDir(string job) => job switch
        {
            "수호기사" => "guardian",
            "광전사" => "berserker",
            "검사" => "swordsman",
            "궁수" => "archer",
            "소환사" => "summoner",
            "사제" => "priest",
            "드루이드" => "druid",
            "음유시인" => "bard",
            "주술사" => "shaman",
            "정령사" => "elemental",
            _ => null,
        };

        /// <summary>기본 5직업 폴더. 전직 그림이 오기 전·전투 SpriteBank와 같다.</summary>
        public static string BaseLookDir(string job) => job switch
        {
            "탱" or "수호기사" or "광전사" => "tank",
            "힐" or "사제" or "드루이드" => "healer",
            "버퍼" or "음유시인" or "주술사" or "정령사" => "buffer",
            "마딜" or "마법사" or "소환사" => "mage",
            _ => "dps",
        };

        /// <summary>전투 idle 스프라이트 폴더. 초상이 아니라 전신 모습을 그릴 때 쓴다.</summary>
        /// <summary>
        /// QA 전용: 기본 5직업 그림을 강제로 본다.
        /// 전투 파티는 기본값이 1차 전직(`AdvancementTier.First`)이라 **기본직업 그림이
        /// 화면에 설 일이 없다** — 오너가 준 기본 5직업 스프라이트가 실제로 어떻게
        /// 그려지는지 눈으로 확인할 방법이 없었다(2026-08-18). 파일·이름·캔버스가
        /// 계약에 맞는 것과 화면에 제대로 나오는 것은 다르다.
        /// </summary>
        public static bool BaseLookForced =>
            System.Environment.GetEnvironmentVariable("QA_BASE_LOOK") == "1";

        /// <summary>
        /// 그림 폴더. **기본은 전직 전**이다(오너 지시 2026-08-18 "앞으로 기본을 전직 전으로 바꿔").
        ///
        /// 예전엔 전직 폴더(`guardian` 등)가 존재하기만 하면 무조건 그쪽을 썼다. `Job` enum이
        /// 전직명(수호기사·검사…)뿐이라 **모든 캐릭터가 항상 전직 그림**으로 그려졌고,
        /// 오너가 준 기본 5직업 스프라이트는 화면에 설 자리가 없었다. 전직 그림은 실제로
        /// 전직한 캐릭터만 쓴다 — 티어를 아는 호출부가 `LookDir(job, tier)`로 알려준다.
        /// </summary>
        public static string LookDir(string job) => BaseLookDir(job);

        /// <summary>
        /// ⚠️ 티어를 받지만 **지금은 항상 기본 그림**이다(2026-08-18).
        ///
        /// 전직 폴더(guardian·priest…)의 그림은 오너 지시로 **몹 계열로 돌렸다**
        /// (`mob_guardian` 등). 그걸 캐릭터가 계속 쓰면 화면에 "옛 캐릭터 이미지"가
        /// 그대로 나온다 — 오너가 전직한 캐릭터를 보고 반복해서 지적한 것이 이것이다.
        /// 캐릭터 아트는 오너가 준 기본 5직업 픽셀아트뿐이므로 전직도 그걸 쓴다.
        /// 전직 전용 그림이 새로 생기면 이 분기를 되살릴 것 — 인자는 그래서 남겨 둔다.
        /// </summary>
        public static string LookDir(string job, AdvancementTier tier) => BaseLookDir(job);

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
        /// <summary>
        /// 실측(LegacyRuntime.ttf, CardTextFitSelfCheck): 20px 글꼴 「사냥 시작」의 줄 높이는
        /// **26.79px**다. 옛 값 22는 글꼴보다 작아 받침이 통째로 잘렸다 — 26으로 올려도
        /// 0.79 모자란다. 글꼴을 바꾸면 이 숫자도 다시 재라.
        /// </summary>
        public const float SlimTitleH = 28f;
        public const float SlimSubMin = 20f;

        /// <summary>
        /// 카드 금테(9-slice) 두께 상한. 비율만 쓰면 짧은 축이 짧은 카드에서 본문이 굶는다.
        /// 슬림 도크(396×95)는 15.2 → 9, 큰 카드(328×152)는 36.5 → 18.
        /// <b>DrawSliced와 CardLayout이 같은 값을 받아야 한다</b> — 한쪽만 얇게 하면
        /// 글씨가 금테 위로 올라간다.
        /// </summary>
        public const float SlimCardPad = 9f;
        public const float BigCardPad = 18f;

        /// <summary>부제가 가져갈 수 있는 최대 높이. 18px 글꼴 세 줄(54.2)이 들어가야 한다.</summary>
        public const float SubMaxH = 56f;

        public static float CardPad(Rect card) => IsSlimCard(card) ? SlimCardPad : BigCardPad;

        /// <summary>
        /// 카드 글꼴 크기. **칸 높이와 같은 곳에서 읽어야** 점검이 진짜를 잰다 —
        /// GameScreen이 숫자를 따로 들고 있으면 CardTextFitSelfCheck가 다른 글꼴을 재게 된다.
        /// </summary>
        public const int CardTitleFont = 22;
        public const int CardSubFont = 18;
        public const int SlimTitleFont = 20;
        public const int SlimSubFont = 14;
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
            var inner = UiAtlas.ContentRect(card, CardChrome(card), UiAtlas.ContentExtra, CardPad(card));
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
            // 남는 높이는 전부 부제에 준다. 옛 식은 -4를 떼고 48에서 잘라, 실측상 두 줄
            // 34.8px가 필요한 슬림 부제에 30.6px만 줬다(§4·§6 같은 꼬리가 잘린 이유).
            float subH = Mathf.Min(SubMaxH, Mathf.Max(minSub, inner.height - titleH - 2f));
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

        /// <summary>글꼴을 줄여서라도 칸에 넣는다. 이 밑으로는 안 줄인다(읽을 수 없어진다).</summary>
        public const int LabelMinFont = 11;

        /// <summary>
        /// 칸에 안 들어가면 **자르는 대신 글꼴을 줄인다**(§16).
        ///
        /// 왜 자르면 안 되나: 도크 카드는 높이가 95로 고정인데 넣는 문구 길이는 상황마다
        /// 다르다 — 잠긴 카드는 런타임에 「잠김 — 」이 앞에 붙어 두 줄이 세 줄이 된다.
        /// 칸을 세 줄에 맞춰 키우면 도크가 필드를 더 가리고(§16 위반), 칸을 두 줄로 두면
        /// 세 번째 줄이 반쯤 잘린 채 남는다(오너가 지적한 그 화면). 글꼴을 줄이면
        /// **문구가 무엇이든** 잘리지 않는다 — 카피를 점검이 못 박지 않아도 된다.
        /// </summary>
        public static void LabelFit(Rect r, string text, GUIStyle style, int minFont = LabelMinFont)
        {
            if (string.IsNullOrEmpty(text) || r.width < 2f || r.height < 2f || style == null)
                return;
            var fit = new GUIStyle(style) { clipping = TextClipping.Clip };
            for (int f = style.fontSize; f > minFont; f--)
            {
                fit.fontSize = f;
                if (fit.CalcHeight(new GUIContent(text), r.width) <= r.height) break;
            }
            GUI.BeginGroup(r);
            GUI.Label(new Rect(0f, 0f, r.width, r.height), text, fit);
            GUI.EndGroup();
        }

        /// <summary>
        /// 주어진 칸에 들어가는 가장 큰 글꼴. LabelFit이 고르는 값과 같다 —
        /// 점검이 이 함수로 「minFont까지 줄여도 안 들어가는 문구」를 잡는다.
        /// </summary>
        public static int FittedFont(Rect r, string text, GUIStyle style, int minFont = LabelMinFont)
        {
            if (string.IsNullOrEmpty(text) || style == null) return style?.fontSize ?? 0;
            var fit = new GUIStyle(style);
            for (int f = style.fontSize; f > minFont; f--)
            {
                fit.fontSize = f;
                if (fit.CalcHeight(new GUIContent(text), Mathf.Max(4f, r.width)) <= r.height) return f;
            }
            return minFont;
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
