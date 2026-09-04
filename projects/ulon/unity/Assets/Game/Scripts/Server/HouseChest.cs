using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class HouseChest : MonoBehaviour
    {
        public string PlotId = HousingPlot.Id;
        public string DisplayName = "주택 상자";
        public float InteractRange = HousingPlot.InteractRange;
    }
}
