using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-12 부지 확장(격자 8×8 → 최대 16×16, 해금 20/50/80층) 소비처.
    /// 소비처 = EstateGrid.Size(논리 격자 폭)가 EstateExpansion.CurrentSize를 읽어
    /// 탑 최고 층에 따라 커진다. QA_NO_ESTATE_EXPAND면 항상 8×8(네거티브).
    /// </summary>
    public static class EstateExpansionSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        static int SizeAtFloor(int floor)
        {
            EstateExpansion.ResetForTest();          // 고정 해제 — 층 기반으로 판정
            GameState.SetTowerFloorForTest(floor);
            return EstateGrid.Size;
        }

        [MenuItem("Ashes to Stars/QA/Estate Expansion Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(EstateExpansion.EnvNo);
            Environment.SetEnvironmentVariable(EstateExpansion.EnvNo, null);

            GameState.ResetAll();
            EstateExpansion.ResetForTest();

            // ── 수치 계약 ──
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.부지확장해금층1 == 20 && cfg.부지확장해금층2 == 50 && cfg.부지확장해금층3 == 80,
                $"BalanceConfig 해금층 기본 20/50/80 (실제 {cfg?.부지확장해금층1}/{cfg?.부지확장해금층2}/{cfg?.부지확장해금층3})");
            UnityEngine.Object.DestroyImmediate(cfg);
            Check(EstateGrid.BaseSize == 8 && EstateGrid.MaxSize == 16, "격자 8×8→16×16(§18-12)");
            Check(!EstateExpansion.Blocked, "기본은 확장 켜짐");
            var g = EstateExpansion.Gates();
            Check(g.Item1 == 20 && g.Item2 == 50 && g.Item3 == 80, "Gates 읽기 20/50/80");

            // ── 강제(§18-12): 층이 오르면 격자가 커진다 ──
            Check(SizeAtFloor(1) == 8, $"1층은 8×8 (실제 {SizeAtFloor(1)})");
            Check(SizeAtFloor(19) == 8, "19층은 아직 8×8");
            Check(SizeAtFloor(20) == 11, $"20층에 1단계 해금 (실제 {SizeAtFloor(20)})");
            Check(SizeAtFloor(49) == 11, "49층은 1단계 유지");
            Check(SizeAtFloor(50) == 13, $"50층에 2단계 해금 (실제 {SizeAtFloor(50)})");
            Check(SizeAtFloor(79) == 13, "79층은 2단계 유지");
            Check(SizeAtFloor(80) == 16, $"80층에 최대 16×16 (실제 {SizeAtFloor(80)})");
            Check(SizeAtFloor(100) == 16, "100층도 16×16 상한");

            // ── 실소비: 넓어진 칸이 실제로 격자에 들어온다 ──
            SizeAtFloor(1);
            EstateGrid.ForgetInMemoryForTest();
            Check(!EstateGrid.InBounds(10, 10), "8×8에서 (10,10)은 격자 밖");
            Check(EstateGrid.At(10, 10) == EstateGrid.Cell.Wall, "밖 칸은 벽으로 읽힌다");
            SizeAtFloor(80);
            EstateGrid.ForgetInMemoryForTest();
            Check(EstateGrid.InBounds(10, 10), "16×16에서 (10,10)은 격자 안");
            Check(EstateGrid.At(10, 10) == EstateGrid.Cell.Empty, "확장 칸은 빈 칸(허브 밖)");

            // ── ForceConfig가 해금층을 읽는다 ──
            var custom = ScriptableObject.CreateInstance<BalanceConfig>();
            custom.부지확장해금층1 = 5;
            custom.부지확장해금층2 = 10;
            custom.부지확장해금층3 = 15;
            EstateExpansion.ForceConfig = custom;
            GameState.SetTowerFloorForTest(12);        // 12 ≥ 5·10, < 15 → 2단계
            Check(EstateGrid.Size == 13, $"ForceConfig 해금층으로 2단계 (실제 {EstateGrid.Size})");
            EstateExpansion.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(custom);

            // ── §21-3 폴백: 비정상 해금층은 원장 기본으로 ──
            var zero = ScriptableObject.CreateInstance<BalanceConfig>();
            zero.부지확장해금층1 = 0; zero.부지확장해금층2 = 0; zero.부지확장해금층3 = 0;
            EstateExpansion.ForceConfig = zero;
            GameState.SetTowerFloorForTest(80);
            Check(EstateGrid.Size == 16, "해금층 0은 기본 20/50/80 폴백(§21-3)");
            EstateExpansion.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(zero);

            var rev = ScriptableObject.CreateInstance<BalanceConfig>();
            rev.부지확장해금층1 = 80; rev.부지확장해금층2 = 50; rev.부지확장해금층3 = 20;
            EstateExpansion.ForceConfig = rev;
            GameState.SetTowerFloorForTest(60);
            var gRev = EstateExpansion.Gates();
            Check(gRev.Item1 == 20 && gRev.Item2 == 50 && gRev.Item3 == 80, "비단조 해금층도 기본 폴백");
            EstateExpansion.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(rev);

            // ── 네거티브: QA_NO면 확장 없음(항상 8×8) ──
            EstateExpansion.ResetForTest();
            Environment.SetEnvironmentVariable(EstateExpansion.EnvNo, "1");
            Check(EstateExpansion.Blocked, "QA_NO면 확장 차단");
            GameState.SetTowerFloorForTest(100);
            Check(EstateGrid.Size == 8, "차단하면 100층도 8×8(옛 동작)");
            Environment.SetEnvironmentVariable(EstateExpansion.EnvNo, null);
            Check(!EstateExpansion.Blocked, "차단을 풀면 다시 확장");

            // ── 소스 계약 ──
            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string gridSrc = File.ReadAllText(Path.Combine(runtime, "EstateGrid.cs"));
            Check(gridSrc.Contains("EstateExpansion.CurrentSize"),
                "EstateGrid.Size가 EstateExpansion.CurrentSize를 소비한다");
            Check(gridSrc.Contains("const int Stride = MaxSize"),
                "EstateGrid가 저장·인덱싱을 최대 폭(Stride)으로 고정한다");
            string expSrc = File.ReadAllText(Path.Combine(runtime, "EstateExpansion.cs"));
            Check(expSrc.Contains("부지확장해금층1") && expSrc.Contains("부지확장해금층3"),
                "EstateExpansion이 BalanceConfig 해금층을 읽는다");
            string balanceSrc = File.ReadAllText(Path.Combine(runtime, "BalanceConfig.cs"));
            Check(balanceSrc.Contains("public int 부지확장해금층1 = 20;")
                && balanceSrc.Contains("public int 부지확장해금층3 = 80;"),
                "BalanceConfig에 §18-12 해금층 필드가 authored돼 있다");

            _ = nameof(EstateExpansion.CurrentSize);
            _ = nameof(EstateExpansion.UnlockedTiers);
            _ = nameof(BalanceConfig.부지확장해금층1);

            // ── 정리: 다음 테스트를 위해 초기(8×8)로 되돌린다 ──
            Environment.SetEnvironmentVariable(EstateExpansion.EnvNo, no);
            GameState.SetTowerFloorForTest(1);
            EstateExpansion.ResetForTest();
            GameState.ResetAll();
            EstateGrid.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "estate_expansion_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS EstateExpansionSelfCheck" : "FAIL EstateExpansionSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[EstateExpansionSelfCheck] PASS → " + path);
            else Debug.LogError("[EstateExpansionSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[EstateExpansionSelfCheck] FAIL {_fail}건");
        }
    }
}
