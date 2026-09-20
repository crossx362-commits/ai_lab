using System;
using System.Collections.Generic;
using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    // Image atlas generated for this game; all labels and values remain live text.
    public static class GuiArt
    {
        static Texture2D _atlas, _title, _logo, _skin;
        static readonly Dictionary<TankKind, Texture2D> Portraits = new Dictionary<TankKind, Texture2D>();
        static readonly Rect[] Regions = {
            new Rect(35,39,270,247), new Rect(345,40,261,249), new Rect(650,40,263,249), new Rect(959,41,260,248),
            new Rect(31,332,275,273), new Rect(334,333,276,271), new Rect(627,415,315,116), new Rect(959,339,267,255),
            new Rect(34,650,258,250), new Rect(323,662,287,241), new Rect(626,664,309,240), new Rect(962,646,265,244),
            new Rect(30,936,275,270), new Rect(333,953,275,240), new Rect(647,936,275,262), new Rect(949,962,287,231)
        };
        static Texture2D Atlas => _atlas != null ? _atlas : (_atlas = Required("casual-atlas"));
        static Texture2D Required(string name)
        {
            var t = Resources.Load<Texture2D>("Gui/" + name);
            if (t == null) throw new InvalidOperationException("GUI image missing: " + name);
            return t;
        }
        static Rect Uv(Rect pixels)
            => new Rect(pixels.x / 1254f, 1f - pixels.yMax / 1254f, pixels.width / 1254f, pixels.height / 1254f);
        static void Patch(Rect target, Rect pixels)
        {
            if (target.width <= 0f || target.height <= 0f) return;
            GUI.DrawTextureWithTexCoords(target, Atlas, Uv(pixels));
        }
        public static void Icon(Rect r, int index)
        {
            var src=Regions[index]; float scale=Mathf.Min(r.width/src.width,r.height/src.height);
            Patch(new Rect(r.center.x-src.width*scale*.5f,r.center.y-src.height*scale*.5f,src.width*scale,src.height*scale),src);
        }
        public static void Panel(Rect r, int index=0, float rim=12f)
        {
            if(_skin==null) _skin=Required("story-skin");
            int cell=index==2?2:index==1||index==3?3:0;
            Rect[] cells={new Rect(40,46,575,566),new Rect(639,46,575,566),new Rect(40,637,575,575),new Rect(639,637,575,575)};
            var s=cells[cell]; float sourceEdge=105f;
            float d=Mathf.Min(rim,Mathf.Min(r.width,r.height)*.30f);
            float[] sx={s.x,s.x+sourceEdge,s.xMax-sourceEdge,s.xMax};
            float[] sy={s.y,s.y+sourceEdge,s.yMax-sourceEdge,s.yMax};
            float[] dx={r.x,r.x+d,r.xMax-d,r.xMax};
            float[] dy={r.y,r.y+d,r.yMax-d,r.yMax};
            for(int y=0;y<3;y++) for(int x=0;x<3;x++)
            {
                var uv=new Rect(sx[x]/_skin.width,1f-sy[y+1]/_skin.height,(sx[x+1]-sx[x])/_skin.width,(sy[y+1]-sy[y])/_skin.height);
                GUI.DrawTextureWithTexCoords(new Rect(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),_skin,uv);
            }
        }

        public static void Portrait(Rect r, TankKind kind)
        {
            if (!Portraits.TryGetValue(kind,out var t)) { t=Required("Portraits/"+kind); Portraits[kind]=t; }
            GUI.DrawTexture(r,t,ScaleMode.ScaleToFit,true);
        }
        public static void Title(Rect r)
        {
            if(_title==null) _title=Required("title-garden");
            GUI.DrawTexture(r,_title,ScaleMode.ScaleAndCrop,true);
        }
        public static void Logo(Rect r)
        {
            if(_logo==null) _logo=Required("title-logo");
            GUI.DrawTexture(r,_logo,ScaleMode.ScaleToFit,true);
        }
        public static void VerifyAssets()
        {
            if(Atlas.width!=1254 || Atlas.height!=1254) throw new InvalidOperationException("GUI atlas layout changed");
            Required("title-garden"); Required("title-logo");
            var skin=Required("story-skin");
            if(skin.width!=1254 || skin.height!=1254) throw new InvalidOperationException("Story skin atlas layout changed");
            foreach(TankKind k in Enum.GetValues(typeof(TankKind))) Required("Portraits/"+k);
        }
    }
}
