using UnityEngine;

namespace Ulon.Server
{
    public sealed class LockedCrate : MonoBehaviour
    {
        public string DisplayName = "잠긴 상자";
        public float InteractRange = 2.4f;
        public bool Opened;
        public int GoldLoot = 8;
        public int ClothLoot = 1;
    }
}
