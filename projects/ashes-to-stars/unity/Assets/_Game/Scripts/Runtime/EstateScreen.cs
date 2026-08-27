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
        int _selX = -1, _selY = -1;
        bool _moveDown;
        bool _moveDragging;
        bool _suppressClick;
        int _moveFromX = -1, _moveFromY = -1;
        int _dropX = -1, _dropY = -1;
        EstateGrid.Cell _moveCell = EstateGrid.Cell.Empty;
        string _moveHint;
        Vector2 _moveFromMouse;
        CharacterRecord _rebornPick;

        /// <summary>경매장 해금 층(§12). 침략과 동시 해금이다.</summary>
        public const int AuctionUnlockFloor = 30;

        protected override string Title => _sub == Sub.없음 ? "영지" : $"영지 · {_sub}";
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.Estate);
        protected override string BackgroundArt => "bg_estate";
        protected override string Subtitle => PlayerSubtitle(_sub switch
        {
            Sub.대장간 => Equipment.SmithUnlocked()
                ? "사냥해서 얻은 재료로 만든다. 강화는 실패해도 파괴되지 않는다(§11)"
                : Equipment.LockLine(),
            Sub.경매장 => AuctionHud.ShowQa
                ? AuctionHud.Line()
                : AuctionHubLockReason() ?? AuctionTrade.TradeLine(),
            Sub.영묘 => Memorial.Unlocked
                ? RebirthSkill.MausoleumSubtitle()
                : Memorial.LockLine(),
            Sub.수비대 => DefenseState.Unlocked
                ? "최대 5명. 침략 때 수비가 적으면 약탈이 늘어난다(§13-5·§15)"
                : DefenseState.LockLine(),
            Sub.월드티어 => "해금한 티어 중 하나를 고르면 필드·던전·하위 레이드가 함께 움직인다(§6)",
            Sub.본성 => "본성 레벨이 다른 건물 상한과 창고 용량이다. 공사는 끝나면 자동 적용(§13-2)",
            Sub.영공 => "층을 오를수록 인식 범위가 넓어진다. 아군 버프·적 디버프를 켠다(§14)",
            _ => SoftCapHubSubtitle(),
        });

        public const string EnvNoHeaderPlayerCopy = "QA_NO_ESTATE_HEADER_PLAYER_COPY";

        /// <summary>
        /// 영지 헤더는 플레이어 화면이다. 구현 근거인 (섹션 번호) 표기는 QA 자막에도 내보내지 않는다.
        /// QA_NO면 최신 정상 화면에서 발견한 옛 노출을 재현한다.
        /// </summary>
        public static string PlayerSubtitle(string value)
        {
            if (string.IsNullOrEmpty(value)
                || System.Environment.GetEnvironmentVariable(EnvNoHeaderPlayerCopy) == "1")
                return value;

            int mark = value.IndexOf("(§", System.StringComparison.Ordinal);
            while (mark >= 0)
            {
                int end = value.IndexOf(')', mark);
                if (end < 0) break;
                value = value.Remove(mark, end - mark + 1);
                mark = value.IndexOf("(§", System.StringComparison.Ordinal);
            }
            return value.Trim();
        }

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
            if (EstateYard.ShowDragQa)
                return EstateYard.Line();
            if (EstateYard.ShowQa)
                return EstateYard.Line();
            if (EstateBuildings.ShowQa)
                return EstateBuildings.Line();
            if (EstateStatusHud.ShowQa)
                return EstateStatusHud.Line();
            if (EstateHud.ShowQa)
                return EstateHud.Line();
            if (EstateStore.ShowQa)
                return EstateStore.Line();
            return EstateYard.Line();
        }

        /// <summary>
        /// 시각 QA가 **건물 안쪽까지** 찍을 수 있게 하는 진입점(`GAME_START=estate`).
        /// 이게 없으면 자동 조종은 허브만 찍고 끝나서, 정작 채운 화면은 한 번도 안 보인다 —
        /// 이 프로젝트에서 "렌더는 되는데 아무도 안 본 화면"이 반복해서 나온 이유다.
        /// </summary>
        public static string AutoOpen;
        bool _pendingDefenseRushShot;
        bool _pendingMineRushShot;
        bool _pendingKeepPoorShot;
        bool _pendingDefensePoorShot;

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
                    _hubPage = 0;
                else if (AutoOpen == "본성칸")
                {
                    // QA 샷: 마을 허브에서 본성 선택 → YardInspectLine 부제 노출
                    _hubPage = 0;
                    _selX = EstateGrid.KeepX;
                    _selY = EstateGrid.KeepY;
                }
                else if (AutoOpen == "광산칸")
                {
                    // QA 샷: 광산 선택 → DrawCoreBuildDock 업비 부제
                    _hubPage = 0;
                    _selX = EstateGrid.MineX;
                    _selY = EstateGrid.MineY;
                }
                else if (AutoOpen == "방어단축")
                {
                    // QA 샷: 방어 탭 + 화살탑 공사 중 → 골드 단축 줄
                    _hubPage = 2;
                    _pendingDefenseRushShot = true;
                }
                else if (AutoOpen == "광산단축")
                {
                    // QA 샷: 광산 선택 + 공사 중 → DrawCoreBuildDock 골드 단축
                    _hubPage = 0;
                    _selX = EstateGrid.MineX;
                    _selY = EstateGrid.MineY;
                    _pendingMineRushShot = true;
                }
                else if (AutoOpen == "본성부족")
                {
                    // QA 샷: 본성 업비 골드 부족 문구
                    _sub = Sub.본성;
                    _pendingKeepPoorShot = true;
                }
                else if (AutoOpen == "방어부족")
                {
                    // QA 샷: 방어 업비 골드 부족 문구
                    _hubPage = 2;
                    _pendingDefensePoorShot = true;
                }
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
            EstateYard.SeedQaIfRequested();
            EstateStatusHud.SeedQaIfRequested();
            if (System.Environment.GetEnvironmentVariable(EstateStatusHud.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable("QA_ESTATE_MINE") == "1")
                _hubPage = 1;
            if (System.Environment.GetEnvironmentVariable("QA_ESTATE_DEFENSE") == "1")
                _hubPage = 2;
            if (System.Environment.GetEnvironmentVariable("QA_ESTATE_RUSH") == "1")
                _sub = Sub.본성;
            if (System.Environment.GetEnvironmentVariable("QA_ESTATE_GRID") == "1")
                _hubPage = 0;
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
            if (System.Environment.GetEnvironmentVariable(NetWorth.EnvShow) == "1")
                _hubPage = 1;
            WorldStar.SeedRaceSenseQaIfRequested();
            SoftCap.SeedQaIfRequested();
            EstateMine.SeedQaIfRequested();
            EstateMine.SeedRaceQaIfRequested();
            EstateMine.SeedSeizeQaIfRequested();
            BankruptcySeize.SeedQaIfRequested();
            NetWorth.SeedQaIfRequested();
            EstateDefense.SeedQaIfRequested();
            if (_pendingDefenseRushShot)
            {
                _pendingDefenseRushShot = false;
                EstateDefense.ResetForTest();
                GameState.SetTowerFloorForTest(EstateDefense.UnlockFloor);
                long cost = EstateDefense.UpgradeCost(0);
                GameState.Grant(cost * 4 + 10_000);
                EstateDefense.TryStart(EstateDefense.Kind.화살탑);
            }
            if (_pendingMineRushShot)
            {
                _pendingMineRushShot = false;
                long cost = EstateBuild.UpgradeCost(EstateGrid.Cell.Mine, EstateBuild.Level(EstateGrid.Cell.Mine));
                GameState.Grant(cost * 4 + 10_000);
                EstateBuild.TryStartUpgrade(EstateGrid.Cell.Mine);
            }
            EstateBuild.SeedRushQaIfRequested();
            if (_pendingKeepPoorShot)
            {
                _pendingKeepPoorShot = false;
                long have = GameState.Wallet.Copper;
                if (have > 0) GameState.Pay(have);
            }
            if (_pendingDefensePoorShot)
            {
                _pendingDefensePoorShot = false;
                EstateDefense.ResetForTest();
                GameState.SetTowerFloorForTest(EstateDefense.UnlockFloor);
                long have = GameState.Wallet.Copper;
                if (have > 0) GameState.Pay(have);
            }
            EstateGrid.SeedQaIfRequested();
            SeedRecallQaIfRequested();
            SeedCellClickQaIfRequested();
            SeedHubClickQaIfRequested();
            SeedDockClickQaIfRequested();
            SeedRowClickQaIfRequested();
            EstateStore.SeedQaIfRequested();
            EstateHud.SeedQaIfRequested();
            EstateBuildings.SeedQaIfRequested();
            EstateBuild.SeedArtTierQaIfRequested();
            EstateBuild.SeedArtDoneQaIfRequested();
            SeedArtScaffoldFrameQaIfRequested();
            StarterSecond.SeedQaIfRequested();
            AuctionState.SeedQaIfRequested();
            AuctionState.SeedBuyLockQaIfRequested();
            AuctionState.SeedExpireQaIfRequested();
            AuctionTrade.SeedQaIfRequested();
            AuctionHud.SeedQaIfRequested();
            Rebirth.SeedQaIfRequested();
            RebirthSkill.SeedQaIfRequested();
            Memorial.SeedQaIfRequested();
            Memorial.SeedUnlockQaIfRequested();
            DefenseState.SeedUnlockQaIfRequested();
            Equipment.SeedUnlockQaIfRequested();
            if (System.Environment.GetEnvironmentVariable(Rebirth.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable(RebirthSkill.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable(Memorial.EnvShow) == "1")
                _sub = Sub.영묘;
            if (RebirthSkill.SeedPick && _rebornPick == null)
            {
                var wait = LifeSystem.GetDeletedCharacters();
                if (wait.Count > 0)
                {
                    _rebornPick = wait[0];
                    RebirthSkill.ConsumeSeedPick();
                }
            }
            if (System.Environment.GetEnvironmentVariable(AuctionState.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable(AuctionState.EnvShowBuyLock) == "1"
                || System.Environment.GetEnvironmentVariable(AuctionState.EnvShowExpire) == "1"
                || System.Environment.GetEnvironmentVariable(AuctionTrade.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable(LifePrice.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable(TokenPrice.EnvShow) == "1"
                || System.Environment.GetEnvironmentVariable(AuctionHud.EnvShow) == "1")
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

            if (_layoutQa)
            {
                _hubPage = DrawTabs(r, new[] { "마을", "현황", "방어" }, 0);
                DrawLayout(UiPages.AfterTabs(r));
                return;
            }
            if (_hubPage == 0 && !EstateYard.FillBlocked)
            {
                DrawVillage(r);
                _hubPage = DrawTabs(r, new[] { "마을", "현황", "방어" }, _hubPage);
                return;
            }
            _hubPage = DrawTabs(r, new[] { "마을", "현황", "방어" }, _hubPage);
            var page = UiPages.AfterTabs(r);
            if (_hubPage == 1)
            {
                if (!EstateYard.FillBlocked && !EstateStatusHud.Blocked)
                    DrawVillageBackdrop(r);
                DrawEstateStatus(page);
                return;
            }
            if (_hubPage == 2)
            {
                DrawDefense(page);
                return;
            }
            DrawVillage(page);
        }

        void DrawVillageBackdrop(Rect r)
        {
            EstateGrid.EnsureHubBuildings();
            var yard = EstateYard.VillageRect(r);
            EstateYard.Draw(yard, -1, -1, out _, out _);
        }

        void DrawVillage(Rect r)
        {
            EstateGrid.SeedQaIfRequested();
            EstateGrid.EnsureHubBuildings();
            var side = EstateGrid.InvaderSide();
            int path = EstateGrid.InvaderPath();
            string hud =
                $"침략 {side} {path}칸 · 성벽 {EstateGrid.Count(EstateGrid.Cell.Wall)}/{EstateDefense.Level(EstateDefense.Kind.성벽)}"
                + $" · 함정 {EstateGrid.Count(EstateGrid.Cell.Trap)}/{EstateDefense.Level(EstateDefense.Kind.함정)}";
            var yard = EstateYard.VillageRect(r);
            if (EstateYard.FillBlocked)
                Info(r, 0, hud);

            HandleBuildingDrag(yard);
            if (EstateYard.Draw(yard, _selX, _selY, out int hx, out int hy))
            {
                if (!_suppressClick)
                    OnYardClick(hx, hy);
            }
            _suppressClick = false;
            if (_moveDragging && EstateGrid.IsCore(_moveCell) && EstateGrid.InBounds(_dropX, _dropY))
            {
                bool ok = string.IsNullOrEmpty(_moveHint);
                EstateYard.DrawMovePreview(yard, _moveCell, _dropX, _dropY, ok);
            }

            if (EstateYard.FillBlocked)
            {
                float bottom = EstateYard.InspectorH + EstateYard.PaletteH + 10f;
                var inspect = new Rect(r.x, r.yMax - bottom, r.width, EstateYard.InspectorH);
                DrawVillageInspect(inspect);
                var palette = new Rect(r.x, r.yMax - EstateYard.PaletteH, r.width, EstateYard.PaletteH);
                DrawVillagePalette(palette);
                return;
            }

            var chip = EstateHud.ChipRect(r);
            string chipText = (_moveDragging && !string.IsNullOrEmpty(_moveHint)) ? _moveHint
                : (_selX < 0 ? hud : null);
            if (!string.IsNullOrEmpty(chipText))
            {
                // QA_NO면 옛 Hint(배경에 글씨만). 새 길은 InfoAt 금테 — 마을 그림에 묻히지 않는다.
                if (EstateHud.Blocked) Hint(chip, chipText);
                else InfoAt(chip, chipText);
            }
            bool selected = _selX >= 0;
            float insH = EstateHud.InspectH(selected);
            var paletteOn = EstateHud.PaletteBar(r);
            if (EstateHud.ShowInspectBar(selected) && insH > 1f)
            {
                var inspectOn = new Rect(r.x, paletteOn.y - insH, r.width, insH);
                DrawVillageInspect(inspectOn);
            }
            DrawVillagePalette(paletteOn);
        }

        void HandleBuildingDrag(Rect yard)
        {
            if (!EstateYard.DragEnabled)
            {
                ClearMoveDrag();
                return;
            }
            var ev = Event.current;
            if (ev == null) return;
            var mouse = ev.mousePosition;
            if (ev.type == EventType.MouseDown && ev.button == 0 && yard.Contains(mouse)
                && EstateYard.TryHitCoreOrigin(yard, mouse, out int ox, out int oy))
            {
                _moveDown = true;
                _moveDragging = false;
                _suppressClick = false;
                _moveFromX = ox;
                _moveFromY = oy;
                _moveCell = EstateGrid.At(ox, oy);
                _dropX = ox;
                _dropY = oy;
                _moveHint = null;
                _moveFromMouse = mouse;
                _selX = ox;
                _selY = oy;
                return;
            }
            if (!_moveDown) return;
            if (ev.type == EventType.MouseDrag && ev.button == 0)
            {
                Vector2 d = mouse - _moveFromMouse;
                if (!_moveDragging && d.sqrMagnitude >= EstateYard.DragSlop * EstateYard.DragSlop)
                    _moveDragging = true;
                if (_moveDragging)
                {
                    if (EstateYard.TryHitCell(yard, mouse, out int hx, out int hy))
                    {
                        _dropX = hx;
                        _dropY = hy;
                    }
                    _moveHint = WhyCannotDragMove(_moveFromX, _moveFromY, _dropX, _dropY);
                    ev.Use();
                }
                return;
            }
            if (ev.type == EventType.MouseUp && ev.button == 0)
            {
                if (_moveDragging)
                {
                    if (TryDragMove(_moveFromX, _moveFromY, _dropX, _dropY))
                    {
                        _selX = _dropX;
                        _selY = _dropY;
                        _moveHint = null;
                    }
                    _suppressClick = true;
                    ev.Use();
                }
                else
                    _moveHint = null;
                ClearMoveDrag();
            }
        }

        void ClearMoveDrag()
        {
            _moveDown = false;
            _moveDragging = false;
            _moveFromX = -1;
            _moveFromY = -1;
            _dropX = -1;
            _dropY = -1;
            _moveCell = EstateGrid.Cell.Empty;
            _moveHint = null;
        }

        /// <summary>핵심 건물 자리 옮김 거부 사유(§2-2). 창고는 EstateStore.</summary>
        public static string WhyCannotDragMove(int ox, int oy, int nx, int ny)
        {
            if (EstateYard.DragBlocked) return "건물 이동이 꺼져 있다";
            if (!EstateGrid.InBounds(ox, oy)) return "격자 밖이다";
            var cell = EstateGrid.At(ox, oy);
            if (!EstateGrid.IsCore(cell)) return "핵심 건물만 옮긴다";
            if (ox == nx && oy == ny) return null;
            if (EstateBuild.Busy(cell)) return "건설 중이다";
            if (cell == EstateGrid.Cell.Warehouse)
                return EstateStore.WhyCannotMove(nx, ny);
            if (!EstateGrid.InBounds(nx, ny)) return "격자 밖이다";
            var f = EstateGrid.FootprintOf(cell);
            if (nx + f.x > EstateGrid.Size || ny + f.y > EstateGrid.Size)
                return "격자 밖이다";
            for (int y = ny; y < ny + f.y; y++)
            for (int x = nx; x < nx + f.x; x++)
            {
                if (!EstateGrid.TryOwner(x, y, out int px, out int py)) continue;
                if (px == ox && py == oy) continue;
                return "자리 크기가 겹친다";
            }
            return null;
        }

        /// <summary>핵심 건물 원점 이동. 창고는 EstateStore.TryMove.</summary>
        public static bool TryDragMove(int ox, int oy, int nx, int ny)
        {
            if (WhyCannotDragMove(ox, oy, nx, ny) != null) return false;
            var cell = EstateGrid.At(ox, oy);
            if (!EstateGrid.IsCore(cell)) return false;
            if (ox == nx && oy == ny) return true;
            if (cell == EstateGrid.Cell.Warehouse)
                return EstateStore.TryMove(nx, ny);
            EstateGrid.SetCellForTest(ox, oy, EstateGrid.Cell.Empty);
            EstateGrid.SetCellForTest(nx, ny, cell);
            return EstateGrid.At(nx, ny) == cell
                && EstateGrid.At(ox, oy) == EstateGrid.Cell.Empty;
        }

        /// <summary>UI·SelfCheck 공용 — 미리보기 거부 사유(§2-2).</summary>
        public static string PreviewWhy(int ox, int oy, int nx, int ny) =>
            WhyCannotDragMove(ox, oy, nx, ny);

        /// <summary>UI·SelfCheck 공용 — 놓을 수 있으면 true.</summary>
        public static bool WouldAccept(int ox, int oy, int nx, int ny) =>
            WhyCannotDragMove(ox, oy, nx, ny) == null;

        void OnYardClick(int x, int y)
        {
            if (EstateGrid.At(x, y) == EstateGrid.Cell.Empty
                && EstateGrid.TryOwner(x, y, out int ox, out int oy))
            {
                x = ox;
                y = oy;
            }
            var cell = EstateGrid.At(x, y);
            if (EstateGrid.IsHub(cell))
            {
                _selX = x;
                _selY = y;
                if (cell == EstateGrid.Cell.Keep) _sub = Sub.본성;
                else if (cell == EstateGrid.Cell.Smith) _sub = Sub.대장간;
                else if (cell == EstateGrid.Cell.Auction) _sub = Sub.경매장;
                else if (cell == EstateGrid.Cell.Mausoleum) _sub = Sub.영묘;
                else if (cell == EstateGrid.Cell.Barracks) _sub = Sub.수비대;
                return;
            }
            if (EstateGrid.IsDefense(cell))
            {
                if (_selX == x && _selY == y)
                    EstateGrid.TryPickUp(x, y);
                _selX = x;
                _selY = y;
                if (EstateGrid.At(x, y) == EstateGrid.Cell.Empty)
                {
                    _selX = -1;
                    _selY = -1;
                }
                return;
            }
            if (cell == EstateGrid.Cell.Empty)
            {
                if (EstateGrid.TryPlace(x, y, _placeKind))
                {
                    _selX = x;
                    _selY = y;
                }
                else
                {
                    _selX = x;
                    _selY = y;
                }
                return;
            }
            _selX = x;
            _selY = y;
        }

        bool _recallQaSeeded;
        int _recallQaX = -1, _recallQaY = -1;
        void SeedRecallQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_ESTATE_RECALL");
            if (raw != "0" && raw != "1") return;
            _hubPage = 0;
            if (!_recallQaSeeded)
            {
                _recallQaSeeded = true;
                if (EstateDefense.Level(EstateDefense.Kind.성벽) < 1)
                    EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 1);
                int n = EstateGrid.Size;
                for (int y = 0; y < n && _recallQaX < 0; y++)
                for (int x = 0; x < n; x++)
                {
                    if (!EstateGrid.InBounds(x, y)) continue;
                    if (!EstateGrid.IsDefense(EstateGrid.At(x, y))) continue;
                    _recallQaX = x;
                    _recallQaY = y;
                    break;
                }
                if (_recallQaX < 0)
                {
                    for (int y = 0; y < n && _recallQaX < 0; y++)
                    for (int x = 0; x < n; x++)
                    {
                        if (!EstateGrid.TryPlace(x, y, EstateGrid.Cell.Wall)) continue;
                        _recallQaX = x;
                        _recallQaY = y;
                        break;
                    }
                }
            }
            if (raw == "1")
            {
                EstateGrid.TryPickUp(_recallQaX, _recallQaY);
                _selX = -1;
                _selY = -1;
            }
            else
            {
                _selX = _recallQaX;
                _selY = _recallQaY;
            }
        }

        bool _layoutQa;
        bool _cellQaPlaced;
        void SeedCellClickQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_ESTATE_CELL");
            if (raw != "0" && raw != "1") return;
            _layoutQa = true;
            _hubPage = 0;
            _placeKind = EstateGrid.Cell.Wall;
            if (raw != "1" || _cellQaPlaced) return;
            _cellQaPlaced = true;
            if (EstateDefense.Level(EstateDefense.Kind.성벽) < 1)
                EstateDefense.SetLevelForTest(EstateDefense.Kind.성벽, 3);
            int n = EstateGrid.Size;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (EstateGrid.TryPlace(x, y, EstateGrid.Cell.Wall))
                    return;
            }
        }

        void SeedRowClickQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_ESTATE_ROW");
            if (raw != "0" && raw != "1") return;
            _layoutQa = false;
            _hubPage = 0;
            _sub = raw == "0" ? Sub.본성 : Sub.없음;
        }

        void SeedDockClickQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_ESTATE_DOCK");
            if (raw != "0" && raw != "1") return;
            _layoutQa = false;
            _hubPage = 0;
            _selX = -1;
            _selY = -1;
            _placeKind = raw == "1" ? EstateGrid.Cell.Arrow : EstateGrid.Cell.Wall;
        }

        void SeedHubClickQaIfRequested()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_ESTATE_HUB");
            if (raw != "0" && raw != "1") return;
            _layoutQa = false;
            _hubPage = 0;
            EstateGrid.EnsureHubBuildings();
            _selX = EstateGrid.KeepX;
            _selY = EstateGrid.KeepY;
            if (raw == "1") OpenHub(EstateGrid.Cell.Keep);
        }

        void SeedArtScaffoldFrameQaIfRequested()
        {
            bool busy = System.Environment.GetEnvironmentVariable("QA_ESTATE_ART_TIERS") == "1";
            bool done = System.Environment.GetEnvironmentVariable("QA_ESTATE_ART_DONE") == "1";
            if (!busy && !done) return;
            _layoutQa = false;
            _hubPage = 0;
            _selX = EstateGrid.BarracksX;
            _selY = EstateGrid.BarracksY;
        }

        void DrawVillageInspect(Rect r)
        {
            if (!EstateHud.ShowInspectBar(_selX >= 0) && !EstateYard.FillBlocked)
                return;
            if (_selX < 0 || !EstateGrid.InBounds(_selX, _selY))
            {
                if (EstateHud.Blocked || EstateYard.FillBlocked)
                    Info(r, 0, "건물을 누르면 들어간다. 빈 칸은 아래 선택한 방어를 놓는다.");
                return;
            }
            var cell = EstateGrid.At(_selX, _selY);
            string title = EstateYard.LabelOf(cell);
            string sub = _moveDragging && !string.IsNullOrEmpty(_moveHint) && EstateGrid.IsCore(cell)
                ? _moveHint
                : YardInspectLine(cell);
            string icon = EstateYard.IconOf(cell);
            bool fat = EstateHud.Blocked || EstateYard.FillBlocked;
            if (!fat)
            {
                if (EstateGrid.IsDefense(cell))
                {
                    Hint(new Rect(r.x, r.y, Mathf.Max(40f, r.width - 140f), r.height),
                        title + " · 한 번 더 누르면 거둔다");
                    var btn = new Rect(r.xMax - 128f, r.y, 128f, r.height);
                    // 클릭을 슬라이스·아이콘보다 먼저 먹는다. 뒤에 둔 GUI.Button(none)은
                    // 타이틀 직업 카드 옛 버그(77b56474)와 같이 크롬에 먹혀 거둠이 안 먹었다.
                    var ev = Event.current;
                    if (ev != null && ev.type == EventType.MouseDown && ev.button == 0
                        && btn.Contains(ev.mousePosition))
                    {
                        EstateGrid.TryPickUp(_selX, _selY);
                        _selX = -1;
                        _selY = -1;
                        ev.Use();
                    }
                    UiAtlas.DrawSliced(btn, UiAtlas.ButtonKey(false, false), 8f);
                    if (!string.IsNullOrEmpty(icon))
                        UiAtlas.DrawFit(new Rect(btn.x + 6f, btn.y + 4f, 28f, 28f), icon);
                    Hint(new Rect(btn.x + 36f, btn.y, btn.width - 40f, btn.height), "회수");
                    return;
                }
                if (EstateGrid.IsCore(cell) && !EstateGrid.IsHub(cell))
                {
                    DrawCoreBuildDock(r, cell, title, icon);
                    return;
                }
                var hubEv = Event.current;
                if (EstateGrid.IsHub(cell) && hubEv != null && hubEv.type == EventType.MouseDown
                    && hubEv.button == 0 && r.Contains(hubEv.mousePosition))
                {
                    OpenHub(cell);
                    hubEv.Use();
                }
                Hint(r, title + " · " + sub);
                return;
            }
            if (EstateGrid.IsDefense(cell))
            {
                if (DrawCard(r, title + " · 회수", "한 번 더 누르면 거둔다 · " + sub, icon))
                {
                    EstateGrid.TryPickUp(_selX, _selY);
                    _selX = -1;
                    _selY = -1;
                }
                return;
            }
            if (EstateGrid.IsHub(cell))
            {
                var hubEv = Event.current;
                if (hubEv != null && hubEv.type == EventType.MouseDown && hubEv.button == 0
                    && r.Contains(hubEv.mousePosition))
                {
                    OpenHub(cell);
                    hubEv.Use();
                }
                DrawCard(r, title, sub, icon);
                return;
            }
            if (EstateGrid.IsCore(cell))
            {
                DrawCoreBuildDock(r, cell, title, icon);
                return;
            }
            DrawCard(r, title, sub, icon, locked: true);
        }

        void OpenHub(EstateGrid.Cell cell)
        {
            if (cell == EstateGrid.Cell.Keep) _sub = Sub.본성;
            else if (cell == EstateGrid.Cell.Smith) _sub = Sub.대장간;
            else if (cell == EstateGrid.Cell.Auction) _sub = Sub.경매장;
            else if (cell == EstateGrid.Cell.Mausoleum) _sub = Sub.영묘;
            else if (cell == EstateGrid.Cell.Barracks) _sub = Sub.수비대;
        }

        string YardInspectLine(EstateGrid.Cell cell)
        {
            string build = CoreBuildCaption(cell);
            switch (cell)
            {
                case EstateGrid.Cell.Keep:
                    return EstateBuild.Busy(EstateGrid.Cell.Keep)
                        ? build
                        : $"{build} · 창고 {EstateStatusHud.ShortCopper(EstateBuild.WarehouseCapCopper())}";
                case EstateGrid.Cell.Mine:
                    return $"{build} · {EstateStatusHud.ShortCopper(EstateMine.CopperPerHourEffective())}/h";
                case EstateGrid.Cell.Warehouse:
                {
                    string a = EstateStatusHud.ShortCopper(GameState.Wallet.Copper);
                    string b = EstateStatusHud.ShortCopper(EstateBuild.WarehouseCapCopper());
                    return $"{build} · {a}/{b}";
                }
                case EstateGrid.Cell.Smith:
                    return build + " · " + (Equipment.LockReason() ?? "제작·강화. 실패해도 장비는 남는다");
                case EstateGrid.Cell.Auction:
                    return build + " · " + (AuctionHubLockReason() ?? AuctionState.FeeLine());
                case EstateGrid.Cell.Mausoleum:
                    return build + " · " + (Memorial.LockReason()
                        ?? $"환생석 {LifeSystem.GetRebornStones()}개");
                case EstateGrid.Cell.Barracks:
                    return build + " · " + (DefenseState.LockReason()
                        ?? $"배치 {DefenseState.Count}/{DefenseState.MaxSlots}");
                case EstateGrid.Cell.Empty:
                    return EstateGrid.WhyCannotPlace(_selX, _selY, _placeKind)
                        ?? $"여기에 {EstateYard.LabelOf(_placeKind)}을(를) 놓는다";
                default:
                    return "방어 건물";
            }
        }

        static string CoreBuildCaption(EstateGrid.Cell cell)
        {
            if (!EstateGrid.IsCore(cell)) return "";
            int lv = EstateBuild.Level(cell);
            if (EstateBuild.Busy(cell))
                return $"Lv{lv} → {EstateBuild.Target(cell)} · {EstateBuild.RemainingText(cell)}";
            return $"Lv{lv}";
        }

        void DrawVillagePalette(Rect r)
        {
            var kinds = EstateDefense.All;
            var tiles = EstateHud.PaletteTiles(r, kinds.Length);
            for (int i = 0; i < kinds.Length; i++)
            {
                var k = kinds[i];
                var cellKind = EstateGrid.CellOf(k);
                var b = tiles[i];
                int left = EstateGrid.Unplaced(cellKind);
                bool on = _placeKind == cellKind;
                string title = on ? $"선택 {k}" : $"{k}";
                string icon = UiAtlas.BuildingKey(k.ToString());
                var palEv = Event.current;
                if (palEv != null && palEv.type == EventType.MouseDown && palEv.button == 0
                    && b.Contains(palEv.mousePosition))
                {
                    _placeKind = cellKind;
                    palEv.Use();
                }
                if (EstateHud.Blocked || EstateYard.FillBlocked)
                {
                    DrawCard(b, $"{title} · {left}",
                        left > 0 ? "빈 칸에 놓는다" : "레벨만큼만", icon);
                    continue;
                }

                string btnKey = UiAtlas.ButtonKey(false, on);
                UiAtlas.DrawSliced(b, btnKey, 8f, EstateHud.PaletteTint(on));
                var inner = UiAtlas.ContentRect(b, btnKey, 2f);
                if (EstateHud.ReadablePaletteBlocked)
                {
                    float oldIconH = Mathf.Min(16f, inner.height - 20f);
                    if (oldIconH > 8f && !string.IsNullOrEmpty(icon))
                        UiAtlas.DrawFit(new Rect(inner.center.x - oldIconH * 0.5f, inner.y,
                            oldIconH, oldIconH), icon);
                    Hint(new Rect(inner.x, inner.yMax - 18f, inner.width, 18f), $"{title} {left}");
                }
                else
                {
                    // 44px 타일에 아이콘 위·글씨 아래를 쌓으면 둘이 10px 넘게 겹쳐
                    // 「화살탑 0」이 작은 회색 얼룩으로 읽혔다. 가로 배치로 클릭 높이는
                    // 유지하고 이름·개수에 한 줄 전체 높이를 준다.
                    float iconW = Mathf.Min(16f, inner.height - 4f);
                    if (iconW > 8f && !string.IsNullOrEmpty(icon))
                        UiAtlas.DrawFit(new Rect(inner.x, inner.center.y - iconW * 0.5f,
                            iconW, iconW), icon);
                    var label = new Rect(inner.x + iconW + 4f, inner.y,
                        Mathf.Max(20f, inner.width - iconW - 4f), inner.height);
                    if (EstateHud.BrightLabelBlocked) Hint(label, $"{title} {left}");
                    else DockLabel(label, $"{title} {left}");
                }
            }
        }

        void DrawEstateStatus(Rect r)
        {
            // 파산 강등·압류 결과를 현황 안에서 노출한다(§18-5). 도크 카드는 아래(DockH)에 붙으므로
            // 위 줄들과 겹치지 않는다. SeedQaIfRequested·EnvShow는 Body()가 이미 켠다.
            int statusRow = 0;
            if (BankruptcySeize.ShowOnHub)
            {
                Info(r, statusRow++, BankruptcySeize.KeepLine());
                Info(r, statusRow++, BankruptcySeize.ItemLine());
            }
            // 대출 연체 2회 → 광산 생산 100%가 빚으로 간다(§18-5). 파산과 별개 트리거라
            // 각자 판정해 스택으로 쌓는다. 압류 중일 때만 광산 문구를 현황에 노출한다.
            if (EstateMine.Seized)
                Info(r, statusRow++, "광산 " + EstateMine.SeizeLine());
            // 대출 한도의 순자산(지갑+장비+영지 평가)을 현황에 노출한다(§18-5). 탑 대출 화면과
            // 같은 NetWorth.Line()을 쓴다. ShowOnHub는 파산·압류와 동형 게이트(QA_NET_WORTH).
            if (NetWorth.ShowOnHub)
                Info(r, statusRow++, NetWorth.Line());
            var cards = EstateStatusHud.Cards(r);
            if (DrawCard(cards[0], "내 별 영공", EstateStatusHud.AuraCaption(), "worldmap"))
                _sub = Sub.영공;
            if (DrawCard(cards[1], "본성", EstateStatusHud.KeepCaption(), "territory"))
                _sub = Sub.본성;
            bool canPick = GameState.UnlockedTier > 0;
            if (DrawCard(cards[2], $"세계 T{GameState.Tier + 1}",
                    EstateStatusHud.WorldCaption(),
                    "tower", locked: !canPick))
                _sub = Sub.월드티어;
            DrawCard(cards[3], "광산", EstateStatusHud.MineCaption(), "field");
            DrawCard(cards[4], "창고", EstateStatusHud.StoreCaption(), "building_auction");
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
                // 클릭을 FillCell·라벨보다 먼저 먹는다. 뒤에 둔 GUI.Button(none)은
                // 회수 버튼 옛 버그(cdfa4fbf)와 같이 칸 색에 먹혀 선택/배치가 안 먹었다.
                var ev = Event.current;
                bool hit = ev != null && ev.type == EventType.MouseDown && ev.button == 0
                    && box.Contains(ev.mousePosition);
                if (hit) ev.Use();
                FillCell(box, CellTint(c, onPath));
                mark.normal.textColor = CellInk(c, onPath);
                UiPages.LabelClip(box, CellMark(c), mark);
                if (!hit) continue;
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

        void DrawSideTag(Rect box, string text, bool hot)
        {
            var st = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = hot ? FontStyle.Bold : FontStyle.Normal,
                clipping = TextClipping.Clip,
                wordWrap = true,
                normal = { textColor = hot
                    ? new Color(0.92f, 0.42f, 0.22f)
                    : new Color(0.95f, 0.79f, 0.42f) },
            };
            UiPages.LabelClip(box, text, st);
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
                string desc = $"{EstateStatusHud.ShortCopper(EstateDefense.UpgradeCost(lv))} · {FormatWait(EstateDefense.UpgradeSeconds(lv))}";
                if (why != null)
                    DrawCard(cards[i], title, why, icon, locked: true);
                else if (DrawCard(cards[i], title, desc, icon))
                    EstateDefense.TryStart(k);
            }
        }

        void Keep(Rect r)
        {
            var c = EstateGrid.Cell.Keep;
            EstateBuild.Tick();
            int lv = EstateBuild.Level(c);
            Info(r, 0, $"본성 Lv{lv} · 창고 {EstateStatusHud.ShortCopper(EstateBuild.WarehouseCapCopper())}(§18-12)");
            int row = 1;
            if (EstateBuild.Busy(c))
            {
                Info(r, row++, $"공사 중 Lv{lv} → {EstateBuild.Target(c)} · 남은 {EstateBuild.RemainingText(c)}");
                Info(r, row++, "끝나면 자동 적용 — 수령할 필요 없다. 단축은 남은 50%까지(§13-2)");
                DrawCoreRush(r, ref row, c);
            }
            else
            {
                string why = EstateBuild.WhyCannotUpgrade(c);
                string label = $"Lv{lv} → {lv + 1}";
                string desc = $"{EstateStatusHud.ShortCopper(EstateBuild.UpgradeCost(c, lv))} · {FormatWait(EstateBuild.UpgradeSeconds(c, lv))}";
                if (why != null)
                    Locked(r, row++, label, why, "territory");
                else if (Row(r, row++, label, desc, "territory"))
                    EstateBuild.TryStartUpgrade(c);
            }
            if (Row(r, row, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
        }

        void DrawCoreRush(Rect r, ref int row, EstateGrid.Cell c)
        {
            long cut = EstateBuild.RushableSeconds(c);
            string goldWhy = EstateBuild.WhyCannotRushGold(c);
            string goldLabel = $"골드 단축 · {EstateStatusHud.ShortCopper(EstateBuild.GoldCostToFloor(c))}";
            string goldDesc = cut > 0
                ? $"남은 {cut}초를 당긴다. 바닥은 원 소요의 50%(§13-2)"
                : "남은 시간의 50%가 바닥이다 — 완전 스킵 불가";
            if (goldWhy != null)
                Locked(r, row++, goldLabel, goldWhy, "gold");
            else if (Row(r, row++, goldLabel, goldDesc, "gold"))
                EstateBuild.TryRushGold(c);

            var mat = EstateRush.FirstOwnedFamilyMaterial();
            if (mat == null)
            {
                Locked(r, row++, "재료 단축",
                    "계열 재료 1개 = 남은 시간의 2%. 부활초·환생석·두루마리는 안 된다",
                    ItemAtlas.KeyFor(Economy.LifeItem.CraftHide));
                return;
            }
            var item = mat.Value;
            string matWhy = EstateBuild.WhyCannotRushMaterial(c, item);
            string matLabel = $"{GameState.Label(item)} 1장 단축";
            string matDesc = $"남은 시간의 2% · {GameState.Bag.GetCount(item)}장";
            if (matWhy != null)
                Locked(r, row++, matLabel, matWhy, ItemAtlas.KeyFor(item));
            else if (Row(r, row++, matLabel, matDesc, ItemAtlas.KeyFor(item)))
                EstateBuild.TryRushMaterial(c, item, 1);
        }

        /// <summary>허브 화면 상단 — 건물 Lv·업그레이드/단축 한 줄(SPEC §2-3).</summary>
        void DrawHubUpgradeRow(Rect r, ref int row, EstateGrid.Cell c)
        {
            EstateBuild.Tick();
            int lv = EstateBuild.Level(c);
            if (EstateBuild.Busy(c))
            {
                Info(r, row++, $"건물 Lv{lv} → {EstateBuild.Target(c)} · 남은 {EstateBuild.RemainingText(c)}");
                DrawCoreRush(r, ref row, c);
                return;
            }
            string why = EstateBuild.WhyCannotUpgrade(c);
            string label = $"건물 Lv{lv} → {lv + 1}";
            string desc = $"{EstateStatusHud.ShortCopper(EstateBuild.UpgradeCost(c, lv))} · {FormatWait(EstateBuild.UpgradeSeconds(c, lv))}";
            if (why != null)
                Locked(r, row++, label, why, EstateYard.IconOf(c));
            else if (Row(r, row++, label, desc, EstateYard.IconOf(c)))
                EstateBuild.TryStartUpgrade(c);
        }

        /// <summary>광산·창고 안내 도크 — 업그레이드·골드 단축(SPEC §2-3).</summary>
        void DrawCoreBuildDock(Rect r, EstateGrid.Cell c, string title, string icon)
        {
            EstateBuild.Tick();
            int lv = EstateBuild.Level(c);
            if (EstateBuild.Busy(c))
            {
                string goldWhy = EstateBuild.WhyCannotRushGold(c);
                string label = $"{title} Lv{lv}→{EstateBuild.Target(c)} · 골드 단축";
                string desc = goldWhy
                    ?? $"{EstateBuild.RemainingText(c)} · {EstateStatusHud.ShortCopper(EstateBuild.GoldCostToFloor(c))}";
                if (goldWhy != null)
                    DrawCard(r, label, desc, icon, locked: true);
                else if (DrawCard(r, label, desc, "gold"))
                    EstateBuild.TryRushGold(c);
                return;
            }
            string why = EstateBuild.WhyCannotUpgrade(c);
            string upLabel = $"{title} Lv{lv} → {lv + 1}";
            string upDesc = why
                ?? $"{EstateStatusHud.ShortCopper(EstateBuild.UpgradeCost(c, lv))} · {FormatWait(EstateBuild.UpgradeSeconds(c, lv))}";
            if (why != null)
                DrawCard(r, upLabel, upDesc, icon, locked: true);
            else if (DrawCard(r, upLabel, upDesc, icon))
                EstateBuild.TryStartUpgrade(c);
        }

        void DrawDefenseRush(Rect r, ref int row)
        {
            long cut = EstateDefense.RushableSeconds();
            string goldWhy = EstateDefense.WhyCannotRushGold();
            string goldLabel = $"골드 단축 · {EstateStatusHud.ShortCopper(EstateDefense.GoldCostToFloor())}";
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
                string pay = EstateStatusHud.ShortCopper((long)(Economy.TierRevenueMultiplier[i] * 10000f)) + "/h";
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
            var back = UiPages.Grid(WorldTierBackBand(r), 1, 1, 10f);
            if (DrawCard(back[0], "영지로", "건물에서 나온다", "territory")) _sub = Sub.없음;
        }

        public const string EnvNoWorldTierBackFit = "QA_NO_WORLD_TIER_BACK_FIT";
        public const float WorldTierBackMaxW = 520f;

        /// <summary>
        /// 월드 티어의 단일 「영지로」 카드는 1208px 전폭 그리드에 넣으면 내용은 왼쪽에
        /// 몰리고 오른쪽은 빈 액자가 된다. 행동 하나는 최대폭으로 가운데 모아 선택지처럼 읽힌다.
        /// QA_NO면 옛 전폭 카드로 돌아가 A/B를 남긴다.
        /// </summary>
        public static Rect WorldTierBackBand(Rect body)
        {
            bool blocked = System.Environment.GetEnvironmentVariable(EnvNoWorldTierBackFit) == "1";
            float w = blocked ? body.width : Mathf.Min(WorldTierBackMaxW, body.width);
            return new Rect(body.center.x - w * 0.5f, body.yMax - 88f, w, 80f);
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
            if (AuctionHud.Blocked)
            {
                AuctionHouseOld(r);
                return;
            }

            int info = 0;
            string lockReason = AuctionHubLockReason();
            if (lockReason != null)
            {
                string bar = AuctionHud.LockBarLine();
                if (bar != null)
                    InfoAt(AuctionHud.BarRect(r, info++), bar);
                var back = AuctionHud.LotsBody(r, info);
                if (Row(back, 0, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
                return;
            }

            InfoAt(AuctionHud.BarRect(r, info++),
                CoreBuildCaption(EstateGrid.Cell.Auction) + " · " + AuctionHud.StatusLine());
            string buyLock = AuctionState.BuyLockLine();
            if (!string.IsNullOrEmpty(buyLock))
                InfoAt(AuctionHud.BarRect(r, info++), buyLock);
            if (!string.IsNullOrEmpty(_msg))
                InfoAt(AuctionHud.BarRect(r, info++), _msg);

            var lotsR = AuctionHud.LotsBody(r, info);
            DrawAuctionLots(lotsR);
        }

        void AuctionHouseOld(Rect r)
        {
            int row = 0;
            DrawHubUpgradeRow(r, ref row, EstateGrid.Cell.Auction);
            string lockReason = AuctionHubLockReason();
            if (lockReason != null)
            {
                Info(r, row++, lockReason);
                if (Row(r, row, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
                return;
            }

            Info(r, row++,
                $"로컬 장 · {EstateStatusHud.ShortCopper(GameState.Wallet.Copper)} · {AuctionTrade.TradeLine()}");
            Info(r, row++, AuctionState.MineLine());
            string buyLock = AuctionState.BuyLockLine();
            if (!string.IsNullOrEmpty(buyLock))
                Info(r, row++, buyLock);
            if (!string.IsNullOrEmpty(_msg))
                Info(r, row++, _msg);

            DrawAuctionLots(r, row);
        }

        void DrawAuctionLots(Rect r, int row = 0)
        {
            var lots = AuctionState.Lots;
            string buyWhy = AuctionState.WhyCannotBuy();
            void DrawLot(AuctionState.Lot lot)
            {
                if (row >= 8) return;
                string who = lot.Npc ? "장" : "내 등록";
                if (lot.Npc)
                {
                    if (Row(r, row++, $"구매 {lot.Label}",
                            buyWhy ?? $"{who} · {EstateStatusHud.ShortCopper(lot.Price)}",
                            ItemAtlas.KeyFor(ParseLotItem(lot))))
                    {
                        _msg = AuctionState.TryBuy(lot.Id)
                            ? $"{lot.Label} 구매"
                            : (buyWhy ?? "구매 실패 — 골드 부족이거나 상한");
                    }
                    return;
                }
                if (Row(r, row++, $"취소 {lot.Label}",
                        $"{who} · {EstateStatusHud.ShortCopper(lot.Price)} · {AuctionState.LotTimeLine(lot)}"))
                    _msg = AuctionState.TryCancel(lot.Id) ? "등록 취소 · 수수료는 소각" : "취소 실패";
            }
            for (int i = 0; i < lots.Count; i++)
                if (!lots[i].Npc) DrawLot(lots[i]);
            for (int i = 0; i < lots.Count; i++)
                if (lots[i].Npc) DrawLot(lots[i]);

            var bag = Equipment.Unequipped();
            bool tradeQa = System.Environment.GetEnvironmentVariable(AuctionTrade.EnvShow) == "1";
            if (!tradeQa && bag.Count > 0 && row < 8)
            {
                var g = bag[0];
                long price = 12_000 + g.Enhance * 2_000;
                if (Row(r, row++, $"등록 {g.Name}",
                        $"수수료 {EstateStatusHud.ShortCopper(AuctionState.ListFee(price))} · {EstateStatusHud.ShortCopper(price)}"))
                {
                    _msg = AuctionState.TryListGear(g.Id, price)
                        ? $"{g.Name} 등록"
                        : "등록 실패 — 수수료·한도·잠금";
                }
            }
            else if (AuctionTrade.TryFirstBag(out var bagItem, out int bagQty) && row < 8)
            {
                long price = AuctionTrade.ListPrice(bagItem);
                if (Row(r, row++, $"등록 {GameState.Label(bagItem)} {bagQty}",
                        $"수수료 {EstateStatusHud.ShortCopper(AuctionState.ListFee(price))} · {EstateStatusHud.ShortCopper(price)}"))
                {
                    _msg = AuctionState.TryListItem(bagItem, bagQty, price)
                        ? $"{GameState.Label(bagItem)} 등록" : "등록 실패";
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
            if (_rebornPick == null)
                DrawHubUpgradeRow(r, ref row, EstateGrid.Cell.Mausoleum);
            string lockReason = Memorial.LockReason();
            if (lockReason != null)
            {
                Info(r, row++, lockReason);
                if (Row(r, row, "← 영지로", "건물에서 나온다"))
                { _sub = Sub.없음; _msg = ""; _rebornPick = null; }
                return;
            }

            if (_rebornPick != null && (!_rebornPick.IsDeleted || _rebornPick.IsSpecialJob))
                _rebornPick = null;
            if (_rebornPick != null)
            {
                var pickNames = RebirthSkill.NamesOf(_rebornPick.Job);
                Info(r, row++, RebirthSkill.PickTitle(_rebornPick));
                Info(r, row++, "생전 스킬 중 하나만 남고 나머지는 소실된다(§4)");
                if (Row(r, row++, "← 선택 취소", "영묘 목록으로"))
                { _rebornPick = null; _msg = ""; return; }
                for (int i = 0; i < pickNames.Length; i++)
                {
                    string skill = pickNames[i];
                    if (Row(r, row++, skill, "이 스킬만 가져간다(§4)",
                            ItemAtlas.KeyFor(Economy.LifeItem.RebornStone)))
                    {
                        if (!RebirthSkill.Apply(_rebornPick, skill))
                            _msg = "그 스킬은 가져갈 수 없다";
                        else if (LifeSystem.UseRebornStone(_rebornPick))
                        {
                            _msg = string.IsNullOrEmpty(RebirthSkill.Line(_rebornPick))
                                ? $"{_rebornPick.Name}이(가) 돌아왔다 — 사망 0에서 다시 시작한다"
                                : $"{_rebornPick.Name}이(가) 돌아왔다 — {Rebirth.DoneLine()} · {RebirthSkill.Line(_rebornPick)}";
                            _rebornPick = null;
                            return;
                        }
                        else _msg = "환생에 실패했다 — 환생석 소모를 확인할 것";
                    }
                }
                if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
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
                        {
                            Info(r, row++, Memorial.GearLine(ch));
                            if (!string.IsNullOrEmpty(Memorial.PartyLine(ch)))
                                Info(r, row++, Memorial.PartyLine(ch));
                            if (!string.IsNullOrEmpty(Memorial.TimeLine(ch)))
                                Info(r, row++, Memorial.TimeLine(ch));
                        }
                        continue;
                    }
                    string desc = Memorial.HasRecord(ch)
                        ? Memorial.Line(ch)
                        : Rebirth.RowDesc(ch, stones);
                    if (Row(r, row++, $"{ch.Name} · {ch.Job} Lv{ch.Level}", desc,
                            ItemAtlas.KeyFor(Economy.LifeItem.RebornStone)))
                    {
                        if (stones <= 0) _msg = "환생석이 없다. 10층 보스를 잡아야 한다(§4)";
                        else if (RebirthSkill.NeedsPick(ch))
                        { _rebornPick = ch; _msg = ""; return; }
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
                        if (!string.IsNullOrEmpty(Memorial.PartyLine(ch)))
                            Info(r, row++, Memorial.PartyLine(ch));
                        if (!string.IsNullOrEmpty(Memorial.TimeLine(ch)))
                            Info(r, row++, Memorial.TimeLine(ch));
                        if (!string.IsNullOrEmpty(Memorial.RebirthLine(ch)))
                            Info(r, row++, Memorial.RebirthLine(ch));
                    }
                }
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
            if (Row(r, row, "← 영지로", "건물에서 나온다"))
            { _sub = Sub.없음; _msg = ""; _rebornPick = null; }
        }

        /// <summary>
        /// 수비대 — 로스터를 최대 5명 세운다(§13-5).
        /// 소비처는 출전 제외다. 침략 본게임(적 별·약탈)은 여기서 열지 않는다.
        /// </summary>
        void Barracks(Rect r)
        {
            int row = 0;
            DrawHubUpgradeRow(r, ref row, EstateGrid.Cell.Barracks);
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
            DrawHubUpgradeRow(r, ref row, EstateGrid.Cell.Smith);
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
                    else if (!BagSlots.CanAddGear())
                        Locked(r, row++, $"{rec.Name} 제작", BagSlots.WhyFull(), craftIcon, GearGrade.Common);
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
                        _msg = Equipment.TryUnequip(ch)
                            ? $"{ch.Name}의 장비를 벗겼다"
                            : BagSlots.WhyFull();
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
                    if (!EquipJob.CanWear(ch, bag[0]))
                        _msg = EquipJob.WhyNot(ch, bag[0]);
                    else if (!EquipLevel.CanWear(ch, bag[0]))
                        _msg = EquipLevel.WhyNot(ch, bag[0]);
                    else
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
            var well = UiAtlas.ContentRect(panel, "panel", 2f);
            float cell = Mathf.Min(96f, well.width / items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                float x = well.x + i * cell;
                float ih = Mathf.Min(40f, well.height);
                ItemAtlas.Draw(new Rect(x, well.y + (well.height - ih) * 0.5f, ih, ih),
                    ItemAtlas.KeyFor(items[i]));
                UiPages.LabelClip(new Rect(x + ih + 4f, well.y, Mathf.Max(12f, cell - ih - 6f), well.height),
                    GameState.Bag.GetCount(items[i]).ToString(),
                    new GUIStyle(GUI.skin.label) { fontSize = 18, clipping = TextClipping.Clip,
                        normal = { textColor = new Color(0.95f, 0.79f, 0.42f) } });
            }
        }
    }
}
