using System;
using System.IO;
using UnityEditor;

namespace AshesToStars
{
    public static class DungeonAbandonCopySelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Dungeon Abandon Copy Self Check")]
        public static void Run()
        {
            string old = Environment.GetEnvironmentVariable(DungeonAbandonCopy.EnvNo);
            try
            {
                Environment.SetEnvironmentVariable(DungeonAbandonCopy.EnvNo, null);
                Check(DungeonAbandonCopy.Text() == "여기서 나간다 — 임시 강화는 모두 사라진다",
                    "정상 문구는 결과를 구체적으로 설명한다");
                Check(!DungeonAbandonCopy.Text().Contains("§"), "정상 문구에 내부 절 번호가 없다");

                Environment.SetEnvironmentVariable(DungeonAbandonCopy.EnvNo, "1");
                Check(DungeonAbandonCopy.Text().Contains("(§7)"), "QA_NO는 옛 문구를 재현한다");

                string runtime = Path.Combine(UnityEngine.Application.dataPath, "_Game/Scripts/Runtime");
                string screen = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
                Check(screen.Contains("DungeonAbandonCopy.Text()"), "던전 포기 카드가 문구 소비자를 호출한다");
                UnityEngine.Debug.Log("[DungeonAbandonCopySelfCheck] PASS");
            }
            finally
            {
                Environment.SetEnvironmentVariable(DungeonAbandonCopy.EnvNo, old);
            }
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("[DungeonAbandonCopySelfCheck] FAIL: " + message);
            UnityEngine.Debug.Log("[DungeonAbandonCopySelfCheck] PASS " + message);
        }
    }
}
