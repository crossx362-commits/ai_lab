using UnityEngine;
using System;
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
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.Character);
        protected override string BackgroundArt => "bg_character";
        // 성장(레벨·경험치)은 이제 실제로 된다 — 전투 보상이 출전 파티에 레벨 비례로 쌓인다(§3·§18-6).
        protected override string Subtitle => "레벨·목숨 관리와 Lv20 1차·Lv50 2차 비살상 전직 시험(§3·§4)";
        protected override bool ShowRarityPreview => UiAtlas.QaShowRarity;

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
            GameFlow.RestorePlayRosterIfRequested();
            GameFlow.SeedV4WipeQaIfRequested();
            SeedRarityQaIfRequested();
            if (_selectedCharacter >= 0)
            {
                // 캐릭터 상세 화면
                var characters = LifeSystem.GetCharacters();
                if (_selectedCharacter < characters.Count)
                {
                    var ch = characters[_selectedCharacter];

                    if (_choosingAdvancement)
                    {
                        var secondTrial = LifeSystem.ActiveSecondTrial;
                        if (secondTrial != null && secondTrial.Character == ch)
                        {
                            Info(r, 0, $"{ch.Name} ({ch.Job}) · 2차 각성 시험 패턴 {secondTrial.Pattern + 1}");
                            Info(r, 1, $"역할 목표: {secondTrial.Objective} {secondTrial.Progress}/{secondTrial.Required}");
                            if (!secondTrial.ObjectiveMet)
                            {
                                for (int i = 0; i < secondTrial.Actions.Count; i++)
                                {
                                    var action = secondTrial.Actions[i];
                                    if (Row(r, i + 2, TrialActionText(action), "각성 시험 상황에 맞는 행동을 선택한다"))
                                    {
                                        if (!LifeSystem.ReportSecondTrialProgress(action))
                                        {
                                            LifeSystem.CancelSecondAdvancementTrial();
                                            _choosingAdvancement = false;
                                        }
                                    }
                                }
                            }
                            else if (Row(r, 3, "각성 성공 확인", $"전직 재료 {LifeSystem.SecondAdvancementMaterialCost}개 소비 후 같은 직업 각성"))
                            {
                                LifeSystem.ConfirmSecondAdvancementTrial();
                                _choosingAdvancement = false;
                            }
                            if (Row(r, 5, "← 시험 중단", "재료를 소비하지 않고 상세로 돌아간다"))
                            {
                                LifeSystem.CancelSecondAdvancementTrial();
                                _choosingAdvancement = false;
                            }
                            return;
                        }

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

                    Info(r, 0, ch.IsRescue
                        ? $"{ch.Name} ({ch.Job}) · {ExpText(ch)} · 긴급 재건"
                        : $"{ch.Name} ({ch.Job}) · {ExpText(ch)}");
                    if (!ch.IsDeleted && ch.Level < LifeSystem.MaxLevel)
                    {
                        float need = LifeSystem.ExpToNext(ch.Level);
                        float ratio = need <= 0f ? 0f : Mathf.Clamp01(ch.Exp / (float)need);
                        UiAtlas.DrawMeter(new Rect(r.xMax - 230, r.y + 18, 210, 22),
                            "xp_frame", ratio, new Color(0.45f, 0.72f, 1f));
                    }

                    // 목숨 상태 표시 — 유니코드 하트는 □로 나와 아틀라스 조각을 쓴다.
                    if (ch.IsDeleted)
                    {
                        Info(r, 1, "삭제됨 — 환생석으로만 복구 가능(§4)");
                        UiAtlas.DrawHearts(new Rect(r.xMax - 90, r.y + (RowH + RowGap) + 18, 80, 22),
                            ch.DeathCount, true);
                    }
                    else
                    {
                        string status = LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중";
                        Info(r, 1, $"목숨 {ch.DeathCount}/3 {status}");
                        UiAtlas.DrawHearts(new Rect(r.xMax - 90, r.y + (RowH + RowGap) + 18, 80, 22),
                            ch.DeathCount, false);

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
                        if (Row(r, 3, "부활초 사용", $"사망 카운트 1 차감 (보유: {LifeSystem.GetRevivePotions()}/3)",
                                ItemAtlas.KeyFor(Economy.LifeItem.RevivalTea)))
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
                        Locked(r, advancementRow++, "1차 전직", "삭제된 캐릭터는 전직할 수 없다(§3·§4)",
                            ItemAtlas.KeyFor(Economy.LifeItem.AdvancementMaterial));
                    }
                    else if (ch.Advancement == AdvancementTier.Basic && ch.Level < 20)
                    {
                        Locked(r, advancementRow++, "1차 전직", $"Lv20 필요 — 현재 Lv.{ch.Level}(§3)",
                            ItemAtlas.KeyFor(Economy.LifeItem.AdvancementMaterial));
                    }
                    else if (ch.Advancement == AdvancementTier.Basic)
                    {
                        int materials = GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial);
                        if (materials < LifeSystem.FirstAdvancementMaterialCost)
                            Locked(r, advancementRow++, "1차 전직 시험",
                                $"전직 재료 {LifeSystem.FirstAdvancementMaterialCost}개 필요 — 현재 {materials}개(던전 파밍)",
                                ItemAtlas.KeyFor(Economy.LifeItem.AdvancementMaterial));
                        else if (Row(r, advancementRow++, "1차 전직 시험", "역할별 직업 선택 후 비살상 훈련",
                                     ItemAtlas.KeyFor(Economy.LifeItem.AdvancementMaterial)))
                            _choosingAdvancement = true;
                    }
                    else if (ch.Advancement == AdvancementTier.First && ch.Level < 50)
                    {
                        Locked(r, advancementRow++, "2차 각성", $"Lv50 필요 — 현재 Lv.{ch.Level}(§3)",
                            ItemAtlas.KeyFor(Economy.LifeItem.AdvancementMaterial));
                    }
                    else if (ch.Advancement == AdvancementTier.First)
                    {
                        int materials = GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial);
                        if (materials < LifeSystem.SecondAdvancementMaterialCost)
                            Locked(r, advancementRow++, "2차 각성 시험",
                                $"전직 재료 {LifeSystem.SecondAdvancementMaterialCost}개 필요 — 현재 {materials}개(던전 파밍)",
                                ItemAtlas.KeyFor(Economy.LifeItem.AdvancementMaterial));
                        else if (Row(r, advancementRow++, "2차 각성 시험", $"{ch.Job} 심화 · 초필살기 해금 준비",
                                     ItemAtlas.KeyFor(Economy.LifeItem.AdvancementMaterial)))
                        {
                            if (LifeSystem.TryBeginSecondAdvancementTrial(ch)) _choosingAdvancement = true;
                        }
                    }
                    else
                    {
                        Info(r, advancementRow++, "전직 단계: 2차 · 4스킬+초필 1개 해금");
                    }

                    if (Row(r, advancementRow, "← 목록으로", "캐릭터 목록으로 돌아간다"))
                    {
                        _selectedCharacter = -1;
                        _choosingAdvancement = false;
                    }
                    DrawWornStrip(r, ch);
                }
                return;
            }

            // 캐릭터 목록 — 초상+역할+목숨 아이콘. 이모지는 기본 폰트에서 □다.
            var allCharacters = LifeSystem.GetCharacters();
            for (int i = 0; i < allCharacters.Count; i++)
            {
                var ch = allCharacters[i];
                string name = ch.IsRescue
                    ? $"{ch.Name} ({ch.Job}) · 재건"
                    : $"{ch.Name} ({ch.Job})";
                string sub = ch.IsDeleted
                    ? "삭제됨"
                    : $"{ExpText(ch)} · {(LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중")}";
                if (Row(r, i, name, "", leftPad: 56f))
                {
                    _selectedCharacter = i;
                    _choosingAdvancement = false;
                }
                DrawRosterDecor(r, i, ch, sub);
            }

            Info(r, allCharacters.Count + 1, "목숨 카운트가 여기서 보인다(§3·§4)");
            // 빈 버튼이었다 — 눌러도 아무 일이 없으면 그건 없는 기능이다.
            if (Row(r, allCharacters.Count + 2, "파티 편성",
                    $"최대 5인(§9) · 지금 {PartyState.Slots.Count}명 편성됨 — 구성이 생존을 가른다(§21-1i)"))
                GameFlow.Go(GameFlow.Party);

            Locked(r, allCharacters.Count + 4, "합성",
                   "준비 중 — 1차 전직 이상 캐릭터를 소멸시켜 패시브를 흡수한다(§3)");
        }

        void DrawRosterDecor(Rect r, int index, CharacterRecord ch, string sub)
        {
            var br = RowButtonRect(r, index);
            if (br.yMax > r.yMax) return;

            var face = new Rect(br.x + 6, br.y + 5, 48, 48);
            UiAtlas.Draw(new Rect(face.x - 2, face.y - 2, 52, 52), "portrait_frame");
            var tint = ch.IsDeleted ? new Color(1f, 1f, 1f, 0.4f) : (Color?)null;
            PortraitAtlas.Draw(face, PortraitAtlas.KeyForJob(ch.Job), tint);
            UiAtlas.Draw(new Rect(face.xMax - 8, face.yMax - 8, 20, 20), UiAtlas.RoleKey(ch.Job));

            var desc = RowDescRect(r, index);
            float heartsW = UiAtlas.DrawHearts(new Rect(desc.x, desc.y + 4, 80, 22), ch.DeathCount, ch.IsDeleted);
            Hint(new Rect(desc.x + heartsW + 6, desc.y + 6, desc.width - heartsW - 6, 22), sub);
            if (!ch.IsDeleted && ch.Level < LifeSystem.MaxLevel)
            {
                float need = LifeSystem.ExpToNext(ch.Level);
                float ratio = need <= 0f ? 0f : Mathf.Clamp01(ch.Exp / (float)need);
                UiAtlas.DrawMeter(new Rect(desc.x, desc.y + 32, Mathf.Min(220f, desc.width), 20),
                    "xp_frame", ratio, new Color(0.45f, 0.72f, 1f));
            }
        }

        void SeedRarityQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_UI_RARITY") != "1") return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            Equipment.SeedCraftedLoadoutForQa(roster[0]);
            if (_selectedCharacter < 0) _selectedCharacter = 0;
        }

        /// <summary>장착 6칸. 글자만 있으면 등급 프레임 소비처가 0곳이다.</summary>
        void DrawWornStrip(Rect r, CharacterRecord ch)
        {
            const float size = 56f, gap = 8f;
            float width = Equipment.SlotCount * (size + gap) - gap;
            // 이름·목숨 아래 오른쪽. 바닥에 두면 전직 줄을 덮는다(실측).
            var strip = new Rect(r.xMax - width, r.y + 2f * (RowH + RowGap), width, size);
            if (strip.x < r.x + RowBtnW + 16f) strip.x = r.x + RowBtnW + 16f;
            for (int i = 0; i < Equipment.SlotCount; i++)
            {
                var cell = new Rect(strip.x + i * (size + gap), strip.y, size, size);
                ItemAtlas.DrawGear(cell, Equipment.Worn(ch, (EquipSlot)i));
            }
        }
    }
}
