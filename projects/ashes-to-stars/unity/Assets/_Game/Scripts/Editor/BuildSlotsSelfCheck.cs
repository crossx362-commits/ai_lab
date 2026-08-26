using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-12 동시 건설 슬롯(2개 = 본성 라인 1 + 그 외 1) 소비처.
    /// 본성은 전용 슬롯이라 무제한, 그 외 핵심 건물은 하나만 동시 공사.
    /// QA_NO_BUILD_SLOTS면 옛 동작(칸마다 병렬 공사 OK).
    /// </summary>
    public static class BuildSlotsSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Build Slots Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(BuildSlots.EnvNo);
            string fast = Environment.GetEnvironmentVariable("QA_ESTATE_KEEP_FAST");
            Environment.SetEnvironmentVariable(BuildSlots.EnvNo, null);
            // 즉시 완료(wait=1)면 공사가 초 경계에서 끝나 슬롯이 흔들린다 — 긴 공사로 고정.
            Environment.SetEnvironmentVariable("QA_ESTATE_KEEP_FAST", null);

            GameState.ResetAll();
            EstateBuild.ResetForTest();
            BuildSlots.ResetForTest();

            // ── 수치 계약 ──
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && cfg.동시건설슬롯 == 2,
                $"BalanceConfig.동시건설슬롯 기본 2 (실제 {cfg?.동시건설슬롯})");
            UnityEngine.Object.DestroyImmediate(cfg);
            Check(!BuildSlots.Blocked, "기본은 슬롯 강제 켜짐");
            Check(BuildSlots.Cap() == 2, $"Cap 읽기 2 (실제 {BuildSlots.Cap()})");
            Check(BuildSlots.OtherSlots() == 1, $"OtherSlots = Cap-1 = 1 (실제 {BuildSlots.OtherSlots()})");

            var custom = ScriptableObject.CreateInstance<BalanceConfig>();
            custom.동시건설슬롯 = 3;
            BuildSlots.ForceConfig = custom;
            Check(BuildSlots.Cap() == 3, "ForceConfig가 동시건설슬롯을 읽는다");
            Check(BuildSlots.OtherSlots() == 2, "슬롯 3이면 그 외 2");
            BuildSlots.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(custom);
            Check(BuildSlots.Cap() == 2, "에셋을 치우면 다시 2");

            var zero = ScriptableObject.CreateInstance<BalanceConfig>();
            zero.동시건설슬롯 = 0;
            BuildSlots.ForceConfig = zero;
            Check(BuildSlots.Cap() == 2, "슬롯 0은 기본 2 폴백(§21-3)");
            BuildSlots.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(zero);

            // ── 강제(§18-12): 본성 전용 · 그 외 1동만 동시 공사 ──
            GameState.ResetAll();
            EstateBuild.ResetForTest();
            EstateBuild.SetLevelForTest(EstateGrid.Cell.Keep, 10);
            EstateBuild.SetLevelForTest(EstateGrid.Cell.Mine, 3);
            EstateBuild.SetLevelForTest(EstateGrid.Cell.Warehouse, 3);
            GameState.Grant(50_000_000);
            Check(EstateBuild.ActiveOtherBuilds() == 0, "착공 전 그 외 공사 0");

            bool startedMine = EstateBuild.TryStartUpgrade(EstateGrid.Cell.Mine);
            Check(startedMine, "첫 그 외 건물(광산) 착공 성공");
            Check(EstateBuild.ActiveOtherBuilds() == 1, "그 외 공사 1");

            string wWare = EstateBuild.WhyCannotUpgrade(EstateGrid.Cell.Warehouse);
            Check(wWare != null && wWare.Contains("슬롯"),
                $"둘째 그 외 건물(창고)은 슬롯이 차서 막힘 (실제 {wWare})");
            Check(!EstateBuild.TryStartUpgrade(EstateGrid.Cell.Warehouse),
                "슬롯이 차면 둘째 착공 실패");

            string wKeep = EstateBuild.WhyCannotUpgrade(EstateGrid.Cell.Keep);
            Check(wKeep == null, $"본성은 전용 슬롯이라 그 외 공사 중에도 착공 가능 (실제 {wKeep})");

            // ── 네거티브: QA_NO면 옛 병렬 공사 ──
            Environment.SetEnvironmentVariable(BuildSlots.EnvNo, "1");
            Check(BuildSlots.Blocked, "QA_NO면 슬롯 강제 꺼짐");
            GameState.Grant(50_000_000);
            string wWareNo = EstateBuild.WhyCannotUpgrade(EstateGrid.Cell.Warehouse);
            Check(wWareNo == null || !wWareNo.Contains("슬롯"),
                $"차단하면 슬롯 사유 없음(옛 병렬) (실제 {wWareNo})");
            Check(EstateBuild.TryStartUpgrade(EstateGrid.Cell.Warehouse),
                "차단하면 광산 공사 중에도 창고 병렬 착공");
            Check(EstateBuild.ActiveOtherBuilds() == 2, "차단하면 그 외 2동 병렬(옛 동작)");
            Environment.SetEnvironmentVariable(BuildSlots.EnvNo, null);
            Check(!BuildSlots.Blocked, "차단을 풀면 다시 슬롯 강제");

            // ── 소스 계약 ──
            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estateSrc = File.ReadAllText(Path.Combine(runtime, "EstateBuild.cs"));
            Check(estateSrc.Contains("BuildSlots.OtherSlots") && estateSrc.Contains("BuildSlots.Blocked"),
                "EstateBuild이 BuildSlots로 착공을 막는다");
            Check(estateSrc.Contains("동시 건설 슬롯이 찼다"),
                "EstateBuild에 §18-12 차단 사유가 있다");
            string buildSlotsSrc = File.ReadAllText(Path.Combine(runtime, "BuildSlots.cs"));
            Check(buildSlotsSrc.Contains("동시건설슬롯"),
                "BuildSlots가 BalanceConfig.동시건설슬롯을 읽는다");
            string balanceSrc = File.ReadAllText(Path.Combine(runtime, "BalanceConfig.cs"));
            Check(balanceSrc.Contains("public int 동시건설슬롯 = 2;"),
                "BalanceConfig에 §18-12 슬롯 필드가 authored돼 있다");

            _ = nameof(BuildSlots.Cap);
            _ = nameof(BuildSlots.OtherSlots);
            _ = nameof(EstateBuild.ActiveOtherBuilds);
            _ = nameof(BalanceConfig.동시건설슬롯);

            Environment.SetEnvironmentVariable(BuildSlots.EnvNo, no);
            Environment.SetEnvironmentVariable("QA_ESTATE_KEEP_FAST", fast);
            BuildSlots.ResetForTest();
            EstateBuild.ResetForTest();
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "build_slots_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS BuildSlotsSelfCheck" : "FAIL BuildSlotsSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[BuildSlotsSelfCheck] PASS → " + path);
            else Debug.LogError("[BuildSlotsSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[BuildSlotsSelfCheck] FAIL {_fail}건");
        }
    }
}
