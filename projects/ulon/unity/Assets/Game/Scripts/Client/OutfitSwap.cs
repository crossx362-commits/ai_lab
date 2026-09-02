using UnityEngine;

namespace Ulon.Client
{
    public static class OutfitSwap
    {
        public static int ApplyMeshes(Transform host, Transform donor, params string[] keys)
        {
            if (host == null || donor == null)
                return 0;
            var hostSmr = host.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var donorSmr = donor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int n = 0;
            for (int i = 0; i < hostSmr.Length; i++)
            {
                string key = PartKey(hostSmr[i].name);
                if (keys != null && keys.Length > 0 && !HasKey(keys, key))
                    continue;
                SkinnedMeshRenderer src = FindByKey(donorSmr, key);
                if (src == null || src.sharedMesh == null)
                    continue;
                hostSmr[i].sharedMesh = src.sharedMesh;
                hostSmr[i].sharedMaterials = src.sharedMaterials;
                n++;
            }
            return n;
        }

        public static void ApplyLook(Transform root, int appearance)
        {
            if (root == null)
                return;
            Transform visual = root.Find("Visual") != null ? root.Find("Visual") : root;
            if (appearance == 1)
            {
                HideGear(visual, "Helmet");
                return;
            }
            if (appearance != 2)
                return;
            var companion = GameObject.Find("Companion");
            if (companion == null)
                return;
            Transform donor = companion.transform.Find("Visual") != null ? companion.transform.Find("Visual") : companion.transform;
            ApplyMeshes(visual, donor);
        }

        public static void HideGear(Transform root, params string[] names)
        {
            if (root == null)
                return;
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                for (int n = 0; n < names.Length; n++)
                    if (ts[i].name.IndexOf(names[n], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        ts[i].gameObject.SetActive(false);
            }
        }

        public static string PartKey(string name)
        {
            int i = name.LastIndexOf('_');
            return i < 0 ? name : name.Substring(i + 1);
        }

        static bool HasKey(string[] keys, string key)
        {
            for (int i = 0; i < keys.Length; i++)
                if (string.Equals(keys[i], key, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static SkinnedMeshRenderer FindByKey(SkinnedMeshRenderer[] list, string key)
        {
            for (int i = 0; i < list.Length; i++)
                if (string.Equals(PartKey(list[i].name), key, System.StringComparison.OrdinalIgnoreCase))
                    return list[i];
            return null;
        }
    }
}
