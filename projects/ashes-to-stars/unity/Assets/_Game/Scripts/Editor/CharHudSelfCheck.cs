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

            Environment.SetEnvironmentVariable(CharHud.EnvShow, show);
            Environment.SetEnvironmentVariable(CharHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[CharHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[CharHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[CharHudSelfCheck] FAIL {_fail}건");
        }
    }
}
