using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>무제한 소지품은 개수만. QA_NO면 옛 상한 숫자(§18-4).</summary>
    public static class BagTextFmtSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Bag Text Fmt Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(BagTextFmt.EnvShow);
            string no = Environment.GetEnvironmentVariable(BagTextFmt.EnvNo);
            Environment.SetEnvironmentVariable(BagTextFmt.EnvShow, null);
            Environment.SetEnvironmentVariable(BagTextFmt.EnvNo, null);

            GameState.ResetAll();
            BagTextFmt.ResetForTest();

            Check(!BagTextFmt.Blocked, "기본은 켜짐");
            Check(BagTextFmt.Unlimited(int.MaxValue), "int.MaxValue는 무제한");
            Check(!BagTextFmt.Unlimited(3), "부활초 3은 상한");
            Check(!BagTextFmt.Unlimited(5), "두루마리 5는 상한");

            Check(GameState.Gain(Economy.LifeItem.RebornStone, 1), "환생석 1");
            string stone = GameState.BagText();
            Check(stone.IndexOf("환생석 1", StringComparison.Ordinal) >= 0
                  && stone.IndexOf("2147483647", StringComparison.Ordinal) < 0
                  && stone.IndexOf("/2147483647", StringComparison.Ordinal) < 0,
                $"환생석 개수만 (실제 {stone})");

            Check(GameState.Gain(Economy.LifeItem.RevivalTea, 1), "부활초 1");
            string both = GameState.BagText();
            Check(both.IndexOf("부활초 1/3", StringComparison.Ordinal) >= 0
                  && both.IndexOf("환생석 1", StringComparison.Ordinal) >= 0
                  && both.IndexOf("2147483647", StringComparison.Ordinal) < 0,
                $"부활초는 상한, 환생석은 개수 (실제 {both})");

            Check(GameState.Gain(Economy.LifeItem.CraftHide, 1), "가죽 1");
            string hide = GameState.BagText();
            Check(hide.IndexOf("사냥 가죽 1", StringComparison.Ordinal) >= 0
                  && hide.IndexOf("사냥 가죽 1/", StringComparison.Ordinal) < 0,
                $"가죽은 개수만 (실제 {hide})");

            Check(BagTextFmt.Format(Economy.LifeItem.RebornStone, 1) == "환생석 1",
                "Format 환생석 1");
            Check(BagTextFmt.Format(Economy.LifeItem.RevivalTea, 2) == "부활초 2/3",
                "Format 부활초 2/3");
            Check(BagTextFmt.Format(Economy.LifeItem.RebornStone, 0) == "",
                "0개는 빈 줄");

            Environment.SetEnvironmentVariable(BagTextFmt.EnvNo, "1");
            Check(BagTextFmt.Blocked, "QA_NO");
            string leaked = GameState.BagText();
            Check(leaked.IndexOf("2147483647", StringComparison.Ordinal) >= 0
                  && leaked.IndexOf("환생석 1/2147483647", StringComparison.Ordinal) >= 0,
                $"QA_NO면 옛 상한 (실제 {leaked})");
            Check(BagTextFmt.Line().IndexOf("상한 숫자", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {BagTextFmt.Line()})");
            Environment.SetEnvironmentVariable(BagTextFmt.EnvNo, null);

            GameState.ResetAll();
            BagTextFmt.ResetForTest();
            Environment.SetEnvironmentVariable(BagTextFmt.EnvShow, "1");
            BagTextFmt.SeedQaIfRequested();
            Check(BagTextFmt.ShowQa, "시드 ShowQa");
            Check(GameState.Bag.GetCount(Economy.LifeItem.RebornStone) >= 1
                  && GameState.Bag.GetCount(Economy.LifeItem.RevivalTea) >= 1,
                "시드 환생석·부활초");
            RaidSpawn.ForceSpawnForTest(1);
            Check(RaidSpawn.Active, "시드 전 레이드");
            BagTextFmt.ResetForTest();
            BagTextFmt.SeedQaIfRequested();
            Check(!RaidSpawn.Active, "시드가 레이드를 걷어 지갑 칸을 연다");
            string seeded = GameState.BagText();
            Check(seeded.IndexOf("환생석 1", StringComparison.Ordinal) >= 0
                  && seeded.IndexOf("부활초 1/3", StringComparison.Ordinal) >= 0
                  && seeded.IndexOf("2147483647", StringComparison.Ordinal) < 0,
                $"시드 줄 (실제 {seeded})");
            Check(BagTextFmt.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"시드 자막 (실제 {BagTextFmt.Line()})");
            string cap = BagTextFmt.Caption();
            Check(cap == "부활초 1/3 · 환생석 1",
                $"시드 Caption (실제 {cap})");
            Check(BagTextFmt.CaptionFits(cap),
                $"시드 CaptionFits {BagTextFmt.RuneCount(cap)} ≤ {BagTextFmt.CaptionMaxRunes}");
            Check(cap.IndexOf("사냥 가죽", StringComparison.Ordinal) < 0
                  && cap.IndexOf("필드 사냥은 무료", StringComparison.Ordinal) < 0
                  && cap.IndexOf("귀환의 두루마리", StringComparison.Ordinal) < 0,
                "Caption은 목숨 2종만");
            Environment.SetEnvironmentVariable(BagTextFmt.EnvShow, null);

            Check(GameState.Gain(Economy.LifeItem.CraftHide, 1), "가죽 1");
            Check(GameState.Gain(Economy.LifeItem.EnhanceStone, 1), "석 1");
            Check(BagTextFmt.Caption() == "부활초 1/3 · 환생석 1",
                "재료가 있어도 Caption은 목숨 2종");

            Environment.SetEnvironmentVariable(BagTextFmt.EnvNo, "1");
            string oldCap = BagTextFmt.Caption();
            Check(oldCap.IndexOf("필드 사냥은 무료", StringComparison.Ordinal) >= 0
                  && oldCap.IndexOf("사냥 가죽", StringComparison.Ordinal) >= 0
                  && !BagTextFmt.CaptionFits(oldCap),
                $"QA_NO면 옛 긴 부제 (실제 {oldCap})");
            Environment.SetEnvironmentVariable(BagTextFmt.EnvNo, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string stateSrc = File.ReadAllText(Path.Combine(runtime, "GameState.cs"));
            string fieldSrc = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            Check(stateSrc.IndexOf("BagTextFmt.Format", StringComparison.Ordinal) >= 0,
                "BagText가 Format을 읽는다");
            Check(fieldSrc.IndexOf("BagTextFmt.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("BagTextFmt.Line", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("BagTextFmt.Caption", StringComparison.Ordinal) >= 0,
                "필드가 시드·줄·Caption을 읽는다");
            Check(fieldSrc.IndexOf("BagText()} · 필드", StringComparison.Ordinal) < 0
                  && fieldSrc.IndexOf("필드 사냥은 무료", StringComparison.Ordinal) < 0,
                "도크가 긴 BagText를 안 붙인다");
            Check(fieldSrc.IndexOf("GameState.BagText()", StringComparison.Ordinal) >= 0,
                "헤더는 전체 BagText를 읽는다");

            _ = nameof(BagTextFmt.Format);
            _ = nameof(BagTextFmt.Line);
            _ = nameof(BagTextFmt.Caption);
            _ = nameof(BagTextFmt.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(BagTextFmt.EnvShow, show);
            Environment.SetEnvironmentVariable(BagTextFmt.EnvNo, no);
            BagTextFmt.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[BagTextFmtSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BagTextFmtSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[BagTextFmtSelfCheck] FAIL {_fail}건");
        }
    }
}
