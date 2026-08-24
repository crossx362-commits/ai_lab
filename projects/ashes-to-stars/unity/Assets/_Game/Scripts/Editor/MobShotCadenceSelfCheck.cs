using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class MobShotCadenceSelfCheck
    {
        static int _fail;
        static readonly StringBuilder Log = new StringBuilder();

        static void Check(bool ok, string what)
        {
            if (!ok) _fail++;
            Log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mob Shot Cadence Self Check")]
        public static void Run()
        {
            _fail = 0;
            Log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MobShotCadence.EnvShow);
            string no = Environment.GetEnvironmentVariable(MobShotCadence.EnvNo);
            Environment.SetEnvironmentVariable(MobShotCadence.EnvShow, null);
            Environment.SetEnvironmentVariable(MobShotCadence.EnvNo, null);
            MobShotCadence.ResetForTest();

            var def = ScriptableObject.CreateInstance<MobDef>();
            Check(def != null && Mathf.Approximately(def.발사간격, 2.4f), "MobDef 기본 발사간격 2.4초");
            Check(Mathf.Approximately(MobShotCadence.Seconds(), 2.4f), "기본값을 읽는다");
            Check(MobShotCadence.Line() == "원거리 발사 2.4초(§10-2)", $"표시 줄: {MobShotCadence.Line()}");
            def.발사간격 = 2.8f;
            MobShotCadence.ForceDef = def;
            Check(Mathf.Approximately(MobShotCadence.Seconds(), 2.8f), "ForceDef 발사간격을 읽는다");
            Check(MobShotCadence.Line().Contains("2.8초"), "에셋 변경이 줄에 반영된다");

            Environment.SetEnvironmentVariable(MobShotCadence.EnvNo, "1");
            Check(MobShotCadence.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(MobShotCadence.Seconds(), 2.4f), "차단하면 옛 기본값");
            Check(MobShotCadence.Line() == "", "차단하면 표시 줄 없음");
            Environment.SetEnvironmentVariable(MobShotCadence.EnvNo, null);
            UnityEngine.Object.DestroyImmediate(def);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string dungeonSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(charSrc.Contains("MobShotCadence.Line"), "캐릭터 속성 탭이 줄을 소비한다");
            Check(charSrc.Contains("MobShotCadence.SeedQaIfRequested"), "캐릭터 QA 시드가 연결됐다");
            Check(dungeonSrc.Contains("MobShotCadence.Line"), "던전 부제가 줄을 소비한다");
            Check(dungeonSrc.Contains("MobShotCadence.SeedQaIfRequested"), "던전 QA 시드가 연결됐다");
            _ = nameof(MobDef.발사간격);

            Environment.SetEnvironmentVariable(MobShotCadence.EnvShow, show);
            Environment.SetEnvironmentVariable(MobShotCadence.EnvNo, no);
            MobShotCadence.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mob_shot_cadence_selfcheck.log");
            File.WriteAllText(path, (_fail == 0 ? "PASS MobShotCadenceSelfCheck\n" : "FAIL MobShotCadenceSelfCheck\n") + Log);
            if (_fail == 0) Debug.Log("[MobShotCadenceSelfCheck] PASS → " + path);
            else throw new InvalidOperationException($"[MobShotCadenceSelfCheck] FAIL {_fail}건 → {path}");
        }
    }
}
