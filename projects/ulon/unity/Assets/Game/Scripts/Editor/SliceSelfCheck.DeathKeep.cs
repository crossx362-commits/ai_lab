using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 기획 §18.4 — 일반 아이템은 시체, 보호 태그(keepOnDeath)는 몸에 남고, 장착은 풀린다.
        /// 유령은 공격 불가. 안내 문구는 화면에 그리는 RecoveryLine과 같은 함수.
        /// </summary>
        static void AssertDeathKeep()
        {
            string deathPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Death.cs");
            string death = File.ReadAllText(deathPath);
            if (death.IndexOf("KeepOnDeath", StringComparison.Ordinal) < 0 ||
                death.IndexOf("equipped.Remove", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HandleDeath가 보호 아이템·장착 해제를 안 합니다.");
            if (HudSourceText().IndexOf("RecoveryLine", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD가 유령 안내를 RecoveryLine으로 안 그립니다.");

            ItemData.Reload();
            if (!ItemCatalog.NcDropProtected && !ItemCatalog.KeepOnDeath(ItemCatalog.WardenCrest))
                throw new InvalidOperationException("본워든의 문장이 keepOnDeath가 아닙니다.");
            if (ItemCatalog.KeepOnDeath("wood"))
                throw new InvalidOperationException("나무는 보호 아이템이 아닙니다.");

            var worldGo = new GameObject("selfcheck-deathkeep-world");
            GameObject ownerGo = null;
            GameObject enemyGo = null;
            GameObject healerGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();

                ownerGo = new GameObject("selfcheck-deathkeep-owner");
                ownerGo.transform.position = new Vector3(1f, 0f, 1f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.DisplayName = "유령시험";
                owner.IsAvatar = true;
                owner.CharacterId = "death-keep";
                owner.AccountId = "death-keep-acc";
                owner.MaxHp = 40f;
                owner.ResetHp();
                var bag = ownerGo.AddComponent<InventoryBag>();
                bag.Add("wood", 1);
                bag.Add(ItemCatalog.IronSword, 1);
                bag.Add(ItemCatalog.WardenCrest, 1);

                enemyGo = new GameObject("selfcheck-deathkeep-enemy");
                enemyGo.transform.position = ownerGo.transform.position;
                var enemy = enemyGo.AddComponent<WorldBody>();
                enemy.IsEnemy = true;
                enemy.MaxHp = 30f;
                enemy.ResetHp();

                healerGo = new GameObject("Healer");
                healerGo.transform.position = ownerGo.transform.position;
                var healer = healerGo.AddComponent<HealerStation>();

                var eq = world.TryEquip(owner, ItemCatalog.IronSword);
                if (!eq.Applied)
                    throw new InvalidOperationException("시험용 장착 실패: " + eq.FailReason);
                if (world.EquippedOf(owner) != ItemCatalog.IronSword)
                    throw new InvalidOperationException("장착 원장이 철검이 아닙니다.");

                world.HandleDeath(owner, "death-keep-acc");
                if (!owner.Ghost)
                    throw new InvalidOperationException("사망 뒤 유령이 아닙니다.");

                var corpse = OfflineWorld.FindCorpse("death-keep-acc");
                if (corpse == null)
                    throw new InvalidOperationException("시체가 없습니다.");
                if (!ItemCatalog.Has(corpse.Items, "wood") || !ItemCatalog.Has(corpse.Items, ItemCatalog.IronSword))
                    throw new InvalidOperationException("일반 아이템이 시체에 없습니다.");
                if (ItemCatalog.Has(corpse.Items, ItemCatalog.WardenCrest))
                    throw new InvalidOperationException("보호 아이템이 시체로 갔습니다.");
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.WardenCrest))
                    throw new InvalidOperationException("보호 아이템이 몸에 안 남았습니다.");
                if (ItemCatalog.Has(bag.Items, "wood") || ItemCatalog.Has(bag.Items, ItemCatalog.IronSword))
                    throw new InvalidOperationException("일반 아이템이 가방에 남았습니다.");
                if (!string.IsNullOrEmpty(world.EquippedOf(owner)))
                    throw new InvalidOperationException("사망 뒤에도 장착이 남아 있습니다: " + world.EquippedOf(owner));

                var punch = world.TryAttack(owner, enemy);
                if (punch.Applied || punch.FailReason != "ghost")
                    throw new InvalidOperationException("유령 공격이 ghost로 거절돼야 합니다: " + punch.FailReason);

                var ghostLoot = world.TryLootCorpse(owner, corpse);
                if (ghostLoot.Applied || ghostLoot.FailReason != "ghost")
                    throw new InvalidOperationException("유령 룻은 ghost로 거절돼야 합니다: " + ghostLoot.FailReason);

                var rez = world.TryResurrect(owner, healer);
                if (!rez.Applied || owner.Ghost)
                    throw new InvalidOperationException("치유사 부활 실패: " + rez.FailReason);

                var loot = world.TryLootCorpse(owner, corpse);
                if (!loot.Applied)
                    throw new InvalidOperationException("부활 뒤 시체 회수 실패: " + loot.FailReason);
                if (!ItemCatalog.Has(bag.Items, "wood") || !ItemCatalog.Has(bag.Items, ItemCatalog.IronSword))
                    throw new InvalidOperationException("회수 뒤 일반 아이템이 가방에 없습니다.");
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.WardenCrest))
                    throw new InvalidOperationException("회수 뒤 보호 아이템이 사라졌습니다.");
            }
            finally
            {
                var leftover = OfflineWorld.FindCorpse("death-keep-acc");
                if (leftover != null)
                    UnityEngine.Object.DestroyImmediate(leftover.gameObject);
                if (enemyGo != null) UnityEngine.Object.DestroyImmediate(enemyGo);
                if (healerGo != null) UnityEngine.Object.DestroyImmediate(healerGo);
                if (ownerGo != null) UnityEngine.Object.DestroyImmediate(ownerGo);
                if (worldGo != null) UnityEngine.Object.DestroyImmediate(worldGo);
            }

            Debug.Log("[Ulon] 죽음 보호 — 일반은 시체 · 문장은 몸 · 장착 해제 · 유령 공격 거절 · 부활 회수");
        }

        static void AssertDeathKeepNegativeControl()
        {
            bool was = ItemCatalog.NcDropProtected;
            bool red = false;
            try
            {
                ItemCatalog.NcDropProtected = true;
                try { AssertDeathKeep(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { ItemCatalog.NcDropProtected = was; }
            if (!red)
                throw new InvalidOperationException("죽음 보호 네거티브 컨트롤 실패 — NcDropProtected 인데 통과했습니다.");
            Debug.Log("[Ulon] 죽음 보호 네거티브 컨트롤 통과 — NcDropProtected 이면 FAIL");
        }
    }
}
