using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class BattlePlayerCopySelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Battle Player Copy Self Check")]
        public static void Run()
        {
            string saved = Environment.GetEnvironmentVariable(BattleScreen.EnvNoPlayerCopy);
            Environment.SetEnvironmentVariable(BattleScreen.EnvNoPlayerCopy, null);

            string cleaned = BattleScreen.PlayerCopy(
                "필드 배회 보스 — 준비 없이 만나면 위험. 환생석 없음(§10-1·§10-8) · 기믹 3종 — 동시 장판 · 쫄 소환 · 힐 체크. 수동 지휘로 대응한다(§5·§10-5)");
            if (cleaned != "필드 배회 보스 — 준비 없이 만나면 위험. 환생석 없음 · 기믹 3종 — 동시 장판 · 쫄 소환 · 힐 체크. 수동 지휘로 대응한다")
                throw new InvalidOperationException("전투 화면 문구에서 절 번호를 제거하지 못했다: " + cleaned);

            string summary = BattleScreen.PlayerCopy("필드 배회 보스 격파 — 재(§10-1)");
            if (summary != "필드 배회 보스 격파 — 재")
                throw new InvalidOperationException("전투 요약에서 절 번호를 제거하지 못했다: " + summary);

            string hint = BattleScreen.PlayerCopy("저체력 귀환 5.0초 — 피격 가능 · 이번 판 보상 없음(§4)");
            if (hint != "저체력 귀환 5.0초 — 피격 가능 · 이번 판 보상 없음")
                throw new InvalidOperationException("저체력 귀환 힌트에서 절 번호를 제거하지 못했다: " + hint);

            Environment.SetEnvironmentVariable(BattleScreen.EnvNoPlayerCopy, "1");
            if (BattleScreen.PlayerCopy("저체력 귀환 — 이번 판 보상 없음(§4)") != "저체력 귀환 — 이번 판 보상 없음(§4)")
                throw new InvalidOperationException("QA_NO가 옛 절 번호 문구를 복원하지 못했다");
            Environment.SetEnvironmentVariable(BattleScreen.EnvNoPlayerCopy, saved);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime/BattleScreen.cs");
            string source = File.ReadAllText(runtime);
            if (!source.Contains("protected override string Subtitle => PlayerCopy(FieldBoss.Fighting")
                || !source.Contains("GameFlow.LastBattleSummary = PlayerCopy(FieldBoss.Fighting")
                || !source.Contains("PlayerCopy($\"저체력 귀환 {LowHpReturn.Remaining:0.0}초")
                || !source.Contains("GameFlow.LastBattleSummary = PlayerCopy(\"저체력 귀환 — 이번 판 보상 없음(§4)\")"))
                throw new InvalidOperationException("전투 화면이 PlayerCopy를 모두 소비하지 않는다");

            if (!source.Contains("ShortCopper(loot)")
                || source.Contains("FormatCurrency(loot)"))
                throw new InvalidOperationException("침략 성공 약탈이 ShortCopper가 아니다");
            if (!source.Contains("FormatCurrency(_reward.GoldReward)"))
                throw new InvalidOperationException("생존 사냥 골드 FormatCurrency가 빠졌다");

            Debug.Log("[BattlePlayerCopySelfCheck] PASS — 전투 화면 절 번호 제거 + QA_NO 복원");
        }
    }
}
