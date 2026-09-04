using UnityEngine;

namespace Ulon.Server
{
    public sealed class CorpseNode : MonoBehaviour
    {
        public string CorpseId;
        public string OwnerId;
        public string LastKind = "";
        public float LastX;
        public float LastY;
        public float LastZ;
        public float InteractRange = 2.4f;
        public float DecaySeconds = 900f;
        public float SpawnedAt;
        public readonly System.Collections.Generic.List<Ulon.Shared.ItemRecord> Items = new System.Collections.Generic.List<Ulon.Shared.ItemRecord>();

        public float SecondsLeft
        {
            get
            {
                float left = DecaySeconds - (Time.time - SpawnedAt);
                return left > 0f ? left : 0f;
            }
        }
    }
}
