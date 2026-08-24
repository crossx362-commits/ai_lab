using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class MobProjectileSpeedSelfCheck
    {
        static int _fail;
        static readonly StringBuilder Log = new StringBuilder();

        static void Check(bool ok, string what)
        {
            if (!ok) _fail++;
            Log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mob Projectile Speed Self Check")]
        public static void Run()
        {
            _fail = 0;
            Log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MobProjectileSpeed.EnvShow);
            string no = Environment.GetEnvironmentVariable(MobProjectileSpeed.EnvNo);
            Environment.SetEnvironmentVariable(MobProjectileSpeed.EnvShow, null);
            Environment.SetEnvironmentVariable(MobProjectileSpeed.EnvNo, null);
            MobProjectileSpeed.ResetForTest();

            var def = ScriptableObject.CreateInstance<MobDef>();
            Check(def != null && Mathf.Approximately(def.탄속, 5.5f), "MobDef 기본 탄속 5.5u/s");
            Check(Mathf.Approximately(MobProjectileSpeed.Units(), 5.5f), "기본값을 읽는다");
            Check(MobProjectileSpeed.Line() == "원거리 탄속 5.5u/s(§10-2)", $"표시 줄: {MobProjectileSpeed.Line()}");
            def.탄속 = 4.25f;
            MobProjectileSpeed.ForceDef = def;
            Check(Mathf.Approximately(MobProjectileSpeed.Units(), 4.25f), "ForceDef 탄속을 읽는다");
            Check(MobProjectileSpeed.Line().Contains("4.3u/s"), "에셋 변경이 줄에 반영된다");

            Environment.SetEnvironmentVariable(MobProjectileSpeed.EnvNo, "1");
            Check(MobProjectileSpeed.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(MobProjectileSpeed.Units(), 5.5f), "차단하면 옛 기본값");
            Check(MobProjectileSpeed.Line() == "", "차단하면 표시 줄 없음");
            Environment.SetEnvironmentVariable(MobProjectileSpeed.EnvNo, null);
            UnityEngine.Object.DestroyImmediate(def);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string dungeonSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(charSrc.Contains("MobProjectileSpeed.Line"), "캐릭터 속성 탭이 줄을 소비한다");
            Check(charSrc.Contains("MobProjectileSpeed.SeedQaIfRequested"), "캐릭터 QA 시드가 연결됐다");
            Check(dungeonSrc.Contains("MobProjectileSpeed.Line"), "던전 부제가 줄을 소비한다");
            Check(dungeonSrc.Contains("MobProjectileSpeed.SeedQaIfRequested"), "던전 QA 시드가 연결됐다");
            _ = nameof(MobDef.탄속);

            Environment.SetEnvironmentVariable(MobProjectileSpeed.EnvShow, show);
            Environment.SetEnvironmentVariable(MobProjectileSpeed.EnvNo, no);
            MobProjectileSpeed.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mob_projectile_speed_selfcheck.log");
            File.WriteAllText(path, (_fail == 0 ? "PASS MobProjectileSpeedSelfCheck\n" : "FAIL MobProjectileSpeedSelfCheck\n") + Log);
            if (_fail == 0) Debug.Log("[MobProjectileSpeedSelfCheck] PASS → " + path);
            else throw new InvalidOperationException($"[MobProjectileSpeedSelfCheck] FAIL {_fail}건 → {path}");
        }
    }
}
