using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>영지 마을을 두 손가락으로 벌려 확대한다. QA_NO면 옛 배율 1(§16).</summary>
    public static class EstateYardPinchSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Yard Pinch Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateYard.EnvShowPinch);
            string showZoom = Environment.GetEnvironmentVariable(EstateYard.EnvShowZoom);
            string noZoom = Environment.GetEnvironmentVariable(EstateYard.EnvNoZoom);
            string noPan = Environment.GetEnvironmentVariable(EstateYard.EnvNoPan);
            string noFill = Environment.GetEnvironmentVariable(EstateYard.EnvNo);
            Environment.SetEnvironmentVariable(EstateYard.EnvShowPinch, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvShowZoom, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoZoom, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoPan, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, null);
            EstateYard.ResetForTest();

            var body = new Rect(GameScreen.BodyPadX, GameScreen.BodyTop,
                1280f - GameScreen.BodyPadX * 2f,
                720f - GameScreen.BodyTop - UiPages.NavReserve);
            var yard = EstateYard.VillageRect(body);
            float home = EstateYard.TileW(yard);
            Check(Mathf.Abs(EstateYard.Zoom - 1f) < 0.001f, $"기본 배율 {EstateYard.Zoom:0.00} = 1");
            float after = EstateYard.ApplyPinch(EstateYard.QaPinchFrom, EstateYard.QaPinchTo);
            Check(Mathf.Abs(after - EstateYard.QaZoom) < 0.001f,
                $"핀치 {after:0.00} = {EstateYard.QaZoom:0.00}");
            float grown = EstateYard.TileW(yard);
            Check(Mathf.Abs(grown - home * EstateYard.QaZoom) < 0.5f,
                $"칸 {grown:0.0} = 기본 {home:0.0} × {EstateYard.QaZoom:0.00}");
            Check(grown > home + 20f, $"칸 {grown:0.0} > 기본 {home:0.0}");
            EstateYard.ApplyPinch(EstateYard.QaPinchTo, EstateYard.QaPinchFrom);
            Check(Mathf.Abs(EstateYard.Zoom - 1f) < 0.001f,
                $"오므리면 {EstateYard.Zoom:0.00} = 1");
            Check(Mathf.Abs(EstateYard.ApplyPinch(0f, 200f) - 1f) < 0.001f,
                "거리 0은 무시");
            EstateYard.ApplyPinch(EstateYard.QaPinchFrom, 400f);
            Check(EstateYard.Zoom <= EstateYard.ZoomMax + 0.001f,
                $"상한 {EstateYard.Zoom:0.00} ≤ {EstateYard.ZoomMax:0.00}");

            Environment.SetEnvironmentVariable(EstateYard.EnvNoZoom, "1");
            EstateYard.ResetForTest();
            EstateYard.ApplyPinch(EstateYard.QaPinchFrom, EstateYard.QaPinchTo);
            Check(!EstateYard.ZoomEnabled, "QA_NO면 고정 배율");
            Check(Mathf.Abs(EstateYard.Zoom - 1f) < 0.001f, "차단 배율 1");
            Check(Mathf.Abs(EstateYard.TileW(yard) - home) < 0.5f,
                "차단하면 칸이 제자리");
            Environment.SetEnvironmentVariable(EstateYard.EnvNoZoom, null);
            EstateYard.ResetForTest();

            Environment.SetEnvironmentVariable(EstateYard.EnvShowPinch, "1");
            EstateYard.SeedQaIfRequested();
            Check(Mathf.Abs(EstateYard.Zoom - EstateYard.QaZoom) < 0.001f,
                $"시드 {EstateYard.Zoom:0.00} = {EstateYard.QaZoom:0.00}");
            Check(EstateYard.Line().Contains("두 손가락"),
                $"시드 줄 (실제 {EstateYard.Line()})");
            Environment.SetEnvironmentVariable(EstateYard.EnvShowPinch, null);
            EstateYard.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string yardSrc = File.ReadAllText(Path.Combine(runtime, "EstateYard.cs"));
            Check(estate.Contains("EstateYard.SeedQaIfRequested"), "영지가 SeedQa를 읽는다");
            Check(estate.Contains("EstateYard.ShowQa"), "자막이 ShowQa를 읽는다");
            Check(yardSrc.Contains("HandlePinch") && yardSrc.Contains("ApplyPinch"),
                "마을이 HandlePinch를 읽는다");
            Check(yardSrc.Contains("ApplyPinch(_pinch0, d)"),
                "HandlePinch가 ApplyPinch를 읽는다");
            Check(yardSrc.Contains("ApplyPinch(QaPinchFrom, QaPinchTo)"),
                "시드가 ApplyPinch를 읽는다");

            Environment.SetEnvironmentVariable(EstateYard.EnvShowPinch, show);
            Environment.SetEnvironmentVariable(EstateYard.EnvShowZoom, showZoom);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoZoom, noZoom);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoPan, noPan);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, noFill);
            EstateYard.ResetForTest();
            if (_fail == 0) Debug.Log("[EstateYardPinchSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateYardPinchSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateYardPinchSelfCheck] FAIL {_fail}건");
        }
    }
}
