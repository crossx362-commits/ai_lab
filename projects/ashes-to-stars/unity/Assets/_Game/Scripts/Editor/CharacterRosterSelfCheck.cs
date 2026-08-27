using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>캐릭터창 — 목록 왼쪽, 대형 모습·장비 오른쪽(오너 20:39).</summary>
    public static class CharacterRosterSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Character Roster Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            UiPages.RosterSplit(new Rect(0, 0, 1000, 400), out var list, out var stage);
            Check(list.x < stage.x, $"목록 x({list.x:0}) < 모습 x({stage.x:0})");
            Check(list.xMax <= stage.x + 0.01f, "목록 오른쪽이 모습 왼쪽을 넘지 않는다");
            Check(list.width < stage.width,
                $"목록({list.width:0})이 모습({stage.width:0})보다 좁다");
            Check(UiPages.RosterListRatio < 0.5f, "목록 비율은 절반 미만");

            var look = UiPages.LargeLook(stage);
            Check(look.width >= 160f && look.height >= 200f, "대형 모습은 160×200 이상");
            Check(look.width > 56f, "대형 모습이 목록 얼굴(56)보다 크다");
            Check(look.x >= stage.x && look.xMax <= stage.xMax, "대형 모습은 오른쪽 안에 있다");
            Check(look.yMax <= stage.yMax + 0.01f, "모습이 오른쪽 패널 아래로 안 넘친다");

            UiPages.RosterSplit(new Rect(0, 0, 1184, 348), out _, out var shortStage);
            var shortLook = UiPages.LargeLook(shortStage);
            Check(shortLook.yMax <= shortStage.yMax + 0.01f && shortLook.height >= 140f,
                "720p 본문에서도 모습이 패널 안에 보인다");

            var c0 = UiPages.RosterCell(list, 0);
            var c1 = UiPages.RosterCell(list, 1);
            var c3 = UiPages.RosterCell(list, 3);
            Check(Mathf.Approximately(c0.y, c1.y) && c0.x < c1.x,
                "바둑판 같은 줄은 왼→오");
            Check(c3.y > c0.y && Mathf.Approximately(c3.x, c0.x),
                "바둑판 다음 줄은 아래·같은 열");
            Check(UiPages.RosterCols == 3, "명부 바둑판은 3열");

            Check(UiPages.LookDir("탱") == "tank" && UiPages.LookDir("수호기사") == "tank",
                "탱 계열은 tank idle");
            Check(UiPages.LookDir("검사") == "dps" && UiPages.LookDir("딜") == "dps",
                "딜 계열은 dps idle");
            Check(UiPages.LookDir("마딜") == "mage" && UiPages.LookDir("마법사") == "mage"
                  && UiPages.LookDir("사제") == "healer" && UiPages.LookDir("음유시인") == "buffer",
                "마딜·마법·힐·버퍼 폴더");

            // 네거티브: 좌우를 손으로 뒤집으면 같은 단언이 실패해야 한다.
            var flippedList = stage;
            var flippedStage = list;
            Check(!(flippedList.x < flippedStage.x && flippedList.width < flippedStage.width),
                "좌우를 뒤집으면 통과 단언이 성립하지 않는다");

            _ = nameof(CharacterScreen);

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("ShortCopper(GameState.Wallet.Copper)")
                  && charSrc.IndexOf("FormatCurrency(GameState.Wallet.Copper)") < 0,
                "캐릭터 지갑은 ShortCopper만");
            Check(charSrc.Contains("ShortCopper(Fusion.CostCopper())")
                  && charSrc.IndexOf("FormatCurrency(Fusion.CostCopper())") < 0,
                "합성 안내 줄은 ShortCopper만");
            Check(charSrc.Contains("ShortCopper(cost)")
                  && charSrc.IndexOf("FormatCurrency(cost)") < 0,
                "소멸 비용은 ShortCopper만");

            int split = charSrc.IndexOf("void DrawRosterSplit", StringComparison.Ordinal);
            int cellFn = charSrc.IndexOf("void DrawRosterCell", StringComparison.Ordinal);
            Check(split >= 0 && cellFn > split, "DrawRosterSplit·DrawRosterCell가 있다");
            string splitSrc = (split >= 0 && cellFn > split)
                ? charSrc.Substring(split, cellFn - split) : "";
            Check(splitSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && splitSrc.IndexOf("cell.Contains", StringComparison.Ordinal) >= 0
                  && splitSrc.IndexOf("_selectedCharacter = i", StringComparison.Ordinal) >= 0,
                "로스터 셀 클릭은 MouseDown으로 _selectedCharacter를 바꾼다");
            Check(splitSrc.IndexOf("GUI.Button(cell", StringComparison.Ordinal) < 0,
                "DrawRosterCell 뒤 GUI.Button(none)을 안 쓴다");

            int studio = charSrc.IndexOf("void DrawEquipStudio", StringComparison.Ordinal);
            int inspect = charSrc.IndexOf("void DrawInspectInfoScrolled", StringComparison.Ordinal);
            Check(studio >= 0 && inspect > studio, "DrawEquipStudio가 있다");
            string studioSrc = (studio >= 0 && inspect > studio)
                ? charSrc.Substring(studio, inspect - studio) : "";
            Check(studioSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && studioSrc.IndexOf("slotRect.Contains", StringComparison.Ordinal) >= 0
                  && studioSrc.IndexOf("TryUnequip", StringComparison.Ordinal) >= 0
                  && studioSrc.IndexOf("_bagFilter = (int)slot", StringComparison.Ordinal) >= 0,
                "장비 링 슬롯 클릭은 MouseDown으로 해제·필터한다");
            Check(studioSrc.IndexOf("GUI.Button(slotRect", StringComparison.Ordinal) < 0,
                "DrawGear 뒤 GUI.Button(slotRect none)을 안 쓴다");

            int inspectFn = charSrc.IndexOf("void DrawInspectInfo(Rect", StringComparison.Ordinal);
            int lookFn = charSrc.IndexOf("static void DrawSelectedLook", StringComparison.Ordinal);
            Check(inspectFn >= 0 && lookFn > inspectFn, "DrawInspectInfo가 있다");
            string inspectSrc = (inspectFn >= 0 && lookFn > inspectFn)
                ? charSrc.Substring(inspectFn, lookFn - inspectFn) : "";
            Check(inspectSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && inspectSrc.IndexOf("gcell.Contains", StringComparison.Ordinal) >= 0
                  && inspectSrc.IndexOf("TryEquip", StringComparison.Ordinal) >= 0,
                "가방 셀 클릭은 MouseDown으로 장착한다");
            Check(inspectSrc.IndexOf("GUI.Button(gcell", StringComparison.Ordinal) < 0,
                "DrawGear 뒤 GUI.Button(gcell none)을 안 쓴다");

            Check(inspectSrc.IndexOf("DrawBagFilterTab", StringComparison.Ordinal) >= 0,
                "가방 줄이 DrawBagFilterTab을 그린다");
            int tabFn = charSrc.IndexOf("void DrawBagFilterTab", StringComparison.Ordinal);
            int compact = charSrc.IndexOf("bool CompactAction", StringComparison.Ordinal);
            Check(tabFn >= 0 && compact > tabFn, "DrawBagFilterTab이 있다");
            string tabSrc = (tabFn >= 0 && compact > tabFn)
                ? charSrc.Substring(tabFn, compact - tabFn) : "";
            Check(tabSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && tabSrc.IndexOf("tr.Contains", StringComparison.Ordinal) >= 0
                  && tabSrc.IndexOf("_bagFilter = filter", StringComparison.Ordinal) >= 0,
                "가방 필터 탭 클릭은 MouseDown으로 _bagFilter를 바꾼다");
            Check(tabSrc.IndexOf("GUI.Button(tr", StringComparison.Ordinal) < 0,
                "DrawBagFilterTab이 GUI.Button(none)을 안 쓴다");

            if (_fail == 0) Debug.Log("[CharacterRosterSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[CharacterRosterSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[CharacterRosterSelfCheck] FAIL {_fail}건");
        }
    }
}
