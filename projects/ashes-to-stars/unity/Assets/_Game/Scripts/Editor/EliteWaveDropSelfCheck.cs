using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 EliteDrop.FieldKills → 다음 웨이브 드랍 배율 소비처.
    /// QA_NO_ELITE_WAVE_DROP면 옛 ×1·가죽 1장.
    /// </summary>
    public static class EliteWaveDropSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Elite Wave Drop Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EliteWaveDrop.EnvShow);
            string no = Environment.GetEnvironmentVariable(EliteWaveDrop.EnvNo);
            string dropNo = Environment.GetEnvironmentVariable(EliteDrop.EnvNo);
            Environment.SetEnvironmentVariable(EliteWaveDrop.EnvShow, null);
            Environment.SetEnvironmentVariable(EliteWaveDrop.EnvNo, null);
            Environment.SetEnvironmentVariable(EliteDrop.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EliteDrop.ResetForTest();
            EliteWaveDrop.ResetForTest();
            _ = LifeSystem.GetCharacters();

            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
            Check(cfg != null && Mathf.Approximately(cfg.정예처치드랍배율, 0.25f),
                $"BalanceConfig.정예처치드랍배율 기본 0.25 (실제 {cfg?.정예처치드랍배율})");
            Check(cfg != null && Mathf.Approximately(cfg.정예처치드랍상한, 2f),
                $"BalanceConfig.정예처치드랍상한 기본 2 (실제 {cfg?.정예처치드랍상한})");
            Check(!EliteWaveDrop.Blocked, "기본은 켜짐");
            UnityEngine.Object.DestroyImmediate(cfg);

            EliteWaveDrop.BeginWave();
            Check(EliteDrop.FieldKills == 0, "FieldKills 기본 0");
            Check(Mathf.Approximately(EliteWaveDrop.Mul(), 1f),
                $"FieldKills 0 → ×1 (실제 {EliteWaveDrop.Mul()})");
            Check(Economy.FieldHuntHideCount() == 1, "0이면 가죽 1장(옛)");

            EliteDrop.NoteFieldKill();
            Check(EliteDrop.FieldKills >= 1, $"처치 후 FieldKills ≥1 (실제 {EliteDrop.FieldKills})");
            Check(Mathf.Approximately(EliteWaveDrop.Mul(), 1f),
                "이번 웨이브는 아직 ×1 — 다음 웨이브부터");
            Check(Economy.FieldHuntHideCount() == 1, "이번 웨이브 가죽은 옛 1장");

            EliteWaveDrop.BeginWave();
            Check(Mathf.Approximately(EliteWaveDrop.Mul(), 1.25f),
                $"다음 웨이브 FieldKills 1 → ×1.25 (실제 {EliteWaveDrop.Mul()})");
            Check(Economy.FieldHuntHideCount() == 1,
                "기본 0.25면 반올림해도 가죽 1장");

            var one = ScriptableObject.CreateInstance<BalanceConfig>();
            one.정예처치드랍배율 = 1f;
            one.정예처치드랍상한 = 2f;
            EliteWaveDrop.ForceConfig = one;
            Check(Mathf.Approximately(EliteWaveDrop.PerKill(), 1f),
                "ForceConfig가 정예처치드랍배율을 읽는다");
            Check(Mathf.Approximately(EliteWaveDrop.Mul(), 2f),
                $"1킬·가산 1 → ×2 (실제 {EliteWaveDrop.Mul()})");
            Check(Economy.FieldHuntHideCount() == 2,
                $"FieldKills ≥1이면 다음 웨이브 가죽이 배율을 읽는다 (실제 {Economy.FieldHuntHideCount()})");

            EliteDrop.NoteFieldKill();
            EliteDrop.NoteFieldKill();
            EliteDrop.NoteFieldKill();
            EliteWaveDrop.BeginWave();
            Check(EliteDrop.FieldKills >= 4, $"누적 ≥4 (실제 {EliteDrop.FieldKills})");
            Check(Mathf.Approximately(EliteWaveDrop.Mul(), 2f),
                $"상한 2로 잘린다 (실제 {EliteWaveDrop.Mul()})");
            Check(Economy.FieldHuntHideCount() == 2, "상한이어도 가죽 2장");
            EliteWaveDrop.ForceConfig = null;
            UnityEngine.Object.DestroyImmediate(one);
            EliteWaveDrop.ResetForTest();

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EliteDrop.ResetForTest();
            EliteWaveDrop.ResetForTest();
            _ = LifeSystem.GetCharacters();
            EliteDrop.NoteFieldKill();
            EliteWaveDrop.BeginWave();
            Environment.SetEnvironmentVariable(EliteWaveDrop.EnvNo, "1");
            Check(EliteWaveDrop.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(EliteWaveDrop.Mul(), 1f), "QA_NO면 배율 ×1(옛)");
            Check(Economy.FieldHuntHideCount() == 1, "QA_NO면 가죽 1장(옛 드랍)");
            Check(EliteWaveDrop.Line() == "", "차단하면 배율 줄 없음(옛 화면)");
            Environment.SetEnvironmentVariable(EliteWaveDrop.EnvNo, null);
            EliteWaveDrop.ResetForTest();
            EliteWaveDrop.BeginWave();
            Check(!EliteWaveDrop.Blocked && Mathf.Approximately(EliteWaveDrop.Mul(), 1.25f),
                "차단을 풀면 다시 다음 웨이브 배율");

            Environment.SetEnvironmentVariable(EliteWaveDrop.EnvShow, "1");
            EliteWaveDrop.ResetForTest();
            EliteWaveDrop.SeedQaIfRequested();
            Check(EliteWaveDrop.ShowQa, "시드 ShowQa");
            Check(EliteWaveDrop.Line().Contains("§10-2"),
                $"시드 줄 (실제 {EliteWaveDrop.Line()})");
            Environment.SetEnvironmentVariable(EliteWaveDrop.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string dropSrc = File.ReadAllText(Path.Combine(runtime, "EliteWaveDrop.cs"));
            Check(dropSrc.Contains("EliteDrop.FieldKills"),
                "EliteWaveDrop.BeginWave가 FieldKills를 읽는다");
            Check(dropSrc.Contains("정예처치드랍배율"),
                "EliteWaveDrop가 BalanceConfig.정예처치드랍배율을 읽는다");
            string ecoSrc = File.ReadAllText(Path.Combine(runtime, "Economy.cs"));
            Check(ecoSrc.Contains("EliteWaveDrop.Mul"),
                "FieldHuntHideCount가 EliteWaveDrop.Mul을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            string battleSrc = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            Check(battleSrc.Contains("EliteWaveDrop.BeginWave"),
                "BattleScreen이 잡몹 웨이브 시작에 BeginWave를 건다");
            string w3Src = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/W3Party.cs"));
            Check(w3Src.IndexOf("EliteWaveDrop", StringComparison.Ordinal) < 0,
                "W3Party를 부르지 않는다(선행 훅 NoteFieldKill만)");

            _ = nameof(EliteWaveDrop.Mul);
            _ = nameof(EliteWaveDrop.BeginWave);
            _ = nameof(EliteWaveDrop.Line);
            _ = nameof(EliteWaveDrop.SeedQaIfRequested);
            _ = nameof(EliteDrop.FieldKills);
            _ = nameof(Economy.FieldHuntHideCount);
            _ = nameof(BalanceConfig.정예처치드랍배율);
            _ = nameof(BalanceConfig.정예처치드랍상한);

            Environment.SetEnvironmentVariable(EliteWaveDrop.EnvShow, show);
            Environment.SetEnvironmentVariable(EliteWaveDrop.EnvNo, no);
            Environment.SetEnvironmentVariable(EliteDrop.EnvNo, dropNo);
            EliteWaveDrop.ResetForTest();
            EliteDrop.ResetForTest();
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "elite_wave_drop_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS EliteWaveDropSelfCheck" : "FAIL EliteWaveDropSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[EliteWaveDropSelfCheck] PASS → " + path);
            else Debug.LogError("[EliteWaveDropSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[EliteWaveDropSelfCheck] FAIL {_fail}건");
        }
    }
}
