using System;
using System.IO;
using FishNet.Object;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        // **상대에게 거는 주문**(랩 ㉱) — 속박·약화·전격.
        // 담는 것: 남을 묶고 깎고 때리는 주문. 안 담는 것: 나를 지키는 주문(`MageryGuard`).
        static void AssertBind()
        {
            AssertVillageIntact();

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Bind) != "속박")
                throw new InvalidOperationException("SpellId.Bind 한글은 속박이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Bind) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("속박 마나는 불씨와 같아야 합니다.");
            if (SpellCast.BindSeconds != 4f)
                throw new InvalidOperationException("BindSeconds는 4이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Bind) || SpellCast.CastTimeOf(SpellId.Bind) != 0f)
                throw new InvalidOperationException("속박은 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-bind-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-bind-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "bind-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Bind, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 속박은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Bind);

                var noTgt = world.TryCast(caster, SpellId.Bind, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 속박은 실패해야 합니다.");

                palGo = new GameObject("selfcheck-bind-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "bind-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Bind, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("속박은 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-bind-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-bind-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Bind, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 속박은 실패해야 합니다.");

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Bind, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 속박은 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                Vector3 mobPos = mobGo.transform.position;
                var cast = world.TryCast(caster, SpellId.Bind, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("속박 시전 실패: " + cast.FailReason);
                if (!mob.IsRooted(Time.time))
                    throw new InvalidOperationException("속박 후 RootUntil이 활성이어야 합니다.");
                if (mob.RootUntil < Time.time + SpellCast.BindSeconds - 0.05f)
                    throw new InvalidOperationException("RootUntil은 약 4초여야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("속박 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("속박은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("속박은 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("속박 후 CastingUntil이 있으면 안 됩니다.");
                if ((mobGo.transform.position - mobPos).sqrMagnitude > 0.0001f)
                    throw new InvalidOperationException("속박 시전은 몹 위치를 바꾸면 안 됩니다.");

                float hp0 = caster.Hp;
                bool struck = world.TryEnemyStrike(mob, caster);
                if (struck || caster.Hp < hp0 - 0.01f)
                    throw new InvalidOperationException("속박 중 몹은 추격/반격하면 안 됩니다.");

                mob.RootUntil = 0f;
                bool freeStrike = world.TryEnemyStrike(mob, caster);
                if (!freeStrike)
                    throw new InvalidOperationException("속박 해제 후 몹 반격이 되어야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertWeaken()
        {
            AssertVillageIntact();

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Weaken) != "약화")
                throw new InvalidOperationException("SpellId.Weaken 한글은 약화이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Weaken) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("약화 마나는 불씨와 같아야 합니다.");
            if (SpellCast.WeakenSeconds != 6f)
                throw new InvalidOperationException("WeakenSeconds는 6이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Weaken) || SpellCast.CastTimeOf(SpellId.Weaken) != 0f)
                throw new InvalidOperationException("약화는 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-weaken-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-weaken-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "weaken-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Weaken, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 약화는 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Weaken);

                var noTgt = world.TryCast(caster, SpellId.Weaken, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 약화는 실패해야 합니다.");

                palGo = new GameObject("selfcheck-weaken-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "weaken-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Weaken, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("약화는 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-weaken-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-weaken-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Weaken, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 약화는 실패해야 합니다.");

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Weaken, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 약화는 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float hpBase = caster.Hp;
                bool baseStruck = world.TryEnemyStrike(mob, caster);
                if (!baseStruck || caster.Hp >= hpBase)
                    throw new InvalidOperationException("약화 기준 몹 반격이 되어야 합니다.");
                float baseLost = hpBase - caster.Hp;
                if (baseLost < 1f)
                    throw new InvalidOperationException("약화 기준 피해가 있어야 합니다.");
                caster.ResetHp();
                mob.WeakenUntil = 0f;

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var cast = world.TryCast(caster, SpellId.Weaken, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("약화 시전 실패: " + cast.FailReason);
                if (!mob.IsWeakened(Time.time))
                    throw new InvalidOperationException("약화 후 WeakenUntil이 활성이어야 합니다.");
                if (mob.WeakenUntil < Time.time + SpellCast.WeakenSeconds - 0.05f)
                    throw new InvalidOperationException("WeakenUntil은 약 6초여야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("약화 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("약화는 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("약화는 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("약화 후 CastingUntil이 있으면 안 됩니다.");

                float hp1 = caster.Hp;
                bool weakStruck = world.TryEnemyStrike(mob, caster);
                if (!weakStruck)
                    throw new InvalidOperationException("약화 중에도 몹 반격은 되어야 합니다.");
                float weakLost = hp1 - caster.Hp;
                float expect = baseLost / 2f;
                if (Math.Abs(weakLost - expect) > 0.0001f)
                    throw new InvalidOperationException("약화 중 피해는 절반이어야 합니다: " + weakLost + " vs " + expect + " (base " + baseLost + ")");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertSpark()
        {
            AssertVillageIntact();

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Spark) != "섬광")
                throw new InvalidOperationException("SpellId.Spark 한글은 섬광이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Spark) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("섬광 마나는 불씨와 같아야 합니다.");
            if (SpellCast.SparkRange >= SpellCast.EmberRange)
                throw new InvalidOperationException("섬광 사거리는 불씨보다 짧아야 합니다.");
            if (SpellCast.RangeOf(SpellId.Spark) != SpellCast.SparkRange || SpellCast.SparkRange != 6f)
                throw new InvalidOperationException("섬광 사거리는 6이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Spark) || SpellCast.CastTimeOf(SpellId.Spark) != 0f)
                throw new InvalidOperationException("섬광은 즉시 시전이어야 합니다.");
            var plain = new StatSet();
            plain.ForceSet(20, 20, 20);
            if (SpellCast.SparkDamage(plain, new SkillSet()) >= SpellCast.EmberDamage(plain, new SkillSet()))
                throw new InvalidOperationException("섬광 피해는 불씨보다 낮아야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-spark-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-spark-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "spark-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Spark, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 섬광은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Spark);

                var noTgt = world.TryCast(caster, SpellId.Spark, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 섬광은 실패해야 합니다.");

                palGo = new GameObject("selfcheck-spark-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "spark-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Spark, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("섬광은 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-spark-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-spark-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Spark, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 섬광은 실패해야 합니다.");

                // Ember range ok but Spark shorter: place mob between SparkRange and EmberRange
                farGo.transform.position = casterGo.transform.position + new Vector3(7f, 0f, 0f);
                var midFar = world.TryCast(caster, SpellId.Spark, far);
                if (midFar.Applied || midFar.FailReason != "range")
                    throw new InvalidOperationException("섬광은 불씨보다 짧은 사거리여야 합니다.");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Spark, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 섬광은 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                float hp0 = mob.Hp;
                var cast = world.TryCast(caster, SpellId.Spark, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("섬광 시전 실패: " + cast.FailReason);
                if (!cast.Hit || cast.Damage <= 0)
                    throw new InvalidOperationException("섬광은 피해를 줘야 합니다.");
                if (mob.Hp >= hp0)
                    throw new InvalidOperationException("섬광 후 몹 HP가 줄어야 합니다.");
                if (cast.Damage >= SpellCast.EmberDamage(world.StatsOf(caster), world.SkillsOf(caster)))
                    throw new InvalidOperationException("섬광 피해는 같은 스탯에서 불씨보다 낮아야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("섬광 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("섬광은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("섬광은 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("섬광 후 CastingUntil이 있으면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }
    }
}
