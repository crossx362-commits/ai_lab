using System.Collections;
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
                StartCoroutine(WaitServerStarted());
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

        IEnumerator WaitServerStarted()
        {
            float until = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < until)
            {
                if (manager != null && manager.IsServerStarted)
                {
                    Debug.Log("Local server is started");
                    yield break;
                }
                yield return null;
            }
        }

        void EnsureSpawns()
        {
            var spawner = GetComponent<PlayerSpawner>();
            if (spawner == null)
                return;
            if (spawner.Spawns != null && spawner.Spawns.Length >= 2)
            {
                // 씬이 자리를 갖고 있어도 **높이는 확인한다** — 아래 사고가 정확히 이 경로였다.
                for (int i = 0; i < spawner.Spawns.Length; i++)
                    LiftToGround(spawner.Spawns[i]);
                return;
            }
            var a = new GameObject("SpawnA").transform;
            a.position = new Vector3(-1.2f, 0f, 1.2f);
            var b = new GameObject("SpawnB").transform;
            b.position = new Vector3(1.2f, 0f, 1.2f);
            LiftToGround(a);
            LiftToGround(b);
            spawner.Spawns = new[] { a, b };
        }

        /// <summary>
        /// **접속 자리를 지면 위로 올린다**(2026-09-08 실측 사고).
        ///
        /// 스폰 자리가 `y = 0`으로 박혀 있었는데 이 월드의 지면은 `LandBase = 10`이다 —
        /// 즉 접속하는 순간 두 아바타가 **지면 10m 아래**에 떨어져 끝없이 낙하했고(실측 y −3.6 → −21.0),
        /// 서버는 그 몸을 죽은 것으로 보아 파티 초대를 `ghost`로 거절했다.
        /// 「온라인에서 파티가 안 된다」의 진짜 뿌리는 버튼이 아니라 **접속 자리**였다.
        /// 높이는 다른 데서 쓰는 자와 같은 것으로 잰다(`Terrain.SampleHeight` + 지형 원점 y).
        /// </summary>
        static void LiftToGround(Transform t)
        {
            if (t == null)
                return;
            // NC 전용 스위치 — 이 결함(접속 자리 y=0)을 **실제로 되살려** 검사가 빨간불인지 본다.
            // 게이트를 끄는 게 아니라 세계를 옛 상태로 되돌리는 쪽이 진짜 네거티브 컨트롤이다.
            if (Cli.Has("-ulon-nc-spawn"))
            {
                Debug.Log("[Ulon] NC — 접속 자리를 지면 위로 올리지 않는다(옛 결함 재현)");
                return;
            }
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return;
            var p = t.position;
            float ground = terrain.SampleHeight(p) + terrain.transform.position.y;
            if (p.y >= ground - 0.05f)
                return;
            t.position = new Vector3(p.x, ground + 0.1f, p.z);
            Debug.Log("[Ulon] 접속 자리를 지면 위로 — " + t.name + " y " + p.y.ToString("0.0") + " → " +
                      t.position.y.ToString("0.0") + " (지면 " + ground.ToString("0.0") + "m)");
        }
    }
}
