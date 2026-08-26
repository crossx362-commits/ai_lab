using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 스윕 등록 대조(회의 20260826-095813 채택 1) — Editor *SelfCheck 전수(반사 발견,
    /// GameFullCheck와 같은 기준)와 GameSweepSelfCheck.Registry를 양방향 대조한다.
    /// EliteDropSelfCheck가 8/18부터 스윕 밖에 잠복했던 「정의된 검증 vs 등록된 검증」
    /// 대응 누락을 스윕이 도는 즉시 FAIL로 지목한다.
    ///
    /// QA_NO_SWEEP_COVERAGE=1이면 네거티브: 실제 등록부에서 한 종을 일부러 가린 상태로
    /// 탐지기를 돌려, 정확히 그 한 종을 지목하는지 재현한다(탐지기 자체의 검증).
    /// </summary>
    public static class SweepCoverageSelfCheck
    {
        internal const string NegEnv = "QA_NO_SWEEP_COVERAGE";

        /// <summary>스윕에 등록될 수 없는 것이 확정된 것들 — 전수 집계에서도 뺀다.</summary>
        static readonly string[] NotSweepable =
        {
            "UiAtlasUvSelfCheck",   // EditorApplication.Exit 호출 — 단독 실행 전용(GameFullCheck.Skip과 동일 사유)
            "GameSweepSelfCheck",   // 스윕 본체 자신 — 자기 자신을 자기 등록부에 넣을 수 없다
        };

        [MenuItem("Ashes to Stars/QA/Sweep Coverage Self Check")]
        public static void Run()
        {
            var all = DiscoverAll();
            var reg = GameSweepSelfCheck.Registry
                .Select(e => e.Run.Method.DeclaringType.Name)
                .ToList();

            string neg = Environment.GetEnvironmentVariable(NegEnv);
            if (!string.IsNullOrEmpty(neg))
            {
                // 네거티브: BodyNavSelfCheck 등록을 가리고, 탐지기가 정확히 그것만 잡는지 본다.
                var broken = reg.Where(n => n != "BodyNavSelfCheck").ToList();
                var caught = MissingNames(all, broken);
                if (caught.Count != 1 || caught[0] != "BodyNavSelfCheck")
                    throw new Exception(
                        $"FAIL 네거티브 탐지 실패 — 가린 것 외에 잡힌 목록 [{string.Join(", ", caught)}]");
                Debug.Log("[SweepCoverageSelfCheck] 네거티브 PASS — 일부러 뺀 BodyNavSelfCheck를 정확히 지목");
                return;
            }

            var missing = MissingNames(all, reg);
            var stale = reg.Where(n => !all.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (missing.Count > 0 || stale.Count > 0)
            {
                string msg = "FAIL 스윕 등록 불일치";
                if (missing.Count > 0)
                    msg += $" — 미등록 {missing.Count}종: {string.Join(", ", missing)}";
                if (stale.Count > 0)
                    msg += $" — 낡은 등록 {stale.Count}종: {string.Join(", ", stale)}";
                throw new Exception(msg);
            }
            Debug.Log($"[SweepCoverageSelfCheck] PASS 전수 {all.Count}종 = 등록 {reg.Count}종 일치");
        }

        /// <summary>GameFullCheck와 같은 반사 기준의 전수 — 스윕 불가 확정분은 뺀다.</summary>
        internal static List<string> DiscoverAll()
        {
            return typeof(SweepCoverageSelfCheck).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed)   // static class
                .Where(t => t.Name.EndsWith("SelfCheck", StringComparison.Ordinal))
                .Where(t => !NotSweepable.Contains(t.Name))
                .Where(t => t.GetMethod("Run", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null) != null)
                .Select(t => t.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        static List<string> MissingNames(List<string> all, List<string> registered)
        {
            return all.Where(n => !registered.Contains(n))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
    }
}
