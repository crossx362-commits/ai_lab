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
        protected override string Title => GameFlow.Kind == GameFlow.BattleKind.보스
            ? $"보스전 · {GameFlow.BossFloor}층" : "전투";
        protected override string Subtitle => GameFlow.Kind == GameFlow.BattleKind.보스
            ? "기믹 3종 — 동시 장판 · 쫄 소환 · 힐 체크. 수동 지휘로 대응한다(§5·§10-5)"
            : "잡몹은 자동. 1~5로 선택하고 우클릭으로 이동 지시(§5)";
        protected override bool ShowBottomBar => false;
        // 전투 장면을 보여줘야 하므로 배경을 깔지 않는다 — 깔면 카메라 렌더가 통째로 가려진다
        protected override bool OpaqueBackground => false;

        float _t;
        global::W3Party _battle;
        static BattleRewardInfo _reward = new BattleRewardInfo();

        protected override void Awake()
        {
            base.Awake();

            // 전투 아레나(반경 14)가 화면에 다 들어오게 잡는다.
            // 메뉴 화면 기준(size 8)이면 파티만 크게 잡히고 몰려오는 물량이 안 보인다.
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographicSize = 15f;
                cam.transform.position = new Vector3(0, 0, -10);
            }

            // 보스전이면 기믹 3종이 도는 판을 얹는다(§9·§10-5).
            // 잡몹 웨이브와 달리 §5가 "보스는 수동 지휘"라 한 구간이다.
            if (GameFlow.Kind == GameFlow.BattleKind.보스)
            {
                var boss = gameObject.AddComponent<BossBattle>();
                boss.OnBossDefeated += _ =>
                {
                    // 보스 격파 — 보상 계산 (§2 코어 루프: 재화 획득)
                    CalculateVictoryReward(GameFlow.BossFloor);
                    // 층을 실제로 돌파한다. 진행도가 안 오르면 §8의 "벽 콘텐츠"가 성립하지 않고
                    // §10-6의 티어 상승(10층마다)도 영원히 일어나지 않는다.
                    GameState.ClearFloor(GameFlow.BossFloor);
                    // 던전 종점 보스는 **런의 끝**이다 — 노드 맵으로 돌아가 클리어를 보여준다.
                    // 탑 레이드는 기존대로 결과 화면으로 간다(§8 벽 콘텐츠는 층 진행이 결과다).
                    if (DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon)
                    {
                        DungeonRun.Complete(true);
                        GameFlow.LastBattleSummary = $"던전 보스 격파 ({_t:F1}초)";
                        GameFlow.Go(GameFlow.Dungeon);
                        return;
                    }
                    GameFlow.LastBattleSummary =
                        $"보스 격파 — {GameFlow.BossFloor}층 ({_t:F1}초) · 다음 {GameState.TowerFloor}층";
                    GameFlow.Go(GameFlow.Result);
                };
                boss.OnPartyWiped += () =>
                {
                    GameFlow.LastBattleSummary = $"보스전 패배 — {GameFlow.BossFloor}층";
                    if (DungeonRun.Active) DungeonRun.End();   // ✅ §7 나가면 초기화
                    GameFlow.Go(GameFlow.Result);
                };
                // 마릿수는 던전 계획이 §10-7 확률(60/30/10)로 이미 뽑아뒀다 —
                // 여기서 다시 1로 고정하면 그 결정이 화면에 도달하지 못한다.
                bool dungeonBoss = DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon;
                int bossCount = dungeonBoss ? DungeonRun.Plan.BossCount : 1;
                // 던전 종점 보스는 몬스터문서 §7의 **75초** 기준이다(탑 층수 스케일이 아니다)
                boss.Begin(GameFlow.BossFloor, bossCount, dungeonBoss ? 75f : 0f);
            }

            // W3Party 컴포넌트 획득 또는 생성
            _battle = GetComponent<global::W3Party>();
            if (_battle == null)
                _battle = gameObject.AddComponent<global::W3Party>();

            // 게임 모드 설정: 표준 5인 한 판만 실행
            _battle.GameMode = true;

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
            base.Update();
            _t += Time.deltaTime;
        }

        /// <summary>
        /// 전투 승리 시 보상을 계산한다 (§2·§18-1·§10-8·§18-4)
        /// </summary>
        void CalculateVictoryReward(int bossFloor)
        {
            _reward.Clear();
            _reward.Survived = true;
            _reward.BattleDurationSeconds = _t;

            // 티어 결정 (§10-6: 탑 10층 돌파마다 필드 티어 상승)
            // 프로토타입이므로 단순화: 층수 / 10을 티어로 (1~9층 = T0, 10~19층 = T1 등)
            int tier = Mathf.Max(0, bossFloor / 10);
            if (tier >= Economy.TierRevenueMultiplier.Length)
                tier = Economy.TierRevenueMultiplier.Length - 1;

            // 골드 지급 (§18-1 티어별 수익 곡선)
            // 기본값: 티어별 1시간 수익 = TierRevenueMultiplier * 10,000 쿠퍼 (1 G/h = 10,000 쿠퍼)
            // 보스 보상: 기본값의 약 15~20% (한판 15분 기준)
            float tierRevenue = Economy.TierRevenueMultiplier[tier];
            long baseGoldPerHour = (long)(tierRevenue * 10000); // 1시간 수익(쿠퍼)
            float battleRewardRatio = 0.25f; // 보스는 1시간 수익의 25% (15분 기준)
            _reward.GoldReward = (long)(baseGoldPerHour * battleRewardRatio);

            // 드랍 출처 (§10-8). 던전 보스와 탑 보스는 **드랍 테이블이 다르다** —
            // ✅ §7·§10-8: 환생석·전직 증표는 탑 등반의 고유 가치라 던전에서는 나오지 않는다.
            // 예전에는 던전에서 이겨도 탑 보스 테이블로 굴려 환생석이 나올 수 있었다.
            bool inDungeon = DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon;
            Economy.DropSource dropSource =
                inDungeon
                    ? (DungeonRun.Plan.Kind == DungeonKind.레이드급
                        ? Economy.DropSource.RaidDungeon
                        : Economy.DropSource.FieldDungeonBoss)
                    : (bossFloor % 10 == 0 ? Economy.DropSource.Tower10Boss
                                           : Economy.DropSource.Tower5Boss);

            // 골드를 **실제로 지갑에 넣는다**. 계산만 하고 반영하지 않으면
            // §2의 순환("번 돈으로 다음 판에 들어간다")이 성립하지 않는다.
            GameState.Earn(_reward.GoldReward);

            // §10-8 판정 규칙대로 굴린다 — 일반 드랍은 보스 개체별, 희귀 고유템은 전투당 1회.
            // 예전에는 테이블 전체를 3회 굴려 **환생석 기대값이 3배**가 됐다.
            // 그러면 §18-4의 "리롤 노가다는 수지가 안 맞는다" 검산이 통째로 무너진다.
            int bossCount = inDungeon ? DungeonRun.Plan.BossCount : 1;
            uint dropSeed = inDungeon
                ? DungeonRun.Plan.RunSeed
                : (uint)(bossFloor * 2654435761u ^ (uint)System.DateTime.UtcNow.Ticks);
            var dropRng = Rng.Stream(dropSeed, bossFloor, SeedChannel.Drop);
            foreach (var drop in Economy.RollBattleDrops(dropSource, bossCount, ref dropRng))
            {
                // 상한 판정은 **실제 소지품**이 한다(§18-4). 예전엔 보유량을 0으로 두고
                // 판정해 상한이 영원히 안 걸렸다 — 상한이 있다는 말만 있고 없는 것과 같았다.
                if (GameState.Gain(drop)) _reward.DroppedItems.Add(drop);
                else _reward.RejectedItems.Add(drop);   // 소실이 아니라 획득 거부
            }
        }

        void OnBattleEnd(bool survived)
        {
            // 던전 안이면 결과 화면이 아니라 **노드 맵으로 돌아간다** — 런이 계속되기 때문이다.
            // 진 경우에만 런을 끝낸다(사망 기록은 아래 공통 경로가 처리한다).
            bool inDungeon = DungeonRun.Active && GameFlow.ReturnTo == GameFlow.Dungeon;
            if (inDungeon && survived)
            {
                DungeonRun.Complete(true);
                GameFlow.LastBattleSummary = $"노드 통과 — {_t:F1}초";
                GameFlow.Go(GameFlow.Dungeon);
                return;
            }

            if (survived)
            {
                // 보스전이 아닌 일반 전투는 보상을 계산하지 않았으므로 최소한의 정보만 표시
                GameFlow.LastBattleSummary = $"생존 — {_t:F1}초";
            }
            else
            {
                GameFlow.LastBattleSummary = $"전멸 — {_t:F1}초 생존\n";

                // 패배 시 출전 캐릭터에게 사망을 기록한다 (§4 사망 시스템)
                // 현재는 W3Party 검증 빌드에서 전체 파티가 함께 전멸하는 구조
                // (실제 게임에선 캐릭터별 생사 상태를 추적할 것 — §5·§10)
                var characters = LifeSystem.GetCharacters();
                var deletedCharacters = new System.Collections.Generic.List<string>();

                foreach (var ch in characters)
                {
                    // 프로토타입: 전체 파티가 함께 전멸하는 것으로 단순화
                    // (검증: W3Party 구조에서 개별 캐릭터 생사 추적은 별개 시스템)
                    if (!ch.IsDeleted)  // 삭제된 캐릭터는 다시 죽지 않음
                    {
                        LifeSystem.RegisterDeath(ch, isPvp: false);  // PvE 사망으로 기록 (§4)
                        if (ch.IsDeleted)
                            deletedCharacters.Add(ch.Name);
                    }
                }

                // 삭제된 캐릭터 안내
                if (deletedCharacters.Count > 0)
                {
                    GameFlow.LastBattleSummary += $"\n[삭제] {string.Join(", ", deletedCharacters)}이(가) 삭제되었습니다\n장착 장비도 함께 사라집니다(§4)";
                }
            }

            // 던전에서 전멸하면 런은 거기서 끝난다(✅ §7 나가면 초기화)
            if (DungeonRun.Active && !survived) DungeonRun.End();

            GameFlow.Go(GameFlow.Result);
        }

        protected override void Body(Rect r)
        {
            Info(r, 0, $"경과 {_t:F1}s");
            if (Row(r, 1, "후퇴", "긴급 탈출 아이템(§4)")) GameFlow.Go(GameFlow.ReturnTo);
        }

        /// <summary>
        /// ResultScreen이 보상 정보를 읽기 위한 접근자
        /// </summary>
        public static BattleRewardInfo _GetLastReward() => _reward;
    }
}
