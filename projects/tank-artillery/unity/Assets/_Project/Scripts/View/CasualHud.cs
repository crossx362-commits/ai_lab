using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public sealed partial class BattleDemo
    {
        void CasualBattleHud(Unit u,float W,float H)
        {
            if(!_gallery) DrawTankHpBars();
            DrawPopups();
            // Keep the arena clear: small crew cards above, one illustrated console below.
            HudTopBar(W);
            CasualCrew(u);
            HudPhaseTimer(W);
            if(!_practice && !_gallery) { DrawTurnOrder(W); DrawMiniMap(W,H); }
            var deck=new Rect(12,H-184,W-24,174);
            GuiArt.Panel(new Rect(deck.x,deck.y,250,deck.height),0,14);
            GuiArt.Panel(new Rect(270,deck.y,W-282,deck.height),0,14);
            float y=deck.y;
            GuiArt.Icon(new Rect(24,y+15,116,116),4);
            GuiArt.Portrait(new Rect(10,y-6,145,145),u.Kind);
            Ui.Text(new Rect(26,y+125,116,24),TankStats.Get(u.Kind).Name,15,Ui.Ink,TextAnchor.MiddleCenter,true);
            Ui.Text(new Rect(26,y+148,116,18),u.Team==0?"우리 팀":"상대 팀",12,u.Team==0?Ui.Ally:Ui.Enemy,TextAnchor.MiddleCenter,true);

            var dial=new Rect(153,y+24,89,89);
            GuiArt.Icon(dial,5);
            // Dial's top is zero; use the same elevation value as the live shot.
            Ui.Arrow(dial.center,90f-u.BarrelPitch,70,5,Ui.Power);
            Ui.Text(new Rect(154,y+112,89,28),$"{u.BarrelPitch:F0}°",20,Ui.Power,TextAnchor.MiddleCenter,true);
            Ui.Text(new Rect(149,y+142,99,18),$"D {(_order!=null?_order.Accumulated(u.Id):0)}  ·  방위 {u.TurretYaw:F0}°",10,Ui.Dim,TextAnchor.MiddleCenter);

            float right=W-324, gx=282, gw=right-gx-22;
            bool firing=IsPlayerTurn && _phase==Phase.Fire;
            string phase=firing?(_charging?"놓으면 발사!":"Space를 누르고 파워를 모으세요"):
                IsPlayerTurn && _phase==Phase.Move?"이동 중  ·  Space로 조준 시작":
                _phase==Phase.Flying?"포탄이 날아가고 있어요!":"상대의 차례예요";
            Ui.Text(new Rect(gx,y+16,gw,22),phase,15,Ui.Ink,TextAnchor.MiddleLeft,true);
            float bx=gx+72,bw=gw-78;
            void Gauge(string name,float at,Color color,float gy,string value,bool power=false)
            {
                Ui.Text(new Rect(gx,gy,69,25),name,12,color,TextAnchor.MiddleLeft,true);
                var bar=new Rect(bx,gy,bw,25);
                Ui.Fill(bar,Ui.Slot);
                Ui.Frame(bar,new Color(.30f,.43f,.63f,.6f));
                var inside=new Rect(bar.x+7,bar.y+4,bar.width-14,bar.height-8);
                Ui.Bar(inside,at,color);
                if(power) { Ui.Ticks(inside,10,Ui.Shadow); Ui.Tick(inside,_mark,Ui.Mark,3); }
                Ui.Text(bar,value,12,Ui.Ink,TextAnchor.MiddleCenter,true);
            }
            Gauge("체력",u.HpFrac,Ui.Good,y+46,$"{u.Hp} / {u.HpMax}");
            Gauge("파워",firing?_power:0f,firing&&_power>.92f?Ui.Bad:Ui.Power,y+80,firing?$"{_power*100:F0}%":"준비",true);
            Gauge("이동",u.Gauge/MoveGaugeMax,Ui.Gauge,y+114,$"{Mathf.Max(0,u.Gauge):F0}");
            Ui.Text(new Rect(gx,y+140,gw,18),"방향키 조준   Space 발사   Q / E 표시점",12,Ui.Dim,TextAnchor.MiddleCenter);

            string[] labels={"일반탄","특수탄","SS","궁극"}; int[] icons={8,9,10,7};
            bool ss=u.Skill.CanSs(), ult=u.Skill.CanUltimate();
            for(int i=0;i<4;i++)
            {
                bool sel=i==0?u.Shell==ShellKind.Normal:i==1?u.Shell==ShellKind.Special&&!_useSs&&!_useUlt:i==2?_useSs:_useUlt;
                var card=new Rect(right+i*73,y+14,67,70);
                bool ready=i<2||(i==2?ss:ult);
                Ui.Choice(card,sel,ready);
                GuiArt.Icon(new Rect(card.x+15,card.y+6,38,38),icons[i]);
                Ui.Text(new Rect(card.x+7,card.y+5,16,16),(i+1).ToString(),10,Ui.Ink,TextAnchor.MiddleLeft,true);
                Ui.Text(new Rect(card.x+3,card.y+44,card.width-6,20),labels[i],12,Ui.Ink,TextAnchor.MiddleCenter,true);
            }
            var fx=ShellEffects.Of(u.Kind,ShellKind.Special);
            string description=u.Shell==ShellKind.Normal?"기본 포탄 · 폭발로 지형을 파괴해요":fx.EffectDesc;
            Ui.TextWrap(new Rect(right+3,y+90,286,38),description,11,Ui.Ink);
            Ui.Bar(new Rect(right+5,y+131,280,10),u.Skill.Points/(float)NiceShot.UltimateCost,ult?Ui.Power:Ui.Gauge);
            Ui.Ticks(new Rect(right+5,y+131,280,10),NiceShot.UltimateCost,Ui.Shadow);
            Ui.Text(new Rect(right+3,y+147,286,20),ult?"궁극 준비 완료!  [4]":ss?"SS 준비 완료!  [3]":$"나이스샷 {u.Skill.Points}/{NiceShot.UltimateCost}",12,Ui.Power,TextAnchor.MiddleCenter,true);

            // Range is real target distance, never an impact prediction.
            var target=AimTarget(u,u.Heading+u.TurretYaw);
            string range="방향키로 상대를 겨눠 보세요";
            if(target!=null)
            {
                var d=target.Pos-u.Pos;
                range=$"상대 {target.Id%MapHeightFunction.TeamSize+1}  ·  거리 {new Vector2(d.x,d.z).magnitude:F0}m  ·  높이 {d.y:+0;-0;0}m";
            }
            var distance=new Rect(W*.5f-225,H-218,450,28);
            GuiArt.Panel(distance,3,8);
            Ui.Text(distance,range,13,Ui.Ink,TextAnchor.MiddleCenter,true);
            if(!string.IsNullOrEmpty(_log) && !_log.StartsWith("전투 개시"))
            {
                var eventLine=new Rect(W*.5f-310,H-244,620,22);
                GuiArt.Panel(eventLine,3,6);
                Ui.Text(eventLine,_log,11,Ui.Ink,TextAnchor.MiddleCenter);
            }
            DrawStatusIcons(u,new Rect(gx,y+159,gw,12));
            if(_itemSlots>0 && u.Team==0) CasualItems(u,W);
            HudNiceFlash(W,H);
        }

        void CasualCrew(Unit current)
        {
            var panel=new Rect(10,10,242,44+MapHeightFunction.TeamSize*2*24);
            GuiArt.Panel(panel,3,10);
            Ui.Text(new Rect(24,17,210,20),"출격 편대",13,Ui.Ink,TextAnchor.MiddleLeft,true);
            float y=42;
            for(int teamIndex=0;teamIndex<2;teamIndex++) foreach(var u in _units)
            {
                if(_gallery) break;
                if(u.Team!=teamIndex) continue;
                Color team=u.Team==0?Ui.Ally:Ui.Enemy;
                var row=new Rect(21,y,218,23);
                if(u==current) GuiArt.Panel(row,3,5);
                GuiArt.Portrait(new Rect(21,y-5,36,32),u.Kind);
                Ui.Text(new Rect(59,y,91,22),$"{(u.Team==0?"아":"적")}{u.Id%MapHeightFunction.TeamSize+1} {TankStats.Get(u.Kind).Name}",11,u.Alive?Ui.Ink:Ui.Dim,TextAnchor.MiddleLeft,u==current);
                Ui.Bar(new Rect(154,y+8,54,8),u.Alive?u.HpFrac:0,team);
                if(_items.HasShield(u.Id)) Ui.Frame(new Rect(153,y+7,56,10),Ui.Gauge);
                Ui.Text(new Rect(208,y,29,22),u.Alive?$"{u.Hp}":"OUT",9,Ui.Ink,TextAnchor.MiddleRight);
                if(u.Alive) DrawRowStatus(u,row);
                y+=24;
            }
        }
        void CasualItems(Unit u,float W)
        {
            var bag=_items.Bag(u.Id);
            var r=new Rect(W-240,10,230,98+bag.Count*22);
            GuiArt.Panel(r,3,10);
            GuiArt.Icon(new Rect(r.x+12,r.y+9,28,28),11);
            Ui.Text(new Rect(r.x+45,r.y+11,165,22),"도움 아이템",13,Ui.Ink,TextAnchor.MiddleLeft,true);
            if(bag.Count==0) Ui.Text(new Rect(r.x+17,r.y+39,190,20),"아직 없어요",12,Ui.Dim);
            for(int i=0;i<bag.Count;i++)
            {
                var row=new Rect(r.x+13,r.y+39+i*22,204,21);
                if(i==_itemSel) GuiArt.Panel(row,3,5);
                Ui.Text(new Rect(row.x+7,row.y,row.width-14,row.height),Items.Get(bag[i]).Name,12,i==_itemSel?Ui.Power:Ui.Ink);
            }
            if(bag.Count>0) Ui.TextWrap(new Rect(r.x+17,r.yMax-58,196,32),Items.Get(bag[Mathf.Clamp(_itemSel,0,bag.Count-1)]).Desc,11,Ui.Dim);
            Ui.Text(new Rect(r.x+14,r.yMax-24,202,18),"[ / ] 선택   Enter 사용",10,Ui.Dim,TextAnchor.MiddleCenter);
        }
    }
}
