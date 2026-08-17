using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>영지 마을을 끌어 본다. QA_NO면 옛 고정 시점(§16).</summary>
    public static class EstateYardCamSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Yard Cam Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateYard.EnvShowPan);
            string noPan = Environment.GetEnvironmentVariable(EstateYard.EnvNoPan);
            string noFill = Environment.GetEnvironmentVariable(EstateYard.EnvNo);
            Environment.SetEnvironmentVariable(EstateYard.EnvShowPan, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoPan, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, null);
            EstateYard.ResetForTest();

            var body = new Rect(GameScreen.BodyPadX, GameScreen.BodyTop,
                1280f - GameScreen.BodyPadX * 2f,
                720f - GameScreen.BodyTop - UiPages.NavReserve);
            var yard = EstateYard.VillageRect(body);
            var home = EstateYard.TileOrigin(yard, 0, 0);
            EstateYard.SetPan(yard, new Vector2(EstateYard.QaPanX, 0f));
            var moved = EstateYard.TileOrigin(yard, 0, 0);
            Check(Mathf.Abs((moved - home).x - EstateYard.QaPanX) < 0.5f,
                $"끌어 X {moved.x - home.x:0.0} = {EstateYard.QaPanX:0}");
            Check(Mathf.Abs(moved.y - home.y) < 0.5f, "끌어 X만이면 Y 불변");
            Check(EstateYard.PanEnabled, "기본은 끌어 보기");
            Check(EstateYard.Line().Contains("끌어 본다"),
                $"줄 (실제 {EstateYard.Line()})");

            EstateYard.SetPan(yard, new Vector2(9999f, -9999f));
            var max = EstateYard.MaxPan(yard);
            Check(EstateYard.Pan.x <= max.x + 0.01f,
                $"상한 X {EstateYard.Pan.x:0} ≤ {max.x:0}");
            Check(EstateYard.Pan.y >= -max.y - 0.01f,
                $"상한 Y {EstateYard.Pan.y:0} ≥ {-max.y:0}");
            Check(max.x > EstateYard.QaPanX, $"상한 {max.x:0} > 시드 {EstateYard.QaPanX:0}");

            Environment.SetEnvironmentVariable(EstateYard.EnvNoPan, "1");
            EstateYard.SetPan(yard, new Vector2(EstateYard.QaPanX, EstateYard.QaPanY));
            var frozen = EstateYard.TileOrigin(yard, 0, 0);
            Check(!EstateYard.PanEnabled, "QA_NO면 고정");
            Check(EstateYard.Pan == Vector2.zero, "차단 팬 0");
            Check(Mathf.Abs(frozen.x - home.x) < 0.5f && Mathf.Abs(frozen.y - home.y) < 0.5f,
                "차단하면 Origin이 제자리");
            Check(EstateYard.Line().Contains("화면을 채운다")
                    && !EstateYard.Line().Contains("끌어 본다"),
                $"차단 줄 (실제 {EstateYard.Line()})");
            Environment.SetEnvironmentVariable(EstateYard.EnvNoPan, null);
            EstateYard.ResetForTest();

            Environment.SetEnvironmentVariable(EstateYard.EnvShowPan, "1");
            EstateYard.SeedQaIfRequested();
            Check(Mathf.Abs(EstateYard.Pan.x - EstateYard.QaPanX) < 0.5f,
                $"시드 X {EstateYard.Pan.x:0} = {EstateYard.QaPanX:0}");
            Check(Mathf.Abs(EstateYard.Pan.y - EstateYard.QaPanY) < 0.5f,
                $"시드 Y {EstateYard.Pan.y:0} = {EstateYard.QaPanY:0}");
            Check(EstateYard.Line().Contains("끌어 본다"), "시드 줄");
            Environment.SetEnvironmentVariable(EstateYard.EnvShowPan, null);
            EstateYard.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string yardSrc = File.ReadAllText(Path.Combine(runtime, "EstateYard.cs"));
            Check(estate.Contains("EstateYard.SeedQaIfRequested"), "영지가 SeedQa를 읽는다");
            Check(estate.Contains("EstateYard.ShowQa"), "자막이 ShowQa를 읽는다");
            Check(yardSrc.Contains("HandlePan") && yardSrc.Contains("SetPan"),
                "마을이 HandlePan을 읽는다");
            Check(yardSrc.Contains("TileOrigin"), "칸이 TileOrigin을 읽는다");

            Environment.SetEnvironmentVariable(EstateYard.EnvShowPan, show);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoPan, noPan);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, noFill);
            EstateYard.ResetForTest();
            if (_fail == 0) Debug.Log("[EstateYardCamSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateYardCamSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateYardCamSelfCheck] FAIL {_fail}건");
        }
    }
}
