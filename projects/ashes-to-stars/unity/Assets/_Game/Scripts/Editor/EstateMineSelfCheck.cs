using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>광산 25%·창고 자동 적립·초과 소멸이 §18-12와 같은지.</summary>
    public static class EstateMineSelfCheck
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
            Environment.SetEnvironmentVariable("QA_NO_MINE", null);
            Environment.SetEnvironmentVariable("QA_ESTATE_MINE", null);
            Environment.SetEnvironmentVariable(EstateMine.EnvShowRace, null);
            Environment.SetEnvironmentVariable(EstateMine.EnvNoRace, null);
            GameState.ResetAll();
            WorldStar.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            EstateMine.ForceRaceMul = 0f;

            long t1 = (long)(0.25 * Economy.COPPER_PER_GOLD);
            Check(EstateMine.CopperPerHour() == t1,
                $"T1 광산은 필드 25% = 25실버/h ({t1}, 실제 {EstateMine.CopperPerHour()})");
            Check(t1 == 25L * Economy.COPPER_PER_SILVER,
                "T1 = 25실버/h(§18-12)");

            long now = 1_700_000_000;
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now;
            Check(EstateMine.Tick() == 0 && GameState.Wallet.Copper == 0,
                "첫 Tick은 기준점만 찍고 0");

            now += 3600;
            long added = EstateMine.Tick();
            Check(added == t1 && GameState.Wallet.Copper == t1,
                $"1시간이면 T1 25실버가 창고에 들어간다 ({added})");

            now += 1800;
            long half = EstateMine.Tick();
            Check(half == t1 / 2 && GameState.Wallet.Copper == t1 + t1 / 2,
                $"30분이면 절반 ({half})");

            GameState.ResetAll();
            GameState.SetTowerFloorForTest(11);
            Check(GameState.TrySelectTier(1), "T2 선택");
            long t2 = (long)(0.25 * 1.6 * Economy.COPPER_PER_GOLD);
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now;
            EstateMine.Tick();
            now += 3600;
            Check(EstateMine.CopperPerHour() == t2 && EstateMine.Tick() == t2,
                $"선택 T2면 40실버/h ({t2})");

            GameState.ResetAll();
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now;
            EstateMine.Tick();
            long cap = EstateBuild.WarehouseCapCopper();
            GameState.Grant(cap);
            now += 3600;
            Check(EstateMine.Tick() == 0 && GameState.Wallet.Copper == cap,
                "창고가 가득이면 광산은 0");
            Check(EstateMine.WastedCopper == t1,
                $"넘친 T1 1시간분은 소멸 ({EstateMine.WastedCopper})");

            GameState.ResetAll();
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now;
            EstateMine.Tick();
            GameState.Grant(cap - 1000);
            now += 3600;
            Check(EstateMine.Tick() == 1000 && GameState.Wallet.Copper == cap,
                "남은 칸만큼만 적립");
            Check(EstateMine.WastedCopper == t1 - 1000, "나머지는 소멸");

            Environment.SetEnvironmentVariable("QA_NO_MINE", "1");
            GameState.ResetAll();
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now;
            EstateMine.Tick();
            now += 3600;
            Check(EstateMine.Tick() == 0 && GameState.Wallet.Copper == 0,
                "QA_NO_MINE=1이면 적립 0");
            Environment.SetEnvironmentVariable("QA_NO_MINE", null);

            GameState.ResetAll();
            EstateMine.ResetForTest();
            EstateMine.NowUnix = () => now;
            EstateMine.Tick();
            now += 3600;
            EstateMine.Tick();
            long saved = GameState.Wallet.Copper;
            GameState.ForgetInMemoryForTest();
            EstateMine.ForgetInMemoryForTest();
            Check(GameState.Wallet.Copper == saved && saved == t1,
                "재기동 뒤에도 적립이 남는다");

            GameState.ResetAll();
            GameState.Grant(100_000);
            Environment.SetEnvironmentVariable("QA_ESTATE_MINE", "1");
            EstateMine.ResetForTest();
            EstateMine.SeedQaIfRequested();
            Check(GameState.Wallet.Copper == t1,
                $"QA 시드는 지갑을 비우고 1시간분만 ({GameState.Wallet.Copper})");
            Environment.SetEnvironmentVariable("QA_ESTATE_MINE", null);

            _ = nameof(EstateMine.Tick);
            _ = nameof(EstateMine.CopperPerHour);
            _ = nameof(EstateMine.WastedCopper);
            _ = nameof(EstateMine.SeedQaIfRequested);

            GameState.ResetAll();
            if (_fail > 0)
            {
                Debug.LogError("[EstateMineSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("EstateMineSelfCheck FAIL " + _fail);
            }
            Debug.Log("[EstateMineSelfCheck] PASS\n" + _log);
        }
    }
}
