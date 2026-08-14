using System.IO;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// W1~W3 러너가 공유하는 빌드 타겟 결정.
///
/// 왜: 러너 3개가 각각 StandaloneWindows64 + Direct3D11 + ".exe"를 하드코딩하고 있어
/// 맥에서 배치 빌드를 걸면 그래픽 API 설정부터 어긋난다(맥 운영기 이관, 2026-08-14).
/// 세 곳에 복붙하면 한 곳만 고쳐져 어긋나므로 여기 한 곳에서만 판정한다.
///
/// 판정은 **에디터가 실제로 돌고 있는 OS**를 따른다. 배치 실행 시
/// -buildTarget 으로 넘어온 활성 타겟이 있으면 그것을 우선한다.
/// </summary>
public static class BuildPlatform
{
    /// <summary>이 기계에서 만들 스탠드얼론 타겟</summary>
    public static BuildTarget Target
    {
        get
        {
            var active = EditorUserBuildSettings.activeBuildTarget;
            if (active == BuildTarget.StandaloneOSX
                || active == BuildTarget.StandaloneWindows64
                || active == BuildTarget.StandaloneLinux64)
                return active;
#if UNITY_EDITOR_OSX
            return BuildTarget.StandaloneOSX;
#elif UNITY_EDITOR_LINUX
            return BuildTarget.StandaloneLinux64;
#else
            return BuildTarget.StandaloneWindows64;
#endif
        }
    }

    /// <summary>산출물 확장자 (맥은 .app 번들)</summary>
    public static string Extension
    {
        get
        {
            switch (Target)
            {
                case BuildTarget.StandaloneOSX: return ".app";
                case BuildTarget.StandaloneWindows64: return ".exe";
                default: return "";
            }
        }
    }

    /// <summary>outDir/stem + 플랫폼 확장자</summary>
    public static string OutputPath(string outDir, string stem)
    {
        return Path.Combine(outDir, stem + Extension);
    }

    /// <summary>측정용 그래픽 API 고정 (맥 Metal / Win D3D11)</summary>
    public static void ApplyGraphicsApi()
    {
        var t = Target;
        switch (t)
        {
            case BuildTarget.StandaloneOSX:
                PlayerSettings.SetGraphicsAPIs(t, new[] { GraphicsDeviceType.Metal });
                break;
            case BuildTarget.StandaloneWindows64:
                PlayerSettings.SetGraphicsAPIs(t, new[] { GraphicsDeviceType.Direct3D11 });
                break;
            default:
                PlayerSettings.SetGraphicsAPIs(t, new[] { GraphicsDeviceType.Vulkan });
                break;
        }
    }
}
