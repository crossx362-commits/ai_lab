using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class WorldBody : MonoBehaviour
    {
        public bool IsEnemy;
        public bool IsAvatar;
        public string MobId;
        public string CharacterId;
        public string AccountId;
        public string DisplayName = "대상";
        public int Appearance;
        public float MaxHp = 30f;
        public float MaxMana = 35f;
        public bool Ghost;
        public int Gold;
        public int Fame;
        public int Karma;
        public int Notoriety;
        public int MurderCount;
        public float CriminalUntil;
        public float CalmUntil;
        public float ProvokeUntil;
        public WorldBody ProvokePartner;
        public float HiddenUntil;
        public float CampSafeUntil;
        public float StealthUntil;
        public bool Tameable;
        public int ControlSlots = 1;
        public string OwnerCharacterId = "";
        public bool PetFollow;
        public bool PetGuard;
        public WorldBody PetAttackTarget;
        public bool PetStabled;
        public bool Bonded;
        public bool HasMark;
        public string GuildId = "";
        public string GuildName = "";
        public float MarkX;
        public float MarkZ;
        public float CombatUntil;
        public float CastingUntil;
        public SpellId PendingSpell;
        public WorldBody PendingCastTarget;
        public WorldBody DuelOpponent;
        public WorldBody PendingDuel;
        public int PoisonTicks;
        public float NextPoisonAt;
        public float WardUntil;
        public float RootUntil;
        public float WeakenUntil;
        public float BlessUntil;
        public string ActiveCraftOrder = "";

        public float Hp { get; private set; }
        public float Mana { get; private set; }
        public bool Alive => Hp > 0f && !Ghost;
        public bool IsHidden(float now) => now < HiddenUntil;
        public bool IsCampSafe(float now) => now < CampSafeUntil;
        public bool IsWarded(float now) => now < WardUntil;
        public bool IsRooted(float now) => now < RootUntil;
        public bool IsWeakened(float now) => now < WeakenUntil;
        public bool IsBlessed(float now) => now < BlessUntil;
        public bool CanMoveHidden(float now) => now < StealthUntil && IsHidden(now);
        public bool InCombat(float now) => now < CombatUntil;
        public bool IsCasting(float now) => CastingUntil > 0f && now < CastingUntil;
        public void ClearCast()
        {
            CastingUntil = 0f;
            PendingCastTarget = null;
        }
        public void BreakHide()
        {
            HiddenUntil = 0f;
            StealthUntil = 0f;
        }

        public void ApplyMobCatalog()
        {
            if (string.IsNullOrEmpty(MobId) || !MobCatalog.TryGet(MobId, out MobDefinition definition))
                return;
            DisplayName = definition.DisplayName;
            MaxHp = definition.MaxHp;
        }

        public void RecalcFromStr(int strength)
        {
            MaxHp = StatSet.MaxHpOf(strength);
            if (Hp > 0f && Hp > MaxHp)
                SetHp(MaxHp);
        }

        public void RecalcFromInt(int intelligence)
        {
            MaxMana = StatSet.MaxManaOf(intelligence);
            if (Mana > MaxMana)
                SetMana(MaxMana);
            if (Mana <= 0f && !Ghost)
                SetMana(MaxMana);
        }

        public void SetMana(float value)
        {
            Mana = Mathf.Clamp(value, 0f, MaxMana);
        }

        public void ResetHp() => SetHp(MaxHp);

        public void ApplyDamage(int amount)
        {
            if (!Alive)
                return;
            SetHp(Hp - amount);
        }

        public void SetHp(float value)
        {
            Hp = Mathf.Max(0f, value);
            bool died = IsAvatar && Hp <= 0f && !Ghost;
            if (died)
            {
                Ghost = true;
                if (OfflineWorld.Instance != null)
                    OfflineWorld.Instance.HandleDeath(this, PersistDriver.AccountKey());
            }
            // Bonded pet: Ghost remains (no corpse loot). Unbonded mob/pet just hides.
            bool petBondDeath = !IsAvatar && Bonded && !string.IsNullOrEmpty(OwnerCharacterId)
                && Hp <= 0f && !Ghost;
            if (petBondDeath)
            {
                Ghost = true;
                PetFollow = false;
                PetGuard = false;
                PetAttackTarget = null;
            }
            bool hide = Hp <= 0f && !IsAvatar && !Ghost;
            var cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = !hide;
            var cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = !hide;
            var rends = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
                rends[i].enabled = !hide;
        }

        public void Resurrect()
        {
            Ghost = false;
            ResetHp();
            SetMana(MaxMana);
        }
    }
}
