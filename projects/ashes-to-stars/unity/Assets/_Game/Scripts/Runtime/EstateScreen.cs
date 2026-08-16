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
    /// 영지 — 허브. 경매장·대장간·영묘는 **영지 안 건물**로 들어간다(§16).
    /// 씬을 새로 파지 않고 하위 화면으로 두는 것이 기획 의도다 —
    /// "메뉴를 늘리지 않고 영지를 실제로 쓰게 만드는 배치"(§16).
    /// </summary>
    public class EstateScreen : GameScreen
    {
        enum Sub { 없음, 대장간, 경매장, 영묘, 수비대, 월드티어 }
        Sub _sub = Sub.없음;
        int _hubPage;

        /// <summary>경매장 해금 층(§12). 침략과 동시 해금이다.</summary>
        public const int AuctionUnlockFloor = 30;

        protected override string Title => _sub == Sub.없음 ? "영지" : $"영지 · {_sub}";
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.Estate);
        protected override string BackgroundArt => "bg_estate";
        protected override string Subtitle => _sub switch
        {
            Sub.대장간 => "사냥해서 얻은 재료로 만든다. 강화는 실패해도 파괴되지 않는다(§11)",
            Sub.경매장 => "탑 30층 달성 시 오픈. 골드는 곧 목숨이라 거래가 성립한다(§12)",
            Sub.영묘 => "환생석으로 삭제된 캐릭터를 되돌린다. 장비는 함께 돌아오지 않는다(§4)",
            Sub.수비대 => "최대 5명. 침략 때 수비가 적으면 약탈이 늘어난다(§13-5·§15)",
            Sub.월드티어 => "해금한 티어 중 하나를 고르면 필드·던전·하위 레이드가 함께 움직인다(§6)",
            _ => TowerEnding.HasTitle
                ? $"{TowerEnding.TitleName} · 모든 콘텐츠의 출발점(§8·§16)"
                : "모든 콘텐츠의 출발점. 건물을 눌러 들어간다 — 메뉴를 늘리지 않는다(§13·§16)",
        };

        /// <summary>
        /// 시각 QA가 **건물 안쪽까지** 찍을 수 있게 하는 진입점(`GAME_START=estate`).
        /// 이게 없으면 자동 조종은 허브만 찍고 끝나서, 정작 채운 화면은 한 번도 안 보인다 —
        /// 이 프로젝트에서 "렌더는 되는데 아무도 안 본 화면"이 반복해서 나온 이유다.
        /// </summary>
        public static string AutoOpen;

        protected override void Update()
        {
            // 하위 화면에서 ESC는 영지로 — 허브 밖으로 튕겨나가지 않게
            if (_sub != Sub.없음 && Input.GetKeyDown(KeyCode.Escape)) { _sub = Sub.없음; return; }
            base.Update();
        }

        protected override void Body(Rect r)
        {
            TowerEnding.SeedQaIfRequested();
            if (!string.IsNullOrEmpty(AutoOpen))
            {
                if (System.Enum.TryParse(AutoOpen, out Sub want))
                {
                    _sub = want;
                    if (want == Sub.대장간)
                    {
                        for (int i = 0; i < Equipment.Recipes.Length; i++)
                            GameState.Gain(Equipment.Recipes[i].Material, 4);
                    }
                }
                AutoOpen = null;                 // 한 번만 — 이후엔 사람이 조작한다
            }

            if (_sub == Sub.영묘) { Mausoleum(r); return; }
            if (_sub == Sub.대장간) { Smith(r); return; }
            if (_sub == Sub.수비대) { Barracks(r); return; }
            if (_sub == Sub.월드티어) { WorldTier(r); return; }

            if (_sub == Sub.경매장) { AuctionHouse(r); return; }

            if (_sub != Sub.없음)
            {
                Info(r, 0, "아직 내용이 없다 — 수직 슬라이스에서 채운다(§21-2)");
                if (Row(r, 1, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
                return;
            }

            _hubPage = DrawTabs(r, new[] { "건물", "현황" }, _hubPage);
            var page = UiPages.AfterTabs(r);
            if (_hubPage == 1)
            {
                DrawEstateStatus(page);
                return;
            }

            var cards = UiPages.Grid(page, 2, 2, 16f);
            if (DrawCard(cards[0], "대장간", "제작·강화. 실패해도 장비는 남는다", UiAtlas.BuildingKey("대장간")))
                _sub = Sub.대장간;
            string auctionLock = AuctionHubLockReason();
            if (DrawCard(cards[1], "경매장",
                    auctionLock ?? "로컬 장 · 등록 2%·체결 8% 소각",
                    UiAtlas.BuildingKey("경매장"), locked: auctionLock != null))
                _sub = Sub.경매장;
            if (DrawCard(cards[2], "영묘",
                    $"환생석 {LifeSystem.GetRebornStones()}개 · 삭제된 캐릭터만",
                    UiAtlas.BuildingKey("영묘")))
                _sub = Sub.영묘;
            if (DrawCard(cards[3], "수비대",
                    $"배치 {DefenseState.Count}/{DefenseState.MaxSlots} · 출전에서 빠진다",
                    UiAtlas.BuildingKey("수비대")))
                _sub = Sub.수비대;
        }

        void DrawEstateStatus(Rect r)
        {
            var cards = UiPages.Grid(r, 2, 2, 16f);
            bool canPick = GameState.UnlockedTier > 0;
            if (DrawCard(cards[0], $"세계 T{GameState.Tier + 1}",
                    canPick
                        ? $"해금 T{GameState.UnlockedTier + 1} · 탑 {GameState.TowerFloor}층 — 눌러 고른다"
                        : $"해금 T1 · 탑 {GameState.TowerFloor}층 — 10층 돌파 시 T2",
                    "tower", locked: !canPick))
                _sub = Sub.월드티어;
            if (TowerEnding.HasTitle)
                DrawCard(cards[1], TowerEnding.TitleName,
                    TowerEnding.HasStarLook
                        ? $"{TowerEnding.LookName} · 전투력 변화 없음 · 100층 재도전(§8)"
                        : "100층 최초 클리어 · 전투력 변화 없음(§8)",
                    "tower", locked: true);
            else
                DrawCard(cards[1], Economy.FormatCurrency(GameState.Wallet.Copper),
                    GameState.Debt > 0 ? $"부채 {Economy.FormatCurrency(GameState.Debt)}" : "부채 없음",
                    "building_auction", locked: true);
            DrawCard(cards[2], $"파티 {PartyState.Slots.Count}/{PartyState.MaxSlots}",
                "편성은 캐릭터 탭 · 파티 화면", "characters", locked: true);
            DrawCard(cards[3], $"수비 {DefenseState.Count}/{DefenseState.MaxSlots}",
                "비어 있으면 침략 약탈이 늘어난다", "building_barracks", locked: true);
        }

        void WorldTier(Rect r)
        {
            int row = 0;
            Info(r, row++,
                $"해금 T{GameState.UnlockedTier + 1} · 탑 {GameState.TowerFloor}층 · 최고 기록은 안 내려간다(§6)");
            int unlocked = GameState.UnlockedTier;
            int last = Mathf.Min(9, unlocked + 1);
            for (int i = 0; i <= last && row < 8; i++)
            {
                string pay = Economy.FormatCurrency((long)(Economy.TierRevenueMultiplier[i] * 10000f)) + "/h";
                if (i > unlocked)
                {
                    Locked(r, row++, $"T{i + 1}", $"탑 {i * 10 + 1}층에서 해금 — 현재 {GameState.TowerFloor}층");
                    continue;
                }
                bool current = i == GameState.Tier;
                if (Row(r, row++, $"T{i + 1} · {pay}",
                        current ? "현재 세계 — 필드·던전·하위 레이드" : "이 티어로 세계를 맞춘다",
                        "tower"))
                    GameState.TrySelectTier(i);
            }
            if (Row(r, row, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
        }

        /// <summary>
        /// 허브 경매 버튼 잠금 사유. null이면 들어간다.
        /// 부채·연체·파산 정지가 층 게이트보다 앞선다 — 열려 보이면 안 된다.
        /// </summary>
        public static string AuctionHubLockReason(long nowUnix)
        {
            if (!GameState.CanUseAuction(nowUnix))
                return GameState.AuctionBlockReason(nowUnix);
            if (GameState.TowerFloor < AuctionUnlockFloor)
                return $"탑 {AuctionUnlockFloor}층 달성 시 해금(현재 {GameState.TowerFloor}층) — 30층 미만은 초보 보호(§12)";
            return null;
        }
        public static string AuctionHubLockReason() => AuctionHubLockReason(
            System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        void AuctionHouse(Rect r)
        {
            int row = 0;
            string lockReason = AuctionHubLockReason();
            if (lockReason != null)
            {
                Info(r, row++, lockReason);
                if (Row(r, row, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
                return;
            }

            Info(r, row++,
                $"로컬 장 · {Economy.FormatCurrency(GameState.Wallet.Copper)} · 등록 2%·체결 8% 소각. 다른 유저 서버 아님.");
            if (!string.IsNullOrEmpty(_msg))
                Info(r, row++, _msg);

            var lots = AuctionState.Lots;
            for (int i = 0; i < lots.Count && row < 7; i++)
            {
                var lot = lots[i];
                string who = lot.Npc ? "장" : "내 등록";
                if (lot.Npc)
                {
                    if (Row(r, row++, $"구매 {lot.Label}",
                            $"{who} · {Economy.FormatCurrency(lot.Price)}",
                            ItemAtlas.KeyFor(ParseLotItem(lot))))
                    {
                        _msg = AuctionState.TryBuy(lot.Id)
                            ? $"{lot.Label} 구매"
                            : "구매 실패 — 골드 부족이거나 상한";
                    }
                }
                else if (Row(r, row++, $"취소 {lot.Label}",
                             $"{who} · {Economy.FormatCurrency(lot.Price)}"))
                {
                    _msg = AuctionState.TryCancel(lot.Id) ? "등록 취소 · 수수료는 소각" : "취소 실패";
                }
            }

            var bag = Equipment.Unequipped();
            if (bag.Count > 0 && row < 8)
            {
                var g = bag[0];
                long price = 12_000 + g.Enhance * 2_000;
                if (Row(r, row++, $"등록 {g.Name}",
                        $"수수료 {Economy.FormatCurrency(AuctionState.ListFee(price))} · {Economy.FormatCurrency(price)}"))
                {
                    _msg = AuctionState.TryListGear(g.Id, price)
                        ? $"{g.Name} 등록"
                        : "등록 실패 — 수수료·한도·잠금";
                }
            }
            else if (GameState.Bag.GetCount(Economy.LifeItem.CraftHide) > 0 && row < 8)
            {
                const long hidePrice = 2_400;
                if (Row(r, row++, "등록 사냥 가죽 1",
                        $"수수료 {Economy.FormatCurrency(AuctionState.ListFee(hidePrice))}"))
                {
                    _msg = AuctionState.TryListItem(Economy.LifeItem.CraftHide, 1, hidePrice)
                        ? "가죽 등록" : "등록 실패";
                }
            }

            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _msg = ""; _sub = Sub.없음; }
        }

        static Economy.LifeItem ParseLotItem(AuctionState.Lot lot)
        {
            if (lot != null && System.Enum.TryParse(lot.Key, out Economy.LifeItem it))
                return it;
            return Economy.LifeItem.EnhanceStone;
        }

        string _msg = "";

        /// <summary>
        /// 영묘 — 환생석으로 삭제된 캐릭터를 되돌린다(§4).
        ///
        /// 영지 4건물 중 여기를 먼저 채운 이유: **이미 있는 시스템만으로 성립한다.**
        /// 삭제된 캐릭터(`IsDeleted`)도, 환생석(`Economy.LifeItem.RebornStone`)도,
        /// 소모 API(`GameState.Consume`)도 전부 있었는데 **화면만 없었다** — 이 저장소가
        /// 반복해서 겪는 「정의는 있고 부르는 곳이 없다」의 또 한 사례다.
        /// 대장간 첫 슬라이스(가죽→흉갑→장착)는 Smith()가 맡는다.
        ///
        /// 삭제가 **되돌릴 수 있는 것**이 되면 목숨 시스템(§4)의 무게가 사라지므로,
        /// 되돌아온 캐릭터는 사망 0에서 다시 시작하고 환생석 자체를 희소하게 둔다.
        /// </summary>
        void Mausoleum(Rect r)
        {
            var dead = LifeSystem.GetDeletedCharacters();
            int stones = LifeSystem.GetRebornStones();
            int row = 0;

            Info(r, row++, $"환생석 {stones}개 · 잠든 캐릭터 {dead.Count}명");

            if (dead.Count == 0)
            {
                // 빈 목록을 조용히 그리지 않는다 — "왜 아무것도 없나"에 답해 준다
                Info(r, row++, "잠든 캐릭터가 없다 — 3회 사망한 캐릭터만 이곳에 온다(§4)");
            }
            else
            {
                foreach (var ch in dead)
                {
                    if (ch.IsSpecialJob)
                    {
                        Locked(r, row++, $"{ch.Name} · {ch.Job} · 기록만",
                            "특수 직업은 환생석으로 되돌릴 수 없다(§3)",
                            ItemAtlas.KeyFor(Economy.LifeItem.RebornStone));
                        continue;
                    }
                    string desc = stones > 0
                        ? "환생석 1개를 써서 되돌린다 — 사망 0에서 다시 시작한다"
                        : "환생석이 없다 — 10층 보스가 떨어뜨린다";
                    if (Row(r, row++, $"{ch.Name} · {ch.Job} Lv{ch.Level}", desc,
                            ItemAtlas.KeyFor(Economy.LifeItem.RebornStone)))
                    {
                        if (stones <= 0) _msg = "환생석이 없다. 10층 보스를 잡아야 한다(§4)";
                        else if (LifeSystem.UseRebornStone(ch))
                        {
                            _msg = $"{ch.Name}이(가) 돌아왔다 — 사망 0에서 다시 시작한다";
                            return;                     // 목록이 바뀌었으니 이번 프레임은 여기서 끝
                        }
                        else _msg = "환생에 실패했다 — 환생석 소모를 확인할 것";
                    }
                }
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
        }

        /// <summary>
        /// 수비대 — 로스터를 최대 5명 세운다(§13-5).
        /// 소비처는 출전 제외다. 침략 본게임(적 별·약탈)은 여기서 열지 않는다.
        /// </summary>
        void Barracks(Rect r)
        {
            var roster = LifeSystem.GetCharacters();
            int row = 0;
            Info(r, row++,
                $"수비 {DefenseState.Count}/{DefenseState.MaxSlots} · 배치된 캐릭터는 출전하지 않는다. 침략 전투는 아직 없다(§13-5)");

            if (roster.Count == 0)
                Info(r, row++, "[주의] 캐릭터가 하나도 없다 — 로스터 생성이 실패했다");
            else
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster[i];
                    if (ch.IsDeleted) continue;
                    bool posted = DefenseState.Contains(i);
                    string label = (posted ? "★ " : "") + $"{ch.Name} · {ch.Job}";
                    string desc = posted
                        ? "배치됨 — 눌러 해임. 출전 편성에서 빠져 있다"
                        : LifeSystem.IsAvailable(ch)
                            ? "대기 — 눌러 배치. 배치하면 출전에서 빠진다"
                            : "회복 중 — 배치할 수 없다(§4)";
                    if (Row(r, row++, label, desc))
                    {
                        if (!DefenseState.Toggle(i))
                            _msg = LifeSystem.IsAvailable(ch)
                                ? $"자리가 없다 — {DefenseState.MaxSlots}명이 상한이다(§13-5)"
                                : "회복 중이거나 삭제된 캐릭터는 세울 수 없다";
                        else _msg = "";
                    }
                }
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
        }

        /// <summary>
        /// 대장간 — 계열 재료로 6부위를 만들고, 강화석으로 +15까지 올린다(§11).
        /// 실패해도 장비는 남는다. 해금은 1차 전직 시점(§13-2).
        /// </summary>
        void Smith(Rect r)
        {
            int row = 0;
            DrawSmithMaterials(r, row++);

            if (!Equipment.SmithUnlocked())
            {
                Locked(r, row++, "제작·강화",
                    "1차 전직을 한 캐릭터가 있어야 대장간이 열린다(§13-2)",
                    "building_smith");
            }
            else
            {
                var target = Equipment.FirstEnhanceable();
                if (target != null)
                {
                    int cost = Equipment.StoneCost(target.Enhance);
                    int stones = GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone);
                    int pct = Equipment.SuccessPercent(target.Enhance);
                    string label = $"{target.Name} +{target.Enhance} 강화";
                    string desc = $"석 {cost}개 · 성공 {pct}% · 실패해도 파괴 없음(§11)";
                    string enhanceIcon = ItemAtlas.KeyFor(Economy.LifeItem.EnhanceStone);
                    if (target.Enhance >= Equipment.MaxEnhance)
                        Locked(r, row++, label, "+15가 상한이다", enhanceIcon, target.Grade);
                    else if (stones < cost)
                        Locked(r, row++, label, $"강화석 {cost}개 필요 — 현재 {stones}개(던전)", enhanceIcon, target.Grade);
                    else if (Row(r, row++, label, desc, enhanceIcon, rarity: target.Grade))
                    {
                        bool attempted = Equipment.TryEnhance(target.Id, out bool ok);
                        _msg = !attempted
                            ? "강화할 수 없다 — 석 수와 상한을 확인할 것"
                            : ok
                                ? $"{target.Name} 강화 성공 +{target.Enhance}"
                                : $"{target.Name} 강화 실패 — 장비는 남았다";
                    }
                }

                for (int i = 0; i < Equipment.Recipes.Length; i++)
                {
                    var rec = Equipment.Recipes[i];
                    if (Equipment.CountOfRecipe(rec.Id) > 0) continue;
                    int have = GameState.Bag.GetCount(rec.Material);
                    string need = $"{GameState.Label(rec.Material)} {rec.Cost}장 · {Equipment.SlotName(rec.Slot)}";
                    string craftIcon = ItemAtlas.KeyForSlot(rec.Slot);
                    if (have < rec.Cost)
                        Locked(r, row++, $"{rec.Name} 제작", $"{need} — 현재 {have}", craftIcon, GearGrade.Common);
                    else if (Row(r, row++, $"{rec.Name} 제작", need, craftIcon, rarity: GearGrade.Common))
                    {
                        _msg = Equipment.TryCraft(rec.Id)
                            ? $"{rec.Name}을(를) 만들었다 — 아래에서 입힌다"
                            : "제작에 실패했다 — 재료와 전직 해금을 확인할 것";
                    }
                }
            }

            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                if (ch.IsDeleted) continue;
                var worn = Equipment.WornAll(ch);
                if (worn.Count > 0)
                {
                    string names = worn[0].Name + (worn[0].Enhance > 0 ? $"+{worn[0].Enhance}" : "");
                    if (worn.Count > 1) names += $" 외 {worn.Count - 1}";
                    if (Row(r, row++, $"{ch.Name} · {names}",
                            $"체력 ×{Equipment.HpMulOf(ch):0.00} — 눌러 벗긴다",
                            ItemAtlas.KeyForGear(worn[0]), rarity: worn[0].Grade))
                    {
                        Equipment.TryUnequip(ch);
                        _msg = $"{ch.Name}의 장비를 벗겼다";
                    }
                    continue;
                }

                var bag = Equipment.Unequipped();
                if (bag.Count == 0)
                {
                    Info(r, row++, $"{ch.Name} · 미착용 — 만들 장비가 없다");
                    continue;
                }
                if (Row(r, row++, $"{ch.Name}에게 {bag[0].Name} 입히기",
                        $"체력 ×{Equipment.EffectiveHpMul(bag[0]):0.00}",
                        ItemAtlas.KeyForGear(bag[0]), rarity: bag[0].Grade))
                {
                    _msg = Equipment.TryEquip(ch, bag[0].Id)
                        ? $"{ch.Name}이(가) {bag[0].Name}을(를) 입었다"
                        : "장착에 실패했다";
                }
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
        }

        /// <summary>재료를 글자 나열로만 쓰면 아이템 아틀라스가 대장간에 소비처 0곳이다.</summary>
        void DrawSmithMaterials(Rect r, int index)
        {
            var panel = new Rect(r.x - 12, r.y + index * (RowH + RowGap), r.width + 24, RowH);
            if (!UiAtlas.DrawSliced(panel, "panel", 14f, new Color(1f, 1f, 1f, 0.92f)))
                UiAtlas.Draw(panel, "panel", new Color(1f, 1f, 1f, 0.92f));

            var items = ItemAtlas.SmithMaterials;
            float cell = Mathf.Min(96f, (r.width - 8f) / items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                float x = r.x + 4f + i * cell;
                ItemAtlas.Draw(new Rect(x, panel.y + 6f, 40f, 40f), ItemAtlas.KeyFor(items[i]));
                GUI.Label(new Rect(x + 42f, panel.y + 16f, cell - 46f, 26f),
                    GameState.Bag.GetCount(items[i]).ToString(),
                    new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = new Color(0.95f, 0.79f, 0.42f) } });
            }
        }
    }
}
