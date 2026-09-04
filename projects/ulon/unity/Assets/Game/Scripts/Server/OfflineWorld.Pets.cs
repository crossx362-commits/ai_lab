using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public int CountFollowers(string ownerCharacterId)
        {
            if (string.IsNullOrEmpty(ownerCharacterId))
                return 0;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null && list[i].OwnerCharacterId == ownerCharacterId
                    && !list[i].PetStabled && list[i].gameObject.activeInHierarchy)
                    n += list[i].ControlSlots < 1 ? 1 : list[i].ControlSlots;
            }
            return n;
        }

        public void TickPets()
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var pet = list[i];
                if (pet == null || string.IsNullOrEmpty(pet.OwnerCharacterId) || pet.PetStabled || !pet.Alive)
                    continue;
                if (pet.PetAttackTarget != null)
                {
                    WorldBody prey = pet.PetAttackTarget;
                    if (prey == null || !prey.Alive || !prey.IsEnemy || prey.IsAvatar)
                    {
                        pet.PetAttackTarget = null;
                    }
                    else
                    {
                        Vector3 t = prey.transform.position;
                        pet.transform.position = new Vector3(t.x + 0.6f, pet.transform.position.y, t.z + 0.6f);
                        TryAttack(pet, prey);
                        if (prey == null || !prey.Alive)
                            pet.PetAttackTarget = null;
                        continue;
                    }
                }
                if (!pet.PetFollow)
                    continue;
                WorldBody owner = null;
                for (int j = 0; j < list.Length; j++)
                {
                    if (list[j] != null && list[j].IsAvatar && list[j].CharacterId == pet.OwnerCharacterId)
                    {
                        owner = list[j];
                        break;
                    }
                }
                if (owner == null)
                    continue;
                Vector3 o = owner.transform.position;
                pet.transform.position = new Vector3(o.x + TameCritter.FollowOffsetX, pet.transform.position.y, o.z + TameCritter.FollowOffsetZ);
            }
        }

        public AttackResult TryTame(WorldBody body, WorldBody target)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (target == null)
                return new AttackResult { FailReason = "no_target" };
            var req = new TameRequest
            {
                Distance = Vector3.Distance(body.transform.position, target.transform.position),
                Range = TameResolve.Range,
                Ghost = body.Ghost,
                Tameable = target.Tameable || MobCatalog.TamableOf(target.MobId),
                AlreadyPet = !string.IsNullOrEmpty(target.OwnerCharacterId),
                UsedSlots = CountFollowers(body.CharacterId),
                ControlSlots = target.ControlSlots < 1 ? TameCritter.ControlSlots : target.ControlSlots,
                FollowerCap = TameResolve.FollowerCap,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = TameResolve.Difficulty
            };
            AttackResult result = TameResolve.Tame(req);
            if (!result.Applied)
            {
                LastTameMessage = result.FailReason;
                return result;
            }
            string ownerId = body.CharacterId;
            if (string.IsNullOrEmpty(ownerId))
                ownerId = PersistDriver.AccountKey();
            if (string.IsNullOrEmpty(ownerId))
                ownerId = "local";
            target.OwnerCharacterId = ownerId;
            if (string.IsNullOrEmpty(body.CharacterId))
                body.CharacterId = ownerId;
            target.PetFollow = true;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            target.IsEnemy = false;
            target.Tameable = true;
            target.Bonded = true;
            LastTameMessage = target.DisplayName + " 조련";
            OpLog.Write("tame", PersistDriver.AccountKey(), target.DisplayName, LastTameMessage);
            return result;
        }

        public AttackResult TryPetFollow(WorldBody body, WorldBody target)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = target != null && !string.IsNullOrEmpty(target.OwnerCharacterId),
                IsOwner = body != null && target != null && !string.IsNullOrEmpty(body.CharacterId) && target.OwnerCharacterId == body.CharacterId
            };
            AttackResult result = TameResolve.Follow(req);
            if (!result.Applied)
                return result;
            target.PetFollow = true;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            LastTameMessage = "따라와";
            return result;
        }

        public AttackResult TryPetStay(WorldBody body, WorldBody target)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = target != null && !string.IsNullOrEmpty(target.OwnerCharacterId),
                IsOwner = body != null && target != null && !string.IsNullOrEmpty(body.CharacterId) && target.OwnerCharacterId == body.CharacterId
            };
            AttackResult result = TameResolve.Stay(req);
            if (!result.Applied)
                return result;
            target.PetFollow = false;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            LastTameMessage = "머물러";
            return result;
        }

        public AttackResult TryPetGuard(WorldBody body, WorldBody target)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = target != null && !string.IsNullOrEmpty(target.OwnerCharacterId),
                IsOwner = body != null && target != null && !string.IsNullOrEmpty(body.CharacterId) && target.OwnerCharacterId == body.CharacterId
            };
            AttackResult result = TameResolve.Guard(req);
            if (!result.Applied)
                return result;
            target.PetFollow = true;
            target.PetGuard = true;
            target.PetAttackTarget = null;
            LastTameMessage = "지켜";
            return result;
        }

        public AttackResult TryPetRelease(WorldBody body, WorldBody target)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = target != null && !string.IsNullOrEmpty(target.OwnerCharacterId),
                IsOwner = body != null && target != null && !string.IsNullOrEmpty(body.CharacterId) && target.OwnerCharacterId == body.CharacterId
            };
            AttackResult result = TameResolve.Release(req);
            if (!result.Applied)
                return result;
            target.OwnerCharacterId = "";
            target.PetFollow = false;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            target.Bonded = false;
            LastTameMessage = "놓아줌";
            OpLog.Write("tame", PersistDriver.AccountKey(), target.DisplayName, LastTameMessage);
            return result;
        }

        public AttackResult TryPetAttack(WorldBody body, WorldBody pet, WorldBody enemy)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = pet != null && !string.IsNullOrEmpty(pet.OwnerCharacterId),
                IsOwner = body != null && pet != null && !string.IsNullOrEmpty(body.CharacterId) && pet.OwnerCharacterId == body.CharacterId,
                PetAlive = pet != null && pet.Alive,
                PetStabled = pet != null && pet.PetStabled,
                HasEnemy = enemy != null && enemy.IsEnemy && enemy.Alive && !enemy.IsAvatar
            };
            AttackResult result = TameResolve.Attack(req);
            if (!result.Applied)
                return result;
            pet.PetFollow = false;
            pet.PetGuard = false;
            pet.PetAttackTarget = enemy;
            LastTameMessage = "공격";
            return result;
        }

        public AttackResult TryPetCome(WorldBody body, WorldBody pet)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = pet != null && !string.IsNullOrEmpty(pet.OwnerCharacterId),
                IsOwner = body != null && pet != null && !string.IsNullOrEmpty(body.CharacterId) && pet.OwnerCharacterId == body.CharacterId,
                PetAlive = pet != null && pet.Alive,
                PetStabled = pet != null && pet.PetStabled
            };
            AttackResult result = TameResolve.Come(req);
            if (!result.Applied)
                return result;
            pet.PetFollow = true;
            pet.PetGuard = false;
            pet.PetAttackTarget = null;
            LastTameMessage = "이리와";
            return result;
        }

        public static StableMaster FindStable(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = StableYard.Object;
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<StableMaster>() : null;
        }

        public bool HasStabled(string characterId)
        {
            return TryGetStable(characterId, out _);
        }

        public void ClearStabled(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return;
            stables.Remove(characterId);
            PersistStable(new StableRecord { CharacterId = characterId });
        }

        bool TryGetStable(string characterId, out StableRecord rec)
        {
            rec = null;
            if (string.IsNullOrEmpty(characterId))
                return false;
            if (stables.TryGetValue(characterId, out rec) && rec != null && !string.IsNullOrEmpty(rec.PetId))
                return true;
            var snap = CharacterStore.LoadStable(characterId);
            if (snap != null && !string.IsNullOrEmpty(snap.PetId))
            {
                rec = new StableRecord
                {
                    CharacterId = characterId,
                    PetId = snap.PetId,
                    ControlSlots = snap.ControlSlots < 1 ? 1 : snap.ControlSlots,
                    DisplayName = snap.DisplayName ?? ""
                };
                stables[characterId] = rec;
                return true;
            }
            rec = null;
            return false;
        }

        WorldBody FindOwnedFollower(string ownerCharacterId)
        {
            if (string.IsNullOrEmpty(ownerCharacterId))
                return null;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var pet = list[i];
                if (pet == null || pet.PetStabled)
                    continue;
                if (pet.OwnerCharacterId == ownerCharacterId)
                    return pet;
            }
            return null;
        }

        WorldBody FindPetBody(string petId)
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var pet = list[i];
                if (pet == null)
                    continue;
                if (!string.IsNullOrEmpty(petId) && pet.MobId == petId)
                    return pet;
                if (pet.gameObject.name == TameCritter.Object)
                    return pet;
            }
            return null;
        }

        static void HidePet(WorldBody pet, bool hide)
        {
            if (pet == null)
                return;
            pet.gameObject.SetActive(!hide);
        }

        void PersistStable(StableRecord rec)
        {
            if (rec == null || string.IsNullOrEmpty(rec.CharacterId))
                return;
            CharacterStore.SaveStable(new StableSnapshot
            {
                CharacterId = rec.CharacterId,
                PetId = rec.PetId ?? "",
                ControlSlots = rec.ControlSlots < 1 ? 1 : rec.ControlSlots,
                DisplayName = rec.DisplayName ?? ""
            });
        }

        public AttackResult TryStable(WorldBody body, StableMaster stable)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (stable == null)
                return new AttackResult { FailReason = "no_stable" };
            string ownerId = body.CharacterId;
            if (string.IsNullOrEmpty(ownerId))
                ownerId = PersistDriver.AccountKey();
            if (string.IsNullOrEmpty(ownerId))
                ownerId = "local";
            if (string.IsNullOrEmpty(body.CharacterId))
                body.CharacterId = ownerId;
            var pet = FindOwnedFollower(ownerId);
            var req = new StableRequest
            {
                Distance = Vector3.Distance(body.transform.position, stable.transform.position),
                Range = stable.InteractRange,
                Ghost = body.Ghost,
                HasFollower = pet != null,
                HasStabled = TryGetStable(ownerId, out _),
                Gold = body.Gold,
                GoldCost = StableYard.GoldCost,
                HasStable = true
            };
            AttackResult result = StableResolve.Park(req);
            if (!result.Applied)
            {
                LastStableMessage = result.FailReason;
                return result;
            }
            body.Gold -= StableYard.GoldCost;
            var rec = new StableRecord
            {
                CharacterId = ownerId,
                PetId = string.IsNullOrEmpty(pet.MobId) ? TameCritter.Id : pet.MobId,
                ControlSlots = pet.ControlSlots < 1 ? TameCritter.ControlSlots : pet.ControlSlots,
                DisplayName = pet.DisplayName
            };
            stables[ownerId] = rec;
            pet.PetFollow = false;
            pet.PetGuard = false;
            pet.PetAttackTarget = null;
            pet.PetStabled = true;
            pet.OwnerCharacterId = "";
            pet.Bonded = false;
            HidePet(pet, true);
            PersistStable(rec);
            LastStableMessage = "마구간 맡김";
            OpLog.Write("stable", PersistDriver.AccountKey(), rec.DisplayName, LastStableMessage);
            return result;
        }

        public AttackResult TryClaimStable(WorldBody body, StableMaster stable)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (stable == null)
                return new AttackResult { FailReason = "no_stable" };
            string ownerId = body.CharacterId;
            if (string.IsNullOrEmpty(ownerId))
                ownerId = PersistDriver.AccountKey();
            if (string.IsNullOrEmpty(ownerId))
                ownerId = "local";
            if (string.IsNullOrEmpty(body.CharacterId))
                body.CharacterId = ownerId;
            TryGetStable(ownerId, out StableRecord rec);
            var req = new StableRequest
            {
                Distance = Vector3.Distance(body.transform.position, stable.transform.position),
                Range = stable.InteractRange,
                Ghost = body.Ghost,
                HasStabled = rec != null && !string.IsNullOrEmpty(rec.PetId),
                UsedSlots = CountFollowers(ownerId),
                ControlSlots = rec != null && rec.ControlSlots > 0 ? rec.ControlSlots : 1,
                FollowerCap = TameResolve.FollowerCap,
                HasStable = true
            };
            AttackResult result = StableResolve.Claim(req);
            if (!result.Applied)
            {
                LastStableMessage = result.FailReason;
                return result;
            }
            var pet = FindPetBody(rec.PetId);
            if (pet == null)
                return new AttackResult { FailReason = "no_pet" };
            HidePet(pet, false);
            pet.PetStabled = false;
            pet.OwnerCharacterId = ownerId;
            pet.PetFollow = true;
            pet.PetGuard = false;
            pet.PetAttackTarget = null;
            pet.IsEnemy = false;
            pet.Tameable = true;
            pet.Bonded = true;
            Vector3 s = stable.transform.position;
            pet.transform.position = new Vector3(s.x + TameCritter.FollowOffsetX, pet.transform.position.y, s.z + TameCritter.FollowOffsetZ);
            stables.Remove(ownerId);
            PersistStable(new StableRecord { CharacterId = ownerId });
            LastStableMessage = "마구간 찾음";
            OpLog.Write("stable", PersistDriver.AccountKey(), pet.DisplayName, LastStableMessage);
            return result;
        }

    }
}
