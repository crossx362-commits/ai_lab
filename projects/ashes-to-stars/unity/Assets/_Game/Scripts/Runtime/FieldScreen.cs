using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>필드 — 자동사냥. 코어 루프의 시작점(§2·§6).</summary>
    public class FieldScreen : GameScreen
    {
        protected override string Title => "필드";
        protected override string Subtitle =>
            $"자동사냥으로 재화를 번다(§2·§6) — 보유 {GameState.WalletText} · {GameState.BagText()}";

        bool _showLastLifeWarning = false;
        bool _showInsufficientGold = false;

        protected override void Body(Rect r)
        {
            if (_showInsufficientGold)
            {
                // 골드 부족 경고 화면 (§18-2 진입 비용)
                Info(r, 0, "⚠️ 골드가 부족합니다");
                Info(r, 1, "던전 입장에는 골드가 필요합니다(§18-2)\n필드 사냥으로 먼저 재화를 모으세요(§2)");

                if (Row(r, 2, "확인", "돌아간다"))
                    _showInsufficientGold = false;
                return;
            }

            if (_showLastLifeWarning)
            {
                // 마지막 목숨 경고 화면
                Info(r, 0, "⚠️ 주의! 마지막 목숨 캐릭터가 파티에 있습니다");
                Info(r, 1, "사망 시 캐릭터가 영구 삭제되며\n장착 장비도 함께 사라집니다(§4)");

                if (Row(r, 2, "계속 진행", "입장한다"))
                {
                    _showLastLifeWarning = false;
                    GameFlow.GoBattle(GameFlow.Field);
                }
                if (Row(r, 3, "취소", "파티를 다시 편성한다"))
                    _showLastLifeWarning = false;
                return;
            }

            if (Row(r, 0, "사냥 시작", "잡몹은 자동, 보스는 수동 지휘(§5)"))
            {
                // 필드 사냥은 무료 (§18-2 절대 원칙)
                if (HasLastLifeCharacter())
                    _showLastLifeWarning = true;
                else
                    GameFlow.GoBattle(GameFlow.Field);
            }
            if (Row(r, 1, "던전 입장", "랜덤 생성 + 종점 보스 1체(§7)"))
            {
                // 던전 입장에는 골드 비용 필요 (§18-2)
                // TODO: 실제 지갑 시스템 필요. 지금은 T1 기준 0.02 G/h를 차감한다고 표시만 함
                // 실제로 차감한다. 모자라면 들어가지 못한다(§18-2).
                // 부분 차감은 하지 않는다 — "돈은 냈는데 못 들어갔다"가 최악이다.
                long dungeonCost = Economy.GetActionCost("DungeonEntry", GameState.Tier);
                if (!GameState.Pay(dungeonCost)) _showInsufficientGold = true;
                else if (HasLastLifeCharacter()) _showLastLifeWarning = true;
                else GameFlow.GoBattle(GameFlow.Field);
            }
            if (Row(r, 2, "자동화 일정", "무엇을 언제 시킬지 예약(§6)")) { }
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
