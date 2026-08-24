using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class MobMeleeCadenceSelfCheck
    {
        static int _fail;
        static readonly StringBuilder Log = new StringBuilder();

        static void Check(bool ok, string what)
        {
            if (!ok) _fail++;
            Log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mob Melee Cadence Self Check")]
        public static void Run()
        {
            _fail = 0;
            Log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MobMeleeCadence.EnvShow);
            string no = Environment.GetEnvironmentVariable(MobMeleeCadence.EnvNo);
            Environment.SetEnvironmentVariable(MobMeleeCadence.EnvShow, null);
            Environment.SetEnvironmentVariable(MobMeleeCadence.EnvNo, null);
            MobMeleeCadence.ResetForTest();

            var def = ScriptableObject.CreateInstance<MobDef>();
            Check(def != null && Mathf.Approximately(def.공격간격, 1f), "MobDef 기본 근접 공격간격 1초");
            Check(Mathf.Approximately(MobMeleeCadence.Seconds(), 1f), "기본값을 읽는다");
            Check(MobMeleeCadence.Line() == "근접 공격 1초(§10-2)", $"표시 줄: {MobMeleeCadence.Line()}");
            def.공격간격 = 1.3f;
            MobMeleeCadence.ForceDef = def;
            Check(Mathf.Approximately(MobMeleeCadence.Seconds(), 1.3f), "ForceDef 공격간격을 읽는다");
            Check(MobMeleeCadence.Line().Contains("1.3초"), "에셋 변경이 줄에 반영된다");

            Environment.SetEnvironmentVariable(MobMeleeCadence.EnvNo, "1");
            Check(MobMeleeCadence.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(MobMeleeCadence.Seconds(), 1f), "차단하면 옛 기본값");
            Check(MobMeleeCadence.Line() == "", "차단하면 표시 줄 없음");
            Environment.SetEnvironmentVariable(MobMeleeCadence.EnvNo, null);
            UnityEngine.Object.DestroyImmediate(def);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string dungeonSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(charSrc.Contains("MobMeleeCadence.Line"), "캐릭터 속성 탭이 줄을 소비한다");
            Check(dungeonSrc.Contains("MobMeleeCadence.Line"), "던전 부제가 줄을 소비한다");
            Check(dungeonSrc.Contains("MobMeleeCadence.SeedQaIfRequested"), "던전 QA 시드가 연결됐다");
            _ = nameof(MobDef.공격간격);

            Environment.SetEnvironmentVariable(MobMeleeCadence.EnvShow, show);
            Environment.SetEnvironmentVariable(MobMeleeCadence.EnvNo, no);
            MobMeleeCadence.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mob_melee_cadence_selfcheck.log");
            File.WriteAllText(path, (_fail == 0 ? "PASS MobMeleeCadenceSelfCheck\n" : "FAIL MobMeleeCadenceSelfCheck\n") + Log);
            if (_fail == 0) Debug.Log("[MobMeleeCadenceSelfCheck] PASS → " + path);
            else throw new InvalidOperationException($"[MobMeleeCadenceSelfCheck] FAIL {_fail}건 → {path}");
        }
    }
}
