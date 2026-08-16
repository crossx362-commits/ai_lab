using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>파티 초상화 시트가 읽히고, HUD에서 쓰는 초상화 영역이 원본을 벗어나지 않는지 검사한다.</summary>
    public static class PortraitAtlasSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(PortraitAtlas.IsReady,
                "[PortraitAtlasSelfCheck] 파티 초상화 시트를 Resources/ui에서 읽지 못했다");

            foreach (var key in PortraitAtlas.RequiredKeys)
            {
                var rect = PortraitAtlas.RectFor(key);
                Debug.Assert(rect.width > 0 && rect.height > 0,
                    $"[PortraitAtlasSelfCheck] {key}: 빈 영역");
                Debug.Assert(rect.xMin >= 0 && rect.yMin >= 0 &&
                             rect.xMax <= PortraitAtlas.Width && rect.yMax <= PortraitAtlas.Height,
                    $"[PortraitAtlasSelfCheck] {key}: 아틀라스 밖 영역 {rect}");
            }

            // 전투 스프라이트와 초상화가 어긋나면 HUD만 다른 사람이 된다.
            Debug.Assert(PortraitAtlas.KeyForJob("검사") == "rogue",
                "[PortraitAtlasSelfCheck] 검사는 후드 도적 칸이어야 한다");
            Debug.Assert(PortraitAtlas.KeyForJob("딜") == "rogue",
                "[PortraitAtlasSelfCheck] 기본 딜도 같은 칸");
            Debug.Assert(PortraitAtlas.KeyForJob("마딜") == "fire_mage",
                "[PortraitAtlasSelfCheck] 기본 마딜은 마법사 칸");
            Debug.Assert(PortraitAtlas.KeyForJob("광전사") == "dwarf_guardian",
                "[PortraitAtlasSelfCheck] 광전사는 망치 드워프 칸");
            Debug.Assert(PortraitAtlas.KeyForJob("궁수") == "ranger",
                "[PortraitAtlasSelfCheck] 궁수는 활 칸");
            Debug.Assert(PortraitAtlas.KeyForJob("드루이드") == "druid",
                "[PortraitAtlasSelfCheck] 드루이드는 뿔 칸");
            string[] jobs =
            {
                "탱", "딜", "마딜", "힐", "버퍼",
                "수호기사", "광전사", "검사", "궁수", "마법사", "소환사",
                "사제", "드루이드", "음유시인", "주술사", "정령사",
            };
            foreach (var job in jobs)
            {
                var key = PortraitAtlas.KeyForJob(job);
                Debug.Assert(System.Array.IndexOf(PortraitAtlas.RequiredKeys, key) >= 0,
                    $"[PortraitAtlasSelfCheck] {job} → {key} 가 RequiredKeys 밖");
            }

            Debug.Log("[PortraitAtlasSelfCheck] PASS");
        }
    }
}
