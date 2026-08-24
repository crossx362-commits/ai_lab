using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-11 BalanceConfig.플레이어이동속도 소비처.
    /// QA_NO_MOVE_SPD면 옛 4.2·이동 줄 없음.
    /// </summary>
    public static class MoveSpdSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Move Spd Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MoveSpd.EnvShow);
            string no = Environment.GetEnvironmentVariable(MoveSpd.EnvNo);
            Environment.SetEnvironmentVariable(MoveSpd.EnvShow, null);
            Environment.SetEnvironmentVariable(MoveSpd.EnvNo, null);

            GameState.ResetAll();
            MoveSpd.ResetForTest();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && Mathf.Approximately(cfg.플레이어이동속도, 4.2f),
                $"BalanceConfig.플레이어이동속도 기본 4.2 (실제 {cfg?.플레이어이동속도})");
            Check(!MoveSpd.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(MoveSpd.Units(), 4.2f), $"읽기 4.2 (실제 {MoveSpd.Units()})");
            Check(MoveSpd.Line() == "이동 4.2(§18-11)",
                $"기본 줄 (실제 {MoveSpd.Line()})");
            UnityEngine.Object.DestroyImmediate(cfg);

            var five = ScriptableObject.CreateInstance<BalanceConfig>();
            five.플레이어이동속도 = 5f;
            MoveSpd.ForceConfig = five;
            Check(Mathf.Approximately(MoveSpd.Units(), 5f), "ForceConfig가 플레이어이동속도를 읽는다");
            Check(MoveSpd.Line().Contains("5") && MoveSpd.Line().Contains("§18-11"),
                $"에셋 5 줄 (실제 {MoveSpd.Line()})");
            MoveSpd.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(five);
            MoveSpd.ResetForTest();
            Check(Mathf.Approximately(MoveSpd.Units(), 4.2f), "에셋을 치우면 다시 4.2");

            GameState.ResetAll();
            MoveSpd.ResetForTest();
            Environment.SetEnvironmentVariable(MoveSpd.EnvNo, "1");
            Check(MoveSpd.Blocked, "QA_NO면 차단");
            var fake = ScriptableObject.CreateInstance<BalanceConfig>();
            fake.플레이어이동속도 = 5f;
            MoveSpd.ForceConfig = fake;
            Check(Mathf.Approximately(MoveSpd.Units(), 4.2f), "차단하면 ForceConfig 5도 옛 4.2");
            Check(MoveSpd.Line() == "", "차단하면 이동 줄 없음(옛 화면)");
            MoveSpd.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(fake);
            Environment.SetEnvironmentVariable(MoveSpd.EnvNo, null);
            MoveSpd.ResetForTest();
            Check(!MoveSpd.Blocked && MoveSpd.Line() == "이동 4.2(§18-11)",
                "차단을 풀면 다시 이동 줄");

            Environment.SetEnvironmentVariable(MoveSpd.EnvShow, "1");
            MoveSpd.ResetForTest();
            MoveSpd.SeedQaIfRequested();
            Check(MoveSpd.ShowQa, "시드 ShowQa");
            Check(MoveSpd.Line().Contains("4.2"),
                $"시드 줄 (실제 {MoveSpd.Line()})");
            Environment.SetEnvironmentVariable(MoveSpd.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string capSrc = File.ReadAllText(Path.Combine(runtime, "MoveSpd.cs"));
            Check(capSrc.Contains("플레이어이동속도"),
                "MoveSpd가 BalanceConfig.플레이어이동속도를 읽는다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("MoveSpd.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("stats + \" · \" + spd"),
                "이동 줄을 StatLine 우선존에 붙인다 — 맨 뒤면 화면에서 잘린다");
            Check(charSrc.Contains("MoveSpd.ShowQa ? MoveSpd.Line()"),
                "부제에 이동 줄을 올린다 — 속성 패널 맨 뒤는 샷에 안 나온다");
            Check(charSrc.Contains("MoveSpd.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");

            _ = nameof(MoveSpd.Units);
            _ = nameof(MoveSpd.Line);
            _ = nameof(MoveSpd.SeedQaIfRequested);
            _ = nameof(BalanceConfig.플레이어이동속도);

            Environment.SetEnvironmentVariable(MoveSpd.EnvShow, show);
            Environment.SetEnvironmentVariable(MoveSpd.EnvNo, no);
            MoveSpd.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "move_spd_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS MoveSpdSelfCheck" : "FAIL MoveSpdSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[MoveSpdSelfCheck] PASS → " + path);
            else Debug.LogError("[MoveSpdSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[MoveSpdSelfCheck] FAIL {_fail}건");
        }
    }
}
