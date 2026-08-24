using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-11 MobDef.속도배율 소비처.
    /// QA_NO_MOB_SPEED면 옛 표·잡몹 이속 줄 없음.
    /// </summary>
    public static class MobSpeedSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mob Speed Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MobSpeed.EnvShow);
            string no = Environment.GetEnvironmentVariable(MobSpeed.EnvNo);
            Environment.SetEnvironmentVariable(MobSpeed.EnvShow, null);
            Environment.SetEnvironmentVariable(MobSpeed.EnvNo, null);

            GameState.ResetAll();
            MobSpeed.ResetForTest();

            var def = ScriptableObject.CreateInstance<MobDef>();
            Check(def != null && Mathf.Approximately(def.속도배율, 0.90f),
                $"MobDef.속도배율 기본 0.90 (실제 {def?.속도배율})");
            Check(!MobSpeed.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(MobSpeed.Of(MobAi.추적), 0.90f),
                $"추적 0.90 (실제 {MobSpeed.Of(MobAi.추적)})");
            Check(Mathf.Approximately(MobSpeed.Of(MobAi.포위), 0.85f),
                $"포위 0.85 (실제 {MobSpeed.Of(MobAi.포위)})");
            Check(Mathf.Approximately(MobSpeed.Of(MobAi.원거리), 0.65f),
                $"원거리 0.65 (실제 {MobSpeed.Of(MobAi.원거리)})");
            Check(Mathf.Approximately(MobSpeed.Of(MobAi.돌진), 0.90f),
                $"돌진 0.90 (실제 {MobSpeed.Of(MobAi.돌진)})");
            Check(MobSpeed.Line() == "잡몹 이속 추적×0.90 · 포위×0.85 · 원거리×0.65(§18-11)",
                $"기본 줄 (실제 {MobSpeed.Line()})");
            UnityEngine.Object.DestroyImmediate(def);

            var fake = ScriptableObject.CreateInstance<MobDef>();
            fake.AI = MobAi.포위;
            fake.속도배율 = 0.70f;
            MobSpeed.ForceDef = fake;
            Check(Mathf.Approximately(MobSpeed.Of(MobAi.포위), 0.70f),
                "ForceDef가 속도배율을 읽는다");
            Check(Mathf.Approximately(MobSpeed.Of(MobAi.추적), 0.90f),
                "ForceDef 포위는 추적을 안 덮는다");
            Check(MobSpeed.Line().Contains("0.70") && MobSpeed.Line().Contains("§18-11"),
                $"에셋 0.70 줄 (실제 {MobSpeed.Line()})");
            MobSpeed.ForceDef = null;
            UnityEngine.Object.DestroyImmediate(fake);
            MobSpeed.ResetForTest();
            Check(Mathf.Approximately(MobSpeed.Of(MobAi.포위), 0.85f), "에셋을 치우면 다시 0.85");

            GameState.ResetAll();
            MobSpeed.ResetForTest();
            Environment.SetEnvironmentVariable(MobSpeed.EnvNo, "1");
            Check(MobSpeed.Blocked, "QA_NO면 차단");
            var blocked = ScriptableObject.CreateInstance<MobDef>();
            blocked.AI = MobAi.포위;
            blocked.속도배율 = 0.70f;
            MobSpeed.ForceDef = blocked;
            Check(Mathf.Approximately(MobSpeed.Of(MobAi.포위), 0.85f),
                "차단하면 ForceDef 0.70도 옛 0.85");
            Check(MobSpeed.Line() == "", "차단하면 잡몹 이속 줄 없음(옛 화면)");
            MobSpeed.ForceDef = null;
            UnityEngine.Object.DestroyImmediate(blocked);
            Environment.SetEnvironmentVariable(MobSpeed.EnvNo, null);
            MobSpeed.ResetForTest();
            Check(!MobSpeed.Blocked
                  && MobSpeed.Line() == "잡몹 이속 추적×0.90 · 포위×0.85 · 원거리×0.65(§18-11)",
                "차단을 풀면 다시 잡몹 이속 줄");

            Environment.SetEnvironmentVariable(MobSpeed.EnvShow, "1");
            MobSpeed.ResetForTest();
            MobSpeed.SeedQaIfRequested();
            Check(MobSpeed.ShowQa, "시드 ShowQa");
            Check(MobSpeed.Line().Contains("0.90") && MobSpeed.Line().Contains("0.65"),
                $"시드 줄 (실제 {MobSpeed.Line()})");
            Environment.SetEnvironmentVariable(MobSpeed.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string editor = Path.Combine(Application.dataPath, "_Game/Scripts/Editor");
            string setup = Path.Combine(editor, "ProjectSetup.cs");
            string mobSrc = File.ReadAllText(Path.Combine(runtime, "MobSpeed.cs"));
            Check(mobSrc.Contains("속도배율"),
                "MobSpeed가 MobDef.속도배율을 읽는다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("MobSpeed.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("Info(r, statusMax + 1, mobSpd)"),
                "잡몹 이속 줄을 우선존 단독 행에 그린다");
            Check(charSrc.Contains("MobSpeed.ShowQa ? MobSpeed.Line()"),
                "부제에 잡몹 이속 줄을 올린다 — 속성 패널 맨 뒤는 샷에 안 나온다");
            Check(charSrc.Contains("MobSpeed.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(mapSrc.Contains("MobSpeed.Line"),
                "던전 지도가 Line을 읽는다");
            Check(mapSrc.Contains("MobSpeed.SeedQaIfRequested"),
                "던전 지도가 시드를 읽는다");
            string setupSrc = File.ReadAllText(setup);
            Check(setupSrc.Contains("0.90f") && setupSrc.Contains("0.85f") && setupSrc.Contains("0.65f"),
                "ProjectSetup이 추적 0.90 · 포위 0.85 · 원거리 0.65를 심는다");

            _ = nameof(MobSpeed.Of);
            _ = nameof(MobSpeed.Line);
            _ = nameof(MobSpeed.SeedQaIfRequested);
            _ = nameof(MobDef.속도배율);

            Environment.SetEnvironmentVariable(MobSpeed.EnvShow, show);
            Environment.SetEnvironmentVariable(MobSpeed.EnvNo, no);
            MobSpeed.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mob_speed_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS MobSpeedSelfCheck" : "FAIL MobSpeedSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[MobSpeedSelfCheck] PASS → " + path);
            else Debug.LogError("[MobSpeedSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[MobSpeedSelfCheck] FAIL {_fail}건");
        }
    }
}
