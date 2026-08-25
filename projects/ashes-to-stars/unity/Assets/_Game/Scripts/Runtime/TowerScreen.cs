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

    /// <summary>탑 — 최대 100층. 10층 돌파마다 필드·던전 난이도가 오른다(§8·§10-6).</summary>
    public class TowerScreen : GameScreen
    {
        protected override string Title => $"탑 · {GameState.TowerFloor}층";
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.Tower);
        protected override string BackgroundArt => "bg_tower";
        protected override bool ShowBossHpPreview => UiAtlas.QaShowBossHp;
        protected override bool FitHeaderSubtitle => TowerSubtitleFit.Enabled;
        protected override string Subtitle
        {
            get
            {
                if (TowerWarnHud.ShowQa) return TowerWarnHud.Line();
                if (BossSkills.ShowQa) return BossSkills.Line();
                if (TowerDockCap.ShowQa) return TowerDockCap.Line();
                if (TowerHud.ShowQa) return TowerHud.Line();
                if (TowerHubCap.ShowQa) return TowerHubCap.Line();
                string rest = TowerEnding.HasTitle
                    ? $"{TowerEnding.TitleName} · 100층 재도전 · 해금 T{GameState.UnlockedTier + 1}"
                    : SoloRaidClear.HasAny
                        ? $"{SoloRaidClear.LastTitle} · 홀로 깬 레이드 {SoloRaidClear.Count} · 해금 T{GameState.UnlockedTier + 1}"
                        : $"최대 100층. 해금 T{GameState.UnlockedTier + 1} · 세계 T{GameState.Tier + 1} · 보유 {GameState.WalletText}";
                return TowerHubCap.Compose(
                    DeathTraining.Line(),
                    RaidScale.Line(),
                    RaidBossPool.Line(),
                    RaidReroll.Line(),
                    RaidCost.Line(),
                    BossHp.Line(),
                    BossHp.CountLine(),
                    BossCount.Line(),
                    BossSkills.Line(),
                    rest);
            }
        }

        bool _showLastLifeWarning = false;
        bool _showDeathConsent = false;
        // 필드 화면과 같은 규칙 — 값을 세우는 코드만 있고 이 필드·표시 화면이 없어서
        // 컴파일이 깨져 있었다. 선언만 넣으면 골드 부족이 조용히 무시되므로 화면까지 맞춘다.
        bool _showInsufficientGold = false;

        // 경고에서 "계속 진행"을 눌렀을 때 비로소 낼 비용과, 그때 들어갈 판.
        // 예전에는 ①버튼을 누른 즉시 차감해 취소하면 골드만 사라졌고
        //          ②경고를 거친 레이드가 인자 없는 GoBattle로 떨어져 **잡몹 웨이브 5층**이 됐다.
        //          (레이드를 고르고 경고에 "계속"을 눌렀는데 보스가 안 나오는 경로였다)
        long _pendingCost = 0;
        GameFlow.BattleKind _pendingKind = GameFlow.BattleKind.잡몹웨이브;
        int _pendingFloor = 0;

        protected override void Body(Rect r)
        {
            TowerWarnHud.SeedQaIfRequested();
            TowerHud.SeedQaIfRequested();
            TowerDockCap.SeedQaIfRequested();
            TowerEnding.SeedQaIfRequested();
            SoloRaidClear.SeedQaIfRequested();
            DeathTraining.SeedQaIfRequested();
            RaidScale.SeedQaIfRequested();
            RaidBossPool.SeedQaIfRequested();
            RaidReroll.SeedQaIfRequested();
            RaidCost.SeedQaIfRequested();
            BossHp.SeedQaIfRequested();
            BossCount.SeedQaIfRequested();
            BossSkills.SeedQaIfRequested();
            LastLifeWarn.SeedQaIfRequested();
            SeedTowerPoorQaIfRequested();
            if (LastLifeWarn.QaPrompt)
            {
                _showLastLifeWarning = true;
                LastLifeWarn.AckQaPrompt();
            }
            if (DeathTraining.QaPromptConsent)
            {
                _showDeathConsent = true;
                DeathTraining.AckQaPrompt();
            }
            if (TowerWarnHud.QaConsentPrompt)
            {
                _showDeathConsent = true;
                TowerWarnHud.AckConsent();
            }
            if (TowerWarnHud.QaGoldPrompt)
            {
                _showInsufficientGold = true;
                if (_pendingCost <= 0)
                    _pendingCost = 10_000L;
                TowerWarnHud.AckGold();
            }
            if (TowerWarnHud.QaLifePrompt)
            {
                _showLastLifeWarning = true;
                TowerWarnHud.AckLife();
            }
            if (_showDeathConsent)
            {
                r = TowerWarnHud.Content(r);
                Info(r, 0, "[주의] " + DeathTraining.ConsentTitle());
                Info(r, 1, DeathTraining.ConsentBody());
                if (DrawChoice(r, "동의하고 입장", "이제부터 목숨이 깎인다(§4)", "tower",
                               "아직 훈련", "5층 전에 돌아간다", "territory", out bool decline))
                {
                    DeathTraining.Consent();
                    _showDeathConsent = false;
                    if (_pendingFloor <= 0)
                        return;
                    if (_pendingCost > 0 && !GameState.Pay(_pendingCost))
                    {
                        _showInsufficientGold = true;
                        return;
                    }
                    int f = _pendingFloor;
                    var k = _pendingKind;
                    _pendingCost = 0;
                    _pendingFloor = 0;
                    RaidReroll.Record(f);
                    GameFlow.GoBattle(GameFlow.Tower, k, f);
                }
                else if (decline)
                {
                    _showDeathConsent = false;
                    _pendingCost = 0;
                }
                return;
            }
            if (_showInsufficientGold)
            {
                r = TowerWarnHud.Content(r);
                Info(r, 0, "[주의] 골드가 부족합니다");
                Info(r, 1, $"필요 {EstateStatusHud.ShortCopper(_pendingCost)} · 보유 {EstateStatusHud.ShortCopper(GameState.Wallet.Copper)}\n필드 사냥은 무료이니 먼저 재화를 모으세요(§2)");
                long shortfall = _pendingCost - GameState.Wallet.Copper;
                Info(r, 2, $"대출 한도 {EstateStatusHud.ShortCopper(GameState.LoanBorrowable)} · 부채 {EstateStatusHud.ShortCopper(GameState.Debt)} · {NetWorth.Line()} · 이자 0.5%/h(§18-5)");
                if (shortfall > 0 && GameState.LoanBorrowable < shortfall)
                    Info(r, 3, "대출 한도가 부족합니다 — 순자산의 30%까지만 빌릴 수 있습니다(§18-5)");

                string okTitle = shortfall > 0 && GameState.LoanBorrowable >= shortfall
                    ? "대출받고 입장" : "확인";
                string okSub = shortfall > 0 && GameState.LoanBorrowable >= shortfall
                    ? "빚을 내서라도 다음 판에 — 골드는 곧 목숨이다(§12)" : "돌아간다";
                if (DrawChoice(r, okTitle, okSub, "tower",
                               "취소", "입장하지 않는다", "territory", out bool cancel))
                {
                    if (shortfall > 0 && GameState.LoanBorrowable >= shortfall)
                    {
                        if (GameState.Borrow(shortfall) && GameState.Pay(_pendingCost))
                        {
                            _showInsufficientGold = false;
                            var k = _pendingKind; int f = _pendingFloor;
                            _pendingCost = 0;
                            RaidReroll.Record(f);
                            GameFlow.GoBattle(GameFlow.Tower, k, f);
                            return;
                        }
                    }
                    else
                    {
                        _showInsufficientGold = false;
                        _pendingCost = 0;
                    }
                }
                else if (cancel)
                {
                    _showInsufficientGold = false;
                    _pendingCost = 0;
                }
                return;
            }

            if (_showLastLifeWarning)
            {
                r = TowerWarnHud.Content(r);
                Info(r, 0, LastLifeWarn.Title());
                Info(r, 1, LastLifeWarn.Body());
                Info(r, 2, LastLifeWarn.GearLine());
                string gearRest = LastLifeWarn.GearRest();
                if (!string.IsNullOrEmpty(gearRest)) Info(r, 3, gearRest);
                if (DrawChoice(r, "계속 진행", "입장한다", "tower",
                               "취소", "파티를 다시 편성한다", "characters", out bool cancel))
                {
                    _showLastLifeWarning = false;
                    if (_pendingCost > 0 && !GameState.Pay(_pendingCost))
                    {
                        _showInsufficientGold = true;
                        return;
                    }
                    _pendingCost = 0;
                    RaidReroll.Record(_pendingFloor);
                    GameFlow.GoBattle(GameFlow.Tower, _pendingKind, _pendingFloor);
                }
                else if (cancel)
                {
                    _showLastLifeWarning = false;
                    _pendingCost = 0;
                }
                return;
            }

            var cards = TowerHud.Cards(r);
            if (DrawCard(cards[0], "다음 층 도전", "벽 콘텐츠 — 재도전 리듬(§8)", "tower"))
                Enter(Economy.GetActionCost("TowerNormalFloor", GameState.UnlockedTier),
                      GameFlow.BattleKind.잡몹웨이브, GameState.TowerFloor);
            {
                int raidFloor = Mathf.Max(5, (GameState.TowerFloor / 5) * 5);
                if (DrawCard(cards[1], "레이드 (5층 단위)",
                        TowerDockCap.Raid(raidFloor), "damage"))
                    Enter(RaidCost.Copper(raidFloor), GameFlow.BattleKind.보스, raidFloor);
            }
            int lower = RaidScale.LowerFloor;
            if (lower > 0)
            {
                if (DrawCard(cards[2], $"하위 레이드 {lower}층",
                        TowerDockCap.Lower(lower), "damage"))
                    Enter(RaidReroll.Cost(lower), GameFlow.BattleKind.보스, lower);
            }
            else
            {
                DrawCard(cards[2], $"{GameState.TowerFloor}층",
                    $"해금 T{GameState.UnlockedTier + 1} · 세계 T{GameState.Tier + 1}",
                    "tower", locked: true);
            }
            DrawCard(cards[3], EstateStatusHud.ShortCopper(GameState.Wallet.Copper),
                GameState.Debt > 0 ? $"부채 {EstateStatusHud.ShortCopper(GameState.Debt)}" : "부채 없음",
                "building_auction", locked: true);
        }


        void SeedTowerPoorQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_TOWER_POOR") != "1") return;
            long have = GameState.Wallet.Copper;
            if (have > 0) GameState.Pay(have);
            _pendingCost = Math.Max(10_000L, Economy.GetActionCost("Tower10Boss", GameState.Tier));
            _pendingKind = GameFlow.BattleKind.보스;
            _pendingFloor = 10;
            _showInsufficientGold = true;
        }

        /// <summary>
        /// 입장 처리 — 잔액 확인 → (마지막 목숨이면 경고로 보류) → 차감 → 전투.
        /// 두 버튼이 같은 순서를 쓰게 한 곳에 모은다. 복붙하면 한쪽만 고쳐진다.
        /// </summary>
        void Enter(long cost, GameFlow.BattleKind kind, int floor)
        {
            if (GameState.Wallet.Copper < cost)
            {
                // 대출 화면이 이 판의 비용·종류·층을 읽어 "대출받고 입장"을 성립시킨다.
                _pendingCost = cost;
                _pendingKind = kind;
                _pendingFloor = floor;
                _showInsufficientGold = true;
                return;
            }
            if (DeathTraining.NeedsConsent(floor))
            {
                _pendingCost = cost;
                _pendingKind = kind;
                _pendingFloor = floor;
                _showDeathConsent = true;
                return;
            }
            if (HasLastLifeCharacter())
            {
                _pendingCost = cost;        // 아직 내지 않는다
                _pendingKind = kind;
                _pendingFloor = floor;
                _showLastLifeWarning = true;
                return;
            }
            if (!GameState.Pay(cost))
            {
                _pendingCost = cost;
                _pendingKind = kind;
                _pendingFloor = floor;
                _showInsufficientGold = true;
                return;
            }
            RaidReroll.Record(floor);
            GameFlow.GoBattle(GameFlow.Tower, kind, floor);
        }

        bool HasLastLifeCharacter() => LastLifeWarn.HasAny();
    }
}
