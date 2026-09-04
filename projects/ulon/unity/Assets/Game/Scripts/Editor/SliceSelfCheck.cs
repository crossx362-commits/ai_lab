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
        [MenuItem("Ulon/Run Slice Self-Check")]
        public static void Run()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != scenePath)
                scene = EditorSceneManager.OpenScene(scenePath);
            VisualSliceBuilder.EnsureHuntMobs();
            VisualSliceBuilder.EnsureFishSpot();
            VisualSliceBuilder.EnsureCampfire();
            VisualSliceBuilder.EnsureMortar();
            VisualSliceBuilder.EnsureLockedCrate();
            VisualSliceBuilder.EnsureHousingPlot();
            VisualSliceBuilder.EnsureHouseVendor();
            VisualSliceBuilder.EnsureTameCritter();
            VisualSliceBuilder.EnsureTameBoar();
            VisualSliceBuilder.EnsureMoongate();
            VisualSliceBuilder.EnsureStable();
            VisualSliceBuilder.EnsureEastField();
            VisualSliceBuilder.EnsureSouthField();
            VisualSliceBuilder.EnsureNorthField();
            VisualSliceBuilder.EnsureDungeon1();
            VisualSliceBuilder.EnsureDungeon2();
            VisualSliceBuilder.EnsureFieldBoss();
            if (VisualSliceBuilder.ConfigureHumanoid(
                    "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Warrior.fbx",
                    true))
                throw new InvalidOperationException("이미 설정된 Humanoid FBX를 셀프체크가 다시 임포트하면 안 됩니다.");
            NetworkSliceSetup.WireMob("Skeleton");
            NetworkSliceSetup.WireMob("Bandit");
            NetworkSliceSetup.WireMob("Raider");
            NetworkSliceSetup.WireMob("Rogue");
            NetworkSliceSetup.WireMob("Knight");
            NetworkSliceSetup.WireMob("Acolyte");
            NetworkSliceSetup.WireMob("Minion");
            NetworkSliceSetup.WireMob("SkelRogue");
            NetworkSliceSetup.WireMob(Dungeon1.MobObject);
            NetworkSliceSetup.WireMob(Dungeon1.BossObject);
            NetworkSliceSetup.WireMob(Dungeon2.MobObject);
            NetworkSliceSetup.WireMob(Dungeon2.BossObject);
            NetworkSliceSetup.WireMob(FieldBoss.Object);
            NetworkSliceSetup.EnsureSceneObjectIds(scene);
            foreach (var networkObject in UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(networkObject);
                var sceneId = serialized.FindProperty("SceneId");
                if (sceneId == null || sceneId.ulongValue == 0)
                    throw new InvalidOperationException("씬 NetworkObject SceneId가 비어 있습니다: " + networkObject.name);
            }

            var bandit = GameObject.Find("Bandit");
            var banditBody = bandit != null ? bandit.GetComponent<WorldBody>() : null;
            if (banditBody == null || banditBody.MobId != "bandit" || !banditBody.IsEnemy)
                throw new InvalidOperationException("두 번째 몬스터 도적이 사냥 구역에 있어야 합니다.");
            if (banditBody.DisplayName != "도적" || Math.Abs(banditBody.MaxHp - 45f) > 0.0001f)
                throw new InvalidOperationException("도적 카탈로그는 이름=도적, HP=45여야 합니다.");
            if (bandit.GetComponent<NetworkObject>() == null || bandit.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("도적 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (MobCatalog.KindCount != 8 || !MobCatalog.TryGet(MobCatalog.Bandit, out MobDefinition banditDefinition)
                || banditDefinition.DisplayName != "도적" || Math.Abs(banditDefinition.MaxHp - 45f) > 0.0001f
                || Math.Abs(banditDefinition.Height - 1.75f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그는 스켈레톤+도적+야만인+자객+기사+주술사+졸병+해골도적 8종이어야 합니다.");

            var serverBanditGo = new GameObject("selfcheck-server-bandit");
            try
            {
                var serverBanditBody = serverBanditGo.AddComponent<WorldBody>();
                serverBanditBody.MobId = "bandit";
                var serverBandit = serverBanditGo.AddComponent<NetMob>();
                serverBandit.OnStartServer();
                if (serverBanditBody.DisplayName != "도적" || Math.Abs(serverBanditBody.MaxHp - 45f) > 0.0001f || Math.Abs(serverBanditBody.Hp - 45f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 도적 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverBanditGo);
            }

            var raider = GameObject.Find("Raider");
            var raiderBody = raider != null ? raider.GetComponent<WorldBody>() : null;
            if (raiderBody == null || raiderBody.MobId != "raider" || !raiderBody.IsEnemy)
                throw new InvalidOperationException("세 번째 몬스터 야만인이 사냥 구역에 있어야 합니다.");
            if (raiderBody.DisplayName != "야만인" || Math.Abs(raiderBody.MaxHp - 60f) > 0.0001f)
                throw new InvalidOperationException("야만인 카탈로그는 이름=야만인, HP=60이어야 합니다.");
            if (raider.GetComponent<NetworkObject>() == null || raider.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("야만인 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Raider, out MobDefinition raiderDefinition)
                || raiderDefinition.DisplayName != "야만인" || Math.Abs(raiderDefinition.MaxHp - 60f) > 0.0001f
                || Math.Abs(raiderDefinition.Height - 1.85f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 야만인이 있어야 합니다.");

            var serverRaiderGo = new GameObject("selfcheck-server-raider");
            try
            {
                var serverRaiderBody = serverRaiderGo.AddComponent<WorldBody>();
                serverRaiderBody.MobId = "raider";
                var serverRaider = serverRaiderGo.AddComponent<NetMob>();
                serverRaider.OnStartServer();
                if (serverRaiderBody.DisplayName != "야만인" || Math.Abs(serverRaiderBody.MaxHp - 60f) > 0.0001f || Math.Abs(serverRaiderBody.Hp - 60f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 야만인 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverRaiderGo);
            }

            var rogue = Array.Find(scene.GetRootGameObjects(), go => go.name == "Rogue");
            var rogueBody = rogue != null ? rogue.GetComponent<WorldBody>() : null;
            Debug.Log("[Ulon] Rogue self-check lookup root=" + (rogue != null)
                      + " body=" + (rogueBody != null)
                      + " mobId=" + (rogueBody != null ? rogueBody.MobId : "-")
                      + " enemy=" + (rogueBody != null && rogueBody.IsEnemy));
            if (rogueBody == null || rogueBody.MobId != "rogue" || !rogueBody.IsEnemy)
                throw new InvalidOperationException("네 번째 몬스터 자객이 사냥 구역에 있어야 합니다.");
            if (rogueBody.DisplayName != "자객" || Math.Abs(rogueBody.MaxHp - 40f) > 0.0001f)
                throw new InvalidOperationException("자객 카탈로그는 이름=자객, HP=40이어야 합니다.");
            if (rogue.GetComponent<NetworkObject>() == null || rogue.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("자객 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Rogue, out MobDefinition rogueDefinition)
                || rogueDefinition.DisplayName != "자객" || Math.Abs(rogueDefinition.MaxHp - 40f) > 0.0001f
                || Math.Abs(rogueDefinition.Height - 1.70f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 자객이 있어야 합니다.");

            var serverRogueGo = new GameObject("selfcheck-server-rogue");
            try
            {
                var serverRogueBody = serverRogueGo.AddComponent<WorldBody>();
                serverRogueBody.MobId = "rogue";
                var serverRogue = serverRogueGo.AddComponent<NetMob>();
                serverRogue.OnStartServer();
                if (serverRogueBody.DisplayName != "자객" || Math.Abs(serverRogueBody.MaxHp - 40f) > 0.0001f || Math.Abs(serverRogueBody.Hp - 40f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 자객 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverRogueGo);
            }

            var knight = GameObject.Find("Knight");
            var knightBody = knight != null ? knight.GetComponent<WorldBody>() : null;
            if (knightBody == null || knightBody.MobId != "knight" || !knightBody.IsEnemy)
                throw new InvalidOperationException("다섯 번째 몬스터 기사가 사냥 구역에 있어야 합니다.");
            if (knightBody.DisplayName != "기사" || Math.Abs(knightBody.MaxHp - 70f) > 0.0001f)
                throw new InvalidOperationException("기사 카탈로그는 이름=기사, HP=70여야 합니다.");
            if (knight.GetComponent<NetworkObject>() == null || knight.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("기사 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Knight, out MobDefinition knightDefinition)
                || knightDefinition.DisplayName != "기사" || Math.Abs(knightDefinition.MaxHp - 70f) > 0.0001f
                || Math.Abs(knightDefinition.Height - 1.80f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 기사가 있어야 합니다.");

            var serverKnightGo = new GameObject("selfcheck-server-knight");
            try
            {
                var serverKnightBody = serverKnightGo.AddComponent<WorldBody>();
                serverKnightBody.MobId = "knight";
                var serverKnight = serverKnightGo.AddComponent<NetMob>();
                serverKnight.OnStartServer();
                if (serverKnightBody.DisplayName != "기사" || Math.Abs(serverKnightBody.MaxHp - 70f) > 0.0001f || Math.Abs(serverKnightBody.Hp - 70f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 기사 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverKnightGo);
            }

            var acolyte = GameObject.Find("Acolyte");
            var acolyteBody = acolyte != null ? acolyte.GetComponent<WorldBody>() : null;
            if (acolyteBody == null || acolyteBody.MobId != "acolyte" || !acolyteBody.IsEnemy)
                throw new InvalidOperationException("여섯 번째 몬스터 주술사가 사냥 구역에 있어야 합니다.");
            if (acolyteBody.DisplayName != "주술사" || Math.Abs(acolyteBody.MaxHp - 50f) > 0.0001f)
                throw new InvalidOperationException("주술사 카탈로그는 이름=주술사, HP=50이어야 합니다.");
            if (acolyte.GetComponent<NetworkObject>() == null || acolyte.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("주술사 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Acolyte, out MobDefinition acolyteDefinition)
                || acolyteDefinition.DisplayName != "주술사" || Math.Abs(acolyteDefinition.MaxHp - 50f) > 0.0001f
                || Math.Abs(acolyteDefinition.Height - 1.65f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 주술사가 있어야 합니다.");

            var serverAcolyteGo = new GameObject("selfcheck-server-acolyte");
            try
            {
                var serverAcolyteBody = serverAcolyteGo.AddComponent<WorldBody>();
                serverAcolyteBody.MobId = "acolyte";
                var serverAcolyte = serverAcolyteGo.AddComponent<NetMob>();
                serverAcolyte.OnStartServer();
                if (serverAcolyteBody.DisplayName != "주술사" || Math.Abs(serverAcolyteBody.MaxHp - 50f) > 0.0001f || Math.Abs(serverAcolyteBody.Hp - 50f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 주술사 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverAcolyteGo);
            }

            var minion = GameObject.Find("Minion");
            var minionBody = minion != null ? minion.GetComponent<WorldBody>() : null;
            if (minionBody == null || minionBody.MobId != "minion" || !minionBody.IsEnemy)
                throw new InvalidOperationException("일곱 번째 몬스터 졸병이 사냥 구역에 있어야 합니다.");
            if (minionBody.DisplayName != "졸병" || Math.Abs(minionBody.MaxHp - 22f) > 0.0001f)
                throw new InvalidOperationException("졸병 카탈로그는 이름=졸병, HP=22여야 합니다.");
            if (minion.GetComponent<NetworkObject>() == null || minion.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("졸병 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Minion, out MobDefinition minionDefinition)
                || minionDefinition.DisplayName != "졸병" || Math.Abs(minionDefinition.MaxHp - 22f) > 0.0001f
                || Math.Abs(minionDefinition.Height - 1.35f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 졸병이 있어야 합니다.");
            if (Math.Abs(minion.transform.position.x - 8.4f) > 0.05f || Math.Abs(minion.transform.position.z - 13.2f) > 0.05f)
                throw new InvalidOperationException("졸병은 사냥 라인 x=8.4 z=13.2에 있어야 하며 같은 x에 쌓이면 안 됩니다.");

            var serverMinionGo = new GameObject("selfcheck-server-minion");
            try
            {
                var serverMinionBody = serverMinionGo.AddComponent<WorldBody>();
                serverMinionBody.MobId = "minion";
                var serverMinion = serverMinionGo.AddComponent<NetMob>();
                serverMinion.OnStartServer();
                if (serverMinionBody.DisplayName != "졸병" || Math.Abs(serverMinionBody.MaxHp - 22f) > 0.0001f || Math.Abs(serverMinionBody.Hp - 22f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 졸병 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverMinionGo);
            }

            var skelRogue = GameObject.Find("SkelRogue");
            var skelRogueBody = skelRogue != null ? skelRogue.GetComponent<WorldBody>() : null;
            if (skelRogueBody == null || skelRogueBody.MobId != "skelrogue" || !skelRogueBody.IsEnemy)
                throw new InvalidOperationException("여덟 번째 몬스터 해골도적이 사냥 구역에 있어야 합니다.");
            if (skelRogueBody.DisplayName != "해골도적" || Math.Abs(skelRogueBody.MaxHp - 28f) > 0.0001f)
                throw new InvalidOperationException("해골도적 카탈로그는 이름=해골도적, HP=28이어야 합니다.");
            if (skelRogue.GetComponent<NetworkObject>() == null || skelRogue.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("해골도적 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.SkelRogue, out MobDefinition skelRogueDefinition)
                || skelRogueDefinition.DisplayName != "해골도적" || Math.Abs(skelRogueDefinition.MaxHp - 28f) > 0.0001f
                || Math.Abs(skelRogueDefinition.Height - 1.50f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그에 해골도적이 있어야 합니다.");
            if (Math.Abs(skelRogue.transform.position.x - 10.4f) > 0.05f || Math.Abs(skelRogue.transform.position.z - 13.2f) > 0.05f)
                throw new InvalidOperationException("해골도적은 사냥 라인 x=10.4 z=13.2에 있어야 하며 같은 x에 쌓이면 안 됩니다.");

            var serverSkelRogueGo = new GameObject("selfcheck-server-skelrogue");
            try
            {
                var serverSkelRogueBody = serverSkelRogueGo.AddComponent<WorldBody>();
                serverSkelRogueBody.MobId = "skelrogue";
                var serverSkelRogue = serverSkelRogueGo.AddComponent<NetMob>();
                serverSkelRogue.OnStartServer();
                if (serverSkelRogueBody.DisplayName != "해골도적" || Math.Abs(serverSkelRogueBody.MaxHp - 28f) > 0.0001f || Math.Abs(serverSkelRogueBody.Hp - 28f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 해골도적 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverSkelRogueGo);
            }

            if (!MobCatalog.TryGet(MobCatalog.BoneWarden, out MobDefinition bossDefinition)
                || bossDefinition.DisplayName != "본워든" || Math.Abs(bossDefinition.MaxHp - 120f) > 0.0001f
                || Math.Abs(bossDefinition.Height - 2.25f) > 0.0001f || !MobCatalog.IsBoss(MobCatalog.BoneWarden)
                || MobCatalog.IsBoss(MobCatalog.Skeleton) || MobCatalog.KindCount != 8)
                throw new InvalidOperationException("던전 1 네임드 엘리트 본워든은 사냥 8종과 별도 보스여야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.ShadowCaptain, out MobDefinition boss2Definition)
                || boss2Definition.DisplayName != "섀도우캡틴" || Math.Abs(boss2Definition.MaxHp - 150f) > 0.0001f
                || Math.Abs(boss2Definition.Height - 2.35f) > 0.0001f || !MobCatalog.IsBoss(MobCatalog.ShadowCaptain)
                || MobCatalog.KillDropOf(MobCatalog.ShadowCaptain) != ItemCatalog.CaptainSigil
                || MobCatalog.KillDropOf(MobCatalog.BoneWarden) != ItemCatalog.WardenCrest
                || MobCatalog.KillDropOf(MobCatalog.ShadowCaptain) == MobCatalog.KillDropOf(MobCatalog.BoneWarden)
                || Math.Abs(boss2Definition.MaxHp - bossDefinition.MaxHp) < 0.0001f
                || Math.Abs(boss2Definition.Height - bossDefinition.Height) < 0.0001f
                || MobCatalog.KindCount != 8)
                throw new InvalidOperationException("던전 2 네임드 엘리트 섀도우캡틴은 본워든과 다른 HP/키/드랍의 별도 보스여야 합니다.");
            if (!MobCatalog.TryGet(MobCatalog.Hexarch, out MobDefinition boss3Definition)
                || boss3Definition.DisplayName != "헥사크" || Math.Abs(boss3Definition.MaxHp - 180f) > 0.0001f
                || Math.Abs(boss3Definition.Height - 2.48f) > 0.0001f || !MobCatalog.IsBoss(MobCatalog.Hexarch)
                || MobCatalog.KillDropOf(MobCatalog.Hexarch) != ItemCatalog.HexSeal
                || MobCatalog.KillDropOf(MobCatalog.Hexarch) == MobCatalog.KillDropOf(MobCatalog.BoneWarden)
                || MobCatalog.KillDropOf(MobCatalog.Hexarch) == MobCatalog.KillDropOf(MobCatalog.ShadowCaptain)
                || Math.Abs(boss3Definition.MaxHp - bossDefinition.MaxHp) < 0.0001f
                || Math.Abs(boss3Definition.MaxHp - boss2Definition.MaxHp) < 0.0001f
                || Math.Abs(boss3Definition.Height - bossDefinition.Height) < 0.0001f
                || Math.Abs(boss3Definition.Height - boss2Definition.Height) < 0.0001f
                || MobCatalog.KindCount != 8)
                throw new InvalidOperationException("필드 네임드 엘리트 헥사크는 본워든/섀도우캡틴과 다른 HP/키/드랍의 별도 보스여야 합니다.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var skills = new SkillSet();
            if (Math.Abs(skills.Get(SkillId.Swordsmanship)) > 0.0001f)
                throw new InvalidOperationException("검술은 0.0에서 시작해야 합니다.");

            var miss = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 9f,
                Skills = skills,
                TargetAlive = true
            });
            if (miss.Applied)
                throw new InvalidOperationException("사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(skills.Get(SkillId.Tactics)) > 0.0001f || Math.Abs(skills.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("실패한 공격은 스킬을 올리면 안 됩니다.");

            var hit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1.2f,
                Now = 1f,
                NextAttackAt = 0f,
                Skills = skills,
                TargetAlive = true
            });
            if (!hit.Applied || !hit.Hit)
                throw new InvalidOperationException("사거리 안 공격이 들어가야 합니다.");
            if (Math.Abs(hit.SkillBefore) > 0.0001f || Math.Abs(hit.SkillAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException($"검술 0.0→0.1이어야 합니다. 실제 {hit.SkillBefore}→{hit.SkillAfter}");
            if (Math.Abs(skills.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("SkillSet에 0.1이 저장되어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("근접 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("근접 공격 후 해부학 0.0→0.1이어야 합니다.");

            var a = new SkillSet();
            var b = new SkillSet();
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 1f, Skills = a, TargetAlive = true });
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 1f, Skills = b, TargetAlive = true });
            if (Math.Abs(a.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f || Math.Abs(b.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("공격자별 스킬이 독립이어야 합니다.");

            var archSkills = new SkillSet();
            var archMiss = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 9f,
                Range = ItemCatalog.ArcheryRange,
                WeaponSkill = SkillId.Archery,
                Skills = archSkills,
                TargetAlive = true
            });
            if (archMiss.Applied)
                throw new InvalidOperationException("활 사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(archSkills.Get(SkillId.Archery)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Tactics)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("실패한 원거리 공격은 스킬을 올리면 안 됩니다.");

            var archHit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 7f,
                Range = ItemCatalog.ArcheryRange,
                Now = 1f,
                NextAttackAt = 0f,
                WeaponSkill = SkillId.Archery,
                Skills = archSkills,
                TargetAlive = true
            });
            if (!archHit.Applied || !archHit.Hit)
                throw new InvalidOperationException("활 사거리 안 공격이 들어가야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Archery) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("궁술 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("활 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("활 공격 후 해부학 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Swordsmanship)) > 0.0001f)
                throw new InvalidOperationException("활 공격은 검술을 올리면 안 됩니다.");

            var meleeFar = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 7f,
                Skills = new SkillSet(),
                TargetAlive = true
            });
            if (meleeFar.Applied)
                throw new InvalidOperationException("근접은 활 사거리에서 들어가면 안 됩니다.");

            var dexLow = new StatSet();
            dexLow.ForceSet(30, 10, 25);
            var dexHigh = new StatSet();
            dexHigh.ForceSet(30, 50, 25);
            var archLow = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.ArcheryRange,
                Now = 2f,
                WeaponSkill = SkillId.Archery,
                Skills = new SkillSet(),
                Stats = dexLow,
                TargetAlive = true
            });
            var archHigh = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.ArcheryRange,
                Now = 2f,
                WeaponSkill = SkillId.Archery,
                Skills = new SkillSet(),
                Stats = dexHigh,
                TargetAlive = true
            });
            if (archHigh.Damage <= archLow.Damage)
                throw new InvalidOperationException("궁술 피해는 DEX 보정이 있어야 합니다.");

            if (!CharacterStore.EnsureRunning())
                throw new InvalidOperationException("persist 서버를 시작하지 못했습니다.");
            var snap = new CharacterSnapshot
            {
                AccountId = "selfcheck",
                CharacterId = "selfcheck",
                Name = "검사",
                X = 1.5f,
                Y = 0f,
                Z = -2f,
                Hp = 41f,
                Skills = new[] { new SkillRecord { Id = (int)SkillId.Swordsmanship, Value = 0.4f, Lock = 0 } },
                Inventory = new[] { new ItemRecord { Slot = 0, TemplateId = "iron_ore", Amount = 2 } }
            };
            CharacterStore.Save(snap);
            var loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || Math.Abs(loaded.Skills[0].Value - 0.4f) > 0.0001f || loaded.Inventory.Length != 1)
                throw new InvalidOperationException("persist 저장/로드 실패");
            snap.Inventory = new[]
            {
                new ItemRecord { Slot = 0, TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 37, MakerId = "crafter-a" }
            };
            CharacterStore.Save(snap);
            loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || loaded.Inventory.Length != 1
                || loaded.Inventory[0].Uses != 37 || loaded.Inventory[0].MakerId != "crafter-a")
                throw new InvalidOperationException("persist 내구/Maker Mark 왕복 실패");

            var gatherSkills = new SkillSet();
            SkillGain.TryRaise(gatherSkills, SkillId.Mining, 10f, out _, out float mineAfter);
            if (Math.Abs(mineAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("채광 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Blacksmithing, 15f, out _, out float smithAfter);
            if (Math.Abs(smithAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("대장장이 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Lumberjacking, 10f, out _, out float woodAfter);
            if (Math.Abs(woodAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("벌목 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Carpentry, 12f, out _, out float carpAfter);
            if (Math.Abs(carpAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("목공 0.0→0.1이어야 합니다.");

            var weak = new StatSet();
            weak.ForceSet(20, 25, 25);
            var strong = new StatSet();
            strong.ForceSet(50, 25, 25);
            var dmgWeak = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 2f, Skills = new SkillSet(), Stats = weak, TargetAlive = true });
            var dmgStrong = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 2f, Skills = new SkillSet(), Stats = strong, TargetAlive = true });
            if (dmgStrong.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("STR가 근접 피해에 반영되어야 합니다.");
            var tac = new SkillSet();
            tac.ForceSet(SkillId.Tactics, 40f, SkillLock.Up);
            var dmgTac = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 3f, Skills = tac, Stats = weak, TargetAlive = true });
            if (dmgTac.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("전술이 근접 피해에 반영되어야 합니다.");
            var ana = new SkillSet();
            ana.ForceSet(SkillId.Anatomy, 40f, SkillLock.Up);
            var dmgAna = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 3f, Skills = ana, Stats = weak, TargetAlive = true });
            if (dmgAna.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("해부학이 근접 피해에 반영되어야 합니다.");
            if (StatSet.MaxHpOf(30) != 50 || StatSet.MaxHpOf(50) != 70)
                throw new InvalidOperationException("MaxHp=20+STR 이어야 합니다.");
            var gainStats = new StatSet();
            int strBefore = gainStats.Str;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, gainStats);
            if (gainStats.Str != strBefore + 1)
                throw new InvalidOperationException("검술 상승 시 STR가 올라야 합니다.");
            snap.Str = 44;
            snap.Dex = 22;
            snap.Int = 18;
            CharacterStore.Save(snap);
            loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || loaded.Str != 44 || loaded.Dex != 22 || loaded.Int != 18)
                throw new InvalidOperationException("persist STR/DEX/INT 왕복 실패");

            var cap = new SkillSet();
            cap.ForceSet(SkillId.Archery, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Tactics, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Parrying, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Anatomy, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Healing, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Magery, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Mining, 100f, SkillLock.Locked);
            if (Math.Abs(cap.Total - 700f) > 0.01f)
                throw new InvalidOperationException("700 캡 픽스처 실패 total=" + cap.Total);
            if (SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out _))
                throw new InvalidOperationException("↓ 스킬 없이 700 캡을 넘기면 안 됩니다.");
            cap.SetLock(SkillId.Mining, SkillLock.Down);
            if (!SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out float afterCap))
                throw new InvalidOperationException("↓ 채광이 있으면 검술이 올라야 합니다.");
            if (Math.Abs(afterCap - 0.1f) > 0.0001f || Math.Abs(cap.Get(SkillId.Mining) - 99.9f) > 0.0001f)
                throw new InvalidOperationException("700캡에서 ↓ 채광이 0.1 줄어야 합니다.");
            cap.SetLock(SkillId.Swordsmanship, SkillLock.Locked);
            if (SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out _))
                throw new InvalidOperationException("잠긴 스킬은 오르면 안 됩니다.");

            var lockedStr = new StatSet();
            lockedStr.SetLock(StatId.Str, SkillLock.Locked);
            int strWas = lockedStr.Str;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, lockedStr);
            if (lockedStr.Str != strWas)
                throw new InvalidOperationException("잠긴 STR은 스킬로 오르면 안 됩니다.");

            var tacLock = new SkillSet();
            tacLock.SetLock(SkillId.Tactics, SkillLock.Locked);
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 4f, Skills = tacLock, TargetAlive = true });
            if (Math.Abs(tacLock.Get(SkillId.Tactics)) > 0.0001f)
                throw new InvalidOperationException("잠긴 전술은 오르면 안 됩니다.");
            if (Math.Abs(tacLock.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("전술 잠금이 검술 상승을 막으면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Tactics) != StatId.Str)
                throw new InvalidOperationException("전술 Primary는 STR이어야 합니다.");

            var anaLock = new SkillSet();
            anaLock.SetLock(SkillId.Anatomy, SkillLock.Locked);
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 5f, Skills = anaLock, TargetAlive = true });
            if (Math.Abs(anaLock.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("잠긴 해부학은 오르면 안 됩니다.");
            if (Math.Abs(anaLock.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("해부학 잠금이 검술 상승을 막으면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Anatomy) != StatId.Int)
                throw new InvalidOperationException("해부학 Primary는 INT이어야 합니다.");
            var anaStats = new StatSet();
            int intWas = anaStats.Int;
            SkillGain.TryRaise(new SkillSet(), SkillId.Anatomy, 20f, out _, out _, anaStats);
            if (anaStats.Int != intWas + 1)
                throw new InvalidOperationException("해부학 상승 시 INT가 올라야 합니다.");

            snap.Inventory = new[] { new ItemRecord { Slot = 0, TemplateId = "wood", Amount = 3 } };
            snap.Bank = System.Array.Empty<ItemRecord>();
            CharacterStore.Save(snap);
            var bankBody = new GameObject("selfcheck-bank");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = bankBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bankBody.AddComponent<InventoryBag>();
                bag.Add("wood", 3);
                var dep = world.DepositAll(body);
                if (!dep.Applied)
                    throw new InvalidOperationException("은행 맡기기 실패: " + dep.FailReason);
                if (bag.Items.Count != 0)
                    throw new InvalidOperationException("맡긴 뒤 가방이 비어야 합니다.");
                var vault = bankBody.GetComponent<BankVault>();
                if (vault == null || vault.Items.Count != 1 || vault.Items[0].Amount != 3)
                    throw new InvalidOperationException("은행에 wood x3이 있어야 합니다.");
                snap = CharacterBinder.Capture("selfcheck", body, new SkillSet(), new StatSet());
                snap.AccountId = "selfcheck";
                snap.CharacterId = "selfcheck";
                CharacterStore.Save(snap);
                loaded = CharacterStore.Load("selfcheck");
                if (loaded == null || loaded.Bank == null || loaded.Bank.Length != 1 || loaded.Bank[0].Amount != 3)
                    throw new InvalidOperationException("persist 은행 왕복 실패");
                CharacterBinder.Apply(body, loaded, new SkillSet(), new StatSet());
                var wd = world.WithdrawAll(body);
                if (!wd.Applied || bag.Items.Count == 0 || vault.Items.Count != 0)
                    throw new InvalidOperationException("은행 찾기 실패");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bankBody);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }

            if (CharacterCreate.Validate("", 30, 25, 25, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 50f, 30f, 20f }) == null)
                throw new InvalidOperationException("빈 이름은 거절해야 합니다.");
            if (CharacterCreate.Validate("검사", 50, 25, 10, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 50f, 30f, 20f }) == null)
                throw new InvalidOperationException("스탯 총합 80이 아니면 거절해야 합니다.");
            if (CharacterCreate.Validate("검사", 30, 25, 25, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 60f, 20f, 20f }) == null)
                throw new InvalidOperationException("시작 스킬 개별 50 초과는 거절해야 합니다.");
            var created = CharacterCreate.Build("create-check", "검사", 1, 30, 25, 25,
                new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing },
                new[] { 50f, 30f, 20f });
            if (created.Name != "검사" || created.Str != 30 || created.Appearance != 1 || created.Hp != 50)
                throw new InvalidOperationException("생성 스냅샷 기본값 실패");
            if (created.Skills.Length != 3 || Math.Abs(created.Skills[0].Value - 50f) > 0.01f)
                throw new InvalidOperationException("생성 시작 스킬 실패");
            bool hasSword = false, hasOre = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == "iron_sword") hasSword = true;
                if (created.Inventory[i].TemplateId == "iron_ore" && created.Inventory[i].Amount == 2) hasOre = true;
            }
            if (!hasSword || !hasOre)
                throw new InvalidOperationException("시작 장비 실패");
            var archerCreate = CharacterCreate.Build("archer-check", "궁수", 0, 20, 40, 20,
                new[] { SkillId.Archery, SkillId.Lumberjacking, SkillId.Carpentry },
                new[] { 50f, 30f, 20f });
            bool hasBow = false;
            for (int i = 0; i < archerCreate.Inventory.Length; i++)
                if (archerCreate.Inventory[i].TemplateId == ItemCatalog.WoodenBow)
                    hasBow = true;
            if (!hasBow)
                throw new InvalidOperationException("궁술 시작은 나무활을 줘야 합니다.");
            var parryCreate = CharacterCreate.Build("parry-check", "방패", 0, 20, 40, 20,
                new[] { SkillId.Parrying, SkillId.Swordsmanship, SkillId.Mining },
                new[] { 50f, 30f, 20f });
            bool hasShieldStart = false;
            for (int i = 0; i < parryCreate.Inventory.Length; i++)
                if (parryCreate.Inventory[i].TemplateId == ItemCatalog.WoodenShield)
                    hasShieldStart = true;
            if (!hasShieldStart)
                throw new InvalidOperationException("방패술 시작은 나무방패를 줘야 합니다.");
            CharacterStore.Save(created);
            loaded = CharacterStore.Load("create-check");
            if (loaded == null || loaded.Name != "검사" || loaded.Appearance != 1 || loaded.Skills == null || loaded.Skills.Length != 3)
                throw new InvalidOperationException("생성 persist 왕복 실패");

            var mageCreate = CharacterCreate.Build("mage-check", "마법", 0, 20, 20, 40,
                new[] { SkillId.Magery, SkillId.Meditation, SkillId.EvaluateIntelligence },
                new[] { 50f, 30f, 20f });
            if (mageCreate.Spells == null || mageCreate.Spells.Length != 3)
                throw new InvalidOperationException("마법 시작은 주문 3개를 줘야 합니다.");
            bool hasBoltStart = false;
            for (int si = 0; si < mageCreate.Spells.Length; si++)
                if (mageCreate.Spells[si] == (int)SpellId.Bolt)
                    hasBoltStart = true;
            if (!hasBoltStart)
                throw new InvalidOperationException("마법 시작 주문에 벼락이 있어야 합니다.");
            bool hasResin = false;
            for (int i = 0; i < mageCreate.Inventory.Length; i++)
                if (mageCreate.Inventory[i].TemplateId == SpellCast.Reagent && mageCreate.Inventory[i].Amount >= 8)
                    hasResin = true;
            if (!hasResin)
                throw new InvalidOperationException("마법 시작 시약은 resin x8");

            var book = new Spellbook();
            if (SpellCast.ManaCost(SpellId.Ember) != 6)
                throw new InvalidOperationException("불씨 마나 비용");
            var mageBody = new GameObject("selfcheck-mage");
            GameObject mageWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    mageWorldGo = new GameObject("selfcheck-mage-world");
                    world = mageWorldGo.AddComponent<OfflineWorld>();
                }
                var body = mageBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                body.RecalcFromInt(40);
                var bag = mageBody.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);
                world.BookOf(body).Learn(SpellId.Ember);
                world.BookOf(body).Learn(SpellId.Mend);
                var noTgt = world.TryCast(body, SpellId.Ember, null);
                if (noTgt.Applied)
                    throw new InvalidOperationException("대상 없는 불씨는 실패해야 합니다.");
                var dummy = new GameObject("selfcheck-skel");
                var skel = dummy.AddComponent<WorldBody>();
                skel.IsEnemy = true;
                skel.MaxHp = 30f;
                skel.ResetHp();
                dummy.transform.position = mageBody.transform.position;
                var ember = world.TryCast(body, SpellId.Ember, skel);
                if (!ember.Applied || skel.Hp >= 30f)
                    throw new InvalidOperationException("불씨 피해 실패: " + ember.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Magery) < 0.09f)
                    throw new InvalidOperationException("불씨 후 마법이 올라야 합니다.");
                body.SetHp(20f);
                var mend = world.TryCast(body, SpellId.Mend, body);
                if (!mend.Applied || body.Hp <= 20f)
                    throw new InvalidOperationException("봉합 실패: " + mend.FailReason);

                bag.Add("iron_sword", 1);
                var death = world.HandleDeath(body, "mage-check");
                if (!death.Applied || !body.Ghost || bag.Items.Count != 0)
                    throw new InvalidOperationException("사망 시 가방이 시체로 가야 합니다.");
                var corpse = OfflineWorld.FindCorpse("mage-check");
                if (corpse == null || corpse.Items.Count < 1)
                    throw new InvalidOperationException("시체 아이템 없음");
                var healerGo = new GameObject("Healer");
                healerGo.transform.position = body.transform.position;
                var healer = healerGo.AddComponent<HealerStation>();
                var rez = world.TryResurrect(body, healer);
                if (!rez.Applied || body.Ghost)
                    throw new InvalidOperationException("부활 실패: " + rez.FailReason);
                var corpseRends = corpse.GetComponentsInChildren<Renderer>(true);
                for (int ri = 0; ri < corpseRends.Length; ri++)
                {
                    var mat = corpseRends[ri].sharedMaterial;
                    if (mat != null && mat.name.IndexOf("Default-Material", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException("시체는 Default-Material 프리미티브면 안 됩니다.");
                }
                var loot = world.TryLootCorpse(body, corpse);
                if (!loot.Applied)
                    throw new InvalidOperationException("시체 회수 실패: " + loot.FailReason);
                var palGo = new GameObject("selfcheck-pal");
                palGo.transform.position = body.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.DisplayName = "동료";
                pal.IsEnemy = false;
                pal.MaxHp = 40f;
                pal.ResetHp();
                var invited = world.TryPartyInvite(body, pal);
                if (!invited.Applied || world.ActiveParty == null || !world.ActiveParty.Contains(pal))
                    throw new InvalidOperationException("파티 초대 실패: " + invited.FailReason);
                var said = world.TryPartySay(body, "hi");
                if (!said.Applied || world.ActiveParty.Chat.Count < 1)
                    throw new InvalidOperationException("파티 채팅 실패");
                bag.Add("resin", 1);
                world.HandleDeath(body, "mage-check");
                var partyCorpse = OfflineWorld.FindCorpse("mage-check");
                var palLoot = world.TryLootCorpse(pal, partyCorpse);
                if (!palLoot.Applied)
                    throw new InvalidOperationException("파티 룻 실패: " + palLoot.FailReason);
                var strangerGo = new GameObject("selfcheck-stranger");
                strangerGo.transform.position = body.transform.position;
                var stranger = strangerGo.AddComponent<WorldBody>();
                stranger.ResetHp();
                bag.Add("wood", 1);
                var healer2 = healerGo.GetComponent<HealerStation>();
                world.TryResurrect(body, healer2);
                world.HandleDeath(body, "mage-check");
                var locked = OfflineWorld.FindCorpse("mage-check");
                var denied = world.TryLootCorpse(stranger, locked);
                if (denied.Applied)
                    throw new InvalidOperationException("파티 밖은 룻하면 안 됩니다.");
                world.TryLootCorpse(pal, locked);
                world.TryResurrect(body, healer2);
                world.TryPartyLeave(body);
                UnityEngine.Object.DestroyImmediate(palGo);
                UnityEngine.Object.DestroyImmediate(strangerGo);
                bag.Add("wood", 1);
                world.HandleDeath(body, "mage-check");
                var rotting = OfflineWorld.FindCorpse("mage-check");
                if (rotting == null)
                    throw new InvalidOperationException("두 번째 시체 없음");
                rotting.SpawnedAt = -9999f;
                rotting.DecaySeconds = 1f;
                world.TickCorpses(0f);
                if (OfflineWorld.FindCorpse("mage-check") != null)
                    throw new InvalidOperationException("시체가 소멸해야 합니다.");
                UnityEngine.Object.DestroyImmediate(dummy);
                UnityEngine.Object.DestroyImmediate(healerGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mageBody);
                if (mageWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(mageWorldGo);
            }

            if (ItemCatalog.CarryCap(30) != 120 || ItemCatalog.WeightOf("iron_ore") != 2f)
                throw new InvalidOperationException("무게 공식 실패");
            var miner = CharacterCreate.Build("tool-check", "광부", 0, 30, 25, 25,
                new[] { SkillId.Mining, SkillId.Lumberjacking, SkillId.Swordsmanship },
                new[] { 40f, 30f, 30f });
            bool pick = false, hat = false;
            for (int i = 0; i < miner.Inventory.Length; i++)
            {
                if (miner.Inventory[i].TemplateId == ItemCatalog.Pickaxe && miner.Inventory[i].Uses == 20) pick = true;
                if (miner.Inventory[i].TemplateId == ItemCatalog.Hatchet && miner.Inventory[i].Uses == 20) hat = true;
            }
            if (!pick || !hat)
                throw new InvalidOperationException("채광/벌목 시작 도구 실패");

            var toolBody = new GameObject("selfcheck-tool");
            GameObject toolWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    toolWorldGo = new GameObject("selfcheck-tool-world");
                    world = toolWorldGo.AddComponent<OfflineWorld>();
                }
                var body = toolBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = toolBody.AddComponent<InventoryBag>();
                var veinGo = new GameObject("IronVein");
                veinGo.transform.position = toolBody.transform.position;
                var vein = veinGo.AddComponent<ResourceNode>();
                vein.ResourceId = "iron_ore";
                vein.GatherSkill = SkillId.Mining;
                vein.Remaining = 5;
                var noTool = world.TryGather(body, vein);
                if (noTool.Applied)
                    throw new InvalidOperationException("곡괭이 없이 채광되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 1 });
                var g1 = world.TryGather(body, vein);
                if (!g1.Applied)
                    throw new InvalidOperationException("곡괭이 채광 실패: " + g1.FailReason);
                var g2 = world.TryGather(body, vein);
                if (g2.Applied)
                    throw new InvalidOperationException("내구 0 곡괭이로 채광되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 1 });
                var forgeGo = new GameObject("Forge");
                forgeGo.transform.position = toolBody.transform.position;
                var forge = forgeGo.AddComponent<CraftStation>();
                var repaired = world.TryCraft(body, forge);
                if (!repaired.Applied)
                    throw new InvalidOperationException("도구 수리 실패: " + repaired.FailReason);
                if (bag.ToolUses(ItemCatalog.Pickaxe) < 10)
                    throw new InvalidOperationException("수리 후 내구가 올라야 합니다.");

                body.CharacterId = "smith-mark";
                bag.Add("iron_ore", 2);
                var forged = world.TryCraft(body, forge);
                if (!forged.Applied)
                    throw new InvalidOperationException("철검 제작 실패: " + forged.FailReason);
                bool markedSword = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    if (bag.Items[i].MakerId != "smith-mark")
                        throw new InvalidOperationException("제작품 Maker Mark가 제작자 id여야 합니다.");
                    if (bag.Items[i].Uses != ItemCatalog.MaxUsesOf(ItemCatalog.IronSword))
                        throw new InvalidOperationException("제작품 내구가 최대여야 합니다.");
                    markedSword = true;
                }
                if (!markedSword)
                    throw new InvalidOperationException("철검 제작 결과가 가방에 있어야 합니다.");
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    var worn = bag.Items[i];
                    worn.Uses = 12;
                    bag.Items[i] = worn;
                    break;
                }
                bag.Add("iron_ore", 1);
                var swordFix = world.TryCraft(body, forge);
                if (!swordFix.Applied)
                    throw new InvalidOperationException("철검 수리 실패: " + swordFix.FailReason);
                bool restored = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    if (bag.Items[i].Uses < 22)
                        throw new InvalidOperationException("수리 후 철검 내구가 올라야 합니다.");
                    if (bag.Items[i].MakerId != "smith-mark")
                        throw new InvalidOperationException("수리는 Maker Mark를 지우면 안 됩니다.");
                    restored = true;
                }
                if (!restored)
                    throw new InvalidOperationException("수리 대상 철검이 없습니다.");

                world.StatsOf(body).ForceSet(10, 25, 25);
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });
                var weakSkelGo = new GameObject("selfcheck-str");
                var weakSkel = weakSkelGo.AddComponent<WorldBody>();
                weakSkel.IsEnemy = true;
                weakSkel.MaxHp = 30f;
                weakSkel.ResetHp();
                weakSkelGo.transform.position = toolBody.transform.position;
                var blocked = world.TryAttack(body, weakSkel);
                if (blocked.Applied)
                    throw new InvalidOperationException("STR 부족인데 철검 공격이 들어가면 안 됩니다.");
                world.StatsOf(body).ForceSet(30, 25, 25);
                var okAtk = world.TryAttack(body, weakSkel);
                if (!okAtk.Applied)
                    throw new InvalidOperationException("STR 충족 공격 실패: " + okAtk.FailReason);

                bag.Add("iron_ore", 80);
                if (!bag.Overweight(30))
                    throw new InvalidOperationException("과적 판정 실패");
                UnityEngine.Object.DestroyImmediate(veinGo);
                UnityEngine.Object.DestroyImmediate(forgeGo);
                UnityEngine.Object.DestroyImmediate(weakSkelGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(toolBody);
                if (toolWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(toolWorldGo);
            }

            var respawnGo = new GameObject("selfcheck-respawn");
            try
            {
                var node = respawnGo.AddComponent<ResourceNode>();
                node.ResourceId = "iron_ore";
                node.GatherSkill = SkillId.Mining;
                node.Remaining = 1;
                node.Capacity = 4;
                node.RespawnSeconds = 5f;
                node.EnsureCapacity();
                node.Remaining = 0;
                node.ReadyAt = 99999f;
                node.Tick(0f);
                if (node.Remaining != 0)
                    throw new InvalidOperationException("리스폰 전에 광맥이 차면 안 됩니다.");
                node.Tick(100000f);
                if (node.Remaining != 4)
                    throw new InvalidOperationException("리스폰 후 Capacity만큼 차야 합니다.");

                var world = OfflineWorld.Instance;
                if (world == null)
                    throw new InvalidOperationException("OfflineWorld 없음");
                var bushGo = new GameObject("ResinBush");
                bushGo.transform.position = Vector3.zero;
                var bush = bushGo.AddComponent<ResourceNode>();
                bush.ResourceId = SpellCast.Reagent;
                bush.GatherSkill = SkillId.Magery;
                bush.Remaining = 2;
                bush.Capacity = 2;
                bush.Difficulty = 8f;
                var herbBody = new GameObject("selfcheck-herb");
                herbBody.transform.position = Vector3.zero;
                var hb = herbBody.AddComponent<WorldBody>();
                hb.IsAvatar = true;
                herbBody.AddComponent<InventoryBag>();
                var herb = world.TryGather(hb, bush);
                if (!herb.Applied)
                    throw new InvalidOperationException("시약 채집 실패: " + herb.FailReason);
                var herbBag = herbBody.GetComponent<InventoryBag>();
                int resin = 0;
                for (int i = 0; i < herbBag.Items.Count; i++)
                    if (herbBag.Items[i].TemplateId == SpellCast.Reagent)
                        resin += herbBag.Items[i].Amount;
                if (resin < 1)
                    throw new InvalidOperationException("시약이 가방에 있어야 합니다.");
                if (world.SkillsOf(hb).Get(SkillId.Magery) < 0.09f)
                    throw new InvalidOperationException("시약 채집 후 마법이 올라야 합니다.");
                UnityEngine.Object.DestroyImmediate(bushGo);
                UnityEngine.Object.DestroyImmediate(herbBody);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(respawnGo);
            }

            var shopBody = new GameObject("selfcheck-shop");
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                    throw new InvalidOperationException("OfflineWorld 없음");
                var body = shopBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.Gold = 40;
                shopBody.AddComponent<InventoryBag>();
                var vendorGo = new GameObject("Vendor");
                vendorGo.transform.position = shopBody.transform.position;
                var vendor = vendorGo.AddComponent<VendorStation>();
                var open = world.TryVendor(body, vendor);
                if (!open.Applied)
                    throw new InvalidOperationException("상점 열기 실패");
                var buy = world.TryBuy(body, ItemCatalog.Pickaxe);
                if (!buy.Applied || body.Gold != 15)
                    throw new InvalidOperationException("곡괭이 구매 실패 gold=" + body.Gold);
                var bag = shopBody.GetComponent<InventoryBag>();
                if (bag.ToolUses(ItemCatalog.Pickaxe) <= 0)
                    throw new InvalidOperationException("산 곡괭이가 없음");
                bag.Add("iron_ore", 1);
                var sell = world.TrySell(body, "iron_ore");
                if (!sell.Applied || body.Gold != 17)
                    throw new InvalidOperationException("광석 판매 실패 gold=" + body.Gold);
                UnityEngine.Object.DestroyImmediate(vendorGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shopBody);
            }

            var trainGo = new GameObject("selfcheck-train");
            try
            {
                var world = OfflineWorld.Instance;
                var body = trainGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.Gold = 10;
                var tr = new GameObject("TrainerTmp");
                tr.transform.position = trainGo.transform.position;
                var station = tr.AddComponent<TrainerStation>();
                var open = world.TryTrainer(body, station);
                if (!open.Applied)
                    throw new InvalidOperationException("훈련사 열기 실패");
                int str0 = world.StatsOf(body).Str;
                var trained = world.TryTrain(body, SkillId.Mining);
                if (!trained.Applied || Math.Abs(world.SkillsOf(body).Get(SkillId.Mining) - 1f) > 0.01f || body.Gold != 5)
                    throw new InvalidOperationException("훈련 실패");
                if (world.StatsOf(body).Str != str0)
                    throw new InvalidOperationException("NPC 훈련은 STR을 올리면 안 됩니다.");
                world.SkillsOf(body).ForceSet(SkillId.Mining, 30f, SkillLock.Up);
                body.Gold = 20;
                var capped = world.TryTrain(body, SkillId.Mining);
                if (capped.Applied)
                    throw new InvalidOperationException("30 이상은 훈련되면 안 됩니다.");
                UnityEngine.Object.DestroyImmediate(tr);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(trainGo);
            }

            OpLog.Write("gm", "selfcheck", "test", "hello");
            string[] recent = OpLog.Recent(5);
            bool saw = false;
            for (int i = 0; i < recent.Length; i++)
                if (recent[i].IndexOf("hello", StringComparison.Ordinal) >= 0)
                    saw = true;
            if (!saw)
                throw new InvalidOperationException("운영 로그 기록이 실패했습니다.");
            OpLog.Freeze("selfcheck-ban", true);
            if (!OpLog.IsFrozen("selfcheck-ban"))
                throw new InvalidOperationException("계정 정지가 안 됩니다.");
            OpLog.Freeze("selfcheck-ban", false);
            if (OpLog.IsFrozen("selfcheck-ban"))
                throw new InvalidOperationException("계정 해제가 안 됩니다.");
            string bak = OpLog.Backup();
            if (string.IsNullOrEmpty(bak) || !Directory.Exists(bak))
                throw new InvalidOperationException("백업 실패");
            var gmGo = new GameObject("selfcheck-gm");
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                    throw new InvalidOperationException("OfflineWorld 없음");
                var body = gmGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                gmGo.AddComponent<InventoryBag>();
                var give = world.GmGive(body, "iron_ore", 2);
                if (!give.Applied)
                    throw new InvalidOperationException("GM 지급 실패");
                var set = world.GmSetSkill(body, SkillId.Swordsmanship, 12f);
                if (!set.Applied || Math.Abs(world.SkillsOf(body).Get(SkillId.Swordsmanship) - 12f) > 0.01f)
                    throw new InvalidOperationException("GM 스킬 수정 실패");
                var warp = world.GmWarpPlaza(body);
                if (!warp.Applied || Math.Abs(body.transform.position.x) > 0.2f)
                    throw new InvalidOperationException("GM 워프 실패");
                var skel = new GameObject("Skeleton");
                var sb = skel.AddComponent<WorldBody>();
                sb.IsEnemy = true;
                sb.MaxHp = 30f;
                sb.ResetHp();
                var spawn = world.GmSpawnSkeleton();
                if (!spawn.Applied || GameObject.Find("Skeleton_gm") == null)
                    throw new InvalidOperationException("GM 소환 실패");
                var gone = world.GmDespawnExtra();
                if (!gone.Applied)
                    throw new InvalidOperationException("GM 삭제 실패");
                UnityEngine.Object.DestroyImmediate(skel);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gmGo);
            }

            if (!GuardZone.Contains(0f, 0f) || GuardZone.Contains(20f, 0f))
                throw new InvalidOperationException("가드존 반경이 마을과 안 맞습니다.");
            var notoGo = new GameObject("selfcheck-noto");
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                    throw new InvalidOperationException("OfflineWorld 없음");
                notoGo.transform.position = Vector3.zero;
                var body = notoGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                world.SetLocalPlayer(body);
                var mob = new GameObject("selfcheck-mob");
                mob.transform.position = Vector3.zero;
                var mb = mob.AddComponent<WorldBody>();
                mb.IsEnemy = true;
                mb.MaxHp = 8f;
                mb.ResetHp();
                int fame0 = body.Fame;
                var hunt = world.TryAttack(body, mb);
                if (!hunt.Applied)
                    throw new InvalidOperationException("사냥 실패: " + hunt.FailReason);
                if (!mb.Alive && body.Fame < fame0 + 10)
                    throw new InvalidOperationException("처치 후 명성이 올라야 합니다.");
                UnityEngine.Object.DestroyImmediate(mob);
                var vic = new GameObject("selfcheck-innocent");
                vic.transform.position = Vector3.zero;
                var vb = vic.AddComponent<WorldBody>();
                vb.IsEnemy = false;
                vb.MaxHp = 40f;
                vb.ResetHp();
                float hp0 = body.Hp;
                var assault = world.TryAttack(body, vb);
                if (assault.Applied || assault.FailReason != "innocent")
                    throw new InvalidOperationException("무고 공격은 막혀야 합니다.");
                if (body.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("무고 공격 후 범죄가 되어야 합니다.");
                if (body.Hp >= hp0)
                    throw new InvalidOperationException("마을 가드가 범죄자를 쳐야 합니다.");
                UnityEngine.Object.DestroyImmediate(vic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(notoGo);
            }


            var carpBody = new GameObject("selfcheck-carp");
            GameObject carpWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    carpWorldGo = new GameObject("selfcheck-carp-world");
                    world = carpWorldGo.AddComponent<OfflineWorld>();
                }
                var body = carpBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "carp-mark";
                var bag = carpBody.AddComponent<InventoryBag>();
                bag.Add("wood", 2);
                var bench = new GameObject("selfcheck-bench");
                bench.transform.position = carpBody.transform.position;
                var station = bench.AddComponent<CraftStation>();
                station.RecipeId = "wooden_club";
                station.DisplayName = "목공소";
                var made = world.TryCraft(body, station);
                if (!made.Applied)
                    throw new InvalidOperationException("목공 제작 실패: " + made.FailReason);
                bool club = false;
                int woodLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenClub) club = true;
                    if (bag.Items[i].TemplateId == "wood") woodLeft += bag.Items[i].Amount;
                }
                if (!club || woodLeft != 0)
                    throw new InvalidOperationException("나무 2 → 나무곤봉 1이어야 합니다.");
                bool clubMark = false;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenClub && bag.Items[i].MakerId == "carp-mark")
                        clubMark = true;
                if (!clubMark)
                    throw new InvalidOperationException("목공 제작품 Maker Mark가 있어야 합니다.");
                var clubRecipe = CraftRecipes.Find("wooden_club");
                var bowRecipe = CraftRecipes.Find("wooden_bow");
                if (clubRecipe == null || !clubRecipe.CanRepair || bowRecipe == null || !bowRecipe.CanRepair)
                    throw new InvalidOperationException("목공소 레시피는 수리 가능해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Carpentry) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("목공 제작 후 0.1이어야 합니다.");
                bag.Add("wood", 3);
                var bowMade = world.TryCraft(body, station, "wooden_bow");
                if (!bowMade.Applied)
                    throw new InvalidOperationException("나무활 제작 실패: " + bowMade.FailReason);
                bool bow = false;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenBow)
                        bow = true;
                if (!bow)
                    throw new InvalidOperationException("나무 3 → 나무활 1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Carpentry) - 0.2f) > 0.0001f)
                    throw new InvalidOperationException("나무활 제작 후 목공 0.2이어야 합니다.");
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.WoodenBow)
                        continue;
                    if (bag.Items[i].MakerId != "carp-mark")
                        throw new InvalidOperationException("나무활 Maker Mark가 있어야 합니다.");
                    var wornBow = bag.Items[i];
                    wornBow.Uses = 8;
                    bag.Items[i] = wornBow;
                    break;
                }
                bag.Add("wood", 1);
                var bowFix = world.TryCraft(body, station, "wooden_bow");
                if (!bowFix.Applied)
                    throw new InvalidOperationException("나무활 수리 실패: " + bowFix.FailReason);
                bool bowRestored = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.WoodenBow)
                        continue;
                    if (bag.Items[i].Uses < 18)
                        throw new InvalidOperationException("목공소 수리 후 내구가 올라야 합니다.");
                    if (bag.Items[i].MakerId != "carp-mark")
                        throw new InvalidOperationException("목공소 수리는 Maker Mark를 지우면 안 됩니다.");
                    bowRestored = true;
                }
                if (!bowRestored)
                    throw new InvalidOperationException("수리 대상 나무활이 없습니다.");

                var dummy = new GameObject("selfcheck-arch-skel");
                try
                {
                    var skel = dummy.AddComponent<WorldBody>();
                    skel.IsEnemy = true;
                    skel.MaxHp = 40f;
                    skel.ResetHp();
                    dummy.transform.position = carpBody.transform.position + new Vector3(0f, 0f, 6.5f);
                    var shot = world.TryAttack(body, skel);
                    if (!shot.Applied)
                        throw new InvalidOperationException("활 원거리 공격 실패: " + shot.FailReason);
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Archery) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("활 공격 후 궁술 0.1이어야 합니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("활 공격 후 전술 0.1이어야 합니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("활 공격 후 해부학 0.1이어야 합니다.");
                    if (skel.Hp >= 40f)
                        throw new InvalidOperationException("활 공격이 피해를 줘야 합니다.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummy);
                }
                UnityEngine.Object.DestroyImmediate(bench);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(carpBody);
                if (carpWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(carpWorldGo);
            }

            int dmgOpen = AttackResolve.RetaliationDamage;
            var noShieldSkills = new SkillSet();
            if (AttackResolve.TryParry(noShieldSkills, new StatSet(), false, 20f, ref dmgOpen, out _, out _))
                throw new InvalidOperationException("방패 없이 막기가 되면 안 됩니다.");
            if (dmgOpen != AttackResolve.RetaliationDamage || Math.Abs(noShieldSkills.Get(SkillId.Parrying)) > 0.0001f)
                throw new InvalidOperationException("방패 없는 반격은 방패술을 올리면 안 됩니다.");

            int dmgBlock = AttackResolve.RetaliationDamage;
            var shieldSkills = new SkillSet();
            var parryStats = new StatSet();
            int dexWas = parryStats.Dex;
            if (!AttackResolve.TryParry(shieldSkills, parryStats, true, 20f, ref dmgBlock, out float parryBefore, out float parryAfter))
                throw new InvalidOperationException("방패 막기가 들어가야 합니다.");
            if (Math.Abs(parryBefore) > 0.0001f || Math.Abs(parryAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException($"방패술 0.0→0.1이어야 합니다. 실제 {parryBefore}→{parryAfter}");
            if (Math.Abs(shieldSkills.Get(SkillId.Parrying) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("SkillSet 방패술이 0.1이어야 합니다.");
            if (dmgBlock >= AttackResolve.RetaliationDamage)
                throw new InvalidOperationException("방패가 피해를 줄여야 합니다.");
            if (parryStats.Dex != dexWas + 1)
                throw new InvalidOperationException("방패술 상승 시 DEX가 올라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Parrying) != StatId.Dex)
                throw new InvalidOperationException("방패술 Primary는 DEX이어야 합니다.");

            var parryLocked = new SkillSet();
            parryLocked.SetLock(SkillId.Parrying, SkillLock.Locked);
            int dmgLocked = AttackResolve.RetaliationDamage;
            AttackResolve.TryParry(parryLocked, null, true, 20f, ref dmgLocked, out _, out _);
            if (Math.Abs(parryLocked.Get(SkillId.Parrying)) > 0.0001f)
                throw new InvalidOperationException("잠긴 방패술은 오르면 안 됩니다.");

            var parryGo = new GameObject("selfcheck-parry");
            GameObject parryWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    parryWorldGo = new GameObject("selfcheck-parry-world");
                    world = parryWorldGo.AddComponent<OfflineWorld>();
                }
                var body = parryGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                var bag = parryGo.AddComponent<InventoryBag>();
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.WoodenShield, Amount = 1, Uses = 30 });
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });
                var dummy = new GameObject("selfcheck-parry-skel");
                try
                {
                    var skel = dummy.AddComponent<WorldBody>();
                    skel.IsEnemy = true;
                    skel.MaxHp = 40f;
                    skel.ResetHp();
                    dummy.transform.position = parryGo.transform.position;
                    float hp0 = body.Hp;
                    var swung = world.TryAttack(body, skel);
                    if (!swung.Applied)
                        throw new InvalidOperationException("방패 근접 공격 실패: " + swung.FailReason);
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Parrying) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("근접 반격 후 방패술 0.1이어야 합니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("방패 루프가 검술 상승을 막으면 안 됩니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("방패 루프가 전술 상승을 막으면 안 됩니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("방패 루프가 해부학 상승을 막으면 안 됩니다.");
                    if (body.Hp >= hp0)
                        throw new InvalidOperationException("반격 피해가 있어야 합니다.");
                    if (body.Hp <= hp0 - AttackResolve.RetaliationDamage)
                        throw new InvalidOperationException("방패가 반격 피해를 줄여야 합니다.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummy);
                }

                var bare = new GameObject("selfcheck-parry-bare");
                try
                {
                    var b2 = bare.AddComponent<WorldBody>();
                    b2.IsAvatar = true;
                    b2.MaxHp = 50f;
                    b2.ResetHp();
                    var bag2 = bare.AddComponent<InventoryBag>();
                    bag2.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });
                    var dummy2 = new GameObject("selfcheck-parry-skel2");
                    var skel2 = dummy2.AddComponent<WorldBody>();
                    skel2.IsEnemy = true;
                    skel2.MaxHp = 40f;
                    skel2.ResetHp();
                    dummy2.transform.position = bare.transform.position;
                    float hpBare = b2.Hp;
                    var swung2 = world.TryAttack(b2, skel2);
                    if (!swung2.Applied)
                        throw new InvalidOperationException("무방패 근접 실패: " + swung2.FailReason);
                    if (Math.Abs(world.SkillsOf(b2).Get(SkillId.Parrying)) > 0.0001f)
                        throw new InvalidOperationException("방패 없이 방패술이 오르면 안 됩니다.");
                    if (Math.Abs(b2.Hp - (hpBare - AttackResolve.RetaliationDamage)) > 0.01f)
                        throw new InvalidOperationException("무방패 반격은 피해 " + AttackResolve.RetaliationDamage + "이어야 합니다.");
                    UnityEngine.Object.DestroyImmediate(dummy2);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bare);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parryGo);
                if (parryWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(parryWorldGo);
            }

            if (CraftRecipes.Find("bandage") == null || CraftRecipes.Find("bandage").Output != ItemCatalog.Bandage)
                throw new InvalidOperationException("붕대 레시피가 카탈로그에 있어야 합니다.");
            if (ItemCatalog.BuyPrice(ItemCatalog.Bandage) <= 0 || ItemCatalog.WeightOf(ItemCatalog.Bandage) <= 0f)
                throw new InvalidOperationException("붕대 카탈로그 항목이 없습니다.");
            if (StatSet.PrimaryOf(SkillId.Healing) != StatId.Dex)
                throw new InvalidOperationException("치유 Primary는 DEX이어야 합니다.");

            var noBandage = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 1f,
                Skills = new SkillSet(),
                HasBandage = false,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (noBandage.Applied)
                throw new InvalidOperationException("붕대 없이 치유되면 안 됩니다.");

            var fullHp = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 1f,
                Skills = new SkillSet(),
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 50f,
                TargetMaxHp = 50f
            });
            if (fullHp.Applied)
                throw new InvalidOperationException("만피면 치유되면 안 됩니다.");

            var farHeal = HealResolve.Resolve(new HealRequest
            {
                Distance = 9f,
                Now = 1f,
                Skills = new SkillSet(),
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (farHeal.Applied)
                throw new InvalidOperationException("사거리 밖 붕대는 들어가면 안 됩니다.");

            var healSkills = new SkillSet();
            var healStats = new StatSet();
            int healDexWas = healStats.Dex;
            var healed = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 1f,
                Skills = healSkills,
                Stats = healStats,
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f,
                Difficulty = HealResolve.Difficulty
            });
            if (!healed.Applied || healed.Damage < HealResolve.BaseHeal)
                throw new InvalidOperationException("붕대 치유가 들어가야 합니다.");
            if (Math.Abs(healed.SkillBefore) > 0.0001f || Math.Abs(healed.SkillAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException($"치유 0.0→0.1이어야 합니다. 실제 {healed.SkillBefore}→{healed.SkillAfter}");
            if (Math.Abs(healSkills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("SkillSet 치유가 0.1이어야 합니다.");
            if (healStats.Dex != healDexWas + 1)
                throw new InvalidOperationException("치유 상승 시 DEX가 올라야 합니다.");
            if (Math.Abs(healSkills.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("붕대는 해부학을 올리면 안 됩니다.");

            var anaBoost = new SkillSet();
            anaBoost.ForceSet(SkillId.Anatomy, 40f, SkillLock.Up);
            int plain = HealResolve.Amount(new SkillSet(), new StatSet());
            int boosted = HealResolve.Amount(anaBoost, new StatSet());
            if (boosted <= plain)
                throw new InvalidOperationException("해부학이 붕대 치유량에 반영되어야 합니다.");

            var healLock = new SkillSet();
            healLock.SetLock(SkillId.Healing, SkillLock.Locked);
            var lockedHeal = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 2f,
                Skills = healLock,
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (!lockedHeal.Applied)
                throw new InvalidOperationException("잠긴 치유도 치료는 되어야 합니다.");
            if (Math.Abs(healLock.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 치유는 오르면 안 됩니다.");

            var healCreate = CharacterCreate.Build("heal-check", "치료", 0, 20, 40, 20,
                new[] { SkillId.Healing, SkillId.Anatomy, SkillId.Tailoring },
                new[] { 50f, 30f, 20f });
            bool hasBandageStart = false, hasClothStart = false;
            for (int i = 0; i < healCreate.Inventory.Length; i++)
            {
                if (healCreate.Inventory[i].TemplateId == ItemCatalog.Bandage && healCreate.Inventory[i].Amount >= 10)
                    hasBandageStart = true;
                if (healCreate.Inventory[i].TemplateId == ItemCatalog.Cloth && healCreate.Inventory[i].Amount >= 4)
                    hasClothStart = true;
            }
            if (!hasBandageStart)
                throw new InvalidOperationException("치유 시작은 붕대를 줘야 합니다.");
            if (!hasClothStart)
                throw new InvalidOperationException("재봉 시작은 천을 줘야 합니다.");

            var healGo = new GameObject("selfcheck-heal");
            GameObject healWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    healWorldGo = new GameObject("selfcheck-heal-world");
                    world = healWorldGo.AddComponent<OfflineWorld>();
                }
                var body = healGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                var bag = healGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Cloth, 1);
                var bench = new GameObject("selfcheck-tailor");
                bench.transform.position = healGo.transform.position;
                var station = bench.AddComponent<CraftStation>();
                station.RecipeId = "bandage";
                station.DisplayName = "재봉";
                var made = world.TryCraft(body, station);
                if (!made.Applied)
                    throw new InvalidOperationException("붕대 제작 실패: " + made.FailReason);
                bool hasBn = false;
                int clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage) hasBn = true;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth) clothLeft += bag.Items[i].Amount;
                }
                if (!hasBn || clothLeft != 0)
                    throw new InvalidOperationException("천 1 → 붕대 1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tailoring) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("붕대 제작 후 재봉 0.1이어야 합니다.");

                body.SetHp(20f);
                var none = world.TryHeal(body, body);
                if (!none.Applied)
                    throw new InvalidOperationException("자가 붕대 실패: " + none.FailReason);
                if (body.Hp <= 20f)
                    throw new InvalidOperationException("붕대가 HP를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 치유 후 0.1이어야 합니다.");
                int leftBn = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        leftBn += bag.Items[i].Amount;
                if (leftBn != 0)
                    throw new InvalidOperationException("성공 치유는 붕대를 소모해야 합니다.");

                var noBn = world.TryHeal(body, body);
                if (noBn.Applied)
                    throw new InvalidOperationException("붕대 소진 후 치유되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("실패한 치유는 스킬을 올리면 안 됩니다.");

                var palHealerGo = new GameObject("selfcheck-heal-other");
                palHealerGo.transform.position = healGo.transform.position;
                var palHealer = palHealerGo.AddComponent<WorldBody>();
                palHealer.IsAvatar = true;
                palHealer.MaxHp = 50f;
                palHealer.ResetHp();
                var palBag = palHealerGo.AddComponent<InventoryBag>();
                palBag.Add(ItemCatalog.Bandage, 2);
                var pal = new GameObject("selfcheck-heal-pal");
                pal.transform.position = healGo.transform.position;
                var pb = pal.AddComponent<WorldBody>();
                pb.IsEnemy = false;
                pb.MaxHp = 40f;
                pb.ResetHp();
                pb.SetHp(15f);
                var palHeal = world.TryHeal(palHealer, pb);
                if (!palHeal.Applied || pb.Hp <= 15f)
                    throw new InvalidOperationException("아군 붕대 실패: " + palHeal.FailReason);

                var foe = new GameObject("selfcheck-heal-foe");
                foe.transform.position = healGo.transform.position;
                var fb = foe.AddComponent<WorldBody>();
                fb.IsEnemy = true;
                fb.MaxHp = 30f;
                fb.ResetHp();
                fb.SetHp(10f);
                var foeHeal = world.TryHeal(palHealer, fb);
                if (foeHeal.Applied)
                    throw new InvalidOperationException("적에게 붕대하면 안 됩니다.");
                UnityEngine.Object.DestroyImmediate(pal);
                UnityEngine.Object.DestroyImmediate(foe);
                UnityEngine.Object.DestroyImmediate(palHealerGo);
                UnityEngine.Object.DestroyImmediate(bench);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(healGo);
                if (healWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(healWorldGo);
            }

            AssertMeditationSlice();
            AssertMagicResistSlice();
            AssertEvalIntSlice();
            AssertBolt();
            AssertFishingSlice();
            AssertCookingSlice();
            AssertFencingSlice();
            AssertMaceSlice();
            AssertAlchemySlice();
            AssertInscription();
            AssertPoisoning();
            AssertTrackingSlice();
            AssertMusicianshipSlice();
            AssertPeacemakingSlice();
            AssertProvocationSlice();
            AssertHidingSlice();
            AssertStealthSlice();
            AssertDetectHiddenSlice();
            AssertCamping();
            AssertStealing();
            AssertHealingResurrect();
            AssertBandageDetox();
            AssertLockpickingSlice();
            AssertAnimalLoreSlice();
            AssertVeterinarySlice();
            AssertHousingSlice();
            AssertTamingSlice();
            AssertPetCommands();
            AssertPetAttack();
            AssertPetCome();
            AssertPetBondVetRez();
            AssertStrengthRequirement();
            AssertOverweight();
            AssertMeditationArmorPenalty();
            AssertCastInterrupt();
            AssertCleanse();
            AssertWard();
            AssertBind();
            AssertWeaken();
            AssertSpark();
            AssertRestore();
            AssertBlink();
            AssertBless();
            AssertControlSlots();
            AssertNestedBag();
            AssertGroundDecay();
            AssertStableSlice();
            AssertTravelSlice();
            AssertMarkRecall();
            AssertOpenPvpSlice();
            AssertSkillTitleSlice();
            AssertReputationTitle();
            AssertKeywordSpeech();
            AssertEastFieldSlice();
            AssertSouthFieldSlice();
            AssertNorthFieldSlice();
            AssertDungeon1Slice();
            AssertDungeon2Slice();
            AssertFieldBossSlice();
            AssertGuildSlice();
            AssertGuildWar();
            AssertDuel();
            AssertExceptional();
            AssertCraftOrder();
            Debug.Log("[Ulon] Slice self-check PASS — 몬스터 8종(스켈레톤+도적+야만인+자객+기사+주술사+졸병+해골도적), 검술/채광/제작, 목공, 궁술/나무활, 전술 0.0→0.1, 방패술 0.0→0.1, 해부학 0.0→0.1, 치유 붕대 0.0→0.1, 명상 마나 0.0→0.1, 마법 저항 0.0→0.1, 지능 평가 0.0→0.1, 낚시 0.0→0.1, 요리 0.0→0.1, 창술/나무창 0.0→0.1, 둔기술/나무곤봉 0.0→0.1, 연금술/회복물약 0.0→0.1, 각인 1(TryInscribe 천/blank+불씨 주문→scroll_ember 0.0→0.1, 주문서 1회 불씨 후 소모, 마법/연금술과 별개), 독 1(TryPoisonWeapon 연금 물약/천 독병 근접무기 도포 0.0→0.1, 다음 TryAttack 짧은 HP 틱, 마법/연금술/수의학과 별개), 추적 0.0→0.1, 음악/류트 0.0→0.1, 평화 0.0→0.1, 도발 0.0→0.1, 은신 0.0→0.1, 잠행 0.0→0.1, 자물쇠따기 0.0→0.1, 동물지식 0.0→0.1, 수의학 0.0→0.1, 하우징 지정 부지 1(가드존 밖 claim/lockdown/secure), 플레이어 상점 1(Public House Vendor Slot 가방 1개 골드 구매), 조련 1(야생하트 follow/release, AnimalTaming 0.0→0.1), 펫 명령 Stay/Guard/Attack/Come 1(TryPetAttack 근처 몹 추격·공격, TryPetCome 공격해제·Follow·주인으로 이동, 아바타 Open PvP 없음), 마구간 1(마을 Stable Master TryStable/TryClaimStable, 골드 2), 여행 1(공개 문게이트 광장 워프), Mark/Recall 1(한 슬롯 필드 기록·귀환, 골드 5, 문게이트는 광장), 동쪽 필드 FieldOak 벌목, 남쪽 필드 FieldFlax 재봉 채집, 북쪽 필드 FieldOre 채광, 던전 1 서쪽 입구/내부 스텁 입장·퇴장+내부 스켈레톤 1+본워든(HP 120, warden_crest), 던전 2 동쪽 입구/내부 스텁 입장·퇴장+내부 도적 1+섀도우캡틴(KayKit Rogue HP 150, captain_sigil), 보스 3 동쪽 필드 아웃라이어 헥사크(KayKit Mage HP 180, hex_seal), 길드 1(창설 골드25·이름1~12·초대/수락·GuildId/GuildName 공유·탈퇴, 파티와 별개, HUD 태그), 길드전 1(TryGuildWarDeclare 필드 합의 PvP·무고 유지·가드존 차단, TryGuildWarPeace, Open PvP와 별개), 결투 1(TryDuelInvite/Accept 필드 합의 PvP·무고 유지·가드존 차단, yield/death/TryDuelEnd, Guild War·Open PvP와 별개), 벼락 1(마법 0.0→0.1, 불씨보다 사거리·피해), Exceptional 1(TryCraft 롤/Force·seed, 플래그+내구/피해, MakerId 별개), 은신 감지 1(TryDetectHidden DEX, 은신 대상 해제 0.0→0.1, 은신/잠행과 별개), 야영 1(TryCamp 화덕 근처 또는 나무 불씨, Camping 0.0→0.1, CampSafeUntil 안전로그아웃, 요리/은신과 별개), 도둑질 1(TrySteal 마을 LockedCrate 팩, 최저가 골드/천 1, Stealing 0.0→0.1, 가드존/목격 실패→Criminal, 자물쇠따기/플레이어가방 아님), 붕대 부활 1(TryResurrectBandage 아바타 Ghost·근접·붕대1, Healing 0.0→0.1, HealerStation TryResurrect 유지), Strength Requirement 1(iron_sword StrReq 25·TryEquip 저STR 실패/고STR 성공, catalog-only, AssertStrengthRequirement), 중갑 명상 패널티 1(iron_plate HeavyArmor·명상 틱 마나 회복 ½, AssertMeditationArmorPenalty), 시전 중단 1(Bolt CastingUntil 풍업·TryAttack Applied 피격 취소·효과 없음·마나 소모 유지, AssertCastInterrupt), 정화 1(SpellId.Cleanse 즉시·자가/근처 아바타 독 틱 해제·마나/시약 Ember급·Magery 0.0→0.1, AssertCleanse), 붕대 해독 1(TryCurePoison 독 틱·생존·붕대1·근접, PoisonTicks 해제, Healing 0.0→0.1, Magery Cleanse/Veterinary/rez 아님, AssertBandageDetox), Bonded Pet+Veterinary 부활 1(조련 시 Bonded, HP0→pet Ghost·슬롯유지·시체룻없음, TryVetResurrect 붕대1·Veterinary 0.0→0.1, AssertPetBondVetRez), Weight/과적 1(CarryCap=STR*4, 가방+아이템>한도 시 TryGather/TryBuy/TryCraft 실패·명확 메시지, AssertOverweight), 수호 1(SpellId.Ward 즉시·자가 WardUntil~8s·TryAttack 피해×0.5·마나/시약 Ember급·Magery 0.0→0.1, AssertWard), 속박 1(SpellId.Bind 즉시·근처 적 몹 RootUntil~4s·추격/이동·반격 불가·마나/시약 Ember급·Magery 0.0→0.1, AssertBind), 약화 1(SpellId.Weaken 즉시·근처 적 몹 WeakenUntil~6s·출격 TryAttack/strike 피해×0.5·마나/시약 Ember급·Magery 0.0→0.1, AssertWeaken), 섬광 1(SpellId.Spark 즉시·근처 적 몹 짧은 사거리·불씨보다 낮은 피해·마나/시약 Ember급·Magery 0.0→0.1, AssertSpark), 회복 1(SpellId.Restore 즉시·자가/근처 아군 아바타 HP 회복·봉합보다 높음·마나/시약 봉합보다 약간 높음·Magery 0.0→0.1, AssertRestore), 도약 1(SpellId.Blink 즉시·자가 전방 3.5m 단거리 텔레포트·마나/시약 Ember급·전투/유령 실패·Magery 0.0→0.1, AssertBlink), 축복 1(SpellId.Bless 즉시·자가/근처 아군 BlessUntil~8s·출격 TryAttack 피해×1.25·마나/시약 Ember급·Magery 0.0→0.1, AssertBless), Nested Container 1(pouch parent_container_id·backpack→pouch→item depth1·TryMoveToPouch/TryTakeFromPouch·무게 합산, AssertNestedBag), Ground Drop 1(월드 GroundItem DecayAt·TickGroundItems 만료 삭제, 집 Lockdown/secure 예외, AssertGroundDecay), Reputation Title 1(Murderer→살인자/Criminal→범죄자/Fame≥100→유명인, HUD 이름 옆 SkillTitles와 별개, AssertReputationTitle), Keyword Speech 1(TrySpeechKeyword bank/은행·guards/경비·vendor/상점, 기존 Banker/GuardStrike/Vendor 배선, AssertKeywordSpeech), Follower Control Slots 1(MaxControlSlots=2·하트/멧돼지 cost1·둘 OK 셋째 no_slot·release/stable 해제, AssertControlSlots), CraftOrder/제작의뢰 1(Forge/Vendor TryAcceptOrder·TryTurnInOrder·직접 제작 iron_sword 1·골드10·한 건, AssertCraftOrder), leftover 던전3, STR/HP, 700캡↓, 은행, 캐릭터 생성, 주문책, 시체/부활, 무게/도구, 리스폰/시약, 상점, 훈련, 운영툴, 명성/가드존, 내구도/수리/Maker Mark, 직업명/숙련 칭호, 야외 Open PvP 1(가드존 밖 아바타, 마을 가드존은 기존)");
        }






    }
}
