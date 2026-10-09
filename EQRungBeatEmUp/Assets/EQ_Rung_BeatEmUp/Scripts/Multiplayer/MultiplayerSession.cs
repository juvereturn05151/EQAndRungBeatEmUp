using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BeatEmUp
{
    public enum SessionMode { Single, Local, Online }
    [DefaultExecutionOrder(-100)]
    public sealed partial class MultiplayerSession : MonoBehaviour
    {
        public static MultiplayerSession Active { get; private set; }
        public MultiplayerCatalog catalog;
        public SessionMode Mode { get; private set; }
        public LobbyState Lobby { get; private set; } = new LobbyState();
        public WorldSnapshot Latest { get; private set; }
        public string Status { get; private set; } = "Choose how to play";
        public bool Busy { get; private set; }
        public bool InGame { get; private set; }
        public bool InLobby { get; private set; }
        public bool IsAuthority => Mode!=SessionMode.Online || (network && network.IsServer);
        public NetworkManager Network => network;
        public StageFlowController Flow { get; private set; }
        public InputDevice PreferredOnlineDevice { get; set; }
        public InputDevice PreferredLocalDevice { get; set; }
        public int PreferredCharacter { get; set; }
        public SessionInput LocalInput(int slot) => sources.TryGetValue(slot,out var input) ? input : null;
        public string ValidationPhase { get; set; }
        public ulong ValidationOwner { get; set; }
        int submittedCommandSequence;
        public void SubmitOwnedCommand(PlayerCommand command)
        {
            var slot=Lobby.slots.Find(s=>IsLocalOwner(s.owner)); if(slot==null) return;
            command.sequence=sources.TryGetValue(slot.slot,out var source) ? source.Read().sequence : ++submittedCommandSequence;
            if(IsAuthority) ApplyCommand(slot.slot,command); else Send(0,"input",JsonUtility.ToJson(command));
        }
        public bool CharacterSelectInputActive { get; set; }
        public bool LocalMenuOpen { get; set; }
        public bool CanStart => InLobby && IsAuthority && Application.CanStreamedLevelBeLoaded(catalog.gameplayScene) && Lobby.slots.Count>=Lobby.requiredPlayerCount && Lobby.slots.All(s=>s.ready && CanConfirm(s));
        public bool CanConfirm(LobbySlot slot)
        {
            var character=catalog.CharacterAt(slot.character);
            var definition=catalog.SelectionAt(slot.character);
            if(!character || !(definition ? definition.Prefab : character.prefab) || (definition && !definition.IsUnlocked)) return false;
            return Lobby.allowDuplicateCharacters || !Lobby.slots.Any(s=>s.slot!=slot.slot && s.character==slot.character);
        }
        public void ConfigureSelection(int required, bool duplicates, string scene)
        {
            if(!IsAuthority || !InLobby || Lobby.running) return;
            Lobby.requiredPlayerCount=Mode==SessionMode.Single ? 1 : Mathf.Clamp(required,1,4);
            Lobby.allowDuplicateCharacters=duplicates;
            var assigned=new HashSet<int>();
            foreach(var slot in Lobby.slots.OrderBy(s=>s.slot))
            {
                if(!catalog.CharacterAt(slot.character) || (!duplicates && !assigned.Add(slot.character)))
                { slot.character=AvailableCharacter(PreferredCharacter,slot.slot); slot.ready=false; }
                if(slot.character>=0) assigned.Add(slot.character);
            }
            if(!string.IsNullOrEmpty(scene)) catalog.gameplayScene=scene;
            BroadcastLobby();
        }
        int AvailableCharacter(int preferred, int excludedSlot=-1)
        {
            var candidates=Enumerable.Range(0,catalog.characters.Length).OrderBy(i=>i==preferred ? 0 : 1);
            foreach(int choice in candidates)
            {
                var definition=catalog.SelectionAt(choice);
                if(definition && (!definition.IsUnlocked || !definition.Prefab)) continue;
                if(!definition && !catalog.CharacterAt(choice).prefab) continue;
                if(!Lobby.allowDuplicateCharacters && Lobby.slots.Any(s=>s.slot!=excludedSlot && s.character==choice)) continue;
                return choice;
            }
            return -1;
        }
        public void RemoveSelectionDevice(InputDevice device)
        {
            if(!InLobby || Mode==SessionMode.Online) return;
            Lobby.slots.RemoveAll(s=>s.device==device); Changed?.Invoke();
        }
        public event Action Changed;
        NetworkManager network;
        readonly Dictionary<int,SessionInput> sources=new Dictionary<int,SessionInput>();
        readonly Dictionary<int,PlayerIdentity> characters=new Dictionary<int,PlayerIdentity>();
        readonly Dictionary<ulong,int> commandSequences=new Dictionary<ulong,int>();
        readonly Dictionary<ulong,float> lastInput=new Dictionary<ulong,float>();
        readonly HashSet<ulong> loaded=new HashSet<ulong>();
        readonly Dictionary<int,SpriteRenderer> replicas=new Dictionary<int,SpriteRenderer>();
        readonly Dictionary<int,Vector3> replicaPositions=new Dictionary<int,Vector3>();
        readonly Dictionary<int,int> replicaTeleports=new Dictionary<int,int>();
        readonly Dictionary<int,NetworkTotemWaveVisual> waveReplicas=new Dictionary<int,NetworkTotemWaveVisual>();
        public int WaveReplicaCount => waveReplicas.Count;
        readonly List<FeedbackState> feedback=new List<FeedbackState>();
        readonly HashSet<(int actor,long tick,string cue)> feedbackKeys=new HashSet<(int,long,string)>();
        readonly HashSet<AttackPlayer> observed=new HashSet<AttackPlayer>();
        GameObject replicaRoot;
        Sprite fallbackSprite;
        Material defaultReplicaMaterial;
        float nextStateTime, nextInputTime;
        int effectId,lastEffectId;
        bool sceneReady, leaving;
        int generation;
        float connectionDeadline;
        const string Protocol="GhostFair/5/";
        const int MaxMessageBytes=512*1024;
        void Awake()
        {
            if(Active && Active!=this) { Destroy(gameObject); return; }
            Active=this; DontDestroyOnLoad(gameObject);
            if(!catalog) catalog=Resources.Load<MultiplayerCatalog>("MultiplayerCatalog");
            InitializeHubPreference();
            SceneManager.sceneLoaded+=SceneLoaded;
            CombatProjectile.Impact+=ProjectileImpact;
            CombatProjectile.Deflected+=ProjectileDeflected;
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded-=SceneLoaded;
            CombatProjectile.Impact-=ProjectileImpact;
            CombatProjectile.Deflected-=ProjectileDeflected;
            generation++;
            StopNetwork();
            foreach(var source in sources.Values) source.Dispose(); sources.Clear();
            if(Active==this) Active=null;
        }
        public bool IsLocalOwner(ulong owner) => Mode!=SessionMode.Online || network && owner==network.LocalClientId;
        public void BeginLocal(bool single=false)
        {
            ClearSession(); Busy=false; Mode=single ? SessionMode.Single : SessionMode.Local;
            InLobby=true; Lobby=new LobbyState{contentHash=catalog.contentHash};
            var device=PreferredLocalDevice!=null && PreferredLocalDevice.added ? PreferredLocalDevice : (InputDevice)Keyboard.current ?? Gamepad.current;
            if(device!=null) JoinDevice(device);
            Status="Choose a character. Enter / A confirms; Escape / B cancels."; Changed?.Invoke();
        }
        public bool JoinDevice(InputDevice device)
        {
            if(!InLobby || Mode==SessionMode.Online || Lobby.slots.Count>=4 || device==null || Lobby.slots.Any(s=>s.device==device)) return false;
            if(Mode==SessionMode.Single && Lobby.slots.Count>0) return UseSingleSelectionDevice(device);
            int slot=Enumerable.Range(0,4).First(i=>Lobby.slots.All(s=>s.slot!=i));
            Lobby.slots.Add(new LobbySlot{slot=slot,owner=(ulong)slot,name="Player "+(slot+1),device=device,character=AvailableCharacter(PreferredCharacter)});
            Changed?.Invoke(); return true;
        }
        void OnGUI()
        {
            // Session HUD owns guidance when the standalone Stage Flow HUD is disabled,
            // including clients whose Stage Flow simulation remains disabled.
            if (InGame && Flow) Flow.DrawNextAreaEdgeArrow(false);
        }
        public bool UseSingleSelectionDevice(InputDevice device)
        {
            if(!InLobby || Mode!=SessionMode.Single || device==null || !device.added) return false;
            var slot=Lobby.slots.FirstOrDefault();
            if(slot==null || slot.device==device) return false;
            slot.device=device; slot.ready=false; PreferredLocalDevice=device;
            Changed?.Invoke(); return true;
        }
        public void Ready(int slot)
        {
            if(!InLobby || Lobby.running) return;
            var entry=Lobby.slots.Find(s=>s.slot==slot); if(entry==null) return;
            if(Mode==SessionMode.Online && !IsAuthority) { if(IsLocalOwner(entry.owner)) Send(0,"ready",(!entry.ready).ToString()); return; }
            if(!IsLocalOwner(entry.owner)) return;
            if(!entry.ready && !CanConfirm(entry)) return;
            entry.ready=!entry.ready; BroadcastLobby(); Changed?.Invoke();
        }
        public async Task HostRelay()
        {
            if(Busy) return; Busy=true; Status="Creating Relay room…"; Changed?.Invoke();
            int requestGeneration=-1;
            try
            {
                ClearSession(); Mode=SessionMode.Online; CreateNetwork();
                requestGeneration=generation;
                await InitializeServices();
                if(!this || requestGeneration!=generation) return;
                var allocation=await RelayService.Instance.CreateAllocationAsync(3);
                if(!this || requestGeneration!=generation) return;
                network.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
                string code=await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                if(!this || requestGeneration!=generation) return;
                if(!network.StartHost()) throw new InvalidOperationException("Host could not start.");
                RegisterMessages(); Lobby.code=code; AddOwner(network.LocalClientId); InLobby=true; BroadcastLobby();
                Status="Room created. Share the code with your friends.";
            }
            catch(Exception e) { if(requestGeneration==generation || requestGeneration<0) { Status="Relay setup: "+e.Message+"\nLink this project and enable Authentication / Relay in Unity Gaming Services."; StopNetwork(); } }
            finally { if(requestGeneration==generation || requestGeneration<0) { Busy=false; Changed?.Invoke(); } }
        }
        public async Task JoinRelay(string code)
        {
            if(string.IsNullOrWhiteSpace(code)) { Status="Enter a room code first."; Changed?.Invoke(); return; }
            if(Busy) return; Busy=true; Status="Connecting to Relay…"; Changed?.Invoke();
            int requestGeneration=-1;
            try
            {
                ClearSession(); Mode=SessionMode.Online; CreateNetwork();
                requestGeneration=generation;
                await InitializeServices();
                if(!this || requestGeneration!=generation) return;
                var allocation=await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
                if(!this || requestGeneration!=generation) return;
                network.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
                Lobby.code=code.Trim().ToUpperInvariant();
                if(!network.StartClient()) throw new InvalidOperationException("Client could not start."); RegisterMessages();
                connectionDeadline=Time.unscaledTime+30;
                Status="Connecting…";
            }
            catch(Exception e) { if(requestGeneration==generation || requestGeneration<0) { Status="Could not join: "+e.Message; StopNetwork(); } }
            finally { if(requestGeneration==generation || requestGeneration<0) { Busy=false; Changed?.Invoke(); } }
        }
        async Task InitializeServices()
        {
            if(string.IsNullOrEmpty(Application.cloudProjectId)) throw new InvalidOperationException("No linked Unity Gaming Services project.");
            if(UnityServices.State!=ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if(!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        // Deliberately available for local development, not a replacement for internet Relay.
        public void DirectHost(ushort port=7777)
        {
            ClearSession(); Busy=false; Mode=SessionMode.Online; CreateNetwork(); network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",port,"0.0.0.0");
            if(!network.StartHost()) { Status="Could not start host"; return; }
            RegisterMessages(); AddOwner(network.LocalClientId); InLobby=true; Status="Development host on port "+port; BroadcastLobby(); Changed?.Invoke();
        }
        public void DirectJoin(string address="127.0.0.1",ushort port=7777)
        {
            ClearSession(); Busy=false; Mode=SessionMode.Online; CreateNetwork(); network.GetComponent<UnityTransport>().SetConnectionData(address,port);
            if(!network.StartClient()) { Status="Could not start client"; return; }
            RegisterMessages(); Status="Connecting to development host…"; Changed?.Invoke();
            connectionDeadline=Time.unscaledTime+30;
        }
        void CreateNetwork()
        {
            // NGO owns an independent persistent root; NetworkManager must not be parented.
            var root=new GameObject("Ghost Fair Network");
            network=root.AddComponent<NetworkManager>(); var transport=root.AddComponent<UnityTransport>();
            network.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=true,TickRate=30};
            network.NetworkConfig.ConnectionData=System.Text.Encoding.UTF8.GetBytes(catalog.contentHash);
            network.ConnectionApprovalCallback=(request,response)=>
            {
                bool matching=System.Text.Encoding.UTF8.GetString(request.Payload)==catalog.contentHash;
                response.Approved=matching && !Lobby.running && network.ConnectedClientsIds.Count<4;
                response.CreatePlayerObject=false; response.Pending=false;
                response.Reason=matching ? "Room full or run already started" : "Build content differs; use the same game build";
            };
            network.OnClientConnectedCallback+=Connected;
            network.OnClientDisconnectCallback+=Disconnected;
            network.OnTransportFailure+=()=>{ Status="Network transport failed"; LeaveToMenu(); };
            Lobby=new LobbyState{contentHash=catalog.contentHash};
        }
        void RegisterMessages()
        {
            RegisterHubMessages();
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"lobby",(sender,reader)=>
            {
                if(IsAuthority || sender!=NetworkManager.ServerClientId) return;
                bool entering=!InLobby && !Lobby.running;
                Lobby=JsonUtility.FromJson<LobbyState>(Read(reader)); InLobby=!Lobby.running;
                connectionDeadline=0;
                Status="Connected. Ready when you are."; Changed?.Invoke();
                var own=Lobby.slots.Find(s=>IsLocalOwner(s.owner));
                if(entering && InLobby && own!=null) Send(0,"meta",JsonUtility.ToJson(MetaSave.Load(0,catalog.level.stages[0].hub)));
                if(entering && InLobby && own!=null && own.character!=PreferredCharacter) SelectCharacter(own.slot,PreferredCharacter);
            });
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"ready",(sender,reader)=>
            {
                if(!IsAuthority || Lobby.running) return; var slot=Lobby.slots.Find(s=>s.owner==sender); if(slot==null) return;
                if(!bool.TryParse(Read(reader),out bool ready) || (ready && !CanConfirm(slot))) return;
                slot.ready=ready; BroadcastLobby(); Changed?.Invoke();
            });
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"character",(sender,reader)=>
            {
                if(!IsAuthority || Lobby.running) return;
                var slot=Lobby.slots.Find(s=>s.owner==sender);
                if(slot!=null && int.TryParse(Read(reader),out int choice)) ChangeCharacter(slot,choice);
            });
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"load",(sender,reader)=>
            { if(!IsAuthority && sender==0) { string sceneName=Read(reader); if(!Application.CanStreamedLevelBeLoaded(sceneName)) { Status="Host gameplay scene is unavailable in this build."; LeaveToMenu(); return; } catalog.gameplayScene=sceneName; InLobby=false; InGame=true; SceneManager.LoadScene(sceneName); } });
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"loaded",(sender,reader)=>
            { if(IsAuthority && Lobby.slots.Any(s=>s.owner==sender)) { loaded.Add(sender); TryBeginSimulation(); } });
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"input",(sender,reader)=>
            {
                if(!IsAuthority || !InGame || !sceneReady) return;
                var command=JsonUtility.FromJson<PlayerCommand>(Read(reader));
                if(commandSequences.TryGetValue(sender,out int previous) && command.sequence<=previous) return;
                commandSequences[sender]=command.sequence; lastInput[sender]=Time.unscaledTime;
                var slot=Lobby.slots.Find(s=>s.owner==sender); if(slot!=null) ApplyCommand(slot.slot,command);
            });
            network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol+"world",(sender,reader)=>
            { if(!IsAuthority && sender==0 && sceneReady) ApplySnapshot(JsonUtility.FromJson<WorldSnapshot>(Read(reader))); });
        }
        static string Read(FastBufferReader reader) { reader.ReadValueSafe(out string value); return value; }
        void Send(ulong target,string name,string value)
        {
            if(!network || !network.IsListening) return;
            int size=System.Text.Encoding.UTF8.GetByteCount(value)*2+16;
            if(size>MaxMessageBytes) { Debug.LogError("Multiplayer message exceeds budget: "+name); return; }
            using(var writer=new FastBufferWriter(size,Allocator.Temp,MaxMessageBytes))
            { writer.WriteValueSafe(value); network.CustomMessagingManager.SendNamedMessage(Protocol+name,target,writer,NetworkDelivery.ReliableFragmentedSequenced); }
        }
        void BroadcastLobby()
        {
            if(Mode!=SessionMode.Online || !IsAuthority || !network.IsListening) return;
            string json=JsonUtility.ToJson(Lobby);
            foreach(ulong peer in network.ConnectedClientsIds) if(peer!=network.LocalClientId) Send(peer,"lobby",json);
        }
        void Connected(ulong id)
        {
            if(IsAuthority) { AddOwner(id); BroadcastLobby(); }
        }
        void AddOwner(ulong id)
        {
            if(Lobby.slots.Any(s=>s.owner==id)) return;
            int slot=Enumerable.Range(0,4).First(i=>Lobby.slots.All(s=>s.slot!=i));
            Lobby.slots.Add(new LobbySlot{slot=slot,owner=id,name="Player "+(slot+1),character=AvailableCharacter(id==network.LocalClientId ? PreferredCharacter : 0),device=id==network.LocalClientId ? PreferredOnlineDevice ?? (InputDevice)Keyboard.current ?? Gamepad.current : null});
            Changed?.Invoke();
        }
        void Disconnected(ulong id)
        {
            if(leaving) return;
            if(!IsAuthority) { Status=string.IsNullOrEmpty(network.DisconnectReason) ? "Host disconnected. Returning to menu." : network.DisconnectReason; LeaveToMenu(); return; }
            var slot=Lobby.slots.Find(s=>s.owner==id);
            if(slot!=null)
            {
                if(characters.TryGetValue(slot.slot,out var player) && player) Destroy(player.gameObject);
                characters.Remove(slot.slot); Lobby.slots.Remove(slot); loaded.Remove(id);
                Flow?.CoopRewards?.CheckComplete(); BroadcastLobby(); TryBeginSimulation(); Changed?.Invoke();
            }
        }
        public void StartGame()
        {
            if(!CanStart) return;
            Lobby.running=true; InLobby=false; InGame=true; loaded.Clear(); sceneReady=false;
            BroadcastLobby();
            if(Mode==SessionMode.Online) foreach(var s in Lobby.slots) if(s.owner!=network.LocalClientId) Send(s.owner,"load",catalog.gameplayScene);
            SceneManager.LoadScene(catalog.gameplayScene);
        }
        public void SelectCharacter(int slotIndex,int choice)
        {
            if(!InLobby || Lobby.running || !catalog.CharacterAt(choice)) return;
            var slot=Lobby.slots.Find(s=>s.slot==slotIndex && IsLocalOwner(s.owner));
            if(slot==null) return;
            PreferredCharacter=choice;
            if(IsAuthority) ChangeCharacter(slot,choice); else Send(0,"character",choice.ToString());
        }
        void ChangeCharacter(LobbySlot slot,int choice)
        {
            if(!catalog.CharacterAt(choice) || Lobby.running || slot.ready) return;
            if(!Lobby.allowDuplicateCharacters && Lobby.slots.Any(s=>s.slot!=slot.slot && s.character==choice)) return;
            slot.character=choice; slot.ready=false; BroadcastLobby(); Changed?.Invoke();
        }
        void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(!InGame || scene.name!=catalog.gameplayScene) return;
            Flow=FindFirstObjectByType<StageFlowController>();
            if(!Flow) { Status="Gameplay scene has no stage flow"; LeaveToMenu(); return; }
            Flow.enabled=false; Flow.showHud=false;
            foreach(var actor in FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach(var clock in FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) clock.enabled=false;
            Flow.RunUpgrades.enabled=false;
            Flow.WorldRewards.enabled=false;
            if(IsAuthority)
            {
                foreach(var slot in Lobby.slots) SpawnPlayer(slot);
                Flow.player=characters.Values.OrderBy(p=>p.slot).First().Motor;
                if(Mode!=SessionMode.Single && !Flow.CoopRewards) Flow.gameObject.AddComponent<CoopRewards>();
                Flow.Restart(true);
                foreach(var p in characters.Values) p.GetComponent<RunBuildState>().ResetRun();
                foreach(var s in Lobby.slots.Where(s=>IsLocalOwner(s.owner))) CreateInput(s);
                sceneReady=true; loaded.Add(Mode==SessionMode.Online ? network.LocalClientId : 0); TryBeginSimulation();
            }
            else
            {
                replicaRoot=new GameObject("Authoritative world replicas");
                var slot=Lobby.slots.Find(s=>s.owner==network.LocalClientId);
                if(slot!=null) { slot.device=PreferredOnlineDevice ?? (InputDevice)Keyboard.current ?? Gamepad.current; CreateInput(slot); }
                sceneReady=true; Send(0,"loaded","ready");
            }
            Changed?.Invoke();
        }
        void TryBeginSimulation()
        {
            if(!IsAuthority || !InGame || !sceneReady) return;
            if(Mode==SessionMode.Online && Lobby.slots.Any(s=>!loaded.Contains(s.owner))) { Status="Waiting for players to load…"; return; }
            Flow.enabled=true;
            foreach(var clock in FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) clock.enabled=true;
            Status="Run started";
        }
        void SpawnPlayer(LobbySlot slot)
        {
            // Instantiate inactive to disable legacy automatic device pairing before enabling the character.
            var parent=new GameObject("Player construction"); parent.SetActive(false);
            var definition=catalog.CharacterAt(slot.character);
            var selectedPrefab=GameplayPlayerSpawner.ResolvePrefab(catalog,slot);
            var go=Instantiate(selectedPrefab,parent.transform); go.name="Player "+(slot.slot+1);
            if(definition)
            {
                var loadout=go.GetComponent<PlayerCharacterLoadout>(); if(!loadout) loadout=go.AddComponent<PlayerCharacterLoadout>();
                loadout.Apply(definition);
            }
            go.GetComponent<PlayerInput>().enabled=false; go.GetComponent<PlayerCombatInput>().enabled=false;
            var own=go.AddComponent<PlayerIdentity>(); own.slot=slot.slot; own.owner=slot.owner; own.character=slot.character;
            own.SpawnedPrefab=selectedPrefab;
            go.GetComponent<ComboUIController>().enabled=false; // Session HUD has separate slots, avoiding four overlapping canvases.
            if(!go.GetComponent<RunBuildState>()) go.AddComponent<RunBuildState>();
            go.transform.SetParent(null); Destroy(parent); characters[slot.slot]=own;
            own.GetComponent<ComboTracker>().debugLog=false;
            InitializePlayerMeta(own);
            if(Flow.framing && (Mode!=SessionMode.Online || IsLocalOwner(slot.owner))) Flow.framing.sharedPlayers.Add(own.Motor.sprite);
        }
        void CreateInput(LobbySlot slot)
        {
            if(slot.device==null) return;
            if(sources.TryGetValue(slot.slot,out var previous)) previous.Dispose();
            sources[slot.slot]=new SessionInput(catalog.playerPrefab.GetComponent<PlayerInput>().actions,slot.device);
        }
        public void ApplyCommand(int slot,PlayerCommand command)
        {
            if(!IsAuthority || !characters.TryGetValue(slot,out var player) || !player || !player.Living) return;
            if(float.IsNaN(command.move.x) || float.IsNaN(command.move.y) || float.IsInfinity(command.move.x) || float.IsInfinity(command.move.y)) return;
            var combat=player.GetComponent<ComboController>();
            if(player.GetComponent<MetaProgress>()?.OpenStation>=0)
            { player.Motor.MoveInput=Vector2.zero; combat.RequestRun(false,Vector2.zero); combat.RequestGuard(false); if(((PlayerButtons)command.buttons&PlayerButtons.Interact)!=0) Flow.GetComponent<PlayerHubController>().Execute(player.Motor,HubAction.Close,0); return; }
            var choice=Flow.CoopRewards ? Flow.CoopRewards.selections.Find(s=>s.player==player) : null;
            bool choosing=choice!=null && choice.opened && !choice.done;
            if(choosing && command.choice==-2) { Flow.CoopRewards.CloseChoice(player); return; }
            player.Motor.MoveInput=choosing || CombatClock.IsPaused ? Vector2.zero : Vector2.ClampMagnitude(command.move,1);
            combat.RequestRun(!choosing && !CombatClock.IsPaused && command.run,player.Motor.MoveInput);
            if(command.choice>=0 && choosing) { Flow.CoopRewards.Choose(player,command.choice); return; }
            combat.RequestGuard(!choosing && (command.buttons & (int)PlayerButtons.Dodge)==0 && command.guard);
            if(choosing) return;
            var buttons=(PlayerButtons)command.buttons;
            if((buttons&PlayerButtons.Attack)!=0) combat.RequestAttack();
            if((buttons&PlayerButtons.Launcher)!=0) combat.RequestLauncher();
            if((buttons&PlayerButtons.Jump)!=0) combat.RequestJump();
            if((buttons&PlayerButtons.Dodge)!=0 && !command.run) combat.RequestDodge();
            if((buttons&PlayerButtons.Skill)!=0) combat.GetComponent<PlayerSkillController>()?.RequestSkill();
            if((buttons&PlayerButtons.Interact)!=0)
            {
                if(Flow.CoopRewards && Flow.CoopRewards.Pending) Flow.CoopRewards.Interact(player);
                else { var previous=Flow.player; Flow.player=player.Motor; Flow.Interact(); Flow.player=previous; }
            }
        }
        public void ChooseLocal(int slot,int choice)
        {
            var entry=Lobby.slots.Find(s=>s.slot==slot); if(entry==null || !IsLocalOwner(entry.owner)) return;
            var command=new PlayerCommand{sequence=sources.TryGetValue(slot,out var input) ? input.Read().sequence : int.MaxValue,choice=choice};
            if(IsAuthority) ApplyCommand(slot,command); else Send(0,"input",JsonUtility.ToJson(command));
        }
        void Update()
        {
            if(connectionDeadline>0 && Time.unscaledTime>=connectionDeadline)
            {
                ClearSession(); Busy=false; Status="Connection timed out. Check the room code and try again."; Changed?.Invoke();
                return;
            }
            if(InLobby && !CharacterSelectInputActive && Mode!=SessionMode.Online)
            {
                if(Keyboard.current!=null && Keyboard.current.enterKey.wasPressedThisFrame) JoinOrReady(Keyboard.current);
                foreach(var pad in Gamepad.all) if(pad.buttonSouth.wasPressedThisFrame) JoinOrReady(pad);
            }
            else if(InLobby && !CharacterSelectInputActive && Mode==SessionMode.Online && ((Keyboard.current!=null && Keyboard.current.enterKey.wasPressedThisFrame) || (Gamepad.current!=null && Gamepad.current.buttonSouth.wasPressedThisFrame)))
            { var me=Lobby.slots.Find(s=>IsLocalOwner(s.owner)); if(me!=null) Ready(me.slot); }
            if(InLobby && !CharacterSelectInputActive && CanStart && ((Keyboard.current!=null && Keyboard.current.spaceKey.wasPressedThisFrame) || Gamepad.all.Any(p=>p.startButton.wasPressedThisFrame))) StartGame();
            if(!InGame || !sceneReady) return;
            if(!IsAuthority)
            {
                float blend=1-Mathf.Exp(-30*Time.unscaledDeltaTime);
                foreach(var pair in replicas) if(pair.Value && replicaPositions.TryGetValue(pair.Key,out var position)) pair.Value.transform.position=Vector3.Lerp(pair.Value.transform.position,position,blend);
            }
            foreach(var input in sources)
            {
                if(input.Value.Device!=null && !input.Value.Device.added)
                {
                    if(IsAuthority && characters.TryGetValue(input.Key,out var p))
                    {
                        p.Motor.MoveInput=Vector2.zero;
                        p.GetComponent<ComboController>().RequestRun(false,Vector2.zero);
                        p.GetComponent<ComboController>().RequestGuard(false);
                    }
                    continue;
                }
                var command=input.Value.Read();
                var menuState=input.Value.Menu.Read();
                if(LocalMenuOpen || Latest?.gameOver==true || Latest?.completed==true || (menuState.Start && input.Value.Device is Gamepad) || (menuState.Cancel && input.Value.Device is Keyboard))
                { command.move=Vector2.zero; command.buttons=0; command.guard=false; command.run=false; command.choice=-1; }
                if(IsAuthority) ApplyCommand(input.Key,command);
                else if(command.buttons!=0 || command.choice>=0 || Time.unscaledTime>=nextInputTime)
                { Send(0,"input",JsonUtility.ToJson(command)); nextInputTime=Time.unscaledTime+1f/30; }
            }
            if(IsAuthority)
            {
                foreach(var pair in lastInput) if(Time.unscaledTime-pair.Value>.5f)
                { var s=Lobby.slots.Find(p=>p.owner==pair.Key); if(s!=null && characters.TryGetValue(s.slot,out var p)) { p.Motor.MoveInput=Vector2.zero; p.GetComponent<ComboController>().RequestRun(false,Vector2.zero); p.GetComponent<ComboController>().RequestGuard(false); } }
                var leader=PlayerRoster.Living.FirstOrDefault(); if(leader) Flow.player=leader.Motor;
                if(Mode==SessionMode.Online && Flow.framing)
                {
                    var local=PlayerRoster.Living.FirstOrDefault(p=>IsLocalOwner(p.owner));
                    Flow.framing.sharedPlayers.Clear();
                    if(local || leader) Flow.framing.sharedPlayers.Add((local ? local : leader).Motor.sprite);
                }
                ObserveFeedback();
                if(Time.unscaledTime>=nextStateTime)
                {
                    nextStateTime=Time.unscaledTime+1f/20;
                    Latest=CaptureSnapshot();
                    if(Mode==SessionMode.Online) { string json=JsonUtility.ToJson(Latest); foreach(var s in Lobby.slots) if(s.owner!=network.LocalClientId && loaded.Contains(s.owner)) Send(s.owner,"world",json); }
                    feedback.Clear();
                    feedbackKeys.Clear();
                }
            }
        }
        void JoinOrReady(InputDevice device) { var slot=Lobby.slots.Find(s=>s.device==device); if(slot==null) JoinDevice(device); else Ready(slot.slot); }
        public WorldSnapshot CaptureSnapshot()
        {
            var result=new WorldSnapshot{validationPhase=ValidationPhase,validationOwner=ValidationOwner,tick=CombatClock.CurrentTick,stage=Flow.StageIndex,exitOpen=Flow.ExitUnlocked,completed=Flow.LevelCompleted,gameOver=!PlayerRoster.Living.Any(),rewardPending=Flow.CoopRewards && Flow.CoopRewards.Pending || Flow.WorldRewards && Flow.WorldRewards.IsPending,status=Flow.Failure};
            result.cameraLocked=Flow.ActiveCameraBounds.HasValue;
            result.encounterCameraBounds=Flow.ActiveCameraBounds ?? default;
            result.encounter=Flow.ActiveEncounterName;
            Flow.RefreshExitMarkers();
            result.nextAreaMarker=Flow.CaptureNextAreaMarker();
            // Capture the latest ground anchor and authoritative slot tint, even if
            // this snapshot precedes the cosmetic LateUpdate for the current frame.
            foreach(var p in PlayerRoster.Players) p.GetComponentInChildren<PlayerGroundIndicator>()?.Refresh();
            foreach(var r in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if(!r.enabled || !r.gameObject.activeInHierarchy || !r.sprite || r==Flow.background || r==Flow.floor) continue;
                if(r.GetComponentInParent<NetworkFeedbackVisual>()) continue;
                var boss=r.GetComponentInParent<TotemBossController>();
                result.sprites.Add(new SpriteState{id=r.GetInstanceID(),sprite=catalog.SpriteId(r.sprite),position=r.transform.position,scale=r.transform.lossyScale,rotation=r.transform.eulerAngles.z,color=r.color,order=r.sortingOrder,layer=r.sortingLayerID,flipX=r.flipX,flipY=r.flipY,hubBlend=HubDefinition && r.sharedMaterial==HubDefinition.panelBlendMaterial,teleportVersion=boss ? boss.WarpsPerformed : 0});
            }
            foreach(var p in PlayerRoster.Players)
            {
                var combo=p.GetComponent<ComboTracker>(); var playback=p.GetComponent<AttackPlayer>(); var build=p.GetComponent<RunBuildState>();
                var reward=Flow.CoopRewards ? Flow.CoopRewards.selections.Find(s=>s.player==p) : null;
                var meter=p.GetComponent<PlayerMeter>();
                result.players.Add(new PlayerState{character=p.character,meter=meter ? meter.CurrentMeter : 0,maxMeter=meter ? meter.MaxMeter : 0,visualId=p.Motor.sprite.GetInstanceID(),slot=p.slot,owner=p.owner,position=p.Motor.sprite.transform.position,hp=p.Health.Current,maxHp=p.Health.EffectiveMaximum,dead=p.Health.IsDead,hits=combo.HitCount,best=combo.BestHitCount,damage=combo.TotalDamage,activeCombo=combo.IsActive,comboTimer=combo.RemainingSeconds,frame=playback.CurrentFrame,attack=catalog.AttackId(playback.CurrentAttack),state=p.GetComponent<ComboController>().State.ToString(),upgrades=build.Acquired.Select(s=>s.upgrade.displayName+" ×"+s.count).ToArray(),choosing=reward!=null && reward.opened && !reward.done,rewardDone=reward!=null && reward.done,choices=reward!=null ? reward.choices.Select(u=>u.displayName).ToArray() : Array.Empty<string>(),descriptions=reward!=null ? reward.choices.Select(u=>u.description).ToArray() : Array.Empty<string>()});
            }
            foreach(var h in FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None))
            {
                var reaction=h.GetComponent<EnemyHitReaction>(); var boss=h.GetComponent<TotemBossController>();
                result.entities.Add(new EntityState{id=h.GetInstanceID(),kind=h.GetComponent<PlayerIdentity>() ? "Player" : boss ? "Boss" : "Enemy",name=h.name,position=h.transform.position,hp=h.Current,maxHp=h.EffectiveMaximum,dead=h.IsDead,state=boss ? boss.State.ToString() : reaction ? reaction.State.ToString() : h.GetComponent<ComboController>()?.State.ToString(),invulnerable=boss && boss.Invulnerable,phase=boss ? boss.Phase2 ? 2 : 1 : 0,vulnerabilityFrames=boss ? boss.VulnerabilityRemaining : 0,warpIndex=boss ? boss.LastWarpIndex : -1,warpVersion=boss ? boss.WarpsPerformed : 0,action=boss?.Selected?.action.ToString()});
            }
            foreach(var prop in FindObjectsByType<DestructibleObject>(FindObjectsSortMode.None)) result.entities.Add(new EntityState{id=prop.GetInstanceID(),kind=prop.kind==PropKind.CursedTotem ? "Totem" : "Prop",name=prop.name,position=prop.transform.position,hp=prop.Current,maxHp=prop.maximumHealth,dead=prop.IsBroken,respawnFrames=prop.GetComponent<BossTotem>()?.RespawnRemaining ?? 0});
            foreach(var wave in FindObjectsByType<TotemBreakWave>(FindObjectsSortMode.None)) result.totemWaves.Add(new TotemWaveState{id=wave.GetInstanceID(),position=wave.transform.position,radius=wave.Radius,maximumRadius=wave.MaximumRadius,age=wave.Age,duration=wave.Duration});
            foreach(var projectile in FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) result.entities.Add(new EntityState{id=projectile.GetInstanceID(),kind="Projectile",name=projectile.name,position=projectile.transform.position});
            if(Flow.CoopRewards && Flow.CoopRewards.Chapel) result.entities.Add(new EntityState{id=Flow.CoopRewards.Chapel.GetInstanceID(),kind="Chapel",name="Reward chapel",position=Flow.CoopRewards.Chapel.position});
            CaptureHubState(result); result.feedback.AddRange(feedback); return result;
        }
        public void ApplySnapshot(WorldSnapshot snapshot)
        {
            if(IsAuthority || snapshot==null || Latest!=null && snapshot.tick<Latest.tick) return;
            bool stageChanged=Latest==null || Latest.stage!=snapshot.stage;
            Latest=snapshot;
            SaveClientMeta(snapshot);
            if(Flow.framing) Flow.framing.SetEncounterBounds(snapshot.cameraLocked ? snapshot.encounterCameraBounds : (Rect?)null);
            if(snapshot.stage>=0 && snapshot.stage<catalog.level.stages.Count) Flow.ApplyStageArt(catalog.level.stages[snapshot.stage]);
            var alive=new HashSet<int>();
            foreach(var state in snapshot.sprites)
            {
                alive.Add(state.id);
                if(!replicas.TryGetValue(state.id,out var r)) { var go=new GameObject("Network visual "+state.id); go.transform.SetParent(replicaRoot.transform); r=go.AddComponent<SpriteRenderer>(); replicas[state.id]=r; r.transform.position=state.position; }
                if(!defaultReplicaMaterial) defaultReplicaMaterial=r.sharedMaterial;
                r.sharedMaterial=state.hubBlend && HubDefinition ? HubDefinition.panelBlendMaterial : defaultReplicaMaterial;
                r.sprite=state.sprite>=0 ? catalog.SpriteAt(state.sprite) : FallbackSprite();
                replicaPositions[state.id]=state.position;
                if(stageChanged || !replicaTeleports.TryGetValue(state.id,out int teleport) || teleport!=state.teleportVersion || (r.transform.position-state.position).sqrMagnitude>4) r.transform.position=state.position;
                replicaTeleports[state.id]=state.teleportVersion;
                r.transform.rotation=Quaternion.Euler(0,0,state.rotation); r.transform.localScale=state.scale;
                r.color=state.color; r.flipX=state.flipX; r.flipY=state.flipY; r.sortingOrder=state.order; r.sortingLayerID=state.layer;
            }
            foreach(int id in replicas.Keys.Where(id=>!alive.Contains(id)).ToArray()) { Destroy(replicas[id].gameObject); replicas.Remove(id); replicaPositions.Remove(id); replicaTeleports.Remove(id); }
            var liveWaves=new HashSet<int>();
            foreach(var state in snapshot.totemWaves)
            {
                liveWaves.Add(state.id);
                bool created=!waveReplicas.TryGetValue(state.id,out var visual);
                if(created) { var go=new GameObject("Network Totem wave "+state.id); go.transform.SetParent(replicaRoot.transform); visual=go.AddComponent<NetworkTotemWaveVisual>(); waveReplicas[state.id]=visual; }
                visual.Apply(state,created || stageChanged);
            }
            foreach(int id in waveReplicas.Keys.Where(id=>!liveWaves.Contains(id)).ToArray()) { Destroy(waveReplicas[id].gameObject); waveReplicas.Remove(id); }
            var local=snapshot.players.Find(p=>IsLocalOwner(p.owner));
            if(local!=null && local.dead) local=snapshot.players.Where(p=>!p.dead).OrderBy(p=>(p.position-local.position).sqrMagnitude).FirstOrDefault() ?? local;
            if(Flow.framing && local!=null)
            {
                Flow.framing.sharedPlayers.Clear();
                replicas.TryGetValue(local.visualId,out var renderer);
                if(renderer) Flow.framing.sharedPlayers.Add(renderer);
                var stage=catalog.level.stages[snapshot.stage]; Flow.framing.SetStageBounds(-stage.artWidth*.5f,stage.artWidth*.5f);
            }
            foreach(var cue in snapshot.feedback)
            {
                if(cue.id<=lastEffectId) continue; lastEffectId=cue.id;
                if(cue.signal=="Parry") AttackFeedback.PlayRemoteFeedback((catalog.CharacterAt(cue.character)?.defense ?? catalog.playerPrefab.GetComponent<ComboController>().defenseData).parryFeedback,cue.point,cue.facing,true);
                else if(cue.attack>=0 && cue.attack<catalog.attacks.Length) AttackFeedback.PlayRemote(catalog.attacks[cue.attack],cue.point,cue.facing,cue.impact,cue.signal,cue.source);
            }
            Changed?.Invoke();
        }
        Sprite FallbackSprite()
        {
            if(fallbackSprite) return fallbackSprite;
            var tex=new Texture2D(1,1); tex.SetPixel(0,0,Color.white); tex.Apply();
            return fallbackSprite=Sprite.Create(tex,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
        }
        void ObserveFeedback()
        {
            observed.RemoveWhere(p=>!p);
            foreach(var p in FindObjectsByType<AttackPlayer>(FindObjectsSortMode.None))
            {
                if(!observed.Add(p)) continue;
                p.FrameEvent+=signal=> { if(p && (signal=="Swing" || signal=="Telegraph" || signal=="Scream" || signal=="DiveWhoosh" || signal=="DiveLanding") && p.CurrentAttack) AddFeedback(p,false,(Vector2)p.motor.transform.position + new Vector2(0,signal=="DiveWhoosh" ? p.motor.Height+.3f : 0),signal); };
                p.Stopped+=attack=> { if(p && attack && attack.feedback!=null && (attack.feedback.areaWarning || attack.feedback.directionalWaveWarning)) AddFeedback(p,false,p.motor.transform.position,"StopArea",attack); };
                var defender=p.GetComponent<ComboController>();
                var capture=p.GetComponent<CombatGrabController>();
                if(capture) capture.HeldStrikeFeedback+=(attack,point,signal)=> { if(p && attack) AddFeedback(p,true,point,signal,attack); };
                var armor=p.GetComponent<HitCountArmor>();
                if(armor) armor.Feedback+=(attack,signal)=> { if(p && attack) AddFeedback(p,true,(Vector2)p.motor.sprite.transform.position+Vector2.up*.6f,signal,attack); };
                if(defender) defender.DefenseImpact+=outcome=> { if(p && outcome==DefenseFeedback.Parry) AddFeedback(p,false,(Vector2)p.motor.transform.position+new Vector2(p.motor.Facing*.45f,.65f),"Parry"); };
                var hitbox=p.GetComponent<AttackHitbox>(); if(hitbox) hitbox.HitConfirmed+=(point,outcome)=>{ if(p && outcome==CombatHitOutcome.Hit && p.CurrentAttack) AddFeedback(p,true,point); };
            }
        }
        void AddFeedback(AttackPlayer player,bool impact,Vector2 point,string signal=null,AttackData stoppedAttack=null)
        {
            if(!feedbackKeys.Add((player.GetInstanceID(),CombatClock.CurrentTick,impact ? signal ?? "Impact" : signal ?? "Swing"))) return;
            feedback.Add(new FeedbackState{id=++effectId,character=player.GetComponent<PlayerIdentity>()?.character ?? 0,source=player.GetInstanceID(),attack=catalog.AttackId(stoppedAttack ? stoppedAttack : player.CurrentAttack),impact=impact,signal=signal,point=point,facing=player.Facing});
        }
        void ProjectileImpact(CombatProjectile projectile,Vector2 point)
        {
            if(!IsAuthority || !InGame || !projectile.feedbackAttack) return;
            feedback.Add(new FeedbackState{id=++effectId,source=projectile.GetInstanceID(),attack=catalog.AttackId(projectile.feedbackAttack),impact=true,point=point,facing=projectile.Velocity.x<0 ? -1 : 1});
        }
        public void QueueEncounterFeedback(AttackData attack,Vector2 point,int facing,int source)
        {
            if(!IsAuthority || !InGame || !attack) return;
            feedback.Add(new FeedbackState{id=++effectId,source=source,attack=catalog.AttackId(attack),impact=true,point=point,facing=facing});
        }
        void ProjectileDeflected(CombatProjectile projectile,Vector2 point)
        {
            if (!IsAuthority || !InGame || !projectile.deflectFeedback) return;
            feedback.Add(new FeedbackState{id=++effectId,source=projectile.GetInstanceID(),attack=catalog.AttackId(projectile.deflectFeedback),impact=true,point=point,facing=projectile.Velocity.x<0 ? -1 : 1});
        }
        public void Retry()
        {
            if(!IsAuthority || !InGame || PlayerRoster.Living.Any() && !Flow.LevelCompleted) return;
            if(Flow.GetComponent<PlayerHubController>()) { ReturnToHub(); return; }
            foreach(var p in PlayerRoster.Players) { p.GetComponent<RunBuildState>().ResetRun(); p.Health.Restore(); }
            Flow.Restart(true);
        }
        public void LeaveToMenu()
        {
            string message=Status; ClearSession(); Busy=false; SceneManager.LoadScene("MainMenu"); Status=message; Changed?.Invoke();
        }
        public void CancelConnection() { if(!Busy) return; ClearSession(); Busy=false; Status="Connection cancelled"; Changed?.Invoke(); }
        void StopNetwork()
        {
            if(!network) return;
            network.OnClientConnectedCallback-=Connected; network.OnClientDisconnectCallback-=Disconnected;
            network.Shutdown(); Destroy(network.gameObject); network=null;
        }
        void ClearSession()
        {
            connectionDeadline=0;
            generation++;
            leaving=true; StopNetwork(); leaving=false;
            foreach(var source in sources.Values) source.Dispose(); sources.Clear();
            characters.Clear(); commandSequences.Clear(); lastInput.Clear(); loaded.Clear(); observed.Clear(); feedback.Clear(); feedbackKeys.Clear();
            if(replicaRoot) Destroy(replicaRoot); replicaRoot=null;
            replicas.Clear(); replicaPositions.Clear(); replicaTeleports.Clear(); waveReplicas.Clear();
            InGame=InLobby=sceneReady=LocalMenuOpen=false; Latest=null; Flow=null; lastEffectId=effectId=0;
            Lobby=new LobbyState(); Time.timeScale=1;
            ValidationPhase=null; ValidationOwner=0;
            remoteMeta.Clear(); savedMeta=null;
            submittedCommandSequence=0;
        }
    }
}
