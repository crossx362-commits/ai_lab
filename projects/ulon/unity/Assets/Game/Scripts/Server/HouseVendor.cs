using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class HouseVendor : MonoBehaviour
    {
        public string PlotId = HousingPlot.Id;
        public string DisplayName = "주택 상점";
        public float InteractRange = HousingPlot.InteractRange;
    }
}
