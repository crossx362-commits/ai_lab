using UnityEditor;
using UnityEngine;

namespace AshesToStars.Editor
{
    public static class CompactInfoSelfCheck
    {
        [MenuItem("AshesToStars/QA/Compact Info Self Check")]
        public static void Run()
        {
            var panel = new Rect(560f, 180f, 690f, 40f);
            string old = System.Environment.GetEnvironmentVariable(GameScreen.EnvNoCompactInfoFit);
            try
            {
                System.Environment.SetEnvironmentVariable(GameScreen.EnvNoCompactInfoFit, null);
                Rect fit = GameScreen.CompactInfoTextRect(panel);
                Check(Mathf.Approximately(fit.y, 186f), $"위 여백 6px (실제 {fit.y - panel.y})");
                Check(Mathf.Approximately(fit.height, 28f), $"한글 획용 28px (실제 {fit.height})");
                Check(fit.yMax <= panel.yMax - 6f, "아랫 여백 6px");

                System.Environment.SetEnvironmentVariable(GameScreen.EnvNoCompactInfoFit, "1");
                Rect blocked = GameScreen.CompactInfoTextRect(panel);
                Check(Mathf.Approximately(blocked.height, 20f), "네거티브는 옛 20px 칸");
                Check(fit.height > blocked.height, "수정 칸이 네거티브보다 높다");
                Debug.Log("[CompactInfoSelfCheck] PASS");
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(GameScreen.EnvNoCompactInfoFit, old);
            }
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new System.Exception("[CompactInfoSelfCheck] FAIL: " + message);
            Debug.Log("[CompactInfoSelfCheck] OK: " + message);
        }
    }
}
