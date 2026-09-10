using UnityEngine;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **판별 테스트 — 톱니가 지형 LOD에서 오는가**(2026-09-10).
        ///
        /// 여기까지 죽은 가설 셋: ①격자 대조 — 노치가 격자선에 안 붙는다(0.130 대 0점 0.146)
        /// ②하이트맵 절반 굽기 — 셀이 두 배(18.83→37.65px)인데 노치 크기는 그대로(31.6→31.7px)
        /// ③물가 경사 — 노치 자리 27.9°, 물가 전체 26.2°로 같다. 처방 후보 둘(반투명 수면·얕은
        /// 전이 띠)도 화면에서 계단을 **하나도 못 지웠다**.
        ///
        /// 남는 것은 **그리는 쪽**이다. 유니티 지형은 `heightmapPixelError`(현재 **5px**)만큼의
        /// 화면 오차를 허용해 메시를 솎는다 — 그 솎음은 **화면 오차 예산이 정하므로 하이트맵
        /// 해상도를 반으로 줄여도 그대로**다. ②가 안 변한 것이 오히려 이 가설과 맞는다.
        /// 오차를 1px로 조여 같은 자리를 다시 찍는다 — 계단이 잘아지면 범인은 LOD다.
        /// </summary>
        public static void RunShoreLodProbe()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            string outDir = System.IO.Path.Combine(Application.dataPath, "../../builds/qa/shore");
            System.IO.Directory.CreateDirectory(outDir);
            var shots = new[] { "64_river_bend", "63_pier_cutface" };

            var t = Object.FindFirstObjectByType<Terrain>();
            if (t == null) throw new System.InvalidOperationException("지형을 찾지 못했습니다 — 판별이 헛돕니다.");
            float keep = t.heightmapPixelError;
            try
            {
                foreach (float e in new[] { 1f, 20f })
                {
                    t.heightmapPixelError = e;
                    foreach (var sh in shots)
                        ShotTo(sh, System.IO.Path.Combine(outDir, sh + "_lod" + e.ToString("0") + ".png"));
                    Debug.Log("[물가LOD] 화면 오차 " + e + "px — 찍음");
                }
            }
            finally
            {
                t.heightmapPixelError = keep;
                Debug.Log("[물가LOD] 원장값 " + keep + "px로 되돌렸다.");
            }
        }
    }
}
