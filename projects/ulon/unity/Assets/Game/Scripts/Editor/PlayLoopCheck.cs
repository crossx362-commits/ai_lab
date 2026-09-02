using System;
using System.IO;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ulon.Editor
{
    public static class PlayLoopCheck
    {
        const string Key = "playloop-verify";
        static int phase;
        static string fail;
        static string report;

        public static void Abort()
        {
            EditorApplication.playModeStateChanged -= OnPlay;
            fail = "aborted";
        }

        [MenuItem("Ulon/Run Play Loop")]
        public static void Run()
        {
            fail = null;
            report = "";
            phase = 1;
            PlayerPrefs.SetString("ulon.account", Key);
            PlayerPrefs.Save();
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.isPlaying = true;
        }

        static void OnPlay(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ExecutePhase;
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (fail != null)
                {
                    EditorApplication.playModeStateChanged -= OnPlay;
                    WriteResult("FAIL — " + fail + " | " + report);
                    Debug.LogError("[Ulon] Play loop FAIL — " + fail + " | " + report);
                }
                else if (phase == 1)
                {
                    phase = 2;
                    EditorApplication.delayCall += () => { EditorApplication.isPlaying = true; };
                }
                else
                {
                    EditorApplication.playModeStateChanged -= OnPlay;
                    WriteResult("PASS — " + report);
                    Debug.Log("[Ulon] Play loop PASS — " + report);
                }
            }
        }

        static void ExecutePhase()
        {
            try
            {
                if (phase == 1)
                    Phase1();
                else
                    Phase2();
            }
            catch (Exception e)
            {
                fail = e.Message;
            }
            EditorApplication.isPlaying = false;
        }

        public static void Phase1()
        {
            var world = OfflineWorld.Instance;
            if (world == null)
                throw new InvalidOperationException("OfflineWorld 없음");
            var player = world.Player;
            if (player == null)
                player = GameObject.Find("Player") ? GameObject.Find("Player").GetComponent<WorldBody>() : null;
            if (player == null)
                throw new InvalidOperationException("Player 없음");
            world.SetLocalPlayer(player);

            var skeletonGo = FindRoot("Skeleton");
            var companionGo = FindRoot("Companion");
            var skeleton = skeletonGo != null ? skeletonGo.GetComponent<WorldBody>() : null;
            var vein = OfflineWorld.FindNode("IronVein");
            var forge = OfflineWorld.FindStation("Forge");
            var companion = companionGo != null ? companionGo.GetComponent<WorldBody>() : null;
            if (skeleton == null || vein == null || forge == null || companion == null)
                throw new InvalidOperationException("씬 오브젝트 부족 skel/vein/forge/companion");

            var anim = player.GetComponentInChildren<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null)
                throw new InvalidOperationException("Animator/컨트롤러 없음");
            anim.Update(0.1f);
            if (anim.GetCurrentAnimatorStateInfo(0).length <= 0f)
                throw new InvalidOperationException("애니 상태가 비어 있음");

            Warp(player, skeleton.transform.position + new Vector3(-1.2f, 0f, 0f));
            var atk = world.TryAttack(player, skeleton);
            if (!atk.Applied)
                throw new InvalidOperationException("공격 실패: " + atk.FailReason);
            if (skeleton.Hp >= 30f)
                throw new InvalidOperationException("스켈레톤 HP가 안 깎임");

            Warp(player, vein.transform.position + new Vector3(-1.2f, 0f, 0f));
            var g1 = world.TryGather(player, vein);
            var g2 = world.TryGather(player, vein);
            if (!g1.Applied || !g2.Applied)
                throw new InvalidOperationException("채광 실패: " + g1.FailReason + "/" + g2.FailReason);

            Warp(player, forge.transform.position + new Vector3(1.2f, 0f, 0f));
            var craft = world.TryCraft(player, forge);
            if (!craft.Applied)
                throw new InvalidOperationException("제작 실패: " + craft.FailReason);

            Warp(player, companion.transform.position + new Vector3(1.2f, 0f, 0f));
            var begin = world.TryTrade(player, companion);
            if (!begin.Applied)
                throw new InvalidOperationException("거래 시작 실패: " + begin.FailReason);
            world.SetTradeOffer(player, "iron_sword");
            world.SetTradeOffer(companion, "");
            world.ConfirmTrade(player);
            var done = world.ConfirmTrade(companion);
            if (!done.Applied || !done.Hit)
                throw new InvalidOperationException("거래 완료 실패: " + done.FailReason);
            var bagP = player.GetComponent<InventoryBag>();
            var bagC = companion.GetComponent<InventoryBag>();
            if (Count(bagP, "iron_sword") != 0 || Count(bagC, "iron_sword") != 1)
                throw new InvalidOperationException("거래 후 가방이 잘못됨");

            var oak = OfflineWorld.FindNode("OakTree");
            var banker = OfflineWorld.FindBank("Banker");
            if (oak == null || banker == null)
                throw new InvalidOperationException("씬 오브젝트 부족 oak/banker");
            Warp(player, oak.transform.position + new Vector3(-1.2f, 0f, 0f));
            var wood = world.TryGather(player, oak);
            var wood2 = world.TryGather(player, oak);
            var wood3 = world.TryGather(player, oak);
            if (!wood.Applied || !wood2.Applied || !wood3.Applied)
                throw new InvalidOperationException("벌목 실패: " + wood.FailReason);
            var carpenter = OfflineWorld.FindStation("Carpenter");
            if (carpenter == null)
                throw new InvalidOperationException("씬 오브젝트 부족 carpenter");
            Warp(player, carpenter.transform.position + new Vector3(1.2f, 0f, 0f));
            var club = world.TryCraft(player, carpenter);
            if (!club.Applied)
                throw new InvalidOperationException("목공 실패: " + club.FailReason);
            if (Count(bagP, ItemCatalog.WoodenClub) < 1)
                throw new InvalidOperationException("나무곤봉이 안 만들어짐");
            Warp(player, banker.transform.position + new Vector3(1.2f, 0f, 0f));
            var banked = world.TryBank(player, banker);
            if (!banked.Applied)
                throw new InvalidOperationException("은행 맡기기 실패: " + banked.FailReason);
            var vault = player.GetComponent<BankVault>();
            if (Count(bagP, "wood") != 0 || vault == null || CountBank(vault, "wood") < 1)
                throw new InvalidOperationException("은행 맡긴 뒤 가방/금고가 잘못됨");

            var snap = CharacterBinder.Capture(Key, player, world.SkillsOf(player), world.StatsOf(player));
            CharacterStore.Save(snap);
            PlayerPrefs.SetString("ulon.account", Key);
            PlayerPrefs.Save();

            report = "atkHp=" + skeleton.Hp
                     + " mine=" + world.SkillsOf(player).Get(SkillId.Mining).ToString("0.0")
                     + " lumber=" + world.SkillsOf(player).Get(SkillId.Lumberjacking).ToString("0.0")
                     + " carp=" + world.SkillsOf(player).Get(SkillId.Carpentry).ToString("0.0")
                     + " smith=" + world.SkillsOf(player).Get(SkillId.Blacksmithing).ToString("0.0")
                     + " sword=" + world.SkillsOf(player).Get(SkillId.Swordsmanship).ToString("0.0")
                     + " bankWood=" + CountBank(vault, "wood")
                     + " anim=" + anim.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");
        }

        public static void Phase2()
        {
            var loaded = CharacterStore.Load(Key);
            if (loaded == null)
                throw new InvalidOperationException("재접속 로드 실패");
            bool mine = false, smith = false, sword = false;
            if (loaded.Skills != null)
            {
                for (int i = 0; i < loaded.Skills.Length; i++)
                {
                    if (loaded.Skills[i].Id == (int)SkillId.Mining && loaded.Skills[i].Value >= 0.19f)
                        mine = true;
                    if (loaded.Skills[i].Id == (int)SkillId.Blacksmithing && loaded.Skills[i].Value >= 0.09f)
                        smith = true;
                    if (loaded.Skills[i].Id == (int)SkillId.Swordsmanship && loaded.Skills[i].Value >= 0.09f)
                        sword = true;
                }
            }
            if (!mine || !smith || !sword)
                throw new InvalidOperationException("재접속 스킬 복원 실패 mine=" + mine + " smith=" + smith + " sword=" + sword);
            bool bankWood = false;
            if (loaded.Bank != null)
            {
                for (int i = 0; i < loaded.Bank.Length; i++)
                    if (loaded.Bank[i].TemplateId == "wood" && loaded.Bank[i].Amount >= 1)
                        bankWood = true;
            }
            if (!bankWood)
                throw new InvalidOperationException("재접속 은행 복원 실패");

            var world = OfflineWorld.Instance;
            if (world != null && world.Player != null)
            {
                if (world.SkillsOf(world.Player).Get(SkillId.Mining) < 0.19f)
                    throw new InvalidOperationException("씬 플레이어 채광이 복원되지 않음");
            }
            report += " | reload mine/smith/sword OK";
        }

        static void Warp(WorldBody body, Vector3 pos)
        {
            pos.y = body.transform.position.y;
            var cc = body.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            body.transform.position = pos;
            if (cc != null)
                cc.enabled = true;
        }

        static GameObject FindRoot(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                return go;
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != name)
                    continue;
                var nob = roots[i].GetComponent<FishNet.Object.NetworkObject>();
                if (nob != null && !nob.IsSpawned)
                    nob.enabled = false;
                roots[i].SetActive(true);
                return roots[i];
            }
            return null;
        }

        static void WriteResult(string line)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/playloop.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, line);
        }

        static int Count(InventoryBag bag, string template)
        {
            if (bag == null)
                return 0;
            int n = 0;
            for (int i = 0; i < bag.Items.Count; i++)
                if (bag.Items[i].TemplateId == template)
                    n += bag.Items[i].Amount;
            return n;
        }

        static int CountBank(BankVault vault, string template)
        {
            if (vault == null)
                return 0;
            int n = 0;
            for (int i = 0; i < vault.Items.Count; i++)
                if (vault.Items[i].TemplateId == template)
                    n += vault.Items[i].Amount;
            return n;
        }
    }
}
