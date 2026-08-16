using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 사냥 시작 — 선택 후 스타트 → 전장, 배치 후 스타트 → 전투.
    /// QA_NO면 예전처럼 바로 싸운다.
    /// </summary>
    public static class HuntStartSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Hunt Start Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(HuntStart.EnvShow);
            string deploy = Environment.GetEnvironmentVariable(HuntStart.EnvDeploy);
            string no = Environment.GetEnvironmentVariable(HuntStart.EnvNo);
            Environment.SetEnvironmentVariable(HuntStart.EnvShow, null);
            Environment.SetEnvironmentVariable(HuntStart.EnvDeploy, null);
            Environment.SetEnvironmentVariable(HuntStart.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            HuntStart.ResetForTest();

            Check(HuntStart.Current == HuntStart.Phase.Idle, "기본은 Idle");
            Check(!HuntStart.ShouldHold && !HuntStart.Picking && !HuntStart.Deploying,
                "Idle이면 고르지도 배치도 아니다");
            Check(!HuntStart.ConfirmPick() && !HuntStart.ConfirmStart(),
                "Idle에서 스타트 두 번 다 거부");

            Check(HuntStart.BeginPick(), "사냥 시작은 선택을 연다");
            Check(HuntStart.Picking && HuntStart.Current == HuntStart.Phase.Picking,
                "BeginPick 뒤 Picking");
            Check(HuntStart.PickTitle.Contains("스타트"),
                $"선택 제목에 스타트 (실제 {HuntStart.PickTitle})");
            Check(HuntStart.PickSubtitle.Contains("전장"),
                "선택 부제에 전장");

            PartyState.SetSlotsForTest();
            Check(!PartyState.CanSortie, "빈 편성");
            Check(!HuntStart.ConfirmPick() && HuntStart.Picking,
                "한 명도 없으면 전장에 안 들어간다");

            _ = LifeSystem.GetCharacters();
            PartyState.SetSlotsForTest(0);
            Check(PartyState.CanSortie, "0번 한 명은 출전 가능");
            Check(HuntStart.ConfirmPick(), "고른 뒤 스타트 → 전장");
            Check(HuntStart.Deploying && HuntStart.ShouldHold,
                "전장에 들어가면 배치 중·전투 보류");
            Check(HuntStart.Selected == 0, "배치 시작은 0번을 고른다");

            var at = new Vector2(2.4f, -1.1f);
            Check(HuntStart.TryPlace(0, at), "배치 중이면 자리를 옮긴다");
            Check((HuntStart.PosOf(0) - at).sqrMagnitude < 0.0001f,
                $"옮긴 자리 (실제 {HuntStart.PosOf(0)})");
            Check(!HuntStart.TryPlace(9, at), "없는 슬롯은 거부");
            Check(!HuntStart.ConfirmPick(), "배치 중엔 선택 스타트가 아니다");

            Check(HuntStart.ConfirmStart(), "배치 뒤 스타트 → 전투");
            Check(HuntStart.Fighting && !HuntStart.ShouldHold && !HuntStart.Deploying,
                "전투가 시작되면 보류가 풀린다");
            Check(!HuntStart.TryPlace(0, Vector2.zero), "전투 중엔 배치가 아니다");
            Check(!HuntStart.ConfirmStart(), "두 번째 전투 스타트는 없다");

            HuntStart.ResetForTest();
            Check(HuntStart.BeginPick(), "다시 고른다");
            HuntStart.Cancel();
            Check(HuntStart.Current == HuntStart.Phase.Idle, "취소하면 Idle");

            var front = HuntStart.DefaultPos(0, "탱");
            var mid = HuntStart.DefaultPos(1, "딜");
            var back = HuntStart.DefaultPos(1, "힐");
            Check(front.y > mid.y && mid.y > back.y,
                $"기본 진형 탱 앞·딜 중간·힐 뒤 ({front.y:0.0}/{mid.y:0.0}/{back.y:0.0})");

            Environment.SetEnvironmentVariable(HuntStart.EnvNo, "1");
            HuntStart.ResetForTest();
            Check(HuntStart.Blocked, "QA_NO면 차단");
            Check(!HuntStart.BeginPick() && !HuntStart.Picking, "차단이면 선택을 안 연다");
            Check(!HuntStart.ShouldHold, "차단이면 전투를 안 보류한다");
            Check(HuntStart.Current == HuntStart.Phase.Idle, "차단은 Idle로 읽힌다");
            Environment.SetEnvironmentVariable(HuntStart.EnvNo, null);

            HuntStart.ResetForTest();
            Environment.SetEnvironmentVariable(HuntStart.EnvShow, "1");
            HuntStart.SeedQaIfRequested();
            Check(HuntStart.Picking, "QA_HUNT_START이면 선택 화면");
            Environment.SetEnvironmentVariable(HuntStart.EnvShow, null);

            HuntStart.ResetForTest();
            Environment.SetEnvironmentVariable(HuntStart.EnvDeploy, "1");
            HuntStart.SeedQaIfRequested();
            Check(HuntStart.Deploying && HuntStart.ShouldHold,
                "QA_HUNT_DEPLOY이면 전장 배치");
            Check(HuntStart.DeployTitle.Contains("스타트"),
                $"배치 제목에 스타트 (실제 {HuntStart.DeployTitle})");
            Environment.SetEnvironmentVariable(HuntStart.EnvDeploy, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string scripts = Path.Combine(Application.dataPath, "Scripts/W3Party.cs");
            string fieldSrc = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            string battleSrc = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string w3Src = File.ReadAllText(scripts);
            Check(fieldSrc.Contains("HuntStart.BeginPick"),
                "필드 사냥 시작이 BeginPick을 부른다 — GoBattle만이면 FAIL");
            Check(fieldSrc.Contains("HuntStart.ConfirmPick"),
                "필드 스타트가 ConfirmPick을 부른다");
            Check(battleSrc.Contains("HuntStart.ConfirmStart"),
                "전장 스타트가 ConfirmStart를 부른다");
            Check(battleSrc.Contains("CombatHeld"),
                "전장이 CombatHeld를 켠다");
            Check(w3Src.Contains("CombatHeld") && w3Src.Contains("ReleaseCombat"),
                "W3Party가 보류·해제를 갖는다");
            Check(w3Src.Contains("if (CombatHeld)") && w3Src.Contains("SpawnMob"),
                "보류 중엔 스폰을 건너뛴다");

            _ = nameof(HuntStart.BeginPick);
            _ = nameof(HuntStart.ConfirmPick);
            _ = nameof(HuntStart.ConfirmStart);
            _ = nameof(HuntStart.TryPlace);
            _ = nameof(HuntStart.SeedQaIfRequested);
            _ = nameof(HuntStart.ShouldHold);

            Environment.SetEnvironmentVariable(HuntStart.EnvShow, show);
            Environment.SetEnvironmentVariable(HuntStart.EnvDeploy, deploy);
            Environment.SetEnvironmentVariable(HuntStart.EnvNo, no);
            HuntStart.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[HuntStartSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("HuntStartSelfCheck FAIL " + _fail);
            }
            Debug.Log("[HuntStartSelfCheck] PASS\n" + _log);
        }
    }
}
