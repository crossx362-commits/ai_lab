using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity 6000.3 전용 내부 API 어댑터. 창 생성/Show/Focus/사용자 입력 조작 없음.
public static class OffscreenCheck
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Type ViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
    static readonly MethodInfo Render = typeof(EditorGUIUtility).GetMethod("RenderPlayModeViewCamerasInternal", BindingFlags.Static | BindingFlags.NonPublic);
    static readonly FieldInfo RenderingView = ViewType.GetField("s_RenderingView", BindingFlags.Static | BindingFlags.NonPublic);
    static ScriptableObject view, host;
    static object previousView;
    static RenderTexture target;
    static Camera camera;
    static Color[] withText;
    static double stageStarted, nextRender;
    static string output;
    static int stage, frames, startRepaints;
    static double deadline;
    static readonly Vector2Int[] Sizes = { new Vector2Int(1440, 900), new Vector2Int(1024, 768), new Vector2Int(1024, 768), new Vector2Int(1024, 768) };

    [Serializable] sealed class Evidence
    {
        public bool batchmode, negativeControl, textNegativeControl;
        public int textDifference;
        public int width, height, screenWidth, screenHeight, repaints;
        public float glyphPixels;
        public string graphics;
    }

    public static void Run()
    {
        if (!Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("Metal batchmode 필요 (-nographics 금지)");
        try
        {
        output = Path.Combine(Application.dataPath, "../evidence");
        Directory.CreateDirectory(output);
        if (Directory.GetFiles(output).Length != 0) throw new IOException("새 출력 디렉터리 필요");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("Offscreen probe");
        camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.blue;
        go.AddComponent<OffscreenMarker>();
        view = ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        host = ScriptableObject.CreateInstance(typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView"));
        typeof(EditorWindow).GetField("m_Parent", Instance).SetValue(view, host);
        previousView = RenderingView.GetValue(null);
        RenderingView.SetValue(null, view);
        deadline = EditorApplication.timeSinceStartup + 60;
        BeginStage();
        EditorApplication.update += Tick;
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static void BeginStage()
    {
        var size = Sizes[stage];
        ViewType.GetProperty("targetSize", Instance).SetValue(view, (Vector2)size);
        ViewType.GetMethod("SetMainPlayModeViewSize", Instance).Invoke(view, new object[] { (Vector2)size });
        target = new RenderTexture(size.x, size.y, 24);
        if (!target.Create()) throw new InvalidOperationException("RenderTexture 생성 실패");
        camera.targetTexture = target;
        OffscreenMarker.HideText = stage == 2;
        stageStarted = EditorApplication.timeSinceStartup;
        nextRender = stageStarted;
        frames = 0;
        startRepaints = OffscreenMarker.Repaints;
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("캡처 제한 60초");
            if (EditorApplication.timeSinceStartup < nextRender) return;
            nextRender = EditorApplication.timeSinceStartup + 1.0 / 60;
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                Render.Invoke(null, new object[] { target, 0, Vector2.zero, false, stage != 3 });
            }
            finally { RenderTexture.active = previous; }
            // 폰트 텍스처 업로드에 Editor 프레임이 필요하다. 같은 메서드 내 반복은 불충분했다.
            if (++frames < 30 || EditorApplication.timeSinceStartup - stageStarted < 2) return;
            SaveAndCheck();
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            target = null;
            if (++stage < Sizes.Length) { BeginStage(); return; }
            Debug.Log("OFFSCREEN_CHECK_PASS 두 해상도 픽셀/글자/음성 대조; 진단 UI만, 게임 합격 아님");
            Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static void SaveAndCheck()
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            string name = stage == 3 ? "negative-no-imgui" : stage == 2 ? "negative-no-text" : $"imgui-{target.width}x{target.height}";
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            int repaints = OffscreenMarker.Repaints - startRepaints;
            var red = texture.GetPixel(target.width - 40, target.height - 40);
            var green = texture.GetPixel(40, 40);
            int glyphs = 0;
            for (int y = target.height - 42; y < target.height - 22; y++)
                for (int x = 105; x < 235; x++)
                {
                    var c = texture.GetPixel(x, y);
                    if (c.r > .2f && Mathf.Abs(c.r - c.g) < .1f && Mathf.Abs(c.g - c.b) < .1f) glyphs++;
                }
            int difference = 0;
            var pixels = texture.GetPixels(105, target.height - 42, 130, 20);
            if (stage == 1) withText = pixels;
            if (stage == 2)
                for (int i = 0; i < pixels.Length; i++)
                    if (Mathf.Abs(pixels[i].r - withText[i].r) > .15f) difference++;
            var result = new Evidence { batchmode = Application.isBatchMode, negativeControl = stage == 3, textNegativeControl = stage == 2,
                width = target.width, height = target.height, screenWidth = Screen.width, screenHeight = Screen.height,
                repaints = repaints, textDifference = difference, glyphPixels = glyphs, graphics = SystemInfo.graphicsDeviceType.ToString() };
            File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(result, true));
            bool markers = red.r > .8f && red.g < .1f && green.g > .8f && green.r < .1f;
            bool valid = stage == 3 ? repaints == 0 && !markers && glyphs == 0 :
                stage == 2 ? repaints > 0 && markers && difference > 50 : repaints > 0 && markers && glyphs > 50;
            if (!valid || Screen.width != target.width || Screen.height != target.height)
                throw new InvalidOperationException("UI 픽셀/해상도 검사 실패 " + JsonUtility.ToJson(result));
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
    }

    static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        if (RenderingView != null) RenderingView.SetValue(null, previousView);
        if (camera) camera.targetTexture = null;
        if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        if (view) UnityEngine.Object.DestroyImmediate(view);
        if (host) UnityEngine.Object.DestroyImmediate(host);
        EditorApplication.Exit(code);
    }
}
