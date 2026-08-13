using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 기획서 §2(코어 루프)·§16(UI 화면 구조)의 골격을 실제로 걸어다닐 수 있게 만든 것.
    // 지금은 각 화면이 "무엇을 하는 곳인가"와 이동만 담는다 — 콘텐츠는 수직 슬라이스에서 채운다.
    // ⚠️ 여기에 전투 로직·수치를 넣지 마라. 수치의 출처는 언제나 §18 기준표와 ScriptableObject다.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 전투 보상 정보 저장소 (§2 코어 루프: 재화 획득 → 캐릭터 성장)
    /// BattleScreen에서 계산하고 ResultScreen에서 표시한다.
    /// GameFlow.LastBattleSummary와 함께 결과를 전달한다.
    /// 출처: §18-1(티어별 수익), §10-8(드랍 설계), §18-4(소지 상한)
    /// </summary>
    public class BattleRewardInfo
    {
        /// <summary>획득 골드 (§18-1 티어별 수익)</summary>
        public long GoldReward { get; set; }

        /// <summary>획득한 드랍 아이템 (부활초·귀환의 두루마리 등, §10-8)</summary>
        public List<Economy.LifeItem> DroppedItems { get; set; } = new List<Economy.LifeItem>();

        /// <summary>소지 상한에 걸려 획득 거부된 아이템 (§18-4)</summary>
        public List<Economy.LifeItem> RejectedItems { get; set; } = new List<Economy.LifeItem>();

        /// <summary>이 전투에서 삭제된 캐릭터 목록 (사망 카운트 3회 도달, §4)</summary>
        public List<string> DeletedCharacters { get; set; } = new List<string>();

        /// <summary>이 전투에서 회복 중이 된 캐릭터 (사망 카운트 증가, §4)</summary>
        public List<(string name, int deathCount)> RecoveredCharacters { get; set; } = new List<(string, int)>();

        /// <summary>전투 소요 시간 (초 단위)</summary>
        public float BattleDurationSeconds { get; set; }

        /// <summary>전투 승리 여부</summary>
        public bool Survived { get; set; }

        public void Clear()
        {
            GoldReward = 0;
            DroppedItems.Clear();
            RejectedItems.Clear();
            DeletedCharacters.Clear();
            RecoveredCharacters.Clear();
            BattleDurationSeconds = 0;
            Survived = false;
        }
    }

    /// <summary>타이틀 — 시작과 종료. 하단바 없음.</summary>
    public class TitleScreen : GameScreen
    {
        protected override string Title => "재와 별";
        protected override string Subtitle => "Ashes to Stars — 죽으면 캐릭터가 진짜 사라진다";
        protected override bool ShowBottomBar => false;

        protected override void Body(Rect r)
        {
            if (Row(r, 0, "게임 시작", "영지에서 시작한다 (§16 허브는 영지)")) GameFlow.Go(GameFlow.Estate);
            if (Row(r, 1, "이어하기", "저장 슬롯 — 수직 슬라이스에서 구현")) GameFlow.Go(GameFlow.Estate);
            if (Row(r, 2, "종료", "Application.Quit")) GameFlow.Quit();
        }
    }

    /// <summary>
    /// 영지 — 허브. 경매장·대장간·영묘는 **영지 안 건물**로 들어간다(§16).
    /// 씬을 새로 파지 않고 하위 화면으로 두는 것이 기획 의도다 —
    /// "메뉴를 늘리지 않고 영지를 실제로 쓰게 만드는 배치"(§16).
    /// </summary>
    public class EstateScreen : GameScreen
    {
        enum Sub { 없음, 대장간, 경매장, 영묘, 수비대 }
        Sub _sub = Sub.없음;

        protected override string Title => _sub == Sub.없음 ? "영지" : $"영지 · {_sub}";
        protected override string Subtitle => _sub switch
        {
            Sub.대장간 => "사냥해서 얻은 재료로 만든다. 강화는 실패해도 파괴되지 않는다(§11)",
            Sub.경매장 => "탑 30층 달성 시 오픈. 골드는 곧 목숨이라 거래가 성립한다(§12)",
            Sub.영묘 => "환생석으로 삭제된 캐릭터를 되돌린다. 장비는 함께 돌아오지 않는다(§4)",
            Sub.수비대 => "침략에 맞설 캐릭터를 세운다. 수비대도 죽으면 사라진다(§13-5)",
            _ => "모든 콘텐츠의 출발점. 건물을 눌러 들어간다 — 메뉴를 늘리지 않는다(§13·§16)",
        };

        protected override void Update()
        {
            // 하위 화면에서 ESC는 영지로 — 허브 밖으로 튕겨나가지 않게
            if (_sub != Sub.없음 && Input.GetKeyDown(KeyCode.Escape)) { _sub = Sub.없음; return; }
            base.Update();
        }

        protected override void Body(Rect r)
        {
            if (_sub != Sub.없음)
            {
                Info(r, 0, "아직 내용이 없다 — 수직 슬라이스에서 채운다(§21-2)");
                if (Row(r, 1, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
                return;
            }

            if (Row(r, 0, "대장간", "장비 제작·강화 (§11)")) _sub = Sub.대장간;
            if (Row(r, 1, "경매장", "탑 30층 달성 시 오픈 (§12)")) _sub = Sub.경매장;
            if (Row(r, 2, "영묘", "환생 — 삭제된 캐릭터의 귀환 (§4)")) _sub = Sub.영묘;
            if (Row(r, 3, "수비대 배치", "침략 방어 (§13-5)")) _sub = Sub.수비대;
        }
    }

    /// <summary>필드 — 자동사냥. 코어 루프의 시작점(§2·§6).</summary>
    public class FieldScreen : GameScreen
    {
        protected override string Title => "필드";
        protected override string Subtitle =>
            $"자동사냥으로 재화를 번다(§2·§6) — 보유 {GameState.WalletText} · {GameState.BagText()}";

        bool _showLastLifeWarning = false;
        bool _showInsufficientGold = false;

        protected override void Body(Rect r)
        {
            if (_showInsufficientGold)
            {
                // 골드 부족 경고 화면 (§18-2 진입 비용)
                Info(r, 0, "⚠️ 골드가 부족합니다");
                Info(r, 1, "던전 입장에는 골드가 필요합니다(§18-2)\n필드 사냥으로 먼저 재화를 모으세요(§2)");

                if (Row(r, 2, "확인", "돌아간다"))
                    _showInsufficientGold = false;
                return;
            }

            if (_showLastLifeWarning)
            {
                // 마지막 목숨 경고 화면
                Info(r, 0, "⚠️ 주의! 마지막 목숨 캐릭터가 파티에 있습니다");
                Info(r, 1, "사망 시 캐릭터가 영구 삭제되며\n장착 장비도 함께 사라집니다(§4)");

                if (Row(r, 2, "계속 진행", "입장한다"))
                {
                    _showLastLifeWarning = false;
                    GameFlow.GoBattle(GameFlow.Field);
                }
                if (Row(r, 3, "취소", "파티를 다시 편성한다"))
                    _showLastLifeWarning = false;
                return;
            }

            if (Row(r, 0, "사냥 시작", "잡몹은 자동, 보스는 수동 지휘(§5)"))
            {
                // 필드 사냥은 무료 (§18-2 절대 원칙)
                if (HasLastLifeCharacter())
                    _showLastLifeWarning = true;
                else
                    GameFlow.GoBattle(GameFlow.Field);
            }
            if (Row(r, 1, "던전 입장", "랜덤 생성 + 종점 보스 1체(§7)"))
            {
                // 던전 입장에는 골드 비용 필요 (§18-2)
                // TODO: 실제 지갑 시스템 필요. 지금은 T1 기준 0.02 G/h를 차감한다고 표시만 함
                // 실제로 차감한다. 모자라면 들어가지 못한다(§18-2).
                // 부분 차감은 하지 않는다 — "돈은 냈는데 못 들어갔다"가 최악이다.
                long dungeonCost = Economy.GetActionCost("DungeonEntry", GameState.Tier);
                if (!GameState.Pay(dungeonCost)) _showInsufficientGold = true;
                else if (HasLastLifeCharacter()) _showLastLifeWarning = true;
                else GameFlow.GoBattle(GameFlow.Field);
            }
            if (Row(r, 2, "자동화 일정", "무엇을 언제 시킬지 예약(§6)")) { }
        }

        bool HasLastLifeCharacter()
        {
            var characters = LifeSystem.GetCharacters();
            foreach (var ch in characters)
            {
                if (!ch.IsDeleted && ch.DeathCount == 2)  // 2회 사망 = 마지막 목숨
                    return true;
            }
            return false;
        }
    }

    /// <summary>탑 — 최대 100층. 10층 돌파마다 필드·던전 난이도가 오른다(§8·§10-6).</summary>
    public class TowerScreen : GameScreen
    {
        protected override string Title => $"탑 · {GameState.TowerFloor}층";
        protected override string Subtitle =>
            $"최대 100층. 10층 돌파마다 티어 상승(§8·§10-6) — 현재 T{GameState.Tier + 1} · 보유 {GameState.WalletText}";

        bool _showLastLifeWarning = false;

        protected override void Body(Rect r)
        {
            if (_showLastLifeWarning)
            {
                // 마지막 목숨 경고 화면
                Info(r, 0, "⚠️ 주의! 마지막 목숨 캐릭터가 파티에 있습니다");
                Info(r, 1, "사망 시 캐릭터가 영구 삭제되며\n장착 장비도 함께 사라집니다(§4)");

                if (Row(r, 2, "계속 진행", "입장한다"))
                {
                    _showLastLifeWarning = false;
                    GameFlow.GoBattle(GameFlow.Tower);
                }
                if (Row(r, 3, "취소", "파티를 다시 편성한다"))
                    _showLastLifeWarning = false;
                return;
            }

            if (Row(r, 0, "다음 층 도전", "벽 콘텐츠 — 재도전 리듬(§8)"))
            {
                long towerCost = Economy.GetActionCost("TowerNormalFloor", GameState.Tier);
                if (!GameState.Pay(towerCost)) _showInsufficientGold = true;
                else if (HasLastLifeCharacter()) _showLastLifeWarning = true;
                else GameFlow.GoBattle(GameFlow.Tower, GameFlow.BattleKind.잡몹웨이브, GameState.TowerFloor);
            }
            // 레이드는 **보스전**이다 — 잡몹 웨이브가 아니라 기믹 3종이 도는 판(§9·§10-5).
            // §5가 "보스는 수동 지휘"라 했으므로 여기서 V3 검증이 이뤄진다.
            if (Row(r, 1, "레이드 (5층 단위)", "5층마다 보스, 10층 단위는 대보스(§9)"))
            {
                long raidCost = Economy.GetActionCost("Tower5BossRaid", GameState.Tier);
                // 레이드 층은 현재 진행도에서 가장 가까운 5층 배수(§9 "5층 단위")
                int raidFloor = Mathf.Max(5, (GameState.TowerFloor / 5) * 5);
                if (!GameState.Pay(raidCost)) _showInsufficientGold = true;
                else if (HasLastLifeCharacter()) _showLastLifeWarning = true;
                else GameFlow.GoBattle(GameFlow.Tower, GameFlow.BattleKind.보스, raidFloor);
            }
        }

        bool HasLastLifeCharacter()
        {
            var characters = LifeSystem.GetCharacters();
            foreach (var ch in characters)
            {
                if (!ch.IsDeleted && ch.DeathCount == 2)  // 2회 사망 = 마지막 목숨
                    return true;
            }
            return false;
        }
    }

    /// <summary>월드맵 — 우주 성계. 침략은 30층 달성 시 해금(§14·§15).</summary>
    public class WorldMapScreen : GameScreen
    {
        protected override string Title => "월드맵";
        protected override string Subtitle => "우주 성계. 침략은 탑 30층 달성 시 해금(§14·§15)";

        protected override void Body(Rect r)
        {
            if (Row(r, 0, "성계 이동", "영지 ↔ 월드맵 연결(§13-6)")) { }
            if (Row(r, 1, "침략", "비동기 PvP — 30층 해금(§15)"))
                GameFlow.GoBattle(GameFlow.WorldMap);
            if (Row(r, 2, "랭킹", "주 단위 장기 루프(§15)")) { }
        }
    }

    /// <summary>캐릭터 — 성장·전직·합성, 그리고 목숨 상태(§3·§4).</summary>
    public class CharacterScreen : GameScreen
    {
        protected override string Title => "캐릭터";
        protected override string Subtitle => "성장·전직·합성. 목숨 카운트가 여기서 보인다(§3·§4)";

        int _selectedCharacter = -1;

        protected override void Body(Rect r)
        {
            if (_selectedCharacter >= 0)
            {
                // 캐릭터 상세 화면
                var characters = LifeSystem.GetCharacters();
                if (_selectedCharacter < characters.Count)
                {
                    var ch = characters[_selectedCharacter];

                    Info(r, 0, $"{ch.Name} ({ch.Job}) Lv.{ch.Level}");

                    // 목숨 상태 표시
                    if (ch.IsDeleted)
                    {
                        Info(r, 1, "❌ 삭제됨 — 환생석으로만 복구 가능(§4)");
                    }
                    else
                    {
                        string hearts = new string('❤', 3 - ch.DeathCount);
                        string status = LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중";
                        Info(r, 1, $"목숨: {hearts}({ch.DeathCount}/3) {status}");

                        // 회복 중이면 시간 표시
                        int recoveryTime = LifeSystem.GetRecoveryTimeRemaining(ch);
                        if (recoveryTime > 0)
                        {
                            Info(r, 2, $"회복 시간: {LifeSystem.FormatRecoveryTime(recoveryTime)}");
                        }
                    }

                    // 부활초 사용 버튼
                    if (!ch.IsDeleted && ch.DeathCount > 0 && LifeSystem.GetRevivePotions() > 0)
                    {
                        if (Row(r, 3, "부활초 사용", $"사망 카운트 1 차감 (보유: {LifeSystem.GetRevivePotions()}/3)"))
                        {
                            LifeSystem.UseRevivePotion(ch);
                        }
                    }
                    else if (!ch.IsDeleted && ch.DeathCount > 0 && LifeSystem.GetRevivePotions() == 0)
                    {
                        Info(r, 3, "부활초 없음 — 던전·레이드에서 획득 가능(§4)");
                    }

                    if (Row(r, 4, "← 목록으로", "캐릭터 목록으로 돌아간다"))
                        _selectedCharacter = -1;
                }
                return;
            }

            // 캐릭터 목록
            var allCharacters = LifeSystem.GetCharacters();
            for (int i = 0; i < allCharacters.Count; i++)
            {
                var ch = allCharacters[i];
                string heartsStr = ch.IsDeleted ? "❌" : new string('❤', 3 - ch.DeathCount);
                string name = $"{ch.Name} ({ch.Job}) - {heartsStr}";
                if (Row(r, i, name, ch.IsDeleted ? "삭제됨" : (LifeSystem.IsAvailable(ch) ? "출전 가능" : "회복 중")))
                    _selectedCharacter = i;
            }

            Info(r, allCharacters.Count + 1, "목숨 카운트가 여기서 보인다(§3·§4)");
            if (Row(r, allCharacters.Count + 2, "파티 편성", "탱1·딜2·힐1·버퍼1 — 1인은 불가(§9, W3에서 검증)")) { }
        }
    }

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
                    GameFlow.LastBattleSummary =
                        $"보스 격파 — {GameFlow.BossFloor}층 ({_t:F1}초) · 다음 {GameState.TowerFloor}층";
                    GameFlow.Go(GameFlow.Result);
                };
                boss.OnPartyWiped += () =>
                {
                    GameFlow.LastBattleSummary = $"보스전 패배 — {GameFlow.BossFloor}층";
                    GameFlow.Go(GameFlow.Result);
                };
                boss.Begin(GameFlow.BossFloor, 1);
            }

            // W3Party 컴포넌트 획득 또는 생성
            _battle = GetComponent<global::W3Party>();
            if (_battle == null)
                _battle = gameObject.AddComponent<global::W3Party>();

            // 게임 모드 설정: 표준 5인 한 판만 실행
            _battle.GameMode = true;

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

            // 드랍 롤 (§10-8, §18-4)
            // 보스 출처 판정: 탑 10층 단위는 대보스, 5층 단위는 중간 레이드
            Economy.DropSource dropSource = bossFloor % 10 == 0
                ? Economy.DropSource.Tower10Boss
                : Economy.DropSource.Tower5Boss;

            // 골드를 **실제로 지갑에 넣는다**. 계산만 하고 반영하지 않으면
            // §2의 순환("번 돈으로 다음 판에 들어간다")이 성립하지 않는다.
            GameState.Earn(_reward.GoldReward);

            // 드랍 롤 3회 (통상 3~5회 정도)
            for (int i = 0; i < 3; i++)
            {
                var drop = Economy.RollDrop(dropSource);
                if (!drop.HasValue) continue;

                // 상한 판정은 **실제 소지품**이 한다(§18-4). 예전엔 보유량을 0으로 두고
                // 판정해 상한이 영원히 안 걸렸다 — 상한이 있다는 말만 있고 없는 것과 같았다.
                if (GameState.Gain(drop.Value)) _reward.DroppedItems.Add(drop.Value);
                else _reward.RejectedItems.Add(drop.Value);   // 소실이 아니라 획득 거부
            }
        }

        void OnBattleEnd(bool survived)
        {
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
                    GameFlow.LastBattleSummary += $"\n🔴 {string.Join(", ", deletedCharacters)}이(가) 삭제되었습니다\n장착 장비도 함께 사라집니다(§4)";
                }
            }

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

    /// <summary>결과 — 전투가 끝나고 들어온 곳으로 돌아간다.</summary>
    public class ResultScreen : GameScreen
    {
        protected override string Title => "결과";
        protected override string Subtitle => "보상 정산 후 원래 화면으로";
        protected override bool ShowBottomBar => false;

        int _rowIndex = 0;

        protected override void Body(Rect r)
        {
            _rowIndex = 0;

            // 전투 결과 요약 (§2 코어 루프)
            Info(r, _rowIndex++, string.IsNullOrEmpty(GameFlow.LastBattleSummary) ? "전투 기록 없음" : GameFlow.LastBattleSummary);

            // 보상 정보 표시 — 승리했을 때만 (§18-1·§10-8·§18-4)
            var reward = BattleScreen._GetLastReward();
            if (reward != null && reward.Survived)
            {
                Info(r, _rowIndex++, "");  // 빈 줄
                Info(r, _rowIndex++, $"💰 획득 골드: {Economy.FormatCurrency(reward.GoldReward)}");

                // 드랍 아이템 표시 (§10-8)
                if (reward.DroppedItems.Count > 0)
                {
                    foreach (var item in reward.DroppedItems)
                    {
                        Info(r, _rowIndex++, $"  ✓ {FormatLifeItem(item)}");
                    }
                }

                // 소지 상한 초과로 거절된 아이템 (§18-4 "획득 거부로 처리")
                if (reward.RejectedItems.Count > 0)
                {
                    foreach (var item in reward.RejectedItems)
                    {
                        Info(r, _rowIndex++, $"  ⚠️ {FormatLifeItem(item)} — 소지 상한에 도달해 획득할 수 없습니다");
                    }
                }
            }

            Info(r, _rowIndex++, "");  // 빈 줄

            if (Row(r, _rowIndex++, "계속", "들어온 화면으로 복귀")) GameFlow.Go(GameFlow.ReturnTo);
            if (Row(r, _rowIndex++, "영지로", "허브 복귀(§16)")) GameFlow.Go(GameFlow.Estate);
        }

        string FormatLifeItem(Economy.LifeItem item)
        {
            return item switch
            {
                Economy.LifeItem.RevivalTea => "부활초 — 사망 카운트 1 차감 (§4)",
                Economy.LifeItem.ScrollOfReturn => "귀환의 두루마리 — 긴급 탈출 아이템 (§4)",
                Economy.LifeItem.RebornStone => "환생석 — 삭제된 캐릭터 복구 (§4)",
                Economy.LifeItem.SpecialJobToken => "특수 직업 전직 증표 — 50층 이상 보상 (§3)",
                _ => "알 수 없는 아이템"
            };
        }
    }
}
