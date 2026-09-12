using System;
using UnityEditor;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// INBOX: 씬은 RAW FBX가 아니라 Env 프리팹이고, 등불·횃불·분수에는 이펙트가 붙어 있어야 한다.
    /// 화덕 불은 별도 자(`AssertCampfireHasFire`)가 잰다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertNoRawEnvRoots()
        {
            int raw = 0;
            string sample = "";
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i].gameObject;
                if (go.name == "Campfire")
                    continue; // 화덕은 등불 메시+돌+불꽃으로 조립. RAW 뿌리를 바꾸면 불을 잃는다.
                var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                if (outer != null && outer != go)
                    continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (string.IsNullOrEmpty(path) || !VisualSliceBuilder.IsModelPath(path))
                    continue;
                if (path.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                raw++;
                if (sample.Length == 0)
                    sample = go.name + " → " + path;
            }
            Debug.Log("[Ulon] 씬 RAW 모델 뿌리 " + raw + "개" + (raw > 0 ? " 예: " + sample : ""));
            if (raw > 0)
                throw new InvalidOperationException(
                    "씬에 RAW 모델이 그대로 붙어 있습니다(" + raw + "개, 예: " + sample +
                    ") — FBX/OBJ를 붙이지 말고 Game/Prefabs 아래 프리팹으로 놓는다.");
        }

        static void AssertEnvPropVfx()
        {
            int lanterns = 0, lanternOk = 0;
            int torches = 0, torchOk = 0;
            int fountains = 0, fountainOk = 0;
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < all.Length; i++)
            {
                var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(all[i].gameObject);
                var go = outer != null ? outer : all[i].gameObject;
                if (!seen.Add(go.GetInstanceID()))
                    continue;
                if (go.name == "Campfire")
                    continue;
                string asset = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                string kind = EnvEffectKind(go, asset);
                if (kind == null)
                    continue;
                bool lit = HasLoopingPropParticles(go);
                if (kind == "lantern")
                {
                    lanterns++;
                    if (lit) lanternOk++;
                }
                else if (kind == "torch")
                {
                    torches++;
                    if (lit) torchOk++;
                }
                else if (kind == "fountain")
                {
                    fountains++;
                    if (lit) fountainOk++;
                }
            }
            Debug.Log("[Ulon] 소품 이펙트 — 등불 " + lanternOk + "/" + lanterns +
                      " · 횃불 " + torchOk + "/" + torches +
                      " · 분수 " + fountainOk + "/" + fountains);
            if (lanterns < 1)
                throw new InvalidOperationException("등불을 한 개도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            if (lanternOk < lanterns)
                throw new InvalidOperationException("등불 " + (lanterns - lanternOk) +
                    "개에 불꽃이 없습니다 — 메시만 놓은 가로등은 켜진 등으로 안 읽힌다.");
            if (fountains < 1)
                throw new InvalidOperationException("분수(치유사)를 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            if (fountainOk < 1)
                throw new InvalidOperationException("분수에 물 이펙트가 없습니다 — 메시만 있으면 죽은 돌로 읽힌다.");
            if (torches > 0 && torchOk < torches)
                throw new InvalidOperationException("횃불 " + (torches - torchOk) +
                    "개에 불이 없습니다 — 광산 횃불은 프리팹에 불꽃을 붙인다.");
            var litPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Env/LanternLit.prefab");
            if (litPrefab == null || litPrefab.transform.Find(VisualSliceBuilder.PropFlameObject) == null)
                throw new InvalidOperationException("LanternLit.prefab에 불꽃이 없습니다 — 다음에 놓을 등불이 또 메시만 됩니다.");
        }

        /// <summary>네거티브 컨트롤 — 등불 불꽃을 끄면 빨간불이어야 한다.</summary>
        static void AssertEnvPropVfxNegativeControl()
        {
            var flame = FindFirstPropFlame();
            if (flame == null)
                throw new InvalidOperationException("등불 불꽃 오브젝트를 못 찾았습니다 — 네거티브 컨트롤을 돌릴 수 없습니다.");
            flame.gameObject.SetActive(false);
            bool red = false;
            try { AssertEnvPropVfx(); }
            catch (InvalidOperationException) { red = true; }
            flame.gameObject.SetActive(true);
            if (!red)
                throw new InvalidOperationException("소품 이펙트 네거티브 컨트롤 실패 — 불꽃을 껐는데 통과했습니다.");
            Debug.Log("[Ulon] 소품 이펙트 NC — 등불 불을 끄자 빨간불, 되돌린 뒤 통과");
        }

        static Transform FindFirstPropFlame()
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != VisualSliceBuilder.PropFlameObject)
                    continue;
                var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(all[i].gameObject);
                if (outer != null && outer.name == "Campfire")
                    continue;
                return all[i];
            }
            return null;
        }

        static string EnvEffectKind(GameObject go, string asset)
        {
            string n = go.name;
            if (n == "DungeonTorch" || n == "DungeonFill" || n.EndsWith("Light", StringComparison.Ordinal))
                return null;
            if (n.IndexOf("lantern", StringComparison.OrdinalIgnoreCase) >= 0)
                return "lantern";
            if (n.IndexOf("torch", StringComparison.OrdinalIgnoreCase) >= 0)
                return "torch";
            if (n == "Healer" || n.IndexOf("fountain", StringComparison.OrdinalIgnoreCase) >= 0)
                return "fountain";
            if (string.IsNullOrEmpty(asset))
                return null;
            if (asset.IndexOf("Lantern", StringComparison.OrdinalIgnoreCase) >= 0)
                return "lantern";
            if (asset.IndexOf("TorchMounted", StringComparison.OrdinalIgnoreCase) >= 0)
                return "torch";
            if (asset.IndexOf("FountainRound", StringComparison.OrdinalIgnoreCase) >= 0)
                return "fountain";
            return null;
        }

        static bool HasLoopingPropParticles(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(false);
            for (int i = 0; i < systems.Length; i++)
            {
                if (!systems[i].gameObject.activeInHierarchy)
                    continue;
                var main = systems[i].main;
                var em = systems[i].emission;
                if (main.loop && main.playOnAwake && em.enabled && em.rateOverTime.constant > 0f)
                    return true;
            }
            return false;
        }
    }
}
