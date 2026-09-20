using System;
using System.Collections.Generic;
using UnityEngine;
using Tankfall.Sim;
namespace Tankfall.View
{
    public sealed partial class BattleDemo
    {
        readonly MatchCountdown _selectClock=new MatchCountdown(), _readyClock=new MatchCountdown();
        readonly Dictionary<int,Vector3> _deploymentOrigins=new Dictionary<int,Vector3>();
        bool _pregameSelfTest;
        int _pregameTestFrame;
        const float DeploymentRadius=25f;

        void BeginSelection()
        {
            _screen=GameScreen.TankSelect;
            _selectClock.Begin(MatchCountdown.SelectionSeconds);
        }
        void CompleteSelection()
        {
            for(int i=0;_picked.Count<MapHeightFunction.TeamSize;i++)
            {
                var k=(TankKind)(i%TankStats.Count);
                if(!_picked.Contains(k) || i>=TankStats.Count) _picked.Add(k);
            }
            _roster=_picked.ToArray(); _screen=GameScreen.Setup; _setupSel=0;
        }
        void BeginDeployment()
        {
            ClearPreview();
            _readyClock.Begin(MatchCountdown.DeploymentSeconds);
            _deploymentOrigins.Clear();
            foreach(var u in _units) _deploymentOrigins[u.Id]=u.Pos;
            _turn=_units.FindIndex(u=>u.Team==0 && u.Alive);
            _phase=Phase.Prepare; _charging=false;
            _camDist=28f; _camPitch=25f;
            _log="배치 준비 — WASD 이동 · Tab 아군 변경 · Enter 준비 완료";
        }
        void CycleDeploymentUnit()
        {
            for(int i=1;i<=_units.Count;i++)
            {
                int at=(_turn+i)%_units.Count;
                if(_units[at].Team==0 && _units[at].Alive) { _turn=at; return; }
            }
        }
        void DeploymentStep(float dt)
        {
            if(Input.GetKeyDown(KeyCode.Tab)) CycleDeploymentUnit();
            var u=Current; var before=u.Pos;
            u.Gauge=MoveGaugeMax;
            PlayerMove(dt);
            var displacement=u.Pos-_deploymentOrigins[u.Id]; displacement.y=0;
            if(displacement.magnitude>DeploymentRadius) { u.Root.position=before; GroundUnit(u,dt); }
            if(_readyClock.Tick(dt) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) FinishDeployment();
            UpdateCamera(dt); TickFx(dt);
        }
        void FinishDeployment()
        {
            Sfx.Engine(false);
            foreach(var u in _units) u.Gauge=MoveGaugeMax;
            _turn=0; _phase=Phase.Move; _phaseTimer=MovePhaseSec;
            _power=0; _charging=false; _battleClock=0; _turnMark=0;
            _log="전투 시작! 이동 후 Space로 조준하세요";
            Debug.Log("[Pregame] DEPLOYMENT_COMPLETE round="+_round);
        }
        void DrawDeployment(float W,float H)
        {
            DrawTankHpBars(); CasualCrew(Current); DrawMiniMap(W,H);
            var banner=new Rect(W*.5f-250,14,500,96); GuiArt.Panel(banner,0,13);
            Ui.Text(new Rect(banner.x,23,500,33),$"배치 준비  {Mathf.CeilToInt(_readyClock.Remaining):00}",25,Ui.Power,TextAnchor.MiddleCenter,true);
            Ui.Text(new Rect(banner.x+20,65,460,26),"안전한 위치를 잡으세요 · 준비 중에는 발사할 수 없어요",13,Ui.Ink,TextAnchor.MiddleCenter);
            Ui.Bar(new Rect(banner.x+24,banner.yMax-14,452,5),_readyClock.Remaining/MatchCountdown.DeploymentSeconds,Ui.Gauge);
            var deck=new Rect(W*.5f-310,H-120,620,102); GuiArt.Panel(deck,0,12);
            GuiArt.Portrait(new Rect(deck.x+5,deck.y-16,132,120),Current.Kind);
            Ui.Text(new Rect(deck.x+142,deck.y+12,450,25),TankStats.Get(Current.Kind).Name+" · 시작 위치에서 25m 안쪽",17,Ui.Ink,TextAnchor.MiddleLeft,true);
            Ui.Text(new Rect(deck.x+142,deck.y+42,450,24),"W / S 이동   A / D 회전   Tab 아군 변경",14,Ui.Dim);
            Ui.Text(new Rect(deck.x+142,deck.y+70,450,22),"Enter 준비 완료 · 30초가 지나면 자동 시작",13,Ui.Power);
        }
        void PregameSelfTestStep()
        {
            try
            {
                if(_pregameTestFrame==0) { _picked.Clear(); _picked.Add(TankKind.Missile); BeginSelection(); }
                if(_pregameTestFrame==10) Shot("00_선택시간");
                if(_pregameTestFrame==20)
                {
                    _picked.Clear(); _picked.Add(TankKind.Missile); BeginSelection();
                    if(!_selectClock.Tick(45)) throw new Exception("selection expiry");
                    CompleteSelection();
                    if(_screen!=GameScreen.Setup || _roster.Length!=MapHeightFunction.TeamSize || _roster[0]!=TankKind.Missile) throw new Exception("selection fill");
                    _practice=false; StartBattle();
                    if(_phase!=Phase.Prepare || _readyClock.Remaining!=30) throw new Exception("deployment entry");
                    var u=Current; var before=u.Pos;
                    for(int i=0;i<15;i++) DriveUnit(u,1,1f/30f);
                    if(Vector3.Distance(before,u.Pos)<.1f) throw new Exception("deployment cannot move");
                    int first=u.Id; CycleDeploymentUnit();
                    if(Current.Team!=0 || Current.Id==first) throw new Exception("deployment switch");
                    float left=_readyClock.Remaining;
                    _screen=GameScreen.Pause; ScreenUpdate();
                    if(_readyClock.Remaining!=left) throw new Exception("pause advanced clock");
                    _screen=GameScreen.Battle;
                    UpdateCamera(0);
                }
                if(_pregameTestFrame==35) Shot("00_배치준비");
                if(_pregameTestFrame==50)
                {
                    if(!_readyClock.Tick(30)) throw new Exception("deployment timeout");
                    FinishDeployment();
                    if(_phase!=Phase.Move || _round!=1 || _charging || _battleClock!=0) throw new Exception("battle entry");
                    foreach(var u in _units) if(u.Gauge!=MoveGaugeMax) throw new Exception("movement budget reset");
                }
                if(_pregameTestFrame==65) Shot("01_전투시작");
                if(_pregameTestFrame++>=80) { Debug.Log("[Pregame] PASS selection timeout/autofill, deploy movement/switch/pause/timeout, battle reset"); Application.Quit(0); }
            }
            catch(Exception e) { Debug.LogException(e); Application.Quit(1); }
        }
    }
}
