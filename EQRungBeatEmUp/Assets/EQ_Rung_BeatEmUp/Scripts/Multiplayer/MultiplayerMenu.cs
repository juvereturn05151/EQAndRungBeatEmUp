using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BeatEmUp
{
    [DefaultExecutionOrder(-80)]
    public sealed partial class MultiplayerMenu : MonoBehaviour
    {
        MultiplayerSession session;
        Canvas canvas;
        GameObject page;
        CharacterSelectManager characterSelect;
        string screen="main", joinCode="", address="127.0.0.1";
        Text status;
        float refreshAt;
        GUIStyle debugStyle;
        readonly Color red=new Color(.8f,.16f,.05f), blue=new Color(.06f,.2f,.34f), dark=new Color(.035f,.05f,.075f,.94f);
        Color[] colors => PlayerGroundIndicatorStyle.SharedPlayerColors;
        readonly Dictionary<int,float> comboEndTime=new Dictionary<int,float>();
        readonly Dictionary<int,bool> comboWasActive=new Dictionary<int,bool>();
        bool debugVisible;
        string lastSignature;
        readonly Dictionary<int,Text> tags=new Dictionary<int,Text>();
        readonly Dictionary<int,Text> comboLabels=new Dictionary<int,Text>();
        void Awake()
        {
            session=GetComponent<MultiplayerSession>();
            var root=new GameObject("Ghost Fair UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            var events=new GameObject("Multiplayer UI input",typeof(EventSystem),typeof(InputSystemUIInputModule)); events.transform.SetParent(transform);
            var uiInput=events.GetComponent<InputSystemUIInputModule>();
            uiInput.AssignDefaultActions();
            // Custom menu actions own submit/move; disable defaults before the first input update,
            // rather than allowing one press to activate the new page's focused button as well.
            uiInput.submit.action.Disable(); uiInput.move.action.Disable();
            InitializeNavigation();
        }
        void Start() { session.Changed+=Refresh; Refresh(); }
        void OnDestroy() { if(session) session.Changed-=Refresh; DisposeNavigation(); }
        void Update()
        {
            UpdateNavigation();
            if(UnityEngine.InputSystem.Keyboard.current!=null && UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame && (Application.isEditor || Debug.isDebugBuild)) debugVisible=!debugVisible;
            if(session.InLobby && Time.unscaledTime>=refreshAt) { refreshAt=Time.unscaledTime+.3f; Refresh(); }
            if(session.InGame && Time.unscaledTime>=refreshAt) { refreshAt=Time.unscaledTime+.1f; Refresh(); }
            var module=GetComponentInChildren<InputSystemUIInputModule>();
            if(module && module.submit!=null) module.submit.action.Disable();
            if(module && module.move!=null) module.move.action.Disable();
            if(session.InGame && session.Latest!=null)
            {
                var camera=Camera.main;
                foreach(var p in session.Latest.players)
                {
                    if(camera && tags.TryGetValue(p.slot,out var tag) && tag)
                    {
                        var viewport=camera.WorldToViewportPoint(p.position+Vector3.up*.45f);
                        tag.rectTransform.anchoredPosition=new Vector2(viewport.x*1920-30,-((1-viewport.y)*1080-30));
                    }
                    if(comboLabels.TryGetValue(p.slot,out var combo) && combo)
                    {
                        float end=comboEndTime.TryGetValue(p.slot,out float value) ? value : -10;
                        var color=combo.color; color.a=p.activeCombo ? 1 : 1-Mathf.Clamp01((Time.unscaledTime-end-.8f)/.35f); combo.color=color;
                    }
                }
            }
        }
        RectTransform Rect(GameObject obj,Transform parent,Rect rect)
        {
            obj.transform.SetParent(parent,false); var t=obj.GetComponent<RectTransform>();
            t.anchorMin=t.anchorMax=new Vector2(0,1); t.pivot=new Vector2(0,1); t.anchoredPosition=new Vector2(rect.x,-rect.y); t.sizeDelta=rect.size; return t;
        }
        GameObject Panel(string name,Rect rect,Color color,Transform parent=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); Rect(go,parent ? parent : page.transform,rect); go.GetComponent<Image>().color=color; return go;
        }
        Text Label(string value,Rect rect,int size=28,Color? color=null,Transform parent=null)
        {
            var go=new GameObject(value,typeof(RectTransform),typeof(Text)); Rect(go,parent ? parent : page.transform,rect);
            var t=go.GetComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize=size; t.color=color ?? Color.white; t.text=value; t.raycastTarget=false; t.horizontalOverflow=HorizontalWrapMode.Wrap; return t;
        }
        Button Button(string text,Rect rect,Action click,Color? color=null,bool enabled=true)
        {
            var go=Panel(text,rect,color ?? blue); var button=go.AddComponent<Button>(); button.targetGraphic=go.GetComponent<Image>(); button.interactable=enabled;
            var label=Label(text,new Rect(12,10,rect.width-24,rect.height-16),28,parent:go.transform); label.alignment=TextAnchor.MiddleCenter;
            var buttonColors=button.colors; buttonColors.highlightedColor=new Color(1,.72f,.35f); buttonColors.selectedColor=buttonColors.highlightedColor; buttonColors.disabledColor=new Color(.4f,.4f,.4f); button.colors=buttonColors;
            button.onClick.AddListener(()=>click()); return button;
        }
        void Image(Sprite sprite,Rect rect,Color color,Transform parent=null)
        {
            var go=Panel("Stage artwork",rect,color,parent); var image=go.GetComponent<Image>(); image.sprite=sprite; image.preserveAspect=true; image.raycastTarget=false;
        }
        InputField Field(string value,Rect rect,Action<string> changed)
        {
            var go=Panel("Input",rect,new Color(.06f,.08f,.12f)); var input=go.AddComponent<InputField>();
            input.textComponent=Label(value,new Rect(15,12,rect.width-30,rect.height-20),28,parent:go.transform); input.text=value;
            input.onValueChanged.AddListener(s=>changed(s)); return input;
        }
        public void Refresh()
        {
            if(session.InLobby && characterSelect && characterSelect.isActiveAndEnabled) { characterSelect.Render(); return; }
            string signature=screen+"/"+session.InGame+"/"+session.InLobby+"/"+session.Status+"/"+session.PreferredCharacter+"/"+session.LocalMenuOpen+"/"+joinCode;
            if(session.InLobby) signature+=JsonUtility.ToJson(session.Lobby);
            if(session.InGame && session.Latest!=null)
            {
                var w=session.Latest; signature+="/"+w.stage+"/"+w.exitOpen+"/"+w.rewardPending+"/"+w.completed+"/"+w.gameOver;
                foreach(var p in w.players) signature+="/"+p.slot+":"+p.hp+":"+p.maxHp+":"+p.meter+":"+p.maxMeter+":"+p.hits+":"+p.damage+":"+p.activeCombo+":"+p.choosing+":"+p.rewardDone+":"+string.Join(",",p.choices);
                foreach(var boss in w.entities.Where(e=>e.kind=="Boss")) signature+="/boss:"+boss.hp+":"+boss.phase+":"+boss.invulnerable+":"+boss.vulnerabilityFrames+":"+boss.dead;
            }
            if(signature==lastSignature) return; lastSignature=signature;
            RememberFocus(); rewardButtons.Clear();
            tags.Clear(); comboLabels.Clear();
            if(page) { page.SetActive(false); Destroy(page); }
            page=new GameObject("Menu page",typeof(RectTransform)); Rect(page,canvas.transform,new Rect(0,0,1920,1080));
            // Every page replaces its selectable tree. Give navigation a valid selection after construction.
            if(session.InGame) { Gameplay(); if(session.LocalMenuOpen) GameMenu(); FinishNavigation(); return; }
            if(session.InLobby)
            {
                var prefab=Resources.Load<CharacterSelectManager>("CharacterSelect/CharacterSelectCanvas");
                if(prefab) { characterSelect=Instantiate(prefab,page.transform); characterSelect.Bind(session); return; }
            }
            if(session.catalog && session.catalog.menuBackground)
            {
                // Use the existing complete hub texture; its background/floor sprite slices remain unchanged.
                var art=new GameObject("Fairground menu artwork",typeof(RectTransform),typeof(RawImage)); Rect(art,page.transform,new Rect(0,0,1920,1080));
                var image=art.GetComponent<RawImage>(); image.texture=session.catalog.menuBackground.texture; image.color=new Color(.42f,.34f,.38f); image.raycastTarget=false;
            }
            Panel("Shade",new Rect(0,0,1920,1080),new Color(.015f,.02f,.035f,.55f));
            Label("GHOST FAIR",new Rect(80,72,800,100),76,new Color(1,.28f,.09f));
            Label("Fight together against the spirits",new Rect(82,178,780,55),30,new Color(1,.82f,.6f));
            status=Label(session.Status,new Rect(80,950,1760,110),25,new Color(.85f,.9f,1));
            if(session.InLobby) Lobby(); else MenuPage();
            FinishNavigation();
        }
        void MenuPage()
        {
            if(screen=="code") { CodeEntry(); return; }
            if(screen=="main")
            {
                Button("PLAY",new Rect(100,300,520,90),()=>Show("modes"),red);
                Button("HOW TO PLAY",new Rect(100,420,520,80),()=>Show("help"));
                Button("OPTIONS",new Rect(100,530,520,80),()=>Show("options"));
                Button("QUIT",new Rect(100,640,520,80),()=>Application.Quit());
                Label("Arrows / WASD / stick / D-pad: move\nEnter / A: confirm    Escape / B: back",new Rect(100,800,950,100),26);
                Label("A 2D BEAT 'EM UP\n\nSingle Player • Local Co-op • Online Co-op\n\nUp to 4 players",new Rect(850,350,850,330),36,new Color(1,.84f,.65f));
            }
            else if(screen=="modes")
            {
                Button("SINGLE PLAYER",new Rect(100,300,620,90),()=>session.BeginLocal(true),red);
                Button("LOCAL CO-OP",new Rect(100,420,620,90),()=>session.BeginLocal());
                Button("ONLINE CO-OP",new Rect(100,540,620,90),()=>Show("online"));
                Button("BACK",new Rect(100,760,300,70),()=>Show("main"));
                Label("Local: one keyboard plus up to three gamepads,\nor up to four gamepads.\n\nOnline: one player per machine.\nCreate a room and share its Relay code.",new Rect(850,340,850,340),34);
            }
            else if(screen=="online")
            {
                Button(session.PreferredOnlineDevice is UnityEngine.InputSystem.Gamepad ? "INPUT: GAMEPAD" : "INPUT: KEYBOARD",new Rect(850,730,650,70),()=>
                { session.PreferredOnlineDevice=session.PreferredOnlineDevice is UnityEngine.InputSystem.Gamepad ? (UnityEngine.InputSystem.InputDevice)UnityEngine.InputSystem.Keyboard.current : UnityEngine.InputSystem.Gamepad.current; lastSignature=null; Refresh(); });
                Button("CREATE LOBBY",new Rect(100,310,560,85),async()=>await session.HostRelay(),red,!session.Busy);
                Label("Room code",new Rect(100,430,600,45));
            var codeField=Field(joinCode,new Rect(100,485,560,70),s=>joinCode=s); codeField.gameObject.name="Room code";
            Button("ENTER CODE WITH BUTTONS",new Rect(100,565,560,65),()=>Show("code"));
                Button("JOIN BY CODE",new Rect(100,650,560,85),async()=>await session.JoinRelay(joinCode),enabled:!session.Busy);
                Button("BACK",new Rect(100,760,300,70),()=>{session.CancelConnection(); Show("modes");});
                Label("Internet co-op uses Unity Relay.\n\nUse the same game build as your friends.\nThe host controls the run and starts when everyone is ready.",new Rect(850,340,850,340),34);
            }
            else if(screen=="help")
            {
                Label("KEYBOARD\nMove: WASD / arrows\nPunch: J     Launcher / air dive: K\nJump: Space     Guard / parry: L (no movement)\nDodge: direction + L\nSkill: I (1 bar)     Interact: E     Upgrade choices: 1 / 2 / 3\n\nGAMEPAD\nMove: left stick     Punch: X\nLauncher / dive: Y     Jump: A\nGuard: left shoulder (no movement)\nDodge: stick direction + left shoulder\nSkill: right trigger (1 bar)     Interact: L1 / LB     Blessing: D-pad + A; B to close\n\nLobby: Enter / A to join or toggle ready. Space / Start to begin.",new Rect(100,290,1640,560),31);
                Button("BACK",new Rect(100,850,300,65),()=>Show("main"));
            }
            else
            {
                Label("AUDIO VOLUME",new Rect(100,320,650,55),36);
                Button("−",new Rect(100,410,120,70),()=>{AudioListener.volume=Mathf.Max(0,AudioListener.volume-.1f); lastSignature=null; Refresh();});
                Label(Mathf.RoundToInt(AudioListener.volume*100)+"%",new Rect(260,420,180,60),34);
                Button("+",new Rect(480,410,120,70),()=>{AudioListener.volume=Mathf.Min(1,AudioListener.volume+.1f); lastSignature=null; Refresh();});
                Button(Screen.fullScreen ? "WINDOWED" : "FULL SCREEN",new Rect(100,550,500,80),()=>{Screen.fullScreen=!Screen.fullScreen; lastSignature=null; Refresh();});
                Button("BACK",new Rect(100,760,300,70),()=>Show("main"));
            }
        }
        void Show(string name) { screen=name; focusName=null; lastSignature=null; Refresh(); }
        void Lobby()
        {
            Label("LOBBY / MATCH SETUP",new Rect(830,90,1000,90),47,new Color(1,.85f,.64f));
            Label(session.Mode==SessionMode.Online ? "ONLINE CO-OP" : "LOCAL CO-OP",new Rect(100,280,720,60),38);
            for(int i=0;i<4;i++)
            {
                int slotId=i; var slot=session.Lobby.slots.Find(s=>s.slot==i);
                var card=Panel("Player slot "+i,new Rect(100+i*440,360,400,300),dark);
                Panel("Player color",new Rect(0,0,400,8),colors[i],card.transform);
                Label("P"+(i+1),new Rect(20,22,80,50),38,colors[i],card.transform);
                Label(slot!=null ? slot.name : "OPEN SLOT",new Rect(105,28,275,50),28,parent:card.transform);
                var definition=session.catalog.CharacterAt(slot?.character ?? 0);
                var pose=definition && definition.portrait ? definition.portrait : session.catalog.playerPrefab.GetComponent<ComboController>().groundCombo[0].frames[0].sprite;
                if(slot!=null) Image(pose,new Rect(135,85,125,145),Color.white,card.transform);
                Label(slot==null ? "Press Enter / A to join" : slot.ready ? "READY ✓" : "NOT READY",new Rect(18,247,365,50),27,slot!=null && slot.ready ? Color.green : Color.white,card.transform);
                if(slot!=null && session.IsLocalOwner(slot.owner)) Button("TOGGLE READY",new Rect(100+i*440,685,400,65),()=>session.Ready(slotId));
                if(slot!=null && session.IsLocalOwner(slot.owner) && session.catalog.characters.Length>0)
                    Button("CHARACTER: "+definition?.displayName,new Rect(100+i*440,742,400,45),()=>session.SelectCharacter(slotId,(slot.character+1)%session.catalog.characters.Length));
                else if(slot!=null && definition) Label(definition.displayName,new Rect(100+i*440,744,400,42),22);
            }
            if(session.Mode==SessionMode.Online)
            {
                Label("ROOM CODE: "+session.Lobby.code,new Rect(100,795,900,70),38,new Color(1,.82f,.45f));
                Button("COPY",new Rect(890,792,180,65),()=>GUIUtility.systemCopyBuffer=session.Lobby.code);
            }
            else Label("Enter / A: join or ready     Space / Start: begin",new Rect(100,805,1100,70),28);
            Button("LEAVE",new Rect(100,875,300,65),()=>{screen="main"; session.LeaveToMenu();});
            if(session.IsAuthority) Button("START GAME",new Rect(1290,820,530,95),()=>session.StartGame(),red,session.CanStart);
            else Label("Waiting for host to start…",new Rect(1220,850,650,70),30);
        }
        void Gameplay()
        {
            var world=session.Latest; if(world==null) { Label("Loading players…",new Rect(700,460,800,80),40); return; }
            foreach(var p in world.players)
            {
                int slot=p.slot;
                var root=Panel("P"+(slot+1)+" HUD",new Rect(18+slot*476,16,456,174),dark);
                Label("P"+(slot+1)+"  "+(p.dead ? "DEFEATED" : "HP "+p.hp.ToString("0")+" / "+p.maxHp.ToString("0")),new Rect(12,10,430,38),27,colors[slot],root.transform);
                Panel("HP backing",new Rect(12,58,430,13),new Color(.15f,.15f,.15f),root.transform);
                Panel("HP",new Rect(12,58,430*Mathf.Clamp01(p.hp/Mathf.Max(1,p.maxHp)),13),colors[slot],root.transform);
                Panel("Skill backing",new Rect(12,142,250,12),new Color(.16f,.12f,.22f),root.transform);
                Panel("Skill fill",new Rect(12,142,250*Mathf.Clamp01(p.meter/Mathf.Max(.01f,p.maxMeter)),12),new Color(.7f,.4f,1),root.transform);
                Label("SKILL "+p.meter.ToString("0.##")+" / "+p.maxMeter.ToString("0.##"),new Rect(276,130,168,34),21,new Color(.85f,.65f,1),root.transform);
                bool was=comboWasActive.TryGetValue(slot,out bool active) && active;
                if(was && !p.activeCombo) comboEndTime[slot]=Time.unscaledTime;
                comboWasActive[slot]=p.activeCombo;
                float elapsed=comboEndTime.TryGetValue(slot,out float end) ? Time.unscaledTime-end : 2;
                if(p.hits>0) comboLabels[slot]=Label(p.hits+" HITS   "+p.damage.ToString("0.##")+" DAMAGE",new Rect(12,89,430,36),25,new Color(1,.72f,.3f,p.activeCombo ? 1 : 1-Mathf.Clamp01((elapsed-.8f)/.35f)),root.transform);
                var camera=Camera.main;
                if(camera && !p.dead)
                {
                    Vector3 viewport=camera.WorldToViewportPoint(p.position+Vector3.up*.45f);
                    tags[slot]=Label("P"+(slot+1),new Rect(viewport.x*1920-30,(1-viewport.y)*1080-30,90,35),24,colors[slot]);
                }
                if(p.choosing && session.IsLocalOwner(p.owner))
                {
                    float width=session.Mode==SessionMode.Local ? 456 : 1060, x=session.Mode==SessionMode.Local ? 18+slot*476 : 430;
                    var card=Panel("Blessings P"+(slot+1),new Rect(x,630,width,410),dark);
                    Label("P"+(slot+1)+": CHOOSE YOUR BLESSING",new Rect(14,14,width-28,44),26,colors[slot],card.transform);
                    for(int choice=0;choice<p.choices.Length;choice++)
                    {
                        int index=choice;
                        var button=Button((choice+1)+". "+p.choices[choice],new Rect(x+12,684+choice*112,width-24,104),()=>session.ChooseLocal(slot,index));
                        if(!rewardButtons.TryGetValue(slot,out var choices)) rewardButtons[slot]=choices=new List<Button>(); choices.Add(button);
                        var title=button.GetComponentInChildren<Text>(); title.fontSize=22; title.alignment=TextAnchor.UpperCenter; title.rectTransform.sizeDelta=new Vector2(width-48,32);
                        if(choice<p.descriptions.Length) Label(p.descriptions[choice],new Rect(12,42,width-48,60),18,parent:button.transform);
                    }
                    rewardButtons[slot].Add(Button("BACK  /  ESC / B",new Rect(x+12,1020,width-24,45),()=>session.ChooseLocal(slot,-2)));
                }
            }
            string stage=world.stage>=0 && world.stage<session.catalog.level.stages.Count ? session.catalog.level.stages[world.stage].stageName : "Loading";
            Label(stage+"  •  "+(world.rewardPending ? "Visit the chapel: E / L1 / LB. Each player chooses a blessing." : world.exitOpen ? "Exit open" : "Clear the encounter"),new Rect(20,202,1850,48),25,new Color(1,.83f,.64f));
            if(world.rewardPending) Label(string.Join("   ",world.players.Where(p=>!p.dead).Select(p=>"P"+(p.slot+1)+": "+(p.rewardDone ? "✓" : p.choosing ? "choosing" : "visit chapel"))),new Rect(20,244,1850,44),25);
            var boss=world.entities.Find(e=>e.kind=="Boss" && !e.dead);
            if(boss!=null)
            {
                var card=Panel("Boss status",new Rect(580,282,760,92),dark);
                string protection=boss.invulnerable ? "SHIELDED — BREAK A NEARBY TOTEM" : "VULNERABLE "+(boss.vulnerabilityFrames/60f).ToString("0.0")+"s";
                Label("WHITE GHOST  •  PHASE "+boss.phase+"  •  "+protection,new Rect(12,9,736,36),22,new Color(.85f,.65f,1),card.transform);
                Panel("Boss HP backing",new Rect(12,60,736,14),new Color(.15f,.1f,.18f),card.transform);
                Panel("Boss HP",new Rect(12,60,736*Mathf.Clamp01(boss.hp/Mathf.Max(1,boss.maxHp)),14),new Color(.7f,.35f,1),card.transform);
            }
            if(world.gameOver || world.completed)
            {
                Panel("End run",new Rect(510,330,900,320),dark);
                Label(world.completed ? "YOU ESCAPED" : "ALL PLAYERS DEFEATED",new Rect(570,370,800,70),46,new Color(1,.6f,.25f));
                if(session.IsAuthority) Button("RETURN TO HUB",new Rect(650,470,620,80),()=>session.Retry(),red);
                Button("MAIN MENU",new Rect(650,570,620,65),()=>{screen="main";session.LeaveToMenu();});
            }
            if(session.LocalMenuOpen) return;
            if(!string.IsNullOrEmpty(world.status)) Label(world.status,new Rect(300,300,1300,150),30,Color.red);
            if(!world.gameOver && !world.completed && !world.players.Any(p=>p.choosing && session.IsLocalOwner(p.owner)))
            {
                if(world.stage>0 && session.IsAuthority) Button("RETURN HUB",new Rect(1340,1002,270,60),()=>session.ReturnToHub());
                Button("MAIN MENU",new Rect(1630,1002,270,60),()=>{screen="main";session.LeaveToMenu();});
            }
        }
        void OnGUI()
        {
            if(!debugVisible || !Application.isEditor && !Debug.isDebugBuild) return;
            debugNavigation.Begin();
            GUILayout.BeginArea(new Rect(12,240,320,420),GUI.skin.box);
            GUILayout.Label("Multiplayer debug (F10)"); address=GUILayout.TextField(address);
            if(!session.InGame)
            {
                if(debugNavigation.Button("Direct host :7777")) session.DirectHost();
                if(debugNavigation.Button("Direct join :7777")) session.DirectJoin(address);
                if(debugNavigation.Button("Local lobby")) session.BeginLocal();
                if(debugNavigation.Button("Force ready (authority)")) foreach(var slot in session.Lobby.slots.Where(s=>session.IsLocalOwner(s.owner))) if(!slot.ready) session.Ready(slot.slot);
            }
            else if(session.IsAuthority && session.Flow)
            {
                for(int i=0;i<session.catalog.level.stages.Count;i++) { int stage=i; if(debugNavigation.Button("Stage "+(i+1))) session.Flow.EnterStage(stage); }
                if(debugNavigation.Button("Complete event")) session.Flow.CompleteStageEvent();
            }
            GUILayout.EndArea();
            debugNavigation.End();
        }
    }
}
