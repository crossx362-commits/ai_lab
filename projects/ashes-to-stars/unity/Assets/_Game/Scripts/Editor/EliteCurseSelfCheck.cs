using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 「저주술사」 — 회복량·이속 감소 오라(✅ 오너 결정 2026-08-13).
    /// 순수 배율 함수(EliteCurse.NearbyMul)와 BalanceConfig 소비, W3Party 소비 계약,
    /// QA_NO_ELITE_CURSE 네거티브를 검증한다. 수치는 원장 미확정이라 값 자체가 아니라
    /// **저주가 일어나는가·차단하면 원복하는가·소비처가 실재하는가**를 본다.
    /// </summary>
    public static class EliteCurseSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Elite Curse Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(EliteCurse.EnvNo);
            Environment.SetEnvironmentVariable(EliteCurse.EnvNo, null);
            var savedForce = EliteCurse.ForceConfig;
            EliteCurse.ForceConfig = null;

            // ── 순수 배율 함수 ─────────────────────────────
            var cursers = new Vector2[] { new Vector2(0f, 0f) };
            float radius = 4.0f, mul = 0.7f;

            // 저주술사 자신 → 저주 없음(1)
            Check(Mathf.Approximately(
                    EliteCurse.NearbyMul(new Vector2(0f, 0f), true, cursers, 1, radius, mul), 1f),
                "저주술사 자신은 저주 없음(1)");
            // 오라 안 대상 → mul(＜1)
            Check(Mathf.Approximately(
                    EliteCurse.NearbyMul(new Vector2(2f, 0f), false, cursers, 1, radius, mul), mul),
                "오라 안 대상은 저주 배율(＜1)");
            // 오라 경계 바로 밖 → 1
            Check(Mathf.Approximately(
                    EliteCurse.NearbyMul(new Vector2(5f, 0f), false, cursers, 1, radius, mul), 1f),
                "오라 밖 대상은 배율 1");
            // 저주술사 0마리 → 1
            Check(Mathf.Approximately(
                    EliteCurse.NearbyMul(new Vector2(0f, 0f), false, cursers, 0, radius, mul), 1f),
                "저주술사 없으면 배율 1");
            // curserPositions null 안전
            Check(Mathf.Approximately(
                    EliteCurse.NearbyMul(new Vector2(0f, 0f), false, null, 3, radius, mul), 1f),
                "위치 배열 null 안전 → 1");
            // count가 배열보다 커도 안전(Count로 상한)
            Check(Mathf.Approximately(
                    EliteCurse.NearbyMul(new Vector2(9f, 9f), false, cursers, 5, radius, mul), 1f),
                "count 초과 안전(먼 대상 → 1)");

            // ── BalanceConfig 소비(ForceConfig) ─────────────
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            cfg.저주술사오라반경 = 6.0f;
            cfg.저주술사회복배율 = 0.4f;
            cfg.저주술사이속배율 = 0.6f;
            EliteCurse.ForceConfig = cfg;
            Check(Mathf.Approximately(EliteCurse.AuraRadius(), 6.0f), $"AuraRadius 설정 소비 (실제 {EliteCurse.AuraRadius()})");
            Check(Mathf.Approximately(EliteCurse.HealMul(), 0.4f), $"HealMul 설정 소비 (실제 {EliteCurse.HealMul()})");
            Check(Mathf.Approximately(EliteCurse.MoveMul(), 0.6f), $"MoveMul 설정 소비 (실제 {EliteCurse.MoveMul()})");
            EliteCurse.ForceConfig = null;

            // 설정 없으면 기본값(원장 미확정)
            Check(Mathf.Approximately(EliteCurse.HealMul(), EliteCurse.DefaultHealMul),
                "설정 없으면 기본 회복 배율");
            Check(EliteCurse.DefaultHealMul < 1f && EliteCurse.DefaultHealMul > 0f
                  && EliteCurse.DefaultMoveMul < 1f && EliteCurse.DefaultMoveMul > 0f,
                "기본값은 실제로 저주(0＜mul＜1)");

            // ── 네거티브: QA_NO_ELITE_CURSE ─────────────────
            Environment.SetEnvironmentVariable(EliteCurse.EnvNo, "1");
            Check(EliteCurse.Blocked, "QA_NO_ELITE_CURSE 차단");
            Check(Mathf.Approximately(EliteCurse.HealMul(), 1f), "차단하면 회복 배율 1(저주 없음)");
            Check(Mathf.Approximately(EliteCurse.MoveMul(), 1f), "차단하면 이속 배율 1(저주 없음)");
            Environment.SetEnvironmentVariable(EliteCurse.EnvNo, null);
            Check(!EliteCurse.Blocked, "기본은 켜짐");

            // ── W3Party 소비 계약(소스) ─────────────────────
            string w3 = FindSource("W3Party.cs");
            Check(w3 != null, "W3Party.cs 소스 발견");
            if (w3 != null)
            {
                string src = File.ReadAllText(w3);
                Check(src.IndexOf("? 8 :", StringComparison.Ordinal) >= 0,
                    "스폰 테이블에 저주술사(kind 8)가 있다");
                Check(src.IndexOf("_mKind[i] == 8", StringComparison.Ordinal) >= 0,
                    "TickMobs에 저주술사(kind 8) 분기가 있다");
                Check(src.IndexOf("EliteCurse.NearbyMul", StringComparison.Ordinal) >= 0,
                    "회복·이동이 EliteCurse.NearbyMul을 소비한다");
                Check(src.IndexOf("RefreshCursers", StringComparison.Ordinal) >= 0,
                    "RefreshCursers가 존재한다");
                // 스냅은 반드시 파티 회복·이동(TickParty) **전에** — 순서가 어긋나면 한 프레임 늦는다.
                int ri = src.IndexOf("RefreshCursers();", StringComparison.Ordinal);
                int tp = src.IndexOf("TickParty(dt);", StringComparison.Ordinal);
                Check(ri >= 0 && tp >= 0 && ri < tp, "RefreshCursers가 TickParty보다 앞선다");
                // 회복 관문(Heal)이 실제로 저주 배율을 곱하는지 — _curseHeal 소비 확인.
                Check(src.IndexOf("_curseHeal", StringComparison.Ordinal) >= 0,
                    "회복 관문(Heal)이 회복 저주 배율(_curseHeal)을 소비한다");
                // 이동 경로가 이속 저주 배율을 곱하는지 — _curseMove 소비 확인.
                Check(src.IndexOf("_curseMove", StringComparison.Ordinal) >= 0,
                    "이동 경로(TickParty)가 이속 저주 배율(_curseMove)을 소비한다");
                Check(src.IndexOf("MobKindShaman", StringComparison.Ordinal) >= 0,
                    "저주술사 실루엣(MobKindShaman)을 쓴다");
                Check(src.IndexOf("8 => Muted", StringComparison.Ordinal) >= 0,
                    "저주술사 전용 색조(case 8 틴트)가 있다");
                Check(src.IndexOf("EliteCurse.Blocked", StringComparison.Ordinal) >= 0,
                    "차단 시 오라 스냅을 건너뛴다(네거티브 코드 경로)");
            }

            _ = nameof(EliteCurse.NearbyMul);
            _ = nameof(EliteCurse.AuraRadius);

            if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            EliteCurse.ForceConfig = savedForce;
            Environment.SetEnvironmentVariable(EliteCurse.EnvNo, no);

            if (_fail == 0) Debug.Log("[EliteCurseSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EliteCurseSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EliteCurseSelfCheck] FAIL {_fail}건");
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
