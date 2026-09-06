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

            // 접속 패널은 **디버그 성격**이다(검수 2026-09-07 정보 구조) — 이미 붙어 있으면 숨긴다.
            // 예전에는 화면 좌상단에 고정으로 그려져 상태 카드 글자와 겹쳐 있었다.
            bool connected = manager != null && (manager.IsServerStarted || manager.IsClientStarted);
            if (connected && !SliceHud.DebugOpen)
                return;

            var area = new Rect(12f, Screen.height - 178f, 320f, 118f);
            SliceHud.RegisterArea(area);
            GUI.BeginGroup(area);
            string server = manager != null && manager.IsServerStarted ? "서버 ON" : "서버 OFF";
            string client = manager != null && manager.IsClientStarted ? "클라 ON" : "클라 OFF";
            int n = 0;
            if (manager != null && manager.IsServerStarted)
                n = manager.ServerManager.Clients.Count;
            GUI.Box(new Rect(0, 0, area.width, area.height), GUIContent.none);
            GUI.Label(new Rect(12, 8, 296, 20), $"{server}  ·  {client}  ·  접속 {n}");

            if (GUI.Button(new Rect(12, 32, 90, 28), "호스트"))
                StartHost();
            if (GUI.Button(new Rect(110, 32, 90, 28), "클라"))
                StartClient();
            if (GUI.Button(new Rect(208, 32, 90, 28), "중지"))
                StopAll();

            GUI.Label(new Rect(12, 68, 296, 36), "전용 서버 또는 호스트 후 클라.\n" + AutoStartNetwork.Host() + ":7770");
            GUI.EndGroup();
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
