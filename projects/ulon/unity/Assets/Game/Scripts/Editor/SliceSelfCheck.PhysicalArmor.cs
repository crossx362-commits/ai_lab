using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.3. 철 갑옷은 물리 피해만 깎는다. NC: NcOff 이면 깎임이 없다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertPhysicalArmor()
        {
            if (PhysicalArmor.NcOff)
                throw new InvalidOperationException("PhysicalArmor.NcOff 가 켜져 있으면 AR이 안 깎입니다.");

            string path = DataLedger.PathOf(PhysicalArmor.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("물리 방어 원장이 없습니다: " + path);
            PhysicalArmor.Reload();
            ItemData.Reload();
            if (PhysicalArmor.FileMinRemaining != 0)
                throw new InvalidOperationException("물리 방어 하한이 0이 아닙니다: " + PhysicalArmor.FileMinRemaining);

            if (ItemCatalog.ArmorOf(ItemCatalog.IronPlate) != PhysicalArmor.FallbackPlate)
                throw new InvalidOperationException("철 갑옷 AR이 4가 아닙니다: " + ItemCatalog.ArmorOf(ItemCatalog.IronPlate));
            if (ItemCatalog.ArmorOf(ItemCatalog.WoodenShield) != 0)
                throw new InvalidOperationException("나무 방패 AR은 0이어야 합니다(막기와 겹침): " + ItemCatalog.ArmorOf(ItemCatalog.WoodenShield));
            if (ItemCatalog.ArmorOf(ItemCatalog.IronSword) != 0)
                throw new InvalidOperationException("철검은 AR이 없어야 합니다: " + ItemCatalog.ArmorOf(ItemCatalog.IronSword));
            if (PhysicalArmor.Apply(12, 4) != 8)
                throw new InvalidOperationException("피해 12 AR 4는 8이어야 합니다: " + PhysicalArmor.Apply(12, 4));
            if (PhysicalArmor.Apply(3, 4) != 0)
                throw new InvalidOperationException("AR이 더 크면 0이어야 합니다: " + PhysicalArmor.Apply(3, 4));
            if (PhysicalArmor.Apply(0, 4) != 0)
                throw new InvalidOperationException("피해 0은 그대로여야 합니다.");

            string combatPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Combat.cs");
            string combat = File.ReadAllText(combatPath);
            if (combat.IndexOf("PhysicalArmor.Apply", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("TryAttack이 PhysicalArmor.Apply를 안 탑니다.");

            string mageryPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Magery.cs");
            string magery = File.ReadAllText(mageryPath);
            if (magery.IndexOf("PhysicalArmor", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("마법이 PhysicalArmor를 타면 단순 방어력으로 마법을 막습니다.");

            string strikePath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Stealth.cs");
            string strike = File.ReadAllText(strikePath);
            if (strike.IndexOf("PhysicalArmor.Apply", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("반격이 PhysicalArmor.Apply를 안 탑니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("PhysicalArmor.Of", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 물리 방어가 없습니다.");

            float savedHit = HitChance.ForcedRoll;
            var worldGo = new GameObject("selfcheck-phys-armor-world");
            var atkGo = new GameObject("selfcheck-phys-armor-atk");
            var plateGo = new GameObject("selfcheck-phys-armor-plate");
            var bareGo = new GameObject("selfcheck-phys-armor-bare");
            try
            {
                var world = worldGo.AddComponent<OfflineWorld>();
                var atk = atkGo.AddComponent<WorldBody>();
                var plated = plateGo.AddComponent<WorldBody>();
                var bare = bareGo.AddComponent<WorldBody>();
                atk.IsAvatar = true;
                plated.IsEnemy = true;
                bare.IsEnemy = true;
                plated.MaxHp = 400f;
                plated.SetHp(400f);
                bare.MaxHp = 400f;
                bare.SetHp(400f);
                atkGo.transform.position = Vector3.zero;
                plateGo.transform.position = new Vector3(1f, 0f, 0f);
                bareGo.transform.position = new Vector3(1.2f, 0f, 0f);
                var plateBag = plateGo.AddComponent<InventoryBag>();
                plateBag.Add(new ItemRecord { TemplateId = ItemCatalog.IronPlate, Amount = 1, Uses = 40 });
                if (PhysicalArmor.Of(plateBag.Items) != PhysicalArmor.FallbackPlate)
                    throw new InvalidOperationException("철갑 가방 AR이 4가 아닙니다: " + PhysicalArmor.Of(plateBag.Items));

                HitChance.ForcedRoll = 0f;
                AttackResult open = world.TryAttack(atk, bare);
                if (!open.Applied || !open.Hit || open.Damage <= 0)
                    throw new InvalidOperationException("무갑은 맞아야 합니다: hit=" + open.Hit + " dmg=" + open.Damage + " " + open.FailReason);

                var atk2Go = new GameObject("selfcheck-phys-armor-atk2");
                try
                {
                    var atk2 = atk2Go.AddComponent<WorldBody>();
                    atk2.IsAvatar = true;
                    atk2Go.transform.position = Vector3.zero;
                    AttackResult blocked = world.TryAttack(atk2, plated);
                    if (!blocked.Applied || !blocked.Hit || blocked.Damage <= 0)
                        throw new InvalidOperationException("철갑도 맞아야 합니다: hit=" + blocked.Hit + " dmg=" + blocked.Damage + " " + blocked.FailReason);
                    if (blocked.Damage != open.Damage - PhysicalArmor.FallbackPlate)
                        throw new InvalidOperationException("철갑 피해가 무갑−4가 아닙니다: 무갑 " + open.Damage + " 철갑 " + blocked.Damage);
                    if (Math.Abs(plated.Hp - (400f - blocked.Damage)) > 0.01f)
                        throw new InvalidOperationException("철갑 HP가 안 깎입니다: " + plated.Hp);
                    if (world.LastCombatMessage.IndexOf("방", StringComparison.Ordinal) < 0)
                        throw new InvalidOperationException("방 안내가 없습니다: " + world.LastCombatMessage);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(atk2Go);
                }
            }
            finally
            {
                HitChance.ForcedRoll = savedHit;
                UnityEngine.Object.DestroyImmediate(worldGo);
                UnityEngine.Object.DestroyImmediate(atkGo);
                UnityEngine.Object.DestroyImmediate(plateGo);
                UnityEngine.Object.DestroyImmediate(bareGo);
            }

            Debug.Log("[Ulon] 물리 방어 — 철갑 AR · TryAttack PhysicalArmor · HUD · 마법 경로 없음");
        }

        static void AssertPhysicalArmorNegativeControl()
        {
            bool was = PhysicalArmor.NcOff;
            bool red = false;
            try
            {
                PhysicalArmor.NcOff = true;
                try { AssertPhysicalArmor(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { PhysicalArmor.NcOff = was; }
            if (!red)
                throw new InvalidOperationException("물리 방어 네거티브 컨트롤 실패 — NcOff 인데 통과했습니다.");
            Debug.Log("[Ulon] 물리 방어 네거티브 컨트롤 통과 — NcOff 이면 FAIL");
        }
    }
}
