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
        /// <summary>
        /// **골드는 서버가 정한다**(축 ③). 필드였을 때는 클라 프로세스의 오프라인 폴백이 제 손으로
        /// 구매·통행료·길드 창설비를 차감했다 — 값을 동기화해도 **정하는 쪽이 클라면 §417 위반**이다.
        /// 받아 적는 자리(`ApplyNetworkState`)는 이 문을 지나지 않고 필드에 직접 쓴다.
        /// </summary>
        public int Gold
        {
            get => gold;
            set
            {
                if (EconomyAuthority.Refuse("골드 변경 " + gold + " → " + value))
                    return;
                gold = value;
            }
        }
        int gold;
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
                    // **죽은 사람의 계정으로 시체를 남긴다.** 예전엔 `PersistDriver.AccountKey()`를
                    // 썼는데 그건 **이 프로세스의 계정**이라, 서버에서는 누가 죽든 시체가 전부
                    // 서버 계정(`playloop-verify`) 앞으로 쌓였다 — 남의 시체를 제 것으로 찾는다
                    // (2026-09-08 축 ② 실측에서 발견). 계정 원장은 `AccountOf` 하나다.
                    OfflineWorld.Instance.HandleDeath(this, OfflineWorld.AccountOf(this));
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
            ApplyVisibility();
        }

        /// <summary>죽어서 사라져야 하는 몸(유령이 아닌 몹)을 화면·물리에서 숨긴다.</summary>
        void ApplyVisibility()
        {
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

        /// <summary>
        /// **서버가 내려준 값을 그대로 얹는다 — 죽음 처리는 하지 않는다**(축 ②, 2026-09-08).
        ///
        /// `SetHp`를 그냥 쓰면 클라가 제 손으로 `HandleDeath`를 돌려 **시체를 하나 더 만들고
        /// persist에 쓴다** — 받아 적어야 할 쪽이 세계를 바꾸는 것이다(원장: 재는 자·비추는 자가
        /// 세계를 바꾸면 안 된다). 죽음의 결과(유령·시체)는 서버가 만들어 따로 내려보낸다.
        /// </summary>
        public void ApplyNetworkState(float hpValue, float maxHpValue, bool ghostValue, int goldValue)
        {
            gold = goldValue;                            // 문을 지나지 않는다 — 이건 서버가 시킨 것이다
            MaxHp = maxHpValue;
            Hp = Mathf.Clamp(hpValue, 0f, Mathf.Max(1f, maxHpValue));
            Ghost = ghostValue;
            ApplyVisibility();
        }

        public void Resurrect()
        {
            Ghost = false;
            ResetHp();
            SetMana(MaxMana);
        }
    }
}
