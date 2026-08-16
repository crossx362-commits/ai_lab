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
        protected override string Subtitle => "레벨·목숨 관리와 Lv20 비살상 1차 전직 시험(§3·§4)";

        /// <summary>레벨·경험치 진척 표기(§18-6). 만렙은 MAX로.</summary>
        static string ExpText(CharacterRecord ch) =>
            ch.Level >= LifeSystem.MaxLevel
                ? $"Lv.{ch.Level} · EXP MAX"
                : $"Lv.{ch.Level} · EXP {ch.Exp}/{LifeSystem.ExpToNext(ch.Level)}";

        static string TrialActionText(FirstTrialAction action) => action switch
        {
            FirstTrialAction.Guard => "인형 앞을 지킨다",
            FirstTrialAction.Taunt => "도발로 후열 공격을 끊는다",
            FirstTrialAction.Brace => "방패벽으로 충격을 버틴다",
            FirstTrialAction.Mark => "우선 표적을 지정한다",
            FirstTrialAction.Strike => "지정 표적을 집중 공격한다",
            FirstTrialAction.Execute => "마지막 표적을 처치한다",
            FirstTrialAction.Heal => "위급한 아군을 치유한다",
            FirstTrialAction.Cleanse => "해로운 효과를 정화한다",
            FirstTrialAction.Stabilize => "세 아군의 생존을 안정시킨다",
            FirstTrialAction.Inspire => "파티 공격을 강화한다",
            FirstTrialAction.Weaken => "훈련 적의 공격을 약화한다",
            _ => "강화·약화를 유지한다",
        };

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
                        var trial = LifeSystem.ActiveFirstTrial;
                        if (trial != null && trial.Character == ch)
                        {
                            Info(r, 0, $"{ch.Name} → {trial.TargetJob} · 시험 패턴 {trial.Pattern + 1}");
                            Info(r, 1, $"역할 목표: {trial.Objective} {trial.Progress}/{trial.Required}");
                            if (!trial.ObjectiveMet)
                            {
                                for (int i = 0; i < trial.Actions.Count; i++)
                                {
                                    var action = trial.Actions[i];
                                    if (Row(r, i + 2, TrialActionText(action), "훈련 상황에 맞는 행동을 선택한다"))
                                    {
                                        // 정답을 화면이 대신 넣지 않는다. 패턴과 다른 행동은 즉시 시험 실패.
                                        if (!LifeSystem.ReportFirstTrialProgress(action))
                                        {
                                            LifeSystem.CancelFirstAdvancementTrial();
                                            _choosingAdvancement = false;
                                        }
                                    }
                                }
                            }
                            else if (Row(r, 3, "시험 성공 확인", $"전직 재료 {LifeSystem.FirstAdvancementMaterialCost}개 소비 후 전직"))
                            {
                                LifeSystem.ConfirmFirstAdvancementTrial();
                                _choosingAdvancement = false;
                            }
                            if (Row(r, 5, "← 시험 중단", "재료를 소비하지 않고 상세로 돌아간다"))
                            {
                                LifeSystem.CancelFirstAdvancementTrial();
                                _choosingAdvancement = false;
                            }
                            return;
                        }

                        var options = LifeSystem.FirstAdvancementOptions(ch);
                        int materials = GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial);
                        Info(r, 0, $"{ch.Name} ({ch.Job}) · 1차 전직 선택 · 재료 {materials}/{LifeSystem.FirstAdvancementMaterialCost}");
                        for (int i = 0; i < options.Count; i++)
                        {
                            string targetJob = options[i];
                            if (Row(r, i + 1, targetJob, $"{ch.Job} → {targetJob} · 1차 전직"))
                            {
                                LifeSystem.TryBeginFirstAdvancementTrial(ch, targetJob);
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
                        int materials = GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial);
                        if (materials < LifeSystem.FirstAdvancementMaterialCost)
                            Locked(r, advancementRow++, "1차 전직 시험",
                                $"전직 재료 {LifeSystem.FirstAdvancementMaterialCost}개 필요 — 현재 {materials}개(던전 파밍)");
                        else if (Row(r, advancementRow++, "1차 전직 시험", "역할별 직업 선택 후 비살상 훈련"))
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

            Locked(r, allCharacters.Count + 4, "합성",
                   "준비 중 — 1차 전직 이상 캐릭터를 소멸시켜 패시브를 흡수한다(§3)");
        }
    }
}
