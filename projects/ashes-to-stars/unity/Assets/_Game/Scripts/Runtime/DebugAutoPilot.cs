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
        static string _advancementJob;

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
            _advancementJob = System.Environment.GetEnvironmentVariable("QA_ADV_JOB");

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

            if (_mode == "dash")
            {
                // 이동기 자가검사 — 전투를 띄우고 W3Party가 스스로 재고 판정한다.
                global::W3Party.QaDashProbe = true;
                GameFlow.GoBattle(GameFlow.Field);
                return;
            }

            if (_mode == "advancement")
            {
                // 1차 6종의 슬롯 1/2를 실제 BattleScreen에서 강제 시전한다.
                // 직업 계약만 확인하는 SelfCheck와 달리 피해·회복·보호막 수치가 남아야 통과다.
                if (global::W3Party.FirstAdvancementProbeMetricNames(_advancementJob).Length != 2)
                    throw new System.InvalidOperationException($"QA_ADV_JOB 미지정/미지원: {_advancementJob}");
                LifeSystem.ResetAll();
                LifeSystem.Initialize();
                PartyState.ResetForTest();
                var roster = LifeSystem.GetCharacters();
                if (roster.Count == 0) throw new System.InvalidOperationException("전직 QA 로스터가 비어 있음");
                roster[0].Job = _advancementJob;
                roster[0].Advancement = AdvancementTier.First;
                PartyState.Refresh();
                global::W3Party.QaFirstAdvancementJob = _advancementJob;
                int.TryParse(System.Environment.GetEnvironmentVariable("QA_ADV_NO_SLOT"),
                             out global::W3Party.QaAdvancementDisabledSlot);
                GameFlow.GoBattle(GameFlow.Field);
                return;
            }

            if (_mode == "second_advancement")
            {
                if (!global::W3Party.SupportsAdvancementJob(_advancementJob))
                    throw new System.InvalidOperationException($"QA_ADV_JOB 미지정: {_advancementJob}");
                LifeSystem.ResetAll();
                LifeSystem.Initialize();
                PartyState.ResetForTest();
                var roster = LifeSystem.GetCharacters();
                if (roster.Count == 0) throw new System.InvalidOperationException("2차 각성 QA 로스터가 비어 있음");
                roster[0].Job = _advancementJob;
                int.TryParse(System.Environment.GetEnvironmentVariable("QA_ULT_BLOCK"),
                             out global::W3Party.QaUltimateBlock);
                roster[0].Advancement = global::W3Party.QaUltimateBlock == 1
                    ? AdvancementTier.First : AdvancementTier.Second;
                PartyState.Refresh();
                global::W3Party.QaSecondAdvancementJob = _advancementJob;
                GameFlow.GoBattle(GameFlow.Field);
                return;
            }

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

            // `GAME_START=go:Field` 처럼 **아무 화면이나** 바로 띄운다.
            // 화면이 하나 늘 때마다 전용 모드를 추가하는 건 같은 일의 반복이다 —
            // 씬 이름은 `GameFlow`가 이미 상수로 들고 있으니 그걸 그대로 받는다.
            if (_mode.StartsWith("go:"))
            {
                string scene = _mode.Substring(3);
                // 건물처럼 하위 화면이 있으면 `go:Estate/영묘` 로 안쪽까지 연다.
                int slash = scene.IndexOf('/');
                if (slash > 0)
                {
                    EstateScreen.AutoOpen = scene.Substring(slash + 1);
                    scene = scene.Substring(0, slash);
                }
                if (EstateScreen.AutoOpen == "대장간") SeedSmithQa();
                GameFlow.Go(scene);
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
            // 전직 계측 모드는 지정 시각까지 스킬을 실제 시전한 뒤 수치와 화면을 함께 남긴다.
            // 일반 프레임 캡처가 먼저 종료하면 PNG만 생기고 [QA-전직] 증거가 사라진다.
            bool ownsCapture = _mode == "advancement" || _mode == "second_advancement";
            if (_shotFrame > 0 && !ownsCapture)
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
            if (_shotSec > 0f && !ownsCapture)
            {
                if (_t >= _shotSec && _step == 0)
                {
                    // 캡처 시점의 전투 계측을 함께 남긴다 — 판 종료 로그는 QA가 못 본다.
                    // ⚠️ `global::`을 **보간 문자열 안에서** 쓰면 컴파일이 깨진다
                    //    (`error CS0103: The name 'global' does not exist`). 이 파일은
                    //    `AshesToStars` 네임스페이스 안이고 `W3Party`는 전역이라 한정자가
                    //    필요한데, 반드시 **밖에서 지역변수로 받아** 넣을 것.
                    int aiDash = global::W3Party.AiDashUsesOnActive();
                    var chg = global::W3Party.ChargeStatsOnActive();
                    Debug.Log($"[QA] AI 이동기 사용 {aiDash}회 · 돌진형 예고 {chg.tell}회 → 돌진 {chg.rush}회 " +
                              $"(-1이면 전투가 안 돌고 있다는 뜻)");
                    Shot($"qa_{_mode}"); _step = 1; _t = 0f;
                }
                else if (_step == 1 && _t > 0.5f) Finish();
                return;
            }

            if (_mode == "hunt")
            {
                if (_step == 0 && _t > 12f) { DumpHugeSprites(); Shot("auto_field_hunt"); _step = 1; _t = 0f; }
                else if (_step == 1 && _t > 1.5f) Finish();
                return;
            }

            if (_mode == "advancement")
            {
                if (_step == 0 && _t > 8f)
                {
                    var metricNames = global::W3Party.FirstAdvancementProbeMetricNames(_advancementJob);
                    var measured = global::W3Party.FirstAdvancementProbeOnActive();
                    bool pass = measured.slot1 > 0f && measured.slot2 > 0f;
                    Debug.Log($"[QA-전직] {_advancementJob} {metricNames[0]}={measured.slot1:F1} " +
                              $"{metricNames[1]}={measured.slot2:F1} 판정={(pass ? "PASS" : "FAIL")}");
                    Shot("qa_advancement"); _step = 1; _t = 0f;
                    if (!pass) Debug.LogError($"[QA-전직] {_advancementJob} 슬롯 계측 0 감지");
                }
                else if (_step == 1 && _t > 0.5f) Finish();
                return;
            }


            if (_mode == "second_advancement")
            {
                if (_step == 0 && _t > 4f)
                {
                    float measured = global::W3Party.UltimateProbeOnActive();
                    int blocked = global::W3Party.QaUltimateBlock;
                    bool pass = global::W3Party.UltimateProbePassed(blocked, measured);
                    Debug.Log($"[QA-2차] {_advancementJob} 초필효과={measured:F1} " +
                              $"차단={blocked} 판정={(pass ? "PASS" : "FAIL")}");
                    Shot("qa_second_advancement");
                    _step = 1; _t = 0f;
                    if (!pass) Debug.LogError($"[QA-2차] {_advancementJob} 초필 게이트 회귀 감지(차단={blocked}, 효과={measured:F1})");
                }
                else if (_step == 1 && _t > 0.5f) Finish();
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

        static void SeedSmithQa()
        {
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            var smith = roster[0];
            if (smith.Advancement == AdvancementTier.Basic)
            {
                smith.Advancement = AdvancementTier.First;
                smith.Job = "수호기사";
                LifeSystem.PersistRoster();
            }
            int need = Equipment.LeatherArmorHideCost * 2;
            int have = GameState.Bag.GetCount(Economy.LifeItem.CraftHide);
            if (have < need) GameState.Gain(Economy.LifeItem.CraftHide, need - have);
            if (Equipment.All.Count == 0) Equipment.TryCraftLeatherArmor();
            if (string.IsNullOrEmpty(smith.EquippedArmorId) && Equipment.Unequipped().Count > 0)
                Equipment.TryEquip(smith, Equipment.Unequipped()[0].Id);
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
