using System.Collections;
using System.IO;
using System.Text;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Client
{
    public sealed class DualClientProbe : MonoBehaviour
    {
        NetworkManager manager;

        void Start()
        {
            if (!Cli.Has("-ulon-check"))
            {
                enabled = false;
                return;
            }
            manager = GetComponent<NetworkManager>();
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            string role = Cli.Get("-ulon-role", "observer");
            string outPath = Cli.Get("-ulon-out", "");
            float deadline = Time.realtimeSinceStartup + 20f;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (manager != null && manager.IsClientStarted)
                    break;
                yield return null;
            }
            if (manager == null || !manager.IsClientStarted)
            {
                Write(outPath, false, "no_connect", 0, false, 0f, 0f);
                Quit();
                yield break;
            }

            NetAvatar mine = null;
            NetMob mob = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                mine = FindOwned();
                mob = FindSceneMob();
                if (mine != null && mob != null && CountAvatars() >= 2)
                    break;
                yield return null;
            }

            int avatars = CountAvatars();
            bool hasMob = mob != null;
            if (mine == null || !hasMob || avatars < 2)
            {
                Write(outPath, manager.IsClientStarted, "no_peer_or_mob", avatars, hasMob,
                    hasMob ? mob.GetComponent<WorldBody>().Hp : 0f, 0f);
                Quit();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            var body = mob.GetComponent<WorldBody>();
            float hpBefore = body.Hp;
            if (role == "attacker")
            {
                yield return new WaitForSeconds(0.25f);
                Vector3 atkPos = mob.transform.position + new Vector3(1.2f, 0f, 0f);
                WarpNextTo(mine.transform, mob.transform.position);
                mine.RpcSetPos(atkPos);
                yield return new WaitForSeconds(0.15f);
                mine.RpcRequestAttack(mob.NetworkObject);
                yield return new WaitForSeconds(0.4f);
            }
            else
            {
                while (Time.realtimeSinceStartup < deadline && body.Hp >= hpBefore - 0.01f)
                    yield return null;
            }

            float hpAfter = body.Hp;
            bool ok = avatars >= 2 && hasMob && hpAfter < hpBefore;
            Write(outPath, true, ok ? "ok" : "hp_unchanged", avatars, true, hpBefore, hpAfter);
            Quit();
        }

        static NetMob FindSceneMob()
        {
            var skel = GameObject.Find("Skeleton");
            if (skel != null)
            {
                var n = skel.GetComponent<NetMob>();
                if (n != null)
                    return n;
            }
            return FindFirstObjectByType<NetMob>();
        }

        static NetAvatar FindOwned()
        {
            var all = FindObjectsByType<NetAvatar>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i].IsOwner)
                    return all[i];
            return null;
        }

        static int CountAvatars()
        {
            return FindObjectsByType<NetAvatar>(FindObjectsSortMode.None).Length;
        }

        static void WarpNextTo(Transform actor, Vector3 target)
        {
            var cc = actor.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            actor.position = target + new Vector3(1.2f, 0f, 0f);
            if (cc != null)
                cc.enabled = true;
        }

        static void Write(string path, bool connected, string status, int avatars, bool mob, float hpBefore, float hpAfter)
        {
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Application.persistentDataPath, "ulon-check.json");
            string json = "{\"connected\":" + (connected ? "true" : "false")
                          + ",\"status\":\"" + status + "\""
                          + ",\"avatars\":" + avatars
                          + ",\"mob\":" + (mob ? "true" : "false")
                          + ",\"hpBefore\":" + hpBefore.ToString("0.##")
                          + ",\"hpAfter\":" + hpAfter.ToString("0.##")
                          + "}";
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, json, new UTF8Encoding(false));
            Debug.Log("[Ulon] check " + json + " -> " + path);
        }

        static void Quit()
        {
            if (Application.isEditor)
                return;
            Application.Quit();
        }
    }
}
