using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 「처형자」 — 후열 돌진 폭딜(✅ 오너 결정 2026-08-13).
    /// 순수 배율 함수(EliteExecutioner.DamageMul)와 BalanceConfig 소비, W3Party 소비 계약,
    /// QA_NO_ELITE_EXECUTIONER 네거티브를 검증한다. 수치는 원장 미확정이라 값 자체가 아니라
    /// **폭딜이 붙는가·차단하면 원복하는가·소비처가 실재하는가**를 본다.
    /// </summary>
    public static class EliteExecutionerSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Elite Executioner Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(EliteExecutioner.EnvNo);
            Environment.SetEnvironmentVariable(EliteExecutioner.EnvNo, null);
            var savedForce = EliteExecutioner.ForceConfig;
            EliteExecutioner.ForceConfig = null;

            // ── 순수 배율 함수 ─────────────────────────────
            float burst = 3.0f;
            // 처형자 명중 → 폭딜(≥1)
            Check(Mathf.Approximately(EliteExecutioner.DamageMul(true, burst), burst),
                "처형자 명중은 폭딜 배율(≥1)");
            // 잡몹 명중 → 1(무변)
            Check(Mathf.Approximately(EliteExecutioner.DamageMul(false, burst), 1f),
                "잡몹 명중은 배율 1(무변)");
            // 폭딜이 1 미만으로 넘어와도 1로 클램프(딜러 거울은 약화가 아니다)
            Check(Mathf.Approximately(EliteExecutioner.DamageMul(true, 0.5f), 1f),
                "1 미만 폭딜은 1로 클램프");

            // ── BalanceConfig 소비(ForceConfig) ─────────────
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            cfg.처형자폭딜배율 = 4.5f;
            cfg.처형자돌진속도배율 = 1.2f;
            EliteExecutioner.ForceConfig = cfg;
            Check(Mathf.Approximately(EliteExecutioner.BurstMul(), 4.5f), $"BurstMul 설정 소비 (실제 {EliteExecutioner.BurstMul()})");
            Check(Mathf.Approximately(EliteExecutioner.RushMul(), 1.2f), $"RushMul 설정 소비 (실제 {EliteExecutioner.RushMul()})");
            EliteExecutioner.ForceConfig = null;

            // 설정 없으면 기본값(원장 미확정)
            Check(Mathf.Approximately(EliteExecutioner.BurstMul(), EliteExecutioner.DefaultBurstMul),
                "설정 없으면 기본 폭딜 배율");
            Check(EliteExecutioner.DefaultBurstMul > 1f && EliteExecutioner.DefaultRushMul > 0f,
                "기본값은 실제로 폭딜(배율＞1)·돌진(＞0)");

            // ── 네거티브: QA_NO_ELITE_EXECUTIONER ────────────
            Environment.SetEnvironmentVariable(EliteExecutioner.EnvNo, "1");
            Check(EliteExecutioner.Blocked, "QA_NO_ELITE_EXECUTIONER 차단");
            Check(Mathf.Approximately(EliteExecutioner.BurstMul(), 1f), "차단하면 폭딜 배율 1(폭딜 없음)");
            Environment.SetEnvironmentVariable(EliteExecutioner.EnvNo, null);
            Check(!EliteExecutioner.Blocked, "기본은 켜짐");

            // ── W3Party 소비 계약(소스) ─────────────────────
            string w3 = FindSource("W3Party.cs");
            Check(w3 != null, "W3Party.cs 소스 발견");
            if (w3 != null)
            {
                string src = File.ReadAllText(w3);
                Check(src.IndexOf("? 9 :", StringComparison.Ordinal) >= 0,
                    "스폰 테이블에 처형자(kind 9)가 있다");
                // 저주(8)·군단장(7)·수호자(6) 스폰 순서를 깨지 않았는지 함께 본다(형제 SelfCheck 회귀 방지).
                Check(src.IndexOf("? 8 :", StringComparison.Ordinal) >= 0,
                    "스폰 테이블 저주술사(? 8 :)를 보존한다");
                Check(src.IndexOf("? 6 : 7", StringComparison.Ordinal) >= 0,
                    "스폰 테이블 군단장·수호자 순서(? 6 : 7)를 보존한다");
                Check(src.IndexOf("_mKind[i] == 9", StringComparison.Ordinal) >= 0,
                    "TickMobs에 처형자(kind 9) 분기가 있다");
                Check(src.IndexOf("EliteExecutioner.DamageMul", StringComparison.Ordinal) >= 0,
                    "근접 피해가 EliteExecutioner.DamageMul을 소비한다");
                Check(src.IndexOf("RefreshExecutioners", StringComparison.Ordinal) >= 0,
                    "RefreshExecutioners가 존재한다");
                // 스냅은 반드시 근접 피해(TickMobs) **전에** — 순서가 어긋나면 한 프레임 늦는다.
                int ri = src.IndexOf("RefreshExecutioners();", StringComparison.Ordinal);
                int tm = src.IndexOf("TickMobs(dt);", StringComparison.Ordinal);
                Check(ri >= 0 && tm >= 0 && ri < tm, "RefreshExecutioners가 TickMobs보다 앞선다");
                // 근접 명중이 폭딜 배율을 곱하는지 — _execBurst 소비 확인.
                Check(src.IndexOf("_execBurst", StringComparison.Ordinal) >= 0,
                    "근접 명중이 폭딜 배율(_execBurst)을 소비한다");
                // 이동 경로가 돌진 배율을 곱하는지 — _execRush 소비 확인.
                Check(src.IndexOf("_execRush", StringComparison.Ordinal) >= 0,
                    "이동 경로(TickMobs)가 돌진 배율(_execRush)을 소비한다");
                // 후열 저격을 원거리형과 공유하는지 — PickBackline 배선 확인.
                Check(src.IndexOf("_mKind[i] == 9) ? PickBackline", StringComparison.Ordinal) >= 0,
                    "처형자는 후열 저격(PickBackline)을 쓴다");
                Check(src.IndexOf("MobKindBerserker", StringComparison.Ordinal) >= 0,
                    "처형자 실루엣(MobKindBerserker)을 쓴다");
                Check(src.IndexOf("9 => Muted", StringComparison.Ordinal) >= 0,
                    "처형자 전용 색조(case 9 틴트)가 있다");
                Check(src.IndexOf("EliteExecutioner.Blocked", StringComparison.Ordinal) >= 0,
                    "차단 시 폭딜 스냅을 건너뛴다(네거티브 코드 경로)");
            }

            _ = nameof(EliteExecutioner.DamageMul);
            _ = nameof(EliteExecutioner.BurstMul);

            if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            EliteExecutioner.ForceConfig = savedForce;
            Environment.SetEnvironmentVariable(EliteExecutioner.EnvNo, no);

            if (_fail == 0) Debug.Log("[EliteExecutionerSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EliteExecutionerSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EliteExecutionerSelfCheck] FAIL {_fail}건");
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
