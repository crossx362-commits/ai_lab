using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class MobSizeSelfCheck
    {
        static int _fail;
        static readonly StringBuilder Log = new StringBuilder();

        static void Check(bool ok, string what)
        {
            if (!ok) _fail++;
            Log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mob Size Self Check")]
        public static void Run()
        {
            _fail = 0;
            Log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MobSize.EnvShow);
            string no = Environment.GetEnvironmentVariable(MobSize.EnvNo);
            Environment.SetEnvironmentVariable(MobSize.EnvShow, null);
            Environment.SetEnvironmentVariable(MobSize.EnvNo, null);
            MobSize.ResetForTest();

            var def = ScriptableObject.CreateInstance<MobDef>();
            Check(def != null && Mathf.Approximately(def.크기, 2.2f), "MobDef 기본 크기 2.2");
            Check(Mathf.Approximately(MobSize.Units(), 2.2f), "기본값을 읽는다");
            Check(MobSize.Line() == "잡몹 크기 ×2.2", $"표시 줄: {MobSize.Line()}");
            def.크기 = 3.25f;
            MobSize.ForceDef = def;
            Check(Mathf.Approximately(MobSize.Units(), 3.25f), "ForceDef 크기를 읽는다");
            Check(MobSize.Line().Contains("×3.3"), "에셋 변경이 줄에 반영된다");

            Environment.SetEnvironmentVariable(MobSize.EnvNo, "1");
            Check(MobSize.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(MobSize.Units(), 2.2f), "차단하면 옛 기본값");
            Check(MobSize.Line() == "", "차단하면 표시 줄 없음");
            Environment.SetEnvironmentVariable(MobSize.EnvNo, null);
            UnityEngine.Object.DestroyImmediate(def);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string dungeonSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(dungeonSrc.Contains("MobSize.Line"), "던전 부제가 줄을 소비한다");
            Check(dungeonSrc.Contains("MobSize.SeedQaIfRequested"), "던전 QA 시드가 연결됐다");
            _ = nameof(MobDef.크기);

            Environment.SetEnvironmentVariable(MobSize.EnvShow, show);
            Environment.SetEnvironmentVariable(MobSize.EnvNo, no);
            MobSize.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mob_size_selfcheck.log");
            File.WriteAllText(path, (_fail == 0 ? "PASS MobSizeSelfCheck\n" : "FAIL MobSizeSelfCheck\n") + Log);
            if (_fail == 0) Debug.Log("[MobSizeSelfCheck] PASS → " + path);
            else throw new InvalidOperationException($"[MobSizeSelfCheck] FAIL {_fail}건 → {path}");
        }
    }
}
