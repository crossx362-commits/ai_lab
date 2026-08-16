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
        protected override string BackgroundArt => "bg_character";
        // 성장(레벨·경험치)은 이제 실제로 된다 — 전투 보상이 출전 파티에 레벨 비례로 쌓인다(§3·§18-6).
        // 1차 직업 선택은 연결됐다. 재료·시험과 합성은 후속 슬라이스라 정직하게 구분한다.
        protected override string Subtitle => "레벨·목숨·부활초 관리(§3·§4). Lv20 1차 직업 선택 가능";

        /// <summary>레벨·경험치 진척 표기(§18-6). 만렙은 MAX로.</summary>
        static string ExpText(CharacterRecord ch) =>
            ch.Level >= LifeSystem.MaxLevel
                ? $"Lv.{ch.Level} · EXP MAX"
                : $"Lv.{ch.Level} · EXP {ch.Exp}/{LifeSystem.ExpToNext(ch.Level)}";

        int _selectedCharacter = -1;
        bool _choosingAdvancement;

        protected override void Body(Rect r)
        {
            if (_selectedCharacter >= 0)
            {
                // 캐릭터 상세 화면
                var characters = LifeSystem.GetCharacters();
                if (_selectedCharacter < characters.Count)
                {
                    var ch = characters[_selectedCharacter];

                    if (_choosingAdvancement)
                    {
                        var options = LifeSystem.FirstAdvancementOptions(ch);
                        Info(r, 0, $"{ch.Name} ({ch.Job}) · 1차 전직 선택");
                        for (int i = 0; i < options.Count; i++)
                        {
                            string targetJob = options[i];
                            if (Row(r, i + 1, targetJob, $"{ch.Job} → {targetJob} · 1차 전직"))
                            {
                                LifeSystem.TryFirstAdvance(ch, targetJob);
                                _choosingAdvancement = false;
                            }
                        }
                        if (Row(r, 5, "← 선택 취소", "캐릭터 상세로 돌아간다"))
                            _choosingAdvancement = false;
                        return;
                    }

                    Info(r, 0, $"{ch.Name} ({ch.Job}) · {ExpText(ch)}");

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

                    int advancementRow = 4;
                    if (ch.IsDeleted)
                    {
                        Locked(r, advancementRow++, "1차 전직", "삭제된 캐릭터는 전직할 수 없다(§3·§4)");
                    }
                    else if (ch.Advancement == AdvancementTier.Basic && ch.Level < 20)
                    {
                        Locked(r, advancementRow++, "1차 전직", $"Lv20 필요 — 현재 Lv.{ch.Level}(§3)");
                    }
                    else if (ch.Advancement == AdvancementTier.Basic)
                    {
                        if (Row(r, advancementRow++, "1차 전직 선택", "역할별 직업 선택 — 재료·시험은 다음 슬라이스"))
                            _choosingAdvancement = true;
                    }
                    else
                    {
                        Info(r, advancementRow++, $"전직 단계: {(ch.Advancement == AdvancementTier.First ? "1차" : "2차")}");
                    }

                    if (Row(r, advancementRow, "← 목록으로", "캐릭터 목록으로 돌아간다"))
                    {
                        _selectedCharacter = -1;
                        _choosingAdvancement = false;
                    }
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
                string sub = ch.IsDeleted
                    ? "삭제됨"
                    : $"{ExpText(ch)} · {(LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중")}";
                if (Row(r, i, name, sub))
                {
                    _selectedCharacter = i;
                    _choosingAdvancement = false;
                }
            }

            Info(r, allCharacters.Count + 1, "목숨 카운트가 여기서 보인다(§3·§4)");
            // 빈 버튼이었다 — 눌러도 아무 일이 없으면 그건 없는 기능이다.
            if (Row(r, allCharacters.Count + 2, "파티 편성",
                    $"최대 5인(§9) · 지금 {PartyState.Slots.Count}명 편성됨 — 구성이 생존을 가른다(§21-1i)"))
                GameFlow.Go(GameFlow.Party);

            Info(r, allCharacters.Count + 3, "전직: 캐릭터를 선택하면 Lv20부터 1차 직업 선택(§3)");
            Locked(r, allCharacters.Count + 4, "합성",
                   "준비 중 — 1차 전직 이상 캐릭터를 소멸시켜 패시브를 흡수한다(§3)");
        }
    }
}
