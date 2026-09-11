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
            // 판정하되 NC 한 판은 남긴다). `_FoamWobbleM = 0`이면 죽임이 꺼진다 — 그 판에서
            // `64` 좌안의 **1~2px 순백 선이 다시 나타나야** 이 처방이 실제로 그것을 지운 것이다.
            var wm = FindWaterMaterial();
            if (wm != null && wm.HasProperty("_FoamWobbleM"))
            {
                float keepWob = wm.GetFloat("_FoamWobbleM");
                try
                {
                    wm.SetFloat("_FoamWobbleM", 0f);
                    // `15`도 같이 찍는다 — NC 판이 곧 이 랩의 **「전」**이다(죽임이 꺼진 상태가
                    // 1.7이 커밋한 화면이다). 호수 띠가 그대로 살아 있는지 나란히 본다.
                    foreach (string nc in new[] { "64_river_bend", "15_lake_river" })
                    {
                        ShotTo(nc, System.IO.Path.Combine(dir, nc + "_nc_thinkill.png"));
                        FoamAreaShares(nc, out _, out _, out _, out string ncDet);
                        Debug.Log("[물눈] NC 죽임끔 " + nc + " 거품 면적 — " + ncDet);
                    }
                }
                finally { wm.SetFloat("_FoamWobbleM", keepWob); }
            }

            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }
    }
}
