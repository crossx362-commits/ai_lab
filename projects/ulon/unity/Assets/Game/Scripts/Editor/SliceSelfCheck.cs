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
    public static class SliceSelfCheck
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

            var rogue = GameObject.Find("Rogue");
            var rogueBody = rogue != null ? rogue.GetComponent<WorldBody>() : null;
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


        static void AssertFieldBossSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3은 두지 않습니다.");
            var boss = GameObject.Find(FieldBoss.Object);
            var bossBody = boss != null ? boss.GetComponent<WorldBody>() : null;
            if (bossBody == null || bossBody.MobId != MobCatalog.Hexarch || !bossBody.IsEnemy)
                throw new InvalidOperationException("동쪽 필드 아웃라이어에 서버 권한 네임드 엘리트 헥사크가 있어야 합니다.");
            if (bossBody.DisplayName != "헥사크" || Math.Abs(bossBody.MaxHp - 180f) > 0.0001f)
                throw new InvalidOperationException("헥사크는 이름=헥사크, HP=180이어야 합니다.");
            if (boss.GetComponent<NetworkObject>() == null || boss.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("헥사크 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (Math.Abs(boss.transform.position.x - FieldBoss.X) > 0.8f || Math.Abs(boss.transform.position.z - FieldBoss.Z) > 0.8f)
                throw new InvalidOperationException("헥사크는 동쪽 필드 아웃라이어 표시 위치에 있어야 합니다.");
            if (GuardZone.Contains(boss.transform.position.x, boss.transform.position.z))
                throw new InvalidOperationException("헥사크는 가드존 밖이어야 합니다.");
            var huntLine = GameObject.Find("SkelRogue");
            if (huntLine != null && Vector3.Distance(boss.transform.position, huntLine.transform.position) < 8f)
                throw new InvalidOperationException("헥사크는 사냥 라인과 겹치면 안 됩니다.");
            var d1Boss = GameObject.Find(Dungeon1.BossObject);
            if (d1Boss != null && Vector3.Distance(boss.transform.position, d1Boss.transform.position) < 40f)
                throw new InvalidOperationException("헥사크는 던전 1 본워든 내부가 아니어야 합니다.");
            var d2Boss = GameObject.Find(Dungeon2.BossObject);
            if (d2Boss != null && Vector3.Distance(boss.transform.position, d2Boss.transform.position) < 40f)
                throw new InvalidOperationException("헥사크는 던전 2 섀도우캡틴 내부가 아니어야 합니다.");
            var oak = GameObject.Find("FieldOak");
            if (oak == null)
                throw new InvalidOperationException("동쪽 FieldOak가 유지되어야 합니다.");
            if (Vector3.Distance(boss.transform.position, oak.transform.position) > 12f)
                throw new InvalidOperationException("헥사크는 동쪽 필드 아웃라이어여야 합니다.");
            var cc = boss.GetComponent<CharacterController>();
            if (cc == null || Math.Abs(cc.height - 2.48f) > 0.05f)
                throw new InvalidOperationException("헥사크는 KayKit Mage를 본워든/섀도우캡틴과 다른 키로 써야 합니다.");

            var worldGo = new GameObject("selfcheck-fieldboss-world");
            try
            {
                var world = worldGo.AddComponent<OfflineWorld>();
                var eliteGo = new GameObject("selfcheck-field-boss");
                eliteGo.transform.position = boss.transform.position;
                var elite = eliteGo.AddComponent<WorldBody>();
                elite.IsEnemy = true;
                elite.MobId = MobCatalog.Hexarch;
                elite.ApplyMobCatalog();
                elite.ResetHp();
                if (Math.Abs(elite.MaxHp - 180f) > 0.0001f || elite.DisplayName != "헥사크")
                    throw new InvalidOperationException("헥사크 카탈로그 HP/이름이 서버에 적용되어야 합니다.");
                var netGo = new GameObject("selfcheck-server-boss3");
                var netBody = netGo.AddComponent<WorldBody>();
                netBody.MobId = MobCatalog.Hexarch;
                var netMob = netGo.AddComponent<NetMob>();
                netMob.OnStartServer();
                if (netBody.DisplayName != "헥사크" || Math.Abs(netBody.MaxHp - 180f) > 0.0001f || Math.Abs(netBody.Hp - 180f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 헥사크 카탈로그와 HP를 권위 있게 적용해야 합니다.");
                UnityEngine.Object.DestroyImmediate(netGo);
                var slayerGo = new GameObject("selfcheck-field-slayer");
                slayerGo.transform.position = eliteGo.transform.position;
                var slayer = slayerGo.AddComponent<WorldBody>();
                slayer.IsAvatar = true;
                slayer.MaxHp = 50f;
                slayer.ResetHp();
                var bag = slayerGo.AddComponent<InventoryBag>();
                elite.ApplyDamage((int)elite.MaxHp - 1);
                var slay = world.TryAttack(slayer, elite);
                if (!slay.Applied)
                    throw new InvalidOperationException("헥사크 처치 실패: " + slay.FailReason);
                if (elite.Alive)
                    throw new InvalidOperationException("헥사크가 죽어야 합니다.");
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.HexSeal))
                    throw new InvalidOperationException("헥사크는 헥스 인장(hex_seal)을 서버가 지급해야 합니다.");
                if (ItemCatalog.Has(bag.Items, ItemCatalog.WardenCrest) || ItemCatalog.Has(bag.Items, ItemCatalog.CaptainSigil))
                    throw new InvalidOperationException("헥사크는 본워든/섀도우캡틴 드랍을 주면 안 됩니다.");
                UnityEngine.Object.DestroyImmediate(eliteGo);
                UnityEngine.Object.DestroyImmediate(slayerGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertDungeon1Slice()
        {
            var entrance = GameObject.Find(Dungeon1.EntranceObject);
            var interior = GameObject.Find(Dungeon1.InteriorObject);
            var exitGo = GameObject.Find(Dungeon1.ExitObject);
            var mob = GameObject.Find(Dungeon1.MobObject);
            if (entrance == null || interior == null || exitGo == null)
                throw new InvalidOperationException("던전 1은 입구+내부+출구가 있어야 합니다.");
            var eg = entrance.GetComponent<DungeonGate>();
            var xg = exitGo.GetComponent<DungeonGate>();
            if (eg == null || xg == null || eg.IsExit || !xg.IsExit || eg.DungeonId != Dungeon1.Id || xg.DungeonId != Dungeon1.Id)
                throw new InvalidOperationException("던전 1 게이트는 서버 DungeonGate 입장/퇴장이어야 합니다.");
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3은 두지 않습니다.");
            var d2e = GameObject.Find(Dungeon2.EntranceObject);
            if (d2e != null && Vector3.Distance(entrance.transform.position, d2e.transform.position) < 12f)
                throw new InvalidOperationException("던전 1 서쪽 입구와 던전 2 입구는 떨어져 있어야 합니다.");
            var hunt = GameObject.Find("Raider");
            if (hunt != null && Vector3.Distance(entrance.transform.position, hunt.transform.position) < 8f)
                throw new InvalidOperationException("던전 입구가 사냥 라인을 건드리면 안 됩니다.");
            var field = GameObject.Find("EastField");
            if (field != null && Vector3.Distance(entrance.transform.position, field.transform.position) < 12f)
                throw new InvalidOperationException("던전 입구가 동쪽 필드를 건드리면 안 됩니다.");
            var south = GameObject.Find("SouthField");
            if (south != null && Vector3.Distance(entrance.transform.position, south.transform.position) < 12f)
                throw new InvalidOperationException("던전 입구가 남쪽 필드를 건드리면 안 됩니다.");
            var north = GameObject.Find("NorthField");
            if (north != null && Vector3.Distance(entrance.transform.position, north.transform.position) < 12f)
                throw new InvalidOperationException("던전 입구가 북쪽 필드를 건드리면 안 됩니다.");
            var body = mob != null ? mob.GetComponent<WorldBody>() : null;
            if (body == null || body.MobId != MobCatalog.Skeleton || !body.IsEnemy)
                throw new InvalidOperationException("던전 내부에는 카탈로그 스켈레톤 1종이 있어야 합니다.");
            if (mob.GetComponent<NetworkObject>() == null || mob.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("던전 몹 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            var boss = GameObject.Find(Dungeon1.BossObject);
            var bossBody = boss != null ? boss.GetComponent<WorldBody>() : null;
            if (bossBody == null || bossBody.MobId != MobCatalog.BoneWarden || !bossBody.IsEnemy)
                throw new InvalidOperationException("던전 1 내부에 서버 권한 네임드 엘리트 본워든이 있어야 합니다.");
            if (bossBody.DisplayName != "본워든" || Math.Abs(bossBody.MaxHp - 120f) > 0.0001f)
                throw new InvalidOperationException("본워든은 이름=본워든, HP=120이어야 합니다.");
            if (boss.GetComponent<NetworkObject>() == null || boss.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("본워든 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (Math.Abs(boss.transform.position.x - Dungeon1.BossX) > 0.8f || Math.Abs(boss.transform.position.z - Dungeon1.BossZ) > 0.8f)
                throw new InvalidOperationException("본워든은 던전 1 내부 표시 위치에 있어야 합니다.");
            var huntLine = GameObject.Find("SkelRogue");
            if (huntLine != null && Vector3.Distance(boss.transform.position, huntLine.transform.position) < 40f)
                throw new InvalidOperationException("본워든은 사냥 라인 아웃라이어가 아니라 던전 1 내부여야 합니다.");
            var cc = boss.GetComponent<CharacterController>();
            if (cc == null || Math.Abs(cc.height - 2.25f) > 0.05f)
                throw new InvalidOperationException("본워든은 KayKit 스켈레톤을 1.4배 키로 써야 합니다.");

            var worldGo = new GameObject("selfcheck-dungeon-world");
            GameObject avatarGo = null;
            GameObject enemyGo = null;
            try
            {
                var world = worldGo.AddComponent<OfflineWorld>();
                avatarGo = new GameObject("selfcheck-dungeon-avatar");
                avatarGo.transform.position = new Vector3(0f, 0.1f, 0f);
                var avatar = avatarGo.AddComponent<WorldBody>();
                avatar.IsAvatar = true;
                avatar.MaxHp = 50f;
                avatar.ResetHp();
                var far = world.TryDungeon(avatar, eg);
                if (far.Applied)
                    throw new InvalidOperationException("입구 사거리 밖 입장이 들어가면 안 됩니다.");
                avatarGo.transform.position = entrance.transform.position;
                var enter = world.TryDungeon(avatar, eg);
                if (!enter.Applied)
                    throw new InvalidOperationException("던전 입장 실패: " + enter.FailReason);
                float dx = avatarGo.transform.position.x - Dungeon1.InteriorX;
                float dz = avatarGo.transform.position.z - Dungeon1.InteriorZ;
                if (dx * dx + dz * dz > 4f)
                    throw new InvalidOperationException("입장은 서버가 내부 스폰으로 워프해야 합니다.");

                enemyGo = new GameObject("selfcheck-dungeon-enemy");
                enemyGo.transform.position = avatarGo.transform.position + new Vector3(0.8f, 0f, 0f);
                var enemy = enemyGo.AddComponent<WorldBody>();
                enemy.IsEnemy = true;
                enemy.MobId = MobCatalog.Skeleton;
                enemy.ApplyMobCatalog();
                enemy.ResetHp();
                var hit = world.TryAttack(avatar, enemy);
                if (!hit.Applied)
                    throw new InvalidOperationException("던전 내부 전투 실패: " + hit.FailReason);

                var eliteGo = new GameObject("selfcheck-dungeon-boss");
                eliteGo.transform.position = avatarGo.transform.position + new Vector3(0.8f, 0f, 0f);
                var elite = eliteGo.AddComponent<WorldBody>();
                elite.IsEnemy = true;
                elite.MobId = MobCatalog.BoneWarden;
                elite.ApplyMobCatalog();
                elite.ResetHp();
                if (Math.Abs(elite.MaxHp - 120f) > 0.0001f || elite.DisplayName != "본워든")
                    throw new InvalidOperationException("본워든 카탈로그 HP/이름이 서버에 적용되어야 합니다.");
                var netGo = new GameObject("selfcheck-server-boss");
                var netBody = netGo.AddComponent<WorldBody>();
                netBody.MobId = MobCatalog.BoneWarden;
                var netMob = netGo.AddComponent<NetMob>();
                netMob.OnStartServer();
                if (netBody.DisplayName != "본워든" || Math.Abs(netBody.MaxHp - 120f) > 0.0001f || Math.Abs(netBody.Hp - 120f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 본워든 카탈로그와 HP를 권위 있게 적용해야 합니다.");
                UnityEngine.Object.DestroyImmediate(netGo);
                var slayerGo = new GameObject("selfcheck-dungeon-slayer");
                slayerGo.transform.position = eliteGo.transform.position;
                var slayer = slayerGo.AddComponent<WorldBody>();
                slayer.IsAvatar = true;
                slayer.MaxHp = 50f;
                slayer.ResetHp();
                var bag = slayerGo.AddComponent<InventoryBag>();
                elite.ApplyDamage((int)elite.MaxHp - 1);
                var slay = world.TryAttack(slayer, elite);
                if (!slay.Applied)
                    throw new InvalidOperationException("본워든 처치 실패: " + slay.FailReason);
                if (elite.Alive)
                    throw new InvalidOperationException("본워든이 죽어야 합니다.");
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.WardenCrest))
                    throw new InvalidOperationException("본워든은 수호자 문장(warden_crest)을 서버가 지급해야 합니다.");
                UnityEngine.Object.DestroyImmediate(eliteGo);
                UnityEngine.Object.DestroyImmediate(slayerGo);

                avatarGo.transform.position = exitGo.transform.position;
                var leave = world.TryDungeon(avatar, xg);
                if (!leave.Applied)
                    throw new InvalidOperationException("던전 퇴장 실패: " + leave.FailReason);
                float lx = avatarGo.transform.position.x - Dungeon1.LeaveX;
                float lz = avatarGo.transform.position.z - Dungeon1.LeaveZ;
                if (lx * lx + lz * lz > 4f)
                    throw new InvalidOperationException("퇴장은 서버가 입구 밖으로 워프해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldGo);
                if (avatarGo != null)
                    UnityEngine.Object.DestroyImmediate(avatarGo);
                if (enemyGo != null)
                    UnityEngine.Object.DestroyImmediate(enemyGo);
            }
        }

        static void AssertDungeon2Slice()
        {
            var entrance = GameObject.Find(Dungeon2.EntranceObject);
            var interior = GameObject.Find(Dungeon2.InteriorObject);
            var exitGo = GameObject.Find(Dungeon2.ExitObject);
            var mob = GameObject.Find(Dungeon2.MobObject);
            if (entrance == null || interior == null || exitGo == null)
                throw new InvalidOperationException("던전 2는 입구+내부+출구가 있어야 합니다.");
            var eg = entrance.GetComponent<DungeonGate>();
            var xg = exitGo.GetComponent<DungeonGate>();
            if (eg == null || xg == null || eg.IsExit || !xg.IsExit || eg.DungeonId != Dungeon2.Id || xg.DungeonId != Dungeon2.Id)
                throw new InvalidOperationException("던전 2 게이트는 서버 DungeonGate 입장/퇴장이어야 합니다.");
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3은 두지 않습니다.");
            var d1e = GameObject.Find(Dungeon1.EntranceObject);
            if (d1e == null)
                throw new InvalidOperationException("던전 1 서쪽 입구가 유지되어야 합니다.");
            if (Vector3.Distance(entrance.transform.position, d1e.transform.position) < 12f)
                throw new InvalidOperationException("던전 2 입구는 던전 1 서쪽 입구와 달라야 합니다.");
            if (Math.Abs(entrance.transform.position.x - Dungeon1.EntranceX) < 4f
                && Math.Abs(entrance.transform.position.z - Dungeon1.EntranceZ) < 4f)
                throw new InvalidOperationException("던전 2 입구는 서쪽 던전 1과 같은 자리가 아니어야 합니다.");
            var hunt = GameObject.Find("Raider");
            if (hunt != null && Vector3.Distance(entrance.transform.position, hunt.transform.position) < 8f)
                throw new InvalidOperationException("던전 2 입구가 사냥 라인을 건드리면 안 됩니다.");
            var field = GameObject.Find("EastField");
            if (field != null && Vector3.Distance(entrance.transform.position, field.transform.position) < 12f)
                throw new InvalidOperationException("던전 2 입구가 동쪽 필드를 건드리면 안 됩니다.");
            var south = GameObject.Find("SouthField");
            if (south != null && Vector3.Distance(entrance.transform.position, south.transform.position) < 12f)
                throw new InvalidOperationException("던전 2 입구가 남쪽 필드를 건드리면 안 됩니다.");
            var north = GameObject.Find("NorthField");
            if (north != null && Vector3.Distance(entrance.transform.position, north.transform.position) < 12f)
                throw new InvalidOperationException("던전 2 입구가 북쪽 필드를 건드리면 안 됩니다.");
            var body = mob != null ? mob.GetComponent<WorldBody>() : null;
            if (body == null || body.MobId != MobCatalog.Bandit || !body.IsEnemy)
                throw new InvalidOperationException("던전 2 내부에는 카탈로그 도적 1종이 있어야 합니다.");
            if (mob.GetComponent<NetworkObject>() == null || mob.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("던전 2 몹 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            var boss = GameObject.Find(Dungeon2.BossObject);
            var bossBody = boss != null ? boss.GetComponent<WorldBody>() : null;
            if (bossBody == null || bossBody.MobId != MobCatalog.ShadowCaptain || !bossBody.IsEnemy)
                throw new InvalidOperationException("던전 2 내부에 서버 권한 네임드 엘리트 섀도우캡틴이 있어야 합니다.");
            if (bossBody.DisplayName != "섀도우캡틴" || Math.Abs(bossBody.MaxHp - 150f) > 0.0001f)
                throw new InvalidOperationException("섀도우캡틴은 이름=섀도우캡틴, HP=150이어야 합니다.");
            if (boss.GetComponent<NetworkObject>() == null || boss.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("섀도우캡틴 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (Math.Abs(boss.transform.position.x - Dungeon2.BossX) > 0.8f || Math.Abs(boss.transform.position.z - Dungeon2.BossZ) > 0.8f)
                throw new InvalidOperationException("섀도우캡틴은 던전 2 내부 표시 위치에 있어야 합니다.");
            var huntLine = GameObject.Find("SkelRogue");
            if (huntLine != null && Vector3.Distance(boss.transform.position, huntLine.transform.position) < 40f)
                throw new InvalidOperationException("섀도우캡틴은 사냥 라인 아웃라이어가 아니라 던전 2 내부여야 합니다.");
            var d1Boss = GameObject.Find(Dungeon1.BossObject);
            if (d1Boss != null && Vector3.Distance(boss.transform.position, d1Boss.transform.position) < 40f)
                throw new InvalidOperationException("섀도우캡틴은 던전 1 본워든과 같은 방이 아니어야 합니다.");
            var cc = boss.GetComponent<CharacterController>();
            if (cc == null || Math.Abs(cc.height - 2.35f) > 0.05f)
                throw new InvalidOperationException("섀도우캡틴은 KayKit Rogue를 본워든과 다른 키로 써야 합니다.");

            var worldGo = new GameObject("selfcheck-dungeon2-world");
            GameObject avatarGo = null;
            GameObject enemyGo = null;
            try
            {
                var world = worldGo.AddComponent<OfflineWorld>();
                avatarGo = new GameObject("selfcheck-dungeon2-avatar");
                avatarGo.transform.position = new Vector3(0f, 0.1f, 0f);
                var avatar = avatarGo.AddComponent<WorldBody>();
                avatar.IsAvatar = true;
                avatar.MaxHp = 50f;
                avatar.ResetHp();
                var far = world.TryDungeon(avatar, eg);
                if (far.Applied)
                    throw new InvalidOperationException("던전 2 입구 사거리 밖 입장이 들어가면 안 됩니다.");
                avatarGo.transform.position = entrance.transform.position;
                var enter = world.TryDungeon(avatar, eg);
                if (!enter.Applied)
                    throw new InvalidOperationException("던전 2 입장 실패: " + enter.FailReason);
                float dx = avatarGo.transform.position.x - Dungeon2.InteriorX;
                float dz = avatarGo.transform.position.z - Dungeon2.InteriorZ;
                if (dx * dx + dz * dz > 4f)
                    throw new InvalidOperationException("던전 2 입장은 서버가 내부 스폰으로 워프해야 합니다.");

                enemyGo = new GameObject("selfcheck-dungeon2-enemy");
                enemyGo.transform.position = avatarGo.transform.position + new Vector3(0.8f, 0f, 0f);
                var enemy = enemyGo.AddComponent<WorldBody>();
                enemy.IsEnemy = true;
                enemy.MobId = MobCatalog.Bandit;
                enemy.ApplyMobCatalog();
                enemy.ResetHp();
                var hit = world.TryAttack(avatar, enemy);
                if (!hit.Applied)
                    throw new InvalidOperationException("던전 2 내부 전투 실패: " + hit.FailReason);

                var eliteGo = new GameObject("selfcheck-dungeon2-boss");
                eliteGo.transform.position = avatarGo.transform.position + new Vector3(0.8f, 0f, 0f);
                var elite = eliteGo.AddComponent<WorldBody>();
                elite.IsEnemy = true;
                elite.MobId = MobCatalog.ShadowCaptain;
                elite.ApplyMobCatalog();
                elite.ResetHp();
                if (Math.Abs(elite.MaxHp - 150f) > 0.0001f || elite.DisplayName != "섀도우캡틴")
                    throw new InvalidOperationException("섀도우캡틴 카탈로그 HP/이름이 서버에 적용되어야 합니다.");
                var netGo = new GameObject("selfcheck-server-boss2");
                var netBody = netGo.AddComponent<WorldBody>();
                netBody.MobId = MobCatalog.ShadowCaptain;
                var netMob = netGo.AddComponent<NetMob>();
                netMob.OnStartServer();
                if (netBody.DisplayName != "섀도우캡틴" || Math.Abs(netBody.MaxHp - 150f) > 0.0001f || Math.Abs(netBody.Hp - 150f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 섀도우캡틴 카탈로그와 HP를 권위 있게 적용해야 합니다.");
                UnityEngine.Object.DestroyImmediate(netGo);
                var slayerGo = new GameObject("selfcheck-dungeon2-slayer");
                slayerGo.transform.position = eliteGo.transform.position;
                var slayer = slayerGo.AddComponent<WorldBody>();
                slayer.IsAvatar = true;
                slayer.MaxHp = 50f;
                slayer.ResetHp();
                var bag = slayerGo.AddComponent<InventoryBag>();
                elite.ApplyDamage((int)elite.MaxHp - 1);
                var slay = world.TryAttack(slayer, elite);
                if (!slay.Applied)
                    throw new InvalidOperationException("섀도우캡틴 처치 실패: " + slay.FailReason);
                if (elite.Alive)
                    throw new InvalidOperationException("섀도우캡틴이 죽어야 합니다.");
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.CaptainSigil))
                    throw new InvalidOperationException("섀도우캡틴은 캡틴 인장(captain_sigil)을 서버가 지급해야 합니다.");
                if (ItemCatalog.Has(bag.Items, ItemCatalog.WardenCrest))
                    throw new InvalidOperationException("섀도우캡틴은 본워든 드랍(warden_crest)을 주면 안 됩니다.");
                UnityEngine.Object.DestroyImmediate(eliteGo);
                UnityEngine.Object.DestroyImmediate(slayerGo);

                avatarGo.transform.position = exitGo.transform.position;
                var leave = world.TryDungeon(avatar, xg);
                if (!leave.Applied)
                    throw new InvalidOperationException("던전 2 퇴장 실패: " + leave.FailReason);
                float lx = avatarGo.transform.position.x - Dungeon2.LeaveX;
                float lz = avatarGo.transform.position.z - Dungeon2.LeaveZ;
                if (lx * lx + lz * lz > 4f)
                    throw new InvalidOperationException("던전 2 퇴장은 서버가 입구 밖으로 워프해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldGo);
                if (avatarGo != null)
                    UnityEngine.Object.DestroyImmediate(avatarGo);
                if (enemyGo != null)
                    UnityEngine.Object.DestroyImmediate(enemyGo);
            }
        }
        static void AssertMeditationSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Meditation) != StatId.Int)
                throw new InvalidOperationException("명상 Primary는 INT이어야 합니다.");

            var fullSkills = new SkillSet();
            var full = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = fullSkills,
                Mana = 35f,
                MaxMana = 35f
            });
            if (full.Applied)
                throw new InvalidOperationException("마나 가득이면 명상되면 안 됩니다.");
            if (Math.Abs(fullSkills.Get(SkillId.Meditation)) > 0.0001f)
                throw new InvalidOperationException("실패한 명상은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            var ok = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                Mana = 5f,
                MaxMana = 35f,
                Difficulty = MeditationResolve.Difficulty
            });
            if (!ok.Applied || ok.Damage < MeditationResolve.BaseRegen)
                throw new InvalidOperationException("명상이 마나를 회복해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 명상 후 0.1이어야 합니다.");

            int light = MeditationResolve.Amount(new SkillSet(), new StatSet(), false);
            int heavy = MeditationResolve.Amount(new SkillSet(), new StatSet(), true);
            if (heavy >= light)
                throw new InvalidOperationException("중갑은 명상 회복이 낮아야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Meditation, SkillLock.Locked);
            var lockedOk = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = locked,
                Mana = 5f,
                MaxMana = 35f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 명상도 회복은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Meditation)) > 0.0001f)
                throw new InvalidOperationException("잠긴 명상은 오르면 안 됩니다.");

            var go = new GameObject("selfcheck-meditate");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-meditate-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(world.StatsOf(body).Int);
                body.SetMana(4f);
                float before = body.Mana;
                var none = world.TryMeditate(body);
                if (!none.Applied)
                    throw new InvalidOperationException("서버 명상 실패: " + none.FailReason);
                if (body.Mana <= before)
                    throw new InvalidOperationException("명상이 마나를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 명상 후 서버 스킬 0.1이어야 합니다.");

                body.SetMana(body.MaxMana);
                var fullWorld = world.TryMeditate(body);
                if (fullWorld.Applied)
                    throw new InvalidOperationException("가득 찬 마나로 명상되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 명상은 스킬을 올리면 안 됩니다.");

                var plateGo = new GameObject("selfcheck-meditate-plate");
                plateGo.transform.position = go.transform.position;
                var plateBody = plateGo.AddComponent<WorldBody>();
                plateBody.IsAvatar = true;
                plateBody.RecalcFromInt(world.StatsOf(plateBody).Int);
                plateBody.SetMana(4f);
                var plateBag = plateGo.AddComponent<InventoryBag>();
                plateBag.Add(ItemCatalog.IronPlate, 1);
                if (!ItemCatalog.HasHeavyArmor(plateBag.Items))
                    throw new InvalidOperationException("iron_plate는 중갑이어야 합니다.");
                var heavyHit = world.TryMeditate(plateBody);
                if (!heavyHit.Applied)
                    throw new InvalidOperationException("중갑 명상 실패: " + heavyHit.FailReason);
                int plated = MeditationResolve.Amount(world.SkillsOf(plateBody), world.StatsOf(plateBody), true);
                if (heavyHit.Damage != plated)
                    throw new InvalidOperationException("중갑 명상 회복량이 패널티를 받아야 합니다.");
                UnityEngine.Object.DestroyImmediate(plateGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertMagicResistSlice()
        {
            if (StatSet.PrimaryOf(SkillId.MagicResist) != StatId.Int)
                throw new InvalidOperationException("마법 저항 Primary는 INT이어야 합니다.");

            int raw = 10;
            int none = MagicResistResolve.Reduce(raw, new SkillSet(), new StatSet(), 0);
            int geared = MagicResistResolve.Reduce(raw, new SkillSet(), new StatSet(), 2);
            if (none >= raw)
                throw new InvalidOperationException("마법 저항은 마법 피해를 줄여야 합니다.");
            if (geared >= none)
                throw new InvalidOperationException("장비 저항이 마법 피해를 더 줄여야 합니다.");
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
            if (Math.Abs(melee.Get(SkillId.MagicResist)) > 0.0001f)
                throw new InvalidOperationException("물리 피격은 마법 저항을 올리면 안 됩니다.");

            var go = new GameObject("selfcheck-resist");
            GameObject worldGo = null;
            GameObject casterGo = null;
            GameObject plateGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-resist-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.IsEnemy = false;
                body.MaxHp = 50f;
                body.ResetHp();
                body.RecalcFromInt(world.StatsOf(body).Int);
                go.AddComponent<InventoryBag>();

                casterGo = new GameObject("selfcheck-resist-caster");
                casterGo.transform.position = go.transform.position;
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsEnemy = true;
                caster.MaxHp = 40f;
                caster.ResetHp();
                caster.RecalcFromInt(40);
                caster.SetMana(40f);
                var cbag = casterGo.AddComponent<InventoryBag>();
                cbag.Add(SpellCast.Reagent, 8);
                world.BookOf(caster).Learn(SpellId.Ember);
                world.BookOf(caster).Learn(SpellId.Mend);

                float hp0 = body.Hp;
                var ember = world.TryCast(caster, SpellId.Ember, body);
                if (!ember.Applied || body.Hp >= hp0)
                    throw new InvalidOperationException("적대 불씨가 플레이어에게 들어가야 합니다: " + ember.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.MagicResist) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("적대 주문 피격 후 마법 저항 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.MagicResist)) > 0.0001f)
                    throw new InvalidOperationException("시전자 마법 저항이 오르면 안 됩니다.");

                int dmg0 = ember.Damage;
                world.SkillsOf(body).ForceSet(SkillId.MagicResist, 40f, SkillLock.Up);
                body.SetHp(50f);
                var hard = world.TryCast(caster, SpellId.Ember, body);
                if (!hard.Applied)
                    throw new InvalidOperationException("고숙련 저항 불씨 실패: " + hard.FailReason);
                if (hard.Damage >= dmg0)
                    throw new InvalidOperationException("높은 마법 저항이 피해를 더 줄여야 합니다.");

                var palGo = new GameObject("selfcheck-resist-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsEnemy = true;
                pal.MaxHp = 40f;
                pal.ResetHp();
                var same = world.TryCast(caster, SpellId.Ember, pal);
                if (same.Applied)
                    throw new InvalidOperationException("같은 편 불씨는 실패해야 합니다.");
                UnityEngine.Object.DestroyImmediate(palGo);

                float resistBeforeMend = world.SkillsOf(body).Get(SkillId.MagicResist);
                var mend = world.TryCast(caster, SpellId.Mend, body);
                if (!mend.Applied)
                    throw new InvalidOperationException("봉합은 시전자 치유여야 합니다: " + mend.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.MagicResist) - resistBeforeMend) > 0.0001f)
                    throw new InvalidOperationException("우호 주문은 마법 저항을 올리면 안 됩니다.");

                var locked = world.SkillsOf(body);
                locked.ForceSet(SkillId.MagicResist, 0f, SkillLock.Locked);
                body.SetHp(50f);
                var lockedHit = world.TryCast(caster, SpellId.Ember, body);
                if (!lockedHit.Applied)
                    throw new InvalidOperationException("잠긴 저항도 피격은 되어야 합니다: " + lockedHit.FailReason);
                if (Math.Abs(locked.Get(SkillId.MagicResist)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 마법 저항은 오르면 안 됩니다.");

                plateGo = new GameObject("selfcheck-resist-plate");
                plateGo.transform.position = casterGo.transform.position;
                var plateBody = plateGo.AddComponent<WorldBody>();
                plateBody.IsAvatar = true;
                plateBody.IsEnemy = false;
                plateBody.MaxHp = 50f;
                plateBody.ResetHp();
                var plateBag = plateGo.AddComponent<InventoryBag>();
                plateBag.Add("iron_plate", 1);
                if (ItemCatalog.EquipmentMagicResist(plateBag.Items) < 2)
                    throw new InvalidOperationException("iron_plate는 장비 마법 저항을 줘야 합니다.");
                float php = plateBody.Hp;
                var plateHit = world.TryCast(caster, SpellId.Ember, plateBody);
                if (!plateHit.Applied || plateBody.Hp >= php)
                    throw new InvalidOperationException("중갑 대상 불씨 실패: " + plateHit.FailReason);
                if (plateHit.Damage >= lockedHit.Damage)
                    throw new InvalidOperationException("장비 저항이 서버 불씨 피해를 더 줄여야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (plateGo != null)
                    UnityEngine.Object.DestroyImmediate(plateGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertEvalIntSlice()
        {
            if (StatSet.PrimaryOf(SkillId.EvaluateIntelligence) != StatId.Int)
                throw new InvalidOperationException("지능 평가 Primary는 INT이어야 합니다.");

            var noneSkills = new SkillSet();
            var none = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Now = 1f,
                Skills = noneSkills,
                TargetStats = null
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 지능 평가는 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("실패한 지능 평가는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                TargetStats = new StatSet(),
                TargetAlive = true
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 지능 평가는 들어가면 안 됩니다.");
            if (Math.Abs(farSkills.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("실패한 지능 평가는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            var targetStats = new StatSet();
            targetStats.ForceSet(20, 20, 40);
            int intWas = stats.Int;
            var ok = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                TargetStats = targetStats,
                TargetAlive = true,
                TargetMana = 12f,
                TargetMaxMana = 50f,
                Difficulty = EvalIntResolve.Difficulty
            });
            if (!ok.Applied || ok.Intelligence != 40 || ok.Mana != 12 || ok.MaxMana != 50)
                throw new InvalidOperationException("지능 평가는 대상 INT/마나를 밝혀야 합니다.");
            if (Math.Abs(skills.Get(SkillId.EvaluateIntelligence) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 지능 평가 후 0.1이어야 합니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("지능 평가 상승 시 INT가 올라야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.EvaluateIntelligence, SkillLock.Locked);
            var lockedOk = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                TargetStats = targetStats,
                TargetAlive = true,
                TargetMana = 12f,
                TargetMaxMana = 50f
            });
            if (!lockedOk.Applied || lockedOk.Intelligence != 40)
                throw new InvalidOperationException("잠긴 지능 평가도 정보는 보여야 합니다.");
            if (Math.Abs(locked.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("잠긴 지능 평가는 오르면 안 됩니다.");

            int plain = SpellCast.EmberDamage(new StatSet(), new SkillSet());
            var boosted = new SkillSet();
            boosted.ForceSet(SkillId.EvaluateIntelligence, 40f, SkillLock.Up);
            int withEval = SpellCast.EmberDamage(new StatSet(), boosted);
            if (withEval <= plain)
                throw new InvalidOperationException("지능 평가가 공격 마법 위력에 반영되어야 합니다.");

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
            if (Math.Abs(melee.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("물리 공격은 지능 평가를 올리면 안 됩니다.");

            var go = new GameObject("selfcheck-evalint");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject casterGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-evalint-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(world.StatsOf(body).Int);

                var missing = world.TryEvaluate(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 지능 평가는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 지능 평가는 스킬을 올리면 안 됩니다.");

                tgtGo = new GameObject("selfcheck-evalint-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.DisplayName = "스켈레톤";
                tgt.MaxHp = 30f;
                tgt.ResetHp();
                world.StatsOf(tgt).ForceSet(20, 20, 40);
                tgt.RecalcFromInt(40);
                tgt.SetMana(18f);

                var hit = world.TryEvaluate(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 지능 평가 실패: " + hit.FailReason);
                if (hit.Intelligence != 40 || hit.Mana != 18 || hit.MaxMana != StatSet.MaxManaOf(40))
                    throw new InvalidOperationException("서버 지능 평가는 대상 INT/마나를 밝혀야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.EvaluateIntelligence) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 지능 평가 후 서버 스킬 0.1이어야 합니다.");
                if (string.IsNullOrEmpty(world.LastEvalMessage) || world.LastEvalMessage.IndexOf("INT 40", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("지능 평가 메시지가 INT를 포함해야 합니다.");

                casterGo = new GameObject("selfcheck-evalint-caster");
                casterGo.transform.position = go.transform.position;
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.RecalcFromInt(world.StatsOf(caster).Int);
                caster.SetMana(40f);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 4);
                world.BookOf(caster).Learn(SpellId.Ember);
                float evalBeforeCast = world.SkillsOf(caster).Get(SkillId.EvaluateIntelligence);
                var ember = world.TryCast(caster, SpellId.Ember, tgt);
                if (!ember.Applied)
                    throw new InvalidOperationException("불씨 대조 실패: " + ember.FailReason);
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.EvaluateIntelligence) - evalBeforeCast) > 0.0001f)
                    throw new InvalidOperationException("주문 시전은 지능 평가를 올리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }




        static void AssertBolt()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
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

        static void AssertMusicianshipSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Musicianship) != StatId.Dex)
                throw new InvalidOperationException("음악 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Musicianship) != "음악" || SkillTitles.JobOf(SkillId.Musicianship) != "음악가")
                throw new InvalidOperationException("음악 스킬명/직업명이 기획과 같아야 합니다.");
            var rec = CraftRecipes.Find("lute");
            if (rec == null || rec.Ingredient != "wood" || rec.Output != ItemCatalog.Lute
                || rec.Skill != SkillId.Carpentry || rec.Count != 2)
                throw new InvalidOperationException("나무 2 → 류트 레시피가 있어야 합니다.");
            if (ItemCatalog.BuyPrice(ItemCatalog.Lute) <= 0 || ItemCatalog.WeightOf(ItemCatalog.Lute) <= 0f)
                throw new InvalidOperationException("류트 무게/상점 가격이 없습니다.");
            if (ItemCatalog.MaxUsesOf(ItemCatalog.Lute) <= 0)
                throw new InvalidOperationException("류트 내구도가 있어야 합니다.");

            var noneSkills = new SkillSet();
            var none = MusicianshipResolve.Resolve(new MusicianshipRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasInstrument = false
            });
            if (none.Applied)
                throw new InvalidOperationException("악기 없는 연주는 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Musicianship)) > 0.0001f)
                throw new InvalidOperationException("실패한 연주는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = MusicianshipResolve.Resolve(new MusicianshipRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasInstrument = true,
                Difficulty = MusicianshipResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("악기 연주는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Musicianship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 연주 후 0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("음악 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("음악은 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Musicianship, SkillLock.Locked);
            var lockedOk = MusicianshipResolve.Resolve(new MusicianshipRequest
            {
                Now = 1f,
                Skills = locked,
                HasInstrument = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 음악도 연주는 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Musicianship)) > 0.0001f)
                throw new InvalidOperationException("잠긴 음악은 오르면 안 됩니다.");

            var track = new SkillSet();
            TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = track,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적"
            });
            if (Math.Abs(track.Get(SkillId.Musicianship)) > 0.0001f)
                throw new InvalidOperationException("추적은 음악을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("music-check", "음악가", 0, 20, 40, 20,
                new[] { SkillId.Musicianship, SkillId.Carpentry, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (!hasLute)
                throw new InvalidOperationException("음악 시작은 류트를 줘야 합니다.");

            var go = new GameObject("selfcheck-music");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject farGo = null;
            GameObject stGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-music-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryPlay(body);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 악기 없는 연주는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Musicianship)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 연주는 스킬을 올리면 안 됩니다.");

                stGo = new GameObject("selfcheck-music-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "wooden_club";
                station.DisplayName = "목공소";
                bag.Add("wood", 2);
                var crafted = world.TryCraft(body, station, "lute");
                if (!crafted.Applied)
                    throw new InvalidOperationException("목공 류트 제작 실패: " + crafted.FailReason);
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.Lute))
                    throw new InvalidOperationException("나무 2 → 류트 1이어야 합니다.");

                tgtGo = new GameObject("selfcheck-music-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();

                farGo = new GameObject("selfcheck-music-far");
                farGo.transform.position = go.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.MaxHp = 45f;
                far.ResetHp();

                var hit = world.TryPlay(body);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 연주 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Musicianship) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 연주 후 서버 스킬 0.1이어야 합니다.");
                if (hit.Calmed < 1)
                    throw new InvalidOperationException("가까운 적은 살짝 진정되어야 합니다.");
                if (tgt.CalmUntil <= Time.time)
                    throw new InvalidOperationException("연주 사거리 안 적은 CalmUntil이 있어야 합니다.");
                if (far.CalmUntil > Time.time)
                    throw new InvalidOperationException("사거리 밖 적은 진정되면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastPlayMessage) || world.LastPlayMessage.IndexOf("진정", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("연주 메시지가 진정을 포함해야 합니다.");

                float hpWas = body.Hp;
                var melee = world.TryAttack(body, tgt);
                if (!melee.Applied)
                    throw new InvalidOperationException("진정 중 근접 공격 실패: " + melee.FailReason);
                if (body.Hp < hpWas - 0.01f)
                    throw new InvalidOperationException("작은 진정은 반격을 막아야 합니다(전체 평화 아님).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }



        static void AssertPeacemakingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Peacemaking) != StatId.Dex)
                throw new InvalidOperationException("평화 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Peacemaking) != "평화" || SkillTitles.JobOf(SkillId.Peacemaking) != "평화사")
                throw new InvalidOperationException("평화 스킬명/직업명이 기획과 같아야 합니다.");

            var noneSkills = new SkillSet();
            var none = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasInstrument = false,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                Distance = 1f
            });
            if (none.Applied)
                throw new InvalidOperationException("악기 없는 평화는 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Peacemaking)) > 0.0001f)
                throw new InvalidOperationException("실패한 평화는 스킬을 올리면 안 됩니다.");

            var noTgt = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTarget = false,
                TargetEnemy = true,
                TargetAlive = true
            });
            if (noTgt.Applied)
                throw new InvalidOperationException("대상 없는 평화는 실패해야 합니다.");

            var pvp = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTarget = true,
                TargetEnemy = false,
                TargetAlive = true,
                Distance = 1f
            });
            if (pvp.Applied)
                throw new InvalidOperationException("플레이어 대상 평화는 안 됩니다(Open PvP 아님).");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasInstrument = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                Distance = 1f,
                Difficulty = PeacemakingResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("대상 몹 평화는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Peacemaking) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 평화 후 0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("평화 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("평화는 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Peacemaking, SkillLock.Locked);
            var lockedOk = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = locked,
                HasInstrument = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                Distance = 1f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 평화도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Peacemaking)) > 0.0001f)
                throw new InvalidOperationException("잠긴 평화는 오르면 안 됩니다.");

            var music = new SkillSet();
            MusicianshipResolve.Resolve(new MusicianshipRequest
            {
                Now = 1f,
                Skills = music,
                HasInstrument = true
            });
            if (Math.Abs(music.Get(SkillId.Peacemaking)) > 0.0001f)
                throw new InvalidOperationException("음악은 평화를 올리면 안 됩니다.");

            var created = CharacterCreate.Build("peace-check", "평화사", 0, 20, 40, 20,
                new[] { SkillId.Peacemaking, SkillId.Musicianship, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (!hasLute)
                throw new InvalidOperationException("평화 시작은 류트를 줘야 합니다.");

            var go = new GameObject("selfcheck-peace");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject otherGo = null;
            GameObject palGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-peace-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = go.AddComponent<InventoryBag>();

                tgtGo = new GameObject("selfcheck-peace-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();

                var missing = world.TryPeace(body, tgt);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 악기 없는 평화는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Peacemaking)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 평화는 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.Lute, 1);

                palGo = new GameObject("selfcheck-peace-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPlayer = world.TryPeace(body, pal);
                if (onPlayer.Applied)
                    throw new InvalidOperationException("서버 평화는 플레이어를 대상으로 하면 안 됩니다.");

                otherGo = new GameObject("selfcheck-peace-other");
                otherGo.transform.position = go.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsEnemy = true;
                other.DisplayName = "졸병";
                other.MaxHp = 30f;
                other.ResetHp();

                var hit = world.TryPeace(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 평화 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Peacemaking) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 평화 후 서버 스킬 0.1이어야 합니다.");
                if (tgt.CalmUntil <= Time.time)
                    throw new InvalidOperationException("평화 대상은 CalmUntil이 있어야 합니다.");
                if (other.CalmUntil > Time.time)
                    throw new InvalidOperationException("평화는 대상 한 몹만 멈춰야 합니다.");
                if (string.IsNullOrEmpty(world.LastPeaceMessage) || world.LastPeaceMessage.IndexOf("평화", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("평화 메시지가 있어야 합니다.");

                float hpWas = body.Hp;
                var melee = world.TryAttack(body, tgt);
                if (!melee.Applied)
                    throw new InvalidOperationException("평화 중 근접 공격 실패: " + melee.FailReason);
                if (body.Hp < hpWas - 0.01f)
                    throw new InvalidOperationException("평화는 대상 몹 반격을 막아야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertProvocationSlice()
        {
            if (SkillId.Provocation == SkillId.Peacemaking)
                throw new InvalidOperationException("도발 SkillId는 평화와 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Provocation) != StatId.Dex)
                throw new InvalidOperationException("도발 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Provocation) != "도발" || SkillTitles.JobOf(SkillId.Provocation) != "도발사")
                throw new InvalidOperationException("도발 스킬명/직업명이 기획과 같아야 합니다.");

            var noneSkills = new SkillSet();
            var none = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasInstrument = false,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (none.Applied)
                throw new InvalidOperationException("악기 없는 도발은 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Provocation)) > 0.0001f)
                throw new InvalidOperationException("실패한 도발은 스킬을 올리면 안 됩니다.");

            var noTgt = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = false,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true
            });
            if (noTgt.Applied)
                throw new InvalidOperationException("대상 하나뿐인 도발은 실패해야 합니다.");

            var same = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                SameTarget = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (same.Applied)
                throw new InvalidOperationException("같은 대상 둘은 도발 실패해야 합니다.");

            var pvp = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = false,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (pvp.Applied)
                throw new InvalidOperationException("플레이어 대상 도발은 안 됩니다.");

            var pvp2 = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = false,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (pvp2.Applied)
                throw new InvalidOperationException("두 번째가 플레이어면 도발은 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f,
                Difficulty = ProvocationResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("몹 둘 도발은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Provocation) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 도발 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Peacemaking)) > 0.0001f)
                throw new InvalidOperationException("도발은 평화를 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("도발 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("도발은 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Provocation, SkillLock.Locked);
            var lockedOk = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = locked,
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 도발도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Provocation)) > 0.0001f)
                throw new InvalidOperationException("잠긴 도발은 오르면 안 됩니다.");

            var peace = new SkillSet();
            PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = peace,
                HasInstrument = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                Distance = 1f
            });
            if (Math.Abs(peace.Get(SkillId.Provocation)) > 0.0001f)
                throw new InvalidOperationException("평화는 도발을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("provoke-check", "도발사", 0, 20, 40, 20,
                new[] { SkillId.Provocation, SkillId.Musicianship, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (!hasLute)
                throw new InvalidOperationException("도발 시작은 류트를 줘야 합니다.");

            var go = new GameObject("selfcheck-provoke");
            GameObject worldGo = null;
            GameObject aGo = null;
            GameObject bGo = null;
            GameObject palGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-provoke-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = go.AddComponent<InventoryBag>();

                aGo = new GameObject("selfcheck-provoke-a");
                aGo.transform.position = go.transform.position;
                var a = aGo.AddComponent<WorldBody>();
                a.IsEnemy = true;
                a.MobId = "bandit";
                a.DisplayName = "도적";
                a.MaxHp = 45f;
                a.ResetHp();

                bGo = new GameObject("selfcheck-provoke-b");
                bGo.transform.position = go.transform.position;
                var b = bGo.AddComponent<WorldBody>();
                b.IsEnemy = true;
                b.DisplayName = "졸병";
                b.MaxHp = 30f;
                b.ResetHp();

                var missing = world.TryProvoke(body, a, b);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 악기 없는 도발은 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Provocation)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 도발은 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.Lute, 1);

                palGo = new GameObject("selfcheck-provoke-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPlayer = world.TryProvoke(body, a, pal);
                if (onPlayer.Applied)
                    throw new InvalidOperationException("서버 도발은 플레이어를 대상으로 하면 안 됩니다.");
                var onPlayer2 = world.TryProvoke(body, pal, b);
                if (onPlayer2.Applied)
                    throw new InvalidOperationException("서버 도발은 첫 대상이 플레이어면 안 됩니다.");

                float playerHp = body.Hp;
                float aHp = a.Hp;
                float bHp = b.Hp;
                var hit = world.TryProvoke(body, a, b);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 도발 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Provocation) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 도발 후 서버 스킬 0.1이어야 합니다.");
                if (a.ProvokeUntil <= Time.time || b.ProvokeUntil <= Time.time)
                    throw new InvalidOperationException("도발 대상은 ProvokeUntil이 있어야 합니다.");
                if (a.ProvokePartner != b || b.ProvokePartner != a)
                    throw new InvalidOperationException("도발은 두 몹이 서로를 상대로 싸워야 합니다.");
                if (string.IsNullOrEmpty(world.LastProvokeMessage) || world.LastProvokeMessage.IndexOf("도발", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("도발 메시지가 있어야 합니다.");

                world.TickProvoke(Time.time);
                if (a.Hp >= aHp - 0.01f && b.Hp >= bHp - 0.01f)
                    throw new InvalidOperationException("도발 후 두 몹이 서로 싸워야 합니다.");
                if (body.Hp < playerHp - 0.01f)
                    throw new InvalidOperationException("도발은 플레이어를 때리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (aGo != null)
                    UnityEngine.Object.DestroyImmediate(aGo);
                if (bGo != null)
                    UnityEngine.Object.DestroyImmediate(bGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertHidingSlice()
        {
            if (SkillId.Hiding == SkillId.Provocation)
                throw new InvalidOperationException("은신 SkillId는 도발과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Hiding) != StatId.Dex)
                throw new InvalidOperationException("은신 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) != "은신" || SkillTitles.JobOf(SkillId.Hiding) != "은신자")
                throw new InvalidOperationException("은신 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) == "잠행")
                throw new InvalidOperationException("은신은 잠행이 아닙니다.");

            var ghostSkills = new SkillSet();
            var ghost = HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = ghostSkills, Ghost = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 은신은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("실패한 은신은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = HidingResolve.Resolve(new HidingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = HidingResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("은신은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Hiding) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 은신 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Provocation)) > 0.0001f)
                throw new InvalidOperationException("은신은 도발을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("은신 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("은신은 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Hiding, SkillLock.Locked);
            var lockedOk = HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = locked });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 은신도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("잠긴 은신은 오르면 안 됩니다.");

            var provoke = new SkillSet();
            ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = provoke,
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (Math.Abs(provoke.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("도발은 은신을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("hide-check", "은신자", 0, 20, 40, 20,
                new[] { SkillId.Hiding, SkillId.Tactics, SkillId.Anatomy },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (hasLute)
                throw new InvalidOperationException("은신 시작은 류트를 주면 안 됩니다.");

            var go = new GameObject("selfcheck-hide");
            GameObject worldGo = null;
            GameObject mobGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-hide-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();

                mobGo = new GameObject("selfcheck-hide-mob");
                mobGo.transform.position = go.transform.position;
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                float hp = body.Hp;
                var miss = world.TryEnemyStrike(mob, body);
                if (!miss)
                    throw new InvalidOperationException("숨지 않은 플레이어는 몹 타격 대상이어야 합니다.");
                if (body.Hp >= hp - 0.01f)
                    throw new InvalidOperationException("숨지 않으면 몹 타격이 들어가야 합니다.");

                body.ResetHp();
                var hit = world.TryHide(body);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 은신 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Hiding) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 은신 후 서버 스킬 0.1이어야 합니다.");
                if (!body.IsHidden(Time.time))
                    throw new InvalidOperationException("은신 후 HiddenUntil이 있어야 합니다.");
                if (string.IsNullOrEmpty(world.LastHideMessage) || world.LastHideMessage.IndexOf("은신", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("은신 메시지가 있어야 합니다.");

                hp = body.Hp;
                var skipped = world.TryEnemyStrike(mob, body);
                if (skipped)
                    throw new InvalidOperationException("은신 중 몹은 플레이어를 타격하면 안 됩니다.");
                if (body.Hp < hp - 0.01f)
                    throw new InvalidOperationException("은신 중 HP가 줄면 안 됩니다.");

                var hideBag = go.AddComponent<InventoryBag>();
                hideBag.Add(ItemCatalog.WoodenClub, 1);
                float mobHp = mob.Hp;
                hp = body.Hp;
                var atk = world.TryAttack(body, mob);
                if (!atk.Applied)
                    throw new InvalidOperationException("은신 중 공격은 성공해야 합니다: " + atk.FailReason);
                if (body.IsHidden(Time.time))
                    throw new InvalidOperationException("공격하면 은신이 풀려야 합니다.");
                if (mob.Hp >= mobHp - 0.01f)
                    throw new InvalidOperationException("공격 후 몹 HP가 줄어야 합니다.");
                if (body.Hp >= hp - 0.01f)
                    throw new InvalidOperationException("은신 해제 후 몹 보복이 들어가야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertStealthSlice()
        {
            if (SkillId.Stealth == SkillId.Hiding)
                throw new InvalidOperationException("잠행 SkillId는 은신과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Stealth) != StatId.Dex)
                throw new InvalidOperationException("잠행 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Stealth) != "잠행" || SkillTitles.JobOf(SkillId.Stealth) != "잠행자")
                throw new InvalidOperationException("잠행 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) != "은신")
                throw new InvalidOperationException("은신 스킬명을 잠행으로 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghost = StealthResolve.Resolve(new StealthRequest { Now = 1f, Skills = ghostSkills, Ghost = true, AlreadyHidden = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 잠행은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("실패한 잠행은 스킬을 올리면 안 됩니다.");

            var standing = new SkillSet();
            var stand = StealthResolve.Resolve(new StealthRequest { Now = 1f, Skills = standing, AlreadyHidden = false });
            if (stand.Applied)
                throw new InvalidOperationException("숨지 않은 잠행은 실패해야 합니다.");
            if (Math.Abs(standing.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("숨지 않은 잠행은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = StealthResolve.Resolve(new StealthRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                AlreadyHidden = true,
                Difficulty = StealthResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("잠행은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Stealth) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 잠행 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("잠행은 은신을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("잠행 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("잠행은 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Stealth, SkillLock.Locked);
            var lockedOk = StealthResolve.Resolve(new StealthRequest { Now = 1f, Skills = locked, AlreadyHidden = true });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 잠행도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("잠긴 잠행은 오르면 안 됩니다.");

            var hiding = new SkillSet();
            HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = hiding });
            if (Math.Abs(hiding.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("은신은 잠행을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("stealth-check", "잠행자", 0, 20, 40, 20,
                new[] { SkillId.Stealth, SkillId.Tactics, SkillId.Anatomy },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (hasLute)
                throw new InvalidOperationException("잠행 시작은 류트를 주면 안 됩니다.");

            var go = new GameObject("selfcheck-stealth");
            GameObject worldGo = null;
            GameObject mobGo = null;
            GameObject walkGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-stealth-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();

                mobGo = new GameObject("selfcheck-stealth-mob");
                mobGo.transform.position = go.transform.position;
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                walkGo = new GameObject("selfcheck-stealth-walk");
                walkGo.transform.position = go.transform.position;
                var walker = walkGo.AddComponent<WorldBody>();
                walker.IsAvatar = true;
                walker.RecalcFromStr(30);
                walker.ResetHp();

                var miss = world.TryStealth(body);
                if (miss.Applied)
                    throw new InvalidOperationException("서버 잠행은 은신 전에 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Stealth)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 잠행은 스킬을 올리면 안 됩니다.");

                var walkHide = world.TryHide(walker);
                if (!walkHide.Applied)
                    throw new InvalidOperationException("이동 검사 은신 실패: " + walkHide.FailReason);
                walkGo.transform.position += new Vector3(2f, 0f, 0f);
                world.TickHiddenMovement(Time.time);
                if (walker.IsHidden(Time.time))
                    throw new InvalidOperationException("은신만으로는 이동하면 풀려야 합니다.");

                var hide = world.TryHide(body);
                if (!hide.Applied)
                    throw new InvalidOperationException("잠행 전 은신이 필요합니다: " + hide.FailReason);
                var hit = world.TryStealth(body);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 잠행 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Stealth) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 잠행 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Hiding) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("잠행은 기존 은신 값을 유지해야 합니다.");
                if (!body.IsHidden(Time.time) || !body.CanMoveHidden(Time.time))
                    throw new InvalidOperationException("잠행 후 이동 가능 은신 상태여야 합니다.");
                if (string.IsNullOrEmpty(world.LastStealthMessage) || world.LastStealthMessage.IndexOf("잠행", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("잠행 메시지가 있어야 합니다.");

                go.transform.position += new Vector3(0.4f, 0f, 0f);
                world.TickHiddenMovement(Time.time);
                if (!body.IsHidden(Time.time))
                    throw new InvalidOperationException("잠행 중 이동해도 은신이 유지되어야 합니다.");

                var hideBag = go.AddComponent<InventoryBag>();
                hideBag.Add(ItemCatalog.WoodenClub, 1);
                float hp = body.Hp;
                var atk = world.TryAttack(body, mob);
                if (!atk.Applied)
                    throw new InvalidOperationException("잠행 중 공격은 성공해야 합니다: " + atk.FailReason);
                if (body.IsHidden(Time.time) || body.CanMoveHidden(Time.time))
                    throw new InvalidOperationException("공격하면 잠행/은신이 풀려야 합니다.");
                if (body.Hp >= hp - 0.01f)
                    throw new InvalidOperationException("잠행 해제 후 몹 보복이 들어가야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (walkGo != null)
                    UnityEngine.Object.DestroyImmediate(walkGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }



        static void AssertDetectHiddenSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (SkillId.DetectHidden == SkillId.Hiding)
                throw new InvalidOperationException("감지 SkillId는 은신과 달라야 합니다.");
            if (SkillId.DetectHidden == SkillId.Stealth)
                throw new InvalidOperationException("감지 SkillId는 잠행과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.DetectHidden) != StatId.Dex)
                throw new InvalidOperationException("감지 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.DetectHidden) != "감지" || SkillTitles.JobOf(SkillId.DetectHidden) != "탐지자")
                throw new InvalidOperationException("감지 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) != "은신" || SkillTitles.JobOf(SkillId.Hiding) != "은신자")
                throw new InvalidOperationException("은신 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Stealth) != "잠행" || SkillTitles.JobOf(SkillId.Stealth) != "잠행자")
                throw new InvalidOperationException("잠행 스킬명/직업명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghost = DetectHiddenResolve.Resolve(new DetectHiddenRequest { Now = 1f, Skills = ghostSkills, Ghost = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 감지는 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("실패한 감지는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = DetectHiddenResolve.Resolve(new DetectHiddenRequest
            {
                Now = 1f,
                Skills = farSkills,
                Distance = DetectHiddenResolve.DetectRange + 1f,
                Range = DetectHiddenResolve.DetectRange
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 감지는 실패해야 합니다.");
            if (Math.Abs(farSkills.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("사거리 밖 감지는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = DetectHiddenResolve.Resolve(new DetectHiddenRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = DetectHiddenResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("감지는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.DetectHidden) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 감지 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("감지는 은신을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("감지는 잠행을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("감지 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("감지는 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("감지는 STR을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.DetectHidden, SkillLock.Locked);
            var lockedOk = DetectHiddenResolve.Resolve(new DetectHiddenRequest { Now = 1f, Skills = locked });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 감지도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("잠긴 감지는 오르면 안 됩니다.");

            var hiding = new SkillSet();
            HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = hiding });
            if (Math.Abs(hiding.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("은신은 감지를 올리면 안 됩니다.");
            var stealth = new SkillSet();
            StealthResolve.Resolve(new StealthRequest { Now = 1f, Skills = stealth, AlreadyHidden = true });
            if (Math.Abs(stealth.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("잠행은 감지를 올리면 안 됩니다.");

            var created = CharacterCreate.Build("detect-check", "탐지자", 0, 20, 40, 20,
                new[] { SkillId.DetectHidden, SkillId.Hiding, SkillId.Stealth },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (hasLute)
                throw new InvalidOperationException("감지 시작은 류트를 주면 안 됩니다.");

            var go = new GameObject("selfcheck-detect");
            GameObject worldGo = null;
            GameObject hidGo = null;
            GameObject mobGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-detect-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();

                hidGo = new GameObject("selfcheck-detect-hidden");
                hidGo.transform.position = go.transform.position;
                var hidden = hidGo.AddComponent<WorldBody>();
                hidden.IsAvatar = true;
                hidden.RecalcFromStr(30);
                hidden.ResetHp();

                mobGo = new GameObject("selfcheck-detect-mob");
                mobGo.transform.position = go.transform.position;
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                var hid = world.TryHide(hidden);
                if (!hid.Applied)
                    throw new InvalidOperationException("감지 대상 은신 실패: " + hid.FailReason);
                if (!hidden.IsHidden(Time.time))
                    throw new InvalidOperationException("은신 후 HiddenUntil이 있어야 합니다.");
                float hp = hidden.Hp;
                var missed = world.TryEnemyStrike(mob, hidden);
                if (missed)
                    throw new InvalidOperationException("은신 중 몹은 숨은 대상을 타격하면 안 됩니다.");
                if (hidden.Hp < hp - 0.01f)
                    throw new InvalidOperationException("은신 중 HP가 줄면 안 됩니다.");

                var detect = world.TryDetectHidden(body);
                if (!detect.Applied)
                    throw new InvalidOperationException("서버 감지 실패: " + detect.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.DetectHidden) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 감지 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Hiding)) > 0.0001f)
                    throw new InvalidOperationException("서버 감지는 은신을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Stealth)) > 0.0001f)
                    throw new InvalidOperationException("서버 감지는 잠행을 올리면 안 됩니다.");
                if (hidden.IsHidden(Time.time))
                    throw new InvalidOperationException("감지는 은신을 해제해야 합니다.");
                if (string.IsNullOrEmpty(world.LastDetectMessage) || world.LastDetectMessage.IndexOf("감지", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("감지 메시지가 있어야 합니다.");

                hp = hidden.Hp;
                var hit = world.TryEnemyStrike(mob, hidden);
                if (!hit)
                    throw new InvalidOperationException("감지 후 몹은 숨었던 대상을 타격해야 합니다.");
                if (hidden.Hp >= hp - 0.01f)
                    throw new InvalidOperationException("감지 후 몹 타격이 들어가야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (hidGo != null)
                    UnityEngine.Object.DestroyImmediate(hidGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }



        static void AssertCamping()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (SkillId.Camping == SkillId.Cooking)
                throw new InvalidOperationException("야영 SkillId는 요리와 달라야 합니다.");
            if (SkillId.Camping == SkillId.Hiding)
                throw new InvalidOperationException("야영 SkillId는 은신과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Camping) != StatId.Dex)
                throw new InvalidOperationException("야영 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Camping) != "야영" || SkillTitles.JobOf(SkillId.Camping) != "야영꾼")
                throw new InvalidOperationException("야영 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Cooking) != "요리" || SkillTitles.JobOf(SkillId.Cooking) != "요리사")
                throw new InvalidOperationException("요리 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) != "은신" || SkillTitles.JobOf(SkillId.Hiding) != "은신자")
                throw new InvalidOperationException("은신 스킬명/직업명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghost = CampingResolve.Resolve(new CampingRequest { Now = 1f, Skills = ghostSkills, Ghost = true, NearCampfire = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 야영은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("실패한 야영은 스킬을 올리면 안 됩니다.");

            var noneSkills = new SkillSet();
            var none = CampingResolve.Resolve(new CampingRequest
            {
                Now = 1f,
                Skills = noneSkills,
                NearCampfire = false,
                HasKindling = false,
                Distance = CampingResolve.CampRange + 4f,
                Range = CampingResolve.CampRange
            });
            if (none.Applied || none.FailReason != "no_fire")
                throw new InvalidOperationException("화덕/불씨 없이 야영하면 안 됩니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("화덕 밖 야영 실패는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = CampingResolve.Resolve(new CampingRequest
            {
                Now = 1f,
                Skills = farSkills,
                NearCampfire = true,
                Distance = CampingResolve.CampRange + 1f,
                Range = CampingResolve.CampRange
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 화덕 야영은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = CampingResolve.Resolve(new CampingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                NearCampfire = true,
                Distance = 1f,
                Difficulty = CampingResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("화덕 야영은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Camping) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 야영 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Cooking)) > 0.0001f)
                throw new InvalidOperationException("야영은 요리를 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("야영은 은신을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("야영 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("야영은 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("야영은 STR을 올리면 안 됩니다.");

            var kindling = new SkillSet();
            var kindled = CampingResolve.Resolve(new CampingRequest
            {
                Now = 1f,
                Skills = kindling,
                NearCampfire = false,
                HasKindling = true
            });
            if (!kindled.Applied)
                throw new InvalidOperationException("나무 불씨 야영은 성공해야 합니다.");
            if (Math.Abs(kindling.Get(SkillId.Camping) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("불씨 야영 후 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Camping, SkillLock.Locked);
            var lockedOk = CampingResolve.Resolve(new CampingRequest { Now = 1f, Skills = locked, NearCampfire = true, Distance = 1f });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 야영도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("잠긴 야영은 오르면 안 됩니다.");

            var cooking = new SkillSet();
            SkillGain.TryRaise(cooking, SkillId.Cooking, 10f, out _, out _);
            if (Math.Abs(cooking.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("요리는 야영을 올리면 안 됩니다.");
            var hiding = new SkillSet();
            HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = hiding });
            if (Math.Abs(hiding.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("은신은 야영을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("camp-check", "야영꾼", 0, 20, 40, 20,
                new[] { SkillId.Camping, SkillId.Hiding, SkillId.Cooking },
                new[] { 50f, 30f, 20f });
            bool hasWood = false, hasLute = false, hasFish = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == "wood" && created.Inventory[i].Amount >= 1)
                    hasWood = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.Fish)
                    hasFish = true;
            }
            if (!hasWood)
                throw new InvalidOperationException("야영 시작은 나무를 줘야 합니다.");
            if (hasLute)
                throw new InvalidOperationException("야영 시작은 류트를 주면 안 됩니다.");
            if (!hasFish)
                throw new InvalidOperationException("요리 시작은 생선을 줘야 합니다.");

            var fire = GameObject.Find("Campfire");
            if (fire == null)
                throw new InvalidOperationException("마을에 화덕(Campfire)이 있어야 합니다.");

            var go = new GameObject("selfcheck-camp");
            GameObject worldGo = null;
            GameObject kindleGo = null;
            GameObject cookGo = null;
            GameObject stGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-camp-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                go.transform.position = fire.transform.position;
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = go.AddComponent<InventoryBag>();
                bag.Add("wood", 1);

                var camp = world.TryCamp(body);
                if (!camp.Applied)
                    throw new InvalidOperationException("서버 화덕 야영 실패: " + camp.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Camping) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 야영 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Cooking)) > 0.0001f)
                    throw new InvalidOperationException("서버 야영은 요리를 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Hiding)) > 0.0001f)
                    throw new InvalidOperationException("서버 야영은 은신을 올리면 안 됩니다.");
                if (!body.IsCampSafe(Time.time))
                    throw new InvalidOperationException("야영 후 CampSafeUntil이 있어야 합니다.");
                if (body.IsHidden(Time.time))
                    throw new InvalidOperationException("야영은 HiddenUntil을 켜면 안 됩니다.");
                int woodLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == "wood")
                        woodLeft += bag.Items[i].Amount;
                if (woodLeft != 1)
                    throw new InvalidOperationException("화덕 근처 야영은 나무를 쓰면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastCampMessage) || world.LastCampMessage.IndexOf("야영", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("야영 메시지가 있어야 합니다.");

                kindleGo = new GameObject("selfcheck-camp-kindle");
                kindleGo.transform.position = fire.transform.position + new Vector3(20f, 0f, 20f);
                var kindleBody = kindleGo.AddComponent<WorldBody>();
                kindleBody.IsAvatar = true;
                kindleBody.RecalcFromStr(30);
                kindleBody.ResetHp();
                var kindleBag = kindleGo.AddComponent<InventoryBag>();
                var noWood = world.TryCamp(kindleBody);
                if (noWood.Applied)
                    throw new InvalidOperationException("화덕 밖·불씨 없이 야영되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(kindleBody).Get(SkillId.Camping)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 야영은 스킬을 올리면 안 됩니다.");
                kindleBag.Add("wood", 1);
                var kindleOk = world.TryCamp(kindleBody);
                if (!kindleOk.Applied)
                    throw new InvalidOperationException("서버 불씨 야영 실패: " + kindleOk.FailReason);
                if (Math.Abs(world.SkillsOf(kindleBody).Get(SkillId.Camping) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("불씨 야영 후 서버 스킬 0.1이어야 합니다.");
                if (!kindleBody.IsCampSafe(Time.time))
                    throw new InvalidOperationException("불씨 야영 후 CampSafeUntil이 있어야 합니다.");
                int kindleWood = 0;
                for (int i = 0; i < kindleBag.Items.Count; i++)
                    if (kindleBag.Items[i].TemplateId == "wood")
                        kindleWood += kindleBag.Items[i].Amount;
                if (kindleWood != 0)
                    throw new InvalidOperationException("화덕 밖 야영은 나무를 1개 써야 합니다.");

                cookGo = new GameObject("selfcheck-camp-cook");
                cookGo.transform.position = fire.transform.position;
                var cookBody = cookGo.AddComponent<WorldBody>();
                cookBody.IsAvatar = true;
                var cookBag = cookGo.AddComponent<InventoryBag>();
                cookBag.Add(ItemCatalog.Fish, 1);
                stGo = new GameObject("selfcheck-camp-st");
                stGo.transform.position = cookGo.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "cooked_fish";
                station.DisplayName = "화덕";
                var cooked = world.TryCraft(cookBody, station);
                if (!cooked.Applied)
                    throw new InvalidOperationException("요리 대조 실패: " + cooked.FailReason);
                if (Math.Abs(world.SkillsOf(cookBody).Get(SkillId.Camping)) > 0.0001f)
                    throw new InvalidOperationException("요리는 야영을 올리면 안 됩니다.");
                if (cookBody.IsCampSafe(Time.time))
                    throw new InvalidOperationException("요리는 CampSafeUntil을 켜면 안 됩니다.");

                var hide = world.TryHide(body);
                if (!hide.Applied)
                    throw new InvalidOperationException("은신 대조 실패: " + hide.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Camping) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("은신은 야영을 올리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (kindleGo != null)
                    UnityEngine.Object.DestroyImmediate(kindleGo);
                if (cookGo != null)
                    UnityEngine.Object.DestroyImmediate(cookGo);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertStealing()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (SkillId.Stealing == SkillId.Lockpicking)
                throw new InvalidOperationException("훔치기 SkillId는 자물쇠따기와 달라야 합니다.");
            if (SkillId.Stealing == SkillId.Camping)
                throw new InvalidOperationException("훔치기 SkillId는 야영과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Stealing) != StatId.Dex)
                throw new InvalidOperationException("훔치기 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Stealing) != "훔치기" || SkillTitles.JobOf(SkillId.Stealing) != "도둑")
                throw new InvalidOperationException("훔치기 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Lockpicking) != "자물쇠따기" || SkillTitles.JobOf(SkillId.Lockpicking) != "자물쇠공")
                throw new InvalidOperationException("자물쇠따기 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Camping) != "야영" || SkillTitles.JobOf(SkillId.Camping) != "야영꾼")
                throw new InvalidOperationException("야영 스킬명/직업명을 바꾸면 안 됩니다.");
            if (StealingResolve.LowestLoot(1, 1) != "gold")
                throw new InvalidOperationException("훔치기는 최저가 골드 1을 먼저 집어야 합니다.");
            if (StealingResolve.LowestLoot(0, 2) != ItemCatalog.Cloth)
                throw new InvalidOperationException("골드가 없으면 천 1을 집어야 합니다.");

            var ghostSkills = new SkillSet();
            var ghost = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = ghostSkills, Ghost = true, HasPack = true, PackGold = 1 });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 훔치기는 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("실패한 훔치기는 스킬을 올리면 안 됩니다.");

            var noneSkills = new SkillSet();
            var none = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = noneSkills, HasPack = false, PackGold = 1 });
            if (none.Applied || none.FailReason != "no_pack")
                throw new InvalidOperationException("팩 없는 훔치기는 실패해야 합니다(플레이어 가방 아님).");
            if (Math.Abs(noneSkills.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("팩 없는 훔치기는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = StealingResolve.Resolve(new StealingRequest
            {
                Now = 1f,
                Skills = farSkills,
                HasPack = true,
                PackGold = 1,
                Distance = StealingResolve.StealRange + 1f,
                Range = StealingResolve.StealRange
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 훔치기는 실패해야 합니다.");
            if (Math.Abs(farSkills.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("사거리 밖 훔치기는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = StealingResolve.Resolve(new StealingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasPack = true,
                PackGold = 2,
                PackCloth = 1,
                Distance = 1f,
                Difficulty = StealingResolve.Difficulty
            });
            if (!ok.Applied || !ok.Stolen || ok.Criminal || ok.LootId != "gold")
                throw new InvalidOperationException("조용한 훔치기는 골드 1을 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Stealing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 훔치기 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Lockpicking)) > 0.0001f)
                throw new InvalidOperationException("훔치기는 자물쇠따기를 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("훔치기 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("훔치기는 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("훔치기는 STR을 올리면 안 됩니다.");

            var clothOnly = new SkillSet();
            var clothOk = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = clothOnly, HasPack = true, PackCloth = 1, Distance = 1f });
            if (!clothOk.Applied || !clothOk.Stolen || clothOk.LootId != ItemCatalog.Cloth)
                throw new InvalidOperationException("골드 없는 팩은 천을 훔쳐야 합니다.");

            var guardSkills = new SkillSet();
            var guard = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = guardSkills, HasPack = true, PackGold = 1, Distance = 1f, InGuardZone = true });
            if (!guard.Applied || guard.Stolen || !guard.Criminal || guard.FailReason != "guard")
                throw new InvalidOperationException("가드존 실패는 범죄이고 아이템을 주면 안 됩니다.");
            if (Math.Abs(guardSkills.Get(SkillId.Stealing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("가드존 실패 시도도 0.1이어야 합니다.");

            var witSkills = new SkillSet();
            var wit = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = witSkills, HasPack = true, PackGold = 1, Distance = 1f, Witnessed = true });
            if (!wit.Applied || wit.Stolen || !wit.Criminal || wit.FailReason != "witness")
                throw new InvalidOperationException("목격 실패는 범죄이고 아이템을 주면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Stealing, SkillLock.Locked);
            var lockedOk = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = locked, HasPack = true, PackGold = 1, Distance = 1f });
            if (!lockedOk.Applied || !lockedOk.Stolen)
                throw new InvalidOperationException("잠긴 훔치기도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 훔치기는 오르면 안 됩니다.");

            var pickSkills = new SkillSet();
            LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = pickSkills, HasCrate = true, HasLockpick = true });
            if (Math.Abs(pickSkills.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("자물쇠따기는 훔치기를 올리면 안 됩니다.");

            var created = CharacterCreate.Build("steal-check", "도둑", 0, 20, 40, 20,
                new[] { SkillId.Stealing, SkillId.Lockpicking, SkillId.Camping },
                new[] { 50f, 30f, 20f });
            bool hasPick = false, hasLute = false, hasWood = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lockpick)
                    hasPick = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
                if (created.Inventory[i].TemplateId == "wood")
                    hasWood = true;
            }
            if (!hasPick)
                throw new InvalidOperationException("자물쇠따기 시작은 자물쇠를 줘야 합니다.");
            if (hasLute)
                throw new InvalidOperationException("훔치기 시작은 류트를 주면 안 됩니다.");
            if (!hasWood)
                throw new InvalidOperationException("야영 시작은 나무를 줘야 합니다.");

            var village = GameObject.Find("LockedCrate");
            if (village == null || village.GetComponent<LockedCrate>() == null)
                throw new InvalidOperationException("마을 Kenney 상자(LockedCrate)를 훔치기 팩으로 재사용해야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var go = new GameObject("selfcheck-steal");
            GameObject worldGo = null;
            GameObject packGo = null;
            GameObject guardGo = null;
            GameObject guardPackGo = null;
            GameObject witGo = null;
            GameObject witPackGo = null;
            GameObject otherGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-steal-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                world.ResetHousePlot();
                go.transform.position = new Vector3(40f, 0f, 0f);
                if (GuardZone.Contains(go.transform.position.x, go.transform.position.z))
                    throw new InvalidOperationException("성공 훔치기 더미는 GuardZone 밖이어야 합니다.");
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                body.Gold = 0;
                var bag = go.AddComponent<InventoryBag>();

                packGo = new GameObject("selfcheck-steal-pack");
                packGo.transform.position = go.transform.position;
                var pack = packGo.AddComponent<LockedCrate>();
                pack.GoldLoot = 2;
                pack.ClothLoot = 1;
                pack.Opened = false;

                otherGo = new GameObject("selfcheck-steal-other");
                otherGo.transform.position = go.transform.position + new Vector3(30f, 0f, 0f);
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.RecalcFromStr(30);
                other.ResetHp();
                var otherBag = otherGo.AddComponent<InventoryBag>();
                otherBag.Add(ItemCatalog.Cloth, 3);

                var hit = world.TrySteal(body);
                if (!hit.Applied || !hit.Stolen)
                    throw new InvalidOperationException("서버 조용 훔치기 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Stealing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 훔치기 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Lockpicking)) > 0.0001f)
                    throw new InvalidOperationException("서버 훔치기는 자물쇠따기를 올리면 안 됩니다.");
                if (body.Gold != 1 || pack.GoldLoot != 1 || pack.ClothLoot != 1)
                    throw new InvalidOperationException("성공 훔치기는 최저가 골드 1만 가져야 합니다.");
                if (pack.Opened)
                    throw new InvalidOperationException("훔치기는 상자를 열면 안 됩니다.");
                if (body.Notoriety == NotorietyId.Criminal)
                    throw new InvalidOperationException("조용한 성공은 범죄가 아니어야 합니다.");
                int otherCloth = 0;
                for (int i = 0; i < otherBag.Items.Count; i++)
                    if (otherBag.Items[i].TemplateId == ItemCatalog.Cloth)
                        otherCloth += otherBag.Items[i].Amount;
                if (otherCloth != 3)
                    throw new InvalidOperationException("다른 플레이어 가방을 건드리면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastStealMessage) || world.LastStealMessage.IndexOf("훔", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("훔치기 메시지가 있어야 합니다.");

                guardGo = new GameObject("selfcheck-steal-guard");
                guardGo.transform.position = Vector3.zero;
                if (!GuardZone.Contains(0f, 0f))
                    throw new InvalidOperationException("가드존 실패 더미는 GuardZone 안이어야 합니다.");
                var guardBody = guardGo.AddComponent<WorldBody>();
                guardBody.IsAvatar = true;
                guardBody.RecalcFromStr(30);
                guardBody.ResetHp();
                guardBody.Gold = 0;
                guardGo.AddComponent<InventoryBag>();
                guardPackGo = new GameObject("selfcheck-steal-guard-pack");
                guardPackGo.transform.position = Vector3.zero;
                var guardPack = guardPackGo.AddComponent<LockedCrate>();
                guardPack.GoldLoot = 1;
                guardPack.ClothLoot = 0;
                var guardHit = world.TrySteal(guardBody);
                if (!guardHit.Applied || guardHit.Stolen || !guardHit.Criminal)
                    throw new InvalidOperationException("서버 가드존 실패는 범죄여야 합니다: " + guardHit.FailReason);
                if (guardBody.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("가드존 실패는 FlagCriminal이어야 합니다.");
                if (guardBody.Gold != 0 || guardPack.GoldLoot != 1)
                    throw new InvalidOperationException("가드존 실패는 아이템을 주면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(guardBody).Get(SkillId.Stealing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("가드존 실패 시도도 서버 스킬 0.1이어야 합니다.");

                witGo = new GameObject("selfcheck-steal-wit");
                witGo.transform.position = new Vector3(50f, 0f, 0f);
                var witBody = witGo.AddComponent<WorldBody>();
                witBody.IsAvatar = true;
                witBody.RecalcFromStr(30);
                witBody.ResetHp();
                witBody.Gold = 0;
                witGo.AddComponent<InventoryBag>();
                witPackGo = new GameObject("selfcheck-steal-wit-pack");
                witPackGo.transform.position = witGo.transform.position;
                var witPack = witPackGo.AddComponent<LockedCrate>();
                witPack.GoldLoot = 1;
                other.transform.position = witGo.transform.position;
                var witHit = world.TrySteal(witBody);
                if (!witHit.Applied || witHit.Stolen || !witHit.Criminal)
                    throw new InvalidOperationException("서버 목격 실패는 범죄여야 합니다: " + witHit.FailReason);
                if (witBody.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("목격 실패는 FlagCriminal이어야 합니다.");
                if (witBody.Gold != 0 || witPack.GoldLoot != 1)
                    throw new InvalidOperationException("목격 실패는 아이템을 주면 안 됩니다.");
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                UnityEngine.Object.DestroyImmediate(go);
                if (packGo != null)
                    UnityEngine.Object.DestroyImmediate(packGo);
                if (guardGo != null)
                    UnityEngine.Object.DestroyImmediate(guardGo);
                if (guardPackGo != null)
                    UnityEngine.Object.DestroyImmediate(guardPackGo);
                if (witGo != null)
                    UnityEngine.Object.DestroyImmediate(witGo);
                if (witPackGo != null)
                    UnityEngine.Object.DestroyImmediate(witPackGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertLockpickingSlice()
        {
            if (SkillId.Lockpicking == SkillId.Stealth)
                throw new InvalidOperationException("자물쇠따기 SkillId는 잠행과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Lockpicking) != StatId.Dex)
                throw new InvalidOperationException("자물쇠따기 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Lockpicking) != "자물쇠따기" || SkillTitles.JobOf(SkillId.Lockpicking) != "자물쇠공")
                throw new InvalidOperationException("자물쇠따기 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Stealth) != "잠행")
                throw new InvalidOperationException("잠행 스킬명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghost = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = ghostSkills, Ghost = true, HasCrate = true, HasLockpick = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 자물쇠따기는 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Lockpicking)) > 0.0001f)
                throw new InvalidOperationException("실패한 자물쇠따기는 스킬을 올리면 안 됩니다.");

            var noCrate = new SkillSet();
            var missCrate = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = noCrate, HasCrate = false, HasLockpick = true });
            if (missCrate.Applied || missCrate.FailReason != "no_crate")
                throw new InvalidOperationException("상자 없는 자물쇠따기는 실패해야 합니다(플레이어 훔치기 아님).");
            if (Math.Abs(noCrate.Get(SkillId.Lockpicking)) > 0.0001f)
                throw new InvalidOperationException("상자 없는 자물쇠따기는 스킬을 올리면 안 됩니다.");

            var noPick = new SkillSet();
            var missPick = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = noPick, HasCrate = true, HasLockpick = false });
            if (missPick.Applied)
                throw new InvalidOperationException("자물쇠 없는 따기는 실패해야 합니다.");

            var opened = new SkillSet();
            var missOpen = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = opened, HasCrate = true, CrateOpened = true, HasLockpick = true });
            if (missOpen.Applied)
                throw new InvalidOperationException("이미 연 상자는 다시 따면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dex0 = stats.Dex;
            var ok = LockpickingResolve.Resolve(new LockpickingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasCrate = true,
                HasLockpick = true,
                Difficulty = LockpickingResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("자물쇠따기는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Lockpicking) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 자물쇠따기 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("자물쇠따기는 잠행을 올리면 안 됩니다.");
            if (stats.Dex <= dex0)
                throw new InvalidOperationException("자물쇠따기 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != StatSet.DefaultInt)
                throw new InvalidOperationException("자물쇠따기는 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Lockpicking, SkillLock.Locked);
            var lockedOk = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = locked, HasCrate = true, HasLockpick = true });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 자물쇠따기도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Lockpicking)) > 0.0001f)
                throw new InvalidOperationException("잠긴 자물쇠따기는 오르면 안 됩니다.");

            var created = CharacterCreate.Build("pick-check", "자물쇠공", 0, 20, 40, 20,
                new[] { SkillId.Lockpicking, SkillId.Tactics, SkillId.Anatomy },
                new[] { 50f, 30f, 20f });
            bool hasPick = false;
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lockpick)
                    hasPick = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (!hasPick)
                throw new InvalidOperationException("자물쇠따기 시작은 자물쇠를 줘야 합니다.");
            if (hasLute)
                throw new InvalidOperationException("자물쇠따기 시작은 류트를 주면 안 됩니다.");
            if (ItemCatalog.BuyPrice(ItemCatalog.Lockpick) <= 0)
                throw new InvalidOperationException("잡화가 자물쇠를 팔아야 합니다.");
            var recipe = CraftRecipes.Find("lockpick");
            if (recipe == null || recipe.Output != ItemCatalog.Lockpick || recipe.Ingredient != "iron_ore" || recipe.Count != 1 || recipe.Skill != SkillId.Blacksmithing)
                throw new InvalidOperationException("자물쇠는 철광석 1로 대장간 제작이어야 합니다.");

            var village = GameObject.Find("LockedCrate");
            if (village == null)
                throw new InvalidOperationException("마을에 Kenney 잠긴 상자(LockedCrate)가 있어야 합니다.");
            var villageCrate = village.GetComponent<LockedCrate>();
            if (villageCrate == null)
                throw new InvalidOperationException("LockedCrate 컴포넌트가 있어야 합니다.");
            if (!GuardZone.Contains(village.transform.position.x, village.transform.position.z))
                throw new InvalidOperationException("잠긴 상자는 마을 가드존 안이어야 합니다.");

            var go = new GameObject("selfcheck-pick");
            GameObject worldGo = null;
            GameObject crateGo = null;
            GameObject forgeGo = null;
            GameObject otherGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-pick-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                body.Gold = 0;
                var bag = go.AddComponent<InventoryBag>();

                crateGo = new GameObject("selfcheck-crate");
                crateGo.transform.position = go.transform.position;
                var crate = crateGo.AddComponent<LockedCrate>();
                crate.DisplayName = "잠긴 상자";
                crate.GoldLoot = 8;
                crate.ClothLoot = 1;

                otherGo = new GameObject("selfcheck-other");
                otherGo.transform.position = go.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                var otherBag = otherGo.AddComponent<InventoryBag>();
                otherBag.Add(ItemCatalog.Cloth, 3);

                var theft = world.TryPick(body, null);
                if (theft.Applied)
                    throw new InvalidOperationException("플레이어 대상 자물쇠따기는 없어야 합니다.");

                var miss = world.TryPick(body, crate);
                if (miss.Applied)
                    throw new InvalidOperationException("서버 자물쇠 없는 따기는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Lockpicking)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 따기는 스킬을 올리면 안 됩니다.");

                forgeGo = new GameObject("selfcheck-pick-forge");
                forgeGo.transform.position = go.transform.position;
                var station = forgeGo.AddComponent<CraftStation>();
                station.RecipeId = "iron_sword";
                station.DisplayName = "대장간";
                bag.Add("iron_ore", 1);
                var crafted = world.TryCraft(body, station, "lockpick");
                if (!crafted.Applied)
                    throw new InvalidOperationException("대장간 자물쇠 제작 실패: " + crafted.FailReason);
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.Lockpick))
                    throw new InvalidOperationException("철광석 1 → 자물쇠 1이어야 합니다.");

                int otherCloth = 0;
                for (int i = 0; i < otherBag.Items.Count; i++)
                    if (otherBag.Items[i].TemplateId == ItemCatalog.Cloth)
                        otherCloth += otherBag.Items[i].Amount;
                var hit = world.TryPick(body, crate);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 자물쇠따기 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Lockpicking) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 따기 후 서버 스킬 0.1이어야 합니다.");
                if (!crate.Opened)
                    throw new InvalidOperationException("성공하면 상자가 열려야 합니다.");
                if (body.Gold != 8)
                    throw new InvalidOperationException("열린 상자 골드 보상이 있어야 합니다.");
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.Cloth))
                    throw new InvalidOperationException("열린 상자 천 보상이 있어야 합니다.");
                if (ItemCatalog.Has(bag.Items, ItemCatalog.Lockpick))
                    throw new InvalidOperationException("성공 따기는 자물쇠를 소모해야 합니다.");
                int otherClothAfter = 0;
                for (int i = 0; i < otherBag.Items.Count; i++)
                    if (otherBag.Items[i].TemplateId == ItemCatalog.Cloth)
                        otherClothAfter += otherBag.Items[i].Amount;
                if (otherClothAfter != otherCloth)
                    throw new InvalidOperationException("다른 플레이어 가방을 건드리면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastPickMessage) || world.LastPickMessage.IndexOf("열림", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("따기 메시지가 있어야 합니다.");

                bag.Add(ItemCatalog.Lockpick, 1);
                var again = world.TryPick(body, crate);
                if (again.Applied)
                    throw new InvalidOperationException("한 번 연 상자는 다시 열리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (crateGo != null)
                    UnityEngine.Object.DestroyImmediate(crateGo);
                if (forgeGo != null)
                    UnityEngine.Object.DestroyImmediate(forgeGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertAnimalLoreSlice()
        {
            if (SkillId.AnimalLore == SkillId.Tracking)
                throw new InvalidOperationException("동물지식 SkillId는 추적과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.AnimalLore) != StatId.Int)
                throw new InvalidOperationException("동물지식 Primary는 INT이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.AnimalLore) != "동물지식" || SkillTitles.JobOf(SkillId.AnimalLore) != "동물학자")
                throw new InvalidOperationException("동물지식 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Tracking) != "추적")
                throw new InvalidOperationException("추적 스킬명을 바꾸면 안 됩니다.");
            if (MobCatalog.TamableOf(MobCatalog.Bandit) || MobCatalog.TamableOf("wolf"))
                throw new InvalidOperationException("동물지식은 조련 가능으로 표시하면 안 됩니다.");

            var noneSkills = new SkillSet();
            var none = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasTarget = false
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 동물지식은 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("실패한 동물지식은 스킬을 올리면 안 됩니다.");

            var playerSkills = new SkillSet();
            var player = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = playerSkills,
                HasTarget = true,
                TargetEnemy = false,
                TargetAlive = true,
                TargetKind = "플레이어"
            });
            if (player.Applied || player.FailReason != "not_mob")
                throw new InvalidOperationException("플레이어 대상 동물지식은 실패해야 합니다(조련/펫 아님).");

            var farSkills = new SkillSet();
            var far = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetKind = "도적"
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 동물지식은 들어가면 안 됩니다.");

            var deadSkills = new SkillSet();
            var dead = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = deadSkills,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = false,
                TargetKind = "도적"
            });
            if (dead.Applied)
                throw new InvalidOperationException("죽은 몹 동물지식은 실패해야 합니다(시체 추적이 아님).");

            MobCatalog.LoreStats(MobCatalog.Bandit, out int bStr, out int bRes, out int bMin, out int bMax);
            var skills = new SkillSet();
            var stats = new StatSet();
            int intWas = stats.Int;
            int dexWas = stats.Dex;
            var ok = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetKind = "도적",
                MobId = MobCatalog.Bandit,
                Hp = 33f,
                MaxHp = 45f,
                Str = bStr,
                Resist = bRes,
                DamageMin = bMin,
                DamageMax = bMax,
                Tamable = true,
                Difficulty = AnimalLoreResolve.Difficulty
            });
            if (!ok.Applied || ok.Kind != "도적" || Math.Abs(ok.Hp - 33f) > 0.0001f || Math.Abs(ok.MaxHp - 45f) > 0.0001f)
                throw new InvalidOperationException("동물지식은 종류/HP를 밝혀야 합니다.");
            if (ok.Str != 28 || ok.Resist != 1 || ok.DamageBand != "4-8")
                throw new InvalidOperationException("동물지식은 추적보다 STR/저항/피해밴드를 더 줘야 합니다.");
            if (ok.Tamable)
                throw new InvalidOperationException("동물지식 결과는 조련불가여야 합니다.");
            if (Math.Abs(skills.Get(SkillId.AnimalLore) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 동물지식 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("동물지식은 추적을 올리면 안 됩니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("동물지식 상승 시 INT가 올라야 합니다.");
            if (stats.Dex != dexWas)
                throw new InvalidOperationException("동물지식은 DEX를 올리면 안 됩니다.");

            var trackSkills = new SkillSet();
            var track = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = trackSkills,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적",
                Hp = 45f,
                MaxHp = 45f
            });
            if (!track.Applied)
                throw new InvalidOperationException("추적 대조 실패");
            if (Math.Abs(trackSkills.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("추적은 동물지식을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.AnimalLore, SkillLock.Locked);
            var lockedOk = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetKind = "도적",
                Hp = 12f,
                MaxHp = 45f,
                Str = 28,
                Resist = 1,
                DamageMin = 4,
                DamageMax = 8
            });
            if (!lockedOk.Applied || lockedOk.Kind != "도적" || lockedOk.Tamable)
                throw new InvalidOperationException("잠긴 동물지식도 정보는 보여야 하고 조련불가야 합니다.");
            if (Math.Abs(locked.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("잠긴 동물지식은 오르면 안 됩니다.");

            var go = new GameObject("selfcheck-lore");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject playerGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-lore-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;

                var missing = world.TryLore(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 동물지식은 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.AnimalLore)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 동물지식은 스킬을 올리면 안 됩니다.");

                playerGo = new GameObject("selfcheck-lore-player");
                playerGo.transform.position = go.transform.position;
                var other = playerGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.IsEnemy = false;
                other.DisplayName = "다른이";
                var pHit = world.TryLore(body, other);
                if (pHit.Applied)
                    throw new InvalidOperationException("서버 동물지식은 플레이어를 살피면 안 됩니다.");

                tgtGo = new GameObject("selfcheck-lore-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();
                tgt.SetHp(33f);

                var hit = world.TryLore(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 동물지식 실패: " + hit.FailReason);
                if (hit.Kind != "도적" || Math.Abs(hit.Hp - 33f) > 0.0001f)
                    throw new InvalidOperationException("서버 동물지식은 종류/HP를 밝혀야 합니다.");
                if (hit.Str != 28 || hit.Resist != 1 || hit.DamageBand != "4-8" || hit.Tamable)
                    throw new InvalidOperationException("서버 동물지식은 STR/저항/피해밴드와 조련불가를 줘야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.AnimalLore) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 동물지식 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tracking)) > 0.0001f)
                    throw new InvalidOperationException("서버 동물지식은 추적을 올리면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastLoreMessage) || world.LastLoreMessage.IndexOf("도적", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("HP", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("STR", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("저항", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("4-8", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("조련불가", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("동물지식 메시지가 추적보다 많은 정보를 포함해야 합니다: " + world.LastLoreMessage);
                if (!string.IsNullOrEmpty(world.LastTrackMessage) && world.LastTrackMessage.IndexOf("조련불가", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("추적 메시지에 동물지식 정보가 섞이면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (playerGo != null)
                    UnityEngine.Object.DestroyImmediate(playerGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertVeterinarySlice()
        {
            if (SkillId.Veterinary == SkillId.Healing)
                throw new InvalidOperationException("수의학 SkillId는 치유와 달라야 합니다.");
            if (SkillId.Veterinary == SkillId.AnimalLore)
                throw new InvalidOperationException("수의학 SkillId는 동물지식과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Veterinary) != StatId.Dex)
                throw new InvalidOperationException("수의학 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학" || SkillTitles.JobOf(SkillId.Veterinary) != "수의사")
                throw new InvalidOperationException("수의학 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Healing) != "치유")
                throw new InvalidOperationException("치유 스킬명을 바꾸면 안 됩니다.");

            var noneSkills = new SkillSet();
            var none = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasBandage = true,
                HasTarget = false,
                TargetHp = 10f,
                TargetMaxHp = 45f
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 수의학은 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("실패한 수의학은 스킬을 올리면 안 됩니다.");

            var playerSkills = new SkillSet();
            var player = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = playerSkills,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = false,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (player.Applied || player.FailReason != "not_mob")
                throw new InvalidOperationException("플레이어/아군 대상 수의학은 실패해야 합니다(치유와 구분).");

            var farSkills = new SkillSet();
            var far = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 45f
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 수의학은 들어가면 안 됩니다.");

            var deadSkills = new SkillSet();
            var dead = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = deadSkills,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = false,
                TargetHp = 0f,
                TargetMaxHp = 45f
            });
            if (dead.Applied)
                throw new InvalidOperationException("죽은 몹 수의학은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = noBnSkills,
                HasBandage = false,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 45f
            });
            if (noBn.Applied)
                throw new InvalidOperationException("붕대 없이 수의학되면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            var ok = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 45f,
                Difficulty = VeterinaryResolve.Difficulty
            });
            if (!ok.Applied || ok.Damage < 1)
                throw new InvalidOperationException("수의학은 산 몹을 치료해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 수의학 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("수의학은 치유를 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("수의학은 동물지식을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("수의학 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("수의학은 INT를 올리면 안 됩니다.");

            var healSkills = new SkillSet();
            var heal = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 1f,
                Skills = healSkills,
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (!heal.Applied)
                throw new InvalidOperationException("치유 대조 실패");
            if (Math.Abs(healSkills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("치유는 수의학을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Veterinary, SkillLock.Locked);
            var lockedOk = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 45f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 수의학도 치료는 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("잠긴 수의학은 오르면 안 됩니다.");

            var created = CharacterCreate.Build("vet-check", "수의", 0, 20, 40, 20,
                new[] { SkillId.Veterinary, SkillId.Healing, SkillId.Tailoring },
                new[] { 50f, 30f, 20f });
            bool hasBn = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Bandage && created.Inventory[i].Amount >= 10)
                    hasBn = true;
            }
            if (!hasBn)
                throw new InvalidOperationException("수의학 시작은 붕대를 줘야 합니다.");

            var go = new GameObject("selfcheck-vet");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject playerGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-vet-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryVet(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 수의학은 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Veterinary)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 수의학은 스킬을 올리면 안 됩니다.");

                playerGo = new GameObject("selfcheck-vet-player");
                playerGo.transform.position = go.transform.position;
                var other = playerGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.IsEnemy = false;
                other.MaxHp = 50f;
                other.ResetHp();
                other.SetHp(20f);
                bag.Add(ItemCatalog.Bandage, 3);
                var pHit = world.TryVet(body, other);
                if (pHit.Applied)
                    throw new InvalidOperationException("서버 수의학은 플레이어를 치료하면 안 됩니다(치유 영역).");

                tgtGo = new GameObject("selfcheck-vet-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();
                tgt.SetHp(10f);

                var hit = world.TryVet(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 수의학 실패: " + hit.FailReason);
                if (tgt.Hp <= 10f)
                    throw new InvalidOperationException("서버 수의학은 산 몹 HP를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 수의학 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Healing)) > 0.0001f)
                    throw new InvalidOperationException("서버 수의학은 치유를 올리면 안 됩니다.");
                int leftBn = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        leftBn += bag.Items[i].Amount;
                if (leftBn != 2)
                    throw new InvalidOperationException("성공 수의학은 붕대를 소모해야 합니다.");
                if (string.IsNullOrEmpty(world.LastVetMessage) || world.LastVetMessage.IndexOf("도적", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("수의학 메시지가 있어야 합니다: " + world.LastVetMessage);

                var foeHeal = world.TryHeal(body, tgt);
                if (foeHeal.Applied)
                    throw new InvalidOperationException("치유는 여전히 적 몹에 들어가면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (playerGo != null)
                    UnityEngine.Object.DestroyImmediate(playerGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }




        static void AssertInscription()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (SkillId.Inscription == SkillId.Magery)
                throw new InvalidOperationException("각인 SkillId는 마법과 달라야 합니다.");
            if (SkillId.Inscription == SkillId.Alchemy)
                throw new InvalidOperationException("각인 SkillId는 연금술과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Inscription) != StatId.Int)
                throw new InvalidOperationException("각인 Primary는 INT이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Inscription) != "각인" || SkillTitles.JobOf(SkillId.Inscription) != "각인사")
                throw new InvalidOperationException("각인 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Magery) != "마법" || SkillNames.KoreanOf(SkillId.Alchemy) != "연금술")
                throw new InvalidOperationException("마법/연금술 스킬명을 바꾸면 안 됩니다.");
            if (ItemCatalog.ScrollEmber != "scroll_ember")
                throw new InvalidOperationException("주문서 템플릿은 scroll_ember여야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.ScrollEmber) <= 0 || ItemCatalog.WeightOf(ItemCatalog.ScrollEmber) <= 0f)
                throw new InvalidOperationException("scroll_ember 무게/가격이 없습니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.Blank) <= 0 || ItemCatalog.WeightOf(ItemCatalog.Blank) <= 0f)
                throw new InvalidOperationException("blank 무게/가격이 없습니다.");

            var unlearned = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = false,
                HasCloth = true,
                Skills = new SkillSet()
            });
            if (unlearned.Applied || unlearned.FailReason != "unlearned")
                throw new InvalidOperationException("불씨를 모르면 각인되면 안 됩니다.");

            var noMatSkills = new SkillSet();
            var noMat = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = false,
                HasBlank = false,
                Skills = noMatSkills
            });
            if (noMat.Applied)
                throw new InvalidOperationException("천/blank 없이 각인되면 안 됩니다.");
            if (Math.Abs(noMatSkills.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("실패한 각인은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int intWas = stats.Int;
            int dexWas = stats.Dex;
            var ok = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = true,
                Skills = skills,
                Stats = stats,
                Difficulty = InscriptionResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("천 각인은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Inscription) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("각인 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("각인은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Alchemy)) > 0.0001f)
                throw new InvalidOperationException("각인은 연금술을 올리면 안 됩니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("각인 상승 시 INT가 올라야 합니다.");
            if (stats.Dex != dexWas)
                throw new InvalidOperationException("각인은 DEX를 올리면 안 됩니다.");

            var blankOk = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasBlank = true,
                Skills = new SkillSet()
            });
            if (!blankOk.Applied)
                throw new InvalidOperationException("blank 각인도 성공해야 합니다.");

            var mag = new SkillSet();
            SkillGain.TryRaise(mag, SkillId.Magery, 20f, out _, out _);
            if (Math.Abs(mag.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("마법은 각인을 올리면 안 됩니다.");
            var alch = new SkillSet();
            SkillGain.TryRaise(alch, SkillId.Alchemy, 10f, out _, out _);
            if (Math.Abs(alch.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("연금술은 각인을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Inscription, SkillLock.Locked);
            var lockedOk = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = true,
                Skills = locked
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 각인도 주문서는 만들어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("잠긴 각인은 오르면 안 됩니다.");

            var noScroll = ScrollUseResolve.Resolve(new ScrollUseRequest { HasScroll = false, HasTarget = true, TargetEnemy = true });
            if (noScroll.Applied || noScroll.FailReason != "no_scroll")
                throw new InvalidOperationException("주문서 없이 쓰면 안 됩니다.");
            var noTgt = ScrollUseResolve.Resolve(new ScrollUseRequest { HasScroll = true, HasTarget = false });
            if (noTgt.Applied)
                throw new InvalidOperationException("대상 없는 주문서는 실패해야 합니다.");

            var created = CharacterCreate.Build("insc-check", "각인", 0, 20, 20, 40,
                new[] { SkillId.Inscription, SkillId.Magery, SkillId.Alchemy },
                new[] { 50f, 30f, 20f });
            bool hasClothStart = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Cloth && created.Inventory[i].Amount >= 1)
                    hasClothStart = true;
            }
            if (!hasClothStart)
                throw new InvalidOperationException("각인 시작은 천을 줘야 합니다.");
            bool hasEmber = false;
            if (created.Spells != null)
            {
                for (int i = 0; i < created.Spells.Length; i++)
                    if (created.Spells[i] == (int)SpellId.Ember)
                        hasEmber = true;
            }
            if (!hasEmber)
                throw new InvalidOperationException("마법 시작은 불씨를 알아야 합니다(각인과 별개).");

            var go = new GameObject("selfcheck-insc");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-insc-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(25);
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryInscribe(body);
                if (missing.Applied)
                    throw new InvalidOperationException("재료/주문 없이 서버 각인되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Inscription)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 각인은 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.Cloth, 1);
                var stillUnknown = world.TryInscribe(body);
                if (stillUnknown.Applied || stillUnknown.FailReason != "unlearned")
                    throw new InvalidOperationException("불씨를 모르면 서버 각인되면 안 됩니다.");
                world.BookOf(body).Learn(SpellId.Ember);
                bag.Add(ItemCatalog.Blank, 1);
                var blankHit = world.TryInscribe(body);
                if (!blankHit.Applied)
                    throw new InvalidOperationException("서버 blank 각인 실패: " + blankHit.FailReason);
                int scrolls = 0, blanks = 0, clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrolls += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Blank)
                        blanks += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (scrolls != 1 || blanks != 0 || clothLeft != 1)
                    throw new InvalidOperationException("blank를 우선 소모하고 scroll_ember 1을 만들어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Inscription) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 각인 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 각인은 마법을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("서버 각인은 연금술을 올리면 안 됩니다.");
                if (world.LastInscribeMessage != ItemCatalog.ScrollEmber)
                    throw new InvalidOperationException("각인 메시지가 있어야 합니다: " + world.LastInscribeMessage);

                var clothHit = world.TryInscribe(body);
                if (!clothHit.Applied)
                    throw new InvalidOperationException("서버 천 각인 실패: " + clothHit.FailReason);
                scrolls = 0;
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrolls += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (scrolls != 2 || clothLeft != 0)
                    throw new InvalidOperationException("천 1 → scroll_ember이어야 합니다.");

                tgtGo = new GameObject("selfcheck-insc-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 40f;
                tgt.ResetHp();
                float hpWas = tgt.Hp;
                int resinWas = 0;
                bag.Add(SpellCast.Reagent, 2);
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinWas += bag.Items[i].Amount;
                float manaWas = body.Mana;
                float magWas = world.SkillsOf(body).Get(SkillId.Magery);
                var used = world.TryUseScroll(body, tgt);
                if (!used.Applied)
                    throw new InvalidOperationException("주문서 사용 실패: " + used.FailReason);
                if (tgt.Hp >= hpWas)
                    throw new InvalidOperationException("주문서는 불씨 피해를 줘야 합니다.");
                int scrollsLeft = 0, resinLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrollsLeft += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinLeft += bag.Items[i].Amount;
                }
                if (scrollsLeft != 1)
                    throw new InvalidOperationException("사용한 주문서는 소모되어야 합니다.");
                if (resinLeft != resinWas)
                    throw new InvalidOperationException("주문서는 시약을 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(body.Mana - manaWas) > 0.01f)
                    throw new InvalidOperationException("주문서는 마나를 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery) - magWas) > 0.0001f)
                    throw new InvalidOperationException("주문서 사용은 마법을 올리면 안 됩니다.");

                var used2 = world.TryUseScroll(body, tgt);
                if (!used2.Applied)
                    throw new InvalidOperationException("두 번째 주문서 사용 실패: " + used2.FailReason);
                scrollsLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrollsLeft += bag.Items[i].Amount;
                if (scrollsLeft != 0)
                    throw new InvalidOperationException("두 번째 사용 후 주문서는 없어야 합니다.");
                var used3 = world.TryUseScroll(body, tgt);
                if (used3.Applied)
                    throw new InvalidOperationException("소모된 주문서를 다시 쓰면 안 됩니다.");

                bag.Add(SpellCast.Reagent, 1);
                body.SetMana(body.MaxMana);
                var ember = world.TryCast(body, SpellId.Ember, tgt);
                if (!ember.Applied)
                    throw new InvalidOperationException("마법 불씨는 각인과 별개로 유지되어야 합니다: " + ember.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Magery) < 0.09f)
                    throw new InvalidOperationException("마법 불씨는 마법을 올려야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertPoisoning()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (SkillId.Poisoning == SkillId.Alchemy)
                throw new InvalidOperationException("독 SkillId는 연금술과 달라야 합니다.");
            if (SkillId.Poisoning == SkillId.Veterinary)
                throw new InvalidOperationException("독 SkillId는 수의학과 달라야 합니다.");
            if (SkillId.Poisoning == SkillId.Magery)
                throw new InvalidOperationException("독 SkillId는 마법과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Poisoning) != StatId.Dex)
                throw new InvalidOperationException("독 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Poisoning) != "독" || SkillTitles.JobOf(SkillId.Poisoning) != "독살자")
                throw new InvalidOperationException("독 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Alchemy) != "연금술" || SkillTitles.JobOf(SkillId.Alchemy) != "연금술사")
                throw new InvalidOperationException("연금술 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학" || SkillTitles.JobOf(SkillId.Veterinary) != "수의사")
                throw new InvalidOperationException("수의학 스킬명/직업명을 바꾸면 안 됩니다.");
            if (ItemCatalog.PoisonVial != "poison_vial")
                throw new InvalidOperationException("독병 템플릿은 poison_vial여야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.PoisonVial) <= 0 || ItemCatalog.WeightOf(ItemCatalog.PoisonVial) <= 0f)
                throw new InvalidOperationException("poison_vial 무게/가격이 없습니다.");
            var rec = CraftRecipes.Find("poison_vial");
            if (rec == null || rec.Ingredient != ItemCatalog.Cloth || rec.Output != ItemCatalog.PoisonVial
                || rec.Skill != SkillId.Poisoning || rec.Count != 1)
                throw new InvalidOperationException("천 1 → 독병 레시피가 있어야 합니다.");
            if (!ItemCatalog.IsMeleeWeapon(ItemCatalog.IronSword) || ItemCatalog.IsMeleeWeapon(ItemCatalog.WoodenBow))
                throw new InvalidOperationException("근접은 검/둔기/창, 활은 원거리여야 합니다.");

            var noMeleeSkills = new SkillSet();
            var noMelee = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = false,
                HasPotion = true,
                Skills = noMeleeSkills
            });
            if (noMelee.Applied || noMelee.FailReason != "no_melee")
                throw new InvalidOperationException("근접 무기 없이 도포되면 안 됩니다.");
            if (Math.Abs(noMeleeSkills.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("실패한 도포는 스킬을 올리면 안 됩니다.");

            var noPoisonSkills = new SkillSet();
            var noPoison = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = false,
                HasVial = false,
                Skills = noPoisonSkills
            });
            if (noPoison.Applied || noPoison.FailReason != "no_poison")
                throw new InvalidOperationException("물약/독병 없이 도포되면 안 됩니다.");
            if (Math.Abs(noPoisonSkills.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("재료 없는 도포는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = true,
                Skills = skills,
                Stats = stats,
                Difficulty = PoisoningResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("연금 물약 도포는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Poisoning) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("독 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Alchemy)) > 0.0001f)
                throw new InvalidOperationException("독은 연금술을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("독은 수의학을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("독은 마법을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("독 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("독은 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("독은 STR을 올리면 안 됩니다.");

            var vialOk = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasVial = true,
                Skills = new SkillSet()
            });
            if (!vialOk.Applied)
                throw new InvalidOperationException("천 독병 도포도 성공해야 합니다.");

            var mag = new SkillSet();
            SkillGain.TryRaise(mag, SkillId.Magery, 20f, out _, out _);
            if (Math.Abs(mag.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("마법은 독을 올리면 안 됩니다.");
            var alch = new SkillSet();
            SkillGain.TryRaise(alch, SkillId.Alchemy, 10f, out _, out _);
            if (Math.Abs(alch.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("연금술은 독을 올리면 안 됩니다.");
            var vet = new SkillSet();
            SkillGain.TryRaise(vet, SkillId.Veterinary, 10f, out _, out _);
            if (Math.Abs(vet.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("수의학은 독을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Poisoning, SkillLock.Locked);
            var lockedOk = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = true,
                Skills = locked
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 독도 도포는 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("잠긴 독은 오르면 안 됩니다.");

            var created = CharacterCreate.Build("poison-check", "독살", 0, 20, 40, 20,
                new[] { SkillId.Poisoning, SkillId.Alchemy, SkillId.Veterinary },
                new[] { 50f, 30f, 20f });
            bool hasClothStart = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Cloth && created.Inventory[i].Amount >= 1)
                    hasClothStart = true;
            }
            if (!hasClothStart)
                throw new InvalidOperationException("독 시작은 천을 줘야 합니다.");

            var go = new GameObject("selfcheck-poison");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-poison-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryPoisonWeapon(body);
                if (missing.Applied)
                    throw new InvalidOperationException("무기/재료 없이 서버 도포되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Poisoning)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 도포는 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.HealthPotion, 1);
                var stillNoMelee = world.TryPoisonWeapon(body);
                if (stillNoMelee.Applied || stillNoMelee.FailReason != "no_melee")
                    throw new InvalidOperationException("근접 무기 없이 서버 도포되면 안 됩니다.");

                bag.Add(ItemCatalog.WoodenBow, 1);
                var bowOnly = world.TryPoisonWeapon(body);
                if (bowOnly.Applied || bowOnly.FailReason != "no_melee")
                    throw new InvalidOperationException("활만 있으면 도포되면 안 됩니다.");
                bag.TakeOne(ItemCatalog.WoodenBow);

                bag.Add(ItemCatalog.IronSword, 1);
                var potionHit = world.TryPoisonWeapon(body);
                if (!potionHit.Applied)
                    throw new InvalidOperationException("서버 물약 도포 실패: " + potionHit.FailReason);
                int pots = 0, vials = 0, clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.HealthPotion)
                        pots += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.PoisonVial)
                        vials += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (pots != 0)
                    throw new InvalidOperationException("도포한 연금 물약은 소모되어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Poisoning) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 도포 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 연금술을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Veterinary)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 수의학을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 마법을 올리면 안 됩니다.");
                if (world.LastPoisonMessage != "poison")
                    throw new InvalidOperationException("도포 메시지가 있어야 합니다: " + world.LastPoisonMessage);

                tgtGo = new GameObject("selfcheck-poison-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 80f;
                tgt.ResetHp();
                float hpWas = tgt.Hp;
                float manaWas = body.Mana;
                int resinWas = 0;
                bag.Add(SpellCast.Reagent, 2);
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinWas += bag.Items[i].Amount;
                float magWas = world.SkillsOf(body).Get(SkillId.Magery);
                var hit = world.TryAttack(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("독 무기 공격 실패: " + hit.FailReason);
                float afterHit = tgt.Hp;
                if (afterHit >= hpWas - hit.Damage + 0.01f)
                    throw new InvalidOperationException("다음 TryAttack은 독 HP 틱을 줘야 합니다.");
                if (Math.Abs(afterHit - (hpWas - hit.Damage - PoisoningResolve.TickDamage)) > 0.01f)
                    throw new InvalidOperationException("첫 독 틱은 공격과 함께 들어가야 합니다.");
                world.TickPoison(UnityEngine.Time.time + PoisoningResolve.TickInterval);
                world.TickPoison(UnityEngine.Time.time + PoisoningResolve.TickInterval * 2);
                float afterTicks = tgt.Hp;
                float expect = hpWas - hit.Damage - PoisoningResolve.TickDamage * PoisoningResolve.TickCount;
                if (Math.Abs(afterTicks - expect) > 0.01f)
                    throw new InvalidOperationException("짧은 HP 틱이 모두 들어가야 합니다.");
                int resinLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinLeft += bag.Items[i].Amount;
                if (resinLeft != resinWas)
                    throw new InvalidOperationException("독은 시약을 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(body.Mana - manaWas) > 0.01f)
                    throw new InvalidOperationException("독은 마나를 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery) - magWas) > 0.0001f)
                    throw new InvalidOperationException("독 공격은 마법을 올리면 안 됩니다.");
                if (tgt.PoisonTicks != 0)
                    throw new InvalidOperationException("짧은 틱이 끝나면 독 잔여가 없어야 합니다.");

                bag.Add(ItemCatalog.Cloth, 1);
                var clothHit = world.TryPoisonWeapon(body);
                if (!clothHit.Applied)
                    throw new InvalidOperationException("서버 천 독병 도포 실패: " + clothHit.FailReason);
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft != 0)
                    throw new InvalidOperationException("천 독병은 소모되어야 합니다.");

                bag.Add(ItemCatalog.PoisonVial, 1);
                var vialHit = world.TryPoisonWeapon(body);
                if (!vialHit.Applied)
                    throw new InvalidOperationException("서버 poison_vial 도포 실패: " + vialHit.FailReason);
                vials = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.PoisonVial)
                        vials += bag.Items[i].Amount;
                if (vials != 0)
                    throw new InvalidOperationException("도포한 독병은 소모되어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertTamingSlice()
        {
            if (SkillId.AnimalTaming == SkillId.Veterinary || SkillId.AnimalTaming == SkillId.AnimalLore)
                throw new InvalidOperationException("조련 SkillId는 수의학/동물지식과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.AnimalTaming) != StatId.Dex)
                throw new InvalidOperationException("조련 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.AnimalTaming) != "조련" || SkillTitles.JobOf(SkillId.AnimalTaming) != "조련사")
                throw new InvalidOperationException("조련 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학" || SkillNames.KoreanOf(SkillId.AnimalLore) != "동물지식")
                throw new InvalidOperationException("수의학/동물지식 스킬명을 바꾸면 안 됩니다.");
            if (MobCatalog.KindCount != 8)
                throw new InvalidOperationException("사냥 몹 종류 수는 그대로 8이어야 합니다.");
            if (MobCatalog.TamableOf(MobCatalog.Bandit) || MobCatalog.TamableOf("wolf") || MobCatalog.TamableOf(MobCatalog.Skeleton))
                throw new InvalidOperationException("사냥 몹은 조련불가여야 합니다.");
            if (!MobCatalog.TamableOf(TameCritter.Id) || TameCritter.ControlSlots != 1)
                throw new InvalidOperationException("야생하트는 Tameable=true ControlSlots=1이어야 합니다.");
            if (!MobCatalog.TamableOf(TameBoar.Id) || TameBoar.ControlSlots != 1)
                throw new InvalidOperationException("야생멧돼지는 Tameable=true ControlSlots=1이어야 합니다.");
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");

            var go = GameObject.Find(TameCritter.Object);
            var body = go != null ? go.GetComponent<WorldBody>() : null;
            if (body == null || !body.Tameable || body.IsEnemy || body.ControlSlots != 1)
                throw new InvalidOperationException("가드존 밖 비적대 조련 대상이 있어야 합니다.");
            if (body.MobId != TameCritter.Id || body.DisplayName != TameCritter.DisplayName)
                throw new InvalidOperationException("조련 대상 카탈로그가 야생하트여야 합니다.");
            Vector3 pos = go.transform.position;
            if (Math.Abs(pos.x - TameCritter.X) > 0.4f || Math.Abs(pos.z - TameCritter.Z) > 0.4f)
                throw new InvalidOperationException("조련 대상 좌표가 지정 위치와 같아야 합니다.");
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("조련 대상은 GuardZone 밖이어야 합니다.");
            float[] lxs = { HousingPlot.X, 18.2f, 3.4f, -12.2f, Dungeon1.EntranceX, Dungeon2.EntranceX, 0f };
            float[] lzs = { HousingPlot.Z, 2.4f, -19.6f, 20.4f, Dungeon1.EntranceZ, Dungeon2.EntranceZ, 13.2f };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = pos.x - lxs[i];
                float dz = pos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 36f)
                    throw new InvalidOperationException("조련 대상이 기존 랜드마크와 겹치면 안 됩니다.");
            }

            var none = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = false, Distance = 1f });
            if (none.Applied || none.FailReason != "not_tameable")
                throw new InvalidOperationException("조련불가 대상은 실패해야 합니다.");
            var owned = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, AlreadyPet = true, Distance = 1f });
            if (owned.Applied || owned.FailReason != "already_pet")
                throw new InvalidOperationException("이미 펫이면 실패해야 합니다.");
            var slot = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, UsedSlots = 1, ControlSlots = 1, FollowerCap = 1, Distance = 1f });
            if (slot.Applied || slot.FailReason != "no_slot")
                throw new InvalidOperationException("슬롯 없으면 실패해야 합니다.");
            var far = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, Distance = 20f });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("너무 멀면 조련 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            var ok = TameResolve.Tame(new TameRequest
            {
                Distance = 1f,
                Tameable = true,
                Skills = skills,
                Stats = stats,
                ControlSlots = 1,
                FollowerCap = 1
            });
            if (!ok.Applied)
                throw new InvalidOperationException("조련 성공해야 합니다: " + ok.FailReason);
            if (Math.Abs(skills.Get(SkillId.AnimalTaming) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 조련 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f || Math.Abs(skills.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("조련은 수의학/동물지식을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1 || stats.Int != intWas)
                throw new InvalidOperationException("조련 상승 시 DEX만 올라야 합니다.");

            var stranger = TameResolve.Follow(new PetCommandRequest { HasPet = true, IsOwner = false });
            if (stranger.Applied || stranger.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 펫을 명령하면 안 됩니다.");

            var worldGo = new GameObject("selfcheck-tame-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            GameObject huntGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ownerGo = new GameObject("selfcheck-tame-owner");
                ownerGo.transform.position = go.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "tame-owner";
                owner.ResetHp();

                huntGo = new GameObject("selfcheck-tame-hunt");
                huntGo.transform.position = go.transform.position;
                var hunt = huntGo.AddComponent<WorldBody>();
                hunt.IsEnemy = true;
                hunt.MobId = MobCatalog.Bandit;
                hunt.Tameable = false;
                hunt.ResetHp();
                var huntTame = world.TryTame(owner, hunt);
                if (huntTame.Applied)
                    throw new InvalidOperationException("사냥 몹은 조련되면 안 됩니다.");

                var hit = world.TryTame(owner, body);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 조련 실패: " + hit.FailReason);
                if (body.OwnerCharacterId != owner.CharacterId || !body.PetFollow)
                    throw new InvalidOperationException("성공 조련 후 펫이 주인을 따라야 합니다.");
                if (Math.Abs(world.SkillsOf(owner).Get(SkillId.AnimalTaming) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 조련 후 서버 스킬 0.1이어야 합니다.");

                otherGo = new GameObject("selfcheck-tame-other");
                otherGo.transform.position = go.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "tame-other";
                other.ResetHp();
                var otherCmd = world.TryPetFollow(other, body);
                if (otherCmd.Applied)
                    throw new InvalidOperationException("타인은 펫 follow를 하면 안 됩니다.");
                var otherRel = world.TryPetRelease(other, body);
                if (otherRel.Applied)
                    throw new InvalidOperationException("타인은 펫 release를 하면 안 됩니다.");

                ownerGo.transform.position = go.transform.position + new Vector3(4f, 0f, 2f);
                world.TickPets();
                float fx = ownerGo.transform.position.x + TameCritter.FollowOffsetX;
                float fz = ownerGo.transform.position.z + TameCritter.FollowOffsetZ;
                if (Math.Abs(body.transform.position.x - fx) > 0.05f || Math.Abs(body.transform.position.z - fz) > 0.05f)
                    throw new InvalidOperationException("펫은 서버 틱에서 주인 오프셋을 따라야 합니다.");

                var rel = world.TryPetRelease(owner, body);
                if (!rel.Applied)
                    throw new InvalidOperationException("주인은 펫을 놓아줘야 합니다: " + rel.FailReason);
                if (!string.IsNullOrEmpty(body.OwnerCharacterId) || body.PetFollow)
                    throw new InvalidOperationException("release 후 소유가 없어야 합니다.");

                var again = world.TryTame(owner, body);
                if (!again.Applied)
                    throw new InvalidOperationException("놓아준 대상은 다시 조련할 수 있어야 합니다: " + again.FailReason);

                ownerGo.transform.position = Vector3.zero;
                otherGo.transform.position = Vector3.zero;
                var assault = world.TryAttack(owner, other);
                if (assault.Applied || assault.FailReason != "innocent")
                    throw new InvalidOperationException("마을 가드존 무고 공격은 막혀야 합니다.");
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (huntGo != null)
                    UnityEngine.Object.DestroyImmediate(huntGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                body.OwnerCharacterId = "";
                body.PetFollow = false;
                body.PetGuard = false;
                go.transform.SetPositionAndRotation(new Vector3(TameCritter.X, go.transform.position.y, TameCritter.Z), go.transform.rotation);
            }
        }




        static void AssertPetCommands()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            string[] prefabs = { StableYard.Object, HousingPlot.VendorObject };
            for (int i = 0; i < prefabs.Length; i++)
            {
                var landmark = GameObject.Find(prefabs[i]);
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(landmark);
                if (string.IsNullOrEmpty(path) || path.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidOperationException("마을 랜드마크는 Prefab이어야 합니다(RAW fbx 아님): " + prefabs[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            var stranger = TameResolve.Stay(new PetCommandRequest { HasPet = true, IsOwner = false });
            if (stranger.Applied || stranger.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 Stay를 하면 안 됩니다.");
            var strangerG = TameResolve.Guard(new PetCommandRequest { HasPet = true, IsOwner = false });
            if (strangerG.Applied || strangerG.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 Guard를 하면 안 됩니다.");
            var ghost = TameResolve.Stay(new PetCommandRequest { Ghost = true, HasPet = true, IsOwner = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 Stay를 하면 안 됩니다.");
            var none = TameResolve.Guard(new PetCommandRequest { HasPet = false, IsOwner = true });
            if (none.Applied || none.FailReason != "not_pet")
                throw new InvalidOperationException("펫 없이 Guard하면 안 됩니다.");
            var okStay = TameResolve.Stay(new PetCommandRequest { HasPet = true, IsOwner = true });
            if (!okStay.Applied)
                throw new InvalidOperationException("주인은 Stay를 해야 합니다.");
            var okGuard = TameResolve.Guard(new PetCommandRequest { HasPet = true, IsOwner = true });
            if (!okGuard.Applied)
                throw new InvalidOperationException("주인은 Guard를 해야 합니다.");

            var worldGo = new GameObject("selfcheck-petcmd-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            GameObject petGo = null;
            GameObject mobGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ownerGo = new GameObject("selfcheck-petcmd-owner");
                ownerGo.transform.position = new Vector3(40f, 0f, 40f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "petcmd-owner";
                owner.ResetHp();

                petGo = new GameObject("selfcheck-petcmd-pet");
                petGo.transform.position = ownerGo.transform.position;
                var pet = petGo.AddComponent<WorldBody>();
                pet.Tameable = true;
                pet.IsEnemy = false;
                pet.MobId = TameCritter.Id;
                pet.DisplayName = TameCritter.DisplayName;
                pet.ControlSlots = 1;
                pet.ResetHp();

                var tamed = world.TryTame(owner, pet);
                if (!tamed.Applied)
                    throw new InvalidOperationException("펫명령 조련 실패: " + tamed.FailReason);
                if (!pet.PetFollow || pet.PetGuard)
                    throw new InvalidOperationException("조련 직후 Follow이고 Guard가 아니어야 합니다.");

                otherGo = new GameObject("selfcheck-petcmd-other");
                otherGo.transform.position = ownerGo.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "petcmd-other";
                other.ResetHp();
                var otherStay = world.TryPetStay(other, pet);
                if (otherStay.Applied)
                    throw new InvalidOperationException("타인은 TryPetStay를 하면 안 됩니다.");
                var otherGuard = world.TryPetGuard(other, pet);
                if (otherGuard.Applied)
                    throw new InvalidOperationException("타인은 TryPetGuard를 하면 안 됩니다.");
                if (!pet.PetFollow || pet.PetGuard)
                    throw new InvalidOperationException("타인 실패 후 Follow 상태가 유지되어야 합니다.");

                ownerGo.transform.position = new Vector3(44f, 0f, 42f);
                world.TickPets();
                float fx = ownerGo.transform.position.x + TameCritter.FollowOffsetX;
                float fz = ownerGo.transform.position.z + TameCritter.FollowOffsetZ;
                if (Math.Abs(pet.transform.position.x - fx) > 0.05f || Math.Abs(pet.transform.position.z - fz) > 0.05f)
                    throw new InvalidOperationException("Follow 중 펫은 오프셋을 따라야 합니다.");

                var stay = world.TryPetStay(owner, pet);
                if (!stay.Applied)
                    throw new InvalidOperationException("주인은 Stay를 해야 합니다: " + stay.FailReason);
                if (pet.PetFollow || pet.PetGuard)
                    throw new InvalidOperationException("Stay 후 PetFollow=false, PetGuard=false여야 합니다.");
                Vector3 held = pet.transform.position;
                ownerGo.transform.position = new Vector3(50f, 0f, 50f);
                world.TickPets();
                if (Math.Abs(pet.transform.position.x - held.x) > 0.01f || Math.Abs(pet.transform.position.z - held.z) > 0.01f)
                    throw new InvalidOperationException("Stay 후 펫은 자리를 지켜야 합니다.");

                mobGo = new GameObject("selfcheck-petcmd-mob");
                mobGo.transform.position = pet.transform.position;
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.MobId = MobCatalog.Bandit;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();
                ownerGo.transform.position = pet.transform.position;
                float hpStay = mob.Hp;
                world.TryEnemyStrike(mob, owner);
                if (mob.Hp < hpStay)
                    throw new InvalidOperationException("Stay 중 펫은 주인을 지키며 공격하면 안 됩니다.");

                var guard = world.TryPetGuard(owner, pet);
                if (!guard.Applied)
                    throw new InvalidOperationException("주인은 Guard를 해야 합니다: " + guard.FailReason);
                if (!pet.PetFollow || !pet.PetGuard)
                    throw new InvalidOperationException("Guard 후 PetFollow=true, PetGuard=true여야 합니다.");
                world.TickPets();
                fx = ownerGo.transform.position.x + TameCritter.FollowOffsetX;
                fz = ownerGo.transform.position.z + TameCritter.FollowOffsetZ;
                if (Math.Abs(pet.transform.position.x - fx) > 0.05f || Math.Abs(pet.transform.position.z - fz) > 0.05f)
                    throw new InvalidOperationException("Guard 중 펫은 주인 오프셋에 있어야 합니다.");

                mobGo.transform.position = ownerGo.transform.position;
                pet.transform.position = ownerGo.transform.position;
                mob.ResetHp();
                float hpBefore = mob.Hp;
                bool struck = world.TryEnemyStrike(mob, owner);
                if (!struck)
                    throw new InvalidOperationException("가드 검증용 몹 타격이 들어가야 합니다.");
                if (mob.Hp >= hpBefore)
                    throw new InvalidOperationException("Guard 펫은 주인을 친 몹을 공격해야 합니다.");

                var follow = world.TryPetFollow(owner, pet);
                if (!follow.Applied)
                    throw new InvalidOperationException("주인은 다시 Follow를 해야 합니다: " + follow.FailReason);
                if (!pet.PetFollow || pet.PetGuard)
                    throw new InvalidOperationException("Follow 후 PetGuard가 꺼져야 합니다.");
                mob.ResetHp();
                hpBefore = mob.Hp;
                world.TryEnemyStrike(mob, owner);
                if (mob.Hp < hpBefore)
                    throw new InvalidOperationException("Follow 중 펫은 자동 반격하면 안 됩니다.");
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (petGo != null)
                    UnityEngine.Object.DestroyImmediate(petGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertPetAttack()
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

            OfflineWorld.Instance?.ResetHousePlot();

            var stranger = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = false, HasEnemy = true });
            if (stranger.Applied || stranger.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 Attack을 하면 안 됩니다.");
            var ghost = TameResolve.Attack(new PetCommandRequest { Ghost = true, HasPet = true, IsOwner = true, HasEnemy = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 Attack을 하면 안 됩니다.");
            var none = TameResolve.Attack(new PetCommandRequest { HasPet = false, IsOwner = true, HasEnemy = true });
            if (none.Applied || none.FailReason != "not_pet")
                throw new InvalidOperationException("펫 없이 Attack하면 안 됩니다.");
            var noEnemy = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = true, HasEnemy = false });
            if (noEnemy.Applied || noEnemy.FailReason != "no_enemy")
                throw new InvalidOperationException("적 없이 Attack하면 안 됩니다.");
            var dead = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = true, HasEnemy = true, PetAlive = false });
            if (dead.Applied || dead.FailReason != "dead")
                throw new InvalidOperationException("죽은 펫 Attack은 실패해야 합니다.");
            var stabled = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = true, HasEnemy = true, PetStabled = true });
            if (stabled.Applied || stabled.FailReason != "stabled")
                throw new InvalidOperationException("마구간 펫 Attack은 실패해야 합니다.");
            var ok = TameResolve.Attack(new PetCommandRequest { HasPet = true, IsOwner = true, HasEnemy = true });
            if (!ok.Applied)
                throw new InvalidOperationException("주인은 Attack을 해야 합니다.");

            var worldGo = new GameObject("selfcheck-petatk-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            GameObject petGo = null;
            GameObject mobGo = null;
            GameObject avatarGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                ownerGo = new GameObject("selfcheck-petatk-owner");
                ownerGo.transform.position = new Vector3(40f, 0f, 40f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "petatk-owner";
                owner.ResetHp();

                petGo = new GameObject("selfcheck-petatk-pet");
                petGo.transform.position = ownerGo.transform.position;
                var pet = petGo.AddComponent<WorldBody>();
                pet.Tameable = true;
                pet.IsEnemy = false;
                pet.MobId = TameCritter.Id;
                pet.DisplayName = TameCritter.DisplayName;
                pet.ControlSlots = 1;
                pet.ResetHp();

                var tamed = world.TryTame(owner, pet);
                if (!tamed.Applied)
                    throw new InvalidOperationException("펫공격 조련 실패: " + tamed.FailReason);

                otherGo = new GameObject("selfcheck-petatk-other");
                otherGo.transform.position = ownerGo.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "petatk-other";
                other.ResetHp();

                mobGo = new GameObject("selfcheck-petatk-mob");
                mobGo.transform.position = ownerGo.transform.position + new Vector3(1.5f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.MobId = MobCatalog.Bandit;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                var otherAtk = world.TryPetAttack(other, pet, mob);
                if (otherAtk.Applied)
                    throw new InvalidOperationException("타인은 TryPetAttack을 하면 안 됩니다.");

                owner.Ghost = true;
                var ghostAtk = world.TryPetAttack(owner, pet, mob);
                if (ghostAtk.Applied || ghostAtk.FailReason != "ghost")
                    throw new InvalidOperationException("유령 주인은 TryPetAttack 실패해야 합니다.");
                owner.Ghost = false;

                avatarGo = new GameObject("selfcheck-petatk-avatar");
                avatarGo.transform.position = ownerGo.transform.position + new Vector3(2f, 0f, 0f);
                var victim = avatarGo.AddComponent<WorldBody>();
                victim.IsAvatar = true;
                victim.IsEnemy = false;
                victim.CharacterId = "petatk-victim";
                victim.ResetHp();
                var pvp = world.TryPetAttack(owner, pet, victim);
                if (pvp.Applied || pvp.FailReason != "no_enemy")
                    throw new InvalidOperationException("펫은 아바타 Open PvP Attack을 하면 안 됩니다.");

                float hpBefore = mob.Hp;
                var atk = world.TryPetAttack(owner, pet, mob);
                if (!atk.Applied)
                    throw new InvalidOperationException("주인은 TryPetAttack을 해야 합니다: " + atk.FailReason);
                if (pet.PetFollow || pet.PetGuard || pet.PetAttackTarget != mob)
                    throw new InvalidOperationException("Attack 후 PetFollow/PetGuard 꺼지고 PetAttackTarget이 몹이어야 합니다.");

                world.TickPets();
                if (mob.Hp >= hpBefore)
                    throw new InvalidOperationException("Attack 펫은 TickPets에서 몹을 공격해야 합니다.");
                float dist = Vector3.Distance(pet.transform.position, mob.transform.position);
                if (dist > ItemCatalog.MeleeRange + 1.2f)
                    throw new InvalidOperationException("Attack 펫은 몹 근처로 추격해야 합니다.");

                var stay = world.TryPetStay(owner, pet);
                if (!stay.Applied)
                    throw new InvalidOperationException("Attack 후 Stay가 되어야 합니다.");
                if (pet.PetAttackTarget != null)
                    throw new InvalidOperationException("Stay 후 PetAttackTarget이 비어야 합니다.");

                pet.PetStabled = true;
                var stabAtk = world.TryPetAttack(owner, pet, mob);
                if (stabAtk.Applied || stabAtk.FailReason != "stabled")
                    throw new InvalidOperationException("마구간 펫 TryPetAttack은 실패해야 합니다.");
                pet.PetStabled = false;

                pet.ApplyDamage((int)pet.MaxHp + 10);
                if (pet.Alive)
                    throw new InvalidOperationException("펫이 죽어야 합니다.");
                var deadAtk = world.TryPetAttack(owner, pet, mob);
                if (deadAtk.Applied || deadAtk.FailReason != "dead")
                    throw new InvalidOperationException("죽은 펫 TryPetAttack은 실패해야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (petGo != null)
                    UnityEngine.Object.DestroyImmediate(petGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (avatarGo != null)
                    UnityEngine.Object.DestroyImmediate(avatarGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertPetCome()
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

            OfflineWorld.Instance?.ResetHousePlot();

            var stranger = TameResolve.Come(new PetCommandRequest { HasPet = true, IsOwner = false });
            if (stranger.Applied || stranger.FailReason != "not_owner")
                throw new InvalidOperationException("타인은 Come을 하면 안 됩니다.");
            var ghost = TameResolve.Come(new PetCommandRequest { Ghost = true, HasPet = true, IsOwner = true });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 Come을 하면 안 됩니다.");
            var none = TameResolve.Come(new PetCommandRequest { HasPet = false, IsOwner = true });
            if (none.Applied || none.FailReason != "not_pet")
                throw new InvalidOperationException("펫 없이 Come하면 안 됩니다.");
            var dead = TameResolve.Come(new PetCommandRequest { HasPet = true, IsOwner = true, PetAlive = false });
            if (dead.Applied || dead.FailReason != "dead")
                throw new InvalidOperationException("죽은 펫 Come은 실패해야 합니다.");
            var stabled = TameResolve.Come(new PetCommandRequest { HasPet = true, IsOwner = true, PetStabled = true });
            if (stabled.Applied || stabled.FailReason != "stabled")
                throw new InvalidOperationException("마구간 펫 Come은 실패해야 합니다.");
            var ok = TameResolve.Come(new PetCommandRequest { HasPet = true, IsOwner = true });
            if (!ok.Applied)
                throw new InvalidOperationException("주인은 Come을 해야 합니다.");

            var worldGo = new GameObject("selfcheck-petcome-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            GameObject petGo = null;
            GameObject mobGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                ownerGo = new GameObject("selfcheck-petcome-owner");
                ownerGo.transform.position = new Vector3(42f, 0f, 42f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "petcome-owner";
                owner.ResetHp();

                petGo = new GameObject("selfcheck-petcome-pet");
                petGo.transform.position = ownerGo.transform.position;
                var pet = petGo.AddComponent<WorldBody>();
                pet.Tameable = true;
                pet.IsEnemy = false;
                pet.MobId = TameCritter.Id;
                pet.DisplayName = TameCritter.DisplayName;
                pet.ControlSlots = 1;
                pet.ResetHp();

                var tamed = world.TryTame(owner, pet);
                if (!tamed.Applied)
                    throw new InvalidOperationException("펫호출 조련 실패: " + tamed.FailReason);

                otherGo = new GameObject("selfcheck-petcome-other");
                otherGo.transform.position = ownerGo.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "petcome-other";
                other.ResetHp();

                mobGo = new GameObject("selfcheck-petcome-mob");
                mobGo.transform.position = ownerGo.transform.position + new Vector3(6f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.MobId = MobCatalog.Bandit;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                var otherCome = world.TryPetCome(other, pet);
                if (otherCome.Applied)
                    throw new InvalidOperationException("타인은 TryPetCome을 하면 안 됩니다.");

                owner.Ghost = true;
                var ghostCome = world.TryPetCome(owner, pet);
                if (ghostCome.Applied || ghostCome.FailReason != "ghost")
                    throw new InvalidOperationException("유령 주인은 TryPetCome 실패해야 합니다.");
                owner.Ghost = false;

                var atk = world.TryPetAttack(owner, pet, mob);
                if (!atk.Applied)
                    throw new InvalidOperationException("Come 전 TryPetAttack이 되어야 합니다: " + atk.FailReason);
                if (pet.PetAttackTarget != mob)
                    throw new InvalidOperationException("Attack 후 PetAttackTarget이 몹이어야 합니다.");

                pet.transform.position = mob.transform.position + new Vector3(0.5f, 0f, 0.5f);
                var come = world.TryPetCome(owner, pet);
                if (!come.Applied)
                    throw new InvalidOperationException("주인은 TryPetCome을 해야 합니다: " + come.FailReason);
                if (!pet.PetFollow || pet.PetGuard || pet.PetAttackTarget != null)
                    throw new InvalidOperationException("Come 후 PetFollow 켜지고 PetGuard/PetAttackTarget이 비어야 합니다.");

                world.TickPets();
                Vector3 want = owner.transform.position + new Vector3(TameCritter.FollowOffsetX, 0f, TameCritter.FollowOffsetZ);
                float dist = Vector3.Distance(new Vector3(pet.transform.position.x, 0f, pet.transform.position.z), new Vector3(want.x, 0f, want.z));
                if (dist > 0.05f)
                    throw new InvalidOperationException("Come 후 TickPets에서 펫이 주인 오프셋으로 와야 합니다.");

                var stay = world.TryPetStay(owner, pet);
                if (!stay.Applied)
                    throw new InvalidOperationException("Come 후 Stay가 되어야 합니다.");
                pet.transform.position = owner.transform.position + new Vector3(8f, 0f, 0f);
                var come2 = world.TryPetCome(owner, pet);
                if (!come2.Applied)
                    throw new InvalidOperationException("Stay 중 Come이 되어야 합니다.");
                if (!pet.PetFollow)
                    throw new InvalidOperationException("Stay 후 Come은 Follow로 바뀌어야 합니다.");
                world.TickPets();
                dist = Vector3.Distance(new Vector3(pet.transform.position.x, 0f, pet.transform.position.z), new Vector3(want.x, 0f, want.z));
                if (dist > 0.05f)
                    throw new InvalidOperationException("Stay→Come 후 펫이 주인에게 와야 합니다.");

                pet.PetStabled = true;
                var stabCome = world.TryPetCome(owner, pet);
                if (stabCome.Applied || stabCome.FailReason != "stabled")
                    throw new InvalidOperationException("마구간 펫 TryPetCome은 실패해야 합니다.");
                pet.PetStabled = false;

                pet.ApplyDamage((int)pet.MaxHp + 10);
                if (pet.Alive)
                    throw new InvalidOperationException("펫이 죽어야 합니다.");
                var deadCome = world.TryPetCome(owner, pet);
                if (deadCome.Applied || deadCome.FailReason != "dead")
                    throw new InvalidOperationException("죽은 펫 TryPetCome은 실패해야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (petGo != null)
                    UnityEngine.Object.DestroyImmediate(petGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertPetBondVetRez()
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
            if (SkillId.Veterinary == SkillId.Healing)
                throw new InvalidOperationException("수의학 SkillId는 치유와 달라야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학")
                throw new InvalidOperationException("수의학 스킬명을 바꾸면 안 됩니다.");

            var ghostHealer = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                HealerGhost = true,
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (ghostHealer.Applied || ghostHealer.FailReason != "ghost")
                throw new InvalidOperationException("유령 시술자 펫 부활은 실패해야 합니다.");

            var noGhost = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = false,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (noGhost.Applied || noGhost.FailReason != "not_pet_ghost")
                throw new InvalidOperationException("유령 아닌 펫 수의학 부활은 실패해야 합니다.");

            var notBond = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = false,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (notBond.Applied || notBond.FailReason != "not_pet_ghost")
                throw new InvalidOperationException("비 Bonded Ghost 수의학 부활은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = false,
                Distance = 1f,
                Skills = noBnSkills
            });
            if (noBn.Applied || noBn.FailReason != "no_bandage")
                throw new InvalidOperationException("붕대 없는 펫 부활은 실패해야 합니다.");
            if (Math.Abs(noBnSkills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("실패한 펫 부활은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = ItemCatalog.MeleeRange + 1f,
                Range = ItemCatalog.MeleeRange,
                Skills = farSkills
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 펫 부활은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = VeterinaryResurrectResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("펫 부활 Resolve는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 펫 부활 후 Veterinary 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("펫 부활은 치유를 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("펫 부활은 마법을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("펫 부활 상승 시 DEX가 올라야 합니다.");

            var forceSkills = new SkillSet();
            var forced = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = forceSkills,
                Stats = new StatSet(),
                Force = true
            });
            if (!forced.Applied || Math.Abs(forceSkills.Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("Force 경로도 Veterinary 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Veterinary, SkillLock.Locked);
            var lockedOk = VeterinaryResurrectResolve.Resolve(new VeterinaryResurrectRequest
            {
                TargetGhost = true,
                TargetBondedPet = true,
                HasBandage = true,
                Distance = 1f,
                Skills = locked,
                Force = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 Veterinary도 펫 부활 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("잠긴 Veterinary는 오르면 안 됩니다.");

            // Healing bandage rez must still reject pet ghost (avatar-only)
            var healOnPet = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = false,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (healOnPet.Applied)
                throw new InvalidOperationException("플레이어 붕대 부활은 펫 Ghost에 들어가면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var worldGo = new GameObject("selfcheck-petbond-world");
            GameObject ownerGo = null;
            GameObject healerGo = null;
            GameObject petGo = null;
            GameObject wildGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                ownerGo = new GameObject("selfcheck-petbond-owner");
                ownerGo.transform.position = new Vector3(42f, 0f, 0f);
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "petbond-owner";
                owner.RecalcFromStr(30);
                owner.ResetHp();

                healerGo = new GameObject("selfcheck-petbond-healer");
                healerGo.transform.position = ownerGo.transform.position;
                var healer = healerGo.AddComponent<WorldBody>();
                healer.IsAvatar = true;
                healer.CharacterId = "petbond-healer";
                healer.RecalcFromStr(30);
                healer.ResetHp();
                var bag = healerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Bandage, 3);

                petGo = new GameObject("selfcheck-petbond-pet");
                petGo.transform.position = ownerGo.transform.position;
                var pet = petGo.AddComponent<WorldBody>();
                pet.IsEnemy = true;
                pet.MobId = TameCritter.Id;
                pet.DisplayName = TameCritter.DisplayName;
                pet.Tameable = true;
                pet.ControlSlots = TameCritter.ControlSlots;
                pet.MaxHp = 40f;
                pet.ResetHp();

                var tame = world.TryTame(owner, pet);
                if (!tame.Applied)
                    throw new InvalidOperationException("조련 실패: " + tame.FailReason);
                if (!pet.Bonded || pet.OwnerCharacterId != owner.CharacterId)
                    throw new InvalidOperationException("조련 후 Bonded 플래그와 주인이 있어야 합니다.");

                int slotsBefore = world.CountFollowers(owner.CharacterId);
                if (slotsBefore < 1)
                    throw new InvalidOperationException("조련 후 팔로워 슬롯이 잡혀야 합니다.");

                pet.ApplyDamage((int)pet.MaxHp + 20);
                if (pet.Alive || !pet.Ghost)
                    throw new InvalidOperationException("Bonded 펫 HP0은 Ghost가 되어야 합니다.");
                if (string.IsNullOrEmpty(pet.OwnerCharacterId) || !pet.Bonded)
                    throw new InvalidOperationException("Bonded 펫 Ghost는 주인/본드를 유지해야 합니다.");
                if (world.CountFollowers(owner.CharacterId) != slotsBefore)
                    throw new InvalidOperationException("펫 Ghost 후에도 팔로워 슬롯이 유지되어야 합니다.");
                if (OfflineWorld.FindCorpse(owner.CharacterId) != null)
                    throw new InvalidOperationException("펫 Ghost는 시체 룻이 생기면 안 됩니다.");

                // Unbonded death: no ghost
                wildGo = new GameObject("selfcheck-petbond-wild");
                wildGo.transform.position = ownerGo.transform.position + new Vector3(1f, 0f, 0f);
                var wild = wildGo.AddComponent<WorldBody>();
                wild.IsEnemy = true;
                wild.MobId = MobCatalog.Bandit;
                wild.DisplayName = "도적";
                wild.MaxHp = 30f;
                wild.ResetHp();
                wild.Bonded = false;
                wild.ApplyDamage((int)wild.MaxHp + 5);
                if (wild.Ghost)
                    throw new InvalidOperationException("비 Bonded 몹 죽음은 Ghost가 되면 안 됩니다.");

                // player bandage rez must fail on pet ghost
                var playRez = world.TryResurrectBandage(healer, pet);
                if (playRez.Applied)
                    throw new InvalidOperationException("플레이어 붕대 부활은 펫 Ghost에 들어가면 안 됩니다.");

                var hit = world.TryVetResurrect(healer, pet);
                if (!hit.Applied || pet.Ghost || !pet.Alive)
                    throw new InvalidOperationException("TryVetResurrect 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("서버 펫 부활 후 Veterinary 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Healing)) > 0.0001f)
                    throw new InvalidOperationException("서버 펫 부활은 치유를 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 펫 부활은 마법을 올리면 안 됩니다.");
                int left = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        left += bag.Items[i].Amount;
                if (left != 2)
                    throw new InvalidOperationException("성공 펫 부활은 붕대 1을 소모해야 합니다.");
                if (string.IsNullOrEmpty(world.LastVetRezMessage) || world.LastVetRezMessage.IndexOf("부활", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("펫 부활 메시지가 있어야 합니다.");

                // TryVet routes to rez when pet ghost again
                pet.ApplyDamage((int)pet.MaxHp + 20);
                if (!pet.Ghost)
                    throw new InvalidOperationException("재사망 후 Ghost여야 합니다.");
                var viaVet = world.TryVet(healer, pet);
                if (!viaVet.Applied || pet.Ghost)
                    throw new InvalidOperationException("TryVet(pet Ghost)는 부활이어야 합니다: " + viaVet.FailReason);

                // Stable claim is separate — ghost pet still owned, not stabled path
                if (world.HasStabled(owner.CharacterId))
                    throw new InvalidOperationException("펫 부활 슬라이스는 Stable claim이 아닙니다.");

                var rel = world.TryPetRelease(owner, pet);
                if (!rel.Applied)
                    throw new InvalidOperationException("부활 후 release가 되어야 합니다: " + rel.FailReason);
                if (pet.Bonded)
                    throw new InvalidOperationException("release 후 Bonded가 꺼져야 합니다.");

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("Bonded Pet 부활 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (healerGo != null)
                    UnityEngine.Object.DestroyImmediate(healerGo);
                if (petGo != null)
                    UnityEngine.Object.DestroyImmediate(petGo);
                if (wildGo != null)
                    UnityEngine.Object.DestroyImmediate(wildGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertStrengthRequirement()
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

            if (ItemCatalog.StrReqOf(ItemCatalog.IronSword) != 25)
                throw new InvalidOperationException("철검 Strength Requirement는 25여야 합니다.");
            if (ItemCatalog.StrReqOf(ItemCatalog.WoodenClub) != 0 || ItemCatalog.StrReqOf(ItemCatalog.WoodenBow) != 0)
                throw new InvalidOperationException("다른 무기는 Strength Requirement 기본 0이어야 합니다.");

            var lowResolve = EquipResolve.Equip(new EquipRequest
            {
                HasItem = true,
                Str = 10,
                StrReq = ItemCatalog.StrReqOf(ItemCatalog.IronSword),
                TemplateId = ItemCatalog.IronSword
            });
            if (lowResolve.Applied || lowResolve.FailReason != "str_req")
                throw new InvalidOperationException("저 STR EquipResolve는 str_req 실패여야 합니다.");
            string msg = EquipResolve.MessageFor("str_req", ItemCatalog.IronSword, 25);
            if (string.IsNullOrEmpty(msg) || msg.IndexOf("근력") < 0)
                throw new InvalidOperationException("장착 실패 메시지가 명확해야 합니다: " + msg);

            var highResolve = EquipResolve.Equip(new EquipRequest
            {
                HasItem = true,
                Str = 30,
                StrReq = ItemCatalog.StrReqOf(ItemCatalog.IronSword),
                TemplateId = ItemCatalog.IronSword
            });
            if (!highResolve.Applied)
                throw new InvalidOperationException("고 STR EquipResolve는 성공해야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-strreq-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                bodyGo = new GameObject("selfcheck-strreq-body");
                bodyGo.transform.position = new Vector3(2f, 0f, 2f);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "strreq-body";
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });

                world.StatsOf(body).ForceSet(10, 25, 25);
                var low = world.TryEquip(body, ItemCatalog.IronSword);
                if (low.Applied || low.FailReason != "str_req")
                    throw new InvalidOperationException("저 STR TryEquip은 실패해야 합니다: " + low.FailReason);
                if (world.EquippedOf(body) == ItemCatalog.IronSword)
                    throw new InvalidOperationException("저 STR이면 장착되면 안 됩니다.");
                if (world.LastEquipMessage.IndexOf("근력") < 0)
                    throw new InvalidOperationException("저 STR 장착 메시지가 명확해야 합니다: " + world.LastEquipMessage);

                world.StatsOf(body).ForceSet(30, 25, 25);
                var high = world.TryEquip(body, ItemCatalog.IronSword);
                if (!high.Applied)
                    throw new InvalidOperationException("고 STR TryEquip 실패: " + high.FailReason);
                if (world.EquippedOf(body) != ItemCatalog.IronSword)
                    throw new InvalidOperationException("고 STR이면 철검이 장착되어야 합니다.");
                if (world.LastEquipMessage.IndexOf("장착") < 0)
                    throw new InvalidOperationException("고 STR 장착 메시지가 있어야 합니다: " + world.LastEquipMessage);

                var club = world.TryEquip(body, ItemCatalog.WoodenClub);
                if (club.Applied || club.FailReason != "no_item")
                    throw new InvalidOperationException("없는 아이템 장착은 no_item이어야 합니다.");

                var off = world.TryUnequip(body);
                if (!off.Applied || world.EquippedOf(body) != "")
                    throw new InvalidOperationException("TryUnequip 후 장착이 비어야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertOverweight()
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

            if (ItemCatalog.CarryCap(10) != 40 || ItemCatalog.CarryCap(30) != 120)
                throw new InvalidOperationException("CarryCap은 STR*4(최소 10)여야 합니다.");
            if (ItemCatalog.WeightOf("iron_ore") != 2f)
                throw new InvalidOperationException("iron_ore 무게는 2여야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-weight-world");
            GameObject bodyGo = null;
            GameObject veinGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                bodyGo = new GameObject("selfcheck-weight-body");
                bodyGo.transform.position = new Vector3(3f, 0f, 3f);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "weight-body";
                body.RecalcFromStr(10);
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 20 });
                // Cap 40; pickaxe 6 + 17 ore = 40 exactly at cap. Next ore (+2) must fail.
                bag.Add("iron_ore", 17);
                world.StatsOf(body).ForceSet(10, 25, 25);
                float near = bag.TotalWeight();
                int cap = ItemCatalog.CarryCap(10);
                if (near > cap || near + ItemCatalog.WeightOf("iron_ore") <= cap)
                    throw new InvalidOperationException("과적 직전 세팅 실패: " + near + "/" + cap);

                veinGo = new GameObject("IronVein");
                veinGo.transform.position = bodyGo.transform.position;
                var vein = veinGo.AddComponent<ResourceNode>();
                vein.ResourceId = "iron_ore";
                vein.GatherSkill = SkillId.Mining;
                vein.Remaining = 5;
                vein.Capacity = 5;

                var blocked = world.TryGather(body, vein);
                if (blocked.Applied || blocked.FailReason != "overweight")
                    throw new InvalidOperationException("과적 직전 TryGather는 overweight 실패여야 합니다: " + blocked.FailReason);
                if (string.IsNullOrEmpty(world.LastWeightMessage) || world.LastWeightMessage.IndexOf("과적") < 0)
                    throw new InvalidOperationException("과적 메시지가 명확해야 합니다: " + world.LastWeightMessage);
                if (bag.TotalWeight() != near)
                    throw new InvalidOperationException("과적 실패 시 가방 무게가 변하면 안 됩니다.");

                // Drop one ore → room for one gather
                if (!bag.TakeOne("iron_ore"))
                    throw new InvalidOperationException("광석 드롭 실패");
                var afterDrop = world.TryGather(body, vein);
                if (!afterDrop.Applied)
                    throw new InvalidOperationException("드롭 후 TryGather 실패: " + afterDrop.FailReason);

                // Fill near high-STR cap then lower STR... better: raise STR and gather again after filling
                // Reset bag near low cap again, then higher STR succeeds without drop
                bag.Items.Clear();
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 20 });
                bag.Add("iron_ore", 17);
                world.StatsOf(body).ForceSet(10, 25, 25);
                var stillBlocked = world.TryGather(body, vein);
                if (stillBlocked.Applied || stillBlocked.FailReason != "overweight")
                    throw new InvalidOperationException("재과적 TryGather는 실패해야 합니다.");
                world.StatsOf(body).ForceSet(30, 25, 25);
                var highStr = world.TryGather(body, vein);
                if (!highStr.Applied)
                    throw new InvalidOperationException("고 STR TryGather 실패: " + highStr.FailReason);

                // Buy path: low STR near cap
                bag.Items.Clear();
                bag.Add("iron_ore", 20); // 40/40
                world.StatsOf(body).ForceSet(10, 25, 25);
                body.Gold = 100;
                var vendorGo = GameObject.Find("Vendor");
                var vendor = vendorGo != null ? vendorGo.GetComponent<VendorStation>() : null;
                if (vendor == null)
                    throw new InvalidOperationException("VendorStation이 있어야 합니다.");
                bodyGo.transform.position = vendorGo.transform.position;
                var opened = world.TryVendor(body, vendor);
                if (!opened.Applied)
                    throw new InvalidOperationException("TryVendor 실패: " + opened.FailReason);
                var buyFail = world.TryBuy(body, ItemCatalog.Bandage);
                if (buyFail.Applied || buyFail.FailReason != "overweight")
                    throw new InvalidOperationException("과적 TryBuy는 overweight 실패여야 합니다: " + buyFail.FailReason);
                if (world.LastWeightMessage.IndexOf("과적") < 0)
                    throw new InvalidOperationException("과적 구매 메시지가 명확해야 합니다: " + world.LastWeightMessage);
                world.CloseVendor();

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("Weight 슬라이스 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.CloseVendor();
                OfflineWorld.Instance?.ResetHousePlot();
                if (veinGo != null)
                    UnityEngine.Object.DestroyImmediate(veinGo);
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertMeditationArmorPenalty()
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

            if (!ItemCatalog.IsHeavyArmor(ItemCatalog.IronPlate))
                throw new InvalidOperationException("iron_plate는 HeavyArmor여야 합니다.");
            if (ItemCatalog.IsHeavyArmor(ItemCatalog.WoodenShield) || ItemCatalog.IsHeavyArmor(ItemCatalog.Cloth))
                throw new InvalidOperationException("방패/천은 중갑이 아니어야 합니다.");
            if (Math.Abs(MeditationResolve.HeavyMul - 0.5f) > 0.0001f)
                throw new InvalidOperationException("중갑 명상 패널티는 절반(0.5)이어야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int light = MeditationResolve.Amount(skills, stats, false);
            int heavy = MeditationResolve.Amount(skills, stats, true);
            int expected = light / 2;
            if (expected < 1)
                expected = 1;
            if (heavy != expected)
                throw new InvalidOperationException("중갑 명상 회복은 절반이어야 합니다: light=" + light + " heavy=" + heavy);
            if (heavy >= light)
                throw new InvalidOperationException("중갑은 경갑/무갑보다 명상 회복이 낮아야 합니다.");

            var lightReq = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                Stats = new StatSet(),
                Mana = 5f,
                MaxMana = 35f,
                HeavyArmor = false,
                Difficulty = MeditationResolve.Difficulty
            });
            if (!lightReq.Applied || lightReq.Damage != light)
                throw new InvalidOperationException("무갑 명상은 정상 회복이어야 합니다.");

            var heavyReq = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                Stats = new StatSet(),
                Mana = 5f,
                MaxMana = 35f,
                HeavyArmor = true,
                Difficulty = MeditationResolve.Difficulty
            });
            if (!heavyReq.Applied || heavyReq.Damage != expected)
                throw new InvalidOperationException("중갑 명상 Resolve 회복량이 절반이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-med-armor-world");
            GameObject lightGo = null;
            GameObject heavyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                lightGo = new GameObject("selfcheck-med-armor-light");
                lightGo.transform.position = new Vector3(2.2f, 0f, 2.2f);
                var lightBody = lightGo.AddComponent<WorldBody>();
                lightBody.IsAvatar = true;
                lightBody.CharacterId = "med-armor-light";
                lightBody.RecalcFromInt(world.StatsOf(lightBody).Int);
                lightBody.SetMana(4f);
                lightGo.AddComponent<InventoryBag>();
                float beforeLight = lightBody.Mana;
                var lightHit = world.TryMeditate(lightBody);
                if (!lightHit.Applied)
                    throw new InvalidOperationException("무갑 TryMeditate 실패: " + lightHit.FailReason);
                int lightAmt = MeditationResolve.Amount(world.SkillsOf(lightBody), world.StatsOf(lightBody), false);
                if (lightHit.Damage != lightAmt)
                    throw new InvalidOperationException("무갑 TryMeditate 회복량이 정상이어야 합니다.");
                if (lightBody.Mana <= beforeLight)
                    throw new InvalidOperationException("무갑 명상이 마나를 올려야 합니다.");

                heavyGo = new GameObject("selfcheck-med-armor-heavy");
                heavyGo.transform.position = new Vector3(2.4f, 0f, 2.4f);
                var heavyBody = heavyGo.AddComponent<WorldBody>();
                heavyBody.IsAvatar = true;
                heavyBody.CharacterId = "med-armor-heavy";
                heavyBody.RecalcFromInt(world.StatsOf(heavyBody).Int);
                heavyBody.SetMana(4f);
                var heavyBag = heavyGo.AddComponent<InventoryBag>();
                heavyBag.Add(ItemCatalog.IronPlate, 1);
                if (!ItemCatalog.HasHeavyArmor(heavyBag.Items))
                    throw new InvalidOperationException("iron_plate 가방은 HasHeavyArmor여야 합니다.");
                float beforeHeavy = heavyBody.Mana;
                var heavyHit = world.TryMeditate(heavyBody);
                if (!heavyHit.Applied)
                    throw new InvalidOperationException("중갑 TryMeditate 실패: " + heavyHit.FailReason);
                int heavyAmt = MeditationResolve.Amount(world.SkillsOf(heavyBody), world.StatsOf(heavyBody), true);
                if (heavyHit.Damage != heavyAmt)
                    throw new InvalidOperationException("중갑 TryMeditate 회복량이 패널티를 받아야 합니다.");
                if (heavyHit.Damage >= lightHit.Damage)
                    throw new InvalidOperationException("중갑 TryMeditate 회복은 무갑보다 적어야 합니다.");
                if (heavyBody.Mana - beforeHeavy != heavyHit.Damage)
                    throw new InvalidOperationException("중갑 명상 마나 증가가 Damage와 같아야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (lightGo != null)
                    UnityEngine.Object.DestroyImmediate(lightGo);
                if (heavyGo != null)
                    UnityEngine.Object.DestroyImmediate(heavyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertCastInterrupt()
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

            if (SpellCast.BoltCastSeconds <= 0f)
                throw new InvalidOperationException("BoltCastSeconds는 양수여야 합니다.");
            if (!SpellCast.Interruptible(SpellId.Bolt))
                throw new InvalidOperationException("Bolt는 interruptible이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Ember) || SpellCast.Interruptible(SpellId.Mend) || SpellCast.Interruptible(SpellId.Cleanse) || SpellCast.Interruptible(SpellId.Ward) || SpellCast.Interruptible(SpellId.Bind) || SpellCast.Interruptible(SpellId.Weaken) || SpellCast.Interruptible(SpellId.Spark))
                throw new InvalidOperationException("Ember/Mend/Cleanse/Ward/Bind/Weaken/Spark는 이 슬라이스에서 non-interruptible이어야 합니다.");
            if (SpellCast.CastTimeOf(SpellId.Ember) != 0f || SpellCast.CastTimeOf(SpellId.Mend) != 0f || SpellCast.CastTimeOf(SpellId.Cleanse) != 0f || SpellCast.CastTimeOf(SpellId.Ward) != 0f || SpellCast.CastTimeOf(SpellId.Bind) != 0f || SpellCast.CastTimeOf(SpellId.Weaken) != 0f || SpellCast.CastTimeOf(SpellId.Spark) != 0f)
                throw new InvalidOperationException("Ember/Mend/Cleanse/Ward/Bind/Weaken/Spark CastTime은 0이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-cast-int-world");
            GameObject casterGo = null;
            GameObject tgtGo = null;
            GameObject atkGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-cast-int-caster");
                casterGo.transform.position = new Vector3(40f, 0f, 40f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "cast-int-caster";
                caster.MaxHp = 80f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 6);
                world.BookOf(caster).Learn(SpellId.Ember);
                world.BookOf(caster).Learn(SpellId.Bolt);

                tgtGo = new GameObject("selfcheck-cast-int-tgt");
                tgtGo.transform.position = casterGo.transform.position + new Vector3(6f, 0f, 0f);
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 100f;
                tgt.ResetHp();

                atkGo = new GameObject("selfcheck-cast-int-atk");
                atkGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var atk = atkGo.AddComponent<WorldBody>();
                atk.IsAvatar = true;
                atk.IsEnemy = false;
                atk.CharacterId = "cast-int-atk";
                atk.MaxHp = 50f;
                atk.ResetHp();
                world.StatsOf(atk).ForceSet(40, 20, 20);
                atk.RecalcFromStr(40);
                atkGo.AddComponent<InventoryBag>().Add(ItemCatalog.IronSword, 1);

                float mana0 = caster.Mana;
                float hpT0 = tgt.Hp;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var start = world.TryCast(caster, SpellId.Bolt, tgt);
                if (!start.Applied || !caster.IsCasting(Time.time))
                    throw new InvalidOperationException("시전 중단 테스트: 벼락 풍업 시작 실패: " + start.FailReason);
                if (tgt.Hp != hpT0)
                    throw new InvalidOperationException("풍업 직후 주문 효과가 나가면 안 됩니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("풍업 시작 시 마나가 소모되어야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("풍업 시작 시 시약이 소모되어야 합니다.");

                float manaAfterSpend = caster.Mana;
                float casterHp0 = caster.Hp;
                var hit = world.TryAttack(atk, caster);
                if (!hit.Applied || hit.Damage <= 0 || caster.Hp >= casterHp0)
                    throw new InvalidOperationException("시전 중단 테스트: TryAttack 피격 실패: " + hit.FailReason);
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("피격 후 CastingUntil이 취소되어야 합니다.");

                world.TickCast(Time.time + SpellCast.BoltCastSeconds + 0.1f);
                if (tgt.Hp != hpT0)
                    throw new InvalidOperationException("중단된 벼락은 효과를 내면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("중단된 벼락은 마법을 올리면 안 됩니다.");
                if (Math.Abs(caster.Mana - manaAfterSpend) > 0.0001f)
                    throw new InvalidOperationException("중단 시 마나는 환불되면 안 됩니다.");

                // complete path: start cast → wait → spell fires
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                float hpT1 = tgt.Hp;
                float manaB = caster.Mana;
                var start2 = world.TryCast(caster, SpellId.Bolt, tgt);
                if (!start2.Applied || !caster.IsCasting(Time.time))
                    throw new InvalidOperationException("완료 경로: 벼락 풍업 시작 실패: " + start2.FailReason);
                world.TickCast(Time.time + SpellCast.BoltCastSeconds + 0.1f);
                if (caster.IsCasting(Time.time + SpellCast.BoltCastSeconds + 0.1f))
                    throw new InvalidOperationException("완료 경로: 풍업 후 시전이 남아 있으면 안 됩니다.");
                if (tgt.Hp >= hpT1)
                    throw new InvalidOperationException("완료 경로: 벼락이 맞아야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("완료 경로: 마법이 0.1이어야 합니다.");
                if (caster.Mana >= manaB)
                    throw new InvalidOperationException("완료 경로: 마나가 소모되어야 합니다.");

                // Ember stays instant / non-interruptible
                tgt.ResetHp();
                float emberHp = tgt.Hp;
                var ember = world.TryCast(caster, SpellId.Ember, tgt);
                if (!ember.Applied || ember.Hit != true || tgt.Hp >= emberHp)
                    throw new InvalidOperationException("불씨는 즉시 시전되어야 합니다: " + ember.FailReason);
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("불씨 후 CastingUntil이 있으면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (atkGo != null)
                    UnityEngine.Object.DestroyImmediate(atkGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertCleanse()
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

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Cleanse) != "정화")
                throw new InvalidOperationException("SpellId.Cleanse 한글은 정화이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Cleanse) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("정화 마나는 불씨와 같아야 합니다.");
            if (SpellCast.Interruptible(SpellId.Cleanse) || SpellCast.CastTimeOf(SpellId.Cleanse) != 0f)
                throw new InvalidOperationException("정화는 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-cleanse-world");
            GameObject casterGo = null;
            GameObject allyGo = null;
            GameObject foeGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-cleanse-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "cleanse-caster";
                caster.MaxHp = 60f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(20, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 6);

                var unknown = world.TryCast(caster, SpellId.Cleanse, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 정화는 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Cleanse);

                caster.PoisonTicks = PoisoningResolve.TickCount;
                caster.NextPoisonAt = Time.time + 10f;
                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var selfHit = world.TryCast(caster, SpellId.Cleanse, null);
                if (!selfHit.Applied)
                    throw new InvalidOperationException("자가 정화 실패: " + selfHit.FailReason);
                if (caster.PoisonTicks != 0 || caster.NextPoisonAt != 0f)
                    throw new InvalidOperationException("정화 후 독 틱이 남아 있으면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("정화 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("정화는 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("정화는 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("정화 후 CastingUntil이 있으면 안 됩니다.");

                allyGo = new GameObject("selfcheck-cleanse-ally");
                allyGo.transform.position = casterGo.transform.position + new Vector3(2f, 0f, 0f);
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.CharacterId = "cleanse-ally";
                ally.MaxHp = 50f;
                ally.ResetHp();
                ally.PoisonTicks = PoisoningResolve.TickCount;
                ally.NextPoisonAt = Time.time + 5f;

                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                var allyHit = world.TryCast(caster, SpellId.Cleanse, ally);
                if (!allyHit.Applied)
                    throw new InvalidOperationException("아군 정화 실패: " + allyHit.FailReason);
                if (ally.PoisonTicks != 0 || ally.NextPoisonAt != 0f)
                    throw new InvalidOperationException("아군 정화 후 독이 남아 있으면 안 됩니다.");

                foeGo = new GameObject("selfcheck-cleanse-foe");
                foeGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var foe = foeGo.AddComponent<WorldBody>();
                foe.IsEnemy = true;
                foe.MaxHp = 40f;
                foe.ResetHp();
                foe.PoisonTicks = PoisoningResolve.TickCount;
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 1);
                var foeHit = world.TryCast(caster, SpellId.Cleanse, foe);
                if (foeHit.Applied || foeHit.FailReason != "no_target")
                    throw new InvalidOperationException("적 정화는 실패해야 합니다.");
                if (foe.PoisonTicks != PoisoningResolve.TickCount)
                    throw new InvalidOperationException("실패한 적 정화는 독을 지우면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (allyGo != null)
                    UnityEngine.Object.DestroyImmediate(allyGo);
                if (foeGo != null)
                    UnityEngine.Object.DestroyImmediate(foeGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertWard()
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

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Ward) != "수호")
                throw new InvalidOperationException("SpellId.Ward 한글은 수호이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Ward) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("수호 마나는 불씨와 같아야 합니다.");
            if (SpellCast.WardSeconds != 8f)
                throw new InvalidOperationException("WardSeconds는 8이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Ward) || SpellCast.CastTimeOf(SpellId.Ward) != 0f)
                throw new InvalidOperationException("수호는 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-ward-world");
            GameObject casterGo = null;
            GameObject atkGo = null;
            GameObject atk2Go = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-ward-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "ward-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 6);

                var unknown = world.TryCast(caster, SpellId.Ward, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 수호는 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Ward);

                atkGo = new GameObject("selfcheck-ward-atk");
                atkGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var atk = atkGo.AddComponent<WorldBody>();
                atk.IsAvatar = true;
                atk.IsEnemy = false;
                atk.CharacterId = "ward-atk";
                atk.MaxHp = 50f;
                atk.ResetHp();
                world.StatsOf(atk).ForceSet(40, 20, 20);
                atk.RecalcFromStr(40);
                atkGo.AddComponent<InventoryBag>().Add(ItemCatalog.IronSword, 1);

                float hp0 = caster.Hp;
                var baseHit = world.TryAttack(atk, caster);
                if (!baseHit.Applied || baseHit.Damage <= 0 || caster.Hp >= hp0)
                    throw new InvalidOperationException("수호 기준 타격 실패: " + baseHit.FailReason);
                int baseDmg = baseHit.Damage;
                caster.ResetHp();
                caster.WardUntil = 0f;

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var cast = world.TryCast(caster, SpellId.Ward, null);
                if (!cast.Applied)
                    throw new InvalidOperationException("수호 시전 실패: " + cast.FailReason);
                if (!caster.IsWarded(Time.time))
                    throw new InvalidOperationException("수호 후 WardUntil이 활성이어야 합니다.");
                if (caster.WardUntil < Time.time + SpellCast.WardSeconds - 0.05f)
                    throw new InvalidOperationException("WardUntil은 약 8초여야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("수호 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("수호는 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("수호는 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("수호 후 CastingUntil이 있으면 안 됩니다.");

                atk2Go = new GameObject("selfcheck-ward-atk2");
                atk2Go.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var atk2 = atk2Go.AddComponent<WorldBody>();
                atk2.IsAvatar = true;
                atk2.IsEnemy = false;
                atk2.CharacterId = "ward-atk2";
                atk2.MaxHp = 50f;
                atk2.ResetHp();
                world.StatsOf(atk2).ForceSet(40, 20, 20);
                atk2.RecalcFromStr(40);
                atk2Go.AddComponent<InventoryBag>().Add(ItemCatalog.IronSword, 1);

                float hp1 = caster.Hp;
                var wardedHit = world.TryAttack(atk2, caster);
                if (!wardedHit.Applied)
                    throw new InvalidOperationException("수호 중 타격 실패: " + wardedHit.FailReason);
                int expect = baseDmg / 2;
                if (wardedHit.Damage != expect)
                    throw new InvalidOperationException("수호 중 피해는 절반이어야 합니다: " + wardedHit.Damage + " vs " + expect + " (base " + baseDmg + ")");
                if (Math.Abs((hp1 - caster.Hp) - wardedHit.Damage) > 0.0001f)
                    throw new InvalidOperationException("수호 중 HP 감소가 Damage와 일치해야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (atkGo != null)
                    UnityEngine.Object.DestroyImmediate(atkGo);
                if (atk2Go != null)
                    UnityEngine.Object.DestroyImmediate(atk2Go);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertBind()
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

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Bind) != "속박")
                throw new InvalidOperationException("SpellId.Bind 한글은 속박이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Bind) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("속박 마나는 불씨와 같아야 합니다.");
            if (SpellCast.BindSeconds != 4f)
                throw new InvalidOperationException("BindSeconds는 4이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Bind) || SpellCast.CastTimeOf(SpellId.Bind) != 0f)
                throw new InvalidOperationException("속박은 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-bind-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-bind-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "bind-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Bind, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 속박은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Bind);

                var noTgt = world.TryCast(caster, SpellId.Bind, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 속박은 실패해야 합니다.");

                palGo = new GameObject("selfcheck-bind-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "bind-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Bind, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("속박은 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-bind-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-bind-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Bind, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 속박은 실패해야 합니다.");

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Bind, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 속박은 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                Vector3 mobPos = mobGo.transform.position;
                var cast = world.TryCast(caster, SpellId.Bind, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("속박 시전 실패: " + cast.FailReason);
                if (!mob.IsRooted(Time.time))
                    throw new InvalidOperationException("속박 후 RootUntil이 활성이어야 합니다.");
                if (mob.RootUntil < Time.time + SpellCast.BindSeconds - 0.05f)
                    throw new InvalidOperationException("RootUntil은 약 4초여야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("속박 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("속박은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("속박은 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("속박 후 CastingUntil이 있으면 안 됩니다.");
                if ((mobGo.transform.position - mobPos).sqrMagnitude > 0.0001f)
                    throw new InvalidOperationException("속박 시전은 몹 위치를 바꾸면 안 됩니다.");

                float hp0 = caster.Hp;
                bool struck = world.TryEnemyStrike(mob, caster);
                if (struck || caster.Hp < hp0 - 0.01f)
                    throw new InvalidOperationException("속박 중 몹은 추격/반격하면 안 됩니다.");

                mob.RootUntil = 0f;
                bool freeStrike = world.TryEnemyStrike(mob, caster);
                if (!freeStrike)
                    throw new InvalidOperationException("속박 해제 후 몹 반격이 되어야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertWeaken()
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

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Weaken) != "약화")
                throw new InvalidOperationException("SpellId.Weaken 한글은 약화이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Weaken) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("약화 마나는 불씨와 같아야 합니다.");
            if (SpellCast.WeakenSeconds != 6f)
                throw new InvalidOperationException("WeakenSeconds는 6이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Weaken) || SpellCast.CastTimeOf(SpellId.Weaken) != 0f)
                throw new InvalidOperationException("약화는 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-weaken-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-weaken-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "weaken-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Weaken, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 약화는 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Weaken);

                var noTgt = world.TryCast(caster, SpellId.Weaken, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 약화는 실패해야 합니다.");

                palGo = new GameObject("selfcheck-weaken-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "weaken-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Weaken, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("약화는 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-weaken-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-weaken-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Weaken, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 약화는 실패해야 합니다.");

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Weaken, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 약화는 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float hpBase = caster.Hp;
                bool baseStruck = world.TryEnemyStrike(mob, caster);
                if (!baseStruck || caster.Hp >= hpBase)
                    throw new InvalidOperationException("약화 기준 몹 반격이 되어야 합니다.");
                float baseLost = hpBase - caster.Hp;
                if (baseLost < 1f)
                    throw new InvalidOperationException("약화 기준 피해가 있어야 합니다.");
                caster.ResetHp();
                mob.WeakenUntil = 0f;

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var cast = world.TryCast(caster, SpellId.Weaken, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("약화 시전 실패: " + cast.FailReason);
                if (!mob.IsWeakened(Time.time))
                    throw new InvalidOperationException("약화 후 WeakenUntil이 활성이어야 합니다.");
                if (mob.WeakenUntil < Time.time + SpellCast.WeakenSeconds - 0.05f)
                    throw new InvalidOperationException("WeakenUntil은 약 6초여야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("약화 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("약화는 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("약화는 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("약화 후 CastingUntil이 있으면 안 됩니다.");

                float hp1 = caster.Hp;
                bool weakStruck = world.TryEnemyStrike(mob, caster);
                if (!weakStruck)
                    throw new InvalidOperationException("약화 중에도 몹 반격은 되어야 합니다.");
                float weakLost = hp1 - caster.Hp;
                float expect = baseLost / 2f;
                if (Math.Abs(weakLost - expect) > 0.0001f)
                    throw new InvalidOperationException("약화 중 피해는 절반이어야 합니다: " + weakLost + " vs " + expect + " (base " + baseLost + ")");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertSpark()
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

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Spark) != "섬광")
                throw new InvalidOperationException("SpellId.Spark 한글은 섬광이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Spark) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("섬광 마나는 불씨와 같아야 합니다.");
            if (SpellCast.SparkRange >= SpellCast.EmberRange)
                throw new InvalidOperationException("섬광 사거리는 불씨보다 짧아야 합니다.");
            if (SpellCast.RangeOf(SpellId.Spark) != SpellCast.SparkRange || SpellCast.SparkRange != 6f)
                throw new InvalidOperationException("섬광 사거리는 6이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Spark) || SpellCast.CastTimeOf(SpellId.Spark) != 0f)
                throw new InvalidOperationException("섬광은 즉시 시전이어야 합니다.");
            var plain = new StatSet();
            plain.ForceSet(20, 20, 20);
            if (SpellCast.SparkDamage(plain, new SkillSet()) >= SpellCast.EmberDamage(plain, new SkillSet()))
                throw new InvalidOperationException("섬광 피해는 불씨보다 낮아야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-spark-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-spark-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "spark-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Spark, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 섬광은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Spark);

                var noTgt = world.TryCast(caster, SpellId.Spark, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 섬광은 실패해야 합니다.");

                palGo = new GameObject("selfcheck-spark-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "spark-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Spark, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("섬광은 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-spark-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-spark-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Spark, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 섬광은 실패해야 합니다.");

                // Ember range ok but Spark shorter: place mob between SparkRange and EmberRange
                farGo.transform.position = casterGo.transform.position + new Vector3(7f, 0f, 0f);
                var midFar = world.TryCast(caster, SpellId.Spark, far);
                if (midFar.Applied || midFar.FailReason != "range")
                    throw new InvalidOperationException("섬광은 불씨보다 짧은 사거리여야 합니다.");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Spark, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 섬광은 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                float hp0 = mob.Hp;
                var cast = world.TryCast(caster, SpellId.Spark, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("섬광 시전 실패: " + cast.FailReason);
                if (!cast.Hit || cast.Damage <= 0)
                    throw new InvalidOperationException("섬광은 피해를 줘야 합니다.");
                if (mob.Hp >= hp0)
                    throw new InvalidOperationException("섬광 후 몹 HP가 줄어야 합니다.");
                if (cast.Damage >= SpellCast.EmberDamage(world.StatsOf(caster), world.SkillsOf(caster)))
                    throw new InvalidOperationException("섬광 피해는 같은 스탯에서 불씨보다 낮아야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("섬광 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("섬광은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("섬광은 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("섬광 후 CastingUntil이 있으면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertCraftOrder()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (GameObject.Find("DressVillage") != null)
                throw new InvalidOperationException("DressVillage 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if (CraftOrderRules.DefaultItem != ItemCatalog.IronSword)
                throw new InvalidOperationException("기본 제작의뢰는 iron_sword여야 합니다.");
            if (CraftOrderRules.GoldReward != 10)
                throw new InvalidOperationException("제작의뢰 골드 보상은 10이어야 합니다.");
            if (CraftOrderRules.Amount != 1)
                throw new InvalidOperationException("제작의뢰 수량은 1이어야 합니다.");

            var noOrder = CraftOrderResolve.TurnIn(new CraftOrderRequest { HasStation = true, Distance = 0f, ActiveOrder = "", HasMatchingCrafted = true });
            if (noOrder.Applied || noOrder.FailReason != "no_order")
                throw new InvalidOperationException("주문 없으면 납품 실패여야 합니다.");
            var wrong = CraftOrderResolve.TurnIn(new CraftOrderRequest { HasStation = true, Distance = 0f, ActiveOrder = ItemCatalog.IronSword, HasMatchingCrafted = false });
            if (wrong.Applied || wrong.FailReason != "wrong_item")
                throw new InvalidOperationException("잘못된 아이템 납품은 실패여야 합니다.");
            var already = CraftOrderResolve.Accept(new CraftOrderRequest { HasStation = true, Distance = 0f, ActiveOrder = ItemCatalog.IronSword, OfferItem = ItemCatalog.IronSword });
            if (already.Applied || already.FailReason != "already")
                throw new InvalidOperationException("이미 의뢰가 있으면 수락 실패여야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-craftorder-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                var forge = OfflineWorld.FindStation("Forge");
                if (forge == null)
                    throw new InvalidOperationException("Forge가 있어야 합니다.");

                bodyGo = new GameObject("selfcheck-craftorder-body");
                bodyGo.transform.position = forge.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "craft-order-smith";
                body.Gold = 0;
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();

                var far = CraftOrderResolve.Accept(new CraftOrderRequest { HasStation = true, Distance = 99f, ActiveOrder = "", OfferItem = CraftOrderRules.DefaultItem });
                if (far.Applied || far.FailReason != "range")
                    throw new InvalidOperationException("멀리서 수락은 range 실패여야 합니다.");

                var accept = world.TryAcceptOrder(body, forge);
                if (!accept.Applied)
                    throw new InvalidOperationException("제작의뢰 수락 실패: " + accept.FailReason);
                if (body.ActiveCraftOrder != ItemCatalog.IronSword)
                    throw new InvalidOperationException("수락 후 ActiveCraftOrder가 iron_sword여야 합니다.");

                var dup = world.TryAcceptOrder(body, forge);
                if (dup.Applied || dup.FailReason != "already")
                    throw new InvalidOperationException("중복 수락은 already여야 합니다.");

                var emptyTurn = world.TryTurnInOrder(body, forge);
                if (emptyTurn.Applied || emptyTurn.FailReason != "wrong_item")
                    throw new InvalidOperationException("아이템 없으면 wrong_item이어야 합니다.");

                // Unmarked / wrong maker sword must not turn in
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40, MakerId = "other" });
                var wrongMaker = world.TryTurnInOrder(body, forge);
                if (wrongMaker.Applied || wrongMaker.FailReason != "wrong_item")
                    throw new InvalidOperationException("타제작 검 납품은 실패해야 합니다.");

                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40, MakerId = body.CharacterId });
                world.SkillsOf(body).ForceSet(SkillId.Blacksmithing, 0f, SkillLock.Up);
                int gold0 = body.Gold;
                int swords0 = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.IronSword)
                        swords0 += bag.Items[i].Amount;

                var turn = world.TryTurnInOrder(body, forge);
                if (!turn.Applied)
                    throw new InvalidOperationException("납품 실패: " + turn.FailReason);
                if (body.Gold != gold0 + CraftOrderRules.GoldReward)
                    throw new InvalidOperationException("납품 후 골드가 +" + CraftOrderRules.GoldReward + "이어야 합니다.");
                if (!string.IsNullOrEmpty(body.ActiveCraftOrder))
                    throw new InvalidOperationException("납품 후 의뢰가 비어야 합니다.");
                int swords1 = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.IronSword)
                        swords1 += bag.Items[i].Amount;
                if (swords1 != swords0 - 1)
                    throw new InvalidOperationException("납품 시 제작 검 1개가 소모되어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Blacksmithing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("납품 후 대장 숙련이 0.1이어야 합니다.");

                var noAgain = world.TryTurnInOrder(body, forge);
                if (noAgain.Applied || noAgain.FailReason != "no_order")
                    throw new InvalidOperationException("의뢰 클리어 후 납품은 no_order여야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertRestore()
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

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Restore) != "회복")
                throw new InvalidOperationException("SpellId.Restore 한글은 회복이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Restore) <= SpellCast.ManaCost(SpellId.Mend))
                throw new InvalidOperationException("회복 마나는 봉합보다 커야 합니다.");
            if (SpellCast.ReagentCost(SpellId.Restore) <= SpellCast.ReagentCost(SpellId.Mend))
                throw new InvalidOperationException("회복 시약은 봉합보다 커야 합니다.");
            if (SpellCast.Interruptible(SpellId.Restore) || SpellCast.CastTimeOf(SpellId.Restore) != 0f)
                throw new InvalidOperationException("회복은 즉시 시전이어야 합니다.");
            var plain = new StatSet();
            plain.ForceSet(20, 20, 40);
            if (SpellCast.RestoreHeal(plain) <= SpellCast.MendHeal(plain))
                throw new InvalidOperationException("회복 치유량은 봉합보다 커야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-restore-world");
            GameObject casterGo = null;
            GameObject allyGo = null;
            GameObject foeGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-restore-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "restore-caster";
                caster.MaxHp = 80f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(20, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Restore, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 회복은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Restore);

                caster.SetHp(20f);
                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var selfHit = world.TryCast(caster, SpellId.Restore, null);
                if (!selfHit.Applied)
                    throw new InvalidOperationException("자가 회복 실패: " + selfHit.FailReason);
                int expectHeal = SpellCast.RestoreHeal(world.StatsOf(caster));
                if (caster.Hp < 20f + expectHeal - 0.01f && caster.Hp < caster.MaxHp - 0.01f)
                    throw new InvalidOperationException("회복 후 HP가 올라야 합니다.");
                if (caster.Hp <= 20f)
                    throw new InvalidOperationException("회복은 HP를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("회복 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("회복은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - SpellCast.ReagentCost(SpellId.Restore))
                    throw new InvalidOperationException("회복은 시약을 봉합보다 많이 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("회복 후 CastingUntil이 있으면 안 됩니다.");

                // Mend heals less than Restore at same INT
                world.BookOf(caster).Learn(SpellId.Mend);
                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                caster.SetHp(10f);
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 4);
                float beforeMend = caster.Hp;
                var mendHit = world.TryCast(caster, SpellId.Mend, caster);
                if (!mendHit.Applied)
                    throw new InvalidOperationException("비교용 봉합 실패: " + mendHit.FailReason);
                float mendGain = caster.Hp - beforeMend;
                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                caster.SetHp(10f);
                caster.SetMana(caster.MaxMana);
                float beforeRestore = caster.Hp;
                var restoreCmp = world.TryCast(caster, SpellId.Restore, caster);
                if (!restoreCmp.Applied)
                    throw new InvalidOperationException("비교용 회복 실패: " + restoreCmp.FailReason);
                float restoreGain = caster.Hp - beforeRestore;
                if (restoreGain <= mendGain)
                    throw new InvalidOperationException("회복 치유량은 봉합보다 커야 합니다(실측).");

                allyGo = new GameObject("selfcheck-restore-ally");
                allyGo.transform.position = casterGo.transform.position + new Vector3(2f, 0f, 0f);
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.CharacterId = "restore-ally";
                ally.MaxHp = 60f;
                ally.ResetHp();
                ally.SetHp(15f);
                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                float allyHp0 = ally.Hp;
                var allyHit = world.TryCast(caster, SpellId.Restore, ally);
                if (!allyHit.Applied)
                    throw new InvalidOperationException("아군 회복 실패: " + allyHit.FailReason);
                if (ally.Hp <= allyHp0)
                    throw new InvalidOperationException("아군 회복 후 HP가 올라야 합니다.");

                foeGo = new GameObject("selfcheck-restore-foe");
                foeGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var foe = foeGo.AddComponent<WorldBody>();
                foe.IsEnemy = true;
                foe.IsAvatar = false;
                foe.MaxHp = 40f;
                foe.ResetHp();
                foe.SetHp(10f);
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                float foeHp0 = foe.Hp;
                var foeHit = world.TryCast(caster, SpellId.Restore, foe);
                if (foeHit.Applied || foeHit.FailReason != "no_target")
                    throw new InvalidOperationException("적 회복은 실패해야 합니다.");
                if (Math.Abs(foe.Hp - foeHp0) > 0.01f)
                    throw new InvalidOperationException("실패한 적 회복은 HP를 바꾸면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (allyGo != null)
                    UnityEngine.Object.DestroyImmediate(allyGo);
                if (foeGo != null)
                    UnityEngine.Object.DestroyImmediate(foeGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertBlink()
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

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Blink) != "도약")
                throw new InvalidOperationException("SpellId.Blink 한글은 도약이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Blink) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("도약 마나는 불씨와 같아야 합니다.");
            if (SpellCast.ReagentCost(SpellId.Blink) != SpellCast.ReagentCost(SpellId.Ember))
                throw new InvalidOperationException("도약 시약은 불씨와 같아야 합니다.");
            if (SpellCast.BlinkDistance < 3f || SpellCast.BlinkDistance > 4f)
                throw new InvalidOperationException("도약 거리는 3~4m여야 합니다.");
            if (SpellCast.Interruptible(SpellId.Blink) || SpellCast.CastTimeOf(SpellId.Blink) != 0f)
                throw new InvalidOperationException("도약은 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-blink-world");
            GameObject casterGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-blink-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                casterGo.transform.rotation = Quaternion.identity;
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "blink-caster";
                caster.MaxHp = 80f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(20, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Blink, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 도약은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Blink);

                caster.Ghost = true;
                var ghostFail = world.TryCast(caster, SpellId.Blink, null);
                if (ghostFail.Applied || ghostFail.FailReason != "ghost")
                    throw new InvalidOperationException("유령 도약은 실패해야 합니다.");
                caster.Ghost = false;

                caster.CombatUntil = Time.time + 30f;
                var combatFail = world.TryCast(caster, SpellId.Blink, null);
                if (combatFail.Applied || combatFail.FailReason != "combat")
                    throw new InvalidOperationException("전투 중 도약은 실패해야 합니다.");
                caster.CombatUntil = 0f;

                caster.SetMana(0f);
                var manaFail = world.TryCast(caster, SpellId.Blink, null);
                if (manaFail.Applied || manaFail.FailReason != "mana")
                    throw new InvalidOperationException("마나 없는 도약은 실패해야 합니다.");
                caster.SetMana(caster.MaxMana);

                bag.Items.Clear();
                var resinFail = world.TryCast(caster, SpellId.Blink, null);
                if (resinFail.Applied || resinFail.FailReason != "reagent")
                    throw new InvalidOperationException("시약 없는 도약은 실패해야 합니다.");
                bag.Add(SpellCast.Reagent, 4);

                // Distinct from Mark/Recall: Blink must not set HasMark / not use Mark coords
                caster.HasMark = false;
                caster.MarkX = 0f;
                caster.MarkZ = 0f;
                Vector3 before = casterGo.transform.position;
                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                var hit = world.TryCast(caster, SpellId.Blink, null);
                if (!hit.Applied)
                    throw new InvalidOperationException("도약 실패: " + hit.FailReason);
                Vector3 after = casterGo.transform.position;
                float moved = Vector3.Distance(new Vector3(before.x, 0f, before.z), new Vector3(after.x, 0f, after.z));
                if (moved < 3f)
                    throw new InvalidOperationException("도약 후 위치가 약 3m 이상 바뀌어야 합니다: " + moved);
                if (Mathf.Abs(moved - SpellCast.BlinkDistance) > 0.2f)
                    throw new InvalidOperationException("도약 거리는 BlinkDistance와 같아야 합니다: " + moved);
                // identity rotation → +Z
                if (Mathf.Abs(after.z - (before.z + SpellCast.BlinkDistance)) > 0.2f)
                    throw new InvalidOperationException("도약은 전방(+Z)으로 이동해야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("도약 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("도약은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - SpellCast.ReagentCost(SpellId.Blink))
                    throw new InvalidOperationException("도약은 시약을 소모해야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("도약 후 CastingUntil이 있으면 안 됩니다.");
                if (caster.HasMark)
                    throw new InvalidOperationException("도약은 Mark 슬롯을 쓰면 안 됩니다.");
                if (Math.Abs(after.x - TravelGate.PlazaX) < 0.2f && Math.Abs(after.z - TravelGate.PlazaZ) < 0.2f)
                    throw new InvalidOperationException("도약은 문게이트 광장 워프가 아닙니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertBless()
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

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Bless) != "축복")
                throw new InvalidOperationException("SpellId.Bless 한글은 축복이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Bless) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("축복 마나는 불씨와 같아야 합니다.");
            if (SpellCast.ReagentCost(SpellId.Bless) != SpellCast.ReagentCost(SpellId.Ember))
                throw new InvalidOperationException("축복 시약은 불씨와 같아야 합니다.");
            if (SpellCast.BlessSeconds != 8f)
                throw new InvalidOperationException("BlessSeconds는 8이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Bless) || SpellCast.CastTimeOf(SpellId.Bless) != 0f)
                throw new InvalidOperationException("축복은 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-bless-world");
            GameObject casterGo = null;
            GameObject tgtGo = null;
            GameObject allyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-bless-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "bless-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);
                bag.Add(ItemCatalog.IronSword, 1);

                var unknown = world.TryCast(caster, SpellId.Bless, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 축복은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Bless);

                tgtGo = new GameObject("selfcheck-bless-tgt");
                tgtGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsAvatar = true;
                tgt.IsEnemy = false;
                tgt.CharacterId = "bless-tgt";
                tgt.MaxHp = 200f;
                tgt.ResetHp();
                world.StatsOf(tgt).ForceSet(40, 20, 20);
                tgt.RecalcFromStr(40);
                tgtGo.AddComponent<InventoryBag>();

                float hp0 = tgt.Hp;
                var baseHit = world.TryAttack(caster, tgt);
                if (!baseHit.Applied || baseHit.Damage <= 0 || tgt.Hp >= hp0)
                    throw new InvalidOperationException("축복 기준 타격 실패: " + baseHit.FailReason);
                int baseDmg = baseHit.Damage;
                tgt.ResetHp();
                caster.BlessUntil = 0f;
                caster.WardUntil = 0f;
                tgt.WardUntil = 0f;

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                var cast = world.TryCast(caster, SpellId.Bless, null);
                if (!cast.Applied)
                    throw new InvalidOperationException("축복 시전 실패: " + cast.FailReason);
                if (!caster.IsBlessed(Time.time))
                    throw new InvalidOperationException("축복 후 BlessUntil이 활성이어야 합니다.");
                if (caster.BlessUntil < Time.time + SpellCast.BlessSeconds - 0.05f)
                    throw new InvalidOperationException("BlessUntil은 약 8초여야 합니다.");
                if (caster.IsWarded(Time.time))
                    throw new InvalidOperationException("축복은 WardUntil을 켜면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("축복 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("축복은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("축복은 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("축복 후 CastingUntil이 있으면 안 됩니다.");

                float hp1 = tgt.Hp;
                var nextAt = typeof(OfflineWorld).GetField("nextAttackAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nextAt != null)
                {
                    var map = nextAt.GetValue(world) as System.Collections.IDictionary;
                    if (map != null)
                        map.Remove(caster.GetInstanceID());
                }
                var blessedHit = world.TryAttack(caster, tgt);
                if (!blessedHit.Applied)
                    throw new InvalidOperationException("축복 중 타격 실패: " + blessedHit.FailReason);
                int expect = (baseDmg * 5) / 4;
                if (blessedHit.Damage != expect)
                    throw new InvalidOperationException("축복 중 피해는 ×1.25여야 합니다: " + blessedHit.Damage + " vs " + expect + " (base " + baseDmg + ")");
                if (Math.Abs((hp1 - tgt.Hp) - blessedHit.Damage) > 0.0001f)
                    throw new InvalidOperationException("축복 중 HP 감소가 Damage와 일치해야 합니다.");
                if (expect <= baseDmg)
                    throw new InvalidOperationException("축복 피해는 기준보다 커야 합니다.");

                // Ally cast: nearby friendly avatar gets BlessUntil (not caster)
                allyGo = new GameObject("selfcheck-bless-ally");
                allyGo.transform.position = casterGo.transform.position + new Vector3(0.5f, 0f, 0f);
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.CharacterId = "bless-ally";
                ally.MaxHp = 80f;
                ally.ResetHp();
                world.StatsOf(ally).ForceSet(20, 20, 20);
                ally.RecalcFromStr(20);
                allyGo.AddComponent<InventoryBag>();
                caster.BlessUntil = 0f;
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                var allyCast = world.TryCast(caster, SpellId.Bless, ally);
                if (!allyCast.Applied)
                    throw new InvalidOperationException("아군 축복 시전 실패: " + allyCast.FailReason);
                if (!ally.IsBlessed(Time.time))
                    throw new InvalidOperationException("아군 축복 후 대상 BlessUntil이 활성이어야 합니다.");
                if (caster.IsBlessed(Time.time))
                    throw new InvalidOperationException("아군 축복은 시전자를 축복하면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (allyGo != null)
                    UnityEngine.Object.DestroyImmediate(allyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertControlSlots()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (GameObject.Find("DressVillage") != null)
                throw new InvalidOperationException("DressVillage 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if (TameResolve.FollowerCap != 2)
                throw new InvalidOperationException("MaxControlSlots/FollowerCap은 2여야 합니다.");
            if (TameCritter.ControlSlots != 1 || TameBoar.ControlSlots != 1)
                throw new InvalidOperationException("하트/멧돼지 ControlCost는 1이어야 합니다.");
            if (!MobCatalog.TamableOf(TameCritter.Id) || !MobCatalog.TamableOf(TameBoar.Id))
                throw new InvalidOperationException("하트/멧돼지는 조련 가능해야 합니다.");

            var twoOk = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, UsedSlots = 1, ControlSlots = 1, FollowerCap = 2, Distance = 1f });
            if (!twoOk.Applied)
                throw new InvalidOperationException("cap2에서 used1+cost1은 성공해야 합니다.");
            var threeFail = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, UsedSlots = 2, ControlSlots = 1, FollowerCap = 2, Distance = 1f });
            if (threeFail.Applied || threeFail.FailReason != "no_slot")
                throw new InvalidOperationException("cap2에서 used2+cost1은 no_slot이어야 합니다.");
            var costly = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, UsedSlots = 1, ControlSlots = 2, FollowerCap = 2, Distance = 1f });
            if (costly.Applied || costly.FailReason != "no_slot")
                throw new InvalidOperationException("used1+cost2는 no_slot이어야 합니다.");

            var hartGo = GameObject.Find(TameCritter.Object);
            var hart = hartGo != null ? hartGo.GetComponent<WorldBody>() : null;
            if (hart == null || hart.ControlSlots != 1 || !hart.Tameable)
                throw new InvalidOperationException("씬 야생하트가 ControlSlots=1이어야 합니다.");
            var boarGo = GameObject.Find(TameBoar.Object);
            var boar = boarGo != null ? boarGo.GetComponent<WorldBody>() : null;
            if (boar == null || boar.ControlSlots != 1 || !boar.Tameable || boar.IsEnemy)
                throw new InvalidOperationException("씬 야생멧돼지가 있어야 합니다.");
            if (boar.MobId != TameBoar.Id || boar.DisplayName != TameBoar.DisplayName)
                throw new InvalidOperationException("멧돼지 카탈로그가 야생멧돼지여야 합니다.");
            Vector3 bpos = boarGo.transform.position;
            if (Math.Abs(bpos.x - TameBoar.X) > 0.4f || Math.Abs(bpos.z - TameBoar.Z) > 0.4f)
                throw new InvalidOperationException("멧돼지 좌표가 지정 위치와 같아야 합니다.");
            if (GuardZone.Contains(bpos.x, bpos.z))
                throw new InvalidOperationException("멧돼지는 GuardZone 밖이어야 합니다.");
            float[] lxs = { HousingPlot.X, TameCritter.X, 18.2f, 3.4f, -12.2f, Dungeon1.EntranceX, Dungeon2.EntranceX, 0f, FieldBoss.X, TravelGate.X, StableYard.X };
            float[] lzs = { HousingPlot.Z, TameCritter.Z, 2.4f, -19.6f, 20.4f, Dungeon1.EntranceZ, Dungeon2.EntranceZ, 13.2f, FieldBoss.Z, TravelGate.Z, StableYard.Z };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = bpos.x - lxs[i];
                float dz = bpos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 9f)
                    throw new InvalidOperationException("멧돼지가 기존 랜드마크/하트와 너무 가까우면 안 됩니다.");
            }
            string bpath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(boarGo);
            if (string.IsNullOrEmpty(bpath) || bpath.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("멧돼지는 Prefab이어야 합니다(RAW fbx 아님).");
            string hpath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hartGo);
            if (string.IsNullOrEmpty(hpath) || hpath.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("하트는 Prefab이어야 합니다(RAW fbx 아님).");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-slots-world");
            GameObject ownerGo = null;
            GameObject thirdGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                hart.OwnerCharacterId = "";
                hart.PetFollow = false;
                hart.PetStabled = false;
                hart.PetGuard = false;
                hart.PetAttackTarget = null;
                if (!hartGo.activeSelf)
                    hartGo.SetActive(true);
                boar.OwnerCharacterId = "";
                boar.PetFollow = false;
                boar.PetStabled = false;
                boar.PetGuard = false;
                boar.PetAttackTarget = null;
                if (!boarGo.activeSelf)
                    boarGo.SetActive(true);

                ownerGo = new GameObject("selfcheck-slots-owner");
                ownerGo.transform.position = hartGo.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "slots-owner";
                owner.Gold = StableYard.GoldCost * 2;
                owner.ResetHp();

                var first = world.TryTame(owner, hart);
                if (!first.Applied)
                    throw new InvalidOperationException("첫 하트 조련 실패: " + first.FailReason);
                if (world.CountFollowers(owner.CharacterId) != 1)
                    throw new InvalidOperationException("하트 조련 후 used slots=1이어야 합니다.");

                ownerGo.transform.position = boarGo.transform.position;
                var second = world.TryTame(owner, boar);
                if (!second.Applied)
                    throw new InvalidOperationException("두 번째 멧돼지 조련 실패: " + second.FailReason);
                if (world.CountFollowers(owner.CharacterId) != 2)
                    throw new InvalidOperationException("두 마리 조련 후 used slots=2이어야 합니다.");

                thirdGo = new GameObject("selfcheck-slots-third");
                thirdGo.transform.position = ownerGo.transform.position;
                var third = thirdGo.AddComponent<WorldBody>();
                third.IsEnemy = false;
                third.IsAvatar = false;
                third.Tameable = true;
                third.MobId = TameBoar.Id;
                third.DisplayName = "여분";
                third.ControlSlots = 1;
                third.ResetHp();
                var over = world.TryTame(owner, third);
                if (over.Applied || over.FailReason != "no_slot")
                    throw new InvalidOperationException("세 번째 조련은 no_slot이어야 합니다: " + over.FailReason);

                var rel = world.TryPetRelease(owner, boar);
                if (!rel.Applied)
                    throw new InvalidOperationException("release는 성공해야 합니다: " + rel.FailReason);
                if (world.CountFollowers(owner.CharacterId) != 1)
                    throw new InvalidOperationException("release 후 used slots=1이어야 합니다.");
                ownerGo.transform.position = boarGo.transform.position;
                var retame = world.TryTame(owner, boar);
                if (!retame.Applied)
                    throw new InvalidOperationException("release 후 재조련 실패: " + retame.FailReason);
                if (world.CountFollowers(owner.CharacterId) != 2)
                    throw new InvalidOperationException("재조련 후 used slots=2이어야 합니다.");

                var stable = GameObject.Find(StableYard.Object)?.GetComponent<StableMaster>();
                if (stable == null)
                    throw new InvalidOperationException("StableMaster가 있어야 합니다.");
                ownerGo.transform.position = GameObject.Find(StableYard.Object).transform.position;
                owner.Gold = StableYard.GoldCost;
                int beforeStable = world.CountFollowers(owner.CharacterId);
                var parked = world.TryStable(owner, stable);
                if (!parked.Applied)
                    throw new InvalidOperationException("마구간 맡김 실패: " + parked.FailReason);
                if (world.CountFollowers(owner.CharacterId) != beforeStable - 1)
                    throw new InvalidOperationException("stable은 슬롯을 해제해야 합니다.");
                third.OwnerCharacterId = "";
                thirdGo.transform.position = ownerGo.transform.position;
                var afterStable = world.TryTame(owner, third);
                if (!afterStable.Applied)
                    throw new InvalidOperationException("stable 후 여유 슬롯 조련 실패: " + afterStable.FailReason);

                world.TryPetRelease(owner, third);
                world.TryPetRelease(owner, hart);
                world.TryPetRelease(owner, boar);
                world.ClearStabled(owner.CharacterId);
                world.ResetHousePlot();
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (thirdGo != null)
                    UnityEngine.Object.DestroyImmediate(thirdGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                if (hartGo != null)
                {
                    if (!hartGo.activeSelf)
                        hartGo.SetActive(true);
                    hart.OwnerCharacterId = "";
                    hart.PetFollow = false;
                    hart.PetStabled = false;
                    hart.PetGuard = false;
                    hart.PetAttackTarget = null;
                    hartGo.transform.SetPositionAndRotation(new Vector3(TameCritter.X, hartGo.transform.position.y, TameCritter.Z), hartGo.transform.rotation);
                }
                if (boarGo != null)
                {
                    if (!boarGo.activeSelf)
                        boarGo.SetActive(true);
                    boar.OwnerCharacterId = "";
                    boar.PetFollow = false;
                    boar.PetStabled = false;
                    boar.PetGuard = false;
                    boar.PetAttackTarget = null;
                    boarGo.transform.SetPositionAndRotation(new Vector3(TameBoar.X, boarGo.transform.position.y, TameBoar.Z), boarGo.transform.rotation);
                }
                OfflineWorld.Instance?.ClearStabled("slots-owner");
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertNestedBag()
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

            if (!ItemCatalog.IsContainer(ItemCatalog.Pouch))
                throw new InvalidOperationException("pouch는 컨테이너여야 합니다.");
            if (ItemCatalog.Stackable(ItemCatalog.Pouch))
                throw new InvalidOperationException("pouch는 스택되면 안 됩니다.");
            if (ItemCatalog.WeightOf(ItemCatalog.Pouch) != 2f)
                throw new InvalidOperationException("pouch 무게는 2여야 합니다.");
            if (ItemCatalog.WeightOf(ItemCatalog.Cloth) != 0.5f)
                throw new InvalidOperationException("cloth 무게는 0.5여야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-nested-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                bodyGo = new GameObject("selfcheck-nested-body");
                bodyGo.transform.position = new Vector3(4f, 0f, 4f);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "nested-body";
                body.RecalcFromStr(20);
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Pouch, 1);
                bag.Add(ItemCatalog.Cloth, 1);
                world.StatsOf(body).ForceSet(20, 25, 25);

                string pouchId = bag.PouchInstanceId();
                if (string.IsNullOrEmpty(pouchId))
                    throw new InvalidOperationException("pouch InstanceId가 있어야 합니다.");

                float w0 = bag.TotalWeight();
                if (Math.Abs(w0 - (ItemCatalog.WeightOf(ItemCatalog.Pouch) + ItemCatalog.WeightOf(ItemCatalog.Cloth))) > 0.0001f)
                    throw new InvalidOperationException("초기 가방 무게가 pouch+cloth여야 합니다: " + w0);

                var badDepth = world.TryMoveToPouch(body, ItemCatalog.Pouch, pouchId);
                if (badDepth.Applied || badDepth.FailReason != "nested_depth")
                    throw new InvalidOperationException("파우치 속 파우치는 nested_depth 실패여야 합니다: " + badDepth.FailReason);

                var moved = world.TryMoveToPouch(body, ItemCatalog.Cloth, pouchId);
                if (!moved.Applied)
                    throw new InvalidOperationException("TryMoveToPouch 실패: " + moved.FailReason);
                if (bag.CountInPouch(ItemCatalog.Cloth, pouchId) != 1)
                    throw new InvalidOperationException("천이 파우치 안에 있어야 합니다.");
                bool clothNested = false;
                bool clothBackpack = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    var it = bag.Items[i];
                    if (it.TemplateId != ItemCatalog.Cloth)
                        continue;
                    if ((it.ParentContainerId ?? "") == pouchId)
                        clothNested = true;
                    if (string.IsNullOrEmpty(it.ParentContainerId))
                        clothBackpack = true;
                }
                if (!clothNested || clothBackpack)
                    throw new InvalidOperationException("천 ParentContainerId가 pouch InstanceId여야 합니다.");
                if (Math.Abs(bag.TotalWeight() - w0) > 0.0001f)
                    throw new InvalidOperationException("파우치 안 천 무게도 Carry에 합산되어야 합니다.");

                var taken = world.TryTakeFromPouch(body, ItemCatalog.Cloth, pouchId);
                if (!taken.Applied)
                    throw new InvalidOperationException("TryTakeFromPouch 실패: " + taken.FailReason);
                if (bag.CountInPouch(ItemCatalog.Cloth, pouchId) != 0)
                    throw new InvalidOperationException("꺼낸 뒤 파우치에 천이 없어야 합니다.");
                clothNested = false;
                clothBackpack = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    var it = bag.Items[i];
                    if (it.TemplateId != ItemCatalog.Cloth)
                        continue;
                    if ((it.ParentContainerId ?? "") == pouchId)
                        clothNested = true;
                    if (string.IsNullOrEmpty(it.ParentContainerId))
                        clothBackpack = true;
                }
                if (clothNested || !clothBackpack)
                    throw new InvalidOperationException("꺼낸 천은 백팩(Parent 없음)이어야 합니다.");
                if (Math.Abs(bag.TotalWeight() - w0) > 0.0001f)
                    throw new InvalidOperationException("꺼낸 뒤 무게가 유지되어야 합니다.");

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("NestedBag 슬라이스 후 던전3가 생기면 안 됩니다.");
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

        static void AssertGroundDecay()
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

            OfflineWorld.Instance?.ResetHousePlot();

            // Clear leftover ground from prior runs.
            var stale = UnityEngine.Object.FindObjectsByType<GroundItem>(FindObjectsSortMode.None);
            for (int i = 0; i < stale.Length; i++)
            {
                if (stale[i] != null)
                    UnityEngine.Object.DestroyImmediate(stale[i].gameObject);
            }

            var worldGo = new GameObject("selfcheck-ground-world");
            GameObject ownerGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                if (Math.Abs(OfflineWorld.GroundDecaySeconds - 30f) > 0.0001f)
                    throw new InvalidOperationException("기본 GroundDecaySeconds는 30이어야 합니다.");

                var cloth = new ItemRecord { TemplateId = ItemCatalog.Cloth, Amount = 1 };
                var ground = world.SpawnGroundItem(cloth, new Vector3(1f, 0f, 1f), 0.1f);
                if (ground == null || string.IsNullOrEmpty(ground.GroundId))
                    throw new InvalidOperationException("SpawnGroundItem이 GroundItem을 만들어야 합니다.");
                if (OfflineWorld.CountGroundItems(ItemCatalog.Cloth) < 1)
                    throw new InvalidOperationException("스폰 직후 월드 천 GroundItem이 있어야 합니다.");
                if (ground.DecayAt <= Time.time)
                    throw new InvalidOperationException("DecayAt은 now+초여야 합니다(만료 전).");

                // Force expiry like corpse decay assert.
                ground.DecayAt = -9999f;
                world.TickGroundItems(0f);
                if (OfflineWorld.CountGroundItems(ItemCatalog.Cloth) != 0)
                    throw new InvalidOperationException("만료 GroundItem은 TickGroundItems 후 삭제되어야 합니다.");
                if (GameObject.Find("GroundItem") != null)
                {
                    var leftoverGo = GameObject.Find("GroundItem");
                    if (leftoverGo != null && leftoverGo.GetComponent<GroundItem>() != null)
                        throw new InvalidOperationException("만료 GroundItem GameObject가 남아 있으면 안 됩니다.");
                }

                // House lockdown / secure must NOT decay with ground tick.
                var station = GameObject.Find(HousingPlot.StationObject);
                var chestGo = GameObject.Find(HousingPlot.ChestObject);
                if (station == null || chestGo == null)
                    throw new InvalidOperationException("HousingPlot station/chest가 있어야 합니다.");
                ownerGo = new GameObject("selfcheck-ground-owner");
                ownerGo.transform.position = station.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "ground-owner";
                owner.AccountId = "ground-acc";
                owner.Gold = HousingPlot.ClaimGold;
                owner.ResetHp();
                var bag = ownerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Cloth, 1);
                var claim = world.TryClaimHouse(owner, station.GetComponent<HousePlotStation>());
                if (!claim.Applied)
                    throw new InvalidOperationException("AssertGroundDecay claim 실패: " + claim.FailReason);
                ownerGo.transform.position = chestGo.transform.position;
                var locked = world.TryLockdown(owner, chestGo.GetComponent<HouseChest>(), ItemCatalog.Cloth);
                if (!locked.Applied)
                    throw new InvalidOperationException("AssertGroundDecay lockdown 실패: " + locked.FailReason);
                if (world.CountHouseSecureItems() < 1)
                    throw new InvalidOperationException("lockdown 후 secure 아이템이 있어야 합니다.");

                // Spawn another short-lived ground drop; lockdown must survive its tick.
                var ground2 = world.SpawnGroundItem(ItemCatalog.Cloth, new Vector3(2f, 0f, 2f), 1, 0.1f);
                ground2.DecayAt = -1f;
                world.TickGroundItems(Time.time + 9999f);
                if (OfflineWorld.CountGroundItems() != 0)
                    throw new InvalidOperationException("강제 만료 후 GroundItem이 없어야 합니다.");
                if (world.CountHouseSecureItems() < 1)
                    throw new InvalidOperationException("집 Lockdown/secure 아이템은 GroundDecay에 삭제되면 안 됩니다.");
                var take = world.TrySecureTake(owner, chestGo.GetComponent<HouseChest>());
                if (!take.Applied)
                    throw new InvalidOperationException("Tick 후에도 secure take가 되어야 합니다: " + take.FailReason);

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("GroundDecay 슬라이스 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                var leftovers = UnityEngine.Object.FindObjectsByType<GroundItem>(FindObjectsSortMode.None);
                for (int i = 0; i < leftovers.Length; i++)
                {
                    if (leftovers[i] != null)
                        UnityEngine.Object.DestroyImmediate(leftovers[i].gameObject);
                }
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertStableSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (StableYard.GoldCost != 2)
                throw new InvalidOperationException("마구간 골드 비용은 2여야 합니다.");
            if (StableResolve.Park(new StableRequest { HasFollower = true, Gold = 2 }).Applied == false)
                throw new InvalidOperationException("기본 Park는 성공해야 합니다.");

            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            var go = GameObject.Find(StableYard.Object);
            var stable = go != null ? go.GetComponent<StableMaster>() : null;
            if (stable == null)
                throw new InvalidOperationException("마을 Stable이 있어야 합니다.");
            Vector3 pos = go.transform.position;
            if (Math.Abs(pos.x - StableYard.X) > 0.4f || Math.Abs(pos.z - StableYard.Z) > 0.4f)
                throw new InvalidOperationException("Stable 좌표가 지정 위치와 같아야 합니다.");
            if (!GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("Stable은 마을 GuardZone 안이어야 합니다.");
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(path) || path.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Stable은 Prefab이어야 합니다(RAW fbx 아님).");
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++)
                    b.Encapsulate(rends[k].bounds);
                if (b.min.y < 0f)
                    throw new InvalidOperationException("Stable이 땅속에 있음 minY=" + b.min.y);
            }
            float[] lxs = { HousingPlot.X, TameCritter.X, TravelGate.X, -6.8f, -5.2f, -3.6f, HousingPlot.X - 1.6f, -7.5f, 2.5f, -5.5f, 4.5f, 1.2f, -2.2f, Dungeon1.EntranceX, Dungeon2.EntranceX, FieldBoss.X };
            float[] lzs = { HousingPlot.Z, TameCritter.Z, TravelGate.Z, 3.4f, 3.4f, -3.6f, HousingPlot.Z + 1.4f, 1.2f, 1.2f, -2.1f, -2.1f, -4.8f, 4.2f, Dungeon1.EntranceZ, Dungeon2.EntranceZ, FieldBoss.Z };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = pos.x - lxs[i];
                float dz = pos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 36f)
                    throw new InvalidOperationException("Stable이 집/랜드마크와 겹치면 안 됩니다.");
            }

            var ghost = StableResolve.Park(new StableRequest { Ghost = true, HasFollower = true, Distance = 1f, Gold = StableYard.GoldCost });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 마구간에 맡기면 안 됩니다.");
            var far = StableResolve.Park(new StableRequest { HasFollower = true, Distance = 20f, Gold = StableYard.GoldCost });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("너무 멀면 마구간 실패해야 합니다.");
            var none = StableResolve.Park(new StableRequest { HasFollower = false, Distance = 1f, Gold = StableYard.GoldCost });
            if (none.Applied || none.FailReason != "no_pet")
                throw new InvalidOperationException("펫 없이 맡기면 안 됩니다.");
            var poor = StableResolve.Park(new StableRequest { HasFollower = true, Distance = 1f, Gold = 0 });
            if (poor.Applied || poor.FailReason != "gold")
                throw new InvalidOperationException("골드 부족 맡김은 실패해야 합니다.");
            var empty = StableResolve.Claim(new StableRequest { HasStabled = false, Distance = 1f });
            if (empty.Applied || empty.FailReason != "empty")
                throw new InvalidOperationException("빈 마구간 찾기는 실패해야 합니다.");

            var tameGo = GameObject.Find(TameCritter.Object);
            var pet = tameGo != null ? tameGo.GetComponent<WorldBody>() : null;
            if (pet == null)
                throw new InvalidOperationException("조련 대상이 있어야 합니다.");
            var worldGo = new GameObject("selfcheck-stable-world");
            GameObject ownerGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ownerGo = new GameObject("selfcheck-stable-owner");
                ownerGo.transform.position = tameGo.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "stable-owner";
                owner.Gold = StableYard.GoldCost;
                owner.ResetHp();
                pet.OwnerCharacterId = "";
                pet.PetFollow = false;
                pet.PetStabled = false;
                if (!tameGo.activeSelf)
                    tameGo.SetActive(true);

                var noPet = world.TryStable(owner, stable);
                if (noPet.Applied)
                    throw new InvalidOperationException("조련 전 맡김은 실패해야 합니다.");

                var tame = world.TryTame(owner, pet);
                if (!tame.Applied)
                    throw new InvalidOperationException("마구간 테스트 조련 실패: " + tame.FailReason);
                float tameSkill = world.SkillsOf(owner).Get(SkillId.AnimalTaming);
                if (world.CountFollowers(owner.CharacterId) < 1)
                    throw new InvalidOperationException("조련 후 팔로워 슬롯이 있어야 합니다.");

                ownerGo.transform.position = go.transform.position + new Vector3(20f, 0f, 20f);
                var rangeFail = world.TryStable(owner, stable);
                if (rangeFail.Applied || rangeFail.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 맡김은 실패해야 합니다.");

                ownerGo.transform.position = go.transform.position;
                owner.Gold = 0;
                var goldFail = world.TryStable(owner, stable);
                if (goldFail.Applied || goldFail.FailReason != "gold")
                    throw new InvalidOperationException("골드 없이 맡김은 실패해야 합니다.");

                owner.Gold = StableYard.GoldCost;
                var parked = world.TryStable(owner, stable);
                if (!parked.Applied)
                    throw new InvalidOperationException("서버 맡김 실패: " + parked.FailReason);
                if (owner.Gold != 0)
                    throw new InvalidOperationException("맡김은 골드를 소모해야 합니다.");
                if (world.CountFollowers(owner.CharacterId) != 0)
                    throw new InvalidOperationException("맡긴 펫은 팔로워 슬롯에서 빠져야 합니다.");
                if (tameGo.activeInHierarchy)
                    throw new InvalidOperationException("맡긴 펫은 despawn 되어야 합니다.");
                if (Math.Abs(world.SkillsOf(owner).Get(SkillId.AnimalTaming) - tameSkill) > 0.0001f)
                    throw new InvalidOperationException("마구간은 조련 스킬을 올리면 안 됩니다.");
                if (!world.HasStabled(owner.CharacterId))
                    throw new InvalidOperationException("맡긴 뒤 HasStabled여야 합니다.");

                var again = world.TryStable(owner, stable);
                if (again.Applied)
                    throw new InvalidOperationException("이미 맡긴 뒤 재맡김은 실패해야 합니다.");

                GameObject otherGo = new GameObject("selfcheck-stable-other");
                try
                {
                    otherGo.transform.position = go.transform.position;
                    var other = otherGo.AddComponent<WorldBody>();
                    other.IsAvatar = true;
                    other.CharacterId = "stable-other";
                    other.Gold = StableYard.GoldCost;
                    other.ResetHp();
                    var otherClaim = world.TryClaimStable(other, stable);
                    if (otherClaim.Applied)
                        throw new InvalidOperationException("타인 마구간 찾기는 실패해야 합니다.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(otherGo);
                }

                var claimed = world.TryClaimStable(owner, stable);
                if (!claimed.Applied)
                    throw new InvalidOperationException("서버 찾기 실패: " + claimed.FailReason);
                if (!tameGo.activeInHierarchy)
                    throw new InvalidOperationException("찾은 펫은 다시 보여야 합니다.");
                if (pet.OwnerCharacterId != owner.CharacterId || !pet.PetFollow || pet.PetStabled)
                    throw new InvalidOperationException("찾은 펫은 주인을 따라야 합니다.");
                if (world.CountFollowers(owner.CharacterId) < 1)
                    throw new InvalidOperationException("찾기 후 팔로워 슬롯이 다시 차야 합니다.");
                if (world.HasStabled(owner.CharacterId))
                    throw new InvalidOperationException("찾기 후 HasStabled가 아니어야 합니다.");
                if (Math.Abs(world.SkillsOf(owner).Get(SkillId.AnimalTaming) - tameSkill) > 0.0001f)
                    throw new InvalidOperationException("찾기는 조련 스킬을 올리면 안 됩니다.");

                var emptyClaim = world.TryClaimStable(owner, stable);
                if (emptyClaim.Applied)
                    throw new InvalidOperationException("빈 마구간 재찾기는 실패해야 합니다.");
            }
            finally
            {
                var leftover = OfflineWorld.Instance;
                if (leftover != null)
                    leftover.ClearStabled("stable-owner");
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                if (tameGo != null)
                {
                    if (!tameGo.activeSelf)
                        tameGo.SetActive(true);
                    pet.OwnerCharacterId = "";
                    pet.PetFollow = false;
                    pet.PetStabled = false;
                    tameGo.transform.SetPositionAndRotation(new Vector3(TameCritter.X, tameGo.transform.position.y, TameCritter.Z), tameGo.transform.rotation);
                }
            }
        }

        static void AssertTravelSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("여행 1은 문게이트만 두고 Recall 주문을 추가하지 않습니다(Bless Count=11과 별개).");

            var go = GameObject.Find(TravelGate.Object);
            var moon = go != null ? go.GetComponent<Moongate>() : null;
            if (moon == null)
                throw new InvalidOperationException("공개 문게이트가 있어야 합니다.");
            Vector3 pos = go.transform.position;
            if (Math.Abs(pos.x - TravelGate.X) > 0.4f || Math.Abs(pos.z - TravelGate.Z) > 0.4f)
                throw new InvalidOperationException("문게이트 좌표가 지정 위치와 같아야 합니다.");
            if (Math.Abs(TravelGate.PlazaX) > 0.0001f || Math.Abs(TravelGate.PlazaZ) > 0.0001f)
                throw new InvalidOperationException("문게이트 목적지는 광장 (0,0)이어야 합니다.");
            float[] lxs = { HousingPlot.X, TameCritter.X, 18.2f, 3.4f, -12.2f, Dungeon1.EntranceX, Dungeon2.EntranceX, 0f, FieldBoss.X };
            float[] lzs = { HousingPlot.Z, TameCritter.Z, 2.4f, -19.6f, 20.4f, Dungeon1.EntranceZ, Dungeon2.EntranceZ, 13.2f, FieldBoss.Z };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = pos.x - lxs[i];
                float dz = pos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 36f)
                    throw new InvalidOperationException("문게이트가 기존 랜드마크와 겹치면 안 됩니다.");
            }

            var ghost = TravelResolve.Gate(new TravelRequest { Ghost = true, Distance = 1f, Gold = TravelGate.GoldCost });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 문게이트를 쓰면 안 됩니다.");
            var far = TravelResolve.Gate(new TravelRequest { Distance = 20f, Gold = TravelGate.GoldCost });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("너무 멀면 문게이트 실패해야 합니다.");
            var poor = TravelResolve.Gate(new TravelRequest { Distance = 1f, Gold = 0 });
            if (poor.Applied || poor.FailReason != "gold")
                throw new InvalidOperationException("골드가 부족하면 여행 실패해야 합니다.");
            var ok = TravelResolve.Gate(new TravelRequest { Distance = 1f, Gold = TravelGate.GoldCost });
            if (!ok.Applied)
                throw new InvalidOperationException("문게이트는 성공해야 합니다: " + ok.FailReason);

            var worldGo = new GameObject("selfcheck-gate-world");
            GameObject bodyGo = null;
            GameObject otherGo = null;
            Vector3 moonPos = go.transform.position;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-gate-body");
                bodyGo.transform.position = moonPos;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "gate-body";
                body.Gold = TravelGate.GoldCost;
                body.ResetHp();
                var hit = world.TryGate(body, moon);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 문게이트 실패: " + hit.FailReason);
                if (Math.Abs(body.transform.position.x - TravelGate.PlazaX) > 0.2f || Math.Abs(body.transform.position.z - TravelGate.PlazaZ) > 0.2f)
                    throw new InvalidOperationException("문게이트는 광장으로 워프해야 합니다.");
                if (body.Gold != 0)
                    throw new InvalidOperationException("여행 골드 비용이 빠져야 합니다.");

                bodyGo.transform.position = moonPos + new Vector3(20f, 0f, 20f);
                body.Gold = TravelGate.GoldCost;
                var rangeFail = world.TryGate(body, moon);
                if (rangeFail.Applied || rangeFail.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 문게이트는 실패해야 합니다.");

                bodyGo.transform.position = moonPos;
                body.Gold = 0;
                var goldFail = world.TryGate(body, moon);
                if (goldFail.Applied || goldFail.FailReason != "gold")
                    throw new InvalidOperationException("골드 없는 문게이트는 실패해야 합니다.");

                otherGo = new GameObject("selfcheck-gate-other");
                bodyGo.transform.position = Vector3.zero;
                otherGo.transform.position = Vector3.zero;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "gate-other";
                other.ResetHp();
                var assault = world.TryAttack(body, other);
                if (assault.Applied || assault.FailReason != "innocent")
                    throw new InvalidOperationException("마을 가드존 무고 공격은 막혀야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertMarkRecall()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("Mark/Recall 1은 주문(Bless 포함 Count=11)과 별개로 골드 기록/귀환만 둡니다.");
            if (Math.Abs(TravelGate.PlazaX) > 0.0001f || Math.Abs(TravelGate.PlazaZ) > 0.0001f)
                throw new InvalidOperationException("문게이트 목적지는 광장이어야 합니다.");

            var ghostMark = TravelResolve.Mark(new TravelRequest { Ghost = true, Gold = TravelMark.GoldCost, GoldCost = TravelMark.GoldCost });
            if (ghostMark.Applied || ghostMark.FailReason != "ghost")
                throw new InvalidOperationException("유령은 위치 기록하면 안 됩니다.");
            var combatMark = TravelResolve.Mark(new TravelRequest { InCombat = true, Gold = TravelMark.GoldCost, GoldCost = TravelMark.GoldCost });
            if (combatMark.Applied || combatMark.FailReason != "combat")
                throw new InvalidOperationException("전투 중 위치 기록은 실패해야 합니다.");
            var poorMark = TravelResolve.Mark(new TravelRequest { Gold = 0, GoldCost = TravelMark.GoldCost });
            if (poorMark.Applied || poorMark.FailReason != "gold")
                throw new InvalidOperationException("골드 부족 기록은 실패해야 합니다.");
            var okMark = TravelResolve.Mark(new TravelRequest { Gold = TravelMark.GoldCost, GoldCost = TravelMark.GoldCost });
            if (!okMark.Applied)
                throw new InvalidOperationException("위치 기록은 성공해야 합니다: " + okMark.FailReason);

            var noMark = TravelResolve.Recall(new TravelRequest());
            if (noMark.Applied || noMark.FailReason != "no_mark")
                throw new InvalidOperationException("기록 없이 귀환하면 안 됩니다.");
            var ghostRecall = TravelResolve.Recall(new TravelRequest { Ghost = true, HasMark = true });
            if (ghostRecall.Applied || ghostRecall.FailReason != "ghost")
                throw new InvalidOperationException("유령은 귀환하면 안 됩니다.");
            var combatRecall = TravelResolve.Recall(new TravelRequest { InCombat = true, HasMark = true });
            if (combatRecall.Applied || combatRecall.FailReason != "combat")
                throw new InvalidOperationException("전투 중 귀환은 실패해야 합니다.");
            var okRecall = TravelResolve.Recall(new TravelRequest { HasMark = true });
            if (!okRecall.Applied)
                throw new InvalidOperationException("귀환은 성공해야 합니다: " + okRecall.FailReason);

            var oak = GameObject.Find("FieldOak");
            float fieldX = oak != null ? oak.transform.position.x : 18.2f;
            float fieldZ = oak != null ? oak.transform.position.z : 2.4f;
            var go = GameObject.Find(TravelGate.Object);
            var moon = go != null ? go.GetComponent<Moongate>() : null;
            if (moon == null)
                throw new InvalidOperationException("공개 문게이트가 있어야 합니다.");

            var worldGo = new GameObject("selfcheck-mark-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-mark-body");
                bodyGo.transform.position = new Vector3(fieldX, 0.1f, fieldZ);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "mark-body";
                body.ResetHp();

                var empty = world.TryRecall(body);
                if (empty.Applied || empty.FailReason != "no_mark")
                    throw new InvalidOperationException("서버 기록 없이 귀환은 실패해야 합니다.");

                body.Gold = 0;
                var goldFail = world.TryMark(body);
                if (goldFail.Applied || goldFail.FailReason != "gold")
                    throw new InvalidOperationException("골드 없는 기록은 실패해야 합니다.");

                body.Ghost = true;
                body.Gold = TravelMark.GoldCost;
                var ghostFail = world.TryMark(body);
                if (ghostFail.Applied || ghostFail.FailReason != "ghost")
                    throw new InvalidOperationException("유령 기록은 실패해야 합니다.");
                body.Ghost = false;

                body.CombatUntil = Time.time + 30f;
                var combatFail = world.TryMark(body);
                if (combatFail.Applied || combatFail.FailReason != "combat")
                    throw new InvalidOperationException("전투 중 서버 기록은 실패해야 합니다.");
                body.CombatUntil = 0f;

                body.Gold = TravelMark.GoldCost;
                var marked = world.TryMark(body);
                if (!marked.Applied)
                    throw new InvalidOperationException("서버 위치 기록 실패: " + marked.FailReason);
                if (body.Gold != 0)
                    throw new InvalidOperationException("기록은 골드를 소모해야 합니다.");
                if (!body.HasMark)
                    throw new InvalidOperationException("기록 후 HasMark여야 합니다.");
                if (Math.Abs(body.MarkX - fieldX) > 0.2f || Math.Abs(body.MarkZ - fieldZ) > 0.2f)
                    throw new InvalidOperationException("기록 좌표는 필드여야 합니다.");

                bodyGo.transform.position = new Vector3(TravelGate.PlazaX, 0.1f, TravelGate.PlazaZ);
                body.Ghost = true;
                var ghostWarp = world.TryRecall(body);
                if (ghostWarp.Applied || ghostWarp.FailReason != "ghost")
                    throw new InvalidOperationException("유령 귀환은 실패해야 합니다.");
                body.Ghost = false;
                body.CombatUntil = Time.time + 30f;
                var combatWarp = world.TryRecall(body);
                if (combatWarp.Applied || combatWarp.FailReason != "combat")
                    throw new InvalidOperationException("전투 중 서버 귀환은 실패해야 합니다.");
                body.CombatUntil = 0f;

                var recalled = world.TryRecall(body);
                if (!recalled.Applied)
                    throw new InvalidOperationException("서버 귀환 실패: " + recalled.FailReason);
                if (Math.Abs(body.transform.position.x - fieldX) > 0.2f || Math.Abs(body.transform.position.z - fieldZ) > 0.2f)
                    throw new InvalidOperationException("귀환은 기록한 필드에 착지해야 합니다.");

                bodyGo.transform.position = go.transform.position;
                body.Gold = TravelGate.GoldCost;
                var gated = world.TryGate(body, moon);
                if (!gated.Applied)
                    throw new InvalidOperationException("문게이트는 그대로 성공해야 합니다: " + gated.FailReason);
                if (Math.Abs(body.transform.position.x - TravelGate.PlazaX) > 0.2f || Math.Abs(body.transform.position.z - TravelGate.PlazaZ) > 0.2f)
                    throw new InvalidOperationException("문게이트는 광장으로 워프해야 합니다.");
                if (Math.Abs(body.transform.position.x - fieldX) < 0.2f && Math.Abs(body.transform.position.z - fieldZ) < 0.2f)
                    throw new InvalidOperationException("문게이트는 기록 좌표가 아니라 광장이어야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertHousingSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            OfflineWorld.Instance?.ResetHousePlot();

            var plot = GameObject.Find(HousingPlot.RootObject);
            if (plot == null)
                throw new InvalidOperationException("지정 HousingPlot이 있어야 합니다.");
            Vector3 pos = plot.transform.position;
            if (Math.Abs(pos.x - HousingPlot.X) > 0.2f || Math.Abs(pos.z - HousingPlot.Z) > 0.2f)
                throw new InvalidOperationException("HousingPlot 좌표가 지정 부지와 같아야 합니다.");
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("HousingPlot은 GuardZone 밖이어야 합니다.");
            float[] lxs = { 18.2f, 3.4f, -12.2f, Dungeon1.EntranceX, Dungeon2.EntranceX };
            float[] lzs = { 2.4f, -19.6f, 20.4f, Dungeon1.EntranceZ, Dungeon2.EntranceZ };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = pos.x - lxs[i];
                float dz = pos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 36f)
                    throw new InvalidOperationException("HousingPlot이 기존 랜드마크와 겹치면 안 됩니다.");
            }
            var station = GameObject.Find(HousingPlot.StationObject);
            if (station == null || station.GetComponent<HousePlotStation>() == null)
                throw new InvalidOperationException("HousePlotStation이 있어야 합니다.");
            var chestGo = GameObject.Find(HousingPlot.ChestObject);
            if (chestGo == null || chestGo.GetComponent<HouseChest>() == null)
                throw new InvalidOperationException("HouseChest가 있어야 합니다.");
            string[] landmarks = { "Forge", "Vendor", "Healer" };
            for (int i = 0; i < landmarks.Length; i++)
            {
                var mark = GameObject.Find(landmarks[i]);
                if (mark == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + landmarks[i]);
                var mr = mark.GetComponentsInChildren<Renderer>();
                if (mr.Length > 0)
                {
                    Bounds mb = mr[0].bounds;
                    for (int k = 1; k < mr.Length; k++)
                        mb.Encapsulate(mr[k].bounds);
                    if (mb.min.y < -0.05f)
                        throw new InvalidOperationException(landmarks[i] + "가 땅속에 있음 minY=" + mb.min.y);
                }
            }
            var vendorGo = GameObject.Find(HousingPlot.VendorObject);
            if (vendorGo == null || vendorGo.GetComponent<HouseVendor>() == null)
                throw new InvalidOperationException("HouseVendor가 있어야 합니다.");
            string vpath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(vendorGo);
            if (string.IsNullOrEmpty(vpath) || vpath.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("HouseVendor는 Prefab이어야 합니다(RAW fbx 아님).");
            var vr = vendorGo.GetComponentsInChildren<Renderer>();
            if (vr.Length > 0)
            {
                Bounds vb = vr[0].bounds;
                for (int k = 1; k < vr.Length; k++)
                    vb.Encapsulate(vr[k].bounds);
                if (vb.min.y < -0.05f)
                    throw new InvalidOperationException("HouseVendor가 땅속에 있음 minY=" + vb.min.y);
            }

            var none = HouseResolve.Claim(new HouseRequest
            {
                PlotExists = true,
                Occupied = false,
                Distance = 1f,
                Gold = 0,
                ActorCharacterId = "a"
            });
            if (none.Applied || none.FailReason != "gold")
                throw new InvalidOperationException("골드 부족 claim은 실패해야 합니다.");
            var occupied = HouseResolve.Claim(new HouseRequest
            {
                PlotExists = true,
                Occupied = true,
                Distance = 1f,
                Gold = HousingPlot.ClaimGold,
                ActorCharacterId = "b"
            });
            if (occupied.Applied || occupied.FailReason != "occupied")
                throw new InvalidOperationException("점유 부지 claim은 실패해야 합니다.");
            var strangerLock = HouseResolve.Lockdown(new HouseRequest
            {
                PlotExists = true,
                Occupied = true,
                Distance = 1f,
                ActorCharacterId = "x",
                OwnerCharacterId = "y",
                HasBackpackItem = true
            });
            if (strangerLock.Applied || strangerLock.FailReason != "not_owner")
                throw new InvalidOperationException("타인 lockdown은 실패해야 합니다.");

            var worldGo = new GameObject("selfcheck-house-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ownerGo = new GameObject("selfcheck-house-owner");
                ownerGo.transform.position = station.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "house-owner";
                owner.AccountId = "house-acc";
                owner.Gold = HousingPlot.ClaimGold;
                owner.ResetHp();
                var bag = ownerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Cloth, 1);

                var stationMb = station.GetComponent<HousePlotStation>();
                var chestMb = chestGo.GetComponent<HouseChest>();
                var claim = world.TryClaimHouse(owner, stationMb);
                if (!claim.Applied)
                    throw new InvalidOperationException("빈 부지 claim은 성공해야 합니다: " + claim.FailReason);
                if (owner.Gold != 0)
                    throw new InvalidOperationException("claim은 골드를 소모해야 합니다.");
                var again = world.TryClaimHouse(owner, stationMb);
                if (again.Applied)
                    throw new InvalidOperationException("같은 캐릭터 재claim은 실패해야 합니다.");

                otherGo = new GameObject("selfcheck-house-other");
                otherGo.transform.position = station.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "house-other";
                other.AccountId = "house-other-acc";
                other.Gold = HousingPlot.ClaimGold;
                other.ResetHp();
                otherGo.AddComponent<InventoryBag>().Add(ItemCatalog.Cloth, 1);
                var otherClaim = world.TryClaimHouse(other, stationMb);
                if (otherClaim.Applied)
                    throw new InvalidOperationException("타인 점유 부지 claim은 실패해야 합니다.");
                otherGo.transform.position = chestGo.transform.position;
                var otherLock = world.TryLockdown(other, chestMb, ItemCatalog.Cloth);
                if (otherLock.Applied)
                    throw new InvalidOperationException("타인 lockdown은 실패해야 합니다.");
                var otherTake = world.TrySecureTake(other, chestMb);
                if (otherTake.Applied)
                    throw new InvalidOperationException("타인 secure take는 실패해야 합니다.");

                ownerGo.transform.position = chestGo.transform.position;
                var lockOk = world.TryLockdown(owner, chestMb, ItemCatalog.Cloth);
                if (!lockOk.Applied)
                    throw new InvalidOperationException("소유자 lockdown은 성공해야 합니다: " + lockOk.FailReason);
                int clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft != 0)
                    throw new InvalidOperationException("lockdown은 가방 천을 옮겨야 합니다.");
                var takeOk = world.TrySecureTake(owner, chestMb);
                if (!takeOk.Applied)
                    throw new InvalidOperationException("소유자 take는 성공해야 합니다: " + takeOk.FailReason);
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft < 1)
                    throw new InvalidOperationException("take는 천을 가방으로 되돌려야 합니다.");

                var vendorMb = vendorGo.GetComponent<HouseVendor>();
                ownerGo.transform.position = vendorGo.transform.position;
                otherGo.transform.position = vendorGo.transform.position;
                var otherList = world.TryListVendor(other, vendorMb, ItemCatalog.Cloth);
                if (otherList.Applied)
                    throw new InvalidOperationException("타인 vendor list는 실패해야 합니다.");
                int price = ItemCatalog.BuyPrice(ItemCatalog.Cloth);
                var listed = world.TryListVendor(owner, vendorMb, ItemCatalog.Cloth);
                if (!listed.Applied)
                    throw new InvalidOperationException("소유자 vendor list는 성공해야 합니다: " + listed.FailReason);
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft != 0)
                    throw new InvalidOperationException("list는 가방 천을 옮겨야 합니다.");
                bag.Add(ItemCatalog.Cloth, 1);
                var listedAgain = world.TryListVendor(owner, vendorMb, ItemCatalog.Cloth);
                if (listedAgain.Applied)
                    throw new InvalidOperationException("vendor slot 1을 넘기면 안 됩니다.");
                var otherBag = otherGo.GetComponent<InventoryBag>();
                other.Gold = price;
                var buy = world.TryBuyHouseVendor(other, vendorMb);
                if (!buy.Applied)
                    throw new InvalidOperationException("타인 vendor buy는 성공해야 합니다: " + buy.FailReason);
                if (other.Gold != 0)
                    throw new InvalidOperationException("buy는 골드를 소모해야 합니다.");
                if (owner.Gold != price)
                    throw new InvalidOperationException("buy는 소유자에게 골드를 줘야 합니다.");
                int otherCloth = 0;
                for (int i = 0; i < otherBag.Items.Count; i++)
                    if (otherBag.Items[i].TemplateId == ItemCatalog.Cloth)
                        otherCloth += otherBag.Items[i].Amount;
                if (otherCloth < 1)
                    throw new InvalidOperationException("buy는 천을 구매자 가방으로 옮겨야 합니다.");
                var buyEmpty = world.TryBuyHouseVendor(other, vendorMb);
                if (buyEmpty.Applied)
                    throw new InvalidOperationException("빈 vendor buy는 실패해야 합니다.");
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertEastFieldSlice()
        {
            var field = GameObject.Find("EastField");
            if (field == null)
                throw new InvalidOperationException("마을 옆 동쪽 필드(EastField)가 있어야 합니다.");
            var oak = GameObject.Find("OakTree");
            if (oak == null)
                throw new InvalidOperationException("마을 OakTree를 필드가 대체하면 안 됩니다.");
            var villageNode = oak.GetComponent<ResourceNode>();
            if (villageNode == null || villageNode.GatherSkill != SkillId.Lumberjacking)
                throw new InvalidOperationException("마을 OakTree 벌목 노드가 유지되어야 합니다.");
            var go = GameObject.Find("FieldOak");
            if (go == null)
                throw new InvalidOperationException("동쪽 필드에 FieldOak가 있어야 합니다.");
            var node = go.GetComponent<ResourceNode>();
            if (node == null || node.GatherSkill != SkillId.Lumberjacking || node.ResourceId != "wood")
                throw new InvalidOperationException("FieldOak는 벌목 ResourceNode여야 합니다.");
            Vector3 pos = go.transform.position;
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("동쪽 필드는 가드존 밖이어야 합니다.");
            var terrain = GameObject.Find("Ground");
            var data = terrain != null ? terrain.GetComponent<Terrain>() : null;
            if (data == null || data.terrainData == null)
                throw new InvalidOperationException("Ground Terrain이 있어야 합니다.");
            Vector3 size = data.terrainData.size;
            if (Math.Abs(size.x - 180f) > 0.1f || Math.Abs(size.z - 180f) > 0.1f)
                throw new InvalidOperationException("필드 추가로 맵 크기를 키우면 안 됩니다.");

            var worldGo = new GameObject("selfcheck-field-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-field-body");
                bodyGo.transform.position = go.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bodyGo.AddComponent<InventoryBag>();
                var noTool = world.TryGather(body, node);
                if (noTool.Applied)
                    throw new InvalidOperationException("도끼 없이 들판 벌목되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Hatchet, Amount = 1, Uses = 4 });
                float lumber0 = world.SkillsOf(body).Get(SkillId.Lumberjacking);
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("들판 벌목 실패: " + ok.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Lumberjacking) < lumber0 + 0.09f)
                    throw new InvalidOperationException("들판 벌목 후 벌목이 올라야 합니다.");
                int wood = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == "wood")
                        wood += bag.Items[i].Amount;
                if (wood < 1)
                    throw new InvalidOperationException("들판 나무가 가방에 있어야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertSouthFieldSlice()
        {
            var east = GameObject.Find("EastField");
            if (east == null)
                throw new InvalidOperationException("남쪽 필드가 동쪽 필드를 대체하면 안 됩니다.");
            var field = GameObject.Find("SouthField");
            if (field == null)
                throw new InvalidOperationException("마을 남쪽 필드(SouthField)가 있어야 합니다.");
            var flax = GameObject.Find("FieldFlax");
            if (flax == null)
                throw new InvalidOperationException("남쪽 필드에 FieldFlax가 있어야 합니다.");
            var node = flax.GetComponent<ResourceNode>();
            if (node == null || node.GatherSkill != SkillId.Tailoring || node.ResourceId != ItemCatalog.Cloth)
                throw new InvalidOperationException("FieldFlax는 재봉 ResourceNode(천)여야 합니다.");
            Vector3 pos = flax.transform.position;
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("남쪽 필드는 가드존 밖이어야 합니다.");
            if (pos.z > -16.5f)
                throw new InvalidOperationException("남쪽 필드는 마을 울타리 남쪽이어야 합니다.");
            var oak = GameObject.Find("FieldOak");
            if (oak == null)
                throw new InvalidOperationException("동쪽 FieldOak가 유지되어야 합니다.");
            if (Vector3.Distance(pos, oak.transform.position) < 14f)
                throw new InvalidOperationException("남쪽 필드는 동쪽 필드와 떨어져 있어야 합니다.");
            var hunt = GameObject.Find("Raider");
            if (hunt != null && Vector3.Distance(pos, hunt.transform.position) < 12f)
                throw new InvalidOperationException("남쪽 필드가 사냥 라인을 건드리면 안 됩니다.");
            var fish = GameObject.Find("FishingSpot");
            if (fish != null && Vector3.Distance(pos, fish.transform.position) < 8f)
                throw new InvalidOperationException("남쪽 필드가 물가를 건드리면 안 됩니다.");
            var terrain = GameObject.Find("Ground");
            var data = terrain != null ? terrain.GetComponent<Terrain>() : null;
            if (data == null || data.terrainData == null)
                throw new InvalidOperationException("Ground Terrain이 있어야 합니다.");
            Vector3 size = data.terrainData.size;
            if (Math.Abs(size.x - 180f) > 0.1f || Math.Abs(size.z - 180f) > 0.1f)
                throw new InvalidOperationException("필드 추가로 맵 크기를 키우면 안 됩니다.");

            var worldGo = new GameObject("selfcheck-south-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-south-body");
                bodyGo.transform.position = flax.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bodyGo.AddComponent<InventoryBag>();
                float tailor0 = world.SkillsOf(body).Get(SkillId.Tailoring);
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("남쪽 아마 채집 실패: " + ok.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Tailoring) < tailor0 + 0.09f)
                    throw new InvalidOperationException("아마 채집 후 재봉이 올라야 합니다.");
                int cloth = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        cloth += bag.Items[i].Amount;
                if (cloth < 1)
                    throw new InvalidOperationException("아마에서 나온 천이 가방에 있어야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertNorthFieldSlice()
        {
            var east = GameObject.Find("EastField");
            if (east == null)
                throw new InvalidOperationException("북쪽 필드가 동쪽 필드를 대체하면 안 됩니다.");
            var south = GameObject.Find("SouthField");
            if (south == null)
                throw new InvalidOperationException("북쪽 필드가 남쪽 필드를 대체하면 안 됩니다.");
            var field = GameObject.Find("NorthField");
            if (field == null)
                throw new InvalidOperationException("마을 북쪽 필드(NorthField)가 있어야 합니다.");
            var ore = GameObject.Find("FieldOre");
            if (ore == null)
                throw new InvalidOperationException("북쪽 필드에 FieldOre가 있어야 합니다.");
            var node = ore.GetComponent<ResourceNode>();
            if (node == null || node.GatherSkill != SkillId.Mining || node.ResourceId != "iron_ore")
                throw new InvalidOperationException("FieldOre는 채광 ResourceNode(철광)여야 합니다.");
            Vector3 pos = ore.transform.position;
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("북쪽 필드는 가드존 밖이어야 합니다.");
            if (pos.z < 16.5f)
                throw new InvalidOperationException("북쪽 필드는 마을 울타리 북쪽이어야 합니다.");
            var oak = GameObject.Find("FieldOak");
            if (oak == null)
                throw new InvalidOperationException("동쪽 FieldOak가 유지되어야 합니다.");
            if (Vector3.Distance(pos, oak.transform.position) < 14f)
                throw new InvalidOperationException("북쪽 필드는 동쪽 필드와 떨어져 있어야 합니다.");
            var flax = GameObject.Find("FieldFlax");
            if (flax == null)
                throw new InvalidOperationException("남쪽 FieldFlax가 유지되어야 합니다.");
            if (Vector3.Distance(pos, flax.transform.position) < 14f)
                throw new InvalidOperationException("북쪽 필드는 남쪽 필드와 떨어져 있어야 합니다.");
            var hunt = GameObject.Find("Raider");
            if (hunt != null && Vector3.Distance(pos, hunt.transform.position) < 12f)
                throw new InvalidOperationException("북쪽 필드가 사냥 라인을 건드리면 안 됩니다.");
            var gate = GameObject.Find(Dungeon1.EntranceObject);
            if (gate != null && Vector3.Distance(pos, gate.transform.position) < 12f)
                throw new InvalidOperationException("북쪽 필드가 던전 1 서쪽 입구를 건드리면 안 됩니다.");
            var vein = GameObject.Find("IronVein");
            if (vein == null)
                throw new InvalidOperationException("마을 IronVein을 필드가 대체하면 안 됩니다.");
            var villageNode = vein.GetComponent<ResourceNode>();
            if (villageNode == null || villageNode.GatherSkill != SkillId.Mining)
                throw new InvalidOperationException("마을 IronVein 채광 노드가 유지되어야 합니다.");
            var terrain = GameObject.Find("Ground");
            var data = terrain != null ? terrain.GetComponent<Terrain>() : null;
            if (data == null || data.terrainData == null)
                throw new InvalidOperationException("Ground Terrain이 있어야 합니다.");
            Vector3 size = data.terrainData.size;
            if (Math.Abs(size.x - 180f) > 0.1f || Math.Abs(size.z - 180f) > 0.1f)
                throw new InvalidOperationException("필드 추가로 맵 크기를 키우면 안 됩니다.");

            var worldGo = new GameObject("selfcheck-north-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-north-body");
                bodyGo.transform.position = ore.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bodyGo.AddComponent<InventoryBag>();
                var noTool = world.TryGather(body, node);
                if (noTool.Applied)
                    throw new InvalidOperationException("곡괭이 없이 들판 채광되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 4 });
                float mine0 = world.SkillsOf(body).Get(SkillId.Mining);
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("북쪽 광맥 채집 실패: " + ok.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Mining) < mine0 + 0.09f)
                    throw new InvalidOperationException("광맥 채집 후 채광이 올라야 합니다.");
                int oreCount = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == "iron_ore")
                        oreCount += bag.Items[i].Amount;
                if (oreCount < 1)
                    throw new InvalidOperationException("들판 철광이 가방에 있어야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertFishingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Fishing) != StatId.Dex)
                throw new InvalidOperationException("낚시 Primary는 DEX이어야 합니다.");
            if (ItemCatalog.ToolFor(SkillId.Fishing) != ItemCatalog.FishingPole)
                throw new InvalidOperationException("낚시는 낚싯대가 도구여야 합니다.");
            if (ItemCatalog.MaxUsesOf(ItemCatalog.FishingPole) != 20)
                throw new InvalidOperationException("낚싯대 내구 20");
            if (ItemCatalog.BuyPrice(ItemCatalog.FishingPole) <= 0 || ItemCatalog.SellPrice(ItemCatalog.Fish) <= 0)
                throw new InvalidOperationException("낚싯대/생선 상점 가격이 없습니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Fishing, 10f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("낚시 0.0→0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("낚시 상승 시 DEX가 올라야 합니다.");

            var created = CharacterCreate.Build("fish-check", "낚시", 0, 20, 40, 20,
                new[] { SkillId.Fishing, SkillId.Mining, SkillId.Swordsmanship },
                new[] { 50f, 30f, 20f });
            bool hasPole = false;
            for (int i = 0; i < created.Inventory.Length; i++)
                if (created.Inventory[i].TemplateId == ItemCatalog.FishingPole && created.Inventory[i].Uses == 20)
                    hasPole = true;
            if (!hasPole)
                throw new InvalidOperationException("낚시 시작은 낚싯대를 줘야 합니다.");

            var spot = GameObject.Find("FishingSpot");
            if (spot == null)
                throw new InvalidOperationException("마을에 물가(FishingSpot)가 있어야 합니다.");
            var sceneNode = spot.GetComponent<ResourceNode>();
            if (sceneNode == null || sceneNode.GatherSkill != SkillId.Fishing || sceneNode.ResourceId != ItemCatalog.Fish)
                throw new InvalidOperationException("물가는 낚시 ResourceNode여야 합니다.");

            var go = new GameObject("selfcheck-fish");
            GameObject worldGo = null;
            GameObject nodeGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-fish-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = go.AddComponent<InventoryBag>();
                nodeGo = new GameObject("selfcheck-fish-node");
                nodeGo.transform.position = go.transform.position;
                var node = nodeGo.AddComponent<ResourceNode>();
                node.ResourceId = ItemCatalog.Fish;
                node.DisplayName = "물가";
                node.GatherSkill = SkillId.Fishing;
                node.Remaining = 5;
                node.Capacity = 5;
                node.Difficulty = 10f;
                var noTool = world.TryGather(body, node);
                if (noTool.Applied)
                    throw new InvalidOperationException("낚싯대 없이 낚시되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fishing)) > 0.0001f)
                    throw new InvalidOperationException("실패한 낚시는 스킬을 올리면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.FishingPole, Amount = 1, Uses = 1 });
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("낚싯대 낚시 실패: " + ok.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fishing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 낚시 후 0.1이어야 합니다.");
                int fish = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Fish)
                        fish += bag.Items[i].Amount;
                if (fish < 1)
                    throw new InvalidOperationException("잡은 생선이 가방에 있어야 합니다.");
                var broken = world.TryGather(body, node);
                if (broken.Applied)
                    throw new InvalidOperationException("내구 0 낚싯대로 낚시되면 안 됩니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Fishing, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Fishing, 10f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Fishing)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 낚시는 오르면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (nodeGo != null)
                    UnityEngine.Object.DestroyImmediate(nodeGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertFencingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Fencing) != StatId.Dex)
                throw new InvalidOperationException("창술 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Fencing) != "창술" || SkillTitles.JobOf(SkillId.Fencing) != "창수")
                throw new InvalidOperationException("창술 스킬명/직업명이 기획과 같아야 합니다.");
            if (ItemCatalog.FencingRange <= ItemCatalog.MeleeRange || ItemCatalog.FencingRange >= ItemCatalog.ArcheryRange)
                throw new InvalidOperationException("창 사거리는 근접과 활 사이여야 합니다.");
            var rec = CraftRecipes.Find("wooden_spear");
            if (rec == null || rec.Ingredient != "wood" || rec.Count != 2 || rec.Output != ItemCatalog.WoodenSpear
                || rec.Skill != SkillId.Carpentry || !rec.CanRepair)
                throw new InvalidOperationException("나무 2 → 나무창 목공 레시피가 있어야 합니다.");
            if (ItemCatalog.CombatSkillOf(ItemCatalog.WoodenSpear) != SkillId.Fencing)
                throw new InvalidOperationException("나무창 전투 스킬은 창술이어야 합니다.");
            if (Math.Abs(ItemCatalog.CombatRangeOf(SkillId.Fencing) - ItemCatalog.FencingRange) > 0.0001f)
                throw new InvalidOperationException("창술 CombatRange가 FencingRange여야 합니다.");
            if (ItemCatalog.WeightOf(ItemCatalog.WoodenSpear) <= 0f || ItemCatalog.BuyPrice(ItemCatalog.WoodenSpear) <= 0
                || ItemCatalog.MaxUsesOf(ItemCatalog.WoodenSpear) <= 0)
                throw new InvalidOperationException("나무창 무게/가격/내구가 없습니다.");

            var missSkills = new SkillSet();
            var far = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 7f,
                Range = ItemCatalog.FencingRange,
                WeaponSkill = SkillId.Fencing,
                Skills = missSkills,
                TargetAlive = true
            });
            if (far.Applied)
                throw new InvalidOperationException("창 사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(missSkills.Get(SkillId.Fencing)) > 0.0001f || Math.Abs(missSkills.Get(SkillId.Swordsmanship)) > 0.0001f)
                throw new InvalidOperationException("실패한 창 공격은 스킬을 올리면 안 됩니다.");

            var hitSkills = new SkillSet();
            var hit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 4f,
                Range = ItemCatalog.FencingRange,
                Now = 1f,
                NextAttackAt = 0f,
                WeaponSkill = SkillId.Fencing,
                Skills = hitSkills,
                TargetAlive = true
            });
            if (!hit.Applied || !hit.Hit)
                throw new InvalidOperationException("창 사거리 안 공격이 들어가야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Fencing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("창술 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("창 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("창 공격 후 해부학 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(hitSkills.Get(SkillId.Archery)) > 0.0001f)
                throw new InvalidOperationException("창 공격은 검술/궁술을 올리면 안 됩니다.");

            var meleeAtSpear = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 4f,
                Skills = new SkillSet(),
                TargetAlive = true
            });
            if (meleeAtSpear.Applied)
                throw new InvalidOperationException("근접은 창 사거리에서 들어가면 안 됩니다.");

            var dexLow = new StatSet();
            dexLow.ForceSet(50, 10, 25);
            var dexHigh = new StatSet();
            dexHigh.ForceSet(50, 50, 25);
            var low = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.FencingRange,
                Now = 2f,
                WeaponSkill = SkillId.Fencing,
                Skills = new SkillSet(),
                Stats = dexLow,
                TargetAlive = true
            });
            var high = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.FencingRange,
                Now = 2f,
                WeaponSkill = SkillId.Fencing,
                Skills = new SkillSet(),
                Stats = dexHigh,
                TargetAlive = true
            });
            if (high.Damage <= low.Damage)
                throw new InvalidOperationException("창술 피해는 DEX 보정이 있어야 합니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Fencing, 20f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("창술 숙련 0.0→0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("창술 상승 시 DEX가 올라야 합니다.");

            var created = CharacterCreate.Build("fence-check", "창수", 0, 20, 40, 20,
                new[] { SkillId.Fencing, SkillId.Carpentry, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasSpear = false, hasWood = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.WoodenSpear)
                    hasSpear = true;
                if (created.Inventory[i].TemplateId == "wood")
                    hasWood = true;
            }
            if (!hasSpear)
                throw new InvalidOperationException("창술 시작은 나무창을 줘야 합니다.");
            if (!hasWood)
                throw new InvalidOperationException("목공 시작은 나무를 줘야 합니다.");

            var go = new GameObject("selfcheck-fence");
            GameObject worldGo = null;
            GameObject stGo = null;
            GameObject dummy = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-fence-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "fence-mark";
                var bag = go.AddComponent<InventoryBag>();
                stGo = new GameObject("selfcheck-fence-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "wooden_spear";
                station.DisplayName = "목공소";
                var noIng = world.TryCraft(body, station);
                if (noIng.Applied)
                    throw new InvalidOperationException("재료 없이 나무창이 되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Carpentry)) > 0.0001f)
                    throw new InvalidOperationException("실패한 목공은 스킬을 올리면 안 됩니다.");
                bag.Add("wood", 2);
                var made = world.TryCraft(body, station);
                if (!made.Applied)
                    throw new InvalidOperationException("나무창 제작 실패: " + made.FailReason);
                bool spear = false;
                int woodLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenSpear)
                    {
                        spear = true;
                        if (bag.Items[i].MakerId != "fence-mark")
                            throw new InvalidOperationException("나무창 Maker Mark가 있어야 합니다.");
                    }
                    if (bag.Items[i].TemplateId == "wood")
                        woodLeft += bag.Items[i].Amount;
                }
                if (!spear || woodLeft != 0)
                    throw new InvalidOperationException("나무 2 → 나무창 1이어야 합니다.");

                dummy = new GameObject("selfcheck-fence-skel");
                var skel = dummy.AddComponent<WorldBody>();
                skel.IsEnemy = true;
                skel.MaxHp = 40f;
                skel.ResetHp();
                dummy.transform.position = go.transform.position + new Vector3(0f, 0f, 4.2f);
                var shot = world.TryAttack(body, skel);
                if (!shot.Applied)
                    throw new InvalidOperationException("창 중거리 공격 실패: " + shot.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fencing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("창 공격 후 창술 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("창 공격 후 전술 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("창 공격 후 해부학 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Swordsmanship)) > 0.0001f)
                    throw new InvalidOperationException("창 공격은 검술을 올리면 안 됩니다.");
                if (skel.Hp >= 40f)
                    throw new InvalidOperationException("창 공격이 피해를 줘야 합니다.");

                dummy.transform.position = go.transform.position + new Vector3(0f, 0f, 7.5f);
                var tooFar = world.TryAttack(body, skel);
                if (tooFar.Applied)
                    throw new InvalidOperationException("창은 활 사거리에서 들어가면 안 됩니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Fencing, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Fencing, 20f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Fencing)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 창술은 오르면 안 됩니다.");
            }
            finally
            {
                if (dummy != null)
                    UnityEngine.Object.DestroyImmediate(dummy);
                UnityEngine.Object.DestroyImmediate(go);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertMaceSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Mace) != StatId.Str)
                throw new InvalidOperationException("둔기술 Primary는 STR이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Mace) != "둔기술" || SkillTitles.JobOf(SkillId.Mace) != "둔기수")
                throw new InvalidOperationException("둔기술 스킬명/직업명이 기획과 같아야 합니다.");
            if (ItemCatalog.CombatSkillOf(ItemCatalog.WoodenClub) != SkillId.Mace)
                throw new InvalidOperationException("나무곤봉 전투 스킬은 둔기술이어야 합니다.");
            if (ItemCatalog.CombatSkillOf(ItemCatalog.IronSword) != SkillId.Swordsmanship)
                throw new InvalidOperationException("철검은 검술이어야 합니다.");
            if (ItemCatalog.CombatSkillOf(ItemCatalog.WoodenSpear) != SkillId.Fencing)
                throw new InvalidOperationException("나무창은 창술이어야 합니다.");
            if (Math.Abs(ItemCatalog.CombatRangeOf(SkillId.Mace) - ItemCatalog.MeleeRange) > 0.0001f)
                throw new InvalidOperationException("둔기술 CombatRange는 근접이어야 합니다.");
            var rec = CraftRecipes.Find("wooden_club");
            if (rec == null || rec.Ingredient != "wood" || rec.Count != 2 || rec.Output != ItemCatalog.WoodenClub
                || rec.Skill != SkillId.Carpentry || !rec.CanRepair)
                throw new InvalidOperationException("나무 2 → 나무곤봉 목공 레시피가 있어야 합니다.");
            if (ItemCatalog.WeightOf(ItemCatalog.WoodenClub) <= 0f || ItemCatalog.BuyPrice(ItemCatalog.WoodenClub) <= 0
                || ItemCatalog.MaxUsesOf(ItemCatalog.WoodenClub) <= 0)
                throw new InvalidOperationException("나무곤봉 무게/가격/내구가 없습니다.");

            var clubOnly = new[] { new ItemRecord { TemplateId = ItemCatalog.WoodenClub, Amount = 1, Uses = 10 } };
            if (ItemCatalog.CombatWeaponOf(clubOnly) != ItemCatalog.WoodenClub)
                throw new InvalidOperationException("곤봉만 있으면 전투 무기는 나무곤봉이어야 합니다.");
            var clubAndSword = new[]
            {
                new ItemRecord { TemplateId = ItemCatalog.WoodenClub, Amount = 1, Uses = 10 },
                new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 10 }
            };
            if (ItemCatalog.CombatWeaponOf(clubAndSword) != ItemCatalog.IronSword)
                throw new InvalidOperationException("철검이 있으면 검이 곤봉보다 우선이어야 합니다.");

            var missSkills = new SkillSet();
            var far = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 4f,
                Range = ItemCatalog.MeleeRange,
                WeaponSkill = SkillId.Mace,
                Skills = missSkills,
                TargetAlive = true
            });
            if (far.Applied)
                throw new InvalidOperationException("둔기 사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(missSkills.Get(SkillId.Mace)) > 0.0001f || Math.Abs(missSkills.Get(SkillId.Swordsmanship)) > 0.0001f
                || Math.Abs(missSkills.Get(SkillId.Fencing)) > 0.0001f)
                throw new InvalidOperationException("실패한 둔기 공격은 스킬을 올리면 안 됩니다.");

            var hitSkills = new SkillSet();
            var hit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.MeleeRange,
                Now = 1f,
                NextAttackAt = 0f,
                WeaponSkill = SkillId.Mace,
                Skills = hitSkills,
                TargetAlive = true
            });
            if (!hit.Applied || !hit.Hit)
                throw new InvalidOperationException("둔기 근접 공격이 들어가야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Mace) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("둔기술 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("둔기 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("둔기 공격 후 해부학 0.0→0.1이어야 합니다.");
            if (Math.Abs(hitSkills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(hitSkills.Get(SkillId.Fencing)) > 0.0001f
                || Math.Abs(hitSkills.Get(SkillId.Archery)) > 0.0001f)
                throw new InvalidOperationException("둔기 공격은 검술/창술/궁술을 올리면 안 됩니다.");

            var strLow = new StatSet();
            strLow.ForceSet(10, 50, 25);
            var strHigh = new StatSet();
            strHigh.ForceSet(50, 50, 25);
            var low = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.MeleeRange,
                Now = 2f,
                WeaponSkill = SkillId.Mace,
                Skills = new SkillSet(),
                Stats = strLow,
                TargetAlive = true
            });
            var high = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.MeleeRange,
                Now = 2f,
                WeaponSkill = SkillId.Mace,
                Skills = new SkillSet(),
                Stats = strHigh,
                TargetAlive = true
            });
            if (high.Damage <= low.Damage)
                throw new InvalidOperationException("둔기술 피해는 STR 보정이 있어야 합니다.");
            var dexHigh = new StatSet();
            dexHigh.ForceSet(10, 90, 25);
            var dexHit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.MeleeRange,
                Now = 3f,
                WeaponSkill = SkillId.Mace,
                Skills = new SkillSet(),
                Stats = dexHigh,
                TargetAlive = true
            });
            if (dexHit.Damage != low.Damage)
                throw new InvalidOperationException("둔기술 피해는 DEX가 아니라 STR이어야 합니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int strWas = stats.Str;
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Mace, 20f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("둔기술 숙련 0.0→0.1이어야 합니다.");
            if (stats.Str != strWas + 1)
                throw new InvalidOperationException("둔기술 상승 시 STR이 올라야 합니다.");
            if (stats.Dex != dexWas)
                throw new InvalidOperationException("둔기술 상승 시 DEX가 올라가면 안 됩니다.");

            var created = CharacterCreate.Build("mace-check", "둔기수", 0, 40, 20, 20,
                new[] { SkillId.Mace, SkillId.Carpentry, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasClub = false, hasWood = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.WoodenClub)
                    hasClub = true;
                if (created.Inventory[i].TemplateId == "wood")
                    hasWood = true;
            }
            if (!hasClub)
                throw new InvalidOperationException("둔기술 시작은 나무곤봉을 줘야 합니다.");
            if (!hasWood)
                throw new InvalidOperationException("목공 시작은 나무를 줘야 합니다.");

            var go = new GameObject("selfcheck-mace");
            GameObject worldGo = null;
            GameObject stGo = null;
            GameObject dummy = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-mace-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "mace-mark";
                var bag = go.AddComponent<InventoryBag>();
                stGo = new GameObject("selfcheck-mace-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "wooden_club";
                station.DisplayName = "목공소";
                var noIng = world.TryCraft(body, station);
                if (noIng.Applied)
                    throw new InvalidOperationException("재료 없이 나무곤봉이 되면 안 됩니다.");
                bag.Add("wood", 2);
                var made = world.TryCraft(body, station);
                if (!made.Applied)
                    throw new InvalidOperationException("나무곤봉 제작 실패: " + made.FailReason);
                bool club = false;
                int woodLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenClub)
                    {
                        club = true;
                        if (bag.Items[i].MakerId != "mace-mark")
                            throw new InvalidOperationException("나무곤봉 Maker Mark가 있어야 합니다.");
                    }
                    if (bag.Items[i].TemplateId == "wood")
                        woodLeft += bag.Items[i].Amount;
                }
                if (!club || woodLeft != 0)
                    throw new InvalidOperationException("나무 2 → 나무곤봉 1이어야 합니다.");

                dummy = new GameObject("selfcheck-mace-skel");
                var skel = dummy.AddComponent<WorldBody>();
                skel.IsEnemy = true;
                skel.MaxHp = 40f;
                skel.ResetHp();
                dummy.transform.position = go.transform.position + new Vector3(0f, 0f, 1.2f);
                var shot = world.TryAttack(body, skel);
                if (!shot.Applied)
                    throw new InvalidOperationException("둔기 근접 공격 실패: " + shot.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Mace) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격 후 둔기술 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격 후 전술 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격 후 해부학 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Swordsmanship)) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격은 검술을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fencing)) > 0.0001f)
                    throw new InvalidOperationException("둔기 공격은 창술을 올리면 안 됩니다.");
                if (skel.Hp >= 40f)
                    throw new InvalidOperationException("둔기 공격이 피해를 줘야 합니다.");

                dummy.transform.position = go.transform.position + new Vector3(0f, 0f, 4.2f);
                var tooFar = world.TryAttack(body, skel);
                if (tooFar.Applied)
                    throw new InvalidOperationException("둔기는 창 사거리에서 들어가면 안 됩니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Mace, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Mace, 20f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Mace)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 둔기술은 오르면 안 됩니다.");
            }
            finally
            {
                if (dummy != null)
                    UnityEngine.Object.DestroyImmediate(dummy);
                UnityEngine.Object.DestroyImmediate(go);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertCookingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Cooking) != StatId.Dex)
                throw new InvalidOperationException("요리 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Cooking) != "요리" || SkillTitles.JobOf(SkillId.Cooking) != "요리사")
                throw new InvalidOperationException("요리 스킬명/직업명이 기획과 같아야 합니다.");
            var fishRec = CraftRecipes.Find("cooked_fish");
            if (fishRec == null || fishRec.Ingredient != ItemCatalog.Fish || fishRec.Output != ItemCatalog.CookedFood
                || fishRec.Skill != SkillId.Cooking || fishRec.Count != 1)
                throw new InvalidOperationException("생선 1 → 요리음식 레시피가 있어야 합니다.");
            var wrap = CraftRecipes.Find("cooked_wrap");
            if (wrap == null || wrap.Ingredient != ItemCatalog.Cloth || wrap.Output != ItemCatalog.CookedFood
                || wrap.Skill != SkillId.Cooking)
                throw new InvalidOperationException("천(재봉 인접) → 요리음식 레시피가 있어야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.CookedFood) <= 0 || ItemCatalog.WeightOf(ItemCatalog.CookedFood) <= 0f)
                throw new InvalidOperationException("요리음식 무게/가격이 없습니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Cooking, 10f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("요리 0.0→0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("요리 상승 시 DEX가 올라야 합니다.");

            var created = CharacterCreate.Build("cook-check", "요리", 0, 20, 40, 20,
                new[] { SkillId.Cooking, SkillId.Fishing, SkillId.Swordsmanship },
                new[] { 50f, 30f, 20f });
            bool hasFish = false, hasPole = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Fish && created.Inventory[i].Amount >= 1)
                    hasFish = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.FishingPole)
                    hasPole = true;
            }
            if (!hasFish)
                throw new InvalidOperationException("요리 시작은 생선을 줘야 합니다.");
            if (!hasPole)
                throw new InvalidOperationException("낚시 시작은 낚싯대를 줘야 합니다.");

            var fire = GameObject.Find("Campfire");
            if (fire == null)
                throw new InvalidOperationException("마을에 화덕(Campfire)이 있어야 합니다.");
            var sceneSt = fire.GetComponent<CraftStation>();
            if (sceneSt == null || sceneSt.RecipeId != "cooked_fish")
                throw new InvalidOperationException("화덕은 생선 요리 CraftStation이어야 합니다.");

            var go = new GameObject("selfcheck-cook");
            GameObject worldGo = null;
            GameObject stGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-cook-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = go.AddComponent<InventoryBag>();
                stGo = new GameObject("selfcheck-cook-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "cooked_fish";
                station.DisplayName = "화덕";
                var noIng = world.TryCraft(body, station);
                if (noIng.Applied)
                    throw new InvalidOperationException("재료 없이 요리되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Cooking)) > 0.0001f)
                    throw new InvalidOperationException("실패한 요리는 스킬을 올리면 안 됩니다.");
                bag.Add(ItemCatalog.Fish, 1);
                var ok = world.TryCraft(body, station);
                if (!ok.Applied)
                    throw new InvalidOperationException("생선 요리 실패: " + ok.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Cooking) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 요리 후 0.1이어야 합니다.");
                int cooked = 0, fishLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.CookedFood)
                        cooked += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Fish)
                        fishLeft += bag.Items[i].Amount;
                }
                if (cooked < 1 || fishLeft != 0)
                    throw new InvalidOperationException("생선 1 → 요리음식 1이어야 합니다.");

                bag.Add(ItemCatalog.Cloth, 1);
                var wrapOk = world.TryCraft(body, station, "cooked_wrap");
                if (!wrapOk.Applied)
                    throw new InvalidOperationException("천 요리 실패: " + wrapOk.FailReason);

                var locked = new SkillSet();
                locked.SetLock(SkillId.Cooking, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Cooking, 10f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Cooking)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 요리는 오르면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertAlchemySlice()
        {
            if (StatSet.PrimaryOf(SkillId.Alchemy) != StatId.Int)
                throw new InvalidOperationException("연금술 Primary는 INT이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Alchemy) != "연금술" || SkillTitles.JobOf(SkillId.Alchemy) != "연금술사")
                throw new InvalidOperationException("연금술 스킬명/직업명이 기획과 같아야 합니다.");
            var rec = CraftRecipes.Find("health_potion");
            if (rec == null || rec.Ingredient != ItemCatalog.Cloth || rec.Output != ItemCatalog.HealthPotion
                || rec.Skill != SkillId.Alchemy || rec.Count != 1)
                throw new InvalidOperationException("천 1 → 회복물약 레시피가 있어야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.HealthPotion) <= 0 || ItemCatalog.WeightOf(ItemCatalog.HealthPotion) <= 0f)
                throw new InvalidOperationException("회복물약 무게/가격이 없습니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int intWas = stats.Int;
            SkillGain.TryRaise(gain, SkillId.Alchemy, 10f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("연금술 0.0→0.1이어야 합니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("연금술 상승 시 INT가 올라야 합니다.");

            var created = CharacterCreate.Build("alch-check", "연금", 0, 20, 20, 40,
                new[] { SkillId.Alchemy, SkillId.Tailoring, SkillId.Magery },
                new[] { 50f, 30f, 20f });
            bool hasCloth = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Cloth && created.Inventory[i].Amount >= 1)
                    hasCloth = true;
            }
            if (!hasCloth)
                throw new InvalidOperationException("연금술 시작은 천을 줘야 합니다.");

            var mortar = GameObject.Find("Mortar");
            if (mortar == null)
                throw new InvalidOperationException("마을에 절구(Mortar)가 있어야 합니다.");
            var sceneSt = mortar.GetComponent<CraftStation>();
            if (sceneSt == null || sceneSt.RecipeId != "health_potion")
                throw new InvalidOperationException("절구는 회복물약 CraftStation이어야 합니다.");

            var go = new GameObject("selfcheck-alch");
            GameObject worldGo = null;
            GameObject stGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-alch-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                var bag = go.AddComponent<InventoryBag>();
                stGo = new GameObject("selfcheck-alch-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "health_potion";
                station.DisplayName = "절구";
                var noIng = world.TryCraft(body, station);
                if (noIng.Applied)
                    throw new InvalidOperationException("재료 없이 연금되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("실패한 연금은 스킬을 올리면 안 됩니다.");
                bag.Add(ItemCatalog.Cloth, 1);
                var ok = world.TryCraft(body, station);
                if (!ok.Applied)
                    throw new InvalidOperationException("천 연금 실패: " + ok.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 연금 후 0.1이어야 합니다.");
                int pots = 0, clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.HealthPotion)
                        pots += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (pots < 1 || clothLeft != 0)
                    throw new InvalidOperationException("천 1 → 회복물약 1이어야 합니다.");

                float hpWas = body.Hp;
                body.SetHp(Math.Max(1f, body.MaxHp - 20f));
                var drink = world.TryDrink(body);
                if (!drink.Applied)
                    throw new InvalidOperationException("물약 마시기 실패: " + drink.FailReason);
                if (body.Hp <= hpWas - 20f + 0.01f)
                    throw new InvalidOperationException("물약은 HP를 회복해야 합니다.");
                int potsLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.HealthPotion)
                        potsLeft += bag.Items[i].Amount;
                if (potsLeft != 0)
                    throw new InvalidOperationException("마신 물약은 소모되어야 합니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Alchemy, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Alchemy, 10f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 연금술은 오르면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertHealingResurrect()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Healing) != "치유")
                throw new InvalidOperationException("치유 스킬명을 바꾸면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Healing) != StatId.Dex)
                throw new InvalidOperationException("치유 Primary는 DEX이어야 합니다.");

            var ghostHealer = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                HealerGhost = true,
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (ghostHealer.Applied || ghostHealer.FailReason != "ghost")
                throw new InvalidOperationException("유령 시술자 붕대 부활은 실패해야 합니다.");

            var noGhost = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = false,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (noGhost.Applied || noGhost.FailReason != "not_ghost")
                throw new InvalidOperationException("유령 아닌 대상 붕대 부활은 실패해야 합니다.");

            var notAvatar = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = false,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (notAvatar.Applied || notAvatar.FailReason != "not_ghost")
                throw new InvalidOperationException("아바타가 아닌 Ghost 붕대 부활은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = false,
                Distance = 1f,
                Skills = noBnSkills
            });
            if (noBn.Applied || noBn.FailReason != "no_bandage")
                throw new InvalidOperationException("붕대 없는 부활은 실패해야 합니다.");
            if (Math.Abs(noBnSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("실패한 붕대 부활은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = ItemCatalog.MeleeRange + 1f,
                Range = ItemCatalog.MeleeRange,
                Skills = farSkills
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 붕대 부활은 실패해야 합니다.");
            if (Math.Abs(farSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("사거리 밖 붕대 부활은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = BandageResurrectResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("붕대 부활 Resolve는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 붕대 부활 후 Healing 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("붕대 부활은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("붕대 부활은 수의학을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("붕대 부활 상승 시 DEX가 올라야 합니다.");

            var forceSkills = new SkillSet();
            var forceStats = new StatSet();
            var forced = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = forceSkills,
                Stats = forceStats,
                Force = true
            });
            if (!forced.Applied || Math.Abs(forceSkills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("Force 경로도 Healing 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Healing, SkillLock.Locked);
            var lockedOk = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = locked,
                Force = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 Healing도 부활 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 Healing은 오르면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var healerGo = new GameObject("selfcheck-healrez-healer");
            var ghostGo = new GameObject("selfcheck-healrez-ghost");
            GameObject worldGo = null;
            GameObject stationGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-healrez-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                world.ResetHousePlot();

                healerGo.transform.position = new Vector3(42f, 0f, 0f);
                var healer = healerGo.AddComponent<WorldBody>();
                healer.IsAvatar = true;
                healer.RecalcFromStr(30);
                healer.ResetHp();
                var bag = healerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Bandage, 2);

                ghostGo.transform.position = healerGo.transform.position;
                var ghost = ghostGo.AddComponent<WorldBody>();
                ghost.IsAvatar = true;
                ghost.DisplayName = "유령";
                ghost.RecalcFromStr(30);
                ghost.ResetHp();
                ghost.Ghost = true;
                ghost.SetHp(0f);
                if (!ghost.Ghost)
                    throw new InvalidOperationException("대상은 Ghost여야 합니다.");

                var living = world.TryResurrectBandage(healer, healer);
                if (living.Applied)
                    throw new InvalidOperationException("살아있는 시술자 자신은 붕대 부활되면 안 됩니다.");

                var hit = world.TryResurrectBandage(healer, ghost);
                if (!hit.Applied || ghost.Ghost)
                    throw new InvalidOperationException("서버 붕대 부활 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("서버 붕대 부활 후 Healing 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 붕대 부활은 마법을 올리면 안 됩니다.");
                int left = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        left += bag.Items[i].Amount;
                if (left != 1)
                    throw new InvalidOperationException("성공 붕대 부활은 붕대 1을 소모해야 합니다.");
                if (ghost.Hp <= 0f || ghost.Ghost)
                    throw new InvalidOperationException("부활 후 HP가 회복되어야 합니다.");
                if (string.IsNullOrEmpty(world.LastHealRezMessage) || world.LastHealRezMessage.IndexOf("부활", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("붕대 부활 메시지가 있어야 합니다.");

                // TryHeal routes to resurrect when target ghost
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                var viaHeal = world.TryHeal(healer, ghost);
                if (!viaHeal.Applied || ghost.Ghost)
                    throw new InvalidOperationException("TryHeal(ghost)는 붕대 부활이어야 합니다: " + viaHeal.FailReason);

                // HealerStation path stays
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                stationGo = new GameObject("selfcheck-healrez-station");
                stationGo.transform.position = ghostGo.transform.position;
                var station = stationGo.AddComponent<HealerStation>();
                var stationRez = world.TryResurrect(ghost, station);
                if (!stationRez.Applied || ghost.Ghost)
                    throw new InvalidOperationException("HealerStation TryResurrect가 유지되어야 합니다: " + stationRez.FailReason);

                // fail: too far
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                bag.Add(ItemCatalog.Bandage, 1);
                ghostGo.transform.position = healerGo.transform.position + new Vector3(ItemCatalog.MeleeRange + 2f, 0f, 0f);
                var ranged = world.TryResurrectBandage(healer, ghost);
                if (ranged.Applied)
                    throw new InvalidOperationException("먼 거리 붕대 부활은 실패해야 합니다.");

                // fail: healer ghost
                healer.Ghost = true;
                ghostGo.transform.position = healerGo.transform.position;
                var hg = world.TryResurrectBandage(healer, ghost);
                if (hg.Applied)
                    throw new InvalidOperationException("유령 시술자 서버 부활은 실패해야 합니다.");
                healer.Ghost = false;
                healer.ResetHp();

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("붕대 부활 후 던전3가 생기면 안 됩니다.");
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                UnityEngine.Object.DestroyImmediate(healerGo);
                UnityEngine.Object.DestroyImmediate(ghostGo);
                if (stationGo != null)
                    UnityEngine.Object.DestroyImmediate(stationGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertBandageDetox()
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

            if (SkillNames.KoreanOf(SkillId.Healing) != "치유")
                throw new InvalidOperationException("치유 스킬명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghostFail = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                HealerGhost = false,
                TargetGhost = true,
                TargetAlive = false,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = ghostSkills
            });
            if (ghostFail.Applied || ghostFail.FailReason != "ghost")
                throw new InvalidOperationException("유령 대상 붕대 해독은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("실패한 해독은 Healing을 올리면 안 됩니다.");

            var healerGhost = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                HealerGhost = true,
                TargetGhost = false,
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (healerGhost.Applied || healerGhost.FailReason != "ghost")
                throw new InvalidOperationException("유령 시술자 붕대 해독은 실패해야 합니다.");

            var noPoisonSkills = new SkillSet();
            var noPoison = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = 0,
                HasBandage = true,
                Distance = 1f,
                Skills = noPoisonSkills
            });
            if (noPoison.Applied || noPoison.FailReason != "no_poison")
                throw new InvalidOperationException("독 없는 붕대 해독은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = false,
                Distance = 1f,
                Skills = noBnSkills
            });
            if (noBn.Applied || noBn.FailReason != "no_bandage")
                throw new InvalidOperationException("붕대 없는 해독은 실패해야 합니다.");
            if (Math.Abs(noBnSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("붕대 없는 해독은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = ItemCatalog.MeleeRange + 1f,
                Range = ItemCatalog.MeleeRange,
                Skills = farSkills
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 붕대 해독은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = BandageCurePoisonResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("붕대 해독 Resolve는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 붕대 해독 후 Healing 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("붕대 해독은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("붕대 해독은 수의학을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("붕대 해독 상승 시 DEX가 올라야 합니다.");

            var forceSkills = new SkillSet();
            var forced = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = forceSkills,
                Stats = new StatSet(),
                Force = true
            });
            if (!forced.Applied || Math.Abs(forceSkills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("Force 경로도 Healing 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Healing, SkillLock.Locked);
            var lockedOk = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = locked,
                Force = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 Healing도 해독 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 Healing은 오르면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var healerGo = new GameObject("selfcheck-detox-healer");
            var allyGo = new GameObject("selfcheck-detox-ally");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-detox-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                world.ResetHousePlot();

                healerGo.transform.position = new Vector3(42f, 0f, 0f);
                var healer = healerGo.AddComponent<WorldBody>();
                healer.IsAvatar = true;
                healer.DisplayName = "치유사";
                healer.RecalcFromStr(30);
                healer.ResetHp();
                var bag = healerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Bandage, 3);

                // self cure
                healer.PoisonTicks = PoisoningResolve.TickCount;
                healer.NextPoisonAt = UnityEngine.Time.time + 10f;
                var selfHit = world.TryCurePoison(healer, healer);
                if (!selfHit.Applied)
                    throw new InvalidOperationException("자가 붕대 해독 실패: " + selfHit.FailReason);
                if (healer.PoisonTicks != 0 || healer.NextPoisonAt != 0f)
                    throw new InvalidOperationException("해독 후 독 틱이 남아 있으면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("서버 해독 후 Healing 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("붕대 해독은 Magery를 올리면 안 됩니다.");
                int left = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        left += bag.Items[i].Amount;
                if (left != 2)
                    throw new InvalidOperationException("성공 해독은 붕대 1을 소모해야 합니다.");
                if (string.IsNullOrEmpty(world.LastCurePoisonMessage) || world.LastCurePoisonMessage.IndexOf("해독", System.StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("해독 메시지가 있어야 합니다.");

                // no poison fail
                var none = world.TryCurePoison(healer, healer);
                if (none.Applied || none.FailReason != "no_poison")
                    throw new InvalidOperationException("독 없을 때 TryCurePoison은 no_poison이어야 합니다.");

                // ally via TryHeal route
                allyGo.transform.position = healerGo.transform.position;
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.DisplayName = "동료";
                ally.RecalcFromStr(30);
                ally.ResetHp();
                ally.PoisonTicks = PoisoningResolve.TickCount;
                ally.NextPoisonAt = UnityEngine.Time.time + 5f;
                float healBefore = world.SkillsOf(healer).Get(SkillId.Healing);
                var viaHeal = world.TryHeal(healer, ally);
                if (!viaHeal.Applied)
                    throw new InvalidOperationException("TryHeal(poisoned)는 해독이어야 합니다: " + viaHeal.FailReason);
                if (ally.PoisonTicks != 0 || ally.NextPoisonAt != 0f)
                    throw new InvalidOperationException("TryHeal 해독 후 독이 남아 있으면 안 됩니다.");
                if (world.SkillsOf(healer).Get(SkillId.Healing) < healBefore)
                    throw new InvalidOperationException("TryHeal 해독 경로도 Healing이 유지/상승해야 합니다.");

                // range fail
                ally.PoisonTicks = PoisoningResolve.TickCount;
                allyGo.transform.position = healerGo.transform.position + new Vector3(ItemCatalog.MeleeRange + 2f, 0f, 0f);
                var ranged = world.TryCurePoison(healer, ally);
                if (ranged.Applied || ranged.FailReason != "range")
                    throw new InvalidOperationException("먼 거리 해독은 range 실패여야 합니다.");

                // no bandage
                allyGo.transform.position = healerGo.transform.position;
                ally.PoisonTicks = PoisoningResolve.TickCount;
                for (int i = bag.Items.Count - 1; i >= 0; i--)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        bag.Items.RemoveAt(i);
                var noBag = world.TryCurePoison(healer, ally);
                if (noBag.Applied || noBag.FailReason != "no_bandage")
                    throw new InvalidOperationException("붕대 없으면 no_bandage여야 합니다.");
                if (ally.PoisonTicks != PoisoningResolve.TickCount)
                    throw new InvalidOperationException("실패한 해독은 독을 지우면 안 됩니다.");

                // ghost target
                bag.Add(ItemCatalog.Bandage, 1);
                ally.Ghost = true;
                ally.SetHp(0f);
                ally.Ghost = true;
                ally.PoisonTicks = PoisoningResolve.TickCount;
                var gh = world.TryCurePoison(healer, ally);
                if (gh.Applied || gh.FailReason != "ghost")
                    throw new InvalidOperationException("유령 대상 서버 해독은 ghost 실패여야 합니다.");

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("붕대 해독 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                UnityEngine.Object.DestroyImmediate(healerGo);
                UnityEngine.Object.DestroyImmediate(allyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

    }
}
