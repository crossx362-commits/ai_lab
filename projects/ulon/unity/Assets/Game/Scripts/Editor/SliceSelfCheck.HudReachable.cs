using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **「클라 코드가 부른다」와 「화면에 그 기능을 여는 수단이 있다」는 다르다**(검수 랩 A, 2026-09-07).
    /// `AssertReachableFeatures`는 호출 그래프만 봐서, 버튼만 사라지고 호출부가 남아도 통과한다 —
    /// HUD를 통째로 갈아엎는 지금 가장 비싼 사각지대였다.
    ///
    /// 이 게이트는 두 쪽에서 잰다.
    ///   ① 소스: `SliceHud.cs`가 부르는 모든 `Try*`/`Rpc*`가 **버튼(`Btn("라벨")`) 핸들러에서 도달**하는가.
    ///      (버튼 → 도우미 메서드 → Try*의 전이를 따라간다. 도달 못 하면 화면에 여는 수단이 없다는 뜻이다.)
    ///   ② 실행: 핵심 기능 라벨이 **실제로 화면에 그려진 적이 있는가** — 증거는 `docs/hud_controls.txt`
    ///      (`bash tools/hud_shots.sh`가 실행 중 게임에서 기록한다).
    ///
    /// 한계(먼저 밝힌다): ②는 **핵심 라벨 원장**만 요구한다. 소스 라벨 전수를 요구하면 상태가 있어야
    /// 그려지는 버튼(유령일 때 「붕대 부활」, 결투 중 「항복」 등)이 매번 실패한다. 원장에 없는 버튼이
    /// 사라지는 것은 이 게이트가 아니라 검수가 샷으로 잡는다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>화면에서 **반드시** 그려져야 하는 조작 수단 — 없어지면 그 기능은 도달 불가다.</summary>
        static readonly string[] HudRequiredControls =
        {
            "가방", "행동", "스킬", "소셜",              // 패널을 여는 탭(근처는 「▸근처◂」로 상태 표시가 붙는다)
            "착용", "장비 해제", "주머니↓", "주머니↑",   // 가방
            "만들기", "수리",                            // 제작대
            "사기", "팔기", "상점 닫기",                 // 상점
            "따기",                                      // 잠긴 궤짝
            "붕대", "물약",                              // 퀵바
        };

        /// <summary>버튼 핸들러에서 도달하지 않아도 되는 호출과 그 이유.</summary>
        static readonly Dictionary<string, string> HudControlExempt = new Dictionary<string, string>
        {
            { "TryMove", "이동은 버튼이 아니라 키·마우스 입력이다" },
        };

        static void AssertHudControlsOnScreen()
        {
            string[] lines = HudSourceLines();

            var methodCalls = HudMethodCalls(lines);
            var labels = HudLabelHandlers(lines);
            if (labels.Count == 0)
                throw new InvalidOperationException("SliceHud에서 버튼 라벨을 하나도 찾지 못했습니다 — " +
                    "버튼은 전부 Btn(\"라벨\")을 거쳐야 합니다(GUILayout.Button 직접 호출은 게이트 눈 밖입니다).");

            // 버튼에서 전이로 도달하는 기능.
            var reachable = new HashSet<string>();
            foreach (var kv in labels)
                Expand(kv.Value, methodCalls, reachable);

            // SliceHud가 실제로 부르는 기능 전량.
            var called = new HashSet<string>();
            var featureRe = new Regex(@"(?<![\w])((?:Try|Rpc)\w+)\s*\(");
            for (int i = 0; i < lines.Length; i++)
                foreach (Match m in featureRe.Matches(lines[i]))
                    called.Add(m.Groups[1].Value);
            called.Remove("TryGetValue");
            if (called.Count == 0)
                throw new InvalidOperationException("SliceHud에서 Try*/Rpc* 호출을 하나도 찾지 못했습니다 — 잰 것이 없습니다.");

            var orphan = new List<string>();
            foreach (string f in called)
                if (!reachable.Contains(f) && !HudControlExempt.ContainsKey(f))
                    orphan.Add(f);
            orphan.Sort();
            if (orphan.Count > 0)
                throw new InvalidOperationException("화면에 여는 수단이 없는 기능 " + orphan.Count + "건: " +
                    string.Join(", ", orphan) + " — SliceHud가 호출은 하지만 **어떤 버튼에서도 도달하지 않습니다**. " +
                    "버튼을 배선하거나, 입력이 버튼이 아니면 HudControlExempt에 이유를 적으세요.");

            Debug.Log("[Ulon] 화면 조작 수단(소스) 통과 — 버튼 " + labels.Count + "종에서 기능 " + called.Count +
                      "종 전부 도달(허용목록 " + HudControlExempt.Count + "건)");

            // ② 실행 증거 — 그 버튼이 화면에 실제로 그려진 적이 있는가.
            var drawn = HudDrawnControls();
            AssertRequiredControlsDrawn(drawn, lines);
            Debug.Log("[Ulon] 화면 조작 수단(실행) 통과 — 기록된 버튼 " + drawn.Count + "종에 핵심 " +
                      HudRequiredControls.Length + "종이 모두 들어 있음 · 기록 " + HudControlsHeader);
        }

        static void AssertRequiredControlsDrawn(HashSet<string> drawn, string[] hudLines)
        {
            if (drawn.Count == 0)
                throw new InvalidOperationException("그려진 버튼 기록이 비었습니다 — 잰 것이 없습니다(0이면 실패).");
            string src = string.Join("\n", hudLines);
            var missing = new List<string>();
            for (int i = 0; i < HudRequiredControls.Length; i++)
            {
                string want = HudRequiredControls[i];
                if (src.IndexOf("\"" + want + "\"", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("핵심 라벨 「" + want + "」이 SliceHud 소스에 없습니다 — " +
                        "라벨을 바꿨으면 HudRequiredControls 원장도 같이 고치세요(이름만 바뀐 건지 기능이 사라진 건지 구분해야 합니다).");
                bool found = false;
                foreach (string d in drawn)
                    if (d.IndexOf(want, StringComparison.Ordinal) >= 0) { found = true; break; }
                if (!found)
                    missing.Add(want);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("화면에 한 번도 그려지지 않은 핵심 버튼 " + missing.Count + "건: " +
                    string.Join(", ", missing) + " — 코드에는 있지만 실행 중 화면에 안 나왔습니다. " +
                    "HUD를 고쳤다면 `bash tools/hud_shots.sh`로 기록을 갱신하세요(기록이 낡아도 이 줄이 뜹니다).");
        }

        static string HudControlsHeader = "";

        static HashSet<string> HudDrawnControls()
        {
            // 기록은 `docs/hud_controls.txt`(git 추적)를 본다 — `builds/`는 무시 경로라 새로 받은 저장소에서
            // 게이트가 증거 없이 빨간불이 된다. `tools/hud_shots.sh`가 실행 뒤 여기로 복사한다.
            string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", ".."));
            string path = Path.Combine(root, "docs/hud_controls.txt");
            if (!File.Exists(path))
                throw new InvalidOperationException("그려진 버튼 기록(" + path + ")이 없습니다 — " +
                    "`bash tools/hud_shots.sh`를 한 번 돌려 실행 중 화면의 조작 수단을 기록하세요.");
            var set = new HashSet<string>();
            HudControlsHeader = "";
            foreach (string line in File.ReadAllLines(path))
            {
                string s = line.Trim();
                if (s.StartsWith("#", StringComparison.Ordinal))
                {
                    HudControlsHeader = s.TrimStart('#').Trim();   // 언제·어느 커밋의 기록인지
                    continue;
                }
                if (s.Length > 0)
                    set.Add(s);
            }
            if (HudControlsHeader.Length == 0)
                throw new InvalidOperationException("기록(" + path + ") 첫 줄에 머리말(생성 시각·HEAD)이 없습니다 — " +
                    "언제 것인지 모르는 기록은 증거가 아닙니다. `bash tools/hud_shots.sh`로 다시 만드세요(손으로 쓰지 마세요).");
            return set;
        }

        /// <summary>메서드 이름 → 그 안에서 부르는 이름들.</summary>
        static Dictionary<string, HashSet<string>> HudMethodCalls(string[] lines)
        {
            var map = new Dictionary<string, HashSet<string>>();
            var declRe = new Regex(@"^\s*(?:public|private|internal|protected|static|void|bool|int|float|string|\w)[\w\s<>,\[\]\?]*\s(\w+)\s*\([^;]*\)\s*$");
            var callRe = new Regex(@"(?<![\w])(\w+)\s*\(");
            string current = null;
            int depth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (current == null)
                {
                    var d = declRe.Match(line);
                    if (d.Success && !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        current = d.Groups[1].Value;
                        if (!map.ContainsKey(current))
                            map[current] = new HashSet<string>();
                        depth = 0;
                    }
                    continue;
                }
                foreach (Match c in callRe.Matches(line))
                    map[current].Add(c.Groups[1].Value);
                depth += Count(line, '{') - Count(line, '}');
                if (depth <= 0 && line.IndexOf('}') >= 0)
                    current = null;
            }
            return map;
        }

        /// <summary>
        /// 버튼 줄 → 그 버튼을 눌렀을 때 도는 이름들. **버튼 줄**은 `Btn(`(단일 버튼)이나 `Row3(`(버튼 3칸 줄)이
        /// 있는 줄이다. 라벨이 문자열이 아니라 식인 버튼(「나가기/들어가기」, 스킬 이름)도 있으므로
        /// 키는 라벨 문자열이 있으면 그것, 없으면 `(식) 줄번호`로 잡는다 — 판정은 **핸들러 도달**로 하니
        /// 라벨을 못 읽는다고 기능이 눈 밖으로 나가지는 않는다.
        /// </summary>
        static Dictionary<string, HashSet<string>> HudLabelHandlers(string[] lines)
        {
            var labels = new Dictionary<string, HashSet<string>>();
            var litRe = new Regex(@"""([^""]+)""");
            var callRe = new Regex(@"(?<![\w])(\w+)\s*\(");
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.IndexOf("Btn(", StringComparison.Ordinal) < 0 && line.IndexOf("Row3(", StringComparison.Ordinal) < 0)
                    continue;
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) ||
                    line.IndexOf("static bool Btn", StringComparison.Ordinal) >= 0)
                    continue;

                var lits = litRe.Matches(line);
                string key = lits.Count > 0 ? lits[0].Groups[1].Value : "(식) " + (i + 1);
                if (!labels.ContainsKey(key))
                    labels[key] = new HashSet<string>();
                var body = labels[key];
                for (int l = 1; l < lits.Count; l++)          // 한 줄에 라벨이 여럿이면(Row3) 다 등록한다
                {
                    string extra = lits[l].Groups[1].Value;
                    if (!labels.ContainsKey(extra))
                        labels[extra] = new HashSet<string>();
                }

                // 같은 줄의 호출(한 줄짜리 핸들러·람다)과, 뒤따르는 블록(여러 줄 핸들러).
                foreach (Match c in callRe.Matches(line))
                    body.Add(c.Groups[1].Value);
                int j = i + 1;
                while (j < lines.Length && lines[j].Trim().Length == 0)
                    j++;
                // 핸들러가 다음 줄에 한 문장으로 있는 꼴: `if (cond && Btn(...))` ⏎ `Handler(net, x);`
                if (j < lines.Length && !line.TrimEnd().EndsWith(";", StringComparison.Ordinal) && lines[j].Trim() != "{")
                    foreach (Match c in callRe.Matches(lines[j]))
                        body.Add(c.Groups[1].Value);
                if (j < lines.Length && lines[j].Trim() == "{")
                {
                    int depth = 0;
                    for (int k = j; k < lines.Length; k++)
                    {
                        foreach (Match c in callRe.Matches(lines[k]))
                            body.Add(c.Groups[1].Value);
                        depth += Count(lines[k], '{') - Count(lines[k], '}');
                        if (depth <= 0 && k > j)
                            break;
                    }
                }
            }
            return labels;
        }

        static void Expand(HashSet<string> seeds, Dictionary<string, HashSet<string>> methodCalls, HashSet<string> into)
        {
            var stack = new Stack<string>(seeds);
            var seen = new HashSet<string>();
            while (stack.Count > 0)
            {
                string name = stack.Pop();
                if (!seen.Add(name))
                    continue;
                into.Add(name);
                if (methodCalls.TryGetValue(name, out HashSet<string> next))
                    foreach (string n in next)
                        stack.Push(n);
            }
        }

        static int Count(string s, char c)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == c) n++;
            return n;
        }

        /// <summary>
        /// 네거티브 컨트롤 — ① 핵심 라벨이 기록에서 빠지면 빨간불인가 ② 버튼과 이어지지 않은 기능을 잡는가.
        /// 「0%」가 잘 된 건지 못 잰 건지는 빨간불을 나란히 봐야 안다(검수).
        /// </summary>
        static void AssertHudControlsNegativeControl()
        {
            var drawn = HudDrawnControls();
            string[] lines = HudSourceLines();

            // ① 기록에서 「수리」를 빼 본다 — 버튼이 화면에서 사라진 상태와 같다.
            var doctored = new HashSet<string>(drawn);
            doctored.RemoveWhere(s => s.IndexOf("수리", StringComparison.Ordinal) >= 0);
            bool red = false;
            try { AssertRequiredControlsDrawn(doctored, lines); }
            catch (InvalidOperationException) { red = true; }
            if (!red)
                throw new InvalidOperationException("화면 조작 수단 네거티브 컨트롤 실패 — 「수리」 버튼이 기록에서 사라졌는데 통과했습니다.");

            // ② 기록이 비면 빨간불이어야 한다(빈 통과 금지).
            bool redEmpty = false;
            try { AssertRequiredControlsDrawn(new HashSet<string>(), lines); }
            catch (InvalidOperationException) { redEmpty = true; }
            if (!redEmpty)
                throw new InvalidOperationException("화면 조작 수단 네거티브 컨트롤 실패 — 기록이 비었는데 통과했습니다(0이면 실패가 안 걸렸습니다).");

            Debug.Log("[Ulon] 화면 조작 수단 네거티브 컨트롤 — 「수리」 제거·빈 기록 둘 다 빨간불");
        }
    }
}
