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
        protected override string Title => "월드맵";
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.WorldMap);
        protected override string BackgroundArt => "bg_worldmap";
        protected override string Subtitle
        {
            get
            {
                if (WorldStar.ShowRangeQa) return WorldStar.SenseLine();
                if (WorldStar.ShowQa) return WorldStar.SizeLine();
                if (WorldMapDockCap.ShowQa) return WorldMapDockCap.Line();
                if (WorldMapHud.ShowQa) return WorldMapHud.Line();
                string s = "내 별 " + WorldStar.SizeLabel(GameState.TowerFloor);
                if (WorldStar.RaceSensePercent() == WorldStar.ElfSensePercent)
                    s += " · " + WorldStar.RaceSenseLine();
                if (WorldStar.EnemyPercent() == WorldStar.EnemyDebuffPercent)
                    s += " · " + WorldStar.EnemyLine();
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
                return s + " · 침략은 탑 30층(§14·§15)";
            }
        }

        /// <summary>침략 해금 층(§15 ✅ "탑 30층 이상 등반 시 해금"). 경매장과 **동시** 해금이다
        /// (SceneStructureBuilder "30층 돌파 → 침략·경매장 동시 해금").</summary>
        public const int InvasionUnlockFloor = 30;

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
            Hint(WorldStar.Caption(plate, icon), starCap);

            if (InvasionApproach.Picking && InvasionHubLockReason() == null)
            {
                DrawApproachPick(r);
                return;
            }

            var cards = WorldMapHud.Cards(r);
            DrawCard(cards[0], "성계 이동",
                WorldMapDockCap.Star(),
                "worldmap", locked: true);

            if (DrawCard(cards[1], "침략",
                    WorldMapDockCap.Caption(),
                    "damage", locked: WorldMapDockCap.IsLocked)
                && !WorldMapDockCap.IsLocked)
            {
                if (InvasionApproach.Blocked) GameFlow.TryGoInvasion();
                else InvasionApproach.Picking = true;
            }

            DrawCard(cards[2], "랭킹", WorldMapDockCap.Rank(),
                "characters", locked: true);
            if (DrawCard(cards[3], $"수비대 {DefenseState.Count}/{DefenseState.MaxSlots}",
                    WorldMapDockCap.Defense(), "building_barracks",
                    locked: !InboundRaid.Pending)
                && InboundRaid.Pending)
                InboundRaid.Settle();
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
