using System;
using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>월드맵 — 우주 성계. 침략은 30층 달성 시 해금(§14·§15).</summary>
    public class WorldMapScreen : GameScreen
    {
        public const string EnvNoPlayerCopy = "QA_NO_WORLD_MAP_PLAYER_COPY";

        public static string PlayerCopy(string value)
        {
            if (string.IsNullOrEmpty(value)
                || Environment.GetEnvironmentVariable(EnvNoPlayerCopy) == "1")
                return value;
            return value.Replace("(§18-13)", "")
                .Replace("(§18-9)", "")
                .Replace("(§14·§15)", "")
                .Replace("(§14)", "");
        }

        protected override string Title => "월드맵";
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.WorldMap);
        protected override string BackgroundArt => "bg_worldmap";
        protected override string Subtitle
        {
            get
            {
                if (WorldExplore.ShowQa) return PlayerCopy(WorldExplore.Line());
                if (WorldStar.ShowRangeQa) return PlayerCopy(WorldStar.SenseLine());
                if (WorldStar.ShowQa) return PlayerCopy(WorldStar.SizeLine());
                if (WorldStar.ShowDebuffCapQa) return PlayerCopy(WorldStar.DebuffCapLine());
                if (WorldMapDockCap.ShowQa) return WorldMapDockCap.Line();
                if (WorldMapHud.ShowQa) return WorldMapHud.Line();
                string s = "내 별 " + WorldStar.SizeLabel(GameState.TowerFloor);
                if (WorldStar.RaceSensePercent() == WorldStar.ElfSensePercent)
                    s += " · " + WorldStar.RaceSenseLine();
                if (!WorldExplore.Blocked && WorldExplore.Percent() == WorldExplore.ElfPercent)
                    s += " · " + WorldExplore.Line();
                if (WorldStar.EnemyPercent() == WorldStar.EnemyDebuffPercent)
                    s += " · " + WorldStar.EnemyLine();
                string debuffCap = WorldStar.DebuffCapLine();
                if (!string.IsNullOrEmpty(debuffCap))
                    s += " · " + debuffCap;
                if (Economy.RaceCostPercent() == Economy.DwarfCostPercent)
                    s += " · " + Economy.RaceCostLine();
                if (Environment.GetEnvironmentVariable(InvasionState.EnvShowCap) == "1"
                    && !InvasionState.LootCapBlocked)
                    s += " · " + InvasionState.LootCapLine();
                if (Environment.GetEnvironmentVariable(InvasionState.EnvShowFloor) == "1"
                    && !InvasionState.LootFloorBlocked)
                    s += " · " + InvasionState.LootFloorLine();
                if (Environment.GetEnvironmentVariable(InvasionState.EnvShowWarehouse) == "1"
                    && !InvasionState.LootWarehouseBlocked)
                    s += " · " + InvasionState.WarehouseLootLine();
                if ((Environment.GetEnvironmentVariable(Honor.EnvShow) == "1"
                     || Environment.GetEnvironmentVariable(Honor.EnvShowDefense) == "1")
                    && !Honor.Blocked)
                    s += " · " + Honor.WinLine();
                if (Honor.ShowGuardQa)
                    s += " · " + Honor.GuardLine();
                if (Environment.GetEnvironmentVariable(InvasionState.EnvShowRepeat) == "1"
                    && !InvasionState.RepeatLootBlocked)
                    s += " · " + InvasionState.RepeatLootLine();
                if (InvasionApproach.ShowQa || InvasionApproach.HasPick || InvasionApproach.Picking)
                    s += " · " + InvasionApproach.Line();
                if (EstateStore.ShowQa)
                    s += " · " + EstateStore.Line();
                return PlayerCopy(s + " · 침략은 탑 30층(§14·§15)");
            }
        }

        /// <summary>침략 해금 층(§15 ✅ "탑 30층 이상 등반 시 해금"). 경매장과 **동시** 해금이다
        /// (SceneStructureBuilder "30층 돌파 → 침략·경매장 동시 해금").</summary>
        public const int InvasionUnlockFloor = 30;

        enum Hub { 없음, 성계, 랭킹 }
        Hub _hub;
        int _starPick = -1;
        int _rankBoard;
        string _netMsg;

        /// <summary>허브 침략 버튼 잠금 사유. null이면 기존 GoBattle(WorldMap)만 유지한다.</summary>
        public static string InvasionHubLockReason(long nowUnix)
        {
            if (GameState.TowerFloor < InvasionUnlockFloor)
                return $"탑 {InvasionUnlockFloor}층 달성 시 해금(현재 {GameState.TowerFloor}층) — 30층 미만은 초보 보호(§15)";
            if (!GameState.CanInvade(nowUnix))
                return GameState.InvasionBlockReason(nowUnix);
            if (InvasionState.ShieldActive)
                return InvasionState.ShieldBlockReason();
            return null;
        }
        public static string InvasionHubLockReason() => InvasionHubLockReason(InvasionState.NowUnix());

        protected override void Body(Rect r)
        {
            InvasionState.SeedQaIfRequested();
            InvasionState.SeedRaceLootQaIfRequested();
            InvasionState.SeedRaceCostQaIfRequested();
            InvasionState.SeedAuraDebuffQaIfRequested();
            InvasionState.SeedLootCapQaIfRequested();
            InvasionState.SeedLootFloorQaIfRequested();
            InvasionState.SeedWarehouseLootQaIfRequested();
            InvasionState.SeedRepeatLootQaIfRequested();
            Honor.SeedQaIfRequested();
            Honor.SeedDefenseQaIfRequested();
            Honor.SeedGuardQaIfRequested();
            InboundRaid.OfferIfDue();
            WorldStar.SeedQaIfRequested();
            WorldStar.SeedRangeQaIfRequested();
            WorldStar.SeedRaceSenseQaIfRequested();
            WorldStar.SeedAuraDebuffQaIfRequested();
            WorldStar.SeedDebuffCapQaIfRequested();
            WorldExplore.SeedQaIfRequested();
            InvasionApproach.SeedQaIfRequested();
            EstateStore.SeedQaIfRequested();
            WorldMapHud.SeedQaIfRequested();
            WorldMapDockCap.SeedQaIfRequested();
            var plate = WorldStar.Plate(r);
            if (!UiAtlas.DrawSliced(plate, "panel", 14f, new Color(1f, 1f, 1f, 0.92f)))
                UiAtlas.Draw(plate, "panel", new Color(1f, 1f, 1f, 0.92f));
            var icon = WorldStar.Icon(plate, GameState.TowerFloor);
            UiAtlas.DrawFit(icon, "worldmap");
            // 별 크기·영공 수치는 헤더 부제(Subtitle)가 이미 「내 별 N층 · 별 ×… · 영공 …」으로
            // 보여준다 — 배너까지 같은 SizeLabel을 반복하면 그 줄이 화면에 두 번 겹쳐 보인다
            // (오너 지적 「중복」, 폴리싱 결함). 배너는 옆의 별 아이콘이 층마다 커지는 §14
            // 규칙 설명만 맡고, 수치는 헤더에 위임한다.
            string starCap = "층을 오를수록 내 별이 커진다(§14)";
            if (WorldStar.RaceSensePercent() == WorldStar.ElfSensePercent)
                starCap = WorldStar.RaceSenseLine() + " · " + starCap;
            Hint(WorldStar.Caption(plate, icon), PlayerCopy(starCap));

            if (InvasionApproach.Picking && InvasionHubLockReason() == null)
            {
                DrawApproachPick(r);
                return;
            }

            if (_hub == Hub.성계)
            {
                DrawTravel(r);
                return;
            }
            if (_hub == Hub.랭킹)
            {
                DrawRank(r);
                return;
            }

            if (!WorldExplore.Blocked && !WorldMapHud.Blocked)
            {
                var after = WorldStar.AfterPlate(r);
                float dockTop = WorldMapHud.Dock(r).y;
                var field = new Rect(after.x, after.y, after.width,
                    Mathf.Max(0f, dockTop - after.y));
                Styles();
                WorldExplore.Draw(field, _panel);
                var ev = Event.current;
                if (!LocalNet.Blocked && ev != null && ev.type == EventType.MouseDown && ev.button == 0)
                {
                    int hit = WorldExplore.HitStar(field, ev.mousePosition);
                    if (hit >= 0)
                    {
                        _starPick = hit;
                        _hub = Hub.성계;
                        _netMsg = "";
                        ev.Use();
                    }
                }
            }

            var cards = WorldMapHud.Cards(r);
            if (DrawCard(cards[0], "성계 이동",
                    WorldMapDockCap.Star(),
                    "worldmap", locked: LocalNet.Blocked)
                && !LocalNet.Blocked)
            {
                _hub = Hub.성계;
                _starPick = -1;
                _netMsg = "";
            }

            if (DrawCard(cards[1], "침략",
                    WorldMapDockCap.Caption(),
                    "damage", locked: WorldMapDockCap.IsLocked)
                && !WorldMapDockCap.IsLocked)
            {
                if (InvasionApproach.Blocked) GameFlow.TryGoInvasion();
                else InvasionApproach.Picking = true;
            }

            if (DrawCard(cards[2], "랭킹", WorldMapDockCap.Rank(),
                    "characters", locked: LocalNet.Blocked)
                && !LocalNet.Blocked)
            {
                _hub = Hub.랭킹;
                _rankBoard = 0;
                _netMsg = "";
            }
            if (DrawCard(cards[3], $"수비대 {DefenseState.Count}/{DefenseState.MaxSlots}",
                    WorldMapDockCap.Defense(), "building_barracks",
                    locked: !InboundRaid.Pending)
                && InboundRaid.Pending)
            {
                if (!InboundRaid.TryFight())
                    InboundRaid.Settle();
            }
        }

        void DrawTravel(Rect r)
        {
            var stars = WorldExplore.Neighbors();
            int floor = Mathf.Max(1, GameState.TowerFloor);
            if (!string.IsNullOrEmpty(_netMsg)) Info(r, 0, _netMsg);
            if (_starPick < 0 || _starPick >= stars.Length)
            {
                Info(r, string.IsNullOrEmpty(_netMsg) ? 0 : 1,
                    $"밝힌 별 {WorldExplore.RevealedCount(floor)}/{stars.Length} · 동맹 {LocalNet.AllyCount}/{LocalNet.AllyCap}");
                var body = new Rect(r.x, r.y + 80f, r.width, r.height - 160f);
                var cells = UiPages.Grid(body, 3, 1, 12f);
                for (int i = 0; i < stars.Length && i < cells.Length; i++)
                {
                    bool seen = WorldExplore.Revealed(stars[i].Dist, floor);
                    string sub = !seen ? "안개 — 탐험 반경 밖"
                        : LocalNet.IsAlly(stars[i].Name) ? "동맹 · 침략 불가"
                        : "침략 · 동맹 · 지나감";
                    if (DrawCard(cells[i], stars[i].Name, sub, "worldmap", locked: !seen) && seen)
                    {
                        _starPick = i;
                        _netMsg = "";
                    }
                }
                if (Row(r, 6, "← 월드맵", "성계에서 나온다"))
                {
                    _hub = Hub.없음;
                    _starPick = -1;
                    _netMsg = "";
                }
                return;
            }

            var star = stars[_starPick];
            bool ally = LocalNet.IsAlly(star.Name);
            Info(r, string.IsNullOrEmpty(_netMsg) ? 0 : 1,
                $"{star.Name} · {(ally ? "동맹" : "중립")} · 거리 {star.Dist:0.0}");
            int row = 2;
            string invadeWhy = InvasionHubLockReason();
            if (ally)
                Locked(r, row++, "침략", "동맹은 서로 침략할 수 없다", "damage");
            else if (invadeWhy != null)
                Locked(r, row++, "침략", invadeWhy, "damage");
            else if (Row(r, row++, "침략", "이 별의 수비대와 싸운다", "damage"))
            {
                LocalNet.MarkVisit(star.Name);
                if (InvasionApproach.Blocked) GameFlow.TryGoInvasion();
                else InvasionApproach.Picking = true;
                _hub = Hub.없음;
            }
            if (ally)
            {
                if (Row(r, row++, "동맹 해제", "버프 대상에서 뺀다", "healer"))
                {
                    LocalNet.TryUnally(star.Name);
                    _netMsg = $"{star.Name} 동맹을 해제했다";
                    _starPick = -1;
                }
            }
            else
            {
                string allyWhy = LocalNet.WhyCannotAlly(star.Name);
                if (allyWhy != null)
                    Locked(r, row++, "동맹 신청", allyWhy, "healer");
                else if (Row(r, row++, "동맹 신청", "로컬은 바로 승인된다 · 침략 불가", "healer"))
                {
                    LocalNet.TryAlly(star.Name);
                    _netMsg = $"{star.Name}과 동맹을 맺었다";
                    _starPick = -1;
                }
            }
            if (Row(r, row++, "지나감", "영공을 스치고 본 별로 돌아간다", "field"))
            {
                LocalNet.MarkVisit(star.Name);
                _netMsg = $"{star.Name}을 지나갔다";
                _starPick = -1;
            }
            if (Row(r, row, "← 별 목록", "다른 별을 고른다"))
            {
                _starPick = -1;
            }
        }

        void DrawRank(Rect r)
        {
            string[] names = { "최고 층", "명예", "수비 실적" };
            _rankBoard = DrawTabs(new Rect(r.x, r.y, r.width, UiPages.TabH), names, _rankBoard);
            var board = (LocalNet.Board)_rankBoard;
            var rows = LocalNet.BoardRows(board);
            Info(r, 1, $"로컬 주간 · 내 순위 {LocalNet.MyPlace(board)}위 · {Honor.BalanceLine()}");
            var body = UiPages.AfterTabs(new Rect(r.x, r.y + 56f, r.width, r.height - 120f));
            for (int i = 0; i < rows.Length; i++)
            {
                string mark = rows[i].Mine ? "★ " : "";
                Info(body, i, $"{i + 1}위  {mark}{rows[i].Name}  ·  {rows[i].Score}");
            }
            if (Row(r, 7, "← 월드맵", "랭킹에서 나온다"))
            {
                _hub = Hub.없음;
                _netMsg = "";
            }
        }

        void DrawApproachPick(Rect r)
        {
            var cards = WorldMapHud.Cards(r);
            var sides = EstateGrid.Sides;
            for (int i = 0; i < sides.Length && i < cards.Length; i++)
            {
                var side = sides[i];
                bool open = InvasionApproach.CanPick(side);
                if (DrawCard(cards[i], InvasionApproach.CardTitle(side),
                        InvasionApproach.CardBody(side), "damage", locked: !open)
                    && open)
                {
                    InvasionApproach.Pick(side);
                    GameFlow.TryGoInvasion();
                }
            }
        }
    }
}
