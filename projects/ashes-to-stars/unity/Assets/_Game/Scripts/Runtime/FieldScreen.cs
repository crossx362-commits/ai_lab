using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>필드 — 자동사냥. 코어 루프의 시작점(§2·§6).</summary>
    public class FieldScreen : GameScreen
    {
        protected override string Title => "필드";
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.Field);
        protected override string BackgroundArt => "bg_field";
        protected override string Subtitle =>
            $"자동사냥으로 재화를 번다(§2·§6) — 세계 T{GameState.Tier + 1} · 보유 {GameState.WalletText} · {GameState.BagText()}";

        bool _showLastLifeWarning = false;
        bool _showInsufficientGold = false;

        /// <summary>
        /// 경고 화면에서 "계속 진행"을 눌렀을 때 **그때** 낼 비용.
        ///
        /// 예전에는 던전 버튼을 누른 즉시 `GameState.Pay`로 차감한 뒤 경고를 띄웠다.
        /// 유저가 "취소"를 고르면 **입장하지 않았는데 골드만 사라졌다** —
        /// 같은 파일이 "부분 차감은 하지 않는다, 돈은 냈는데 못 들어갔다가 최악"이라고
        /// 적어두고 정확히 그 일을 하고 있었다. 결제는 되돌릴 수 없는 마지막 단계에서 한다.
        /// </summary>
        long _pendingCost = 0;
        bool _pendingRaid;      // 경고를 거친 뒤 들어갈 곳이 레이드급인가

        protected override void Awake()
        {
            base.Awake();
            RaidSpawn.Tick();          // ✅ §7 레이드급 던전은 필드에 랜덤 출현한다
        }

        protected override void Body(Rect r)
        {
            if (_showInsufficientGold)
            {
                Info(r, 0, "[주의] 골드가 부족합니다");
                Info(r, 1, "던전 입장에는 골드가 필요합니다(§18-2)\n필드 사냥으로 먼저 재화를 모으세요(§2)");
                if (DrawChoice(r, "확인", "돌아간다", "field",
                               "영지로", "허브로 간다", "territory", out bool home)
                    || home)
                {
                    _showInsufficientGold = false;
                    if (home) GameFlow.Go(GameFlow.Estate);
                }
                return;
            }

            if (_showLastLifeWarning)
            {
                Info(r, 0, "[주의] 마지막 목숨 캐릭터가 파티에 있습니다");
                Info(r, 1, "사망 시 캐릭터가 영구 삭제되며\n장착 장비도 함께 사라집니다(§4)");
                if (DrawChoice(r, "계속 진행", "입장한다", "field",
                               "취소", "파티를 다시 편성한다", "characters", out bool cancel))
                {
                    _showLastLifeWarning = false;
                    if (_pendingCost > 0 && !GameState.Pay(_pendingCost))
                    {
                        _pendingCost = 0;
                        _showInsufficientGold = true;
                        return;
                    }
                    bool dungeon = _pendingCost > 0;
                    bool raid = _pendingRaid;
                    _pendingCost = 0; _pendingRaid = false;
                    if (raid) EnterRaid();
                    else if (dungeon) EnterDungeon();
                    else GameFlow.GoBattle(GameFlow.Field);
                }
                else if (cancel)
                {
                    _showLastLifeWarning = false;
                    _pendingCost = 0; _pendingRaid = false;
                }
                return;
            }

            var cards = UiPages.Grid(r, 2, 2, 16f);
            if (DrawCard(cards[0], "사냥 시작", "잡몹은 자동, 보스는 수동 지휘(§5)", "field"))
            {
                if (HasLastLifeCharacter())
                    _showLastLifeWarning = true;
                else
                    GameFlow.GoBattle(GameFlow.Field);
            }
            long dungeonCost = Economy.GetActionCost("DungeonEntry", GameState.Tier);
            if (DrawCard(cards[1], "던전 입장",
                    $"랜덤 생성 + 종점 보스 · {Economy.FormatCurrency(dungeonCost)}(§7)",
                    "tower"))
            {
                if (GameState.Wallet.Copper < dungeonCost)
                    _showInsufficientGold = true;
                else if (HasLastLifeCharacter())
                {
                    _pendingCost = dungeonCost;
                    _showLastLifeWarning = true;
                }
                else if (!GameState.Pay(dungeonCost))
                    _showInsufficientGold = true;
                else
                    EnterDungeon();
            }

            if (RaidSpawn.Active)
            {
                long raidCost = Economy.GetActionCost("RaidDungeon", GameState.Tier);
                if (DrawCard(cards[2], $"레이드급 {RaidSpawn.RemainingText()}",
                        $"5인 전제 · {Economy.FormatCurrency(raidCost)} · 환생석·증표 없음(§10-8)",
                        "tower"))
                {
                    if (GameState.Wallet.Copper < raidCost) _showInsufficientGold = true;
                    else if (HasLastLifeCharacter())
                    {
                        _pendingCost = raidCost; _pendingRaid = true; _showLastLifeWarning = true;
                    }
                    else if (!GameState.Pay(raidCost)) _showInsufficientGold = true;
                    else EnterRaid();
                }
            }
            else
            {
                DrawCard(cards[2], GameState.WalletText,
                    $"{GameState.BagText()} · 필드 사냥은 무료", "building_auction", locked: true);
            }

            bool on = LowHpReturn.Enabled;
            if (DrawCard(cards[3], on ? "저체력 귀환 켜짐" : "저체력 귀환 꺼짐",
                    "HP 30%면 3초 뒤 영지. 이번 판 보상 없음(§4·§6)",
                    on ? "heart" : "heart_broken"))
                LowHpReturn.Enabled = !on;
        }

        /// <summary>
        /// 던전 진입 — 계획을 만들고 노드 맵으로 간다.
        ///
        /// 시드는 **매번 달라야 한다**(✅ §7 "진입할 때마다 구조가 바뀜").
        /// 프로토타입이라 로컬 시각·진행도에서 만든다(본게임은 서버, ✅ §22-2).
        /// </summary>
        void EnterDungeon()
        {
            uint seed = (uint)(System.DateTime.UtcNow.Ticks & 0x7FFFFFFF) ^ (uint)(GameState.TowerFloor * 2654435761u);
            DungeonRun.Begin(seed, GameState.Tier, DungeonKind.일반, GameFlow.Field);
            GameFlow.Go(GameFlow.Dungeon);
        }

        /// <summary>
        /// 레이드급 진입. 시드는 **출현할 때 정해진 것**을 쓴다 —
        /// 들어갈 때 새로 뽑으면 들락거리며 좋은 판이 나올 때까지 리롤할 수 있다(§19 악용 대응).
        /// 들어가는 순간 필드에서 사라진다: 한 번 뜬 것을 반복해 돌 수 있으면 "한정"이 아니다.
        /// </summary>
        void EnterRaid()
        {
            uint seed = RaidSpawn.Seed;
            RaidSpawn.Consume();
            DungeonRun.Begin(seed, GameState.Tier, DungeonKind.레이드급, GameFlow.Field);
            GameFlow.Go(GameFlow.Dungeon);
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
}
