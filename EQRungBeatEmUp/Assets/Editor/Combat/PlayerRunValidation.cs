using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerRunValidation
{
    const string Pending="PlayerRun.Validation";
    static readonly List<string> results=new List<string>();
    static ComboController actor;static CombatClock clock;static GameObject root;
    static PlayerRunValidation(){EditorApplication.update+=Poll;}
    [MenuItem("Beat Em Up/Characters/Validate dash-to-run (Play Mode)")]
    public static void BuildAndValidate()
    {
        PlayerRunSetup.Build();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS: "+label);}
    static void Step(int count){for(int i=0;i<count;i++)clock.StepFrame();}
    static void Fixture(int index)
    {
        if(root)Object.DestroyImmediate(root);root=new GameObject("Run validation fixture");
        var definition=AssetDatabase.LoadAssetAtPath<PlayableCharacterData>(PlayerRunSetup.Definitions[index]);
        actor=Object.Instantiate(definition.prefab,root.transform).GetComponent<ComboController>();
        actor.GetComponent<PlayerCombatInput>().enabled=false;
        actor.motor.arenaMin=new Vector2(-100,-5);actor.motor.arenaMax=new Vector2(100,5);actor.motor.ResetForStage(Vector2.zero);
        clock=Object.FindFirstObjectByType<CombatClock>();clock.enabled=false;
    }
    static void Hold(bool guard,Vector2 direction)
    {
        actor.motor.MoveInput=direction;bool directional=PlayerCombatInput.HasDefenseDirection(direction);
        actor.RequestRun(guard&&directional,direction);actor.RequestGuard(guard&&!directional);
    }
    static void StartRun(Vector2 direction)
    {
        actor.ResetRunInput();actor.health.Restore();actor.motor.ResetForStage(Vector2.zero);Hold(true,direction);Step(actor.EffectiveDashToRunFrame);
        Check(actor.IsRunning,"Held direction and Guard complete dash-to-run transition");
    }
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        SessionState.SetBool(Pending,false);results.Clear();bool passed=false;
        var oldBackground=InputSystem.settings.backgroundBehavior;
        var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
        try
        {
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            for(int i=0;i<2;i++){Fixture(i);RunCases(i);InputCases(i);}
            if(root)Object.DestroyImmediate(root);root=null;
            DefenseRegression();passed=true;
        }
        catch(Exception e){results.Add("FAIL: "+e);Debug.LogException(e);}
        finally
        {
            if(root)Object.DestroyImmediate(root);
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/PlayerRunValidationResults.txt",results);
            Debug.Log("PLAYER RUN VALIDATION "+(passed?"PASSED":"FAILED"));
            if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);else EditorApplication.ExitPlaymode();
        }
    }
    static void RunCases(int index)
    {
        string name=PlayerRunSetup.Names[index];
        Check(actor.runData&&actor.runData==actor.GetComponent<PlayerCharacterLoadout>().character.run&&actor.runData.poses.Length==8,name+" loads its own eight run poses through existing loadout");
        Check(actor.runData.poses.All(p=>p.sprite&&p.sprite.pixelsPerUnit==100&&p.sprite.texture.filterMode==FilterMode.Point),name+" run sprites use current pixel density and point filtering");
        Check(actor.animationDriver.animator.HasState(0,Animator.StringToHash("Base Layer.Run")),name+" existing locomotion Animator includes Run clip for preview");
        Hold(true,Vector2.zero);Check(actor.GuardActive&&!actor.IsRunning,name+" standing Guard stays guard/parry");Hold(false,Vector2.zero);
        Hold(true,Vector2.right);Check(actor.State==CombatState.Dodge&&!actor.IsRunning,name+" first enters existing dash animation/state");
        Step(actor.EffectiveDashToRunFrame-1);Check(actor.State==CombatState.Dodge,name+" remains in Dash before transition frame");
        Step(1);Check(actor.IsRunning&&!actor.IsDefenseState&&!actor.DodgeInvulnerable&&!actor.GuardActive,name+" Run begins at tuned frame with no guard or dash invulnerability");
        Check(actor.motor.sprite.sprite==actor.runData.poses[0].sprite&&!actor.animationDriver.animator.enabled,name+" first Run pose takes over through existing frame-driven sprite control");
        float start=actor.transform.position.x;Step(30);float run=actor.transform.position.x-start;
        Hold(false,Vector2.right);Check(!actor.IsRunning&&actor.State==CombatState.Idle&&actor.motor.GroundSpeedOverride==0,name+" releasing Guard immediately returns to walk");
        start=actor.transform.position.x;Step(30);float walk=actor.transform.position.x-start;
        Check(run>walk*1.5f&&Mathf.Abs(run-actor.runData.runSpeed*.5f)<.01f,name+" run distance is clearly faster than walk through same motor");
        StartRun(Vector2.left);Check(actor.motor.Facing==-1&&actor.motor.sprite.flipX,name+" left Dash and Run mirror/facing correctly");
        Hold(true,Vector2.right);Step(1);Check(actor.IsRunning&&actor.motor.Facing==1&&!actor.motor.sprite.flipX,name+" direction reversal changes facing without restarting Dash");
        int poses=actor.runData.poses.Sum(p=>p.frames);
        for(int f=0;f<poses*2;f++)
        {
            Step(1);if(!actor.IsRunning||actor.motor.sprite.sprite!=PlayerDefenseData.LoopPose(actor.runData.poses,actor.RunFrame-1))throw new Exception(name+" run pose/clock desync at "+f);
        }
        Check(true,name+" two complete run loops follow combat clock without pose resets or flicker");
        var position=actor.transform.position;int phase=actor.RunFrame;var sprite=actor.motor.sprite.sprite;
        actor.attackPlayer.Freeze(5);Step(5);
        Check(actor.transform.position==position&&actor.RunFrame==phase&&actor.motor.sprite.sprite==sprite,name+" hitstop freezes run movement and animation together");
        Step(1);Check(actor.RunFrame==phase+1,name+" run resumes exact phase after hitstop");
        position=actor.transform.position;phase=actor.RunFrame;CombatClock.SetPaused(root,true);Step(5);CombatClock.SetPaused(root,false);
        Check(actor.transform.position==position&&actor.RunFrame==phase,name+" combat pause freezes Run");
        Hold(true,Vector2.zero);Check(!actor.IsRunning&&actor.GuardActive,name+" releasing direction while holding Guard exits into guard");
        Hold(false,Vector2.zero);Check(actor.State==CombatState.Idle,name+" releasing both returns to Idle");
        StartRun(Vector2.right);actor.RequestAttack();Check(!actor.IsRunning&&actor.CurrentAttack==actor.groundCombo[0]&&actor.motor.GroundSpeedOverride==0,name+" attack immediately exits Run into existing Punch1 timeline");
        int cancel=actor.groundCombo[0].frames.FindIndex(f=>f.canCancelIntoAttack);
        Check(cancel>=0,name+" existing Punch1 has an authored combo cancel window");
        Step(Mathf.Max(0,cancel-2));actor.RequestAttack();for(int f=0;f<100&&actor.CurrentAttack!=actor.groundCombo[1];f++)Step(1);
        Check(actor.CurrentAttack==actor.groundCombo[1],name+" existing buffered Punch1-to-Punch2 combo still cancels after Run");
        actor.ResetRunInput();actor.health.Restore();actor.motor.ResetForStage(Vector2.zero);Step(5);
        StartRun(Vector2.right);actor.RequestJump();Check(!actor.IsRunning&&!actor.motor.IsGrounded&&actor.motor.GroundSpeedOverride==0,name+" jumping exits Run into existing jump movement");
        actor.motor.ResetForStage(Vector2.zero);actor.health.Restore();
        StartRun(Vector2.right);actor.ReceiveHit(new AttackHitboxData{damage=1,hitstunFrames=8},-1);
        Check(!actor.IsRunning&&actor.State==CombatState.Hitstun&&actor.motor.GroundSpeedOverride==0,name+" taking damage exits Run without stale speed");
        actor.health.Restore();StartRun(Vector2.right);actor.motor.ResetForStage(Vector2.zero);
        Check(!actor.IsRunning&&!actor.RunHeld&&actor.motor.GroundSpeedOverride==0,name+" stage reset clears run input and speed");
        Hold(true,Vector2.right);Step(3);Hold(false,Vector2.right);Step(actor.EffectiveDodgeFrames);
        Check(!actor.IsRunning&&actor.State==CombatState.Idle,name+" releasing during Dash prevents transition and preserves original dash recovery");
        StartRun(Vector2.right);
        var wall=new GameObject("Run collision wall");wall.transform.SetParent(root.transform);wall.transform.position=actor.transform.position+Vector3.right*.7f;
        wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,3);wall.AddComponent<CombatWall>().allowsBounce=false;Physics2D.SyncTransforms();
        float wallX=wall.transform.position.x;Step(30);Check(actor.transform.position.x<wallX-.2f,name+" Run respects existing swept wall collision");Object.DestroyImmediate(wall);
        actor.motor.arenaMax=new Vector2(actor.transform.position.x+.2f,5);Step(20);Check(actor.transform.position.x<=actor.motor.arenaMax.x+.0001f,name+" Run respects stage horizontal boundary");
        actor.motor.arenaMax=new Vector2(100,5);Hold(true,Vector2.up);Step(30);Check(actor.IsRunning&&actor.transform.position.y<=actor.motor.arenaMax.y,name+" Run supports lane movement with preserved facing and bounds");
        Hold(false,Vector2.zero);StartRun(Vector2.right);
        var cameraObject=new GameObject("Run follow camera");cameraObject.transform.SetParent(root.transform);cameraObject.AddComponent<Camera>();
        var framing=cameraObject.AddComponent<StageFraming>();framing.player=actor.motor;framing.followSmoothTime=0;framing.ApplyFraming(0,true);
        float initialCameraX=cameraObject.transform.position.x;
        for(int f=0;f<120;f++){Step(1);framing.ApplyFraming(1f/60);}
        var view=cameraObject.GetComponent<Camera>();var viewport=view.WorldToViewportPoint(actor.motor.sprite.bounds.center);
        Check(cameraObject.transform.position.x>initialCameraX&&viewport.x>=0&&viewport.x<=1&&Mathf.Abs(cameraObject.transform.position.y-framing.verticalCenter)<.01f,name+" existing camera follows Run while preserving vertical composition");
        Object.DestroyImmediate(cameraObject);
        var catalog=Resources.Load<MultiplayerCatalog>("MultiplayerCatalog");Check(actor.runData.poses.All(p=>catalog.SpriteId(p.sprite)>=0),name+" all run poses resolve in multiplayer sprite catalog");
        Hold(false,Vector2.zero);
    }
    static void InputCases(int index)
    {
        actor.health.Restore();actor.motor.ResetForStage(Vector2.zero);
        var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();var pad=InputSystem.AddDevice<Gamepad>();SessionInput source=null;
        var input=actor.GetComponent<PlayerInput>();var bridge=actor.GetComponent<PlayerCombatInput>();
        try
        {
            input.enabled=false;input.enabled=true;input.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse);input.SwitchCurrentActionMap("Player");bridge.enabled=true;bridge.SendMessage("Start");
            Action<Key[]> keys=held=>{InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));InputSystem.Update();bridge.SendMessage("Update");};
            keys(new[]{Key.L});Check(actor.GuardActive&&!actor.IsRunning,"Character "+(index+1)+" keyboard Guard alone never runs");
            keys(new[]{Key.L,Key.D});Check(actor.State==CombatState.Dodge,"Character "+(index+1)+" Guard-then-direction keyboard order starts Dash");Step(actor.EffectiveDashToRunFrame);
            Check(actor.IsRunning,"Character "+(index+1)+" actual held keyboard action transitions Dash into Run");
            keys(new[]{Key.D});Check(!actor.IsRunning&&!actor.GuardActive,"Character "+(index+1)+" actual keyboard Guard release exits Run");
            keys(new Key[0]);actor.health.Restore();actor.motor.ResetForStage(Vector2.zero);
            keys(new[]{Key.A});keys(new[]{Key.A,Key.L});Step(actor.EffectiveDashToRunFrame);
            Check(actor.IsRunning&&actor.motor.Facing==-1,"Character "+(index+1)+" direction-then-Guard keyboard order runs left");
            keys(new[]{Key.L});Check(!actor.IsRunning&&actor.GuardActive,"Character "+(index+1)+" actual direction release becomes stationary Guard");keys(new Key[0]);
            actor.health.Restore();actor.motor.ResetForStage(Vector2.zero);input.SwitchCurrentControlScheme("Gamepad",pad);
            var heldPad=new GamepadState{leftStick=Vector2.right}.WithButton(GamepadButton.East);
            InputSystem.QueueStateEvent(pad,heldPad);InputSystem.Update();bridge.SendMessage("Update");Step(actor.EffectiveDashToRunFrame);
            Check(actor.IsRunning,"Character "+(index+1)+" gamepad stick plus Guard transitions into Run");
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right});InputSystem.Update();bridge.SendMessage("Update");Check(!actor.IsRunning,"Character "+(index+1)+" gamepad Guard release exits Run");
            bridge.enabled=false;source=new SessionInput(input.actions,keyboard);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D,Key.L));InputSystem.Update();var command=source.Read();
            Check(command.run&&!command.guard&&command.move.x>0,"Character "+(index+1)+" multiplayer input transmits held directional Guard as Run");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D,Key.L));InputSystem.Update();Check(source.Read().run,"Multiplayer Run hold persists without a second pressed event");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));InputSystem.Update();Check(!source.Read().run,"Multiplayer Guard release clears Run command");
        }
        finally{source?.Dispose();bridge.enabled=false;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(pad);}
    }
    static void DefenseRegression()
    {
        var type=typeof(PlayerDefenseValidation);var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var checks=(List<string>)type.GetField("results",flags).GetValue(null);checks.Clear();
        foreach(var name in new[]{"Dodge","GuardParry","ParryBoundariesAndRearm","ParryCompatibilityAndCounter","Knockdown","Death","RealEnemyAttacks"})
        {type.GetMethod(name,flags).Invoke(null,null);Check(true,"Existing defense regression "+name+" passes unchanged");}
        Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/RunDefenseRegressionResults.txt",checks);
    }
}
