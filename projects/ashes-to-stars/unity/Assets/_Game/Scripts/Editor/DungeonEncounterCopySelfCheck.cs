using System;
using System.IO;
using UnityEditor;

namespace AshesToStars
{
    public static class DungeonEncounterCopySelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Dungeon Encounter Copy Self Check")]
        public static void Run()
        {
            string old = Environment.GetEnvironmentVariable(DungeonEncounterCopy.EnvNo);
            try
            {
                var node = new DungeonNode
                {
                    Template = ArenaTemplate.choke,
                    Wave = new WavePlan { TargetCount = 105, RangedPercent = 20f }
                };

                Environment.SetEnvironmentVariable(DungeonEncounterCopy.EnvNo, null);
                string current = DungeonEncounterCopy.Text(node);
                Check(current == "최대 105마리 · 원거리 적 20% · 좁은 길", "플레이어 문구");
                Check(!current.Contains("동시") && !current.Contains("병목"), "구현 용어 비노출");

                Environment.SetEnvironmentVariable(DungeonEncounterCopy.EnvNo, "1");
                Check(DungeonEncounterCopy.Text(node) == "동시 105체 · 원거리 20% · 병목",
                    "QA_NO는 옛 문구를 재현한다");

                string runtime = Path.Combine(UnityEngine.Application.dataPath, "_Game/Scripts/Runtime");
                string screen = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
                Check(screen.Contains("DungeonEncounterCopy.Text(n)"), "던전 카드가 문구 소비자를 호출한다");
                UnityEngine.Debug.Log("[DungeonEncounterCopySelfCheck] PASS");
            }
            finally
            {
                Environment.SetEnvironmentVariable(DungeonEncounterCopy.EnvNo, old);
            }
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("[DungeonEncounterCopySelfCheck] FAIL: " + message);
            UnityEngine.Debug.Log("[DungeonEncounterCopySelfCheck] PASS " + message);
        }
    }
}
