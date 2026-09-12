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
        /// 기획 §18.11 — 보스 시체는 기여자/파티 우선권 후 공개. 가방에 바로 넣지 않는다.
        /// </summary>
        static void AssertBossLoot()
        {
            string combatPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Combat.cs");
            string deathPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Death.cs");
            string combat = File.ReadAllText(combatPath);
            string death = File.ReadAllText(deathPath);
            if (combat.IndexOf("Bag(attacker).Add(drop", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("보스 드랍이 처치자 가방으로 바로 갑니다 — 시체 우선권이어야 합니다.");
            if (death.IndexOf("TrySpawnBossCorpse", StringComparison.Ordinal) < 0 ||
                death.IndexOf("ContributorIds", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("보스 시체 스폰·기여자 복사가 없습니다.");

            AssertVillageIntact();

            var worldGo = new GameObject("selfcheck-bossloot-world");
            GameObject eliteGo = null;
            GameObject killerGo = null;
            GameObject palGo = null;
            GameObject strangerGo = null;
            CorpseNode leftover = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();

                eliteGo = new GameObject("selfcheck-bossloot-elite");
                eliteGo.transform.position = new Vector3(2f, 0f, 2f);
                var elite = eliteGo.AddComponent<WorldBody>();
                elite.IsEnemy = true;
                elite.MobId = MobCatalog.BoneWarden;
                elite.ApplyMobCatalog();
                elite.ResetHp();

                killerGo = new GameObject("selfcheck-bossloot-killer");
                killerGo.transform.position = eliteGo.transform.position;
                var killer = killerGo.AddComponent<WorldBody>();
                killer.IsAvatar = true;
                killer.AccountId = "boss-killer";
                killer.CharacterId = "boss-killer";
                killer.MaxHp = 50f;
                killer.ResetHp();
                var killerBag = killerGo.AddComponent<InventoryBag>();

                palGo = new GameObject("selfcheck-bossloot-pal");
                palGo.transform.position = eliteGo.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.DisplayName = "동료";
                pal.IsEnemy = false;
                pal.AccountId = "boss-pal";
                pal.CharacterId = "boss-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                palGo.AddComponent<InventoryBag>();

                strangerGo = new GameObject("selfcheck-bossloot-stranger");
                strangerGo.transform.position = eliteGo.transform.position;
                var stranger = strangerGo.AddComponent<WorldBody>();
                stranger.IsAvatar = true;
                stranger.AccountId = "boss-stranger";
                stranger.CharacterId = "boss-stranger";
                stranger.ResetHp();
                strangerGo.AddComponent<InventoryBag>();

                var invited = world.TryPartyInvite(killer, pal);
                if (!invited.Applied || killer.Party == null || !killer.Party.Contains(pal))
                    throw new InvalidOperationException("파티 초대 실패: " + invited.FailReason);

                elite.ApplyDamage((int)elite.MaxHp - 1);
                var slay = world.TryAttack(killer, elite);
                if (!slay.Applied)
                    throw new InvalidOperationException("본워든 처치 실패: " + slay.FailReason);
                if (elite.Alive)
                    throw new InvalidOperationException("본워든이 죽어야 합니다.");
                if (ItemCatalog.Has(killerBag.Items, ItemCatalog.WardenCrest))
                    throw new InvalidOperationException("문장이 가방에 바로 들어갔습니다 — 시체여야 합니다.");

                leftover = RequireBossCorpse(elite, ItemCatalog.WardenCrest);
                if (leftover.ContributorIds.Count < 1)
                    throw new InvalidOperationException("보스 시체에 기여자가 없습니다.");

                var denied = world.TryLootCorpse(stranger, leftover);
                if (denied.Applied || denied.FailReason != "loot_right")
                    throw new InvalidOperationException("창 중 낮선이는 loot_right여야 합니다: " + denied.FailReason);

                var palLoot = world.TryLootCorpse(pal, leftover);
                if (!palLoot.Applied)
                    throw new InvalidOperationException("창 중 기여자 파티원 룻 실패: " + palLoot.FailReason);
                leftover = null;

                var elite2Go = new GameObject("selfcheck-bossloot-elite2");
                elite2Go.transform.position = eliteGo.transform.position;
                var elite2 = elite2Go.AddComponent<WorldBody>();
                elite2.IsEnemy = true;
                elite2.MobId = MobCatalog.BoneWarden;
                elite2.ApplyMobCatalog();
                elite2.ResetHp();
                var killer2Go = new GameObject("selfcheck-bossloot-killer2");
                killer2Go.transform.position = elite2Go.transform.position;
                var killer2 = killer2Go.AddComponent<WorldBody>();
                killer2.IsAvatar = true;
                killer2.AccountId = "boss-killer2";
                killer2.MaxHp = 50f;
                killer2.ResetHp();
                killer2Go.AddComponent<InventoryBag>();
                elite2.ApplyDamage((int)elite2.MaxHp - 1);
                var slay2 = world.TryAttack(killer2, elite2);
                if (!slay2.Applied || elite2.Alive)
                    throw new InvalidOperationException("두 번째 본워든 처치 실패: " + slay2.FailReason);
                leftover = RequireBossCorpse(elite2, ItemCatalog.WardenCrest);
                leftover.SpawnedAt = Time.time - leftover.ExclusiveSeconds - 1f;
                var openLoot = world.TryLootCorpse(stranger, leftover);
                if (!openLoot.Applied)
                    throw new InvalidOperationException("창 만료 후 낮선 룻이 성공해야 합니다: " + openLoot.FailReason);
                leftover = null;
                UnityEngine.Object.DestroyImmediate(killer2Go);
                UnityEngine.Object.DestroyImmediate(elite2Go);

                Debug.Log("[Ulon] 보스 룻 — 시체 드랍 · 창 중 기여자/파티 · 낮선 거절 · 창 후 공개");
            }
            finally
            {
                if (leftover != null)
                    UnityEngine.Object.DestroyImmediate(leftover.gameObject);
                if (eliteGo != null) UnityEngine.Object.DestroyImmediate(eliteGo);
                if (killerGo != null) UnityEngine.Object.DestroyImmediate(killerGo);
                if (palGo != null) UnityEngine.Object.DestroyImmediate(palGo);
                if (strangerGo != null) UnityEngine.Object.DestroyImmediate(strangerGo);
                if (worldGo != null) UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertBossLootNegativeControl()
        {
            bool was = BossLootRights.NcOpen;
            bool red = false;
            try
            {
                BossLootRights.NcOpen = true;
                try { AssertBossLoot(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { BossLootRights.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("보스 룻 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 보스 룻 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }

        static CorpseNode RequireBossCorpse(WorldBody elite, string drop)
        {
            if (elite == null)
                throw new InvalidOperationException("보스 몸이 없습니다.");
            var list = UnityEngine.Object.FindObjectsByType<CorpseNode>(FindObjectsSortMode.None);
            CorpseNode found = null;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null && list[i].MobId == elite.MobId)
                    found = list[i];
            }
            if (found == null)
                throw new InvalidOperationException("보스 시체가 없습니다: " + elite.MobId);
            if (!ItemCatalog.Has(found.Items, drop))
                throw new InvalidOperationException("보스 시체에 드랍이 없습니다: " + drop);
            return found;
        }

        static void TakeBossDropFromCorpse(WorldBody slayer, WorldBody elite, string drop)
        {
            var bag = slayer != null ? slayer.GetComponent<InventoryBag>() : null;
            if (bag != null && ItemCatalog.Has(bag.Items, drop))
                throw new InvalidOperationException("보스 드랍이 가방에 바로 들어갔습니다 — 시체 우선권(§18.11): " + drop);
            var corpse = RequireBossCorpse(elite, drop);
            var world = OfflineWorld.Instance;
            if (world == null)
                throw new InvalidOperationException("OfflineWorld 없음");
            var loot = world.TryLootCorpse(slayer, corpse);
            if (!loot.Applied)
                throw new InvalidOperationException("기여자 보스 룻 실패: " + loot.FailReason);
            if (bag == null || !ItemCatalog.Has(bag.Items, drop))
                throw new InvalidOperationException("룻 뒤 가방에 드랍이 없습니다: " + drop);
        }
    }
}
