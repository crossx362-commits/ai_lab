using System;
namespace Tankfall.Sim
{
    /// <summary>Caller supplies elapsed active time; menus and pauses never advance this clock.</summary>
    public sealed class MatchCountdown
    {
        public const float SelectionSeconds=45f, DeploymentSeconds=30f;
        public bool Active { get; private set; }
        public float Remaining { get; private set; }
        public void Begin(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Remaining=seconds; Active=true;
        }
        public bool Tick(float dt)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0) throw new ArgumentOutOfRangeException(nameof(dt));
            if(!Active) return false;
            Remaining=Math.Max(0,Remaining-dt);
            if(Remaining>0) return false;
            Active=false; return true;
        }
    }
}
