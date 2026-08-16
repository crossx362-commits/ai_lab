using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class JobVfxSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(JobVfxSheets.IsReady, "[JobVfxSelfCheck] 직업 이펙트 시트를 읽지 못했다");
            Debug.Assert(JobVfxSheets.FrameCount == 8, "[JobVfxSelfCheck] 8프레임 시트가 아니다");
            Debug.Assert(JobVfxSheets.SourceCount == 4, "[JobVfxSelfCheck] 사제 치유 시트가 등록되지 않았다");
            Debug.Log("[JobVfxSelfCheck] PASS");
        }
    }
}
