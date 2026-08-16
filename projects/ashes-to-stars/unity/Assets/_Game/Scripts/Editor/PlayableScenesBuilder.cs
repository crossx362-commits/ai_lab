using System.Collections.Generic;
using System.IO;
using AshesToStars;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이 가능한 화면 씬 8개를 만들고 **빌드 세팅에 등록**한다.
/// 메뉴: 재와별 ▸ 플레이 씬 생성
///
/// 왜 씬이 이렇게 가벼운가:
///   예전에 화면마다 오브젝트 트리를 채운 씬을 11개 만들다 배치모드가 죽었다(§21 기록).
///   여기서는 씬에 **카메라와 화면 스크립트 하나만** 넣고 내용은 런타임 IMGUI로 그린다.
///   저장할 것이 거의 없으니 배치에서도 안정적이다.
///
/// ⚠️ 빌드 세팅 등록을 빼먹으면 SceneManager.LoadScene이 전부 실패한다 —
///    "버튼은 눌리는데 화면이 안 바뀐다"로 보여 진단이 오래 걸리는 종류의 실수다.
/// </summary>
public static class PlayableScenesBuilder
{
    const string DIR = "Assets/_Game/Scenes/Play";

    // 씬 이름 → 붙일 화면 스크립트. GameFlow.All과 짝이 맞아야 한다.
    static readonly (string scene, System.Type screen)[] MAP =
    {
        (GameFlow.Title,     typeof(TitleScreen)),
        (GameFlow.Estate,    typeof(EstateScreen)),
        (GameFlow.Field,     typeof(FieldScreen)),
        (GameFlow.Tower,     typeof(TowerScreen)),
        (GameFlow.WorldMap,  typeof(WorldMapScreen)),
        (GameFlow.Character, typeof(CharacterScreen)),
        (GameFlow.Party,     typeof(PartyScreen)),
        (GameFlow.Style,     typeof(StyleScreen)),
        (GameFlow.Dungeon,   typeof(DungeonScreen)),
        (GameFlow.Battle,    typeof(BattleScreen)),
        (GameFlow.Result,    typeof(ResultScreen)),
        (GameFlow.VfxTest,   typeof(VfxTestScreen)),
    };

    [MenuItem("재와별/플레이 씬 생성", priority = 2)]
    public static void Build()
    {
        // 플레이 중에는 EditorSceneManager.NewScene이 예외를 던진다(유니티 제약).
        // 메뉴는 눌리는데 콘솔에만 빨간 줄이 뜨므로, 무엇을 해야 하는지 먼저 알린다.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("재와 별",
                "플레이 중에는 씬을 만들 수 없다.\n\nPlay를 멈춘 뒤 다시 실행할 것.", "확인");
            return;
        }

        if (!Directory.Exists(DIR)) Directory.CreateDirectory(DIR);

        var paths = new List<string>();
        foreach (var (name, screen) in MAP)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera));
            cam.tag = "MainCamera";
            var c = cam.GetComponent<Camera>();
            c.orthographic = true;
            c.orthographicSize = 8f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
            cam.transform.position = new Vector3(0, 0, -10);

            new GameObject(name + "Screen", screen);

            string path = $"{DIR}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            paths.Add(path);
            Debug.Log($"[플레이씬] {path}");
        }

        RegisterBuildSettings(paths);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[플레이씬] 완료 — {paths.Count}개 생성·등록. 시작 씬은 {GameFlow.All[0]}");
    }

    /// <summary>기존 검증 씬 등록을 지우지 않고, 플레이 씬을 앞쪽에 놓는다.</summary>
    static void RegisterBuildSettings(List<string> playPaths)
    {
        var list = new List<EditorBuildSettingsScene>();
        foreach (var p in playPaths) list.Add(new EditorBuildSettingsScene(p, true));

        foreach (var s in EditorBuildSettings.scenes)
        {
            bool dup = false;
            foreach (var p in playPaths) if (p == s.path) { dup = true; break; }
            if (!dup) list.Add(s);
        }
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>
    /// 배치모드 진입점 — 씬 생성 + 스탠드얼론 빌드까지.
    /// -executeMethod PlayableScenesBuilder.BuildGame
    /// </summary>
    public static void BuildGame()
    {
        Build();

        var scenes = new List<string>();
        foreach (var n in GameFlow.All) scenes.Add($"{DIR}/{n}.unity");

        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build_game"));
        Directory.CreateDirectory(outDir);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = BuildPlatform.OutputPath(outDir, "AshesToStars"),
            target = BuildPlatform.Target,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"[플레이빌드] {report.summary.result} — {report.summary.totalSize} bytes → {outDir}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
