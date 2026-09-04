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
        static void AssertTamingSlice()
        {
            if (SkillId.AnimalTaming == SkillId.Veterinary || SkillId.AnimalTaming == SkillId.AnimalLore)
                throw new InvalidOperationException("조련 SkillId는 수의학/동물지식과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.AnimalTaming) != StatId.Dex)
                throw new InvalidOperationException("조련 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.AnimalTaming) != "조련" || SkillTitles.JobOf(SkillId.AnimalTaming) != "조련사")
                throw new InvalidOperationException("조련 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학" || SkillNames.KoreanOf(SkillId.AnimalLore) != "동물지식")
                throw new InvalidOperationException("수의학/동물지식 스킬명을 바꾸면 안 됩니다.");
            if (MobCatalog.KindCount != 8)
                throw new InvalidOperationException("사냥 몹 종류 수는 그대로 8이어야 합니다.");
            if (MobCatalog.TamableOf(MobCatalog.Bandit) || MobCatalog.TamableOf("wolf") || MobCatalog.TamableOf(MobCatalog.Skeleton))
                throw new InvalidOperationException("사냥 몹은 조련불가여야 합니다.");
            if (!MobCatalog.TamableOf(TameCritter.Id) || TameCritter.ControlSlots != 1)
                throw new InvalidOperationException("야생하트는 Tameable=true ControlSlots=1이어야 합니다.");
            if (!MobCatalog.TamableOf(TameBoar.Id) || TameBoar.ControlSlots != 1)
                throw new InvalidOperationException("야생멧돼지는 Tameable=true ControlSlots=1이어야 합니다.");
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");

            var go = GameObject.Find(TameCritter.Object);
            var body = go != null ? go.GetComponent<WorldBody>() : null;
            if (body == null || !body.Tameable || body.IsEnemy || body.ControlSlots != 1)
                throw new InvalidOperationException("가드존 밖 비적대 조련 대상이 있어야 합니다.");
            if (body.MobId != TameCritter.Id || body.DisplayName != TameCritter.DisplayName)
                throw new InvalidOperationException("조련 대상 카탈로그가 야생하트여야 합니다.");
            Vector3 pos = go.transform.position;
            if (Math.Abs(pos.x - TameCritter.X) > 0.4f || Math.Abs(pos.z - TameCritter.Z) > 0.4f)
                throw new InvalidOperationException("조련 대상 좌표가 지정 위치와 같아야 합니다.");
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("조련 대상은 GuardZone 밖이어야 합니다.");
            float[] lxs = { HousingPlot.X, 18.2f, 3.4f, -12.2f, Dungeon1.EntranceX, Dungeon2.EntranceX, 0f };
            float[] lzs = { HousingPlot.Z, 2.4f, -19.6f, 20.4f, Dungeon1.EntranceZ, Dungeon2.EntranceZ, 13.2f };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = pos.x - lxs[i];
                float dz = pos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 36f)
                    throw new InvalidOperationException("조련 대상이 기존 랜드마크와 겹치면 안 됩니다.");
            }

            var none = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = false, Distance = 1f });
            if (none.Applied || none.FailReason != "not_tameable")
                throw new InvalidOperationException("조련불가 대상은 실패해야 합니다.");
            var owned = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, AlreadyPet = true, Distance = 1f });
            if (owned.Applied || owned.FailReason != "already_pet")
                throw new InvalidOperationException("이미 펫이면 실패해야 합니다.");
            var slot = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, UsedSlots = 1, ControlSlots = 1, FollowerCap = 1, Distance = 1f });
            if (slot.Applied || slot.FailReason != "no_slot")
                throw new InvalidOperationException("슬롯 없으면 실패해야 합니다.");
            var far = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, Distance = 20f });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("너무 멀면 조련 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            var ok = TameResolve.Tame(new TameRequest
            {
                Distance = 1f,
                Tameable = true,
                Skills = skills,
                Stats = stats,
                ControlSlots = 1,
                FollowerCap = 1
            });
            if (!ok.Applied)
                throw new InvalidOperationException("조련 성공해야 합니다: " + ok.FailReason);
            if (Math.Abs(skills.Get(SkillId.AnimalTaming) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 조련 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f || Math.Abs(skills.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("조련은 수의학/동물지식을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1 || stats.Int != intWas)
                throw new InvalidOperationException("조련 상승 시 DEX만 올라야 합니다.");

            var stranger = TameResolve.Follow(new PetCommandRequest { HasPet = true, IsOwner = false });
            if (stranger.Applied || stranger.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 펫을 명령하면 안 됩니다.");

            var worldGo = new GameObject("selfcheck-tame-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            GameObject huntGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ownerGo = new GameObject("selfcheck-tame-owner");
                ownerGo.transform.position = go.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "tame-owner";
                owner.ResetHp();

                huntGo = new GameObject("selfcheck-tame-hunt");
                huntGo.transform.position = go.transform.position;
                var hunt = huntGo.AddComponent<WorldBody>();
                hunt.IsEnemy = true;
                hunt.MobId = MobCatalog.Bandit;
                hunt.Tameable = false;
                hunt.ResetHp();
                var huntTame = world.TryTame(owner, hunt);
                if (huntTame.Applied)
                    throw new InvalidOperationException("사냥 몹은 조련되면 안 됩니다.");

                var hit = world.TryTame(owner, body);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 조련 실패: " + hit.FailReason);
                if (body.OwnerCharacterId != owner.CharacterId || !body.PetFollow)
                    throw new InvalidOperationException("성공 조련 후 펫이 주인을 따라야 합니다.");
                if (Math.Abs(world.SkillsOf(owner).Get(SkillId.AnimalTaming) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 조련 후 서버 스킬 0.1이어야 합니다.");

                otherGo = new GameObject("selfcheck-tame-other");
                otherGo.transform.position = go.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "tame-other";
                other.ResetHp();
                var otherCmd = world.TryPetFollow(other, body);
                if (otherCmd.Applied)
                    throw new InvalidOperationException("타인은 펫 follow를 하면 안 됩니다.");
                var otherRel = world.TryPetRelease(other, body);
                if (otherRel.Applied)
                    throw new InvalidOperationException("타인은 펫 release를 하면 안 됩니다.");

                ownerGo.transform.position = go.transform.position + new Vector3(4f, 0f, 2f);
                world.TickPets();
                float fx = ownerGo.transform.position.x + TameCritter.FollowOffsetX;
                float fz = ownerGo.transform.position.z + TameCritter.FollowOffsetZ;
                if (Math.Abs(body.transform.position.x - fx) > 0.05f || Math.Abs(body.transform.position.z - fz) > 0.05f)
                    throw new InvalidOperationException("펫은 서버 틱에서 주인 오프셋을 따라야 합니다.");

                var rel = world.TryPetRelease(owner, body);
                if (!rel.Applied)
                    throw new InvalidOperationException("주인은 펫을 놓아줘야 합니다: " + rel.FailReason);
                if (!string.IsNullOrEmpty(body.OwnerCharacterId) || body.PetFollow)
                    throw new InvalidOperationException("release 후 소유가 없어야 합니다.");

                var again = world.TryTame(owner, body);
                if (!again.Applied)
                    throw new InvalidOperationException("놓아준 대상은 다시 조련할 수 있어야 합니다: " + again.FailReason);

                ownerGo.transform.position = Vector3.zero;
                otherGo.transform.position = Vector3.zero;
                var assault = world.TryAttack(owner, other);
                if (assault.Applied || assault.FailReason != "innocent")
                    throw new InvalidOperationException("마을 가드존 무고 공격은 막혀야 합니다.");
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (huntGo != null)
                    UnityEngine.Object.DestroyImmediate(huntGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                body.OwnerCharacterId = "";
                body.PetFollow = false;
                body.PetGuard = false;
                go.transform.SetPositionAndRotation(new Vector3(TameCritter.X, go.transform.position.y, TameCritter.Z), go.transform.rotation);
            }
        }




        static void AssertPetCommands()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            string[] prefabs = { StableYard.Object, HousingPlot.VendorObject };
            for (int i = 0; i < prefabs.Length; i++)
            {
                var landmark = GameObject.Find(prefabs[i]);
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(landmark);
                if (string.IsNullOrEmpty(path) || path.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidOperationException("마을 랜드마크는 Prefab이어야 합니다(RAW fbx 아님): " + prefabs[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            var stranger = TameResolve.Stay(new PetCommandRequest { HasPet = true, IsOwner = false });
            if (stranger.Applied || stranger.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 Stay를 하면 안 됩니다.");
            var strangerG = TameResolve.Guard(new PetCommandRequest { HasPet = true, IsOwner = false });
            if (strangerG.Applied || strangerG.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 Guard를 하면 안 됩니다.");
            var ghost = TameResolve.Stay(new PetCommandRequest { Ghost = true, HasPet = true, IsOwner = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 Stay를 하면 안 됩니다.");
            var none = TameResolve.Guard(new PetCommandRequest { HasPet = false, IsOwner = true });
            if (none.Applied || none.FailReason != "not_pet")
                throw new InvalidOperationException("펫 없이 Guard하면 안 됩니다.");
            var okStay = TameResolve.Stay(new PetCommandRequest { HasPet = true, IsOwner = true });
            if (!okStay.Applied)
                throw new InvalidOperationException("주인은 Stay를 해야 합니다.");
            var okGuard = TameResolve.Guard(new PetCommandRequest { HasPet = true, IsOwner = true });
            if (!okGuard.Applied)
                throw new InvalidOperationException("주인은 Guard를 해야 합니다.");

            var worldGo = new GameObject("selfcheck-petcmd-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            GameObject petGo = null;
            GameObject mobGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ownerGo = new GameObject("selfcheck-petcmd-owner");
                ownerGo.transform.position = new Vector3(40f, 0f, 40f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "petcmd-owner";
                owner.ResetHp();

                petGo = new GameObject("selfcheck-petcmd-pet");
                petGo.transform.position = ownerGo.transform.position;
                var pet = petGo.AddComponent<WorldBody>();
                pet.Tameable = true;
                pet.IsEnemy = false;
                pet.MobId = TameCritter.Id;
                pet.DisplayName = TameCritter.DisplayName;
                pet.ControlSlots = 1;
                pet.ResetHp();

                var tamed = world.TryTame(owner, pet);
                if (!tamed.Applied)
                    throw new InvalidOperationException("펫명령 조련 실패: " + tamed.FailReason);
                if (!pet.PetFollow || pet.PetGuard)
                    throw new InvalidOperationException("조련 직후 Follow이고 Guard가 아니어야 합니다.");

                otherGo = new GameObject("selfcheck-petcmd-other");
                otherGo.transform.position = ownerGo.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "petcmd-other";
                other.ResetHp();
                var otherStay = world.TryPetStay(other, pet);
                if (otherStay.Applied)
                    throw new InvalidOperationException("타인은 TryPetStay를 하면 안 됩니다.");
                var otherGuard = world.TryPetGuard(other, pet);
                if (otherGuard.Applied)
                    throw new InvalidOperationException("타인은 TryPetGuard를 하면 안 됩니다.");
                if (!pet.PetFollow || pet.PetGuard)
                    throw new InvalidOperationException("타인 실패 후 Follow 상태가 유지되어야 합니다.");

                ownerGo.transform.position = new Vector3(44f, 0f, 42f);
                world.TickPets();
                float fx = ownerGo.transform.position.x + TameCritter.FollowOffsetX;
                float fz = ownerGo.transform.position.z + TameCritter.FollowOffsetZ;
                if (Math.Abs(pet.transform.position.x - fx) > 0.05f || Math.Abs(pet.transform.position.z - fz) > 0.05f)
                    throw new InvalidOperationException("Follow 중 펫은 오프셋을 따라야 합니다.");

                var stay = world.TryPetStay(owner, pet);
                if (!stay.Applied)
                    throw new InvalidOperationException("주인은 Stay를 해야 합니다: " + stay.FailReason);
                if (pet.PetFollow || pet.PetGuard)
                    throw new InvalidOperationException("Stay 후 PetFollow=false, PetGuard=false여야 합니다.");
                Vector3 held = pet.transform.position;
                ownerGo.transform.position = new Vector3(50f, 0f, 50f);
                world.TickPets();
                if (Math.Abs(pet.transform.position.x - held.x) > 0.01f || Math.Abs(pet.transform.position.z - held.z) > 0.01f)
                    throw new InvalidOperationException("Stay 후 펫은 자리를 지켜야 합니다.");

                mobGo = new GameObject("selfcheck-petcmd-mob");
                mobGo.transform.position = pet.transform.position;
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.MobId = MobCatalog.Bandit;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();
                ownerGo.transform.position = pet.transform.position;
                float hpStay = mob.Hp;
                world.TryEnemyStrike(mob, owner);
                if (mob.Hp < hpStay)
                    throw new InvalidOperationException("Stay 중 펫은 주인을 지키며 공격하면 안 됩니다.");

                var guard = world.TryPetGuard(owner, pet);
                if (!guard.Applied)
                    throw new InvalidOperationException("주인은 Guard를 해야 합니다: " + guard.FailReason);
                if (!pet.PetFollow || !pet.PetGuard)
                    throw new InvalidOperationException("Guard 후 PetFollow=true, PetGuard=true여야 합니다.");
                world.TickPets();
                fx = ownerGo.transform.position.x + TameCritter.FollowOffsetX;
                fz = ownerGo.transform.position.z + TameCritter.FollowOffsetZ;
                if (Math.Abs(pet.transform.position.x - fx) > 0.05f || Math.Abs(pet.transform.position.z - fz) > 0.05f)
                    throw new InvalidOperationException("Guard 중 펫은 주인 오프셋에 있어야 합니다.");

                mobGo.transform.position = ownerGo.transform.position;
                pet.transform.position = ownerGo.transform.position;
                mob.ResetHp();
                float hpBefore = mob.Hp;
                bool struck = world.TryEnemyStrike(mob, owner);
                if (!struck)
                    throw new InvalidOperationException("가드 검증용 몹 타격이 들어가야 합니다.");
                if (mob.Hp >= hpBefore)
                    throw new InvalidOperationException("Guard 펫은 주인을 친 몹을 공격해야 합니다.");

                var follow = world.TryPetFollow(owner, pet);
                if (!follow.Applied)
                    throw new InvalidOperationException("주인은 다시 Follow를 해야 합니다: " + follow.FailReason);
                if (!pet.PetFollow || pet.PetGuard)
                    throw new InvalidOperationException("Follow 후 PetGuard가 꺼져야 합니다.");
                mob.ResetHp();
                hpBefore = mob.Hp;
                world.TryEnemyStrike(mob, owner);
                if (mob.Hp < hpBefore)
                    throw new InvalidOperationException("Follow 중 펫은 자동 반격하면 안 됩니다.");
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (petGo != null)
                    UnityEngine.Object.DestroyImmediate(petGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertPetAttack()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var stranger = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = false, HasEnemy = true });
            if (stranger.Applied || stranger.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 Attack을 하면 안 됩니다.");
            var ghost = TameResolve.Attack(new PetCommandRequest { Ghost = true, HasPet = true, IsOwner = true, HasEnemy = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 Attack을 하면 안 됩니다.");
            var none = TameResolve.Attack(new PetCommandRequest { HasPet = false, IsOwner = true, HasEnemy = true });
            if (none.Applied || none.FailReason != "not_pet")
                throw new InvalidOperationException("펫 없이 Attack하면 안 됩니다.");
            var noEnemy = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = true, HasEnemy = false });
            if (noEnemy.Applied || noEnemy.FailReason != "no_enemy")
                throw new InvalidOperationException("적 없이 Attack하면 안 됩니다.");
            var dead = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = true, HasEnemy = true, PetAlive = false });
            if (dead.Applied || dead.FailReason != "dead")
                throw new InvalidOperationException("죽은 펫 Attack은 실패해야 합니다.");
            var stabled = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = true, HasEnemy = true, PetStabled = true });
            if (stabled.Applied || stabled.FailReason != "stabled")
                throw new InvalidOperationException("마구간 펫 Attack은 실패해야 합니다.");
            var ok = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = true, HasEnemy = true });
            if (!ok.Applied)
                throw new InvalidOperationException("주인은 Attack을 해야 합니다.");

            var worldGo = new GameObject("selfcheck-petatk-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            GameObject petGo = null;
            GameObject mobGo = null;
            GameObject avatarGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                ownerGo = new GameObject("selfcheck-petatk-owner");
                ownerGo.transform.position = new Vector3(40f, 0f, 40f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "petatk-owner";
                owner.ResetHp();

                petGo = new GameObject("selfcheck-petatk-pet");
                petGo.transform.position = ownerGo.transform.position;
                var pet = petGo.AddComponent<WorldBody>();
                pet.Tameable = true;
                pet.IsEnemy = false;
                pet.MobId = TameCritter.Id;
                pet.DisplayName = TameCritter.DisplayName;
                pet.ControlSlots = 1;
                pet.ResetHp();

                var tamed = world.TryTame(owner, pet);
                if (!tamed.Applied)
                    throw new InvalidOperationException("펫공격 조련 실패: " + tamed.FailReason);

                otherGo = new GameObject("selfcheck-petatk-other");
                otherGo.transform.position = ownerGo.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "petatk-other";
                other.ResetHp();

                mobGo = new GameObject("selfcheck-petatk-mob");
                mobGo.transform.position = ownerGo.transform.position + new Vector3(1.5f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.MobId = MobCatalog.Bandit;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                var otherAtk = world.TryPetAttack(other, pet, mob);
                if (otherAtk.Applied)
                    throw new InvalidOperationException("타인은 TryPetAttack을 하면 안 됩니다.");

                owner.Ghost = true;
                var ghostAtk = world.TryPetAttack(owner, pet, mob);
                if (ghostAtk.Applied || ghostAtk.FailReason != "ghost")
                    throw new InvalidOperationException("유령 주인은 TryPetAttack 실패해야 합니다.");
                owner.Ghost = false;

                avatarGo = new GameObject("selfcheck-petatk-avatar");
                avatarGo.transform.position = ownerGo.transform.position + new Vector3(2f, 0f, 0f);
                var victim = avatarGo.AddComponent<WorldBody>();
                victim.IsAvatar = true;
                victim.IsEnemy = false;
                victim.CharacterId = "petatk-victim";
                victim.ResetHp();
                var pvp = world.TryPetAttack(owner, pet, victim);
                if (pvp.Applied || pvp.FailReason != "no_enemy")
                    throw new InvalidOperationException("펫은 아바타 Open PvP Attack을 하면 안 됩니다.");

                float hpBefore = mob.Hp;
                var atk = world.TryPetAttack(owner, pet, mob);
                if (!atk.Applied)
                    throw new InvalidOperationException("주인은 TryPetAttack을 해야 합니다: " + atk.FailReason);
                if (pet.PetFollow || pet.PetGuard || pet.PetAttackTarget != mob)
                    throw new InvalidOperationException("Attack 후 PetFollow/PetGuard 꺼지고 PetAttackTarget이 몹이어야 합니다.");

                world.TickPets();
                if (mob.Hp >= hpBefore)
                    throw new InvalidOperationException("Attack 펫은 TickPets에서 몹을 공격해야 합니다.");
                float dist = Vector3.Distance(pet.transform.position, mob.transform.position);
                if (dist > ItemCatalog.MeleeRange + 1.2f)
                    throw new InvalidOperationException("Attack 펫은 몹 근처로 추격해야 합니다.");

                var stay = world.TryPetStay(owner, pet);
                if (!stay.Applied)
                    throw new InvalidOperationException("Attack 후 Stay가 되어야 합니다.");
                if (pet.PetAttackTarget != null)
                    throw new InvalidOperationException("Stay 후 PetAttackTarget이 비어야 합니다.");

                pet.PetStabled = true;
                var stabAtk = world.TryPetAttack(owner, pet, mob);
                if (stabAtk.Applied || stabAtk.FailReason != "stabled")
                    throw new InvalidOperationException("마구간 펫 TryPetAttack은 실패해야 합니다.");
                pet.PetStabled = false;

                pet.ApplyDamage((int)pet.MaxHp + 10);
                if (pet.Alive)
                    throw new InvalidOperationException("펫이 죽어야 합니다.");
                var deadAtk = world.TryPetAttack(owner, pet, mob);
                if (deadAtk.Applied || deadAtk.FailReason != "dead")
                    throw new InvalidOperationException("죽은 펫 TryPetAttack은 실패해야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (petGo != null)
                    UnityEngine.Object.DestroyImmediate(petGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (avatarGo != null)
                    UnityEngine.Object.DestroyImmediate(avatarGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertPetCome()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var stranger = TameResolve.Come(new PetCommandRequest { HasPet = true, IsOwner = false });
            if (stranger.Applied || stranger.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 Come을 하면 안 됩니다.");
            var ghost = TameResolve.Come(new PetCommandRequest { Ghost = true, HasPet = true, IsOwner = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 Come을 하면 안 됩니다.");
            var none = TameResolve.Come(new PetCommandRequest { HasPet = false, IsOwner = true });
            if (none.Applied || none.FailReason != "not_pet")
                throw new InvalidOperationException("펫 없이 Come하면 안 됩니다.");
            var dead = TameResolve.Come(new PetCommandRequest { HasPet = true, IsOwner = true, PetAlive = false });
            if (dead.Applied || dead.FailReason != "dead")
                throw new InvalidOperationException("죽은 펫 Come은 실패해야 합니다.");
            var stabled = TameResolve.Come(new PetCommandRequest { HasPet = true, IsOwner = true, PetStabled = true });
            if (stabled.Applied || stabled.FailReason != "stabled")
                throw new InvalidOperationException("마구간 펫 Come은 실패해야 합니다.");
            var ok = TameResolve.Come(new PetCommandRequest { HasPet = true, IsOwner = true });
            if (!ok.Applied)
                throw new InvalidOperationException("주인은 Come을 해야 합니다.");

            var worldGo = new GameObject("selfcheck-petcome-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            GameObject petGo = null;
            GameObject mobGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                ownerGo = new GameObject("selfcheck-petcome-owner");
                ownerGo.transform.position = new Vector3(42f, 0f, 42f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "petcome-owner";
                owner.ResetHp();

                petGo = new GameObject("selfcheck-petcome-pet");
                petGo.transform.position = ownerGo.transform.position;
                var pet = petGo.AddComponent<WorldBody>();
                pet.Tameable = true;
                pet.IsEnemy = false;
                pet.MobId = TameCritter.Id;
                pet.DisplayName = TameCritter.DisplayName;
                pet.ControlSlots = 1;
                pet.ResetHp();

                var tamed = world.TryTame(owner, pet);
                if (!tamed.Applied)
                    throw new InvalidOperationException("펫호출 조련 실패: " + tamed.FailReason);

                otherGo = new GameObject("selfcheck-petcome-other");
                otherGo.transform.position = ownerGo.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "petcome-other";
                other.ResetHp();

                mobGo = new GameObject("selfcheck-petcome-mob");
                mobGo.transform.position = ownerGo.transform.position + new Vector3(6f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.MobId = MobCatalog.Bandit;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                var otherCome = world.TryPetCome(other, pet);
                if (otherCome.Applied)
                    throw new InvalidOperationException("타인은 TryPetCome을 하면 안 됩니다.");

                owner.Ghost = true;
                var ghostCome = world.TryPetCome(owner, pet);
                if (ghostCome.Applied || ghostCome.FailReason != "ghost")
                    throw new InvalidOperationException("유령 주인은 TryPetCome 실패해야 합니다.");
                owner.Ghost = false;

                var atk = world.TryPetAttack(owner, pet, mob);
                if (!atk.Applied)
                    throw new InvalidOperationException("Come 전 TryPetAttack이 되어야 합니다: " + atk.FailReason);
                if (pet.PetAttackTarget != mob)
                    throw new InvalidOperationException("Attack 후 PetAttackTarget이 몹이어야 합니다.");

                pet.transform.position = mob.transform.position + new Vector3(0.5f, 0f, 0.5f);
                var come = world.TryPetCome(owner, pet);
                if (!come.Applied)
                    throw new InvalidOperationException("주인은 TryPetCome을 해야 합니다: " + come.FailReason);
                if (!pet.PetFollow || pet.PetGuard || pet.PetAttackTarget != null)
                    throw new InvalidOperationException("Come 후 PetFollow 켜지고 PetGuard/PetAttackTarget이 비어야 합니다.");

                world.TickPets();
                Vector3 want = owner.transform.position + new Vector3(TameCritter.FollowOffsetX, 0f, TameCritter.FollowOffsetZ);
                float dist = Vector3.Distance(new Vector3(pet.transform.position.x, 0f, pet.transform.position.z), new Vector3(want.x, 0f, want.z));
                if (dist > 0.05f)
                    throw new InvalidOperationException("Come 후 TickPets에서 펫이 주인 오프셋으로 와야 합니다.");

                var stay = world.TryPetStay(owner, pet);
                if (!stay.Applied)
                    throw new InvalidOperationException("Come 후 Stay가 되어야 합니다.");
                pet.transform.position = owner.transform.position + new Vector3(8f, 0f, 0f);
                var come2 = world.TryPetCome(owner, pet);
                if (!come2.Applied)
                    throw new InvalidOperationException("Stay 중 Come이 되어야 합니다.");
                if (!pet.PetFollow)
                    throw new InvalidOperationException("Stay 후 Come은 Follow로 바뀌어야 합니다.");
                world.TickPets();
                dist = Vector3.Distance(new Vector3(pet.transform.position.x, 0f, pet.transform.position.z), new Vector3(want.x, 0f, want.z));
                if (dist > 0.05f)
                    throw new InvalidOperationException("Stay→Come 후 펫이 주인에게 와야 합니다.");

                pet.PetStabled = true;
                var stabCome = world.TryPetCome(owner, pet);
                if (stabCome.Applied || stabCome.FailReason != "stabled")
                    throw new InvalidOperationException("마구간 펫 TryPetCome은 실패해야 합니다.");
                pet.PetStabled = false;

                pet.ApplyDamage((int)pet.MaxHp + 10);
                if (pet.Alive)
                    throw new InvalidOperationException("펫이 죽어야 합니다.");
                var deadCome = world.TryPetCome(owner, pet);
                if (deadCome.Applied || deadCome.FailReason != "dead")
                    throw new InvalidOperationException("죽은 펫 TryPetCome은 실패해야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (petGo != null)
                    UnityEngine.Object.DestroyImmediate(petGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertPetBondVetRez()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");
            if (SkillId.Veterinary == SkillId.Healing)
                throw new InvalidOperationException("수의학 SkillId는 치유와 달라야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학")
                throw new InvalidOperationException("수의학 스킬명을 바꾸면 안 됩니다.");

            var ghostHealer = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                HealerGhost = true,
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (ghostHealer.Applied || ghostHealer.FailReason != "ghost")
                throw new InvalidOperationException("유령 시술자 펫 부활은 실패해야 합니다.");

            var noGhost = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = false,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (noGhost.Applied || noGhost.FailReason != "not_pet_ghost")
                throw new InvalidOperationException("유령 아닌 펫 수의학 부활은 실패해야 합니다.");

            var notBond = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = false,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (notBond.Applied || notBond.FailReason != "not_pet_ghost")
                throw new InvalidOperationException("비 Bonded Ghost 수의학 부활은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = false,
                Distance = 1f,
                Skills = noBnSkills
            });
            if (noBn.Applied || noBn.FailReason != "no_bandage")
                throw new InvalidOperationException("붕대 없는 펫 부활은 실패해야 합니다.");
            if (Math.Abs(noBnSkills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("실패한 펫 부활은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = ItemCatalog.MeleeRange + 1f,
                Range = ItemCatalog.MeleeRange,
                Skills = farSkills
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 펫 부활은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = VeterinaryResurrectResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("펫 부활 Resolve는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 펫 부활 후 Veterinary 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("펫 부활은 치유를 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("펫 부활은 마법을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("펫 부활 상승 시 DEX가 올라야 합니다.");

            var forceSkills = new SkillSet();
            var forced = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = forceSkills,
                Stats = new StatSet(),
                Force = true
            });
            if (!forced.Applied || Math.Abs(forceSkills.Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("Force 경로도 Veterinary 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Veterinary, SkillLock.Locked);
            var lockedOk = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = locked,
                Force = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 Veterinary도 펫 부활 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("잠긴 Veterinary는 오르면 안 됩니다.");

            // Healing bandage rez must still reject pet ghost (avatar-only)
            var healOnPet = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = false,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (healOnPet.Applied)
                throw new InvalidOperationException("플레이어 붕대 부활은 펫 Ghost에 들어가면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var worldGo = new GameObject("selfcheck-petbond-world");
            GameObject ownerGo = null;
            GameObject healerGo = null;
            GameObject petGo = null;
            GameObject wildGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                ownerGo = new GameObject("selfcheck-petbond-owner");
                ownerGo.transform.position = new Vector3(42f, 0f, 0f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "petbond-owner";
                owner.RecalcFromStr(30);
                owner.ResetHp();

                healerGo = new GameObject("selfcheck-petbond-healer");
                healerGo.transform.position = ownerGo.transform.position;
                var healer = healerGo.AddComponent<WorldBody>();
                healer.IsAvatar = true;
                healer.CharacterId = "petbond-healer";
                healer.RecalcFromStr(30);
                healer.ResetHp();
                var bag = healerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Bandage, 3);

                petGo = new GameObject("selfcheck-petbond-pet");
                petGo.transform.position = ownerGo.transform.position;
                var pet = petGo.AddComponent<WorldBody>();
                pet.IsEnemy = true;
                pet.MobId = TameCritter.Id;
                pet.DisplayName = TameCritter.DisplayName;
                pet.Tameable = true;
                pet.ControlSlots = TameCritter.ControlSlots;
                pet.MaxHp = 40f;
                pet.ResetHp();

                var tame = world.TryTame(owner, pet);
                if (!tame.Applied)
                    throw new InvalidOperationException("조련 실패: " + tame.FailReason);
                if (!pet.Bonded || pet.OwnerCharacterId != owner.CharacterId)
                    throw new InvalidOperationException("조련 후 Bonded 플래그와 주인이 있어야 합니다.");

                int slotsBefore = world.CountFollowers(owner.CharacterId);
                if (slotsBefore < 1)
                    throw new InvalidOperationException("조련 후 팔로워 슬롯이 잡혀야 합니다.");

                pet.ApplyDamage((int)pet.MaxHp + 20);
                if (pet.Alive || !pet.Ghost)
                    throw new InvalidOperationException("Bonded 펫 HP0은 Ghost가 되어야 합니다.");
                if (string.IsNullOrEmpty(pet.OwnerCharacterId) || !pet.Bonded)
                    throw new InvalidOperationException("Bonded 펫 Ghost는 주인/본드를 유지해야 합니다.");
                if (world.CountFollowers(owner.CharacterId) != slotsBefore)
                    throw new InvalidOperationException("펫 Ghost 후에도 팔로워 슬롯이 유지되어야 합니다.");
                if (OfflineWorld.FindCorpse(owner.CharacterId) != null)
                    throw new InvalidOperationException("펫 Ghost는 시체 룻이 생기면 안 됩니다.");

                // Unbonded death: no ghost
                wildGo = new GameObject("selfcheck-petbond-wild");
                wildGo.transform.position = ownerGo.transform.position + new Vector3(1f, 0f, 0f);
                var wild = wildGo.AddComponent<WorldBody>();
                wild.IsEnemy = true;
                wild.MobId = MobCatalog.Bandit;
                wild.DisplayName = "도적";
                wild.MaxHp = 30f;
                wild.ResetHp();
                wild.Bonded = false;
                wild.ApplyDamage((int)wild.MaxHp + 5);
                if (wild.Ghost)
                    throw new InvalidOperationException("비 Bonded 몹 죽음은 Ghost가 되면 안 됩니다.");

                // player bandage rez must fail on pet ghost
                var playRez = world.TryResurrectBandage(healer, pet);
                if (playRez.Applied)
                    throw new InvalidOperationException("플레이어 붕대 부활은 펫 Ghost에 들어가면 안 됩니다.");

                var hit = world.TryVetResurrect(healer, pet);
                if (!hit.Applied || pet.Ghost || !pet.Alive)
                    throw new InvalidOperationException("TryVetResurrect 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("서버 펫 부활 후 Veterinary 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Healing)) > 0.0001f)
                    throw new InvalidOperationException("서버 펫 부활은 치유를 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 펫 부활은 마법을 올리면 안 됩니다.");
                int left = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        left += bag.Items[i].Amount;
                if (left != 2)
                    throw new InvalidOperationException("성공 펫 부활은 붕대 1을 소모해야 합니다.");
                if (string.IsNullOrEmpty(world.LastVetRezMessage) || world.LastVetRezMessage.IndexOf("부활", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("펫 부활 메시지가 있어야 합니다.");

                // TryVet routes to rez when pet ghost again
                pet.ApplyDamage((int)pet.MaxHp + 20);
                if (!pet.Ghost)
                    throw new InvalidOperationException("재사망 후 Ghost여야 합니다.");
                var viaVet = world.TryVet(healer, pet);
                if (!viaVet.Applied || pet.Ghost)
                    throw new InvalidOperationException("TryVet(pet Ghost)는 부활이어야 합니다: " + viaVet.FailReason);

                // Stable claim is separate — ghost pet still owned, not stabled path
                if (world.HasStabled(owner.CharacterId))
                    throw new InvalidOperationException("펫 부활 슬라이스는 Stable claim이 아닙니다.");

                var rel = world.TryPetRelease(owner, pet);
                if (!rel.Applied)
                    throw new InvalidOperationException("부활 후 release가 되어야 합니다: " + rel.FailReason);
                if (pet.Bonded)
                    throw new InvalidOperationException("release 후 Bonded가 꺼져야 합니다.");

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("Bonded Pet 부활 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (healerGo != null)
                    UnityEngine.Object.DestroyImmediate(healerGo);
                if (petGo != null)
                    UnityEngine.Object.DestroyImmediate(petGo);
                if (wildGo != null)
                    UnityEngine.Object.DestroyImmediate(wildGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


    }
}
