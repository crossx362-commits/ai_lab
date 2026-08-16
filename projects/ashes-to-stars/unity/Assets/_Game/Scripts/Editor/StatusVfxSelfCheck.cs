using UnityEngine;

namespace AshesToStars
{
    /// <summary>상태 아이콘과 독·빙결·보스 경고 스프라이트 시트의 로드·분할을 검사한다.</summary>
    public static class StatusVfxSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(StatusIconAtlas.IsReady, "[StatusVfx] 상태 아이콘 아틀라스 로드 실패");
            foreach (var key in StatusIconAtlas.RequiredKeys)
                Debug.Assert(StatusIconAtlas.RectFor(key).width > 0, $"[StatusVfx] 상태 아이콘 누락: {key}");

            for (int sheet = 0; sheet < StatusVfxSheets.SourceCount; sheet++)
                for (int frame = 0; frame < 8; frame++)
                    Debug.Assert(StatusVfxSheets.Frame(sheet, frame) != null,
                        $"[StatusVfx] 시트 {sheet} 프레임 {frame} 누락");

            Debug.Log("[StatusVfxSelfCheck] PASS");
        }
    }
}
