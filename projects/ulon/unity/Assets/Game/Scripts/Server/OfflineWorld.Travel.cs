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
                    WarpBody(body, Dungeon1.InteriorX, Dungeon1.InteriorZ, true);
                return new AttackResult { Applied = true };
            }
            if (gate.DungeonId == Dungeon2.Id)
            {
                if (gate.IsExit)
                    WarpBody(body, Dungeon2.LeaveX, Dungeon2.LeaveZ);
                else
                    WarpBody(body, Dungeon2.InteriorX, Dungeon2.InteriorZ, true);
                return new AttackResult { Applied = true };
            }
            if (gate.DungeonId == Dungeon3.Id)
            {
                if (gate.IsExit)
                    WarpBody(body, Dungeon3.LeaveX, Dungeon3.LeaveZ);
                else
                    WarpBody(body, Dungeon3.InteriorX, Dungeon3.InteriorZ, true);
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
                LastTravelMessage = Tell(body, result.FailReason);
                return result;
            }
            body.Gold -= TravelMark.GoldCost;
            body.HasMark = true;
            body.MarkX = body.transform.position.x;
            body.MarkZ = body.transform.position.z;
            LastTravelMessage = Tell(body, "기록");
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
                LastTravelMessage = Tell(body, result.FailReason);
                return result;
            }
            WarpBody(body, body.MarkX, body.MarkZ);
            LastTravelMessage = Tell(body, "귀환");
            return result;
        }

        /// <summary>
        /// 워프는 **그 자리 지표 위**로 내려놓는다. 옛 코드는 y를 0.1로 박아 뒀는데(평지 시절 값)
        /// 지형을 올린 뒤로는 워프할 때마다 플레이어가 10m 지하에 처박혔다(검수 2026-09-06 A 랩에서 발견).
        /// </summary>
        static void WarpBody(WorldBody body, float x, float z, bool indoor = false)
            => WarpTo(body, WarpTarget(x, z, indoor));

        /// <summary>이미 정해진 자리로 내려놓는다 — CharacterController를 끄고 옮겨야 밀려나지 않는다.</summary>
        static void WarpTo(WorldBody body, Vector3 spot)
        {
            var cc = body.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            body.transform.position = spot;
            if (cc != null)
                cc.enabled = true;
        }

        /// <summary>
        /// **대상 옆**에 내려놓을 자리(은행·상인 같은 키워드 워프용). 대상 좌표를 그대로 쓰면
        /// 그 자리에 서 있는 것이 건물일 때 플레이어가 **구조물 안**으로 들어간다 —
        /// 「은행」이라고 말하면 풍차 속으로 떨어지던 결함이 그것이다(검수 랩 B, 2026-09-07).
        ///
        /// 대상 바운드 밖 + 사거리 안에서 여덟 방향을 돌며 **몸이 들어갈 빈자리**를 고른다.
        /// 게임과 게이트가 이 함수를 **같이** 쓴다(원장 하나, 같은 자).
        /// </summary>
        public static Vector3 WarpBesideTarget(Transform target, float interactRange)
        {
            var center = target.position;
            float radius = TargetFootprint(target) + 0.6f;                 // 바운드 밖으로 조금 더
            float reach = Mathf.Max(radius, Mathf.Min(interactRange - 0.4f, radius + 1.2f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                float x = center.x + Mathf.Cos(a) * reach;
                float z = center.z + Mathf.Sin(a) * reach;
                var spot = WarpTarget(x, z);
                if (SpotIsClear(spot))
                    return spot;
            }
            // 여덟 방향이 다 막혔으면 그래도 대상 밖에 세운다(안보다는 낫다).
            return WarpTarget(center.x + reach, center.z);
        }

        /// <summary>
        /// 사거리는 **대상의 표면**에서 잰다. 중심에서 재면 풍차처럼 큰 건물은 「옆에 붙어 서도 사거리 밖」이 된다 —
        /// 은행 옆으로 워프시켜 놓고 그 자리에서 「사거리 밖」이라 거절하던 모순이 그것이었다(검수 랩 B).
        /// </summary>
        public static bool WithinReach(Vector3 from, Transform target, float range)
        {
            float flat = Vector2.Distance(new Vector2(from.x, from.z),
                                          new Vector2(target.position.x, target.position.z));
            return flat - TargetFootprint(target) <= range;
        }

        /// <summary>
        /// 도약이 내려앉을 자리. 전방 `BlinkDistance`가 **막혀 있으면 막히기 직전까지만** 간다 —
        /// 벽 너머로 뛰면 플레이어가 구조물 안에 처박힌다(은행 워프와 결과가 같다, 검수 랩 C).
        /// 게임과 게이트가 같이 쓴다.
        /// </summary>
        public static Vector3 BlinkLanding(Vector3 from, Vector3 dir)
        {
            const float Body = 0.35f;
            float want = SpellCast.BlinkDistance;
            var foot = from + Vector3.up * 0.5f;
            var head = from + Vector3.up * 1.6f;
            float go = want;
            if (Physics.CapsuleCast(foot, head, Body, dir, out RaycastHit hit, want, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<WorldBody>() == null
                && hit.collider.GetComponent<TerrainCollider>() == null)
                go = Mathf.Max(0f, hit.distance - (Body + 0.15f));      // 막히기 직전까지
            var spot = WarpTarget(from.x + dir.x * go, from.z + dir.z * go);
            // 지표 경사·소품 때문에 그래도 겹치면 조금씩 물러선다.
            for (int i = 0; i < 6 && !SpotIsClear(spot); i++)
            {
                go = Mathf.Max(0f, go - 0.5f);
                spot = WarpTarget(from.x + dir.x * go, from.z + dir.z * go);
            }
            return spot;
        }

        /// <summary>대상이 바닥에 차지하는 반경 — 렌더러 바운드의 가로/세로 중 큰 쪽 절반.</summary>
        static float TargetFootprint(Transform target)
        {
            var rends = target.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return 0.5f;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            return Mathf.Max(b.extents.x, b.extents.z);
        }

        /// <summary>그 자리에 사람 몸이 들어가는가 — 구조물·소품과 겹치면 빈자리가 아니다.</summary>
        static bool SpotIsClear(Vector3 groundSpot)
        {
            var foot = groundSpot + Vector3.up * 0.5f;
            var head = groundSpot + Vector3.up * 1.6f;
            var hits = Physics.OverlapCapsule(foot, head, 0.35f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<WorldBody>() != null)
                    continue;                                              // 사람·몹은 비켜서면 그만이다
                if (hits[i].GetComponent<TerrainCollider>() != null)
                    continue;                                              // 지표는 딛는 것이지 막는 것이 아니다
                return false;
            }
            return true;
        }

        /// <summary>
        /// 워프가 내려놓는 자리. **게이트와 같은 함수를 쓰라고** 공개해 둔다 —
        /// 판정이 자기만의 계산을 하면 게임이 실제로 어디에 내려놓는지와 갈라진다(발 높이 원장과 같은 이유).
        /// </summary>
        public static Vector3 WarpTarget(float x, float z, bool indoor = false)
        {
            float y = WorldTerrain.HeightAt(x, z) + 0.1f;
            if (indoor)
                y -= WorldTerrain.DungeonDepth;             // 던전 방 바닥
            return new Vector3(x, y, z);
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

        /// <summary>
        /// 12.2 — 수치 원장(items.json·mobs.json)을 **실행 중에** 다시 읽는다. 로더가 한 번 읽고 캐시하므로
        /// 이 길이 없으면 파일을 고쳐도 게임을 껐다 켜야 반영된다(그때 「재빌드 없이」는 반만 맞는 말이다).
        /// </summary>
        public string GmReloadLedgers()
        {
            ItemData.Reload();
            MobData.Reload();
            RecipeData.Reload();
            string err = "";
            if (!string.IsNullOrEmpty(ItemData.LoadError))
                err += " 아이템: " + ItemData.LoadError;
            if (!string.IsNullOrEmpty(MobData.LoadError))
                err += " 몹: " + MobData.LoadError;
            if (!string.IsNullOrEmpty(RecipeData.LoadError))
                err += " 제작법: " + RecipeData.LoadError;
            OpLog.Write("gm", PersistDriver.AccountKey(), "-", "reload_ledgers");
            return "원장 재적재 — 아이템 " + ItemData.Count + "종·몹 " + MobData.Count + "종·제작법 " + RecipeData.Count + "종" + (err == "" ? "" : " / 불량:" + err);
        }

        public AttackResult GmWarpPlaza(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            WarpBody(body, 0f, 0f);
            OpLog.Write("gm", PersistDriver.AccountKey(), body.name, "warp_plaza");
            return new AttackResult { Applied = true };
        }

        /// <summary>§6.1 테스트 공간으로 워프 — 개발자 전용 QA 마당은 마을에서 이어지는 길이 없다.</summary>
        public AttackResult GmWarpTest(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            WarpBody(body, WorldRegions.TestChamber.X, WorldRegions.TestChamber.Z);
            OpLog.Write("gm", PersistDriver.AccountKey(), body.name, "warp_test");
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
