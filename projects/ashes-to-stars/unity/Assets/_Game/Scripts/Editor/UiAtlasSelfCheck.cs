using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>UI 아틀라스가 런타임에서 읽히고, 지정된 모든 조각이 원본 안에 있는지 검사한다.</summary>
    public static class UiAtlasSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(UiAtlas.IsReady, "[UiAtlasSelfCheck] UI 아틀라스를 Resources/ui에서 읽지 못했다");

            foreach (var key in UiAtlas.RequiredKeys)
            {
                var rect = UiAtlas.RectFor(key);
                Debug.Assert(rect.width > 0 && rect.height > 0,
                    $"[UiAtlasSelfCheck] {key}: 빈 영역");
                Debug.Assert(rect.xMin >= 0 && rect.yMin >= 0 &&
                             rect.xMax <= UiAtlas.Width && rect.yMax <= UiAtlas.Height,
                    $"[UiAtlasSelfCheck] {key}: 아틀라스 밖 영역 {rect}");
            }

            Debug.Assert(UiAtlas.ButtonKey(false, false) == "button_normal",
                "[UiAtlasSelfCheck] 기본 버튼 키가 아틀라스 조각과 어긋난다");
            Debug.Assert(UiAtlas.ButtonKey(true, false) == "button_hover",
                "[UiAtlasSelfCheck] 호버 버튼 키가 아틀라스 조각과 어긋난다");
            Debug.Assert(UiAtlas.ButtonKey(false, true) == "button_pressed",
                "[UiAtlasSelfCheck] 눌림이 호버보다 우선해야 한다");
            Debug.Assert(UiAtlas.ButtonKey(true, true) == "button_pressed",
                "[UiAtlasSelfCheck] 호버+눌림은 pressed 조각을 써야 한다");
            Debug.Assert(UiAtlas.RectFor("hp_frame").width > 0,
                "[UiAtlasSelfCheck] 체력바 프레임 조각이 없다");
            Debug.Assert(UiAtlas.RectFor("xp_frame").width > 0,
                "[UiAtlasSelfCheck] 경험치 프레임 조각이 없다");
            Debug.Assert(UiAtlas.RectFor("portrait_frame").width > 0,
                "[UiAtlasSelfCheck] 초상 프레임 조각이 없다");
            Debug.Assert(UiAtlas.RectFor("heart").width > 0 && UiAtlas.RectFor("heart_broken").width > 0,
                "[UiAtlasSelfCheck] 목숨 아이콘이 없다");

            Debug.Assert(UiAtlas.RoleKey("탱") == "tank" && UiAtlas.RoleKey("수호기사") == "tank"
                         && UiAtlas.RoleKey("광전사") == "tank",
                "[UiAtlasSelfCheck] 탱 계열 역할 키가 어긋난다");
            Debug.Assert(UiAtlas.RoleKey("딜") == "damage" && UiAtlas.RoleKey("검사") == "damage"
                         && UiAtlas.RoleKey("궁수") == "damage",
                "[UiAtlasSelfCheck] 딜 계열 역할 키가 어긋난다");
            Debug.Assert(UiAtlas.RoleKey("힐") == "healer" && UiAtlas.RoleKey("사제") == "healer",
                "[UiAtlasSelfCheck] 힐 계열 역할 키가 어긋난다");
            Debug.Assert(UiAtlas.RoleKey("버퍼") == "buffer" && UiAtlas.RoleKey("음유시인") == "buffer"
                         && UiAtlas.RoleKey("정령사") == "buffer",
                "[UiAtlasSelfCheck] 버퍼 계열 역할 키가 어긋난다");

            var grid = UiPages.Grid(new Rect(0, 0, 400, 200), 2, 2, 16f);
            Debug.Assert(grid.Length == 4 && Mathf.Approximately(grid[0].width, 192f)
                         && Mathf.Approximately(grid[3].x, 208f),
                "[UiAtlasSelfCheck] 2×2 카드 격자가 어긋난다");
            var after = UiPages.AfterTabs(new Rect(0, 0, 100, 200));
            Debug.Assert(after.y > 0 && after.height < 200,
                "[UiAtlasSelfCheck] 탭 아래 본문 영역이 줄지 않는다");
            Debug.Assert(UiPages.EquipRingDegrees.Length == 6,
                "[UiAtlasSelfCheck] 장비 링은 6칸");
            var ring = UiPages.SlotOnRing(new Vector2(100, 100), 80f, 80f, -90f, 20f);
            Debug.Assert(Mathf.Approximately(ring.center.x, 100f) && ring.center.y < 100f,
                "[UiAtlasSelfCheck] 12시 칸은 초상 위에 있어야 한다");
            var box = new Rect(400f, 80f, 760f, 470f);
            var faceIn = UiPages.LargeLook(box);
            UiPages.EquipRingFit(box, faceIn, out var fitX, out var fitY);
            for (int i = 0; i < UiPages.EquipRingDegrees.Length; i++)
            {
                var sl = UiPages.ClampIn(box, UiPages.SlotOnRing(faceIn.center, fitX, fitY,
                    UiPages.EquipRingDegrees[i], UiPages.EquipSlotSize));
                Debug.Assert(sl.x >= box.x - 0.01f && sl.xMax <= box.xMax + 0.01f
                             && sl.y >= box.y - 0.01f && sl.yMax <= box.yMax + 0.01f,
                    $"[UiAtlasSelfCheck] 장비 칸 {i}가 패널 밖으로 나간다 {sl}");
            }

            UiPages.RosterSplit(new Rect(0, 0, 1000, 400), out var list, out var stage);
            Debug.Assert(list.x < stage.x && list.xMax <= stage.x + 0.01f,
                "[UiAtlasSelfCheck] 명부 목록이 왼쪽, 모습이 오른쪽이어야 한다");
            Debug.Assert(list.width < stage.width,
                "[UiAtlasSelfCheck] 목록이 모습 영역보다 넓으면 오너 지시와 반대다");
            Debug.Assert(UiPages.RosterListRatio < 0.5f,
                "[UiAtlasSelfCheck] 목록 비율이 절반 이상이면 모습이 작아진다");
            var look = UiPages.LargeLook(stage);
            Debug.Assert(look.width >= 160f && look.height >= 200f
                         && look.width > 56f && look.x >= stage.x && look.xMax <= stage.xMax,
                "[UiAtlasSelfCheck] 대형 모습이 목록 얼굴(56)보다 크고 오른쪽 안에 있어야 한다");
            var c0 = UiPages.RosterCell(list, 0);
            var c1 = UiPages.RosterCell(list, 1);
            var c3 = UiPages.RosterCell(list, 3);
            Debug.Assert(Mathf.Approximately(c0.y, c1.y) && c0.x < c1.x,
                "[UiAtlasSelfCheck] 명부 바둑판 같은 줄은 왼→오");
            Debug.Assert(c3.y > c0.y && Mathf.Approximately(c3.x, c0.x),
                "[UiAtlasSelfCheck] 명부 바둑판 다음 줄은 아래");
            Debug.Assert(UiPages.LookDir("탱") == "tank" && UiPages.LookDir("수호기사") == "tank"
                         && UiPages.LookDir("검사") == "dps" && UiPages.LookDir("마딜") == "mage"
                         && UiPages.LookDir("마법사") == "mage"
                         && UiPages.LookDir("사제") == "healer" && UiPages.LookDir("음유시인") == "buffer",
                "[UiAtlasSelfCheck] 직업→전신 폴더가 어긋난다");
            _ = nameof(CharacterScreen);

            Debug.Assert(UiAtlas.BuildingKey("대장간") == "building_smith",
                "[UiAtlasSelfCheck] 대장간 건물 키가 어긋난다");
            Debug.Assert(UiAtlas.BuildingKey("경매장") == "building_auction",
                "[UiAtlasSelfCheck] 경매장 건물 키가 어긋난다");
            Debug.Assert(UiAtlas.BuildingKey("영묘") == "building_mausoleum",
                "[UiAtlasSelfCheck] 영묘 건물 키가 어긋난다");
            Debug.Assert(UiAtlas.BuildingKey("수비대") == "building_barracks",
                "[UiAtlasSelfCheck] 수비대 건물 키가 어긋난다");
            Debug.Assert(UiAtlas.BuildingKey("없는건물") == null,
                "[UiAtlasSelfCheck] 모르는 건물은 null이어야 한다");

            Debug.Assert(UiAtlas.HeartKey(0, 0, false) == "heart"
                         && UiAtlas.HeartKey(2, 0, false) == "heart",
                "[UiAtlasSelfCheck] 목숨 3이면 세 칸 모두 온전해야 한다");
            Debug.Assert(UiAtlas.HeartKey(0, 1, false) == "heart"
                         && UiAtlas.HeartKey(2, 1, false) == "heart_broken",
                "[UiAtlasSelfCheck] 사망 1이면 마지막 칸만 깨져야 한다");
            Debug.Assert(UiAtlas.HeartKey(0, 0, true) == "heart_broken"
                         && UiAtlas.HeartKey(2, 3, true) == "heart_broken",
                "[UiAtlasSelfCheck] 삭제는 세 칸 모두 깨져야 한다");

            Debug.Assert(UiAtlas.HeaderKey("Field") == "field" && UiAtlas.HeaderKey("필드") == "field",
                "[UiAtlasSelfCheck] 필드 헤더는 나침반이 아니라 field 조각이어야 한다");
            Debug.Assert(UiAtlas.HeaderKey("Tower") == "tower" && UiAtlas.HeaderKey("탑") == "tower",
                "[UiAtlasSelfCheck] 탑 헤더는 나침반이 아니라 tower 조각이어야 한다");
            Debug.Assert(UiAtlas.HeaderKey("Estate") == "territory"
                         && UiAtlas.HeaderKey("Character") == "characters"
                         && UiAtlas.HeaderKey("WorldMap") == "worldmap",
                "[UiAtlasSelfCheck] 영지·캐릭터·월드맵 헤더 키가 어긋난다");
            Debug.Assert(UiAtlas.HeaderKey("없는화면") == null,
                "[UiAtlasSelfCheck] 모르는 화면은 null이어야 한다");
            Debug.Assert(UiAtlas.HeaderKey("Party") == null,
                "[UiAtlasSelfCheck] 매핑 없는 화면을 worldmap으로 숨기면 안 된다");

            Debug.Assert(GameScreen.HeaderH <= 92f && GameScreen.BodyTop <= 108f,
                "[UiAtlasSelfCheck] 제목판이 다시 커지면 720p 본문이 카드 한 줄을 못 채운다");
            var dock = UiPages.NavDock(GameFlow.BottomBar.Length);
            Debug.Assert(dock.Length == 5, "[UiAtlasSelfCheck] 하단 도크는 §16 5칸");
            float dockUsed = dock[4].xMax - dock[0].x;
            Debug.Assert(dockUsed < 1280f * 0.55f && dock[0].x > 240f,
                "[UiAtlasSelfCheck] 하단 5칸이 전체 폭을 5등분하면 길쭉한 알약이다");
            for (int i = 0; i < dock.Length; i++)
            {
                float aspect = dock[i].width / dock[i].height;
                Debug.Assert(aspect <= UiPages.NavMaxAspect && dock[i].width <= 100f,
                    $"[UiAtlasSelfCheck] 도크 {i} 가로/세로={aspect:0.00} — 길쭉한 아이콘 버튼");
            }
            Debug.Assert(UiPages.NavReserve < 96f && UiPages.NavReserve > UiPages.NavTileH,
                "[UiAtlasSelfCheck] 도크 아래 28px를 안 되돌리면 본문이 예전과 같다");
            Debug.Assert(UiPages.NavIcon("Estate") == "territory"
                         && UiPages.NavIcon("Field") == "field"
                         && UiPages.NavIcon("Tower") == "tower"
                         && UiPages.NavIcon("WorldMap") == "worldmap"
                         && UiPages.NavIcon("Character") == "characters"
                         && UiPages.NavIcon("Party") == null,
                "[UiAtlasSelfCheck] 하단 도크 아이콘 키가 화면과 어긋난다");
            _ = nameof(UiPages.NavDock);
            Debug.Assert(UiPages.TabH >= 48f,
                "[UiAtlasSelfCheck] 탭이 48보다 작으면 메뉴가 안 읽힌다");
            Debug.Assert(UiPages.CardMinIcon >= 72f,
                "[UiAtlasSelfCheck] 카드 아이콘이 72보다 작으면 허브가 비어 보인다");
            var tight = new Rect(0f, 0f, 200f, 150f);
            UiPages.CardLayout(tight, true, out var tIcon, out var tTitle, out var tSub);
            Debug.Assert(!UiPages.LayoutOverlaps(tIcon, tTitle) && !UiPages.LayoutOverlaps(tIcon, tSub),
                "[UiAtlasSelfCheck] 카드 아이콘과 글자가 겹치면 잘린다");
            Debug.Assert(tTitle.xMax <= tight.xMax + 0.01f && tSub.yMax <= tight.yMax + 0.01f,
                "[UiAtlasSelfCheck] 카드 글자가 칸 밖으로 나가면 잘린다");
            Debug.Assert(tTitle.height >= UiPages.CardTitleH - 0.01f,
                "[UiAtlasSelfCheck] 제목 칸이 36보다 작으면 한글이 잘린다");
            Debug.Assert(UiAtlas.FitsInContent(tight, tIcon) && UiAtlas.FitsInContent(tight, tTitle),
                "[UiAtlasSelfCheck] 작은 카드 글씨가 금테 밖으로 나간다");
            Debug.Assert(UiAtlas.SlicePad(new Rect(0f, 0f, 532f, 180f), "panel")
                         > UiAtlas.ContentExtra,
                "[UiAtlasSelfCheck] 타이틀 카드 금테 여백이 ContentExtra보다 작으면 글씨가 장식에 겹친다");
            _ = nameof(UiPages.LabelClip);
            var stretchBox = new Rect(0f, 0f, 400f, 180f);
            var fitIcon = UiAtlas.FitInside(stretchBox, 120f, 122f);
            Debug.Assert(Mathf.Abs(fitIcon.width / fitIcon.height - 120f / 122f) < 0.02f
                         && fitIcon.width < stretchBox.width - 8f,
                "[UiAtlasSelfCheck] FitInside가 가로 칸에서 아이콘을 늘리면 오너 21:50 위반");
            var five = UiPages.JobPickCards(new Rect(0f, 0f, 900f, 400f), 5);
            Debug.Assert(five.Length == 5 && five[3].x > five[0].x,
                "[UiAtlasSelfCheck] 직업 5장은 3×2 빈 칸이 아니라 Packed여야 한다");
            _ = nameof(UiAtlas.DrawFit);
            _ = nameof(UiPages.PackedCards);
            var tallCard = new Rect(0f, 0f, 280f, 220f);
            UiPages.CardLayout(tallCard, true, out var tallIcon, out var tallTitle, out var tallSub);
            Debug.Assert(tallIcon.y < tallTitle.y && tallIcon.height >= 24f,
                "[UiAtlasSelfCheck] 높은 카드는 아이콘이 위를 채워야 한다");
            Debug.Assert(UiAtlas.FitsInContent(tallCard, tallIcon)
                         && UiAtlas.FitsInContent(tallCard, tallTitle)
                         && UiAtlas.FitsInContent(tallCard, tallSub),
                "[UiAtlasSelfCheck] 높은 카드 글씨·아이콘이 금테 밖으로 나간다");
            var wideCard = new Rect(0f, 0f, 400f, 88f);
            UiPages.CardLayout(wideCard, true, out var wideIcon, out var wideTitle, out var wideSub);
            Debug.Assert(wideIcon.x < wideTitle.x && wideIcon.height >= 24f,
                "[UiAtlasSelfCheck] 넓은 카드는 아이콘이 왼쪽에 붙어야 한다");
            Debug.Assert(UiPages.IsSlimCard(wideCard)
                         && UiPages.TitleHOf(wideCard) == UiPages.SlimTitleH
                         && wideSub.height + 0.01f >= UiPages.SlimSubMin,
                "[UiAtlasSelfCheck] 높이 88 도크는 슬림 제목·부제 칸이 있어야 한다");
            string slimChrome = UiPages.CardChrome(wideCard);
            Debug.Assert(UiAtlas.FitsInContent(wideCard, wideIcon, slimChrome)
                         && UiAtlas.FitsInContent(wideCard, wideTitle, slimChrome)
                         && UiAtlas.FitsInContent(wideCard, wideSub, slimChrome),
                "[UiAtlasSelfCheck] 넓은 카드 글씨가 금테 밖으로 나간다");
            var hubWide = new Rect(0f, 0f, 596f, 169f);
            UiPages.CardLayout(hubWide, true, out var hubIcon, out var hubTitle, out var hubSub);
            Debug.Assert(UiPages.IsWideCard(hubWide),
                "[UiAtlasSelfCheck] 필드 2×3(596×169)은 가로 카드다");
            Debug.Assert(hubIcon.x < hubTitle.x && !UiPages.LayoutOverlaps(hubIcon, hubTitle)
                         && !UiPages.LayoutOverlaps(hubIcon, hubSub),
                "[UiAtlasSelfCheck] 가로 허브 카드는 아이콘 왼쪽·글씨 오른쪽");
            Debug.Assert(hubTitle.center.y < hubWide.center.y + 24f
                         && hubTitle.y > hubWide.y + 20f,
                "[UiAtlasSelfCheck] 가로 허브 제목은 아래 테두리가 아니라 세로 가운데");
            Debug.Assert(UiAtlas.FitsInContent(hubWide, hubIcon)
                         && UiAtlas.FitsInContent(hubWide, hubTitle)
                         && UiAtlas.FitsInContent(hubWide, hubSub),
                "[UiAtlasSelfCheck] 가로 허브 글씨가 금테 밖으로 나간다");
            var menuCard = new Rect(0f, 0f, 532f, 180f);
            UiPages.CardLayout(menuCard, true, out var menuIcon, out var menuTitle, out var menuSub);
            Debug.Assert(UiPages.IsWideCard(menuCard) && menuIcon.x < menuTitle.x,
                "[UiAtlasSelfCheck] 타이틀 1×3(532×180)은 가로 카드다");
            Debug.Assert(UiAtlas.FitsInContent(menuCard, menuIcon)
                         && UiAtlas.FitsInContent(menuCard, menuTitle)
                         && UiAtlas.FitsInContent(menuCard, menuSub),
                "[UiAtlasSelfCheck] 타이틀 카드 글씨가 금테에 겹친다");
            var menuQuit = new Rect(0f, 0f, 532f, 180f);
            UiPages.CardLayout(menuQuit, false, out _, out var quitTitle, out var quitSub);
            Debug.Assert(UiAtlas.FitsInContent(menuQuit, quitTitle)
                         && UiAtlas.FitsInContent(menuQuit, quitSub)
                         && Mathf.Abs(quitTitle.center.y - menuQuit.center.y) < 40f,
                "[UiAtlasSelfCheck] 아이콘 없는 타이틀 카드 제목이 금테·빈 가운데에 뜬다");
            var roster = new Rect(0f, 0f, 120f, UiPages.RosterCellH);
            UiPages.RosterCellLayout(roster, out var rf, out var rn, out var rj, out var rh);
            Debug.Assert(!UiPages.LayoutOverlaps(rf, rn) && !UiPages.LayoutOverlaps(rn, rj)
                         && !UiPages.LayoutOverlaps(rj, rh) && rh.yMax <= roster.yMax + 0.01f,
                "[UiAtlasSelfCheck] 명부 칸에서 초상·이름·직업·목숨이 겹친다");
            var party = new Rect(0f, 0f, 160f, 140f);
            UiPages.PartyCardLayout(party, out var pf, out var pn, out var pm);
            Debug.Assert(!UiPages.LayoutOverlaps(pf, pn) && !UiPages.LayoutOverlaps(pn, pm)
                         && pn.yMax <= pm.y + 0.01f,
                "[UiAtlasSelfCheck] 편성 카드에서 이름과 목숨이 겹친다");
            var huntPick = new Rect(0f, 0f, 397f, 132f);
            UiPages.PartyCardLayout(huntPick, out var hf, out var hn, out var hm);
            Debug.Assert(UiPages.IsWideCard(huntPick, 1.35f) && hf.x < hn.x && hn.x < hm.x,
                "[UiAtlasSelfCheck] 사냥 선택 카드는 초상 왼쪽·글씨 가운데");
            Debug.Assert(!UiPages.LayoutOverlaps(hf, hn) && !UiPages.LayoutOverlaps(hn, hm)
                         && !UiPages.LayoutOverlaps(hf, hm),
                "[UiAtlasSelfCheck] 사냥 선택 초상·이름·목숨이 겹치면 글씨가 초상 한가운데로 들어간다");

            // 편성 화면이 목숨·역할을 글자로만 쓰면 캐릭터 화면과 같은 조각이 소비처 0곳이다.
            var tankFull = UiAtlas.SlotChrome("탱", 0, false);
            Debug.Assert(tankFull.frame == "portrait_frame" && tankFull.role == "tank"
                         && tankFull.heart0 == "heart" && tankFull.heart2 == "heart",
                "[UiAtlasSelfCheck] 편성 명부 크롬(탱·목숨3)이 어긋난다");
            var dpsHurt = UiAtlas.SlotChrome("딜", 1, false);
            Debug.Assert(dpsHurt.role == "damage" && dpsHurt.heart0 == "heart"
                         && dpsHurt.heart2 == "heart_broken",
                "[UiAtlasSelfCheck] 편성 명부 크롬(딜·사망1)이 어긋난다");
            var dead = UiAtlas.SlotChrome("힐", 0, true);
            Debug.Assert(dead.role == "healer" && dead.heart0 == "heart_broken"
                         && dead.heart1 == "heart_broken" && dead.heart2 == "heart_broken",
                "[UiAtlasSelfCheck] 편성 명부 크롬(삭제)이 어긋난다");
            Debug.Assert(UiAtlas.SlotChrome("버퍼", 2, false).role == "buffer",
                "[UiAtlasSelfCheck] 편성 명부 크롬(버퍼)이 어긋난다");
            _ = nameof(UiAtlas.DrawRosterFrame);
            _ = nameof(UiAtlas.DrawRosterMarks);

            Debug.Assert(UiAtlas.ButtonStateSamples.Length == 3,
                "[UiAtlasSelfCheck] 버튼 3상태 견본 개수가 어긋난다");
            Debug.Assert(UiAtlas.ButtonKey(UiAtlas.ButtonStateSamples[0].hover,
                                          UiAtlas.ButtonStateSamples[0].pressed) == "button_normal"
                         && UiAtlas.ButtonKey(UiAtlas.ButtonStateSamples[1].hover,
                                              UiAtlas.ButtonStateSamples[1].pressed) == "button_hover"
                         && UiAtlas.ButtonKey(UiAtlas.ButtonStateSamples[2].hover,
                                              UiAtlas.ButtonStateSamples[2].pressed) == "button_pressed",
                "[UiAtlasSelfCheck] 견본 3칸이 보통·호버·눌림 조각과 어긋난다");
            Debug.Assert(UiAtlas.RectFor("field").width > 0 && UiAtlas.RectFor("tower").width > 0,
                "[UiAtlasSelfCheck] 필드·탑 헤더 조각이 없다");

            Debug.Assert(UiAtlas.RarityKey(GearGrade.Common) == "rarity_common"
                         && UiAtlas.RarityKey(GearGrade.Uncommon) == "rarity_uncommon"
                         && UiAtlas.RarityKey(GearGrade.Rare) == "rarity_rare"
                         && UiAtlas.RarityKey(GearGrade.Heroic) == "rarity_heroic"
                         && UiAtlas.RarityKey(GearGrade.Legendary) == "rarity_legendary",
                "[UiAtlasSelfCheck] 5등급 키가 아틀라스 조각과 어긋난다");
            Debug.Assert(UiAtlas.RaritySamples.Length == 5, "[UiAtlasSelfCheck] 등급 견본이 5종이 아니다");
            Debug.Assert(UiAtlas.RaritySamples[0].label == "일반"
                         && UiAtlas.RaritySamples[4].label == "전설",
                "[UiAtlasSelfCheck] 등급 견본 라벨이 기획서 §11과 어긋난다");
            for (int i = 0; i < UiAtlas.RaritySamples.Length; i++)
            {
                string key = UiAtlas.RarityKey(UiAtlas.RaritySamples[i].grade);
                Debug.Assert(UiAtlas.RectFor(key).width > 0,
                    $"[UiAtlasSelfCheck] 등급 조각 {key} 없음");
            }
            _ = nameof(UiAtlas.DrawRarity);

            Debug.Assert(System.Array.IndexOf(UiAtlas.RequiredKeys, UiAtlas.BossHpFrameKey) >= 0,
                "[UiAtlasSelfCheck] boss_hp_frame 이 RequiredKeys에 없다");
            Debug.Assert(UiAtlas.RectFor(UiAtlas.BossHpFrameKey).width > 0,
                "[UiAtlasSelfCheck] 보스 HP 프레임 조각이 없다");
            Debug.Assert(UiAtlas.PhaseCountForFloor(1) == 2
                         && UiAtlas.PhaseCountForFloor(5) == 2
                         && UiAtlas.PhaseCountForFloor(10) == 3
                         && UiAtlas.PhaseCountForFloor(15) == 2
                         && UiAtlas.PhaseCountForFloor(50) == 4,
                "[UiAtlasSelfCheck] 층별 페이즈 수가 §10-5와 어긋난다");
            Debug.Assert(UiAtlas.BossHpSamples.Length == 3,
                "[UiAtlasSelfCheck] 보스 HP 견본이 3칸이 아니다");
            Debug.Assert(UiAtlas.BossHpSamples[1].current == 4500f
                         && UiAtlas.BossHpSamples[1].phases == 2,
                "[UiAtlasSelfCheck] 1/2 견본이 페이즈 경계와 어긋난다");
            _ = nameof(UiAtlas.DrawBossHp);

            Debug.Log("[UiAtlasSelfCheck] PASS");
        }
    }
}
