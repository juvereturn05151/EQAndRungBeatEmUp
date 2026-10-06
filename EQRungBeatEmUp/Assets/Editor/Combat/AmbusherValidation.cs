using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class AmbusherValidation
{
    const string Pending="BeatEmUp.AmbusherValidation";
    static readonly List<Object> temporary=new List<Object>();static readonly List<string> results=new List<string>();
    static ComboController player;static EnemyCombat enemy;static CombatGrabController grab;static CombatClock clock;
    static AmbusherValidation(){EditorApplication.update+=Poll;}
    [MenuItem("Beat Em Up/Enemies/Validate Ambusher (Play Mode)")]
    public static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Poll(){
        if(!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/AmbusherValidation.request")){try{File.Delete("Temp/AmbusherValidation.request");}catch(IOException){return;}Run();return;}
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;SessionState.SetBool(Pending,false);results.Clear();
        try{
            foreach(var f in Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None))f.enabled=false;
            foreach(var f in Object.FindObjectsByType<StageFraming>(FindObjectsSortMode.None))f.enabled=false;
            foreach(var m in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None))m.gameObject.SetActive(false);
            foreach(var w in Object.FindObjectsByType<CombatWall>(FindObjectsSortMode.None))w.gameObject.SetActive(false);
            clock=Object.FindFirstObjectByType<CombatClock>();clock.enabled=false;clock.combatFPS=60;
            Leap();Capture();Cleanup();AI();
            results.Add("NOTE: Separate online peers were not launched. Existing snapshot/catalog and per-strike feedback cue integration inspected.");Debug.Log("AMBUSHER VALIDATION PASSED: "+results.Count+" results");
        }catch(Exception e){results.Add("FAIL: "+e);Debug.LogException(e);}finally{Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/AmbusherValidationResults.txt",results);Clear();EditorApplication.ExitPlaymode();}
    }
    static T Keep<T>(T o) where T:Object{temporary.Add(o);return o;}
    static void Clear(){foreach(var o in temporary.ToArray())if(o)Object.DestroyImmediate(o);temporary.Clear();}
    static void Check(bool p,string label){if(!p)throw new Exception(label);results.Add("PASS: "+label);}
    static void Step(int n=1){for(int i=0;i<n;i++){Physics2D.SyncTransforms();clock.StepFrame();}}
    static void Until(Func<bool> p,string label,int max=400){for(int i=0;i<max&&!p();i++)Step();Check(p(),label);}
    static void Fixture(Vector2 point,int facing=1,bool ai=false){
        Clear();player=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath))).GetComponent<ComboController>();player.GetComponent<PlayerCombatInput>().enabled=false;player.health.SafeStageProtection=false;player.health.Restore();player.motor.ResetForStage(point);
        enemy=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AmbusherSetup.PrefabPath))).GetComponent<EnemyCombat>();enemy.motor.ResetForStage(Vector2.zero);enemy.motor.Face(facing);enemy.reaction.health.SafeStageProtection=false;enemy.reaction.health.Restore();enemy.target=player.transform;enemy.passiveTrainingDummy=false;if(!ai)enemy.enabled=false;else enemy.RefreshAI();grab=enemy.GetComponent<CombatGrabController>();enemy.motor.arenaMin=player.motor.arenaMin=Vector2.one*-20;enemy.motor.arenaMax=player.motor.arenaMax=Vector2.one*20;
    }
    static void Start(){Check(enemy.attackPlayer.Play(grab.grabAttack),"Frame-data leap begins");}
    static void HitEnemy(){enemy.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{damage=1,hitstunFrames=18,knockback=0},1,player.motor);}
    static void Leap(){
        foreach(int facing in new[]{1,-1}){
            var target=new Vector2(2.4f*facing,1.2f);Fixture(target,facing);Start();Step(16);Check(!grab.DirectionLocked&&!grab.LeapActive&&enemy.motor.IsGrounded&&enemy.transform.position==Vector3.zero,"Crouch telegraph stays stationary / grounded without early capture");Step();Check(grab.DirectionLocked&&grab.LockedTarget==player.transform&&grab.TargetPositionAtCommit==target,"Target player / XY destination locks near end at17");
            player.motor.ResetForStage(new Vector2(-5*facing,-2));Step(3);Check(grab.LeapActive&&enemy.motor.Height>0&&!grab.GrabActive,"Takeoff20 enters separate airborne height before opening grab");
            Step(14);Check(Mathf.Abs(enemy.motor.Height-1.6f)<.001f&&Vector2.Distance(enemy.transform.position,target*.5f)<.001f&&enemy.transform.position.z==0,"Authored midpoint reaches1.6 height and half locked XY travel");
            Step(10);Check(!grab.GrabActive&&!player.IsGrabbed,"No capture before descending frame45");Step();Check(grab.GrabActive&&enemy.motor.Height<=.75f,"Final leap grab opens45 with height/lane limit");Step(4);Check(enemy.motor.IsGrounded&&Vector2.Distance(enemy.transform.position,target)<.001f&&!player.IsGrabbed,"Miss reaches original target without homing and returns to floor49");Step();Check(!grab.GrabActive&&!grab.LeapActive&&enemy.attackPlayer.CurrentFrame==50,"Landing50 disables grab and starts miss recovery");Step(29);Check(enemy.attackPlayer.CurrentFrame==79&&enemy.attackPlayer.CurrentAttack,"All30 miss recovery frames retained");Step();Check(!enemy.attackPlayer.CurrentAttack,"Miss timeline completes80");
        }
        Fixture(new Vector2(0,3));Start();Step(35);Check(enemy.motor.Height>1&&enemy.transform.position.x==0&&enemy.transform.position.y>0,"Pure depth leap does not confuse depth with air height");
        Fixture(new Vector2(2,0));Start();Step(10);player.motor.ResetForStage(new Vector2(-2,1));Step(7);Check(grab.LockedGrabDirection.x<0&&enemy.attackPlayer.Facing==-1,"Early telegraph tracks changed target before commit");
        Fixture(new Vector2(3,0));Start();Step(17);player.motor.ResetForStage(new Vector2(3,1));Step(33);Check(!player.IsGrabbed,"Sidestep after commit escapes traveling grab lane");
        Fixture(new Vector2(3,0));Start();Step(40);Check(player.RequestDodge(),"Dodge starts before existing4-frame invulnerability startup");Step(10);Check(grab.Captures==0,"Existing dodge immunity avoids descending grab");
        Fixture(new Vector2(3,0));Start();Step(10);HitEnemy();Step(50);Check(!enemy.attackPlayer.CurrentAttack&&!grab.LeapActive&&!player.IsGrabbed&&enemy.transform.position==Vector3.zero,"Ordinary hit interrupts telegraph without Grappler armor");
        Fixture(new Vector2(3,0));Start();Step(30);HitEnemy();Step(80);Check(!grab.LeapActive&&enemy.motor.IsGrounded&&!player.IsGrabbed,"Air interruption restores normal falling / recovery safely");
        Fixture(new Vector2(3,0));Start();Step(30);var position=enemy.transform.position;float height=enemy.motor.Height;enemy.attackPlayer.Freeze(4);Step(4);Check(enemy.transform.position==position&&enemy.motor.Height==height,"Hitstop freezes both ground travel and arc height");
        Fixture(new Vector2(3,0));var wall=Keep(new GameObject("Ambusher validation wall"));wall.transform.position=new Vector2(.8f,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,4);wall.AddComponent<CombatWall>().allowsBounce=false;Start();Step(35);Check(grab.StoppedByWall&&!grab.LeapActive&&!grab.GrabActive&&enemy.motor.IsGrounded&&enemy.transform.position.x<.46f&&enemy.attackPlayer.CurrentFrame>=50,"Solid CombatWall stops leap, lands actor and starts miss recovery");
        Fixture(new Vector2(5,0));grab.leapDistance=1;Start();Step(50);Check(Mathf.Abs(enemy.transform.position.x-1)<.001f&&!player.IsGrabbed,"Distance cap prevents unbounded leap travel");
        Fixture(new Vector2(3,0));grab.leapHorizontalSpeed=2;Start();Step(50);Check(Mathf.Abs(enemy.transform.position.x-1)<.001f,"Speed limits travel distance over authored duration");
    }
    static void Capture(){
        foreach(int facing in new[]{1,-1}){
            Fixture(new Vector2(2.5f*facing,0),facing);Start();Until(()=>player.IsGrabbed,"Final airborne approach captures player");Check(!grab.LeapActive&&!grab.GrabActive&&enemy.motor.IsGrounded&&player.GrabOwner==grab&&grab.CurrentGrabbedTarget==player,"Capture stops leap, lands owner and reuses bilateral Grabbed ownership");
            Check(Vector2.Distance(player.transform.position,grab.AnchorPosition)<.001f,"GrabAnchor mirrors correctly facing "+facing);
            player.motor.MoveInput=Vector2.one;player.RequestAttack();player.RequestLauncher();player.RequestJump();player.RequestGuard(true);player.RequestDodge();Check(!player.CurrentAttack&&!player.JumpBuffered&&!player.GuardActive&&!player.ParryActive&&!player.CanStartSkill&&player.IsGrabbed,"Existing Grabbed state blocks all gameplay actions");
            var partner=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath))).GetComponent<ComboController>();partner.GetComponent<PlayerCombatInput>().enabled=false;partner.health.Restore();partner.motor.ResetForStage(new Vector2(-4,0));partner.RequestAttack();Check(partner.CurrentAttack&&!partner.IsGrabbed,"Other player continues combat independently");
            Step(5);Check(player.health.Current==200&&grab.FaceStrikesApplied==0,"Hold and windup do not apply early damage");Step();Check(player.health.Current==192&&grab.FaceStrikesApplied==1&&player.IsGrabbed,"Impact6 applies first separate8 damage and preserves attachment");Check(enemy.attackPlayer.HitstopRemaining==2&&grab.StrikeFeedbackCount==1&&player.motor.sprite.sprite==grab.heldImpactPose,"Impact VFX/SFX cue /2-frame hitstop /held hurt pose occur together");
            Until(()=>grab.FaceStrikesApplied==2,"Second authored impact loop executes");Check(player.health.Current==184&&player.IsGrabbed,"Second hit does not release held player");Until(()=>grab.FaceStrikesApplied==3,"Third authored impact loop executes");Check(player.health.Current==176&&grab.StrikeFeedbackCount==3&&player.IsGrabbed,"Default3 face strikes total24 damage with3 feedback cues");Until(()=>!player.IsGrabbed,"Final sequence releases player");Check(!grab.CurrentGrabbedTarget&&!player.GrabOwner&&player.State==CombatState.KnockDown,"Release18 transitions into existing KnockDown");Until(()=>player.State==CombatState.Idle,"Knockdown / get-up returns normal controls");
        }
        foreach(int count in new[]{1,4}){Fixture(new Vector2(2.5f,0));grab.faceAttackCount=count;Start();Until(()=>player.IsGrabbed,"Configurable count fixture captures");Until(()=>!player.IsGrabbed,"Configured strike count completes");Check(grab.FaceStrikesApplied==count&&player.health.Current==200-8*count,"Configurable face attack count "+count+" produces exact damage");}
    }
    static void Cleanup(){
        foreach(int mode in new[]{0,1,2,3,4,5}){Fixture(new Vector2(2.5f,0));Start();Until(()=>player.IsGrabbed,"Cleanup fixture captures");if(mode==0)HitEnemy();if(mode==1)enemy.reaction.health.Damage(999);if(mode==2)grab.enabled=false;if(mode==3)player.health.Restore();if(mode==4){grab.faceHit.damage=999;Step(6);}if(mode==5)player.health.Damage(999);Step();Check(!player.IsGrabbed&&!player.GrabOwner&&!grab.CurrentGrabbedTarget,"Hit interrupt / enemy death / disable / restore / lethal strike releases links, case "+mode);if(mode==4 || mode==5)Check(player.health.IsDead&&player.State==CombatState.Die&&!enemy.attackPlayer.CurrentAttack,"Lethal face strike stops sequence and uses normal Die state");}
        foreach(int invalid in new[]{0,1,2,3,4}){Fixture(new Vector2(2.5f,0));if(invalid==0)player.ungrabbable=true;if(invalid==1)player.GetComponentInChildren<CombatHurtbox>().externalInvulnerable=true;if(invalid==2)player.health.SafeStageProtection=true;if(invalid==3)player.health.Damage(999);if(invalid==4)player.motor.Launch(20,0);Start();Step(50);Check(grab.Captures==0,"Invalid target rejected, case "+invalid);}
        Fixture(new Vector2(2.5f,0));Start();Until(()=>player.IsGrabbed,"Exclusive owner fixture captures");var other=Keep(Object.Instantiate(enemy.gameObject)).GetComponent<CombatGrabController>();Check(!player.TryEnterGrab(other),"One player cannot be captured by two owners");
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);Check(catalog.AttackId(grab.grabAttack)>=0&&catalog.AttackId(grab.successfulGrab)>=0,"Leap / held-strike assets registered in existing multiplayer catalog");
    }
    static void AI(){
        Fixture(new Vector2(.5f,0),1,true);Check(!enemy.AI.AvailableAttacks().Any(),"AI minimum range excludes point-blank leaps");Step(10);Check(enemy.transform.position.x<0&&!enemy.attackPlayer.CurrentAttack,"Point-blank Ambusher retreats to preferred leap distance");
        Fixture(new Vector2(5.5f,0),1,true);Step(10);Check(enemy.transform.position.x>0&&!enemy.attackPlayer.CurrentAttack,"Out-of-range Ambusher approaches normally");
        Fixture(new Vector2(0,2.5f),1,true);Until(()=>enemy.attackPlayer.CurrentAttack,"AI selects medium-distance depth leap");Until(()=>grab.LeapActive,"AI maintains committed airborne timeline");Step(10);Check(grab.LeapActive&&enemy.attackPlayer.CurrentAttack==grab.grabAttack,"Existing AI does not cancel its authored jump as generic airborne hitstun");
        Until(()=>player.IsGrabbed,"AI-controlled leap reaches and grabs target");Until(()=>!player.IsGrabbed,"AI-controlled face sequence releases player");Until(()=>!enemy.attackPlayer.CurrentAttack,"AI success branch completes");Check(enemy.AI.Cooldown("Primary")>2.9f,"Success cooldown maps back to original3-second leap choice");
        Fixture(new Vector2(2.5f,0),1,true);Until(()=>enemy.attackPlayer.CurrentAttack,"AI interrupt fixture enters telegraph");HitEnemy();Check(enemy.AI.Cooldown("Primary")>2.9f,"Interrupted telegraph consumes meaningful leap cooldown");
        Fixture(new Vector2(2.5f,0));Start();Step(10);CaptureImage("Telegraph");Step(25);CaptureImage("Leap");Until(()=>player.IsGrabbed,"Preview capture succeeds");Step(6);CaptureImage("FaceStrike");
    }
    static void CaptureImage(string name){var go=new GameObject("Ambusher preview camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2.4f;camera.transform.position=new Vector3(1,1.5f,-10);var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;var previous=RenderTexture.active;try{camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();Directory.CreateDirectory("Documentation/AmbusherPreview");File.WriteAllBytes("Documentation/AmbusherPreview/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}finally{RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}}
}
