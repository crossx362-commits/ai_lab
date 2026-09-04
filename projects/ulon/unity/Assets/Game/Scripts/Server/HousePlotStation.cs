using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class HousePlotStation : MonoBehaviour
    {
        public string PlotId = HousingPlot.Id;
        public string DisplayName = "주택 부지";
        public float InteractRange = HousingPlot.InteractRange;
    }
}
