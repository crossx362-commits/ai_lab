using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// JobDef 최대체력·공격력이 전투 수치로 들어간다. QA_NO면 역할 버킷.
    /// </summary>
    public static class JobCombatStatSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Job Combat Stat Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string saved = Environment.GetEnvironmentVariable(JobInfo.EnvNoCombatStats);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoCombatStats, null);

            Check(!JobInfo.CombatStatsBlocked, "기본은 켠다");
            Check(Mathf.Approximately(JobInfo.CombatHp("수호기사", 320f), 320f),
                "수호기사 HP 320");
            Check(Mathf.Approximately(JobInfo.CombatAtk("수호기사", 10f), 10f),
                "수호기사 공격 10");
            Check(Mathf.Approximately(JobInfo.CombatHp("광전사", 320f), 260f),
                $"광전사 HP 260 (버킷 320이 아님, 실제 {JobInfo.CombatHp("광전사", 320f)})");
            Check(Mathf.Approximately(JobInfo.CombatAtk("광전사", 10f), 20f),
                $"광전사 공격 20 (버킷 10이 아님, 실제 {JobInfo.CombatAtk("광전사", 10f)})");
            Check(Mathf.Approximately(JobInfo.CombatHp("마법사", 130f), 110f),
                $"마법사 HP 110 (버킷 130이 아님, 실제 {JobInfo.CombatHp("마법사", 130f)})");
            Check(Mathf.Approximately(JobInfo.CombatAtk("소환사", 26f), 14f),
                $"소환사 공격 14 (버킷 26이 아님, 실제 {JobInfo.CombatAtk("소환사", 26f)})");
            Check(Mathf.Approximately(JobInfo.CombatHp("없는직업", 150f), 150f),
                "에셋 없으면 fallback");

            Environment.SetEnvironmentVariable(JobInfo.EnvNoCombatStats, "1");
            Check(JobInfo.CombatStatsBlocked, "QA_NO면 차단");
            Check(Mathf.Approximately(JobInfo.CombatHp("광전사", 320f), 320f),
                "차단하면 광전사 HP가 탱 버킷 320");
            Check(Mathf.Approximately(JobInfo.CombatAtk("소환사", 26f), 26f),
                "차단하면 소환사 공격이 딜 버킷 26");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoCombatStats, null);

            string party = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/W3Party.cs"));
            Check(party.Contains("JobInfo.CombatHp") && party.Contains("JobInfo.CombatAtk"),
                "W3Party가 JobInfo 전투 스탯을 읽는다");

            Environment.SetEnvironmentVariable(JobInfo.EnvNoCombatStats, saved);
            if (_fail == 0) Debug.Log("[JobCombatStatSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[JobCombatStatSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[JobCombatStatSelfCheck] FAIL {_fail}건");
        }
    }
}
