using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>로컬 테스트 시드 — 돈·층·레벨. 스모크·QA_NO는 안 넣는다.</summary>
    public static class LocalPlayKitSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/로컬 테스트 시드 넣기")]
        public static void SeedNow()
        {
            Environment.SetEnvironmentVariable(LocalPlayKit.EnvShow, "1");
            LocalPlayKit.ResetForTest();
            LocalPlayKit.Apply();
            Debug.Log("[LocalPlayKit] " + LocalPlayKit.Line);
        }

        [MenuItem("Ashes to Stars/QA/Local Play Kit Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(LocalPlayKit.EnvShow);
            string no = Environment.GetEnvironmentVariable(LocalPlayKit.EnvNo);
            string start = Environment.GetEnvironmentVariable("GAME_START");
            string noTitleLane = Environment.GetEnvironmentVariable(TitleScreen.EnvNoLocalKitLane);
            Environment.SetEnvironmentVariable(LocalPlayKit.EnvShow, null);
            Environment.SetEnvironmentVariable(LocalPlayKit.EnvNo, null);
            Environment.SetEnvironmentVariable("GAME_START", null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            LifeSystem.Initialize();
            LocalPlayKit.ResetForTest();

            Environment.SetEnvironmentVariable(LocalPlayKit.EnvNo, "1");
            LocalPlayKit.ResetForTest();
            Check(!LocalPlayKit.ShouldApply(), "QA_NO_PLAY면 시드 안 함");
            LocalPlayKit.ApplyIfNeeded();
            Check(GameState.Wallet.Copper == 0, "차단이면 지갑 0");
            Check(GameState.TowerFloor == 1, "차단이면 1층");
            Environment.SetEnvironmentVariable(LocalPlayKit.EnvNo, null);

            Environment.SetEnvironmentVariable("GAME_START", "estate");
            LocalPlayKit.ResetForTest();
            Check(!LocalPlayKit.ShouldApply(), "GAME_START면 샷을 안 오염");
            Environment.SetEnvironmentVariable("GAME_START", null);

            Environment.SetEnvironmentVariable(LocalPlayKit.EnvShow, "1");
            LocalPlayKit.ResetForTest();
            Check(LocalPlayKit.ShouldApply(), "QA_PLAY=1이면 시드");
            LocalPlayKit.ApplyIfNeeded();
            Check(GameState.Wallet.Copper >= LocalPlayKit.WantCopper,
                $"골드 {LocalPlayKit.PlayGold} (실제 {GameState.Wallet.Copper})");
            Check(GameState.TowerFloor >= LocalPlayKit.PlayFloor,
                $"층 {LocalPlayKit.PlayFloor} (실제 {GameState.TowerFloor})");
            Check(Equipment.SmithUnlocked(), "1차 전직이라 대장간이 열린다");
            Check(DefenseState.Unlocked, "30층이라 수비대가 열린다");
            var roster = LifeSystem.GetCharacters();
            int okLv = 0;
            for (int i = 0; i < roster.Count; i++)
                if (!roster[i].IsDeleted && roster[i].Level >= LocalPlayKit.PlayLevel)
                    okLv++;
            Check(okLv >= 5, $"산 캐릭터 Lv{LocalPlayKit.PlayLevel} 5명 (실제 {okLv})");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) >= 30, "가죽 30");
            Check(GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) >= 40, "전직 재료 40");
            Check(LocalPlayKit.Line.Contains("로컬 테스트"), "타이틀에 시드 줄");

            var body = new Rect(GameScreen.BodyPadX, GameScreen.BodyTop,
                1280f - GameScreen.BodyPadX * 2f, 720f - GameScreen.BodyTop - 36f);
            var rightCards = UiPages.Grid(new Rect(body.x + body.width * 0.56f, body.y + 8f,
                body.width * 0.44f, body.height - 16f), 1, 3, 14f);
            var lane = TitleScreen.LocalKitRect(body);
            Check(lane.xMax < rightCards[2].x,
                $"로컬 안내 오른쪽 {lane.xMax:0} < 종료 카드 왼쪽 {rightCards[2].x:0}");

            Environment.SetEnvironmentVariable(TitleScreen.EnvNoLocalKitLane, "1");
            var blockedLane = TitleScreen.LocalKitRect(body);
            Check(blockedLane.Overlaps(rightCards[2]), "네거티브는 옛 전체 폭으로 종료 카드를 가린다");
            Environment.SetEnvironmentVariable(TitleScreen.EnvNoLocalKitLane, null);

            Environment.SetEnvironmentVariable(LocalPlayKit.EnvShow, show);
            Environment.SetEnvironmentVariable(LocalPlayKit.EnvNo, no);
            Environment.SetEnvironmentVariable("GAME_START", start);
            Environment.SetEnvironmentVariable(TitleScreen.EnvNoLocalKitLane, noTitleLane);
            LocalPlayKit.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "GameScreen.cs"));
            Check(screen.Contains("LocalPlayKit.ApplyIfNeeded"),
                "GameScreen이 시드를 읽는다");
            Check(screen.Contains("DebugAutoPilot.BootstrapIfRequested"),
                "스모크를 시드보다 먼저 본다");

            if (_fail == 0) Debug.Log("[LocalPlayKitSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[LocalPlayKitSelfCheck] FAIL {_fail}\n" + _log);
        }
    }
}
