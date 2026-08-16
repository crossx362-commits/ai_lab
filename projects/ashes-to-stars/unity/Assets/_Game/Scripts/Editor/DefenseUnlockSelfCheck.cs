using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>수비대 주둔지는 탑 30층(침략과 같다)에 열린다. QA_NO면 층과 무관(§13-2).</summary>
    public static class DefenseUnlockSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Defense Unlock Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(DefenseState.EnvShowUnlock);
            string no = Environment.GetEnvironmentVariable(DefenseState.EnvNoUnlock);
            string recover = Environment.GetEnvironmentVariable(DefenseState.EnvShow);
            Environment.SetEnvironmentVariable(DefenseState.EnvShowUnlock, null);
            Environment.SetEnvironmentVariable(DefenseState.EnvNoUnlock, null);
            Environment.SetEnvironmentVariable(DefenseState.EnvShow, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            DefenseState.ResetForTest();
            PartyState.ResetForTest();

            Check(DefenseState.UnlockFloor == WorldMapScreen.InvasionUnlockFloor,
                "주둔지 해금 층 = 침략 해금 층");
            Check(DefenseState.UnlockFloor == EstateScreen.AuctionUnlockFloor,
                "주둔지 해금 층 = 경매장 해금 층");
            Check(GameState.TowerFloor < DefenseState.UnlockFloor,
                $"기본 층은 잠김 (실제 {GameState.TowerFloor})");
            Check(!DefenseState.Unlocked, "1층은 잠김");
            Check(DefenseState.LockReason() != null
                  && DefenseState.LockReason().Contains("30층")
                  && DefenseState.LockReason().Contains("§13-2"),
                $"잠금 문구 (실제 {DefenseState.LockReason()})");
            Check(!DefenseState.Toggle(0), "29층 이하는 배치 거부");
            Check(DefenseState.Count == 0, "거부하면 수비가 비어 있다");

            GameState.SetTowerFloorForTest(DefenseState.UnlockFloor - 1);
            Check(!DefenseState.Unlocked && !DefenseState.Toggle(0),
                "29층도 거부");

            GameState.SetTowerFloorForTest(DefenseState.UnlockFloor);
            Check(DefenseState.Unlocked, "30층은 열린다");
            Check(string.IsNullOrEmpty(DefenseState.LockReason()),
                "30층은 잠금 문구 없음");
            Check(DefenseState.Toggle(0), "30층에서 배치");
            Check(DefenseState.Contains(0) && DefenseState.Count == 1,
                "배치 후 수비 1명");

            GameState.SetTowerFloorForTest(29);
            Check(DefenseState.Toggle(0), "잠겨도 해임은 된다");
            Check(!DefenseState.Contains(0), "해임 후 비어 있다");
            Check(!DefenseState.Toggle(0), "29층에서 다시 넣기는 거부");

            GameState.SetTowerFloorForTest(30);
            Check(DefenseState.Toggle(0), "30층 재배치");
            GameState.ForgetInMemoryForTest();
            DefenseState.ForgetInMemoryForTest();
            Check(GameState.TowerFloor == 30 && DefenseState.Unlocked,
                "재기동 뒤에도 30층 해금");
            Check(DefenseState.Contains(0), "재기동 뒤에도 배치");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            DefenseState.ResetForTest();
            PartyState.ResetForTest();
            Environment.SetEnvironmentVariable(DefenseState.EnvNoUnlock, "1");
            GameState.SetTowerFloorForTest(1);
            Check(DefenseState.Unlocked, "QA_NO면 1층도 열림");
            Check(DefenseState.Toggle(0), "QA_NO면 1층 배치");
            Check(string.IsNullOrEmpty(DefenseState.LockReason()),
                "QA_NO면 잠금 문구 없음");
            Environment.SetEnvironmentVariable(DefenseState.EnvNoUnlock, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            DefenseState.ResetForTest();
            Environment.SetEnvironmentVariable(DefenseState.EnvShowUnlock, "1");
            DefenseState.SeedUnlockQaIfRequested();
            Check(GameState.TowerFloor == 29, $"시드 29층 (실제 {GameState.TowerFloor})");
            Check(!DefenseState.Unlocked, "시드는 잠김");
            Check(DefenseState.LockLine().Contains("29층"),
                $"시드 문구 (실제 {DefenseState.LockLine()})");
            Environment.SetEnvironmentVariable(DefenseState.EnvShowUnlock, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string def = File.ReadAllText(Path.Combine(runtime, "DefenseState.cs"));
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(def.Contains("if (!Unlocked) return false"),
                "Toggle이 Unlocked를 읽는다");
            Check(estate.Contains("DefenseState.LockReason")
                  && estate.Contains("DefenseState.SeedUnlockQaIfRequested"),
                "영지가 잠금·시드를 읽는다");

            _ = nameof(DefenseState.Unlocked);
            _ = nameof(DefenseState.LockReason);
            _ = nameof(DefenseState.SeedUnlockQaIfRequested);

            Environment.SetEnvironmentVariable(DefenseState.EnvShowUnlock, show);
            Environment.SetEnvironmentVariable(DefenseState.EnvNoUnlock, no);
            Environment.SetEnvironmentVariable(DefenseState.EnvShow, recover);
            DefenseState.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail == 0) Debug.Log("[DefenseUnlockSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[DefenseUnlockSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[DefenseUnlockSelfCheck] FAIL {_fail}건");
        }
    }
}
