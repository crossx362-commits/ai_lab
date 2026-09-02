using System.IO;
using System.Text;
using Ulon.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    public static class OutfitSlice
    {
        const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        const string MageFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Mage.fbx";
        const string KnightFbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Knight.fbx";

        [MenuItem("Ulon/Swap Player Head To Mage")]
        public static void SwapPlayerHeadToMage()
        {
            EditorSceneManager.OpenScene(ScenePath);
            ConfigureMage();
            var player = GameObject.Find("Player");
            if (player == null)
                throw new System.InvalidOperationException("Player 없음");
            Transform visual = player.transform.Find("Visual") ?? player.transform;
            string before = MeshSummary(visual);

            var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MageFbx);
            if (magePrefab == null)
                throw new System.InvalidOperationException("Mage.fbx 없음");
            var donor = Object.Instantiate(magePrefab);
            donor.hideFlags = HideFlags.HideAndDontSave;
            int n = OutfitSwap.ApplyMeshes(visual, donor.transform, "Head");
            OutfitSwap.HideGear(visual, "Helmet");
            Object.DestroyImmediate(donor);

            var anim = visual.GetComponent<Animator>();
            if (anim != null)
            {
                anim.Rebind();
                anim.Update(0f);
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            string after = MeshSummary(visual);
            string report = "swapped=" + n + "\nbefore=" + before + "\nafter=" + after
                            + "\nsharedCtrl=" + (anim != null && anim.runtimeAnimatorController != null
                                ? anim.runtimeAnimatorController.name : "null")
                            + "\navatarHuman=" + (anim != null && anim.avatar != null && anim.avatar.isHuman);
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/outfit-swap.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllText(outPath, report, Encoding.UTF8);
            Debug.Log("[Ulon] " + report);
            if (n < 1)
                throw new System.InvalidOperationException("헤드 메시 교체 실패");
        }

        public static void BatchSwap() => SwapPlayerHeadToMage();

        static void ConfigureMage()
        {
            var importer = AssetImporter.GetAtPath(MageFbx) as ModelImporter;
            if (importer == null)
                throw new System.InvalidOperationException("Mage importer 없음");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        static string MeshSummary(Transform root)
        {
            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var sb = new StringBuilder();
            for (int i = 0; i < smrs.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(smrs[i].name).Append('=')
                    .Append(smrs[i].sharedMesh != null ? smrs[i].sharedMesh.name : "null");
            }
            return sb.ToString();
        }
    }
}
