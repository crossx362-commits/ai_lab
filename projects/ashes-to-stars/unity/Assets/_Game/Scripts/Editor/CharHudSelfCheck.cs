using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>캐릭터창 3열·장비 둘레 글씨가 잘리지 않는다. QA_NO면 옛 좁은 칸(§16).</summary>
    public static class CharHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Char Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(CharHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(CharHud.EnvNo);
            string noNav = Environment.GetEnvironmentVariable(CharHud.EnvNoNav);
            string noPortrait = Environment.GetEnvironmentVariable(CharHud.EnvNoPortraitFit);
            string noScroll = Environment.GetEnvironmentVariable(CharHud.EnvNoScroll);
            string noSelTint = Environment.GetEnvironmentVariable(CharHud.EnvNoSelTint);
            Environment.SetEnvironmentVariable(CharHud.EnvShow, null);
            Environment.SetEnvironmentVariable(CharHud.EnvNo, null);
            Environment.SetEnvironmentVariable(CharHud.EnvNoNav, null);
            Environment.SetEnvironmentVariable(CharHud.EnvNoPortraitFit, null);
            Environment.SetEnvironmentVariable(CharHud.EnvNoScroll, null);
            Environment.SetEnvironmentVariable(CharHud.EnvNoSelTint, null);
            CharHud.ResetForTest();

            var body = new Rect(36f, 56f, 1208f, 584f);
            CharHud.RosterSplit(body, out var list, out var stage);
            Check(list.x < stage.x, $"목록 x({list.x:0}) < 모습 x({stage.x:0})");
            Check(list.width < stage.width,
                $"목록({list.width:0})이 모습({stage.width:0})보다 좁다");
            Check(list.width >= CharHud.MinListW,
                $"목록 {list.width:0} ≥ {CharHud.MinListW:0}");
            Check(CharHud.ListW(body) >= CharHud.MinListW,
                $"ListW {CharHud.ListW(body):0} ≥ {CharHud.MinListW:0}");

            var c0 = CharHud.RosterCell(list, 0);
            var c1 = CharHud.RosterCell(list, 1);
            var c3 = CharHud.RosterCell(list, 3);
            Check(c0.width >= CharHud.MinCellW,
                $"칸 폭 {c0.width:0} ≥ {CharHud.MinCellW:0}");
            Check(Mathf.Approximately(c0.y, c1.y) && c0.x < c1.x,
                "바둑판 같은 줄은 왼→오");
            Check(c3.y > c0.y && Mathf.Approximately(c3.x, c0.x),
                "바둑판 다음 줄은 아래·같은 열");
            Check(CharHud.Cols == 3, "명부는 3열");
            Check(Mathf.Approximately(c0.height, CharHud.CellH),
                $"칸 높이 {c0.height:0} = {CharHud.CellH:0}");

            UiPages.RosterCellLayout(c0, out _, out var nameR, out _, out var heartsR);
            Check(nameR.width >= 108f, $"이름 칸 {nameR.width:0} ≥ 108");
            Check(nameR.yMax <= heartsR.y + 0.01f,
                $"이름 아래 {nameR.yMax:0.0} ≤ 하트 위 {heartsR.y:0.0} — 끝글자가 하트에 안 덮인다");

            var oldFace = UiPages.LargeLook(stage);
            var face = CharHud.EquipPortrait(stage);
            Check(face.width < oldFace.width && face.height < oldFace.height,
                $"장비 초상 {face.width:0}×{face.height:0} < 옛 {oldFace.width:0}×{oldFace.height:0}");
            Check(Vector2.Distance(face.center, oldFace.center) < 0.01f,
                "장비 초상을 줄여도 링 중심축은 유지한다");
            Environment.SetEnvironmentVariable(CharHud.EnvNoPortraitFit, "1");
            var blockedFace = CharHud.EquipPortrait(stage);
            Check(Mathf.Approximately(blockedFace.width, oldFace.width)
                  && Mathf.Approximately(blockedFace.height, oldFace.height),
                "QA_NO_CHAR_PORTRAIT_FIT이면 옛 큰 초상");
            Environment.SetEnvironmentVariable(CharHud.EnvNoPortraitFit, null);
            CharHud.EquipRingFit(stage, face, out float ringX, out float ringY);
            int labelOk = 0;
            for (int i = 0; i < UiPages.EquipRingDegrees.Length; i++)
            {
                var slot = UiPages.ClampIn(stage,
                    UiPages.SlotOnRing(face.center, ringX, ringY,
                        UiPages.EquipRingDegrees[i], UiPages.EquipSlotSize));
                var lab = CharHud.EquipLabel(stage, slot);
                bool inside = lab.x >= stage.x - 0.01f && lab.xMax <= stage.xMax + 0.01f
                    && lab.y >= stage.y - 0.01f && lab.yMax <= stage.yMax + 0.01f;
                bool below = lab.y >= slot.yMax - 0.01f;
                bool wide = lab.width + 0.01f >= CharHud.LabelW;
                bool noOverlap = !lab.Overlaps(new Rect(slot.x, slot.y, slot.width, slot.height - 1f));
                if (inside && below && wide && noOverlap) labelOk++;
                Check(inside, $"라벨 {i} 패널 안 {lab}");
                Check(below, $"라벨 {i} 칸 아래 y {lab.y:0} ≥ {slot.yMax:0}");
                Check(wide, $"라벨 {i} 폭 {lab.width:0} ≥ {CharHud.LabelW:0}");
                Check(noOverlap, $"라벨 {i}가 칸과 안 겹친다");
            }
            Check(labelOk == 6, $"둘레 라벨 6/6 통과 (실제 {labelOk})");

            // 평평한 내부(flat) 판정 — 라벨이 장식 여백으로 나가면 결함이다(실측 2026-08-24:
            // 갑옷 라벨 패널 가장자리 절단·신발 라벨 하단 금색 장식 위 묻힘). DrawEquipStudio가
            // 쓰는 것과 같은 stage↔chrome 0.5 보간선으로 flat을 만들어 검사한다.
            var chromeFlat = UiAtlas.ContentRect(stage, "panel", 2f);
            var flat = Rect.MinMaxRect(
                Mathf.Lerp(stage.x, chromeFlat.x, 0.5f), Mathf.Lerp(stage.y, chromeFlat.y, 0.5f),
                Mathf.Lerp(stage.xMax, chromeFlat.xMax, 0.5f), Mathf.Lerp(stage.yMax, chromeFlat.yMax, 0.5f));
            int flatOk = 0;
            for (int i = 0; i < UiPages.EquipRingDegrees.Length; i++)
            {
                var slot = UiPages.ClampIn(stage,
                    UiPages.SlotOnRing(face.center, ringX, ringY,
                        UiPages.EquipRingDegrees[i], UiPages.EquipSlotSize));
                var lab = CharHud.EquipLabel(stage, slot, flat);
                bool inFlat = lab.x >= flat.x - 0.01f && lab.xMax <= flat.xMax + 0.01f
                    && lab.y >= flat.y - 0.01f && lab.yMax <= flat.yMax + 0.01f;
                bool clear = !lab.Overlaps(new Rect(slot.x, slot.y, slot.width, slot.height));
                if (inFlat && clear) flatOk++;
                Check(inFlat, $"평평한 내부 라벨 {i} 안 ({lab})");
                Check(clear, $"평평한 내부 라벨 {i}가 칸과 안 겹친다");
            }
            Check(flatOk == 6, $"평평한 내부 라벨 6/6 통과 (실제 {flatOk})");

            // 정보 칸 바닥 — 실측(2026-08-24, polish_r66 플레이모드 샷 픽셀 재단)하면 「panel」의
            // 안쪽 금테 선은 pad의 ≈2/3 지점이라 옛 0.45(0.5 flat도 마찬가지)는 선보다 아래로
            // 내려와 목록 마지막 줄이 선에 덮였다. InfoBottom은 실제 선(0.667)에서 8px 위.
            // 아울러 바닥을 올리며 14줄(표제·xp·전투력·상태·전직·편성·장착 헤더+장비 6)×18
            // + 가방 간격 4 + 가방 판정 16 = 254px 수용을 잃지 않는다 — 잃으면 가방 줄 잘림이 돌아온다.
            // 꼭대기도 같은 실측 선(polish_r67: 하트 밴드가 상단 선 밴드와 14행 겹침 · 0.667 예측과
            // 실측 내연 1px 일치) — InfoTop은 실제 선에서 4px 아래(8px면 254px 예산이 깨진다).
            // 치수는 DrawEquipStudio와 같게 — 런타임 체인 그대로: Body → AfterTabs(탭 62) →
            // RosterSplit → 다시 AfterTabs(장비·속성 탭 62) → 하단 액션바 56을 뺀 studio stage.
            var pageRect = UiPages.AfterTabs(body);
            CharHud.RosterSplit(pageRect, out var rtStage, out _);
            var afterTabs = UiPages.AfterTabs(rtStage);
            var studio = new Rect(rtStage.x, afterTabs.y, rtStage.width,
                Mathf.Max(80f, afterTabs.height - 56f));
            var studioChrome = UiAtlas.ContentRect(studio, "panel", 2f);
            float frameLine = Mathf.Lerp(studio.yMax, studioChrome.yMax, CharHud.FrameLineFrac);
            float topLine = Mathf.Lerp(studio.y, studioChrome.y, CharHud.FrameLineFrac);
            float infoBottom = CharHud.InfoBottom(studio, studioChrome);
            float infoTop = CharHud.InfoTop(studio, studioChrome);
            Check(infoBottom <= frameLine - (CharHud.InfoBottomGap - 1f),
                $"정보 바닥 {infoBottom:0.0} ≤ 실측 금테 선 {frameLine:0.0} − {CharHud.InfoBottomGap - 1f:0} — 마지막 줄이 금테에 안 닿는다");
            Check(infoTop >= topLine + (CharHud.InfoTopGap - 1f),
                $"정보 꼭대기 {infoTop:0.0} ≥ 실측 상단 선 {topLine:0.0} + {CharHud.InfoTopGap - 1f:0} — 하트가 상단 금테에 안 닿는다");
            Check(infoBottom - infoTop >= 13f * 18f + 4f + 16f,
                $"정보 칸 높이 {infoBottom - infoTop:0.0} ≥ 14줄+간격 {13f * 18f + 4f + 16f:0} — 가방 줄이 안 잘린다");

            Check(CharHud.EquipLabel(stage,
                    new Rect(stage.x + 40f, stage.y + 80f, 48f, 48f)).width >= CharHud.LabelW,
                "flat 없는 옛 호출도 80폭 새 길");
            Check(CharHud.SlotLabel(EquipSlot.Accessory, null) == "장신구",
                "빈 장신구 라벨");
            Check(CharHud.Line().Contains("잘리지 않는다"),
                $"줄 (실제 {CharHud.Line()})");
            Check(LifeSystem.JobFace("딜") == "검사·궁수",
                $"딜 얼굴 (실제 {LifeSystem.JobFace("딜")})");
            Check(LifeSystem.JobFace("마딜") == "마법사·소환사",
                $"마딜 얼굴 (실제 {LifeSystem.JobFace("마딜")})");
            Check(LifeSystem.JobFace("힐") == "사제·드루이드",
                $"힐 얼굴 (실제 {LifeSystem.JobFace("힐")})");
            Check(LifeSystem.JobFace("탱") == "수호기사·광전사",
                $"탱 얼굴 (실제 {LifeSystem.JobFace("탱")})");
            Check(LifeSystem.JobFace("버퍼") == "음유시인 외",
                $"버퍼 얼굴은 세 종이 칸을 넘치지 않게 외 (실제 {LifeSystem.JobFace("버퍼")})");
            Check(LifeSystem.JobFace("수호기사") == "수호기사",
                "1차 직업명은 그대로");
            Check(CharHud.JobFace("딜") == "검사·궁수",
                $"표시 딜 (실제 {CharHud.JobFace("딜")})");
            Check(CharHud.JobFace("딜") != "딜" && CharHud.JobFace("마딜") != "마딜"
                  && CharHud.JobFace("힐") != "힐" && CharHud.JobFace("버퍼") != "버퍼",
                "표시는 기본직 ID가 아니다");

            var hubBody = new Rect(36f, HubHeader.SlimBodyTop, 1208f,
                720f - HubHeader.SlimBodyTop - UiPages.NavReserve);
            var page = UiPages.AfterTabs(hubBody);
            var content = CharHud.Content(page);
            float navTop = CharHud.NavPlateTop();
            Check(content.yMax <= navTop - CharHud.NavGap + 0.01f,
                $"액션바 아랫변 {content.yMax:0} ≤ 내비-간격 {navTop - CharHud.NavGap:0}");
            Check(navTop - content.yMax >= 10f,
                $"액션바-내비 간격 {navTop - content.yMax:0} ≥ 10 (전폭 금테가 내비와 한 덩어리가 되지 않게)");
            Check(Mathf.Approximately(content.x, page.x) && Mathf.Approximately(content.width, page.width),
                "본문 가로는 그대로");

            Environment.SetEnvironmentVariable(CharHud.EnvNoNav, "1");
            Check(CharHud.NavBlocked, "QA_NO_CHAR_NAV면 차단");
            var oldNav = CharHud.Content(page);
            Check(oldNav.yMax > CharHud.NavPlateTop() - 1f,
                $"차단 아랫변 {oldNav.yMax:0} 이 내비와 겹친다");
            Environment.SetEnvironmentVariable(CharHud.EnvNoNav, null);

            Environment.SetEnvironmentVariable(CharHud.EnvNo, "1");
            Check(CharHud.Blocked, "QA_NO면 차단");
            Check(CharHud.ListW(body) < 450f,
                $"차단 목록 {CharHud.ListW(body):0} < 450 (옛 435)");
            CharHud.RosterSplit(body, out var oldList, out _);
            var oldCell = CharHud.RosterCell(oldList, 0);
            Check(oldCell.width < CharHud.MinCellW,
                $"차단 칸 {oldCell.width:0} < {CharHud.MinCellW:0}");
            UiPages.RosterCellLayout(oldCell, out _, out var oldName, out _, out var oldHearts);
            Check(oldName.width < 100f, $"차단 이름 {oldName.width:0} < 100");
            Check(oldName.yMax <= oldHearts.y + 0.01f,
                $"차단 칸에서도 이름 아래 {oldName.yMax:0.0} ≤ 하트 위 {oldHearts.y:0.0}");
            Check(Mathf.Approximately(oldCell.height, CharHud.OldCellH),
                $"차단 높이 {oldCell.height:0} = 옛 132");
            var oldLab = CharHud.EquipLabel(stage, new Rect(stage.x + 40f, stage.y + 80f, 48f, 48f));
            Check(oldLab.width <= CharHud.OldLabelW + 0.01f,
                $"차단 라벨 {oldLab.width:0} ≤ 옛 48");
            // 네거티브 — 차단하면 옛 0.45 보간으로 돌아가 실측 금테 선(0.667)보다 아래로 내려가 결함이 재현된다.
            float oldInfoBottom = CharHud.InfoBottom(studio, studioChrome);
            Check(oldInfoBottom > frameLine - 1f,
                $"차단 정보 바닥 {oldInfoBottom:0.0} > 실측 금테 선 {frameLine:0.0} − 1 — 옛 결함 경로");
            // 네거티브(꼭대기) — 차단하면 옛 0.62 보간으로 돌아가 실측 상단 선보다 위라 하트가 금테에 얹힌다.
            float oldInfoTop = CharHud.InfoTop(studio, studioChrome);
            Check(oldInfoTop < topLine,
                $"차단 정보 꼭대기 {oldInfoTop:0.0} < 실측 상단 선 {topLine:0.0} — 옛 결함 경로");
            Check(CharHud.Line().Contains("잘린다") && !CharHud.Line().Contains("잘리지 않는다"),
                $"차단 줄 (실제 {CharHud.Line()})");
            Check(CharHud.JobFace("딜") == "딜" && CharHud.JobFace("마딜") == "마딜",
                $"차단 직업은 옛 ID (실제 {CharHud.JobFace("딜")}/{CharHud.JobFace("마딜")})");
            Environment.SetEnvironmentVariable(CharHud.EnvNo, null);

            Environment.SetEnvironmentVariable(CharHud.EnvShow, "1");
            CharHud.SeedQaIfRequested();
            Check(CharHud.ShowQa, "시드 켜짐");
            Check(CharHud.Line().Contains("잘리지 않는다"), "시드 줄");
            Environment.SetEnvironmentVariable(CharHud.EnvShow, null);
            CharHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(screen.Contains("CharHud.RosterSplit"), "명부가 RosterSplit을 읽는다");
            Check(screen.Contains("CharHud.RosterCell"), "칸이 RosterCell을 읽는다");
            Check(screen.Contains("CharHud.EquipLabel"), "둘레가 EquipLabel을 읽는다");
            Check(screen.Contains("CharHud.EquipRingFit"), "링이 EquipRingFit을 읽는다");
            Check(screen.Contains("CharHud.Line"), "자막이 Line을 읽는다");
            Check(screen.Contains("CharHud.SeedQaIfRequested"), "시드를 읽는다");
            Check(screen.Contains("CharHud.SlotLabel"), "칸 이름이 SlotLabel을 읽는다");
            Check(screen.Contains("CharHud.InfoBottom"),
                "정보 칸 바닥이 InfoBottom을 읽는다 (옛 인라인 0.45 금지)");
            Check(screen.Contains("CharHud.InfoTop"),
                "정보 칸 꼭대기가 InfoTop을 읽는다 (옛 인라인 0.62 금지)");
            Check(screen.Contains("CharHud.JobFace"),
                "명부·헤더 직업이 JobFace를 읽는다 (옛 ch.Job ID 금지)");
            Check(screen.Contains("CharHud.Content"),
                "명부가 Content를 읽는다 (page.yMax 붙이기 금지)");
            // 스크롤 — 속성 30행·정보 칸 가방은 패널 예산을 넘겨 옛엔 조용히 잘렸다(오너 2026-08-25).
            // 새 길은 스크롤로 끝까지 도달하고, QA_NO_CHAR_SCROLL이면 옛 잘림 경로다.
            Check(screen.Contains("DrawAttributesScrolled") && screen.Contains("GUI.BeginScrollView"),
                "속성 탭이 스크롤로 끝까지 도달한다 (옛 조용한 절단 금지)");
            Check(screen.Contains("DrawInspectInfoScrolled"),
                "장비 정보 칸이 스크롤로 가방 전체를 보인다 (8칸 제한 금지)");
            Check(screen.Contains("CharHud.ScrollBlocked"),
                "스크롤이 QA_NO_CHAR_SCROLL 게이트를 읽는다");
            Check(screen.Contains("CharHud.SelTintBlocked") && screen.Contains("DockLabel(nameR"),
                "선택 셀 이름이 DockLabel 강조를 읽는다 (선택 피드백)");
            // 스크롤 가상공간의 하단 소비처 — InfoAt의 REF_H(720) 절대 컷이 y>680 행
            // (보유 스킬·초필·종족 5줄·부활초 상한·사망 상한·성능 예산)을 조용히 지웠다
            // (2026-08-26 플레이모드 실측). 접힘 한계가 필드화돼 스크롤 경로만 해제된다.
            Check(screen.Contains("InfoFoldLimit = contentH") && screen.Contains("InfoFoldLimit = REF_H"),
                "속성 스크롤이 접힘 한계를 올렸다 되돌린다 (하단 소비처 생존)");
            string gameScreen = File.ReadAllText(Path.Combine(runtime, "GameScreen.cs"));
            Check(gameScreen.Contains("InfoFoldLimit")
                  && !gameScreen.Contains("if (panel.yMax > REF_H) return;"),
                "InfoAt이 InfoFoldLimit을 읽는다 (REF_H 하드컷 금지)");
            Environment.SetEnvironmentVariable(CharHud.EnvNoScroll, "1");
            Check(CharHud.ScrollBlocked, "QA_NO_CHAR_SCROLL면 차단");
            Environment.SetEnvironmentVariable(CharHud.EnvNoScroll, null);
            Check(!CharHud.ScrollBlocked, "게이트 끄면 스크롤 복귀");
            Environment.SetEnvironmentVariable(CharHud.EnvNoSelTint, "1");
            Check(CharHud.SelTintBlocked, "QA_NO_CHAR_SEL_TINT면 차단");
            Environment.SetEnvironmentVariable(CharHud.EnvNoSelTint, null);
            Check(!CharHud.SelTintBlocked, "게이트 끄면 선택 강조 복귀");
            string hud = File.ReadAllText(Path.Combine(runtime, "CharHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "액션바가 NavPlateTop을 읽는다 (body.yMax 붙이기 금지)");
            Check(hud.Contains("NavGap"),
                "액션바가 NavGap을 읽는다");

            Environment.SetEnvironmentVariable(CharHud.EnvShow, show);
            Environment.SetEnvironmentVariable(CharHud.EnvNo, no);
            Environment.SetEnvironmentVariable(CharHud.EnvNoNav, noNav);
            Environment.SetEnvironmentVariable(CharHud.EnvNoPortraitFit, noPortrait);
            Environment.SetEnvironmentVariable(CharHud.EnvNoScroll, noScroll);
            Environment.SetEnvironmentVariable(CharHud.EnvNoSelTint, noSelTint);
            if (_fail == 0) Debug.Log("[CharHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[CharHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[CharHudSelfCheck] FAIL {_fail}건");
        }
    }
}
