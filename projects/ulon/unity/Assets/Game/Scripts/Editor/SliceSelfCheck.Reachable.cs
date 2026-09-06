using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **셀프체크가 부르니까 통과하지만, 게임에서는 아무도 못 부르는 기능**을 잡는다.
        /// Assert는 OfflineWorld를 직접 호출한다 — 그래서 UI·키 배선이 통째로 없어도 PASS가 난다.
        /// 실제로 착용/해제·주머니 넣기/꺼내기·펫 놓아주기가 그 상태였다(2026-09-06 스캔).
        ///
        /// 판정: Server의 public Try* 는 (a) Client 코드가 부르거나, (b) 그런 Try*가 부르는 것(전이)이거나,
        /// (c) 이유를 적은 허용목록(서버 내부 로직)이어야 한다.
        /// </summary>
        static readonly Dictionary<string, string> ReachableExempt = new Dictionary<string, string>
        {
            { "TryEnemyStrike", "몹 AI가 서버 틱에서 부른다 — 플레이어 입력이 아니다" },
        };

        static void AssertReachableFeatures()
        {
            string scripts = Path.Combine(Application.dataPath, "Game/Scripts");
            string serverDir = Path.Combine(scripts, "Server");
            string clientDir = Path.Combine(scripts, "Client");
            if (!Directory.Exists(serverDir) || !Directory.Exists(clientDir))
                throw new InvalidOperationException("Server/Client 스크립트 폴더가 없습니다.");

            var declRe = new Regex(@"public\s+[\w<>\[\]\?]+\s+(Try\w+)\s*\(");
            // 앞에 점이 와도 잡아야 한다 — `OfflineWorld.Instance.TryHeal(`이 실제 호출 형태다.
            var callRe = new Regex(@"(?<![\w])(Try\w+)\s*\(");

            // 서버: 선언과, 그 선언 본문 안에서 부르는 Try* 목록.
            var declared = new List<string>();
            var calls = new Dictionary<string, HashSet<string>>();
            foreach (string file in Directory.GetFiles(serverDir, "*.cs", SearchOption.AllDirectories))
            {
                string current = null;
                foreach (string line in File.ReadAllLines(file))
                {
                    var d = declRe.Match(line);
                    if (d.Success)
                    {
                        current = d.Groups[1].Value;
                        if (!declared.Contains(current))
                            declared.Add(current);
                        if (!calls.ContainsKey(current))
                            calls[current] = new HashSet<string>();
                        continue;
                    }
                    if (current == null)
                        continue;
                    foreach (Match c in callRe.Matches(line))
                        calls[current].Add(c.Groups[1].Value);
                }
            }

            // 뿌리: 클라 코드(HUD·아바타·NetAvatar Rpc 본체)가 이름으로 부르는 것.
            var reachable = new HashSet<string>();
            foreach (string file in Directory.GetFiles(clientDir, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match c in callRe.Matches(File.ReadAllText(file)))
                    reachable.Add(c.Groups[1].Value);
            }

            // 전이: 도달한 Try*가 부르는 Try*도 도달이다(TryVet → TryVetResurrect처럼).
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var kv in calls)
                {
                    if (!reachable.Contains(kv.Key))
                        continue;
                    foreach (string callee in kv.Value)
                    {
                        if (reachable.Add(callee))
                            grew = true;
                    }
                }
            }

            var dead = new List<string>();
            for (int i = 0; i < declared.Count; i++)
            {
                if (reachable.Contains(declared[i]) || ReachableExempt.ContainsKey(declared[i]))
                    continue;
                dead.Add(declared[i]);
            }
            if (dead.Count > 0)
                throw new InvalidOperationException("게임에서 도달할 수 없는 기능 " + dead.Count + "건: " + string.Join(", ", dead) +
                    " — 서버에 구현돼 있고 셀프체크가 직접 불러 통과시키지만 클라에 호출부가 없어 **플레이어는 쓸 수 없습니다**. " +
                    "HUD 버튼/키와 NetAvatar Rpc를 배선하거나, 서버 내부 로직이면 ReachableExempt에 이유를 적으세요.");

            Debug.Log("[Ulon] 도달 가능 통과 — 서버 public Try* " + declared.Count + "종 전부 클라에서 도달(허용목록 " + ReachableExempt.Count + "건)");
        }
    }
}
