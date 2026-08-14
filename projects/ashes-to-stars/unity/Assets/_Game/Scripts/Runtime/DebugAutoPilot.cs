using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 무인 스모크 — 사람 없이 던전을 한 바퀴 돌며 **스크린샷을 남긴다**.
    ///
    ///   AshesToStars --auto dungeon --seed 12345 --shots <폴더>
    ///
    /// 왜 필요한가: 던전 노드 맵·아레나 장애물은 배치 자가검사로 **로직만** 굳혔다.
    /// 화면은 눌러봐야 알 수 있는데, 매번 사람이 눌러 확인하면 아무도 확인하지 않게 된다.
    /// 이 프로젝트는 「파티원 5명 중 4명이 몹 스프라이트」를 수치 검증 전부 통과한 채로 놓쳤다 —
    /// 그림은 그림으로만 확인된다.
    ///
    /// ⚠️ 자동 진행이지 자동 판정이 아니다. 이 도구는 그림을 남길 뿐이고, 맞는지는 사람이 본다.
    /// </summary>
    public class DebugAutoPilot : MonoBehaviour
    {
        public static bool Requested { get; private set; }
        static uint _seed = 20260814u;
        static string _shotDir;

        float _t;
        int _step;

        /// <summary>타이틀 화면이 부팅될 때 한 번 호출한다.</summary>
        public static void BootstrapIfRequested()
        {
            var a = System.Environment.GetCommandLineArgs();
            bool auto = false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == "--auto" && i + 1 < a.Length && a[i + 1] == "dungeon") auto = true;
                if (a[i] == "--seed" && i + 1 < a.Length && uint.TryParse(a[i + 1], out uint s)) _seed = s;
                if (a[i] == "--shots" && i + 1 < a.Length) _shotDir = a[i + 1];
            }
            if (!auto || Requested) return;

            Requested = true;
            _shotDir ??= Application.persistentDataPath;
            var go = new GameObject("DebugAutoPilot");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugAutoPilot>();
        }

        void Start()
        {
            // 진입 비용을 낼 수 있게 지갑을 채운다 — 스모크의 목적은 경제 검증이 아니다
            GameState.Earn(500000);
            DungeonRun.Begin(_seed, 3, DungeonKind.일반, GameFlow.Field);
            GameFlow.Go(GameFlow.Dungeon);
        }

        void Update()
        {
            _t += Time.deltaTime;

            // 1) 노드 맵을 찍는다
            if (_step == 0 && _t > 1.2f)
            {
                Shot("auto_dungeon_map");
                _step = 1; _t = 0f;
                return;
            }

            // 2) 전투가 있는 첫 노드로 들어간다
            if (_step == 1 && _t > 0.6f)
            {
                foreach (int n in DungeonRun.NextNodes())
                {
                    if (DungeonRun.Plan.Nodes[n].Wave == null) continue;
                    Debug.Log($"[스모크] 노드 {n} 진입 — {DungeonRun.Plan.Nodes[n].Kind} " +
                              $"/ {DungeonRun.Plan.Nodes[n].Template}");
                    DungeonRun.Enter(n);
                    _step = 2; _t = 0f;
                    return;
                }
                // 전투 노드가 하나도 없으면(있을 수 없다) 그냥 끝낸다 — 조용히 멈추지 않는다
                Debug.LogWarning("[스모크] 전투 노드를 찾지 못했다");
                Finish();
                return;
            }

            // 3) 전투 장면을 찍는다 — 장애물·파티클·스프라이트가 다 나온 뒤에
            if (_step == 2 && _t > 12f)
            {
                Shot("auto_dungeon_battle");
                _step = 3; _t = 0f;
                return;
            }

            if (_step == 3 && _t > 1.5f) Finish();
        }

        void Shot(string name)
        {
            string path = System.IO.Path.Combine(_shotDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[스모크] 스크린샷: {path}");
        }

        void Finish()
        {
            Debug.Log("[스모크] 완료");
            Application.Quit();
        }
    }
}
