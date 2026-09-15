// 배치 빌드 — 유니티를 사람이 열지 않고도 "실제로 돌아가는가"를 증명하기 위한 것.
//
//   Unity.exe -batchmode -quit -projectPath <프로젝트> -executeMethod Tankfall.EditorTools.BuildScript.BuildWindows
//
// 빌드된 실행파일에 -autoshot 을 주면 스크린샷을 찍고 스스로 종료한다(PlayDemo.cs).

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tankfall.EditorTools
{
    public static class BuildScript
    {
        const string SceneDir = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/Boot.unity";

        /// <summary>
        /// 빈 부트 씬을 만든다. PlayDemo 가 RuntimeInitializeOnLoadMethod 로 전부 생성하므로
        /// 씬에는 아무것도 없어도 된다 — 빌드 설정이 씬 하나를 요구할 뿐이다(§3-0).
        /// </summary>
        [MenuItem("Tankfall/Create Boot Scene")]
        public static void CreateBootScene()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[Tankfall] 부트 씬 생성: {ScenePath}");
        }

        /// <summary>
        /// ⚠️ 빌드에 셰이더를 명시적으로 넣는다.
        ///
        /// 이 프로젝트는 머티리얼을 전부 코드로 만든다(에셋 없음). 그러면 빌드 수집기가
        /// "아무도 참조하지 않는 셰이더"로 보고 Standard 를 빼버려 런타임에 Shader.Find 가 null 을 돌려주고,
        /// 지형·탱크가 통째로 안 그려진다(실제 빌드에서 발생). 첫 빌드는 우연히 포함돼 넘어갔던 것이라 더 위험했다.
        /// </summary>
        static void EnsureShadersIncluded()
        {
            var names = new[] { "Standard", "Legacy Shaders/Diffuse" };
            var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphics == null || graphics.Length == 0) return;

            var so = new SerializedObject(graphics[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;

            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) { Debug.LogWarning($"[Tankfall] 셰이더 없음: {n}"); continue; }

                bool already = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { already = true; break; }
                if (already) continue;

                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                Debug.Log($"[Tankfall] AlwaysIncludedShaders 에 추가: {n}");
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tankfall/Build Windows")]
        public static void BuildWindows()
        {
            EnsureShadersIncluded();
            if (!File.Exists(ScenePath)) CreateBootScene();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/Tankfall.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[Tankfall] 빌드 {s.result} · {s.totalSize / 1024 / 1024} MB · {s.totalTime.TotalSeconds:F0}s · 에러 {s.totalErrors}");
            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
