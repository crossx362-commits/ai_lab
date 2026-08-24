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
            Environment.SetEnvironmentVariable(CharHud.EnvShow, null);
            Environment.SetEnvironmentVariable(CharHud.EnvNo, null);
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

            var face = UiPages.LargeLook(stage);
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

            // 정보 칸 바닥 — 옛 0.45 보간은 안쪽 금테 선(flat.yMax)보다 0.05×pad ≈ 5px 아래라
            // 목록 마지막 줄(장비 6줄·가방)이 선을 가로질러 글리프 하단이 덮였다(실측 2026-08-24,
            // polish_r65). InfoBottom은 금테 선에서 10px 위라 최악 줄 rect 바닥(yMax+2)도
            // 선 위 8px에 머문다. 아울러 바닥을 올리며 13줄(정보 5+장착 헤더+장비 6+가방)
            // ×20+6 = 266px 수용을 잃지 않는다 — 잃으면 잘림 결함이 돌아온다.
            // 치수는 DrawEquipStudio와 같게 — 런타임 체인 그대로: Body → AfterTabs(탭 62) →
            // RosterSplit → 다시 AfterTabs(장비·속성 탭 62) → 하단 액션바 56을 뺀 studio stage.
            var pageRect = UiPages.AfterTabs(body);
            CharHud.RosterSplit(pageRect, out var rtStage, out _);
            var afterTabs = UiPages.AfterTabs(rtStage);
            var studio = new Rect(rtStage.x, afterTabs.y, rtStage.width,
                Mathf.Max(80f, afterTabs.height - 56f));
            var studioChrome = UiAtlas.ContentRect(studio, "panel", 2f);
            var studioFlat = Rect.MinMaxRect(
                Mathf.Lerp(studio.x, studioChrome.x, 0.5f), Mathf.Lerp(studio.y, studioChrome.y, 0.5f),
                Mathf.Lerp(studio.xMax, studioChrome.xMax, 0.5f),
                Mathf.Lerp(studio.yMax, studioChrome.yMax, 0.5f));
            float infoBottom = CharHud.InfoBottom(studio, studioChrome);
            float infoTop = Mathf.Lerp(studio.y, studioChrome.y, 0.60f);
            Check(infoBottom <= studioFlat.yMax - 9f,
                $"정보 바닥 {infoBottom:0.0} ≤ 금테 선 {studioFlat.yMax:0.0} − 9 — 마지막 줄이 금테에 안 닿는다");
            Check(infoTop >= studioFlat.y + 8f,
                $"정보 꼭대기 {infoTop:0.0} ≥ 금테 선 {studioFlat.y:0.0} + 8 — 첫 줄도 금테에 안 붙는다");
            // 최악 14줄(표제·xp·전투력·출전·전직·편성·장착 헤더+장비 6)×20 + 가방 앞 간격 4
            // + 가방 줄 판정 18 — 바닥·꼭대기를 올리며 가방 줄을 잃지 않았는다(실측 2026-08-24:
            // 10px 여유만으로는 14줄 목록에서 가방 줄이 0.9px 넘겐 잘렸다).
            Check(infoBottom - infoTop >= 13f * 20f + 4f + 18f,
                $"정보 칸 높이 {infoBottom - infoTop:0} ≥ 14줄+간격 {13f * 20f + 4f + 18f:0} — 가방 줄이 안 잘린다");

            Check(CharHud.EquipLabel(stage,
                    new Rect(stage.x + 40f, stage.y + 80f, 48f, 48f)).width >= CharHud.LabelW,
                "flat 없는 옛 호출도 80폭 새 길");
            Check(CharHud.SlotLabel(EquipSlot.Accessory, null) == "장신구",
                "빈 장신구 라벨");
            Check(CharHud.Line().Contains("잘리지 않는다"),
                $"줄 (실제 {CharHud.Line()})");

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
            // 네거티브 — 차단하면 옛 0.45 보간(금테 선 아래 0.05×pad)으로 돌아가 결함이 재현된다.
            float oldInfoBottom = CharHud.InfoBottom(studio, studioChrome);
            Check(oldInfoBottom > studioFlat.yMax - 9f,
                $"차단 정보 바닥 {oldInfoBottom:0.0} > 금테 선 {studioFlat.yMax:0.0} − 9 — 옛 결함 경로");
            Check(CharHud.Line().Contains("잘린다") && !CharHud.Line().Contains("잘리지 않는다"),
                $"차단 줄 (실제 {CharHud.Line()})");
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

            Environment.SetEnvironmentVariable(CharHud.EnvShow, show);
            Environment.SetEnvironmentVariable(CharHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[CharHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[CharHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[CharHudSelfCheck] FAIL {_fail}건");
        }
    }
}
