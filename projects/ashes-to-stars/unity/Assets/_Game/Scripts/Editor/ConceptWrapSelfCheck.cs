using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §4 직업 특성 ConceptLine 접기. QA_NO_CONCEPT_WRAP면 옛 한 줄 Clip.
    /// 수호기사 고유메커니즘 「소모해 보호막」이 LabelClip에 잘리던 화면.
    /// </summary>
    public static class ConceptWrapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Concept Wrap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noWrap = Environment.GetEnvironmentVariable(JobInfo.EnvNoConceptWrap);
            Environment.SetEnvironmentVariable(JobInfo.EnvNoConceptWrap, null);

            var defs = Resources.LoadAll<JobDef>("jobs");
            Check(defs != null && defs.Length > 0,
                $"Resources/jobs 로드 ({(defs == null ? 0 : defs.Length)}종)");
            Check(!JobInfo.ConceptWrapBlocked, "기본 접기는 켜짐");

            string guard = JobInfo.ConceptLine("수호기사");
            Check(guard.StartsWith("직업 특성 — ", StringComparison.Ordinal),
                $"수호기사 ConceptLine 접두 — 「{guard}」");
            Check(guard.Contains("정통 방패탱"),
                $"수호기사 컨셉 — 「{guard}」");
            Check(guard.Contains("수호 게이지") && guard.Contains("소모해 보호막"),
                $"수호기사 고유메커니즘 끝 글자 — 「{guard}」");

            string mage = JobInfo.ConceptLine("마법사");
            Check(mage.Contains("광역 섬멸") && mage.Contains("마나 순환"),
                $"마법사 ConceptLine(짧은 칸) — 「{mage}」");

            Check(JobInfo.ConceptLine("없는직업") == "", "모르는 직업은 빈 문자열(지어내지 않음)");

            Environment.SetEnvironmentVariable(JobInfo.EnvNoConceptWrap, "1");
            Check(JobInfo.ConceptWrapBlocked, "QA_NO면 차단");
            Check(JobInfo.ConceptLine("수호기사").Contains("소모해 보호막"),
                "접기 차단해도 문구는 같다(그리기만 한 줄 Clip)");
            Environment.SetEnvironmentVariable(JobInfo.EnvNoConceptWrap, null);
            Check(!JobInfo.ConceptWrapBlocked, "접기 차단을 풀면 다시 두 줄");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("JobInfo.ConceptLine"),
                "CharacterScreen이 ConceptLine을 속성 탭에 그린다");
            Check(charSrc.Contains("QA_JOB_TRAIT"),
                "CharacterScreen이 수호기사 검사 시드를 갖는다");
            Check(charSrc.Contains("ConceptWrapBlocked"),
                "QA_NO_CONCEPT_WRAP면 옛 한 줄 Clip");
            int traitAt = charSrc.IndexOf("string trait = JobInfo.ConceptLine", StringComparison.Ordinal);
            Check(traitAt >= 0, "ConceptLine 지역변수가 있다");
            if (traitAt >= 0)
            {
                int nextLine = charSrc.IndexOf("string skills = JobInfo.SkillLine", traitAt, StringComparison.Ordinal);
                if (nextLine < 0) nextLine = charSrc.Length;
                string block = charSrc.Substring(traitAt, nextLine - traitAt);
                Check(block.Contains("InfoWrap"),
                    "직업 특성 줄은 InfoWrap(LabelFit) — Info LabelClip이면 「소모해 보」에서 잘린다");
                Check(block.Contains("RowHt"),
                    "직업 특성은 한 행 높이(RowHt) — 기본 52면 초필 줄이 밀린다");
                Check(block.Contains("ConceptWrapBlocked"),
                    "직업 특성 블록이 QA_NO 분기를 갖는다");
            }

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
                Check(wrapBody.Contains("float minH"),
                    "InfoWrap이 minH를 받는다 — 한 행 접기에 필요");
            }

            string jobSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/JobInfo.cs"));
            Check(jobSrc.Contains("EnvNoConceptWrap"),
                "JobInfo가 접기 QA_NO를 갖는다");
            Check(jobSrc.Contains("d.고유메커니즘"),
                "JobInfo가 고유메커니즘을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(JobInfo.ConceptLine);
            _ = nameof(JobInfo.ConceptWrapBlocked);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(JobInfo.EnvNoConceptWrap, noWrap);

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "concept_wrap_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS ConceptWrapSelfCheck" : "FAIL ConceptWrapSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[ConceptWrapSelfCheck] PASS → " + path);
            else Debug.LogError("[ConceptWrapSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[ConceptWrapSelfCheck] FAIL {_fail}건");
        }
    }
}
