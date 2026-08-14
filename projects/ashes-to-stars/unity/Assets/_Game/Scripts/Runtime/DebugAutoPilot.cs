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
        static string _mode = "dungeon";
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
                if (a[i] == "--auto" && i + 1 < a.Length)
                {
                    auto = true;
                    _mode = a[i + 1];      // dungeon | party
                }
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

            if (_mode == "hunt")
            {
                // 던전 **밖** 경로 — 던전이 아닐 때도 전투가 정상인지 본다.
                // 지금까지 스모크가 전부 던전이라 DungeonRun이 꺼진 경로는 한 번도 안 밟았다.
                GameFlow.GoBattle(GameFlow.Field);
                return;
            }

            if (_mode == "boss")
            {
                // 보스 노드까지 이겨서 가려면 몇 분이 걸린다 — 기믹 3종이 **도는지**만 보면 되므로
                // 계획을 만든 뒤 종점 보스 노드로 바로 들어간다(검증용 지름길).
                DungeonRun.Begin(_seed, 3, DungeonKind.일반, GameFlow.Field);
                DungeonRun.Enter(DungeonRun.Plan.BossIndex);
                return;
            }

            if (_mode == "raid")
            {
                // ✅ §7 레이드급이 필드에 떠 있는 상태를 만든다 — 랜덤 출현을 기다릴 수 없으니 강제 소환
                RaidSpawn.ForceSpawnForTest(_seed);
                GameFlow.Go(GameFlow.Field);
                return;
            }

            if (_mode == "party")
            {
                // 편성 화면만 확인한다. 회복 중·마지막 목숨 표시를 보려고 상태를 하나 만들어 둔다.
                var roster = LifeSystem.GetCharacters();
                if (roster.Count >= 3)
                {
                    LifeSystem.RegisterDeath(roster[2]);                 // 회복 중 표시
                    LifeSystem.RegisterDeath(roster[1]);
                    LifeSystem.UseRevivePotion(roster[1]);               // 회복만 풀고 사망 1회 유지
                    LifeSystem.RegisterDeath(roster[1]);                 // 마지막 목숨(2회)
                }
                GameFlow.Go(GameFlow.Party);
                return;
            }
            DungeonRun.Begin(_seed, 3, DungeonKind.일반, GameFlow.Field);
            GameFlow.Go(GameFlow.Dungeon);
        }

        void Update()
        {
            _t += Time.deltaTime;

            if (_mode == "hunt")
            {
                if (_step == 0 && _t > 12f) { Shot("auto_field_hunt"); _step = 1; _t = 0f; }
                else if (_step == 1 && _t > 1.5f) Finish();
                return;
            }

            if (_mode == "boss")
            {
                // 기믹(동시 장판·쫄 소환·힐 체크)이 도는 구간을 노린다 — 시작 직후는 아직 아무것도 안 돈다
                if (_step == 0 && _t > 18f) { Shot("auto_boss"); _step = 1; _t = 0f; }
                else if (_step == 1 && _t > 1.5f) Finish();
                return;
            }

            if (_mode == "raid")
            {
                if (_step == 0 && _t > 1.2f) { Shot("auto_raid_field"); _step = 1; _t = 0f; }
                else if (_step == 1 && _t > 1.5f) Finish();
                return;
            }

            if (_mode == "party")
            {
                // ⚠️ 찍고 바로 끄면 안 된다 — CaptureScreenshot은 그 프레임 **끝**에 기록된다.
                //    같은 프레임에 Quit하면 파일이 안 생긴다(던전 경로에서 이미 겪은 함정과 같은 계열).
                if (_step == 0 && _t > 1.2f) { Shot("auto_party"); _step = 1; _t = 0f; }
                else if (_step == 1 && _t > 1.5f) Finish();
                return;
            }

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
