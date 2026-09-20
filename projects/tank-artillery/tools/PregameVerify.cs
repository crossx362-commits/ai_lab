using System;
using Tankfall.Sim;
static class PregameVerify
{
    static void Check(bool ok,string label) { if(!ok) throw new Exception(label); }
    static void Main()
    {
        var c=new MatchCountdown(); c.Begin(30);
        Check(c.Active && c.Remaining==30,"30 second start");
        Check(!c.Tick(29.9f) && c.Active,"must not end early");
        float left=c.Remaining; Check(!c.Tick(0) && c.Remaining==left,"pause preserves time");
        Check(c.Tick(.2f) && !c.Active && c.Remaining==0,"expiry transitions once");
        Check(!c.Tick(100),"no duplicate transition");
        c.Begin(45); Check(c.Active && c.Remaining==45,"selection restart");
        bool bad=false; try { c.Tick(-1); } catch(ArgumentOutOfRangeException) { bad=true; }
        Check(bad,"reject negative time");
        float max=0;
        for(float t=0;t<2;t+=.005f) max=MathF.Max(max,FlightMotion.Offset(new Vec3(50,35,0),t,1).Length);
        Check(max < 1.5f,"missile lateral control must be restrained, not a 4m corkscrew");
        Console.WriteLine("PREGAME_AND_MISSILE_PASS");
    }
}
