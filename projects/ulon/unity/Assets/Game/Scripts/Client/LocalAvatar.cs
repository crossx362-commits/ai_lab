using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Client
{
    [RequireComponent(typeof(ClickMotor))]
    public sealed class LocalAvatar : MonoBehaviour
    {
        [SerializeField] float engageRange = 2.2f;

        ClickMotor motor;
        CharacterAnim anim;
        WorldBody chasing;

        void Awake()
        {
            motor = GetComponent<ClickMotor>();
            anim = GetComponent<CharacterAnim>();
        }

        void Update()
        {
            if (PersistDriver.Creating || PersistDriver.Frozen)
                return;
            if (Input.GetMouseButton(0))
                HandlePointer(Input.GetMouseButtonDown(0));
            if (Input.GetKeyDown(KeyCode.F))
                CommandOwnPet(0);
            else if (Input.GetKeyDown(KeyCode.H))
                CommandOwnPet(1);
            else if (Input.GetKeyDown(KeyCode.G))
                CommandOwnPet(2);
            else if (Input.GetKeyDown(KeyCode.A))
                CommandOwnPetAttack();
            else if (Input.GetKeyDown(KeyCode.C))
                CommandOwnPetCome();

            TickChase();
        }

        void HandlePointer(bool down)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
                return;

            var node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                if (down)
                    TryUseNode(node);
                return;
            }

            var station = hit.collider.GetComponentInParent<CraftStation>();
            if (station != null)
            {
                if (down)
                    TryUseStation(station);
                return;
            }

            var bank = hit.collider.GetComponentInParent<BankStation>();
            if (bank != null)
            {
                if (down)
                    TryUseBank(bank);
                return;
            }

            var plot = hit.collider.GetComponentInParent<HousePlotStation>();
            if (plot != null)
            {
                if (down)
                    TryUseHousePlot(plot);
                return;
            }

            var houseChest = hit.collider.GetComponentInParent<HouseChest>();
            if (houseChest != null)
            {
                if (down)
                    TryUseHouseChest(houseChest);
                return;
            }

            var houseVendor = hit.collider.GetComponentInParent<HouseVendor>();
            if (houseVendor != null)
            {
                if (down)
                    TryUseHouseVendor(houseVendor);
                return;
            }

            var stable = hit.collider.GetComponentInParent<StableMaster>();
            if (stable != null)
            {
                if (down)
                    TryUseStable(stable);
                return;
            }

            var vendor = hit.collider.GetComponentInParent<VendorStation>();
            if (vendor != null)
            {
                if (down)
                    TryUseVendor(vendor);
                return;
            }

            var crate = hit.collider.GetComponentInParent<LockedCrate>();
            if (crate != null)
            {
                if (down)
                    TryUseCrate(crate);
                return;
            }

            var trainer = hit.collider.GetComponentInParent<TrainerStation>();
            if (trainer != null)
            {
                if (down)
                    TryUseTrainer(trainer);
                return;
            }

            var healer = hit.collider.GetComponentInParent<HealerStation>();
            if (healer != null)
            {
                if (down)
                    TryUseHealer(healer);
                return;
            }

            var gate = hit.collider.GetComponentInParent<DungeonGate>();
            if (gate != null)
            {
                if (down)
                    TryUseGate(gate);
                return;
            }

            var moon = hit.collider.GetComponentInParent<Moongate>();
            if (moon != null)
            {
                if (down)
                    TryUseMoongate(moon);
                return;
            }

            var corpse = hit.collider.GetComponentInParent<CorpseNode>();
            if (corpse != null)
            {
                if (down)
                    TryUseCorpse(corpse);
                return;
            }

            var mineGhost = GetComponent<WorldBody>();
            if (mineGhost != null && mineGhost.Ghost)
            {
                chasing = null;
                motor.SetDestination(hit.point);
                return;
            }

            var body = hit.collider.GetComponentInParent<WorldBody>();
            var mine = GetComponent<WorldBody>();
            if (down && body != null && body != mine)
            {
                bool wild = body.Tameable && string.IsNullOrEmpty(body.OwnerCharacterId);
                bool minePet = mine != null && !string.IsNullOrEmpty(body.OwnerCharacterId) && body.OwnerCharacterId == mine.CharacterId;
                if (wild || minePet)
                {
                    OfflineWorld.Instance?.Select(body);
                    if (wild)
                        OfflineWorld.Instance?.TryTame(mine, body);
                    else
                        CycleOwnPet(mine, body);
                    return;
                }
            }
            if (body != null && body.IsEnemy && body.Alive)
            {
                if (down)
                {
                    OfflineWorld.Instance?.Select(body);
                    chasing = body;
                }
                return;
            }

            if (down && body != null && !body.IsEnemy && body != mine)
            {
                TryTrade(body);
                return;
            }

            chasing = null;
            motor.SetDestination(hit.point);
        }

        void TickChase()
        {
            var self = GetComponent<WorldBody>();
            if (self != null && self.Ghost)
            {
                chasing = null;
                return;
            }
            if (chasing == null || !chasing.Alive)
            {
                chasing = null;
                return;
            }

            float dist = Vector3.Distance(transform.position, chasing.transform.position);
            var bag = GetComponent<InventoryBag>();
            SkillId weaponSkill = bag != null
                ? ItemCatalog.CombatSkillOf(ItemCatalog.CombatWeaponOf(bag.Items))
                : SkillId.Swordsmanship;
            float range = ItemCatalog.CombatRangeOf(weaponSkill);
            if (range < engageRange)
                range = engageRange;
            if (dist > range)
            {
                Vector3 to = chasing.transform.position - transform.position;
                to.y = 0f;
                motor.SetDestination(chasing.transform.position - to.normalized * (range * 0.7f));
                return;
            }

            motor.Stop();
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                var nob = chasing.GetComponent<FishNet.Object.NetworkObject>();
                if (nob != null)
                    net.RpcRequestAttack(nob);
                return;
            }

            var mine = GetComponent<WorldBody>();
            var result = OfflineWorld.Instance != null
                ? OfflineWorld.Instance.TryAttack(mine, chasing)
                : default;
            if (result.Applied)
                anim?.PlayAttack();
        }

        void TryUseNode(ResourceNode node)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, node.transform.position);
            if (dist > node.InteractRange)
            {
                motor.SetDestination(node.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcGather(node.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryGather(mine, node);
        }

        void TryUseStation(CraftStation station)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, station.transform.position);
            if (dist > station.InteractRange)
            {
                motor.SetDestination(station.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcCraft(station.gameObject.name, "");
                return;
            }
            OfflineWorld.Instance?.TryCraft(mine, station);
        }

        void TryUseBank(BankStation station)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, station.transform.position);
            if (dist > station.InteractRange)
            {
                motor.SetDestination(station.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcBank(station.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryBank(mine, station);
        }


        void TryUseCrate(LockedCrate crate)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, crate.transform.position);
            if (dist > crate.InteractRange)
            {
                motor.SetDestination(crate.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcPick(crate.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryPick(mine, crate);
        }

        void TryUseVendor(VendorStation station)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, station.transform.position);
            if (dist > station.InteractRange)
            {
                motor.SetDestination(station.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcVendor(station.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryVendor(mine, station);
        }

        void TryUseTrainer(TrainerStation station)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, station.transform.position);
            if (dist > station.InteractRange)
            {
                motor.SetDestination(station.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcTrainer(station.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryTrainer(mine, station);
        }

        void TryUseGate(DungeonGate gate)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, gate.transform.position);
            if (dist > gate.InteractRange)
            {
                motor.SetDestination(gate.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcDungeon(gate.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryDungeon(mine, gate);
        }

        void TryUseMoongate(Moongate gate)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, gate.transform.position);
            if (dist > gate.InteractRange)
            {
                motor.SetDestination(gate.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcGate(gate.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryGate(mine, gate);
        }

        void CycleOwnPet(WorldBody mine, WorldBody pet)
        {
            if (pet.PetGuard)
                OfflineWorld.Instance?.TryPetFollow(mine, pet);
            else if (pet.PetFollow)
                OfflineWorld.Instance?.TryPetStay(mine, pet);
            else
                OfflineWorld.Instance?.TryPetGuard(mine, pet);
        }

        void CommandOwnPet(int mode)
        {
            var mine = GetComponent<WorldBody>();
            var pet = FindOwnPet(mine);
            if (pet == null || OfflineWorld.Instance == null)
                return;
            if (mode == 1)
                OfflineWorld.Instance.TryPetStay(mine, pet);
            else if (mode == 2)
                OfflineWorld.Instance.TryPetGuard(mine, pet);
            else
                OfflineWorld.Instance.TryPetFollow(mine, pet);
        }

        void CommandOwnPetAttack()
        {
            var mine = GetComponent<WorldBody>();
            var pet = FindOwnPet(mine);
            if (pet == null || OfflineWorld.Instance == null)
                return;
            WorldBody enemy = FindNearbyEnemy(mine);
            OfflineWorld.Instance.TryPetAttack(mine, pet, enemy);
        }

        void CommandOwnPetCome()
        {
            var mine = GetComponent<WorldBody>();
            var pet = FindOwnPet(mine);
            if (pet == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryPetCome(mine, pet);
        }

        static WorldBody FindOwnPet(WorldBody mine)
        {
            if (mine == null || string.IsNullOrEmpty(mine.CharacterId))
                return null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == mine.CharacterId)
                    return b;
            }
            return null;
        }

        static WorldBody FindNearbyEnemy(WorldBody mine)
        {
            if (mine == null)
                return null;
            var sel = OfflineWorld.Instance != null ? OfflineWorld.Instance.Selected : null;
            if (sel != null && sel.IsEnemy && sel.Alive && !sel.IsAvatar)
            {
                float sd = Vector3.Distance(mine.transform.position, sel.transform.position);
                if (sd <= TameResolve.AttackRange)
                    return sel;
            }
            WorldBody best = null;
            float bestDist = TameResolve.AttackRange;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b == null || b == mine || !b.IsEnemy || !b.Alive || b.IsAvatar)
                    continue;
                float d = Vector3.Distance(mine.transform.position, b.transform.position);
                if (d > bestDist)
                    continue;
                bestDist = d;
                best = b;
            }
            return best;
        }

        void TryUseStable(StableMaster stable)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, stable.transform.position);
            if (dist > stable.InteractRange)
            {
                motor.SetDestination(stable.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcStable(stable.gameObject.name);
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            string cid = mine != null ? mine.CharacterId : "";
            if (OfflineWorld.Instance.HasStabled(cid))
                OfflineWorld.Instance.TryClaimStable(mine, stable);
            else
                OfflineWorld.Instance.TryStable(mine, stable);
        }

        void TryUseHealer(HealerStation station)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, station.transform.position);
            if (dist > station.InteractRange)
            {
                motor.SetDestination(station.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcResurrect(station.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryResurrect(mine, station);
        }

        void TryUseCorpse(CorpseNode node)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, node.transform.position);
            if (dist > node.InteractRange)
            {
                motor.SetDestination(node.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcLoot(node.OwnerId);
                return;
            }
            OfflineWorld.Instance?.TryLootCorpse(mine, node);
        }


        void TryUseHousePlot(HousePlotStation station)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, station.transform.position);
            if (dist > station.InteractRange)
            {
                motor.SetDestination(station.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                net.RpcClaimHouse(station.gameObject.name);
                return;
            }
            OfflineWorld.Instance?.TryClaimHouse(mine, station);
        }

        void TryUseHouseChest(HouseChest chest)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, chest.transform.position);
            if (dist > chest.InteractRange)
            {
                motor.SetDestination(chest.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            var bag = GetComponent<InventoryBag>();
            bool has = false;
            if (bag != null)
            {
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth && bag.Items[i].Amount > 0)
                    {
                        has = true;
                        break;
                    }
                }
            }
            if (net != null && net.IsClientInitialized)
            {
                if (has)
                    net.RpcHouseLockdown(chest.gameObject.name);
                else
                    net.RpcHouseTake(chest.gameObject.name);
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            if (has)
                OfflineWorld.Instance.TryLockdown(mine, chest, ItemCatalog.Cloth);
            else
                OfflineWorld.Instance.TrySecureTake(mine, chest);
        }

        void TryUseHouseVendor(HouseVendor vendor)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, vendor.transform.position);
            if (dist > vendor.InteractRange)
            {
                motor.SetDestination(vendor.transform.position);
                return;
            }
            var bag = GetComponent<InventoryBag>();
            bool has = false;
            if (bag != null)
            {
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth && bag.Items[i].Amount > 0)
                    {
                        has = true;
                        break;
                    }
                }
            }
            var net = GetComponent<NetAvatar>();
            bool owner = OfflineWorld.Instance != null && OfflineWorld.Instance.OwnsPlot(mine, vendor.PlotId);
            if (net != null && net.IsClientInitialized)
            {
                if (owner && has)
                    net.RpcHouseVendorList(vendor.gameObject.name);
                else
                    net.RpcHouseVendorBuy(vendor.gameObject.name);
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            if (owner && has)
                OfflineWorld.Instance.TryListVendor(mine, vendor, ItemCatalog.Cloth);
            else
                OfflineWorld.Instance.TryBuyHouseVendor(mine, vendor);
        }

        void TryTrade(WorldBody other)
        {
            var mine = GetComponent<WorldBody>();
            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist > 2.8f)
            {
                motor.SetDestination(other.transform.position);
                return;
            }
            var net = GetComponent<NetAvatar>();
            if (net != null && net.IsClientInitialized)
            {
                var nob = other.GetComponent<FishNet.Object.NetworkObject>();
                if (nob != null)
                    net.RpcTrade(nob);
                return;
            }
            OfflineWorld.Instance?.TryTrade(mine, other);
        }
    }
}
