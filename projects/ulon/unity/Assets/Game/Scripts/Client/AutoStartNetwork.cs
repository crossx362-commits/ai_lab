using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Client
{
    public sealed class AutoStartNetwork : MonoBehaviour
    {
        NetworkManager manager;
        bool offlineReady;

        void Awake()
        {
            if (GetComponent<DualClientProbe>() == null)
                gameObject.AddComponent<DualClientProbe>();
        }

        void Start()
        {
            manager = GetComponent<NetworkManager>();
            if (manager == null)
                return;

            EnsureSpawns();
            bool dedicated = IsDedicated();
            bool asClient = Cli.Has("-ulon-client");

            if (dedicated)
            {
                PrepareSceneForNetwork();
                ActivateSkeleton();
                ApplyListen(manager, true);
                manager.ServerManager.StartConnection();
                Debug.Log("[Ulon] Dedicated server listening UDP 7770 bind 0.0.0.0 host=" + Host());
                return;
            }

            if (asClient)
            {
                PrepareSceneForNetwork();
                ApplyListen(manager, false);
                manager.ClientManager.StartConnection();
                Debug.Log("[Ulon] Client connecting " + Host() + ":7770 account=" + PersistDriver.AccountKey());
                return;
            }

        }

        void LateUpdate()
        {
            if (offlineReady)
                return;
            if (manager != null && (manager.IsServerStarted || manager.IsClientStarted))
            {
                offlineReady = true;
                return;
            }
            WakeOfflineMobs();
            TintIfDefault("IronVein", new Color(0.42f, 0.38f, 0.32f));
            TintIfDefault("Forge", new Color(0.38f, 0.22f, 0.12f));
            offlineReady = true;
        }

        public static string Host()
        {
            string host = Cli.Get("-ulon-host", "");
            if (!string.IsNullOrEmpty(host))
                return host;
            return "127.0.0.1";
        }

        public static void ApplyListen(NetworkManager manager, bool asServer)
        {
            if (manager == null || manager.TransportManager == null)
                return;
            var t = manager.TransportManager.Transport;
            if (t == null)
                return;
            t.SetPort(7770);
            t.SetClientAddress(Host());
            if (asServer)
                t.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);
        }

        public static bool IsDedicated()
        {
#if UNITY_SERVER
            return true;
#else
            if (Application.isBatchMode && !Cli.Has("-ulon-client"))
                return true;
            if (Cli.Has("-ulon-server"))
                return true;
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetBool("ulon.dedicated", false);
#else
            return false;
#endif
#endif
        }

        public static void PrepareSceneForNetwork()
        {
            DisableNamed("Player");
            DisableNamed("Companion");
        }

        static void DisableNamed(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                go.SetActive(false);
        }

        static void ActivateSkeleton()
        {
            WakeOfflineMobs();
        }

        static void WakeOfflineMobs()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != "Skeleton")
                    continue;
                var nob = roots[i].GetComponent<FishNet.Object.NetworkObject>();
                if (nob != null)
                    nob.enabled = false;
                roots[i].SetActive(true);
            }
        }

        static void TintIfDefault(string name, Color color)
        {
            var go = GameObject.Find(name);
            if (go == null)
                return;
            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                return;
            if (renderer.sharedMaterial.name.IndexOf("Default-Material", System.StringComparison.Ordinal) < 0)
                return;
            var mat = new Material(renderer.sharedMaterial);
            mat.color = color;
            renderer.material = mat;
        }

        void EnsureSpawns()
        {
            var spawner = GetComponent<PlayerSpawner>();
            if (spawner == null)
                return;
            if (spawner.Spawns != null && spawner.Spawns.Length >= 2)
                return;
            var a = new GameObject("SpawnA").transform;
            a.position = new Vector3(-1.2f, 0f, 1.2f);
            var b = new GameObject("SpawnB").transform;
            b.position = new Vector3(1.2f, 0f, 1.2f);
            spawner.Spawns = new[] { a, b };
        }
    }
}
