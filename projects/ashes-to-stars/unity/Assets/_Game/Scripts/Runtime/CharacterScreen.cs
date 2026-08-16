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
        protected override string Subtitle => "왼쪽 바둑판에서 고르면 오른쪽에 모습과 정보가 나온다(§3·§4)";
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
        Vector2 _listScroll;

        protected override void Body(Rect r)
        {
            GameFlow.RestorePlayRosterIfRequested();
            GameFlow.SeedV4WipeQaIfRequested();
            SeedRarityQaIfRequested();
            SeedFusionQaIfRequested();
            SeedSpecialJobQaIfRequested();
            SeedTowerEndingQaIfRequested();
            SeedSoloRaidQaIfRequested();
            FloorRecruit.SeedQaIfRequested();
            SeedCharLookQaIfRequested();
            StarterPick.SeedQaIfRequested();
            StarterSecond.SeedQaIfRequested();
            SeedDefenseRecoverQaIfRequested();
            SeedRaceRecoverQaIfRequested();
            Rebirth.SeedQaIfRequested();
            Memorial.SeedQaIfRequested();
            if ((System.Environment.GetEnvironmentVariable(Rebirth.EnvShow) == "2"
                 || System.Environment.GetEnvironmentVariable(Memorial.EnvShow) == "1")
                && _selectedCharacter < 0)
                _selectedCharacter = 0;
            if (StarterSecond.Pending)
            {
                DrawStarterSecond(r);
                return;
            }
            if (_fusing)
            {
                DrawFusion(r);
                return;
            }
            if (_choosingAdvancement && _selectedCharacter >= 0)
            {
                var characters = LifeSystem.GetCharacters();
                if (_selectedCharacter < characters.Count)
                    DrawAdvancement(r, characters[_selectedCharacter]);
                return;
            }

            _listPage = DrawTabs(r, new[] { "명부", "합성" }, _listPage);
            var page = UiPages.AfterTabs(r);
            if (_listPage == 1)
            {
                DrawFusionEntry(page);
                return;
            }
            DrawRosterSplit(page);
        }

        void DrawAdvancement(Rect r, CharacterRecord ch)
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
        }

        void DrawAttributes(Rect r, CharacterRecord ch)
        {
                    string detail = $"{ch.Name} ({ch.Job}) · {ExpText(ch)}";
                    if (!ch.IsDeleted && ch.Level == Rebirth.StartLevel
                        && !string.IsNullOrEmpty(Rebirth.LastName)
                        && ch.Name == Rebirth.LastName)
                        detail += " · " + Rebirth.DoneLine();
                    else if (System.Environment.GetEnvironmentVariable(Rebirth.EnvShow) == "2"
                        && !ch.IsDeleted)
                        detail += " · " + Rebirth.Line();
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
                        Info(r, 1, Memorial.HasRecord(ch)
                            ? Memorial.Line(ch)
                            : (ch.IsSpecialJob
                                ? "삭제됨 — 특수 직업은 환생석으로 되돌릴 수 없다(§3)"
                                : "삭제됨 — 환생석으로만 복구 가능(§4)"));
                        UiAtlas.DrawHearts(new Rect(r.xMax - 90, r.y + (RowH + RowGap) + 18, 80, 22),
                            ch.DeathCount, true, ch.MaxLives);
                        if (Memorial.HasRecord(ch))
                        {
                            Info(r, 2, Memorial.GearLine(ch));
                            if (!string.IsNullOrEmpty(Memorial.PartyLine(ch)))
                                Info(r, 3, Memorial.PartyLine(ch));
                        }
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
                            bool posted = DefenseState.Contains(_selectedCharacter);
                            Info(r, 2, posted
                                ? $"수비대 회복 {LifeSystem.FormatRecoveryPhrase(recoveryTime)} — 출전 불가(§15)"
                                : $"회복 {LifeSystem.FormatRecoveryPhrase(recoveryTime)} — 출전 불가(§4·§18-8)");
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
        }

        void DrawStarterSecond(Rect r)
        {
            Hint(new Rect(r.x, r.y, r.width, 24f), StarterSecond.PickTitle);
            var picks = UiPages.JobPickCards(new Rect(r.x, r.y + 28f, r.width, r.height - 28f),
                LifeSystem.BasicJobs.Length);
            for (int i = 0; i < LifeSystem.BasicJobs.Length && i < picks.Length; i++)
            {
                string job = LifeSystem.BasicJobs[i];
                if (DrawCard(picks[i], LifeSystem.BasicJobLabel(job),
                        StarterSecond.PickSubtitle))
                    StarterSecond.TryClaim(job);
            }
        }

        void DrawRosterSplit(Rect r)
        {
            if (FloorRecruit.AwaitingPick)
            {
                Hint(new Rect(r.x, r.y, r.width, 24f), FloorRecruit.PickTitle());
                string special = FloorRecruit.SpecialHint();
                float pickTop = 28f;
                if (!string.IsNullOrEmpty(special))
                {
                    Hint(new Rect(r.x, r.y + 26f, r.width, 22f), special);
                    pickTop = 52f;
                }
                var picks = UiPages.JobPickCards(new Rect(r.x, r.y + pickTop, r.width, r.height - pickTop),
                    LifeSystem.BasicJobs.Length);
                for (int i = 0; i < LifeSystem.BasicJobs.Length && i < picks.Length; i++)
                {
                    string job = LifeSystem.BasicJobs[i];
                    if (DrawCard(picks[i], LifeSystem.BasicJobLabel(job),
                            FloorRecruit.PickSubtitle()))
                        FloorRecruit.TryClaim(job);
                }
                return;
            }
            if (StarterSecond.Pending)
            {
                DrawStarterSecond(r);
                return;
            }
            if (FloorRecruit.PendingSpecialBanner)
                Hint(new Rect(r.x, r.y, r.width, 22f), FloorRecruit.SpecialHint());
            float top = FloorRecruit.PendingSpecialBanner ? 28f : 0f;
            var area = new Rect(r.x, r.y + top, r.width, r.height - top);
            var allCharacters = LifeSystem.GetCharacters();
            if (allCharacters.Count == 0)
            {
                Hint(area, "명부가 비었다");
                return;
            }
            if (_selectedCharacter < 0 || _selectedCharacter >= allCharacters.Count)
                _selectedCharacter = 0;

            UiPages.RosterSplit(area, out var list, out var stage);
            const float partyH = 56f;
            var listBody = new Rect(list.x, list.y, list.width, Mathf.Max(40f, list.height - partyH - 8f));
            var partyRect = new Rect(list.x, list.yMax - partyH, list.width, partyH);
            int cols = UiPages.RosterCols;
            int rows = (allCharacters.Count + cols - 1) / cols;
            float contentH = Mathf.Max(listBody.height,
                rows * (UiPages.RosterCellH + UiPages.RosterRowGap));
            var view = new Rect(0f, 0f, Mathf.Max(40f, listBody.width - 16f), contentH);
            _listScroll = GUI.BeginScrollView(listBody, _listScroll, view);
            var board = new Rect(0f, 0f, view.width, contentH);
            for (int i = 0; i < allCharacters.Count; i++)
            {
                var cell = UiPages.RosterCell(board, i);
                DrawRosterCell(cell, allCharacters[i], i == _selectedCharacter, i);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                {
                    _selectedCharacter = i;
                    _choosingAdvancement = false;
                    _detailPage = 0;
                }
            }
            GUI.EndScrollView();
            if (CompactAction(partyRect, $"파티 {PartyState.Slots.Count}/5", "tank"))
                GameFlow.Go(GameFlow.Party);

            var ch = allCharacters[_selectedCharacter];
            int recoverLeft = LifeSystem.GetRecoveryTimeRemaining(ch);
            if (recoverLeft > 0)
            {
                Hint(new Rect(stage.x, stage.y, stage.width, 22f),
                    DefenseState.Contains(_selectedCharacter)
                        ? $"수비대 회복 {LifeSystem.FormatRecoveryPhrase(recoverLeft)} — 출전 불가(§15)"
                        : $"회복 {LifeSystem.FormatRecoveryPhrase(recoverLeft)} — 출전 불가(§4·§18-8)");
                stage = new Rect(stage.x, stage.y + 24f, stage.width, Mathf.Max(40f, stage.height - 24f));
            }
            _detailPage = DrawTabs(stage, new[] { "장비", "속성" }, _detailPage);
            var detailBody = UiPages.AfterTabs(stage);
            if (_detailPage == 0) DrawEquipStudio(detailBody, ch);
            else DrawAttributes(detailBody, ch);
        }

        void DrawRosterCell(Rect cell, CharacterRecord ch, bool selected, int rosterIndex)
        {
            var tint = ch.IsDeleted ? new Color(1f, 1f, 1f, 0.45f) : (Color?)null;
            UiAtlas.DrawSliced(cell, UiAtlas.ButtonKey(false, selected), 10f,
                selected ? (Color?)null : new Color(1f, 1f, 1f, 0.78f));
            UiPages.RosterCellLayout(cell, out var face, out var nameR, out var jobR, out var hearts);
            UiAtlas.DrawRosterFrame(face);
            PortraitAtlas.Draw(face, PortraitAtlas.KeyForJob(ch.Job), tint);
            UiAtlas.Draw(new Rect(face.xMax - 14f, face.yMax - 14f, 18f, 18f), UiAtlas.RoleKey(ch.Job));
            string name = ch.IsRescue ? $"{ch.Name}·재건" : ch.Name;
            Hint(nameR, name);
            int cellLeft = LifeSystem.GetRecoveryTimeRemaining(ch);
            string job = ch.Job;
            if (cellLeft > 0 && DefenseState.Contains(rosterIndex))
                job = $"수비대 회복 {LifeSystem.FormatRecoveryPhrase(cellLeft)}";
            else if (cellLeft > 0)
                job = $"회복 {LifeSystem.FormatRecoveryPhrase(cellLeft)}";
            Hint(jobR, job);
            UiAtlas.DrawHearts(hearts, ch.DeathCount, ch.IsDeleted, ch.MaxLives);
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

        void SeedCharLookQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_CHAR_LOOK") != "1") return;
            FloorRecruit.ResetForTest();
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            Equipment.SeedCraftedLoadoutForQa(roster[0]);
            _selectedCharacter = 0;
            _listPage = 0;
            _detailPage = 0;
            _fusing = false;
            _choosingAdvancement = false;
        }

        void SeedRarityQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_UI_RARITY") != "1") return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            Equipment.SeedCraftedLoadoutForQa(roster[0]);
            if (_selectedCharacter < 0) _selectedCharacter = 0;
        }

        void SeedDefenseRecoverQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(DefenseState.EnvShow) != "1") return;
            DefenseState.SeedQaIfRequested();
            if (_selectedCharacter < 0) _selectedCharacter = 0;
        }

        void SeedRaceRecoverQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(LifeSystem.EnvShowRaceRecover) != "1") return;
            LifeSystem.SeedRaceRecoverQaIfRequested();
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

        /// <summary>오른쪽: 큰 모습 + 정보. 장비 6칸은 둘레, 가방은 정보 아래.</summary>
        void DrawEquipStudio(Rect r, CharacterRecord ch)
        {
            var stage = new Rect(r.x, r.y, r.width, r.height - 56f);
            if (!UiAtlas.DrawSliced(stage, "panel", 18f, new Color(1f, 1f, 1f, 0.9f)))
                UiAtlas.Draw(stage, "panel");

            string title = ch.Name;
            if (ch.IsRescue) title += " · 재건";
            if (ch.IsSpecialJob) title += " · 특수";
            Hint(new Rect(stage.x + 18f, stage.y + 10f, 360f, 24f), $"{title} · {ch.Job}");
            UiAtlas.Draw(new Rect(stage.x + 18f, stage.y + 36f, 26f, 26f), "sword");
            Hint(new Rect(stage.x + 48f, stage.y + 38f, 280f, 24f),
                $"전투력  {CombatPower(ch):N0}");
            UiAtlas.DrawHearts(new Rect(stage.xMax - 96f, stage.y + 14f, 80f, 22f),
                ch.DeathCount, ch.IsDeleted, ch.MaxLives);

            var face = UiPages.LargeLook(stage);
            var tint = ch.IsDeleted ? new Color(1f, 1f, 1f, 0.4f) : (Color?)null;
            DrawSelectedLook(face, ch.Job, tint);
            UiAtlas.Draw(new Rect(face.center.x - 16f, face.yMax - 18f, 32f, 32f),
                UiAtlas.RoleKey(ch.Job));

            var center = face.center;
            UiPages.EquipRingFit(stage, face, out float ringX, out float ringY);
            for (int i = 0; i < RingSlots.Length; i++)
            {
                var slot = RingSlots[i];
                float deg = UiPages.EquipRingDegrees[i];
                var slotRect = UiPages.ClampIn(stage,
                    UiPages.SlotOnRing(center, ringX, ringY, deg, UiPages.EquipSlotSize));
                var worn = Equipment.Worn(ch, slot);
                ItemAtlas.DrawGear(slotRect, worn);
                if (worn == null)
                {
                    float inset = 12f;
                    ItemAtlas.DrawHud(new Rect(slotRect.x + inset, slotRect.y + inset,
                            slotRect.width - inset * 2f, slotRect.height - inset * 2f),
                        ItemAtlas.KeyForSlot(slot), new Color(1f, 1f, 1f, 0.28f));
                    var cap = UiPages.ClampIn(stage,
                        new Rect(slotRect.x - 4f, slotRect.yMax - 2f, slotRect.width + 8f,
                            UiPages.EquipLabelH));
                    Hint(cap, Equipment.SlotName(slot));
                }
                else if (worn.Enhance > 0)
                {
                    var cap = UiPages.ClampIn(stage,
                        new Rect(slotRect.x, slotRect.yMax - 2f, slotRect.width, UiPages.EquipLabelH));
                    Hint(cap, $"+{worn.Enhance}");
                }
                if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none) && !ch.IsDeleted)
                {
                    if (worn != null) Equipment.TryUnequip(ch, slot);
                    _bagFilter = (int)slot;
                }
            }

            float infoX = Mathf.Max(face.xMax + 36f, stage.x + 300f);
            var info = new Rect(infoX, stage.y + 36f, stage.xMax - infoX - 14f, stage.height - 48f);
            DrawInspectInfo(info, ch);

            var bar = new Rect(r.x, r.yMax - 48f, r.width, 44f);
            var actions = UiPages.Grid(bar, 2, 1, 12f);
            if (CompactAction(actions[0], "자동 장착", "sword") && !ch.IsDeleted)
                AutoEquip(ch);
            CompactAction(actions[1],
                $"{Economy.FormatCurrency(GameState.Wallet.Copper)}  ·  석 {GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone)}",
                "gold", locked: true);
        }

        void DrawInspectInfo(Rect r, CharacterRecord ch)
        {
            float y = r.y;
            void Line(string text)
            {
                if (y + 20f > r.yMax) return;
                Hint(new Rect(r.x, y, r.width, 20f), text);
                y += 22f;
            }

            Line(ExpText(ch));
            if (!ch.IsDeleted && ch.Level < LifeSystem.MaxLevel)
            {
                float need = LifeSystem.ExpToNext(ch.Level);
                float ratio = need <= 0f ? 0f : Mathf.Clamp01(ch.Exp / (float)need);
                UiAtlas.DrawMeter(new Rect(r.x, y, Mathf.Min(220f, r.width), 16f),
                    "xp_frame", ratio, new Color(0.45f, 0.72f, 1f));
                y += 22f;
            }
            if (ch.IsDeleted)
                Line(ch.IsSpecialJob ? "삭제됨 · 환생 불가" : "삭제됨 · 환생석만");
            else
                Line(LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중");
            Line(ch.Advancement switch
            {
                AdvancementTier.Second => "전직 2차 · 초필 해금",
                AdvancementTier.First => "전직 1차",
                _ => "전직 기본",
            });
            if (ch.IsSpecialJob) Line($"특수 직업 · 목숨 {ch.MaxLives}");
            if (ch.IsRescue) Line("긴급 재건");
            if (PartyState.Contains(_selectedCharacter)) Line("지금 출전 편성");
            if (DefenseState.Contains(_selectedCharacter)) Line("수비 배치 중");
            if (HuntSchedule.Contains(_selectedCharacter)) Line("일정 사냥 중");
            if (TowerEnding.HasStarLook) Line(TowerEnding.LookName);
            if (SoloRaidClear.HasLook) Line(SoloRaidClear.LookName);
            if (!ch.IsDeleted && ch.AbsorbedBoons.Count > 0)
                Line($"흡수 {Fusion.AbsorbedSummary(ch)} ({ch.AbsorbedBoons.Count}/{Fusion.SlotCap})");
            if (!ch.IsDeleted && ch.PendingBoon >= 0)
                Line($"보류 {Fusion.LabelOf((BoonId)ch.PendingBoon)}");

            Line("장착");
            for (int s = 0; s < Equipment.SlotCount; s++)
            {
                var worn = Equipment.Worn(ch, (EquipSlot)s);
                string slot = Equipment.SlotName((EquipSlot)s);
                Line(worn == null
                    ? $"{slot}  ·  없음"
                    : $"{slot}  ·  {worn.Name}" + (worn.Enhance > 0 ? $" +{worn.Enhance}" : ""));
            }

            y += 6f;
            var bag = Equipment.Unequipped();
            int filled = 0;
            for (int i = 0; i < bag.Count; i++)
                if (_bagFilter < 0 || (int)bag[i].Slot == _bagFilter) filled++;
            Line($"가방  {filled}/{bag.Count}");
            const float cell = 44f, gap = 6f;
            int col = 0;
            int shown = 0;
            for (int i = 0; i < bag.Count && shown < 8; i++)
            {
                if (_bagFilter >= 0 && (int)bag[i].Slot != _bagFilter) continue;
                var gcell = new Rect(r.x + col * (cell + gap), y, cell, cell);
                if (gcell.yMax > r.yMax) break;
                ItemAtlas.DrawGear(gcell, bag[i]);
                if (GUI.Button(gcell, GUIContent.none, GUIStyle.none) && !ch.IsDeleted)
                    Equipment.TryEquip(ch, bag[i].Id);
                col++;
                if (col >= 4) { col = 0; y += cell + gap; }
                shown++;
            }
        }

        static void DrawSelectedLook(Rect target, string job, Color? tint) =>
            UiPages.DrawJobLook(target, job, false, tint);

        void DrawBagFilterTab(Rect tr, string label, int filter)
        {
            bool on = _bagFilter == filter;
            UiAtlas.DrawSliced(tr, UiAtlas.ButtonKey(false, on), 8f,
                on ? (Color?)null : new Color(1f, 1f, 1f, 0.62f));
            Hint(tr, label);
            if (GUI.Button(tr, GUIContent.none, GUIStyle.none))
                _bagFilter = filter;
        }

        bool CompactAction(Rect r, string label, string icon, bool locked = false)
        {
            var tint = locked ? new Color(1f, 1f, 1f, 0.55f) : (Color?)null;
            UiAtlas.DrawSliced(r, UiAtlas.ButtonKey(false, false), 10f, tint);
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
