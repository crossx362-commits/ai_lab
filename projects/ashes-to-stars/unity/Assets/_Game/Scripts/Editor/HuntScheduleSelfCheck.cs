using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 자동사냥 일정 — 허브에서도 돈다. 사망 없음. QA_NO면 0(§6).</summary>
    public static class HuntScheduleSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Hunt Schedule Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(HuntSchedule.EnvShow);
            string no = Environment.GetEnvironmentVariable(HuntSchedule.EnvNo);
            Environment.SetEnvironmentVariable(HuntSchedule.EnvShow, null);
            Environment.SetEnvironmentVariable(HuntSchedule.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            HuntSchedule.ResetForTest();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);
            _ = LifeSystem.GetCharacters();
            _ = PartyState.Slots;

            Check(Mathf.Abs(HuntSchedule.CapSeconds - 12f * 3600f) < 0.01f, "상한 12시간");
            Check(!HuntSchedule.Running && HuntSchedule.Count == 0, "시작 전 꺼짐");
            Check(PartyState.Slots.Count > 0, $"편성 {PartyState.Slots.Count}명");

            int beforeDeath = LifeSystem.GetCharacters()[0].DeathCount;
            long beforeExp = LifeSystem.GetCharacters()[0].Exp;
            int partyN = PartyState.Slots.Count;
            Check(HuntSchedule.TryStart(), "편성으로 일정 시작");
            Check(HuntSchedule.Running && HuntSchedule.Count == partyN,
                $"일정 {partyN}명 (실제 {HuntSchedule.Count})");
            Check(!PartyState.CanSortie, "전원 일정이면 출전 불가");
            Check(HuntSchedule.Contains(0), "0번은 일정");
            Check(!PartyState.Toggle(0), "일정 중인 캐릭터는 편성 거부");

            HuntSchedule.Tick(3600f);
            Check(Mathf.Abs(HuntSchedule.Elapsed - 3600f) < 0.01f,
                $"3600초 경과 (실제 {HuntSchedule.Elapsed})");
            Check(HuntSchedule.PendingGold() == 10_000,
                $"T1 3600초 대기 1골드 (실제 {HuntSchedule.PendingGold()})");
            Check(HuntSchedule.Line().Contains("1골드") && HuntSchedule.Line().Contains("§6"),
                $"문구 1골드 (실제 {HuntSchedule.Line()})");

            HuntSchedule.Tick(12f * 3600f);
            Check(Mathf.Abs(HuntSchedule.Elapsed - HuntSchedule.CapSeconds) < 0.01f,
                $"12시간+1초도 12시간 (실제 {HuntSchedule.Elapsed})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            HuntSchedule.ResetForTest();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);
            _ = LifeSystem.GetCharacters();
            _ = PartyState.Slots;
            beforeDeath = LifeSystem.GetCharacters()[0].DeathCount;
            beforeExp = LifeSystem.GetCharacters()[0].Exp;
            Check(HuntSchedule.TryStart(), "정산용 시작");
            HuntSchedule.Tick(3600f);
            Check(HuntSchedule.Stop(), "정산 중지");
            Check(!HuntSchedule.Running, "중지 뒤 꺼짐");
            Check(GameState.Wallet.Copper == 10_000,
                $"정산 1골드 (실제 {GameState.Wallet.Copper})");
            Check(LifeSystem.GetCharacters()[0].DeathCount == beforeDeath,
                $"사망 카운트 불변 (실제 {LifeSystem.GetCharacters()[0].DeathCount})");
            Check(LifeSystem.GetCharacters()[0].Exp > beforeExp
                  || LifeSystem.GetCharacters()[0].Level > 10,
                "경험치가 올랐다");

            HuntSchedule.ForgetInMemoryForTest();
            Check(!HuntSchedule.Running && GameState.Wallet.Copper == 10_000,
                "재기동 뒤 일정은 꺼져 있고 골드는 남는다");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            HuntSchedule.ResetForTest();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            Environment.SetEnvironmentVariable(HuntSchedule.EnvNo, "1");
            _ = LifeSystem.GetCharacters();
            _ = PartyState.Slots;
            Check(HuntSchedule.Blocked, "QA_NO면 차단");
            Check(!HuntSchedule.TryStart(), "차단하면 시작 거부");
            Check(!HuntSchedule.Running, "차단하면 안 돈다");
            Check(HuntSchedule.Line().Contains("없음"),
                $"차단 문구 (실제 {HuntSchedule.Line()})");
            Check(GameState.Wallet.Copper == 0, "차단하면 골드 0");
            Environment.SetEnvironmentVariable(HuntSchedule.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            HuntSchedule.ResetForTest();
            Environment.SetEnvironmentVariable(HuntSchedule.EnvShow, "1");
            HuntSchedule.SeedQaIfRequested();
            Check(HuntSchedule.Running, "시드면 일정 중");
            Check(HuntSchedule.PendingGold() == 10_000,
                $"시드 대기 1골드 (실제 {HuntSchedule.PendingGold()})");
            Check(HuntSchedule.Line().Contains("§6"), "시드 문구 §6");
            Environment.SetEnvironmentVariable(HuntSchedule.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string field = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            string game = File.ReadAllText(Path.Combine(runtime, "GameScreen.cs"));
            string party = File.ReadAllText(Path.Combine(runtime, "PartyState.cs"));
            Check(field.Contains("HuntSchedule.SeedQaIfRequested")
                  && field.Contains("HuntSchedule.CardTitle")
                  && field.Contains("HuntSchedule.Line"),
                "필드가 시드·카드·자막을 읽는다");
            Check(game.Contains("HuntSchedule.Tick"), "허브 Update가 Tick을 읽는다");
            Check(party.Contains("HuntSchedule.Contains"), "편성이 일정을 읽는다");

            _ = nameof(HuntSchedule.TryStart);
            _ = nameof(HuntSchedule.Stop);
            _ = nameof(HuntSchedule.Tick);
            _ = nameof(HuntSchedule.Line);
            _ = nameof(HuntSchedule.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(HuntSchedule.EnvShow, show);
            Environment.SetEnvironmentVariable(HuntSchedule.EnvNo, no);
            HuntSchedule.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[HuntScheduleSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("HuntScheduleSelfCheck FAIL " + _fail);
            }
            Debug.Log("[HuntScheduleSelfCheck] PASS\n" + _log);
        }
    }
}
