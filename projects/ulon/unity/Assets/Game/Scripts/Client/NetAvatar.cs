using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Client
{
    public sealed class NetAvatar : NetworkBehaviour, IHintSink
    {
        /// <summary>이 클라 화면에 떠 있는 마지막 안내 — TargetRpc로만 채워진다(검수 B).</summary>
        string clientHint = "";
        public string ClientHint => clientHint;
        readonly SyncVar<float> skill = new SyncVar<float>();
        // **축 ② — 체력·사망/유령을 서버가 말한다**(오너 결정 (C) 단계 도입, 2026-09-08).
        //
        // 왜 필요한가: `WorldBody`는 `NetworkBehaviour`가 아니라 평범한 MonoBehaviour라
        // Hp·Ghost가 그냥 필드였고, HUD는 **클라의 로컬 값**을 그렸다. 실측에서 서버가 「유령이라
        // 거절」하는 동안 클라 화면은 `HP 0 · 유령 False`였다 — 죽어도 유령 화면이 안 뜨고
        // 부활 안내도 안 떴다는 뜻이다(`docs/NETWORK_STATE_AUDIT.md`).
        //
        // 방향은 하나뿐이다: **서버가 쓰고 클라는 받아 적는다.** 클라가 자기 값으로 되돌리면
        // 그건 「절반만 서버 권위」이고 지금보다 나쁘다(검수 판정).
        readonly SyncVar<float> hp = new SyncVar<float>();
        readonly SyncVar<float> maxHp = new SyncVar<float>();
        readonly SyncVar<bool> ghost = new SyncVar<bool>();
        // 축 ③ — 골드와 가방. **값만 내리는 것으로는 부족하다**(검수 조건 1): 소비·획득 판정은
        // 서버가 하고(`EconomyAuthority`가 클라의 손을 막는다) 여기로는 **결과만** 내려온다.
        // 가방은 「개수」가 아니라 **무엇이 들어 있나**를 실어야 양쪽이 같은지 볼 수 있다 —
        // 개수만 보면 다른 물건이 같은 수로 있어도 통과한다(검수 조건 3).
        readonly SyncVar<int> gold = new SyncVar<int>();
        readonly SyncVar<string> bagSig = new SyncVar<string>();
        // 축 ④ — **스킬 원장 전량을 한 줄로**. 예전엔 `skill`(검술 하나)만 내려왔는데 **읽는 곳이
        // 검사 프로브뿐**이라 화면은 여전히 클라 로컬 값을 그렸다 — SyncVar가 있는데 화면은 딴 값,
        // 빈 통과의 전형이다(검수 지적). 그래서 화면이 읽는 자리(`OfflineWorld.SkillsOf`)에 얹는다.
        readonly SyncVar<string> skillSig = new SyncVar<string>();
        string accountId;

        public float SwordSkill => skill.Value;
        public float ServerHp => hp.Value;
        public float ServerMaxHp => maxHp.Value;
        public bool ServerGhost => ghost.Value;
        public int ServerGold => gold.Value;
        public string ServerBag => bagSig.Value;
        public string ServerSkills => skillSig.Value;

        public override void OnStartClient()
        {
            bool mine = IsOwner;
            var motor = GetComponent<ClickMotor>();
            var avatar = GetComponent<LocalAvatar>();
            if (motor != null)
                motor.enabled = mine;
            if (avatar != null)
                avatar.enabled = mine;

            if (!mine)
                return;

            var cam = Camera.main != null ? Camera.main.GetComponent<QuarterViewCamera>() : null;
            cam?.SetFollow(transform);
            var body = GetComponent<WorldBody>();
            OfflineWorld.Instance?.SetLocalPlayer(body);
            RpcBind(PersistDriver.AccountKey());
        }

        void Update()
        {
            var body = GetComponent<WorldBody>();
            if (body == null)
                return;
            if (IsServerInitialized)
            {
                // 서버가 원장이다 — 몸의 값을 그대로 싣는다. SyncVar는 값이 바뀔 때만 나간다.
                // NC 스위치가 켜져 있으면 **싣지 않는다** — 동기화 경로를 실제로 끊어 놓고
                // 검사가 빨간불인지 본다(게이트를 끄는 NC는 아무것도 증명하지 않는다).
                if (!Cli.Has("-ulon-nc-nosync"))
                {
                    hp.Value = body.Hp;
                    maxHp.Value = body.MaxHp;
                    ghost.Value = body.Ghost;
                    gold.Value = body.Gold;
                    // 문자열 두 줄은 **매 프레임 새로 짜지 않는다**(SyncVar는 값이 같으면 안 나가지만
                    // 문자열을 만드는 비용은 매 프레임 든다). 0.25초마다면 화면에 늦음이 안 보인다.
                    if (Time.time >= nextSigAt)
                    {
                        nextSigAt = Time.time + 0.25f;
                        bagSig.Value = BagSignature(GetComponent<InventoryBag>());
                        skillSig.Value = SkillSignature(OfflineWorld.Instance?.SkillsOf(body));
                    }
                    PublishCorpse();
                }
                return;
            }
            // 클라는 받아 적기만 한다. 여기서 로컬 시뮬레이션이 덮어써도 다음 프레임에 되돌아온다 —
            // 화면이 흔들리면 그건 클라가 아직 자기 세계를 돌리고 있다는 신호다(축 ②의 반쪽).
            // **서버가 아직 한 번도 내보내지 않았으면 덮지 않는다.** SyncVar 초기값은 0이라, 그대로
        // 쓰면 접속 직후 몇 프레임 동안 **모든 아바타가 HP 0**이 되고 그 사이에 파티·길드 초대가
        // 「죽은 사람」으로 거절된다(실측: 이 가드가 없어 파티 0명·길드 1명으로 무너졌다).
            if (maxHp.Value <= 0f)
                return;
            body.ApplyNetworkState(hp.Value, maxHp.Value, ghost.Value, gold.Value);
            var myBag = GetComponent<InventoryBag>();
            if (myBag != null)
                myBag.ApplyNetworkItems(ParseBag(bagSig.Value));
            // **화면이 읽는 자리에 얹는다** — HUD·행동 판정이 보는 것은 `OfflineWorld.SkillsOf(body)`다.
            var mySkills = OfflineWorld.Instance != null ? OfflineWorld.Instance.SkillsOf(body) : null;
            if (mySkills != null && !string.IsNullOrEmpty(skillSig.Value))
                mySkills.ApplyNetworkValues(ParseSkills(skillSig.Value));
        }

        float nextSigAt;

        /// <summary>스킬 원장을 한 줄로 — `SkillId` 순서대로 값만 `|`로 잇는다(전량, 대표만이 아니다).</summary>
        internal static string SkillSignature(SkillSet skills)
        {
            if (skills == null)
                return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < (int)SkillId.Count; i++)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(skills.Get((SkillId)i).ToString("0.###"));
            }
            return sb.ToString();
        }

        static float[] ParseSkills(string sig)
        {
            var parts = sig.Split('|');
            var v = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                float.TryParse(parts[i], out v[i]);
            return v;
        }

        /// <summary>가방을 한 줄로 — `템플릿:개수:남은횟수` 를 `|`로 잇는다(빈 가방은 빈 문자열).</summary>
        internal static string BagSignature(InventoryBag bag)
        {
            if (bag == null)
                return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < bag.Items.Count; i++)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(bag.Items[i].TemplateId).Append(':')
                  .Append(bag.Items[i].Amount).Append(':')
                  .Append(bag.Items[i].Uses);
            }
            return sb.ToString();
        }

        static System.Collections.Generic.List<ItemRecord> ParseBag(string sig)
        {
            var list = new System.Collections.Generic.List<ItemRecord>();
            if (string.IsNullOrEmpty(sig))
                return list;
            var parts = sig.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                var f = parts[i].Split(':');
                if (f.Length < 3)
                    continue;
                int.TryParse(f[1], out int amount);
                int.TryParse(f[2], out int uses);
                list.Add(new ItemRecord { Slot = i, TemplateId = f[0], Amount = amount, Uses = uses });
            }
            return list;
        }

        /// <summary>서버가 마지막으로 알린 시체 자리 — 바뀔 때만 방송한다(매 프레임 Rpc 금지).</summary>
        string corpseSent = "";

        /// <summary>
        /// **시체는 서버에만 있었다**(축 ② 실측). `HandleDeath`가 만드는 `Corpse`는 평범한
        /// GameObject라 클라 화면에는 아무것도 안 뜬다 — 죽은 사람도, 옆 사람도 시체를 못 봤다.
        /// 그래서 **서버가 시체 원장(`FindCorpse`)을 매 틱 읽어** 생김/사라짐만 방송한다.
        /// 제거 경로(약탈·교체·소멸)가 여럿이라 각 호출부에 방송을 심으면 하나를 빠뜨린다 —
        /// 원장 하나를 보고 판단한다(같은 로직이 여러 곳에 살면 재발한다).
        /// </summary>
        void PublishCorpse()
        {
            if (string.IsNullOrEmpty(accountId))
                return;
            var node = OfflineWorld.FindCorpse(accountId);
            string now = node == null ? "" : node.CorpseId + "@" +
                         node.transform.position.x.ToString("0.0") + "," +
                         node.transform.position.y.ToString("0.0") + "," +
                         node.transform.position.z.ToString("0.0");
            if (now == corpseSent)
                return;
            corpseSent = now;
            if (node == null)
                RpcCorpseGone(accountId);
            else
                RpcCorpse(accountId, node.CorpseId, node.LastKind, node.transform.position, node.SecondsLeft);
        }

        [ObserversRpc]
        void RpcCorpse(string ownerId, string corpseId, string kind, Vector3 pos, float secondsLeft)
        {
            if (IsServerInitialized)
                return;                      // 서버에는 진짜 시체가 이미 있다
            OfflineWorld.Instance?.ApplyCorpseView(ownerId, corpseId, kind, pos, secondsLeft, GetComponent<WorldBody>());
        }

        /// <summary>이 클라가 마지막으로 시체를 열어 본 결과 — 거절 사유(빈 문자열이면 성공).</summary>
        public static string LastPeekFail = "";

        /// <summary>
        /// **시체 열람**(오너 판정 2026-09-08). 근접이면 누구나 볼 수 있고, **목록은 요청한
        /// 사람에게만** 간다 — 방송하면 멀리 있는 사람 화면에도 남의 가방이 뜬다.
        /// 가져가기는 이 길이 아니다(`RpcLoot` → `LootAllowed`) — 규칙이 다르므로 길도 다르다.
        /// </summary>
        [ServerRpc]
        public void RpcCorpsePeek(string ownerId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var node = OfflineWorld.FindCorpse(ownerId);
            var result = OfflineWorld.Instance.TryPeekCorpse(GetComponent<WorldBody>(), node, out string items);
            RpcCorpseItems(Owner, ownerId, result.Applied ? items : "", result.Applied ? "" : result.FailReason);
        }

        [TargetRpc]
        void RpcCorpseItems(FishNet.Connection.NetworkConnection conn, string ownerId, string items, string fail)
        {
            LastPeekFail = fail ?? "";
            if (string.IsNullOrEmpty(fail))
                OfflineWorld.Instance?.ApplyCorpseItems(ownerId, items);
            else
                Debug.Log("[Ulon] 시체 열람 거절 — " + fail + " (시체 " + ownerId + ")");
        }

        [ObserversRpc]
        void RpcCorpseGone(string ownerId)
        {
            if (IsServerInitialized)
                return;
            OfflineWorld.Instance?.RemoveCorpseView(ownerId);
        }

        public override void OnStopNetwork()
        {
            if (IsServerInitialized)
                SaveNow();
        }

        [ServerRpc]
        public void RpcBind(string account)
        {
            accountId = account;
            CharacterStore.EnsureRunning();
            var body = GetComponent<WorldBody>();
            var skills = OfflineWorld.Instance.SkillsOf(body);
            var stats = OfflineWorld.Instance.StatsOf(body);
            var snap = CharacterStore.Load(account);
            if (snap != null)
            {
                CharacterBinder.Apply(body, snap, skills, stats);
                skill.Value = skills.Get(SkillId.Swordsmanship);
            }
            else
                PersistDriver.Creating = true;
        }

        [ServerRpc]
        public void RpcSetPos(Vector3 world)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            transform.position = world;
            if (cc != null)
                cc.enabled = true;
        }

        [ServerRpc]
        public void RpcDungeon(string gateName)
        {
            if (OfflineWorld.Instance == null)
                return;
            var gate = OfflineWorld.FindGate(gateName);
            OfflineWorld.Instance.TryDungeon(GetComponent<WorldBody>(), gate);
        }

        [ServerRpc]
        public void RpcGate(string gateName)
        {
            if (OfflineWorld.Instance == null)
                return;
            var gate = OfflineWorld.FindMoongate(gateName);
            var result = OfflineWorld.Instance.TryGate(GetComponent<WorldBody>(), gate);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcStable(string stableName)
        {
            if (OfflineWorld.Instance == null)
                return;
            var stable = OfflineWorld.FindStable(stableName);
            var body = GetComponent<WorldBody>();
            AttackResult result;
            string cid = body != null ? body.CharacterId : "";
            if (OfflineWorld.Instance.HasStabled(cid))
                result = OfflineWorld.Instance.TryClaimStable(body, stable);
            else
                result = OfflineWorld.Instance.TryStable(body, stable);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcRequestAttack(NetworkObject target)
        {
            if (target == null || OfflineWorld.Instance == null)
                return;
            var attacker = GetComponent<WorldBody>();
            var victim = target.GetComponent<WorldBody>();
            var result = OfflineWorld.Instance.TryAttack(attacker, victim);
            if (result.Applied && victim != null)
            {
                // 효과는 **서버에서 재생하면 안 된다** — 서버 인스턴스에서만 난다(검수 랩 D).
                RpcPlayEffect((int)ActionVfx.Kind.Hit, victim.transform.position + Vector3.up * 1.0f);
            }
            if (!result.Applied)
            {
                Debug.Log("[Ulon] attack fail " + result.FailReason);
                return;
            }
            skill.Value = result.SkillAfter;
            target.GetComponent<NetMob>()?.ServerSetHp(victim.Hp);
            RpcPlayAttack();
            SaveNow();
        }

        [ServerRpc]
        public void RpcGather(string nodeId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var node = OfflineWorld.FindNode(nodeId);
            var result = OfflineWorld.Instance.TryGather(GetComponent<WorldBody>(), node);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTrade(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryTrade(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
            BroadcastTrade();
        }

        [ServerRpc]
        public void RpcTradeOffer(string template)
        {
            OfflineWorld.Instance?.SetTradeOffer(GetComponent<WorldBody>(), template);
            BroadcastTrade();
        }

        [ServerRpc]
        public void RpcTradeAccept()
        {
            var result = OfflineWorld.Instance != null
                ? OfflineWorld.Instance.ConfirmTrade(GetComponent<WorldBody>())
                : default;
            if (result.Applied)
                SaveNow();
            BroadcastTrade();
        }

        [ServerRpc]
        public void RpcTradeCancel()
        {
            OfflineWorld.Instance?.CancelTrade(GetComponent<WorldBody>());
            BroadcastTrade();
        }

        void BroadcastTrade()
        {
            var me = GetComponent<WorldBody>();
            var t = me != null ? me.Trade : null;
            if (t == null)
            {
                RpcTradeState(false, 0, 0, "", "", "", "", false, false);
                return;
            }
            int idA = t.A.GetComponent<NetworkObject>() != null ? t.A.GetComponent<NetworkObject>().ObjectId : 0;
            int idB = t.B.GetComponent<NetworkObject>() != null ? t.B.GetComponent<NetworkObject>().ObjectId : 0;
            RpcTradeState(true, idA, idB, t.A.DisplayName, t.B.DisplayName, t.OfferA, t.OfferB, t.AcceptA, t.AcceptB);
        }

        [ObserversRpc]
        void RpcTradeState(bool open, int idA, int idB, string nameA, string nameB, string offerA, string offerB, bool accA, bool accB)
        {
            TradeView.Open = open;
            TradeView.IdA = idA;
            TradeView.IdB = idB;
            TradeView.NameA = nameA;
            TradeView.NameB = nameB;
            TradeView.OfferA = offerA;
            TradeView.OfferB = offerB;
            TradeView.AcceptA = accA;
            TradeView.AcceptB = accB;
        }

        [ServerRpc]
        public void RpcCraft(string stationId, string recipeId = "")
        {
            if (OfflineWorld.Instance == null)
                return;
            var station = OfflineWorld.FindStation(stationId);
            var result = OfflineWorld.Instance.TryCraft(GetComponent<WorldBody>(), station, recipeId);
            if (result.Applied)
            {
                if (station != null)
                    RpcPlayEffect((int)ActionVfx.Kind.Craft, station.transform.position + Vector3.up * 1.1f);
                SaveNow();
            }
        }

        [ServerRpc]
        public void RpcRepair(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var station = OfflineWorld.FindStation(stationId);
            var result = OfflineWorld.Instance.TryRepair(GetComponent<WorldBody>(), station);
            if (result.Applied)
            {
                if (station != null)
                    RpcPlayEffect((int)ActionVfx.Kind.Craft, station.transform.position + Vector3.up * 1.1f);
                SaveNow();
            }
        }

        [ServerRpc]
        public void RpcBank(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var station = OfflineWorld.FindBank(stationId);
            var result = OfflineWorld.Instance.TryBank(GetComponent<WorldBody>(), station);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcVendor(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var vendor = OfflineWorld.FindVendor(stationId);
            OfflineWorld.Instance.TryVendor(GetComponent<WorldBody>(), vendor);
        }

        [ServerRpc]
        public void RpcBuy(string templateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryBuy(GetComponent<WorldBody>(), templateId);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcSell(string templateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TrySell(GetComponent<WorldBody>(), templateId);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTrainer(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var trainer = OfflineWorld.FindTrainer(stationId);
            OfflineWorld.Instance.TryTrainer(GetComponent<WorldBody>(), trainer);
        }

        [ServerRpc]
        public void RpcTrain(int skillId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryTrain(GetComponent<WorldBody>(), (SkillId)skillId);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcCast(int spellId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryCast(GetComponent<WorldBody>(), (SpellId)spellId, SelectedTarget());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcMark()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryMark(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcRecall()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryRecall(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcDrink()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryDrink(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHeal()
        {
            if (OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            WorldBody target = SelectedTarget();
            if (target != null && target.Ghost && target.IsAvatar && target != body)
            {
                var rez = OfflineWorld.Instance.TryResurrectBandage(body, target);
                if (rez.Applied)
                    SaveNow();
                return;
            }
            if (target == null || target.IsEnemy || !target.Alive)
                target = body;
            var result = OfflineWorld.Instance.TryHeal(body, target);
            if (result.Applied && target != null)
            {
                RpcPlayEffect((int)ActionVfx.Kind.Heal, target.transform.position + Vector3.up * 1.0f);
            }
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcMeditate()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryMeditate(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcEvaluate()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryEvaluate(GetComponent<WorldBody>(), SelectedTarget());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTrack()
        {
            if (OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            var world = OfflineWorld.Instance;
            TrackingResult result;
            var pick = world.TargetOf(body);
            if (pick != null)
                result = world.TryTrack(body, pick);
            else
                result = world.TryTrackCorpse(body, OfflineWorld.FindCorpse(body != null ? body.CharacterId : ""));
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcLore()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryLore(GetComponent<WorldBody>(), SelectedTarget());
            if (result.Applied)
                SaveNow();
        }

        // 기획서 §7.2 서버 권한형 — 조련·펫 명령이 이 배선을 빠뜨려 온라인에서 각 클라의
        // 로컬 오프라인 월드만 바꾸고 있었다(검수 2026-09-06 P0). 다른 기능과 같은 모양으로 맞춘다.
        [ServerRpc]
        public void RpcAcceptOrder()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryAcceptOrder(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTurnInOrder()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryTurnInOrder(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTame(NetworkObject target)
        {
            if (target == null || OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryTame(GetComponent<WorldBody>(), target.GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPetCommand(NetworkObject pet, int mode)
        {
            if (pet == null || OfflineWorld.Instance == null)
                return;
            var me = GetComponent<WorldBody>();
            var body = pet.GetComponent<WorldBody>();
            var result = mode == 1
                ? OfflineWorld.Instance.TryPetStay(me, body)
                : mode == 2
                    ? OfflineWorld.Instance.TryPetGuard(me, body)
                    : OfflineWorld.Instance.TryPetFollow(me, body);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPetAttack(NetworkObject pet, NetworkObject enemy)
        {
            if (pet == null || OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPetAttack(GetComponent<WorldBody>(), pet.GetComponent<WorldBody>(),
                enemy != null ? enemy.GetComponent<WorldBody>() : null);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPetCome(NetworkObject pet)
        {
            if (pet == null || OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPetCome(GetComponent<WorldBody>(), pet.GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        // 아래 5개는 서버에 구현만 있고 클라 호출부가 없어 **게임에서 도달 불가**였다(2026-09-06 도달 스캔).
        [ServerRpc]
        public void RpcPetRelease(NetworkObject pet)
        {
            if (pet == null || OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPetRelease(GetComponent<WorldBody>(), pet.GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcEquip(string templateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryEquip(GetComponent<WorldBody>(), templateId);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcUnequip()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryUnequip(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcMoveToPouch(string templateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryMoveToPouch(GetComponent<WorldBody>(), templateId, "");
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTakeFromPouch(string templateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryTakeFromPouch(GetComponent<WorldBody>(), templateId, "");
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcSpeech(string text)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TrySpeechKeyword(GetComponent<WorldBody>(), text);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcVet()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryVet(GetComponent<WorldBody>(), SelectedTarget());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcInscribe()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryInscribe(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPoisonWeapon()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPoisonWeapon(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcUseScroll()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryUseScroll(GetComponent<WorldBody>(), SelectedTarget());
            if (result.Applied)
                SaveNow();
        }


        [ServerRpc]
        public void RpcPlay()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPlay(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPeace()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPeace(GetComponent<WorldBody>(), SelectedTarget());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcProvoke()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryProvokeStep(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHide()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryHide(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }


        [ServerRpc]
        public void RpcPick(string crateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var crate = OfflineWorld.FindCrate(crateId);
            var result = OfflineWorld.Instance.TryPick(GetComponent<WorldBody>(), crate);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcStealth()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryStealth(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcDetectHidden()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryDetectHidden(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }


        [ServerRpc]
        public void RpcCamp()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryCamp(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcSteal()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TrySteal(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcResurrectBandage()
        {
            if (OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            WorldBody target = SelectedTarget();
            if (target == null || !target.Ghost || !target.IsAvatar || target == body)
                target = OfflineWorld.NearestGhostAvatar(body);
            var result = OfflineWorld.Instance.TryResurrectBandage(body, target);
            if (result.Applied)
                SaveNow();
        }
        [ServerRpc]
        public void RpcCurePoison()
        {
            if (OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            WorldBody target = SelectedTarget();
            if (target == null || target.IsEnemy || !target.Alive || target.Ghost)
                target = body;
            var result = OfflineWorld.Instance.TryCurePoison(body, target);
            if (result.Applied)
                SaveNow();
        }


        [ServerRpc]
        public void RpcResurrect(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var healer = OfflineWorld.FindHealer(stationId);
            var result = OfflineWorld.Instance.TryResurrect(GetComponent<WorldBody>(), healer);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPartyInvite(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            // **거절 사유를 서버가 남긴다** — 안 남기면 클라에서는 「버튼을 눌렀는데 아무 일도 안 난다」로만
            // 보인다(2클라 판정에서 실제로 여기서 막혔다).
            var res = OfflineWorld.Instance.TryPartyInvite(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
            if (!res.Applied)
                Debug.Log("[Ulon] 파티 초대 거절 — " + res.FailReason + " (대상 " + other.name + ")");
            BroadcastParty();
        }

        [ServerRpc]
        public void RpcPartyAccept()
        {
            OfflineWorld.Instance?.TryPartyAccept(GetComponent<WorldBody>());
            BroadcastParty();
        }

        [ServerRpc]
        public void RpcPartyLeave()
        {
            OfflineWorld.Instance?.TryPartyLeave(GetComponent<WorldBody>());
            BroadcastParty();
        }

        [ServerRpc]
        public void RpcPartySay(string text)
        {
            OfflineWorld.Instance?.TryPartySay(GetComponent<WorldBody>(), text);
            BroadcastParty();
        }

        void BroadcastParty()
        {
            var me = GetComponent<WorldBody>();
            var p = me != null ? me.Party : null;
            if (p == null)
            {
                RpcPartyState(false, 0, "", "", "");
                return;
            }
            string roster = p.Leader != null ? p.Leader.DisplayName + " " + p.Leader.Hp.ToString("0") + "/" + p.Leader.MaxHp.ToString("0") : "";
            for (int i = 0; i < p.Members.Count; i++)
            {
                var m = p.Members[i];
                if (m == null)
                    continue;
                roster += "\n" + m.DisplayName + " " + m.Hp.ToString("0") + "/" + m.MaxHp.ToString("0");
            }
            string chat = "";
            for (int i = 0; i < p.Chat.Count; i++)
            {
                if (i > 0)
                    chat += "\n";
                chat += p.Chat[i];
            }
            int pendingId = 0;
            if (p.Pending != null)
            {
                var pn = p.Pending.GetComponent<NetworkObject>();
                if (pn != null)
                    pendingId = pn.ObjectId;
            }
            RpcPartyState(true, pendingId, p.Leader != null ? p.Leader.DisplayName : "", roster, chat);
        }

        [ObserversRpc]
        void RpcPartyState(bool open, int pendingId, string leader, string roster, string chat)
        {
            PartyView.Open = open;
            // **「나에게 온 초대인가」는 내 아바타와 비교해야 한다**(2026-09-08 2클라 실측).
            // 이 RPC는 **초대한 사람의 아바타**에서 방송되므로 `ObjectId`는 그 사람의 것이다 —
            // 그래서 초대받은 쪽에서 `pendingId == ObjectId`가 영영 성립하지 않았고,
            // **수락 버튼이 아무에게도 안 그려졌다**(파티가 대장 1명에서 멈춰 있던 이유).
            var mineBody = OfflineWorld.Instance != null ? OfflineWorld.Instance.Player : null;
            var mineNob = mineBody != null ? mineBody.GetComponent<NetworkObject>() : null;
            PartyView.PendingMe = pendingId != 0 && mineNob != null && pendingId == mineNob.ObjectId;
            PartyView.Leader = leader;
            PartyView.Roster = roster;
            PartyView.Chat = chat;
        }

        [ServerRpc]
        public void RpcGuildCreate(string name)
        {
            OfflineWorld.Instance?.TryGuildCreate(GetComponent<WorldBody>(), name);
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildInvite(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            var gres = OfflineWorld.Instance.TryGuildInvite(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
            if (!gres.Applied)
                Debug.Log("[Ulon] 길드 초대 거절 — " + gres.FailReason + " (대상 " + other.name + ")");
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildAccept()
        {
            OfflineWorld.Instance?.TryGuildAccept(GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildLeave()
        {
            OfflineWorld.Instance?.TryGuildLeave(GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildWarDeclare(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryGuildWarDeclare(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildWarPeace()
        {
            OfflineWorld.Instance?.TryGuildWarPeace(GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcDuelInvite(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryDuelInvite(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
        }

        [ServerRpc]
        public void RpcDuelAccept()
        {
            OfflineWorld.Instance?.TryDuelAccept(GetComponent<WorldBody>());
        }

        [ServerRpc]
        public void RpcDuelEnd()
        {
            OfflineWorld.Instance?.TryDuelEnd(GetComponent<WorldBody>());
        }

        [ServerRpc]
        public void RpcDuelYield()
        {
            OfflineWorld.Instance?.TryDuelYield(GetComponent<WorldBody>());
        }

        void BroadcastGuild()
        {
            var body = GetComponent<WorldBody>();
            var world = OfflineWorld.Instance;
            var g = world != null ? world.GuildOf(body) : null;
            if (g == null)
            {
                RpcGuildState(false, 0, "", "", "", "", "");
                return;
            }
            string roster = g.Leader != null ? g.Leader.DisplayName : "";
            for (int i = 0; i < g.Members.Count; i++)
            {
                var m = g.Members[i];
                if (m == null)
                    continue;
                roster += "\n" + m.DisplayName;
            }
            int pendingId = 0;
            if (g.Pending != null)
            {
                var pn = g.Pending.GetComponent<NetworkObject>();
                if (pn != null)
                    pendingId = pn.ObjectId;
            }
            string warName = "";
            if (!string.IsNullOrEmpty(g.WarWithId))
            {
                var enemy = world.FindGuild(g.WarWithId);
                warName = enemy != null ? enemy.Name : g.WarWithId;
            }
            RpcGuildState(true, pendingId, g.Id, g.Name, g.Leader != null ? g.Leader.DisplayName : "", roster, warName);
        }

        [ObserversRpc]
        void RpcGuildState(bool open, int pendingId, string guildId, string guildName, string leader, string roster, string warName)
        {
            GuildView.Open = open;
            // 파티와 **같은 결함**이었다 — 이 RPC도 초대한 사람의 아바타에서 방송되므로
            // `ObjectId`는 그 사람 것이다. 받는 쪽에서 성립하지 않아 수락 버튼이 안 그려졌다.
            var mineGuildBody = OfflineWorld.Instance != null ? OfflineWorld.Instance.Player : null;
            var mineGuildNob = mineGuildBody != null ? mineGuildBody.GetComponent<NetworkObject>() : null;
            GuildView.PendingMe = pendingId != 0 && mineGuildNob != null && pendingId == mineGuildNob.ObjectId;
            GuildView.GuildId = guildId ?? "";
            GuildView.GuildName = guildName ?? "";
            GuildView.Leader = leader ?? "";
            GuildView.Roster = roster ?? "";
            GuildView.WarName = warName ?? "";
            var body = GetComponent<WorldBody>();
            if (body != null && IsOwner)
            {
                body.GuildId = guildId ?? "";
                body.GuildName = guildName ?? "";
            }
        }


        [ServerRpc]
        public void RpcLoot(string ownerId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var node = OfflineWorld.FindCorpse(ownerId);
            var result = OfflineWorld.Instance.TryLootCorpse(GetComponent<WorldBody>(), node);
            if (result.Applied)
                SaveNow();
        }

        [ObserversRpc]
        void RpcPlayAttack()
        {
            GetComponent<CharacterAnim>()?.PlayAttack();
        }

        /// <summary>
        /// 안내는 그 사람에게만 간다(검수 2026-09-08 B). ObserversRpc로 보내면
        /// 「화면에 떴다」만으로 통과하고 옆 사람도 같은 글을 본다.
        /// NC `-ulon-nc-nohint`는 호출부가 이 함수를 안 부르게 해서 절단한다.
        /// </summary>
        public void SendHint(string text)
        {
            if (Owner == null || !IsServerInitialized)
                return;
            RpcHint(Owner, text ?? "");
        }

        [ServerRpc]
        public void RpcSelect(NetworkObject nob)
        {
            if (OfflineWorld.Instance == null)
                return;
            WorldBody target = nob != null ? nob.GetComponent<WorldBody>() : null;
            OfflineWorld.Instance.Select(GetComponent<WorldBody>(), target);
        }

        WorldBody SelectedTarget()
        {
            return OfflineWorld.Instance != null
                ? OfflineWorld.Instance.TargetOf(GetComponent<WorldBody>())
                : null;
        }

        [TargetRpc]
        void RpcHint(NetworkConnection conn, string text)
        {
            clientHint = text ?? "";
        }

        /// <summary>
        /// 행동 효과를 **모든 클라이언트**에 방송한다. 서버 Rpc 본체에서 `ActionVfx/Sfx.Play`를 부르면
        /// 서버 인스턴스에서만 재생돼 행동한 본인도 옆 사람도 아무것도 못 받는다(검수 랩 D).
        /// 불티와 소리는 **한 곳에서 같이** 낸다 — 따로 부르면 한쪽만 조건 밖으로 새는 결함이 또 생긴다.
        /// </summary>
        [ObserversRpc]
        void RpcPlayEffect(int kind, Vector3 at)
        {
            var k = (ActionVfx.Kind)kind;
            // 두 열거형이 같은 순서라는 가정에 기대지 않는다 — 순서가 갈리면 소리만 엉뚱해진다.
            var s = k == ActionVfx.Kind.Heal ? ActionSfx.Kind.Heal
                  : k == ActionVfx.Kind.Craft ? ActionSfx.Kind.Craft
                  : ActionSfx.Kind.Hit;
            ActionVfx.Play(k, at);
            ActionSfx.Play(s, at);
        }


        [ServerRpc]
        public void RpcClaimHouse(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var station = OfflineWorld.FindHouseStation(stationId);
            var result = OfflineWorld.Instance.TryClaimHouse(GetComponent<WorldBody>(), station);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHouseLockdown(string chestId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var chest = OfflineWorld.FindHouseChest(chestId);
            var result = OfflineWorld.Instance.TryLockdown(GetComponent<WorldBody>(), chest, ItemCatalog.Cloth);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHouseTake(string chestId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var chest = OfflineWorld.FindHouseChest(chestId);
            var result = OfflineWorld.Instance.TrySecureTake(GetComponent<WorldBody>(), chest);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHouseVendorList(string vendorId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var vendor = OfflineWorld.FindHouseVendor(vendorId);
            var result = OfflineWorld.Instance.TryListVendor(GetComponent<WorldBody>(), vendor, ItemCatalog.Cloth);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHouseVendorBuy(string vendorId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var vendor = OfflineWorld.FindHouseVendor(vendorId);
            var result = OfflineWorld.Instance.TryBuyHouseVendor(GetComponent<WorldBody>(), vendor);
            if (result.Applied)
                SaveNow();
        }

        void SaveNow()
        {
            if (string.IsNullOrEmpty(accountId) || OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            CharacterStore.Save(CharacterBinder.Capture(accountId, body, OfflineWorld.Instance.SkillsOf(body), OfflineWorld.Instance.StatsOf(body)));
        }
    }
}
