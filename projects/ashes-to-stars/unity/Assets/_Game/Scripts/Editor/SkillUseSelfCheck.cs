using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>스킬 자동/수동 — 누르면 이김 · 수동은 자동 없음 · 저장 · QA 강제 끔.</summary>
    public static class SkillUseSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Skill Use Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string oldOff = Environment.GetEnvironmentVariable("QA_NO_SKILL_AUTO");
            string oldOn = Environment.GetEnvironmentVariable("QA_SKILL_AUTO");
            Environment.SetEnvironmentVariable("QA_NO_SKILL_AUTO", null);
            Environment.SetEnvironmentVariable("QA_SKILL_AUTO", null);

            SkillUse.ResetForTest();
            Check(SkillUse.IsAuto, "기본은 자동(§5 잡몹 완전 자동)");
            Check(SkillUse.HudLabel == "스킬 자동", "HUD 라벨이 자동이다");

            Check(SkillUse.Resolve(true, 2, 0f, 1) == 2, "자동 중이라도 누른 슬롯2가 이긴다");
            Check(SkillUse.Resolve(true, 1, 9f, 2) == 1, "쿨이 남아 있어도 누르면 나간다");
            Check(SkillUse.Resolve(true, 0, 0f, 1) == 1, "자동·쿨0·다음1 → 슬롯1");
            Check(SkillUse.Resolve(true, 0, 0f, 2) == 2, "자동·쿨0·다음2 → 슬롯2");
            Check(SkillUse.Resolve(true, 0, 1f, 1) == 0, "자동이어도 쿨이면 안 넣는다");
            Check(SkillUse.Resolve(false, 0, 0f, 1) == 0, "수동은 안 누르면 0");
            Check(SkillUse.Resolve(false, 1, 0f, 1) == 1, "수동도 누르면 나간다");
            Check(SkillUse.Flip(1) == 2 && SkillUse.Flip(2) == 1, "다음 슬롯은 1↔2");

            int force = 0;
            float cd = 0f;
            int next = 1;
            Check(SkillUse.Apply(ref force, ref cd, ref next) == 1, "자동 Apply가 슬롯1을 넣는다");
            Check(force == 1 && Mathf.Approximately(cd, 0f) && next == 1,
                "넣기만 하고 아직 안 나갔으면 쿨·다음을 안 바꾼다");
            SkillUse.SettleAuto(ref cd, ref next, 1);
            Check(Mathf.Approximately(cd, SkillUse.DefaultCd), "나간 뒤에만 기본 쿨");
            Check(next == 2, "나간 뒤에만 다음은 슬롯2");

            force = 2;
            cd = 0f;
            next = 1;
            Check(SkillUse.Apply(ref force, ref cd, ref next) == 2, "누른 슬롯2는 Apply도 그대로");
            Check(force == 2 && Mathf.Approximately(cd, 0f) && next == 1,
                "누르면 쿨·다음 슬롯을 안 덮는다");

            SkillUse.IsAuto = false;
            force = 0;
            cd = 0f;
            next = 1;
            Check(SkillUse.Apply(ref force, ref cd, ref next) == 0, "수동 Apply는 빈손");
            Check(force == 0 && Mathf.Approximately(cd, 0f), "수동은 상태 불변");
            Check(SkillUse.HudLabel == "스킬 수동", "HUD 라벨이 수동이다");

            SkillUse.ForgetInMemoryForTest();
            Check(!SkillUse.IsAuto, "끔이 저장에서 되살아난다");

            SkillUse.ResetForTest();
            Environment.SetEnvironmentVariable("QA_NO_SKILL_AUTO", "1");
            Check(!SkillUse.IsAuto, "QA_NO_SKILL_AUTO=1이면 수동");
            force = 0;
            cd = 0f;
            next = 1;
            Check(SkillUse.Apply(ref force, ref cd, ref next) == 0, "강제 수동은 자동 큐 없음");
            Environment.SetEnvironmentVariable("QA_NO_SKILL_AUTO", null);

            Environment.SetEnvironmentVariable("QA_SKILL_AUTO", "1");
            SkillUse.IsAuto = false;
            Check(SkillUse.IsAuto, "QA_SKILL_AUTO=1이면 자동");
            Environment.SetEnvironmentVariable("QA_SKILL_AUTO", oldOn);
            Environment.SetEnvironmentVariable("QA_NO_SKILL_AUTO", oldOff);

            SkillUse.ResetForTest();
            _ = nameof(SkillUse.Apply);
            _ = nameof(SkillUse.SettleAuto);
            _ = nameof(SkillUse.Resolve);
            _ = nameof(SkillUse.IsAuto);
            _ = nameof(global::W3Party.ApplySkillUse);
            string w3 = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/W3Party.cs"));
            Check(w3.Contains("ApplySkillUse(ref m.ForceSkill"),
                "전투 Tick이 ApplySkillUse를 부른다(정의만 있고 호출 0곳 금지)");
            Check(w3.Contains("SkillUse.HudLabel"),
                "전투 HUD가 자동/수동 토글을 그린다");
            Check(w3.Contains("SkillUse.IsAuto &&"),
                "수동이면 암묵 자동 분기가 닫힌다");

            if (_fail == 0) Debug.Log("[SkillUseSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SkillUseSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[SkillUseSelfCheck] FAIL {_fail}건");
        }
    }
}
