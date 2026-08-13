using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// W2 조작감 검증 빌드 (§21 프로토타입 V2).
/// W1과 별도 실행 파일로 뽑는다 — 성능 측정판과 조작 체험판은 목적이 달라
/// 한 빌드에 섞으면 둘 다 애매해진다.
/// </summary>
public static class W2Runner
{
    const string SCENE = "Assets/Scenes/W2.unity";

    [MenuItem("재와별/W2 씬 생성")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 9f;               // 조작 체험이므로 W1보다 가깝게
        cam.transform.position = new Vector3(0, 0, -20f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);

        new GameObject("W2Arena", typeof(W2Arena));

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, SCENE);
        Debug.Log("[W2] 씬 생성 완료: " + SCENE);
    }

    [MenuItem("재와별/W2 빌드")]
    public static void Build()
    {
        W1Runner.ReimportTextures();
        if (!File.Exists(SCENE)) CreateScene();

        // 반드시 true — false로 두면 창이 포커스를 잃는 순간 Update가 멈춰
        // 자동 봇 검증이 아예 돌지 않는다 (2026-08-13에 실제로 이걸로 한 번 죽었다)
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

        var outDir = Path.GetFullPath("../build_w2_control");
        Directory.CreateDirectory(outDir);
        var exe = Path.Combine(outDir, "W2.exe");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { SCENE },
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        });

        var s = report.summary;
        Debug.Log($"[W2] 빌드 {s.result} — {s.totalSize / 1048576}MB → {exe}");
        if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
