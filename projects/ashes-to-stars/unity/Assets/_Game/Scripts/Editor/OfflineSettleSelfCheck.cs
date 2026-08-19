using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 오프라인 정산 감쇠(§18-14): 8h 100% · 8~12h 50% · 12h 초과 0(실효 상한 10h).
    /// 순수 곡선 + 광산 Tick 실소비 + QA_NO 네거티브를 본다.
    /// </summary>
    public static class OfflineSettleSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Offline Settle Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(OfflineSettle.EnvNo);
            Environment.SetEnvironmentVariable(OfflineSettle.EnvNo, null);

            const long H = 3600L;

            // ── 순수 감쇠 곡선(§18-14 값 그대로) ──
            Check(OfflineSettle.EffectiveSeconds(0) == 0, "경과 0 → 0");
            Check(OfflineSettle.EffectiveSeconds(H) == H, "1시간 → 1시간(100%)");
            Check(OfflineSettle.EffectiveSeconds(8 * H) == 8 * H, "8시간 → 8시간(100% 경계)");
            Check(OfflineSettle.EffectiveSeconds(10 * H) == 9 * H, "10시간 → 9시간(8h + 2h×50%)");
            Check(OfflineSettle.EffectiveSeconds(12 * H) == 10 * H, "12시간 → 10시간(8h + 4h×50%)");
            Check(OfflineSettle.EffectiveSeconds(24 * H) == OfflineSettle.MaxEffectiveSeconds,
                "24시간 → 실효 상한 10시간");
            Check(OfflineSettle.MaxEffectiveSeconds == 10 * H, "실효 상한 = 10시간(36000초)");

            // ── QA_NO 네거티브: 옛 전 구간 100% ──
            Environment.SetEnvironmentVariable(OfflineSettle.EnvNo, "1");
            Check(OfflineSettle.EffectiveSeconds(24 * H) == 24 * H, "QA_NO면 24시간 그대로(옛 100%)");
            Environment.SetEnvironmentVariable(OfflineSettle.EnvNo, null);

            // ── 광산 Tick 실소비: 24h 오프라인이면 실효 10h만 정산 ──
            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.TrySelectTier(0);
            long t1 = EstateMine.CopperPerHour();          // T1 = 25실버/h
            long baseUnix = 1_700_000_000L;
            EstateMine.NowUnix = () => baseUnix;
            EstateMine.Tick();                              // 기준점만
            EstateMine.NowUnix = () => baseUnix + 24 * H;
            long decayed = EstateMine.Tick();
            Check(decayed == t1 * 10, $"24h 오프라인 → 실효 10h만 정산 ({decayed}, 기대 {t1 * 10})");

            // 네거티브: QA_NO면 24h 전액
            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.TrySelectTier(0);
            Environment.SetEnvironmentVariable(OfflineSettle.EnvNo, "1");
            EstateMine.NowUnix = () => baseUnix;
            EstateMine.Tick();
            EstateMine.NowUnix = () => baseUnix + 24 * H;
            long full = EstateMine.Tick();
            Environment.SetEnvironmentVariable(OfflineSettle.EnvNo, null);
            Check(full == t1 * 24, $"QA_NO면 24h 전액 정산 ({full}, 기대 {t1 * 24})");
            Check(full > decayed, "감쇠가 실제로 정산량을 줄인다");

            // ── 소비처 확인: 광산 Tick이 실효 초를 읽는다 ──
            string mine = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateMine.cs"));
            Check(mine.Contains("OfflineSettle.EffectiveSeconds"),
                "광산 Tick이 OfflineSettle.EffectiveSeconds를 읽는다");

            _ = nameof(EstateMine.Tick);
            _ = nameof(OfflineSettle.EffectiveSeconds);

            // 정리
            Environment.SetEnvironmentVariable(OfflineSettle.EnvNo, no);
            EstateMine.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            GameState.ResetAll();
            WorldStar.ResetForTest();
            EstateMine.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[OfflineSettleSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("OfflineSettleSelfCheck FAIL " + _fail);
            }
            Debug.Log("[OfflineSettleSelfCheck] PASS\n" + _log);
        }
    }
}
