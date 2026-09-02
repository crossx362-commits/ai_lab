using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class WorldBody : MonoBehaviour
    {
        public bool IsEnemy;
        public bool IsAvatar;
        public string MobId;
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

        public float Hp { get; private set; }
        public float Mana { get; private set; }
        public bool Alive => Hp > 0f && !Ghost;

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
            bool hide = Hp <= 0f && !IsAvatar;
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
