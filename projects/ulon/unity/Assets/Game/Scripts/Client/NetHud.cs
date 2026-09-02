using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed class NetHud : MonoBehaviour
    {
        NetworkManager manager;

        void Awake() => manager = InstanceFinder.NetworkManager;

        void OnGUI()
        {
            if (manager == null)
                manager = InstanceFinder.NetworkManager;

            GUI.Box(new Rect(16, 116, 320, 118), "");
            string server = manager != null && manager.IsServerStarted ? "서버 ON" : "서버 OFF";
            string client = manager != null && manager.IsClientStarted ? "클라 ON" : "클라 OFF";
            int n = 0;
            if (manager != null && manager.IsServerStarted)
                n = manager.ServerManager.Clients.Count;
            GUI.Label(new Rect(28, 124, 300, 20), $"{server}  ·  {client}  ·  접속 {n}");

            if (GUI.Button(new Rect(28, 148, 90, 28), "호스트"))
                StartHost();
            if (GUI.Button(new Rect(126, 148, 90, 28), "클라"))
                StartClient();
            if (GUI.Button(new Rect(224, 148, 90, 28), "중지"))
                StopAll();

            GUI.Label(new Rect(28, 184, 300, 36), "전용 서버 또는 호스트 후 클라.\n" + AutoStartNetwork.Host() + ":7770");
        }

        void StartHost()
        {
            PrepareSceneForNetwork();
            if (manager == null)
                return;
            AutoStartNetwork.ApplyListen(manager, true);
            if (!manager.IsServerStarted)
                manager.ServerManager.StartConnection();
            if (!manager.IsClientStarted)
                manager.ClientManager.StartConnection();
        }

        void StartClient()
        {
            PrepareSceneForNetwork();
            if (manager == null)
                return;
            AutoStartNetwork.ApplyListen(manager, false);
            if (!manager.IsClientStarted)
                manager.ClientManager.StartConnection();
        }

        void StopAll()
        {
            if (manager == null)
                return;
            if (manager.IsClientStarted)
                manager.ClientManager.StopConnection();
            if (manager.IsServerStarted)
                manager.ServerManager.StopConnection(true);
        }

        static void PrepareSceneForNetwork() => AutoStartNetwork.PrepareSceneForNetwork();
    }
}
