using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §3 직업 스킬 표 — SkillDef.위력배율 소비처.
    /// QA_NO_SKILL_POW면 위력 조각을 뺀다(옛 SkillLine = 이름·쿨만).
    /// </summary>
    public static class SkillPowSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Skill Pow Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noCd = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillCd);
            string noPow = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillPow);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, null);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillPow, null);

            var defs = Resources.LoadAll<JobDef>("jobs");
            Check(defs != null && defs.Length > 0,
                $"Resources/jobs 로드 ({(defs == null ? 0 : defs.Length)}종)");

            Check(!JobInfo.SkillPowBlocked, "기본은 켜짐");

            int withPow = 0;
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
                        bool notable = s.위력배율 > 0f && Mathf.Abs(s.위력배율 - 1f) >= 0.0001f;
                        if (!notable) continue;
                        withPow++;
                        string mul = "×" + s.위력배율.ToString("0.##");
                        Check(line.Contains(mul),
                            $"{d.직업명}: SkillLine이 위력배율 필드를 읽는다 ({s.이름} {mul}) — 「{line}」");
                        if (s.쿨다운 > 0f)
                        {
                            string want = s.이름 + "(" + s.쿨다운.ToString("0.#") + "초·" + mul + ")";
                            Check(line.Contains(want),
                                $"{d.직업명}: 쿨+위력 합쳐 표기 ({want}) — 「{line}」");
                        }
                        else
                        {
                            Check(line.Contains(s.이름 + " " + mul),
                                $"{d.직업명}: 쿨0·위력은 괄호 없이 ({s.이름} {mul}) — 「{line}」");
                            Check(line.IndexOf(s.이름 + "(", StringComparison.Ordinal) < 0,
                                $"{d.직업명}: 쿨0 {s.이름}에 이름( 금지(SkillCd 호환) — 「{line}」");
                        }
                    }
                }
            Check(withPow > 0, $"위력≠1 스킬 {withPow}개 검사됨 (0이면 배선 확인 불가)");

            // 앵커 — ProjectSetup authored.
            string mage = JobInfo.SkillLine("마법사");
            Check(mage.Contains("화염폭풍(5초·×1.2)"),
                $"마법사 화염폭풍 5초·×1.2 — 「{mage}」");
            Check(mage.Contains("빙결(10초·×0.4)"),
                $"마법사 빙결 10초·×0.4 — 「{mage}」");
            Check(mage.Contains("점멸(8초)") && mage.IndexOf("점멸(8초·", StringComparison.Ordinal) < 0,
                $"마법사 점멸(위력0)은 쿨만 — 「{mage}」");

            string sword = JobInfo.SkillLine("검사");
            Check(sword.Contains("일섬 ×3.2"),
                $"검사 일섬(쿨0) ×3.2 괄호 없음 — 「{sword}」");
            Check(sword.Contains("발도(8초·×1.2)"),
                $"검사 발도 8초·×1.2 — 「{sword}」");

            string guard = JobInfo.SkillLine("수호기사");
            Check(guard.Contains("도발의 함성(6초)") && guard.IndexOf("도발의 함성(6초·", StringComparison.Ordinal) < 0,
                $"수호기사 도발(위력0)은 쿨만 — 「{guard}」");

            Check(JobInfo.SkillLine("없는직업") == "", "모르는 직업은 빈 문자열(지어내지 않음)");

            // 네거티브: QA_NO면 위력 조각만 빠지고 쿨은 남음(옛 줄).
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillPow, "1");
            Check(JobInfo.SkillPowBlocked, "QA_NO면 차단");
            string old = JobInfo.SkillLine("마법사");
            Check(old.Contains("화염폭풍(5초)") && old.IndexOf("×1.2", StringComparison.Ordinal) < 0,
                $"차단하면 화염폭풍은 쿨만 — 「{old}」");
            Check(old.IndexOf("×", StringComparison.Ordinal) < 0,
                $"차단하면 ×P 조각이 없다 — 「{old}」");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillPow, null);
            Check(!JobInfo.SkillPowBlocked && JobInfo.SkillLine("마법사").Contains("화염폭풍(5초·×1.2)"),
                "차단을 풀면 다시 위력 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("JobInfo.SkillLine"),
                "CharacterScreen이 SkillLine을 속성 탭에 그린다");
            Check(charSrc.Contains("SkillDef.위력배율") || charSrc.Contains("위력배율"),
                "CharacterScreen 주석이 위력배율 소비처를 가리킨다");

            string jobSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/JobInfo.cs"));
            Check(jobSrc.Contains("s.위력배율"),
                "JobInfo가 s.위력배율을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(JobInfo.SkillLine);
            _ = nameof(SkillDef.위력배율);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillCd, noCd);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillPow, noPow);

            if (_fail == 0) Debug.Log("[SkillPowSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SkillPowSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[SkillPowSelfCheck] FAIL {_fail}건");
        }
    }
}
