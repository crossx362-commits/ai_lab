using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>월드맵 성계·랭킹·침략 도크 부제는 한 줄. QA_NO면 옛 긴 줄(§16).</summary>
    public static class WorldMapDockCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/World Map Dock Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(WorldMapDockCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(WorldMapDockCap.EnvNo);
            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvShow, null);
            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvNo, null);

            GameState.ResetAll();
            InvasionState.ResetForTest();
            InvasionApproach.ResetForTest();
            EstateStore.ResetForTest();
            Honor.ResetForTest();
            WorldMapDockCap.ResetForTest();

            Check(!WorldMapDockCap.Blocked, "기본은 켜짐");
            Check(GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor, "기본 층은 잠김");
            Check(WorldMapDockCap.IsLocked, "1층은 잠김");
            Check(WorldMapDockCap.Caption() == WorldMapDockCap.LockFloor,
                $"잠김 부제 (실제 {WorldMapDockCap.Caption()})");
            Check(WorldMapDockCap.CaptionFits(WorldMapDockCap.Caption()),
                $"잠김 길이 {WorldMapDockCap.RuneCount(WorldMapDockCap.Caption())} ≤ {WorldMapDockCap.CaptionMaxRunes}");
            string locked = "잠김 — " + WorldMapDockCap.Caption();
            Check(WorldMapDockCap.CaptionFits(locked),
                $"잠김 접두 {WorldMapDockCap.RuneCount(locked)} ≤ {WorldMapDockCap.CaptionMaxRunes}");
            Check(!WorldMapDockCap.CaptionFits(WorldMapDockCap.OldLock()),
                $"옛 잠금 줄은 안 맞음 (길이 {WorldMapDockCap.RuneCount(WorldMapDockCap.OldLock())})");

            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            InvasionState.ResetForTest();
            InvasionApproach.ResetForTest();
            Check(!WorldMapDockCap.IsLocked, "30층은 열림");
            string open = WorldMapDockCap.Open();
            string expect = $"{InvasionApproach.Side} {InvasionApproach.Path()}{WorldMapDockCap.OpenTail}";
            Check(open == expect, $"열린 부제 (실제 {open})");
            Check(WorldMapDockCap.Caption() == open, "열린 Caption=Open");
            Check(WorldMapDockCap.CaptionFits(open),
                $"열린 길이 {WorldMapDockCap.RuneCount(open)} ≤ {WorldMapDockCap.CaptionMaxRunes}");
            Check(!WorldMapDockCap.CaptionFits(WorldMapDockCap.OldOpen()),
                $"옛 열린 줄은 안 맞음 (길이 {WorldMapDockCap.RuneCount(WorldMapDockCap.OldOpen())})");
            Check(WorldMapDockCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {WorldMapDockCap.Line()})");

            Check(WorldMapDockCap.Star() == WorldMapDockCap.StarCap,
                $"성계 부제 (실제 {WorldMapDockCap.Star()})");
            Check(WorldMapDockCap.Rank() == WorldMapDockCap.RankCap,
                $"랭킹 부제 (실제 {WorldMapDockCap.Rank()})");
            string starLocked = "잠김 — " + WorldMapDockCap.Star();
            string rankLocked = "잠김 — " + WorldMapDockCap.Rank();
            Check(WorldMapDockCap.CaptionFits(starLocked),
                $"성계 접두 {WorldMapDockCap.RuneCount(starLocked)} ≤ {WorldMapDockCap.CaptionMaxRunes}");
            Check(WorldMapDockCap.CaptionFits(rankLocked),
                $"랭킹 접두 {WorldMapDockCap.RuneCount(rankLocked)} ≤ {WorldMapDockCap.CaptionMaxRunes}");
            Check(!WorldMapDockCap.CaptionFits(WorldMapDockCap.OldStar)
                  && !WorldMapDockCap.CaptionFits(WorldMapDockCap.OldRank),
                $"옛 성계·랭킹 줄은 안 맞음 (성계 {WorldMapDockCap.RuneCount(WorldMapDockCap.OldStar)} 랭킹 {WorldMapDockCap.RuneCount(WorldMapDockCap.OldRank)})");

            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvNo, "1");
            Check(WorldMapDockCap.Blocked, "QA_NO");
            Check(WorldMapDockCap.Caption() == WorldMapDockCap.OldOpen()
                  && !WorldMapDockCap.CaptionFits(WorldMapDockCap.Caption()),
                $"QA_NO 옛 긴 줄 (실제 {WorldMapDockCap.Caption()})");
            Check(WorldMapDockCap.Star() == WorldMapDockCap.OldStar
                  && WorldMapDockCap.Rank() == WorldMapDockCap.OldRank,
                $"QA_NO 옛 성계·랭킹 (성계 {WorldMapDockCap.Star()})");
            Check(WorldMapDockCap.Line().IndexOf("두 줄", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {WorldMapDockCap.Line()})");
            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvNo, null);

            GameState.ResetAll();
            InvasionState.ResetForTest();
            InvasionApproach.ResetForTest();
            WorldMapDockCap.ResetForTest();
            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvShow, "1");
            Check(WorldMapDockCap.ShowQa, "시드 ShowQa");
            WorldMapDockCap.SeedQaIfRequested();
            Check(GameState.TowerFloor == WorldMapScreen.InvasionUnlockFloor,
                $"시드 층 (실제 {GameState.TowerFloor})");
            Check(!WorldMapDockCap.IsLocked, "시드는 열림");
            Check(WorldMapDockCap.CaptionFits(WorldMapDockCap.Caption()),
                $"시드 부제 (실제 {WorldMapDockCap.Caption()})");
            Check(WorldMapDockCap.Line().IndexOf("한 줄", StringComparison.Ordinal) >= 0,
                $"시드 자막 (실제 {WorldMapDockCap.Line()})");
            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            Check(mapSrc.IndexOf("WorldMapDockCap.SeedQaIfRequested", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("WorldMapDockCap.Line", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("WorldMapDockCap.Caption", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("WorldMapDockCap.IsLocked", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("WorldMapDockCap.Star", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("WorldMapDockCap.Rank", StringComparison.Ordinal) >= 0,
                "월드맵이 시드·줄·Caption·IsLocked·Star·Rank를 읽는다");
            Check(mapSrc.IndexOf("InvasionApproach.Line()} · 출정", StringComparison.Ordinal) < 0
                  && mapSrc.IndexOf("진입 {EstateGrid.InvaderSide()}", StringComparison.Ordinal) < 0
                  && mapSrc.IndexOf("성계 시스템 미구현", StringComparison.Ordinal) < 0
                  && mapSrc.IndexOf("랭킹 서버 없음", StringComparison.Ordinal) < 0,
                "도크가 옛 긴 줄을 안 붙인다");

            _ = nameof(WorldMapDockCap.Caption);
            _ = nameof(WorldMapDockCap.Open);
            _ = nameof(WorldMapDockCap.Lock);
            _ = nameof(WorldMapDockCap.Star);
            _ = nameof(WorldMapDockCap.Rank);
            _ = nameof(WorldMapDockCap.Line);
            _ = nameof(WorldMapDockCap.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvShow, show);
            Environment.SetEnvironmentVariable(WorldMapDockCap.EnvNo, no);
            WorldMapDockCap.ResetForTest();
            InvasionApproach.ResetForTest();
            InvasionState.ResetForTest();
            EstateStore.ResetForTest();
            Honor.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[WorldMapDockCapSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[WorldMapDockCapSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[WorldMapDockCapSelfCheck] FAIL {_fail}건");
        }
    }
}
