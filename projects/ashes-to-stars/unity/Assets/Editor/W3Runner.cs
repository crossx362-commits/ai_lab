using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>W3 파티·자동 전투 검증 빌드 (§21).</summary>
public static class W3Runner
{
    const string SCENE = "Assets/Scenes/W3.unity";

    [MenuItem("재와별/W3 씬 생성")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 8.5f;
        cam.transform.position = new Vector3(0, 0, -20f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);

        new GameObject("W3Party", typeof(W3Party));

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, SCENE);
        Debug.Log("[W3] 씬 생성 완료: " + SCENE);
    }

    [MenuItem("재와별/W3 빌드")]
    public static void Build()
    {
        W1Runner.ReimportTextures();
        if (!File.Exists(SCENE)) CreateScene();

        PlayerSettings.runInBackground = true;   // 자동 검증이 포커스 없이도 돌게 (W2 사고 재발 방지)
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

        var outDir = Path.GetFullPath("../빌드_W3파티");
        Directory.CreateDirectory(outDir);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { SCENE },
            locationPathName = Path.Combine(outDir, "W3.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        });

        Debug.Log($"[W3] 빌드 {report.summary.result}");
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
