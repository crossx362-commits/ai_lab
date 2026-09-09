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
        // **여기 남은 것은 쏘고 쫓는 것**(랩 ㉱) — 전격탄·추적.
        // 멀리 있는 것을 맞히고 찾아가는 기술만 남겼다. 정신 쪽은 `MindSkills`, 소리 쪽은 `Bardic`.
        static void AssertBolt()
        {
            AssertDungeon3Leftover();
            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Bolt) != "벼락")
                throw new InvalidOperationException("SpellId.Bolt 한글은 벼락이어야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Magery) != StatId.Int)
                throw new InvalidOperationException("마법 Primary는 INT이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Bolt) <= SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("벼락 마나는 불씨보다 커야 합니다.");
            if (SpellCast.RangeOf(SpellId.Bolt) <= SpellCast.RangeOf(SpellId.Ember))
                throw new InvalidOperationException("벼락 사거리는 불씨보다 길어야 합니다.");
            if (SpellCast.RangeOf(SpellId.Ember) != SpellCast.EmberRange || SpellCast.EmberRange != 8f)
                throw new InvalidOperationException("불씨 사거리는 8이어야 합니다.");
            if (SpellCast.BoltRange != 12f)
                throw new InvalidOperationException("벼락 사거리는 12이어야 합니다.");

            var hi = new StatSet();
            hi.ForceSet(20, 20, 40);
            var lo = new StatSet();
            lo.ForceSet(20, 20, 10);
            int emberDmg = SpellCast.EmberDamage(hi, new SkillSet());
            int boltDmg = SpellCast.BoltDamage(hi, new SkillSet());
            if (boltDmg <= emberDmg)
                throw new InvalidOperationException("벼락 피해는 불씨보다 커야 합니다.");
            if (SpellCast.BoltDamage(hi, new SkillSet()) <= SpellCast.BoltDamage(lo, new SkillSet()))
                throw new InvalidOperationException("벼락 피해는 INT에 비례해야 합니다.");

            var go = new GameObject("selfcheck-bolt");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject palGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-bolt-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.IsEnemy = false;
                body.MaxHp = 50f;
                body.ResetHp();
                world.StatsOf(body).ForceSet(20, 20, 40);
                body.RecalcFromInt(40);
                body.SetMana(body.MaxMana);
                var bag = go.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(body, SpellId.Bolt, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 벼락은 실패해야 합니다.");

                world.BookOf(body).Learn(SpellId.Ember);
                world.BookOf(body).Learn(SpellId.Bolt);

                tgtGo = new GameObject("selfcheck-bolt-tgt");
                tgtGo.transform.position = go.transform.position + new Vector3(10f, 0f, 0f);
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 80f;
                tgt.ResetHp();

                var noTgt = world.TryCast(body, SpellId.Bolt, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 벼락은 실패해야 합니다.");

                palGo = new GameObject("selfcheck-bolt-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsEnemy = false;
                pal.MaxHp = 40f;
                pal.ResetHp();
                var same = world.TryCast(body, SpellId.Bolt, pal);
                if (same.Applied)
                    throw new InvalidOperationException("같은 편 벼락은 실패해야 합니다.");

                var emberFar = world.TryCast(body, SpellId.Ember, tgt);
                if (emberFar.Applied || emberFar.FailReason != "range")
                    throw new InvalidOperationException("10유닛은 불씨 사거리 밖이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("실패한 불씨는 마법을 올리면 안 됩니다.");

                float hp0 = tgt.Hp;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;
                var bolt = world.TryCast(body, SpellId.Bolt, tgt);
                if (!bolt.Applied)
                    throw new InvalidOperationException("10유닛 벼락 시전 시작 실패: " + bolt.FailReason);
                if (!body.IsCasting(Time.time))
                    throw new InvalidOperationException("벼락은 CastingUntil 풍업이어야 합니다.");
                if (tgt.Hp < hp0)
                    throw new InvalidOperationException("풍업 중 벼락 효과가 나가면 안 됩니다.");
                world.TickCast(Time.time + SpellCast.BoltCastSeconds);
                if (body.IsCasting(Time.time + SpellCast.BoltCastSeconds))
                    throw new InvalidOperationException("풍업 후 시전이 남아 있으면 안 됩니다.");
                if (tgt.Hp >= hp0)
                    throw new InvalidOperationException("10유닛 벼락은 맞아야 합니다: " + bolt.FailReason);
                int boltDealt = (int)(hp0 - tgt.Hp);
                if (boltDealt <= emberDmg)
                    throw new InvalidOperationException("서버 벼락 피해는 불씨보다 커야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("벼락은 시약 1을 써야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("벼락 후 마법이 0.1이어야 합니다.");

                tgtGo.transform.position = go.transform.position + new Vector3(13f, 0f, 0f);
                var tooFar = world.TryCast(body, SpellId.Bolt, tgt);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("13유닛은 벼락 사거리 밖이어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertTrackingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Tracking) != StatId.Dex)
                throw new InvalidOperationException("추적 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Tracking) != "추적" || SkillTitles.JobOf(SkillId.Tracking) != "추적자")
                throw new InvalidOperationException("추적 스킬명/직업명이 기획과 같아야 합니다.");

            var noneSkills = new SkillSet();
            var none = TrackingResolve.Resolve(new TrackingRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasTarget = false
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 추적은 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("실패한 추적은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적"
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 추적은 들어가면 안 됩니다.");
            if (Math.Abs(farSkills.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("실패한 추적은 스킬을 올리면 안 됩니다.");

            var deadSkills = new SkillSet();
            var dead = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = deadSkills,
                HasTarget = true,
                IsCorpse = false,
                TargetAlive = false,
                TargetKind = "도적"
            });
            if (dead.Applied)
                throw new InvalidOperationException("살아 있지 않은 몹 추적은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적",
                Hp = 45f,
                MaxHp = 45f,
                Difficulty = TrackingResolve.Difficulty
            });
            if (!ok.Applied || ok.Kind != "도적" || Math.Abs(ok.Hp - 45f) > 0.0001f || Math.Abs(ok.MaxHp - 45f) > 0.0001f)
                throw new InvalidOperationException("추적은 몹 종류/HP를 밝혀야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Tracking) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 추적 후 0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("추적 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("추적은 INT를 올리면 안 됩니다.");

            var corpseSkills = new SkillSet();
            var corpse = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = corpseSkills,
                HasTarget = true,
                IsCorpse = true,
                TargetAlive = false,
                TargetKind = "스켈레톤",
                LastX = 3.5f,
                LastZ = 7.25f
            });
            if (!corpse.Applied || !corpse.IsCorpse)
                throw new InvalidOperationException("시체 추적은 성공해야 합니다.");
            if (string.IsNullOrEmpty(corpse.LastPosition) || corpse.LastPosition.IndexOf("x=3.5", StringComparison.Ordinal) < 0
                || corpse.LastPosition.IndexOf("z=7.3", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("시체 추적은 마지막 위치 문자열을 줘야 합니다: " + corpse.LastPosition);
            if (Math.Abs(corpseSkills.Get(SkillId.Tracking) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("시체 추적 후 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Tracking, SkillLock.Locked);
            var lockedOk = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적",
                Hp = 12f,
                MaxHp = 45f
            });
            if (!lockedOk.Applied || lockedOk.Kind != "도적")
                throw new InvalidOperationException("잠긴 추적도 정보는 보여야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("잠긴 추적은 오르면 안 됩니다.");

            var melee = new SkillSet();
            var phys = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1.2f,
                Now = 1f,
                Skills = melee,
                TargetAlive = true
            });
            if (!phys.Applied)
                throw new InvalidOperationException("물리 공격 대조 실패");
            if (Math.Abs(melee.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("물리 공격은 추적을 올리면 안 됩니다.");

            var go = new GameObject("selfcheck-track");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject corpseGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-track-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;

                var missing = world.TryTrack(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 추적은 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tracking)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 추적은 스킬을 올리면 안 됩니다.");

                tgtGo = new GameObject("selfcheck-track-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();
                tgt.SetHp(33f);

                var hit = world.TryTrack(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 추적 실패: " + hit.FailReason);
                if (hit.Kind != "도적" || Math.Abs(hit.Hp - 33f) > 0.0001f)
                    throw new InvalidOperationException("서버 추적은 종류/HP를 밝혀야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tracking) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 추적 후 서버 스킬 0.1이어야 합니다.");
                if (string.IsNullOrEmpty(world.LastTrackMessage) || world.LastTrackMessage.IndexOf("도적", StringComparison.Ordinal) < 0
                    || world.LastTrackMessage.IndexOf("HP", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("추적 메시지가 종류/HP를 포함해야 합니다.");

                corpseGo = new GameObject("selfcheck-track-corpse");
                corpseGo.transform.position = go.transform.position;
                var node = corpseGo.AddComponent<CorpseNode>();
                node.LastKind = "스켈레톤";
                node.LastX = 4f;
                node.LastZ = -2.5f;
                var scoutGo = new GameObject("selfcheck-track-scout");
                scoutGo.transform.position = go.transform.position;
                var scout = scoutGo.AddComponent<WorldBody>();
                scout.IsAvatar = true;
                var corpseHit = world.TryTrackCorpse(scout, node);
                if (!corpseHit.Applied)
                    throw new InvalidOperationException("서버 시체 추적 실패: " + corpseHit.FailReason);
                if (string.IsNullOrEmpty(corpseHit.LastPosition) || corpseHit.LastPosition.IndexOf("x=4.0", StringComparison.Ordinal) < 0
                    || corpseHit.LastPosition.IndexOf("z=-2.5", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("서버 시체 추적은 마지막 위치 문자열이어야 합니다: " + corpseHit.LastPosition);
                if (string.IsNullOrEmpty(world.LastTrackMessage) || world.LastTrackMessage.IndexOf("마지막", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("시체 추적 메시지가 마지막 위치를 포함해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                var leftoverScout = GameObject.Find("selfcheck-track-scout");
                if (leftoverScout != null)
                    UnityEngine.Object.DestroyImmediate(leftoverScout);
                if (corpseGo != null)
                    UnityEngine.Object.DestroyImmediate(corpseGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }



    }
}
