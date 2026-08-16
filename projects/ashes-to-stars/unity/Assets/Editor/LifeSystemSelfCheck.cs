using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 목숨·지갑 시스템 자가검사 (배치모드 실행용).
    ///
    ///   Unity -batchmode -quit -projectPath <프로젝트> -executeMethod AshesToStars.LifeSystemSelfCheck.Run
    ///
    /// 왜 필요한가: 목숨 시스템은 §4 이 게임의 정체성인데 **화면을 눌러보는 것 말고는
    /// 확인할 방법이 없었다.** 그래서 `LifeSystem.Initialize()`가 아무 데서도 호출되지 않아
    /// 로스터가 항상 비어 있던 것을 아무도 못 잡았고, 인수인계에는 "목숨 시스템 완료"로 적혔다.
    /// W1~W3 하네스는 Assets/Scripts(검증 전용)만 돌려서 이쪽을 전혀 건드리지 않는다.
    ///
    /// 검사 항목은 전부 "계산이 되는가"가 아니라 **"다음 판에서 읽히는가"**를 본다
    /// (인수인계 §5 「계산은 되는데 반영이 안 됨」).
    /// </summary>
    public static class LifeSystemSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            GameState.ResetAll();
            LifeSystem.ResetAll();

            // ① 로스터가 저절로 선다 — Initialize를 아무도 안 불러도
            var roster = LifeSystem.GetCharacters();
            Check(roster.Count == 5, $"로스터 자동 생성 (기대 5, 실제 {roster.Count})");
            Check(roster[0].Job == "탱" && roster[1].Job == "딜" && roster[2].Job == "마딜"
                  && roster[3].Job == "힐" && roster[4].Job == "버퍼",
                  "신규 로스터는 기본직업 5종(탱·딜·마딜·힐·버퍼)으로 시작(오너 21:38)");
            Check(roster.TrueForAll(c => c.Advancement == AdvancementTier.Basic),
                  "신규 로스터 전원은 미전직(Basic) 단계");

            // 기본직업은 아직 전용 전투 Job enum이 없다. 프로토타입 전투는
            // 기존 1차 아키타입으로 어댑트해 파티가 0명이 되는 회귀를 막는다.
            PartyState.ResetForTest();
            var sortieJobs = PartyState.SortieJobs();
            Check(sortieJobs.Count == 5 && sortieJobs[0] == "수호기사"
                  && sortieJobs[1] == "검사" && sortieJobs[2] == "마법사"
                  && sortieJobs[3] == "사제" && sortieJobs[4] == "음유시인",
                  "기본직업 5인이 프로토타입 전투 Job으로 모두 어댑트됨");
            var basicCombatants = PartyState.SortieCombatants();
            Check(basicCombatants.Count == 5
                  && basicCombatants.TrueForAll(c => c.Advancement == AdvancementTier.Basic)
                  && basicCombatants.TrueForAll(c => c.SkillCount == 2),
                  "기본직업 전투원은 단계와 기본 스킬 2개를 전투 경계에 전달(§3)");

            LifeSystem.ForgetInMemoryForTest();
            roster = LifeSystem.GetCharacters();
            Check(roster.TrueForAll(c => c.Advancement == AdvancementTier.Basic),
                  "재기동 후에도 기본직업 전직 단계 유지");

            // 예전 7필드 저장에는 전직 단계가 없다. 기존 직업명은 1차 완료로 보존한다.
            PlayerPrefs.SetString("ats.roster", "예전탱크\t수호기사\t10\t0\t0\t0\t25\n");
            PlayerPrefs.Save();
            LifeSystem.ForgetInMemoryForTest();
            var legacy = LifeSystem.GetCharacters();
            Check(legacy.Count == 1 && legacy[0].Job == "수호기사"
                  && legacy[0].Advancement == AdvancementTier.First && legacy[0].Exp == 25,
                  "기존 1차 직업 7필드 저장을 1차 완료로 하위호환");

            // ①-B Lv20 1차 전직 — 역할에 맞는 선택지만 허용하고 결과가 다음 판에도 남는다.
            GameState.ResetAll();
            LifeSystem.ResetAll();
            var candidate = LifeSystem.GetCharacters()[0]; // 기본직업 탱
            Check(LifeSystem.FirstAdvancementOptions(candidate).Count == 2
                  && LifeSystem.FirstAdvancementOptions(candidate)[0] == "수호기사"
                  && LifeSystem.FirstAdvancementOptions(candidate)[1] == "광전사",
                  "탱 1차 선택지는 수호기사·광전사 2종(§3)");
            var dpsOptions = LifeSystem.FirstAdvancementOptions(LifeSystem.GetCharacters()[1]);
            Check(dpsOptions.Count == 2 && dpsOptions[0] == "검사" && dpsOptions[1] == "궁수",
                  "딜 1차 선택지 2종(검사·궁수)");
            var mageOptions = LifeSystem.FirstAdvancementOptions(LifeSystem.GetCharacters()[2]);
            Check(mageOptions.Count == 2 && mageOptions[0] == "마법사" && mageOptions[1] == "소환사",
                  "마딜 1차 선택지 2종(마법사·소환사)");
            var healOptions = LifeSystem.FirstAdvancementOptions(LifeSystem.GetCharacters()[3]);
            Check(healOptions.Count == 2 && healOptions[0] == "사제" && healOptions[1] == "드루이드",
                  "힐 1차 선택지 2종(사제·드루이드)");
            var bufferOptions = LifeSystem.FirstAdvancementOptions(LifeSystem.GetCharacters()[4]);
            Check(bufferOptions.Count == 3 && bufferOptions[0] == "음유시인"
                  && bufferOptions[1] == "주술사" && bufferOptions[2] == "정령사",
                  "버퍼 1차 선택지 3종(음유시인·주술사·정령사)");
            candidate.Level = 19;
            Check(!LifeSystem.TryBeginFirstAdvancementTrial(candidate, "수호기사")
                  && candidate.Job == "탱" && candidate.Advancement == AdvancementTier.Basic,
                  "Lv19는 1차 전직 불가 — 캐릭터 상태 불변");
            candidate.Level = 20;
            Check(!LifeSystem.TryBeginFirstAdvancementTrial(candidate, "마법사")
                  && candidate.Job == "탱" && candidate.Advancement == AdvancementTier.Basic,
                  "기본직업과 다른 계열의 1차 직업 선택 거부");
            var outsider = new CharacterRecord("외부", "탱", 20);
            Check(!LifeSystem.TryBeginFirstAdvancementTrial(outsider, "수호기사"),
                  "로스터에 없는 캐릭터는 전직 불가");
            candidate.IsDeleted = true;
            Check(LifeSystem.FirstAdvancementOptions(candidate).Count == 0
                  && !LifeSystem.TryBeginFirstAdvancementTrial(candidate, "수호기사"),
                  "삭제된 캐릭터는 선택지 없음·전직 불가");
            candidate.IsDeleted = false;
            Check(!LifeSystem.TryBeginFirstAdvancementTrial(candidate, "광전사"),
                  "전직 재료가 없으면 시험 시작 불가");
            Check(GameState.Gain(Economy.LifeItem.AdvancementMaterial, 10), "전직 재료 10개 획득");
            Check(LifeSystem.TryBeginFirstAdvancementTrial(candidate, "광전사"), "재료 보유 시 비살상 시험 시작");
            int trialPattern = LifeSystem.ActiveFirstTrial.Pattern;
            int livesBeforeTrial = candidate.DeathCount;
            int materialsBeforeTrial = GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial);
            Check(!LifeSystem.ConfirmFirstAdvancementTrial()
                  && GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) == materialsBeforeTrial
                  && candidate.Job == "탱" && candidate.DeathCount == livesBeforeTrial,
                  "역할 목표 미달은 전직·재료·목숨 상태 불변");
            LifeSystem.CancelFirstAdvancementTrial();
            Check(GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) == materialsBeforeTrial
                  && candidate.DeathCount == livesBeforeTrial,
                  "시험 중단은 재료 0소비·사망 카운트 0증가");
            Check(LifeSystem.TryBeginFirstAdvancementTrial(candidate, "광전사")
                  && LifeSystem.ActiveFirstTrial.Pattern == trialPattern,
                  "같은 캐릭터 재입장 시 시험 패턴 고정(리롤 불가)");
            string stableId = candidate.Id;
            var requiredAction = LifeSystem.ActiveFirstTrial.RequiredAction;
            var wrongAction = requiredAction == FirstTrialAction.Guard ? FirstTrialAction.Mark : FirstTrialAction.Guard;
            Check(!LifeSystem.ReportFirstTrialProgress(wrongAction)
                  && LifeSystem.ActiveFirstTrial.Progress == 0,
                  "현재 패턴이 요구하지 않은 역할 행동은 진행도에 반영하지 않음");
            LifeSystem.CancelFirstAdvancementTrial();
            Check(GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) == materialsBeforeTrial,
                  "잘못된 역할 행동으로 시험 실패해도 재료 0소비");
            Check(LifeSystem.TryBeginFirstAdvancementTrial(candidate, "광전사"), "역할 시험 재도전 횟수 제한 없음");
            while (!LifeSystem.ActiveFirstTrial.ObjectiveMet)
                LifeSystem.ReportFirstTrialProgress(LifeSystem.ActiveFirstTrial.RequiredAction);
            GameState.FailNextAtomicStageForTest();
            bool atomicFailureCaught = false;
            try { LifeSystem.ConfirmFirstAdvancementTrial(); }
            catch (InvalidOperationException) { atomicFailureCaught = true; }
            LifeSystem.ForgetInMemoryForTest();
            GameState.ForgetInMemoryForTest();
            candidate = LifeSystem.GetCharacters()[0];
            Check(atomicFailureCaught && candidate.Job == "탱" && candidate.Advancement == AdvancementTier.Basic
                  && GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) == materialsBeforeTrial,
                  "원자 저장 실패 주입 후 재기동: 직업·재료 모두 원상복구");
            Check(LifeSystem.TryBeginFirstAdvancementTrial(candidate, "광전사"), "저장 실패 뒤 시험 재시작 가능");
            while (!LifeSystem.ActiveFirstTrial.ObjectiveMet)
                LifeSystem.ReportFirstTrialProgress(LifeSystem.ActiveFirstTrial.RequiredAction);
            Check(LifeSystem.ConfirmFirstAdvancementTrial()
                  && candidate.Job == "광전사" && candidate.Advancement == AdvancementTier.First
                  && GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) == materialsBeforeTrial - 5
                  && candidate.DeathCount == livesBeforeTrial,
                  "시험 성공 확인 때만 재료 5개 소비·1차 전직·목숨 불변");
            PartyState.ResetForTest();
            var advancedCombatants = PartyState.SortieCombatants();
            Check(advancedCombatants.Count > 0
                  && advancedCombatants[0].Job == "광전사"
                  && advancedCombatants[0].Advancement == AdvancementTier.First
                  && advancedCombatants[0].SkillCount == 4
                  && !advancedCombatants[0].HasUltimate,
                  "1차 전직 결과가 전투 경계에서 스킬 4개로 확장(§3)");
            candidate.Advancement = AdvancementTier.Second;
            PartyState.ResetForTest();
            var secondCombatants = PartyState.SortieCombatants();
            Check(secondCombatants.Count > 0
                  && secondCombatants[0].SkillCount == 4
                  && secondCombatants[0].HasUltimate
                  && secondCombatants[0].CommandCount == 5,
                  "2차 각성 결과가 전투 경계에서 4스킬+초필 1개로 확장(§3)");
            Check(global::W3Party.CanUseUltimate(AdvancementTier.Second, 100f, 0f)
                  && !global::W3Party.CanUseUltimate(AdvancementTier.First, 100f, 0f)
                  && !global::W3Party.CanUseUltimate(AdvancementTier.Second, 99f, 0f)
                  && !global::W3Party.CanUseUltimate(AdvancementTier.Second, 100f, 0.1f),
                  "초필살기는 2차·게이지100%·쿨다운0 종료를 모두 요구(§18-6)");
            Check(global::W3Party.SupportsAdvancementJob("광전사")
                  && global::W3Party.SupportsAdvancementJob("수호기사")
                  && !global::W3Party.SupportsAdvancementJob("미지원직업"),
                  "2차 QA는 실전 아키타입 11종만 허용");
            Check(global::W3Party.UltimateProbePassed(0, 1f)
                  && !global::W3Party.UltimateProbePassed(0, 0f)
                  && global::W3Party.UltimateProbePassed(1, 0f)
                  && global::W3Party.UltimateProbePassed(2, 0f)
                  && global::W3Party.UltimateProbePassed(3, 0f)
                  && !global::W3Party.UltimateProbePassed(1, 1f),
                  "초필 QA는 정상=효과>0, 차단=효과0을 PASS로 판정");
            candidate.Advancement = AdvancementTier.First;
            PartyState.ResetForTest();
            Check(!LifeSystem.TryBeginFirstAdvancementTrial(candidate, "수호기사")
                  && candidate.Job == "광전사" && candidate.Advancement == AdvancementTier.First,
                  "이미 1차 전직한 캐릭터는 반복 전직 불가");
            LifeSystem.ForgetInMemoryForTest();
            candidate = LifeSystem.GetCharacters()[0];
            Check(candidate.Job == "광전사" && candidate.Advancement == AdvancementTier.First
                  && candidate.Id == stableId,
                  "재기동 후에도 1차 전직 결과·영속 캐릭터 ID 유지");

            // ①-C Lv50 2차 각성 — 1차 직업을 바꾸지 않고 재료20+시험 성공 때만 단계가 오른다.
            candidate.Level = 49;
            Check(!LifeSystem.TryBeginSecondAdvancementTrial(candidate)
                  && candidate.Job == "광전사" && candidate.Advancement == AdvancementTier.First,
                  "Lv49는 2차 각성 불가 — 직업·단계 불변");
            candidate.Level = 50;
            Check(!LifeSystem.TryBeginSecondAdvancementTrial(candidate), "재료 20개 미만이면 2차 시험 시작 불가");
            Check(GameState.Gain(Economy.LifeItem.AdvancementMaterial, 20), "2차 각성 재료 20개 추가 획득");
            int secondMaterials = GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial);
            Check(LifeSystem.TryBeginSecondAdvancementTrial(candidate), "Lv50·1차·재료20이면 2차 비살상 시험 시작");
            Check(LifeSystem.ActiveSecondTrial.TargetJob == "광전사", "2차는 분기 없이 같은 1차 직업을 유지");
            LifeSystem.CancelSecondAdvancementTrial();
            Check(GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) == secondMaterials
                  && candidate.Advancement == AdvancementTier.First,
                  "2차 시험 중단은 재료 0소비·단계 불변");
            Check(LifeSystem.TryBeginSecondAdvancementTrial(candidate), "2차 시험 재도전 가능");
            while (!LifeSystem.ActiveSecondTrial.ObjectiveMet)
                LifeSystem.ReportSecondTrialProgress(LifeSystem.ActiveSecondTrial.RequiredAction);
            GameState.FailNextAtomicStageForTest();
            atomicFailureCaught = false;
            try { LifeSystem.ConfirmSecondAdvancementTrial(); }
            catch (InvalidOperationException) { atomicFailureCaught = true; }
            LifeSystem.ForgetInMemoryForTest();
            GameState.ForgetInMemoryForTest();
            candidate = LifeSystem.GetCharacters()[0];
            Check(atomicFailureCaught && candidate.Job == "광전사"
                  && candidate.Advancement == AdvancementTier.First
                  && GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) == secondMaterials,
                  "2차 원자 저장 실패 후 재기동: 같은 직업·1차 단계·재료 모두 원상복구");
            Check(LifeSystem.TryBeginSecondAdvancementTrial(candidate), "2차 저장 실패 뒤 시험 재시작 가능");
            while (!LifeSystem.ActiveSecondTrial.ObjectiveMet)
                LifeSystem.ReportSecondTrialProgress(LifeSystem.ActiveSecondTrial.RequiredAction);
            Check(LifeSystem.ConfirmSecondAdvancementTrial()
                  && candidate.Job == "광전사" && candidate.Advancement == AdvancementTier.Second
                  && GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) == secondMaterials - 20,
                  "2차 시험 성공 확인 때만 재료20 소비·같은 직업 각성(§3·§18-6)");
            Check(!LifeSystem.TryBeginSecondAdvancementTrial(candidate), "이미 2차인 캐릭터는 반복 각성 불가");
            LifeSystem.ForgetInMemoryForTest();
            candidate = LifeSystem.GetCharacters()[0];
            Check(candidate.Job == "광전사" && candidate.Advancement == AdvancementTier.Second,
                  "재기동 후에도 같은 직업의 2차 각성 단계 유지");

            // W3가 지원하던 5직업 외 6종도 검사 폴백이 아니라 자기 전투 계약을 가져야 한다.
            string[] distinctJobs = { "광전사", "궁수", "소환사", "드루이드", "주술사", "정령사" };
            string[] expectedRoles = { "Tank", "Dps", "Dps", "Healer", "Buffer", "Buffer" };
            float[] expectedRanges = { 1.8f, 8.0f, 7.0f, 6.0f, 6.0f, 6.5f };
            var mechanicIds = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < distinctJobs.Length; i++)
            {
                string job = distinctJobs[i];
                string[] labels = W3Party.FirstAdvancementSkillLabels(job);
                string mechanic = W3Party.FirstAdvancementMechanic(job);
                Check(labels != null && labels.Length == 2 && labels[0] != "—" && labels[1] != "—",
                      $"{job} 1차 고유 스킬 2종이 전투 UI 계약에 존재");
                Check(!string.IsNullOrEmpty(mechanic) && mechanic != "swordsman_fallback",
                      $"{job}가 검사 폴백이 아닌 고유 전투 메커니즘을 소비");
                Check(W3Party.FirstAdvancementRole(job) == expectedRoles[i]
                      && System.Math.Abs(W3Party.FirstAdvancementRange(job) - expectedRanges[i]) < 0.001f,
                      $"{job} 고유 역할·사거리 계약 ({expectedRoles[i]}, {expectedRanges[i]:F1})");
                string[] probeMetrics = W3Party.FirstAdvancementProbeMetricNames(job);
                Check(probeMetrics.Length == 2
                      && !string.IsNullOrEmpty(probeMetrics[0])
                      && !string.IsNullOrEmpty(probeMetrics[1])
                      && probeMetrics[0] != probeMetrics[1],
                      $"{job} 슬롯1/2 실전 효과를 서로 다른 수치로 계측");
                mechanicIds.Add(mechanic);
            }
            Check(mechanicIds.Count == distinctJobs.Length,
                  $"미지원 1차 6종 메커니즘이 서로 구분됨 (기대 6, 실제 {mechanicIds.Count})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            roster = LifeSystem.GetCharacters();

            // ② 부활초 단일 소스 — LifeSystem과 소지품이 같은 숫자를 본다
            Check(LifeSystem.GetRevivePotions() == GameState.Bag.GetCount(Economy.LifeItem.RevivalTea),
                  $"부활초 단일 소스 (LifeSystem {LifeSystem.GetRevivePotions()} / Bag {GameState.Bag.GetCount(Economy.LifeItem.RevivalTea)})");
            Check(LifeSystem.GetRevivePotions() == 3, "초기 부활초 3개(§18-4 상한과 같음)");

            // ③ 사망이 기록된다 — 그리고 마지막 목숨(2회)이 감지된다
            var a = roster[0];
            LifeSystem.RegisterDeath(a);
            LifeSystem.RegisterDeath(a);
            Check(a.DeathCount == 2, $"사망 2회 기록 (실제 {a.DeathCount})");
            Check(!LifeSystem.IsAvailable(a), "사망 직후 회복 중이라 출전 불가(§4)");

            // ④ 부활초 사용이 소지품에서 실제로 줄어든다
            int before = LifeSystem.GetRevivePotions();
            bool used = LifeSystem.UseRevivePotion(a);
            Check(used && a.DeathCount == 1, $"부활초로 사망 카운트 차감 (실제 {a.DeathCount})");
            Check(LifeSystem.GetRevivePotions() == before - 1,
                  $"부활초 소지품 차감 ({before} → {LifeSystem.GetRevivePotions()})");

            // ⑤ 3회 사망 = 삭제
            LifeSystem.RegisterDeath(a);
            LifeSystem.RegisterDeath(a);
            Check(a.IsDeleted && a.DeathCount == 3, $"3회 사망 삭제 (삭제={a.IsDeleted}, 카운트={a.DeathCount})");

            // ⑥ **다시 켜도 남는다** — 껐다 켜면 사라지는 영구사망은 영구사망이 아니다
            LifeSystem.ForgetInMemoryForTest();
            var reloaded = LifeSystem.GetCharacters();
            Check(reloaded.Count == 5, $"재기동 후 로스터 복원 (기대 5, 실제 {reloaded.Count})");
            Check(reloaded[0].IsDeleted && reloaded[0].DeathCount == 3,
                  "재기동 후에도 삭제 상태 유지(§4 영구사망)");

            // ⑦ 지갑도 같은 규칙 — 벌면 쌓이고 내면 줄고, 모자라면 **차감하지 않는다**
            GameState.Earn(1000);
            Check(GameState.Wallet.Copper == 1000, $"보상 누적 (실제 {GameState.Wallet.Copper})");
            Check(!GameState.Pay(5000) && GameState.Wallet.Copper == 1000,
                  "잔액 부족 시 부분 차감 없음(§18-2)");
            Check(GameState.Pay(400) && GameState.Wallet.Copper == 600,
                  $"진입 비용 차감 (실제 {GameState.Wallet.Copper})");

            // ⑧ 성장(§3 경험치 분배 · §18-6 곡선) — 계산이 아니라 **다음 판에서 읽히는가**를 본다
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            // 곡선: §18-6 100×Lv^2.2, 단조 증가
            Check(LifeSystem.ExpToNext(1) == 100, $"Lv1→2 필요 경험치 100 (§18-6, 실제 {LifeSystem.ExpToNext(1)})");
            Check(LifeSystem.ExpToNext(2) > LifeSystem.ExpToNext(1), "필요 경험치 단조 증가(뒤 레벨이 더 비싸다)");

            // 임계 도달 시 딱 한 레벨 오르고 잔여 경험치 0
            var g = LifeSystem.GetCharacters()[0];
            int lv0 = g.Level;
            long need = LifeSystem.ExpToNext(g.Level);
            int up = LifeSystem.AddExp(g, need);
            Check(up == 1 && g.Level == lv0 + 1, $"경험치 임계 도달 → 레벨업 (Lv {lv0}→{g.Level})");
            Check(g.Exp == 0, $"레벨업 후 잔여 경험치 0 (실제 {g.Exp})");

            // 레벨 상한 100 — 넘게 부어도 안 오른다
            var h = LifeSystem.GetCharacters()[1];
            h.Level = LifeSystem.MaxLevel;
            Check(LifeSystem.AddExp(h, 999999) == 0 && h.Level == LifeSystem.MaxLevel,
                  $"레벨 상한 {LifeSystem.MaxLevel}(§18-6) — 만렙은 더 안 오른다");

            // 분배: 출전 파티(자동편성 5인, 전부 Lv1)에 총합 보존해서 나눈다(§3)
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            var lines = LifeSystem.AwardBattleExp(100);
            long sumExp = 0;
            foreach (var c in LifeSystem.GetCharacters()) sumExp += c.Exp;
            Check(sumExp == 100, $"경험치 총합 보존 (기대 100, 실제 {sumExp})");
            Check(lines.Count == PartyState.Slots.Count && lines.Count > 0,
                  $"출전 인원수만큼 분배 (편성 {PartyState.Slots.Count} / 분배 {lines.Count})");

            // 재기동 후에도 경험치가 남는다 — 계산만 되고 저장 안 되면 성장이 사라진다
            LifeSystem.ForgetInMemoryForTest();
            long reloadedExp = 0;
            foreach (var c in LifeSystem.GetCharacters()) reloadedExp += c.Exp;
            Check(reloadedExp == 100, $"재기동 후 경험치 유지 (기대 100, 실제 {reloadedExp})");

            // ⑨ 긴급 탈출(§4) — 귀환의 두루마리는 **소모처가 있어야** 한다.
            //    BattleScreen "후퇴"가 이 불변식에 의존한다: 없으면 잠기고, 있으면 1개 줄며,
            //    다 쓰면 다시 잠긴다. 예전엔 이 아이템이 드랍만 되고 소모처가 0곳이라 공짜 탈출이었다.
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Check(GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) == 0,
                  $"초기 귀환의 두루마리 0개 — 긴급 탈출은 희소(§4, 실제 {GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn)})");
            Check(!GameState.Consume(Economy.LifeItem.ScrollOfReturn),
                  "두루마리 0개일 때 소모 실패 → 후퇴 잠김(공짜 탈출 아님)");
            GameState.Gain(Economy.LifeItem.ScrollOfReturn, 1);
            Check(GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) == 1, "두루마리 획득 반영");
            Check(GameState.Consume(Economy.LifeItem.ScrollOfReturn)
                  && GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) == 0,
                  "후퇴 시 두루마리 1개 실제 차감(§4)");
            Check(!GameState.Consume(Economy.LifeItem.ScrollOfReturn), "다 쓰면 다시 잠김");

            // ⑩ 대출(§12·§18-5) — "골드가 없을 때 빚을 내고, 수입의 50%가 자동 상환된다".
            //    TowerScreen "대출받고 입장"과 GameState.Earn 자동상환이 이 불변식에 의존한다.
            //    계산이 아니라 **다음 판에서 읽히는가**(지갑에 실제로 들어오고 저장되는가)를 본다.
            GameState.ResetAll();
            LifeSystem.ResetAll();
            long now = 1000000L;   // 이자를 결정론적으로 검증하려고 시각을 주입한다

            // 무자산 대출 방지(§18-5): 잔고 0 → 한도 0 → 못 빌린다
            Check(GameState.LoanLimit == 0 && !GameState.Borrow(100, now),
                  $"무자산이면 대출 불가(§18-5, 한도 {GameState.LoanLimit})");

            // 한도 = min(순자산 30%, 20G/h·티어). 보유 10골드(=100000쿠퍼), T1 → 30% 캡이 지배.
            GameState.Earn(100000);
            long expectLimit = Economy.LoanLimitCopper(100000, GameState.Tier);   // 30000
            Check(GameState.LoanLimit == expectLimit && expectLimit == 30000,
                  $"대출 한도 = 순자산 30% (기대 30000, 실제 {GameState.LoanLimit})");

            // 한도까지는 되고, 1쿠퍼라도 넘으면 거부(부분 대출 없음)
            Check(GameState.Borrow(30000, now) && GameState.Wallet.Copper == 130000 && GameState.Debt == 30000,
                  $"대출 시 지갑↑·부채↑ (지갑 {GameState.Wallet.Copper}, 부채 {GameState.Debt})");
            Check(!GameState.Borrow(1, now) && GameState.Debt == 30000,
                  "한도 초과 대출 거부(부분 대출 없음)");

            // 수입 50% 자동 상환(§18-5) — 이것이 대출 상태의 상시 소비처
            GameState.Earn(1000);
            Check(GameState.Debt == 29500 && GameState.Wallet.Copper == 130500,
                  $"수입 50% 자동 상환 (부채 {GameState.Debt}, 지갑 {GameState.Wallet.Copper})");

            // 이자 복리(§18-5 0.5%/h) — 72시간 뒤 잔액이 Economy 계산과 정확히 일치하고 늘어난다
            long beforeAccrual = GameState.Debt;
            long expectAccrued = Economy.AccrueLoan(beforeAccrual, 72);
            GameState.AccrueLoan(now + 72 * 3600);
            Check(GameState.Debt == expectAccrued && GameState.Debt > beforeAccrual,
                  $"72h 이자 가산 (기대 {expectAccrued}, 실제 {GameState.Debt}, 이전 {beforeAccrual})");
            Check(Economy.AccrueLoan(20000, 72) > 27200,
                  "복리가 단리(72×0.5%=36% → 27200)를 초과");

            // 재기동 후에도 부채가 남는다 — 저장 안 되면 빚이 사라진다
            GameState.ForgetInMemoryForTest();
            Check(GameState.Debt == expectAccrued,
                  $"재기동 후 부채 유지 (기대 {expectAccrued}, 실제 {GameState.Debt})");

            // 수동 상환 — 지갑에서 갚은 만큼 부채가 준다. 다 갚으면 0.
            long paid = GameState.Repay(GameState.Debt, now + 72 * 3600);
            Check(paid > 0 && GameState.Debt == 0,
                  $"수동 상환 후 부채 0 (갚음 {paid}, 부채 {GameState.Debt})");

            // ⑪ §8 탑 등반 — "다음 층 도전"(잡몹웨이브)을 버티면 한 층 오른다.
            //   도입 이래 ClearFloor가 보스 격파에서만 불려 일반 층 돌파가 진행도에 반영되지 않았다.
            GameState.SetTowerFloorForTest(1);
            Check(GameState.TowerFloor == 1, $"탑 층 베이스라인 1 (실제 {GameState.TowerFloor})");
            // 판정: 탑에서 온 잡몹웨이브를 살아남으면 층 돌파 = 참
            Check(GameFlow.IsTowerFloorClear(true, false, GameFlow.Tower, GameFlow.BattleKind.잡몹웨이브),
                  "탑 잡몹웨이브 생존 → 층 돌파 판정 참(§8)");
            // 이중 상승 방지: 보스전은 OnBossDefeated가 따로 올리므로 여기선 거짓
            Check(!GameFlow.IsTowerFloorClear(true, false, GameFlow.Tower, GameFlow.BattleKind.보스),
                  "탑 보스전은 이 경로에서 거짓(OnBossDefeated가 올림 — 이중 상승 방지)");
            // 필드 사냥·전멸·던전은 탑 층을 올리지 않는다
            Check(!GameFlow.IsTowerFloorClear(true, false, GameFlow.Field, GameFlow.BattleKind.잡몹웨이브),
                  "필드 사냥 생존은 탑 층을 안 올린다");
            Check(!GameFlow.IsTowerFloorClear(false, false, GameFlow.Tower, GameFlow.BattleKind.잡몹웨이브),
                  "전멸은 층을 안 올린다");
            Check(!GameFlow.IsTowerFloorClear(true, true, GameFlow.Tower, GameFlow.BattleKind.잡몹웨이브),
                  "던전 런은 탑 층을 안 올린다(노드 맵으로 돌아감)");
            // 실제 돌파: ClearFloor가 층을 하나 올리고, 재도전은 최고 기록을 안 내린다(단조 증가)
            GameState.ClearFloor(1);
            Check(GameState.TowerFloor == 2, $"1층 돌파 → 2층 (실제 {GameState.TowerFloor})");
            GameState.ClearFloor(1);
            Check(GameState.TowerFloor == 2, $"이미 지난 층 재도전은 진행도 유지 (실제 {GameState.TowerFloor})");
            // 재기동 후에도 돌파한 층이 남는다(저장)
            GameState.ForgetInMemoryForTest();
            Check(GameState.TowerFloor == 2, $"재기동 후 돌파 층 유지 (실제 {GameState.TowerFloor})");

            // ⑫ V4 삭제 루프 준비 — 보스/전멸이 출전 파티에만 사망을 남기고,
            //    3회 삭제 뒤 생존 0명이면 긴급 재건 1명으로 계속 플레이한다.
            //    예전 OnBattleEnd는 로스터 전원에게 RegisterDeath를 뿌렸고,
            //    OnPartyWiped(힐체크 실패)는 결과 화면만 열고 목숨을 안 깎았다.
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            var wipeRoster = LifeSystem.GetCharacters();
            Check(wipeRoster.Count == 5 && LifeSystem.LivingCount() == 5,
                  $"V4 베이스라인 로스터 5·생존 5 (실제 {wipeRoster.Count}/{LifeSystem.LivingCount()})");
            _ = PartyState.Slots;
            PartyState.Toggle(2);
            PartyState.Toggle(3);
            PartyState.Toggle(4);
            Check(PartyState.Slots.Count == 2, $"출전 2명만 남김 (실제 {PartyState.Slots.Count})");

            var firstWipe = GameFlow.ApplyPveDefeat();
            Check(firstWipe.FallenNames.Count == 2 && firstWipe.DeletedNames.Count == 0,
                  $"1회 패배: 출전 2명만 쓰러짐 (사망 {firstWipe.FallenNames.Count}, 삭제 {firstWipe.DeletedNames.Count})");
            Check(wipeRoster[0].DeathCount == 1 && wipeRoster[1].DeathCount == 1,
                  $"출전 사망 카운트 1 (실제 {wipeRoster[0].DeathCount}/{wipeRoster[1].DeathCount})");
            Check(wipeRoster[2].DeathCount == 0 && wipeRoster[3].DeathCount == 0 && wipeRoster[4].DeathCount == 0,
                  "벤치 3명은 목숨이 그대로다 — 로스터 전원이 죽으면 안 된다");
            Check(firstWipe.LivingCount == 5 && !firstWipe.RescueGranted,
                  $"회복 중은 생존으로 센다 — 긴급 재건 없음 (생존 {firstWipe.LivingCount}, 재건 {firstWipe.RescueGranted})");

            var pvpWipe = LifeSystem.ApplyWipe(new[] { wipeRoster[2] }, isPvp: true);
            Check(pvpWipe.FallenNames.Count == 0 && wipeRoster[2].DeathCount == 0,
                  "PvP 패배는 사망 카운트를 안 올린다(§4)");

            var secondWipe = LifeSystem.ApplyWipe(new[] { wipeRoster[0], wipeRoster[1] });
            var thirdWipe = LifeSystem.ApplyWipe(new[] { wipeRoster[0], wipeRoster[1] });
            Check(wipeRoster[0].IsDeleted && wipeRoster[1].IsDeleted && thirdWipe.DeletedNames.Count == 2,
                  $"출전 2명 3회 사망 = 삭제 (삭제목록 {thirdWipe.DeletedNames.Count})");
            Check(LifeSystem.LivingCount() == 3 && !thirdWipe.RescueGranted,
                  $"벤치 3명이 남아 재건은 안 나간다 (생존 {LifeSystem.LivingCount()})");

            LifeSystem.ApplyWipe(new[] { wipeRoster[2], wipeRoster[3], wipeRoster[4] });
            LifeSystem.ApplyWipe(new[] { wipeRoster[2], wipeRoster[3], wipeRoster[4] });
            var lastWipe = LifeSystem.ApplyWipe(new[] { wipeRoster[2], wipeRoster[3], wipeRoster[4] });
            Check(lastWipe.RescueGranted && lastWipe.LivingCount == 1 && !string.IsNullOrEmpty(lastWipe.RescueName),
                  $"전원 삭제 뒤 긴급 재건 1명 (재건={lastWipe.RescueGranted}, 생존={lastWipe.LivingCount}, 이름={lastWipe.RescueName})");
            var rescue = LifeSystem.ActiveRescue();
            Check(rescue != null && rescue.IsRescue && rescue.Level == 1 && rescue.Advancement == AdvancementTier.Basic
                  && (rescue.Job == "탱" || rescue.Job == "딜" || rescue.Job == "마딜"
                      || rescue.Job == "힐" || rescue.Job == "버퍼")
                  && !rescue.IsDeleted,
                  $"재건은 기본직업 Lv1·무장비 (job={rescue?.Job}, lv={rescue?.Level}, rescue={rescue?.IsRescue})");

            var extra = LifeSystem.EnsureEmergencyRecruit();
            Check(extra == rescue && LifeSystem.LivingCount() == 1,
                  "생존 재건이 있으면 두 번째 무료 영입이 나가지 않는다");

            LifeSystem.ForgetInMemoryForTest();
            PartyState.ResetForTest();
            var reloadedWipe = LifeSystem.GetCharacters();
            Check(reloadedWipe.Exists(c => c.IsDeleted) && reloadedWipe.Exists(c => c.IsRescue && !c.IsDeleted),
                  "재기동 후에도 삭제와 긴급 재건이 남는다");
            Check(LifeSystem.LivingCount() == 1, $"재기동 후 생존 1 (실제 {LifeSystem.LivingCount()})");

            // 뒷정리 — 검사가 실제 저장을 남기면 다음 플레이가 오염된다
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            string head = _fail == 0 ? "[자가검사] PASS" : $"[자가검사] FAIL {_fail}건";
            Debug.Log(head + "\n" + _log);
            if (_fail > 0)
            {
                // 배치모드에서 실패를 종료 코드로 알린다 — 로그만 남기면 자동화가 못 읽는다
                EditorApplication.Exit(1);
            }
        }
    }
}
