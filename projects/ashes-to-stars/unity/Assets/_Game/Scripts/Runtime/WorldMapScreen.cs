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
                string s = "내 별 " + WorldStar.SizeLabel(GameState.TowerFloor);
                if (WorldStar.RaceSensePercent() == WorldStar.ElfSensePercent)
                    s += " · " + WorldStar.RaceSenseLine();
                if (WorldStar.EnemyPercent() == WorldStar.EnemyDebuffPercent)
                    s += " · " + WorldStar.EnemyLine();
                if (Economy.RaceCostPercent() == Economy.DwarfCostPercent)
                    s += " · " + Economy.RaceCostLine();
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
            WorldStar.SeedRaceSenseQaIfRequested();
            WorldStar.SeedAuraDebuffQaIfRequested();
            var plate = WorldStar.Plate(r);
            if (!UiAtlas.DrawSliced(plate, "panel", 14f, new Color(1f, 1f, 1f, 0.92f)))
                UiAtlas.Draw(plate, "panel", new Color(1f, 1f, 1f, 0.92f));
            var icon = WorldStar.Icon(plate, GameState.TowerFloor);
            UiAtlas.DrawFit(icon, "worldmap");
            string starCap = "내 별 · " + WorldStar.SizeLabel(GameState.TowerFloor)
                + " — 층을 오를수록 커진다(§14)";
            if (WorldStar.RaceSensePercent() == WorldStar.ElfSensePercent)
                starCap = WorldStar.RaceSenseLine() + " · " + starCap;
            Hint(WorldStar.Caption(plate, icon), starCap);

            var cards = UiPages.Grid(WorldStar.AfterPlate(r), 2, 2, 16f);
            DrawCard(cards[0], "성계 이동",
                "성계 시스템 미구현 — 지금은 영지·필드·탑만 오간다(§13-6)",
                "worldmap", locked: true);

            string invasionLock = InvasionHubLockReason();
            string invasionOpen = $"진입 {EstateGrid.InvaderSide()} {EstateGrid.InvaderPath()}칸 · 출정 {Economy.FormatCurrency(InvasionState.SortieCost())} (§13-3·§15)";
            if (Economy.RaceCostPercent() == Economy.DwarfCostPercent)
                invasionOpen = $"{Economy.RaceCostLine()} · " + invasionOpen;
            else if (InvasionState.RaceLootPercent() == InvasionState.BeastLootPercent)
                invasionOpen = $"{InvasionState.RaceLootLine()} · 예상 {Economy.FormatCurrency(InvasionState.LootCopper())} · " + invasionOpen;
            else if (WorldStar.EnemyPercent() == WorldStar.EnemyDebuffPercent)
                invasionOpen = $"{WorldStar.EnemyLine()} · 예상 {Economy.FormatCurrency(InvasionState.LootCopper())} · " + invasionOpen;
            if (DrawCard(cards[1], "침략",
                    invasionLock ?? invasionOpen,
                    "damage", locked: invasionLock != null)
                && invasionLock == null)
                GameFlow.TryGoInvasion();

            DrawCard(cards[2], "랭킹", "랭킹 서버 없음 — 온라인 기능이다(§15)",
                "characters", locked: true);
            DrawCard(cards[3], $"수비대 {DefenseState.Count}/{DefenseState.MaxSlots}",
                "침략 전투는 아직 없다(§13-5)", "building_barracks", locked: true);
        }
    }
}
