using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    /// <summary>World ground drop with DecayAt. House lockdown/secure items are not GroundItems.</summary>
    public sealed class GroundItem : MonoBehaviour
    {
        public string GroundId;
        public ItemRecord Item;
        /// <summary>Absolute Time.time when this ground drop is removed.</summary>
        public float DecayAt;
        public float InteractRange = 2.4f;

        public float SecondsLeft
        {
            get
            {
                float left = DecayAt - Time.time;
                return left > 0f ? left : 0f;
            }
        }

        public bool Expired(float now) => now >= DecayAt;
    }
}
