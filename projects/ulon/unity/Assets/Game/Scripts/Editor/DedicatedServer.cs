using System.IO;
using Ulon.Server;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    public static class DedicatedServer
    {
        const string Scene = "Assets/Game/Scenes/Bootstrap.unity";
        public const string ClientOut = "builds/client/UlonClient.app";

        [MenuItem("Ulon/Start Dedicated Server (Play)")]
        public static void StartInEditor()
        {
            SessionState.SetBool("ulon.dedicated", true);
            EditorSceneManager.OpenScene(Scene);
            EditorApplication.EnterPlaymode();
            Debug.Log("[Ulon] 에디터를 전용 서버로 기동. 클라 빌드는 127.0.0.1:7770 으로 접속.");
        }

        [MenuItem("Ulon/Closed Alpha Smoke")]
        public static void AlphaSmoke()
        {
            if (!CharacterStore.Health())
                throw new System.InvalidOperationException("persist http://127.0.0.1:8777 가 응답하지 않습니다. server/start_persist.sh 를 먼저 실행하세요.");
            string bak = OpLog.Backup();
            Debug.Log("[Ulon] Closed Alpha smoke PASS persist=ok backup=" + bak + " host=-ulon-host <LAN> :7770");
        }

        [MenuItem("Ulon/Build Dedicated Server + Client")]
        public static void BuildBoth()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
            string client = BuildClient();
            Debug.Log("[Ulon] player build=" + client + " (서버는 -ulon-server 헤드리스)");
        }

        public static string BuildClient()
        {
            if (!File.Exists(Scene))
                throw new System.InvalidOperationException("missing scene: " + Scene);
            RejectGuidlessScripts(Scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            string project = Directory.GetParent(Application.dataPath)!.FullName;
            PurgeStalePlayerData(project);

            var opened = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(opened);
            EditorSceneManager.SaveScene(opened);
            AssetDatabase.ForceReserializeAssets(new[] { Scene }, ForceReserializeAssetsOptions.ReserializeAssets);

            string dest = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath)!.Parent!.FullName, ClientOut));
            if (Directory.Exists(dest))
                Directory.Delete(dest, true);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = dest,
                target = BuildTarget.StandaloneOSX,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.Development | BuildOptions.CleanBuildCache
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("클라 빌드 실패: " + report.summary.result);
            return dest;
        }

        static void RejectGuidlessScripts(string scenePath)
        {
            foreach (string line in File.ReadAllLines(scenePath))
            {
                if (line.Contains("m_Script:") && !line.Contains("guid:"))
                    throw new System.InvalidOperationException("scene has m_Script without guid (extra MonoBehaviour in one .cs file): " + scenePath);
            }
        }

        static void PurgeStalePlayerData(string project)
        {
            foreach (string rel in new[] { "Library/PlayerDataCache", "Library/BuildPlayerData" })
            {
                string folder = Path.Combine(project, rel);
                if (!Directory.Exists(folder))
                    continue;
                Directory.Delete(folder, true);
                Debug.Log("[Ulon] purged stale " + rel);
            }
        }
    }
}
