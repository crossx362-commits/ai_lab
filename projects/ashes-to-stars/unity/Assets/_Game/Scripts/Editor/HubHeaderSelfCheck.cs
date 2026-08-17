using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>허브 제목판이 세계를 덜 가린다. QA_NO면 옛 88(§16).</summary>
    public static class HubHeaderSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Hub Header Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(HubHeader.EnvShow);
            string no = Environment.GetEnvironmentVariable(HubHeader.EnvNo);
            Environment.SetEnvironmentVariable(HubHeader.EnvShow, null);
            Environment.SetEnvironmentVariable(HubHeader.EnvNo, null);
            HubHeader.ResetForTest();

            Check(Mathf.Approximately(HubHeader.H, HubHeader.SlimH),
                $"높이 {HubHeader.H:0} = 슬림 {HubHeader.SlimH:0}");
            Check(Mathf.Approximately(GameScreen.HeaderH, HubHeader.SlimH),
                $"GameScreen.HeaderH {GameScreen.HeaderH:0} = 슬림");
            Check(Mathf.Approximately(GameScreen.BodyTop, HubHeader.SlimBodyTop),
                $"GameScreen.BodyTop {GameScreen.BodyTop:0} = 슬림 {HubHeader.SlimBodyTop:0}");
            Check(HubHeader.H < 60f, $"높이 {HubHeader.H:0} < 60 (옛 88)");
            Check(HubHeader.BodyTop < 70f, $"본문 시작 {HubHeader.BodyTop:0} < 70 (옛 100)");
            Check(HubHeader.H < HubHeader.OldH, "슬림이 옛 88보다 낮다");
            float open = HubHeader.OpenH(UiPages.NavReserve);
            float oldOpen = HubHeader.ScreenH - HubHeader.OldBodyTop - UiPages.NavReserve;
            Check(open > oldOpen, $"열린 본문 {open:0} > 옛 {oldOpen:0}");
            Check(open > 560f, $"열린 본문 {open:0} > 560");
            Check(HubHeader.IconSize < 48f, $"아이콘 {HubHeader.IconSize:0} < 48 (옛 60)");
            var icon = HubHeader.IconRect();
            Check(icon.yMax <= HubHeader.H, $"아이콘 바닥 {icon.yMax:0} ≤ 제목판 {HubHeader.H:0}");
            var title = HubHeader.TitleRect(true);
            Check(title.yMax <= HubHeader.H, $"제목 바닥 {title.yMax:0} ≤ 제목판");
            Check(title.x > icon.x, "제목이 아이콘 오른쪽");
            Check(HubHeader.Line().Contains("가리지 않는다"),
                $"줄 (실제 {HubHeader.Line()})");

            Environment.SetEnvironmentVariable(HubHeader.EnvNo, "1");
            Check(HubHeader.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(HubHeader.H, HubHeader.OldH),
                $"차단 높이 {HubHeader.H:0} = 옛 88");
            Check(Mathf.Approximately(GameScreen.HeaderH, HubHeader.OldH),
                $"차단 GameScreen.HeaderH {GameScreen.HeaderH:0} = 88");
            Check(Mathf.Approximately(GameScreen.BodyTop, HubHeader.OldBodyTop),
                $"차단 BodyTop {GameScreen.BodyTop:0} = 100");
            Check(HubHeader.IconSize >= 60f, $"차단 아이콘 {HubHeader.IconSize:0} = 60");
            Check(HubHeader.Line().Contains("가린다"),
                $"차단 줄 (실제 {HubHeader.Line()})");
            Environment.SetEnvironmentVariable(HubHeader.EnvNo, null);

            Environment.SetEnvironmentVariable(HubHeader.EnvShow, "1");
            HubHeader.SeedQaIfRequested();
            Check(HubHeader.ShowQa, "시드 켜짐");
            Check(HubHeader.Line().Contains("가리지 않는다"), "시드 줄");
            Environment.SetEnvironmentVariable(HubHeader.EnvShow, null);
            HubHeader.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "GameScreen.cs"));
            Check(screen.Contains("HubHeader.H") || screen.Contains("HubHeader.BodyTop"),
                "GameScreen이 HubHeader 높이를 읽는다");
            Check(screen.Contains("HubHeader.IconRect") || screen.Contains("HubHeader.TitleRect"),
                "제목판이 Icon/Title 칸을 읽는다");
            Check(screen.Contains("HubHeader.Line"), "자막이 Line을 읽는다");
            Check(screen.Contains("HubHeader.SeedQaIfRequested"), "시드를 읽는다");

            Environment.SetEnvironmentVariable(HubHeader.EnvShow, show);
            Environment.SetEnvironmentVariable(HubHeader.EnvNo, no);
            if (_fail == 0) Debug.Log("[HubHeaderSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[HubHeaderSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[HubHeaderSelfCheck] FAIL {_fail}건");
        }
    }
}
