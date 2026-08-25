using System;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class DungeonEmptyHudSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Dungeon Empty Hud Self Check")]
        public static void Run()
        {
            var body = new Rect(36f, 56f, 1208f, 628f);
            string saved = Environment.GetEnvironmentVariable("QA_NO_DUNGEON_EMPTY_CENTER");
            Environment.SetEnvironmentVariable("QA_NO_DUNGEON_EMPTY_CENTER", null);
            Rect centered = DungeonScreen.EmptyCardRect(body);
            if (Mathf.Abs(centered.center.y - (body.center.y + 24f)) > 0.01f)
                throw new InvalidOperationException("빈 상태 카드가 본문 중심에 있지 않다");

            Environment.SetEnvironmentVariable("QA_NO_DUNGEON_EMPTY_CENTER", "1");
            Rect old = DungeonScreen.EmptyCardRect(body);
            if (!Mathf.Approximately(old.y, body.y + 88f) || centered.y <= old.y + 100f)
                throw new InvalidOperationException("QA_NO가 옛 상단 쏠림을 복원하지 못한다");
            Environment.SetEnvironmentVariable("QA_NO_DUNGEON_EMPTY_CENTER", saved);
            Debug.Log($"[DungeonEmptyHudSelfCheck] PASS — 중앙 y {centered.y:0}, 옛 y {old.y:0}");
        }
    }
}
