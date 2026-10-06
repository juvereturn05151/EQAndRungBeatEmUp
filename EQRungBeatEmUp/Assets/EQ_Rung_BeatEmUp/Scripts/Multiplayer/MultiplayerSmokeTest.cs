#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BeatEmUp
{
    // Explicit opt-in only. Runs the real session/transport in separate development player processes.
    public sealed class MultiplayerSmokeTest : MonoBehaviour
    {
        public static bool Finished { get; private set; }
        public static bool Passed { get; private set; }
        public static readonly List<string> Results=new List<string>();
        string output;
        int count=2;
        bool failed;
        MultiplayerSession session;
        Gamepad pad;
        readonly HashSet<int> stages=new HashSet<int>();
        readonly HashSet<string> kinds=new HashSet<string>();
        readonly HashSet<string> commandsSent=new HashSet<string>();
        string lastPhase;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            var args=Environment.GetCommandLineArgs();
            if(args.Any(s=>s=="--coop-test-host" || s=="--coop-test-client" || s=="--coop-test-local")) new GameObject("Opt-in multiplayer smoke test").AddComponent<MultiplayerSmokeTest>();
        }
        public void StartLocal(int players,string path)
        {
            count=players; output=path; Begin(); StartCoroutine(Local());
        }
        void Begin()
        {
            DontDestroyOnLoad(gameObject); Finished=Passed=false; Results.Clear(); session=MultiplayerSession.Active;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        }
        void Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(!args.Any(s=>s.StartsWith("--coop-test-"))) return;
            for(int i=0;i<args.Length-1;i++) { if(args[i]=="--coop-count") count=int.Parse(args[i+1]); if(args[i]=="--coop-output") output=args[i+1]; }
            Begin();
            if(args.Contains("--coop-test-host")) StartCoroutine(Host());
            else if(args.Contains("--coop-test-client")) StartCoroutine(Client());
            else StartCoroutine(Local());
        }
        void Check(bool condition,string label)
        {
            Results.Add((condition ? "PASS: " : "FAIL: ")+label); if(!condition) failed=true;
        }
        IEnumerator WaitFor(Func<bool> condition,float timeout=20)
        {
            float end=Time.realtimeSinceStartup+timeout;
            while(!condition() && Time.realtimeSinceStartup<end) yield return null;
            if(!condition()) { Check(false,"Timed out waiting for multiplayer state"); Finish(); }
        }
        IEnumerator Local()
        {
            InputSystem.AddDevice<Keyboard>();
            session.BeginLocal(count==1);
            if(count>1)
            {
                for(int i=1;i<count;i++) session.JoinDevice(InputSystem.AddDevice<Gamepad>());
                foreach(var slot in session.Lobby.slots) if(!slot.ready) session.Ready(slot.slot);
                Check(session.CanStart,"Local joined players all ready"); session.StartGame();
            }
            yield return WaitFor(()=>session.InGame && session.Flow && PlayerRoster.Players.Count()==count);
            if(Finished) yield break;
            Check(PlayerRoster.Players.Count()==count,"Independent player instances: "+count);
            var players=PlayerRoster.Players.ToArray();
            Check(players.Select(p=>p.GetComponent<ComboController>()).Distinct().Count()==count,"Independent combat controllers");
            Check(players.Select(p=>p.GetComponent<RunBuildState>()).Distinct().Count()==count,"Independent run builds");
            Check(players.Select(p=>p.GetComponent<ComboTracker>()).Distinct().Count()==count,"Independent landed-hit trackers");
            if(count>1)
            {
                var device=session.Lobby.slots.First(s=>s.slot==1).device as Gamepad;
                var start=players.Select(p=>p.transform.position).ToArray();
                long movementStartTick=CombatClock.CurrentTick;
                InputSystem.QueueStateEvent(device,new GamepadState{leftStick=Vector2.right}); InputSystem.Update();
                Check(session.LocalInput(1).Actions.FindAction("Move").ReadValue<Vector2>().x>.9f,"P2 private action copy reads only its paired gamepad");
                yield return WaitFor(()=>CombatClock.CurrentTick>=movementStartTick+12,10);
                InputSystem.QueueStateEvent(device,new GamepadState()); InputSystem.Update(); yield return null;
                Check(players[1].transform.position.x>start[1].x+.1f,"Paired P2 gamepad moves P2");
                Check(Mathf.Abs(players[0].transform.position.x-start[0].x)<.01f,"P2 gamepad does not move P1");
                Check(session.Flow.framing.sharedPlayers.Count==count,"One shared camera tracks all couch players");
            }
            yield return CoreLoop(); Finish();
        }
        IEnumerator Host()
        {
            session.DirectHost();
            yield return WaitFor(()=>session.Lobby.slots.Count==count);
            if(Finished) yield break;
            session.Ready(0);
            yield return WaitFor(()=>session.CanStart);
            if(Finished) yield break;
            Check(session.Lobby.slots.Count==count,"Connected host plus "+(count-1)+" clients");
            session.StartGame(); yield return WaitFor(()=>session.Flow && PlayerRoster.Players.Count()==count);
            if(Finished) yield break;
            var start=PlayerRoster.Players.Where(p=>p.owner!=session.Network.LocalClientId).ToDictionary(p=>p.slot,p=>p.transform.position);
            yield return new WaitForSecondsRealtime(1.7f);
            Check(PlayerRoster.Players.Where(p=>p.owner!=session.Network.LocalClientId).Any(p=>(p.transform.position-start[p.slot]).sqrMagnitude>.005f),"Remote device commands move authoritative host characters");
            yield return RemoteCombat();
            if(Finished) yield break;
            yield return CoreLoop();
            // Keep the final synchronized state visible before shutdown.
            yield return new WaitForSecondsRealtime(1);
            var remote=session.Lobby.slots.FirstOrDefault(s=>s.owner!=session.Network.LocalClientId);
            if(remote!=null)
            {
                session.Network.DisconnectClient(remote.owner);
                yield return new WaitForSecondsRealtime(.3f);
                Check(session.Lobby.slots.Count==count-1 && PlayerRoster.Players.Count()==count-1,"Disconnected player removed; remaining party retained");
            }
            session.LeaveToMenu(); yield return new WaitForSecondsRealtime(.4f); Finish();
        }
        IEnumerator Client()
        {
            pad=InputSystem.AddDevice<Gamepad>(); session.PreferredOnlineDevice=pad; session.DirectJoin();
            yield return WaitFor(()=>session.InLobby && session.Lobby.slots.Any(s=>session.IsLocalOwner(s.owner)));
            if(Finished) yield break;
            session.Ready(session.Lobby.slots.First(s=>session.IsLocalOwner(s.owner)).slot);
            yield return WaitFor(()=>session.InGame && session.Latest!=null);
            if(Finished) yield break;
            Check(!session.IsAuthority,"Client never owns combat simulation");
            Check(!FindFirstObjectByType<CombatClock>().enabled,"Client combat clock stays disabled");
            Check(!FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Any(e=>e.isActiveAndEnabled),"No independent client enemy AI");
            Check(!FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None).Any(),"No duplicate client health/damage actors");
            float start=Time.realtimeSinceStartup, limit=start+130;
            bool sawOwnAttack=false;
            while(session.InGame && Time.realtimeSinceStartup<limit)
            {
                var world=session.Latest;
                if(world!=null)
                {
                    stages.Add(world.stage); foreach(var entity in world.entities) kinds.Add(entity.kind);
                    float elapsed=Time.realtimeSinceStartup-start;
                    // Actual isolated input source -> NGO command -> authoritative combat.
                    var state=new GamepadState{leftStick=elapsed<.8f ? Vector2.right : Vector2.zero};
                    if(string.IsNullOrEmpty(world.validationPhase) && elapsed>.3f && elapsed<.4f) state=state.WithButton(GamepadButton.West);
                    if(session.IsLocalOwner(world.validationOwner))
                    {
                        if(world.validationPhase=="Guard" || world.validationPhase=="Block") state=state.WithButton(GamepadButton.LeftShoulder);
                        if(world.validationPhase=="Dodge") state=state.WithButton(GamepadButton.RightShoulder);
                    }
                    InputSystem.QueueStateEvent(pad,state);
                    var local=world.players.FirstOrDefault(p=>session.IsLocalOwner(p.owner));
                    if(local!=null && local.attack>=0) sawOwnAttack=true;
                    if(local!=null && session.IsLocalOwner(world.validationOwner)) DriveRemoteCombat(world,local);
                    if(local!=null && local.choosing) session.ChooseLocal(local.slot,0);
                    if(world.completed) break;
                }
                yield return null;
            }
            InputSystem.QueueStateEvent(pad,new GamepadState()); InputSystem.Update();
            Check(session.Latest!=null && session.Latest.completed,"Client reaches authoritative run completion");
            Check(stages.Count==session.catalog.level.stages.Count,"Client observes every synchronized stage");
            foreach(string kind in new[]{"Player","Enemy","Projectile","Totem","Boss","Chapel"}) Check(kinds.Contains(kind),"Client receives "+kind+" lifecycle");
            Check(session.Latest!=null && session.Latest.players.Count==count,"Client receives all player health/ownership states");
            Check(sawOwnAttack,"Client input starts an authoritative attack and receives its frame/state");
            yield return WaitFor(()=>!session.InGame && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu",10);
            if(Finished) yield break;
            Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu","Host shutdown/disconnect returns client to menu");
            Finish();
        }
        void SendOnce(string key,PlayerButtons button)
        {
            if(commandsSent.Add(key)) session.SubmitOwnedCommand(new PlayerCommand{buttons=(int)button});
        }
        void DriveRemoteCombat(WorldSnapshot world,PlayerState player)
        {
            if(lastPhase!=world.validationPhase) { lastPhase=world.validationPhase; commandsSent.Clear(); }
            var combat=session.catalog.playerPrefab.GetComponent<ComboController>();
            if(world.validationPhase=="Ground" || world.validationPhase=="Air")
            {
                SendOnce("Punch1",PlayerButtons.Attack);
                if(player.hits>=1 && player.attack==session.catalog.AttackId(combat.groundCombo[0])) SendOnce("Punch2",PlayerButtons.Attack);
                if(player.hits>=2 && player.attack==session.catalog.AttackId(combat.groundCombo[1])) SendOnce("Third",world.validationPhase=="Ground" ? PlayerButtons.Attack : PlayerButtons.Launcher);
                if(world.validationPhase=="Air")
                {
                    if(player.attack==session.catalog.AttackId(combat.launcher)) SendOnce("Jump",PlayerButtons.Jump);
                    if(player.hits>=3 && player.attack<0 && player.state==CombatState.Jumping.ToString()) SendOnce("Air1",PlayerButtons.Attack);
                    if(player.hits>=4 && player.attack==session.catalog.AttackId(combat.airCombo[0])) SendOnce("Air2",PlayerButtons.Attack);
                    if(player.hits>=5 && player.attack==session.catalog.AttackId(combat.airCombo[1])) SendOnce("Air3",PlayerButtons.Attack);
                }
            }
            if(world.validationPhase=="Dive") SendOnce("Dive",PlayerButtons.Launcher);
        }
        IEnumerator RemoteCombat()
        {
            var flow=session.Flow; flow.enabled=false;
            var player=PlayerRoster.Players.First(p=>p.owner!=session.Network.LocalClientId);
            var combat=player.GetComponent<ComboController>(); var tracker=player.GetComponent<ComboTracker>();
            var prefab=flow.level.stages.SelectMany(s=>s.encounters).SelectMany(e=>e.waves).SelectMany(w=>w.enemySpawns).Select(s=>s.prefab).First(p=>p && p.GetComponent<EnemyCombat>() && !p.GetComponent<TotemBossController>());
            var npc=Instantiate(prefab); npc.GetComponent<EnemyCombat>().passiveTrainingDummy=true;
            var enemy=npc.GetComponent<EnemyHitReaction>(); enemy.health.maximumHealth=500;
            session.ValidationOwner=player.owner;
            Action reset=()=>
            {
                session.ValidationPhase="Reset";
                player.Health.Restore(); player.Motor.ResetForStage(new Vector2(-1,0)); player.Motor.Face(1);
                enemy.health.Restore(); enemy.motor.ResetForStage(new Vector2(-.15f,0));
            };
            reset(); yield return new WaitForSecondsRealtime(.15f); session.ValidationPhase="Ground";
            yield return WaitFor(()=>tracker.HitCount==3,5); if(Finished) yield break;
            Check(tracker.IsActive && tracker.TotalDamage==31,"Remote Punch1 -> Punch2 -> Punch3 is one actual 31-damage combo");
            Check(PlayerRoster.Players.Where(p=>p!=player).All(p=>p.GetComponent<ComboTracker>().HitCount==0),"Remote combo belongs only to its player");
            reset(); yield return new WaitForSecondsRealtime(.15f); session.ValidationPhase="Air";
            yield return WaitFor(()=>tracker.HitCount==6,7); if(Finished) yield break;
            Check(tracker.IsActive && tracker.TotalDamage==500-enemy.health.Current,"Remote ground -> launcher -> jump -> three air punches remains one six-hit combo");
            reset(); combat.RequestJump(); long tick=CombatClock.CurrentTick;
            yield return WaitFor(()=>CombatClock.CurrentTick>=tick+12,5); if(Finished) yield break;
            enemy.motor.Launch(4,0); enemy.motor.Simulate(.1f);
            npc.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{damage=8,hitstunFrames=100,canHitAirborne=true,knockback=0},1,player.Motor);
            session.ValidationPhase="Dive";
            yield return WaitFor(()=>tracker.HitCount==2,5); if(Finished) yield break;
            Check(tracker.TotalDamage==22,"Remote airborne dive follows up through authoritative collision");
            reset(); yield return new WaitForSecondsRealtime(.15f); session.ValidationPhase="Guard";
            yield return WaitFor(()=>combat.State==CombatState.GuardEnter,5); if(Finished) yield break;
            var hurtbox=player.GetComponentInChildren<CombatHurtbox>();
            hurtbox.Receive(new AttackHitboxData{damage=8,hitstunFrames=20},-1,enemy.motor);
            Check(hurtbox.LastHitOutcome==CombatHitOutcome.Parry && player.Health.Current==player.Health.maximumHealth,"Remote guard input opens an authoritative parry");
            session.ValidationPhase="Block";
            yield return WaitFor(()=>combat.State==CombatState.GuardHold,5); if(Finished) yield break;
            hurtbox.Receive(new AttackHitboxData{damage=8,hitstunFrames=20},-1,enemy.motor);
            Check(hurtbox.LastHitOutcome==CombatHitOutcome.Block,"Held remote guard resolves block on host");
            session.ValidationPhase="Reset"; yield return new WaitForSecondsRealtime(.2f);
            session.ValidationPhase="Dodge";
            yield return WaitFor(()=>combat.DodgeInvulnerable,5); if(Finished) yield break;
            float before=player.Health.Current; bool accepted=hurtbox.Receive(new AttackHitboxData{damage=8},-1,enemy.motor);
            Check(!accepted && player.Health.Current==before,"Remote dodge grants authored authoritative invulnerability");
            session.ValidationPhase=null; Destroy(npc); player.Health.Restore(); flow.EnterStage(0); flow.enabled=true;
        }
        IEnumerator CoreLoop()
        {
            var flow=session.Flow; int stageCount=flow.level.stages.Count;
            var players=PlayerRoster.Players.ToArray();
            for(int stage=0;stage<stageCount;stage++)
            {
                if(flow.StageIndex!=stage) flow.EnterStage(stage);
                Check(flow.StageIndex==stage,"Authority entered stage "+stage);
                if(flow.CurrentStage.hub)
                {
                    var hub=flow.GetComponent<PlayerHubController>(); var leader=players.First(p=>p.Living);
                    leader.Motor.ResetForStage(hub.definition.stations[3]); hub.Interact(leader.Motor);
                    Check(hub.Execute(leader.Motor,HubAction.EnterWorld,0) && flow.StageIndex==stage+1,"Physical Hub gate begins party run");
                    continue;
                }
                yield return new WaitForSecondsRealtime(.25f);
                if(flow.CurrentStage.IsSafeStage) Check(players.All(p=>p.Health.SafeStageProtection),"Safe stage protects every player");
                int loops=0; bool projectileTested=false;
                while(!flow.EncountersComplete && loops++<30)
                {
                    foreach(var encounter in flow.CurrentStage.encounters)
                    {
                        if(encounter.trigger==EncounterTrigger.PlayerZone)
                        { players[players.Length-1].Motor.ResetForStage(encounter.triggerZone.center); flow.Tick(.05f); }
                        flow.SignalEncounter(encounter.encounterId);
                        foreach(var wave in encounter.waves) flow.SignalWave(encounter.encounterId,wave.waveId);
                    }
                    flow.Tick(.5f); yield return new WaitForSecondsRealtime(.18f);
                    var boss=flow.StageEnemies.Select(h=>h.GetComponent<TotemBossController>()).FirstOrDefault(b=>b);
                    if(boss && flow.RemainingTotems>0)
                    {
                        float hp=boss.GetComponent<CharacterHealth>().Current;
                        bool hit=boss.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{damage=1,canHitAirborne=true},1,players[0].Motor);
                        Check(boss.Invulnerable && !hit && boss.GetComponent<CharacterHealth>().Current==hp,"Boss rejects damage while cursed totems remain");
                    }
                    if (boss && flow.Destructibles.Any(p => p && !p.IsBroken)) boss.GetComponent<CharacterMotor>().SnapGrabToGround(flow.Destructibles.First(p => p && !p.IsBroken).transform.position);
                    foreach(var prop in flow.Destructibles.Where(p=>p && !p.IsBroken))
                    {
                        players[0].Motor.ResetForStage(new Vector2(prop.transform.position.x-.4f,prop.transform.position.y));
                        prop.Receive(new AttackHitboxData{damage=10000,laneTolerance=2},1,players[0].Motor);
                    }
                    if(boss)
                    {
                        long gateTick=CombatClock.CurrentTick;
                        yield return WaitFor(()=>CombatClock.CurrentTick>=gateTick+2,5); if(Finished) yield break;
                        Check(flow.RemainingTotems==0 && !boss.Invulnerable,"Authoritative radial Totem wave physically reaches boss and opens vulnerability");
                    }
                    var thrower=flow.StageEnemies.Select(h=>h.GetComponent<EnemyProjectileAttack>()).FirstOrDefault(p=>p);
                    if(thrower && !projectileTested)
                    {
                        projectileTested=true;
                        foreach(var player in players) player.Motor.ResetForStage((Vector2)thrower.transform.position+Vector2.left*2);
                        float hp=players.Sum(p=>p.Health.Current);
                        yield return new WaitForSecondsRealtime(2);
                        Check(players.Sum(p=>p.Health.Current)<hp,"Authoritative Thrower projectile damages a player");
                    }
                    foreach(var enemy in flow.LivingEnemies.ToArray()) enemy.Damage(10000);
                    flow.Tick(.2f); yield return new WaitForSecondsRealtime(.1f);
                }
                Check(flow.EncountersComplete,"Stage encounters resolve once: "+stage);
                if(flow.CurrentStage.completionMode==StageCompletion.Event) flow.CompleteStageEvent();
                foreach(var player in players) { player.GetComponent<ComboController>().ResetCombo(); player.Motor.ResetForStage(flow.CurrentStage.playerExitPoint); }
                flow.Tick(.1f);
                if(flow.CoopRewards && flow.CoopRewards.Pending)
                {
                    var reward=flow.CoopRewards; Check(reward.Chapel,"One authoritative co-op chapel");
                    foreach(var player in players.Where(p=>p.Living))
                    {
                        player.Motor.ResetForStage(reward.Chapel.position); Check(reward.Interact(player),"P"+(player.slot+1)+" opens independent reward");
                    }
                    yield return new WaitForSecondsRealtime(.25f);
                    var choices=reward.selections.Where(s=>s.player && s.player.Living && !s.done).ToArray();
                    if(choices.Length>0)
                    {
                        var first=choices[0]; Check(reward.Choose(first.player,0),"Host validates upgrade selection");
                        if(choices.Length>1) Check(reward.Pending && !flow.ExitUnlocked,"Exit waits for other players' rewards");
                    }
                    foreach(var choice in choices.Skip(1))
                    {
                        // Remote players choose via normal client input when online.
                        if(session.Mode!=SessionMode.Online || session.IsLocalOwner(choice.player.owner)) reward.Choose(choice.player,0);
                    }
                    yield return WaitFor(()=>!reward.Pending,8);
                    if(Finished) yield break;
                    Check(players.Where(p=>p.Living).All(p=>p.GetComponent<RunBuildState>().Acquired.Count>0),"Independent upgrades are retained");
                }
                else if(flow.WorldRewards && flow.WorldRewards.IsPending)
                {
                    var reward=flow.WorldRewards; flow.player.ResetForStage(reward.Chapel.transform.position); reward.Interact();
                    if(reward.State==WorldRewardState.Choosing) { flow.player.ResetForStage(reward.ChoiceObjects[0].transform.position); reward.Interact(); }
                }
                // Allow clients to observe this stage and its reward before transition.
                yield return new WaitForSecondsRealtime(.35f);
                if(flow.StageIndex==stage)
                {
                    foreach(var player in players.Where(p=>p.Living)) { player.GetComponent<ComboController>().ResetCombo(); player.Motor.ResetForStage(flow.CurrentStage.playerExitPoint); }
                    flow.Tick(.1f);
                }
                Check(flow.StageIndex==stage+1 || stage==stageCount-1 && flow.LevelCompleted,"Authoritative stage exit advances: "+stage);
            }
            Check(flow.LevelCompleted,"Full hub-to-exit run completes");
            if(players.Length>1)
            {
                players[0].Health.SafeStageProtection=false; players[0].Health.Damage(10000);
                Check(!session.CaptureSnapshot().gameOver,"One defeated player does not end the party run");
            }
        }
        void Finish()
        {
            if(Finished) return; Finished=true; Passed=!failed;
            Results.Add("MULTIPLAYER SMOKE "+(Passed ? "PASSED" : "FAILED")+" ("+count+" players)");
            if(!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))); File.WriteAllLines(output,Results); }
            Debug.Log(Results[Results.Count-1]);
            if(!Application.isEditor) Application.Quit(Passed ? 0 : 1);
        }
    }
}
#endif
