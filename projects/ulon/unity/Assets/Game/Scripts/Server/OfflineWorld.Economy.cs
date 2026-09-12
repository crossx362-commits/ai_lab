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
                LastWeightMessage = Tell(body, WeightRefuseMessage(StatsOf(body).Str, bag));
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
            if (from.Ghost || to.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            if (dist > 2.8f)
                return new AttackResult { FailReason = "range" };
            var session = new TradeSession { A = from, B = to };
            from.Trade = session;
            to.Trade = session;
            return new AttackResult { Applied = true };
        }

        public void SetTradeOffer(WorldBody me, string template)
        {
            if (me != null)
                me.Trade?.SetOffer(me, template);
        }

        public void SetTradeGold(WorldBody me, int gold)
        {
            if (me != null)
                me.Trade?.SetGold(me, gold);
        }

        public AttackResult ConfirmTrade(WorldBody me)
        {
            var t = me != null ? me.Trade : null;
            if (t == null)
                return new AttackResult { FailReason = "no_trade" };
            if (!t.Settled && !t.SetAccept(me, true))
                return new AttackResult { FailReason = "waiting" };
            var a = t.A;
            var b = t.B;
            var bagA = Bag(a);
            var bagB = Bag(b);
            string offerA = t.OfferA ?? "";
            string offerB = t.OfferB ?? "";
            float wA = string.IsNullOrEmpty(offerA) ? 0f : ItemCatalog.WeightOf(offerA);
            float wB = string.IsNullOrEmpty(offerB) ? 0f : ItemCatalog.WeightOf(offerB);
            float extraB = wA - wB;
            float extraA = wB - wA;
            var check = TradeResolve.Settle(new TradeSettleRequest
            {
                Settled = t.Settled,
                AcceptA = t.AcceptA,
                AcceptB = t.AcceptB,
                GoldA = t.GoldA,
                GoldB = t.GoldB,
                HaveGoldA = a != null ? a.Gold : 0,
                HaveGoldB = b != null ? b.Gold : 0,
                OfferA = offerA,
                OfferB = offerB,
                HaveItemA = string.IsNullOrEmpty(offerA) || CountItem(bagA, offerA) >= 1,
                HaveItemB = string.IsNullOrEmpty(offerB) || CountItem(bagB, offerB) >= 1,
                CanCarryA = extraA <= 0f || bagA.CanCarryWeight(StatsOf(a).Str, extraA),
                CanCarryB = extraB <= 0f || bagB.CanCarryWeight(StatsOf(b).Str, extraB),
                GhostA = a != null && a.Ghost,
                GhostB = b != null && b.Ghost
            });
            if (!check.Applied)
                return check;
            if (!string.IsNullOrEmpty(offerA))
            {
                ConsumeItem(bagA, offerA, 1);
                bagB.Add(offerA, 1);
            }
            if (!string.IsNullOrEmpty(offerB))
            {
                ConsumeItem(bagB, offerB, 1);
                bagA.Add(offerB, 1);
            }
            if (a != null)
            {
                a.Gold -= t.GoldA;
                a.Gold += t.GoldB;
            }
            if (b != null)
            {
                b.Gold -= t.GoldB;
                b.Gold += t.GoldA;
            }
            t.Settled = true;
            if (a != null) a.Trade = null;
            if (b != null) b.Trade = null;
            OpLog.Write("trade", PersistDriver.AccountKey(), "trade",
                (offerA ?? "") + "+" + t.GoldA + "<->" + (offerB ?? "") + "+" + t.GoldB);
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult CancelTrade(string reason = "cancel") => CancelTrade(Player, reason);

        public AttackResult CancelTrade(WorldBody me, string reason = "cancel")
        {
            var t = me != null ? me.Trade : null;
            if (t != null)
            {
                if (t.A != null) t.A.Trade = null;
                if (t.B != null) t.B.Trade = null;
            }
            else if (me != null)
                me.Trade = null;
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
            // 사거리는 건물 표면에서 잰다 — 중심 기준이면 풍차 옆에 붙어 서도 「사거리 밖」이다(검수 랩 B).
            if (!WithinReach(body.transform.position, station.transform, station.InteractRange))
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
                LastSpeechMessage = Tell(body, "");
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
            LastSpeechMessage = Tell(body, "");
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
                LastSpeechMessage = Tell(body, "은행 없음");
                return new AttackResult { FailReason = "no_bank" };
            }
            // 대상 좌표 그대로 보내면 건물 안에 처박힌다 — 옆 빈자리로(검수 랩 B).
            if (!WithinReach(body.transform.position, station.transform, station.InteractRange))
                WarpTo(body, WarpBesideTarget(station.transform, station.InteractRange));
            var result = TryBank(body, station);
            LastSpeechMessage = Tell(body, "은행");
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
                    LastSpeechMessage = Tell(body, "경비");
                    return new AttackResult { Applied = true, Hit = true };
                }
                LastSpeechMessage = Tell(body, "경비는 마을에만 있다.");
                return new AttackResult { Applied = true };
            }
            LastSpeechMessage = Tell(body, "경비가 순찰 중이다.");
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
                LastSpeechMessage = Tell(body, "상점 없음");
                return new AttackResult { FailReason = "no_vendor" };
            }
            // 위와 같은 이유 — 상인 위가 아니라 상인 옆에 선다.
            if (!WithinReach(body.transform.position, nearest.transform, nearest.InteractRange))
                WarpTo(body, WarpBesideTarget(nearest.transform, nearest.InteractRange));
            var result = TryVendor(body, nearest);
            if (result.Applied)
            {
                LastSpeechMessage = Tell(body, "상점");
                return result;
            }
            LastSpeechMessage = Tell(body, "상점: " + nearest.DisplayName + " 쪽으로");
            return new AttackResult { Applied = true };
        }


        /// <summary>이 몸이 누구 것인가 — **계정 원장은 여기 하나뿐이다**(시체·집·거래가 같이 쓴다).</summary>
        internal static string AccountOf(WorldBody body)
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
