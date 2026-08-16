using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>이펙트 시험장은 열자마자 재생 상태여야 검은 빈 화면으로 오인되지 않는다.</summary>
    public static class VfxTestScreenSelfCheck
    {
        public static void Run()
        {
            var host = new GameObject("VfxTestScreenSelfCheck");
            var screen = host.AddComponent<VfxTestScreen>();
            Debug.Assert(screen.IsAutoPlaying,
                "[VfxTestScreen] 테스트 씬 진입 직후 자동 재생이 꺼져 있어 이펙트가 보이지 않는다");
            Object.DestroyImmediate(host);
            Debug.Log("[VfxTestScreenSelfCheck] PASS");
        }
    }
}
