using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class FieldPlayerCopySelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Field Player Copy Self Check")]
        public static void Run()
        {
            string saved = Environment.GetEnvironmentVariable(FieldScreen.EnvNoPlayerCopy);
            Environment.SetEnvironmentVariable(FieldScreen.EnvNoPlayerCopy, null);

            string cleaned = FieldScreen.PlayerCopy(
                "자동사냥으로 재화를 번다(§2·§6) — 세계 T1 · 시간당 10G · 보유 0G");
            if (cleaned != "자동사냥으로 재화를 번다 — 세계 T1 · 시간당 10G · 보유 0G")
                throw new InvalidOperationException("필드 부제에서 절 번호를 제거하지 못했다: " + cleaned);

            string goldWarn = FieldScreen.PlayerCopy(
                "던전 입장에는 골드가 필요합니다(§18-2)\n필드 사냥으로 먼저 재화를 모으세요(§2)");
            if (goldWarn != "던전 입장에는 골드가 필요합니다\n필드 사냥으로 먼저 재화를 모으세요")
                throw new InvalidOperationException("골드 부족 경고에서 절 번호를 제거하지 못했다: " + goldWarn);

            Environment.SetEnvironmentVariable(FieldScreen.EnvNoPlayerCopy, "1");
            if (FieldScreen.PlayerCopy("재화를 번다(§2·§6)") != "재화를 번다(§2·§6)")
                throw new InvalidOperationException("QA_NO가 옛 절 번호 문구를 복원하지 못했다");
            Environment.SetEnvironmentVariable(FieldScreen.EnvNoPlayerCopy, saved);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime/FieldScreen.cs");
            string source = File.ReadAllText(runtime);
            if (!source.Contains("string rest = PlayerCopy(")
                || !source.Contains("PlayerCopy(\"던전 입장에는 골드가 필요합니다(§18-2)"))
                throw new InvalidOperationException("필드 부제·골드 경고가 PlayerCopy를 모두 소비하지 않는다");

            Debug.Log("[FieldPlayerCopySelfCheck] PASS — 필드 부제·골드 경고 절 번호 제거 + QA_NO 복원");
        }
    }
}
