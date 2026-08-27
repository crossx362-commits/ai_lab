using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// V4 루프 경계 로그. 삭제 판정은 LifeSystem/Memorial 이벤트, Cap 문구 아님.
    /// 30분 가드·닫힌 enum·QA_NO 무기록. 사람 70%/24h·STATUS V4 PASS는 여기 없다.
    /// </summary>
    public static class V4LoopBoundarySelfCheck
    {
        const string SidGrown = "v4self_grown";
        const string SidYoung = "v4self_young";
        const string SidNo = "v4self_qano";
        const string SidEvents = "v4self_events";
        const string SidRound = "v4self_round";
        const string SidZero = "v4self_zero";
        const string SidOld = "v4self_oldsave";

        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/V4 Loop Boundary Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(V4LoopLog.EnvNo);
            string sid = Environment.GetEnvironmentVariable(V4LoopLog.EnvSession);
            Environment.SetEnvironmentVariable(V4LoopLog.EnvNo, null);
            Environment.SetEnvironmentVariable(V4LoopLog.EnvSession, null);
            var prevNow = LifeSystem.NowUnix;

            Check(V4LoopLog.GrowthGuardSeconds == 30 * 60,
                $"가드 30분={V4LoopLog.GrowthGuardSeconds}초");
            Check(V4LoopLog.Events.Length == 5, $"event 5종 (실제 {V4LoopLog.Events.Length})");
            Check(V4LoopLog.Reasons.Length == 5, $"reason 5종 (실제 {V4LoopLog.Reasons.Length})");
            Check(V4LoopLog.IsEvent("permadeath") && V4LoopLog.IsEvent("rebuild_offer")
                  && V4LoopLog.IsEvent("rebuild_accept") && V4LoopLog.IsEvent("rebuild_decline")
                  && V4LoopLog.IsEvent("session_end"),
                "event 닫힌 집합");
            Check(!V4LoopLog.IsEvent("deleted") && !V4LoopLog.IsEvent("삭제")
                  && !V4LoopLog.IsEvent(""),
                "event에 Cap/임의 문자열 없음");
            Check(V4LoopLog.IsReason("조작 불명확") && V4LoopLog.IsReason("난이도")
                  && V4LoopLog.IsReason("손실 분노") && V4LoopLog.IsReason("다시 키우기 지루함")
                  && V4LoopLog.IsReason("기술 문제") && V4LoopLog.IsReason(""),
                "reason 다섯 + 빈 칸");
            Check(!V4LoopLog.IsReason("기타") && !V4LoopLog.IsReason("삭제됨"),
                "reason 여섯 번째 거부");

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string writer = File.ReadAllText(Path.Combine(runtime, "V4LoopLog.cs"));
            string life = File.ReadAllText(Path.Combine(runtime, "LifeSystem.cs"));
            string memorial = File.ReadAllText(Path.Combine(runtime, "Memorial.cs"));
            Check(writer.IndexOf("삭제됨", StringComparison.Ordinal) < 0
                  && writer.IndexOf("PartyHudCap", StringComparison.Ordinal) < 0
                  && writer.IndexOf("DeletedShort", StringComparison.Ordinal) < 0
                  && writer.IndexOf("OldDeleted", StringComparison.Ordinal) < 0,
                "로그 작성기에 Cap 표시 문자열 없음");
            Check(writer.IndexOf("30 * 60", StringComparison.Ordinal) >= 0,
                "가드 상수가 30 * 60");
            Check(life.IndexOf("V4LoopLog.NotePermadeath", StringComparison.Ordinal) >= 0
                  && life.IndexOf("V4LoopLog.NoteRebuildOffer", StringComparison.Ordinal) >= 0,
                "LifeSystem이 permadeath·rebuild_offer를 건다");
            Check(memorial.IndexOf("V4LoopLog.NotePermadeath", StringComparison.Ordinal) >= 0,
                "Memorial.Stamp가 permadeath를 건다");
            Check(life.IndexOf("GrowthStartUnix", StringComparison.Ordinal) >= 0,
                "로스터가 성장 시작 벽시계를 갖는다");
            Check(life.IndexOf("Append(c.GrowthStartUnix)", StringComparison.Ordinal) >= 0,
                "StageRosterForSave가 GrowthStartUnix를 남긴다");
            Check(life.IndexOf("p.Length > 17 ? SafeLong(p[17], 0) : 0", StringComparison.Ordinal) >= 0,
                "옛 저장·0은 GrowthStartUnix를 Now로 안 채운다");

            // ── 30분 미만: 삭제는 되고 표본 로그는 없다 ──
            WipeSession(SidYoung);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            V4LoopLog.ResetForTest();
            long t0 = 1_800_000_000L;
            LifeSystem.NowUnix = () => t0;
            V4LoopLog.ForceSessionIdForTest(SidYoung);
            var young = LifeSystem.GetCharacters()[0];
            young.DeathCount = 2;
            LifeSystem.PersistRoster();
            Check(young.GrowthStartUnix == t0, $"미만 생성 시각 {young.GrowthStartUnix}");
            Check(!V4LoopLog.MeetsGrowthGuard(young), "생성 직후는 가드 미달");
            LifeSystem.RegisterDeath(young);
            Check(young.IsDeleted, "30분 미만도 삭제는 된다(규칙 불변)");
            Check(!File.Exists(V4LoopLog.CurrentPath),
                "30분 미만은 V4 표본 파일을 안 만든다");

            // ── 30분 이상: permadeath 한 줄, 필드 집합 ──
            WipeSession(SidGrown);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            V4LoopLog.ResetForTest();
            LifeSystem.NowUnix = () => t0;
            V4LoopLog.ForceSessionIdForTest(SidGrown);
            var grown = LifeSystem.GetCharacters()[0];
            grown.GrowthStartUnix = t0;
            grown.DeathCount = 2;
            LifeSystem.PersistRoster();
            LifeSystem.NowUnix = () => t0 + V4LoopLog.GrowthGuardSeconds;
            Check(V4LoopLog.MeetsGrowthGuard(grown), "30분 경과면 가드 통과");
            LifeSystem.RegisterDeath(grown);
            Check(grown.IsDeleted, "30분 이상 삭제");
            string grownPath = V4LoopLog.CurrentPath;
            Check(File.Exists(grownPath), $"permadeath 파일 ({grownPath})");
            string grownBody = File.Exists(grownPath) ? File.ReadAllText(grownPath) : "";
            Check(grownBody.IndexOf("\"event\":\"permadeath\"", StringComparison.Ordinal) >= 0,
                "permadeath 이벤트");
            Check(HasMinFields(FirstLine(grownBody)),
                $"최소 필드 (실제 {FirstLine(grownBody)})");
            Check(Field(FirstLine(grownBody), "char_id") == grown.Id,
                $"char_id={grown.Id}");
            Check(Field(FirstLine(grownBody), "session_id") == SidGrown, "session_id");
            Check(grownBody.IndexOf("삭제됨", StringComparison.Ordinal) < 0,
                "로그 본문에 Cap 문구 없음");
            int beforeDup = CountLines(grownBody);
            V4LoopLog.NotePermadeath(grown);
            string afterDup = File.ReadAllText(grownPath);
            Check(CountLines(afterDup) == beforeDup, "같은 캐릭 permadeath는 한 줄");

            // ── 다섯 event + 사유 닫힘 + 잘못된 사유 거부 ──
            WipeSession(SidEvents);
            V4LoopLog.ResetForTest();
            V4LoopLog.ForceSessionIdForTest(SidEvents);
            LifeSystem.NowUnix = () => t0 + V4LoopLog.GrowthGuardSeconds;
            V4LoopLog.NoteRebuildOffer(grown);
            V4LoopLog.NoteRebuildAccept(grown, "");
            V4LoopLog.NoteRebuildDecline(grown, "손실 분노");
            V4LoopLog.NoteSessionEnd(grown, "기술 문제");
            V4LoopLog.NoteRebuildDecline(grown, "기타");
            string evPath = V4LoopLog.CurrentPath;
            string evBody = File.Exists(evPath) ? File.ReadAllText(evPath) : "";
            Check(evBody.IndexOf("\"event\":\"rebuild_offer\"", StringComparison.Ordinal) >= 0,
                "rebuild_offer");
            Check(evBody.IndexOf("\"event\":\"rebuild_accept\"", StringComparison.Ordinal) >= 0,
                "rebuild_accept");
            Check(evBody.IndexOf("\"event\":\"rebuild_decline\"", StringComparison.Ordinal) >= 0,
                "rebuild_decline");
            Check(evBody.IndexOf("\"event\":\"session_end\"", StringComparison.Ordinal) >= 0,
                "session_end");
            Check(evBody.IndexOf("손실 분노", StringComparison.Ordinal) >= 0
                  && evBody.IndexOf("기술 문제", StringComparison.Ordinal) >= 0,
                "사유 enum이 로그에 남는다");
            Check(evBody.IndexOf("기타", StringComparison.Ordinal) < 0,
                "닫히지 않은 사유는 안 쓴다");
            Check(CountLines(evBody) == 4, $"이벤트 4줄(거절 제외) 실제 {CountLines(evBody)}");

            // ── 긴급 재건이 rebuild_offer를 건다 ──
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            V4LoopLog.ResetForTest();
            LifeSystem.NowUnix = () => t0;
            V4LoopLog.ForceSessionIdForTest(SidEvents);
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                roster[i].GrowthStartUnix = t0 - V4LoopLog.GrowthGuardSeconds;
                roster[i].DeathCount = 2;
            }
            LifeSystem.PersistRoster();
            LifeSystem.NowUnix = () => t0;
            var wipe = LifeSystem.ApplyWipe(roster);
            Check(wipe.RescueGranted, "전멸이면 긴급 재건");
            string rescueBody = File.Exists(V4LoopLog.CurrentPath)
                ? File.ReadAllText(V4LoopLog.CurrentPath) : "";
            Check(rescueBody.IndexOf("\"event\":\"rebuild_offer\"", StringComparison.Ordinal) >= 0,
                "EnsureEmergencyRecruit이 rebuild_offer");
            Check(rescueBody.IndexOf("\"event\":\"permadeath\"", StringComparison.Ordinal) >= 0,
                "전멸 삭제 표본 permadeath");

            // ── QA_NO: 파일 없음 · 기존 줄 유지 · 삭제는 옛 동작 ──
            WipeSession(SidNo);
            V4LoopLog.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            LifeSystem.NowUnix = () => t0;
            V4LoopLog.ForceSessionIdForTest(SidNo);
            var seed = LifeSystem.GetCharacters()[0];
            seed.GrowthStartUnix = t0 - V4LoopLog.GrowthGuardSeconds;
            seed.DeathCount = 2;
            LifeSystem.NowUnix = () => t0;
            V4LoopLog.NoteRebuildOffer(seed);
            string noPath = V4LoopLog.CurrentPath;
            string beforeNo = File.ReadAllText(noPath);
            int beforeNoLines = CountLines(beforeNo);
            Environment.SetEnvironmentVariable(V4LoopLog.EnvNo, "1");
            Check(V4LoopLog.Blocked, "QA_NO 차단");
            V4LoopLog.NotePermadeath(seed);
            V4LoopLog.NoteRebuildAccept(seed, "난이도");
            V4LoopLog.NoteSessionEnd(seed, "조작 불명확");
            LifeSystem.RegisterDeath(seed);
            Check(seed.IsDeleted, "QA_NO여도 삭제는 된다(옛 동작)");
            Check(File.Exists(noPath) && File.ReadAllText(noPath) == beforeNo
                  && CountLines(File.ReadAllText(noPath)) == beforeNoLines,
                "QA_NO면 새 줄 없음");
            Environment.SetEnvironmentVariable(V4LoopLog.EnvNo, "1");
            V4LoopLog.ResetForTest();
            V4LoopLog.ForceSessionIdForTest("v4self_qano_empty");
            WipeSession("v4self_qano_empty");
            GameState.ResetAll();
            LifeSystem.ResetAll();
            LifeSystem.NowUnix = () => t0;
            var silent = LifeSystem.GetCharacters()[0];
            silent.GrowthStartUnix = t0 - V4LoopLog.GrowthGuardSeconds;
            silent.DeathCount = 2;
            LifeSystem.RegisterDeath(silent);
            Check(!File.Exists(V4LoopLog.CurrentPath),
                "QA_NO 빈 세션은 파일을 안 만든다");
            Environment.SetEnvironmentVariable(V4LoopLog.EnvNo, null);

            // ── 30분 경과 저장→로드: GrowthStartUnix 불변 · permadeath 1줄 ──
            WipeSession(SidRound);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            V4LoopLog.ResetForTest();
            LifeSystem.NowUnix = () => t0;
            V4LoopLog.ForceSessionIdForTest(SidRound);
            var rt = LifeSystem.GetCharacters()[0];
            long grownStart = t0 - V4LoopLog.GrowthGuardSeconds;
            rt.GrowthStartUnix = grownStart;
            rt.DeathCount = 2;
            LifeSystem.PersistRoster();
            string rtId = rt.Id;
            LifeSystem.ForgetInMemoryForTest();
            LifeSystem.NowUnix = () => t0;
            var rt2 = LifeSystem.GetCharacters()[0];
            Check(rt2.Id == rtId, "왕복 후 같은 캐릭");
            Check(rt2.GrowthStartUnix == grownStart,
                $"왕복 후 GrowthStartUnix 불변 {rt2.GrowthStartUnix}");
            Check(V4LoopLog.MeetsGrowthGuard(rt2), "왕복 후 가드 통과");
            LifeSystem.RegisterDeath(rt2);
            Check(rt2.IsDeleted, "왕복 후 삭제");
            string rtPath = V4LoopLog.CurrentPath;
            string rtBody = File.Exists(rtPath) ? File.ReadAllText(rtPath) : "";
            Check(CountLines(rtBody) == 1
                  && rtBody.IndexOf("\"event\":\"permadeath\"", StringComparison.Ordinal) >= 0,
                $"왕복 permadeath 1줄 (실제 {CountLines(rtBody)})");

            // ── 0 키 왕복: 표본 아님 · 가드 false · 로그 0줄 ──
            WipeSession(SidZero);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            V4LoopLog.ResetForTest();
            LifeSystem.NowUnix = () => t0;
            V4LoopLog.ForceSessionIdForTest(SidZero);
            var zero = LifeSystem.GetCharacters()[0];
            zero.GrowthStartUnix = 0;
            zero.DeathCount = 2;
            LifeSystem.PersistRoster();
            LifeSystem.ForgetInMemoryForTest();
            LifeSystem.NowUnix = () => t0 + V4LoopLog.GrowthGuardSeconds + 1;
            var zero2 = LifeSystem.GetCharacters()[0];
            Check(zero2.GrowthStartUnix == 0, $"0 키 왕복은 0 (실제 {zero2.GrowthStartUnix})");
            Check(!V4LoopLog.MeetsGrowthGuard(zero2), "0 키는 30분 후에도 가드 실패");
            LifeSystem.RegisterDeath(zero2);
            Check(zero2.IsDeleted, "0 키도 삭제는 된다");
            Check(!File.Exists(V4LoopLog.CurrentPath),
                "0 키는 표본 파일을 안 만든다");

            // ── 키 없는 옛 저장: 표본 아님 · 가드 false · 로그 0줄 ──
            WipeSession(SidOld);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            V4LoopLog.ResetForTest();
            LifeSystem.NowUnix = () => t0;
            V4LoopLog.ForceSessionIdForTest(SidOld);
            var oldc = LifeSystem.GetCharacters()[0];
            oldc.GrowthStartUnix = grownStart;
            oldc.DeathCount = 2;
            LifeSystem.PersistRoster();
            string rosterRaw = PlayerPrefs.GetString("ats.roster", "");
            PlayerPrefs.SetString("ats.roster", DropLastTabField(rosterRaw));
            PlayerPrefs.Save();
            LifeSystem.ForgetInMemoryForTest();
            LifeSystem.NowUnix = () => t0 + V4LoopLog.GrowthGuardSeconds + 1;
            var old2 = LifeSystem.GetCharacters()[0];
            Check(old2.GrowthStartUnix == 0, $"키 없는 옛 저장은 0 (실제 {old2.GrowthStartUnix})");
            Check(!V4LoopLog.MeetsGrowthGuard(old2), "옛 저장은 30분 후에도 가드 실패");
            LifeSystem.RegisterDeath(old2);
            Check(old2.IsDeleted, "옛 저장도 삭제는 된다");
            Check(!File.Exists(V4LoopLog.CurrentPath),
                "옛 저장은 표본 파일을 안 만든다");

            _ = nameof(V4LoopLog.NotePermadeath);
            _ = nameof(V4LoopLog.NoteRebuildOffer);
            _ = nameof(V4LoopLog.NoteRebuildAccept);
            _ = nameof(V4LoopLog.NoteRebuildDecline);
            _ = nameof(V4LoopLog.NoteSessionEnd);
            _ = nameof(V4LoopLog.MeetsGrowthGuard);
            _ = nameof(Memorial.Stamp);

            Environment.SetEnvironmentVariable(V4LoopLog.EnvNo, no);
            Environment.SetEnvironmentVariable(V4LoopLog.EnvSession, sid);
            LifeSystem.NowUnix = prevNow;
            V4LoopLog.ResetForTest();
            Memorial.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "v4_loop_boundary_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS V4LoopBoundarySelfCheck" : "FAIL V4LoopBoundarySelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[V4LoopBoundarySelfCheck] PASS → " + path + "\n" + _log);
            else Debug.LogError($"[V4LoopBoundarySelfCheck] FAIL {_fail}건 → " + path + "\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[V4LoopBoundarySelfCheck] FAIL {_fail}건");
        }

        static string DropLastTabField(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder();
            foreach (string line in raw.Split('\n'))
            {
                if (string.IsNullOrEmpty(line)) continue;
                int tab = line.LastIndexOf('\t');
                if (tab >= 0) sb.Append(line.Substring(0, tab));
                else sb.Append(line);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static void WipeSession(string id)
        {
            string dir = V4LoopLog.LogDir;
            if (string.IsNullOrEmpty(dir)) return;
            string path = Path.Combine(dir, "session_" + id + ".log");
            if (File.Exists(path)) File.Delete(path);
        }

        static string FirstLine(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            int n = raw.IndexOf('\n');
            return n < 0 ? raw.Trim() : raw.Substring(0, n).Trim();
        }

        static int CountLines(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            int n = 0;
            using (var sr = new StringReader(raw))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                    if (line.Length > 0) n++;
            }
            return n;
        }

        static bool HasMinFields(string line)
        {
            return line.IndexOf("\"session_id\":", StringComparison.Ordinal) >= 0
                && line.IndexOf("\"build\":", StringComparison.Ordinal) >= 0
                && line.IndexOf("\"t_utc\":", StringComparison.Ordinal) >= 0
                && line.IndexOf("\"char_id\":", StringComparison.Ordinal) >= 0
                && line.IndexOf("\"event\":", StringComparison.Ordinal) >= 0
                && line.IndexOf("\"reason_enum\":", StringComparison.Ordinal) >= 0;
        }

        static string Field(string line, string key)
        {
            string pat = "\"" + key + "\":\"";
            int i = line.IndexOf(pat, StringComparison.Ordinal);
            if (i < 0) return "";
            int start = i + pat.Length;
            int end = line.IndexOf('"', start);
            return end < 0 ? "" : line.Substring(start, end - start);
        }
    }
}
