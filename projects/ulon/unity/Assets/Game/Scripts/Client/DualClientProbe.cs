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
            float deadline = Time.realtimeSinceStartup + 40f;   // 파티 단계가 붙어 예산을 늘렸다(20s로는 관찰자가 못 끝냈다)

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

            // **파티가 온라인에서 만들어지는가**(검수 랩 2026-09-08). 소스 게이트로는 「부르도록
            // 적혀 있다」까지만 보인다 — 실제로 두 클라이언트의 파티 상태가 **같아지는지**를 여기서 잰다.
            // 초대 대상은 HUD와 **같은 함수**로 고른다(`OfflineWorld.NearestInvitee`) — 자가 둘이면
            // 「버튼이 고른 것」과 「검사가 고른 것」이 갈린다.
            yield return Party(role, mine, deadline);

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

            // **효과가 이 클라이언트에 도착했는가** — 소스 게이트로는 알 수 없는 부분이다(검수 지시).
            // HP가 바뀐 뒤에도 방송은 몇 프레임 늦게 올 수 있으므로 잠깐 더 기다린다.
            float effectDeadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < effectDeadline && ActionVfx.Played == 0)
                yield return null;

            float hpAfter = body.Hp;
            bool ok = avatars >= 2 && hasMob && hpAfter < hpBefore && ActionVfx.Played > 0 && ActionSfx.Played > 0;
            string status = ok ? "ok"
                : hpAfter >= hpBefore ? "hp_unchanged"
                : "effect_missing";   // 피해는 갔는데 불티·소리가 이 화면엔 안 왔다
            Write(outPath, true, status, avatars, true, hpBefore, hpAfter);
            Quit();
        }

        /// <summary>이 클라이언트가 본 파티 상태 — json으로 나가고, 두 클라가 같아야 통과다.</summary>
        static bool partyOpen;
        static string partyLeader = "";
        static int partyMembers;

        /// <summary>
        /// A가 B에게 다가가 **HUD와 같은 경로로** 초대하고, B가 수락한다.
        /// `-ulon-nc-party`를 주면 초대 RPC만 끊는다(네거티브 컨트롤) — 그때는 파티가 안 생겨야 한다.
        /// </summary>
        static IEnumerator Party(string role, NetAvatar mine, float deadline)
        {
            bool ncCutInvite = Cli.Get("-ulon-nc-party", "") != "";
            var myBody = mine.GetComponent<WorldBody>();
            if (role == "attacker")
            {
                NetAvatar other = null;
                var all = FindObjectsByType<NetAvatar>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                    if (!all[i].IsOwner)
                        other = all[i];
                if (other != null)
                {
                    // **사거리 안으로 들어갈 때까지 붙인다.** 한 번 순간이동시키고 0.4초 기다렸더니
                    // 중력으로 6m 아래로 떨어져(y −6.4 vs −0.4) 초대가 「사거리 밖」이 됐다 —
                    // 자리를 한 번 정해 놓고 시간이 지나면 그 자리는 이미 옛말이다.
                    float inviteDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 6f);
                    while (Time.realtimeSinceStartup < inviteDeadline && !PartyView.Open)
                    {
                        var spot = other.transform.position + new Vector3(1.2f, 0f, 0f);
                        WarpNextTo(mine.transform, other.transform.position);
                        mine.RpcSetPos(spot);
                        yield return new WaitForSeconds(0.25f);
                        var pick = OfflineWorld.NearestInvitee(myBody, Ulon.Shared.PartyResolve.InviteRange);
                        var nob = pick != null ? pick.GetComponent<FishNet.Object.NetworkObject>() : null;
                        if (ncCutInvite)
                        {
                            Debug.Log("[Ulon] 파티 초대 안 보냄 — NC(초대 RPC 끊음)");
                            break;
                        }
                        if (nob == null)
                            continue;
                        Debug.Log("[Ulon] 내 상태 — HP " + myBody.Hp.ToString("0") + "/" + myBody.MaxHp.ToString("0") +
                                  " · 유령 " + myBody.Ghost + " · y " + myBody.transform.position.y.ToString("0.0"));
                        Debug.Log("[Ulon] 파티 초대 보냄 — " + pick.name + " " +
                                  Vector3.Distance(myBody.transform.position, pick.transform.position).ToString("0.0") + "m");
                        mine.RpcPartyInvite(nob);
                        yield return new WaitForSeconds(0.35f);
                    }
                }
            }
            else
            {
                float acceptDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 6f);
                while (Time.realtimeSinceStartup < acceptDeadline && !PartyView.PendingMe)
                    yield return null;
                if (PartyView.PendingMe)
                    mine.RpcPartyAccept();
            }
            float partyDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 4f);
            while (Time.realtimeSinceStartup < partyDeadline && RosterCount() < 2)
                yield return null;
            partyOpen = PartyView.Open;
            partyLeader = PartyView.Leader ?? "";
            partyMembers = RosterCount();
            Debug.Log("[Ulon] 파티 상태(" + role + ") — open " + partyOpen + " · 대장 " + partyLeader +
                      " · 인원 " + partyMembers);
        }

        /// <summary>명부의 줄 수 = 파티 인원(대장 한 줄 + 파티원 줄들).</summary>
        static int RosterCount()
        {
            if (!PartyView.Open || string.IsNullOrEmpty(PartyView.Roster))
                return 0;
            return PartyView.Roster.Split('\n').Length;
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
            string json = "{\"vfx\":" + ActionVfx.Played + ",\"sfx\":" + ActionSfx.Played
                          + ",\"connected\":" + (connected ? "true" : "false")
                          + ",\"status\":\"" + status + "\""
                          + ",\"avatars\":" + avatars
                          + ",\"mob\":" + (mob ? "true" : "false")
                          + ",\"hpBefore\":" + hpBefore.ToString("0.##")
                          + ",\"hpAfter\":" + hpAfter.ToString("0.##")
                          + ",\"partyOpen\":" + (partyOpen ? "true" : "false")
                          + ",\"partyLeader\":\"" + partyLeader.Replace("\"", "") + "\""
                          + ",\"partyMembers\":" + partyMembers
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
