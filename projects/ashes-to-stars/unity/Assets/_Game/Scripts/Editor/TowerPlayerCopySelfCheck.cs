using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class TowerPlayerCopySelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Tower Player Copy Self Check")]
        public static void Run()
        {
            string saved = Environment.GetEnvironmentVariable(TowerScreen.EnvNoPlayerCopy);
            Environment.SetEnvironmentVariable(TowerScreen.EnvNoPlayerCopy, null);

            string cleaned = TowerScreen.PlayerCopy(
                "3번 죽으면 장비가 사라진다(§4) · 5층마다 보스(§9) · 탑 규칙(§8·§10-6)");
            if (cleaned != "3번 죽으면 장비가 사라진다 · 5층마다 보스 · 탑 규칙")
                throw new InvalidOperationException("탑 플레이어 문구에서 절 번호를 제거하지 못했다: " + cleaned);

            Environment.SetEnvironmentVariable(TowerScreen.EnvNoPlayerCopy, "1");
            if (TowerScreen.PlayerCopy("목숨이 깎인다(§4)") != "목숨이 깎인다(§4)")
                throw new InvalidOperationException("QA_NO가 옛 절 번호 문구를 복원하지 못했다");
            Environment.SetEnvironmentVariable(TowerScreen.EnvNoPlayerCopy, saved);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime/TowerScreen.cs");
            string source = File.ReadAllText(runtime);
            if (!source.Contains("PlayerCopy(TowerHubCap.Compose")
                || !source.Contains("PlayerCopy(DeathTraining.ConsentBody())")
                || !source.Contains("PlayerCopy(\"벽 콘텐츠 — 재도전 리듬(§8)\")"))
                throw new InvalidOperationException("탑 헤더·경고·카드가 PlayerCopy를 모두 소비하지 않는다");

            Debug.Log("[TowerPlayerCopySelfCheck] PASS — 탑 헤더·경고·카드 절 번호 제거 + QA_NO 복원");
        }
    }
}
