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
        protected override string Subtitle =>
            $"최대 100층. 해금 T{GameState.UnlockedTier + 1} · 세계 T{GameState.Tier + 1} · 보유 {GameState.WalletText}";

        bool _showLastLifeWarning = false;
        // 필드 화면과 같은 규칙 — 값을 세우는 코드만 있고 이 필드·표시 화면이 없어서
        // 컴파일이 깨져 있었다. 선언만 넣으면 골드 부족이 조용히 무시되므로 화면까지 맞춘다.
        bool _showInsufficientGold = false;

        // 경고에서 "계속 진행"을 눌렀을 때 비로소 낼 비용과, 그때 들어갈 판.
        // 예전에는 ①버튼을 누른 즉시 차감해 취소하면 골드만 사라졌고
        //          ②경고를 거친 레이드가 인자 없는 GoBattle로 떨어져 **잡몹 웨이브 5층**이 됐다.
        //          (레이드를 고르고 경고에 "계속"을 눌렀는데 보스가 안 나오는 경로였다)
        long _pendingCost = 0;
        GameFlow.BattleKind _pendingKind = GameFlow.BattleKind.잡몹웨이브;
        int _pendingFloor = 1;

        protected override void Body(Rect r)
        {
            if (_showInsufficientGold)
            {
                // 골드 부족 경고 화면 (§18-2 진입 비용)
                Info(r, 0, "[주의] 골드가 부족합니다");
                Info(r, 1, $"필요 {Economy.FormatCurrency(_pendingCost)} · 보유 {GameState.WalletText}\n필드 사냥은 무료이니 먼저 재화를 모으세요(§2)");

                // §12 "골드가 없을 때 대출" — 빚을 내서라도 다음 판에. 골드는 곧 목숨이다.
                // 무자산이면 한도 0이라 이 버튼이 안 뜬다(§18-5 무자산 대출 방지) → 필드 사냥으로 복구.
                long shortfall = _pendingCost - GameState.Wallet.Copper;
                Info(r, 2, $"대출 한도 {Economy.FormatCurrency(GameState.LoanBorrowable)} · 현재 부채 {Economy.FormatCurrency(GameState.Debt)} · 이자 시간당 0.5%(§18-5)");
                int row = 3;
                if (shortfall > 0 && GameState.LoanBorrowable >= shortfall)
                {
                    if (Row(r, row++, "대출받고 입장", "빚을 내서라도 다음 판에 — 골드는 곧 목숨이다(§12)"))
                    {
                        if (GameState.Borrow(shortfall) && GameState.Pay(_pendingCost))
                        {
                            _showInsufficientGold = false;
                            var k = _pendingKind; int f = _pendingFloor;
                            _pendingCost = 0;
                            GameFlow.GoBattle(GameFlow.Tower, k, f);
                            return;
                        }
                    }
                }
                else if (shortfall > 0)
                {
                    Info(r, row++, "대출 한도가 부족합니다 — 순자산의 30%까지만 빌릴 수 있습니다(§18-5)");
                }

                if (Row(r, row, "확인", "돌아간다"))
                {
                    _showInsufficientGold = false;
                    _pendingCost = 0;
                }
                return;
            }

            if (_showLastLifeWarning)
            {
                // 마지막 목숨 경고 화면
                Info(r, 0, "[주의] 마지막 목숨 캐릭터가 파티에 있습니다");
                Info(r, 1, "사망 시 캐릭터가 영구 삭제되며\n장착 장비도 함께 사라집니다(§4)");

                if (Row(r, 2, "계속 진행", "입장한다"))
                {
                    _showLastLifeWarning = false;
                    // 대출 화면이 _pendingCost/Kind/Floor를 읽으므로 여기서 지우지 않는다.
                    if (_pendingCost > 0 && !GameState.Pay(_pendingCost))
                    {
                        _showInsufficientGold = true;
                        return;
                    }
                    _pendingCost = 0;
                    GameFlow.GoBattle(GameFlow.Tower, _pendingKind, _pendingFloor);
                }
                if (Row(r, 3, "취소", "파티를 다시 편성한다"))
                {
                    _showLastLifeWarning = false;
                    _pendingCost = 0;   // 입장하지 않았으니 아무것도 내지 않는다
                }
                return;
            }

            if (Row(r, 0, "다음 층 도전", "벽 콘텐츠 — 재도전 리듬(§8)"))
                Enter(Economy.GetActionCost("TowerNormalFloor", GameState.UnlockedTier),
                      GameFlow.BattleKind.잡몹웨이브, GameState.TowerFloor);
            // 레이드는 **보스전**이다 — 잡몹 웨이브가 아니라 기믹 3종이 도는 판(§9·§10-5).
            // §5가 "보스는 수동 지휘"라 했으므로 여기서 V3 검증이 이뤄진다.
            if (Row(r, 1, "레이드 (5층 단위)", "5층마다 보스, 10층 단위는 대보스(§9)"))
            {
                // 레이드 층은 현재 진행도에서 가장 가까운 5층 배수(§9 "5층 단위")
                int raidFloor = Mathf.Max(5, (GameState.TowerFloor / 5) * 5);
                Enter(Economy.GetActionCost("Tower5BossRaid", GameState.UnlockedTier),
                      GameFlow.BattleKind.보스, raidFloor);
            }
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
            GameFlow.GoBattle(GameFlow.Tower, kind, floor);
        }

        bool HasLastLifeCharacter()
        {
            var characters = LifeSystem.GetCharacters();
            foreach (var ch in characters)
            {
                if (!ch.IsDeleted && ch.DeathCount == 2)  // 2회 사망 = 마지막 목숨
                    return true;
            }
            return false;
        }
    }
}
