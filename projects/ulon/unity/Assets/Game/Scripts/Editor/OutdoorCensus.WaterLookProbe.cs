using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **새 수면을 눈으로 보기 위한 네 장** — 물 재작업(깊이 색 + 물가 거품) 중에 쓴다.
        /// 전체 QA 한 판이 16~20분이라 물만 고치는 동안 그걸 돌리면 하루가 간다.
        /// `14`(먼 바다) · `15`(호수와 강) · `63`(부두, 수직면) · `64`(모래 둑, 완경사) —
        /// 마지막 둘이 **거품 문턱을 아래 면 각도로 달리하는** 축의 두 끝이다.
        /// </summary>
        public static void RunWaterLook()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            VisualSliceBuilder.RebuildWater();
            UnityEditor.AssetDatabase.SaveAssets();

            string dir = System.IO.Path.Combine(Application.dataPath, "../../builds/qa/water");
            System.IO.Directory.CreateDirectory(dir);
            foreach (string shot in new[] { "14_world_vista", "15_lake_river", "63_pier_cutface", "64_river_bend" })
            {
                ShotTo(shot, System.IO.Path.Combine(dir, shot + "_look.png"));
                // 눈으로 볼 판과 **같은 판의 수**를 같이 찍는다 — 그림과 숫자가 다른 렌더에서
                // 나오면 둘을 맞대 볼 수 없다(1.6단계에서 그 함정을 한 번 밟았다).
                if (shot != "14_world_vista")
                {
                    FoamAreaShares(shot, out _, out _, out _, out string det);
                    Debug.Log("[물눈] " + shot + " 거품 면적 — " + det);
                }
            }

            // **실루엣 페이드의 네거티브 컨트롤**(1.7단계, 검수 조건 — 새 자를 만들지 않고 화면으로
            // 판정하되 NC 한 판은 남긴다). `_FoamMinBandPx = 0`이면 죽임이 꺼진다 — 그 판에서
            // `64` 좌안의 **1~2px 순백 선이 다시 나타나야** 이 처방이 실제로 그것을 지운 것이다.
            var wm = FindWaterMaterial();
            if (wm != null && wm.HasProperty("_FoamMinBandPx"))
            {
                float keepPx = wm.GetFloat("_FoamMinBandPx");
                try
                {
                    wm.SetFloat("_FoamMinBandPx", 0f);
                    ShotTo("64_river_bend", System.IO.Path.Combine(dir, "64_river_bend_nc_thinkill.png"));
                    Debug.Log("[물눈] NC — 얇은 띠 죽임을 끈 판을 64_river_bend_nc_thinkill.png에 남겼다");
                }
                finally { wm.SetFloat("_FoamMinBandPx", keepPx); }
            }

            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
