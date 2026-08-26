using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 「수호자」 — 피해 감소 오라·고방어(✅ 오너 결정 2026-08-13).
    /// 순수 배율 함수(EliteGuardian.Multiplier)와 BalanceConfig 소비, W3Party 소비 계약,
    /// QA_NO_ELITE_GUARDIAN 네거티브를 검증한다. 수치는 원장 미확정이라 값 자체가 아니라
    /// **감소가 일어나는가·차단하면 원복하는가·소비처가 실재하는가**를 본다.
    /// </summary>
    public static class EliteGuardianSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Elite Guardian Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(EliteGuardian.EnvNo);
            Environment.SetEnvironmentVariable(EliteGuardian.EnvNo, null);
            var savedForce = EliteGuardian.ForceConfig;
            EliteGuardian.ForceConfig = null;

            // ── 순수 배율 함수 ─────────────────────────────
            var guardians = new Vector2[] { new Vector2(0f, 0f) };
            float radius = 3.5f, nearby = 0.5f, self = 0.4f;

            // 수호자 자신 → 고방어(self)만, 오라와 이중 감소 안 함
            Check(Mathf.Approximately(
                    EliteGuardian.Multiplier(new Vector2(0f, 0f), true, guardians, 1, radius, nearby, self), self),
                "수호자 자신은 고방어 배율");
            // 오라 안 잡몹 → nearby
            Check(Mathf.Approximately(
                    EliteGuardian.Multiplier(new Vector2(2f, 0f), false, guardians, 1, radius, nearby, self), nearby),
                "오라 안 잡몹은 감소 배율");
            // 오라 경계 바로 밖 → 1
            Check(Mathf.Approximately(
                    EliteGuardian.Multiplier(new Vector2(4f, 0f), false, guardians, 1, radius, nearby, self), 1f),
                "오라 밖 잡몹은 배율 1");
            // 수호자 0마리 → 1
            Check(Mathf.Approximately(
                    EliteGuardian.Multiplier(new Vector2(0f, 0f), false, guardians, 0, radius, nearby, self), 1f),
                "수호자 없으면 배율 1");
            // guardianPositions null 안전
            Check(Mathf.Approximately(
                    EliteGuardian.Multiplier(new Vector2(0f, 0f), false, null, 3, radius, nearby, self), 1f),
                "위치 배열 null 안전 → 1");
            // count가 배열보다 커도 안전(Count로 상한)
            Check(Mathf.Approximately(
                    EliteGuardian.Multiplier(new Vector2(9f, 9f), false, guardians, 5, radius, nearby, self), 1f),
                "count 초과 안전(먼 대상 → 1)");

            // ── BalanceConfig 소비(ForceConfig) ─────────────
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            cfg.수호자오라반경 = 7.0f;
            cfg.수호자주변피해배율 = 0.3f;
            cfg.수호자자체피해배율 = 0.2f;
            EliteGuardian.ForceConfig = cfg;
            Check(Mathf.Approximately(EliteGuardian.AuraRadius(), 7.0f), $"AuraRadius 설정 소비 (실제 {EliteGuardian.AuraRadius()})");
            Check(Mathf.Approximately(EliteGuardian.NearbyTakenMul(), 0.3f), $"NearbyTakenMul 설정 소비 (실제 {EliteGuardian.NearbyTakenMul()})");
            Check(Mathf.Approximately(EliteGuardian.SelfTakenMul(), 0.2f), $"SelfTakenMul 설정 소비 (실제 {EliteGuardian.SelfTakenMul()})");
            EliteGuardian.ForceConfig = null;

            // 설정 없으면 기본값(원장 미확정)
            Check(Mathf.Approximately(EliteGuardian.NearbyTakenMul(), EliteGuardian.DefaultNearbyTakenMul),
                "설정 없으면 기본 주변 배율");
            Check(EliteGuardian.DefaultNearbyTakenMul < 1f && EliteGuardian.DefaultSelfTakenMul < 1f,
                "기본값은 실제로 감소(<1)");

            // ── 네거티브: QA_NO_ELITE_GUARDIAN ──────────────
            Environment.SetEnvironmentVariable(EliteGuardian.EnvNo, "1");
            Check(EliteGuardian.Blocked, "QA_NO_ELITE_GUARDIAN 차단");
            Check(Mathf.Approximately(EliteGuardian.NearbyTakenMul(), 1f), "차단하면 주변 배율 1(감소 없음)");
            Check(Mathf.Approximately(EliteGuardian.SelfTakenMul(), 1f), "차단하면 자체 배율 1(고방어 없음)");
            Environment.SetEnvironmentVariable(EliteGuardian.EnvNo, null);
            Check(!EliteGuardian.Blocked, "기본은 켜짐");

            // ── W3Party 소비 계약(소스) ─────────────────────
            string w3 = FindSource("W3Party.cs");
            Check(w3 != null, "W3Party.cs 소스 발견");
            if (w3 != null)
            {
                string src = File.ReadAllText(w3);
                Check(src.IndexOf(": 6;", StringComparison.Ordinal) >= 0
                      || src.IndexOf("? 4 : 6", StringComparison.Ordinal) >= 0
                      || src.IndexOf("? 6 : 7", StringComparison.Ordinal) >= 0,
                    "스폰 테이블에 수호자(kind 6)가 있다");
                Check(src.IndexOf("_mKind[i] == 6", StringComparison.Ordinal) >= 0,
                    "TickMobs에 수호자(kind 6) 분기가 있다");
                Check(src.IndexOf("EliteGuardian.Multiplier", StringComparison.Ordinal) >= 0,
                    "DamageMob이 EliteGuardian.Multiplier를 소비한다");
                Check(src.IndexOf("RefreshGuardians", StringComparison.Ordinal) >= 0,
                    "RefreshGuardians가 존재한다");
                // 스냅은 반드시 파티 피해(TickParty) **전에** — 순서가 어긋나면 한 프레임 늦는다.
                int ri = src.IndexOf("RefreshGuardians();", StringComparison.Ordinal);
                int tp = src.IndexOf("TickParty(dt);", StringComparison.Ordinal);
                Check(ri >= 0 && tp >= 0 && ri < tp, "RefreshGuardians가 TickParty보다 앞선다");
                Check(src.IndexOf("MobKindGuardian", StringComparison.Ordinal) >= 0,
                    "수호자 실루엣(MobKindGuardian)을 쓴다");
                Check(src.IndexOf("6 => Muted", StringComparison.Ordinal) >= 0,
                    "수호자 전용 색조(case 6 틴트)가 있다");
                Check(src.IndexOf("EliteGuardian.Blocked", StringComparison.Ordinal) >= 0,
                    "차단 시 오라 스냅을 건너뛴다(네거티브 코드 경로)");
            }

            _ = nameof(EliteGuardian.Multiplier);
            _ = nameof(EliteGuardian.AuraRadius);

            if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            EliteGuardian.ForceConfig = savedForce;
            Environment.SetEnvironmentVariable(EliteGuardian.EnvNo, no);

            if (_fail == 0) Debug.Log("[EliteGuardianSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EliteGuardianSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EliteGuardianSelfCheck] FAIL {_fail}건");
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
