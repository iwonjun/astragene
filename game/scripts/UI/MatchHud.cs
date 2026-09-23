using Godot;
using System;
using System.Collections.Generic;
using RtsGame.Bridge;
using RtsGame.View;
using RtsGame.Net;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;

namespace RtsGame.UI;
public partial class MatchHud : CanvasLayer
{
    private readonly record struct Card(string Label,CommandType Type,int Definition=-1,bool Target=false,int Menu=0);
    private MatchBridge _bridge=null!;
    private SelectionController _selection=null!;
    private RtsCamera _camera=null!;
    private Control _root=null!,_pause=null!,_keyEditor=null!;
    private Label _resources=null!,_title=null!,_stats=null!,_notice=null!,_chatLog=null!;
    private GridContainer _units=null!;
    private ScrollContainer _unitScroll=null!;
    private ulong _selectionSignature;
    private Portrait _portrait=null!;
    private Minimap _minimap=null!;
    private LineEdit _chat=null!;
    private readonly Button[] _buttons=new Button[12],_queue=new Button[5],_keyButtons=new Button[12];
    private readonly Card?[] _cards=new Card?[12];
    private readonly Hotkeys _keys=new();
    private Card? _target;
    private int _menu,_editingKey=-1;
    private bool _teamChat,_surrendered;
    private float _refresh,_noticeTime,_shownOre=450,_shownPlasma;
    private readonly List<string> _messages=new();
    private readonly Dictionary<EntityId,long> _buildingHealth=new();
    private ulong _lastAttackAlert;
    private AudioStreamPlayer _alertSound=null!;
    public event Action<string,bool>? ChatRequested;
    public string NoticeText=>_notice.Text;
    public override void _Ready()
    {
        _bridge=GetNode<MatchBridge>("../Bridge");_selection=GetNode<SelectionController>("../Selection");_camera=GetNode<RtsCamera>("../Camera");
        _root=GetNode<Control>("Root");_pause=_root.GetNode<Control>("Pause");_keyEditor=_root.GetNode<Control>("KeyEditor");_pause.Hide();_keyEditor.Hide();
        _resources=_root.GetNode<Label>("Top/Resources");_resources.AddThemeFontOverride("font",GD.Load<Font>("res://game/assets/fonts/RobotoMono.ttf"));
        _title=_root.GetNode<Label>("SelectionPanel/Title");_stats=_root.GetNode<Label>("SelectionPanel/Stats");_portrait=_root.GetNode<Portrait>("SelectionPanel/Portrait");_units=_root.GetNode<GridContainer>("SelectionPanel/UnitScroll/Units");_unitScroll=_root.GetNode<ScrollContainer>("SelectionPanel/UnitScroll");
        _notice=_root.GetNode<Label>("Notice");_chatLog=_root.GetNode<Label>("ChatLog");_chat=_root.GetNode<LineEdit>("ChatInput");_minimap=_root.GetNode<Minimap>("MapPanel/Minimap");
        for(int i=0;i<12;i++){int slot=i;_buttons[i]=_root.GetNode<Button>($"CommandPanel/Slot{i}");_buttons[i].Pressed+=()=>ActivateSlot(slot);_keyButtons[i]=_keyEditor.GetNode<Button>($"Key{i}");_keyButtons[i].Pressed+=()=>{_editingKey=slot;_keyButtons[slot].Text="키를 누르세요";};}
        for(int i=0;i<5;i++){int slot=i;_queue[i]=_root.GetNode<Button>($"SelectionPanel/Queue{i}");_queue[i].Pressed+=()=>CancelQueue(slot);}
        _root.GetNode<Button>("Top/Menu").Pressed+=TogglePause;_pause.GetNode<Button>("Resume").Pressed+=TogglePause;
        _pause.GetNode<Button>("Keys").Pressed+=()=>{_keyEditor.Show();RefreshKeys();};_keyEditor.GetNode<Button>("Save").Pressed+=()=>{_keys.Save();_keyEditor.Hide();_editingKey=-1;RefreshCards();};
        _pause.GetNode<Button>("Surrender").Pressed+=()=>{_bridge.IsPaused=false;_pause.Hide();_bridge.Issue(new Command(CommandType.Surrender,_bridge.LocalPlayer,EntityId.None,EntityId.None,Fix2.Zero));};
        _chat.TextSubmitted+=text=>{if(!string.IsNullOrWhiteSpace(text)){ReceiveChat(_bridge.LocalPlayer,text,_teamChat);ChatRequested?.Invoke(text,_teamChat);}_chat.Clear();_chat.Hide();_chat.ReleaseFocus();};
        _pause.GetNode<Button>("Quit").Pressed+=()=>{_bridge.IsPaused=false;GetTree().ChangeSceneToFile("res://game/scenes/lobby.tscn");};
        if(GetNodeOrNull<LockstepRunner>("../Net") is LockstepRunner runner){ChatRequested+=runner.SendChat;runner.ChatReceived+=ReceiveChat;}
        _alertSound=new AudioStreamPlayer{Stream=GD.Load<AudioStream>("res://game/assets/generated/alert.tres"),VolumeDb=-16};AddChild(_alertSound);
        if(Array.IndexOf(OS.GetCmdlineUserArgs(),"--render-benchmark")>=0 || Array.IndexOf(OS.GetCmdlineUserArgs(),"--art-capture")>=0){Hide();SetProcess(false);SetProcessUnhandledInput(false);return;}
        Refresh();
    }
    public override void _Process(double delta)
    {
        _root.Scale=GetViewport().GetVisibleRect().Size/new Vector2(1440,900);
        _camera.ControlsEnabled=!_chat.Visible&&!_pause.Visible&&!_keyEditor.Visible;
        _refresh-=(float)delta;if(_refresh<=0){Refresh();_refresh=0.1f;}
        var r=_bridge.View.Resources;ResourceDelta(r.Ore,r.Plasma);float speed=1-Mathf.Exp(-(float)delta*12);_shownOre=Mathf.Lerp(_shownOre,r.Ore,speed);_shownPlasma=Mathf.Lerp(_shownPlasma,r.Plasma,speed);
        _resources.Text=$"ORE {Mathf.RoundToInt(_shownOre),5}   PLASMA {Mathf.RoundToInt(_shownPlasma),4}   SUPPLY {r.UsedSupply} / {r.MaxSupply}";
        _resources.Modulate=r.UsedSupply>=r.MaxSupply?new Color(1,0.5f,0.45f):Colors.White;
        if(_noticeTime>0){_noticeTime-=(float)delta;_notice.Modulate=new Color(1,1,1,Mathf.Min(1,_noticeTime));}
    }
    public void Refresh()
    {
        _selection.Prune();
        var selected=_selection.Selected;
        bool single=selected.Count==1;_portrait.Visible=single;_stats.Visible=selected.Count<2;_unitScroll.Visible=selected.Count>1;
        _title.Text=selected.Count==0?"부대를 선택하세요":$"선택한 부대  /  {selected.Count}";
        ulong signature=14695981039346656037;foreach(var id in selected){signature=unchecked((signature^(uint)id.Index)*1099511628211);signature=unchecked((signature^id.Generation)*1099511628211);}bool rebuild=signature!=_selectionSignature;_selectionSignature=signature;
        if(rebuild)foreach(var child in _units.GetChildren()){_units.RemoveChild(child);child.QueueFree();}
        if(single)
        {
            var e=_bridge.View.Get(selected[0]);string name=e.Type.IsBuilding?DefDatabase.Buildings[e.Type.Definition].Name:DefDatabase.Units[e.Type.Definition].Name;
            _title.Text=name+"  /  "+(e.Type.Definition>=(e.Type.IsBuilding?6:5)?"VERGE":"LUMINA");
            _stats.Text=$"체력 {e.Health.Current.FloorToInt()} / {e.Health.Maximum.FloorToInt()}";
            if(!e.Type.IsBuilding){var d=DefDatabase.Units[e.Type.Definition];_stats.Text+=$"\n공격 {d.Damage}  방어 {d.Armor}  사거리 {d.Range}";}
            int construction=_bridge.View.ConstructionTicks(e.Id);if(construction>0)_stats.Text+=$"\n건설 중  {construction/20f:0.0}초";
            _portrait.Role=e.Type.Definition;_portrait.Building=e.Type.IsBuilding;_portrait.Accent=WorldRenderer.Team(e.Owner.Player);_portrait.QueueRedraw();
            for(int i=0;i<5;i++){var job=_bridge.View.ProductionAt(e.Id,i);_queue[i].Visible=job.Definition>=0;_queue[i].Text=job.Definition<0?"":$"{(job.Research?"연구":DefDatabase.Units[job.Definition].Name)}\n{job.RemainingTicks/20f:0.0}s";_queue[i].TooltipText="클릭: 취소 / 전액 환불";}
        }
        else
        {
            foreach(var q in _queue)q.Hide();
            if(rebuild)foreach(var id in selected){var e=_bridge.View.Get(id);string name=e.Type.IsBuilding?DefDatabase.Buildings[e.Type.Definition].Name:DefDatabase.Units[e.Type.Definition].Name;var button=new Button{Text=name.Substring(0,Math.Min(2,name.Length)),CustomMinimumSize=new Vector2(43,34),TooltipText=name,FocusMode=Control.FocusModeEnum.None};button.Pressed+=()=>{_selection.SelectOnly(id);Refresh();};_units.AddChild(button);}
            if(selected.Count==0)_stats.Text="드래그 선택 · 우클릭 명령\n일꾼으로 자원을 우클릭해 채집하세요.";
        }
        RefreshCards();
        while(_bridge.View.TryDequeueEvent(out var ev))
        {
            if(ev.Kind=="PlayerLeft")Notify($"P{ev.Entity.Index+1} 플레이어가 나갔습니다. 해당 유닛은 중립화됩니다.");
            if(ev.Kind=="Rejected")Notify("명령을 실행할 수 없습니다. 자원·보급·선행 건물을 확인하세요.");
            if(ev.Kind is "UnitComplete" or "UpgradeComplete" or "BuildComplete")Notify(ev.Kind=="UpgradeComplete"?"업그레이드 완료":"생산 / 건설 완료",false);
        }
        for(int i=0;i<_bridge.View.Capacity;i++)
        {
            var id=_bridge.View.IdAt(i);if(id==EntityId.None)continue;var e=_bridge.View.Get(id);if(!e.Type.IsBuilding || e.Owner.Player!=_bridge.LocalPlayer)continue;
            if(_buildingHealth.TryGetValue(id,out long hp)&&e.Health.Current.Raw<hp&&Time.GetTicksMsec()-_lastAttackAlert>2500){_lastAttackAlert=Time.GetTicksMsec();Notify("기지가 공격받고 있습니다!  Space: 이동");_minimap.AlertPosition=_bridge.SurfacePosition(e.Transform.Position);_minimap.AlertUntil=Time.GetTicksMsec()+4500;_lastEvent=_minimap.AlertPosition;}
            _buildingHealth[id]=e.Health.Current.Raw;
        }
        if(_bridge.View.Surrendered&&!_surrendered){_surrendered=true;_bridge.IsPaused=true;_pause.Show();_pause.GetNode<Label>("Title").Text="항복했습니다";_pause.GetNode<Button>("Surrender").Disabled=true;_pause.GetNode<Button>("Resume").Disabled=true;}
    }
    public int BuildingPreview=>_target is Card card && card.Type==CommandType.Build?card.Definition:-1;
    public MatchBridge Bridge=>_bridge;
    public SelectionController Selection=>_selection;
    private int _lastOre=450,_lastPlasma;
    private void ResourceDelta(int ore,int plasma)
    {
        if(ore==_lastOre && plasma==_lastPlasma)return;
        var label=new Label{Text=$"{(ore-_lastOre):+0;-0;0} Ore   {(plasma-_lastPlasma):+0;-0;0} Plasma",Position=new Vector2(520,58),Modulate=new Color(0.5f,1,0.88f),MouseFilter=Control.MouseFilterEnum.Ignore};
        _root.GetNode("Top").AddChild(label);var tween=CreateTween().SetParallel();tween.TweenProperty(label,"position",label.Position+new Vector2(0,22),0.9);tween.TweenProperty(label,"modulate:a",0.0,0.9);tween.Chain().TweenCallback(Callable.From(label.QueueFree));_lastOre=ore;_lastPlasma=plasma;
    }
    private Vector3 _lastEvent=new(20,0,20);
    private void RefreshCards()
    {
        Array.Clear(_cards);var list=_selection.Selected;
        if(list.Count>0)
        {
            var e=_bridge.View.Get(list[0]);int faction=e.Type.Definition>=(e.Type.IsBuilding?6:5)?1:0;
            if(_menu==1 && !e.Type.IsBuilding)
            {
                for(int i=0;i<6;i++){var b=DefDatabase.Buildings[faction*6+i];_cards[i]=new Card($"{b.Name}\n{b.Ore}O {b.Plasma}P",CommandType.Build,b.Id,true);}_cards[11]=new Card("뒤로",CommandType.Stop,Menu:2);
            }
            else if(e.Type.IsBuilding)
            {
                int slot=0;foreach(var unit in DefDatabase.Units)if(unit.Trainer==e.Type.Definition)_cards[slot++]=new Card($"{unit.Name}\n{unit.Ore}O {unit.Plasma}P",CommandType.Train,unit.Id);
                if(e.Type.Definition is 3 or 9)for(int kind=0;kind<3;kind++){int level=_bridge.View.UpgradeLevel(kind);if(level<3){var d=DefDatabase.Upgrades[kind*3+level];_cards[4+kind]=new Card($"{d.Name}\n{d.Ore}O {d.Plasma}P",CommandType.Research,d.Id);}}
                _cards[8]=new Card("집결 지점",CommandType.Rally,Target:true);_cards[11]=new Card("취소 / 환불",CommandType.Cancel);
            }
            else
            {
                _cards[0]=new Card("이동",CommandType.Move,Target:true);_cards[1]=new Card("공격 이동",CommandType.AttackMove,Target:true);_cards[2]=new Card("정지",CommandType.Stop);_cards[3]=new Card("위치 사수",CommandType.Hold);
                _cards[4]=new Card("순찰",CommandType.Patrol,Target:true);_cards[5]=new Card("따라가기",CommandType.Follow,Target:true);
                if(DefDatabase.Units[e.Type.Definition].Worker){_cards[6]=new Card("채집",CommandType.Gather,Target:true);_cards[7]=new Card("수리",CommandType.Repair,Target:true);_cards[8]=new Card("건설",CommandType.Build,Menu:1);}
            }
        }
        for(int i=0;i<12;i++){_buttons[i].Disabled=_cards[i]==null;_buttons[i].Text=_cards[i] is Card c?$"[{_keys[i]}] {c.Label}":"";}
    }
    public void ActivateSlot(int slot)
    {
        if(_cards[slot] is not Card card || _selection.Selected.Count==0)return;
        if(card.Menu!=0){_menu=card.Menu==1?1:0;RefreshCards();return;}
        if(card.Target){_target=card;Notify(card.Type==CommandType.Build?"건설 위치를 클릭하세요. 우클릭으로 취소합니다.":"목표 위치 또는 유닛을 클릭하세요.",false);return;}
        Send(card,Vector3.Zero,EntityId.None);
    }
    private void Send(Card card,Vector3 point,EntityId target)
    {
        if(card.Type==CommandType.Gather){if(target!=EntityId.None && _bridge.View.Get(target).Type.IsBuilding)point=_bridge.SurfacePosition(_bridge.View.Get(target).Transform.Position);card=card with {Definition=_bridge.ResourceNodeAt(point)};target=EntityId.None;}
        if(card.Type is CommandType.Build or CommandType.Train or CommandType.Research)
        {
            int ore,plasma,supply=0;var r=_bridge.View.Resources;
            if(card.Type==CommandType.Build){var d=DefDatabase.Buildings[card.Definition];ore=d.Ore;plasma=d.Plasma;}
            else if(card.Type==CommandType.Train){var d=DefDatabase.Units[card.Definition];ore=d.Ore;plasma=d.Plasma;supply=d.Supply*d.SpawnCount;}
            else{var d=DefDatabase.Upgrades[card.Definition];ore=d.Ore;plasma=d.Plasma;}
            if(r.Ore<ore || r.Plasma<plasma){Notify("자원이 부족합니다.");return;}if(r.UsedSupply+supply>r.MaxSupply){Notify("보급이 부족합니다. 보급 건물을 건설하세요.");return;}
        }
        foreach(var id in _selection.Selected)
        {
            _bridge.Issue(new Command(card.Type,_bridge.LocalPlayer,id,target,MatchBridge.ToSim(point),card.Definition,Input.IsKeyPressed(Key.Shift)));
            if(card.Type is CommandType.Build or CommandType.Train or CommandType.Research or CommandType.Cancel)break;
        }
    }
    private void CancelQueue(int index){if(_selection.Selected.Count>0)_bridge.Issue(new Command(CommandType.Cancel,_bridge.LocalPlayer,_selection.Selected[0],EntityId.None,Fix2.Zero,index));}
    public void Notify(string text,bool sound=true){_notice.Text=text;_noticeTime=3.5f;_notice.Modulate=Colors.White;if(sound)_alertSound.Play();}
    private void TogglePause()
    {
        if(_surrendered)return;_pause.Visible=!_pause.Visible;_target=null;
        // Lockstep peers cannot pause one client alone; the menu opens while the match continues.
        if(_bridge.Mode==MatchMode.Network){_pause.GetNode<Label>("Status").Text="멀티플레이 경기는 메뉴가 열려 있어도 계속 진행됩니다.";return;}
        _bridge.IsPaused=_pause.Visible;
    }
    private void RefreshKeys(){for(int i=0;i<12;i++)_keyButtons[i].Text=$"{i+1}  [{_keys[i]}]";}
    public void ReceiveChat(int player,string text,bool team)
    {
        string clean=text.Replace("\n"," ").Replace("\r"," ");if(clean.Length>160)clean=clean.Substring(0,160);
        _messages.Add($"[{(team?"팀":"전체")}] P{player+1}: {clean}");if(_messages.Count>6)_messages.RemoveAt(0);_chatLog.Text=string.Join("\n",_messages);
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(input is InputEventKey key && key.Pressed&&!key.Echo)
        {
            if(_editingKey>=0){if(_keys.Assign(_editingKey,key.PhysicalKeycode)){_editingKey=-1;RefreshKeys();}else Notify("WASD를 제외한 영문 키를 지정하세요.");GetViewport().SetInputAsHandled();return;}
            if(key.PhysicalKeycode==Key.Escape){if(_keyEditor.Visible){_keyEditor.Hide();_editingKey=-1;}else if(_target!=null){_target=null;}else if(_chat.Visible){_chat.Hide();_chat.ReleaseFocus();}else TogglePause();GetViewport().SetInputAsHandled();return;}
            if(key.PhysicalKeycode==Key.Enter){_chat.Show();_chat.GrabFocus();GetViewport().SetInputAsHandled();return;}
            if(key.PhysicalKeycode==Key.Space && _lastAttackAlert>0){_camera.Jump(_lastEvent);GetViewport().SetInputAsHandled();return;}
            if(_pause.Visible || _chat.Visible){GetViewport().SetInputAsHandled();return;}
            for(int i=0;i<12;i++)if(key.PhysicalKeycode==_keys[i]){ActivateSlot(i);GetViewport().SetInputAsHandled();return;}
        }
        if(_pause.Visible || _keyEditor.Visible){GetViewport().SetInputAsHandled();return;}
        if(input is InputEventMouseButton m && m.Pressed && _target is Card card)
        {
            if(m.ButtonIndex==MouseButton.Right){_target=null;GetViewport().SetInputAsHandled();return;}
            if(m.ButtonIndex==MouseButton.Left && _bridge.PickGround(_camera,m.Position) is Vector3 p)
            {
                var target=_bridge.PickEntity(p);Send(card,p,target);if(!Input.IsKeyPressed(Key.Shift)){_target=null;_menu=0;}GetViewport().SetInputAsHandled();
            }
        }
    }
    public override void _Input(InputEvent input)
    {
        if(_chat!=null && _chat.Visible && input is InputEventKey key && key.Pressed && key.PhysicalKeycode==Key.Tab){_teamChat=!_teamChat;_chat.PlaceholderText=_teamChat?"팀 메시지":"전체 메시지";GetViewport().SetInputAsHandled();}
    }
}
