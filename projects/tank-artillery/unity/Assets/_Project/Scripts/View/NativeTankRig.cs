using UnityEngine;

namespace Tankfall.View
{
    /// <summary>Persistent prefab rig: usable in scenes and by the match spawner.</summary>
    [DisallowMultipleComponent]
    public sealed class NativeTankRig : MonoBehaviour
    {
        public Transform Turret, Barrel, FirePoint;
        public Transform[] Wheels;
        public Renderer[] Body, Tracks, Team, Wood, Snow;
        public bool Hover;
        public float WheelRadius;

        void Awake() { ConfigureDrive(); }
        public void ConfigureDrive()
        {
            var drive=GetComponent<TankDrive>();
            if(drive==null) drive=gameObject.AddComponent<TankDrive>();
            drive.Hover=Hover; drive.WheelRadius=WheelRadius;
            drive.Wheels.Clear();
            if(Wheels!=null) drive.Wheels.AddRange(Wheels);
        }
        static void Paint(Renderer[] renderers, Material material)
        {
            if(renderers==null || material==null) return;
            foreach(var renderer in renderers) if(renderer!=null) renderer.sharedMaterial=material;
        }
        public void Bind(Material body, Material tracks, Material team, Material wood, Material snow)
        {
            Paint(Body,body); Paint(Tracks,tracks); Paint(Team,team); Paint(Wood,wood); Paint(Snow,snow);
            // 원화 재질을 보존하고 전용 Team 렌더러에만 팀색을 주입한다.
            if (Team != null && team != null)
            {
                var block = new MaterialPropertyBlock();
                foreach (var renderer in Team) if (renderer != null)
                {
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_Color", team.color);
                    renderer.SetPropertyBlock(block);
                }
            }
            if(Snow!=null) foreach(var renderer in Snow) if(renderer!=null) renderer.gameObject.SetActive(snow!=null);
            ConfigureDrive();
        }
    }
}
