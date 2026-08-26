using System;
using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>
    /// 전투 — W3Party 검증 빌드와 연동. 전투는 자동으로 시작되고
    /// 결과가 나면 결과 화면으로 이동한다.
    /// 보상 정보는 정적 필드에 저장되어 ResultScreen에서 읽는다.
    /// </summary>
    public class BattleScreen : GameScreen
    {
        public const string EnvNoPlayerCopy = "QA_NO_BATTLE_PLAYER_COPY";

        public static string PlayerCopy(string value)
        {
            if (string.IsNullOrEmpty(value)
                || Environment.GetEnvironmentVariable(EnvNoPlayerCopy) == "1")
                return value;
            return System.Text.RegularExpressions.Regex.Replace(
                value, @"\(§[0-9]+(?:-[0-9]+)?(?:[·,]§[0-9]+(?:-[0-9]+)?)*\)", "");
        }

        protected override string Title => FieldBoss.Fighting
            ? FieldBoss.BattleTitle()
            : GameFlow.Kind == GameFlow.BattleKind.보스
            ? RaidBossPool.BattleTitle()
            : GameFlow.Kind == GameFlow.BattleKind.침략 ? "침략" : "전투";
        protected override string Subtitle => PlayerCopy(FieldBoss.Fighting
            ? "필드 배회 보스 — 준비 없이 만나면 위험. 환생석 없음(§10-1·§10-8)"
            : GameFlow.Kind == GameFlow.BattleKind.보스
            ? (string.IsNullOrEmpty(RaidBossPool.PickedLine())
                ? "기믹 3종 — 동시 장판 · 쫄 소환 · 힐 체크. 수동 지휘로 대응한다(§5·§10-5)"
                : RaidBossPool.PickedLine() + " · 기믹은 출현 보스(§9)")
            : GameFlow.Kind == GameFlow.BattleKind.침략
                ? "로컬 별 수비대. PvP 사망은 목숨을 깎지 않는다(§15)"
                : "잡몹은 자동. " + EscapeManual.Line());
        protected override bool ShowBottomBar => false;
        protected override bool ShowHeader => false;
        // 전투 장면을 보여줘야 하므로 배경을 깔지 않는다 — 깔면 카메라 렌더가 통째로 가려진다
        protected override bool OpaqueBackground => false;
        // W3Party 파티 카드가 바닥 전체를 소유한다. 공통 「ESC — 뒤로」 힌트를 그리면
        // 1번 카드 체력바와 겹쳐 회색 글씨가 흰 수치를 뭉갠다(2026-08-19 qa_boss.png). ESC 키는 유효.
        protected override bool ShowEscHint => false;

        float _t;
        global::W3Party _battle;
        static BattleRewardInfo _reward = new BattleRewardInfo();
        bool _defeatApplied;
        bool _leftForSafety;
        bool _sortieRecorded;
        float _bossMaxHp;
        bool _escapeReject;

        void RecordSortie()
        {
            if (_sortieRecorded) return;
            _sortieRecorded = true;
            SortieTime.Apply(_t);
        }

        protected override void Awake()
        {
            base.Awake();

            // 전투 아레나(반경 14)가 화면에 다 들어오게 잡는다.
            // 메뉴 화면 기준(size 8)이면 파티만 크게 잡히고 몰려오는 물량이 안 보인다.
            var cam = Camera.main;
            if (cam != null)
            {
                // 기본 13(c77b782f: 아레나가 다 들어와야 물량이 보인다) ↔ 캐릭터 가독성이
                // 충돌한다(오너 2026-08-18 「캐릭터 안 보인다」). QA_ORTHO로 값을 바꿔
                // **실측 비교**한 뒤 기본값을 정한다 — 감으로 정하지 않는다.
                // 실측으로 정했다: 화면 높이 720px, 캐릭터 2.0u.
                //   ortho 13 → 720/26×2 = 55px = 화면 높이의 7.7%
                //   ortho 10 → 720/20×2 = 72px = 10.0%
                // 주인공이 화면 높이의 10% 아래로 내려가면 실루엣이 잡몹 무리에 묻힌다.
                // 물량 가시성(c77b782f)은 아레나 반경이 결정하지 ortho가 결정하지 않는다.
                float ortho = 10f;
                var envOrtho = System.Environment.GetEnvironmentVariable("QA_ORTHO");
                if (!string.IsNullOrEmpty(envOrtho) && float.TryParse(envOrtho, out float eo)) ortho = eo;
                cam.orthographicSize = ortho;
                cam.transform.position = new Vector3(0, 0, -10);
            }

            // 보스전이면 기믹 3종이 도는 판을 얹는다(§9·§10-5).
            // 잡몹 웨이브와 달리 §5가 "보스는 수동 지휘"라 한 구간이다.
            BossBattle activeBoss = null;
            if (GameFlow.Kind == GameFlow.BattleKind.보스)
            {
                if (!FieldBoss.Fighting)
                {
                    RaidBossPool.SeedQaIfRequested();
                    if (RaidBossPool.PickedFloor <= 0)
                        RaidBossPool.Pick(GameFlow.BossFloor);
                }
                var boss = gameObject.AddComponent<BossBattle>();
                activeBoss = boss;
                boss.OnBossDefeated += _ =>
                {
                    // 보스 격파 — 보상 계산 (§2 코어 루프: 재화 획득)
                    CalculateVictoryReward(GameFlow.BossFloor);
                    // 층을 실제로 돌파한다. 진행도가 안 오르면 §8의 "벽 콘텐츠"가 성립하지 않고
                    // §10-6의 티어 상승(10층마다)도 영원히 일어나지 않는다.
                    // 종단 SelfCheck가 같은 경계를 부른다 — 여기만 ClearFloor를 쓰면
                    // 검사가 생산 경로를 못 증명한다.
                    // 필드 배회 보스는 탑 층이 아니다(§10-1).
                    if (!FieldBoss.Fighting)
                        GameFlow.ApplyTowerBossVictory(GameFlow.BossFloor);
                    // 던전 종점 보스는 **런의 끝**이다 — 노드 맵으로 돌아가 클리어를 보여준다.
                    // 탑 레이드는 기존대로 결과 화면으로 간다(§8 벽 콘텐츠는 층 진행이 결과다).
                    if (DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon)
                    {
                        RecordSortie();
                        DungeonRun.Complete(true);
                        GameFlow.LastBattleSummary = $"던전 보스 격파 ({_t:F1}초)";
                        GameFlow.Go(GameFlow.Dungeon);
                        return;
                    }
                    RecordSortie();
                    GameFlow.LastBattleSummary = PlayerCopy(FieldBoss.Fighting
                        ? $"필드 배회 보스 격파 — {FieldBoss.Name()}(§10-1)"
                        : $"보스 격파 — {GameFlow.BossFloor}층 ({_t:F1}초) · 다음 {GameState.TowerFloor}층");
                    GameFlow.Go(GameFlow.Result);
                };
                boss.OnPartyWiped += () =>
                {
                    RecordSortie();
                    ApplyDefeatOnce($"보스전 패배 — {GameFlow.BossFloor}층");
                    if (DungeonRun.Active) DungeonRun.End();   // ✅ §7 나가면 초기화
                    GameFlow.Go(GameFlow.Result);
                };
                // 마릿수: 던전은 계획이 60/30/10. 탑 대보스는 BossCount(§10-7).
                // 옛 길은 탑을 1로 고정해 던전만 다중 등장이었다. 배회는 💡라 1.
                bool dungeonBoss = DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon;
                // 던전 종점 보스는 몬스터문서 §7의 **75초** 기준이다(탑 층수 스케일이 아니다).
                // 하위 레이드는 §18-10 계수 0.65로 목표 시간만 덮는다. BossBattle은 안 고친다.
                // 출현 층은 풀 추첨(§9). 골드·경험은 입장 층, HP 시간은 입장 층 스케일.
                float raidTime = dungeonBoss ? 75f : RaidScale.TargetSeconds(GameFlow.BossFloor);
                int fightFloor = dungeonBoss || FieldBoss.Fighting
                    ? GameFlow.BossFloor
                    : RaidBossPool.FightFloor;
                int bossCount = dungeonBoss
                    ? DungeonRun.Plan.BossCount
                    : BossCount.Begin(fightFloor);
                boss.Begin(fightFloor, bossCount, raidTime);
                _bossMaxHp = BossBattle.ActiveTotalHp;
            }

            // W3Party 컴포넌트 획득 또는 생성
            _battle = GetComponent<global::W3Party>();
            if (_battle == null)
                _battle = gameObject.AddComponent<global::W3Party>();

            // 게임 모드 설정: 표준 5인 한 판만 실행
            _battle.GameMode = true;
            HuntStart.SeedQaIfRequested();
            if (HuntStart.ShouldHold) _battle.CombatHeld = true;
            _battle.ApplyGameParty();
            if (HuntStart.Deploying)
            {
                for (int i = 0; i < _battle.PartyCount; i++)
                    _battle.TrySetPartyPos(i, HuntStart.PosOf(i));
            }
            activeBoss?.AttachCombatTargets();

            // 필드 전투에 실제로 막히는 엄폐물을 켠다(§10-2). **GameMode 대입 뒤에** 부른다 —
            // W3Party.Awake 시점엔 GameMode가 아직 false라 거기서 켜면 조용히 꺼진다.
            // 검증(W1~W3)은 BattleScreen을 타지 않으므로 빈 판이 그대로 유지된다.
            _battle.EnableFieldCover();

            if (DungeonRun.Active && DungeonRun.State != null)
                HuntBoon.BindDungeon(DungeonRun.State.Boons, DungeonRun.Plan.RunSeed);
            else
                HuntBoon.BeginField((uint)(20260817 ^ GameState.Tier * 2654435761u));

            // §10-2 정예 처치 → 다음 웨이브 드랍. 이번 웨이브는 시작 시점 FieldKills.
            if (GameFlow.Kind == GameFlow.BattleKind.잡몹웨이브)
                EliteWaveDrop.BeginWave();

            // 던전 노드는 **편성이 계획에서 온다**(§3-5 밀도 곡선). 여기서 꽂지 않으면
            // 어느 노드를 들어가든 같은 판이 돌아 "던전이 매번 바뀐다"가 거짓말이 된다.
            var wave = DungeonRun.PendingWave();
            if (GameFlow.Kind == GameFlow.BattleKind.던전 && wave != null)
            {
                _battle.시작웨이브 = Mathf.Max(1, wave.StartCount);
                _battle.최대시간 = Mathf.Max(20f, wave.DurationSec);
                // 목표 동시 몹 수까지 DurationSec 동안 선형으로 올린다 —
                // 웨이브 단계 수를 먼저 정하고 그 수로 증가폭을 나눈다.
                const int steps = 5;
                _battle.점증간격 = Mathf.Max(1f, wave.DurationSec / steps);
                _battle.단계당증가 = Mathf.Max(1, (wave.TargetCount - wave.StartCount) / steps);
                Debug.Log($"[던전] 노드 편성 주입: 시작 {wave.StartCount} → 목표 {wave.TargetCount} " +
                          $"/ {wave.DurationSec:F0}s (단계 {steps})");
            }

            // 전투 종료 콜백: 결과 저장 및 화면 이동
            _battle.OnBattleEnd = OnBattleEnd;
        }

        protected override void Update()
        {
            if (HuntStart.Deploying)
            {
                HandleDeployInput();
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    HuntStart.Cancel();
                    GameFlow.Go(GameFlow.Field);
                }
                return;
            }
            // 공통 ESC는 영지로 공짜 이동한다. 전투 귀환은 §4 두루마리 + 6초 캐스트.
            if (Input.GetKeyDown(KeyCode.Escape) && !EmergencyEscape.Casting)
            {
                if (EscapeManual.Allowed() && EmergencyEscape.TryBegin())
                    _escapeReject = false;
                else
                    _escapeReject = !EscapeManual.Allowed();
            }
            _t += Time.deltaTime;
        }

        void HandleDeployInput()
        {
            if (_battle == null) return;
            for (int i = 0; i < _battle.PartyCount && i < 5; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) HuntStart.Select(i);
            if (!Input.GetMouseButtonDown(0) || OverDeployStart(Input.mousePosition)) return;
            Vector2 world = _battle.ScreenToArena(Input.mousePosition);
            int hit = _battle.HitParty(world);
            if (hit >= 0) HuntStart.Select(hit);
            else if (HuntStart.Selected >= 0
                     && HuntStart.TryPlace(HuntStart.Selected, world))
                _battle.TrySetPartyPos(HuntStart.Selected, world);
        }

        static bool OverDeployStart(Vector3 mouse)
        {
            float s = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float x = mouse.x / s;
            float y = (Screen.height - mouse.y) / s;
            return new Rect(460f, 608f, 360f, 64f).Contains(new Vector2(x, y));
        }

        bool TryConfirmDeploy()
        {
            if (!HuntStart.ConfirmStart()) return false;
            _battle?.ReleaseCombat();
            return true;
        }

        void LateUpdate()
        {
            bool hit = Time.time - global::W3Party.LastPartyDamageAt < 0.08f;
            var phase = EmergencyEscape.Tick(Time.deltaTime, hit);
            if (phase == EmergencyEscape.Phase.Escaped)
                LeaveByEscape();

            if (_leftForSafety || _defeatApplied) return;
            bool watch = LowHpReturn.ShouldWatch(GameFlow.Kind, GameFlow.ReturnTo);
            var leave = LowHpReturn.Tick(
                Time.deltaTime, global::W3Party.ActivePartyLowestHpRatio, watch);
            if (leave == LowHpReturn.Phase.Left) LeaveForLowHp();
        }

        protected override void Overlay()
        {
            if (HuntStart.Deploying)
            {
                var banner = new Rect(280f, 16f, 720f, 52f);
                UiAtlas.DrawSliced(banner, "panel", 8f, new Color(1f, 1f, 1f, 0.9f));
                Hint(UiAtlas.ContentRect(banner, "panel", 2f), HuntStart.DeployTitle);
                Hint(new Rect(banner.x, banner.yMax + 4f, banner.width, 22f), HuntStart.DeployHint);
                var start = new Rect(460f, 608f, 360f, 64f);
                if (DrawCard(start, "스타트", "배치를 끝내고 전투를 시작한다", "field"))
                    TryConfirmDeploy();
                return;
            }

            float y = 10f;
            if (BossBattle.IsActive && _bossMaxHp > 0f)
            {
                var bar = new Rect(360f, y, 560f, 36f);
                int phases = UiAtlas.PhaseCountForFloor(RaidBossPool.FightFloor);
                UiAtlas.DrawBossHp(bar, BossBattle.ActiveTotalHp, _bossMaxHp, phases);
                Hint(new Rect(bar.x, bar.yMax + 2f, bar.width, 20f),
                    $"{RaidBossPool.BattleHint()}  {BossBattle.ActiveTotalHp:0}/{_bossMaxHp:0}  페이즈 {phases}");
                y = bar.yMax + 26f;
            }
            if (LowHpReturn.Leaving)
            {
                var leave = new Rect(340f, y + 8f, 600f, 28f);
                UiAtlas.DrawSliced(leave, "panel", 8f, new Color(1f, 1f, 1f, 0.88f));
                Hint(UiAtlas.ContentRect(leave, "panel", 2f),
                    PlayerCopy($"저체력 귀환 {LowHpReturn.Remaining:0.0}초 — 피격 가능 · 이번 판 보상 없음(§4)"));
                float fill = 1f - Mathf.Clamp01(LowHpReturn.Remaining / LowHpReturn.LeaveSeconds);
                GUI.Box(new Rect(leave.x, leave.y, leave.width * Mathf.Max(0.02f, fill), leave.height), "");
                y = leave.yMax + 8f;
            }
            if (DrawHuntBoonPick()) return;
            if (_escapeReject && !EmergencyEscape.Casting && !EscapeManual.Allowed())
            {
                var deny = new Rect(340f, y + 8f, 600f, 28f);
                UiAtlas.DrawSliced(deny, "panel", 8f, new Color(1f, 1f, 1f, 0.88f));
                Hint(UiAtlas.ContentRect(deny, "panel", 2f), EscapeManual.WhyNot());
                y = deny.yMax + 8f;
            }
            if (!EmergencyEscape.Casting) return;
            float p = EmergencyEscape.Progress;
            var box = new Rect(340f, y + 8f, 600f, 28f);
            UiAtlas.DrawSliced(box, "panel", 8f, new Color(1f, 1f, 1f, 0.88f));
            Hint(UiAtlas.ContentRect(box, "panel", 2f),
                $"귀환 {(EmergencyEscape.CastSeconds - EmergencyEscape.Elapsed):0.0}초 — 피격 시 시전 취소");
            GUI.Box(new Rect(box.x, box.y, box.width * Mathf.Max(0.02f, p), box.height), "");
        }

        bool DrawHuntBoonPick()
        {
            if (!HuntBoon.Waiting) return false;
            // 전체 화면 dim은 쓰지 않는다 — 카드가 뜬 동안에도 전장이 보여야 한다(§16).
            // 대신 카드를 아래 도크에 붙이고 반투명하게 그린다.
            var dock = HuntBoon.Dock(new Rect(0f, 0f, 1280f, 720f));
            // 높이 48 미만으로 줄이지 마라 — panel 금테(0.24)가 안쪽을 먹어 18px 글씨가 반토막 난다.
            var banner = new Rect(240f, dock.y - 56f, 800f, 48f);
            UiAtlas.DrawSliced(banner, "panel", 8f, new Color(1f, 1f, 1f, 0.92f * HuntBoon.CardAlpha));
            Hint(UiAtlas.ContentRect(banner, "panel", 2f),
                $"이 판 강화 {HuntBoon.Owned.Count}/8 — 나가면 사라진다. 고를 때까지 전투가 멈춘다");
            var picks = HuntBoon.Offered;
            var area = HuntBoon.PickBand(dock);
            var cells = UiPages.Grid(area, Mathf.Max(1, picks.Count), 1, HuntBoon.CardGap);
            for (int i = 0; i < picks.Count && i < cells.Length; i++)
            {
                var d = Boons.Def(picks[i]);
                if (DrawCard(cells[i], d.Name, d.Desc, HuntBoon.IconOf(picks[i]),
                             alpha: HuntBoon.CardAlpha))
                {
                    HuntBoon.Take(picks[i]);
                    _battle?.RefreshHuntBoons();
                }
            }
            return true;
        }

        void OnDisable()
        {
            HuntBoon.LeaveBattle();
        }

        void LeaveForLowHp()
        {
            if (_leftForSafety || _defeatApplied) return;
            _leftForSafety = true;
            RecordSortie();
            _reward.Clear();
            GameFlow.LastBattleSummary = PlayerCopy("저체력 귀환 — 이번 판 보상 없음(§4)");
            GameFlow.Go(GameFlow.Estate);
        }

        void LeaveByEscape()
        {
            if (_leftForSafety || _defeatApplied) return;
            _leftForSafety = true;
            RecordSortie();
            EscapeForfeit.Apply(_reward);
            // QA_NO·옛 경로는 결과 없이 영지. 소비처가 있을 때만 포기를 보여 준다.
            GameFlow.Go(EscapeForfeit.Blocked ? GameFlow.Estate : GameFlow.Result);
        }

        /// <summary>
        /// 전투 승리 시 보상을 계산한다 (§2·§18-1·§10-8·§18-4)
        /// </summary>
        void CalculateVictoryReward(int bossFloor)
        {
            _reward.Clear();
            _reward.Survived = true;
            _reward.BattleDurationSeconds = _t;

            // 골드 지급 (§18-1 티어별 수익 곡선). 하위 레이드는 §18-10 계수 0.65.
            // 첫 클리어는 층/10 레거시를 유지한다.
            _reward.GoldReward = RaidScale.Gold(bossFloor);

            // 드랍 출처 (§10-8). 던전 보스와 탑 보스는 **드랍 테이블이 다르다** —
            // ✅ §7·§10-8: 환생석·전직 증표는 탑 등반의 고유 가치라 던전에서는 나오지 않는다.
            // 예전에는 던전에서 이겨도 탑 보스 테이블로 굴려 환생석이 나올 수 있었다.
            bool inDungeon = DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon;
            int dropFloor = FieldBoss.Fighting || inDungeon ? 0 : RaidBossPool.FightFloor;
            Economy.DropSource dropSource =
                FieldBoss.Fighting
                    ? FieldBoss.DropSource
                    : inDungeon
                    ? (DungeonRun.Plan.Kind == DungeonKind.레이드급
                        ? Economy.DropSource.RaidDungeon
                        : Economy.DropSource.FieldDungeonBoss)
                    : RaidBossPool.DropSourceFor(dropFloor);

            // 골드를 **실제로 지갑에 넣는다**. 계산만 하고 반영하지 않으면
            // §2의 순환("번 돈으로 다음 판에 들어간다")이 성립하지 않는다.
            // §18-14 소프트캡은 Earn이 읽는다. 화면에 찍는 금액도 캡 뒤 값이다.
            _reward.GoldReward = GameState.Earn(_reward.GoldReward);

            // ✅ §3 경험치 분배 — 출전 파티가 레벨 비례로 나눠 갖는다(§18-6 성장 곡선).
            // 여기(승리 해소)에서 골드와 함께 딱 1회 지급된다 — ResultScreen은 표시만 한다.
            // ⚠️ 절대 총량(battleExp)은 프로토타입 기준값이다: §18-6의 육성 소요시간 앵커
            //    (Lv20≈2h 등)에 맞춘 보정은 전투 빈도·시간 데이터가 필요해 유보한다.
            //    ✅ 확정인 것은 "분배가 레벨 비례"(§3)와 "곡선 100×Lv^2.2"(§18-6)이고, 둘 다 정확하다.
            long battleExp = RaidScale.Exp(bossFloor);
            if (battleExp < 1) battleExp = 1;
            _reward.ExpGains = LifeSystem.AwardBattleExp(battleExp);

            // §10-8 판정 규칙대로 굴린다 — 일반 드랍은 보스 개체별, 희귀 고유템은 전투당 1회.
            // 예전에는 테이블 전체를 3회 굴려 **환생석 기대값이 3배**가 됐다.
            // 그러면 §18-4의 "리롤 노가다는 수지가 안 맞는다" 검산이 통째로 무너진다.
            int bossCount = inDungeon ? DungeonRun.Plan.BossCount : BossCount.Fight;
            uint dropSeed = inDungeon
                ? DungeonRun.Plan.RunSeed
                : (uint)(bossFloor * 2654435761u ^ (uint)System.DateTime.UtcNow.Ticks);
            var dropRng = Rng.Stream(dropSeed, bossFloor, SeedChannel.Drop);
            foreach (var drop in Economy.RollBattleDrops(dropSource, bossCount, ref dropRng,
                         dropFloor))
            {
                // 상한 판정은 **실제 소지품**이 한다(§18-4). 예전엔 보유량을 0으로 두고
                // 판정해 상한이 영원히 안 걸렸다 — 상한이 있다는 말만 있고 없는 것과 같았다.
                if (GameState.Gain(drop)) _reward.DroppedItems.Add(drop);
                else _reward.RejectedItems.Add(drop);   // 소실이 아니라 획득 거부
            }

            // §10-8 보스 주 드랍 = 고급 장비. 잡몹 생존 경로는 안 탄다.
            var gear = GearDrop.Apply(dropSource, ref dropRng);
            if (gear != null) _reward.DroppedGear.Add(GearDrop.Format(gear));
        }

        void OnBattleEnd(bool survived)
        {
            if (_leftForSafety) return;
            RecordSortie();
            if (GameFlow.Kind == GameFlow.BattleKind.침략)
            {
                string repeatLine = InvasionState.RepeatLootLine();
                int repeatPct = InvasionState.RepeatPercent();
                long loot = InvasionState.Settle(survived);
                if (survived)
                {
                    string lootLine = InvasionState.RaceLootPercent() == InvasionState.BeastLootPercent
                        ? " · " + InvasionState.RaceLootLine()
                        : "";
                    if (repeatPct != InvasionState.RepeatFirstPercent)
                        lootLine += " · " + repeatLine;
                    string honorLine = Honor.Blocked ? "" : " · " + Honor.WinLine();
                    GameFlow.LastBattleSummary =
                        $"침략 성공 — 약탈 {EstateStatusHud.ShortCopper(loot)}{lootLine}{honorLine} ({_t:F1}초)";
                }
                else
                    ApplyDefeatOnce($"침략 패배 — {_t:F1}초", isPvp: true);
                GameFlow.Go(GameFlow.Result);
                return;
            }

            // 던전 안이면 결과 화면이 아니라 **노드 맵으로 돌아간다** — 런이 계속되기 때문이다.
            // 진 경우에만 런을 끝낸다(사망 기록은 아래 공통 경로가 처리한다).
            bool inDungeon = DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon;
            if (inDungeon && survived)
            {
                DungeonRun.Complete(true);
                long nodeExp = Economy.WaveHuntExp(GameState.Tier, _t);
                LifeSystem.AwardWaveHunt(_t);
                string elite = string.IsNullOrEmpty(EliteDrop.LastLine)
                    ? "" : " · " + EliteDrop.Line();
                GameFlow.LastBattleSummary = $"노드 통과 — {_t:F1}초 · EXP {nodeExp}{elite}";
                GameFlow.Go(GameFlow.Dungeon);
                return;
            }

            if (survived)
            {
                // ✅ §8 탑 등반 — 탑에서 온 잡몹웨이브("다음 층 도전")를 버티면 **한 층 오른다**.
                // ClearFloor는 도입 이래 보스 격파(OnBossDefeated) 경로에서만 불렸다 — 그래서
                // 정작 층수의 대부분인 일반 층은 이겨도 진행도가 안 올라, "다음 층 도전" 버튼이
                // 라벨과 달리 같은 층을 무한 반복했다(§8 벽 콘텐츠·§10-6 티어·§15 침략 게이트가
                // 열리지 않음 = "눌러도 규칙을 무시하는" 거짓 버튼). BossFloor엔 입장 시 층(TowerFloor)이
                // 담겨 있다. 보스전은 위 OnBossDefeated가 따로 올리므로 IsTowerFloorClear가 잡몹웨이브만
                // 참으로 걸러 이중 상승을 막는다.
                if (GameFlow.IsTowerFloorClear(survived, inDungeon, GameFlow.ReturnTo, GameFlow.Kind))
                {
                    GameState.ClearFloor(GameFlow.BossFloor);
                    FloorRecruit.OnCleared(GameFlow.BossFloor);
                    long floorExp = Economy.WaveHuntExp(GameState.Tier, _t);
                    _reward.Clear();
                    _reward.Survived = true;
                    _reward.BattleDurationSeconds = _t;
                    _reward.ExpGains = LifeSystem.AwardWaveHunt(_t);
                    GameFlow.LastBattleSummary =
                        $"{GameFlow.BossFloor}층 돌파 — {_t:F1}초 · 다음 {GameState.TowerFloor}층 · EXP {floorExp}";
                }
                else
                {
                    // 필드 사냥은 보스 테이블을 안 굴린다. 가죽은 여기서만 준다(§11).
                    // 경험치는 가죽과 같이 생존 정산에서만 준다 — 예전엔 0이라 테스터 레벨이 안 올랐다.
                    if (GameFlow.ReturnTo == GameFlow.Field)
                    {
                        _reward.Clear();
                        _reward.Survived = true;
                        _reward.BattleDurationSeconds = _t;
                        int hides = Economy.FieldHuntHideCount();
                        if (hides > 0 && GameState.Gain(Economy.LifeItem.CraftHide, hides))
                        {
                            for (int i = 0; i < hides; i++)
                                _reward.DroppedItems.Add(Economy.LifeItem.CraftHide);
                        }
                        long huntGold = Economy.WaveHuntGold(GameState.Tier, _t);
                        _reward.GoldReward = GameState.Earn(huntGold);
                        long huntExp = Economy.WaveHuntExp(GameState.Tier, _t);
                        _reward.ExpGains = LifeSystem.AwardWaveHunt(_t);
                        GameFlow.LastBattleSummary =
                            $"생존 — {_t:F1}초 · 사냥 가죽 {hides}장 · {EstateStatusHud.ShortCopper(_reward.GoldReward)} · EXP {huntExp}";
                    }
                    else
                        GameFlow.LastBattleSummary = $"생존 — {_t:F1}초";
                }
            }
            else
            {
                // 힐체크 실패(OnPartyWiped)가 먼저 목숨을 깎았을 수 있다 — 한 판에 한 번만.
                ApplyDefeatOnce($"전멸 — {_t:F1}초 생존");
            }

            // 던전에서 전멸하면 런은 거기서 끝난다(✅ §7 나가면 초기화)
            if (DungeonRun.Active && !survived) DungeonRun.End();

            GameFlow.Go(GameFlow.Result);
        }

        void ApplyDefeatOnce(string head, bool isPvp = false)
        {
            if (_defeatApplied) return;
            _defeatApplied = true;
            var report = GameFlow.ApplyPveDefeat(isPvp);
            GameFlow.LastBattleSummary = GameFlow.FormatDefeatSummary(head, report);
        }

        protected override void Body(Rect r)
        {
            // 전투 HUD는 W3Party가 전체 안전 영역을 소유한다. 여기서 공통 행을 그리면
            // 우측 보상 레인과 중앙 전장을 다시 가리게 된다. 귀환은 ESC로 유지한다.
        }

        /// <summary>
        /// ResultScreen이 보상 정보를 읽기 위한 접근자
        /// </summary>
        public static BattleRewardInfo _GetLastReward() => _reward;

        /// <summary>QA_RACE_DROP=1이면 수인 + 가죽 1장으로 결과 화면을 연다.</summary>
        public static void SeedRaceDropRewardQaIfRequested()
        {
            if (System.Environment.GetEnvironmentVariable(Economy.EnvShowDrop) != "1") return;
            RacePrefs.Set(RaceId.수인);
            // 앞선 SelfCheck가 _reward에 가죽이 아닌 드랍을 남긴 채 승리·수인 상태로 두면
            // Count>0만 보던 옛 가드가 재시드를 건너뛰어 「시드 가죽 1장」이 FAIL이었다
            // (형제 SeedRaceAdvMatRewardQaIfRequested는 특정 아이템 Contains를 본다 — 그와 맞춘다).
            if (_reward.Survived && _reward.DroppedItems != null
                && _reward.DroppedItems.Contains(Economy.LifeItem.CraftHide)
                && Economy.RaceDropLine().Contains("+15%"))
                return;
            _reward.Clear();
            _reward.Survived = true;
            _reward.DroppedItems.Add(Economy.LifeItem.CraftHide);
            if (string.IsNullOrEmpty(GameFlow.LastBattleSummary)
                || !GameFlow.LastBattleSummary.Contains("+15%"))
                GameFlow.LastBattleSummary = "생존 — " + Economy.RaceDropLine();
        }

        /// <summary>QA_RACE_ADV=1이면 인간 + 전직 재료 1개로 결과 화면을 연다.</summary>
        public static void SeedRaceAdvMatRewardQaIfRequested()
        {
            if (System.Environment.GetEnvironmentVariable(Economy.EnvShowAdvMat) != "1") return;
            RacePrefs.Set(RaceId.인간);
            if (_reward.Survived && _reward.DroppedItems != null
                && _reward.DroppedItems.Contains(Economy.LifeItem.AdvancementMaterial)
                && Economy.RaceAdvMatLine().Contains("+15%"))
                return;
            _reward.Clear();
            _reward.Survived = true;
            _reward.DroppedItems.Add(Economy.LifeItem.AdvancementMaterial);
            if (string.IsNullOrEmpty(GameFlow.LastBattleSummary)
                || !GameFlow.LastBattleSummary.Contains("전직 재료"))
                GameFlow.LastBattleSummary = "생존 — " + Economy.RaceAdvMatLine();
        }

        /// <summary>QA_HUNT_EXP=1이면 결과 화면에 필드 생존 경험치 줄을 심는다.</summary>
        public static void SeedHuntExpRewardQaIfRequested()
        {
            if (System.Environment.GetEnvironmentVariable("QA_HUNT_EXP") != "1") return;
            LifeSystem.SeedHuntExpQaIfRequested();
            if (_reward.ExpGains != null && _reward.ExpGains.Count > 0) return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0) return;
            long exp = Economy.WaveHuntExp(GameState.Tier, 274f);
            _reward.Clear();
            _reward.Survived = true;
            _reward.BattleDurationSeconds = 274f;
            _reward.ExpGains.Add($"{roster[0].Name} +{exp} EXP → Lv.{roster[0].Level}");
            if (string.IsNullOrEmpty(GameFlow.LastBattleSummary))
                GameFlow.LastBattleSummary = $"생존 — 274.0초 · 사냥 가죽 1장 · EXP {exp}";
        }

        /// <summary>QA_GEAR_DROP=1이면 결과 화면에 고급 흉갑 줄을 심는다.</summary>
        public static void SeedGearDropRewardQaIfRequested()
        {
            GearDrop.SeedQaIfRequested();
            if (!GearDrop.ShowQa) return;
            if (_reward.DroppedGear != null && _reward.DroppedGear.Count > 0) return;
            string line = GearDrop.LastLine;
            if (string.IsNullOrEmpty(line))
            {
                var bag = Equipment.Unequipped();
                for (int i = 0; i < bag.Count; i++)
                {
                    if (bag[i].Grade == GearDrop.BossGrade)
                    {
                        line = GearDrop.Format(bag[i]);
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(line)) return;
            _reward.Survived = true;
            _reward.DroppedGear.Add(line);
            if (string.IsNullOrEmpty(GameFlow.LastBattleSummary)
                || !GameFlow.LastBattleSummary.Contains("고급"))
                GameFlow.LastBattleSummary = "생존 — " + GearDrop.Line();
        }

        /// <summary>QA_HUNT_GOLD=1이면 결과 화면에 T1 1시간 = 1골드를 심는다.</summary>
        /// <summary>QA_INVASION_LOOT=1이면 결과 화면에 침략 성공 약탈 줄을 심는다.</summary>
        public static void SeedInvasionLootQaIfRequested()
        {
            if (System.Environment.GetEnvironmentVariable("QA_INVASION_LOOT") != "1") return;
            GameFlow.LastBattleSummary =
                $"침략 성공 — 약탈 {EstateStatusHud.ShortCopper(50_000)} (1.0초)";
        }

        public static void SeedHuntGoldRewardQaIfRequested()
        {
            if (System.Environment.GetEnvironmentVariable(Economy.EnvShowHuntGold) != "1") return;
            Economy.SeedHuntGoldQaIfRequested();
            long gold = Economy.WaveHuntGold(GameState.Tier, Economy.HuntGoldHourSeconds);
            gold = GameState.Earn(gold);
            _reward.Clear();
            _reward.Survived = true;
            _reward.BattleDurationSeconds = Economy.HuntGoldHourSeconds;
            _reward.GoldReward = gold;
            GameFlow.LastBattleSummary =
                $"생존 — {Economy.HuntGoldHourSeconds:0.0}초 · 사냥 가죽 0장 · {EstateStatusHud.ShortCopper(gold)} · EXP 0";
        }
    }
}
