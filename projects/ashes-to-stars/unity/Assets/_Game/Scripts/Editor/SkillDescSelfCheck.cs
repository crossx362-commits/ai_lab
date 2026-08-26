using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §3 SkillDef.설명 소비처. QA_NO_SKILL_DESC면 설명 줄을 비운다(옛 화면).
    /// </summary>
    public static class SkillDescSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Skill Desc Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noDesc = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillDesc);
            string noWrap = Environment.GetEnvironmentVariable(JobInfo.EnvNoSkillDescWrap);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillDesc, null);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillDescWrap, null);

            var defs = Resources.LoadAll<JobDef>("jobs");
            Check(defs != null && defs.Length > 0,
                $"Resources/jobs 로드 ({(defs == null ? 0 : defs.Length)}종)");
            Check(!JobInfo.SkillDescBlocked, "기본은 켜짐");

            int withDesc = 0;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null || string.IsNullOrEmpty(d.직업명) || d.스킬 == null) continue;
                    string line = JobInfo.SkillDescLine(d.직업명);
                    Check(!string.IsNullOrEmpty(line) && line.StartsWith("스킬 설명 — ", StringComparison.Ordinal),
                        $"{d.직업명}: SkillDescLine 접두 — 「{line}」");
                    foreach (var s in d.스킬)
                    {
                        if (s == null || string.IsNullOrEmpty(s.이름) || s.초필살기) continue;
                        if (string.IsNullOrEmpty(s.설명))
                        {
                            Check(!PieceHasName(line, s.이름),
                                $"{d.직업명}: {s.이름} 설명 공백은 조각 없음 — 「{line}」");
                            continue;
                        }
                        withDesc++;
                        string want = s.이름 + ": " + s.설명;
                        Check(line.Contains(want),
                            $"{d.직업명}: SkillDescLine이 설명 필드를 읽는다 ({want}) — 「{line}」");
                    }
                    string stats = JobInfo.SkillLine(d.직업명);
                    Check(!string.IsNullOrEmpty(stats) && stats.IndexOf("스킬 설명", StringComparison.Ordinal) < 0,
                        $"{d.직업명}: SkillLine은 숫자만 — 「{stats}」");
                }
            Check(withDesc > 0, $"표시 설명 스킬 {withDesc}개 검사됨 (0이면 배선 확인 불가)");

            string mage = JobInfo.SkillDescLine("마법사");
            Check(mage.Contains("화염폭풍:") && mage.Contains("장판 광역"),
                $"마법사 화염폭풍 설명 — 「{mage}」");
            Check(mage.Contains("점멸: 순간이동 회피"),
                $"마법사 점멸 설명 — 「{mage}」");
            Check(mage.Contains("빙결: 광역 슬로우"),
                $"마법사 빙결 설명 — 「{mage}」");

            string sword = JobInfo.SkillDescLine("검사");
            Check(sword.Contains("일섬:") && sword.Contains("스택 5"),
                $"검사 일섬 설명 — 「{sword}」");

            Check(JobInfo.SkillDescLine("없는직업") == "", "모르는 직업은 빈 문자열(지어내지 않음)");

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillDesc, "1");
            Check(JobInfo.SkillDescBlocked, "QA_NO면 차단");
            Check(JobInfo.SkillDescLine("마법사") == "",
                "차단하면 설명 줄 없음(옛 화면)");
            Check(JobInfo.SkillLine("마법사").Contains("화염폭풍(5초·×1.2·반경3.2)"),
                "차단해도 SkillLine 숫자는 유지");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillDesc, null);
            Check(!JobInfo.SkillDescBlocked
                  && JobInfo.SkillDescLine("마법사").Contains("화염폭풍: 장판 광역"),
                "차단을 풀면 다시 설명 줄");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            // 2026-08-26 갱신 — 화면은 RebirthSkill.SkillDescLine(계승 위임)을 쓴다.
            Check(charSrc.Contains("RebirthSkill.SkillDescLine"),
                "CharacterScreen이 RebirthSkill.SkillDescLine을 속성 탭에 그린다");
            Check(charSrc.Contains("SkillDef.설명") || charSrc.Contains("s.설명")
                  || charSrc.IndexOf("설명", StringComparison.Ordinal) >= 0,
                "CharacterScreen 주석이 설명 소비처를 가리킨다");
            Check(charSrc.Contains("QA_SKILL_DESC"),
                "CharacterScreen이 검사 시드를 갖는다");
            Check(charSrc.Contains("InfoWrap"),
                "설명 줄은 InfoWrap(두 줄 LabelFit) — Info LabelClip이면 「빙결: 광」에서 잘린다");
            Check(charSrc.Contains("SkillDescWrapBlocked"),
                "QA_NO_SKILL_DESC_WRAP면 옛 한 줄 Clip");

            string gameSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/GameScreen.cs"));
            int wrapAt = gameSrc.IndexOf("protected int InfoWrap", StringComparison.Ordinal);
            Check(wrapAt >= 0, "GameScreen.InfoWrap이 있다");
            if (wrapAt >= 0)
            {
                int wrapEnd = gameSrc.IndexOf("protected void InfoAt", wrapAt, StringComparison.Ordinal);
                if (wrapEnd < 0) wrapEnd = gameSrc.Length;
                string wrapBody = gameSrc.Substring(wrapAt, wrapEnd - wrapAt);
                Check(wrapBody.Contains("LabelFit"),
                    "InfoWrap은 LabelFit — LabelClip이면 우측이 다시 잘린다");
                Check(wrapBody.IndexOf("20f", StringComparison.Ordinal) < 0,
                    "InfoWrap inner는 20px 한 줄이 아니다");
                Check(wrapBody.Contains("52f"),
                    "InfoWrap 최소 높이는 두 줄(52)");
            }

            Check(!JobInfo.SkillDescWrapBlocked, "기본 접기는 켜짐");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillDescWrap, "1");
            Check(JobInfo.SkillDescWrapBlocked, "QA_NO_WRAP면 차단");
            Check(JobInfo.SkillDescLine("마법사").Contains("빙결: 광역 슬로우"),
                "접기 차단해도 문구는 같다(그리기만 한 줄 Clip)");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillDescWrap, null);
            Check(!JobInfo.SkillDescWrapBlocked, "접기 차단을 풀면 다시 두 줄");

            string jobSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/JobInfo.cs"));
            Check(jobSrc.Contains("s.설명"),
                "JobInfo가 s.설명을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            Check(jobSrc.Contains("EnvNoSkillDescWrap"),
                "JobInfo가 접기 QA_NO를 갖는다");

            _ = nameof(JobInfo.SkillDescLine);
            _ = nameof(JobInfo.SkillDescWrapBlocked);
            _ = nameof(SkillDef.설명);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillDesc, noDesc);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoSkillDescWrap, noWrap);

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "skill_desc_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS SkillDescSelfCheck" : "FAIL SkillDescSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[SkillDescSelfCheck] PASS → " + path);
            else Debug.LogError("[SkillDescSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[SkillDescSelfCheck] FAIL {_fail}건");
        }

        static bool PieceHasName(string line, string name)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(name)) return false;
            return line.IndexOf(name + ":", StringComparison.Ordinal) >= 0;
        }
    }
}
