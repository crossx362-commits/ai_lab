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
    }
}
