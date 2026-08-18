using System;

namespace AshesToStars
{
    /// <summary>
    /// 월드맵 도크 부제. 침략은 면·출정, 성계·랭킹·수비대는 미구현 설명을 줄인다.
    /// QA_NO면 옛 긴 줄. WorldMapScreen이 읽는다.
    /// </summary>
    public static class WorldMapDockCap
    {
        public const string EnvShow = "QA_WORLD_DOCK";
        public const string EnvNo = "QA_NO_WORLD_DOCK";
        /// <summary>월드맵 도크 한 칸. 「잠김 — 」을 붙여도 한 줄.</summary>
        public const int CaptionMaxRunes = 18;
        public const string LockFloor = "30층 해금";
        public const string LockOverdue = "연체 불가";
        public const string LockShield = "보호막";
        public const string OpenTail = "칸 · 출정";
        public const string OldStar = "성계 시스템 미구현 — 지금은 영지·필드·탑만 오간다(§13-6)";
        public const string OldRank = "랭킹 서버 없음 — 온라인 기능이다(§15)";
        public const string OldDefense = "침략 전투는 아직 없다(§13-5)";
        public const string StarCap = "로컬 허브만";
        public const string RankCap = "서버 없음";
        public const string DefenseCap = "침략 없음";

        static bool _qaSeeded;

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

        public static bool IsLocked =>
            WorldMapScreen.InvasionHubLockReason() != null;

        public static string Line() => Blocked
            ? "부제가 두 줄이다"
            : "수비대 부제는 한 줄이다(§16)";

        public static int RuneCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsLowSurrogate(text[i])) n++;
            }
            return n;
        }

        public static bool CaptionFits(string text) =>
            RuneCount(text) <= CaptionMaxRunes;

        /// <summary>옛 잠금 사유. 30층 문장·연체·보호막을 그대로 붙였다.</summary>
        public static string OldLock() =>
            WorldMapScreen.InvasionHubLockReason() ?? "";

        /// <summary>옛 소비처 — 면 안내와 출정 비용을 이어 붙였다.</summary>
        public static string OldOpen()
        {
            string open = InvasionApproach.Blocked
                ? $"진입 {EstateGrid.InvaderSide()} {EstateGrid.InvaderPath()}칸 · 출정 {Economy.FormatCurrency(InvasionState.SortieCost())} (§13-3·§15)"
                : $"{InvasionApproach.Line()} · 출정 {Economy.FormatCurrency(InvasionState.SortieCost())}";
            if (EstateStore.ShowQa)
                open = EstateStore.Line() + " · " + open;
            if (Economy.RaceCostPercent() == Economy.DwarfCostPercent)
                open = $"{Economy.RaceCostLine()} · " + open;
            else if (InvasionState.RaceLootPercent() == InvasionState.BeastLootPercent)
                open = $"{InvasionState.RaceLootLine()} · 예상 {Economy.FormatCurrency(InvasionState.LootCopper())} · " + open;
            else if (WorldStar.EnemyPercent() == WorldStar.EnemyDebuffPercent)
                open = $"{WorldStar.EnemyLine()} · 예상 {Economy.FormatCurrency(InvasionState.LootCopper())} · " + open;
            else if (Environment.GetEnvironmentVariable(InvasionState.EnvShowCap) == "1"
                     && !InvasionState.LootCapBlocked)
                open = $"{InvasionState.LootCapLine()} · 예상 {Economy.FormatCurrency(InvasionState.LootCopper())} · " + open;
            else if (Environment.GetEnvironmentVariable(InvasionState.EnvShowFloor) == "1"
                     && !InvasionState.LootFloorBlocked)
                open = $"{InvasionState.LootFloorLine()} · 예상 {Economy.FormatCurrency(InvasionState.LootCopper())} · " + open;
            else if (Environment.GetEnvironmentVariable(InvasionState.EnvShowWarehouse) == "1"
                     && !InvasionState.LootWarehouseBlocked)
                open = $"{InvasionState.WarehouseLootLine()} · 예상 {Economy.FormatCurrency(InvasionState.LootCopper())} · " + open;
            else if ((Environment.GetEnvironmentVariable(Honor.EnvShow) == "1"
                      || Environment.GetEnvironmentVariable(Honor.EnvShowDefense) == "1")
                     && !Honor.Blocked)
                open = $"{Honor.WinLine()} · " + open;
            else if (Environment.GetEnvironmentVariable(InvasionState.EnvShowRepeat) == "1"
                     && !InvasionState.RepeatLootBlocked)
                open = $"{InvasionState.RepeatLootLine()} · 예상 {Economy.FormatCurrency(InvasionState.LootCopper())} · " + open;
            return open;
        }

        public static string Lock()
        {
            if (Blocked) return OldLock();
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                return LockFloor;
            if (!GameState.CanInvade())
                return LockOverdue;
            if (InvasionState.ShieldActive)
                return LockShield;
            return LockFloor;
        }

        public static string Open()
        {
            if (Blocked) return OldOpen();
            return $"{InvasionApproach.Side} {InvasionApproach.Path()}{OpenTail}";
        }

        /// <summary>잠기면 Lock, 아니면 Open. DrawCard가 잠김 접두를 붙인다.</summary>
        public static string Caption() => IsLocked ? Lock() : Open();

        /// <summary>옛 소비처 — 미구현 설명을 통째로 붙였다.</summary>
        public static string Star() => Blocked ? OldStar : StarCap;

        /// <summary>옛 소비처 — 서버 부재 설명을 통째로 붙였다.</summary>
        public static string Rank() => Blocked ? OldRank : RankCap;

        /// <summary>옛 소비처 — 침략 본게임이 없다는 문장을 통째로 붙였다.</summary>
        public static string Defense() => Blocked ? OldDefense : DefenseCap;

        /// <summary>시각 QA. 30층이라 카드가 열리고 최단 면 부제가 한 줄.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            if (GameState.Wallet.Copper < 10_000)
                GameState.Grant(10_000);
            InvasionState.ResetForTest();
            InvasionApproach.ResetForTest();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
