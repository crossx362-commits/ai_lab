// TurnOrder 라이브러리 검증. 네거티브 컨트롤 없는 통과는 통과가 아니다.

using System;
using System.Collections.Generic;
using Tankfall.Sim;

static class TurnOrderVerify
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== TurnOrder (포트리스2 딜레이 기반 턴) 검증 ===\n");

        int failures = 0;

        // [1] 전원 딜레이 동일 → Id 순 교대(라운드로빈)
        Console.WriteLine("[1] 라운드로빈 (동일 딜레이)");
        {
            var to = new TurnOrder();
            for (int i = 0; i < 6; i++) to.Add(i, 560);  // 모두 동일
            var alive = new bool[] { true, true, true, true, true, true };
            Func<int, bool> aliveCheck = id => alive[id];

            var sequence = new List<int>();
            for (int i = 0; i < 60; i++)  // 10라운드
            {
                int u = to.Next(aliveCheck);
                sequence.Add(u);
                to.Consume(u, ShellKind.Normal, false);
            }

            // 라운드로빈: 0,1,2,3,4,5, 0,1,2,3,4,5, ...
            bool correct = true;
            for (int i = 0; i < 60; i++)
            {
                if (sequence[i] != i % 6) { correct = false; break; }
            }

            if (correct)
            {
                Console.WriteLine("  ✅ 정확히 Id 순 교대 (0→1→2→3→4→5→...)");
            }
            else
            {
                Console.WriteLine("  ❌ 교대 순서가 틀림");
                failures++;
            }
        }
        Console.WriteLine();

        // [2] 딜레이 불균형: 한 유닛만 빠름 (530) 나머지는 느림 (580)
        //     530/580 ≈ 1.094 배 비율 체크
        Console.WriteLine("[2] 딜레이 불균형 (530 vs 580)");
        {
            var to = new TurnOrder();
            to.Add(0, 530);  // 가장 빠름
            for (int i = 1; i < 6; i++) to.Add(i, 580);
            var alive = new bool[] { true, true, true, true, true, true };
            Func<int, bool> aliveCheck = id => alive[id];

            int[] actionCount = new int[6];
            for (int i = 0; i < 600; i++)  // 100라운드(6×100)
            {
                int u = to.Next(aliveCheck);
                actionCount[u]++;
                to.Consume(u, ShellKind.Normal, false);
            }

            float ratio = (float)actionCount[0] / actionCount[1];  // 빠른 것 / 느린 것
            float expected = 580f / 530f;  // ≈ 1.094
            float errorPct = Math.Abs(ratio - expected) / expected * 100f;

            Console.WriteLine($"  빠름(530): {actionCount[0]} 번, 느림(580): {actionCount[1]} 번");
            Console.WriteLine($"  비율: {ratio:F3} (기대: {expected:F3}, 오차: {errorPct:F1}%)");
            if (errorPct <= 3f)
            {
                Console.WriteLine("  ✅ 비율 ±3% 이내");
            }
            else
            {
                Console.WriteLine("  ❌ 비율 오차가 3%를 초과");
                failures++;
            }
        }
        Console.WriteLine();

        // [3] 죽은 유닛은 절대 선택되지 않음
        Console.WriteLine("[3] 죽은 유닛 선택 안 함");
        {
            var to = new TurnOrder();
            for (int i = 0; i < 6; i++) to.Add(i, 560);
            var alive = new bool[] { true, true, true, true, true, true };
            Func<int, bool> aliveCheck = id => alive[id];

            // 유닛 3 죽이고 200번 돌리기
            alive[3] = false;
            int deadCount = 0;
            for (int i = 0; i < 200; i++)
            {
                int u = to.Next(aliveCheck);
                if (u == 3) deadCount++;
                to.Consume(u, ShellKind.Normal, false);
            }

            if (deadCount == 0)
            {
                Console.WriteLine("  ✅ 죽은 유닛(3) 선택 0회");
            }
            else
            {
                Console.WriteLine($"  ❌ 죽은 유닛 {deadCount}회 선택됨");
                failures++;
            }
        }
        Console.WriteLine();

        // [4] 결정론성: 같은 입력 → 같은 순서
        Console.WriteLine("[4] 결정론성 (재현 가능)");
        {
            var seq1 = new List<int>();
            {
                var to = new TurnOrder();
                for (int i = 0; i < 6; i++) to.Add(i, 560 + (i % 2) * 20);  // 560, 580, 560, 580, ...
                var alive = new bool[] { true, true, true, true, true, true };
                Func<int, bool> aliveCheck = id => alive[id];
                for (int i = 0; i < 100; i++)
                {
                    int u = to.Next(aliveCheck);
                    seq1.Add(u);
                    to.Consume(u, ShellKind.Normal, false);
                }
            }

            var seq2 = new List<int>();
            {
                var to = new TurnOrder();
                for (int i = 0; i < 6; i++) to.Add(i, 560 + (i % 2) * 20);
                var alive = new bool[] { true, true, true, true, true, true };
                Func<int, bool> aliveCheck = id => alive[id];
                for (int i = 0; i < 100; i++)
                {
                    int u = to.Next(aliveCheck);
                    seq2.Add(u);
                    to.Consume(u, ShellKind.Normal, false);
                }
            }

            bool identical = true;
            for (int i = 0; i < seq1.Count; i++)
            {
                if (seq1[i] != seq2[i]) { identical = false; break; }
            }

            if (identical)
            {
                Console.WriteLine("  ✅ 두 실행 결과 완벽히 동일");
            }
            else
            {
                Console.WriteLine("  ❌ 순서가 다름");
                failures++;
            }
        }
        Console.WriteLine();

        // [5] 특수탄·이동이 누적을 실제로 늘리는가
        Console.WriteLine("[5] 특수탄·이동 추가 딜레이");
        {
            var to = new TurnOrder();
            for (int i = 0; i < 3; i++) to.Add(i, 560);
            var alive = new bool[] { true, true, true };
            Func<int, bool> aliveCheck = id => alive[id];

            // 각 유닛을 1회씩 소비 — 다음 라운드에서 순서 확인
            int u0 = to.Next(aliveCheck); to.Consume(u0, ShellKind.Normal, false);  // u0: +560
            int u1 = to.Next(aliveCheck); to.Consume(u1, ShellKind.Special, false); // u1: +560×1.2 = +672
            int u2 = to.Next(aliveCheck); to.Consume(u2, ShellKind.Normal, true);   // u2: +560+56 = +616

            // u0가 가장 빠르게 다시 나와야 한다 (560 vs 672 vs 616)
            int next = to.Next(aliveCheck);

            Console.WriteLine($"  u0(Normal, no move): Accumulated={to.Accumulated(0)}");
            Console.WriteLine($"  u1(Special, no move): Accumulated={to.Accumulated(1)}");
            Console.WriteLine($"  u2(Normal, moved): Accumulated={to.Accumulated(2)}");
            Console.WriteLine($"  → 다음 순서: {next}");

            if (next == u0)
            {
                Console.WriteLine("  ✅ 올바른 순서 (u0가 가장 빠름)");
            }
            else
            {
                Console.WriteLine($"  ❌ 틀린 순서 (u{next}가 나옴)");
                failures++;
            }
        }
        Console.WriteLine();

        // 최종 결과
        Console.WriteLine(failures == 0 ? "=== 모든 검증 통과 ===" : $"=== {failures}개 검증 실패 ===");
        Environment.Exit(failures > 0 ? 1 : 0);
    }
}
