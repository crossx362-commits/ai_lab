using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>영지 현황 HUD가 마을을 덜 가린다. QA_NO면 옛 2×2 전폭(§16).</summary>
    public static class EstateStatusHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Status Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateStatusHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(EstateStatusHud.EnvNo);
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvShow, null);
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvNo, null);
            EstateStatusHud.ResetForTest();

            var body = new Rect(36f, 118f, 1208f, EstateStatusHud.OldBodyH);
            var slim = EstateStatusHud.Cards(body);
            Check(slim.Length == EstateStatusHud.CardCount,
                $"도크 {EstateStatusHud.CardCount}칸 (실제 {slim.Length})");
            Check(Mathf.Approximately(EstateStatusHud.OverlayH(body), EstateStatusHud.DockH),
                $"겹침 {EstateStatusHud.OverlayH(body):0} = 도크 {EstateStatusHud.DockH:0}");
            Check(EstateStatusHud.OverlayH(body) < 100f, "겹침 < 100 (옛 540)");
            Check(EstateStatusHud.OverlayH(body) < body.height * 0.20f,
                $"겹침 {EstateStatusHud.OverlayH(body):0} < 본문 20%");
            Check(EstateStatusHud.OpenH(body) > 400f,
                $"열린 배경 {EstateStatusHud.OpenH(body):0} > 400");
            Check(slim[0].y > body.y + body.height * 0.70f,
                $"도크 y {slim[0].y:0} 는 아래쪽");
            Check(slim[0].height < 90f,
                $"도크 칸 높이 {slim[0].height:0} < 90");
            Check(slim[0].width > 180f,
                $"도크 칸 폭 {slim[0].width:0} 가로 카드");
            Check(UiPages.IsSlimCard(slim[0]), "도크 칸은 슬림 가로");
            Check(UiPages.TitleHOf(slim[0]) == UiPages.SlimTitleH,
                $"도크 제목 {UiPages.TitleHOf(slim[0]):0} = {UiPages.SlimTitleH:0}");
            Check(UiPages.CardChrome(slim[0]) == "button_normal",
                "도크 크롬은 얇은 버튼");
            UiPages.CardLayout(slim[0], true, out _, out var dockTitle, out var dockSub);
            Check(dockTitle.height + 0.01f >= UiPages.SlimTitleH,
                $"도크 제목 칸 {dockTitle.height:0} ≥ {UiPages.SlimTitleH:0}");
            Check(dockSub.height + 0.01f >= UiPages.SlimSubMin,
                $"도크 부제 칸 {dockSub.height:0} ≥ {UiPages.SlimSubMin:0}");
            float oldSub = UiAtlas.ContentRect(slim[0], "button_normal").height
                - UiPages.CardTitleH - 4f;
            Check(oldSub < UiPages.SlimSubMin,
                $"옛 제목 36이면 부제 {oldSub:0.0} < {UiPages.SlimSubMin:0} (네거티브)");
            Check(EstateStatusHud.CaptionFits(EstateStatusHud.AuraCaption()),
                $"영공 부제 (실제 {EstateStatusHud.AuraCaption()})");
            Check(EstateStatusHud.CaptionFits(EstateStatusHud.KeepCaption()),
                $"본성 부제 (실제 {EstateStatusHud.KeepCaption()})");
            Check(!EstateStatusHud.CaptionFits("Lv10 · 915골드 7실버 50쿠퍼"),
                "옛 본성 FormatCurrency 풀표기는 안 맞음");
            string keepSrc = File.ReadAllText(Path.Combine(
                Application.dataPath, "_Game/Scripts/Runtime/EstateStatusHud.cs"));
            int keepMethod = keepSrc.IndexOf("public static string KeepCaption()", StringComparison.Ordinal);
            int nextMethod = keepSrc.IndexOf("public static string WorldCaption()", StringComparison.Ordinal);
            Check(keepMethod >= 0 && nextMethod > keepMethod
                  && keepSrc.IndexOf("ShortCopper(EstateBuild.WarehouseCapCopper())", keepMethod, nextMethod - keepMethod) >= 0
                  && keepSrc.IndexOf("FormatCurrency", keepMethod, nextMethod - keepMethod) < 0,
                "KeepCaption은 ShortCopper만 쓴다");
            Check(keepSrc.IndexOf("OldKeepCaption", StringComparison.Ordinal) >= 0,
                "OldKeepCaption이 남아 있다");
            Check(EstateStatusHud.CaptionFits(EstateStatusHud.WorldCaption()),
                $"세계 부제 (실제 {EstateStatusHud.WorldCaption()})");
            Check(EstateStatusHud.CaptionFits(EstateStatusHud.MineCaption()),
                $"광산 부제 (실제 {EstateStatusHud.MineCaption()})");
            Check(!EstateStatusHud.CaptionFits("9150골드 7실버 50쿠퍼/h"),
                "옛 광산 FormatCurrency+/h 풀표기는 안 맞음");
            string mineSrc = File.ReadAllText(Path.Combine(
                Application.dataPath, "_Game/Scripts/Runtime/EstateStatusHud.cs"));
            int mineMethod = mineSrc.IndexOf("public static string MineCaption()", StringComparison.Ordinal);
            int nextMine = mineSrc.IndexOf("public static string OldStoreCaption()", StringComparison.Ordinal);
            if (nextMine < 0) nextMine = mineSrc.IndexOf("public static string StoreCaption()", StringComparison.Ordinal);
            Check(mineMethod >= 0 && nextMine > mineMethod
                  && mineSrc.IndexOf("ShortCopper(EstateMine.CopperPerHourEffective())", mineMethod, nextMine - mineMethod) >= 0
                  && mineSrc.IndexOf("FormatCurrency", mineMethod, nextMine - mineMethod) < 0,
                "MineCaption은 ShortCopper만 쓴다");
            Check(mineSrc.IndexOf("OldMineCaption", StringComparison.Ordinal) >= 0,
                "OldMineCaption이 남아 있다");
            Check(EstateStatusHud.CaptionFits(EstateStatusHud.StoreCaption()),
                $"창고 부제 (실제 {EstateStatusHud.StoreCaption()})");
            Check(!EstateStatusHud.CaptionFits("9150골드 7실버 50쿠퍼 / 12골드 0실버 0쿠퍼"),
                "옛 창고 FormatCurrency 풀표기는 안 맞음");
            string storeSrc = File.ReadAllText(Path.Combine(
                Application.dataPath, "_Game/Scripts/Runtime/EstateStatusHud.cs"));
            int storeMethod = storeSrc.IndexOf("public static string StoreCaption()", StringComparison.Ordinal);
            int nextStore = storeSrc.IndexOf("public static string ShortCopper(", StringComparison.Ordinal);
            if (nextStore < 0) nextStore = storeSrc.IndexOf("public static bool CaptionFits(", StringComparison.Ordinal);
            Check(storeMethod >= 0 && nextStore > storeMethod
                  && storeSrc.IndexOf("FormatCurrency", storeMethod, nextStore - storeMethod) < 0
                  && storeSrc.IndexOf("ShortCopper", storeMethod, nextStore - storeMethod) >= 0,
                "StoreCaption은 ShortCopper만 쓴다");
            Check(storeSrc.IndexOf("OldStoreCaption", StringComparison.Ordinal) >= 0,
                "OldStoreCaption이 남아 있다");
            Check(EstateStatusHud.Line().Contains("부제"),
                $"줄 (실제 {EstateStatusHud.Line()})");

            Environment.SetEnvironmentVariable(EstateStatusHud.EnvNo, "1");
            Check(EstateStatusHud.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(EstateStatusHud.OverlayH(body), body.height),
                $"차단 겹침 {EstateStatusHud.OverlayH(body):0} = 옛 540");
            var old = EstateStatusHud.Cards(body);
            Check(old[0].height > 70f && old[0].width > 1000f,
                $"차단 영공 {old[0].width:0}×{old[0].height:0} 전폭");
            Check(old[1].height > 150f, $"차단 칸 {old[1].height:0} 전폭 카드");
            Check(old[0].y < body.y + 4f, "차단하면 본문 위에서 시작");
            Check(EstateStatusHud.Line().Contains("가린다"),
                $"차단 줄 (실제 {EstateStatusHud.Line()})");
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvNo, null);

            Environment.SetEnvironmentVariable(EstateStatusHud.EnvShow, "1");
            EstateStatusHud.SeedQaIfRequested();
            Check(EstateStatusHud.ShowQa, "시드 켜짐");
            Check(EstateStatusHud.Line().Contains("부제"), "시드 줄");
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvShow, null);
            EstateStatusHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(estate.Contains("EstateStatusHud.Cards"), "영지가 Cards를 읽는다");
            Check(estate.Contains("EstateStatusHud.Line"), "자막이 Line을 읽는다");
            Check(estate.Contains("EstateStatusHud.KeepCaption"), "본성이 KeepCaption을 읽는다");
            Check(estate.Contains("EstateStatusHud.MineCaption"), "광산이 MineCaption을 읽는다");
            Check(estate.Contains("EstateStatusHud.StoreCaption"), "창고가 StoreCaption을 읽는다");
            Check(estate.Contains("EstateStatusHud.SeedQaIfRequested"), "시드를 읽는다");

            // 건물 칸 상세 미리보기(YardInspectLine): 본성·광산·창고는 ShortCopper만
            // 끝은 바로 다음 메서드(CoreBuildCaption). AuctionHubLockReason 호출/정의로 자르면 안 됨.
            int yi = estate.IndexOf("string YardInspectLine");
            Check(yi >= 0, "YardInspectLine이 있다");
            int yiEnd = estate.IndexOf("static string CoreBuildCaption", yi);
            string yiSrc = yi >= 0 && yiEnd > yi ? estate.Substring(yi, yiEnd - yi) : "";
            Check(yiSrc.Contains("EstateStatusHud.ShortCopper")
                  && yiSrc.IndexOf("FormatCurrency") < 0,
                "YardInspectLine 본성·광산·창고는 ShortCopper만 (FormatCurrency 없음)");
            Check(yiSrc.Contains("WarehouseCapCopper()")
                  && yiSrc.Contains("CopperPerHourEffective()")
                  && yiSrc.Contains("GameState.Wallet.Copper"),
                "YardInspectLine이 본성·광산·창고 값을 읽는다");

            // 본성 상세 Info: 창고 칸만 ShortCopper (업그레이드 비용 줄은 다음 결함)
            int keepFn = estate.IndexOf("void Keep(Rect");
            Check(keepFn >= 0, "Keep 상세가 있다");
            int keepEnd = estate.IndexOf("void DrawCoreRush", keepFn);
            string keepDetailSrc = keepFn >= 0 && keepEnd > keepFn
                ? estate.Substring(keepFn, keepEnd - keepFn) : "";
            int infoWh = keepDetailSrc.IndexOf("창고 ");
            Check(infoWh >= 0, "본성 Info에 창고 줄이 있다");
            // Info 첫 줄만: FormatCurrency(WarehouseCap) 금지 · ShortCopper(WarehouseCap) 필수
            int infoLine = keepDetailSrc.IndexOf("Info(r, 0");
            int infoSemi = infoLine >= 0 ? keepDetailSrc.IndexOf(";", infoLine) : -1;
            string info0 = infoLine >= 0 && infoSemi > infoLine
                ? keepDetailSrc.Substring(infoLine, infoSemi - infoLine) : "";
            Check(info0.Contains("EstateStatusHud.ShortCopper")
                  && info0.Contains("WarehouseCapCopper()")
                  && info0.IndexOf("FormatCurrency") < 0,
                "본성 Info 창고는 ShortCopper만");

            // Keep 업그레이드 비용 줄: ShortCopper(UpgradeCost) · Info/업그레이드 desc만 범위
            int upDesc = keepDetailSrc.IndexOf("string desc =");
            int upSemi = upDesc >= 0 ? keepDetailSrc.IndexOf(";", upDesc) : -1;
            string up0 = upDesc >= 0 && upSemi > upDesc
                ? keepDetailSrc.Substring(upDesc, upSemi - upDesc) : "";
            Check(up0.Contains("EstateStatusHud.ShortCopper")
                  && up0.Contains("UpgradeCost")
                  && up0.IndexOf("FormatCurrency") < 0,
                "본성 업그레이드 비용은 ShortCopper만");

            // DrawCoreRush 골드 단축: ShortCopper(GoldCostToFloor) — 업비 줄과 별개
            int rushFn = estate.IndexOf("void DrawCoreRush(Rect");
            Check(rushFn >= 0, "DrawCoreRush가 있다");
            int rushEnd = estate.IndexOf("void DrawHubUpgradeRow", rushFn);
            string rushSrc = rushFn >= 0 && rushEnd > rushFn
                ? estate.Substring(rushFn, rushEnd - rushFn) : "";
            int gl = rushSrc.IndexOf("골드 단축");
            int glSemi = gl >= 0 ? rushSrc.IndexOf(";", gl) : -1;
            string gl0 = gl >= 0 && glSemi > gl ? rushSrc.Substring(gl, glSemi - gl) : "";
            Check(gl0.Contains("EstateStatusHud.ShortCopper")
                  && gl0.Contains("GoldCostToFloor")
                  && gl0.IndexOf("FormatCurrency") < 0,
                "본성 골드 단축은 ShortCopper만");

            // 광산·창고 도크 업비: DrawCoreBuildDock UpgradeCost → ShortCopper
            int dockFn = estate.IndexOf("void DrawCoreBuildDock(Rect");
            Check(dockFn >= 0, "DrawCoreBuildDock이 있다");
            int dockEnd = estate.IndexOf("void DrawDefenseRush", dockFn);
            string dockSrc = dockFn >= 0 && dockEnd > dockFn
                ? estate.Substring(dockFn, dockEnd - dockFn) : "";
            // upDesc 줄만 (GoldCostToFloor 줄과 구분)
            int dockUpLine = dockSrc.LastIndexOf("string upDesc");
            int dockUpSemi = dockUpLine >= 0 ? dockSrc.IndexOf(";", dockUpLine) : -1;
            string dockUp0 = dockUpLine >= 0 && dockUpSemi > dockUpLine
                ? dockSrc.Substring(dockUpLine, dockUpSemi - dockUpLine) : "";
            Check(dockUp0.Contains("EstateStatusHud.ShortCopper")
                  && dockUp0.Contains("UpgradeCost")
                  && dockUp0.IndexOf("FormatCurrency") < 0,
                "광산·창고 업비는 ShortCopper만");

            // 방어 업비: DrawDefense UpgradeCost → ShortCopper
            int defFn = estate.IndexOf("void DrawDefense(Rect");
            Check(defFn >= 0, "DrawDefense가 있다");
            int defEnd = estate.IndexOf("void Keep(Rect", defFn);
            string defSrc = defFn >= 0 && defEnd > defFn
                ? estate.Substring(defFn, defEnd - defFn) : "";
            int dUp = defSrc.IndexOf("string desc =");
            int dSemi = dUp >= 0 ? defSrc.IndexOf(";", dUp) : -1;
            string d0 = dUp >= 0 && dSemi > dUp ? defSrc.Substring(dUp, dSemi - dUp) : "";
            Check(d0.Contains("EstateStatusHud.ShortCopper")
                  && d0.Contains("EstateDefense.UpgradeCost")
                  && d0.IndexOf("FormatCurrency") < 0,
                "방어 업비는 ShortCopper만");

            // 방어 골드 단축: DrawDefenseRush GoldCostToFloor → ShortCopper
            int drFn = estate.IndexOf("void DrawDefenseRush(Rect");
            Check(drFn >= 0, "DrawDefenseRush가 있다");
            int drEnd = estate.IndexOf("static string FormatWait", drFn);
            string drSrc = drFn >= 0 && drEnd > drFn
                ? estate.Substring(drFn, drEnd - drFn) : "";
            int drGl = drSrc.IndexOf("골드 단축");
            int drGlSemi = drGl >= 0 ? drSrc.IndexOf(";", drGl) : -1;
            string drGl0 = drGl >= 0 && drGlSemi > drGl ? drSrc.Substring(drGl, drGlSemi - drGl) : "";
            Check(drGl0.Contains("EstateStatusHud.ShortCopper")
                  && drGl0.Contains("EstateDefense.GoldCostToFloor")
                  && drGl0.IndexOf("FormatCurrency") < 0,
                "방어 골드 단축은 ShortCopper만");

            // 광산·창고 Busy 골드 단축: DrawCoreBuildDock GoldCostToFloor → ShortCopper
            int dockFn2 = estate.IndexOf("void DrawCoreBuildDock(Rect");
            Check(dockFn2 >= 0, "DrawCoreBuildDock(Busy)이 있다");
            int dockEnd2 = estate.IndexOf("void DrawDefenseRush", dockFn2);
            string dockSrc2 = dockFn2 >= 0 && dockEnd2 > dockFn2
                ? estate.Substring(dockFn2, dockEnd2 - dockFn2) : "";
            int rem = dockSrc2.IndexOf("RemainingText");
            int remSemi = rem >= 0 ? dockSrc2.IndexOf(";", rem) : -1;
            string rem0 = rem >= 0 && remSemi > rem ? dockSrc2.Substring(rem, remSemi - rem) : "";
            Check(rem0.Contains("EstateStatusHud.ShortCopper")
                  && rem0.Contains("GoldCostToFloor")
                  && rem0.IndexOf("FormatCurrency") < 0,
                "광산·창고 Busy 골드 단축은 ShortCopper만");

            // 허브 업비 잔여: DrawHubUpgradeRow UpgradeCost → ShortCopper
            int hubFn = estate.IndexOf("void DrawHubUpgradeRow(Rect");
            Check(hubFn >= 0, "DrawHubUpgradeRow가 있다");
            int hubEnd = estate.IndexOf("void DrawCoreBuildDock", hubFn);
            string hubSrc = hubFn >= 0 && hubEnd > hubFn
                ? estate.Substring(hubFn, hubEnd - hubFn) : "";
            int hDesc = hubSrc.IndexOf("string desc =");
            int hSemi = hDesc >= 0 ? hubSrc.IndexOf(";", hDesc) : -1;
            string h0 = hDesc >= 0 && hSemi > hDesc ? hubSrc.Substring(hDesc, hSemi - hDesc) : "";
            Check(h0.Contains("EstateStatusHud.ShortCopper")
                  && h0.Contains("UpgradeCost")
                  && h0.IndexOf("FormatCurrency") < 0,
                "허브 업비(DrawHubUpgradeRow)는 ShortCopper만");

            // 경매 로컬 장 지갑: AuctionHouseOld
            int auc = estate.IndexOf("void AuctionHouseOld");
            Check(auc >= 0, "AuctionHouseOld가 있다");
            int aucEnd = estate.IndexOf("void DrawAuctionLots", auc);
            string aucSrc = auc >= 0 && aucEnd > auc
                ? estate.Substring(auc, aucEnd - auc) : "";
            int loc = aucSrc.IndexOf("로컬 장");
            int locSemi = loc >= 0 ? aucSrc.IndexOf(";", loc) : -1;
            string loc0 = loc >= 0 && locSemi > loc ? aucSrc.Substring(loc, locSemi - loc) : "";
            Check(loc0.Contains("EstateStatusHud.ShortCopper")
                  && loc0.Contains("Wallet.Copper")
                  && loc0.IndexOf("FormatCurrency") < 0,
                "경매 로컬 장 지갑은 ShortCopper만");

            // 경매 호가 lot.Price
            Check(estate.Contains("void DrawAuctionLots"), "DrawAuctionLots가 있다");
            Check(estate.Contains("EstateStatusHud.ShortCopper(lot.Price)")
                  && estate.IndexOf("FormatCurrency(lot.Price)") < 0,
                "경매 호가 lot.Price는 ShortCopper만");

            // 경매 등록 수수료·가격
            Check(estate.Contains("EstateStatusHud.ShortCopper(AuctionState.ListFee(price))")
                  && estate.Contains("EstateStatusHud.ShortCopper(price)")
                  && estate.IndexOf("FormatCurrency(AuctionState.ListFee") < 0
                  && estate.IndexOf("FormatCurrency(price)") < 0,
                "경매 등록 수수료·가격은 ShortCopper만");

            // 월드티어 /h
            int wt = estate.IndexOf("void WorldTier(Rect");
            Check(wt >= 0, "WorldTier가 있다");
            int wtEnd = estate.IndexOf("허브 경매 버튼 잠금", wt);
            string wtSrc = wt >= 0 && wtEnd > wt ? estate.Substring(wt, wtEnd - wt) : "";
            Check(wtSrc.Contains("EstateStatusHud.ShortCopper")
                  && wtSrc.Contains("TierRevenueMultiplier")
                  && wtSrc.IndexOf("FormatCurrency") < 0,
                "월드티어 /h는 ShortCopper만");
            // 순자산 줄(§18-5)은 파산·광산 압류와 동형으로 ShowOnHub 게이트 뒤 statusRow에서만
            // 그린다 — 도크 5칸(DockH 하단)은 상시 슬림이다. NetWorthSelfCheck가 배선을 요구하므로
            // 「NetWorth.Line 문자열 부재」는 이제 틀린 단언이다. 게이트 존재로 상시 슬림을 지킨다.
            Check(estate.Contains("if (NetWorth.ShowOnHub)"),
                "긴 순자산 줄은 ShowOnHub 게이트 뒤에서만 (도크 상시 슬림)");
            Check(!estate.Contains("UiPages.Grid(new Rect(r.x, r.y + 92f"),
                "옛 2×2 전폭 Grid를 안 쓴다");

            string pages = File.ReadAllText(Path.Combine(runtime, "UiPages.cs"));
            Check(pages.Contains("SlimTitleH"), "슬림 제목 상수가 있다");
            Check(pages.Contains("IsSlimCard"), "IsSlimCard가 있다");
            string screen = File.ReadAllText(Path.Combine(runtime, "GameScreen.cs"));
            Check(screen.Contains("CardChrome"), "DrawCard가 CardChrome을 읽는다");
            Check(screen.Contains("_h2Slim"), "슬림 부제 글씨체를 읽는다");

            Environment.SetEnvironmentVariable(EstateStatusHud.EnvShow, show);
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[EstateStatusHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateStatusHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[EstateStatusHudSelfCheck] FAIL {_fail}건");
        }
    }
}
