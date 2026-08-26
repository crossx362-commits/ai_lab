using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §3 SkillDef.자원소모 소비처. QA_NO_SKILL_COST면 소모 조각을 뺀다(옛 SkillLine).
    /// </summary>
    public static class SkillCostSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Skill Cost Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noCd = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillCd);
            string noPow = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillPow);
            string noRad = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillRad);
            string noCost = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillCost);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, null);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillPow, null);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillRad, null);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCost, null);

            var defs = Resources.LoadAll<JobDef>("jobs");
            Check(defs != null && defs.Length > 0,
                $"Resources/jobs 로드 ({(defs == null ? 0 : defs.Length)}종)");
            Check(!JobInfo.SkillCostBlocked, "기본은 켜짐");

            int withCost = 0;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null || string.IsNullOrEmpty(d.직업명) || d.스킬 == null) continue;
                    string line = JobInfo.SkillLine(d.직업명);
                    Check(!string.IsNullOrEmpty(line) && line.StartsWith("보유 스킬 — ", StringComparison.Ordinal),
                        $"{d.직업명}: SkillLine 접두 — 「{line}」");
                    foreach (var s in d.스킬)
                    {
                        if (s == null || string.IsNullOrEmpty(s.이름) || s.초필살기) continue;
                        Check(line.Contains(s.이름),
                            $"{d.직업명}: 형제 이름 보존 ({s.이름}) — 「{line}」");
                        if (s.자원소모 <= 0f)
                        {
                            Check(!PieceHasCost(line, s.이름),
                                $"{d.직업명}: {s.이름} 자원0은 소모 없음 — 「{line}」");
                            continue;
                        }
                        withCost++;
                        string cost = "소모" + s.자원소모.ToString("0.#");
                        Check(line.Contains(cost),
                            $"{d.직업명}: SkillLine이 자원소모 필드를 읽는다 ({s.이름} {cost}) — 「{line}」");
                        if (s.쿨다운 <= 0f)
                            Check(line.IndexOf(s.이름 + "(", StringComparison.Ordinal) < 0,
                                $"{d.직업명}: 쿨0 {s.이름}에 이름( 금지 — 「{line}」");
                    }
                }
            Check(withCost > 0, $"표시 소모 스킬 {withCost}개 검사됨 (0이면 배선 확인 불가)");

            string sword = JobInfo.SkillLine("검사");
            Check(sword.Contains("일섬 ×3.2 소모5"),
                $"검사 일섬(쿨0) ×3.2 소모5 — 「{sword}」");
            Check(sword.IndexOf("일섬(", StringComparison.Ordinal) < 0,
                $"검사 일섬 이름( 금지 — 「{sword}」");
            Check(sword.Contains("발도(8초·×1.2)") && sword.IndexOf("발도(8초·×1.2·소모", StringComparison.Ordinal) < 0,
                $"검사 발도(자원0)는 쿨·위력만 — 「{sword}」");

            string priest = JobInfo.SkillLine("사제");
            Check(priest.Contains("기적 소모100"),
                $"사제 기적(쿨0) 소모100 — 「{priest}」");
            Check(priest.IndexOf("반경99", StringComparison.Ordinal) < 0,
                $"사제 기적(99)은 전역표식이라 반경 숫자 없음 — 「{priest}」");

            string mage = JobInfo.SkillLine("마법사");
            Check(mage.Contains("화염폭풍(5초·×1.2·반경3.2)") && mage.IndexOf("소모", StringComparison.Ordinal) < 0,
                $"마법사(자원0)는 소모 없음 — 「{mage}」");

            Check(JobInfo.SkillLine("없는직업") == "", "모르는 직업은 빈 문자열(지어내지 않음)");

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCost, "1");
            Check(JobInfo.SkillCostBlocked, "QA_NO면 차단");
            string old = JobInfo.SkillLine("검사");
            Check(old.Contains("일섬 ×3.2") && old.IndexOf("소모", StringComparison.Ordinal) < 0,
                $"차단하면 일섬은 쿨·위력만 — 「{old}」");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCost, null);
            Check(!JobInfo.SkillCostBlocked && JobInfo.SkillLine("검사").Contains("일섬 ×3.2 소모5"),
                "차단을 풀면 다시 소모 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            // 2026-08-26 갱신 — 화면은 RebirthSkill.SkillLine(계승 위임)을 쓴다.
            Check(charSrc.Contains("RebirthSkill.SkillLine"),
                "CharacterScreen이 RebirthSkill.SkillLine을 속성 탭에 그린다");
            Check(charSrc.Contains("자원소모"),
                "CharacterScreen 주석이 자원소모 소비처를 가리킨다");
            Check(charSrc.Contains("QA_SKILL_COST"),
                "CharacterScreen이 검사 시드를 갖는다");

            string jobSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/JobInfo.cs"));
            Check(jobSrc.Contains("s.자원소모"),
                "JobInfo가 s.자원소모를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(JobInfo.SkillLine);
            _ = nameof(SkillDef.자원소모);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, noCd);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillPow, noPow);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillRad, noRad);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCost, noCost);

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "skill_cost_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS SkillCostSelfCheck" : "FAIL SkillCostSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[SkillCostSelfCheck] PASS → " + path);
            else Debug.LogError("[SkillCostSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[SkillCostSelfCheck] FAIL {_fail}건");
        }

        // 해당 스킬 조각(다음 · 전까지)에 「소모」가 있는지. 줄 전체에 IndexOf하면
        // 형제 스킬의 소모가 자원0 스킬 FAIL을 오염시킨다.
        static bool PieceHasCost(string line, string name)
        {
            int i = line.IndexOf(name, StringComparison.Ordinal);
            if (i < 0) return false;
            int end = line.IndexOf(" · ", i, StringComparison.Ordinal);
            string piece = end < 0 ? line.Substring(i) : line.Substring(i, end - i);
            return piece.IndexOf("소모", StringComparison.Ordinal) >= 0;
        }
    }
}
