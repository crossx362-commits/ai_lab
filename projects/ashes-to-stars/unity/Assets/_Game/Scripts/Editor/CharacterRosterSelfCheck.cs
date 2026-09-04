using System;
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

            if (_fail == 0) Debug.Log("[CharacterRosterSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[CharacterRosterSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[CharacterRosterSelfCheck] FAIL {_fail}건");
        }
    }
}
