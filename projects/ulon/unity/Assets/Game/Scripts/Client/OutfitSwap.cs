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
            // **여기 씬 이름 고정은 구멍이 아니다**(검수 판정 2026-09-08). 네트워크에서는 동료
            // 오브젝트 자체가 없으므로 도너가 없는 것이 맞다 — 파티·길드 초대처럼 「할 수 있어야 하는데
            // 못 하는」 경우가 아니라, 애초에 대상이 없는 경우다. 다음 사람이 같은 패턴이라고
            // 오해하고 고치지 않도록 남긴다.
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
