using System;
using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

// reload-nudge
namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 시체 루팅 우선창(§18.4) — 창 중 소유자/파티만, 창 후 근접 전원 공개.
        /// 기존 RunCharacter 파티 검사는 유지(여기 복제 호출 금지).
        /// </summary>
        static void AssertLootRight()
        {
            AssertVillageIntact();

            var worldGo = new GameObject("selfcheck-lootright-world");
            GameObject ownerGo = null;
            GameObject strangerGo = null;
            GameObject palGo = null;
            GameObject healerGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();

                // --- 솔로 소유자: 창 중 낮선 loot_right, 소유자 성공 ---
                ownerGo = new GameObject("selfcheck-lootright-owner");
                ownerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.DisplayName = "룻주인";
                owner.IsAvatar = true;
                owner.CharacterId = "loot-owner";
                owner.AccountId = "loot-acc";
                owner.MaxHp = 40f;
                owner.ResetHp();
                var bag = ownerGo.AddComponent<InventoryBag>();
                bag.Add("wood", 1);

                strangerGo = new GameObject("selfcheck-lootright-stranger");
                strangerGo.transform.position = ownerGo.transform.position;
                var stranger = strangerGo.AddComponent<WorldBody>();
                stranger.DisplayName = "낮선이";
                stranger.IsAvatar = true;
                stranger.CharacterId = "loot-stranger";
                stranger.AccountId = "loot-stranger-acc";
                stranger.ResetHp();
                strangerGo.AddComponent<InventoryBag>();

                healerGo = new GameObject("selfcheck-lootright-healer");
                healerGo.transform.position = ownerGo.transform.position;
                var healer = healerGo.AddComponent<HealerStation>();

                world.HandleDeath(owner, "loot-owner");
                var solo = OfflineWorld.FindCorpse("loot-owner");
                if (solo == null)
                    throw new InvalidOperationException("솔로 시체가 있어야 합니다.");
                if (Math.Abs(solo.ExclusiveSeconds - CorpseNode.DefaultExclusiveSeconds) > 0.0001f)
                    throw new InvalidOperationException("시체 ExclusiveSeconds 기본값은 DefaultExclusiveSeconds여야 합니다.");

                var deniedSolo = world.TryLootCorpse(stranger, solo);
                if (deniedSolo.Applied || deniedSolo.FailReason != "loot_right")
                    throw new InvalidOperationException("창 중 솔로 시체는 낮선이 loot_right로 거절돼야 합니다: " + deniedSolo.FailReason);

                // 유령은 가져가기 불가(Death.cs). 회수는 부활 뒤 — RunCharacter와 같은 약속.
                var ghostLoot = world.TryLootCorpse(owner, solo);
                if (ghostLoot.Applied || ghostLoot.FailReason != "ghost")
                    throw new InvalidOperationException("유령 소유자 룻은 ghost로 거절돼야 합니다: " + ghostLoot.FailReason);

                var rez = world.TryResurrect(owner, healer);
                if (!rez.Applied || owner.Ghost)
                    throw new InvalidOperationException("부활 실패: " + rez.FailReason);

                var ownerLoot = world.TryLootCorpse(owner, solo);
                if (!ownerLoot.Applied)
                    throw new InvalidOperationException("창 중 솔로 소유자 룻 실패: " + ownerLoot.FailReason);

                // --- 파티: 창 중 파티원 성공·낮선 거절 (기존 RunCharacter와 같은 약속, 복제 호출 아님) ---

                palGo = new GameObject("selfcheck-lootright-pal");
                palGo.transform.position = ownerGo.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.DisplayName = "동료";
                pal.IsEnemy = false;
                pal.MaxHp = 40f;
                pal.ResetHp();
                palGo.AddComponent<InventoryBag>();

                var invited = world.TryPartyInvite(owner, pal);
                if (!invited.Applied || owner.Party == null || !owner.Party.Contains(pal))
                    throw new InvalidOperationException("파티 초대 실패: " + invited.FailReason);

                bag.Add("resin", 1);
                world.HandleDeath(owner, "loot-owner");
                var partyCorpse = OfflineWorld.FindCorpse("loot-owner");
                if (partyCorpse == null)
                    throw new InvalidOperationException("파티 시체가 있어야 합니다.");

                var deniedParty = world.TryLootCorpse(stranger, partyCorpse);
                if (deniedParty.Applied || deniedParty.FailReason != "loot_right")
                    throw new InvalidOperationException("창 중 파티 시체는 낮선이 loot_right로 거절돼야 합니다: " + deniedParty.FailReason);

                var palLoot = world.TryLootCorpse(pal, partyCorpse);
                if (!palLoot.Applied)
                    throw new InvalidOperationException("창 중 파티원 룻 실패: " + palLoot.FailReason);

                // --- 창 만료 후: 낮선 성공 ---
                world.TryResurrect(owner, healer);
                bag.Add("cloth", 1);
                world.HandleDeath(owner, "loot-owner");
                var expired = OfflineWorld.FindCorpse("loot-owner");
                if (expired == null)
                    throw new InvalidOperationException("만료 시험용 시체가 있어야 합니다.");
                expired.SpawnedAt = Time.time - expired.ExclusiveSeconds - 1f;
                var openLoot = world.TryLootCorpse(stranger, expired);
                if (!openLoot.Applied)
                    throw new InvalidOperationException("창 만료 후 낮선 룻이 성공해야 합니다: " + openLoot.FailReason);

                // --- NC: ExclusiveSeconds=0 → 창 없이 낮선 성공(주인은 살아 있음 — 시체만 별도 배치) ---
                world.TryResurrect(owner, healer);
                if (owner.Ghost)
                    throw new InvalidOperationException("NC 전에 주인이 살아 있어야 합니다.");
                if (owner.Party != null)
                    world.TryPartyLeave(owner);

                var ncGo = new GameObject("Corpse");
                ncGo.transform.position = ownerGo.transform.position;
                var openNow = ncGo.AddComponent<CorpseNode>();
                openNow.CorpseId = "loot-nc";
                openNow.OwnerId = "loot-owner";
                openNow.OwnerBody = owner;
                openNow.SpawnedAt = Time.time;
                openNow.DecaySeconds = 900f;
                openNow.ExclusiveSeconds = 0f; // ExclusiveDisabled
                openNow.Items.Add(new ItemRecord { TemplateId = "iron_ore", Amount = 1 });
                var ncLoot = world.TryLootCorpse(stranger, openNow);
                if (!ncLoot.Applied)
                    throw new InvalidOperationException("ExclusiveSeconds=0이면 낮선 룻이 성공해야 합니다(NC): " + ncLoot.FailReason);
                if (!owner || owner.Ghost)
                    throw new InvalidOperationException("NC 뒤에도 주인은 살아 있어야 합니다.");
            }
            finally
            {
                var leftover = OfflineWorld.FindCorpse("loot-owner");
                if (leftover != null)
                    UnityEngine.Object.DestroyImmediate(leftover.gameObject);
                if (palGo != null) UnityEngine.Object.DestroyImmediate(palGo);
                if (strangerGo != null) UnityEngine.Object.DestroyImmediate(strangerGo);
                if (healerGo != null) UnityEngine.Object.DestroyImmediate(healerGo);
                if (ownerGo != null) UnityEngine.Object.DestroyImmediate(ownerGo);
                if (worldGo != null) UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
