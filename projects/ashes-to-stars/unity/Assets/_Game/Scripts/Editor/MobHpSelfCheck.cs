using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-11 MobDef.체력배율 소비처.
    /// QA_NO_MOB_HP면 옛 1.2·잡몹 HP 줄 없음.
    /// </summary>
    public static class MobHpSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mob Hp Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(MobHp.EnvShow);
            string no = Environment.GetEnvironmentVariable(MobHp.EnvNo);
            Environment.SetEnvironmentVariable(MobHp.EnvShow, null);
            Environment.SetEnvironmentVariable(MobHp.EnvNo, null);

            GameState.ResetAll();
            MobHp.ResetForTest();

            var def = ScriptableObject.CreateInstance<MobDef>();
            Check(def != null && Mathf.Approximately(def.체력배율, 1.2f),
                $"MobDef.체력배율 기본 1.2 (실제 {def?.체력배율})");
            Check(!MobHp.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(MobHp.Mul(), 1.2f), $"읽기 1.2 (실제 {MobHp.Mul()})");
            Check(MobHp.Line() == "잡몹 HP ×1.2(§18-11)",
                $"기본 줄 (실제 {MobHp.Line()})");
            UnityEngine.Object.DestroyImmediate(def);

            var fake = ScriptableObject.CreateInstance<MobDef>();
            fake.체력배율 = 0.8f;
            MobHp.ForceDef = fake;
            Check(Mathf.Approximately(MobHp.Mul(), 0.8f), "ForceDef가 체력배율을 읽는다");
            Check(MobHp.Line().Contains("0.8") && MobHp.Line().Contains("§18-11"),
                $"에셋 0.8 줄 (실제 {MobHp.Line()})");
            MobHp.ForceDef = null;
            UnityEngine.Object.DestroyImmediate(fake);
            MobHp.ResetForTest();
            Check(Mathf.Approximately(MobHp.Mul(), 1.2f), "에셋을 치우면 다시 1.2");

            GameState.ResetAll();
            MobHp.ResetForTest();
            Environment.SetEnvironmentVariable(MobHp.EnvNo, "1");
            Check(MobHp.Blocked, "QA_NO면 차단");
            var blocked = ScriptableObject.CreateInstance<MobDef>();
            blocked.체력배율 = 0.8f;
            MobHp.ForceDef = blocked;
            Check(Mathf.Approximately(MobHp.Mul(), 1.2f), "차단하면 ForceDef 0.8도 옛 1.2");
            Check(MobHp.Line() == "", "차단하면 잡몹 HP 줄 없음(옛 화면)");
            MobHp.ForceDef = null;
            UnityEngine.Object.DestroyImmediate(blocked);
            Environment.SetEnvironmentVariable(MobHp.EnvNo, null);
            MobHp.ResetForTest();
            Check(!MobHp.Blocked && MobHp.Line() == "잡몹 HP ×1.2(§18-11)",
                "차단을 풀면 다시 잡몹 HP 줄");

            Environment.SetEnvironmentVariable(MobHp.EnvShow, "1");
            MobHp.ResetForTest();
            MobHp.SeedQaIfRequested();
            Check(MobHp.ShowQa, "시드 ShowQa");
            Check(MobHp.Line().Contains("1.2"),
                $"시드 줄 (실제 {MobHp.Line()})");
            Environment.SetEnvironmentVariable(MobHp.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string hpSrc = File.ReadAllText(Path.Combine(runtime, "MobHp.cs"));
            Check(hpSrc.Contains("체력배율"),
                "MobHp가 MobDef.체력배율을 읽는다");
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(charSrc.Contains("MobHp.Line"),
                "CharacterScreen이 Line을 속성 탭에 그린다");
            Check(charSrc.Contains("Info(r, statusMax + 1, mobHp)"),
                "잡몹 HP 줄을 우선존 단독 행에 그린다");
            Check(charSrc.Contains("MobHp.ShowQa ? MobHp.Line()"),
                "부제에 잡몹 HP 줄을 올린다 — 속성 패널 맨 뒤는 샷에 안 나온다");
            Check(charSrc.Contains("MobHp.SeedQaIfRequested"),
                "CharacterScreen이 SeedQa를 부른다");
            Check(charSrc.Contains("!roster[i].IsDeleted"),
                "시드가 삭제된 캐릭터를 건너뛴다");
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            Check(mapSrc.Contains("MobHp.Line"),
                "던전 지도가 Line을 읽는다");
            Check(mapSrc.Contains("MobHp.SeedQaIfRequested"),
                "던전 지도가 시드를 읽는다");

            _ = nameof(MobHp.Mul);
            _ = nameof(MobHp.Line);
            _ = nameof(MobHp.SeedQaIfRequested);
            _ = nameof(MobDef.체력배율);

            Environment.SetEnvironmentVariable(MobHp.EnvShow, show);
            Environment.SetEnvironmentVariable(MobHp.EnvNo, no);
            MobHp.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mob_hp_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS MobHpSelfCheck" : "FAIL MobHpSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[MobHpSelfCheck] PASS → " + path);
            else Debug.LogError("[MobHpSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[MobHpSelfCheck] FAIL {_fail}건");
        }
    }
}
