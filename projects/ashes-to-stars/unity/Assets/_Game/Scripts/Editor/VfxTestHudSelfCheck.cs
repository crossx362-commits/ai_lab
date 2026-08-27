using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>VfxTestScreen 기본 6버튼. GUI.Button이면 FAIL, Row면 PASS.</summary>
    public static class VfxTestHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Vfx Test Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string src = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/VfxTestScreen.cs"));
            Check(src.IndexOf("GUI.Button", StringComparison.Ordinal) < 0,
                "기본 6버튼이 GUI.Button을 안 쓴다");
            Check(src.IndexOf("Row(side", StringComparison.Ordinal) >= 0,
                "기본 조작은 Row다");
            Check(src.IndexOf("자동 재생 중지", StringComparison.Ordinal) >= 0
                  && src.IndexOf("다음 페이지", StringComparison.Ordinal) >= 0
                  && src.IndexOf("이전 페이지", StringComparison.Ordinal) >= 0
                  && src.IndexOf("직업 이펙트 6종", StringComparison.Ordinal) >= 0
                  && src.IndexOf("상태·보스 이펙트 7종", StringComparison.Ordinal) >= 0
                  && src.IndexOf("영지로 돌아가기", StringComparison.Ordinal) >= 0,
                "6개 라벨이 있다");
            Check(src.IndexOf("Play(_keys", StringComparison.Ordinal) >= 0
                  && src.IndexOf("FxPool.PlayJob", StringComparison.Ordinal) >= 0
                  && src.IndexOf("FxPool.PlayStatus", StringComparison.Ordinal) >= 0,
                "재생 호출은 그대로다");

            if (_fail == 0) Debug.Log("[VfxTestHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[VfxTestHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[VfxTestHudSelfCheck] FAIL {_fail}건");
        }
    }
}
