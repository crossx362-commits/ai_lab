using UnityEngine;

namespace Ulon.Server
{
    public sealed class CraftStation : MonoBehaviour
    {
        public string RecipeId = "iron_sword";
        public string DisplayName = "대장간";
        public float InteractRange = 2.4f;
    }

    public sealed class BankStation : MonoBehaviour
    {
        public string DisplayName = "은행";
        public float InteractRange = 2.4f;
    }

    public sealed class HealerStation : MonoBehaviour
    {
        public string DisplayName = "치유사";
        public float InteractRange = 2.8f;
    }

    public sealed class VendorStation : MonoBehaviour
    {
        public string DisplayName = "잡화";
        public float InteractRange = 2.4f;
    }

    public sealed class TrainerStation : MonoBehaviour
    {
        public string DisplayName = "훈련사";
        public float InteractRange = 2.4f;
        public float Cap = 30f;
        public int Cost = 5;
    }

    public sealed class CorpseNode : MonoBehaviour
    {
        public string CorpseId;
        public string OwnerId;
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
