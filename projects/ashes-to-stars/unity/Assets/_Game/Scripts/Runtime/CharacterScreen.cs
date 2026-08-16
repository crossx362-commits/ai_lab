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
        bool _fusing;
        int _fusionHost = -1;
        int _fusionMaterial = -1;
        int _listPage;
        int _detailPage;
        int _bagFilter = -1;

        protected override void Body(Rect r)
        {
            GameFlow.RestorePlayRosterIfRequested();
            GameFlow.SeedV4WipeQaIfRequested();
            SeedRarityQaIfRequested();
            SeedFusionQaIfRequested();
            SeedSpecialJobQaIfRequested();
            SeedTowerEndingQaIfRequested();
            SeedSoloRaidQaIfRequested();
            if (_fusing)
            {
                DrawFusion(r);
                return;
            }
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

                    _detailPage = DrawTabs(r, new[] { "장비", "속성" }, _detailPage);
                    var detailBody = UiPages.AfterTabs(r);
                    if (_detailPage == 0)
                    {
                        DrawEquipStudio(detailBody, ch);
                        return;
                    }
                    r = detailBody;

                    string detail = $"{ch.Name} ({ch.Job}) · {ExpText(ch)}";
                    if (ch.IsRescue) detail += " · 긴급 재건";
                    if (ch.IsSpecialJob) detail += " · 특수 직업";
                    if (TowerEnding.HasStarLook) detail += $" · {TowerEnding.LookName}";
                    if (SoloRaidClear.HasLook) detail += $" · {SoloRaidClear.LookName}";
                    Info(r, 0, detail);
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
                        Info(r, 1, ch.IsSpecialJob
                            ? "삭제됨 — 특수 직업은 환생석으로 되돌릴 수 없다(§3)"
                            : "삭제됨 — 환생석으로만 복구 가능(§4)");
                        UiAtlas.DrawHearts(new Rect(r.xMax - 90, r.y + (RowH + RowGap) + 18, 80, 22),
                            ch.DeathCount, true, ch.MaxLives);
                    }
                    else
                    {
                        string status = LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중";
                        Info(r, 1, ch.IsSpecialJob
                            ? $"목숨 {ch.DeathCount}/{ch.MaxLives} {status} · 부활초·환생석 불가(§3)"
                            : $"목숨 {ch.DeathCount}/{ch.MaxLives} {status}");
                        UiAtlas.DrawHearts(new Rect(r.xMax - 90, r.y + (RowH + RowGap) + 18, 80, 22),
                            ch.DeathCount, false, ch.MaxLives);

                        // 회복 중이면 시간 표시
                        int recoveryTime = LifeSystem.GetRecoveryTimeRemaining(ch);
                        if (recoveryTime > 0)
                        {
                            Info(r, 2, $"회복 시간: {LifeSystem.FormatRecoveryTime(recoveryTime)}");
                        }
                    }

                    // 부활초 사용 버튼 — 특수 직업은 1회 사망이 곧 소멸이라 버튼을 열지 않는다.
                    if (ch.IsSpecialJob && !ch.IsDeleted)
                    {
                        Locked(r, 3, "부활초 사용", "특수 직업은 부활초를 쓸 수 없다(§3)",
                            ItemAtlas.KeyFor(Economy.LifeItem.RevivalTea));
                    }
                    else if (!ch.IsDeleted && ch.DeathCount > 0 && LifeSystem.GetRevivePotions() > 0)
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
                    if (ch.IsSpecialJob && !ch.IsDeleted)
                    {
                        int tokens = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);
                        Info(r, advancementRow++,
                            $"특수 직업 · 1목숨 · 일반 전직 경로 밖 · 증표 {tokens}(§3)");
                    }
                    else if (!ch.IsDeleted && !ch.IsRescue
                             && GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken) > 0)
                    {
                        int tokens = GameState.Bag.GetCount(Economy.LifeItem.SpecialJobToken);
                        if (Row(r, advancementRow++, "특수 직업 전직",
                                $"증표 1장으로 1목숨이 된다 — 부활초·환생석 불가(보유 {tokens})",
                                ItemAtlas.KeyFor(Economy.LifeItem.SpecialJobToken)))
                            LifeSystem.TryBecomeSpecial(ch);
                    }
                    if (ch.IsSpecialJob)
                    {
                        // 특수 직업은 일반 전직 경로 밖 — 위 줄이 소비처다.
                    }
                    else if (ch.IsDeleted)
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

                    if (!ch.IsDeleted && ch.AbsorbedBoons.Count > 0)
                        Info(r, advancementRow++, $"흡수 {Fusion.AbsorbedSummary(ch)} ({ch.AbsorbedBoons.Count}/{Fusion.SlotCap})");
                    if (!ch.IsDeleted && ch.PendingBoon >= 0)
                        Info(r, advancementRow++, $"보류 {Fusion.LabelOf((BoonId)ch.PendingBoon)} — 합성에서 교체/포기");

                    if (Row(r, advancementRow, "← 목록으로", "캐릭터 목록으로 돌아간다"))
                    {
                        _selectedCharacter = -1;
                        _choosingAdvancement = false;
                        _detailPage = 0;
                    }
                }
                return;
            }

            _listPage = DrawTabs(r, new[] { "명부", "합성" }, _listPage);
            var page = UiPages.AfterTabs(r);
            if (_listPage == 1)
            {
                DrawFusionEntry(page);
                return;
            }
            DrawRosterPage(page);
        }

        void DrawRosterPage(Rect r)
        {
            var allCharacters = LifeSystem.GetCharacters();
            var cells = UiPages.Grid(new Rect(r.x, r.y, r.width, r.height - 88f), 3, 2, 12f);
            for (int i = 0; i < allCharacters.Count && i < cells.Length; i++)
            {
                var ch = allCharacters[i];
                string sub = ch.IsDeleted
                    ? (ch.IsSpecialJob ? "삭제됨 · 특수 직업" : "삭제됨")
                    : ch.IsSpecialJob
                        ? $"{ExpText(ch)} · 특수 직업 1목숨"
                        : $"{ExpText(ch)} · {(LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중")}";
                if (DrawRosterCard(cells[i], ch, sub))
                {
                    _selectedCharacter = i;
                    _choosingAdvancement = false;
                }
            }
            if (DrawCard(new Rect(r.x, r.yMax - 76f, r.width, 70f),
                    "파티 편성",
                    $"최대 5인 · 지금 {PartyState.Slots.Count}명 — 구성이 생존을 가른다",
                    "tank"))
                GameFlow.Go(GameFlow.Party);
        }

        bool DrawRosterCard(Rect card, CharacterRecord ch, string sub)
        {
            var tint = ch.IsDeleted ? new Color(1f, 1f, 1f, 0.45f) : (Color?)null;
            if (!UiAtlas.DrawSliced(card, "panel", 14f, tint ?? new Color(1f, 1f, 1f, 0.94f)))
                UiAtlas.Draw(card, "panel", tint);
            var face = new Rect(card.x + 14f, card.y + 12f, 56f, 56f);
            UiAtlas.DrawRosterFrame(face);
            PortraitAtlas.Draw(face, PortraitAtlas.KeyForJob(ch.Job), tint);
            UiAtlas.Draw(new Rect(face.xMax - 10f, face.yMax - 10f, 22f, 22f), UiAtlas.RoleKey(ch.Job));
            string name = ch.IsRescue ? $"{ch.Name} · 재건" : ch.Name;
            Hint(new Rect(face.xMax + 10f, card.y + 14f, card.width - 90f, 24f), name + " · " + ch.Job);
            UiAtlas.DrawHearts(new Rect(face.xMax + 10f, card.y + 40f, 80f, 20f),
                ch.DeathCount, ch.IsDeleted, ch.MaxLives);
            Hint(new Rect(card.x + 14f, card.yMax - 28f, card.width - 28f, 22f), sub);
            return GUI.Button(card, GUIContent.none, GUIStyle.none);
        }

        void DrawFusionEntry(Rect r)
        {
            var allCharacters = LifeSystem.GetCharacters();
            CharacterRecord pendingHost = null;
            for (int i = 0; i < allCharacters.Count; i++)
                if (allCharacters[i].PendingBoon >= 0) { pendingHost = allCharacters[i]; break; }

            var cards = UiPages.Grid(r, 2, 2, 16f);
            if (pendingHost != null)
            {
                if (DrawCard(cards[0], "합성 결과 확인",
                        $"{pendingHost.Name} · {Fusion.LabelOf((BoonId)pendingHost.PendingBoon)}",
                        "buffer"))
                {
                    _fusing = true;
                    _fusionHost = allCharacters.IndexOf(pendingHost);
                    _fusionMaterial = -1;
                }
            }
            else if (!Fusion.HasMaterial())
            {
                DrawCard(cards[0], "합성",
                    "1차 전직 이상 재료가 없다 — 기본직업은 갈 수 없다",
                    "buffer", locked: true);
            }
            else if (DrawCard(cards[0], "합성 시작",
                         $"1차 이상 캐릭터를 소멸시켜 패시브를 흡수한다. {Economy.FormatCurrency(Fusion.CostCopper())}(§18-7)",
                         "buffer"))
            {
                _fusing = true;
                _fusionHost = -1;
                _fusionMaterial = -1;
            }
            DrawCard(cards[1], "규칙",
                $"슬롯 4 · {Economy.FormatCurrency(Fusion.CostCopper())} · 넘치면 본 뒤 교체/포기. 재료는 영묘에 안 간다",
                "heart", locked: true);
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
            float heartsW = UiAtlas.DrawHearts(new Rect(desc.x, desc.y + 4, 80, 22),
                ch.DeathCount, ch.IsDeleted, ch.MaxLives);
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

        void SeedSpecialJobQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_SPECIAL_JOB") != "1") return;
            LifeSystem.SeedSpecialJobQaIfRequested();
            if (_selectedCharacter < 0) _selectedCharacter = 0;
        }

        void SeedTowerEndingQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_TOWER_END") != "1") return;
            TowerEnding.SeedQaIfRequested();
            if (_selectedCharacter < 0) _selectedCharacter = 0;
        }

        void SeedSoloRaidQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_SOLO_CLEAR") != "1") return;
            SoloRaidClear.SeedQaIfRequested();
            if (_selectedCharacter < 0) _selectedCharacter = 0;
        }

        void SeedFusionQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_FUSION") != "1") return;
            Fusion.SeedQaIfRequested();
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            _fusionHost = 0;
            _fusionMaterial = -1;
            for (int i = 1; i < roster.Count; i++)
            {
                if (!Fusion.CanBeMaterial(roster[i])) continue;
                if (Fusion.DrawPool(roster[0], roster[i]).Count == 0) continue;
                _fusionMaterial = i;
                break;
            }
            if (_fusionMaterial >= 0)
            {
                _fusing = true;
                _selectedCharacter = -1;
            }
            else if (_selectedCharacter < 0) _selectedCharacter = 0;
        }

        void DrawFusion(Rect r)
        {
            var roster = LifeSystem.GetCharacters();
            if (_fusionHost >= 0 && _fusionHost < roster.Count && roster[_fusionHost].PendingBoon >= 0)
            {
                var host = roster[_fusionHost];
                var pending = (BoonId)host.PendingBoon;
                Info(r, 0, $"{host.Name} 슬롯이 찼다 — {Fusion.LabelOf(pending)}을(를) 본 뒤에 고른다(§18-7)");
                for (int i = 0; i < host.AbsorbedBoons.Count && i < Fusion.SlotCap; i++)
                {
                    var have = (BoonId)host.AbsorbedBoons[i];
                    if (Row(r, i + 1, $"교체 {i + 1}. {Fusion.LabelOf(have)}",
                            $"{Fusion.LabelOf(have)}를 버리고 {Fusion.LabelOf(pending)}을(를) 넣는다"))
                    {
                        Fusion.AcceptReplace(host, i);
                        _fusing = false;
                        _selectedCharacter = _fusionHost;
                    }
                }
                if (Row(r, 6, "버린다", "새 패시브는 영구 소실된다. 기존 4칸은 유지"))
                {
                    Fusion.DiscardPending(host);
                    _fusing = false;
                    _selectedCharacter = _fusionHost;
                }
                return;
            }

            if (_fusionHost < 0)
            {
                Info(r, 0, "받을 캐릭터를 고른다 — 흡수 슬롯은 이 캐릭터의 것이다");
                int row = 1;
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster[i];
                    if (!Fusion.CanBeHost(ch)) continue;
                    if (Row(r, row++, $"{ch.Name} ({ch.Job})",
                            Fusion.AbsorbedSummary(ch)))
                        _fusionHost = i;
                }
                if (Row(r, row, "← 취소", "캐릭터 목록으로 돌아간다"))
                    _fusing = false;
                return;
            }

            if (_fusionHost >= roster.Count)
            {
                _fusing = false;
                return;
            }

            var chosen = roster[_fusionHost];
            if (_fusionMaterial < 0)
            {
                Info(r, 0, $"{chosen.Name}에게 바칠 재료 — 1차 이상, 이 캐릭터는 되돌릴 수 없다(§3)");
                int row = 1;
                bool any = false;
                for (int i = 0; i < roster.Count; i++)
                {
                    if (i == _fusionHost) continue;
                    var ch = roster[i];
                    if (!Fusion.CanBeMaterial(ch)) continue;
                    var pool = Fusion.DrawPool(chosen, ch);
                    if (pool.Count == 0)
                    {
                        Locked(r, row++, $"{ch.Name} ({ch.Job})", "이미 가진 패시브만 있어 흡수할 것이 없다");
                        continue;
                    }
                    any = true;
                    if (Row(r, row++, $"{ch.Name} ({ch.Job})",
                            $"가능 {pool.Count}종 · 영묘에도 안 남는다"))
                        _fusionMaterial = i;
                }
                if (!any) Info(r, row++, "지금 바칠 재료가 없다");
                if (Row(r, row, "← 받는 캐릭터 다시", "호스트를 다시 고른다"))
                    _fusionHost = -1;
                return;
            }

            if (_fusionMaterial >= roster.Count)
            {
                _fusionMaterial = -1;
                return;
            }

            var fodder = roster[_fusionMaterial];
            long cost = Fusion.CostCopper();
            Info(r, 0, $"{fodder.Name} ({fodder.Job}) → {chosen.Name}");
            Info(r, 1, $"이 캐릭터는 되돌릴 수 없습니다. 영묘에도 가지 않습니다(§3) · {Economy.FormatCurrency(cost)}");
            if (GameState.Wallet.Copper < cost)
            {
                Locked(r, 2, "소멸시키고 흡수한다",
                       $"골드 {Economy.FormatCurrency(cost)} 필요 — 지금 {GameState.WalletText}(§18-7)");
            }
            else if (Row(r, 2, "소멸시키고 흡수한다",
                         $"결과는 랜덤 1개 · {Economy.FormatCurrency(cost)}. 슬롯이 차면 본 뒤에 교체/포기"))
            {
                uint seed = (uint)(Environment.TickCount ^ fodder.Id.GetHashCode());
                if (Fusion.TryFuse(chosen, fodder, seed, out var picked))
                {
                    _fusionMaterial = -1;
                    roster = LifeSystem.GetCharacters();
                    _fusionHost = roster.IndexOf(chosen);
                    if (chosen.PendingBoon < 0)
                    {
                        _fusing = false;
                        _selectedCharacter = _fusionHost;
                    }
                    Debug.Log($"[합성] {chosen.Name} ← {Fusion.LabelOf(picked)}");
                }
                else _fusionMaterial = -1;
            }
            if (Row(r, 3, "← 재료 다시", "아직 소멸시키지 않았다"))
                _fusionMaterial = -1;
        }

        static int CombatPower(CharacterRecord ch)
        {
            if (ch == null || ch.IsDeleted) return 0;
            float mul = Equipment.HpMulOf(ch) * Fusion.HpMulOf(ch);
            return Mathf.Max(1, (int)(ch.Level * 120f * mul));
        }

        static readonly EquipSlot[] RingSlots =
        {
            EquipSlot.Helm, EquipSlot.Accessory, EquipSlot.Weapon,
            EquipSlot.Boots, EquipSlot.Armor, EquipSlot.Gloves,
        };

        /// <summary>참고작처럼 초상 둘레에 6칸, 오른쪽에 가방 격자.</summary>
        void DrawEquipStudio(Rect r, CharacterRecord ch)
        {
            float leftW = r.width * 0.56f;
            var stage = new Rect(r.x, r.y, leftW - 10f, r.height - 56f);
            if (!UiAtlas.DrawSliced(stage, "panel", 18f, new Color(1f, 1f, 1f, 0.9f)))
                UiAtlas.Draw(stage, "panel");

            Hint(new Rect(stage.x + 18f, stage.y + 10f, 280f, 24f),
                $"{ch.Name} · {ch.Job}");
            var power = new Rect(stage.x + 18f, stage.y + 36f, 280f, 28f);
            UiAtlas.Draw(new Rect(power.x, power.y, 26f, 26f), "sword");
            Hint(new Rect(power.x + 30f, power.y + 2f, 250f, 24f),
                $"전투력  {CombatPower(ch):N0}");
            Hint(new Rect(stage.x + 18f, stage.y + 64f, 280f, 20f), ExpText(ch));
            UiAtlas.DrawHearts(new Rect(stage.xMax - 96f, stage.y + 14f, 80f, 22f),
                ch.DeathCount, ch.IsDeleted, ch.MaxLives);

            var face = new Rect(stage.center.x - 90f, stage.y + 96f, 180f, 216f);
            UiAtlas.DrawRosterFrame(face);
            var tint = ch.IsDeleted ? new Color(1f, 1f, 1f, 0.4f) : (Color?)null;
            PortraitAtlas.Draw(face, PortraitAtlas.KeyForJob(ch.Job), tint);
            UiAtlas.Draw(new Rect(face.center.x - 16f, face.yMax - 18f, 32f, 32f),
                UiAtlas.RoleKey(ch.Job));

            var center = face.center;
            for (int i = 0; i < RingSlots.Length; i++)
            {
                var slot = RingSlots[i];
                float deg = UiPages.EquipRingDegrees[i];
                var slotRect = UiPages.SlotOnRing(center, 210f, 160f, deg, 64f);
                var worn = Equipment.Worn(ch, slot);
                ItemAtlas.DrawGear(slotRect, worn);
                if (worn == null)
                {
                    float inset = 12f;
                    ItemAtlas.DrawHud(new Rect(slotRect.x + inset, slotRect.y + inset,
                            slotRect.width - inset * 2f, slotRect.height - inset * 2f),
                        ItemAtlas.KeyForSlot(slot), new Color(1f, 1f, 1f, 0.28f));
                    Hint(new Rect(slotRect.x - 4f, slotRect.yMax - 2f, slotRect.width + 8f, 16f),
                        Equipment.SlotName(slot));
                }
                else if (worn.Enhance > 0)
                {
                    Hint(new Rect(slotRect.x, slotRect.yMax - 2f, slotRect.width, 16f),
                        $"+{worn.Enhance}");
                }
                if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none) && !ch.IsDeleted)
                {
                    if (worn != null) Equipment.TryUnequip(ch, slot);
                    _bagFilter = (int)slot;
                }
            }

            var bagPanel = new Rect(r.x + leftW, r.y, r.width - leftW, r.height - 56f);
            if (!UiAtlas.DrawSliced(bagPanel, "panel", 16f, new Color(1f, 1f, 1f, 0.9f)))
                UiAtlas.Draw(bagPanel, "panel");
            int filled = 0;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (_bagFilter < 0 || (int)bag[i].Slot == _bagFilter) filled++;
            Hint(new Rect(bagPanel.x + 12f, bagPanel.y + 8f, bagPanel.width - 24f, 20f),
                $"가방  {filled}/{bag.Count}");

            float tabY = bagPanel.y + 32f;
            float tabW = (bagPanel.width - 24f) / 7f;
            DrawBagFilterTab(new Rect(bagPanel.x + 10f, tabY, tabW, 26f), "전체", -1);
            for (int s = 0; s < Equipment.SlotCount; s++)
            {
                var tr = new Rect(bagPanel.x + 10f + (s + 1) * tabW, tabY, tabW, 26f);
                DrawBagFilterTab(tr, Equipment.SlotName((EquipSlot)s), s);
            }

            const float cell = 56f, gap = 8f;
            float gx = bagPanel.x + 14f, gy = tabY + 36f;
            int col = 0, shown = 0;
            for (int i = 0; i < bag.Count && shown < 16; i++)
            {
                if (_bagFilter >= 0 && (int)bag[i].Slot != _bagFilter) continue;
                var gcell = new Rect(gx + col * (cell + gap), gy, cell, cell);
                if (gcell.yMax > bagPanel.yMax - 10f) break;
                ItemAtlas.DrawGear(gcell, bag[i]);
                if (bag[i].Enhance > 0)
                    Hint(new Rect(gcell.x, gcell.yMax - 14f, gcell.width, 14f), $"+{bag[i].Enhance}");
                if (GUI.Button(gcell, GUIContent.none, GUIStyle.none) && !ch.IsDeleted)
                    Equipment.TryEquip(ch, bag[i].Id);
                col++;
                if (col >= 4) { col = 0; gy += cell + gap; }
                shown++;
            }

            var bar = new Rect(r.x, r.yMax - 48f, r.width, 44f);
            var actions = UiPages.Grid(bar, 3, 1, 12f);
            if (CompactAction(actions[0], "자동 장착", "sword") && !ch.IsDeleted)
                AutoEquip(ch);
            CompactAction(actions[1],
                $"{Economy.FormatCurrency(GameState.Wallet.Copper)}  ·  석 {GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone)}",
                "gold", locked: true);
            if (CompactAction(actions[2], "목록으로", "characters"))
            {
                _selectedCharacter = -1;
                _detailPage = 0;
            }
        }

        void DrawBagFilterTab(Rect tr, string label, int filter)
        {
            bool on = _bagFilter == filter;
            UiAtlas.Draw(tr, UiAtlas.ButtonKey(false, on), on ? (Color?)null : new Color(1f, 1f, 1f, 0.62f));
            Hint(tr, label);
            if (GUI.Button(tr, GUIContent.none, GUIStyle.none))
                _bagFilter = filter;
        }

        bool CompactAction(Rect r, string label, string icon, bool locked = false)
        {
            var tint = locked ? new Color(1f, 1f, 1f, 0.55f) : (Color?)null;
            UiAtlas.Draw(r, UiAtlas.ButtonKey(false, false), tint);
            ItemAtlas.DrawHud(new Rect(r.x + 8f, r.y + 6f, 32f, 32f), icon, tint);
            Hint(new Rect(r.x + 44f, r.y + 10f, r.width - 52f, 24f), label);
            if (locked) return false;
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        static void AutoEquip(CharacterRecord ch)
        {
            var bag = Equipment.Unequipped();
            for (int s = 0; s < Equipment.SlotCount; s++)
            {
                if (Equipment.Worn(ch, (EquipSlot)s) != null) continue;
                for (int i = 0; i < bag.Count; i++)
                {
                    if ((int)bag[i].Slot != s) continue;
                    Equipment.TryEquip(ch, bag[i].Id);
                    break;
                }
            }
        }
    }
}
