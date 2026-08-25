using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class TitlePlayerCopySelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Title Player Copy Self Check")]
        public static void Run()
        {
            string saved = Environment.GetEnvironmentVariable(TitleScreen.EnvNoPlayerCopy);
            Environment.SetEnvironmentVariable(TitleScreen.EnvNoPlayerCopy, null);

            string tower = TitleScreen.PlayerCopy("정상 정복자 · 전투력은 그대로 · 100층을 다시 오를 수 있다(§8)");
            if (tower != "정상 정복자 · 전투력은 그대로 · 100층을 다시 오를 수 있다")
                throw new InvalidOperationException("정상 정복 문구에서 절 번호를 제거하지 못했다: " + tower);

            string solo = TitleScreen.PlayerCopy("고독한 정복자 · 홀로 깬 레이드 3 · 전투력은 그대로(§8)");
            if (solo != "고독한 정복자 · 홀로 깬 레이드 3 · 전투력은 그대로")
                throw new InvalidOperationException("홀로 레이드 문구에서 절 번호를 제거하지 못했다: " + solo);

            Environment.SetEnvironmentVariable(TitleScreen.EnvNoPlayerCopy, "1");
            if (TitleScreen.PlayerCopy("전투력은 그대로(§8)") != "전투력은 그대로(§8)")
                throw new InvalidOperationException("QA_NO가 옛 절 번호 문구를 복원하지 못했다");
            Environment.SetEnvironmentVariable(TitleScreen.EnvNoPlayerCopy, saved);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime/TitleScreen.cs");
            string source = File.ReadAllText(runtime);
            if (!source.Contains("PlayerCopy($\"{TowerEnding.LookName} · 전투력은 그대로 · 100층을 다시 오를 수 있다(§8)\")")
                || !source.Contains("PlayerCopy($\"{SoloRaidClear.LookName} · 홀로 깬 레이드 {SoloRaidClear.Count} · 전투력은 그대로(§8)\")"))
                throw new InvalidOperationException("타이틀 화면 칭호 문구가 PlayerCopy를 모두 소비하지 않는다");

            Debug.Log("[TitlePlayerCopySelfCheck] PASS — 타이틀 화면 칭호 문구 절 번호 제거 + QA_NO 복원");
        }
    }
}
