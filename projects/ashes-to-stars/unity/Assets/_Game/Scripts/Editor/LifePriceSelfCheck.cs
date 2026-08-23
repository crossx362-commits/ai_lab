using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>목숨 아이템 경매 시세 하한·상한. QA_NO면 옛 고정가·상한 없음(§4·§18-4).</summary>
    public static class LifePriceSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Life Price Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(LifePrice.EnvShow);
            string no = Environment.GetEnvironmentVariable(LifePrice.EnvNo);
            string trade = Environment.GetEnvironmentVariable(AuctionTrade.EnvShow);
            Environment.SetEnvironmentVariable(LifePrice.EnvShow, null);
            Environment.SetEnvironmentVariable(LifePrice.EnvNo, null);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            LifePrice.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);
            GameState.Grant(2_000_000);

            Check(Mathf.Approximately(LifePrice.Hours(Economy.LifeItem.ScrollOfReturn), 2f),
                "두루마리 2 G/h");
            Check(Mathf.Approximately(LifePrice.Hours(Economy.LifeItem.RevivalTea), 3f),
                "부활초 3 G/h");
            Check(Mathf.Approximately(LifePrice.Hours(Economy.LifeItem.RebornStone), 150f),
                "환생석 150 G/h");
            Check(Mathf.Approximately(LifePrice.CeilHoursOf(Economy.LifeItem.ScrollOfReturn), 4f),
                "두루마리 상한 4 G/h");
            Check(Mathf.Approximately(LifePrice.CeilHoursOf(Economy.LifeItem.RevivalTea), 8f),
                "부활초 상한 8 G/h");
            Check(Mathf.Approximately(LifePrice.CeilHoursOf(Economy.LifeItem.RebornStone), 300f),
                "환생석 상한 300 G/h");
            Check(LifePrice.Hours(Economy.LifeItem.CraftHide) == 0f, "가죽은 하한 없음");
            Check(LifePrice.CeilHoursOf(Economy.LifeItem.CraftHide) == 0f, "가죽 상한 없음");
            Check(LifePrice.Ceil(Economy.LifeItem.CraftHide) == 0, "가죽 Ceil 0");
            Check(LifePrice.Copper(150f, 0) == 1_500_000,
                $"T1 환생석 150골드 (실제 {LifePrice.Copper(150f, 0)})");
            Check(LifePrice.Copper(300f, 0) == 3_000_000,
                $"T1 환생석 상한 300골드 (실제 {LifePrice.Copper(300f, 0)})");
            Check(LifePrice.Copper(3f, 0) == 30_000,
                $"T1 부활초 3골드 (실제 {LifePrice.Copper(3f, 0)})");
            Check(LifePrice.Copper(8f, 0) == 80_000,
                $"T1 부활초 상한 8골드 (실제 {LifePrice.Copper(8f, 0)})");
            Check(LifePrice.Copper(2f, 0) == 20_000,
                $"T1 두루마리 2골드 (실제 {LifePrice.Copper(2f, 0)})");
            Check(LifePrice.Copper(4f, 0) == 40_000,
                $"T1 두루마리 상한 4골드 (실제 {LifePrice.Copper(4f, 0)})");
            Check(LifePrice.Ceil(Economy.LifeItem.RebornStone) == 3_000_000,
                $"T1 Ceil 300골드 (실제 {LifePrice.Ceil(Economy.LifeItem.RebornStone)})");
            long t5 = LifePrice.Copper(150f, 4);
            Check(t5 >= 9_830_000 && t5 <= 9_831_000,
                $"T5 환생석 ≈983골드 (실제 {t5})");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.RebornStone) == 1_500_000,
                $"ListPrice T1 환생석 (실제 {AuctionTrade.ListPrice(Economy.LifeItem.RebornStone)})");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.CraftHide) == 2_400, "가죽은 옛 값");

            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(!AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, 200_000),
                "하한 아래 200000은 거절");
            Check(GameState.Bag.GetCount(Economy.LifeItem.RebornStone) == 1, "거절은 가방을 안 뺀다");
            Check(!AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, 1_499_999),
                "하한 −1 거절");
            Check(AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, 1_500_000),
                "하한 정각은 등록");
            Check(AuctionState.MineCount == 1, "내 등록 1건");
            Check(LifePrice.Line().Contains("150골드") && LifePrice.Line().Contains("300골드")
                  && LifePrice.Line().Contains("§18-4"),
                $"줄 150~300골드 (실제 {LifePrice.Line()})");
            Check(LifePrice.AboveCeil(Economy.LifeItem.RebornStone, 3_000_001),
                "상한 +1은 AboveCeil");
            Check(!LifePrice.AboveCeil(Economy.LifeItem.RebornStone, 3_000_000),
                "상한 정각은 허용");
            Check(LifePrice.AboveCeil(Economy.LifeItem.RevivalTea, 80_001),
                "부활초 상한 +1은 AboveCeil");
            Check(LifePrice.AboveCeil(Economy.LifeItem.ScrollOfReturn, 40_001),
                "두루마리 상한 +1은 AboveCeil");
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(!AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, 3_000_001),
                "상한 +1은 거절");
            Check(GameState.Bag.GetCount(Economy.LifeItem.RebornStone) == 1, "상한 거절은 가방을 안 뺀다");
            Check(AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, 3_000_000),
                "상한 정각은 등록");
            Check(AuctionState.MineCount == 2, "하한+상한 2건");

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(51);
            GameState.TrySelectTier(4);
            GameState.Grant(15_000_000);
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            long t5Price = LifePrice.Copper(150f, 4);
            Check(AuctionTrade.ListPrice(Economy.LifeItem.RebornStone) == t5Price,
                $"T5 ListPrice (실제 {AuctionTrade.ListPrice(Economy.LifeItem.RebornStone)})");
            Check(!AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, 1_500_000),
                "T5에서 T1 하한은 거절");
            Check(AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, t5Price),
                "T5 하한은 등록");

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);
            Environment.SetEnvironmentVariable(LifePrice.EnvNo, "1");
            Check(LifePrice.Blocked, "QA_NO면 차단");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.RebornStone) == LifePrice.OldStone,
                $"차단하면 옛 20골드 (실제 {AuctionTrade.ListPrice(Economy.LifeItem.RebornStone)})");
            GameState.Grant(300_000);
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, LifePrice.OldStone),
                "차단하면 옛 가로 등록");
            Check(LifePrice.Ceil(Economy.LifeItem.RebornStone) == long.MaxValue,
                "차단하면 상한 없음");
            Check(!LifePrice.AboveCeil(Economy.LifeItem.RebornStone, 3_000_001),
                "차단하면 상한 +1도 허용");
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(AuctionState.TryListItem(Economy.LifeItem.RebornStone, 1, 3_000_001),
                "차단하면 상한 위도 등록");
            Check(LifePrice.Line().Contains("옛"),
                $"차단 문구 (실제 {LifePrice.Line()})");
            Environment.SetEnvironmentVariable(LifePrice.EnvNo, null);

            GameState.ResetAll();
            AuctionState.ResetForTest();
            AuctionTrade.ResetForTest();
            Environment.SetEnvironmentVariable(LifePrice.EnvShow, "1");
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, "1");
            AuctionTrade.SeedQaIfRequested();
            Check(GameState.TowerFloor >= 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(GameState.Tier == 0, $"시드 T1 (실제 {GameState.Tier})");
            Check(GameState.Bag.GetCount(Economy.LifeItem.RebornStone) >= 1, "시드 환생석");
            Check(AuctionTrade.TryFirstBag(out var seeded, out _)
                  && seeded == Economy.LifeItem.RebornStone,
                $"시드 첫 가방 환생석 (실제 {seeded})");
            Check(AuctionTrade.ListPrice(Economy.LifeItem.RebornStone) == 1_500_000,
                "시드 ListPrice 150골드");
            Check(AuctionTrade.TradeLine().Contains("150골드"),
                $"시드 문구 (실제 {AuctionTrade.TradeLine()})");
            Check(EstateScreen.AuctionHubLockReason() == null, "시드면 경매장이 열린다");
            Environment.SetEnvironmentVariable(LifePrice.EnvShow, null);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string auctionSrc = File.ReadAllText(Path.Combine(runtime, "AuctionState.cs"));
            string tradeSrc = File.ReadAllText(Path.Combine(runtime, "AuctionTrade.cs"));
            Check(auctionSrc.Contains("LifePrice.BelowFloor"),
                "TryListItem이 BelowFloor를 읽는다");
            Check(auctionSrc.Contains("LifePrice.AboveCeil"),
                "TryListItem이 AboveCeil을 읽는다");
            Check(auctionSrc.Contains("LifePrice.Floor"),
                "NPC가 Floor를 읽는다");
            Check(tradeSrc.Contains("LifePrice.Floor") && tradeSrc.Contains("LifePrice.SeedQaIfRequested"),
                "ListPrice·시드가 LifePrice를 읽는다");

            _ = nameof(LifePrice.Floor);
            _ = nameof(LifePrice.Ceil);
            _ = nameof(LifePrice.BelowFloor);
            _ = nameof(LifePrice.AboveCeil);
            _ = nameof(LifePrice.Line);

            string lifeSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/LifePrice.cs"));
            Check(lifeSrc.Contains("ShortCopper(stone)")
                  && lifeSrc.Contains("ShortCopper(hi)")
                  && lifeSrc.IndexOf("FormatCurrency(stone)") < 0
                  && lifeSrc.IndexOf("FormatCurrency(hi)") < 0,
                "목숨 시세 환생석·상한은 ShortCopper만");
            _ = nameof(AuctionTrade.ListPrice);
            _ = nameof(AuctionState.TryListItem);

            Environment.SetEnvironmentVariable(LifePrice.EnvShow, show);
            Environment.SetEnvironmentVariable(LifePrice.EnvNo, no);
            Environment.SetEnvironmentVariable(AuctionTrade.EnvShow, trade);
            LifePrice.ResetForTest();
            AuctionTrade.ResetForTest();
            AuctionState.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[LifePriceSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("LifePriceSelfCheck FAIL " + _fail);
            }
            Debug.Log("[LifePriceSelfCheck] PASS\n" + _log);
        }
    }
}
