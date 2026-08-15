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
        static int _shotFrame;     // >0이면 이 프레임에 찍고 끝낸다(모드별 시나리오를 건너뛴다)
        static float _shotSec;     // >0이면 이 **게임 시간**(초)에 찍는다. 기믹처럼 초 단위로
                                   // 예약된 것을 보려면 프레임이 아니라 이걸 써야 한다

        float _t;
        int _step;
        int _frames;

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
                // 캡처 시점을 **프레임 수**로 지정한다. 시간(초)은 기계마다 로딩이 달라
                // 같은 순간을 못 잡는다 — 프레임은 결정적이다.
                if (a[i] == "--shot-frame" && i + 1 < a.Length && int.TryParse(a[i + 1], out int f)) _shotFrame = f;
            }

            // ── 환경변수 경로 (오너 지침 2026-08-15: `GAME_START=dungeon` 형태)
            //    CLI 인자보다 **약하다** — 인자가 있으면 그쪽이 이긴다. 루프가 환경변수로 기본을
            //    깔고, 특정 실행만 인자로 덮어쓰는 사용을 상정한 순서다.
            var envStart = System.Environment.GetEnvironmentVariable("GAME_START");
            if (!auto && !string.IsNullOrEmpty(envStart)) { auto = true; _mode = envStart.Trim(); }
            var envShots = System.Environment.GetEnvironmentVariable("GAME_SHOT_DIR");
            if (_shotDir == null && !string.IsNullOrEmpty(envShots)) _shotDir = envShots;
            var envFrame = System.Environment.GetEnvironmentVariable("GAME_SHOT_FRAME");
            if (_shotFrame <= 0 && int.TryParse(envFrame, out int ef)) _shotFrame = ef;
            var envSec = System.Environment.GetEnvironmentVariable("GAME_SHOT_SEC");
            if (_shotSec <= 0f && float.TryParse(envSec, out float es)) _shotSec = es;

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

            if (_mode == "stress")
            {
                // 부하 측정(§10-9 「잡몹 상한에서 60fps」).
                // ⚠️ `StressTest`는 완성돼 있는데 **어디서도 생성되지 않는 죽은 코드**였다
                //    (`AddComponent<StressTest>` 0곳, 2026-08-15 확인). 즉 이 게임의
                //    성능 기준은 **한 번도 실측된 적이 없다.** 상한을 올리려면 먼저
                //    재야 하므로 진입점을 만든다.
                //    CSV 경로는 `--out`으로 넘긴다(없으면 persistentDataPath에 숨는다).
                var host = new GameObject("StressTest");
                DontDestroyOnLoad(host);
                host.AddComponent<global::StressTest>();
                return;                       // 자체 수명주기로 돈다(끝나면 스스로 Quit)
            }

            if (_mode == "estate")
            {
                // 영묘(§4) 확인 — 삭제된 캐릭터와 환생석이 **둘 다 있어야** 화면이 성립한다.
                // 빈 목록만 찍으면 "환생할 수 있다"가 확인되지 않는다.
                var roster = LifeSystem.GetCharacters();
                if (roster.Count >= 2)
                    for (int k = 0; k < 3; k++) LifeSystem.RegisterDeath(roster[1]);   // 3회 = 삭제
                GameState.Gain(Economy.LifeItem.RebornStone, 2);
                EstateScreen.AutoOpen = "영묘";   // 허브만 찍으면 채운 화면을 못 본다
                GameFlow.Go(GameFlow.Estate);
                return;
            }

            if (_mode == "style")
            {
                // 전투 스타일 화면(§3). 기본값만 보이면 「고를 수 있다」가 확인되지 않으므로
                // 서로 다른 스타일을 미리 넣어 **선택이 실제로 저장·표시되는지** 함께 본다.
                CombatStylePrefs.Set("수호기사", StyleId.방어형);
                CombatStylePrefs.Set("검사", StyleId.공격형);
                CombatStylePrefs.Set("사제", StyleId.생존형);
                GameFlow.Go(GameFlow.Style);
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
            _frames++;

            // ── 프레임 지정 캡처 (오너 지침: "지정한 프레임에서 뷰포트를 PNG로 저장한 뒤 종료")
            //    모드별 시나리오보다 **우선**한다. 시나리오는 "던전을 한 바퀴 돌며" 같은 서사를
            //    태우지만, 시각 QA는 "그 화면을 지금 보여달라"가 전부다.
            //    ⚠️ 찍은 프레임에 바로 Quit하면 파일이 안 생긴다 — CaptureScreenshot은 그 프레임
            //       **끝**에 기록된다. 그래서 찍고 나서 몇 프레임 더 돌린 뒤 종료한다.
            if (_shotFrame > 0)
            {
                if (_frames == _shotFrame) Shot($"qa_{_mode}");
                else if (_frames >= _shotFrame + 8) Finish();
                return;
            }

            // ── 시간 지정 캡처 (`GAME_SHOT_SEC`)
            //    프레임 지정만으론 부족하다: 이 게임은 `targetFrameRate = -1`이라 4000fps로
            //    돌고 `deltaTime`이 0.00025초다. 2400프레임을 돌려도 **게임 시간은 0.6초**뿐이라
            //    6초 뒤 터지는 보스 기믹을 영영 못 본다(2026-08-15 실측).
            //    "몇 프레임 뒤"와 "몇 초 뒤"는 이 프로젝트에서 전혀 다른 뜻이다.
            if (_shotSec > 0f)
            {
                if (_t >= _shotSec && _step == 0) { Shot($"qa_{_mode}"); _step = 1; _t = 0f; }
                else if (_step == 1 && _t > 0.5f) Finish();
                return;
            }

            if (_mode == "hunt")
            {
                if (_step == 0 && _t > 12f) { DumpHugeSprites(); Shot("auto_field_hunt"); _step = 1; _t = 0f; }
                else if (_step == 1 && _t > 1.5f) Finish();
                return;
            }

            if (_mode == "boss")
            {
                // 소환(2번째 기믹, ~18초)이 터진 뒤 쫄이 파티에 붙어 때리는 구간을 노린다.
                // 그래야 화면에 쫄이 보이고, 「소환 몹이 준 파티 피해」도 누적돼 로그로 읽힌다.
                if (_step == 0 && _t > 30f)
                {
                    float summonDmg = global::W3Party.SummonDmgOnActive();
                    Debug.Log($"[QA] 소환 몹이 파티에 준 피해(누적) = {summonDmg:F0}");
                    Shot("auto_boss"); _step = 1; _t = 0f;
                }
                else if (_step == 1 && _t > 1.5f) Finish();
                return;
            }

            // 부하 측정은 `StressTest`가 자체 수명주기로 돌고 스스로 종료한다.
            // 여기서 손대면 측정 도중에 꺼져 CSV가 안 나온다.
            if (_mode == "stress") return;

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

        /// <summary>
        /// 화면에 거대한 것이 떠 있을 때 **무엇인지** 특정한다.
        /// 필드 전투에서 아레나만 한 노란 타원이 나왔는데, 도발 링(반경 4.5)으로도
        /// 장판 링(3.2)으로도 크기가 설명되지 않았다 — 짐작으로 고치면 멀쩡한 표시를 죽인다.
        /// 그래서 실제로 화면에 있는 것 중 폭 10유닛이 넘는 스프라이트를 전부 찍는다.
        /// </summary>
        static void DumpHugeSprites()
        {
            var all = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            int n = 0;
            foreach (var sr in all)
            {
                if (!sr.enabled || sr.sprite == null) continue;
                var b = sr.bounds.size;
                if (b.x < 10f && b.y < 10f) continue;
                Debug.Log($"[거대] {sr.name} 크기 {b.x:F1}x{b.y:F1} 스케일 {sr.transform.localScale} " +
                          $"정렬 {sr.sortingOrder} 색 {sr.color} 스프라이트 {sr.sprite.name} " +
                          $"부모 {(sr.transform.parent ? sr.transform.parent.name : "-")}");
                n++;
            }
            Debug.Log($"[거대] 폭 10유닛 초과 스프라이트 {n}개 / 전체 {all.Length}개");
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
