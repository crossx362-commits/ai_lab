using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed class SliceHud : MonoBehaviour
    {
        string createName = "";
        int createAppear;
        int createStr = 30;
        int createDex = 25;
        int createInt = 25;
        SkillId createA = SkillId.Swordsmanship;
        SkillId createB = SkillId.Mining;
        SkillId createC = SkillId.Blacksmithing;
        float createAv = 50f;
        float createBv = 30f;
        float createCv = 20f;
        string createError = "";
        bool lookApplied;
        string partyChat = "";
        string keywordSpeech = "";
        string guildNameInput = "";
        bool gmOpen;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                gmOpen = !gmOpen;
            if (PersistDriver.Creating || lookApplied)
                return;
            var world = OfflineWorld.Instance;
            if (world == null || world.Player == null)
                return;
            OutfitSwap.ApplyLook(world.Player.transform, world.Player.Appearance);
            lookApplied = true;
        }

        void OnGUI()
        {
            OfflineWorld world = OfflineWorld.Instance;
            if (world == null)
                return;
            if (PersistDriver.Creating)
            {
                DrawCreate();
                return;
            }

            float skill = world.PlayerSkills.Get(SkillId.Swordsmanship);
            var net = world.Player != null ? world.Player.GetComponent<NetAvatar>() : null;
            if (net != null && net.IsClientInitialized)
                skill = net.SwordSkill;
            WorldBody target = world.Selected;
            string targetLine = target == null || !target.Alive
                ? "대상 없음"
                : $"{target.DisplayName}  HP {target.Hp:0}/{target.MaxHp:0}";
            if (!string.IsNullOrEmpty(world.LastEvalMessage))
                targetLine += "  " + world.LastEvalMessage;
            if (!string.IsNullOrEmpty(world.LastTrackMessage))
                targetLine += "  " + world.LastTrackMessage;
            if (!string.IsNullOrEmpty(world.LastPlayMessage))
                targetLine += "  " + world.LastPlayMessage;
            if (!string.IsNullOrEmpty(world.LastPeaceMessage))
                targetLine += "  " + world.LastPeaceMessage;
            if (!string.IsNullOrEmpty(world.LastProvokeMessage))
                targetLine += "  " + world.LastProvokeMessage;
            if (!string.IsNullOrEmpty(world.LastHideMessage))
                targetLine += "  " + world.LastHideMessage;
            if (!string.IsNullOrEmpty(world.LastStealthMessage))
                targetLine += "  " + world.LastStealthMessage;
            if (!string.IsNullOrEmpty(world.LastDetectMessage))
                targetLine += "  " + world.LastDetectMessage;
            if (!string.IsNullOrEmpty(world.LastPickMessage))
                targetLine += "  " + world.LastPickMessage;
            if (!string.IsNullOrEmpty(world.LastLoreMessage))
                targetLine += "  " + world.LastLoreMessage;
            if (!string.IsNullOrEmpty(world.LastVetMessage))
                targetLine += "  " + world.LastVetMessage;
            if (!string.IsNullOrEmpty(world.LastTameMessage))
                targetLine += "  " + world.LastTameMessage;
            if (!string.IsNullOrEmpty(world.LastStableMessage))
                targetLine += "  " + world.LastStableMessage;
            if (!string.IsNullOrEmpty(world.LastTravelMessage))
                targetLine += "  " + world.LastTravelMessage;
            if (!string.IsNullOrEmpty(world.LastHealRezMessage))
                targetLine += "  " + world.LastHealRezMessage;
            if (!string.IsNullOrEmpty(world.LastSpeechMessage))
                targetLine += "  " + world.LastSpeechMessage;

            var bag = world.Player != null ? world.Player.GetComponent<InventoryBag>() : null;
            string inv = "가방 비움";
            if (bag != null && bag.Items.Count > 0)
            {
                inv = "";
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    inv += bag.Items[i].TemplateId + " x" + bag.Items[i].Amount;
                    if (bag.Items[i].Uses > 0)
                        inv += "(" + bag.Items[i].Uses + ")";
                    if (bag.Items[i].Exceptional)
                        inv += "*";
                    if (!string.IsNullOrEmpty(bag.Items[i].MakerId))
                        inv += "[" + bag.Items[i].MakerId + "]";
                    inv += "  ";
                }
            }
            float mining = world.PlayerSkills.Get(SkillId.Mining);
            float lumber = world.PlayerSkills.Get(SkillId.Lumberjacking);
            float smith = world.PlayerSkills.Get(SkillId.Blacksmithing);
            string bankInv = "은행 비움";
            var vault = world.Player != null ? world.Player.GetComponent<BankVault>() : null;
            if (vault != null && vault.Items.Count > 0)
            {
                bankInv = "은행 ";
                for (int i = 0; i < vault.Items.Count; i++)
                    bankInv += vault.Items[i].TemplateId + " x" + vault.Items[i].Amount + "  ";
            }
            float magery = world.PlayerSkills.Get(SkillId.Magery);
            float archery = world.PlayerSkills.Get(SkillId.Archery);
            float tactics = world.PlayerSkills.Get(SkillId.Tactics);
            float carp = world.PlayerSkills.Get(SkillId.Carpentry);
            float parry = world.PlayerSkills.Get(SkillId.Parrying);
            float anatomy = world.PlayerSkills.Get(SkillId.Anatomy);
            float healing = world.PlayerSkills.Get(SkillId.Healing);
            float meditation = world.PlayerSkills.Get(SkillId.Meditation);
            float resist = world.PlayerSkills.Get(SkillId.MagicResist);
            float evalInt = world.PlayerSkills.Get(SkillId.EvaluateIntelligence);
            float fishing = world.PlayerSkills.Get(SkillId.Fishing);
            float cooking = world.PlayerSkills.Get(SkillId.Cooking);
            float fencing = world.PlayerSkills.Get(SkillId.Fencing);
            float mace = world.PlayerSkills.Get(SkillId.Mace);
            float alchemy = world.PlayerSkills.Get(SkillId.Alchemy);
            float tracking = world.PlayerSkills.Get(SkillId.Tracking);
            float music = world.PlayerSkills.Get(SkillId.Musicianship);
            float peace = world.PlayerSkills.Get(SkillId.Peacemaking);
            float provoke = world.PlayerSkills.Get(SkillId.Provocation);
            float hiding = world.PlayerSkills.Get(SkillId.Hiding);
            float stealth = world.PlayerSkills.Get(SkillId.Stealth);
            float lockpick = world.PlayerSkills.Get(SkillId.Lockpicking);
            float lore = world.PlayerSkills.Get(SkillId.AnimalLore);
            float vet = world.PlayerSkills.Get(SkillId.Veterinary);
            float tame = world.PlayerSkills.Get(SkillId.AnimalTaming);
            float inscription = world.PlayerSkills.Get(SkillId.Inscription);
            float poisoning = world.PlayerSkills.Get(SkillId.Poisoning);
            float detectHidden = world.PlayerSkills.Get(SkillId.DetectHidden);
            float camping = world.PlayerSkills.Get(SkillId.Camping);
            float stealing = world.PlayerSkills.Get(SkillId.Stealing);
            GUI.Box(new Rect(16, 16, 340, 304), "");
            var st = world.PlayerStats;
            var sk = world.PlayerSkills;
            GUI.Label(new Rect(28, 24, 320, 22), $"{SkillNames.KoreanOf(SkillId.Swordsmanship)} {skill:0.0}  {SkillNames.KoreanOf(SkillId.Archery)} {archery:0.0}  {SkillNames.KoreanOf(SkillId.Tactics)} {tactics:0.0}  {SkillNames.KoreanOf(SkillId.Parrying)} {parry:0.0}  {SkillNames.KoreanOf(SkillId.Anatomy)} {anatomy:0.0}  {SkillNames.KoreanOf(SkillId.Healing)} {healing:0.0}  {SkillNames.KoreanOf(SkillId.Meditation)} {meditation:0.0}  {SkillNames.KoreanOf(SkillId.MagicResist)} {resist:0.0}  {SkillNames.KoreanOf(SkillId.EvaluateIntelligence)} {evalInt:0.0}  채광 {mining:0.0}  벌목 {lumber:0.0}  대장 {smith:0.0}  목공 {carp:0.0}  낚시 {fishing:0.0}  요리 {cooking:0.0}  창술 {fencing:0.0}  둔기 {mace:0.0}  연금 {alchemy:0.0}  추적 {tracking:0.0}  음악 {music:0.0}  평화 {peace:0.0}  도발 {provoke:0.0}  은신 {hiding:0.0}  잠행 {stealth:0.0}  자물쇠 {lockpick:0.0}  지식 {lore:0.0}  수의 {vet:0.0}  조련 {tame:0.0}  각인 {inscription:0.0}  독 {poisoning:0.0}  감지 {detectHidden:0.0}  야영 {camping:0.0}  훔치기 {stealing:0.0}  마법 {magery:0.0}");
            string ghost = world.Player != null && world.Player.Ghost ? "  유령" : "";
            if (world.Player != null && world.Player.IsHidden(UnityEngine.Time.time))
                ghost += world.Player.CanMoveHidden(UnityEngine.Time.time) ? "  잠행" : "  은신";
            if (world.Player != null && world.Player.IsCampSafe(UnityEngine.Time.time))
                ghost += "  야영";
            string noto = world.Player != null ? NotorietyId.Korean(world.Player.Notoriety) : "";
            bool town = world.Player != null && GuardZone.Contains(world.Player.transform.position.x, world.Player.transform.position.z);
            string title = world.Player != null ? world.TitleOf(world.Player) : "";
            string repTitle = world.Player != null ? world.ReputationTitleOf(world.Player) : "";
            string guildTag = "";
            if (world.Player != null && !string.IsNullOrEmpty(world.Player.GuildName))
                guildTag = "[" + world.Player.GuildName + "]";
            else if (!string.IsNullOrEmpty(GuildView.GuildName))
                guildTag = "[" + GuildView.GuildName + "]";
            GUI.Label(new Rect(28, 46, 320, 22), $"{(world.Player != null ? world.Player.DisplayName : "")}{(string.IsNullOrEmpty(guildTag) ? "" : " " + guildTag)}{(string.IsNullOrEmpty(repTitle) ? "" : "  " + repTitle)}{(string.IsNullOrEmpty(title) ? "" : "  " + title)}  STR {st.Str}  DEX {st.Dex}  INT {st.Int}  HP {(world.Player != null ? world.Player.Hp : 0):0}/{(world.Player != null ? world.Player.MaxHp : 0):0}  MP {(world.Player != null ? world.Player.Mana : 0):0}/{(world.Player != null ? world.Player.MaxMana : 0):0}  G {(world.Player != null ? world.Player.Gold : 0)}{ghost}");
            GUI.Label(new Rect(28, 68, 320, 22), $"{targetLine}  명성 {(world.Player != null ? world.Player.Fame : 0)}  {noto}  {(town ? "마을" : "야외")}");
            GUI.Label(new Rect(28, 226, 320, 22), RecoveryLine(world.Player));
            float w = bag != null ? bag.TotalWeight() : 0f;
            int cap = ItemCatalog.CarryCap(st.Str);
            GUI.Label(new Rect(28, 90, 320, 22), inv);
            GUI.Label(new Rect(28, 206, 320, 22), "무게 " + w.ToString("0.0") + "/" + cap + (bag != null && bag.Overweight(st.Str) ? "  과적" : ""));
            GUI.Label(new Rect(28, 162, 320, 22), bankInv);
            if (world.Player != null && !world.Player.Ghost)
            {
                var book = world.BookOf(world.Player);
                if (book.Knows(SpellId.Ember) && GUI.Button(new Rect(28, 186, 70, 24), SpellNames.KoreanOf(SpellId.Ember)))
                    Cast(net, SpellId.Ember);
                if (book.Knows(SpellId.Mend) && GUI.Button(new Rect(104, 186, 70, 24), SpellNames.KoreanOf(SpellId.Mend)))
                    Cast(net, SpellId.Mend);
                if (book.Knows(SpellId.Bolt) && GUI.Button(new Rect(332, 186, 70, 24), SpellNames.KoreanOf(SpellId.Bolt)))
                    Cast(net, SpellId.Bolt);
                if (book.Knows(SpellId.Cleanse) && GUI.Button(new Rect(408, 186, 70, 24), SpellNames.KoreanOf(SpellId.Cleanse)))
                    Cast(net, SpellId.Cleanse);
                if (book.Knows(SpellId.Ward) && GUI.Button(new Rect(484, 186, 70, 24), SpellNames.KoreanOf(SpellId.Ward)))
                    Cast(net, SpellId.Ward);
                if (book.Knows(SpellId.Bind) && GUI.Button(new Rect(560, 186, 70, 24), SpellNames.KoreanOf(SpellId.Bind)))
                    Cast(net, SpellId.Bind);
                if (book.Knows(SpellId.Weaken) && GUI.Button(new Rect(636, 186, 70, 24), SpellNames.KoreanOf(SpellId.Weaken)))
                    Cast(net, SpellId.Weaken);
                if (book.Knows(SpellId.Spark) && GUI.Button(new Rect(712, 186, 70, 24), SpellNames.KoreanOf(SpellId.Spark)))
                    Cast(net, SpellId.Spark);
                if (book.Knows(SpellId.Restore) && GUI.Button(new Rect(788, 186, 70, 24), SpellNames.KoreanOf(SpellId.Restore)))
                    Cast(net, SpellId.Restore);
                if (book.Knows(SpellId.Blink) && GUI.Button(new Rect(864, 186, 70, 24), SpellNames.KoreanOf(SpellId.Blink)))
                    Cast(net, SpellId.Blink);
                if (book.Knows(SpellId.Bless) && GUI.Button(new Rect(940, 186, 70, 24), SpellNames.KoreanOf(SpellId.Bless)))
                    Cast(net, SpellId.Bless);
                if (GUI.Button(new Rect(180, 186, 70, 24), "붕대"))
                    Bandage(net);
                if (GUI.Button(new Rect(256, 186, 70, 24), SkillNames.KoreanOf(SkillId.Meditation)))
                    Meditate(net);
                if (GUI.Button(new Rect(180, 162, 70, 24), "물약"))
                    Drink(net);
                if (GUI.Button(new Rect(256, 162, 70, 24), SkillNames.KoreanOf(SkillId.EvaluateIntelligence)))
                    Evaluate(net);
                if (GUI.Button(new Rect(28, 248, 70, 22), SkillNames.KoreanOf(SkillId.Tracking)))
                    Track(net);
                if (GUI.Button(new Rect(104, 248, 70, 22), "연주"))
                    PlayLute(net);
                if (GUI.Button(new Rect(180, 248, 70, 22), SkillNames.KoreanOf(SkillId.Peacemaking)))
                    Peace(net);
                if (GUI.Button(new Rect(256, 248, 70, 22), SkillNames.KoreanOf(SkillId.Provocation)))
                    Provoke(net);
                if (GUI.Button(new Rect(28, 270, 70, 22), SkillNames.KoreanOf(SkillId.Hiding)))
                    Hide(net);
                if (GUI.Button(new Rect(104, 270, 70, 22), "기록"))
                    Mark(net);
                if (GUI.Button(new Rect(180, 270, 70, 22), SkillNames.KoreanOf(SkillId.Stealth)))
                    Stealth(net);
                if (GUI.Button(new Rect(256, 270, 70, 22), "귀환"))
                    Recall(net);
                if (GUI.Button(new Rect(28, 292, 70, 22), SkillNames.KoreanOf(SkillId.AnimalLore)))
                    Lore(net);
                if (GUI.Button(new Rect(180, 292, 70, 22), SkillNames.KoreanOf(SkillId.Veterinary)))
                    Vet(net);
                if (GUI.Button(new Rect(104, 314, 70, 22), SkillNames.KoreanOf(SkillId.Inscription)))
                    Inscribe(net);
                if (GUI.Button(new Rect(180, 336, 70, 22), SkillNames.KoreanOf(SkillId.Poisoning)))
                    PoisonWeapon(net);
                if (GUI.Button(new Rect(28, 336, 70, 22), SkillNames.KoreanOf(SkillId.DetectHidden)))
                    DetectHidden(net);
                if (GUI.Button(new Rect(104, 358, 70, 22), SkillNames.KoreanOf(SkillId.Camping)))
                    Camp(net);
                if (GUI.Button(new Rect(180, 380, 70, 22), SkillNames.KoreanOf(SkillId.Stealing)))
                    Steal(net);
                if (GUI.Button(new Rect(256, 380, 70, 22), "부활"))
                    ResurrectBandage(net);
                if (GUI.Button(new Rect(180, 402, 70, 22), "해독"))
                    CurePoison(net);
                if (GUI.Button(new Rect(424, 402, 50, 22), "의뢰"))
                    AcceptCraftOrder(net);
                if (GUI.Button(new Rect(480, 402, 50, 22), "납품"))
                    TurnInCraftOrder(net);
                if (GUI.Button(new Rect(28, 402, 70, 22), "펫공격"))
                    PetAttack(net);
                if (GUI.Button(new Rect(104, 402, 70, 22), "펫호출"))
                    PetCome(net);
                if (GUI.Button(new Rect(256, 402, 50, 22), "은행"))
                    SpeakKeyword(net, "은행");
                if (GUI.Button(new Rect(312, 402, 50, 22), "경비"))
                    SpeakKeyword(net, "경비");
                if (GUI.Button(new Rect(368, 402, 50, 22), "상점"))
                    SpeakKeyword(net, "상점");
                keywordSpeech = GUI.TextField(new Rect(424, 402, 80, 22), keywordSpeech ?? "");
                if (GUI.Button(new Rect(508, 402, 36, 22), "말"))
                {
                    SpeakKeyword(net, keywordSpeech);
                    keywordSpeech = "";
                }
                if (GUI.Button(new Rect(256, 314, 70, 22), "주문서"))
                    UseScroll(net);
            }
            if (LockButton(28, 112, SkillNames.KoreanOf(SkillId.Swordsmanship), sk.GetLock(SkillId.Swordsmanship)))
                sk.CycleLock(SkillId.Swordsmanship);
            if (LockButton(118, 112, SkillNames.KoreanOf(SkillId.Archery), sk.GetLock(SkillId.Archery)))
                sk.CycleLock(SkillId.Archery);
            if (LockButton(208, 112, SkillNames.KoreanOf(SkillId.Tactics), sk.GetLock(SkillId.Tactics)))
                sk.CycleLock(SkillId.Tactics);
            if (LockButton(28, 88, SkillNames.KoreanOf(SkillId.Parrying), sk.GetLock(SkillId.Parrying)))
                sk.CycleLock(SkillId.Parrying);
            if (LockButton(118, 88, SkillNames.KoreanOf(SkillId.Anatomy), sk.GetLock(SkillId.Anatomy)))
                sk.CycleLock(SkillId.Anatomy);
            if (LockButton(208, 88, SkillNames.KoreanOf(SkillId.Healing), sk.GetLock(SkillId.Healing)))
                sk.CycleLock(SkillId.Healing);
            if (LockButton(298, 88, SkillNames.KoreanOf(SkillId.Meditation), sk.GetLock(SkillId.Meditation)))
                sk.CycleLock(SkillId.Meditation);
            if (LockButton(298, 206, SkillNames.KoreanOf(SkillId.MagicResist), sk.GetLock(SkillId.MagicResist)))
                sk.CycleLock(SkillId.MagicResist);
            if (LockButton(208, 206, SkillNames.KoreanOf(SkillId.EvaluateIntelligence), sk.GetLock(SkillId.EvaluateIntelligence)))
                sk.CycleLock(SkillId.EvaluateIntelligence);
            if (LockButton(298, 112, "채광", sk.GetLock(SkillId.Mining)))
                sk.CycleLock(SkillId.Mining);
            if (LockButton(298, 138, "대장", sk.GetLock(SkillId.Blacksmithing)))
                sk.CycleLock(SkillId.Blacksmithing);
            if (LockButton(208, 162, "낚시", sk.GetLock(SkillId.Fishing)))
                sk.CycleLock(SkillId.Fishing);
            if (LockButton(118, 162, "요리", sk.GetLock(SkillId.Cooking)))
                sk.CycleLock(SkillId.Cooking);
            if (LockButton(28, 162, "창술", sk.GetLock(SkillId.Fencing)))
                sk.CycleLock(SkillId.Fencing);
            if (LockButton(298, 162, "둔기", sk.GetLock(SkillId.Mace)))
                sk.CycleLock(SkillId.Mace);
            if (LockButton(208, 226, "연금", sk.GetLock(SkillId.Alchemy)))
                sk.CycleLock(SkillId.Alchemy);
            if (LockButton(118, 226, "추적", sk.GetLock(SkillId.Tracking)))
                sk.CycleLock(SkillId.Tracking);
            if (LockButton(298, 226, "음악", sk.GetLock(SkillId.Musicianship)))
                sk.CycleLock(SkillId.Musicianship);
            if (LockButton(298, 248, "평화", sk.GetLock(SkillId.Peacemaking)))
                sk.CycleLock(SkillId.Peacemaking);
            if (LockButton(256, 226, "도발", sk.GetLock(SkillId.Provocation)))
                sk.CycleLock(SkillId.Provocation);
            if (LockButton(104, 270, "은신", sk.GetLock(SkillId.Hiding)))
                sk.CycleLock(SkillId.Hiding);
            if (LockButton(256, 270, "잠행", sk.GetLock(SkillId.Stealth)))
                sk.CycleLock(SkillId.Stealth);
            if (LockButton(180, 270, "자물쇠", sk.GetLock(SkillId.Lockpicking)))
                sk.CycleLock(SkillId.Lockpicking);
            if (LockButton(104, 292, "지식", sk.GetLock(SkillId.AnimalLore)))
                sk.CycleLock(SkillId.AnimalLore);
            if (LockButton(256, 292, "수의", sk.GetLock(SkillId.Veterinary)))
                sk.CycleLock(SkillId.Veterinary);
            if (LockButton(180, 314, "조련", sk.GetLock(SkillId.AnimalTaming)))
                sk.CycleLock(SkillId.AnimalTaming);
            if (LockButton(28, 314, "각인", sk.GetLock(SkillId.Inscription)))
                sk.CycleLock(SkillId.Inscription);
            if (LockButton(256, 336, "독", sk.GetLock(SkillId.Poisoning)))
                sk.CycleLock(SkillId.Poisoning);
            if (LockButton(104, 336, "감지", sk.GetLock(SkillId.DetectHidden)))
                sk.CycleLock(SkillId.DetectHidden);
            if (LockButton(180, 358, "야영", sk.GetLock(SkillId.Camping)))
                sk.CycleLock(SkillId.Camping);
            if (LockButton(256, 380, "훔치기", sk.GetLock(SkillId.Stealing)))
                sk.CycleLock(SkillId.Stealing);
            if (LockButton(28, 138, "STR", st.GetLock(StatId.Str)))
                st.CycleLock(StatId.Str);
            if (LockButton(118, 138, "DEX", st.GetLock(StatId.Dex)))
                st.CycleLock(StatId.Dex);
            if (LockButton(208, 138, "INT", st.GetLock(StatId.Int)))
                st.CycleLock(StatId.Int);

            var me = world.Player;
            if (me == null)
                return;
            if (world.ActiveVendor != null && !me.Ghost)
            {
                GUI.Box(new Rect(16, 250, 340, 184), "");
                GUI.Label(new Rect(28, 258, 320, 22), "잡화  " + world.ActiveVendor.DisplayName);
                if (GUI.Button(new Rect(28, 284, 100, 24), "곡괭이 25"))
                    Shop(net, true, ItemCatalog.Pickaxe);
                if (GUI.Button(new Rect(134, 284, 100, 24), "도끼 25"))
                    Shop(net, true, ItemCatalog.Hatchet);
                if (GUI.Button(new Rect(240, 284, 90, 24), "낚싯대 25"))
                    Shop(net, true, ItemCatalog.FishingPole);
                if (GUI.Button(new Rect(28, 314, 100, 24), "광석팔기"))
                    Shop(net, false, "iron_ore");
                if (GUI.Button(new Rect(134, 314, 100, 24), "나무팔기"))
                    Shop(net, false, "wood");
                if (GUI.Button(new Rect(240, 314, 90, 24), "생선팔기"))
                    Shop(net, false, ItemCatalog.Fish);
                if (GUI.Button(new Rect(28, 344, 100, 24), "붕대 5"))
                    Shop(net, true, ItemCatalog.Bandage);
                if (GUI.Button(new Rect(134, 344, 100, 24), "천 3"))
                    Shop(net, true, ItemCatalog.Cloth);
                if (GUI.Button(new Rect(240, 344, 44, 24), "시약"))
                    Shop(net, true, "resin");
                if (GUI.Button(new Rect(28, 374, 100, 24), "류트 20"))
                    Shop(net, true, ItemCatalog.Lute);
                if (GUI.Button(new Rect(134, 374, 100, 24), "자물쇠 8"))
                    Shop(net, true, ItemCatalog.Lockpick);
                if (GUI.Button(new Rect(286, 374, 44, 24), "닫기"))
                    world.CloseVendor();
            }
            if (world.ActiveTrainer != null && !me.Ghost)
            {
                GUI.Box(new Rect(16, 390, 340, 214), "");
                GUI.Label(new Rect(28, 398, 220, 22), "훈련  5G / +1  상한 30");
                if (GUI.Button(new Rect(28, 424, 70, 24), "검술"))
                    Train(net, SkillId.Swordsmanship);
                if (GUI.Button(new Rect(104, 424, 70, 24), "채광"))
                    Train(net, SkillId.Mining);
                if (GUI.Button(new Rect(180, 424, 70, 24), "벌목"))
                    Train(net, SkillId.Lumberjacking);
                if (GUI.Button(new Rect(256, 424, 70, 24), "대장"))
                    Train(net, SkillId.Blacksmithing);
                if (GUI.Button(new Rect(28, 454, 70, 24), "마법"))
                    Train(net, SkillId.Magery);
                if (GUI.Button(new Rect(104, 454, 70, 24), SkillNames.KoreanOf(SkillId.Archery)))
                    Train(net, SkillId.Archery);
                if (GUI.Button(new Rect(180, 454, 70, 24), SkillNames.KoreanOf(SkillId.Parrying)))
                    Train(net, SkillId.Parrying);
                if (GUI.Button(new Rect(256, 454, 70, 24), SkillNames.KoreanOf(SkillId.Meditation)))
                    Train(net, SkillId.Meditation);
                if (GUI.Button(new Rect(28, 484, 90, 24), SkillNames.KoreanOf(SkillId.MagicResist)))
                    Train(net, SkillId.MagicResist);
                if (GUI.Button(new Rect(124, 484, 90, 24), SkillNames.KoreanOf(SkillId.EvaluateIntelligence)))
                    Train(net, SkillId.EvaluateIntelligence);
                if (GUI.Button(new Rect(220, 484, 70, 24), "낚시"))
                    Train(net, SkillId.Fishing);
                if (GUI.Button(new Rect(296, 484, 70, 24), "요리"))
                    Train(net, SkillId.Cooking);
                if (GUI.Button(new Rect(28, 514, 70, 24), "창술"))
                    Train(net, SkillId.Fencing);
                if (GUI.Button(new Rect(104, 514, 70, 24), "둔기"))
                    Train(net, SkillId.Mace);
                if (GUI.Button(new Rect(180, 514, 70, 24), "연금"))
                    Train(net, SkillId.Alchemy);
                if (GUI.Button(new Rect(256, 514, 70, 24), "추적"))
                    Train(net, SkillId.Tracking);
                if (GUI.Button(new Rect(28, 544, 70, 24), "음악"))
                    Train(net, SkillId.Musicianship);
                if (GUI.Button(new Rect(104, 544, 70, 24), "평화"))
                    Train(net, SkillId.Peacemaking);
                if (GUI.Button(new Rect(180, 544, 70, 24), "도발"))
                    Train(net, SkillId.Provocation);
                if (GUI.Button(new Rect(256, 544, 70, 24), "은신"))
                    Train(net, SkillId.Hiding);
                if (GUI.Button(new Rect(28, 574, 70, 24), "잠행"))
                    Train(net, SkillId.Stealth);
                if (GUI.Button(new Rect(104, 574, 70, 24), "자물쇠"))
                    Train(net, SkillId.Lockpicking);
                if (GUI.Button(new Rect(180, 574, 70, 24), "지식"))
                    Train(net, SkillId.AnimalLore);
                if (GUI.Button(new Rect(256, 574, 70, 24), "각인"))
                    Train(net, SkillId.Inscription);
                if (GUI.Button(new Rect(28, 604, 70, 24), "독"))
                    Train(net, SkillId.Poisoning);
                if (GUI.Button(new Rect(104, 604, 70, 24), "감지"))
                    Train(net, SkillId.DetectHidden);
                if (GUI.Button(new Rect(180, 604, 70, 24), "야영"))
                    Train(net, SkillId.Camping);
                if (GUI.Button(new Rect(256, 604, 70, 24), "훔치기"))
                    Train(net, SkillId.Stealing);
                if (GUI.Button(new Rect(256, 398, 70, 22), "닫기"))
                    world.CloseTrainer();
            }

            var locked = OfflineWorld.FindCrate("LockedCrate");
            if (locked != null && !me.Ghost
                && Vector3.Distance(me.transform.position, locked.transform.position) <= locked.InteractRange)
            {
                GUI.Box(new Rect(16, 430, 340, 40), "");
                GUI.Label(new Rect(28, 434, 180, 20), locked.Opened ? locked.DisplayName + " (열림)" : locked.DisplayName);
                if (!locked.Opened && GUI.Button(new Rect(220, 434, 110, 24), "따기"))
                {
                    if (net != null && net.IsClientInitialized)
                        net.RpcPick(locked.gameObject.name);
                    else
                        world.TryPick(me, locked);
                }
            }
            var mortar = OfflineWorld.FindStation("Mortar");
            if (mortar != null && !me.Ghost
                && Vector3.Distance(me.transform.position, mortar.transform.position) <= mortar.InteractRange)
            {
                GUI.Box(new Rect(16, 470, 340, 40), "");
                GUI.Label(new Rect(28, 474, 180, 20), mortar.DisplayName);
                if (GUI.Button(new Rect(220, 474, 110, 24), "회복물약"))
                    CraftAt(net, mortar, "health_potion");
            }
            var forge = OfflineWorld.FindStation("Forge");
            if (forge != null && !me.Ghost
                && Vector3.Distance(me.transform.position, forge.transform.position) <= forge.InteractRange)
            {
                GUI.Box(new Rect(16, 448, 340, 40), "");
                GUI.Label(new Rect(28, 452, 180, 20), forge.DisplayName);
                if (GUI.Button(new Rect(220, 452, 110, 24), "자물쇠"))
                    CraftAt(net, forge, "lockpick");
            }
            var carpenter = OfflineWorld.FindStation("Carpenter");
            if (carpenter != null && !me.Ghost
                && Vector3.Distance(me.transform.position, carpenter.transform.position) <= carpenter.InteractRange)
            {
                GUI.Box(new Rect(16, 510, 340, 80), "");
                GUI.Label(new Rect(28, 516, 320, 20), carpenter.DisplayName);
                if (GUI.Button(new Rect(28, 536, 100, 24), "나무활"))
                    CraftAt(net, carpenter, "wooden_bow");
                if (GUI.Button(new Rect(134, 536, 100, 24), "나무창"))
                    CraftAt(net, carpenter, "wooden_spear");
                if (GUI.Button(new Rect(240, 536, 100, 24), "나무곤봉"))
                    CraftAt(net, carpenter, "wooden_club");
                if (GUI.Button(new Rect(28, 562, 100, 24), "류트"))
                    CraftAt(net, carpenter, "lute");
            }
            DungeonGate near = NearestGate(me,
                Dungeon1.EntranceObject, Dungeon1.ExitObject,
                Dungeon2.EntranceObject, Dungeon2.ExitObject, Dungeon3.EntranceObject, Dungeon3.ExitObject);
            if (near != null && !me.Ghost)
            {
                GUI.Box(new Rect(16, 568, 340, 52), "");
                GUI.Label(new Rect(28, 574, 220, 20), near.DisplayName);
                if (GUI.Button(new Rect(28, 592, 100, 22), near.IsExit ? "나가기" : "들어가기"))
                {
                    if (net != null && net.IsClientInitialized)
                        net.RpcDungeon(near.gameObject.name);
                    else
                        world.TryDungeon(me, near);
                }
            }
            DrawParty(world, me, net);
            DrawGuild(world, me, net);
            DrawDuel(world, me, net);
            DrawGm(world, me);
            bool open = false;
            string otherName = "";
            string mineOffer = "";
            string theirs = "";
            bool mineOk = false;
            bool theirOk = false;
            if (TradeView.Open && net != null)
            {
                int myId = net.ObjectId;
                bool iAmA = myId == TradeView.IdA;
                bool iAmB = myId == TradeView.IdB;
                if (!iAmA && !iAmB)
                    return;
                open = true;
                otherName = iAmA ? TradeView.NameB : TradeView.NameA;
                mineOffer = iAmA ? TradeView.OfferA : TradeView.OfferB;
                theirs = iAmA ? TradeView.OfferB : TradeView.OfferA;
                mineOk = iAmA ? TradeView.AcceptA : TradeView.AcceptB;
                theirOk = iAmA ? TradeView.AcceptB : TradeView.AcceptA;
            }
            else if (world.ActiveTrade != null && (world.ActiveTrade.A == me || world.ActiveTrade.B == me))
            {
                open = true;
                var other = world.ActiveTrade.Other(me);
                otherName = other != null ? other.DisplayName : "?";
                mineOffer = me == world.ActiveTrade.A ? world.ActiveTrade.OfferA : world.ActiveTrade.OfferB;
                theirs = me == world.ActiveTrade.A ? world.ActiveTrade.OfferB : world.ActiveTrade.OfferA;
                mineOk = me == world.ActiveTrade.A ? world.ActiveTrade.AcceptA : world.ActiveTrade.AcceptB;
                theirOk = me == world.ActiveTrade.A ? world.ActiveTrade.AcceptB : world.ActiveTrade.AcceptA;
            }
            if (!open)
                return;
            GUI.Box(new Rect(16, 250, 340, 150), "");
            GUI.Label(new Rect(28, 258, 320, 22), "거래  " + otherName);
            GUI.Label(new Rect(28, 280, 320, 22), "나: " + Label(mineOffer) + (mineOk ? "  수락" : ""));
            GUI.Label(new Rect(28, 300, 320, 22), "상대: " + Label(theirs) + (theirOk ? "  수락" : ""));
            if (GUI.Button(new Rect(28, 326, 70, 24), "광석"))
                Offer(net, me, "iron_ore");
            if (GUI.Button(new Rect(104, 326, 70, 24), "철검"))
                Offer(net, me, "iron_sword");
            if (GUI.Button(new Rect(180, 326, 70, 24), "없음"))
                Offer(net, me, "");
            if (GUI.Button(new Rect(28, 356, 90, 28), "수락"))
            {
                if (net != null && net.IsClientInitialized)
                    net.RpcTradeAccept();
                else
                    world.ConfirmTrade(me);
            }
            if (GUI.Button(new Rect(126, 356, 90, 28), "취소"))
            {
                if (net != null && net.IsClientInitialized)
                    net.RpcTradeCancel();
                else
                    world.CancelTrade();
            }
        }

        void DrawGm(OfflineWorld world, WorldBody me)
        {
            if (!gmOpen && !Cli.Has("-ulon-gm"))
                return;
            if (me == null)
                return;
            GUI.Box(new Rect(370, 200, 280, 220), "");
            GUI.Label(new Rect(382, 206, 256, 20), PersistDriver.Frozen ? "GM  계정 정지됨" : "GM  (F1)");
            if (GUI.Button(new Rect(382, 230, 90, 24), "광장복구"))
                world.GmWarpPlaza(me);
            if (GUI.Button(new Rect(478, 230, 70, 24), "곡괭이"))
                world.GmGive(me, ItemCatalog.Pickaxe, 1);
            if (GUI.Button(new Rect(554, 230, 70, 24), "철검"))
                world.GmGive(me, ItemCatalog.IronSword, 1);
            if (GUI.Button(new Rect(382, 258, 90, 24), "검술+10"))
                world.GmSetSkill(me, SkillId.Swordsmanship, world.SkillsOf(me).Get(SkillId.Swordsmanship) + 10f);
            if (GUI.Button(new Rect(478, 258, 70, 24), "회수"))
                world.GmTake(me, "iron_ore");
            if (GUI.Button(new Rect(554, 258, 70, 24), "백업"))
                OpLog.Backup();
            if (GUI.Button(new Rect(382, 286, 90, 24), "스켈소환"))
                world.GmSpawnSkeleton();
            if (GUI.Button(new Rect(478, 286, 70, 24), "스켈삭제"))
                world.GmDespawnExtra();
            if (GUI.Button(new Rect(554, 286, 70, 24), PersistDriver.Frozen ? "해제" : "정지"))
            {
                bool next = !OpLog.IsFrozen(PersistDriver.AccountKey());
                OpLog.Freeze(PersistDriver.AccountKey(), next);
                PersistDriver.Frozen = next;
            }
            string[] logs = OpLog.Recent(3);
            string logLine = logs.Length == 0 ? "(로그 없음)" : logs[logs.Length - 1];
            if (logLine.Length > 42)
                logLine = logLine.Substring(logLine.Length - 42);
            GUI.Label(new Rect(382, 318, 256, 90), logLine);
        }


        void DrawDuel(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            if (me == null || world == null)
                return;
            GUI.Box(new Rect(370, 360, 280, 70), "");
            GUI.Label(new Rect(382, 366, 256, 18), "결투");
            var foe = world.Selected;
            var pal = GameObject.Find("Companion");
            var palBody = pal != null ? pal.GetComponent<WorldBody>() : null;
            var duelFoe = foe != null && foe != me && foe.IsAvatar && !foe.IsEnemy ? foe : palBody;
            if (me.DuelOpponent == null)
            {
                if (duelFoe != null && duelFoe != me && GUI.Button(new Rect(382, 388, 100, 22), "결투 초대"))
                {
                    var nob = duelFoe.GetComponent<FishNet.Object.NetworkObject>();
                    if (net != null && net.IsClientInitialized && nob != null)
                        net.RpcDuelInvite(nob);
                    else
                        world.TryDuelInvite(me, duelFoe);
                }
                bool pendingMe = false;
                var bodies = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i] != null && bodies[i].PendingDuel == me)
                    {
                        pendingMe = true;
                        break;
                    }
                }
                if (pendingMe && GUI.Button(new Rect(488, 388, 70, 22), "결투수락"))
                {
                    if (net != null && net.IsClientInitialized)
                        net.RpcDuelAccept();
                    else
                        world.TryDuelAccept(me);
                }
            }
            else
            {
                GUI.Label(new Rect(382, 388, 160, 20), "결투 중 " + (me.DuelOpponent.DisplayName ?? ""));
                if (GUI.Button(new Rect(488, 386, 50, 22), "항복"))
                {
                    if (net != null && net.IsClientInitialized)
                        net.RpcDuelYield();
                    else
                        world.TryDuelYield(me);
                }
                if (GUI.Button(new Rect(542, 386, 50, 22), "종료"))
                {
                    if (net != null && net.IsClientInitialized)
                        net.RpcDuelEnd();
                    else
                        world.TryDuelEnd(me);
                }
            }
        }


        void DrawParty(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            var party = world.ActiveParty;
            bool netOpen = PartyView.Open && (net == null || !net.IsServerInitialized);
            GUI.Box(new Rect(370, 16, 280, 170), "");
            GUI.Label(new Rect(382, 22, 256, 20), "파티");
            if (party == null && !netOpen)
            {
                if (PartyView.PendingMe)
                {
                    if (GUI.Button(new Rect(382, 46, 80, 24), "수락"))
                    {
                        if (net != null && net.IsClientInitialized)
                            net.RpcPartyAccept();
                        else
                            world.TryPartyAccept(me);
                    }
                    return;
                }
                var pal = GameObject.Find("Companion");
                var palBody = pal != null ? pal.GetComponent<WorldBody>() : null;
                if (palBody != null && GUI.Button(new Rect(382, 46, 120, 24), "동료 초대"))
                {
                    var nob = pal.GetComponent<FishNet.Object.NetworkObject>();
                    if (net != null && net.IsClientInitialized && nob != null)
                        net.RpcPartyInvite(nob);
                    else
                        world.TryPartyInvite(me, palBody);
                }
                return;
            }
            string roster = "";
            if (party != null)
            {
                if (party.Leader != null)
                    roster = party.Leader.DisplayName + " " + party.Leader.Hp.ToString("0") + "/" + party.Leader.MaxHp.ToString("0");
                for (int i = 0; i < party.Members.Count; i++)
                {
                    var m = party.Members[i];
                    if (m == null)
                        continue;
                    roster += "  " + m.DisplayName + " " + m.Hp.ToString("0") + "/" + m.MaxHp.ToString("0");
                }
            }
            else
                roster = PartyView.Roster.Replace("\n", "  ");
            GUI.Label(new Rect(382, 44, 256, 36), roster);
            string chat = party != null && party.Chat.Count > 0 ? party.Chat[party.Chat.Count - 1] : PartyView.Chat;
            GUI.Label(new Rect(382, 82, 256, 22), chat);
            partyChat = GUI.TextField(new Rect(382, 106, 160, 22), partyChat ?? "");
            if (GUI.Button(new Rect(548, 106, 40, 22), "말"))
            {
                if (net != null && net.IsClientInitialized)
                    net.RpcPartySay(partyChat);
                else
                    world.TryPartySay(me, partyChat);
                partyChat = "";
            }
            if (GUI.Button(new Rect(382, 134, 70, 24), "탈퇴"))
            {
                if (net != null && net.IsClientInitialized)
                    net.RpcPartyLeave();
                else
                    world.TryPartyLeave(me);
            }
            if (party != null && party.Pending == me && GUI.Button(new Rect(458, 134, 70, 24), "수락"))
                world.TryPartyAccept(me);
        }


        void DrawGuild(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            var guild = me != null ? world.GuildOf(me) : null;
            bool netOpen = GuildView.Open && (net == null || !net.IsServerInitialized);
            GUI.Box(new Rect(370, 190, 280, 140), "");
            GUI.Label(new Rect(382, 196, 256, 20), "길드");
            if (guild == null && !netOpen && (me == null || string.IsNullOrEmpty(me.GuildId)))
            {
                if (GuildView.PendingMe)
                {
                    if (GUI.Button(new Rect(382, 220, 80, 24), "수락"))
                    {
                        if (net != null && net.IsClientInitialized)
                            net.RpcGuildAccept();
                        else
                            world.TryGuildAccept(me);
                    }
                    return;
                }
                guildNameInput = GUI.TextField(new Rect(382, 220, 120, 22), guildNameInput ?? "");
                if (GUI.Button(new Rect(508, 220, 70, 22), "창설"))
                {
                    if (net != null && net.IsClientInitialized)
                        net.RpcGuildCreate(guildNameInput);
                    else
                        world.TryGuildCreate(me, guildNameInput);
                }
                return;
            }
            string name = guild != null ? guild.Name : GuildView.GuildName;
            string roster = "";
            if (guild != null)
            {
                if (guild.Leader != null)
                    roster = guild.Leader.DisplayName;
                for (int i = 0; i < guild.Members.Count; i++)
                {
                    var m = guild.Members[i];
                    if (m == null)
                        continue;
                    roster += "  " + m.DisplayName;
                }
            }
            else
                roster = GuildView.Roster.Replace("\n", "  ");
            string war = guild != null && !string.IsNullOrEmpty(guild.WarWithId)
                ? (world.FindGuild(guild.WarWithId) != null ? world.FindGuild(guild.WarWithId).Name : guild.WarWithId)
                : GuildView.WarName;
            GUI.Label(new Rect(382, 218, 256, 36), "[" + name + "] " + roster + (string.IsNullOrEmpty(war) ? "" : "  전쟁 " + war));
            var pal = GameObject.Find("Companion");
            var palBody = pal != null ? pal.GetComponent<WorldBody>() : null;
            if (palBody != null && guild != null && guild.Leader == me && GUI.Button(new Rect(382, 256, 100, 24), "동료 초대"))
            {
                var nob = pal.GetComponent<FishNet.Object.NetworkObject>();
                if (net != null && net.IsClientInitialized && nob != null)
                    net.RpcGuildInvite(nob);
                else
                    world.TryGuildInvite(me, palBody);
            }
            if (GUI.Button(new Rect(488, 256, 70, 24), "탈퇴"))
            {
                if (net != null && net.IsClientInitialized)
                    net.RpcGuildLeave();
                else
                    world.TryGuildLeave(me);
            }
            if (guild != null && guild.Pending == me && GUI.Button(new Rect(564, 256, 70, 24), "수락"))
                world.TryGuildAccept(me);
            var foe = world.Selected;
            if (guild != null && guild.Leader == me && foe != null && foe != me && world.GuildOf(foe) != null && world.GuildOf(foe) != guild
                && string.IsNullOrEmpty(guild.WarWithId) && GUI.Button(new Rect(382, 282, 100, 22), "선전포고"))
            {
                var nob = foe.GetComponent<FishNet.Object.NetworkObject>();
                if (net != null && net.IsClientInitialized && nob != null)
                    net.RpcGuildWarDeclare(nob);
                else
                    world.TryGuildWarDeclare(me, foe);
            }
            if (guild != null && guild.Leader == me && !string.IsNullOrEmpty(guild.WarWithId) && GUI.Button(new Rect(488, 282, 70, 22), "강화"))
            {
                if (net != null && net.IsClientInitialized)
                    net.RpcGuildWarPeace();
                else
                    world.TryGuildWarPeace(me);
            }
        }

        static string RecoveryLine(WorldBody me)
        {
            if (me == null)
                return "";
            if (me.Ghost)
            {
                var healer = GameObject.Find("Healer");
                if (healer == null)
                    return "유령 · 치유사에서 부활";
                float hd = Vector3.Distance(me.transform.position, healer.transform.position);
                return "유령 · 치유사 " + Compass(me.transform.position, healer.transform.position) + " " + hd.ToString("0") + "m";
            }
            var corpse = OfflineWorld.FindCorpse(PersistDriver.AccountKey());
            if (corpse == null)
                return "";
            float d = Vector3.Distance(me.transform.position, corpse.transform.position);
            return "시체 " + Compass(me.transform.position, corpse.transform.position) + " " + d.ToString("0") + "m · " + corpse.SecondsLeft.ToString("0") + "초";
        }

        static string Compass(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            if (d.sqrMagnitude < 0.25f)
                return "여기";
            float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (ang < 0f)
                ang += 360f;
            int oct = (int)((ang + 22.5f) / 45f) % 8;
            if (oct == 0) return "북";
            if (oct == 1) return "북동";
            if (oct == 2) return "동";
            if (oct == 3) return "남동";
            if (oct == 4) return "남";
            if (oct == 5) return "남서";
            if (oct == 6) return "서";
            return "북서";
        }

        static bool LockButton(float x, float y, string name, SkillLock state)
        {
            return GUI.Button(new Rect(x, y, 86, 24), name + " " + SkillLockMarks.Glyph(state));
        }

        static string Label(string id) => string.IsNullOrEmpty(id) ? "(없음)" : id;

        static void Offer(NetAvatar net, WorldBody me, string template)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTradeOffer(template);
            else
                OfflineWorld.Instance?.SetTradeOffer(me, template);
        }

        static void Cast(NetAvatar net, SpellId spell)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCast((int)spell);
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryCast(OfflineWorld.Instance.Player, spell, OfflineWorld.Instance.Selected);
        }

        static void Mark(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcMark();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryMark(OfflineWorld.Instance.Player);
        }

        static void Recall(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcRecall();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryRecall(OfflineWorld.Instance.Player);
        }

        static void Meditate(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcMeditate();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryMeditate(OfflineWorld.Instance.Player);
        }

        static void Evaluate(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcEvaluate();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryEvaluate(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void Track(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTrack();
            else if (OfflineWorld.Instance != null)
            {
                var world = OfflineWorld.Instance;
                if (world.Selected != null)
                    world.TryTrack(world.Player, world.Selected);
                else
                    world.TryTrackCorpse(world.Player, OfflineWorld.FindCorpse(world.Player != null ? world.Player.CharacterId : ""));
            }
        }

        static void Lore(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcLore();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryLore(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void Vet(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcVet();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryVet(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void Inscribe(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcInscribe();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryInscribe(OfflineWorld.Instance.Player);
        }

        static void PoisonWeapon(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPoisonWeapon();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPoisonWeapon(OfflineWorld.Instance.Player);
        }

        static void UseScroll(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcUseScroll();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryUseScroll(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void PlayLute(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPlay();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPlay(OfflineWorld.Instance.Player);
        }

        static void Peace(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPeace();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPeace(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void Provoke(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcProvoke();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryProvokeStep(OfflineWorld.Instance.Player);
        }

        static void Hide(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcHide();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryHide(OfflineWorld.Instance.Player);
        }

        static void Stealth(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcStealth();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryStealth(OfflineWorld.Instance.Player);
        }


        static void Camp(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCamp();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryCamp(OfflineWorld.Instance.Player);
        }

        static void DetectHidden(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcDetectHidden();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryDetectHidden(OfflineWorld.Instance.Player);
        }

        static void Steal(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcSteal();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TrySteal(OfflineWorld.Instance.Player);
        }

        static void ResurrectBandage(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcResurrectBandage();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Selected;
                if (tgt == null || !tgt.Ghost || !tgt.IsAvatar || tgt == me)
                    tgt = OfflineWorld.NearestGhostAvatar(me);
                OfflineWorld.Instance.TryResurrectBandage(me, tgt);
            }
        }

        static void AcceptCraftOrder(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryAcceptOrder(OfflineWorld.Instance.Player);
        }

        static void TurnInCraftOrder(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryTurnInOrder(OfflineWorld.Instance.Player);
        }

        static void CurePoison(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCurePoison();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Selected;
                if (tgt == null || tgt.IsEnemy || !tgt.Alive || tgt.Ghost)
                    tgt = me;
                OfflineWorld.Instance.TryCurePoison(me, tgt);
            }
        }


        static void PetAttack(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            WorldBody pet = null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == me.CharacterId)
                {
                    pet = b;
                    break;
                }
            }
            WorldBody enemy = OfflineWorld.Instance.Selected;
            if (enemy == null || !enemy.IsEnemy || !enemy.Alive || enemy.IsAvatar)
            {
                enemy = null;
                float best = TameResolve.AttackRange;
                for (int i = 0; i < list.Length; i++)
                {
                    var b = list[i];
                    if (b == null || b == me || !b.IsEnemy || !b.Alive || b.IsAvatar)
                        continue;
                    float d = Vector3.Distance(me.transform.position, b.transform.position);
                    if (d > best)
                        continue;
                    best = d;
                    enemy = b;
                }
            }
            OfflineWorld.Instance.TryPetAttack(me, pet, enemy);
        }


        static void SpeakKeyword(NetAvatar net, string text)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            OfflineWorld.Instance.TrySpeechKeyword(me, text);
        }

        static void PetCome(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            WorldBody pet = null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == me.CharacterId)
                {
                    pet = b;
                    break;
                }
            }
            if (pet == null)
                return;
            OfflineWorld.Instance.TryPetCome(me, pet);
        }

        static void Pick(NetAvatar net, LockedCrate crate)
        {
            if (crate == null || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcPick(crate.gameObject.name);
            else
                OfflineWorld.Instance.TryPick(OfflineWorld.Instance.Player, crate);
        }

        static void Drink(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcDrink();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryDrink(OfflineWorld.Instance.Player);
        }

        static void Bandage(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcHeal();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Selected;
                if (tgt != null && tgt.Ghost && tgt.IsAvatar && tgt != me)
                {
                    OfflineWorld.Instance.TryResurrectBandage(me, tgt);
                    return;
                }
                if (tgt == null || tgt.IsEnemy || !tgt.Alive)
                    tgt = me;
                OfflineWorld.Instance.TryHeal(me, tgt);
            }
        }

        static void Train(NetAvatar net, SkillId skill)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTrain((int)skill);
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryTrain(OfflineWorld.Instance.Player, skill);
        }

        static void CraftAt(NetAvatar net, CraftStation station, string recipeId)
        {
            if (station == null || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
            {
                net.RpcCraft(station.gameObject.name, recipeId);
                return;
            }
            OfflineWorld.Instance.TryCraft(OfflineWorld.Instance.Player, station, recipeId);
        }

        static void Shop(NetAvatar net, bool buy, string template)
        {
            if (net != null && net.IsClientInitialized)
            {
                if (buy) net.RpcBuy(template);
                else net.RpcSell(template);
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            if (buy)
                OfflineWorld.Instance.TryBuy(OfflineWorld.Instance.Player, template);
            else
                OfflineWorld.Instance.TrySell(OfflineWorld.Instance.Player, template);
        }

        void DrawCreate()
        {
            GUI.Box(new Rect(16, 16, 460, 520), "");
            GUI.Label(new Rect(28, 24, 430, 22), "캐릭터 생성  (직업 선택 없음)");
            GUI.Label(new Rect(28, 50, 80, 22), "이름");
            createName = GUI.TextField(new Rect(110, 48, 200, 24), createName ?? "");
            GUI.Label(new Rect(28, 80, 80, 22), "외형");
            if (GUI.Button(new Rect(110, 78, 80, 24), createAppear == 0 ? "[기사]" : "기사"))
                createAppear = 0;
            if (GUI.Button(new Rect(196, 78, 90, 24), createAppear == 1 ? "[민머리]" : "민머리"))
                createAppear = 1;
            if (GUI.Button(new Rect(292, 78, 80, 24), createAppear == 2 ? "[야만]" : "야만"))
                createAppear = 2;

            int leftStat = CharacterCreate.StatTotal - createStr - createDex - createInt;
            GUI.Label(new Rect(28, 112, 400, 22), "스탯 총합 " + CharacterCreate.StatTotal + "  남은 " + leftStat);
            DrawStat(28, 136, "STR", ref createStr, leftStat);
            DrawStat(28, 164, "DEX", ref createDex, leftStat);
            DrawStat(28, 192, "INT", ref createInt, leftStat);

            float leftSkill = CharacterCreate.SkillTotal - createAv - createBv - createCv;
            GUI.Label(new Rect(28, 228, 400, 22), "시작 스킬 3개 총합 " + CharacterCreate.SkillTotal + "  남은 " + leftSkill.ToString("0"));
            DrawSkillPick(28, 256, ref createA, ref createAv, createB, createC, leftSkill);
            DrawSkillPick(28, 284, ref createB, ref createBv, createA, createC, leftSkill);
            DrawSkillPick(28, 312, ref createC, ref createCv, createA, createB, leftSkill);

            if (!string.IsNullOrEmpty(createError))
                GUI.Label(new Rect(28, 350, 420, 40), createError);
            if (GUI.Button(new Rect(28, 400, 140, 32), "시작"))
                SubmitCreate();
        }

        void DrawStat(float x, float y, string label, ref int value, int remaining)
        {
            GUI.Label(new Rect(x, y, 50, 22), label);
            if (GUI.Button(new Rect(x + 54, y, 28, 22), "-") && value > CharacterCreate.StatMin)
                value--;
            GUI.Label(new Rect(x + 88, y, 40, 22), value.ToString());
            if (GUI.Button(new Rect(x + 128, y, 28, 22), "+") && remaining > 0 && value < CharacterCreate.StatEachMax)
                value++;
        }

        void DrawSkillPick(float x, float y, ref SkillId id, ref float value, SkillId otherA, SkillId otherB, float remaining)
        {
            if (GUI.Button(new Rect(x, y, 24, 22), "<"))
                id = NextSkill(id, otherA, otherB, -1);
            GUI.Label(new Rect(x + 28, y, 88, 22), SkillNames.KoreanOf(id));
            if (GUI.Button(new Rect(x + 118, y, 24, 22), ">"))
                id = NextSkill(id, otherA, otherB, 1);
            if (GUI.Button(new Rect(x + 150, y, 28, 22), "-") && value > 1f)
                value -= 5f;
            if (value < 1f)
                value = 1f;
            GUI.Label(new Rect(x + 184, y, 40, 22), value.ToString("0"));
            if (GUI.Button(new Rect(x + 224, y, 28, 22), "+") && remaining >= 5f && value + 5f <= CharacterCreate.SkillEachMax)
                value += 5f;
        }

        static SkillId NextSkill(SkillId current, SkillId skipA, SkillId skipB, int dir)
        {
            int n = (int)SkillId.Count;
            int i = (int)current;
            for (int step = 0; step < n; step++)
            {
                i = (i + dir + n) % n;
                var id = (SkillId)i;
                if (id != skipA && id != skipB)
                    return id;
            }
            return current;
        }

        void SubmitCreate()
        {
            var picks = new[] { createA, createB, createC };
            var values = new[] { createAv, createBv, createCv };
            createError = CharacterCreate.Validate(createName, createStr, createDex, createInt, picks, values);
            if (createError != null)
                return;
            var snap = CharacterCreate.Build(PersistDriver.AccountKey(), createName, createAppear, createStr, createDex, createInt, picks, values);
            PersistDriver.Commit(snap);
            var world = OfflineWorld.Instance;
            if (world != null && world.Player != null)
                OutfitSwap.ApplyLook(world.Player.transform, createAppear);
            lookApplied = true;
            createError = "";
        }
        static DungeonGate NearestGate(WorldBody me, params string[] names)
        {
            DungeonGate near = null;
            float best = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                var g = OfflineWorld.FindGate(names[i]);
                if (g == null)
                    continue;
                float d = Vector3.Distance(me.transform.position, g.transform.position);
                if (d > g.InteractRange)
                    continue;
                if (near == null || d < best)
                {
                    near = g;
                    best = d;
                }
            }
            return near;
        }

    }
}
