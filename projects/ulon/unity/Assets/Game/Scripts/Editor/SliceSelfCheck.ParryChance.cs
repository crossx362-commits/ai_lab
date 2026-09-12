using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.3. 방패가 있으면 막기 판정. NC: NcNeverBlock 이면 막음이 없다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertParryChance()
        {
            if (ParryChance.NcNeverBlock)
                throw new InvalidOperationException("ParryChance.NcNeverBlock 가 켜져 있으면 막음이 없습니다.");

            string path = DataLedger.PathOf(ParryChance.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("막기 원장이 없습니다: " + path);
            ParryChance.Reload();
            if (Math.Abs(ParryChance.FileShieldMul - 0.3f) > 0.01f)
                throw new InvalidOperationException("막기 shield_mul이 0.3이 아닙니다: " + ParryChance.FileShieldMul);
            if (Math.Abs(ParryChance.FileMin) > 0.01f)
                throw new InvalidOperationException("막기 하한이 0이 아닙니다: " + ParryChance.FileMin);
            if (Math.Abs(ParryChance.FileMax - 1f) > 0.01f)
                throw new InvalidOperationException("막기 상한이 1이 아닙니다: " + ParryChance.FileMax);

            float gm = ParryChance.Percent(100f);
            if (Math.Abs(gm - 0.3f) > 0.01f)
                throw new InvalidOperationException("방패술 100 막기가 30%가 아닙니다: " + gm);
            float zero = ParryChance.Percent(0f);
            if (Math.Abs(zero) > 0.01f)
                throw new InvalidOperationException("방패술 0 막기가 0이 아닙니다: " + zero);
            if (gm <= zero)
                throw new InvalidOperationException("방패술이 높으면 막기가 높아야 합니다: " + gm + " vs " + zero);

            var empty = new SkillSet();
            if (ParryChance.Blocks(false, empty, null, 20f, 0f))
                throw new InvalidOperationException("방패 없이 막히면 안 됩니다.");
            if (Math.Abs(empty.Get(SkillId.Parrying)) > 0.0001f)
                throw new InvalidOperationException("방패 없는 막기는 방패술을 올리면 안 됩니다.");

            var gmSkills = new SkillSet();
            var gmVals = new float[(int)SkillId.Count];
            gmVals[(int)SkillId.Parrying] = 100f;
            gmSkills.ApplyNetworkValues(gmVals);
            if (!ParryChance.Blocks(true, gmSkills, null, 20f, 0f))
                throw new InvalidOperationException("방패술 100·굴림 0은 막혀야 합니다.");
            if (ParryChance.Blocks(true, gmSkills, null, 20f, 0.99f))
                throw new InvalidOperationException("방패술 100·굴림 0.99는 30%에서 안 막혀야 합니다.");

            string combatPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Combat.cs");
            string combat = File.ReadAllText(combatPath);
            if (combat.IndexOf("ParryChance.Blocks", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("TryAttack이 ParryChance.Blocks를 안 탑니다.");
            if (combat.IndexOf("ParryChance.NextRoll", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("TryAttack이 ParryChance.NextRoll을 안 탑니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("ParryChance.Percent", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 막기 확률이 없습니다.");
            if (hud.IndexOf("LastCombatMessage", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD가 막음 안내를 안 읽습니다.");

            float savedHit = HitChance.ForcedRoll;
            float savedParry = ParryChance.ForcedRoll;
            var worldGo = new GameObject("selfcheck-parry-chance-world");
            var atkGo = new GameObject("selfcheck-parry-chance-atk");
            var missGo = new GameObject("selfcheck-parry-chance-open");
            var defGo = new GameObject("selfcheck-parry-chance-def");
            try
            {
                var world = worldGo.AddComponent<OfflineWorld>();
                var atk = atkGo.AddComponent<WorldBody>();
                var opener = missGo.AddComponent<WorldBody>();
                var def = defGo.AddComponent<WorldBody>();
                atk.IsAvatar = true;
                opener.IsAvatar = true;
                def.IsEnemy = true;
                def.MaxHp = 400f;
                def.SetHp(400f);
                atkGo.transform.position = Vector3.zero;
                missGo.transform.position = Vector3.zero;
                defGo.transform.position = new Vector3(1f, 0f, 0f);
                var defBag = defGo.AddComponent<InventoryBag>();
                defBag.Add(new ItemRecord { TemplateId = ItemCatalog.WoodenShield, Amount = 1, Uses = 30 });
                var defVals = new float[(int)SkillId.Count];
                defVals[(int)SkillId.Parrying] = 100f;
                world.SkillsOf(def).ApplyNetworkValues(defVals);

                HitChance.ForcedRoll = 0f;
                ParryChance.ForcedRoll = 0f;
                AttackResult blocked = world.TryAttack(atk, def);
                if (!blocked.Applied || blocked.Hit || blocked.Damage != 0 || blocked.FailReason != "parry")
                    throw new InvalidOperationException("굴림 0 막기는 parry여야 합니다: hit=" + blocked.Hit + " dmg=" + blocked.Damage + " " + blocked.FailReason);
                if (Math.Abs(def.Hp - 400f) > 0.01f)
                    throw new InvalidOperationException("막음은 HP를 안 깎아야 합니다: " + def.Hp);
                if (world.LastCombatMessage.IndexOf("막음", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("막음 안내가 없습니다: " + world.LastCombatMessage);

                ParryChance.ForcedRoll = 0.99f;
                AttackResult open = world.TryAttack(opener, def);
                if (!open.Applied || !open.Hit || open.Damage <= 0)
                    throw new InvalidOperationException("굴림 0.99는 막히지 않고 맞아야 합니다: hit=" + open.Hit + " dmg=" + open.Damage + " " + open.FailReason);
                if (Math.Abs(def.Hp - (400f - open.Damage)) > 0.01f)
                    throw new InvalidOperationException("못 막으면 HP가 깎여야 합니다: " + def.Hp);
            }
            finally
            {
                HitChance.ForcedRoll = savedHit;
                ParryChance.ForcedRoll = savedParry;
                UnityEngine.Object.DestroyImmediate(worldGo);
                UnityEngine.Object.DestroyImmediate(atkGo);
                UnityEngine.Object.DestroyImmediate(missGo);
                UnityEngine.Object.DestroyImmediate(defGo);
            }

            Debug.Log("[Ulon] 막기 — 방패술 · TryAttack ParryChance · HUD · 막음 HP 유지");
        }

        static void AssertParryChanceNegativeControl()
        {
            bool was = ParryChance.NcNeverBlock;
            bool red = false;
            try
            {
                ParryChance.NcNeverBlock = true;
                try { AssertParryChance(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { ParryChance.NcNeverBlock = was; }
            if (!red)
                throw new InvalidOperationException("막기 네거티브 컨트롤 실패 — NcNeverBlock 인데 통과했습니다.");
            Debug.Log("[Ulon] 막기 네거티브 컨트롤 통과 — NcNeverBlock 이면 FAIL");
        }
    }
}
