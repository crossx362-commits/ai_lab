using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **행동 효과(불티·소리)가 실제로 그 사람 화면·귀에 닿는가**(검수 랩 D, 2026-09-07).
    ///
    /// 사각지대 표 ④의 나머지 절반이다. 효과를 「불렀다」는 것과 「닿았다」는 것은 다르다:
    /// ① 매달린 조건 — `if (성공)` 아래에 중괄호 없이 두 줄을 쓰면 **둘째 줄은 조건 밖**이다.
    ///    빗나가도 소리가 난다(소리가 결과를 거짓말한다).
    /// ② 관측자 배선 — `[ServerRpc]` 본체에서 효과를 재생하면 **서버 인스턴스에서만** 난다.
    ///    행동한 본인도, 옆에 선 사람도 못 본다. 방송하려면 `[ObserversRpc]`를 거쳐야 한다.
    ///
    /// 런타임이 아니라 소스를 본다 — `AssertServerAuthorityWiring`과 같은 이유다(온라인 경로는
    /// 배치모드에서 재생할 수 없고, 오프라인 경로만 재생하면 이 결함이 통과한다).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        const string EffectVfxCall = "ActionVfx.Play(";
        const string EffectSfxCall = "ActionSfx.Play(";

        static bool IsEffectCall(string line)
        {
            return line.Contains(EffectVfxCall) || line.Contains(EffectSfxCall);
        }

        /// <summary>
        /// 한 파일의 배선 결함 목록. **게이트와 네거티브 컨트롤이 같은 함수를 쓴다** —
        /// NC는 결함이 든 소스 문자열을 이 함수에 그대로 먹인다.
        ///
        /// ① 매달린 조건은 **효과 호출에 한정하지 않는다**(검수 지시 2026-09-07) — 이 형태가 실제로
        ///    결함 5건을 낳았고, 골드 지급·아이템 소비에 같은 형태가 생기면 그건 소리가 아니라
        ///    **게임 규칙이 거짓말하는 것**이다. 그래서 모든 문장에 대해 본다.
        /// </summary>
        static List<string> EffectWiringDefects(string fileName, string[] lines)
        {
            var defects = new List<string>(DanglingIfDefects(fileName, lines));
            string lastRpcAttribute = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("[ServerRpc", StringComparison.Ordinal))
                    lastRpcAttribute = "ServerRpc";
                else if (t.StartsWith("[ObserversRpc", StringComparison.Ordinal))
                    lastRpcAttribute = "ObserversRpc";
                if (!IsEffectCall(t))
                    continue;

                // ② 관측자 배선 — NetAvatar의 효과는 방송을 거쳐야 한다.
                if (fileName == "NetAvatar.cs" && lastRpcAttribute == "ServerRpc")
                    defects.Add(fileName + ":" + (i + 1) + " 서버에서만 재생 — `[ServerRpc]` 본체라 다른 사람 화면·귀에는 아무것도 안 간다(`[ObserversRpc]`로 방송할 것): " + t);
            }
            return defects;
        }

        /// <summary>
        /// **중괄호 없는 `if` 아래 둘째 문장은 조건 밖이다.** 들여쓰기가 같아 눈으로는 안 보인다.
        ///
        /// 판단을 **보류하는 형태**(한계, 다음 사람이 「전부 검사된다」고 읽지 않게):
        /// 여러 줄에 걸친 조건식, 본문이 한 문장으로 안 끝나는 경우, 한 줄에 `{ … }`로 닫은 if.
        /// 셋 다 육안으로도 매달림이 아니다.
        /// </summary>
        static List<string> DanglingIfDefects(string fileName, string[] lines)
        {
            var defects = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string t = StripComment(lines[i]);
                if (!t.StartsWith("if (", StringComparison.Ordinal) && !t.StartsWith("else if (", StringComparison.Ordinal))
                    continue;
                if (CountChar(t, '(') != CountChar(t, ')') || !t.EndsWith(")", StringComparison.Ordinal))
                    continue;                                   // 여러 줄 조건 / 같은 줄에 본문이 있음
                int b = NextCode(lines, i);
                if (b < 0)
                    continue;
                string bt = StripComment(lines[b]);
                if (bt.StartsWith("{", StringComparison.Ordinal) || !bt.EndsWith(";", StringComparison.Ordinal))
                    continue;                                   // 중괄호 블록 / 한 문장으로 안 끝남
                int n = NextCode(lines, b);
                if (n < 0)
                    continue;
                string nt = StripComment(lines[n]);
                if (Indent(lines[n]) != Indent(lines[b]))
                    continue;                                   // 들여쓰기가 다르면 형제 문장이 아니다
                if (nt.StartsWith("else", StringComparison.Ordinal) || nt.StartsWith("}", StringComparison.Ordinal)
                    || nt.StartsWith("{", StringComparison.Ordinal) || nt.StartsWith("catch", StringComparison.Ordinal)
                    || nt.StartsWith("finally", StringComparison.Ordinal))
                    continue;
                defects.Add(fileName + ":" + (n + 1) + " 매달린 조건 — 위 `if`에 중괄호가 없어 이 줄은 **조건 밖**에서 항상 실행된다: " + nt);
            }
            return defects;
        }

        static string StripComment(string line)
        {
            int i = line.IndexOf("//", StringComparison.Ordinal);
            return (i >= 0 ? line.Substring(0, i) : line).Trim();
        }

        static int CountChar(string s, char c)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == c) n++;
            return n;
        }

        static int Indent(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            return i;
        }

        static int NextCode(string[] lines, int i)
        {
            for (int k = i + 1; k < lines.Length; k++)
                if (StripComment(lines[k]).Length > 0)
                    return k;
            return -1;
        }

        static void AssertEffectWiring()
        {
            string clientDir = Path.Combine(Application.dataPath, "Game/Scripts/Client");
            if (!Directory.Exists(clientDir))
                throw new InvalidOperationException("클라 스크립트 폴더가 없습니다: " + clientDir);
            // 매달린 조건은 클라 밖에서도 결함이다 — 골드·아이템을 다루는 서버·공용까지 본다.
            var dirs = new List<string> { clientDir };
            foreach (string sub in new[] { "Game/Scripts/Server", "Game/Scripts/Shared" })
            {
                string d = Path.Combine(Application.dataPath, sub);
                if (!Directory.Exists(d))
                    throw new InvalidOperationException("스크립트 폴더가 없습니다: " + d);
                dirs.Add(d);
            }

            var defects = new List<string>();
            int calls = 0;
            int broadcast = 0;
            int scanned = 0;
            var files = new List<string>();
            for (int d = 0; d < dirs.Count; d++)
                files.AddRange(Directory.GetFiles(dirs[d], "*.cs", SearchOption.AllDirectories));
            foreach (string file in files)
            {
                scanned++;
                string name = Path.GetFileName(file);
                if (name == "ActionVfx.cs" || name == "ActionSfx.cs")
                    continue;   // 효과 구현 자체 — 여기 있는 `Play`는 호출부가 아니다.
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (IsEffectCall(lines[i].Trim()))
                        calls++;
                if (name == "NetAvatar.cs")
                {
                    string attr = "";
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string t = lines[i].Trim();
                        if (t.StartsWith("[ObserversRpc", StringComparison.Ordinal)) attr = "ObserversRpc";
                        else if (t.StartsWith("[ServerRpc", StringComparison.Ordinal)) attr = "ServerRpc";
                        if (attr == "ObserversRpc" && IsEffectCall(t))
                            broadcast++;
                    }
                }
                defects.AddRange(EffectWiringDefects(name, lines));
            }

            // 실패 사유를 **한꺼번에** 던진다 — 하나씩 던지면 첫 사유에서 멈춰 나머지 결함이 안 보인다.
            var reasons = new List<string>(defects);
            if (calls == 0)
                reasons.Add("효과 호출을 한 개도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            if (scanned == 0)
                reasons.Add("소스 파일을 한 개도 못 읽었습니다 — 잰 것이 없습니다(0이면 실패).");
            if (broadcast == 0)
                reasons.Add("NetAvatar에 방송되는 효과가 없습니다 — 온라인에서는 아무 불티도 소리도 안 납니다(0이면 실패).");
            if (reasons.Count > 0)
                throw new InvalidOperationException("행동 효과 배선 결함 " + reasons.Count + "건:\n  " + string.Join("\n  ", reasons));

            Debug.Log("[Ulon] 행동 효과 배선 — 소스 " + scanned + "개 훑어 매달린 조건 0건, 효과 호출 " + calls +
                      "곳 전부 조건 안, NetAvatar 방송 재생 " + broadcast + "곳");
        }

        /// <summary>
        /// 네거티브 컨트롤 — **결함이 든 소스를 같은 분석기에 먹여** 두 종류를 다 잡는지 본다.
        /// 실제 파일을 망가뜨리지 않으므로 실행 순서에 상관없이 안전하다.
        /// </summary>
        static void AssertEffectWiringNegativeControl()
        {
            string[] dangling =
            {
                "            var result = Attack();",
                "            if (result.Applied)",
                "                ActionVfx.Play(ActionVfx.Kind.Hit, at);",
                "                ActionSfx.Play(ActionSfx.Kind.Hit, at);",
            };
            var d1 = EffectWiringDefects("Fake.cs", dangling);
            if (d1.Count == 0)
                throw new InvalidOperationException("효과 배선 네거티브 컨트롤 실패 — 매달린 조건을 못 잡았습니다.");

            // **효과 밖에서도 잡는가** — 골드 지급 같은 규칙 코드에 같은 형태가 생기면 그건 소리가 아니라
            // 게임 규칙이 거짓말하는 것이다(검수 지시 2026-09-07).
            string[] danglingRule =
            {
                "            if (order.Applied)",
                "                bag.AddGold(10);",
                "                ConsumeItem(bag, order.Item, 1);",
            };
            var d3 = EffectWiringDefects("Fake.cs", danglingRule);
            if (d3.Count == 0)
                throw new InvalidOperationException("효과 배선 네거티브 컨트롤 실패 — 효과 호출이 아닌 매달린 조건(골드·아이템)을 못 잡았습니다.");

            string[] serverOnly =
            {
                "        [ServerRpc]",
                "        public void RpcRequestAttack()",
                "        {",
                "            ActionSfx.Play(ActionSfx.Kind.Hit, at);",
                "        }",
            };
            var d2 = EffectWiringDefects("NetAvatar.cs", serverOnly);
            if (d2.Count == 0)
                throw new InvalidOperationException("효과 배선 네거티브 컨트롤 실패 — 서버에서만 재생하는 효과를 못 잡았습니다.");

            Debug.Log("[Ulon] 효과 배선 네거티브 컨트롤 — 매달린 조건(효과/규칙)·서버 전용 재생 셋 다 검출: " + d3[0] + " | " + d1[0] + " | " + d2[0]);
        }
    }
}
