using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 시작 로스터 2명 — 첫 선택 1명, 5분 뒤 두 번째, 세 번째는 이 경로로 없다.
    /// </summary>
    public static class StarterSecondSelfCheck
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
            string show = Environment.GetEnvironmentVariable(StarterSecond.EnvShow);
            string no = Environment.GetEnvironmentVariable(StarterSecond.EnvNo);
            string startShow = Environment.GetEnvironmentVariable(StarterPick.EnvShow);
            string startJob = Environment.GetEnvironmentVariable(StarterPick.EnvJob);
            string startNo = Environment.GetEnvironmentVariable(StarterPick.EnvNo);
            Environment.SetEnvironmentVariable(StarterSecond.EnvShow, null);
            Environment.SetEnvironmentVariable(StarterSecond.EnvNo, null);
            Environment.SetEnvironmentVariable(StarterPick.EnvShow, null);
            Environment.SetEnvironmentVariable(StarterPick.EnvJob, null);
            Environment.SetEnvironmentVariable(StarterPick.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            StarterPick.ResetForTest();
            StarterSecond.ResetForTest();

            Check(Mathf.Abs(StarterSecond.UnlockSeconds - 300f) < 0.01f, "해금은 5분(§3 5~10분 하한)");
            Check(StarterPick.TryChoose("힐"), "첫 캐릭터 힐");
            var roster = LifeSystem.GetCharacters();
            Check(roster.Count == 1, $"고른 뒤 1명 (실제 {roster.Count})");
            Check(roster[0].Job == "힐" && roster[0].Level == 10, "0번은 힐 Lv10");
            bool extras = false;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Job != "힐") extras = true;
            Check(!extras, "나머지 기본직업을 같이 넣지 않는다");
            Check(PartyState.Slots.Count == 1, $"출전 1명 (실제 {PartyState.Slots.Count})");
            Check(StarterSecond.Started && !StarterSecond.Pending && !StarterSecond.Claimed,
                "새 여정은 시작됨·대기 아님");
            Check(StarterSecond.PlayedSeconds < 0.01f, "시작 직후 플레이 0초");

            StarterSecond.Tick(299f);
            Check(!StarterSecond.Pending && roster.Count == 1, "299초엔 아직 1명");
            Check(!StarterSecond.TryClaim("탱"), "해금 전 영입 거부");
            Check(LifeSystem.GetCharacters().Count == 1, "거부 뒤 명부 1명");

            StarterSecond.Tick(2f);
            Check(StarterSecond.Pending, "301초면 두 번째를 고른다");
            Check(!StarterSecond.TryClaim("수호기사"), "1차 직업명은 거부");
            Check(LifeSystem.GetCharacters().Count == 1, "거부 뒤 명부 불변");

            Check(StarterSecond.TryClaim("탱"), "탱을 두 번째로 고른다");
            roster = LifeSystem.GetCharacters();
            Check(roster.Count == 2, $"시작 로스터 2명 (실제 {roster.Count})");
            Check(roster[0].Job == "힐" && roster[1].Job == "탱", "힐 + 탱");
            Check(roster[1].Level == 10 && roster[1].Advancement == AdvancementTier.Basic,
                "두 번째도 Lv10 기본직업");
            Check(roster[1].Name == "탱커", $"두 번째 이름 탱커 (실제 {roster[1].Name})");
            Check(!StarterSecond.Pending && StarterSecond.Claimed, "고른 뒤 대기 종료");
            Check(PartyState.Slots.Count == 2, $"출전 2명 (실제 {PartyState.Slots.Count})");
            Check(!StarterSecond.TryClaim("딜"), "세 번째는 이 경로로 없다");
            Check(LifeSystem.GetCharacters().Count == 2, "세 번째 거부 뒤 2명");
            StarterSecond.Tick(400f);
            Check(!StarterSecond.Pending && LifeSystem.GetCharacters().Count == 2,
                "더 기다려도 다시 안 연다");

            StarterSecond.ForgetInMemoryForTest();
            LifeSystem.ForgetInMemoryForTest();
            PartyState.ResetForTest();
            Check(StarterSecond.Claimed && StarterSecond.LastJob == "탱", "재기동 뒤 지급 유지");
            Check(LifeSystem.GetCharacters().Count == 2, "재기동 뒤 명부 2명");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            StarterPick.ResetForTest();
            StarterSecond.ResetForTest();
            StarterPick.TryChoose("힐");
            Check(StarterSecond.TryClaim("힐") == false, "5분 전엔 같은 직업도 거부");
            StarterSecond.Tick(300f);
            Check(StarterSecond.TryClaim("힐"), "같은 역할 두 명도 된다(💡 강제 없음)");
            roster = LifeSystem.GetCharacters();
            Check(roster.Count == 2 && roster[0].Job == "힐" && roster[1].Job == "힐",
                "힐 + 힐");
            Check(roster[1].Name == "힐러2", $"같은 직업 둘째 이름 (실제 {roster[1].Name})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            StarterPick.ResetForTest();
            StarterSecond.ResetForTest();
            Environment.SetEnvironmentVariable(StarterSecond.EnvNo, "1");
            StarterPick.TryChoose("딜");
            StarterSecond.Tick(400f);
            Check(!StarterSecond.Pending && !StarterSecond.TryClaim("탱"),
                "QA_NO_STARTER_SECOND면 거부");
            Check(LifeSystem.GetCharacters().Count == 1, "끄면 명부 1명");
            Environment.SetEnvironmentVariable(StarterSecond.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            StarterPick.ResetForTest();
            StarterSecond.ResetForTest();
            Environment.SetEnvironmentVariable(StarterSecond.EnvShow, "1");
            StarterSecond.SeedQaIfRequested();
            Check(LifeSystem.GetCharacters().Count == 1
                  && LifeSystem.GetCharacters()[0].Job == "힐",
                "QA 시드는 힐 1명");
            Check(StarterSecond.Pending, "QA_STARTER_SECOND면 바로 대기");
            Environment.SetEnvironmentVariable(StarterSecond.EnvShow, null);

            string lifeSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/LifeSystem.cs"));
            Check(lifeSrc.Contains("StarterSecond.OnNewGame"),
                "BeginNewGame이 두 번째 시계를 연다");
            Check(!lifeSrc.Contains("프로토타입 레이드는 5인이라 나머지 기본직업을 같이 넣는다"),
                "5인 동반 주석을 되돌리면 FAIL");
            string gameSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/GameScreen.cs"));
            Check(gameSrc.Contains("StarterSecond.Tick"),
                "플레이 화면이 5분 시계를 소비한다");
            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("StarterSecond.Pending"),
                "캐릭터 화면이 두 번째 카드를 그린다");
            string estateSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateScreen.cs"));
            Check(estateSrc.Contains("StarterSecond.Pending"),
                "영지가 두 번째 카드를 그린다");

            _ = nameof(StarterSecond.Tick);
            _ = nameof(StarterSecond.TryClaim);
            _ = nameof(StarterSecond.OnNewGame);
            _ = nameof(StarterSecond.SeedQaIfRequested);
            _ = nameof(LifeSystem.AddStarterCompanion);
            _ = nameof(LifeSystem.BeginNewGame);

            Environment.SetEnvironmentVariable(StarterSecond.EnvShow, show);
            Environment.SetEnvironmentVariable(StarterSecond.EnvNo, no);
            Environment.SetEnvironmentVariable(StarterPick.EnvShow, startShow);
            Environment.SetEnvironmentVariable(StarterPick.EnvJob, startJob);
            Environment.SetEnvironmentVariable(StarterPick.EnvNo, startNo);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            StarterPick.ResetForTest();
            StarterSecond.ResetForTest();

            if (_fail == 0) Debug.Log("[StarterSecondSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[StarterSecondSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[StarterSecondSelfCheck] FAIL {_fail}건");
        }
    }
}
