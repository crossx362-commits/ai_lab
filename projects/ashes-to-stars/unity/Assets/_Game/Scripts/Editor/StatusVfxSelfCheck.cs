using UnityEngine;

namespace AshesToStars
{
    /// <summary>상태 아이콘과 독·빙결·보스 경고 스프라이트 시트의 로드·분할을 검사한다.</summary>
    public static class StatusVfxSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(StatusIconAtlas.IsReady, "[StatusVfx] 상태 아이콘 아틀라스 로드 실패");
            Debug.Assert(StatusVfxSheets.SourceCount == 7, "[StatusVfx] 상태·보스 기믹 시트 7종이 등록돼야 한다");
            foreach (var key in StatusIconAtlas.RequiredKeys)
                Debug.Assert(StatusIconAtlas.RectFor(key).width > 0, $"[StatusVfx] 상태 아이콘 누락: {key}");
            var live = StatusIconAtlas.LiveKeys(true, true, true, true);
            Debug.Assert(live.Count == 3 && live[0] == "shield" && live[1] == "taunt" && live[2] == "attack_up",
                "[StatusVfx] 켜진 상태만 아이콘으로 접혀야 한다");
            Debug.Assert(StatusIconAtlas.LiveKeys(false, false, false, false).Count == 0,
                "[StatusVfx] 꺼진 상태를 그리면 안 된다");
            Debug.Assert(StatusIconAtlas.LiveKeys(false, false, false, true).Count == 1
                         && StatusIconAtlas.LiveKeys(false, false, false, true)[0] == "shield",
                "[StatusVfx] 최후의 보루는 방패 아이콘");

            for (int sheet = 0; sheet < StatusVfxSheets.SourceCount; sheet++)
                for (int frame = 0; frame < 8; frame++)
                    Debug.Assert(StatusVfxSheets.Frame(sheet, frame) != null,
                        $"[StatusVfx] 시트 {sheet} 프레임 {frame} 누락");

            Debug.Log("[StatusVfxSelfCheck] PASS");
        }
    }
}
