using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>
    /// 영지 — 허브. 경매장·대장간·영묘는 **영지 안 건물**로 들어간다(§16).
    /// 씬을 새로 파지 않고 하위 화면으로 두는 것이 기획 의도다 —
    /// "메뉴를 늘리지 않고 영지를 실제로 쓰게 만드는 배치"(§16).
    /// </summary>
    public class EstateScreen : GameScreen
    {
        enum Sub { 없음, 대장간, 경매장, 영묘, 수비대, 월드티어, 본성, 영공 }
        Sub _sub = Sub.없음;
        int _hubPage;
        EstateGrid.Cell _placeKind = EstateGrid.Cell.Wall;

        /// <summary>경매장 해금 층(§12). 침략과 동시 해금이다.</summary>
        public const int AuctionUnlockFloor = 30;

        protected override string Title => _sub == Sub.없음 ? "영지" : $"영지 · {_sub}";
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.Estate);
        protected override string BackgroundArt => "bg_estate";
        protected override string Subtitle => _sub switch
        {
            Sub.대장간 => Equipment.SmithUnlocked()
                ? "사냥해서 얻은 재료로 만든다. 강화는 실패해도 파괴되지 않는다(§11)"
                : Equipment.LockLine(),
            Sub.경매장 => "탑 30층 달성 시 오픈. 골드는 곧 목숨이라 거래가 성립한다(§12)",
            Sub.영묘 => Memorial.Unlocked
                ? Rebirth.MausoleumSubtitle()
                : Memorial.LockLine(),
            Sub.수비대 => DefenseState.Unlocked
                ? "최대 5명. 침략 때 수비가 적으면 약탈이 늘어난다(§13-5·§15)"
                : DefenseState.LockLine(),
            Sub.월드티어 => "해금한 티어 중 하나를 고르면 필드·던전·하위 레이드가 함께 움직인다(§6)",
            Sub.본성 => "본성 레벨이 다른 건물 상한과 창고 용량이다. 공사는 끝나면 자동 적용(§13-2)",
            Sub.영공 => "층을 오를수록 인식 범위가 넓어진다. 아군 버프·적 디버프를 켠다(§14)",
            _ => SoftCapHubSubtitle(),
        };

        static string SoftCapHubSubtitle()
        {
            if (System.Environment.GetEnvironmentVariable(Memorial.EnvShowUnlock) == "1"
                && !Memorial.UnlockBlocked)
                return Memorial.LockLine();
            if (System.Environment.GetEnvironmentVariable(DefenseState.EnvShowUnlock) == "1"
                && !DefenseState.UnlockBlocked)
                return DefenseState.LockLine();
            if (System.Environment.GetEnvironmentVariable(Equipment.EnvShowUnlock) == "1"
                && !Equipment.UnlockBlocked)
                return Equipment.LockLine();
            if (BankruptcySeize.ShowOnHub)
                return BankruptcySeize.Line();
            if (System.Environment.GetEnvironmentVariable(SoftCap.EnvShow) == "1"
                && !SoftCap.Blocked)
                return SoftCap.HourLine();
            if (TowerEnding.HasTitle)
                return $"{TowerEnding.TitleName} · 모든 콘텐츠의 출발점(§8·§16)";
            if (SoloRaidClear.HasAny)
                return $"{SoloRaidClear.LastTitle} · 모든 콘텐츠의 출발점(§8·§16)";
            return "모든 콘텐츠의 출발점. 건물을 눌러 들어간다 — 메뉴를 늘리지 않는다(§13·§16)";
        }

        /// <summary>
        /// 시각 QA가 **건물 안쪽까지** 찍을 수 있게 하는 진입점(`GAME_START=estate`).
        /// 이게 없으면 자동 조종은 허브만 찍고 끝나서, 정작 채운 화면은 한 번도 안 보인다 —
        /// 이 프로젝트에서 "렌더는 되는데 아무도 안 본 화면"이 반복해서 나온 이유다.
        /// </summary>
        public static string AutoOpen;

        protected override void Update()
        {
            // 하위 화면에서 ESC는 영지로 — 허브 밖으로 튕겨나가지 않게
            if (_sub != Sub.없음 && Input.GetKeyDown(KeyCode.Escape)) { _sub = Sub.없음; return; }
            base.Update();
        }

        protected override void Body(Rect r)
        {
            TowerEnding.SeedQaIfRequested();
            SoloRaidClear.SeedQaIfRequested();
            if (!string.IsNullOrEmpty(AutoOpen))
            {
                if (AutoOpen == "현황")
                    _hubPage = 1;
                else if (AutoOpen == "방어")
                    _hubPage = 2;
                else if (AutoOpen == "배치")
                    _hubPage = 3;
                else if (System.Enum.TryParse(AutoOpen, out Sub want))
                {
                    _sub = want;
                    if (want == Sub.대장간)
                    {
                        for (int i = 0; i < Equipment.Recipes.Length; i++)
                            GameState.Gain(Equipment.Recipes[i].Material, 4);
                    }
                }
                AutoOpen = null;                 // 한 번만 — 이후엔 사람이 조작한다
            }

            EstateBuild.Tick();
            EstateMine.Tick();
            EstateDefense.Tick();
            if (System.Environment.GetEnvironmentVariable("QA_ESTATE_MINE") == "1")
                _hubPage = 1;
            if (System.Environment.GetEnvironmentVariable("QA_ESTATE_DEFENSE") == "1")
                _hubPage = 2;
            if (System.Environment.GetEnvironmentVariable("QA_ESTATE_RUSH") == "1")
                _sub = Sub.본성;
            if (System.Environment.GetEnvironmentVariable("QA_ESTATE_GRID") == "1")
                _hubPage = 3;
            if (System.Environment.GetEnvironmentVariable("QA_WORLD_AURA") == "1")
                _sub = Sub.영공;
            if (System.Environment.GetEnvironmentVariable(WorldStar.EnvShowSense) == "1")
                _sub = Sub.영공;
            if (System.Environment.GetEnvironmentVariable(EstateMine.EnvShowRace) == "1")
                _hubPage = 1;
            if (System.Environment.GetEnvironmentVariable(EstateMine.EnvShowSeize) == "1")
                _hubPage = 1;
            if (System.Environment.GetEnvironmentVariable(BankruptcySeize.EnvShow) == "1")
                _hubPage = 1;
            if (System.Environment.GetEnvironmentVariable(SoftCap.EnvShow) == "1")
                _hubPage = 1;
            WorldStar.SeedRaceSenseQaIfRequested();
            SoftCap.SeedQaIfRequested();
            EstateMine.SeedQaIfRequested();
            EstateMine.SeedRaceQaIfRequested();
            EstateMine.SeedSeizeQaIfRequested();
            BankruptcySeize.SeedQaIfRequested();
            EstateDefense.SeedQaIfRequested();
            EstateBuild.SeedRushQaIfRequested();
            EstateGrid.SeedQaIfRequested();
            StarterSecond.SeedQaIfRequested();
            AuctionState.SeedQaIfRequested();
            AuctionState.SeedBuyLockQaIfRequested();
            AuctionState.SeedExpireQaIfRequested();
            Rebirth.SeedQaIfRequested();
            Memorial.SeedQaIfRequested();
            Memorial.SeedUnlockQaIfRequested();
            DefenseState.SeedUnlockQaIfRequested();
            Equipment.SeedUnlockQaIfRequested();
            if (System.Environment.GetEnvironmentVariable(Rebirth.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable(Memorial.EnvShow) == "1")
                _sub = Sub.영묘;
            if (System.Environment.GetEnvironmentVariable(AuctionState.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable(AuctionState.EnvShowBuyLock) == "1"
                || System.Environment.GetEnvironmentVariable(AuctionState.EnvShowExpire) == "1")
                _sub = Sub.경매장;
            if (StarterSecond.Pending)
            {
                Hint(new Rect(r.x, r.y, r.width, 24f), StarterSecond.PickTitle);
                var picks = UiPages.JobPickCards(new Rect(r.x, r.y + 28f, r.width, r.height - 28f),
                    LifeSystem.BasicJobs.Length);
                for (int i = 0; i < LifeSystem.BasicJobs.Length && i < picks.Length; i++)
                {
                    string job = LifeSystem.BasicJobs[i];
                    if (DrawCard(picks[i], LifeSystem.BasicJobLabel(job),
                            StarterSecond.PickSubtitle))
                        StarterSecond.TryClaim(job);
                }
                return;
            }
            if (_sub == Sub.본성) { Keep(r); return; }
            if (_sub == Sub.영묘) { Mausoleum(r); return; }
            if (_sub == Sub.대장간) { Smith(r); return; }
            if (_sub == Sub.수비대) { Barracks(r); return; }
            if (_sub == Sub.월드티어) { WorldTier(r); return; }
            if (_sub == Sub.영공) { Aura(r); return; }

            if (_sub == Sub.경매장) { AuctionHouse(r); return; }

            if (_sub != Sub.없음)
            {
                Info(r, 0, "아직 내용이 없다 — 수직 슬라이스에서 채운다(§21-2)");
                if (Row(r, 1, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
                return;
            }

            _hubPage = DrawTabs(r, new[] { "건물", "현황", "방어", "배치" }, _hubPage);
            var page = UiPages.AfterTabs(r);
            if (_hubPage == 1)
            {
                DrawEstateStatus(page);
                return;
            }
            if (_hubPage == 2)
            {
                DrawDefense(page);
                return;
            }
            if (_hubPage == 3)
            {
                DrawLayout(page);
                return;
            }

            var cards = UiPages.Grid(page, 2, 2, 16f);
            string smithLock = Equipment.LockReason();
            if (DrawCard(cards[0], "대장간",
                    smithLock ?? "제작·강화. 실패해도 장비는 남는다",
                    UiAtlas.BuildingKey("대장간"), locked: smithLock != null))
                _sub = Sub.대장간;
            string auctionLock = AuctionHubLockReason();
            string auctionSub = auctionLock
                ?? (AuctionState.CanBuy()
                    ? "로컬 장 · " + AuctionState.FeeLine()
                    : AuctionState.BuyLockLine());
            if (DrawCard(cards[1], "경매장",
                    auctionSub,
                    UiAtlas.BuildingKey("경매장"), locked: auctionLock != null))
                _sub = Sub.경매장;
            string mausoleumLock = Memorial.LockReason();
            if (DrawCard(cards[2], "영묘",
                    mausoleumLock
                        ?? $"환생석 {LifeSystem.GetRebornStones()}개 · 삭제된 캐릭터만",
                    UiAtlas.BuildingKey("영묘"), locked: mausoleumLock != null))
                _sub = Sub.영묘;
            string barracksLock = DefenseState.LockReason();
            if (DrawCard(cards[3], "수비대",
                    barracksLock
                        ?? $"배치 {DefenseState.Count}/{DefenseState.MaxSlots} · 출전에서 빠진다",
                    UiAtlas.BuildingKey("수비대"), locked: barracksLock != null))
                _sub = Sub.수비대;
        }

        void DrawEstateStatus(Rect r)
        {
            string auraSub = WorldStar.SizeLabel(GameState.TowerFloor) + " · " + WorldStar.AuraLabel();
            if (WorldStar.RaceSensePercent() == WorldStar.ElfSensePercent)
                auraSub = WorldStar.RaceSenseLine() + " · " + auraSub;
            var aura = new Rect(r.x, r.y, r.width, 80f);
            if (DrawCard(aura, "내 별 영공", auraSub, "worldmap"))
                _sub = Sub.영공;
            var cards = UiPages.Grid(new Rect(r.x, r.y + 92f, r.width, r.height - 92f), 2, 2, 16f);
            string keepSub = EstateBuild.KeepBusy
                ? $"Lv{EstateBuild.KeepLevel} → {EstateBuild.KeepTarget} · 남은 {EstateBuild.RemainingText()}"
                : $"Lv{EstateBuild.KeepLevel} · 창고 {Economy.FormatCurrency(EstateBuild.WarehouseCapCopper())}";
            if (BankruptcySeize.DidDowngrade ||
                System.Environment.GetEnvironmentVariable(BankruptcySeize.EnvShow) == "1")
                keepSub += " · " + BankruptcySeize.KeepLine();
            if (DrawCard(cards[0], "본성", keepSub, "territory"))
                _sub = Sub.본성;
            bool canPick = GameState.UnlockedTier > 0;
            if (DrawCard(cards[1], $"세계 T{GameState.Tier + 1}",
                    canPick
                        ? $"해금 T{GameState.UnlockedTier + 1} · 탑 {GameState.TowerFloor}층 — 눌러 고른다"
                        : $"해금 T1 · 탑 {GameState.TowerFloor}층 — 10층 돌파 시 T2",
                    "tower", locked: !canPick))
                _sub = Sub.월드티어;
            string mineRate = Economy.FormatCurrency(EstateMine.CopperPerHourEffective()) + "/h";
            string mineSub = mineRate + " · 창고에 자동 적립(§13)";
            if (EstateMine.Seized)
                mineSub = mineRate + " · " + EstateMine.SeizeLine();
            else if (EstateMine.RacePercent() != EstateMine.HumanPercent)
                mineSub = mineRate + " · " + EstateMine.RaceLine();
            DrawCard(cards[2], "광산", mineSub, "field", locked: true);
            string warehouse = $"{Economy.FormatCurrency(GameState.Wallet.Copper)} / {Economy.FormatCurrency(EstateBuild.WarehouseCapCopper())}";
            if (BankruptcySeize.DidSeize ||
                System.Environment.GetEnvironmentVariable(BankruptcySeize.EnvShow) == "1")
                warehouse += " · " + BankruptcySeize.ItemLine();
            else if (!SoftCap.Blocked
                && (System.Environment.GetEnvironmentVariable(SoftCap.EnvShow) == "1"
                    || SoftCap.EarnedThisHour > 0))
                warehouse += " · " + SoftCap.HourLine();
            else if (EstateMine.WastedCopper > 0)
                warehouse += $" · 넘친 {Economy.FormatCurrency(EstateMine.WastedCopper)} 소멸";
            else
                warehouse += " · 넘치면 소멸";
            DrawCard(cards[3], "창고", warehouse, "building_auction", locked: true);
        }

        void Aura(Rect r)
        {
            string auraInfo = "내 별 · " + WorldStar.SizeLabel(GameState.TowerFloor)
                + " · " + WorldStar.AuraLabel();
            if (WorldStar.RaceSensePercent() == WorldStar.ElfSensePercent)
                auraInfo = WorldStar.RaceSenseLine() + " · " + auraInfo;
            Info(r, 0, auraInfo);
            var cards = UiPages.Grid(new Rect(r.x, r.y + 80f, r.width, r.height - 168f), 2, 1, 16f);
            if (DrawCard(cards[0], WorldStar.AllyBuff ? "아군 버프 켜짐" : "아군 버프 꺼짐",
                    "광산 생산 ×" + WorldStar.AllyMul.ToString("0.00") + "(§14)",
                    "healer"))
                WorldStar.AllyBuff = !WorldStar.AllyBuff;
            if (DrawCard(cards[1], WorldStar.EnemyDebuff ? "적 디버프 켜짐" : "적 디버프 꺼짐",
                    WorldStar.EnemyLine() + " · 침략 약탈 ×" + WorldStar.EnemyMul.ToString("0.00") + "(§14)",
                    "damage"))
                WorldStar.EnemyDebuff = !WorldStar.EnemyDebuff;
            if (Row(r, 4, "← 영지로", "영공에서 나온다")) _sub = Sub.없음;
        }

        void DrawLayout(Rect r)
        {
            EstateGrid.SeedQaIfRequested();
            var side = EstateGrid.InvaderSide();
            int path = EstateGrid.InvaderPath();
            Info(r, 0,
                $"침략 진입 {side} {path}칸 · 북{Len(EstateGrid.Side.북)} 동{Len(EstateGrid.Side.동)} 남{Len(EstateGrid.Side.남)} 서{Len(EstateGrid.Side.서)}(§13-3)");
            string stock =
                $"성벽 {EstateGrid.Count(EstateGrid.Cell.Wall)}/{EstateDefense.Level(EstateDefense.Kind.성벽)}"
                + $" · 함정 {EstateGrid.Count(EstateGrid.Cell.Trap)}/{EstateDefense.Level(EstateDefense.Kind.함정)}"
                + $" · 화살 {EstateGrid.Count(EstateGrid.Cell.Arrow)}/{EstateDefense.Level(EstateDefense.Kind.화살탑)}"
                + $" · 마법 {EstateGrid.Count(EstateGrid.Cell.Magic)}/{EstateDefense.Level(EstateDefense.Kind.마법탑)}";
            Info(r, 1, stock);

            float top = 2 * (RowH + RowGap) + 6f;
            float bot = 78f;
            var board = new Rect(r.x, r.y + top, r.width, Mathf.Max(80f, r.height - top - bot));
            float cell = Mathf.Min(58f, Mathf.Min((board.width - 100f) / EstateGrid.Size,
                (board.height - 40f) / EstateGrid.Size));
            if (cell < 28f) cell = 28f;
            float gridW = cell * EstateGrid.Size;
            float gridH = cell * EstateGrid.Size;
            float gx = board.x + (board.width - gridW) * 0.5f;
            float gy = board.y + 20f;
            DrawSideTag(new Rect(gx, board.y, gridW, 18f),
                $"북 {Len(EstateGrid.Side.북)}", EstateGrid.Side.북 == side);
            DrawSideTag(new Rect(gx, gy + gridH, gridW, 18f),
                $"남 {Len(EstateGrid.Side.남)}", EstateGrid.Side.남 == side);
            DrawSideTag(new Rect(gx - 44f, gy, 42f, gridH),
                $"서\n{Len(EstateGrid.Side.서)}", EstateGrid.Side.서 == side);
            DrawSideTag(new Rect(gx + gridW + 2f, gy, 42f, gridH),
                $"동\n{Len(EstateGrid.Side.동)}", EstateGrid.Side.동 == side);

            var mark = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = cell >= 40f ? 16 : 13,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            for (int y = 0; y < EstateGrid.Size; y++)
            for (int x = 0; x < EstateGrid.Size; x++)
            {
                var box = new Rect(gx + x * cell, gy + y * cell, cell - 2f, cell - 2f);
                var c = EstateGrid.At(x, y);
                bool onPath = EstateGrid.OnInvaderPath(x, y);
                FillCell(box, CellTint(c, onPath));
                mark.normal.textColor = CellInk(c, onPath);
                GUI.Label(box, CellMark(c), mark);
                if (!GUI.Button(box, GUIContent.none, GUIStyle.none)) continue;
                if (EstateGrid.IsDefense(c))
                    EstateGrid.TryPickUp(x, y);
                else
                    EstateGrid.TryPlace(x, y, _placeKind);
            }

            var kinds = EstateDefense.All;
            float bw = (r.width - 12f) / kinds.Length;
            for (int i = 0; i < kinds.Length; i++)
            {
                var k = kinds[i];
                var cellKind = EstateGrid.CellOf(k);
                var b = new Rect(r.x + i * bw, r.yMax - 70f, bw - 8f, 62f);
                int left = EstateGrid.Unplaced(cellKind);
                bool on = _placeKind == cellKind;
                string title = on ? $"선택 {k} · {left}" : $"{k} · {left}";
                string desc = left > 0 ? "빈 칸에 놓는다" : "레벨만큼만 놓는다";
                if (DrawCard(b, title, desc, UiAtlas.BuildingKey(k.ToString())))
                    _placeKind = cellKind;
            }
        }

        static string Len(EstateGrid.Side s)
        {
            int n = EstateGrid.PathLength(s);
            return n < 0 ? "막힘" : n + "칸";
        }

        static void DrawSideTag(Rect box, string text, bool hot)
        {
            var st = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = hot ? FontStyle.Bold : FontStyle.Normal,
                clipping = TextClipping.Clip,
                normal = { textColor = hot
                    ? new Color(0.92f, 0.42f, 0.22f)
                    : new Color(0.35f, 0.28f, 0.22f) },
            };
            GUI.Label(box, text, st);
        }

        static string CellMark(EstateGrid.Cell c) => c switch
        {
            EstateGrid.Cell.Keep => "본",
            EstateGrid.Cell.Mine => "광",
            EstateGrid.Cell.Warehouse => "창",
            EstateGrid.Cell.Arrow => "화",
            EstateGrid.Cell.Magic => "마",
            EstateGrid.Cell.Wall => "벽",
            EstateGrid.Cell.Trap => "함",
            _ => "",
        };

        static Color CellTint(EstateGrid.Cell c, bool onPath) => c switch
        {
            EstateGrid.Cell.Keep => new Color(0.93f, 0.74f, 0.22f, 1f),
            EstateGrid.Cell.Mine => new Color(0.62f, 0.44f, 0.24f, 1f),
            EstateGrid.Cell.Warehouse => new Color(0.28f, 0.58f, 0.86f, 1f),
            EstateGrid.Cell.Wall => new Color(0.32f, 0.30f, 0.28f, 1f),
            EstateGrid.Cell.Arrow => new Color(0.82f, 0.28f, 0.18f, 1f),
            EstateGrid.Cell.Magic => new Color(0.52f, 0.30f, 0.78f, 1f),
            EstateGrid.Cell.Trap => new Color(0.62f, 0.16f, 0.16f, 1f),
            _ => onPath
                ? new Color(0.96f, 0.62f, 0.22f, 1f)
                : new Color(0.88f, 0.82f, 0.70f, 1f),
        };

        static Color CellInk(EstateGrid.Cell c, bool onPath)
        {
            if (c == EstateGrid.Cell.Empty)
                return onPath ? new Color(0.35f, 0.14f, 0.04f) : new Color(0.40f, 0.32f, 0.22f);
            return Color.white;
        }

        static void FillCell(Rect box, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawDefense(Rect r)
        {
            EstateDefense.Tick();
            string eff = GameState.TowerFloor < EstateDefense.UnlockFloor
                ? $"탑 {EstateDefense.UnlockFloor}층부터 순차 해금(현재 {GameState.TowerFloor}층)"
                : $"수비 {DefenseState.Count}명 · 효율 {EstateDefense.EfficiencyPercent()}% · 약탈 -{EstateDefense.CutPercent()}%(§13-5)";
            Info(r, 0, eff);
            float top = 80f;
            if (EstateDefense.Busy)
            {
                int row = 1;
                DrawDefenseRush(r, ref row);
                top = row * (RowH + RowGap);
            }
            var cards = UiPages.Grid(new Rect(r.x, r.y + top, r.width, r.height - top - 8f), 2, 2, 16f);
            var kinds = EstateDefense.All;
            for (int i = 0; i < kinds.Length && i < cards.Length; i++)
            {
                var k = kinds[i];
                int lv = EstateDefense.Level(k);
                string title = $"{k} · Lv{lv}";
                string icon = UiAtlas.BuildingKey(k.ToString());
                if (EstateDefense.BusyKind == k)
                {
                    DrawCard(cards[i], title,
                        $"공사 중 → Lv{lv + 1} · 남은 {EstateDefense.RemainingText()}",
                        icon, locked: true);
                    continue;
                }
                string why = EstateDefense.WhyCannotStart(k);
                string desc = $"{Economy.FormatCurrency(EstateDefense.UpgradeCost(lv))} · {FormatWait(EstateDefense.UpgradeSeconds(lv))}";
                if (why != null)
                    DrawCard(cards[i], title, why, icon, locked: true);
                else if (DrawCard(cards[i], title, desc, icon))
                    EstateDefense.TryStart(k);
            }
        }

        void Keep(Rect r)
        {
            EstateBuild.Tick();
            int lv = EstateBuild.KeepLevel;
            Info(r, 0, $"본성 Lv{lv} · 창고 {Economy.FormatCurrency(EstateBuild.WarehouseCapCopper())}(§18-12)");
            int row = 1;
            if (EstateBuild.KeepBusy)
            {
                Info(r, row++, $"공사 중 Lv{lv} → {EstateBuild.KeepTarget} · 남은 {EstateBuild.RemainingText()}");
                Info(r, row++, "끝나면 자동 적용 — 수령할 필요 없다. 단축은 남은 50%까지(§13-2)");
                DrawKeepRush(r, ref row);
            }
            else
            {
                string why = EstateBuild.WhyCannotUpgrade();
                string label = $"Lv{lv} → {lv + 1}";
                string desc = $"{Economy.FormatCurrency(EstateBuild.UpgradeCost(lv))} · {FormatWait(EstateBuild.UpgradeSeconds(lv))}";
                if (why != null)
                    Locked(r, row++, label, why, "territory");
                else if (Row(r, row++, label, desc, "territory"))
                    EstateBuild.TryStartKeep();
            }
            if (Row(r, row, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
        }

        void DrawKeepRush(Rect r, ref int row)
        {
            long cut = EstateBuild.RushableSeconds();
            string goldWhy = EstateBuild.WhyCannotRushGold();
            string goldLabel = $"골드 단축 · {Economy.FormatCurrency(EstateBuild.GoldCostToFloor())}";
            string goldDesc = cut > 0
                ? $"남은 {cut}초를 당긴다. 바닥은 원 소요의 50%(§13-2)"
                : "남은 시간의 50%가 바닥이다 — 완전 스킵 불가";
            if (goldWhy != null)
                Locked(r, row++, goldLabel, goldWhy, "gold");
            else if (Row(r, row++, goldLabel, goldDesc, "gold"))
                EstateBuild.TryRushGold();

            var mat = EstateRush.FirstOwnedFamilyMaterial();
            if (mat == null)
            {
                Locked(r, row++, "재료 단축",
                    "계열 재료 1개 = 남은 시간의 2%. 부활초·환생석·두루마리는 안 된다",
                    ItemAtlas.KeyFor(Economy.LifeItem.CraftHide));
                return;
            }
            var item = mat.Value;
            string matWhy = EstateBuild.WhyCannotRushMaterial(item);
            string matLabel = $"{GameState.Label(item)} 1장 단축";
            string matDesc = $"남은 시간의 2% · {GameState.Bag.GetCount(item)}장";
            if (matWhy != null)
                Locked(r, row++, matLabel, matWhy, ItemAtlas.KeyFor(item));
            else if (Row(r, row++, matLabel, matDesc, ItemAtlas.KeyFor(item)))
                EstateBuild.TryRushMaterial(item, 1);
        }

        void DrawDefenseRush(Rect r, ref int row)
        {
            long cut = EstateDefense.RushableSeconds();
            string goldWhy = EstateDefense.WhyCannotRushGold();
            string goldLabel = $"골드 단축 · {Economy.FormatCurrency(EstateDefense.GoldCostToFloor())}";
            if (goldWhy != null)
                Locked(r, row++, goldLabel, goldWhy, "gold");
            else if (Row(r, row++, goldLabel,
                    cut > 0 ? $"방어 공사 {cut}초 · 상한 50%" : "바닥", "gold"))
                EstateDefense.TryRushGold();

            var mat = EstateRush.FirstOwnedFamilyMaterial();
            if (mat == null)
            {
                Locked(r, row++, "재료 단축", "계열 재료가 없다", ItemAtlas.KeyFor(Economy.LifeItem.CraftHide));
                return;
            }
            var item = mat.Value;
            string matWhy = EstateDefense.WhyCannotRushMaterial(item);
            if (matWhy != null)
                Locked(r, row++, $"{GameState.Label(item)} 1장 단축", matWhy, ItemAtlas.KeyFor(item));
            else if (Row(r, row++, $"{GameState.Label(item)} 1장 단축",
                    "남은 시간의 2%", ItemAtlas.KeyFor(item)))
                EstateDefense.TryRushMaterial(item, 1);
        }

        static string FormatWait(double seconds)
        {
            if (seconds >= 3600) return $"{seconds / 3600.0:0.#}시간";
            if (seconds >= 60) return $"{seconds / 60.0:0.#}분";
            return $"{seconds:0}초";
        }

        void WorldTier(Rect r)
        {
            Info(r, 0,
                $"해금 T{GameState.UnlockedTier + 1} · 탑 {GameState.TowerFloor}층 · 최고 기록은 안 내려간다(§6)");
            int unlocked = GameState.UnlockedTier;
            var cells = UiPages.Grid(new Rect(r.x, r.y + 80f, r.width, r.height - 180f), 5, 2, 10f);
            for (int i = 0; i < cells.Length; i++)
            {
                string pay = Economy.FormatCurrency((long)(Economy.TierRevenueMultiplier[i] * 10000f)) + "/h";
                if (i > unlocked)
                {
                    DrawCard(cells[i], $"T{i + 1}",
                        $"탑 {i * 10 + 1}층에서 해금 — 현재 {GameState.TowerFloor}층",
                        "tower", locked: true);
                    continue;
                }
                bool current = i == GameState.Tier;
                if (DrawCard(cells[i], $"T{i + 1} · {pay}",
                        current ? "현재 세계 — 필드·던전·하위 레이드" : "이 티어로 세계를 맞춘다",
                        "tower"))
                    GameState.TrySelectTier(i);
            }
            var back = UiPages.Grid(new Rect(r.x, r.yMax - 88f, r.width, 80f), 1, 1, 10f);
            if (DrawCard(back[0], "영지로", "건물에서 나온다", "territory")) _sub = Sub.없음;
        }

        /// <summary>
        /// 허브 경매 버튼 잠금 사유. null이면 들어간다.
        /// 부채·연체·파산 정지가 층 게이트보다 앞선다 — 열려 보이면 안 된다.
        /// </summary>
        public static string AuctionHubLockReason(long nowUnix)
        {
            if (!GameState.CanUseAuction(nowUnix))
                return GameState.AuctionBlockReason(nowUnix);
            if (GameState.TowerFloor < AuctionUnlockFloor)
                return $"탑 {AuctionUnlockFloor}층 달성 시 해금(현재 {GameState.TowerFloor}층) — 30층 미만은 초보 보호(§12)";
            return null;
        }
        public static string AuctionHubLockReason() => AuctionHubLockReason(
            System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        void AuctionHouse(Rect r)
        {
            int row = 0;
            string lockReason = AuctionHubLockReason();
            if (lockReason != null)
            {
                Info(r, row++, lockReason);
                if (Row(r, row, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
                return;
            }

            Info(r, row++,
                $"로컬 장 · {Economy.FormatCurrency(GameState.Wallet.Copper)} · {AuctionState.FeeLine()}. 다른 유저 서버 아님.");
            Info(r, row++, AuctionState.MineLine());
            string buyLock = AuctionState.BuyLockLine();
            if (!string.IsNullOrEmpty(buyLock))
                Info(r, row++, buyLock);
            if (!string.IsNullOrEmpty(_msg))
                Info(r, row++, _msg);

            var lots = AuctionState.Lots;
            string buyWhy = AuctionState.WhyCannotBuy();
            void DrawLot(AuctionState.Lot lot)
            {
                if (row >= 8) return;
                string who = lot.Npc ? "장" : "내 등록";
                if (lot.Npc)
                {
                    if (Row(r, row++, $"구매 {lot.Label}",
                            buyWhy ?? $"{who} · {Economy.FormatCurrency(lot.Price)}",
                            ItemAtlas.KeyFor(ParseLotItem(lot))))
                    {
                        _msg = AuctionState.TryBuy(lot.Id)
                            ? $"{lot.Label} 구매"
                            : (buyWhy ?? "구매 실패 — 골드 부족이거나 상한");
                    }
                    return;
                }
                if (Row(r, row++, $"취소 {lot.Label}",
                        $"{who} · {Economy.FormatCurrency(lot.Price)} · {AuctionState.LotTimeLine(lot)}"))
                    _msg = AuctionState.TryCancel(lot.Id) ? "등록 취소 · 수수료는 소각" : "취소 실패";
            }
            for (int i = 0; i < lots.Count; i++)
                if (!lots[i].Npc) DrawLot(lots[i]);
            for (int i = 0; i < lots.Count; i++)
                if (lots[i].Npc) DrawLot(lots[i]);

            var bag = Equipment.Unequipped();
            if (bag.Count > 0 && row < 8)
            {
                var g = bag[0];
                long price = 12_000 + g.Enhance * 2_000;
                if (Row(r, row++, $"등록 {g.Name}",
                        $"수수료 {Economy.FormatCurrency(AuctionState.ListFee(price))} · {Economy.FormatCurrency(price)}"))
                {
                    _msg = AuctionState.TryListGear(g.Id, price)
                        ? $"{g.Name} 등록"
                        : "등록 실패 — 수수료·한도·잠금";
                }
            }
            else if (GameState.Bag.GetCount(Economy.LifeItem.CraftHide) > 0 && row < 8)
            {
                const long hidePrice = 2_400;
                if (Row(r, row++, "등록 사냥 가죽 1",
                        $"수수료 {Economy.FormatCurrency(AuctionState.ListFee(hidePrice))}"))
                {
                    _msg = AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, hidePrice)
                        ? "가죽 등록" : "등록 실패";
                }
            }

            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _msg = ""; _sub = Sub.없음; }
        }

        static Economy.LifeItem ParseLotItem(AuctionState.Lot lot)
        {
            if (lot != null && System.Enum.TryParse(lot.Key, out Economy.LifeItem it))
                return it;
            return Economy.LifeItem.EnhanceStone;
        }

        string _msg = "";

        /// <summary>
        /// 영묘 — 환생석으로 삭제된 캐릭터를 되돌린다(§4).
        ///
        /// 영지 4건물 중 여기를 먼저 채운 이유: **이미 있는 시스템만으로 성립한다.**
        /// 삭제된 캐릭터(`IsDeleted`)도, 환생석(`Economy.LifeItem.RebornStone`)도,
        /// 소모 API(`GameState.Consume`)도 전부 있었는데 **화면만 없었다** — 이 저장소가
        /// 반복해서 겪는 「정의는 있고 부르는 곳이 없다」의 또 한 사례다.
        /// 대장간 첫 슬라이스(가죽→흉갑→장착)는 Smith()가 맡는다.
        ///
        /// 삭제가 **되돌릴 수 있는 것**이 되면 목숨 시스템(§4)의 무게가 사라지므로,
        /// 되돌아온 캐릭터는 사망 0에서 다시 시작하고 환생석 자체를 희소하게 둔다.
        /// </summary>
        void Mausoleum(Rect r)
        {
            int row = 0;
            string lockReason = Memorial.LockReason();
            if (lockReason != null)
            {
                Info(r, row++, lockReason);
                if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
                return;
            }

            var dead = LifeSystem.GetDeletedCharacters();
            int stones = LifeSystem.GetRebornStones();

            Info(r, row++, $"환생석 {stones}개 · 잠든 캐릭터 {dead.Count}명");
            if (!string.IsNullOrEmpty(Memorial.HubLine()))
                Info(r, row++, Memorial.HubLine());
            if (!string.IsNullOrEmpty(Rebirth.Line()))
                Info(r, row++, Rebirth.Line());

            if (dead.Count == 0)
            {
                // 빈 목록을 조용히 그리지 않는다 — "왜 아무것도 없나"에 답해 준다
                Info(r, row++, "잠든 캐릭터가 없다 — 3회 사망한 캐릭터만 이곳에 온다(§4)");
            }
            else
            {
                foreach (var ch in dead)
                {
                    if (ch.IsSpecialJob)
                    {
                        Locked(r, row++, $"{ch.Name} · {ch.Job} · 기록만",
                            string.IsNullOrEmpty(Memorial.Line(ch))
                                ? "특수 직업은 환생석으로 되돌릴 수 없다(§3)"
                                : Memorial.Line(ch),
                            ItemAtlas.KeyFor(Economy.LifeItem.RebornStone));
                        if (Memorial.HasRecord(ch))
                            Info(r, row++, Memorial.GearLine(ch));
                        continue;
                    }
                    string desc = Memorial.HasRecord(ch)
                        ? Memorial.Line(ch)
                        : Rebirth.RowDesc(ch, stones);
                    if (Row(r, row++, $"{ch.Name} · {ch.Job} Lv{ch.Level}", desc,
                            ItemAtlas.KeyFor(Economy.LifeItem.RebornStone)))
                    {
                        if (stones <= 0) _msg = "환생석이 없다. 10층 보스를 잡아야 한다(§4)";
                        else if (LifeSystem.UseRebornStone(ch))
                        {
                            _msg = string.IsNullOrEmpty(Rebirth.DoneLine())
                                ? $"{ch.Name}이(가) 돌아왔다 — 사망 0에서 다시 시작한다"
                                : $"{ch.Name}이(가) 돌아왔다 — {Rebirth.DoneLine()}";
                            return;                     // 목록이 바뀌었으니 이번 프레임은 여기서 끝
                        }
                        else _msg = "환생에 실패했다 — 환생석 소모를 확인할 것";
                    }
                    if (Memorial.HasRecord(ch))
                    {
                        Info(r, row++, Memorial.GearLine(ch));
                        if (!string.IsNullOrEmpty(Memorial.RebirthLine(ch)))
                            Info(r, row++, Memorial.RebirthLine(ch));
                    }
                }
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
        }

        /// <summary>
        /// 수비대 — 로스터를 최대 5명 세운다(§13-5).
        /// 소비처는 출전 제외다. 침략 본게임(적 별·약탈)은 여기서 열지 않는다.
        /// </summary>
        void Barracks(Rect r)
        {
            int row = 0;
            string lockReason = DefenseState.LockReason();
            if (lockReason != null)
            {
                Info(r, row++, lockReason);
                if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
                return;
            }

            var roster = LifeSystem.GetCharacters();
            Info(r, row++,
                $"수비 {DefenseState.Count}/{DefenseState.MaxSlots} · 배치된 캐릭터는 출전하지 않는다. 침략 전투는 아직 없다(§13-5)");

            if (roster.Count == 0)
                Info(r, row++, "[주의] 캐릭터가 하나도 없다 — 로스터 생성이 실패했다");
            else
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster[i];
                    if (ch.IsDeleted) continue;
                    bool posted = DefenseState.Contains(i);
                    string label = (posted ? "★ " : "") + $"{ch.Name} · {ch.Job}";
                    string desc = posted
                        ? "배치됨 — 눌러 해임. 출전 편성에서 빠져 있다"
                        : LifeSystem.IsAvailable(ch)
                            ? "대기 — 눌러 배치. 배치하면 출전에서 빠진다"
                            : "회복 중 — 배치할 수 없다(§4)";
                    if (Row(r, row++, label, desc))
                    {
                        if (!DefenseState.Toggle(i))
                            _msg = LifeSystem.IsAvailable(ch)
                                ? $"자리가 없다 — {DefenseState.MaxSlots}명이 상한이다(§13-5)"
                                : "회복 중이거나 삭제된 캐릭터는 세울 수 없다";
                        else _msg = "";
                    }
                }
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
        }

        /// <summary>
        /// 대장간 — 계열 재료로 6부위를 만들고, 강화석으로 +15까지 올린다(§11).
        /// 실패해도 장비는 남는다. 해금은 1차 전직 시점(§13-2).
        /// </summary>
        void Smith(Rect r)
        {
            int row = 0;
            DrawSmithMaterials(r, row++);

            string lockReason = Equipment.LockReason();
            if (lockReason != null)
            {
                Locked(r, row++, "제작·강화", lockReason, "building_smith");
            }
            else
            {
                var target = Equipment.FirstEnhanceable();
                if (target != null)
                {
                    int cost = Equipment.StoneCost(target.Enhance);
                    int stones = GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone);
                    int pct = Equipment.SuccessPercent(target.Enhance);
                    string label = $"{target.Name} +{target.Enhance} 강화";
                    string desc = $"석 {cost}개 · 성공 {pct}% · 실패해도 파괴 없음(§11)";
                    string enhanceIcon = ItemAtlas.KeyFor(Economy.LifeItem.EnhanceStone);
                    if (target.Enhance >= Equipment.MaxEnhance)
                        Locked(r, row++, label, "+15가 상한이다", enhanceIcon, target.Grade);
                    else if (stones < cost)
                        Locked(r, row++, label, $"강화석 {cost}개 필요 — 현재 {stones}개(던전)", enhanceIcon, target.Grade);
                    else if (Row(r, row++, label, desc, enhanceIcon, rarity: target.Grade))
                    {
                        bool attempted = Equipment.TryEnhance(target.Id, out bool ok);
                        _msg = !attempted
                            ? "강화할 수 없다 — 석 수와 상한을 확인할 것"
                            : ok
                                ? $"{target.Name} 강화 성공 +{target.Enhance}"
                                : $"{target.Name} 강화 실패 — 장비는 남았다";
                    }
                }

                for (int i = 0; i < Equipment.Recipes.Length; i++)
                {
                    var rec = Equipment.Recipes[i];
                    if (Equipment.CountOfRecipe(rec.Id) > 0) continue;
                    int have = GameState.Bag.GetCount(rec.Material);
                    string need = $"{GameState.Label(rec.Material)} {rec.Cost}장 · {Equipment.SlotName(rec.Slot)}";
                    string craftIcon = ItemAtlas.KeyForSlot(rec.Slot);
                    if (have < rec.Cost)
                        Locked(r, row++, $"{rec.Name} 제작", $"{need} — 현재 {have}", craftIcon, GearGrade.Common);
                    else if (Row(r, row++, $"{rec.Name} 제작", need, craftIcon, rarity: GearGrade.Common))
                    {
                        _msg = Equipment.TryCraft(rec.Id)
                            ? $"{rec.Name}을(를) 만들었다 — 아래에서 입힌다"
                            : "제작에 실패했다 — 재료와 전직 해금을 확인할 것";
                    }
                }
            }

            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                if (ch.IsDeleted) continue;
                var worn = Equipment.WornAll(ch);
                if (worn.Count > 0)
                {
                    string names = worn[0].Name + (worn[0].Enhance > 0 ? $"+{worn[0].Enhance}" : "");
                    if (worn.Count > 1) names += $" 외 {worn.Count - 1}";
                    if (Row(r, row++, $"{ch.Name} · {names}",
                            $"체력 ×{Equipment.HpMulOf(ch):0.00} — 눌러 벗긴다",
                            ItemAtlas.KeyForGear(worn[0]), rarity: worn[0].Grade))
                    {
                        Equipment.TryUnequip(ch);
                        _msg = $"{ch.Name}의 장비를 벗겼다";
                    }
                    continue;
                }

                var bag = Equipment.Unequipped();
                if (bag.Count == 0)
                {
                    Info(r, row++, $"{ch.Name} · 미착용 — 만들 장비가 없다");
                    continue;
                }
                if (Row(r, row++, $"{ch.Name}에게 {bag[0].Name} 입히기",
                        $"체력 ×{Equipment.EffectiveHpMul(bag[0]):0.00}",
                        ItemAtlas.KeyForGear(bag[0]), rarity: bag[0].Grade))
                {
                    _msg = Equipment.TryEquip(ch, bag[0].Id)
                        ? $"{ch.Name}이(가) {bag[0].Name}을(를) 입었다"
                        : "장착에 실패했다";
                }
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
        }

        /// <summary>재료를 글자 나열로만 쓰면 아이템 아틀라스가 대장간에 소비처 0곳이다.</summary>
        void DrawSmithMaterials(Rect r, int index)
        {
            var panel = new Rect(r.x - 12, r.y + index * (RowH + RowGap), r.width + 24, RowH);
            if (!UiAtlas.DrawSliced(panel, "panel", 14f, new Color(1f, 1f, 1f, 0.92f)))
                UiAtlas.Draw(panel, "panel", new Color(1f, 1f, 1f, 0.92f));

            var items = ItemAtlas.SmithMaterials;
            float cell = Mathf.Min(96f, (r.width - 8f) / items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                float x = r.x + 4f + i * cell;
                ItemAtlas.Draw(new Rect(x, panel.y + 6f, 40f, 40f), ItemAtlas.KeyFor(items[i]));
                UiPages.LabelClip(new Rect(x + 42f, panel.y + 16f, Mathf.Max(12f, cell - 46f), 26f),
                    GameState.Bag.GetCount(items[i]).ToString(),
                    new GUIStyle(GUI.skin.label) { fontSize = 18, clipping = TextClipping.Clip,
                        normal = { textColor = new Color(0.95f, 0.79f, 0.42f) } });
            }
        }
    }
}
