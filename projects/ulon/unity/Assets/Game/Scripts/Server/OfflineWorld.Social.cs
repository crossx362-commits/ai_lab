using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        /// <summary>
        /// **초대할 수 있는 가장 가까운 몸** — 씬 이름이 아니라 **거리**로 고른다(검수 지시 2026-09-08).
        ///
        /// 전에는 HUD가 `GameObject.Find("Companion")`으로 대상을 잡았다. 그런데 네트워크에서는
        /// `AutoStartNetwork.PrepareSceneForNetwork`가 `Player`·`Companion`을 꺼 버리므로 그 몸이
        /// 사라지고, **버튼 자체가 안 그려져** 온라인에서 파티를 만들 방법이 없었다. 파티에 매달린
        /// 규칙(시체 파티 우선권·유령의 파티원 통신)까지 통째로 도달 불가였다.
        ///
        /// 그래서 대상 선정을 서버 규칙과 **같은 자**로 바꾼다 — 반경은 `PartyResolve.InviteRange`고,
        /// 그 값은 결투·길드 초대와 같은 수다(임의로 고른 반경이 아니다). 오프라인 슬라이스에서는
        /// 옆에 선 동료가 그대로 가장 가까운 몸이라 기존 경로가 살아 있다.
        /// </summary>
        public static WorldBody NearestInvitee(WorldBody me, float radius)
        {
            if (me == null)
                return null;
            var all = Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            WorldBody best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                var b = all[i];
                if (b == null || b == me || b.IsEnemy || b.Ghost)
                    continue;
                float d = Vector3.Distance(me.transform.position, b.transform.position);
                if (d > radius || d >= bestD)
                    continue;
                bestD = d;
                best = b;
            }
            return best;
        }

        public AttackResult TryPartyInvite(WorldBody from, WorldBody to)
        {
            if (from == null || to == null || from == to)
                return new AttackResult { FailReason = "no_target" };
            if (from.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (to.IsEnemy)
                return new AttackResult { FailReason = "enemy" };
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            if (dist > PartyResolve.InviteRange)
                return new AttackResult { FailReason = "range" };
            if (from.Party != null && from.Party.Leader != from)
                return new AttackResult { FailReason = "not_leader" };
            if (from.Party != null && from.Party.Contains(to))
                return new AttackResult { FailReason = "already" };
            if (from.Party != null && from.Party.Members.Count >= 3)
                return new AttackResult { FailReason = "full" };
            if (from.Party == null)
                from.Party = new Party { Leader = from };
            if (!to.IsAvatar)
            {
                from.Party.Add(to);
                to.Party = from.Party;
                return new AttackResult { Applied = true };
            }
            from.Party.Pending = to;
            to.Party = from.Party;
            return new AttackResult { Applied = true };
        }

        public AttackResult TryPartyAccept(WorldBody body)
        {
            if (body == null || body.Party == null)
                return new AttackResult { FailReason = "no_party" };
            if (body.Party.Pending != body)
                return new AttackResult { FailReason = "no_invite" };
            float dist = Vector3.Distance(body.transform.position, body.Party.Leader.transform.position);
            if (dist > PartyResolve.AcceptRange)
                return new AttackResult { FailReason = "range" };
            body.Party.Add(body);
            return new AttackResult { Applied = true };
        }

        public AttackResult TryPartyLeave(WorldBody body)
        {
            if (body == null || body.Party == null || !body.Party.Contains(body))
                return new AttackResult { FailReason = "no_party" };
            var p = body.Party;
            if (body == p.Leader)
            {
                if (p.Leader != null) p.Leader.Party = null;
                for (int i = 0; i < p.Members.Count; i++)
                    if (p.Members[i] != null) p.Members[i].Party = null;
                if (p.Pending != null) p.Pending.Party = null;
                return new AttackResult { Applied = true };
            }
            p.Members.Remove(body);
            body.Party = null;
            return new AttackResult { Applied = true };
        }

        public AttackResult TryPartySay(WorldBody body, string text)
        {
            if (body == null || body.Party == null || !body.Party.Contains(body))
                return new AttackResult { FailReason = "no_party" };
            if (string.IsNullOrEmpty(text))
                return new AttackResult { FailReason = "empty" };
            body.Party.Say(body.DisplayName, text);
            return new AttackResult { Applied = true };
        }

        public AttackResult TryGuildCreate(WorldBody body, string name)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            name = GuildResolve.NormalizeName(name);
            var check = GuildResolve.Create(new GuildRequest
            {
                Name = name,
                Ghost = body.Ghost,
                AlreadyInGuild = !string.IsNullOrEmpty(body.GuildId),
                Gold = body.Gold,
                GoldCost = GuildRules.GoldCost
            });
            if (!check.Applied)
            {
                LastGuildMessage = Tell(body, check.FailReason);
                return check;
            }
            guildSeq++;
            string id = "g" + guildSeq;
            var guild = new Guild { Id = id, Name = name, Leader = body };
            guilds[id] = guild;
            body.GuildId = id;
            body.GuildName = name;
            body.Gold -= GuildRules.GoldCost;
            LastGuildMessage = Tell(body, "created");
            return new AttackResult { Applied = true, Hit = true, Damage = GuildRules.GoldCost };
        }

        public AttackResult TryGuildInvite(WorldBody from, WorldBody to)
        {
            if (from == null || to == null || from == to)
                return new AttackResult { FailReason = "no_target" };
            var guild = GuildOf(from);
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            var check = GuildResolve.Invite(new GuildRequest
            {
                Ghost = from.Ghost,
                HasGuild = guild != null,
                IsLeader = guild != null && guild.Leader == from,
                HasTarget = true,
                SameAsSelf = false,
                TargetEnemy = to.IsEnemy,
                TargetInGuild = !string.IsNullOrEmpty(to.GuildId),
                Distance = dist,
                Range = GuildRules.InviteRange
            });
            if (!check.Applied)
            {
                LastGuildMessage = Tell(from, check.FailReason);
                return check;
            }
            if (!to.IsAvatar)
            {
                guild.Add(to);
                to.GuildId = guild.Id;
                to.GuildName = guild.Name;
                LastGuildMessage = Tell(from, "joined");
                return new AttackResult { Applied = true };
            }
            guild.Pending = to;
            LastGuildMessage = Tell(from, "invited");
            return new AttackResult { Applied = true };
        }

        public AttackResult TryGuildAccept(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            Guild guild = null;
            foreach (var kv in guilds)
            {
                if (kv.Value != null && kv.Value.Pending == body)
                {
                    guild = kv.Value;
                    break;
                }
            }
            float dist = guild != null && guild.Leader != null
                ? Vector3.Distance(body.transform.position, guild.Leader.transform.position)
                : 999f;
            var check = GuildResolve.Accept(new GuildRequest
            {
                HasGuild = guild != null,
                HasPending = guild != null && guild.Pending != null,
                PendingIsMe = guild != null && guild.Pending == body,
                Ghost = body.Ghost,
                AlreadyInGuild = !string.IsNullOrEmpty(body.GuildId),
                Distance = dist,
                Range = GuildRules.AcceptRange
            });
            if (!check.Applied)
            {
                LastGuildMessage = Tell(body, check.FailReason);
                return check;
            }
            guild.Add(body);
            body.GuildId = guild.Id;
            body.GuildName = guild.Name;
            LastGuildMessage = Tell(body, "accepted");
            return new AttackResult { Applied = true };
        }

        public AttackResult TryGuildLeave(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            var guild = GuildOf(body);
            var check = GuildResolve.Leave(new GuildRequest { HasGuild = guild != null });
            if (!check.Applied)
            {
                LastGuildMessage = Tell(body, check.FailReason);
                return check;
            }
            if (body == guild.Leader)
            {
                ClearGuildWar(guild);
                ClearGuildMembers(guild);
                guilds.Remove(guild.Id);
            }
            else
            {
                guild.Members.Remove(body);
                if (guild.Pending == body)
                    guild.Pending = null;
                body.GuildId = "";
                body.GuildName = "";
            }
            LastGuildMessage = Tell(body, "left");
            return new AttackResult { Applied = true };
        }


        public AttackResult TryDuelInvite(WorldBody from, WorldBody to)
        {
            if (from == null || to == null)
                return new AttackResult { FailReason = "no_target" };
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            var check = DuelResolve.Invite(new DuelRequest
            {
                Ghost = from.Ghost,
                HasTarget = true,
                SameAsSelf = from == to,
                TargetEnemy = to.IsEnemy,
                TargetAvatar = to.IsAvatar,
                AlreadyDueling = from.DuelOpponent != null,
                TargetBusy = to.DuelOpponent != null || (to.PendingDuel != null && to.PendingDuel != from),
                Distance = dist,
                Range = DuelRules.InviteRange
            });
            if (!check.Applied)
            {
                LastDuelMessage = Tell(from, check.FailReason);
                return check;
            }
            from.PendingDuel = to;
            LastDuelMessage = Tell(from, "invited");
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryDuelAccept(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            WorldBody from = null;
            var all = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].PendingDuel == body)
                {
                    from = all[i];
                    break;
                }
            }
            float dist = from != null ? Vector3.Distance(body.transform.position, from.transform.position) : 999f;
            var check = DuelResolve.Accept(new DuelRequest
            {
                HasPending = from != null,
                PendingIsMe = from != null && from.PendingDuel == body,
                Ghost = body.Ghost,
                AlreadyDueling = body.DuelOpponent != null || (from != null && from.DuelOpponent != null),
                Distance = dist,
                Range = DuelRules.AcceptRange
            });
            if (!check.Applied)
            {
                LastDuelMessage = Tell(body, check.FailReason);
                return check;
            }
            from.PendingDuel = null;
            from.DuelOpponent = body;
            body.DuelOpponent = from;
            body.PendingDuel = null;
            LastDuelMessage = Tell(body, "accepted");
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryDuelEnd(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            var check = DuelResolve.End(new DuelRequest { InDuel = body.DuelOpponent != null });
            if (!check.Applied)
            {
                LastDuelMessage = Tell(body, check.FailReason);
                return check;
            }
            ClearDuel(body);
            LastDuelMessage = Tell(body, "ended");
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryDuelYield(WorldBody body)
        {
            var r = TryDuelEnd(body);
            if (r.Applied)
                LastDuelMessage = Tell(body, "yielded");
            return r;
        }

        void ClearDuel(WorldBody body)
        {
            if (body == null)
                return;
            var other = body.DuelOpponent;
            body.DuelOpponent = null;
            body.PendingDuel = null;
            if (other != null)
            {
                if (other.DuelOpponent == body)
                    other.DuelOpponent = null;
                if (other.PendingDuel == body)
                    other.PendingDuel = null;
            }
        }


        public AttackResult TryGuildWarDeclare(WorldBody from, WorldBody to)
        {
            if (from == null || to == null || from == to)
                return new AttackResult { FailReason = "no_target" };
            var ga = GuildOf(from);
            var gb = GuildOf(to);
            bool already = ga != null && gb != null && (ga.WarWithId == gb.Id || gb.WarWithId == ga.Id
                || (!string.IsNullOrEmpty(ga.WarWithId) && ga.WarWithId != (gb != null ? gb.Id : ""))
                || (!string.IsNullOrEmpty(gb != null ? gb.WarWithId : "") && gb.WarWithId != (ga != null ? ga.Id : "")));
            var check = GuildWarResolve.Declare(new GuildWarRequest
            {
                Ghost = from.Ghost,
                HasGuild = ga != null,
                IsLeader = ga != null && ga.Leader == from,
                HasTargetGuild = gb != null,
                SameGuild = ga != null && gb != null && ga.Id == gb.Id,
                AlreadyWar = already
            });
            if (!check.Applied)
            {
                LastGuildMessage = Tell(from, check.FailReason);
                return check;
            }
            ga.WarWithId = gb.Id;
            gb.WarWithId = ga.Id;
            LastGuildMessage = Tell(from, "war");
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryGuildWarPeace(WorldBody from)
        {
            if (from == null)
                return new AttackResult { FailReason = "no_target" };
            var ga = GuildOf(from);
            var gb = ga != null ? FindGuild(ga.WarWithId) : null;
            var check = GuildWarResolve.Peace(new GuildWarRequest
            {
                Ghost = from.Ghost,
                HasGuild = ga != null,
                IsLeader = ga != null && ga.Leader == from,
                AtWar = ga != null && gb != null && ga.WarWithId == gb.Id && gb.WarWithId == ga.Id
            });
            if (!check.Applied)
            {
                LastGuildMessage = Tell(from, check.FailReason);
                return check;
            }
            ClearGuildWar(ga);
            LastGuildMessage = Tell(from, "peace");
            return new AttackResult { Applied = true, Hit = true };
        }

        void ClearGuildWar(Guild guild)
        {
            if (guild == null)
                return;
            if (!string.IsNullOrEmpty(guild.WarWithId))
            {
                var enemy = FindGuild(guild.WarWithId);
                if (enemy != null && enemy.WarWithId == guild.Id)
                    enemy.WarWithId = "";
            }
            guild.WarWithId = "";
        }

        void ClearGuildMembers(Guild guild)
        {
            if (guild == null)
                return;
            if (guild.Leader != null)
            {
                guild.Leader.GuildId = "";
                guild.Leader.GuildName = "";
            }
            for (int i = 0; i < guild.Members.Count; i++)
            {
                var m = guild.Members[i];
                if (m == null)
                    continue;
                m.GuildId = "";
                m.GuildName = "";
            }
            if (guild.Pending != null)
            {
                guild.Pending = null;
            }
            guild.Members.Clear();
            guild.Leader = null;
        }


    }
}
