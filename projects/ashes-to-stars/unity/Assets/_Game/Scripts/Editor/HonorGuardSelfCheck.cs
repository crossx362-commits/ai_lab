using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>수비 성공 +20. 수비대가 있으면 막고, 없으면 0. QA_NO면 옛 침략 없음(§18-13).</summary>
    public static class HonorGuardSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        static void ResetWorld()
        {
            GameState.ResetAll();
            SoftCap.ResetForTest();
            LifeSystem.ResetAll();
            Honor.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            InboundRaid.ResetForTest();
            EstateDefense.ResetForTest();
            EstateBuild.ResetForTest();
            WorldStar.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            InvasionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        [MenuItem("Ashes to Stars/QA/Honor Guard Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Honor.EnvShowGuard);
            string no = Environment.GetEnvironmentVariable(Honor.EnvNoGuard);
            string honorShow = Environment.GetEnvironmentVariable(Honor.EnvShow);
            string honorNo = Environment.GetEnvironmentVariable(Honor.EnvNo);
            Environment.SetEnvironmentVariable(Honor.EnvShowGuard, null);
            Environment.SetEnvironmentVariable(Honor.EnvNoGuard, null);
            Environment.SetEnvironmentVariable(Honor.EnvShow, null);
            Environment.SetEnvironmentVariable(Honor.EnvNo, null);

            ResetWorld();

            Check(Honor.Guard == 20, "수비 +20");
            Check(Honor.GuardCap == "수비 +20", "카드 문구");
            Check(Honor.ApplyGuard(true) == 20, "Apply 성공 20");
            Check(Honor.Points == 20, $"잔액 20 (실제 {Honor.Points})");
            Check(Honor.LastGain == 20, "LastGain 20");
            Check(Honor.GuardLine().Contains("+20") && Honor.GuardLine().Contains("§18-13"),
                $"문구 (실제 {Honor.GuardLine()})");

            Honor.ForgetInMemoryForTest();
            Check(Honor.Points == 20, "재기동 뒤에도 20");

            Check(Honor.ApplyGuard(false) == 0, "실패 Apply=0");
            Check(Honor.Points == 20, $"실패해도 잔액 20 (실제 {Honor.Points})");
            Check(Honor.LastGain == 0, "실패 LastGain 0");

            Environment.SetEnvironmentVariable(Honor.EnvNoGuard, "1");
            Check(Honor.GuardBlocked, "QA_NO_HONOR_GUARD");
            Check(Honor.ApplyGuard(true) == 0, "차단하면 성공도 0");
            Check(Honor.Points == 20, $"차단 중 잔액 불변 (실제 {Honor.Points})");
            Check(Honor.GuardLine().Contains("없음"),
                $"차단 문구 (실제 {Honor.GuardLine()})");
            Check(WorldMapDockCap.Defense() == WorldMapDockCap.DefenseCap,
                $"차단 카드는 침략 없음 (실제 {WorldMapDockCap.Defense()})");
            Environment.SetEnvironmentVariable(Honor.EnvNoGuard, null);

            Check(Honor.ApplyGuard(true) == 20, "차단을 풀면 다시 20");
            Check(Honor.Points == 40, $"두 번째 성공 40 (실제 {Honor.Points})");
            Check(WorldMapDockCap.Defense() == Honor.GuardCap,
                $"카드 수비 +20 (실제 {WorldMapDockCap.Defense()})");

            ResetWorld();
            GameState.SetTowerFloorForTest(1);
            Check(!InboundRaid.OfferIfDue(), "1층은 습격을 안 건다");
            Check(!InboundRaid.Pending, "1층 대기 없음");

            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            Check(InboundRaid.OfferIfDue(), "30층 빈 수비는 습격을 건다");
            Check(InboundRaid.Pending, "대기 중");
            Check(!InboundRaid.HeldNow(), "수비 없음");
            Check(InboundRaid.Settle() == 0, "빈 수비 정산 0");
            Check(!InboundRaid.Pending, "정산 뒤 대기 없음");
            Check(Honor.Points == 0, "실패 명예 0");
            Check(InvasionState.ShieldActive, "실패면 보호막");
            Check(!InboundRaid.OfferIfDue(), "보호막 중엔 안 건다");

            ResetWorld();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            var roster = LifeSystem.GetCharacters();
            Check(roster.Count > 0, "로스터");
            Check(DefenseState.Toggle(0), "수비 1명");
            Check(InboundRaid.HeldNow(), "수비 있음");
            Check(InboundRaid.Queue(), "습격");
            Check(InboundRaid.Settle() == 20, "막으면 +20");
            Check(Honor.Points == 20, $"성공 잔액 20 (실제 {Honor.Points})");
            Check(!InvasionState.ShieldActive, "성공은 보호막을 안 건다");
            Check(!InboundRaid.OfferIfDue(), "12시간 창 안엔 안 건다");

            InboundRaid.ForgetInMemoryForTest();
            Check(!InboundRaid.Pending, "재기동 뒤 대기는 꺼져 있다");
            Check(InboundRaid.LastUnix > 0, "재기동 뒤 시각 유지");

            InvasionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                + InvasionState.GuardSeconds + 1;
            Check(InboundRaid.OfferIfDue(), "창이 끝나면 다시 건다");
            Check(InboundRaid.Pending, "창 후 대기");
            InvasionState.NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            ResetWorld();
            Environment.SetEnvironmentVariable(Honor.EnvShowGuard, "1");
            Honor.SeedGuardQaIfRequested();
            Check(Honor.ShowGuardQa, "시드 ShowQa");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor, "시드 30층");
            Check(DefenseState.Count >= 1, $"시드 수비 (실제 {DefenseState.Count})");
            Check(Honor.Points == 20, $"시드 정산 20 (실제 {Honor.Points})");
            Check(Honor.LastGain == 20, "시드 LastGain 20");
            Check(!InboundRaid.Pending, "시드는 정산까지 끝낸다");
            Check(Honor.GuardLine().Contains("+20"),
                $"시드 문구 (실제 {Honor.GuardLine()})");
            Check(WorldMapDockCap.Defense() == Honor.GuardCap, "시드 카드");
            Environment.SetEnvironmentVariable(Honor.EnvShowGuard, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string honorSrc = File.ReadAllText(Path.Combine(runtime, "Honor.cs"));
            string raidSrc = File.ReadAllText(Path.Combine(runtime, "InboundRaid.cs"));
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            string dockSrc = File.ReadAllText(Path.Combine(runtime, "WorldMapDockCap.cs"));
            Check(raidSrc.IndexOf("Honor.ApplyGuard", StringComparison.Ordinal) >= 0,
                "InboundRaid가 ApplyGuard를 읽는다");
            Check(honorSrc.IndexOf("InboundRaid.SeedHeldForQa", StringComparison.Ordinal) >= 0,
                "시드가 정산을 부른다");
            Check(mapSrc.IndexOf("InboundRaid.OfferIfDue", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("InboundRaid.Settle", StringComparison.Ordinal) >= 0
                  && mapSrc.IndexOf("Honor.GuardLine", StringComparison.Ordinal) >= 0,
                "월드맵이 대기·정산·문구를 읽는다");
            Check(dockSrc.IndexOf("Honor.GuardCap", StringComparison.Ordinal) >= 0,
                "도크가 GuardCap을 읽는다");

            _ = nameof(Honor.ApplyGuard);
            _ = nameof(Honor.GuardLine);
            _ = nameof(Honor.SeedGuardQaIfRequested);
            _ = nameof(InboundRaid.Settle);
            _ = nameof(InboundRaid.OfferIfDue);

            Environment.SetEnvironmentVariable(Honor.EnvShowGuard, show);
            Environment.SetEnvironmentVariable(Honor.EnvNoGuard, no);
            Environment.SetEnvironmentVariable(Honor.EnvShow, honorShow);
            Environment.SetEnvironmentVariable(Honor.EnvNo, honorNo);
            ResetWorld();

            if (_fail == 0) Debug.Log("[HonorGuardSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[HonorGuardSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[HonorGuardSelfCheck] FAIL {_fail}건");
        }
    }
}
