using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class MobRangedDistanceSelfCheck
    {
        static int _fail;
        static readonly StringBuilder Log = new StringBuilder();

        static void Check(bool ok, string what)
        {
            if (!ok) _fail++;
            Log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mob Ranged Distance Self Check")]
        public static void Run()
        {
            _fail = 0;
            Log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MobRangedDistance.EnvShow);
            string no = Environment.GetEnvironmentVariable(MobRangedDistance.EnvNo);
            Environment.SetEnvironmentVariable(MobRangedDistance.EnvShow, null);
            Environment.SetEnvironmentVariable(MobRangedDistance.EnvNo, null);
            MobRangedDistance.ResetForTest();

            var def = ScriptableObject.CreateInstance<MobDef>();
            Check(def != null && Mathf.Approximately(def.유지거리, 6.5f), "MobDef 기본 유지거리 6.5u");
            Check(Mathf.Approximately(MobRangedDistance.Units(), 6.5f), "기본값을 읽는다");
            Check(MobRangedDistance.Line() == "원거리 유지 6.5u(§10-2)", $"표시 줄: {MobRangedDistance.Line()}");
            def.유지거리 = 8.25f;
            MobRangedDistance.ForceDef = def;
            Check(Mathf.Approximately(MobRangedDistance.Units(), 8.25f), "ForceDef 유지거리를 읽는다");
            Check(MobRangedDistance.Line().Contains("8.3u"), "에셋 변경이 줄에 반영된다");

            Environment.SetEnvironmentVariable(MobRangedDistance.EnvNo, "1");
            Check(MobRangedDistance.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(MobRangedDistance.Units(), 6.5f), "차단하면 옛 기본값");
            Check(MobRangedDistance.Line() == "", "차단하면 표시 줄 없음");
            Environment.SetEnvironmentVariable(MobRangedDistance.EnvNo, null);
            UnityEngine.Object.DestroyImmediate(def);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string dungeonSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(charSrc.Contains("MobRangedDistance.Line"), "캐릭터 속성 탭이 줄을 소비한다");
            Check(charSrc.Contains("MobRangedDistance.SeedQaIfRequested"), "캐릭터 QA 시드가 연결됐다");
            Check(dungeonSrc.Contains("MobRangedDistance.Line"), "던전 부제가 줄을 소비한다");
            Check(dungeonSrc.Contains("MobRangedDistance.SeedQaIfRequested"), "던전 QA 시드가 연결됐다");
            _ = nameof(MobDef.유지거리);

            Environment.SetEnvironmentVariable(MobRangedDistance.EnvShow, show);
            Environment.SetEnvironmentVariable(MobRangedDistance.EnvNo, no);
            MobRangedDistance.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mob_ranged_distance_selfcheck.log");
            File.WriteAllText(path, (_fail == 0 ? "PASS MobRangedDistanceSelfCheck\n" : "FAIL MobRangedDistanceSelfCheck\n") + Log);
            if (_fail == 0) Debug.Log("[MobRangedDistanceSelfCheck] PASS → " + path);
            else throw new InvalidOperationException($"[MobRangedDistanceSelfCheck] FAIL {_fail}건 → {path}");
        }
    }
}
