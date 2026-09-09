using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **같은 게이트가 두 번 배선돼 있지 않은가**(검수 지시 2026-09-08, 병행 개발 중).
    ///
    /// 발단: 세션 둘이 각자 같은 게이트(`AssertBarehandVillagers`)를 만들어 `SliceSelfCheck.cs`에
    /// **호출이 두 줄** 들어갔다. 검사는 통과했지만 — 두 번 도는 것은 결과를 바꾸지 않으니까 —
    /// **다음 사람은 어느 쪽을 고쳐야 할지 모른다.** 병행 개발이 계속되는 한 이건 또 난다.
    /// 사람 눈으로 막을 일이 아니라 **자로 막을 일이다**(검수).
    ///
    /// 재는 것: 실행 목록 파일(`SliceSelfCheck.cs`)에서 `AssertXxx(...)` 호출이 이름별로 몇 번인가.
    /// **정의부와 NC 본체는 다른 파일에 있으므로** 여기서 세는 것은 「배선」뿐이다
    /// (NC가 자기 안에서 본 게이트를 부르는 것은 배선이 아니라 구현이다).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **배선이 사는 파일들** — 본체(`SliceSelfCheck.cs`)와 자 배선판(`SliceSelfCheck.Gates.cs`).
        ///
        /// 랩 ㉪에서 배선 217줄이 `Gates.cs`로 옮겨 갔는데 이 자는 여전히 본체만 읽고 있었다 —
        /// **세 줄만 보고 「중복 없음」이라 답하고 있었다.** 빨간불도 빈 통과도 아닌 **거의 빈 통과**다
        /// (랩 ㉰에서 셈으로 발견). 파일 이름 하나에 묶인 자의 전형이다.
        /// **여기 없는 파일은 배선이 아니다** — 구현부·NC 본체가 자기 게이트를 부르는 것까지 세면
        /// 중복 배선이 아니라 오탐이다(그래서 폴더 전체를 읽지 않는다).
        /// 배선을 새 파일로 또 옮기면 **이 목록에 그 파일을 더해라.**
        /// </summary>
        static string[] RunListPaths()
        {
            string dir = Path.Combine(Application.dataPath, "Game/Scripts/Editor");
            string[] paths = { Path.Combine(dir, "SliceSelfCheck.cs"), Path.Combine(dir, "SliceSelfCheck.Gates.cs") };
            for (int i = 0; i < paths.Length; i++)
                if (!File.Exists(paths[i]))
                    throw new InvalidOperationException("실행 목록 파일을 못 찾았습니다: " + paths[i] + " — 못 읽은 것을 통과로 적지 않는다.");
            return paths;
        }

        static string RunListSource()
        {
            var sb = new System.Text.StringBuilder();
            foreach (string p in RunListPaths())
                sb.Append(File.ReadAllText(p)).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// 배선 횟수를 센다 — 주석 줄은 빼고(주석 속 예시가 배선으로 세이면 안 된다),
        /// **인자 없는 호출만**. 실측에서 `AssertHuntSpot("졸병", …)`처럼 **대상마다 부르는 도우미**가
        /// 2회로 걸렸다 — 그건 중복 배선이 아니라 원래 여러 번 부르는 함수다.
        /// 배선은 인자 없는 `AssertXxx();` 한 줄이라는 이 저장소의 관행을 자로 삼는다.
        /// </summary>
        internal static Dictionary<string, int> WiringCounts(string source)
        {
            var counts = new Dictionary<string, int>();
            // 배선은 두 모양이다: 본체의 `AssertXxx();`와 배선판의 `Gate(AssertXxx);`.
            var call = new Regex(@"^\s*(?:Gate\(\s*)?(Assert\w+)\s*(?:\)\s*;|\(\s*\)\s*;)");
            var lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart();
                if (line.StartsWith("//") || line.StartsWith("///"))
                    continue;
                var m = call.Match(lines[i]);
                if (!m.Success)
                    continue;
                string name = m.Groups[1].Value;
                counts.TryGetValue(name, out int n);
                counts[name] = n + 1;
            }
            return counts;
        }

        static List<string> DoubleWired(string source)
        {
            var bad = new List<string>();
            foreach (var kv in WiringCounts(source))
                if (kv.Value > 1)
                    bad.Add(kv.Key + " " + kv.Value + "회");
            return bad;
        }

        static void AssertNoDoubleWiredGates()
        {
            string source = RunListSource();
            var counts = WiringCounts(source);
            if (counts.Count == 0)
                throw new InvalidOperationException("배선을 하나도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var bad = DoubleWired(source);
            Debug.Log("[Ulon] 게이트 배선 — 서로 다른 게이트 " + counts.Count + "개, 두 번 이상 불리는 것 " + bad.Count + "개" +
                      (bad.Count > 0 ? ": " + string.Join(", ", bad) : ""));
            if (bad.Count > 0)
                throw new InvalidOperationException("같은 게이트가 여러 번 배선돼 있습니다: " + string.Join(", ", bad) +
                    " — 두 번 돌아도 결과는 같아 조용히 지나가지만, 다음 사람은 어느 쪽을 고칠지 모릅니다. " +
                    "한 줄만 남기십시오(`SliceSelfCheck.cs`).");
        }

        /// <summary>
        /// NC — **실제로 한 줄을 복사해 두 번 배선하면** 빨간불이어야 한다.
        /// 파일을 고치지 않는다: 판정이 **본문 문자열의 함수**이므로, 복사한 본문을 같은 자로 잰다
        /// (계측이 세계를 바꾸면 안 된다 — 저장소 파일을 고쳤다가 되돌리는 방식은 쓰지 않는다).
        /// </summary>
        static void AssertNoDoubleWiredGatesNegativeControl()
        {
            string source = RunListSource();
            var counts = WiringCounts(source);
            string victim = null;
            foreach (var kv in counts)
                if (kv.Value == 1) { victim = kv.Key; break; }
            if (victim == null)
                throw new InvalidOperationException("배선 NC 대상이 없습니다 — 한 번만 불리는 게이트가 없습니다(0이면 실패).");
            // **두 모양 다 시험한다** — 본체의 `AssertXxx();`와 배선판의 `Gate(AssertXxx);`.
            // 한 모양만 재면 다른 모양으로 들어온 중복 배선을 놓친다(랩 ㉰: 자가 세 줄만 보고 있었다).
            string[] shapes = { "\n            " + victim + "();\n", "\n            Gate(" + victim + ");\n" };
            for (int s = 0; s < shapes.Length; s++)
                if (DoubleWired(source + shapes[s]).Count == 0)
                    throw new InvalidOperationException("게이트 배선 네거티브 컨트롤 실패 — " + victim +
                        " 호출을" + (s == 0 ? " " : " `Gate(...)` 모양으로 ") + "한 줄 더 넣었는데 통과했습니다.");
            Debug.Log("[Ulon] 게이트 배선 네거티브 컨트롤 통과 — " + victim + " 호출을 한 줄 더 넣으면 FAIL");
        }
    }
}
