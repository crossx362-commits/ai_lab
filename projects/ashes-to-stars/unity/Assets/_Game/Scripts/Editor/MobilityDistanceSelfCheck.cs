using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §5 이동기 기본 사양 표(원장 374 「이동 거리 | 캐릭터 기본 이동 3초분」, 헤더 369 ✅ 오너 결정
    /// 2026-08-13) 1행인 <c>JobDef.이동기거리</c>를 화면에 배선한다. 형제 무적·쿨(5cc0a8ee)은 이미
    /// JobInfo.MobilityStatLine에 있었는데 같은 표 1행 거리만 소비처 0곳(정의 JobDef.cs:29 default 3f,
    /// grep 소비처 0곳)이었다 — 이 저장소가 반복해 겪은 「정의만 있고 부르는 곳이 0곳」 함정.
    ///
    /// 배선은 표시 전용(문구). 전투·밸런스 수치는 안 건드린다. 값은 커밋/기본 값을 그대로 읽는다.
    /// </summary>
    public static class MobilityDistanceSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mobility Distance Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            var defs = Resources.LoadAll<JobDef>("jobs");
            Check(defs != null && defs.Length > 0,
                $"Resources/jobs/*.asset 로드 ({(defs == null ? 0 : defs.Length)}종)");

            // 형제 무적·쿨은 이미 표시되고 있었다(5cc0a8ee) — 거리만 죽어 있었다.
            // 배선 후: 문자열이 이동기거리 필드 값을 실제로 읽어 "거리 {값}초분"을 낸다.
            int seen = 0;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null || string.IsNullOrEmpty(d.직업명)) continue;
                    if (d.이동기거리 <= 0f) continue;
                    seen++;
                    string line = JobInfo.MobilityStatLine(d.직업명);
                    string want = "거리 " + d.이동기거리.ToString("0.#") + "초분";
                    Check(line.Contains(want),
                        $"{d.직업명}: MobilityStatLine이 이동기거리 필드값을 읽는다 ({want}) — 실제 「{line}」");
                    // 표 순서(거리→무적→쿨): 거리가 무적·쿨보다 앞. 형제 substring도 보존.
                    int di = line.IndexOf("거리", StringComparison.Ordinal);
                    int mi = line.IndexOf("무적", StringComparison.Ordinal);
                    Check(di >= 0 && (mi < 0 || di < mi),
                        $"{d.직업명}: 거리가 무적·쿨 앞 (거리 idx {di} · 무적 idx {mi})");
                    Check(line.Contains("무적 " + d.무적시간.ToString("0.#") + "초 · 쿨 "
                                        + d.이동기쿨.ToString("0.#") + "초"),
                        $"{d.직업명}: 형제 무적·쿨 조각 보존");
                }
            Check(seen > 0, $"이동기거리>0 직업 {seen}종 검사됨 (0이면 배선 확인 불가)");

            // 매칭 없는 직업은 지어내지 않는다(빈 문자열).
            Check(JobInfo.MobilityStatLine("없는직업") == "", "모르는 직업은 빈 문자열(지어내지 않음)");

            // 소비처(CharacterScreen)가 실제로 이 문자열을 그린다 — 배선의 반대편.
            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("JobInfo.MobilityStatLine"),
                "CharacterScreen이 MobilityStatLine을 프로필 행에 그린다");

            // 네거티브 컨트롤 재현 근거: 배선을 제거하면(거리 조각을 안 붙이면) 위 「거리 {값}초분」
            // 단언들이 FAIL한다. meas 사본에서 JobInfo의 거리 append 줄을 지우고 이 SelfCheck를
            // 다시 돌리면 seen종만큼 FAIL이 뜬다(mobility_distance_negctrl.log).
            string jobSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/JobInfo.cs"));
            Check(jobSrc.Contains("d.이동기거리"),
                "JobInfo가 d.이동기거리를 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(JobInfo.MobilityStatLine);
            _ = nameof(CharacterScreen);

            if (_fail == 0) Debug.Log("[MobilityDistanceSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[MobilityDistanceSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[MobilityDistanceSelfCheck] FAIL {_fail}건");
        }
    }
}
