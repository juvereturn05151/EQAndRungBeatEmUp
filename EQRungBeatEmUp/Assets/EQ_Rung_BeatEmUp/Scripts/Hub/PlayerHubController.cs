using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace BeatEmUp
{
    public enum HubAction { Close, Character, Stat, Skill, EnterWorld, Return }
    [System.Serializable] public sealed class HubRequest { public int slot, index; public HubAction action; }
    [RequireComponent(typeof(StageFlowController))]
    public sealed class PlayerHubController : MonoBehaviour
    {
        public PlayerHubDefinition definition;
        StageFlowController flow;
        float deathAt=-1;
        readonly System.Collections.Generic.HashSet<int> paidRooms=new System.Collections.Generic.HashSet<int>();
        readonly System.Collections.Generic.Dictionary<int,int> uiChoices=new System.Collections.Generic.Dictionary<int,int>();
        MenuNavigationInput standaloneMenu;
        int visibleMenuSlot;
        void OnDestroy() { standaloneMenu?.Dispose(); }
        readonly string[] names={"Change Character","Upgrade Base Stats","Upgrade Skill","Enter World 1"};
        public bool InHub=>flow && flow.CurrentStage!=null && flow.CurrentStage.hub;
        void Awake() { flow=GetComponent<StageFlowController>(); }
        void Start()
        {
            if(MultiplayerSession.Active || !definition || !flow.player) return;
            var meta=flow.player.GetComponent<MetaProgress>() ?? flow.player.gameObject.AddComponent<MetaProgress>();
            meta.Initialize(definition,MetaSave.Load(0,definition),0,true);
            var catalog=Resources.Load<MultiplayerCatalog>("MultiplayerCatalog");
            int choice=System.Array.FindIndex(catalog.characters,c=>c.characterId==meta.profile.characterId);
            flow.player.GetComponent<PlayerCharacterLoadout>().Apply(catalog.CharacterAt(Mathf.Max(0,choice)));
            flow.player.GetComponent<CharacterHealth>().Restore();
        }
        public int Nearby(Vector2 position)
        {
            if(!definition) return -1;
            for(int i=0;i<definition.stations.Length;i++) if(Vector2.Distance(position,definition.stations[i])<=definition.interactionRadius) return i;
            return -1;
        }
        public bool Interact(CharacterMotor actor)
        {
            if(!InHub || !actor.IsGrounded || actor.attackPlayer.CurrentAttack) return false;
            var meta=actor.GetComponent<MetaProgress>(); int station=Nearby(actor.transform.position);
            if(!meta || station<0) return false;
            meta.OpenStation=meta.OpenStation==station ? -1 : station;
            actor.GetComponent<ComboController>().ResetCombo(); actor.StopGroundedMotion(); return true;
        }
        public bool Execute(CharacterMotor actor,HubAction action,int index)
        {
            if(!actor || MultiplayerSession.Active && !MultiplayerSession.Active.IsAuthority) return false;
            var meta=actor.GetComponent<MetaProgress>(); if(!meta) return false;
            if(action==HubAction.Close) { meta.OpenStation=-1; return true; }
            if(!InHub || !actor.IsGrounded || meta.OpenStation<0 || Nearby(actor.transform.position)!=meta.OpenStation) return false;
            if(action==HubAction.Character && meta.OpenStation==0)
            {
                var catalog=Resources.Load<MultiplayerCatalog>("MultiplayerCatalog"); var character=catalog.CharacterAt(index); if(!character) return false;
                var previous=meta.profile.characterId; meta.profile.characterId=character.characterId;
                if(meta.persistLocally && !MetaSave.Save(meta.saveSlot,meta.profile)) { meta.profile.characterId=previous; return false; }
                actor.GetComponent<ComboController>().ResetCombo(); actor.GetComponent<PlayerCharacterLoadout>().Apply(character);
                var identity=actor.GetComponent<PlayerIdentity>(); if(identity) { identity.character=index; MultiplayerSession.Active?.SetHubCharacter(identity,index); }
                actor.GetComponent<CharacterHealth>().Restore(); return true;
            }
            if(action==HubAction.Stat && meta.OpenStation==1) return meta.Upgrade(index);
            if(action==HubAction.Skill && meta.OpenStation==2) return meta.Upgrade(0,true);
            if(action==HubAction.EnterWorld && meta.OpenStation==3)
            {
                if(MultiplayerSession.Active && !MultiplayerSession.Active.IsLocalOwner(actor.GetComponent<PlayerIdentity>().owner)) return false;
                BeginRun(); return true;
            }
            return false;
        }
        public void BeginRun()
        {
            paidRooms.Clear(); foreach(var actor in flow.Players)
            { actor.GetComponent<MetaProgress>().OpenStation=-1; actor.GetComponent<RunBuildState>()?.ResetRun(); actor.GetComponent<CharacterHealth>().Restore(); }
            flow.EnterStage(flow.level.stages.FindIndex(s=>s.stageId=="Stage01_EntranceGate"));
        }
        public void ReturnToHub()
        {
            if(MultiplayerSession.Active && !MultiplayerSession.Active.IsAuthority) return;
            flow.CoopRewards?.Cancel(); flow.WorldRewards?.Cancel();
            foreach(var actor in flow.Players)
            { actor.GetComponent<ComboController>().ResetCombo(); actor.GetComponent<RunBuildState>()?.ResetRun(); var meta=actor.GetComponent<MetaProgress>(); if(meta) { meta.OpenStation=-1; meta.Save(); } actor.GetComponent<CharacterHealth>().Restore(); }
            flow.Restart(true); paidRooms.Clear(); deathAt=-1;
        }
        void Update()
        {
            if(!definition || !flow || !flow.player || MultiplayerSession.Active && !MultiplayerSession.Active.InGame) return;
            MenuInput();
            bool authority=!MultiplayerSession.Active || MultiplayerSession.Active.IsAuthority;
            if(!authority)
            {
                var world=MultiplayerSession.Active.Latest;
                if(world!=null && world.stage>0 && world.gameOver) { if(deathAt<0) deathAt=Time.unscaledTime; }
                else deathAt=-1;
            }
            if(authority)
            {
                if(!flow.LivingPlayers.Any() && !InHub)
                { if(deathAt<0) deathAt=Time.unscaledTime; if(Time.unscaledTime-deathAt>=definition.deathReturnDelay) ReturnToHub(); }
                else deathAt=-1;
                if(!InHub && flow.CurrentStage!=null && !flow.CurrentStage.IsSafeStage && flow.EncountersComplete && flow.ExitUnlocked && paidRooms.Add(flow.StageIndex))
                    foreach(var actor in flow.Players) { var meta=actor.GetComponent<MetaProgress>(); if(meta) { meta.profile.essence=Mathf.Min(1000000,meta.profile.essence+definition.roomReward); meta.Save(); } }
                foreach(var actor in flow.Players)
                {
                    var meta=actor.GetComponent<MetaProgress>();
                    if(meta && meta.OpenStation>=0 && (!InHub || Nearby(actor.transform.position)!=meta.OpenStation)) meta.OpenStation=-1;
                }
            }
            if(!MultiplayerSession.Active && InHub && flow.player.GetComponent<MetaProgress>()?.OpenStation>=0)
            { flow.player.MoveInput=Vector2.zero; flow.player.StopGroundedMotion(); if(Keyboard.current?.escapeKey.wasPressedThisFrame==true) flow.player.GetComponent<MetaProgress>().OpenStation=-1; }
        }
        void Command(int slot,HubAction action,int index=0)
        { if(MultiplayerSession.Active) MultiplayerSession.Active.HubCommand(slot,action,index); else Execute(flow.player,action,index); }
        void MenuInput()
        {
            var session=MultiplayerSession.Active;
            if(session && session.LocalMenuOpen) return;
            standaloneMenu?.Read();
            var local=session ? session.Latest?.players.Where(p=>session.IsLocalOwner(p.owner)).ToArray() : new[]{new PlayerState{slot=0,hubStation=flow.player.GetComponent<MetaProgress>()?.OpenStation ?? -1}};
            if(local==null) return;
            foreach(var state in local.Where(p=>p.hubStation>=0))
            {
                if(!session && standaloneMenu==null) standaloneMenu=new MenuNavigationInput(flow.player.GetComponent<PlayerInput>().actions);
                var input=session ? session.LocalInput(state.slot)?.Menu.Read() ?? default : standaloneMenu.Read();
                if(input.Navigate!=Vector2.zero || input.Confirm || input.Cancel) visibleMenuSlot=state.slot;
                int actions=state.hubStation==0 ? Resources.Load<MultiplayerCatalog>("MultiplayerCatalog").characters.Length : state.hubStation==1 ? 4 : 1;
                int count=actions+1; // Close is a selectable button, as well as the cancel shortcut.
                int choice=uiChoices.TryGetValue(state.slot,out int old) ? old : 0;
                if(input.Navigate.x>0 || input.Navigate.y<0) choice++;
                if(input.Navigate.x<0 || input.Navigate.y>0) choice--;
                choice=(choice+count)%count; uiChoices[state.slot]=choice;
                if(input.Cancel) Command(state.slot,HubAction.Close);
                else if(input.Confirm)
                    Command(state.slot,choice==actions ? HubAction.Close : state.hubStation==0 ? HubAction.Character : state.hubStation==1 ? HubAction.Stat : state.hubStation==2 ? HubAction.Skill : HubAction.EnterWorld,choice);
            }
        }
        void OnGUI()
        {
            if(!definition || !flow) return;
            var session=MultiplayerSession.Active;
            bool hub=session ? session.InGame && session.Latest?.stage==0 : InHub;
            if(!hub)
            {
                if(deathAt>=0) { var c=GUI.color; GUI.color=new Color(0,0,0,Mathf.Clamp01((Time.unscaledTime-deathAt)/definition.deathReturnDelay)); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture); GUI.color=c; }
                return;
            }
            var old=GUI.matrix; GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/960f,Screen.height/540f,1));
            var local=session ? session.Latest.players.Where(p=>session.IsLocalOwner(p.owner)).ToArray() : new[]{new PlayerState{slot=0,position=flow.player.transform.position,character=0,meta=flow.player.GetComponent<MetaProgress>()?.profile,hubStation=flow.player.GetComponent<MetaProgress>()?.OpenStation ?? -1}};
            foreach(var p in local.OrderByDescending(p=>p.slot==visibleMenuSlot))
            {
                int near=Nearby(p.position); if(p.hubStation<0) { if(near>=0) GUI.Box(new Rect(270,440+24*p.slot,420,28),"P"+(p.slot+1)+"  [E / Select] "+names[near]); continue; }
                var catalog=session ? session.catalog : Resources.Load<MultiplayerCatalog>("MultiplayerCatalog"); var selected=session ? catalog.CharacterAt(p.character) : catalog.characters.FirstOrDefault(c=>c.characterId==p.meta?.characterId);
                GUI.Box(new Rect(210,90,540,350),names[p.hubStation]+"  ·  P"+(p.slot+1)+"  ·  Essence "+(p.meta?.essence ?? 0));
                int cursor=uiChoices.TryGetValue(p.slot,out int cursorValue) ? cursorValue : 0;
                int actionCount=p.hubStation==0 ? catalog.characters.Length : p.hubStation==1 ? 4 : 1;
                GUI.Label(new Rect(235,125,480,25),"Currently selected: "+(selected ? selected.displayName : p.meta?.characterId));
                if(p.hubStation==0)
                    for(int i=cursor<catalog.characters.Length ? (cursor/2)*2 : 0;i<Mathf.Min(catalog.characters.Length,(cursor<catalog.characters.Length ? (cursor/2)*2 : 0)+2);i++)
                    {
                        var character=catalog.characters[i]; var rect=new Rect(245+(i%2)*240,165,220,190);
                        if(character.portrait) GUI.DrawTexture(new Rect(rect.x+60,rect.y,100,100),character.portrait.texture,ScaleMode.ScaleToFit,true);
                        GUI.Label(new Rect(rect.x,rect.y+105,220,30),character.skill.displayName);
                        if(GUI.Button(new Rect(rect.x,rect.y+140,220,35),(cursor==i ? "→ " : "")+character.displayName)) Command(p.slot,HubAction.Character,i);
                    }
                if(p.hubStation==1 && p.meta!=null)
                {
                    string[] stats={"Max Health","Attack Power","Damage Reduction","Meter Gain"};
                    for(int i=0;i<4;i++)
                    { int level=p.meta.Level(i); GUI.Label(new Rect(240,165+i*45,255,35),(cursor==i ? "→ " : "")+stats[i]+"  Lv "+level+" / "+definition.maximumLevel); GUI.enabled=level<definition.maximumLevel && p.meta.essence>=definition.Cost(level); if(GUI.Button(new Rect(510,165+i*45,200,35),"Upgrade · "+definition.Cost(level)+" essence")) Command(p.slot,HubAction.Stat,i); GUI.enabled=true; }
                }
                if(p.hubStation==2 && p.meta!=null)
                {
                    GUI.Label(new Rect(240,165,470,65),(selected ? selected.skill.displayName : "Equipped skill")+" · Lv "+p.meta.SkillLevel+"\n+"+(definition.skillPowerPerLevel*100).ToString("0")+"% skill damage per level. Cost remains one bar.");
                    GUI.enabled=p.meta.SkillLevel<definition.maximumLevel && p.meta.essence>=definition.Cost(p.meta.SkillLevel);
                    if(GUI.Button(new Rect(260,255,440,45),(cursor==0 ? "► " : "")+"Upgrade Skill · "+definition.Cost(p.meta.SkillLevel)+" essence")) Command(p.slot,HubAction.Skill); GUI.enabled=true;
                }
                if(p.hubStation==3)
                {
                    GUI.Label(new Rect(240,175,470,65),"World 1 · Thai Haunted House\nStart a fresh run with your permanent blessings.");
                    GUI.enabled=!session || session.IsAuthority;
                    if(GUI.Button(new Rect(260,265,440,45),(cursor==0 ? "► " : "")+"Begin Run (host starts the party)")) Command(p.slot,HubAction.EnterWorld); GUI.enabled=true;
                }
                if(GUI.Button(new Rect(260,385,440,35),(cursor==actionCount ? "► " : "")+"Close [E / Select / Esc / B]")) Command(p.slot,HubAction.Close);
                break;
            }
            GUI.matrix=old;
        }
    }
}
