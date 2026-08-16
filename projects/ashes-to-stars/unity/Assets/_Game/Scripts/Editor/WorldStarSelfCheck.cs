using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>월드맵 별 크기 — 층이 오르면 커지고, 같은 층은 같은 크기(§14).</summary>
    public static class WorldStarSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            Check(WorldStar.SizePx(1) == WorldStar.MinPx, "1층은 최소");
            Check(Mathf.Abs(WorldStar.SizePx(100) - WorldStar.MaxPx) < 0.01f, "100층은 최대");
            Check(WorldStar.SizePx(1) < WorldStar.SizePx(30), "30층이 1층보다 크다");
            Check(WorldStar.SizePx(30) < WorldStar.SizePx(60), "60층이 30층보다 크다");
            Check(WorldStar.SizePx(60) < WorldStar.SizePx(100), "100층이 60층보다 크다");
            Check(WorldStar.SizePx(0) == WorldStar.SizePx(1), "0층은 1층으로 본다");
            Check(WorldStar.SizePx(200) == WorldStar.SizePx(100), "100층 이상은 커지지 않는다");
            Check(WorldStar.SizePx(29) < WorldStar.SizePx(30), "한 층마다 커진다");
            Check(WorldStar.SizeLabel(7).Contains("7층"), "라벨에 층이 있다");

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

            Debug.Log("[WorldStarSelfCheck]\n" + _log);
            if (_fail > 0)
                throw new System.Exception($"WorldStarSelfCheck FAIL {_fail}");
        }
    }
}
