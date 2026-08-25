using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class ResultPlayerCopySelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Result Player Copy Self Check")]
        public static void Run()
        {
            string saved = Environment.GetEnvironmentVariable(ResultScreen.EnvNoPlayerCopy);
            Environment.SetEnvironmentVariable(ResultScreen.EnvNoPlayerCopy, null);

            string cleaned = ResultScreen.PlayerCopy(
                "100층 최초 클리어(§8) · 재가 되어 영묘에 기록됩니다(§16-6) · 허브 복귀(§16)");
            if (cleaned != "100층 최초 클리어 · 재가 되어 영묘에 기록됩니다 · 허브 복귀")
                throw new InvalidOperationException("결과 화면 문구에서 절 번호를 제거하지 못했다: " + cleaned);

            string item = ResultScreen.PlayerCopy("부활초 — 사망 카운트 1 차감 (§4)");
            if (item != "부활초 — 사망 카운트 1 차감 ")
                throw new InvalidOperationException("아이템 설명에서 절 번호를 제거하지 못했다: " + item);

            Environment.SetEnvironmentVariable(ResultScreen.EnvNoPlayerCopy, "1");
            if (ResultScreen.PlayerCopy("허브 복귀(§16)") != "허브 복귀(§16)")
                throw new InvalidOperationException("QA_NO가 옛 절 번호 문구를 복원하지 못했다");
            Environment.SetEnvironmentVariable(ResultScreen.EnvNoPlayerCopy, saved);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime/ResultScreen.cs");
            string source = File.ReadAllText(runtime);
            if (!source.Contains("PlayerCopy($\"{TowerEnding.TitleName} — 100층 최초 클리어(§8)\")")
                || !source.Contains("PlayerCopy($\"[삭제] {string.Join(\", \", defeat.DeletedNames)}")
                || !source.Contains("PlayerCopy(\"허브 복귀(§16)\")")
                || !source.Contains("return PlayerCopy(item switch"))
                throw new InvalidOperationException("결과 화면·아이템 설명이 PlayerCopy를 모두 소비하지 않는다");

            Debug.Log("[ResultPlayerCopySelfCheck] PASS — 결과 화면·아이템 설명 절 번호 제거 + QA_NO 복원");
        }
    }
}
