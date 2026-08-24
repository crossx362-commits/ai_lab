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
        protected override string Subtitle =>
            GearOpt.ShowListQa ? GearOpt.ListLine()
            : GearOpt.ShowQa ? GearOpt.Line()
            : BagSlots.ShowQa ? BagSlots.Line()
            : EquipJob.ShowQa ? EquipJob.Line()
            : EquipLevel.ShowQa ? EquipLevel.Line()
            : ReviveCap.ShowQa ? ReviveCap.Line()
            : DeathCap.ShowQa ? DeathCap.Line()
            : EliteDrop.ShowQa ? EliteDrop.Line()
            : GearDrop.ShowQa ? GearDrop.Line()
            : CharHud.ShowQa ? CharHud.Line()
            : "왼쪽 바둑판에서 고르면 오른쪽에 모습과 정보가 나온다(§3·§4)";
        protected override bool ShowRarityPreview => UiAtlas.QaShowRarity;

        /// <summary>레벨·경험치 진척 표기(§18-6). 만렙은 MAX로.</summary>
        static string ExpText(CharacterRecord ch) =>
            ch.Level >= LifeSystem.MaxLevel
                ? $"Lv.{ch.Level} · 경험 최대"
                : $"Lv.{ch.Level} · 경험 {ch.Exp}/{LifeSystem.ExpToNext(ch.Level)}";

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
        string _equipMsg = "";

        protected override void Body(Rect r)
        {
            GameFlow.RestorePlayRosterIfRequested();
            GameFlow.SeedV4WipeQaIfRequested();
            SeedRarityQaIfRequested();
            SeedFusionQaIfRequested();
            SeedFusionEntryQaIfRequested();
            SeedSpecialJobQaIfRequested();
            SeedJobTraitQaIfRequested();
            SeedSkillCostQaIfRequested();
            SeedSkillDescQaIfRequested();
            SeedSkillUltQaIfRequested();
            SeedRaceTraitQaIfRequested();
            SeedRaceDefenseQaIfRequested();
            SeedRaceDurabilityQaIfRequested();
            SeedReviveCapQaIfRequested();
            SeedDeathCapQaIfRequested();
            SeedPerfCapQaIfRequested();
            SeedSummonCapQaIfRequested();
            SeedTowerEndingQaIfRequested();
            SeedSoloRaidQaIfRequested();
            FloorRecruit.SeedQaIfRequested();
            SeedCharLookQaIfRequested();
            CharHud.SeedQaIfRequested();
            EquipJob.SeedQaIfRequested();
            EquipLevel.SeedQaIfRequested();
            BagSlots.SeedQaIfRequested();
            EliteDrop.SeedQaIfRequested();
            GearDrop.SeedQaIfRequested();
            GearOpt.SeedQaIfRequested();
            GearOpt.SeedListQaIfRequested();
            if ((GearOpt.ShowQa || GearOpt.ShowListQa) && _selectedCharacter < 0)
                _selectedCharacter = 0;
            if (EquipJob.ShowQa && _selectedCharacter < 0)
                _selectedCharacter = EquipJob.QaHealerIndex();
            if (EquipLevel.ShowQa && _selectedCharacter < 0)
                _selectedCharacter = EquipLevel.QaCharIndex();
            StarterPick.SeedQaIfRequested();
            StarterSecond.SeedQaIfRequested();
            SeedDefenseRecoverQaIfRequested();
            SeedRaceRecoverQaIfRequested();
            Rebirth.SeedQaIfRequested();
            RebirthSkill.SeedQaIfRequested();
            Memorial.SeedQaIfRequested();
            if ((System.Environment.GetEnvironmentVariable(Rebirth.EnvShow) == "2"
                 || System.Environment.GetEnvironmentVariable(RebirthSkill.EnvShow) == "2"
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
                    // 속성 탭은 표제·목숨·기본스탯·이동기·전직·직업 특성·보유 스킬·종족 특성·정체성까지
                    // ✅ 소비처가 10행 가까이 쌓여 76px 기본 그리드로는 하위 줄이 넘쳐 사라졌다(밀도 상한).
                    // 이 패널만 행 피치를 46/40으로 낮춰 같은 index 그리드로 전부 담는다. 끝에서 되돌린다.
                    RowPitch = 46f; RowHt = 40f;
                    string detail = $"{ch.Name} ({CharHud.JobFace(ch.Job)}) · {ExpText(ch)}";
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

                    // 목숨/전직 줄이 절대 인덱스로 그려지던 것을 실제 사용된 최상단 행 다음으로
                    // 붙인다. 회복·부활 슬롯(row 2·3)이 안 쓰이는 건강한 캐릭터에서 빈 행이 생겨
                    // 직업 특성 줄이 패널 하단으로 밀리고, 보유 스킬 줄은 r.yMax를 넘겨 아예 안
                    // 그려졌다(Info는 넘치면 조용히 건너뛴다). statusMax는 이 섹션이 실제로 쓴
                    // 가장 아래 행이며, 전직 블록은 항상 그 다음부터 시작해 겹치지 않는다.
                    int statusMax = 1;
                    // 목숨 상태 표시 — 유니코드 하트는 □로 나와 아틀라스 조각을 쓴다.
                    if (ch.IsDeleted)
                    {
                        Info(r, 1, Memorial.HasRecord(ch)
                            ? Memorial.Line(ch)
                            : (ch.IsSpecialJob
                                ? "삭제됨 — 특수 직업은 환생석으로 되돌릴 수 없다(§3)"
                                : "삭제됨 — 환생석으로만 복구 가능(§4)"));
                        UiAtlas.DrawHearts(new Rect(r.xMax - 90, r.y + RowPitch + 18, 80, 22),
                            ch.DeathCount, true, ch.MaxLives);
                        if (Memorial.HasRecord(ch))
                        {
                            Info(r, 2, Memorial.GearLine(ch)); statusMax = 2;
                            if (!string.IsNullOrEmpty(Memorial.PartyLine(ch)))
                            {
                                Info(r, 3, Memorial.PartyLine(ch)); statusMax = 3;
                            }
                            if (!string.IsNullOrEmpty(Memorial.TimeLine(ch)))
                            {
                                Info(r, 4, Memorial.TimeLine(ch)); statusMax = 4;
                            }
                        }
                    }
                    else
                    {
                        string status = LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중";
                        Info(r, 1, ch.IsSpecialJob
                            ? $"목숨 {ch.DeathCount}/{ch.MaxLives} {status} · 부활초·환생석 불가(§3)"
                            : $"목숨 {ch.DeathCount}/{ch.MaxLives} {status}");
                        UiAtlas.DrawHearts(new Rect(r.xMax - 90, r.y + RowPitch + 18, 80, 22),
                            ch.DeathCount, false, ch.MaxLives);

                        // 회복 중이면 시간 표시
                        int recoveryTime = LifeSystem.GetRecoveryTimeRemaining(ch);
                        if (recoveryTime > 0)
                        {
                            bool posted = DefenseState.Contains(_selectedCharacter);
                            Info(r, 2, posted
                                ? $"수비대 회복 {LifeSystem.FormatRecoveryPhrase(recoveryTime)} — 출전 불가(§15)"
                                : $"회복 {LifeSystem.FormatRecoveryPhrase(recoveryTime)} — 출전 불가(§4·§18-8)");
                            statusMax = 2;
                        }
                    }

                    // 부활초 사용 버튼 — 특수 직업은 1회 사망이 곧 소멸이라 버튼을 열지 않는다.
                    if (ch.IsSpecialJob && !ch.IsDeleted)
                    {
                        Locked(r, 3, "부활초 사용", "특수 직업은 부활초를 쓸 수 없다(§3)",
                            ItemAtlas.KeyFor(Economy.LifeItem.RevivalTea));
                        statusMax = Math.Max(statusMax, 3);
                    }
                    else if (!ch.IsDeleted && ch.DeathCount > 0 && LifeSystem.GetRevivePotions() > 0)
                    {
                        if (Row(r, 3, "부활초 사용", $"사망 카운트 1 차감 (보유: {LifeSystem.GetRevivePotions()}/{ReviveCap.Limit()})",
                                ItemAtlas.KeyFor(Economy.LifeItem.RevivalTea)))
                        {
                            LifeSystem.UseRevivePotion(ch);
                        }
                        statusMax = Math.Max(statusMax, 3);
                    }
                    else if (!ch.IsDeleted && ch.DeathCount > 0 && LifeSystem.GetRevivePotions() == 0)
                    {
                        Info(r, 3, "부활초 없음 — 던전·레이드에서 획득 가능(§4)");
                        statusMax = Math.Max(statusMax, 3);
                    }

                    // §3·§4 기본 전투 스탯(JobDef 최대체력·공격력·사거리·공격간격의 유일한 소비처).
                    // **속성** 탭의 표제 데이터라 이동기 프로필·종족 특성보다 우선순위가 높다 — 그래서
                    // 관례(새 조각은 맨 뒤)와 달리 전직 블록 **앞**(status 바로 뒤)에 두어 패널이 꽉 차도
                    // 항상 보이게 한다. 매칭 직업이 없으면(기본직 또는 에셋 미로드) 빈 문자열이라 줄을
                    // 그리지 않고 statusMax도 안 밀어, 종족 줄 여유를 뺏지 않는다(지어내지 않음).
                    if (!ch.IsDeleted)
                    {
                        string stats = JobInfo.StatLine(ch.Job);
                        if (!string.IsNullOrEmpty(stats)) { Info(r, statusMax + 1, stats); statusMax += 1; }

                        // §5 이동기 프로필(형태·거리·무적·쿨) + §4 사망 리스크 + §6 자동사냥을 한 행에.
                        // 무적은 원장 379 「이 게임 조작의 핵심 기술」이라 ConceptLine(직업 특성)·SkillLine(보유
                        // 스킬)보다 우선순위가 높다 — StatLine처럼 전직 블록 **앞** 우선존에 둬 패널이 꽉 차도
                        // 항상 보이게 한다. 과거엔 Concept·Skill 뒤(행 6)라 r.yMax를 넘겨 §5 표시행 전체가
                        // 조용히 사라졌다(Info는 넘치면 건너뜀 — GameScreen.cs:485). 여유가 없으면 대신 하위
                        // 우선순위(SkillLine·종족 줄)가 밀린다. 각 조각은 빈 값이면 빠지고(지어내지 않음),
                        // 넷 다 비면(기본직·에셋 미로드) 행을 안 그려 종족 줄 여유를 뺏지 않는다.
                        string dash = JobInfo.MovementLine(ch.Job);
                        string mobility = JobInfo.MobilityStatLine(ch.Job);
                        string risk = JobInfo.RiskLine(ch.Job);
                        string hunt = JobInfo.AutoHuntLine(ch.Job);
                        // 순서 = 우선순위. InfoAt은 LabelClip(좌측 정렬·우측 잘림)이라 행이 넘치면 뒤부터
                        // 잘린다. 형태·거리·무적·쿨(둘 다 §5 이동기)을 **맨 앞**에 몰아 절대 안 잘리게 하고,
                        // 리스크(§4)·자동사냥(§6)은 뒤라 여유 없을 때 이것만 잘린다.
                        string profile = dash;
                        if (!string.IsNullOrEmpty(mobility))
                            profile = string.IsNullOrEmpty(profile) ? mobility : profile + " · " + mobility;
                        if (!string.IsNullOrEmpty(risk))
                            profile = string.IsNullOrEmpty(profile) ? risk : profile + " · " + risk;
                        if (!string.IsNullOrEmpty(hunt))
                            profile = string.IsNullOrEmpty(profile) ? hunt : profile + " · " + hunt;
                        if (!string.IsNullOrEmpty(profile)) { Info(r, statusMax + 1, profile); statusMax += 1; }
                    }

                    int advancementRow = statusMax + 1;
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

                    // §4 직업 특성·§3 보유 스킬·§18-9 종족 특성/정체성/이속/체력/방어 — 전부 **이미 배선된 ✅ 표시
                    // 소비처**(ConceptLine·SkillLine(쿨·위력·반경·소모)·SkillDescLine(설명)·MechanicLine·IdentityLine·SpeedLine·HealthLine·DefenseLine)인데, 76px 기본 그리드에선
                    // 1차+ 캐릭터에서 표제·전직·이동기 우선존이 행 6을 다 먹어 넘쳐 사라졌다(밀도 상한).
                    // 이 패널은 위 DrawAttributes 머리에서 RowPitch/RowHt를 컴팩트로 낮춰(공유 헬퍼 필드)
                    // 같은 인덱스 그리드로 더 많은 행을 담으므로 이 줄들도 한 판에 보인다. 기본직·에셋
                    // 미로드면 빈 문자열이라 줄을 안 그린다(지어내지 않음).
                    if (!ch.IsDeleted)
                    {
                        string trait = JobInfo.ConceptLine(ch.Job);
                        if (!string.IsNullOrEmpty(trait))
                        {
                            // Info(LabelClip·inner 20px)면 수호기사 「소모해 보」에서 우측이 잘린다 —
                            // InfoWrap(LabelFit). 기본 52px면 컴팩트 피치에서 2칸을 먹어 초필이
                            // 밀리므로 한 행(RowHt). QA_NO_CONCEPT_WRAP이면 옛 한 줄 Clip.
                            if (JobInfo.ConceptWrapBlocked) Info(r, advancementRow++, trait);
                            else advancementRow += InfoWrap(r, advancementRow, trait, RowHt);
                        }
                        // §3 SkillDef.쿨다운·위력배율·반경·자원소모 — SkillLine이 이름 옆에 (N초·×P·반경R·소모C) (표시 전용).
                        // 환생 계승이 있으면 1개만(§4). QA_NO_REBORN_SKILL이면 옛 직업 표 전체.
                        string skills = RebirthSkill.SkillLine(ch);
                        if (!string.IsNullOrEmpty(skills)) Info(r, advancementRow++, skills);
                        // §3 SkillDef.초필살기 — SkillUltLine이 초필 이름·쿨만 별도 한 줄(표시 전용).
                        // SkillLine에 붙이면 뒤부터 잘리고, 설명 줄은 이미 두 줄이라 초필을 잃는다.
                        // QA_NO면 빈 문자열이라 행을 안 그린다. 계승 1개면 초필 줄은 접는다.
                        string skillUlt = RebirthSkill.SkillUltLine(ch);
                        if (!string.IsNullOrEmpty(skillUlt)) Info(r, advancementRow++, skillUlt);
                        // §3 SkillDef.설명 — SkillDescLine이 이름:설명 한 줄(표시 전용). SkillLine에 붙이면
                        // 뒷 스킬 이름이 LabelClip에 잘리므로 다음 행. QA_NO면 빈 문자열이라 행을 안 그린다.
                        // Info(LabelClip·inner 20px)면 마법사 「빙결: 광」에서 우측이 잘린다 — 두 줄
                        // InfoWrap(LabelFit). QA_NO_SKILL_DESC_WRAP이면 옛 한 줄 Clip.
                        string skillDesc = RebirthSkill.SkillDescLine(ch);
                        if (!string.IsNullOrEmpty(skillDesc))
                        {
                            if (JobInfo.SkillDescWrapBlocked) Info(r, advancementRow++, skillDesc);
                            else advancementRow += InfoWrap(r, advancementRow, skillDesc);
                        }
                        // §18-9·§14 종족 고유 메커니즘·정체성 — 계정 종족(RacePrefs.Get) RaceDef의 유일 소비처.
                        string raceTrait = RaceInfo.MechanicLine(RacePrefs.Get());
                        if (!string.IsNullOrEmpty(raceTrait)) Info(r, advancementRow++, raceTrait);
                        string raceIdent = RaceInfo.IdentityLine(RacePrefs.Get());
                        if (!string.IsNullOrEmpty(raceIdent)) Info(r, advancementRow++, raceIdent);
                        // §18-9 RaceDef.이속배율 — 드워프 ×0.85 등 기준과 다를 때만 한 줄(SpeedLine).
                        string raceSpeed = RaceInfo.SpeedLine(RacePrefs.Get());
                        if (!string.IsNullOrEmpty(raceSpeed)) Info(r, advancementRow++, raceSpeed);
                        // §18-9 RaceDef.체력배율 — 엘프 ×0.85 등 기준과 다를 때만 한 줄(HealthLine).
                        string raceHp = RaceInfo.HealthLine(RacePrefs.Get());
                        if (!string.IsNullOrEmpty(raceHp)) Info(r, advancementRow++, raceHp);
                        // §18-9 RaceDef.방어배율 — 엘프 ×0.8 등 기준과 다를 때만 한 줄(DefenseLine).
                        // 표시 전용. W3Party 전투 피해 배율은 안 건드린다. QA_NO면 빈 문자열이라 행을 안 그린다.
                        string raceDef = RaceInfo.DefenseLine(RacePrefs.Get());
                        if (!string.IsNullOrEmpty(raceDef)) Info(r, advancementRow++, raceDef);
                        // §18-9 RaceDef.건물내구배율 — 드워프 ×1.2 등 기준과 다를 때만 한 줄.
                        // 표시 전용. 건물 HP는 안 건드린다. QA_NO면 빈 문자열이라 행을 안 그린다.
                        string raceDur = RaceInfo.DurabilityLine(RacePrefs.Get());
                        if (!string.IsNullOrEmpty(raceDur)) Info(r, advancementRow++, raceDur);
                        // §4 BalanceConfig.부활초소지상한 — ReviveCap.Line. QA_NO면 빈 문자열.
                        string teaCap = ReviveCap.Line();
                        if (!string.IsNullOrEmpty(teaCap)) Info(r, advancementRow++, teaCap);
                        // §4 BalanceConfig.사망상한 — DeathCap.Line. QA_NO면 빈 문자열.
                        string deathCap = DeathCap.Line();
                        if (!string.IsNullOrEmpty(deathCap)) Info(r, advancementRow++, deathCap);
                        // §10-9 BalanceConfig.잡몹상한 — PerfCap.Line. QA_NO면 빈 문자열.
                        string perfCap = PerfCap.Line();
                        if (!string.IsNullOrEmpty(perfCap)) Info(r, advancementRow++, perfCap);
                        // §10-9 BalanceConfig.소환수상한 — SummonCap.Line. QA_NO면 빈 문자열.
                        string summonCap = SummonCap.Line();
                        if (!string.IsNullOrEmpty(summonCap)) Info(r, advancementRow++, summonCap);
                    }
                    // 컴팩트 피치는 이 패널 전용 — 같은 화면의 index 그리드 경로(DrawAdvancement 등)가
                    // 기본 76/64를 기대하므로 반드시 되돌린다(안 하면 다음 프레임에 그 화면이 눌린다).
                    RowPitch = RowH + RowGap; RowHt = RowH;
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

            CharHud.RosterSplit(area, out var list, out var stage);
            const float partyH = 56f;
            var listBody = new Rect(list.x, list.y, list.width, Mathf.Max(40f, list.height - partyH - 8f));
            var partyRect = new Rect(list.x, list.yMax - partyH, list.width, partyH);
            int cols = CharHud.Cols;
            int rows = (allCharacters.Count + cols - 1) / cols;
            float contentH = Mathf.Max(listBody.height,
                rows * (CharHud.CellH + UiPages.RosterRowGap));
            var view = new Rect(0f, 0f, Mathf.Max(40f, listBody.width - 16f), contentH);
            _listScroll = GUI.BeginScrollView(listBody, _listScroll, view);
            var board = new Rect(0f, 0f, view.width, contentH);
            for (int i = 0; i < allCharacters.Count; i++)
            {
                var cell = CharHud.RosterCell(board, i);
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
            string job = CharHud.JobFace(ch.Job);
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
                         $"1차 이상 캐릭터를 소멸시켜 패시브를 흡수한다. {EstateStatusHud.ShortCopper(Fusion.CostCopper())}(§18-7)",
                         "buffer"))
            {
                _fusing = true;
                _fusionHost = -1;
                _fusionMaterial = -1;
            }
            DrawCard(cards[1], "규칙",
                $"슬롯 4 · {EstateStatusHud.ShortCopper(Fusion.CostCopper())} · 넘치면 본 뒤 교체/포기. 재료는 영묘에 안 간다",
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

        // QA — 1차 직업(수호기사) 캐릭터를 선택해 §4 직업 특성 줄(JobDef 소비처)이
        // 실제 렌더되는지 눈으로 확인한다. 기본 로스터는 전부 기본직이라 이 시드 없이는
        // 특성 줄이 뜰 상황을 화면에서 못 만든다. env 게이트라 일반 플레이엔 영향 없음.
        void SeedJobTraitQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_JOB_TRAIT") != "1") return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            roster[0].Job = "수호기사";
            roster[0].Advancement = AdvancementTier.First;
            _selectedCharacter = 0;
            _detailPage = 1; // 속성 탭 — DrawAttributes에 직업 특성 줄이 있다
        }

        // QA — 1차 검사. SkillDef.자원소모(일섬 5)가 SkillLine에 「소모5」로 보이는지
        // 육안 확인용. 수호기사 성채 방패도 소모60이 있지만 SelfCheck 대표 칸은 일섬이라 검사를 심는다.
        void SeedSkillCostQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_SKILL_COST") != "1") return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            roster[0].Job = "검사";
            roster[0].Advancement = AdvancementTier.First;
            _selectedCharacter = 0;
            _detailPage = 1;
        }

        // QA — 1차 마법사. SkillDef.설명(화염폭풍 장판 광역)이 SkillDescLine에 보이는지
        // 육안 확인용. 검사 일섬 설명은 길어 잘리기 쉬워 대표 칸은 마법사.
        void SeedSkillDescQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_SKILL_DESC") != "1") return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            roster[0].Job = "마법사";
            roster[0].Advancement = AdvancementTier.First;
            _selectedCharacter = 0;
            _detailPage = 1;
        }

        // QA — 1차 수호기사. SkillDef.초필살기(파티 전원 무적 180초)가 SkillUltLine에 보이는지
        // 육안 확인용. 원장 §3 예시 4직업 중 수호기사가 대표 칸. 마법사는 authored 초필이 없다.
        void SeedSkillUltQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_SKILL_ULT") != "1") return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            roster[0].Job = "수호기사";
            roster[0].Advancement = AdvancementTier.First;
            _selectedCharacter = 0;
            _detailPage = 1;
        }

        // 시각 QA. QA_RACE_TRAIT=1이면 계정 종족을 드워프로 두고 속성 탭을 연다 —
        // 기본직 캐릭터라 직업 특성/이동기 줄이 비어 종족 특성 줄이 패널 안에 넉넉히 들어간다.
        // 종족 특성 줄에 RaceInfo.MechanicLine이 붙이는 「(발동 25%)」(불굴 고유발동확률 0.25)를 육안 확인용.
        void SeedRaceTraitQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_RACE_TRAIT") != "1") return;
            RacePrefs.Set(RaceId.드워프);
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            _selectedCharacter = 0;
            _detailPage = 1;
        }

        // 시각 QA. QA_RACE_DEFENSE=1이면 계정 종족을 엘프로 두고 속성 탭을 연다 —
        // §18-9 방어 -20%가 DefenseLine에 보이는지 육안 확인용. 인간·드워프·수인은 ×1이라 줄이 없다.
        void SeedRaceDefenseQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_RACE_DEFENSE") != "1") return;
            RacePrefs.Set(RaceId.엘프);
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            _selectedCharacter = 0;
            _detailPage = 1;
        }

        // 시각 QA. QA_RACE_DURABILITY=1이면 계정 종족을 드워프로 두고 속성 탭을 연다 —
        // §18-9 건물 내구 +20%가 DurabilityLine에 보이는지 육안 확인용. 나머지 종족은 ×1이라 줄이 없다.
        void SeedRaceDurabilityQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(RaceInfo.EnvShowDurability) != "1") return;
            RacePrefs.Set(RaceId.드워프);
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            _selectedCharacter = 0;
            _detailPage = 1;
        }

        void SeedReviveCapQaIfRequested()
        {
            ReviveCap.SeedQaIfRequested();
            if (!ReviveCap.ShowQa) return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            _selectedCharacter = 0;
            _detailPage = 1;
        }

        void SeedDeathCapQaIfRequested()
        {
            DeathCap.SeedQaIfRequested();
            if (!DeathCap.ShowQa) return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            _selectedCharacter = 0;
            _detailPage = 1;
        }

        void SeedPerfCapQaIfRequested()
        {
            PerfCap.SeedQaIfRequested();
            if (!PerfCap.ShowQa) return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            int pick = 0;
            for (int i = 0; i < roster.Count; i++)
                if (!roster[i].IsDeleted) { pick = i; break; }
            _selectedCharacter = pick;
            _detailPage = 1;
        }

        void SeedSummonCapQaIfRequested()
        {
            SummonCap.SeedQaIfRequested();
            if (!SummonCap.ShowQa) return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            int pick = 0;
            for (int i = 0; i < roster.Count; i++)
                if (!roster[i].IsDeleted) { pick = i; break; }
            _selectedCharacter = pick;
            _detailPage = 1;
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


        void SeedFusionEntryQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_FUSION_ENTRY") != "1") return;
            // 안내 줄 샷: 합성 탭만. DrawFusion(소멸 확인)으로 들어가지 않는다.
            Environment.SetEnvironmentVariable("QA_FUSION", "1");
            Fusion.SeedQaIfRequested();
            Environment.SetEnvironmentVariable("QA_FUSION", null);
            _listPage = 1;
            _fusing = false;
            _selectedCharacter = -1;
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
            Info(r, 1, $"이 캐릭터는 되돌릴 수 없습니다. 영묘에도 가지 않습니다(§3) · {EstateStatusHud.ShortCopper(cost)}");
            if (GameState.Wallet.Copper < cost)
            {
                Locked(r, 2, "소멸시키고 흡수한다",
                       $"골드 {EstateStatusHud.ShortCopper(cost)} 필요 — 지금 {EstateStatusHud.ShortCopper(GameState.Wallet.Copper)}(§18-7)");
            }
            else if (Row(r, 2, "소멸시키고 흡수한다",
                         $"결과는 랜덤 1개 · {EstateStatusHud.ShortCopper(cost)}. 슬롯이 차면 본 뒤에 교체/포기"))
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
            var chrome = UiAtlas.ContentRect(stage, "panel", 2f);
            // 전투력은 오른쪽 정보 패널로 내렸다 — 헤더 둘째 줄(전투력)이 초상 위
            // 「투구」 링 라벨과 같은 좁은 상단 밴드를 다퉈 겹쳤다(폴리싱, 겹침 결함).
            // 제목은 초상 위 좌측에 둔다. 목숨 하트는 정보 칸 Lv 줄 오른쪽으로 내렸다(아래 참조).
            Hint(new Rect(chrome.x, chrome.y, 360f, 24f), $"{title} · {CharHud.JobFace(ch.Job)}");

            var face = UiPages.LargeLook(stage);
            var tint = ch.IsDeleted ? new Color(1f, 1f, 1f, 0.4f) : (Color?)null;
            DrawSelectedLook(face, ch.Job, tint);
            UiAtlas.Draw(new Rect(face.center.x - 16f, face.yMax - 18f, 32f, 32f),
                UiAtlas.RoleKey(ch.Job));

            var center = face.center;
            CharHud.EquipRingFit(stage, face, out float ringX, out float ringY);
            // 라벨이 나가 있는 실제 평평한 내부 — 안쪽 금테 선(stage↔chrome 중간)까지.
            // 정보 칸(infoTop·infoBottom)과 같은 보간선이라 한 선을 맞춘다. 이 선 안으로만
            // 클램프하지 않으면 좌측 라벨(장갑·갑옷)이 장식 여백에 걸리고 신발은 하단
            // 장식 위에 묻힌다(실측 2026-08-24).
            var flat = Rect.MinMaxRect(
                Mathf.Lerp(stage.x, chrome.x, 0.5f), Mathf.Lerp(stage.y, chrome.y, 0.5f),
                Mathf.Lerp(stage.xMax, chrome.xMax, 0.5f), Mathf.Lerp(stage.yMax, chrome.yMax, 0.5f));
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
                }
                var cap = CharHud.EquipLabel(stage, slotRect, flat);
                Hint(cap, CharHud.SlotLabel(slot, worn));
                if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none) && !ch.IsDeleted)
                {
                    if (worn != null) Equipment.TryUnequip(ch, slot);
                    _bagFilter = (int)slot;
                }
            }

            float infoX = Mathf.Max(face.xMax + 36f, stage.x + 300f);
            // 정보 칸은 패널의 **안쪽 얇은 금테 사각형** 안에 가둔다. 「panel」의 ContentRect(chrome)는
            // 9-slice 테두리를 그대로 빼 위·아래로 ≈50px씩 과하게 안쪽을 잡는다(스프라이트 바깥 스크롤워크
            // 장식까지 여백으로 셈) — 그래서 정보 줄이 chrome 높이(≈9줄)를 못 넘겨 아래쪽 장비 슬롯·가방(§11)이
            // 잘렸고, 옛 세션은 이를 피하려 top을 프레임 밖으로 끌어올려(pull-up) 첫 두 줄(Lv·xp)이 탭 옆에
            // 삐져나가는 넘침 결함을 만들었다. 실제 평평한 내부는 안쪽 금테 선(≈stage와 chrome의 중간)까지다 —
            // stage↔chrome를 0.5로 보간해 그 선에 맞추면 넘침 없이 ≈13줄을 담아 장비 전부·가방이 보인다.
            // 꼭대기도 CharHud.InfoTop — 바닥과 같은 실측 선(pad 2/3)에서 4px 아래. 옛 0.62 보간은
            // 선보다 위라 목숨 하트·Lv 줄이 상단 금테에 얹혔다(플레이모드 픽셀 재단 2026-08-24).
            float infoTop = CharHud.InfoTop(stage, chrome);
            // 바닥은 CharHud.InfoBottom — 실측하면 안쪽 금테 선은 pad의 ≈2/3 지점(0.5 flat보다
            // 16px 위)이라 옛 0.45·0.5 어느 쪽이든 마지막 줄이 선에 덮였다(플레이모드 픽셀 재단
            // 2026-08-24). 실제 선(0.667)에서 8px 위로 끊고, 줄 피치를 20→18로 낮춰 14줄+가방을
            // 다 담는다(아래 DrawInspectInfo 참조).
            float infoBottom = CharHud.InfoBottom(stage, chrome);
            var info = new Rect(infoX, infoTop, stage.xMax - infoX - 14f, infoBottom - infoTop);
            // 목숨 하트는 정보 칸 맨 윗줄(Lv·경험)의 오른쪽에 붙여 헤더로 읽힌다 — chrome.xMax 우측 끝에
            // 두던 옛 위치는 정보 칸 중간(무기·없음 줄)에 떠 라벨 없이 겹쳐 보였다(겹침 결함).
            UiAtlas.DrawHearts(new Rect(info.xMax - 76f, infoTop, 76f, 22f),
                ch.DeathCount, ch.IsDeleted, ch.MaxLives);
            DrawInspectInfo(info, ch);

            var bar = new Rect(r.x, r.yMax - 48f, r.width, 44f);
            var actions = UiPages.Grid(bar, 2, 1, 12f);
            if (CompactAction(actions[0], "자동 장착", "sword") && !ch.IsDeleted)
                AutoEquip(ch);
            CompactAction(actions[1],
                $"{EstateStatusHud.ShortCopper(GameState.Wallet.Copper)}  ·  석 {GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone)}",
                "gold", locked: true);
        }

        void DrawInspectInfo(Rect r, CharacterRecord ch)
        {
            float y = r.y;
            // 줄 간격 18f — 정보 칸은 실제 금테 선(pad 2/3, 실측 2026-08-24) 안쪽 ≈268px에
            // 14줄(표제·xp·전투력·상태·전직·편성·장착 헤더·장비 6·가방)을 담는다. 20f였다가
            // 바닥을 실제 선 위 8px로 올리며 14줄+가방이 다 들어오도록 낮췄다(22f→20f 사례와 같은 계열).
            void Line(string text)
            {
                if (y + 16f > r.yMax) return;
                Hint(new Rect(r.x, y, r.width, 18f), text);
                y += 18f;
            }

            Line(ExpText(ch));
            if (!ch.IsDeleted && ch.Level < LifeSystem.MaxLevel)
            {
                float need = LifeSystem.ExpToNext(ch.Level);
                float ratio = need <= 0f ? 0f : Mathf.Clamp01(ch.Exp / (float)need);
                UiAtlas.DrawMeter(new Rect(r.x, y, Mathf.Min(220f, r.width), 16f),
                    "xp_frame", ratio, new Color(0.45f, 0.72f, 1f));
                y += 18f;
            }
            Line($"전투력  {CombatPower(ch):N0}");
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
            if (EquipJob.ShowQa) Line(EquipJob.Line());
            if (EquipLevel.ShowQa)
            {
                Line(EquipLevel.Line());
                string deny = EquipLevel.SeedWhyNot();
                if (!string.IsNullOrEmpty(deny)) Line(deny);
            }
            if (BagSlots.ShowQa) Line(BagSlots.Line());
            if (GearOpt.ShowQa || GearOpt.ShowListQa)
            {
                if (GearOpt.ShowListQa) Line(GearOpt.ListLine());
                if (GearOpt.ShowQa)
                {
                    Line(GearOpt.Line());
                    Line(GearOpt.CombatLine());
                }
                if (!string.IsNullOrEmpty(GearOpt.LastLine)) Line(GearOpt.LastLine);
                var armor = Equipment.Worn(ch, EquipSlot.Armor);
                if (armor != null && GearOpt.CountOf(armor) > 0)
                    Line(GearOpt.CombatLine(armor));
            }
            if (!string.IsNullOrEmpty(_equipMsg)) Line(_equipMsg);

            Line("장착");
            for (int s = 0; s < Equipment.SlotCount; s++)
            {
                var worn = Equipment.Worn(ch, (EquipSlot)s);
                string slot = Equipment.SlotName((EquipSlot)s);
                string opt = worn == null ? "" : GearOpt.Format(worn);
                Line(worn == null
                    ? $"{slot}  ·  없음"
                    : $"{slot}  ·  {Equipment.DisplayName(worn)}"
                      + (worn.Enhance > 0 ? $" +{worn.Enhance}" : "")
                      + (string.IsNullOrEmpty(opt) ? "" : " · " + opt));
            }

            // 4f — 장비 블록과 가방 줄 사이 여백. 6f였다가 바닥을 실제 금테 선(pad 2/3) 위 8px로
            // 올린 뒤에도 가방 줄이 남도록 4f로.
            y += 4f;
            var bag = Equipment.Unequipped();
            int filled = 0;
            for (int i = 0; i < bag.Count; i++)
                if (_bagFilter < 0 || (int)bag[i].Slot == _bagFilter) filled++;
            Line(BagSlots.Line() + (filled != bag.Count ? $" · 이 칸 {filled}" : ""));
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
                {
                    if (!EquipJob.CanWear(ch, bag[i]))
                        _equipMsg = EquipJob.WhyNot(ch, bag[i]);
                    else if (!EquipLevel.CanWear(ch, bag[i]))
                        _equipMsg = EquipLevel.WhyNot(ch, bag[i]);
                    else if (Equipment.TryEquip(ch, bag[i].Id))
                        _equipMsg = "";
                }
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
                    if (!EquipJob.CanWear(ch, bag[i])) continue;
                    if (!EquipLevel.CanWear(ch, bag[i])) continue;
                    Equipment.TryEquip(ch, bag[i].Id);
                    break;
                }
            }
        }
    }
}
