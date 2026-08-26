using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §3 SkillDef.초필살기 소비처. QA_NO_SKILL_ULT면 초필 줄을 비운다(옛 화면).
    /// </summary>
    public static class SkillUltSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Skill Ult Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noUlt = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillUlt);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillUlt, null);

            var defs = Resources.LoadAll<JobDef>("jobs");
            Check(defs != null && defs.Length > 0,
                $"Resources/jobs 로드 ({(defs == null ? 0 : defs.Length)}종)");
            Check(!JobInfo.SkillUltBlocked, "기본은 켜짐");

            int withUlt = 0;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null || string.IsNullOrEmpty(d.직업명) || d.스킬 == null) continue;
                    string line = JobInfo.SkillUltLine(d.직업명);
                    string skills = JobInfo.SkillLine(d.직업명);
                    string desc = JobInfo.SkillDescLine(d.직업명);
                    int ultCount = 0;
                    foreach (var s in d.스킬)
                    {
                        if (s == null || string.IsNullOrEmpty(s.이름) || !s.초필살기) continue;
                        ultCount++;
                        withUlt++;
                        Check(!string.IsNullOrEmpty(line) && line.StartsWith("초필살기 — ", StringComparison.Ordinal),
                            $"{d.직업명}: SkillUltLine 접두 — 「{line}」");
                        Check(line.Contains(s.이름),
                            $"{d.직업명}: SkillUltLine이 초필 필드를 읽는다 ({s.이름}) — 「{line}」");
                        if (s.쿨다운 > 0f)
                            Check(line.Contains(s.쿨다운.ToString("0.#") + "초"),
                                $"{d.직업명}: 초필 쿨 {s.쿨다운:0.#}초 — 「{line}」");
                        Check(string.IsNullOrEmpty(skills) || skills.IndexOf(s.이름, StringComparison.Ordinal) < 0,
                            $"{d.직업명}: 초필은 SkillLine에 안 붙는다 ({s.이름}) — 「{skills}」");
                        Check(string.IsNullOrEmpty(desc) || desc.IndexOf(s.이름 + ":", StringComparison.Ordinal) < 0,
                            $"{d.직업명}: 초필은 SkillDescLine에 안 붙는다 ({s.이름}) — 「{desc}」");
                    }
                    if (ultCount == 0)
                        Check(line == "",
                            $"{d.직업명}: authored 초필 없으면 빈 문자열(지어내지 않음) — 「{line}」");
                }
            Check(withUlt > 0, $"표시 초필 스킬 {withUlt}개 검사됨 (0이면 배선 확인 불가)");

            string guard = JobInfo.SkillUltLine("수호기사");
            Check(guard.Contains("파티 전원 무적(180초)"),
                $"수호기사 초필 180초 — 「{guard}」");

            string mage = JobInfo.SkillUltLine("마법사");
            Check(mage == "",
                $"마법사는 authored 초필 없음(지어내지 않음) — 「{mage}」");

            Check(JobInfo.SkillUltLine("없는직업") == "", "모르는 직업은 빈 문자열(지어내지 않음)");

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillUlt, "1");
            Check(JobInfo.SkillUltBlocked, "QA_NO면 차단");
            Check(JobInfo.SkillUltLine("수호기사") == "",
                "차단하면 초필 줄 없음(옛 화면)");
            Check(JobInfo.SkillLine("수호기사").Contains("도발의 함성(6초"),
                "차단해도 SkillLine 일반 스킬은 유지");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillUlt, null);
            Check(!JobInfo.SkillUltBlocked
                  && JobInfo.SkillUltLine("수호기사").Contains("파티 전원 무적(180초)"),
                "차단을 풀면 다시 초필 줄");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            // 2026-08-26 갱신 — 화면은 RebirthSkill.SkillUltLine(계승 위임)을 쓴다.
            Check(charSrc.Contains("RebirthSkill.SkillUltLine"),
                "CharacterScreen이 RebirthSkill.SkillUltLine을 속성 탭에 그린다");
            Check(charSrc.Contains("SkillDef.초필살기") || charSrc.Contains("초필살기"),
                "CharacterScreen 주석이 초필 소비처를 가리킨다");
            Check(charSrc.Contains("QA_SKILL_ULT"),
                "CharacterScreen이 검사 시드를 갖는다");

            string jobSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/JobInfo.cs"));
            Check(jobSrc.Contains("s.초필살기"),
                "JobInfo가 s.초필살기를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(JobInfo.SkillUltLine);
            _ = nameof(SkillDef.초필살기);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillUlt, noUlt);

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "skill_ult_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS SkillUltSelfCheck" : "FAIL SkillUltSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[SkillUltSelfCheck] PASS → " + path);
            else Debug.LogError("[SkillUltSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[SkillUltSelfCheck] FAIL {_fail}건");
        }
    }
}
