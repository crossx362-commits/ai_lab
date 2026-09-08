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
            yield return Guild(role, mine, deadline);
            // ── 축 ② 실측: **A가 B를 때렸을 때 B 클라 화면의 숫자가 따라 내려가는가** ──
            // 「줄었다」가 아니라 **전/후 숫자 양쪽**을 남긴다(검수 지시 2026-09-08).
            // 가드존(반지름 16m) 안에서는 아바타끼리 때릴 수 없으므로 둘을 들판으로 옮긴 뒤 친다.
            // **몹 사냥보다 먼저 한다** — 몹과 치고받은 뒤에 하면 때리는 쪽이 이미 유령이라
            // 서버가 `attack fail ghost`로 거절하고, 검사는 판마다 결과가 달라진다(실측).
            yield return PvpHp(role, mine, deadline);
            yield return Economy(role, mine, deadline);
            // 축 ④ — 스킬은 **서버가 올려 준다**. 몹 사냥(아래)이 검술을 올리므로 그 앞뒤로 잰다.
            SkillsSnapshot(mine, out skSwordBefore, out skMiningBefore, out skMageryBefore, out skillsSeen);

            // 살아 있는가·땅 위인가 — 판정은 스크립트가 한다(여기서는 잰 값만 남긴다).
            var myBodyNow = mine.GetComponent<WorldBody>();
            myY = mine.transform.position.y;
            var terrainNow = Terrain.activeTerrain;
            myGroundY = terrainNow != null
                ? terrainNow.SampleHeight(mine.transform.position) + terrainNow.transform.position.y
                : float.NaN;
            myHp = myBodyNow != null ? myBodyNow.Hp : 0f;
            myGhost = myBodyNow != null && myBodyNow.Ghost;
            // **계측 전용(검수 조사 지시 2026-09-08)** — 화면이 읽는 값이 서버와 같은가를 보려면
            // 클라가 실제로 들고 있는 수치를 그대로 내보내야 한다. 고치지 않는다, 재기만 한다.
            myMaxHp = myBodyNow != null ? myBodyNow.MaxHp : 0f;
            myMana = myBodyNow != null ? myBodyNow.Mana : 0f;
            myGold = myBodyNow != null ? myBodyNow.Gold : 0;
            myName = myBodyNow != null ? myBodyNow.DisplayName : "-";
            var bagNow = myBodyNow != null ? myBodyNow.GetComponent<InventoryBag>() : null;
            myBag = bagNow != null ? bagNow.Items.Count : -1;
            mySkill = mine.SwordSkill;
            Debug.Log("[Ulon] 생존 상태(" + role + ") — y " + myY.ToString("0.0") + " · 지면 " +
                      myGroundY.ToString("0.0") + " · HP " + myHp.ToString("0") + " · 유령 " + myGhost);

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

            SkillsSnapshot(mine, out skSwordAfter, out skMiningAfter, out skMageryAfter, out skillsSeen);
            SkillCheat(mine);

            float hpAfter = body.Hp;
            bool ok = avatars >= 2 && hasMob && hpAfter < hpBefore && ActionVfx.Played > 0 && ActionSfx.Played > 0;
            string status = ok ? "ok"
                : hpAfter >= hpBefore ? "hp_unchanged"
                : "effect_missing";   // 피해는 갔는데 불티·소리가 이 화면엔 안 왔다
            Write(outPath, true, status, avatars, true, hpBefore, hpAfter);
            if (role == "attacker")
                StoreWriteExperiment(mine);
            Quit();
        }

        /// <summary>이 클라이언트가 본 길드 상태 — 파티와 같은 방식으로 두 클라가 같아야 통과다.</summary>
        static bool guildOpen;
        static string guildName = "";
        static int guildMembers;

        /// <summary>
        /// **축 ②의 실측** — 가드존 밖에서 A가 B를 때리고, **B 클라가 들고 있는 HP**를 전/후로 남긴다.
        /// 서버가 아는 값이 아니라 **화면이 읽는 값**을 재는 것이 요점이다(그래서 자기 몸에서 읽는다).
        /// </summary>
        static IEnumerator PvpHp(string role, NetAvatar mine, float deadline)
        {
            // 지면(`LandBase`) 위에 세운다 — y=0으로 보내면 땅 10m 아래로 떨어진다(2026-09-08 낙하 사고).
            var field = new Vector3(role == "attacker" ? 26f : 24.6f, Ulon.Shared.WorldTerrain.LandBase, 26f);
            mine.RpcSetPos(field);
            WarpNextTo(mine.transform, field);
            yield return new WaitForSeconds(1.0f);
            var myBody = mine.GetComponent<WorldBody>();
            pvpHpBefore = myBody != null ? myBody.Hp : -1f;
            pvpGhostBefore = myBody != null && myBody.Ghost;
            if (role == "attacker")
            {
                // 상대 아바타를 찾는다 — 내 것이 아닌 NetAvatar.
                NetAvatar other = null;
                var all = Object.FindObjectsByType<NetAvatar>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length && other == null; i++)
                    if (all[i] != mine)
                        other = all[i];
                if (other != null)
                {
                    for (int hit = 0; hit < 26 && Time.realtimeSinceStartup < deadline; hit++)
                    {
                        mine.RpcRequestAttack(other.NetworkObject);
                        yield return new WaitForSeconds(0.35f);
                    }
                }
            }
            else
            {
                float until = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < until && Time.realtimeSinceStartup < deadline)
                    yield return null;
            }
            pvpHpAfter = myBody != null ? myBody.Hp : -1f;
            pvpGhostAfter = myBody != null && myBody.Ghost;
            // **죽음의 결과가 이 화면에 왔는가** — 유령 여부만이 아니라 **시체가 보이는지**와
            // HUD가 그리는 안내 문구 그대로를 남긴다(맞은 쪽·때린 쪽 양쪽 json에서 본다).
            var corpses = Object.FindObjectsByType<Ulon.Server.CorpseNode>(FindObjectsSortMode.None);
            // **몇 구인가가 아니라 「누구의 시체인가」**를 남긴다 — 첫 실측에서 화면에 있던 시체는
            // 무관한 계정(`playloop-verify`)의 옛 시체였고, 개수만 셌다면 **동기화가 0건이어도
            // 초록불**이었다(원장: 무엇이 거기 있는지부터 세라).
            pvpCorpses = corpses.Length;
            var owners = new System.Text.StringBuilder();
            for (int i = 0; i < corpses.Length; i++)
            {
                if (owners.Length > 0) owners.Append('|');
                owners.Append(corpses[i].OwnerId);
            }
            pvpCorpseOwner = owners.ToString();
            pvpRecovery = SliceHud.RecoveryLine(myBody);
            Debug.Log("[Ulon] 축2 실측(" + role + ") — 내 HP " + pvpHpBefore.ToString("0.##") + " → " +
                      pvpHpAfter.ToString("0.##") + " · 유령 " + pvpGhostBefore + " → " + pvpGhostAfter);
        }

        /// <summary>
        /// **축 ③ 실측 — 골드·가방을 누가 정하는가.** 값이 따라오는지(동기화)와
        /// **클라가 제 손으로 정할 수 있는지(치트)**를 한 번에 잰다. 앞의 것만 재면
        /// 「이름만 서버 권위」를 초록불로 넘긴다(검수 조건 1·2).
        /// </summary>
        static IEnumerator Economy(string role, NetAvatar mine, float deadline)
        {
            var body = mine.GetComponent<WorldBody>();
            var bag = mine.GetComponent<InventoryBag>();
            if (body == null || bag == null)
                yield break;
            if (role != "attacker")
            {
                ecoGoldBefore = ecoGoldAfter = body.Gold;
                ecoBagBefore = ecoBagAfter = NetAvatar.BagSignature(bag);
                yield break;
            }
            var vendors = Object.FindObjectsByType<VendorStation>(FindObjectsSortMode.None);
            if (vendors.Length == 0)
                yield break;
            var vendor = vendors[0];
            var spot = vendor.transform.position + new Vector3(1.4f, 0f, 0f);
            mine.RpcSetPos(spot);
            WarpNextTo(mine.transform, vendor.transform.position);
            yield return new WaitForSeconds(1.0f);
            ecoGoldBefore = body.Gold;
            ecoBagBefore = NetAvatar.BagSignature(bag);
            mine.RpcVendor(vendor.gameObject.name);
            yield return new WaitForSeconds(0.5f);
            mine.RpcBuy(ItemCatalog.Bandage);
            yield return new WaitForSeconds(1.0f);
            ecoGoldAfter = body.Gold;
            ecoBagAfter = NetAvatar.BagSignature(bag);

            // ── 치트 시도: **클라가 제 손으로** 골드를 올리고 비싼 것을 산다 ──
            // 오늘의 실제 동작이 이랬다(오프라인 폴백이 클라 프로세스에서 `TryBuy`를 돈다).
            // 둘 다 거짓이어야 서버 권위다. NC(`-ulon-nc-localeconomy`)로 문을 떼면 둘 다 참이 된다.
            int seen = body.Gold;
            body.Gold = seen + 10000;
            ecoCheatStuck = body.Gold != seen;
            OfflineWorld.Instance?.TryVendor(body, vendor);       // 클라 제 세계의 상점을 연다
            var local = OfflineWorld.Instance != null
                ? OfflineWorld.Instance.TryBuy(body, ItemCatalog.IronSword)
                : new AttackResult { FailReason = "no_world" };
            ecoLocalBuy = local.Applied;
            ecoBagAfterCheat = NetAvatar.BagSignature(bag);
            Debug.Log("[Ulon] 축3 실측 — 골드 " + ecoGoldBefore + " → " + ecoGoldAfter +
                      " · 치트가 먹혔나 " + ecoCheatStuck + " · 클라 구매가 먹혔나 " + ecoLocalBuy);
        }

        /// <summary>
        /// **화면이 읽는 자리에서 잰다** — HUD도 행동 판정도 `OfflineWorld.SkillsOf(body)`를 본다.
        /// 대표 셋(검술·채광·마법)만 판정하지만, 값은 **원장 전량**이 같은 한 줄로 내려온다
        /// (`skillsSeen` = 받은 항목 수. 28개가 다 오는지 여기서 센다 — 대표만 오면 빈 통과다).
        /// </summary>
        static void SkillsSnapshot(NetAvatar mine, out float sword, out float mining, out float magery, out int seen)
        {
            sword = mining = magery = -1f;
            seen = 0;
            var body = mine.GetComponent<WorldBody>();
            var skills = OfflineWorld.Instance != null && body != null ? OfflineWorld.Instance.SkillsOf(body) : null;
            if (skills == null)
                return;
            sword = skills.Get(SkillId.Swordsmanship);
            mining = skills.Get(SkillId.Mining);
            magery = skills.Get(SkillId.Magery);
            string sig = mine.ServerSkills ?? "";
            seen = string.IsNullOrEmpty(sig) ? 0 : sig.Split('|').Length;
        }

        /// <summary>
        /// **치트 시도** — 클라가 제 스킬을 올려 놓고 어려운 행동을 하려 한다(축 ④, 검수 조건 3).
        /// 문(`EconomyAuthority`)이 막으면 값이 안 붙는다. NC로 문을 떼면 붙는다.
        /// </summary>
        static void SkillCheat(NetAvatar mine)
        {
            var body = mine.GetComponent<WorldBody>();
            var skills = OfflineWorld.Instance != null && body != null ? OfflineWorld.Instance.SkillsOf(body) : null;
            if (skills == null)
                return;
            float seenValue = skills.Get(SkillId.Swordsmanship);
            skills.TrySet(SkillId.Swordsmanship, 90f);
            skCheatStuck = skills.Get(SkillId.Swordsmanship) > seenValue + 0.5f;
            Debug.Log("[Ulon] 축4 실측 — 검술 " + skSwordBefore.ToString("0.###") + " → " +
                      skSwordAfter.ToString("0.###") + " · 원장 항목 " + skillsSeen +
                      " · 치트가 먹혔나 " + skCheatStuck);
        }

        /// <summary>
        /// **격리 실험 — 끊긴 클라가 공유 저장소를 덮는가**(검수 지시 2026-09-08).
        ///
        /// 앞선 판에서 저장소 값이 서버 것(`2`)이었던 것은 **동기화가 매 프레임 되돌린 결과**라
        /// 이 경로를 가른 실험이 아니었다. 그래서 **동기화가 덮을 수 없는 상태**를 만든다:
        /// 접속을 끊고 → 제 값을 굴리고 → 종료한다(`PersistDriver.OnDestroy → SaveLocal`).
        /// 판정은 이 프로세스가 아니라 **저장소를 읽는 검사 스크립트**가 한다(json에는 안 남는다 —
        /// 이 시점엔 이미 json을 썼다). 문이 닫혀 있으면 저장소는 서버 값 그대로여야 한다.
        /// </summary>
        static void StoreWriteExperiment(NetAvatar mine)
        {
            var body = mine.GetComponent<WorldBody>();
            // ① **문 두드리기** — 클라가 제 손으로 공유 저장소에 자기 스냅샷을 쓴다(골드 12345).
            //    이게 통하면 서버가 아는 진실이 덮인다. 문이 닫혀 있으면 저장소는 그대로여야 한다.
            //    (아래 ②는 종료 경로 실험인데, 실측 결과 `OnDestroy` 시점엔 `OfflineWorld.Instance`가
            //     이미 null이라 그 경로로는 안 써진다 — 파괴 순서에 기댄 안전이라 문을 따로 달았다.)
            if (body != null && OfflineWorld.Instance != null)
            {
                // 저장 대상은 **검사 전용 계정**이다 — 제 계정(`ds-a`)에 쓰면 잠시 뒤 서버가 접속 종료
                // 처리로 제 값을 덮어써서, 「막혔다」와 「썼는데 서버가 되돌렸다」가 구별되지 않는다
                // (실측으로 확인한 함정). 아무도 안 쓰는 자리에 써 봐야 문이 열렸는지 알 수 있다.
                var snap = CharacterBinder.Capture("storeprobe", body,
                    OfflineWorld.Instance.SkillsOf(body), OfflineWorld.Instance.StatsOf(body));
                snap.Gold = 12345;
                CharacterStore.Save(snap);
                Debug.Log("[Ulon] 저장소 실험 — 클라가 직접 저장소에 골드 12345를 써 봤다");
            }
            var nm = FishNet.InstanceFinder.NetworkManager;
            if (nm != null)
                nm.ClientManager.StopConnection();
            if (body != null)
                body.Gold = 12345;                       // 끊긴 클라가 제 값을 굴린다
            Debug.Log("[Ulon] 저장소 실험 — 접속 끊고 골드 " + (body != null ? body.Gold : -1) +
                      " 로 두고 종료한다(저장소가 이걸 받으면 구멍이다)");
        }

        static float skSwordBefore = -1f, skSwordAfter = -1f;
        static float skMiningBefore = -1f, skMiningAfter = -1f, skMageryBefore = -1f, skMageryAfter = -1f;
        static int skillsSeen;
        static bool skCheatStuck;

        static int ecoGoldBefore = -1, ecoGoldAfter = -1;
        static string ecoBagBefore = "", ecoBagAfter = "", ecoBagAfterCheat = "";
        static bool ecoCheatStuck, ecoLocalBuy;

        static float pvpHpBefore = -1f, pvpHpAfter = -1f;
        static int pvpCorpses;
        static string pvpCorpseOwner = "", pvpRecovery = "";
        static bool pvpGhostBefore, pvpGhostAfter;
        static float myY, myGroundY, myHp;
        static bool myGhost;
        static float myMaxHp, myMana, mySkill;
        static int myGold, myBag;
        static string myName = "-";

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

        /// <summary>
        /// 길드도 **같은 방식으로** 판정한다(검수 지시 2026-09-08): A가 창설하고 옆의 몸을 초대,
        /// B가 수락하면 두 클라의 길드 상태가 같아져야 한다. `-ulon-nc-party`면 초대만 끊는다.
        /// </summary>
        static IEnumerator Guild(string role, NetAvatar mine, float deadline)
        {
            bool ncCutInvite = Cli.Get("-ulon-nc-party", "") != "";
            var myBody = mine.GetComponent<WorldBody>();
            if (role == "attacker")
            {
                mine.RpcGuildCreate("검사길드");
                yield return new WaitForSeconds(0.4f);
                float inviteDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 5f);
                while (Time.realtimeSinceStartup < inviteDeadline && GuildRosterCount() < 2)
                {
                    var pick = OfflineWorld.NearestInvitee(myBody, Ulon.Shared.GuildRules.InviteRange);
                    var nob = pick != null ? pick.GetComponent<FishNet.Object.NetworkObject>() : null;
                    if (ncCutInvite)
                    {
                        Debug.Log("[Ulon] 길드 초대 안 보냄 — NC(초대 RPC 끊음)");
                        break;
                    }
                    if (nob == null)
                    {
                        yield return new WaitForSeconds(0.25f);
                        continue;
                    }
                    mine.RpcGuildInvite(nob);
                    yield return new WaitForSeconds(0.35f);
                }
            }
            else
            {
                float acceptDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 6f);
                while (Time.realtimeSinceStartup < acceptDeadline && !GuildView.PendingMe)
                    yield return null;
                if (GuildView.PendingMe)
                    mine.RpcGuildAccept();
            }
            float until = Mathf.Min(deadline, Time.realtimeSinceStartup + 4f);
            while (Time.realtimeSinceStartup < until && GuildRosterCount() < 2)
                yield return null;
            guildOpen = GuildView.Open;
            guildName = GuildView.GuildName ?? "";
            guildMembers = GuildRosterCount();
            Debug.Log("[Ulon] 길드 상태(" + role + ") — open " + guildOpen + " · 이름 " + guildName +
                      " · 인원 " + guildMembers);
        }

        static int GuildRosterCount()
        {
            if (!GuildView.Open || string.IsNullOrEmpty(GuildView.Roster))
                return 0;
            return GuildView.Roster.Split('\n').Length;
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
                          + ",\"guildOpen\":" + (guildOpen ? "true" : "false")
                          + ",\"guildName\":\"" + guildName.Replace("\"", "") + "\""
                          + ",\"guildMembers\":" + guildMembers
                          + ",\"y\":" + myY.ToString("0.##")
                          + ",\"groundY\":" + myGroundY.ToString("0.##")
                          + ",\"hp\":" + myHp.ToString("0.##")
                          + ",\"ghost\":" + (myGhost ? "true" : "false")
                          + ",\"maxHp\":" + myMaxHp.ToString("0.##")
                          + ",\"mana\":" + myMana.ToString("0.##")
                          + ",\"gold\":" + myGold
                          + ",\"bag\":" + myBag
                          + ",\"skill\":" + mySkill.ToString("0.##")
                          + ",\"name\":\"" + myName.Replace("\"", "") + "\""
                          + ",\"pvpHpBefore\":" + pvpHpBefore.ToString("0.##")
                          + ",\"pvpHpAfter\":" + pvpHpAfter.ToString("0.##")
                          + ",\"pvpGhostBefore\":" + (pvpGhostBefore ? "true" : "false")
                          + ",\"pvpGhostAfter\":" + (pvpGhostAfter ? "true" : "false")
                          + ",\"pvpCorpses\":" + pvpCorpses
                          + ",\"pvpCorpseOwner\":\"" + pvpCorpseOwner.Replace("\"", "") + "\""
                          + ",\"pvpRecovery\":\"" + pvpRecovery.Replace("\"", "") + "\""
                          + ",\"ecoGoldBefore\":" + ecoGoldBefore
                          + ",\"ecoGoldAfter\":" + ecoGoldAfter
                          + ",\"ecoBagBefore\":\"" + ecoBagBefore + "\""
                          + ",\"ecoBagAfter\":\"" + ecoBagAfter + "\""
                          + ",\"ecoBagAfterCheat\":\"" + ecoBagAfterCheat + "\""
                          + ",\"ecoCheatStuck\":" + (ecoCheatStuck ? "true" : "false")
                          + ",\"ecoLocalBuy\":" + (ecoLocalBuy ? "true" : "false")
                          + ",\"skSwordBefore\":" + skSwordBefore.ToString("0.###")
                          + ",\"skSwordAfter\":" + skSwordAfter.ToString("0.###")
                          + ",\"skMiningAfter\":" + skMiningAfter.ToString("0.###")
                          + ",\"skMageryAfter\":" + skMageryAfter.ToString("0.###")
                          + ",\"skillsSeen\":" + skillsSeen
                          + ",\"skCheatStuck\":" + (skCheatStuck ? "true" : "false")
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
