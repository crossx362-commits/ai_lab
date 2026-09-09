using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
    {
        // **버튼이 서버로 보내는 것**(랩 ㉭) — 단추 하나가 어느 명령을 어떤 인자로 쏘는지만 담는다.
        // 담는 것: `NetAvatar`로 나가는 요청 래퍼. 안 담는 것: 그 단추를 어디에 그리는지(패널·상시 화면).
        static void Offer(NetAvatar net, WorldBody me, string template)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTradeOffer(template);
            else
                OfflineWorld.Instance?.SetTradeOffer(me, template);
        }

        static void Cast(NetAvatar net, SpellId spell)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCast((int)spell);
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryCast(OfflineWorld.Instance.Player, spell, OfflineWorld.Instance.Player.Selected);
        }

        static void Mark(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcMark();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryMark(OfflineWorld.Instance.Player);
        }

        static void Recall(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcRecall();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryRecall(OfflineWorld.Instance.Player);
        }

        static void Meditate(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcMeditate();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryMeditate(OfflineWorld.Instance.Player);
        }

        static void Evaluate(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcEvaluate();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryEvaluate(OfflineWorld.Instance.Player, OfflineWorld.Instance.Player.Selected);
        }

        static void Track(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTrack();
            else if (OfflineWorld.Instance != null)
            {
                var world = OfflineWorld.Instance;
                if (world.Player != null && world.Player.Selected != null)
                    world.TryTrack(world.Player, world.Player.Selected);
                else
                    world.TryTrackCorpse(world.Player, OfflineWorld.FindCorpse(world.Player != null ? world.Player.CharacterId : ""));
            }
        }

        static void Lore(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcLore();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryLore(OfflineWorld.Instance.Player, OfflineWorld.Instance.Player.Selected);
        }

        static void Vet(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcVet();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryVet(OfflineWorld.Instance.Player, OfflineWorld.Instance.Player.Selected);
        }

        static void Inscribe(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcInscribe();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryInscribe(OfflineWorld.Instance.Player);
        }

        static void PoisonWeapon(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPoisonWeapon();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPoisonWeapon(OfflineWorld.Instance.Player);
        }

        static void UseScroll(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcUseScroll();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryUseScroll(OfflineWorld.Instance.Player, OfflineWorld.Instance.Player.Selected);
        }

        static void PlayLute(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPlay();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPlay(OfflineWorld.Instance.Player);
        }

        static void Peace(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPeace();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPeace(OfflineWorld.Instance.Player, OfflineWorld.Instance.Player.Selected);
        }

        static void Provoke(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcProvoke();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryProvokeStep(OfflineWorld.Instance.Player);
        }

        static void Hide(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcHide();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryHide(OfflineWorld.Instance.Player);
        }

        static void Stealth(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcStealth();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryStealth(OfflineWorld.Instance.Player);
        }


        static void Camp(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCamp();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryCamp(OfflineWorld.Instance.Player);
        }

        static void DetectHidden(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcDetectHidden();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryDetectHidden(OfflineWorld.Instance.Player);
        }

        static void Steal(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcSteal();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TrySteal(OfflineWorld.Instance.Player);
        }

        static void ResurrectBandage(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcResurrectBandage();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Player.Selected;
                if (tgt == null || !tgt.Ghost || !tgt.IsAvatar || tgt == me)
                    tgt = OfflineWorld.NearestGhostAvatar(me);
                OfflineWorld.Instance.TryResurrectBandage(me, tgt);
            }
        }

        static void AcceptCraftOrder(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
            {
                net.RpcAcceptOrder();
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryAcceptOrder(OfflineWorld.Instance.Player);
        }

        static void TurnInCraftOrder(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
            {
                net.RpcTurnInOrder();
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryTurnInOrder(OfflineWorld.Instance.Player);
        }

        static void CurePoison(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCurePoison();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Player.Selected;
                if (tgt == null || tgt.IsEnemy || !tgt.Alive || tgt.Ghost)
                    tgt = me;
                OfflineWorld.Instance.TryCurePoison(me, tgt);
            }
        }


        static void PetAttack(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            WorldBody pet = null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == me.CharacterId)
                {
                    pet = b;
                    break;
                }
            }
            WorldBody enemy = OfflineWorld.Instance.Player.Selected;
            if (enemy == null || !enemy.IsEnemy || !enemy.Alive || enemy.IsAvatar)
            {
                enemy = null;
                float best = TameResolve.AttackRange;
                for (int i = 0; i < list.Length; i++)
                {
                    var b = list[i];
                    if (b == null || b == me || !b.IsEnemy || !b.Alive || b.IsAvatar)
                        continue;
                    float d = Vector3.Distance(me.transform.position, b.transform.position);
                    if (d > best)
                        continue;
                    best = d;
                    enemy = b;
                }
            }
            var pno = pet.GetComponent<FishNet.Object.NetworkObject>();
            if (net != null && net.IsClientInitialized && pno != null)
            {
                net.RpcPetAttack(pno, enemy != null ? enemy.GetComponent<FishNet.Object.NetworkObject>() : null);
                return;
            }
            OfflineWorld.Instance.TryPetAttack(me, pet, enemy);
        }


        static void SpeakKeyword(NetAvatar net, string text)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            if (net != null && net.IsClientInitialized)
            {
                net.RpcSpeech(text);
                return;
            }
            OfflineWorld.Instance.TrySpeechKeyword(me, text);
        }

        static void PetCome(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            WorldBody pet = null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == me.CharacterId)
                {
                    pet = b;
                    break;
                }
            }
            if (pet == null)
                return;
            var pno = pet.GetComponent<FishNet.Object.NetworkObject>();
            if (net != null && net.IsClientInitialized && pno != null)
            {
                net.RpcPetCome(pno);
                return;
            }
            OfflineWorld.Instance.TryPetCome(me, pet);
        }

        // 아래 5개는 서버 구현만 있고 클라 호출부가 없어 **플레이어가 쓸 수 없던** 기능이다
        // (2026-09-06 도달 스캔, AssertReachableFeatures). 셀프체크는 OfflineWorld를 직접 불러 통과시키고 있었다.
        static void PetRelease(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            WorldBody pet = null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == me.CharacterId)
                {
                    pet = b;
                    break;
                }
            }
            if (pet == null)
                return;
            var pno = pet.GetComponent<FishNet.Object.NetworkObject>();
            if (net != null && net.IsClientInitialized && pno != null)
            {
                net.RpcPetRelease(pno);
                return;
            }
            OfflineWorld.Instance.TryPetRelease(me, pet);
        }

        /// <summary>가방에서 착용할 무기를 고른다 — 없으면 아무것도 안 한다.</summary>
        static string EquipCandidate(WorldBody me)
        {
            var bag = me != null ? me.GetComponent<InventoryBag>() : null;
            if (bag == null)
                return "";
            for (int i = 0; i < bag.Items.Count; i++)
            {
                var rec = bag.Items[i];
                if (rec.Amount > 0 && ItemCatalog.IsMeleeWeapon(rec.TemplateId))
                    return rec.TemplateId;
            }
            return "";
        }

        /// <summary>
        /// **고른 물건에** 작용한다 — 「가방에서 알아서 하나 고른다」는 예전 방식은 플레이어가 원하는 것을
        /// 착용할 방법이 없었다(검수 2026-09-07: 기능이 아니라 도달 가능성 문제).
        /// 후보 자동 선택(`Equip`/`PouchIn`/`PouchOut`)은 남겨 둔다 — 다른 호출부·게이트가 쓴다.
        /// </summary>
        static void EquipItem(NetAvatar net, string id)
        {
            if (string.IsNullOrEmpty(id) || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcEquip(id);
            else
                OfflineWorld.Instance.TryEquip(OfflineWorld.Instance.Player, id);
        }

        static void PouchInItem(NetAvatar net, string id)
        {
            if (string.IsNullOrEmpty(id) || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcMoveToPouch(id);
            else
                OfflineWorld.Instance.TryMoveToPouch(OfflineWorld.Instance.Player, id, "");
        }

        static void PouchOutItem(NetAvatar net, string id)
        {
            if (string.IsNullOrEmpty(id) || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcTakeFromPouch(id);
            else
                OfflineWorld.Instance.TryTakeFromPouch(OfflineWorld.Instance.Player, id, "");
        }

        static void Equip(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            string id = EquipCandidate(OfflineWorld.Instance.Player);
            if (string.IsNullOrEmpty(id))
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcEquip(id);
            else
                OfflineWorld.Instance.TryEquip(OfflineWorld.Instance.Player, id);
        }

        static void Unequip(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcUnequip();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryUnequip(OfflineWorld.Instance.Player);
        }

        /// <summary>주머니에 넣을/에서 꺼낼 물건 — 주머니 자신은 넣을 수 없다(중첩 깊이 1).</summary>
        static string PouchCandidate(WorldBody me, bool inPouch)
        {
            var bag = me != null ? me.GetComponent<InventoryBag>() : null;
            if (bag == null)
                return "";
            string pouch = bag.PouchInstanceId();
            if (string.IsNullOrEmpty(pouch))
                return "";
            for (int i = 0; i < bag.Items.Count; i++)
            {
                var rec = bag.Items[i];
                if (rec.Amount <= 0 || ItemCatalog.IsContainer(rec.TemplateId))
                    continue;
                bool inside = rec.ParentContainerId == pouch;
                if (inside == inPouch)
                    return rec.TemplateId;
            }
            return "";
        }

        static void PouchIn(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            string id = PouchCandidate(OfflineWorld.Instance.Player, false);
            if (string.IsNullOrEmpty(id))
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcMoveToPouch(id);
            else
                OfflineWorld.Instance.TryMoveToPouch(OfflineWorld.Instance.Player, id, "");
        }

        static void PouchOut(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            string id = PouchCandidate(OfflineWorld.Instance.Player, true);
            if (string.IsNullOrEmpty(id))
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcTakeFromPouch(id);
            else
                OfflineWorld.Instance.TryTakeFromPouch(OfflineWorld.Instance.Player, id, "");
        }

        static void Pick(NetAvatar net, LockedCrate crate)
        {
            if (crate == null || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcPick(crate.gameObject.name);
            else
                OfflineWorld.Instance.TryPick(OfflineWorld.Instance.Player, crate);
        }

        static void Drink(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcDrink();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryDrink(OfflineWorld.Instance.Player);
        }

        static void Bandage(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcHeal();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Player.Selected;
                if (tgt != null && tgt.Ghost && tgt.IsAvatar && tgt != me)
                {
                    OfflineWorld.Instance.TryResurrectBandage(me, tgt);
                    return;
                }
                if (tgt == null || tgt.IsEnemy || !tgt.Alive)
                    tgt = me;
                var healed = OfflineWorld.Instance.TryHeal(me, tgt);
                if (healed.Applied && tgt != null)
                {
                    ActionVfx.Play(ActionVfx.Kind.Heal, tgt.transform.position + Vector3.up * 1.0f);
                    ActionSfx.Play(ActionSfx.Kind.Heal, tgt.transform.position + Vector3.up * 1.0f);
                }
            }
        }

        static void Train(NetAvatar net, SkillId skill)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTrain((int)skill);
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryTrain(OfflineWorld.Instance.Player, skill);
        }

        /// <summary>도구가 얼마나 남았는지 — 곡괭이·도끼·낚싯대의 남은 사용 횟수(§18.8 Tool Uses).</summary>
        static string ToolLine(InventoryBag bag)
        {
            if (bag == null)
                return "도구 없음";
            string[] tools = { ItemCatalog.Pickaxe, ItemCatalog.Hatchet, ItemCatalog.FishingPole };
            string[] names = { "곡괭이", "도끼", "낚싯대" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < tools.Length; i++)
            {
                int uses = bag.ToolUses(tools[i]);
                if (uses <= 0)
                    continue;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(names[i]).Append(' ').Append(uses);
            }
            return sb.Length == 0 ? "도구 없음" : sb.ToString();
        }

        static void RepairAt(NetAvatar net, CraftStation station)
        {
            if (station == null || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
            {
                net.RpcRepair(station.gameObject.name);
                return;
            }
            OfflineWorld.Instance.TryRepair(OfflineWorld.Instance.Player, station);
        }

        static void CraftAt(NetAvatar net, CraftStation station, string recipeId)
        {
            if (station == null || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
            {
                net.RpcCraft(station.gameObject.name, recipeId);
                return;
            }
            var made = OfflineWorld.Instance.TryCraft(OfflineWorld.Instance.Player, station, recipeId);
            if (made.Applied)
            {
                ActionVfx.Play(ActionVfx.Kind.Craft, station.transform.position + Vector3.up * 1.1f);
                ActionSfx.Play(ActionSfx.Kind.Craft, station.transform.position + Vector3.up * 1.1f);
            }
        }

        static void Shop(NetAvatar net, bool buy, string template)
        {
            if (net != null && net.IsClientInitialized)
            {
                if (buy) net.RpcBuy(template);
                else net.RpcSell(template);
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            if (buy)
                OfflineWorld.Instance.TryBuy(OfflineWorld.Instance.Player, template);
            else
                OfflineWorld.Instance.TrySell(OfflineWorld.Instance.Player, template);
        }
    }
}
