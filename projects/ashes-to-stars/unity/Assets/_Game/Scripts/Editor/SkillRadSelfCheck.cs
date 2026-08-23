using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §3 SkillDef.반경 소비처. QA_NO_SKILL_RAD면 반경 조각을 뺀다(옛 SkillLine).
    /// </summary>
    public static class SkillRadSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Skill Rad Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noCd = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillCd);
            string noPow = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillPow);
            string noRad = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillRad);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, null);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillPow, null);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillRad, null);

            var defs = Resources.LoadAll<JobDef>("jobs");
            Check(defs != null && defs.Length > 0,
                $"Resources/jobs 로드 ({(defs == null ? 0 : defs.Length)}종)");
            Check(!JobInfo.SkillRadBlocked, "기본은 켜짐");

            int withRad = 0;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null || string.IsNullOrEmpty(d.직업명) || d.스킬 == null) continue;
                    string line = JobInfo.SkillLine(d.직업명);
                    Check(!string.IsNullOrEmpty(line) && line.StartsWith("보유 스킬 — ", StringComparison.Ordinal),
                        $"{d.직업명}: SkillLine 접두 — 「{line}」");
                    foreach (var s in d.스킬)
                    {
                        if (s == null || string.IsNullOrEmpty(s.이름)) continue;
                        Check(line.Contains(s.이름),
                            $"{d.직업명}: 형제 이름 보존 ({s.이름}) — 「{line}」");
                        bool notable = s.반경 > 0f && s.반경 < JobInfo.SkillRadDisplayCap;
                        if (!notable)
                        {
                            if (s.반경 >= JobInfo.SkillRadDisplayCap)
                                Check(line.IndexOf("반경" + s.반경.ToString("0.#"), StringComparison.Ordinal) < 0,
                                    $"{d.직업명}: {s.이름} 전역표식 반경 숫자 없음 (값={s.반경}) — 「{line}」");
                            continue;
                        }
                        withRad++;
                        string rad = "반경" + s.반경.ToString("0.#");
                        Check(line.Contains(rad),
                            $"{d.직업명}: SkillLine이 반경 필드를 읽는다 ({s.이름} {rad}) — 「{line}」");
                        if (s.쿨다운 <= 0f)
                            Check(line.IndexOf(s.이름 + "(", StringComparison.Ordinal) < 0,
                                $"{d.직업명}: 쿨0 {s.이름}에 이름( 금지 — 「{line}」");
                    }
                }
            Check(withRad > 0, $"표시 반경 스킬 {withRad}개 검사됨 (0이면 배선 확인 불가)");

            string mage = JobInfo.SkillLine("마법사");
            Check(mage.Contains("화염폭풍(5초·×1.2·반경3.2)"),
                $"마법사 화염폭풍 5초·×1.2·반경3.2 — 「{mage}」");
            Check(mage.Contains("빙결(10초·×0.4·반경4)"),
                $"마법사 빙결 10초·×0.4·반경4 — 「{mage}」");
            Check(mage.Contains("점멸(8초)") && mage.IndexOf("점멸(8초·", StringComparison.Ordinal) < 0,
                $"마법사 점멸(반경0)은 쿨만 — 「{mage}」");

            string guard = JobInfo.SkillLine("수호기사");
            Check(guard.Contains("도발의 함성(6초·반경4.5)"),
                $"수호기사 도발 6초·반경4.5 — 「{guard}」");
            Check(guard.Contains("성채 방패 반경8") && guard.IndexOf("성채 방패(", StringComparison.Ordinal) < 0,
                $"수호기사 성채(쿨0) 반경8 괄호 없음 — 「{guard}」");

            string bard = JobInfo.SkillLine("음유시인");
            Check(bard.Contains("진군가 반경8") && bard.IndexOf("진군가(", StringComparison.Ordinal) < 0,
                $"음유시인 진군가(쿨0·위력0) 반경8 — 「{bard}」");

            string priest = JobInfo.SkillLine("사제");
            Check(priest.Contains("기적") && priest.IndexOf("반경99", StringComparison.Ordinal) < 0,
                $"사제 기적(99)은 전역 표식이라 반경 숫자 없음 — 「{priest}」");

            Check(JobInfo.SkillLine("없는직업") == "", "모르는 직업은 빈 문자열(지어내지 않음)");

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillRad, "1");
            Check(JobInfo.SkillRadBlocked, "QA_NO면 차단");
            string old = JobInfo.SkillLine("마법사");
            Check(old.Contains("화염폭풍(5초·×1.2)") && old.IndexOf("반경", StringComparison.Ordinal) < 0,
                $"차단하면 화염폭풍은 쿨·위력만 — 「{old}」");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillRad, null);
            Check(!JobInfo.SkillRadBlocked && JobInfo.SkillLine("마법사").Contains("화염폭풍(5초·×1.2·반경3.2)"),
                "차단을 풀면 다시 반경 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("JobInfo.SkillLine"),
                "CharacterScreen이 SkillLine을 속성 탭에 그린다");
            Check(charSrc.Contains("반경"),
                "CharacterScreen 주석이 반경 소비처를 가리킨다");

            string jobSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/JobInfo.cs"));
            Check(jobSrc.Contains("s.반경"),
                "JobInfo가 s.반경을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(JobInfo.SkillLine);
            _ = nameof(SkillDef.반경);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, noCd);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillPow, noPow);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillRad, noRad);

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "skill_rad_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS SkillRadSelfCheck" : "FAIL SkillRadSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[SkillRadSelfCheck] PASS → " + path);
            else Debug.LogError("[SkillRadSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[SkillRadSelfCheck] FAIL {_fail}건");
        }
    }
}
