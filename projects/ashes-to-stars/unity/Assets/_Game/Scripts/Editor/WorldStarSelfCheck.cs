using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>월드맵 별 크기는 §18-13 `1 + 층×0.02`. QA_NO면 옛 40~112 선형.</summary>
    public static class WorldStarSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/World Star Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(WorldStar.EnvShow);
            string no = Environment.GetEnvironmentVariable(WorldStar.EnvNo);
            Environment.SetEnvironmentVariable(WorldStar.EnvShow, null);
            Environment.SetEnvironmentVariable(WorldStar.EnvNo, null);
            WorldStar.ResetForTest();

            Check(!WorldStar.Blocked, "기본은 켜짐");
            Check(Mathf.Approximately(WorldStar.SizeMul(1), 1f + WorldStar.SizePerFloor),
                $"1층 배율 {WorldStar.SizeMul(1):0.00} = 1.02");
            Check(Mathf.Approximately(WorldStar.SizeMul(50), 2f),
                $"50층 배율 {WorldStar.SizeMul(50):0.00} = 2");
            Check(Mathf.Approximately(WorldStar.SizeMul(100), 3f),
                $"100층 배율 {WorldStar.SizeMul(100):0.00} = 3");
            Check(Mathf.Approximately(WorldStar.SizePx(1), WorldStar.MinPx * WorldStar.SizeMul(1)),
                $"1층 픽셀은 SizeMul (실제 {WorldStar.SizePx(1):0.0})");
            Check(Mathf.Approximately(WorldStar.SizePx(100), WorldStar.MinPx * 3f),
                $"100층 픽셀 {WorldStar.SizePx(100):0} = {WorldStar.MinPx:0}×3");
            Check(WorldStar.SizePx(100) > WorldStar.MaxPx,
                $"100층 {WorldStar.SizePx(100):0} > 옛 선형 {WorldStar.MaxPx:0}");
            Check(WorldStar.SizePx(1) < WorldStar.SizePx(30), "30층이 1층보다 크다");
            Check(WorldStar.SizePx(30) < WorldStar.SizePx(60), "60층이 30층보다 크다");
            Check(WorldStar.SizePx(60) < WorldStar.SizePx(100), "100층이 60층보다 크다");
            Check(WorldStar.SizePx(0) == WorldStar.SizePx(1), "0층은 1층으로 본다");
            Check(WorldStar.SizePx(200) == WorldStar.SizePx(100), "100층 이상은 커지지 않는다");
            Check(WorldStar.SizePx(29) < WorldStar.SizePx(30), "한 층마다 커진다");
            Check(WorldStar.SizeLabel(7).Contains("7층")
                  && WorldStar.SizeLabel(7).IndexOf("×1.14", StringComparison.Ordinal) >= 0,
                $"라벨 (실제 {WorldStar.SizeLabel(7)})");
            Check(WorldStar.SizeLine(100).IndexOf("×3.00", StringComparison.Ordinal) >= 0
                  && WorldStar.SizeLine(100).IndexOf("§18-13", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {WorldStar.SizeLine(100)})");

            var body = new Rect(0f, 0f, 800f, 400f);
            var plate = WorldStar.Plate(body);
            var small = WorldStar.Icon(plate, 1);
            var big = WorldStar.Icon(plate, 100);
            Check(small.width < big.width && small.height < big.height,
                "아이콘 칸도 층에 따라 커진다");
            Check(big.xMax < plate.xMax, "큰 별도 판 안에 있다");
            Check(WorldStar.AfterPlate(body).y >= plate.yMax, "카드는 별 아래에 있다");
            Check(!UiPages.LayoutOverlaps(plate, WorldStar.AfterPlate(body)),
                "별 판과 카드가 겹치지 않는다");

            Environment.SetEnvironmentVariable(WorldStar.EnvNo, "1");
            Check(WorldStar.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(WorldStar.SizePx(1), WorldStar.MinPx),
                $"차단 1층 {WorldStar.SizePx(1):0} = 옛 {WorldStar.MinPx:0}");
            Check(Mathf.Approximately(WorldStar.SizePx(100), WorldStar.MaxPx),
                $"차단 100층 {WorldStar.SizePx(100):0} = 옛 {WorldStar.MaxPx:0}");
            Check(WorldStar.SizeLine().IndexOf("옛 선형", StringComparison.Ordinal) >= 0,
                $"차단 줄 (실제 {WorldStar.SizeLine()})");
            Environment.SetEnvironmentVariable(WorldStar.EnvNo, null);

            Environment.SetEnvironmentVariable(WorldStar.EnvShow, "1");
            WorldStar.SeedQaIfRequested();
            Check(WorldStar.ShowQa, "시드 켜짐");
            Check(GameState.TowerFloor == WorldStar.MaxFloor,
                $"시드 100층 (실제 {GameState.TowerFloor})");
            Check(WorldStar.SizeLine().IndexOf("×3.00", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {WorldStar.SizeLine()})");
            Environment.SetEnvironmentVariable(WorldStar.EnvShow, null);
            WorldStar.ResetForTest();

            WorldStar.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            Check(WorldStar.SenseBase(1) == WorldStar.MinSense, "1층 영공은 최소");
            Check(WorldStar.SenseBase(1) < WorldStar.SenseBase(50), "층이 오르면 영공이 넓어진다");
            Check(Mathf.Abs(WorldStar.SenseBase(100) - WorldStar.MaxSense) < 0.01f, "100층 영공은 최대");
            Check(Mathf.Abs(WorldStar.Sense(1) - WorldStar.SenseBase(1)) < 0.01f, "인간은 기준 영공");
            Check(!WorldStar.AllyBuff && !WorldStar.EnemyDebuff, "기본은 영공 꺼짐 — 켠다");
            long raw = EstateMine.CopperPerHour();
            Check(EstateMine.CopperPerHourEffective() == raw, "끄면 광산은 기준값");
            WorldStar.AllyBuff = true;
            Check(EstateMine.CopperPerHourEffective() > raw, "아군 버프가 광산을 올린다");
            WorldStar.EnemyDebuff = true;
            Check(WorldStar.AuraLabel().Contains("디버프"), "적 디버프를 켤 수 있다");
            Check(Mathf.Abs(WorldStar.EnemyMul - WorldStar.EnemyDebuffMul) < 0.001f,
                "적 디버프는 ×0.95");
            WorldStar.ResetForTest();
            Check(WorldStar.AllyMul == 1f, "리셋하면 배율이 1이다");
            Check(WorldStar.EnemyMul == 1f, "리셋하면 적 배율도 1이다");

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string star = File.ReadAllText(Path.Combine(runtime, "WorldStar.cs"));
            string map = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            Check(star.IndexOf("MinPx * SizeMul", StringComparison.Ordinal) >= 0,
                "SizePx가 SizeMul을 읽는다");
            Check(star.IndexOf("1f + f * SizePerFloor", StringComparison.Ordinal) >= 0,
                "SizeMul이 1+층×0.02를 쓴다");
            Check(map.IndexOf("WorldStar.SizeLine", StringComparison.Ordinal) >= 0,
                "지도가 SizeLine을 읽는다");
            Check(map.IndexOf("WorldStar.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "지도가 시드를 읽는다");
            _ = nameof(WorldStar.SizeMul);
            _ = nameof(WorldStar.SizeLine);
            _ = nameof(WorldStar.OldSizePx);

            Environment.SetEnvironmentVariable(WorldStar.EnvShow, show);
            Environment.SetEnvironmentVariable(WorldStar.EnvNo, no);
            if (_fail == 0) Debug.Log("[WorldStarSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[WorldStarSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[WorldStarSelfCheck] FAIL {_fail}건");
        }
    }
}
