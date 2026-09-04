using UnityEngine;

namespace Ulon.Server
{
    public sealed class DungeonGate : MonoBehaviour
    {
        public string DungeonId = "dungeon1";
        public bool IsExit;
        public string DisplayName = "던전 입구";
        public float InteractRange = 2.6f;
    }
}
