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
        static void AssertGuildSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (GuildRules.GoldCost != 25)
                throw new InvalidOperationException("길드 창설 골드는 25여야 합니다.");
            if (GuildRules.NameMin != 1 || GuildRules.NameMax != 12)
                throw new InvalidOperationException("길드 이름은 1~12자여야 합니다.");

            var emptyName = GuildResolve.Create(new GuildRequest { Name = "", Gold = 25 });
            if (emptyName.Applied || emptyName.FailReason != "name")
                throw new InvalidOperationException("빈 길드명은 실패해야 합니다.");
            var longName = GuildResolve.Create(new GuildRequest { Name = "abcdefghijklm", Gold = 25 });
            if (longName.Applied || longName.FailReason != "name")
                throw new InvalidOperationException("13자 길드명은 실패해야 합니다.");
            var poor = GuildResolve.Create(new GuildRequest { Name = "Ulons", Gold = 0 });
            if (poor.Applied || poor.FailReason != "gold")
                throw new InvalidOperationException("골드 부족 창설은 실패해야 합니다.");
            var ghost = GuildResolve.Create(new GuildRequest { Name = "Ulons", Gold = 25, Ghost = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령 창설은 실패해야 합니다.");
            var ok = GuildResolve.Create(new GuildRequest { Name = "Ulons", Gold = 25 });
            if (!ok.Applied)
                throw new InvalidOperationException("길드 창설 Resolve는 성공해야 합니다: " + ok.FailReason);

            var worldGo = new GameObject("selfcheck-guild-world");
            GameObject bodyGo = null;
            GameObject palGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-guild-body");
                bodyGo.transform.position = Vector3.zero;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.DisplayName = "길드장";
                body.Gold = GuildRules.GoldCost;
                body.ResetHp();

                body.Gold = 0;
                var noGold = world.TryGuildCreate(body, "Ulons");
                if (noGold.Applied || noGold.FailReason != "gold")
                    throw new InvalidOperationException("서버 골드 부족 창설 실패해야 합니다.");
                body.Gold = GuildRules.GoldCost;
                var badName = world.TryGuildCreate(body, "");
                if (badName.Applied || badName.FailReason != "name")
                    throw new InvalidOperationException("서버 빈 이름 창설은 실패해야 합니다.");
                var created = world.TryGuildCreate(body, "Ulons");
                if (!created.Applied)
                    throw new InvalidOperationException("서버 길드 창설 실패: " + created.FailReason);
                if (body.Gold != 0)
                    throw new InvalidOperationException("창설은 골드 25를 소모해야 합니다.");
                if (string.IsNullOrEmpty(body.GuildId) || body.GuildName != "Ulons")
                    throw new InvalidOperationException("창설 후 GuildId/GuildName이 있어야 합니다.");

                // Party distinct: party invite should still work while guilded
                palGo = new GameObject("selfcheck-guild-pal");
                palGo.transform.position = bodyGo.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.DisplayName = "동료";
                pal.IsEnemy = false;
                pal.MaxHp = 40f;
                pal.ResetHp();
                var partyInvite = world.TryPartyInvite(body, pal);
                if (!partyInvite.Applied)
                    throw new InvalidOperationException("길드와 파티는 별개여야 합니다: " + partyInvite.FailReason);
                if (world.ActiveParty == null)
                    throw new InvalidOperationException("파티가 유지되어야 합니다.");
                // accept party for avatar
                var partyAccept = world.TryPartyAccept(pal);
                if (!partyAccept.Applied)
                    throw new InvalidOperationException("파티 수락 실패: " + partyAccept.FailReason);

                var invited = world.TryGuildInvite(body, pal);
                if (!invited.Applied)
                    throw new InvalidOperationException("길드 초대 실패: " + invited.FailReason);
                var guild = world.GuildOf(body);
                if (guild == null || guild.Pending != pal)
                    throw new InvalidOperationException("길드 초대 Pending이 있어야 합니다.");
                if (!string.IsNullOrEmpty(pal.GuildId))
                    throw new InvalidOperationException("수락 전 동료 GuildId는 비어 있어야 합니다.");

                var accepted = world.TryGuildAccept(pal);
                if (!accepted.Applied)
                    throw new InvalidOperationException("길드 수락 실패: " + accepted.FailReason);
                if (pal.GuildId != body.GuildId || pal.GuildName != body.GuildName)
                    throw new InvalidOperationException("두 아바타 GuildId/GuildName이 같아야 합니다.");
                if (pal.GuildName != "Ulons")
                    throw new InvalidOperationException("동료 GuildName은 Ulons여야 합니다.");

                // leave member keeps leader guild
                var left = world.TryGuildLeave(pal);
                if (!left.Applied)
                    throw new InvalidOperationException("길드 탈퇴 실패: " + left.FailReason);
                if (!string.IsNullOrEmpty(pal.GuildId) || !string.IsNullOrEmpty(pal.GuildName))
                    throw new InvalidOperationException("탈퇴 후 동료 길드 필드는 비어야 합니다.");
                if (string.IsNullOrEmpty(body.GuildId) || body.GuildName != "Ulons")
                    throw new InvalidOperationException("멤버 탈퇴는 리더 길드를 유지해야 합니다.");
                // party still distinct
                if (world.ActiveParty == null || !world.ActiveParty.Contains(pal))
                    throw new InvalidOperationException("길드 탈퇴가 파티를 깨면 안 됩니다.");

                // re-invite and leader leave dissolves
                world.TryGuildInvite(body, pal);
                world.TryGuildAccept(pal);
                string gid = body.GuildId;
                var leaderLeft = world.TryGuildLeave(body);
                if (!leaderLeft.Applied)
                    throw new InvalidOperationException("리더 탈퇴 실패: " + leaderLeft.FailReason);
                if (!string.IsNullOrEmpty(body.GuildId) || !string.IsNullOrEmpty(pal.GuildId))
                    throw new InvalidOperationException("리더 탈퇴는 길드를 해산해야 합니다.");
                if (world.FindGuild(gid) != null)
                    throw new InvalidOperationException("해산된 길드는 없어야 합니다.");
                if (world.ActiveParty == null)
                    throw new InvalidOperationException("길드 해산이 파티를 깨면 안 됩니다.");
                world.TryPartyLeave(body);
            }
            finally
            {
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertGuildWar()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (!GuardZone.Contains(0f, 0f) || GuardZone.Contains(20f, 0f))
                throw new InvalidOperationException("가드존 반경이 마을과 안 맞습니다.");

            var noGuild = GuildWarResolve.Declare(new GuildWarRequest { HasGuild = false, IsLeader = true, HasTargetGuild = true });
            if (noGuild.Applied || noGuild.FailReason != "no_guild")
                throw new InvalidOperationException("길드 없는 선전포고는 실패해야 합니다.");
            var notLeader = GuildWarResolve.Declare(new GuildWarRequest { HasGuild = true, IsLeader = false, HasTargetGuild = true });
            if (notLeader.Applied || notLeader.FailReason != "not_leader")
                throw new InvalidOperationException("리더가 아닌 선전포고는 실패해야 합니다.");
            var noTarget = GuildWarResolve.Declare(new GuildWarRequest { HasGuild = true, IsLeader = true, HasTargetGuild = false });
            if (noTarget.Applied || noTarget.FailReason != "no_target")
                throw new InvalidOperationException("상대 길드 없는 선전포고는 실패해야 합니다.");
            var same = GuildWarResolve.Declare(new GuildWarRequest { HasGuild = true, IsLeader = true, HasTargetGuild = true, SameGuild = true });
            if (same.Applied || same.FailReason != "same_guild")
                throw new InvalidOperationException("같은 길드 선전포고는 실패해야 합니다.");
            var already = GuildWarResolve.Declare(new GuildWarRequest { HasGuild = true, IsLeader = true, HasTargetGuild = true, AlreadyWar = true });
            if (already.Applied || already.FailReason != "already")
                throw new InvalidOperationException("이미 전쟁 중 선전포고는 실패해야 합니다.");
            var ghost = GuildWarResolve.Declare(new GuildWarRequest { HasGuild = true, Ghost = true, HasTargetGuild = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령 선전포고는 실패해야 합니다.");
            var ok = GuildWarResolve.Declare(new GuildWarRequest { HasGuild = true, IsLeader = true, HasTargetGuild = true });
            if (!ok.Applied)
                throw new InvalidOperationException("선전포고 Resolve는 성공해야 합니다: " + ok.FailReason);
            var noWar = GuildWarResolve.Peace(new GuildWarRequest { HasGuild = true, IsLeader = true, AtWar = false });
            if (noWar.Applied || noWar.FailReason != "no_war")
                throw new InvalidOperationException("전쟁 없는 강화는 실패해야 합니다.");
            var peaceOk = GuildWarResolve.Peace(new GuildWarRequest { HasGuild = true, IsLeader = true, AtWar = true });
            if (!peaceOk.Applied)
                throw new InvalidOperationException("강화 Resolve는 성공해야 합니다: " + peaceOk.FailReason);
            if (GuildWarResolve.FieldWar(true, true, "g1", "g2", "g2", "g1", 0f, 0f, 0f, 0f))
                throw new InvalidOperationException("가드존 안 길드전은 FieldWar이 아니어야 합니다.");
            if (!GuildWarResolve.FieldWar(true, true, "g1", "g2", "g2", "g1", 20f, 0f, 20f, 0f))
                throw new InvalidOperationException("야외 길드전은 FieldWar이어야 합니다.");
            if (GuildWarResolve.FieldWar(true, true, "g1", "g2", "", "", 20f, 0f, 20f, 0f))
                throw new InvalidOperationException("전쟁 없는 야외는 FieldWar이 아니어야 합니다.");

            var worldGo = new GameObject("selfcheck-gwar-world");
            GameObject aGo = null;
            GameObject bGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                aGo = new GameObject("selfcheck-gwar-a");
                bGo = new GameObject("selfcheck-gwar-b");
                aGo.transform.position = new Vector3(20f, 0f, 0f);
                bGo.transform.position = new Vector3(20f, 0f, 0f);
                var a = aGo.AddComponent<WorldBody>();
                var b = bGo.AddComponent<WorldBody>();
                a.IsAvatar = true;
                b.IsAvatar = true;
                a.DisplayName = "길드장A";
                b.DisplayName = "길드장B";
                a.Gold = GuildRules.GoldCost;
                b.Gold = GuildRules.GoldCost;
                a.MaxHp = 50f;
                b.MaxHp = 50f;
                a.ResetHp();
                b.ResetHp();
                var createdA = world.TryGuildCreate(a, "Ulons");
                var createdB = world.TryGuildCreate(b, "Rivals");
                if (!createdA.Applied || !createdB.Applied)
                    throw new InvalidOperationException("길드전 창설 실패: " + createdA.FailReason + "/" + createdB.FailReason);
                if (a.GuildId == b.GuildId)
                    throw new InvalidOperationException("길드 A와 B는 달라야 합니다.");

                var declared = world.TryGuildWarDeclare(a, b);
                if (!declared.Applied)
                    throw new InvalidOperationException("선전포고 실패: " + declared.FailReason);
                if (!world.AtWar(a, b))
                    throw new InvalidOperationException("선전포고 후 AtWar여야 합니다.");

                if (GuardZone.Contains(aGo.transform.position.x, aGo.transform.position.z)
                    || GuardZone.Contains(bGo.transform.position.x, bGo.transform.position.z))
                    throw new InvalidOperationException("길드전 더미는 GuardZone 밖이어야 합니다.");
                float hp0 = b.Hp;
                int noto0 = a.Notoriety;
                var hit = world.TryAttack(a, b);
                if (!hit.Applied)
                    throw new InvalidOperationException("길드전 야외 공격은 적용되어야 합니다: " + hit.FailReason);
                if (b.Hp >= hp0)
                    throw new InvalidOperationException("길드전 야외 공격은 대상 HP가 줄어야 합니다.");
                if (a.Notoriety != NotorietyId.Innocent || a.Notoriety != noto0)
                    throw new InvalidOperationException("길드전 공격 후 노토라이어티는 무고여야 합니다.");

                aGo.transform.position = Vector3.zero;
                bGo.transform.position = Vector3.zero;
                float plazaHp = b.Hp;
                var blocked = world.TryAttack(a, b);
                if (blocked.Applied || blocked.FailReason != "innocent")
                    throw new InvalidOperationException("광장 길드전은 막혀야 합니다.");
                if (b.Hp != plazaHp)
                    throw new InvalidOperationException("광장 길드전은 피해가 들어가면 안 됩니다.");
                if (a.Notoriety != NotorietyId.Innocent)
                    throw new InvalidOperationException("광장 길드전 차단 후에도 무고여야 합니다.");

                var peaced = world.TryGuildWarPeace(a);
                if (!peaced.Applied)
                    throw new InvalidOperationException("강화 실패: " + peaced.FailReason);
                if (world.AtWar(a, b))
                    throw new InvalidOperationException("강화 후 AtWar가 아니어야 합니다.");

                aGo.transform.position = new Vector3(20f, 0f, 0f);
                bGo.transform.position = new Vector3(20f, 0f, 0f);
                if (b.Notoriety != NotorietyId.Innocent)
                    throw new InvalidOperationException("강화 직후 B는 무고여야 합니다.");
                var open = world.TryAttack(b, a);
                if (!open.Applied)
                    throw new InvalidOperationException("강화 후 야외는 Open PvP여야 합니다: " + open.FailReason);
                if (b.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("비길드전 야외 공격은 범죄여야 합니다.");
                if (a.Notoriety != NotorietyId.Innocent)
                    throw new InvalidOperationException("길드전 공격자 A는 강화 후에도 무고여야 합니다.");

                world.TryGuildLeave(a);
                world.TryGuildLeave(b);
            }
            finally
            {
                if (aGo != null)
                    UnityEngine.Object.DestroyImmediate(aGo);
                if (bGo != null)
                    UnityEngine.Object.DestroyImmediate(bGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertDuel()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (!GuardZone.Contains(0f, 0f) || GuardZone.Contains(20f, 0f))
                throw new InvalidOperationException("가드존 반경이 마을과 안 맞습니다.");

            var noTarget = DuelResolve.Invite(new DuelRequest { HasTarget = false });
            if (noTarget.Applied || noTarget.FailReason != "no_target")
                throw new InvalidOperationException("대상 없는 결투 초대는 실패해야 합니다.");
            var self = DuelResolve.Invite(new DuelRequest { SameAsSelf = true });
            if (self.Applied || self.FailReason != "no_target")
                throw new InvalidOperationException("자기 자신 결투 초대는 실패해야 합니다.");
            var enemy = DuelResolve.Invite(new DuelRequest { TargetEnemy = true, TargetAvatar = true });
            if (enemy.Applied || enemy.FailReason != "enemy")
                throw new InvalidOperationException("적 대상 결투 초대는 실패해야 합니다.");
            var notAv = DuelResolve.Invite(new DuelRequest { TargetAvatar = false });
            if (notAv.Applied || notAv.FailReason != "not_avatar")
                throw new InvalidOperationException("비아바타 결투 초대는 실패해야 합니다.");
            var busy = DuelResolve.Invite(new DuelRequest { AlreadyDueling = true });
            if (busy.Applied || busy.FailReason != "busy")
                throw new InvalidOperationException("이미 결투 중 초대는 실패해야 합니다.");
            var ghost = DuelResolve.Invite(new DuelRequest { Ghost = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령 결투 초대는 실패해야 합니다.");
            var far = DuelResolve.Invite(new DuelRequest { Distance = 99f, Range = DuelRules.InviteRange });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("거리 밖 결투 초대는 실패해야 합니다.");
            var okInvite = DuelResolve.Invite(new DuelRequest { Distance = 1f });
            if (!okInvite.Applied)
                throw new InvalidOperationException("결투 초대 Resolve는 성공해야 합니다: " + okInvite.FailReason);

            var noInvite = DuelResolve.Accept(new DuelRequest { HasPending = false, PendingIsMe = false });
            if (noInvite.Applied || noInvite.FailReason != "no_invite")
                throw new InvalidOperationException("초대 없는 수락은 실패해야 합니다.");
            var okAccept = DuelResolve.Accept(new DuelRequest { HasPending = true, PendingIsMe = true, Distance = 1f, Range = DuelRules.AcceptRange });
            if (!okAccept.Applied)
                throw new InvalidOperationException("결투 수락 Resolve는 성공해야 합니다: " + okAccept.FailReason);
            var noDuel = DuelResolve.End(new DuelRequest { InDuel = false });
            if (noDuel.Applied || noDuel.FailReason != "no_duel")
                throw new InvalidOperationException("결투 없는 종료는 실패해야 합니다.");
            var okEnd = DuelResolve.End(new DuelRequest { InDuel = true });
            if (!okEnd.Applied)
                throw new InvalidOperationException("결투 종료 Resolve는 성공해야 합니다: " + okEnd.FailReason);

            if (DuelResolve.FieldDuel(true, true, true, 0f, 0f, 0f, 0f))
                throw new InvalidOperationException("가드존 안 결투는 FieldDuel이 아니어야 합니다.");
            if (!DuelResolve.FieldDuel(true, true, true, 20f, 0f, 20f, 0f))
                throw new InvalidOperationException("야외 결투는 FieldDuel이어야 합니다.");
            if (DuelResolve.FieldDuel(true, true, false, 20f, 0f, 20f, 0f))
                throw new InvalidOperationException("미수락 야외는 FieldDuel이 아니어야 합니다.");

            var worldGo = new GameObject("selfcheck-duel-world");
            GameObject aGo = null;
            GameObject bGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                aGo = new GameObject("selfcheck-duel-a");
                bGo = new GameObject("selfcheck-duel-b");
                aGo.transform.position = new Vector3(20f, 0f, 0f);
                bGo.transform.position = new Vector3(20f, 0f, 0f);
                var a = aGo.AddComponent<WorldBody>();
                var b = bGo.AddComponent<WorldBody>();
                a.IsAvatar = true;
                b.IsAvatar = true;
                a.DisplayName = "결투A";
                b.DisplayName = "결투B";
                a.MaxHp = 50f;
                b.MaxHp = 50f;
                a.ResetHp();
                b.ResetHp();

                var invited = world.TryDuelInvite(a, b);
                if (!invited.Applied)
                    throw new InvalidOperationException("결투 초대 실패: " + invited.FailReason);
                if (a.PendingDuel != b)
                    throw new InvalidOperationException("초대 후 PendingDuel이 있어야 합니다.");
                if (world.AtDuel(a, b))
                    throw new InvalidOperationException("수락 전 AtDuel이면 안 됩니다.");

                var accepted = world.TryDuelAccept(b);
                if (!accepted.Applied)
                    throw new InvalidOperationException("결투 수락 실패: " + accepted.FailReason);
                if (!world.AtDuel(a, b))
                    throw new InvalidOperationException("수락 후 AtDuel이어야 합니다.");
                if (a.PendingDuel != null)
                    throw new InvalidOperationException("수락 후 PendingDuel은 비어야 합니다.");

                if (GuardZone.Contains(aGo.transform.position.x, aGo.transform.position.z)
                    || GuardZone.Contains(bGo.transform.position.x, bGo.transform.position.z))
                    throw new InvalidOperationException("결투 더미는 GuardZone 밖이어야 합니다.");
                float hp0 = b.Hp;
                int noto0 = a.Notoriety;
                var hit = world.TryAttack(a, b);
                if (!hit.Applied)
                    throw new InvalidOperationException("결투 야외 공격은 적용되어야 합니다: " + hit.FailReason);
                if (b.Hp >= hp0)
                    throw new InvalidOperationException("결투 야외 공격은 대상 HP가 줄어야 합니다.");
                if (a.Notoriety != NotorietyId.Innocent || a.Notoriety != noto0)
                    throw new InvalidOperationException("결투 공격 후 노토라이어티는 무고여야 합니다.");

                aGo.transform.position = Vector3.zero;
                bGo.transform.position = Vector3.zero;
                float plazaHp = b.Hp;
                var blocked = world.TryAttack(a, b);
                if (blocked.Applied || blocked.FailReason != "innocent")
                    throw new InvalidOperationException("광장 결투는 막혀야 합니다.");
                if (b.Hp != plazaHp)
                    throw new InvalidOperationException("광장 결투는 피해가 들어가면 안 됩니다.");
                if (a.Notoriety != NotorietyId.Innocent)
                    throw new InvalidOperationException("광장 결투 차단 후에도 무고여야 합니다.");

                aGo.transform.position = new Vector3(20f, 0f, 0f);
                bGo.transform.position = new Vector3(20f, 0f, 0f);
                var ended = world.TryDuelEnd(a);
                if (!ended.Applied)
                    throw new InvalidOperationException("결투 종료 실패: " + ended.FailReason);
                if (world.AtDuel(a, b))
                    throw new InvalidOperationException("종료 후 AtDuel이 아니어야 합니다.");

                // re-accept then yield
                world.TryDuelInvite(a, b);
                world.TryDuelAccept(b);
                if (!world.AtDuel(a, b))
                    throw new InvalidOperationException("재수락 후 AtDuel이어야 합니다.");
                var yielded = world.TryDuelYield(b);
                if (!yielded.Applied)
                    throw new InvalidOperationException("항복 실패: " + yielded.FailReason);
                if (world.AtDuel(a, b))
                    throw new InvalidOperationException("항복 후 AtDuel이 아니어야 합니다.");
                if (a.Notoriety != NotorietyId.Innocent || b.Notoriety != NotorietyId.Innocent)
                    throw new InvalidOperationException("항복 후에도 양쪽 무고여야 합니다.");

                // death ends duel, no Criminal / no murder from duel
                world.TryDuelInvite(a, b);
                world.TryDuelAccept(b);
                b.SetHp(1f);
                int murder0 = a.MurderCount;
                var nextAt = typeof(OfflineWorld).GetField("nextAttackAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nextAt != null)
                {
                    var map = nextAt.GetValue(world) as System.Collections.IDictionary;
                    if (map != null)
                        map.Remove(a.GetInstanceID());
                }
                var finish = world.TryAttack(a, b);
                if (!finish.Applied)
                    throw new InvalidOperationException("결투 마지막 타격은 적용되어야 합니다: " + finish.FailReason);
                if (world.AtDuel(a, b))
                    throw new InvalidOperationException("사망 후 결투는 끝나야 합니다.");
                if (a.Notoriety != NotorietyId.Innocent)
                    throw new InvalidOperationException("결투 킬 후 무고여야 합니다.");
                if (a.MurderCount != murder0)
                    throw new InvalidOperationException("결투 킬은 MurderCount를 올리면 안 됩니다.");

                // distinct from Open PvP after duel ends
                b.Ghost = false;
                b.ResetHp();
                a.ResetHp();
                if (b.Notoriety != NotorietyId.Innocent)
                    throw new InvalidOperationException("재설정 후 B는 무고여야 합니다.");
                var open = world.TryAttack(b, a);
                if (!open.Applied)
                    throw new InvalidOperationException("결투 종료 후 야외는 Open PvP여야 합니다: " + open.FailReason);
                if (b.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("비결투 야외 공격은 범죄여야 합니다.");
                if (a.Notoriety != NotorietyId.Innocent)
                    throw new InvalidOperationException("결투 공격자 A는 종료 후에도 무고여야 합니다.");
            }
            finally
            {
                if (aGo != null)
                    UnityEngine.Object.DestroyImmediate(aGo);
                if (bGo != null)
                    UnityEngine.Object.DestroyImmediate(bGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertExceptional()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");

            ExceptionalCraft.Force = true;
            ExceptionalCraft.Seed = 0;
            try
            {
                if (!ExceptionalCraft.Roll(0f))
                    throw new InvalidOperationException("Force면 숙련 0에서도 Exceptional이어야 합니다.");
            }
            finally
            {
                ExceptionalCraft.Force = false;
            }

            ExceptionalCraft.Seed = 7;
            try
            {
                if (!ExceptionalCraft.Roll(100f))
                    throw new InvalidOperationException("고숙련+seed 롤은 Exceptional이어야 합니다.");
            }
            finally
            {
                ExceptionalCraft.Seed = 0;
            }

            var weak = new StatSet();
            weak.ForceSet(20, 25, 25);
            var normal = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 2f, Skills = new SkillSet(), Stats = weak, TargetAlive = true });
            var boosted = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 2f, Skills = new SkillSet(), Stats = weak, TargetAlive = true, Exceptional = true });
            if (boosted.Damage != normal.Damage + ExceptionalCraft.DamageBonus)
                throw new InvalidOperationException("Exceptional 피해 보너스가 있어야 합니다.");

            var snap = new CharacterSnapshot
            {
                AccountId = "selfcheck-ex",
                CharacterId = "selfcheck-ex",
                Name = "예외",
                Inventory = new[]
                {
                    new ItemRecord { Slot = 0, TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 44, MakerId = "crafter-a", Exceptional = true }
                }
            };
            CharacterStore.Save(snap);
            var loaded = CharacterStore.Load("selfcheck-ex");
            if (loaded == null || loaded.Inventory.Length != 1
                || !loaded.Inventory[0].Exceptional
                || loaded.Inventory[0].MakerId != "crafter-a"
                || loaded.Inventory[0].Uses != 44)
                throw new InvalidOperationException("persist Exceptional/MakerId 왕복 실패");
            if (loaded.Inventory[0].MakerId.StartsWith(ExceptionalCraft.PersistPrefix))
                throw new InvalidOperationException("로드된 MakerId에 persist prefix가 보이면 안 됩니다.");

            var go = new GameObject("selfcheck-ex");
            GameObject worldGo = null;
            GameObject forgeGo = null;
            ExceptionalCraft.Force = true;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-ex-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "ex-smith";
                body.RecalcFromStr(30);
                world.SkillsOf(body).ForceSet(SkillId.Blacksmithing, 100f, SkillLock.Up);
                var bag = go.AddComponent<InventoryBag>();
                forgeGo = new GameObject("Forge");
                forgeGo.transform.position = go.transform.position;
                var forge = forgeGo.AddComponent<CraftStation>();
                bag.Add("iron_ore", 2);
                var forged = world.TryCraft(body, forge);
                if (!forged.Applied)
                    throw new InvalidOperationException("Exceptional 제작 실패: " + forged.FailReason);
                ItemRecord sword = default;
                bool found = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    sword = bag.Items[i];
                    found = true;
                    break;
                }
                if (!found)
                    throw new InvalidOperationException("Exceptional 철검이 가방에 있어야 합니다.");
                if (!sword.Exceptional)
                    throw new InvalidOperationException("Force/고숙련 제작은 Exceptional 플래그가 있어야 합니다.");
                if (sword.MakerId != "ex-smith")
                    throw new InvalidOperationException("Exceptional MakerId는 제작자 id여야 하고 prefix가 아니어야 합니다.");
                if (sword.Uses != ItemCatalog.MaxUsesOf(ItemCatalog.IronSword) + ExceptionalCraft.UsesBonus)
                    throw new InvalidOperationException("Exceptional 내구 보너스가 있어야 합니다.");
            }
            finally
            {
                ExceptionalCraft.Force = false;
                ExceptionalCraft.Seed = 0;
                UnityEngine.Object.DestroyImmediate(go);
                if (forgeGo != null)
                    UnityEngine.Object.DestroyImmediate(forgeGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertOpenPvpSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (!GuardZone.Contains(0f, 0f) || GuardZone.Contains(20f, 0f))
                throw new InvalidOperationException("가드존 반경이 마을과 안 맞습니다.");
            if (PvpResolve.MurdererThreshold != 5)
                throw new InvalidOperationException("살인자 기준은 기획 Murder Count 5입니다.");

            var worldGo = new GameObject("selfcheck-pvp-world");
            GameObject aGo = null;
            GameObject bGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                aGo = new GameObject("selfcheck-pvp-a");
                bGo = new GameObject("selfcheck-pvp-b");
                aGo.transform.position = new Vector3(20f, 0f, 0f);
                bGo.transform.position = new Vector3(20f, 0f, 0f);
                var a = aGo.AddComponent<WorldBody>();
                var b = bGo.AddComponent<WorldBody>();
                a.IsAvatar = true;
                b.IsAvatar = true;
                a.MaxHp = 50f;
                b.MaxHp = 50f;
                a.ResetHp();
                b.ResetHp();
                if (GuardZone.Contains(aGo.transform.position.x, aGo.transform.position.z)
                    || GuardZone.Contains(bGo.transform.position.x, bGo.transform.position.z))
                    throw new InvalidOperationException("Open PvP 더미는 GuardZone 밖이어야 합니다.");
                float hp0 = b.Hp;
                var hit = world.TryAttack(a, b);
                if (!hit.Applied)
                    throw new InvalidOperationException("야외 Open PvP는 적용되어야 합니다: " + hit.FailReason);
                if (b.Hp >= hp0)
                    throw new InvalidOperationException("야외 Open PvP는 대상 HP가 줄어야 합니다.");
                if (a.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("야외 무고 공격 후 범죄가 되어야 합니다.");

                aGo.transform.position = Vector3.zero;
                bGo.transform.position = Vector3.zero;
                float plazaHp = b.Hp;
                var blocked = world.TryAttack(a, b);
                if (blocked.Applied || blocked.FailReason != "innocent")
                    throw new InvalidOperationException("광장 Open PvP는 막혀야 합니다.");
                if (b.Hp != plazaHp)
                    throw new InvalidOperationException("광장 무고 공격은 피해가 들어가면 안 됩니다.");
            }
            finally
            {
                if (aGo != null)
                    UnityEngine.Object.DestroyImmediate(aGo);
                if (bGo != null)
                    UnityEngine.Object.DestroyImmediate(bGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertSkillTitleSlice()
        {
            if (SkillTitles.JobOf(SkillId.Swordsmanship) != "검사" || SkillTitles.JobOf(SkillId.Archery) != "궁수"
                || SkillTitles.JobOf(SkillId.Magery) != "마법사" || SkillTitles.JobOf(SkillId.Healing) != "치료사"
                || SkillTitles.JobOf(SkillId.Mining) != "광부" || SkillTitles.JobOf(SkillId.Blacksmithing) != "대장장이")
                throw new InvalidOperationException("대표 스킬 직업명이 기획서와 같아야 합니다.");
            if (SkillTitles.RankOf(0f) != "" || SkillTitles.RankOf(29.9f) != "" || SkillTitles.RankOf(30f) != "초심자"
                || SkillTitles.RankOf(40f) != "수습" || SkillTitles.RankOf(50f) != "견습" || SkillTitles.RankOf(60f) != "숙련"
                || SkillTitles.RankOf(70f) != "전문가" || SkillTitles.RankOf(80f) != "달인" || SkillTitles.RankOf(90f) != "대가"
                || SkillTitles.RankOf(100f) != "그랜드마스터")
                throw new InvalidOperationException("숙련 칭호 구간이 30/40/50/60/70/80/90/100이어야 합니다.");

            var empty = new SkillSet();
            if (SkillTitles.Of(empty) != "")
                throw new InvalidOperationException("스킬 0은 직업명이 없어야 합니다.");

            var low = new SkillSet();
            SkillGain.TryRaise(low, SkillId.Swordsmanship, 20f, out _, out _);
            if (SkillTitles.Of(low) != "검사")
                throw new InvalidOperationException("검술 0.1은 칭호 없이 검사여야 합니다.");

            var mid = new SkillSet();
            mid.ForceSet(SkillId.Swordsmanship, 60f, SkillLock.Up);
            if (SkillTitles.Of(mid) != "숙련 검사")
                throw new InvalidOperationException("검술 60은 숙련 검사여야 합니다.");
            mid.ForceSet(SkillId.Swordsmanship, 80f, SkillLock.Up);
            if (SkillTitles.Of(mid) != "달인 검사")
                throw new InvalidOperationException("검술 80은 달인 검사여야 합니다.");
            mid.ForceSet(SkillId.Swordsmanship, 100f, SkillLock.Up);
            if (SkillTitles.Of(mid) != "그랜드마스터 검사")
                throw new InvalidOperationException("검술 100은 그랜드마스터 검사여야 합니다.");

            var mine = new SkillSet();
            mine.ForceSet(SkillId.Mining, 50f, SkillLock.Up);
            mine.ForceSet(SkillId.Swordsmanship, 40f, SkillLock.Up);
            if (SkillTitles.Of(mine) != "견습 광부")
                throw new InvalidOperationException("최고 스킬이 대표 직업이어야 합니다.");

            var tie = new SkillSet();
            tie.ForceSet(SkillId.Swordsmanship, 50f, SkillLock.Up);
            tie.ForceSet(SkillId.Mining, 50f, SkillLock.Up);
            if (SkillTitles.Of(tie) != "견습 검사")
                throw new InvalidOperationException("동점이면 목록 앞 스킬이 대표여야 합니다.");

            var worldGo = new GameObject("selfcheck-title-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-title-body");
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                if (world.TitleOf(body) != "")
                    throw new InvalidOperationException("서버 스킬 0은 직업명이 없어야 합니다.");
                world.SkillsOf(body).ForceSet(SkillId.Archery, 70f, SkillLock.Up);
                if (world.TitleOf(body) != "전문가 궁수")
                    throw new InvalidOperationException("직업명은 서버 SkillSet에서 계산해야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertReputationTitle()
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
            if (GameObject.Find("DressVillage") != null)
                throw new InvalidOperationException("DressVillage 오브젝트가 있으면 안 됩니다.");

            if (ReputationTitles.FameFamous != 100)
                throw new InvalidOperationException("유명인 Fame 임계값은 100이어야 합니다.");
            if (ReputationTitles.Of(NotorietyId.Murderer, 0) != "살인자")
                throw new InvalidOperationException("Murderer는 살인자여야 합니다.");
            if (ReputationTitles.Of(NotorietyId.Criminal, 0) != "범죄자")
                throw new InvalidOperationException("Criminal은 범죄자여야 합니다.");
            if (ReputationTitles.Of(NotorietyId.Innocent, ReputationTitles.FameFamous) != "유명인")
                throw new InvalidOperationException("Fame≥임계는 유명인이어야 합니다.");
            if (ReputationTitles.Of(NotorietyId.Innocent, ReputationTitles.FameFamous - 1) != "")
                throw new InvalidOperationException("낮은 Fame은 칭호가 비어 있어야 합니다.");
            // Murderer beats fame/criminal
            if (ReputationTitles.Of(NotorietyId.Murderer, 999) != "살인자")
                throw new InvalidOperationException("Murderer가 Fame보다 우선이어야 합니다.");
            if (ReputationTitles.Of(NotorietyId.Criminal, 999) != "범죄자")
                throw new InvalidOperationException("Criminal이 Fame보다 우선이어야 합니다.");
            // Skill title still independent
            if (SkillTitles.Of(new SkillSet()) != "")
                throw new InvalidOperationException("Reputation은 SkillTitles를 깨면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-rep-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-rep-body");
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.DisplayName = "평판";
                body.Notoriety = NotorietyId.Innocent;
                body.Fame = 0;
                body.Karma = 0;
                if (world.ReputationTitleOf(body) != "")
                    throw new InvalidOperationException("서버 Innocent/저Fame은 평판 칭호가 없어야 합니다.");

                body.Notoriety = NotorietyId.Murderer;
                if (world.ReputationTitleOf(body) != "살인자")
                    throw new InvalidOperationException("Force Murderer → 살인자여야 합니다.");

                body.Notoriety = NotorietyId.Criminal;
                if (world.ReputationTitleOf(body) != "범죄자")
                    throw new InvalidOperationException("Force Criminal → 범죄자여야 합니다.");

                body.Notoriety = NotorietyId.Innocent;
                body.Fame = ReputationTitles.FameFamous;
                if (world.ReputationTitleOf(body) != "유명인")
                    throw new InvalidOperationException("Force Fame → 유명인이어야 합니다.");

                // Skill job title still works beside reputation
                world.SkillsOf(body).ForceSet(SkillId.Archery, 70f, SkillLock.Up);
                if (world.TitleOf(body) != "전문가 궁수")
                    throw new InvalidOperationException("평판 슬라이스는 SkillTitles를 깨면 안 됩니다.");
                if (world.ReputationTitleOf(body) != "유명인")
                    throw new InvalidOperationException("SkillTitles와 Reputation은 동시여야 합니다.");

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("Reputation 슬라이스 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertKeywordSpeech()
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
            if (GameObject.Find("DressVillage") != null)
                throw new InvalidOperationException("DressVillage 오브젝트가 있으면 안 됩니다.");
            if (GameObject.Find("Banker") == null)
                throw new InvalidOperationException("Banker가 있어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-keyword-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-keyword-body");
                bodyGo.transform.position = new Vector3(0f, 0.1f, 0f);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.DisplayName = "말";
                body.MaxHp = 50f;
                body.ResetHp();
                body.Notoriety = NotorietyId.Innocent;
                var bag = bodyGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Cloth, 1);

                var bankHit = world.TrySpeechKeyword(body, "bank");
                if (!bankHit.Applied)
                    throw new InvalidOperationException("bank 키워드는 Applied여야 합니다: " + bankHit.FailReason);
                if (world.LastSpeechMessage != "은행")
                    throw new InvalidOperationException("bank LastSpeechMessage는 은행이어야 합니다.");
                var vault = body.GetComponent<BankVault>();
                if (vault == null || vault.Items.Count < 1)
                    throw new InvalidOperationException("bank 키워드는 기존 TryBank 입금 경로여야 합니다.");

                var bankKo = world.TrySpeechKeyword(body, "은행");
                if (!bankKo.Applied || world.LastSpeechMessage != "은행")
                    throw new InvalidOperationException("은행 키워드도 bank 경로여야 합니다.");

                body.Notoriety = NotorietyId.Criminal;
                body.CriminalUntil = Time.time + 120f;
                body.ResetHp();
                bodyGo.transform.position = new Vector3(0f, 0.1f, 0f);
                if (!GuardZone.Contains(bodyGo.transform.position.x, bodyGo.transform.position.z))
                    throw new InvalidOperationException("경비 assert 더미는 GuardZone 안이어야 합니다.");
                float hpBefore = body.Hp;
                var guardHit = world.TrySpeechKeyword(body, "guards");
                if (!guardHit.Applied || !guardHit.Hit)
                    throw new InvalidOperationException("범죄자+가드존 guards는 GuardStrike여야 합니다.");
                if (body.Hp >= hpBefore)
                    throw new InvalidOperationException("guards는 HP를 깎아야 합니다.");
                if (world.LastSpeechMessage != "경비")
                    throw new InvalidOperationException("guards LastSpeechMessage는 경비여야 합니다.");

                body.Notoriety = NotorietyId.Innocent;
                body.ResetHp();
                var flavor = world.TrySpeechKeyword(body, "경비");
                if (!flavor.Applied || world.LastSpeechMessage != "경비가 순찰 중이다.")
                    throw new InvalidOperationException("무고 경비 키워드는 분위기 메시지여야 합니다.");

                world.CloseVendor();
                var vendorHit = world.TrySpeechKeyword(body, "vendor");
                if (!vendorHit.Applied)
                    throw new InvalidOperationException("vendor 키워드는 Applied여야 합니다: " + vendorHit.FailReason);
                if (world.ActiveVendor == null)
                    throw new InvalidOperationException("vendor 키워드는 ActiveVendor를 열어야 합니다.");
                if (world.LastSpeechMessage != "상점")
                    throw new InvalidOperationException("vendor LastSpeechMessage는 상점이어야 합니다.");
                world.CloseVendor();

                var shopKo = world.TrySpeechKeyword(body, "상점");
                if (!shopKo.Applied || world.ActiveVendor == null || world.LastSpeechMessage != "상점")
                    throw new InvalidOperationException("상점 키워드도 vendor 경로여야 합니다.");
                world.CloseVendor();

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("Keyword Speech 슬라이스 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.CloseVendor();
                OfflineWorld.Instance?.ResetHousePlot();
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


    }
}
