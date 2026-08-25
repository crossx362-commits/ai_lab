using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>파티 편성 헤더·삭제 카드 상태는 한 줄. QA_NO면 옛 긴 줄(§16).</summary>
    public static class PartyHudCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Party Hud Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(PartyHudCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(PartyHudCap.EnvNo);
            Environment.SetEnvironmentVariable(PartyHudCap.EnvShow, null);
            Environment.SetEnvironmentVariable(PartyHudCap.EnvNo, null);

            GameState.ResetAll();
            PartyState.ResetForTest();
            PartyHudCap.ResetForTest();

            Check(!PartyHudCap.Blocked, "기본은 켜짐");
            string cap = PartyHudCap.Caption();
            Check(cap == $"편성 {PartyState.Slots.Count}/{PartyState.MaxSlots} · 1번=탱 · 부활초 {LifeSystem.GetRevivePotions()}/3",
                $"부제 (실제 {cap})");
            Check(PartyHudCap.CaptionFits(cap),
                $"길이 {PartyHudCap.RuneCount(cap)} ≤ {PartyHudCap.CaptionMaxRunes}");
            Check(!PartyHudCap.CaptionFits(PartyHudCap.Old()),
                $"옛 줄은 안 맞음 (길이 {PartyHudCap.RuneCount(PartyHudCap.Old())})");
            Check(PartyHudCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {PartyHudCap.Line()})");

            Check(PartyHudCap.Deleted() == PartyHudCap.DeletedShort,
                $"삭제 상태 (실제 {PartyHudCap.Deleted()})");
            Check(PartyHudCap.CardStatusFits(PartyHudCap.Deleted()),
                $"삭제 길이 {PartyHudCap.RuneCount(PartyHudCap.Deleted())} ≤ {PartyHudCap.CardStatusMaxRunes}");
            Check(!PartyHudCap.CardStatusFits(PartyHudCap.OldDeleted)
                  && PartyHudCap.OldDeletedStatus() == PartyHudCap.OldDeleted,
                $"옛 삭제는 긴 줄 (길이 {PartyHudCap.RuneCount(PartyHudCap.OldDeleted)})");
            Check(PartyHudCap.DeletedLine().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"삭제 줄 (실제 {PartyHudCap.DeletedLine()})");

            Environment.SetEnvironmentVariable(PartyHudCap.EnvNo, "1");
            Check(PartyHudCap.Blocked, "QA_NO");
            Check(PartyHudCap.Caption() == PartyHudCap.Old()
                  && !PartyHudCap.CaptionFits(PartyHudCap.Caption()),
                $"QA_NO 옛 긴 줄 (실제 {PartyHudCap.Caption()})");
            Check(PartyHudCap.Line().IndexOf("잘린다", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {PartyHudCap.Line()})");
            Check(PartyHudCap.Deleted() == PartyHudCap.OldDeleted
                  && !PartyHudCap.CardStatusFits(PartyHudCap.Deleted()),
                $"QA_NO 옛 삭제 (실제 {PartyHudCap.Deleted()})");
            Check(PartyHudCap.DeletedLine().IndexOf("하트", StringComparison.Ordinal) >= 0,
                $"QA_NO 삭제 줄 (실제 {PartyHudCap.DeletedLine()})");
            Environment.SetEnvironmentVariable(PartyHudCap.EnvNo, null);

            PartyHudCap.ResetForTest();
            Environment.SetEnvironmentVariable(PartyHudCap.EnvShow, "1");
            Check(PartyHudCap.ShowQa, "시드 ShowQa");
            PartyHudCap.SeedQaIfRequested();
            Check(PartyHudCap.CaptionFits(PartyHudCap.Caption()),
                $"시드 부제 (실제 {PartyHudCap.Caption()})");
            Check(PartyHudCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"시드 자막 (실제 {PartyHudCap.Line()})");
            Environment.SetEnvironmentVariable(PartyHudCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string partySrc = File.ReadAllText(Path.Combine(runtime, "PartyScreen.cs"));
            Check(partySrc.IndexOf("PartyHudCap.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && partySrc.IndexOf("PartyHudCap.Line", StringComparison.Ordinal) >= 0
                  && partySrc.IndexOf("PartyHudCap.Caption", StringComparison.Ordinal) >= 0
                  && partySrc.IndexOf("PartyHudCap.Deleted", StringComparison.Ordinal) >= 0,
                "파티가 Caption·Line·Deleted·시드를 읽는다");
            Check(partySrc.IndexOf("최대 {PartyState.MaxSlots}인(§9)", StringComparison.Ordinal) < 0
                  && partySrc.IndexOf("§10-4 진형", StringComparison.Ordinal) < 0
                  && partySrc.IndexOf("PartyHudCap.Old", StringComparison.Ordinal) < 0,
                "헤더가 긴 옛 줄을 안 붙인다");
            Check(partySrc.IndexOf("환생석으로만 복구", StringComparison.Ordinal) < 0
                  && partySrc.IndexOf("PartyHudCap.OldDeleted", StringComparison.Ordinal) < 0,
                "카드가 긴 삭제 줄을 안 붙인다");
            Check(partySrc.IndexOf("출전 불가(§", StringComparison.Ordinal) < 0
                  && partySrc.IndexOf("영구 삭제(§", StringComparison.Ordinal) < 0
                  && partySrc.IndexOf("상한이다(§", StringComparison.Ordinal) < 0,
                "플레이어 상태 문구에 내부 절 번호를 노출하지 않는다");
            Check(partySrc.IndexOf("마지막 목숨 — 죽으면 영구 삭제\"", StringComparison.Ordinal) >= 0
                  && partySrc.IndexOf("영지 수비대에서 해임해야 출전한다\"", StringComparison.Ordinal) >= 0,
                "절 번호를 걷어도 위험과 해결 행동은 남긴다");

            _ = nameof(PartyHudCap.Caption);
            _ = nameof(PartyHudCap.Line);
            _ = nameof(PartyHudCap.Deleted);
            _ = nameof(PartyHudCap.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(PartyHudCap.EnvShow, show);
            Environment.SetEnvironmentVariable(PartyHudCap.EnvNo, no);
            PartyHudCap.ResetForTest();
            PartyState.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[PartyHudCapSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[PartyHudCapSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[PartyHudCapSelfCheck] FAIL {_fail}건");
        }
    }
}
