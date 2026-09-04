using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ulon.Editor
{
    public static class NetworkSliceSetup
    {
        const string PrefabPath = "Assets/Game/Prefabs/NetPlayer.prefab";
        const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";

        [MenuItem("Ulon/Setup Network Slice")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var player = GameObject.Find("Player");
            if (player == null)
                throw new System.InvalidOperationException("Bootstrap 씬에 Player가 없습니다.");

            var prefabRoot = Object.Instantiate(player);
            prefabRoot.name = "NetPlayer";
            if (prefabRoot.GetComponent<NetworkObject>() == null)
                prefabRoot.AddComponent<NetworkObject>();
            if (prefabRoot.GetComponent<NetworkTransform>() == null)
                prefabRoot.AddComponent<NetworkTransform>();
            if (prefabRoot.GetComponent<NetAvatar>() == null)
                prefabRoot.AddComponent<NetAvatar>();
            var body = prefabRoot.GetComponent<WorldBody>();
            if (body != null)
                body.IsAvatar = true;

            System.IO.Directory.CreateDirectory("Assets/Game/Prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Object.DestroyImmediate(prefabRoot);
            var prefabNob = prefab.GetComponent<NetworkObject>();

            WireMob("Skeleton");
            WireMob("Bandit");
            WireMob("Raider");
            WireMob("Rogue");
            WireMob("Knight");
            WireMob("Acolyte");
            WireMob("Minion");
            WireMob("SkelRogue");
            WireMob(Dungeon1.MobObject);
            WireMob(Dungeon1.BossObject);
            WireMob(Dungeon2.MobObject);
            WireMob(Dungeon2.BossObject);
            WireMob(FieldBoss.Object);

            var nmGo = GameObject.Find("NetworkManager");
            if (nmGo == null)
                nmGo = new GameObject("NetworkManager");
            var nm = nmGo.GetComponent<NetworkManager>() ?? nmGo.AddComponent<NetworkManager>();
            if (nmGo.GetComponent<Tugboat>() == null)
                nmGo.AddComponent<Tugboat>();
            if (nmGo.GetComponent<AutoStartNetwork>() == null)
                nmGo.AddComponent<AutoStartNetwork>();
            var spawner = nmGo.GetComponent<PlayerSpawner>() ?? nmGo.AddComponent<PlayerSpawner>();
            spawner.SetPlayerPrefab(prefabNob);
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(nmGo);

            var world = GameObject.Find("OfflineWorld");
            if (world != null && world.GetComponent<NetHud>() == null)
                world.AddComponent<NetHud>();
            if (world != null && world.GetComponent<PersistDriver>() == null)
                world.AddComponent<PersistDriver>();

            EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Ulon] Network slice ready. Prefab=" + PrefabPath);
        }

        public static void WireMob(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
                return;
            if (go.GetComponent<NetworkObject>() == null)
                go.AddComponent<NetworkObject>();
            if (go.GetComponent<NetMob>() == null)
                go.AddComponent<NetMob>();
        }
    }
}
