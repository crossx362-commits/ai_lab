using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// W1 성능 검증 자동화 (§21).
/// Unity를 배치모드로 띄워 씬을 만들고, 스탠드얼론 플레이어를 빌드한다.
/// 실제 측정은 빌드된 플레이어를 실행해서 한다 — 배치모드 -nographics는
/// 렌더링을 하지 않으므로 "60fps 나오는가"를 물을 수 없다.
/// </summary>
public static class W1Runner
{
    const string SCENE = "Assets/Scenes/W1.unity";

    [MenuItem("재와별/W1 씬 생성")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 12f;                 // 화면에 실제로 수백 마리가 보이게
        cam.transform.position = new Vector3(0, 0, -20f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
        cam.allowHDR = false;
        cam.allowMSAA = false;

        new GameObject("StressTest", typeof(StressTest));

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, SCENE);
        Debug.Log("[W1] 씬 생성 완료: " + SCENE);
    }

    [MenuItem("재와별/텍스처 재임포트")]
    public static void ReimportTextures()
    {
        // TextureImportRules를 반드시 태우기 위해 강제 재임포트
        AssetDatabase.ImportAsset("Assets/Resources",
            ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        // 검증: 실제로 읽기 가능해졌는지 확인 (설정했다고 믿지 말고 확인)
        int bad = 0, total = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            total++;
            if (tex != null && !tex.isReadable) { bad++; Debug.LogError("[W1] 읽기 불가: " + path); }
        }
        Debug.Log($"[W1] 텍스처 재임포트 — {total}장 중 읽기 불가 {bad}장");
        if (bad > 0) EditorApplication.Exit(2);
    }

    [MenuItem("재와별/W1 빌드")]
    public static void Build()
    {
        ReimportTextures();
        if (!File.Exists(SCENE)) CreateScene();

        // 측정에 방해되는 요소 제거
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        QualitySettings.vSyncCount = 0;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        PlayerSettings.gcIncremental = true;

        var outDir = Path.GetFullPath("../빌드_W1성능");
        Directory.CreateDirectory(outDir);
        var exe = Path.Combine(outDir, "W1.exe");

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { SCENE },
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log($"[W1] 빌드 {s.result} — {s.totalSize / 1048576}MB, {s.totalTime.TotalSeconds:F0}s → {exe}");
        if (s.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
