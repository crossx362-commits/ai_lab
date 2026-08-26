using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 「군단장」 — 공속·이속 증가 오라(✅ 오너 결정 2026-08-13).
    /// 순수 배율 함수(EliteLegion.NearbyMul)와 BalanceConfig 소비, W3Party 소비 계약,
    /// QA_NO_ELITE_LEGION 네거티브를 검증한다. 수치는 원장 미확정이라 값 자체가 아니라
    /// **버프가 일어나는가·차단하면 원복하는가·소비처가 실재하는가**를 본다.
    /// </summary>
    public static class EliteLegionSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Elite Legion Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(EliteLegion.EnvNo);
            Environment.SetEnvironmentVariable(EliteLegion.EnvNo, null);
            var savedForce = EliteLegion.ForceConfig;
            EliteLegion.ForceConfig = null;

            // ── 순수 배율 함수 ─────────────────────────────
            var cmds = new Vector2[] { new Vector2(0f, 0f) };
            float radius = 4.0f, mul = 1.5f;

            // 군단장 자신 → 버프 없음(1)
            Check(Mathf.Approximately(
                    EliteLegion.NearbyMul(new Vector2(0f, 0f), true, cmds, 1, radius, mul), 1f),
                "군단장 자신은 버프 없음(1)");
            // 오라 안 잡몹 → mul
            Check(Mathf.Approximately(
                    EliteLegion.NearbyMul(new Vector2(2f, 0f), false, cmds, 1, radius, mul), mul),
                "오라 안 잡몹은 버프 배율");
            // 오라 경계 바로 밖 → 1
            Check(Mathf.Approximately(
                    EliteLegion.NearbyMul(new Vector2(5f, 0f), false, cmds, 1, radius, mul), 1f),
                "오라 밖 잡몹은 배율 1");
            // 군단장 0마리 → 1
            Check(Mathf.Approximately(
                    EliteLegion.NearbyMul(new Vector2(0f, 0f), false, cmds, 0, radius, mul), 1f),
                "군단장 없으면 배율 1");
            // commanderPositions null 안전
            Check(Mathf.Approximately(
                    EliteLegion.NearbyMul(new Vector2(0f, 0f), false, null, 3, radius, mul), 1f),
                "위치 배열 null 안전 → 1");
            // count가 배열보다 커도 안전(Count로 상한)
            Check(Mathf.Approximately(
                    EliteLegion.NearbyMul(new Vector2(9f, 9f), false, cmds, 5, radius, mul), 1f),
                "count 초과 안전(먼 대상 → 1)");

            // ── BalanceConfig 소비(ForceConfig) ─────────────
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            cfg.군단장오라반경 = 8.0f;
            cfg.군단장주변공속배율 = 2.0f;
            cfg.군단장주변이속배율 = 1.8f;
            EliteLegion.ForceConfig = cfg;
            Check(Mathf.Approximately(EliteLegion.AuraRadius(), 8.0f), $"AuraRadius 설정 소비 (실제 {EliteLegion.AuraRadius()})");
            Check(Mathf.Approximately(EliteLegion.AtkSpdMul(), 2.0f), $"AtkSpdMul 설정 소비 (실제 {EliteLegion.AtkSpdMul()})");
            Check(Mathf.Approximately(EliteLegion.MoveMul(), 1.8f), $"MoveMul 설정 소비 (실제 {EliteLegion.MoveMul()})");
            EliteLegion.ForceConfig = null;

            // 설정 없으면 기본값(원장 미확정)
            Check(Mathf.Approximately(EliteLegion.AtkSpdMul(), EliteLegion.DefaultAtkSpdMul),
                "설정 없으면 기본 공속 배율");
            Check(EliteLegion.DefaultAtkSpdMul > 1f && EliteLegion.DefaultMoveMul > 1f,
                "기본값은 실제로 버프(>1)");

            // ── 네거티브: QA_NO_ELITE_LEGION ────────────────
            Environment.SetEnvironmentVariable(EliteLegion.EnvNo, "1");
            Check(EliteLegion.Blocked, "QA_NO_ELITE_LEGION 차단");
            Check(Mathf.Approximately(EliteLegion.AtkSpdMul(), 1f), "차단하면 공속 배율 1(버프 없음)");
            Check(Mathf.Approximately(EliteLegion.MoveMul(), 1f), "차단하면 이속 배율 1(버프 없음)");
            Environment.SetEnvironmentVariable(EliteLegion.EnvNo, null);
            Check(!EliteLegion.Blocked, "기본은 켜짐");

            // ── W3Party 소비 계약(소스) ─────────────────────
            string w3 = FindSource("W3Party.cs");
            Check(w3 != null, "W3Party.cs 소스 발견");
            if (w3 != null)
            {
                string src = File.ReadAllText(w3);
                Check(src.IndexOf("? 6 : 7", StringComparison.Ordinal) >= 0,
                    "스폰 테이블에 군단장(kind 7)이 있다");
                Check(src.IndexOf("_mKind[i] == 7", StringComparison.Ordinal) >= 0,
                    "TickMobs에 군단장(kind 7) 분기가 있다");
                Check(src.IndexOf("EliteLegion.NearbyMul", StringComparison.Ordinal) >= 0,
                    "이동·공격이 EliteLegion.NearbyMul을 소비한다");
                Check(src.IndexOf("RefreshCommanders", StringComparison.Ordinal) >= 0,
                    "RefreshCommanders가 존재한다");
                // 스냅은 반드시 몹 이동·공격(TickMobs) **전에** — 순서가 어긋나면 한 프레임 늦는다.
                int ri = src.IndexOf("RefreshCommanders();", StringComparison.Ordinal);
                int tm = src.IndexOf("TickMobs(dt);", StringComparison.Ordinal);
                Check(ri >= 0 && tm >= 0 && ri < tm, "RefreshCommanders가 TickMobs보다 앞선다");
                Check(src.IndexOf("MobKindBard", StringComparison.Ordinal) >= 0,
                    "군단장 실루엣(MobKindBard)을 쓴다");
                Check(src.IndexOf("7 => Muted", StringComparison.Ordinal) >= 0,
                    "군단장 전용 색조(case 7 틴트)가 있다");
                Check(src.IndexOf("EliteLegion.Blocked", StringComparison.Ordinal) >= 0,
                    "차단 시 오라 스냅을 건너뛴다(네거티브 코드 경로)");
            }

            _ = nameof(EliteLegion.NearbyMul);
            _ = nameof(EliteLegion.AuraRadius);

            if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            EliteLegion.ForceConfig = savedForce;
            Environment.SetEnvironmentVariable(EliteLegion.EnvNo, no);

            if (_fail == 0) Debug.Log("[EliteLegionSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EliteLegionSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EliteLegionSelfCheck] FAIL {_fail}건");
        }

        static string FindSource(string fileName)
        {
            try
            {
                string[] roots =
                {
                    Path.Combine(Application.dataPath, "Scripts"),
                    Path.Combine(Application.dataPath, "_Game/Scripts/Runtime"),
                };
                foreach (var root in roots)
                {
                    if (!Directory.Exists(root)) continue;
                    var hit = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                    if (hit.Length > 0) return hit[0];
                }
                var all = Directory.GetFiles(Application.dataPath, fileName, SearchOption.AllDirectories);
                return all.Length > 0 ? all[0] : null;
            }
            catch { return null; }
        }
    }
}
