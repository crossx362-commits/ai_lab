using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play 버튼을 어느 씬에서 눌러도 **타이틀부터** 시작하게 한다.
///
/// 왜 필요한가 (2026-08-13 오너 보고 "플레이 하면 결과만 나오는데"):
///   유니티는 빌드 세팅의 0번 씬이 아니라 **에디터에 열려 있는 씬**을 재생한다.
///   Result 씬을 열어둔 채 Play를 누르면 결과 화면만 뜬다 — 버그가 아니라 유니티의 동작이다.
///   씬을 8개로 나눈 순간 이 혼란은 반드시 생기므로, 진입점을 코드로 고정한다.
///
/// 특정 씬만 따로 보고 싶으면 메뉴에서 끄면 된다.
/// </summary>
[InitializeOnLoad]
public static class PlayFromTitle
{
    const string PREF = "AshesToStars.PlayFromTitle";
    const string MENU = "재와별/Play는 항상 타이틀부터";
    const string TITLE_PATH = "Assets/_Game/Scenes/Play/Title.unity";

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PREF, true);      // 기본 켜짐
        set => EditorPrefs.SetBool(PREF, value);
    }

    static PlayFromTitle()
    {
        EditorApplication.delayCall += Apply;        // 임포트가 끝난 뒤에 붙인다
    }

    [MenuItem(MENU, priority = 20)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
        Debug.Log($"[재와별] Play 시작 씬 고정: {(Enabled ? "타이틀" : "현재 열린 씬")}");
    }

    [MenuItem(MENU, validate = true)]
    static bool ToggleCheck()
    {
        Menu.SetChecked(MENU, Enabled);
        return true;
    }

    static void Apply()
    {
        if (!Enabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TITLE_PATH);
        if (asset == null)
        {
            // 씬이 아직 없으면 조용히 넘어간다 — 생성 전에 경고를 띄우면 소음만 된다
            EditorSceneManager.playModeStartScene = null;
            return;
        }
        EditorSceneManager.playModeStartScene = asset;
    }
}
