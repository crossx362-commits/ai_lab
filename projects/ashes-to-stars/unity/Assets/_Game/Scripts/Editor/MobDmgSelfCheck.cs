using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-11 MobDef.피해비율 소비처.
    /// QA_NO_MOB_DMG면 옛 0.03·잡몹 피해 줄 없음.
    /// </summary>
    public static class MobDmgSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mob Dmg Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MobDmg.EnvShow);
            string no = Environment.GetEnvironmentVariable(MobDmg.EnvNo);
            Environment.SetEnvironmentVariable(MobDmg.EnvShow, null);
            Environment.SetEnvironmentVariable(MobDmg.EnvNo, null);

            GameState.ResetAll();
            MobDmg.ResetForTest();

            var def = ScriptableObject.CreateInstance<MobDef>();
            Check(def != null && Mathf.Approximately(def.피해비율, 0.03f),
                $"MobDef.피해비율 기본 0.03 (실제 {def?.피해비율})");
            Check(!MobDmg.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(MobDmg.Ratio(), 0.03f), $"읽기 0.03 (실제 {MobDmg.Ratio()})");
            Check(MobDmg.Line() == "잡몹 피해 3%(§18-11)",
                $"기본 줄 (실제 {MobDmg.Line()})");
            UnityEngine.Object.DestroyImmediate(def);

            var fake = ScriptableObject.CreateInstance<MobDef>();
            fake.피해비율 = 0.02f;
            MobDmg.ForceDef = fake;
            Check(Mathf.Approximately(MobDmg.Ratio(), 0.02f), "ForceDef가 피해비율을 읽는다");
            Check(MobDmg.Line().Contains("2%") && MobDmg.Line().Contains("§18-11"),
                $"에셋 0.02 줄 (실제 {MobDmg.Line()})");
            MobDmg.ForceDef = null;
            UnityEngine.Object.DestroyImmediate(fake);
            MobDmg.ResetForTest();
            Check(Mathf.Approximately(MobDmg.Ratio(), 0.03f), "에셋을 치우면 다시 0.03");

            GameState.ResetAll();
            MobDmg.ResetForTest();
            Environment.SetEnvironmentVariable(MobDmg.EnvNo, "1");
            Check(MobDmg.Blocked, "QA_NO면 차단");
            var blocked = ScriptableObject.CreateInstance<MobDef>();
            blocked.피해비율 = 0.02f;
            MobDmg.ForceDef = blocked;
            Check(Mathf.Approximately(MobDmg.Ratio(), 0.03f), "차단하면 ForceDef 0.02도 옛 0.03");
            Check(MobDmg.Line() == "", "차단하면 잡몹 피해 줄 없음(옛 화면)");
            MobDmg.ForceDef = null;
            UnityEngine.Object.DestroyImmediate(blocked);
            Environment.SetEnvironmentVariable(MobDmg.EnvNo, null);
            MobDmg.ResetForTest();
            Check(!MobDmg.Blocked && MobDmg.Line() == "잡몹 피해 3%(§18-11)",
                "차단을 풀면 다시 잡몹 피해 줄");

            Environment.SetEnvironmentVariable(MobDmg.EnvShow, "1");
            MobDmg.ResetForTest();
            MobDmg.SeedQaIfRequested();
            Check(MobDmg.ShowQa, "시드 ShowQa");
            Check(MobDmg.Line().Contains("3%"),
                $"시드 줄 (실제 {MobDmg.Line()})");
            Environment.SetEnvironmentVariable(MobDmg.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string dmgSrc = File.ReadAllText(Path.Combine(runtime, "MobDmg.cs"));
            Check(dmgSrc.Contains("피해비율"),
                "MobDmg가 MobDef.피해비율을 읽는다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("MobDmg.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("Info(r, statusMax + 1, mobDmg)"),
                "잡몹 피해 줄을 우선존 단독 행에 그린다");
            Check(charSrc.Contains("MobDmg.ShowQa ? MobDmg.Line()"),
                "부제에 잡몹 피해 줄을 올린다 — 속성 패널 맨 뒤는 샷에 안 나온다");
            Check(charSrc.Contains("MobDmg.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(mapSrc.Contains("MobDmg.Line"),
                "던전 지도가 Line을 읽는다");
            Check(mapSrc.Contains("MobDmg.SeedQaIfRequested"),
                "던전 지도가 시드를 읽는다");

            _ = nameof(MobDmg.Ratio);
            _ = nameof(MobDmg.Line);
            _ = nameof(MobDmg.SeedQaIfRequested);
            _ = nameof(MobDef.피해비율);

            Environment.SetEnvironmentVariable(MobDmg.EnvShow, show);
            Environment.SetEnvironmentVariable(MobDmg.EnvNo, no);
            MobDmg.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mob_dmg_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS MobDmgSelfCheck" : "FAIL MobDmgSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[MobDmgSelfCheck] PASS → " + path);
            else Debug.LogError("[MobDmgSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[MobDmgSelfCheck] FAIL {_fail}건");
        }
    }
}
