using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public AttackResult TryGather(WorldBody body, ResourceNode node)
        {
            if (body == null || node == null)
                return new AttackResult { FailReason = "no_node" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            node.Tick(Time.time);
            if (node.Remaining <= 0)
                return new AttackResult { FailReason = "depleted" };
            float dist = Vector3.Distance(body.transform.position, node.transform.position);
            if (dist > node.InteractRange)
                return new AttackResult { FailReason = "range" };
            var bag = Bag(body);
            if (!bag.CanCarry(StatsOf(body).Str, node.ResourceId, 1))
            {
                LastWeightMessage = WeightRefuseMessage(StatsOf(body).Str, bag);
                return new AttackResult { FailReason = "overweight" };
            }
            string tool = ItemCatalog.ToolFor(node.GatherSkill);
            if (!string.IsNullOrEmpty(tool) && !bag.WearTool(tool))
                return new AttackResult { FailReason = "no_tool" };

            var skills = SkillsOf(body);
            var req = new AttackRequest
            {
                Distance = 0f,
                Range = 99f,
                Now = Time.time,
                NextAttackAt = 0f,
                TargetAlive = true,
                Skills = skills,
                Difficulty = node.Difficulty,
                Damage = 0
            };
            SkillGain.TryRaise(skills, node.GatherSkill, node.Difficulty, out float before, out float after, StatsOf(body));
            if (body.IsAvatar)
                body.RecalcFromStr(StatsOf(body).Str);
            node.Remaining -= 1;
            node.AfterTake(Time.time);
            bag.Add(node.ResourceId, 1);
            return new AttackResult { Applied = true, Hit = true, SkillBefore = before, SkillAfter = after };
        }

        public AttackResult TryTrade(WorldBody from, WorldBody to)
        {
            if (from == null || to == null || from == to)
                return new AttackResult { FailReason = "no_target" };
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            if (dist > 2.8f)
                return new AttackResult { FailReason = "range" };
            ActiveTrade = new TradeSession { A = from, B = to };
            return new AttackResult { Applied = true };
        }

        public void SetTradeOffer(WorldBody me, string template)
        {
            ActiveTrade?.SetOffer(me, template);
        }

        public AttackResult ConfirmTrade(WorldBody me)
        {
            if (ActiveTrade == null)
                return new AttackResult { FailReason = "no_trade" };
            if (!ActiveTrade.SetAccept(me, true))
                return new AttackResult { FailReason = "waiting" };
            string offerA = ActiveTrade.OfferA;
            string offerB = ActiveTrade.OfferB;
            var a = ActiveTrade.A;
            var b = ActiveTrade.B;
            if (!string.IsNullOrEmpty(offerA) && CountItem(Bag(a), offerA) < 1)
                return CancelTrade("missing");
            if (!string.IsNullOrEmpty(offerB) && CountItem(Bag(b), offerB) < 1)
                return CancelTrade("missing");
            if (!string.IsNullOrEmpty(offerA))
            {
                ConsumeItem(Bag(a), offerA, 1);
                Bag(b).Add(offerA, 1);
            }
            if (!string.IsNullOrEmpty(offerB))
            {
                ConsumeItem(Bag(b), offerB, 1);
                Bag(a).Add(offerB, 1);
            }
            ActiveTrade = null;
            OpLog.Write("trade", PersistDriver.AccountKey(), "trade", (offerA ?? "") + "<->" + (offerB ?? ""));
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult CancelTrade(string reason = "cancel")
        {
            ActiveTrade = null;
            return new AttackResult { FailReason = reason };
        }

        static InventoryBag Bag(WorldBody body)
        {
            return body.GetComponent<InventoryBag>() ?? body.gameObject.AddComponent<InventoryBag>();
        }

        static BankVault Vault(WorldBody body)
        {
            return body.GetComponent<BankVault>() ?? body.gameObject.AddComponent<BankVault>();
        }

        public AttackResult TryBank(WorldBody body, BankStation station)
        {
            if (body == null || station == null)
                return new AttackResult { FailReason = "no_bank" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (dist > station.InteractRange)
                return new AttackResult { FailReason = "range" };
            if (Bag(body).Items.Count > 0)
                return DepositAll(body);
            return WithdrawAll(body);
        }

        public AttackResult TrySpeechKeyword(WorldBody body, string text)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (string.IsNullOrWhiteSpace(text))
            {
                LastSpeechMessage = "";
                return new AttackResult { FailReason = "empty" };
            }
            string raw = text.Trim();
            string key = raw.ToLowerInvariant();
            if (key == "bank" || raw == "은행")
                return SpeechBank(body);
            if (key == "guards" || raw == "경비")
                return SpeechGuards(body);
            if (key == "vendor" || raw == "상점")
                return SpeechVendor(body);
            LastSpeechMessage = "";
            return new AttackResult { FailReason = "no_match" };
        }

        AttackResult SpeechBank(WorldBody body)
        {
            var station = FindBank("Banker");
            if (station == null)
            {
                var banks = Object.FindObjectsByType<BankStation>(FindObjectsSortMode.None);
                float best = float.MaxValue;
                for (int i = 0; i < banks.Length; i++)
                {
                    float d = Vector3.Distance(body.transform.position, banks[i].transform.position);
                    if (d >= best)
                        continue;
                    best = d;
                    station = banks[i];
                }
            }
            if (station == null)
            {
                LastSpeechMessage = "은행 없음";
                return new AttackResult { FailReason = "no_bank" };
            }
            float dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (dist > station.InteractRange)
                WarpBody(body, station.transform.position.x, station.transform.position.z);
            var result = TryBank(body, station);
            LastSpeechMessage = "은행";
            if (result.Applied)
                return result;
            if (result.FailReason == "empty_bag" || result.FailReason == "empty_bank")
                return new AttackResult { Applied = true, Hit = true };
            return result;
        }

        AttackResult SpeechGuards(WorldBody body)
        {
            bool inZone = GuardZone.Contains(body.transform.position.x, body.transform.position.z);
            if (body.Notoriety == NotorietyId.Criminal || body.Notoriety == NotorietyId.Murderer)
            {
                if (inZone)
                {
                    GuardStrike(body);
                    LastSpeechMessage = "경비";
                    return new AttackResult { Applied = true, Hit = true };
                }
                LastSpeechMessage = "경비는 마을에만 있다.";
                return new AttackResult { Applied = true };
            }
            LastSpeechMessage = "경비가 순찰 중이다.";
            return new AttackResult { Applied = true };
        }

        AttackResult SpeechVendor(WorldBody body)
        {
            var vendors = Object.FindObjectsByType<VendorStation>(FindObjectsSortMode.None);
            VendorStation nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < vendors.Length; i++)
            {
                float d = Vector3.Distance(body.transform.position, vendors[i].transform.position);
                if (d >= best)
                    continue;
                best = d;
                nearest = vendors[i];
            }
            if (nearest == null)
            {
                LastSpeechMessage = "상점 없음";
                return new AttackResult { FailReason = "no_vendor" };
            }
            if (best > nearest.InteractRange)
                WarpBody(body, nearest.transform.position.x, nearest.transform.position.z);
            var result = TryVendor(body, nearest);
            if (result.Applied)
            {
                LastSpeechMessage = "상점";
                return result;
            }
            LastSpeechMessage = "상점: " + nearest.DisplayName + " 쪽으로";
            return new AttackResult { Applied = true };
        }


        static string AccountOf(WorldBody body)
        {
            if (body != null && !string.IsNullOrEmpty(body.AccountId))
                return body.AccountId;
            if (body != null && !string.IsNullOrEmpty(body.CharacterId))
                return body.CharacterId;
            return PersistDriver.AccountKey();
        }

        static string CharacterOf(WorldBody body)
        {
            if (body != null && !string.IsNullOrEmpty(body.CharacterId))
                return body.CharacterId;
            return AccountOf(body);
        }

        HouseRecord RecordOf(string plotId)
        {
            if (string.IsNullOrEmpty(plotId))
                plotId = HousingPlot.Id;
            if (!houses.TryGetValue(plotId, out HouseRecord rec) || rec == null)
            {
                rec = new HouseRecord { PlotId = plotId };
                houses[plotId] = rec;
            }
            return rec;
        }

    }
}
