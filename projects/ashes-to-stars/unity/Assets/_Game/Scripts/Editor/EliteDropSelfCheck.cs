using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>정예 주 드랍은 강화석·일반 장비. QA_NO면 옛 0(§10-8).</summary>
    public static class EliteDropSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Elite Drop Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EliteDrop.EnvShow);
            string no = Environment.GetEnvironmentVariable(EliteDrop.EnvNo);
            Environment.SetEnvironmentVariable(EliteDrop.EnvShow, null);
            Environment.SetEnvironmentVariable(EliteDrop.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EliteDrop.ResetForTest();
            DungeonRun.End();
            _ = LifeSystem.GetCharacters();

            Check(!EliteDrop.Blocked, "기본은 켜짐");
            Check(EliteDrop.Applies(NodeKind.정예), "정예 노드");
            Check(!EliteDrop.Applies(NodeKind.전투)
                  && !EliteDrop.Applies(NodeKind.보스)
                  && !EliteDrop.Applies(NodeKind.강화),
                "전투·보스·강화는 아님");
            Check(EliteDrop.Grade == GearGrade.Common, "일반");
            Check(Equipment.GradeLabel(EliteDrop.Grade) == "일반", "일반 라벨");

            var rng = new Rng(20260818u);
            int stones = GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone);
            int before = Equipment.Unequipped().Count;
            var drop = EliteDrop.Apply(NodeKind.정예, ref rng);
            Check(drop != null && drop.Grade == GearGrade.Common,
                $"정예 일반 (실제 {drop?.Grade})");
            Check(Equipment.Unequipped().Count == before + 1, "가방 +1");
            Check(GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) == stones + EliteDrop.Stones,
                $"강화석 +{EliteDrop.Stones}");
            Check(EliteDrop.Format(drop).IndexOf("일반", StringComparison.Ordinal) >= 0
                  && EliteDrop.Format(drop).IndexOf("§10-8", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {EliteDrop.Format(drop)})");
            Check(EliteDrop.Line().IndexOf("정예 일반 장비", StringComparison.Ordinal) >= 0,
                $"화면 줄 (실제 {EliteDrop.Line()})");

            var skip = new Rng(3u);
            int skipGear = Equipment.Unequipped().Count;
            int skipStone = GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone);
            Check(EliteDrop.Apply(NodeKind.전투, ref skip) == null
                  && Equipment.Unequipped().Count == skipGear
                  && GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) == skipStone,
                "전투 노드는 0");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EliteDrop.ResetForTest();
            _ = LifeSystem.GetCharacters();
            Environment.SetEnvironmentVariable(EliteDrop.EnvNo, "1");
            var blockedRng = new Rng(1u);
            Check(EliteDrop.Blocked, "QA_NO");
            Check(EliteDrop.Apply(NodeKind.정예, ref blockedRng) == null, "QA_NO면 0");
            Check(Equipment.Unequipped().Count == 0, "QA_NO 가방 비움");
            Check(GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) == 0, "QA_NO 석 0");
            Check(EliteDrop.Line().IndexOf("없음", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {EliteDrop.Line()})");
            Environment.SetEnvironmentVariable(EliteDrop.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EliteDrop.ResetForTest();
            _ = LifeSystem.GetCharacters();
            GameState.Gain(Economy.LifeItem.EnhanceStone, 1);
            while (BagSlots.Used() < BagSlots.Cap)
            {
                if (Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe) == null) break;
            }
            var fullRng = new Rng(2u);
            int full = Equipment.Unequipped().Count;
            int fullStone = GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone);
            Check(!BagSlots.CanAddGear(), "가방 가득");
            Check(EliteDrop.Apply(NodeKind.정예, ref fullRng) == null
                  && Equipment.Unequipped().Count == full,
                "가득이면 장비 거부");
            Check(GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) == fullStone + EliteDrop.Stones,
                "가득이어도 있던 석은 받는다");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EliteDrop.ResetForTest();
            DungeonRun.End();
            _ = LifeSystem.GetCharacters();
            int elite = -1;
            for (uint seed = 1; seed <= 24 && elite < 0; seed++)
            {
                DungeonRun.End();
                DungeonRun.Begin(seed, 0, DungeonKind.일반, GameFlow.Field);
                if (!DungeonRun.Active) continue;
                for (int i = 0; i < DungeonRun.Plan.Nodes.Length; i++)
                    if (DungeonRun.Plan.Nodes[i].Kind == NodeKind.정예) { elite = i; break; }
            }
            Check(elite >= 0, $"정예 노드 (실제 {elite})");
            if (elite >= 0)
            {
                int runGear = Equipment.Unequipped().Count;
                int runStone = GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone);
                DungeonRun.EnterForTest(elite);
                DungeonRun.Complete(true);
                Check(DungeonRun.State.ElitesKilled == 1, "처치 1");
                Check(Equipment.Unequipped().Count == runGear + 1, "Complete가 장비를 읽는다");
                Check(GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) == runStone + 1,
                    "Complete가 강화석을 읽는다");
                Check(EliteDrop.LastLine.IndexOf("일반", StringComparison.Ordinal) >= 0,
                    $"Complete 줄 (실제 {EliteDrop.LastLine})");
            }

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            EliteDrop.ResetForTest();
            Environment.SetEnvironmentVariable(EliteDrop.EnvShow, "1");
            EliteDrop.SeedQaIfRequested();
            Check(EliteDrop.ShowQa, "시드 ShowQa");
            Check(EliteDrop.Line().IndexOf("정예 일반 장비", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {EliteDrop.Line()})");
            bool seeded = false;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].Grade == GearGrade.Common
                    && bag[i].RecipeId == Equipment.LeatherArmorRecipe)
                    seeded = true;
            Check(seeded, "시드 가방 일반 흉갑");
            Check(GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) >= 1, "시드 강화석");
            Environment.SetEnvironmentVariable(EliteDrop.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string dungeonSrc = File.ReadAllText(Path.Combine(runtime, "DungeonRun.cs"));
            string battleSrc = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string mapSrc = File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"));
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(dungeonSrc.IndexOf("EliteDrop.Apply", StringComparison.Ordinal) >= 0,
                "정예 노드가 Apply를 읽는다");
            Check(battleSrc.IndexOf("EliteDrop.Line", StringComparison.Ordinal) >= 0,
                "전투가 줄을 읽는다");
            Check(mapSrc.IndexOf("EliteDrop.Line", StringComparison.Ordinal) >= 0,
                "던전 지도가 줄을 읽는다");
            Check(charSrc.IndexOf("EliteDrop.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "캐릭터창이 시드를 읽는다");

            _ = nameof(EliteDrop.Apply);
            _ = nameof(EliteDrop.Applies);
            _ = nameof(EliteDrop.Line);
            _ = nameof(EliteDrop.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(EliteDrop.EnvShow, show);
            Environment.SetEnvironmentVariable(EliteDrop.EnvNo, no);
            EliteDrop.ResetForTest();
            DungeonRun.End();
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            if (_fail == 0) Debug.Log("[EliteDropSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EliteDropSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EliteDropSelfCheck] FAIL {_fail}건");
        }
    }
}
