using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 합성 첫 슬라이스 자가검사. 재료가 사라지고 강골이 다음 판 HpMul에 남는지 본다.
    /// </summary>
    public static class FusionSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Fusion Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DefenseState.ResetForTest();
            Fusion.ForcePick = null;

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count >= 2, $"로스터 자동 생성 (실제 {roster.Count})");
            var host = roster[0];
            var basic = roster[1];

            Check(!Fusion.CanBeMaterial(basic), "기본직업은 합성 재료가 아니다(§3)");
            Check(!Fusion.TryFuse(host, basic, 1u, out _), "기본직업 재료는 거부");
            Check(roster.Count >= 2, "거부하면 재료가 남아 있다");

            host.Advancement = AdvancementTier.First;
            host.Job = "수호기사";
            host.Level = 20;
            Check(!Fusion.TryFuse(host, host, 1u, out _), "자기 자신은 재료가 아니다");

            basic.IsSpecialJob = true;
            basic.Advancement = AdvancementTier.First;
            basic.Job = "수호기사";
            Check(!Fusion.CanBeMaterial(basic), "특수 직업은 합성 재료가 아니다(§3)");
            basic.IsSpecialJob = false;
            Check(Fusion.CanBeMaterial(basic), "1차 전직은 재료가 된다");

            Fusion.ForcePick = BoonId.강골;
            int before = roster.Count;
            Check(Fusion.TryFuse(host, basic, 1u, out var first), "수호기사 재료 → 강골 흡수");
            Check(first == BoonId.강골, "강제 추첨이 강골이다");
            Check(roster.Count == before - 1, "재료가 로스터에서 사라진다");
            Check(!LifeSystem.GetDeletedCharacters().Contains(basic), "합성 소멸은 영묘에 안 간다(§3)");
            Check(host.AbsorbedBoons.Count == 1 && host.AbsorbedBoons[0] == (int)BoonId.강골,
                "호스트가 강골 1개를 가진다");
            Check(Mathf.Approximately(Fusion.HpMulOf(host), 1.25f), "강골 체력 배율 1.25");

            LifeSystem.PersistRoster();
            LifeSystem.ForgetInMemoryForTest();
            var again = LifeSystem.GetCharacters();
            Check(again.Count == before - 1 && again[0].AbsorbedBoons.Count == 1
                  && again[0].AbsorbedBoons[0] == (int)BoonId.강골,
                "흡수 패시브가 저장에서 되살아난다");
            host = again[0];

            PartyState.ResetForTest();
            var sortie = PartyState.SortieCombatants();
            Check(sortie.Count > 0 && Mathf.Approximately(sortie[0].HpMul, 1.25f),
                "출전 계약이 강골을 전투 HpMul에 곱한다");
            Check(Mathf.Approximately(global::W3Party.GearHpMultiplier(sortie[0].HpMul), 1.25f),
                "전투 HP 경로가 출전 배율을 읽는다");

            string old = Environment.GetEnvironmentVariable("QA_NO_FUSION");
            Environment.SetEnvironmentVariable("QA_NO_FUSION", "1");
            Check(Mathf.Approximately(Fusion.HpMulOf(host), 1f), "QA_NO_FUSION=1이면 배율 1");
            Environment.SetEnvironmentVariable("QA_NO_FUSION", old);

            var extra = new CharacterRecord("재료2", "수호기사", 20, AdvancementTier.First);
            again.Add(extra);
            Fusion.ForcePick = BoonId.강골;
            Check(!Fusion.TryFuse(host, extra, 2u, out _), "이미 가진 강골은 추첨에서 빠진다");
            Check(again.Contains(extra), "풀이 비면 재료를 소비하지 않는다");
            Fusion.ForcePick = BoonId.방벽;
            Check(Fusion.TryFuse(host, extra, 2u, out var second) && second == BoonId.방벽,
                "남은 방벽을 흡수한다");
            Check(host.AbsorbedBoons.Count == 2, "슬롯이 2개다");

            for (int i = host.AbsorbedBoons.Count; i < Fusion.SlotCap; i++)
            {
                var filler = new CharacterRecord("채움" + i, "검사", 20, AdvancementTier.First);
                LifeSystem.GetCharacters().Add(filler);
                Fusion.ForcePick = i == 2 ? BoonId.예리함 : BoonId.숙련;
                if (host.AbsorbedBoons.Contains((int)Fusion.ForcePick.Value))
                    Fusion.ForcePick = BoonId.집중;
                Check(Fusion.TryFuse(host, filler, (uint)(10 + i), out _), $"슬롯 채움 {i + 1}/4");
            }
            Check(host.AbsorbedBoons.Count == Fusion.SlotCap, "슬롯 상한 4");

            var overflow = new CharacterRecord("넘침", "마법사", 20, AdvancementTier.First);
            LifeSystem.GetCharacters().Add(overflow);
            Fusion.ForcePick = BoonId.분노;
            Check(Fusion.TryFuse(host, overflow, 99u, out var pending) && pending == BoonId.분노,
                "4칸이 차면 결과는 보류다");
            Check(host.AbsorbedBoons.Count == Fusion.SlotCap && host.PendingBoon == (int)BoonId.분노,
                "보류는 기존 4칸을 바로 덮지 않는다");
            int slot0 = host.AbsorbedBoons[0];
            Check(Fusion.DiscardPending(host) && host.PendingBoon < 0
                  && host.AbsorbedBoons[0] == slot0,
                "보류를 버리면 기존 슬롯이 유지된다");

            LifeSystem.GetCharacters().Add(overflow = new CharacterRecord("넘침2", "마법사", 20, AdvancementTier.First));
            Fusion.ForcePick = BoonId.분노;
            Check(Fusion.TryFuse(host, overflow, 100u, out _), "다시 보류");
            Check(Fusion.AcceptReplace(host, 0) && host.AbsorbedBoons[0] == (int)BoonId.분노
                  && host.PendingBoon < 0,
                "교체하면 고른 칸만 바뀐다");

            GameState.Gain(Economy.LifeItem.RebornStone);
            host.IsDeleted = true;
            Check(LifeSystem.UseRebornStone(host), "환생석 사용");
            Check(host.AbsorbedBoons.Count == 0 && host.PendingBoon < 0,
                "환생하면 흡수 패시브가 전부 소멸한다(§4)");

            Fusion.ForcePick = null;
            _ = nameof(Fusion.TryFuse);
            _ = nameof(Fusion.HpMulOf);
            _ = nameof(LifeSystem.SacrificeForFusion);

            if (_fail == 0) Debug.Log("[FusionSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[FusionSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[FusionSelfCheck] FAIL {_fail}건");
        }
    }
}
