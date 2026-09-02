using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class ResourceNode : MonoBehaviour
    {
        public string ResourceId = "iron_ore";
        public string DisplayName = "철 광맥";
        public SkillId GatherSkill = SkillId.Mining;
        public int Remaining = 12;
        public int Capacity;
        public float RespawnSeconds = 8f;
        public float Difficulty = 10f;
        public float InteractRange = 2.4f;
        public float ReadyAt;

        void OnEnable() => EnsureCapacity();

        public void EnsureCapacity()
        {
            if (Capacity <= 0)
                Capacity = Remaining > 0 ? Remaining : 12;
        }

        public void Tick(float now)
        {
            EnsureCapacity();
            if (Remaining > 0)
            {
                SetPresent(true);
                return;
            }
            if (now < ReadyAt)
            {
                SetPresent(false);
                return;
            }
            Remaining = Capacity;
            SetPresent(true);
        }

        public void AfterTake(float now)
        {
            if (Remaining > 0)
                return;
            ReadyAt = now + RespawnSeconds;
            SetPresent(false);
        }

        void SetPresent(bool present)
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                rends[i].enabled = present;
            var cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = present;
        }
    }
}
