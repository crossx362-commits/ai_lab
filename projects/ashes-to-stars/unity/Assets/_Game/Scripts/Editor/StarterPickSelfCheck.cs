using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 시작 기본직업 5종 — 전신 폴더가 서로 다르고, 고르면 0번이 그 직업이다.
    /// 힉스필드 재생성 없이 기존 sprites/{tank,dps,mage,healer,buffer}를 소비한다.
    /// </summary>
    public static class StarterPickSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Starter Pick Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(StarterPick.EnvShow);
            string jobEnv = Environment.GetEnvironmentVariable(StarterPick.EnvJob);
            string no = Environment.GetEnvironmentVariable(StarterPick.EnvNo);
            Environment.SetEnvironmentVariable(StarterPick.EnvShow, null);
            Environment.SetEnvironmentVariable(StarterPick.EnvJob, null);
            Environment.SetEnvironmentVariable(StarterPick.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            StarterPick.ResetForTest();

            Check(StarterPick.Jobs.Length == 5, "기본직업 5종");
            Check(UiPages.LookDir("탱") == "tank" && UiPages.LookDir("딜") == "dps"
                  && UiPages.LookDir("마딜") == "mage" && UiPages.LookDir("힐") == "healer"
                  && UiPages.LookDir("버퍼") == "buffer",
                "5종이 서로 다른 전신 폴더");
            Check(UiPages.LookDir("딜") != UiPages.LookDir("마딜"),
                "물리딜과 마법딜은 같은 딜러 그림을 쓰지 않는다");
            Check(UiPages.LookDir("수호기사") == "tank" && UiPages.LookDir("탱") == "tank",
                "1차 수호기사는 탱 폴더를 빌린다 — 기본 탱이 따로 있다");
            Check(UiPages.LookDir("마법사") == "mage" && UiPages.LookDir("마딜") == "mage",
                "1차 마법사는 마딜 폴더를 빌린다");
            Check(UiPages.LookPath("마딜", "idle_00") == "sprites/mage/mage_idle_00",
                "마딜 전신은 mage 폴더 — 빼면 dps로 폴백한다");
            Check(!UiPages.StarterLookWalks, "시작 카드는 idle — 걷기로 바꾸면 FAIL");
            Check(UiPages.JobLookFrame(false) == UiPages.IdleFrame
                  && UiPages.IdleFrame == "idle_00",
                "walk=false는 idle_00");
            Check(UiPages.JobLookFrame(false).StartsWith("walk_") == false,
                "시작 프레임 이름에 walk가 없다");
            Check(UiPages.JobLookFrame(true).StartsWith("walk_"),
                "walk=true만 걷기 — 되돌리면 idle과 안 갈린다");

            string root = Path.Combine(Application.dataPath, "Resources", "sprites");
            string[] frames = { "idle_00", "walk_00", "walk_01" };
            for (int i = 0; i < StarterPick.Jobs.Length; i++)
            {
                string job = StarterPick.Jobs[i];
                string dir = UiPages.LookDir(job);
                for (int f = 0; f < frames.Length; f++)
                {
                    string path = Path.Combine(root, dir, dir + "_" + frames[f] + ".png");
                    Check(File.Exists(path), $"{job} {frames[f]} 반입 {path}");
                }
            }

            Check(!LifeSystem.HasSavedRoster(), "리셋 뒤에는 저장 로스터 없음");
            Check(StarterPick.ShouldOffer(), "저장 없으면 직업을 고른다");
            StarterPick.Request();
            Check(StarterPick.Open, "Request면 선택 화면");

            Check(!StarterPick.TryChoose("수호기사"), "1차 직업명은 거부");
            Check(StarterPick.Open, "거부 뒤에도 선택 화면");
            Check(!LifeSystem.HasSavedRoster(), "거부 뒤 로스터는 비어 있다");

            Check(StarterPick.TryChoose("마딜"), "마딜을 고르면 여정이 열린다");
            Check(!StarterPick.Open, "고른 뒤 선택 화면을 닫는다");
            var roster = LifeSystem.GetCharacters();
            Check(roster.Count == 5, $"고른 뒤 5인 (실제 {roster.Count})");
            Check(roster[0].Job == "마딜" && roster[0].Advancement == AdvancementTier.Basic
                  && roster[0].Name == "마법딜러",
                $"0번은 마딜 기본직업 (실제 {roster[0].Name} {roster[0].Job} {roster[0].Advancement})");
            Check(roster[0].Job != "마법사" && roster[0].Job != "수호기사",
                "고른 기본직업을 1차 이름으로 바꾸지 않는다");
            bool hasEach = true;
            for (int i = 0; i < LifeSystem.BasicJobs.Length; i++)
            {
                string job = LifeSystem.BasicJobs[i];
                bool found = false;
                for (int c = 0; c < roster.Count; c++)
                    if (roster[c].Job == job) { found = true; break; }
                if (!found) hasEach = false;
            }
            Check(hasEach, "5인 로스터에 기본직업이 한 명씩 있다");
            Check(LifeSystem.HasSavedRoster(), "고른 뒤 저장이 있다");
            Check(!StarterPick.ShouldOffer(), "저장이 있으면 다시 고르지 않는다");

            Check(StarterPick.TryChoose("딜"), "딜로 다시 고르면 덮어쓴다");
            roster = LifeSystem.GetCharacters();
            Check(roster[0].Job == "딜" && roster[0].Advancement == AdvancementTier.Basic
                  && roster[0].Name == "물리딜러",
                $"0번은 딜 (실제 {roster[0].Job} {roster[0].Name})");

            Environment.SetEnvironmentVariable(StarterPick.EnvNo, "1");
            StarterPick.ResetForTest();
            Check(!StarterPick.ShouldOffer() && !StarterPick.TryChoose("탱"),
                "QA_NO_START_PICK이면 거부");
            Check(LifeSystem.GetCharacters()[0].Job == "딜", "거부 뒤 0번은 그대로 딜");
            Environment.SetEnvironmentVariable(StarterPick.EnvNo, null);

            StarterPick.ResetForTest();
            Environment.SetEnvironmentVariable(StarterPick.EnvShow, "1");
            StarterPick.SeedQaIfRequested();
            Check(StarterPick.Open, "QA_START_PICK이면 선택 화면");
            Environment.SetEnvironmentVariable(StarterPick.EnvShow, null);

            _ = nameof(StarterPick.TryChoose);
            _ = nameof(StarterPick.SeedQaIfRequested);
            _ = nameof(LifeSystem.BeginNewGame);
            _ = nameof(UiPages.DrawJobLook);
            _ = nameof(UiPages.StarterLookWalks);
            _ = nameof(UiPages.JobLookFrame);
            _ = nameof(TitleScreen);

            Environment.SetEnvironmentVariable(StarterPick.EnvShow, show);
            Environment.SetEnvironmentVariable(StarterPick.EnvJob, jobEnv);
            Environment.SetEnvironmentVariable(StarterPick.EnvNo, no);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            StarterPick.ResetForTest();

            if (_fail == 0) Debug.Log("[StarterPickSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[StarterPickSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[StarterPickSelfCheck] FAIL {_fail}건");
        }
    }
}
