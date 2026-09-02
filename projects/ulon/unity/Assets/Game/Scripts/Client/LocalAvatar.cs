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

            var vendor = hit.collider.GetComponentInParent<VendorStation>();
            if (vendor != null)
            {
                if (down)
                    TryUseVendor(vendor);
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
            if (body != null && body.IsEnemy && body.Alive)
            {
                if (down)
                {
                    OfflineWorld.Instance?.Select(body);
                    chasing = body;
                }
                return;
            }

            var mine = GetComponent<WorldBody>();
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
