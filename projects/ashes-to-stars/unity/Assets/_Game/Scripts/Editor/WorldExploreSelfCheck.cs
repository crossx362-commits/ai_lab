using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §14 전장의 안개 + §18-9 엘프 탐험 +30%. RaceDef.탐험범위배율 소비처.
    /// QA_NO면 옛 「로컬 허브만」·안개 없음.
    /// </summary>
    public static class WorldExploreSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.01f;

        [MenuItem("Ashes to Stars/QA/World Explore Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(WorldExplore.EnvShow);
            string no = Environment.GetEnvironmentVariable(WorldExplore.EnvNo);
            string noDup = Environment.GetEnvironmentVariable(WorldExplore.EnvNoDup);
            string noRange = Environment.GetEnvironmentVariable(WorldStar.EnvNoRange);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = WorldExplore.ForceMul;
            int oldFloor = GameState.TowerFloor;
            Environment.SetEnvironmentVariable(WorldExplore.EnvShow, null);
            Environment.SetEnvironmentVariable(WorldExplore.EnvNo, null);
            Environment.SetEnvironmentVariable(WorldExplore.EnvNoDup, null);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoRange, null);
            WorldExplore.ForceMul = 0f;

            GameState.ResetAll();
            WorldStar.ResetForTest();
            WorldExplore.ResetForTest();
            GameState.SetTowerFloorForTest(30);

            var defs = Resources.LoadAll<RaceDef>("races");
            Check(defs != null && defs.Length >= 4,
                $"Resources/races 로드 ({(defs == null ? 0 : defs.Length)}종)");

            RaceDef elf = RaceInfo.For(RaceId.엘프);
            RaceDef human = RaceInfo.For(RaceId.인간);
            RaceDef dwarf = RaceInfo.For(RaceId.드워프);
            RaceDef beast = RaceInfo.For(RaceId.수인);
            Check(elf != null && Mathf.Abs(elf.탐험범위배율 - 1.30f) < 0.0001f,
                $"엘프 탐험범위배율 1.30 (실제 {elf?.탐험범위배율})");
            Check(human != null && Mathf.Abs(human.탐험범위배율 - 1f) < 0.0001f,
                $"인간 탐험범위배율 1 (실제 {human?.탐험범위배율})");
            Check(dwarf != null && Mathf.Abs(dwarf.탐험범위배율 - 1f) < 0.0001f,
                $"드워프 탐험범위배율 1 (실제 {dwarf?.탐험범위배율})");
            Check(beast != null && Mathf.Abs(beast.탐험범위배율 - 1f) < 0.0001f,
                $"수인 탐험범위배율 1 (실제 {beast?.탐험범위배율})");

            float base30 = WorldStar.SenseBase(30);
            Check(Near(base30, 4f), $"30층 SenseBase 4 (실제 {base30:0.00})");
            float far = base30 * WorldExplore.ElfPercent / 100f;
            Check(Near(far, 5.2f), $"안개 별 = 30층×1.30 (실제 {far:0.00})");

            RacePrefs.Set(RaceId.인간);
            Check(WorldExplore.Percent() == WorldExplore.HumanPercent,
                $"인간 탐험 100 (실제 {WorldExplore.Percent()})");
            Check(Near(WorldExplore.Radius(30), base30),
                $"인간 반경=SenseBase (실제 {WorldExplore.Radius(30):0.00})");
            Check(WorldExplore.RevealedCount(30) == 2,
                $"인간 30층 2/3 (실제 {WorldExplore.RevealedCount(30)})");
            Check(WorldExplore.Revealed(base30, 30), "인간 30층 경계 별은 보인다");
            Check(!WorldExplore.Revealed(far, 30), "인간 30층 안개 별은 숨는다");
            Check(WorldExplore.Caption() == "탐험 2/3",
                $"인간 카드 (실제 {WorldExplore.Caption()})");
            Check(WorldExplore.Line(30).Contains("2/3")
                  && WorldExplore.Line(30).Contains("§14"),
                $"인간 문구 (실제 {WorldExplore.Line(30)})");

            RacePrefs.Set(RaceId.엘프);
            Check(WorldExplore.Percent() == WorldExplore.ElfPercent,
                $"엘프 탐험 130 (실제 {WorldExplore.Percent()})");
            Check(Near(WorldExplore.Radius(30), far),
                $"엘프 반경=인간×1.30 (실제 {WorldExplore.Radius(30):0.00})");
            Check(WorldExplore.RevealedCount(30) == 3,
                $"엘프 30층 3/3 (실제 {WorldExplore.RevealedCount(30)})");
            Check(WorldExplore.Revealed(far, 30), "엘프 30층 안개 별이 열린다");
            Check(WorldExplore.Caption() == "탐험 3/3",
                $"엘프 카드 (실제 {WorldExplore.Caption()})");
            Check(WorldExplore.Line(30).Contains("+30%")
                  && WorldExplore.Line(30).Contains("3/3"),
                $"엘프 문구 (실제 {WorldExplore.Line(30)})");
            Check(WorldExplore.HeaderOwnsLine(), "엘프는 헤더가 Line을 가진다");
            Check(string.IsNullOrEmpty(WorldExplore.FieldCaption(30)),
                $"엘프 필드 캡션은 헤더가 가져서 빈다 (실제 '{WorldExplore.FieldCaption(30)}')");
            Check(WorldMapDockCap.Star() == "탐험 3/3",
                $"도크가 Caption을 읽는다 (실제 {WorldMapDockCap.Star()})");

            RacePrefs.Set(RaceId.인간);
            Check(!WorldExplore.HeaderOwnsLine(), "인간은 헤더가 Line을 안 가진다");
            Check(WorldExplore.FieldCaption(30) == WorldExplore.Line(30),
                $"인간 필드 캡션은 Line (실제 {WorldExplore.FieldCaption(30)})");
            RacePrefs.Set(RaceId.엘프);
            Environment.SetEnvironmentVariable(WorldExplore.EnvNoDup, "1");
            Check(WorldExplore.DupBlocked, "QA_NO_EXPLORE_DUP면 차단");
            Check(WorldExplore.FieldCaption(30) == WorldExplore.Line(30)
                  && WorldExplore.FieldCaption(30).Contains("+30%"),
                $"차단 캡션은 옛 중복 (실제 {WorldExplore.FieldCaption(30)})");
            Environment.SetEnvironmentVariable(WorldExplore.EnvNoDup, null);
            Check(string.IsNullOrEmpty(WorldExplore.FieldCaption(30)),
                "차단을 풀면 다시 빈다");

            RacePrefs.Set(RaceId.인간);
            Check(WorldExplore.RevealedCount(1) == 1,
                $"인간 1층 1/3 (실제 {WorldExplore.RevealedCount(1)})");
            Check(WorldExplore.RevealedCount(100) == 3,
                $"인간 100층 3/3 (실제 {WorldExplore.RevealedCount(100)})");

            Environment.SetEnvironmentVariable(WorldExplore.EnvNo, "1");
            Check(WorldExplore.Blocked, "QA_NO_EXPLORE_FOG");
            Check(WorldExplore.Percent() == WorldExplore.HumanPercent, "차단하면 100");
            Check(WorldExplore.Line().Contains("없음"),
                $"차단 문구 (실제 {WorldExplore.Line()})");
            Check(WorldMapDockCap.Star() == WorldMapDockCap.StarCap,
                $"차단 카드는 로컬 허브만 (실제 {WorldMapDockCap.Star()})");
            Environment.SetEnvironmentVariable(WorldExplore.EnvNo, null);

            RacePrefs.Set(RaceId.엘프);
            Check(WorldExplore.RevealedCount(30) == 3, "차단을 풀면 다시 3/3");

            Environment.SetEnvironmentVariable(WorldExplore.EnvShow, "1");
            WorldExplore.ResetForTest();
            WorldExplore.SeedQaIfRequested();
            Check(RacePrefs.Get() == RaceId.엘프, "시드는 엘프");
            Check(GameState.TowerFloor >= 30, $"시드 30층 (실제 {GameState.TowerFloor})");
            Check(WorldExplore.Line().Contains("+30%"), "시드 문구 +30%");
            Check(WorldExplore.RevealedCount(GameState.TowerFloor) == 3, "시드 3/3");
            Check(WorldExplore.HeaderOwnsLine(), "시드면 헤더가 Line");
            Check(string.IsNullOrEmpty(WorldExplore.FieldCaption()),
                "시드(헤더=Line)면 필드 캡션 없음");
            Environment.SetEnvironmentVariable(WorldExplore.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string expSrc = File.ReadAllText(Path.Combine(runtime, "WorldExplore.cs"));
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            string dockSrc = File.ReadAllText(Path.Combine(runtime, "WorldMapDockCap.cs"));
            string raceSrc = File.ReadAllText(Path.Combine(runtime, "RaceDef.cs"));
            string setupSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Editor/ProjectSetup.cs"));
            Check(expSrc.Contains("d.탐험범위배율"),
                "WorldExplore가 d.탐험범위배율을 읽는다");
            Check(expSrc.Contains("FieldCaption(floor)")
                  && expSrc.Contains("QA_NO_EXPLORE_DUP"),
                "Draw가 FieldCaption을 그린다 (옛 Line 중복 금지)");
            Check(mapSrc.Contains("WorldExplore.Draw")
                  && mapSrc.Contains("WorldExplore.SeedQaIfRequested")
                  && mapSrc.Contains("WorldExplore.Line"),
                "월드맵이 안개·시드·문구를 읽는다");
            Check(dockSrc.Contains("WorldExplore.Caption"),
                "도크가 Caption을 읽는다");
            Check(raceSrc.Contains("탐험범위배율"),
                "RaceDef에 탐험범위배율 필드");
            Check(setupSrc.Contains("탐험범위배율 = r.Item1 == RaceId.엘프 ? 1.30f"),
                "ProjectSetup이 엘프 1.30을 심는다");

            _ = nameof(WorldExplore.Radius);
            _ = nameof(WorldExplore.RevealedCount);
            _ = nameof(WorldExplore.Caption);
            _ = nameof(WorldExplore.FieldCaption);
            _ = nameof(WorldExplore.Draw);
            _ = nameof(RaceDef.탐험범위배율);

            Environment.SetEnvironmentVariable(WorldExplore.EnvShow, show);
            Environment.SetEnvironmentVariable(WorldExplore.EnvNo, no);
            Environment.SetEnvironmentVariable(WorldExplore.EnvNoDup, noDup);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoRange, noRange);
            WorldExplore.ForceMul = oldForce;
            RacePrefs.Set(oldRace);
            WorldExplore.ResetForTest();
            WorldStar.ResetForTest();
            if (oldFloor > 0) GameState.SetTowerFloorForTest(oldFloor);
            else GameState.ResetAll();

            if (_fail == 0) Debug.Log("[WorldExploreSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[WorldExploreSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[WorldExploreSelfCheck] FAIL {_fail}건");
        }
    }
}
