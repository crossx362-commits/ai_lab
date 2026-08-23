using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §3 직업 스킬 표 — SkillDef.쿨다운 소비처.
    /// QA_NO_SKILL_CD면 이름만 남긴다(옛 SkillLine).
    /// </summary>
    public static class SkillCdSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Skill Cd Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillCd);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, null);

            var defs = Resources.LoadAll<JobDef>("jobs");
            Check(defs != null && defs.Length > 0,
                $"Resources/jobs 로드 ({(defs == null ? 0 : defs.Length)}종)");

            Check(!JobInfo.SkillCdBlocked, "기본은 켜짐");

            int withCd = 0;
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
                        if (s.쿨다운 <= 0f)
                        {
                            Check(line.IndexOf(s.이름 + "(", StringComparison.Ordinal) < 0,
                                $"{d.직업명}: 쿨 0인 {s.이름}에는 (N초)를 안 붙인다 — 「{line}」");
                            continue;
                        }
                        withCd++;
                        string want = s.이름 + "(" + s.쿨다운.ToString("0.#") + "초)";
                        Check(line.Contains(want),
                            $"{d.직업명}: SkillLine이 쿨다운 필드를 읽는다 ({want}) — 「{line}」");
                    }
                }
            Check(withCd > 0, $"쿨다운>0 스킬 {withCd}개 검사됨 (0이면 배선 확인 불가)");

            // 수호기사 앵커 — ProjectSetup authored 도발 6 · 최후 40 · 성채 0(게이지).
            string guard = JobInfo.SkillLine("수호기사");
            Check(guard.Contains("도발의 함성(6초)"),
                $"수호기사 도발 6초 — 「{guard}」");
            Check(guard.Contains("최후의 보루(40초)"),
                $"수호기사 최후 40초 — 「{guard}」");
            Check(guard.Contains("성채 방패") && guard.IndexOf("성채 방패(", StringComparison.Ordinal) < 0,
                $"수호기사 성채(쿨0)는 이름만 — 「{guard}」");

            Check(JobInfo.SkillLine("없는직업") == "", "모르는 직업은 빈 문자열(지어내지 않음)");

            // 네거티브: QA_NO면 이름만(옛 줄).
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, "1");
            Check(JobInfo.SkillCdBlocked, "QA_NO면 차단");
            string old = JobInfo.SkillLine("수호기사");
            Check(old.Contains("도발의 함성") && old.IndexOf("도발의 함성(", StringComparison.Ordinal) < 0,
                $"차단하면 도발은 이름만 — 「{old}」");
            Check(old.IndexOf("초)", StringComparison.Ordinal) < 0,
                $"차단하면 (N초) 조각이 없다 — 「{old}」");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, null);
            Check(!JobInfo.SkillCdBlocked && JobInfo.SkillLine("수호기사").Contains("도발의 함성(6초)"),
                "차단을 풀면 다시 쿨다운 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("JobInfo.SkillLine"),
                "CharacterScreen이 SkillLine을 속성 탭에 그린다");

            string jobSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/JobInfo.cs"));
            Check(jobSrc.Contains("s.쿨다운"),
                "JobInfo가 s.쿨다운을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(JobInfo.SkillLine);
            _ = nameof(SkillDef.쿨다운);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, no);

            if (_fail == 0) Debug.Log("[SkillCdSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SkillCdSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[SkillCdSelfCheck] FAIL {_fail}건");
        }
    }
}
