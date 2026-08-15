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
