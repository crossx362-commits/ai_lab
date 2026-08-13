using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>캐릭터 — 성장·전직·합성, 그리고 목숨 상태(§3·§4).</summary>
    public class CharacterScreen : GameScreen
    {
        protected override string Title => "캐릭터";
        protected override string Subtitle => "성장·전직·합성. 목숨 카운트가 여기서 보인다(§3·§4)";

        int _selectedCharacter = -1;

        protected override void Body(Rect r)
        {
            if (_selectedCharacter >= 0)
            {
                // 캐릭터 상세 화면
                var characters = LifeSystem.GetCharacters();
                if (_selectedCharacter < characters.Count)
                {
                    var ch = characters[_selectedCharacter];

                    Info(r, 0, $"{ch.Name} ({ch.Job}) Lv.{ch.Level}");

                    // 목숨 상태 표시
                    if (ch.IsDeleted)
                    {
                        Info(r, 1, "❌ 삭제됨 — 환생석으로만 복구 가능(§4)");
                    }
                    else
                    {
                        string hearts = new string('❤', 3 - ch.DeathCount);
                        string status = LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중";
                        Info(r, 1, $"목숨: {hearts}({ch.DeathCount}/3) {status}");

                        // 회복 중이면 시간 표시
                        int recoveryTime = LifeSystem.GetRecoveryTimeRemaining(ch);
                        if (recoveryTime > 0)
                        {
                            Info(r, 2, $"회복 시간: {LifeSystem.FormatRecoveryTime(recoveryTime)}");
                        }
                    }

                    // 부활초 사용 버튼
                    if (!ch.IsDeleted && ch.DeathCount > 0 && LifeSystem.GetRevivePotions() > 0)
                    {
                        if (Row(r, 3, "부활초 사용", $"사망 카운트 1 차감 (보유: {LifeSystem.GetRevivePotions()}/3)"))
                        {
                            LifeSystem.UseRevivePotion(ch);
                        }
                    }
                    else if (!ch.IsDeleted && ch.DeathCount > 0 && LifeSystem.GetRevivePotions() == 0)
                    {
                        Info(r, 3, "부활초 없음 — 던전·레이드에서 획득 가능(§4)");
                    }

                    if (Row(r, 4, "← 목록으로", "캐릭터 목록으로 돌아간다"))
                        _selectedCharacter = -1;
                }
                return;
            }

            // 캐릭터 목록
            var allCharacters = LifeSystem.GetCharacters();
            for (int i = 0; i < allCharacters.Count; i++)
            {
                var ch = allCharacters[i];
                string heartsStr = ch.IsDeleted ? "❌" : new string('❤', 3 - ch.DeathCount);
                string name = $"{ch.Name} ({ch.Job}) - {heartsStr}";
                if (Row(r, i, name, ch.IsDeleted ? "삭제됨" : (LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중")))
                    _selectedCharacter = i;
            }

            Info(r, allCharacters.Count + 1, "목숨 카운트가 여기서 보인다(§3·§4)");
            if (Row(r, allCharacters.Count + 2, "파티 편성", "탱1·딜2·힐1·버퍼1 — 1인은 불가(§9, W3에서 검증)")) { }
        }
    }
}
