using System;
using UnityEditor;
using UnityEngine;

namespace AshesToStars.Editor
{
    public static class EscapeHintHudSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Escape Hint HUD Self Check")]
        public static void Run()
        {
            string old = Environment.GetEnvironmentVariable(GameScreen.EnvNoEscapeSafeMargin);
            try
            {
                Environment.SetEnvironmentVariable(GameScreen.EnvNoEscapeSafeMargin, null);
                Rect safe = GameScreen.EscapeHintRect();
                Check(Mathf.Approximately(safe.yMax, 708f), $"안전 힌트 바닥 {safe.yMax:0} = 708");
                Check(Mathf.Approximately(720f - safe.yMax, 12f), "화면 바닥 안전 여백 12");

                Environment.SetEnvironmentVariable(GameScreen.EnvNoEscapeSafeMargin, "1");
                Rect blocked = GameScreen.EscapeHintRect();
                Check(Mathf.Approximately(blocked.yMax, 714f), $"네거티브 바닥 {blocked.yMax:0} = 714");
                Check(blocked.y > safe.y, "네거티브는 옛 바닥 밀착 좌표");
                Debug.Log("[EscapeHintHudSelfCheck] PASS");
            }
            finally
            {
                Environment.SetEnvironmentVariable(GameScreen.EnvNoEscapeSafeMargin, old);
            }
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception("[EscapeHintHudSelfCheck] FAIL: " + message);
            Debug.Log("[EscapeHintHudSelfCheck] " + message);
        }
    }
}
