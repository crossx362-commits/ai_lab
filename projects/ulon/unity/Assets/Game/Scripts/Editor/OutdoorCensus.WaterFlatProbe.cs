using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **판별 테스트 — 먼 바다의 격자가 수면 무늬에서 오는가.**
        /// 무늬를 셋(사인 4겹·사인 3겹·늘인 잡음)이나 갈아 봤는데 `14_world_vista`의 십자 격자가
        /// 매번 그대로였다. 「내가 고치는 그것이 화면의 그것이 맞나」를 먼저 확인한다.
        /// 2026-09-10 근접(`64`)에서 물이 **사선 체크(직조 천)**로 읽힌다는 판정이 와서 같은 자를
        /// 근접 둘(`64`·`63`)에도 돌린다 — 체크가 남으면 강바닥·지형이고, 사라지면 수면 무늬다.
        /// 수면 겹만 **단색**으로 구워 같은 자리를 찍는다 — 격자가 그대로면 범인은 무늬가 아니다.
        /// </summary>
        public static void RunWaterFlatProbe()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            string outDir = System.IO.Path.Combine(Application.dataPath, "../../builds/qa/water");
            System.IO.Directory.CreateDirectory(outDir);

            string keep = VisualSliceBuilder.FlatLayerForCensus;
            try
            {
                VisualSliceBuilder.FlatLayerForCensus = "SeaWater";
                VisualSliceBuilder.RebuildWater();
                UnityEditor.AssetDatabase.SaveAssets();
                foreach (var sh in new[] { "14_world_vista", "64_river_bend", "63_pier_cutface" })
                    ShotTo(sh, System.IO.Path.Combine(outDir, sh + "_flatwater.png"));
            }
            finally
            {
                VisualSliceBuilder.FlatLayerForCensus = keep;
                VisualSliceBuilder.RebuildWater();
                UnityEditor.AssetDatabase.SaveAssets();
            }
        }

        static void ShotTo(string shotName, string path)
        {
            foreach (var s in QaShots.BuildShots())
            {
                if (QaShots.NameOf(s) != shotName) continue;
                Vector3 eye, look;
                QaShots.EyeOf(s, out eye, out look);
                var go = new GameObject("_flatCam");
                var cam = go.AddComponent<Camera>();
                cam.transform.position = eye;
                cam.transform.LookAt(look);
                cam.farClipPlane = 4000f;
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var img = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                img.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                img.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(path, img.EncodeToPNG());
                Object.DestroyImmediate(img);
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(go);
                Debug.Log("[수면판별] " + shotName + " 단색 수면으로 찍음 → " + path);
                return;
            }
            throw new System.InvalidOperationException("샷 " + shotName + "을 찾지 못했습니다 — 판별이 헛돕니다.");
        }
    }
}
