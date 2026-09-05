using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public static DungeonGate FindGate(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<DungeonGate>() : null;
        }

        public AttackResult TryDungeon(WorldBody body, DungeonGate gate)
        {
            if (body == null || gate == null)
                return new AttackResult { FailReason = "no_gate" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, gate.transform.position);
            if (dist > gate.InteractRange)
                return new AttackResult { FailReason = "range" };
            if (gate.DungeonId == Dungeon1.Id)
            {
                if (gate.IsExit)
                    WarpBody(body, Dungeon1.LeaveX, Dungeon1.LeaveZ);
                else
                    WarpBody(body, Dungeon1.InteriorX, Dungeon1.InteriorZ);
                return new AttackResult { Applied = true };
            }
            if (gate.DungeonId == Dungeon2.Id)
            {
                if (gate.IsExit)
                    WarpBody(body, Dungeon2.LeaveX, Dungeon2.LeaveZ);
                else
                    WarpBody(body, Dungeon2.InteriorX, Dungeon2.InteriorZ);
                return new AttackResult { Applied = true };
            }
            if (gate.DungeonId == Dungeon3.Id)
            {
                if (gate.IsExit)
                    WarpBody(body, Dungeon3.LeaveX, Dungeon3.LeaveZ);
                else
                    WarpBody(body, Dungeon3.InteriorX, Dungeon3.InteriorZ);
                return new AttackResult { Applied = true };
            }
            return new AttackResult { FailReason = "unknown_dungeon" };
        }

        public static Moongate FindMoongate(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<Moongate>() : null;
        }

        public AttackResult TryGate(WorldBody body, Moongate gate)
        {
            if (body == null || gate == null)
                return new AttackResult { FailReason = "no_gate" };
            float dist = Vector3.Distance(body.transform.position, gate.transform.position);
            var result = TravelResolve.Gate(new TravelRequest
            {
                Distance = dist,
                Range = gate.InteractRange,
                Ghost = body.Ghost,
                Gold = body.Gold
            });
            if (!result.Applied)
                return result;
            body.Gold -= TravelGate.GoldCost;
            WarpBody(body, TravelGate.PlazaX, TravelGate.PlazaZ);
            return result;
        }

        public AttackResult TryMark(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var result = TravelResolve.Mark(new TravelRequest
            {
                Ghost = body.Ghost,
                InCombat = body.InCombat(Time.time),
                Gold = body.Gold,
                GoldCost = TravelMark.GoldCost
            });
            if (!result.Applied)
            {
                LastTravelMessage = result.FailReason;
                return result;
            }
            body.Gold -= TravelMark.GoldCost;
            body.HasMark = true;
            body.MarkX = body.transform.position.x;
            body.MarkZ = body.transform.position.z;
            LastTravelMessage = "기록";
            return result;
        }

        public AttackResult TryRecall(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var result = TravelResolve.Recall(new TravelRequest
            {
                Ghost = body.Ghost,
                InCombat = body.InCombat(Time.time),
                HasMark = body.HasMark
            });
            if (!result.Applied)
            {
                LastTravelMessage = result.FailReason;
                return result;
            }
            WarpBody(body, body.MarkX, body.MarkZ);
            LastTravelMessage = "귀환";
            return result;
        }

        static void WarpBody(WorldBody body, float x, float z)
        {
            var cc = body.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            body.transform.position = new Vector3(x, 0.1f, z);
            if (cc != null)
                cc.enabled = true;
        }

        static void EnsureDungeon1Runtime()
        {
            var mob = GameObject.Find(Dungeon1.MobObject);
            if (mob != null)
            {
                var body = mob.GetComponent<WorldBody>();
                if (body != null)
                {
                    body.MobId = MobCatalog.Skeleton;
                    body.IsEnemy = true;
                    body.ApplyMobCatalog();
                }
            }
            var boss = GameObject.Find(Dungeon1.BossObject);
            if (boss == null)
                return;
            var bossBody = boss.GetComponent<WorldBody>();
            if (bossBody == null)
                return;
            bossBody.MobId = MobCatalog.BoneWarden;
            bossBody.IsEnemy = true;
            bossBody.ApplyMobCatalog();
        }

        static void EnsureDungeon2Runtime()
        {
            var mob = GameObject.Find(Dungeon2.MobObject);
            if (mob != null)
            {
                var body = mob.GetComponent<WorldBody>();
                if (body != null)
                {
                    body.MobId = MobCatalog.Bandit;
                    body.IsEnemy = true;
                    body.ApplyMobCatalog();
                }
            }
            var boss = GameObject.Find(Dungeon2.BossObject);
            if (boss == null)
                return;
            var bossBody = boss.GetComponent<WorldBody>();
            if (bossBody == null)
                return;
            bossBody.MobId = MobCatalog.ShadowCaptain;
            bossBody.IsEnemy = true;
            bossBody.ApplyMobCatalog();
        }

        static void EnsureDungeon3Runtime()
        {
            var mob = GameObject.Find(Dungeon3.MobObject);
            if (mob == null)
                return;
            var body = mob.GetComponent<WorldBody>();
            if (body == null)
                return;
            body.MobId = MobCatalog.Raider;
            body.IsEnemy = true;
            body.ApplyMobCatalog();
            var boss = GameObject.Find(Dungeon3.BossObject);
            if (boss == null)
                return;
            var bossBody = boss.GetComponent<WorldBody>();
            if (bossBody == null)
                return;
            bossBody.MobId = MobCatalog.IronTyrant;
            bossBody.IsEnemy = true;
            bossBody.ApplyMobCatalog();
        }

        static void EnsureFieldBossRuntime()
        {
            var boss = GameObject.Find(FieldBoss.Object);
            if (boss == null)
                return;
            var bossBody = boss.GetComponent<WorldBody>();
            if (bossBody == null)
                return;
            bossBody.MobId = MobCatalog.Hexarch;
            bossBody.IsEnemy = true;
            bossBody.ApplyMobCatalog();
        }

        public AttackResult GmWarpPlaza(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            WarpBody(body, 0f, 0f);
            OpLog.Write("gm", PersistDriver.AccountKey(), body.name, "warp_plaza");
            return new AttackResult { Applied = true };
        }

        public AttackResult GmGive(WorldBody body, string template, int amount)
        {
            if (body == null || string.IsNullOrEmpty(template))
                return new AttackResult { FailReason = "no_body" };
            Bag(body).Add(template, amount < 1 ? 1 : amount);
            OpLog.Write("gm", PersistDriver.AccountKey(), template, "give " + amount);
            return new AttackResult { Applied = true };
        }

        public AttackResult GmTake(WorldBody body, string template)
        {
            if (body == null || string.IsNullOrEmpty(template))
                return new AttackResult { FailReason = "no_body" };
            if (!Bag(body).TakeOne(template))
                return new AttackResult { FailReason = "missing" };
            OpLog.Write("gm", PersistDriver.AccountKey(), template, "take");
            return new AttackResult { Applied = true };
        }

        public AttackResult GmSetSkill(WorldBody body, SkillId skill, float value)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (!SkillsOf(body).TrySet(skill, value))
                return new AttackResult { FailReason = "lock" };
            OpLog.Write("gm", PersistDriver.AccountKey(), skill.ToString(), "set " + value.ToString("0.0"));
            return new AttackResult { Applied = true };
        }

        public AttackResult GmSpawnSkeleton()
        {
            var src = GameObject.Find("Skeleton");
            if (src == null)
                return new AttackResult { FailReason = "no_template" };
            var go = UnityEngine.Object.Instantiate(src);
            go.name = "Skeleton_gm";
            go.SetActive(true);
            go.transform.position = new Vector3(2.2f, 0.1f, 12.4f);
            var nob = go.GetComponent<FishNet.Object.NetworkObject>();
            if (nob != null)
                nob.enabled = false;
            var body = go.GetComponent<WorldBody>();
            if (body != null)
            {
                body.IsEnemy = true;
                body.Ghost = false;
                body.ResetHp();
            }
            OpLog.Write("gm", PersistDriver.AccountKey(), "Skeleton_gm", "spawn");
            return new AttackResult { Applied = true };
        }

        public AttackResult GmDespawnExtra()
        {
            int n = 0;
            var all = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.name != "Skeleton_gm")
                    continue;
                KillGo(all[i].gameObject);
                n++;
            }
            OpLog.Write("gm", PersistDriver.AccountKey(), "Skeleton_gm", "despawn " + n);
            return n > 0 ? new AttackResult { Applied = true } : new AttackResult { FailReason = "none" };
        }

        bool LootAllowed(WorldBody looter, CorpseNode node)
        {
            if (ActiveParty == null)
                return true;
            if (ActiveParty.Contains(looter))
                return true;
            return false;
        }

        static string WeightRefuseMessage(int str, InventoryBag bag)
        {
            float w = bag != null ? bag.TotalWeight() : 0f;
            int cap = ItemCatalog.CarryCap(str);
            return "과적 — 더 들 수 없습니다 (" + w.ToString("0.#") + "/" + cap + ")";
        }

        bool TooHeavy(WorldBody body)
        {
            var bag = body.GetComponent<InventoryBag>();
            return bag != null && bag.Overweight(StatsOf(body).Str);
        }

        static void KillGo(GameObject go)
        {
            if (go == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
