using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 허브 HUD가 배경을 덜 가린다. QA_NO면 옛 2×3 전폭(§16).</summary>
    public static class FieldHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Field Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(FieldHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(FieldHud.EnvNo);
            Environment.SetEnvironmentVariable(FieldHud.EnvShow, null);
            Environment.SetEnvironmentVariable(FieldHud.EnvNo, null);
            FieldHud.ResetForTest();

            var body = new Rect(36f, 100f, 1208f, FieldHud.OldBodyH);
            var slim = FieldHud.Cards(body);
            Check(slim.Length == 6, $"도크 6칸 (실제 {slim.Length})");
            Check(Mathf.Approximately(FieldHud.OverlayH(body), FieldHud.DockH),
                $"겹침 {FieldHud.OverlayH(body):0} = 도크 {FieldHud.DockH:0}");
            Check(FieldHud.OverlayH(body) < 220f, "겹침 < 220 (옛 540)");
            Check(FieldHud.OverlayH(body) < body.height * 0.40f,
                $"겹침 {FieldHud.OverlayH(body):0} < 본문 40%");
            Check(FieldHud.OpenH(body) > 300f,
                $"열린 배경 {FieldHud.OpenH(body):0} > 300");
            Check(slim[0].y > body.y + body.height * 0.55f,
                $"도크 y {slim[0].y:0} 는 아래쪽");
            Check(slim[0].height < 110f,
                $"도크 칸 높이 {slim[0].height:0} < 110");
            Check(slim[0].width > 300f,
                $"도크 칸 폭 {slim[0].width:0} 가로 카드");
            float dockBottom = slim[slim.Length - 1].yMax;
            float navTop = FieldHud.NavPlateTop();
            Check(dockBottom <= navTop - FieldHud.NavGap + 0.01f,
                $"도크 아랫변 {dockBottom:0} ≤ 내비-간격 {navTop - FieldHud.NavGap:0}");
            Check(navTop - dockBottom >= 10f,
                $"도크-내비 간격 {navTop - dockBottom:0} ≥ 10 (전폭 카드가 내비와 한 덩어리가 되지 않게)");
            Check(FieldHud.Line().Contains("가리지 않는다"),
                $"줄 (실제 {FieldHud.Line()})");
            Check(FieldHud.Line().Contains("내비"),
                $"줄에 내비 (실제 {FieldHud.Line()})");

            Environment.SetEnvironmentVariable(FieldHud.EnvNo, "1");
            Check(FieldHud.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(FieldHud.OverlayH(body), body.height),
                $"차단 겹침 {FieldHud.OverlayH(body):0} = 옛 540");
            var old = FieldHud.Cards(body);
            Check(old[0].height > 150f, $"차단 칸 {old[0].height:0} 전폭 카드");
            Check(old[0].y < body.y + 20f, "차단하면 본문 위에서 시작");
            Check(old[old.Length - 1].yMax > FieldHud.NavPlateTop() - 1f,
                $"차단 아랫변 {old[old.Length - 1].yMax:0} 이 내비와 겹친다");
            Check(FieldHud.Line().Contains("가린다"),
                $"차단 줄 (실제 {FieldHud.Line()})");
            Environment.SetEnvironmentVariable(FieldHud.EnvNo, null);

            Environment.SetEnvironmentVariable(FieldHud.EnvShow, "1");
            FieldHud.SeedQaIfRequested();
            Check(FieldHud.ShowQa, "시드 켜짐");
            Check(FieldHud.Line().Contains("가리지 않는다"), "시드 줄");
            Environment.SetEnvironmentVariable(FieldHud.EnvShow, null);
            FieldHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string field = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            Check(field.Contains("FieldHud.Cards"), "필드가 Cards를 읽는다");
            Check(field.Contains("FieldHud.Line"), "자막이 Line을 읽는다");
            Check(field.Contains("FieldHud.SeedQaIfRequested"), "시드를 읽는다");
            Check(!field.Contains("UiPages.Grid(r, 2, 3"),
                "옛 2×3 전폭 Grid를 안 쓴다");
            string hud = File.ReadAllText(Path.Combine(runtime, "FieldHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "도크가 NavPlateTop을 읽는다 (body.yMax 붙이기 금지)");

            Environment.SetEnvironmentVariable(FieldHud.EnvShow, show);
            Environment.SetEnvironmentVariable(FieldHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[FieldHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[FieldHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[FieldHudSelfCheck] FAIL {_fail}건");
        }
    }
}
