using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class GrapplerValidation
{
    const string Pending="BeatEmUp.GrapplerValidation";
    static readonly List<Object> temporary=new List<Object>();
    static readonly List<string> results=new List<string>();
    static ComboController player; static EnemyCombat enemy; static HitCountArmor armor; static CombatGrabController grab; static CombatClock clock;
    static GrapplerValidation(){EditorApplication.update+=Poll;}
    [MenuItem("Beat Em Up/Enemies/Validate Grappler (Play Mode)")]
    public static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Poll()
    {
        if(!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/GrapplerValidation.request")){File.Delete("Temp/GrapplerValidation.request");Run();return;}
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        SessionState.SetBool(Pending,false);results.Clear();
        try{
            foreach(var f in Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None))f.enabled=false;
            foreach(var f in Object.FindObjectsByType<StageFraming>(FindObjectsSortMode.None))f.enabled=false;
            foreach(var m in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None))m.gameObject.SetActive(false);
            foreach(var w in Object.FindObjectsByType<CombatWall>(FindObjectsSortMode.None))w.gameObject.SetActive(false);
            clock=Object.FindFirstObjectByType<CombatClock>();clock.enabled=false;clock.combatFPS=60;
            Armor();Geometry();Captured();InvalidAndCleanup();Interaction();Lunge();
            results.Add("NOTE: Independent online peers were not launched; state/body/armor-color snapshots and catalog membership checked through the existing host architecture.");
            Debug.Log("GRAPPLER VALIDATION PASSED: "+results.Count+" results");
        }catch(Exception error){results.Add("FAIL: "+error);Debug.LogException(error);}
        finally{File.WriteAllLines("Documentation/GrapplerValidationResults.txt",results);Clear();EditorApplication.ExitPlaymode();}
    }
    static void Check(bool pass,string label){if(!pass)throw new Exception(label);results.Add("PASS: "+label);}
    static T Keep<T>(T obj) where T:Object{temporary.Add(obj);return obj;}
    static void Clear(){foreach(var obj in temporary.ToArray())if(obj)Object.DestroyImmediate(obj);temporary.Clear();}
    static void Step(int count=1){for(int i=0;i<count;i++){Physics2D.SyncTransforms();clock.StepFrame();}}
    static void Until(Func<bool> ready,string label,int max=500){for(int i=0;i<max && !ready();i++)Step();Check(ready(),label);}
    static void Fixture(Vector2 point,int facing=1,bool ai=false)
    {
        Clear();player=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath))).GetComponent<ComboController>();
        player.GetComponent<PlayerCombatInput>().enabled=false;player.health.SafeStageProtection=false;player.health.Restore();player.motor.ResetForStage(point);
        enemy=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GrapplerSetup.PrefabPath))).GetComponent<EnemyCombat>();
        enemy.motor.ResetForStage(Vector2.zero);enemy.motor.Face(facing);enemy.reaction.health.SafeStageProtection=false;enemy.reaction.health.Restore();enemy.target=player.transform;enemy.passiveTrainingDummy=false;
        if(!ai)enemy.enabled=false;else enemy.RefreshAI();
        armor=enemy.GetComponent<HitCountArmor>();grab=enemy.GetComponent<CombatGrabController>();
        enemy.motor.arenaMin=player.motor.arenaMin=Vector2.one*-20;enemy.motor.arenaMax=player.motor.arenaMax=Vector2.one*20;
    }
    static void Start(){Check(enemy.attackPlayer.Play(grab.grabAttack),"Frame-data grab begins with committed facing");}
    static void Hit(int count=1){for(int i=0;i<count;i++)enemy.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{damage=1,hitstunFrames=18,hitstopFrames=0,knockback=4,canHitAirborne=true},-1,player.motor);}
    static void Armor()
    {
        Fixture(new Vector2(3,0));Check(armor.ArmorRemaining==6 && armor.maxArmorHits==6,"Grappler begins with six configurable armor points");Start();
        for(int i=1;i<=5;i++){Hit();Check(armor.ArmorRemaining==6-i && enemy.reaction.State==EnemyReaction.Normal && enemy.attackPlayer.CurrentAttack==grab.grabAttack,"Accepted hit "+i+" damages without hitstun or action interruption");}
        var position=enemy.transform.position;Step(3);Check(enemy.transform.position==position && enemy.reaction.health.Current==195,"Armored hits do not knock the Grappler backward and retain all damage");
        Check(armor.FeedbackCount==5 && armor.armorHitFeedback.feedback.impactSound && enemy.motor.sprite.color==armor.armorColor,"Armored contacts have separate flash, SFX and accepted-hit feedback");
        Hit();Check(armor.ArmorBroken && !enemy.attackPlayer.CurrentAttack && enemy.reaction.State==EnemyReaction.GroundHit && enemy.reaction.RecoveryFrames==60,"Sixth hit breaks armor and interrupts telegraph with 60f stagger");
        Check(armor.LastOutcome==CombatHitOutcome.ArmorBreak && armor.FeedbackCount==6 && armor.armorBreakFeedback.feedback.impactSound!=armor.armorHitFeedback.feedback.impactSound,"Armor break has a distinct accepted outcome, color and heavy sound");
        Step(60);Check(armor.ArmorBroken,"Ending break stun does not immediately restore armor");Hit();
        Check(enemy.reaction.State==EnemyReaction.GroundHit && armor.RecoveryRemaining==300,"Unarmored hits use normal reactions and restart the 300f recovery delay");
        Step(299);Check(armor.ArmorBroken,"Armor remains broken for full delay after last accepted damage");Step();Check(armor.ArmorRemaining==6,"Idle healthy Grappler restores full armor after delay");
        armor.restoreFullArmor=false;Hit(6);Step(300);Check(armor.ArmorRemaining==1,"Optional partial recovery restores one point per delay");
        enemy.reaction.health.Restore();Check(armor.ArmorRemaining==6,"Existing restore/reset replenishes armor");
        CombatClock.SetPaused(enemy.gameObject,true);Hit();Check(armor.ArmorRemaining==6,"Global pause rejects incoming hits before consuming armor");CombatClock.SetPaused(enemy.gameObject,false);
        var hurtbox=enemy.GetComponentInChildren<CombatHurtbox>();hurtbox.externalInvulnerable=true;Hit();Check(armor.ArmorRemaining==6,"Rejected invulnerable hits do not consume armor");
    }
    static void Geometry()
    {
        foreach(int facing in new[]{1,-1})foreach(var point in new[]{new Vector2(2.5f,0),new Vector2(-.8f,0),new Vector2(2.5f,.39f),new Vector2(2.5f,.41f),new Vector2(3.8f,0)}){
            Fixture(new Vector2(2.4f*facing,0),facing);Start();Step(26);player.motor.ResetForStage(new Vector2(point.x*facing,point.y));Step(3);Check(!grab.GrabActive && !player.IsGrabbed && enemy.transform.position==Vector3.zero,"Telegraph contains no active grab or movement for 30 frames");
            Step(19);bool expected=point.x>0 && point.x<=3.5f && Mathf.Abs(point.y)<=.4f;
            Check(player.IsGrabbed==expected,"Traveling committed grab rectangle / lane geometry at "+point+", facing "+facing);
            if(!expected){Check(!grab.GrabActive && !grab.LungeActive && enemy.attackPlayer.CurrentFrame==48,"Miss stops lunge and disables grab at frame 48");Step(41);Check(enemy.attackPlayer.CurrentAttack && enemy.attackPlayer.CurrentFrame==89,"Miss retains all 42 vulnerable recovery frames");Step();Check(!enemy.attackPlayer.CurrentAttack,"Miss recovery ends at frame 90");}
        }
        Fixture(new Vector2(.8f,0));Start();Step(26);player.motor.ResetForStage(new Vector2(-.8f,0));Step(22);Check(!player.IsGrabbed && enemy.attackPlayer.Facing==1,"Crossing behind after commit avoids the locked direction");
        Fixture(new Vector2(.8f,0));Start();Step(24);player.RequestDodge();Step(7);Check(player.DodgeInvulnerable && !player.IsGrabbed,"Existing dodge immunity rejects the first active grab frame");
        player.motor.ResetForStage(new Vector2(3,2));Step(16);Check(grab.Captures==0,"Dodging out of the path avoids remaining active frames");
    }
    static void Captured()
    {
        foreach(int facing in new[]{1,-1}){
            Fixture(new Vector2(.8f*facing,0),facing);player.motor.Face(facing);player.RequestAttack();Start();Step(31);
            Check(player.IsGrabbed && player.GrabOwner==grab && grab.CurrentGrabbedTarget==player && !player.CurrentAttack,"Capture cancels the action and records both ownership links");
            Check((Vector2)player.transform.position==grab.AnchorPosition && player.motor.MovementLocked && player.health.Current==200,"Zero-damage connection attaches to mirrored GrabAnchor");
            Check(player.motor.sprite.sprite==player.defenseData.grabbed[0].sprite && player.motor.sprite.sprite.name.Contains("PLACEHOLDER"),"Held player uses explicitly marked non-idle grabbed reference pose");
            player.motor.MoveInput=Vector2.one;player.RequestAttack();player.RequestLauncher();player.RequestJump();player.RequestGuard(true);player.RequestDodge();
            Check(!player.CurrentAttack && !player.JumpBuffered && !player.GuardActive && !player.ParryActive && !player.CanStartSkill && player.IsGrabbed,"Grabbed rejects movement/combo/jump/guard/parry/dodge/skill without disabling global input");
            var second=Keep(Object.Instantiate(player.gameObject)).GetComponent<ComboController>();second.health.Restore();second.motor.ResetForStage(new Vector2(3,0));second.RequestAttack();
            Check(second.CurrentAttack && !second.IsGrabbed,"Independent player keeps normal combat control");
            enemy.motor.transform.position=new Vector2(.2f,.2f);Step();Check((Vector2)player.transform.position==grab.AnchorPosition,"Held target follows caster position without drift");
            Step(28);Check(player.IsGrabbed && player.health.Current==200,"Hold lasts 30 frames without applying slam damage early");
            Step();Check(!player.IsGrabbed && !player.GrabOwner && !grab.CurrentGrabbedTarget && player.State==CombatState.KnockDown && player.health.Current==176,"Throw frame releases ownership and applies one separate 24-damage knockdown");
            Until(()=>player.State==CombatState.Idle,"KnockDown / Downed / GetUp restores normal player control");
            Check(!player.motor.MovementLocked,"Release leaves no movement lock");
        }
    }
    static void InvalidAndCleanup()
    {
        for(int mode=0;mode<6;mode++){
            Fixture(new Vector2(.8f,0));
            if(mode==0)player.ungrabbable=true;
            if(mode==1)player.GetComponentInChildren<CombatHurtbox>().externalInvulnerable=true;
            if(mode==2)player.ReceiveHit(new AttackHitboxData{hitType=HitType.KnockDown},1);
            if(mode==3)player.health.Damage(500);
            if(mode==4)player.health.SafeStageProtection=true;
            if(mode==5)player.motor.Launch(8,0);
            Start();Step(31);Check(!player.IsGrabbed,"Invalid / airborne target rejected, case "+mode);
        }
        Fixture(new Vector2(.8f,0));Start();Step(31);
        var other=Keep(Object.Instantiate(enemy.gameObject)).GetComponent<CombatGrabController>();
        Check(!player.TryEnterGrab(other),"A second owner cannot acquire an already grabbed player");
        var another=Keep(Object.Instantiate(player.gameObject)).GetComponent<ComboController>();another.health.Restore();
        Check(!another.TryEnterGrab(grab),"One owner cannot acquire a second player while holding its target");
        enemy.attackPlayer.Stop();Check(!player.IsGrabbed && !player.GrabOwner,"Interrupted hold releases its player immediately");
        foreach(int mode in new[]{0,1,2,3}){
            Fixture(new Vector2(.8f,0));Start();Step(31);
            if(mode==0)enemy.reaction.health.Damage(500);
            if(mode==1)grab.enabled=false;
            if(mode==2)player.health.Restore();
            if(mode==3){grab.maximumGrabFrames=2;Step(2);}
            Check(!player.IsGrabbed && !player.GrabOwner,"Death / disable / restore / watchdog releases ownership, case "+mode);
        }
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        Check(catalog.AttackId(grab.grabAttack)>=0 && catalog.AttackId(grab.successfulGrab)>=0 && catalog.SpriteId(player.defenseData.grabbed[0].sprite)>=0,"Grab branch attacks and held-player pose registered in multiplayer catalog");
    }
    static void Interaction()
    {
        Fixture(new Vector2(.8f,0));Start();Hit(5);Step(31);Check(player.IsGrabbed && armor.ArmorRemaining==1,"Five hits during telegraph consume armor but allow committed grab to connect");
        Hit();Check(armor.ArmorBroken && !player.IsGrabbed,"Breaking final armor during hold releases player for future rescue support");
        Fixture(new Vector2(.8f,0));Start();Hit(6);Step(40);Check(!player.IsGrabbed && grab.Captures==0,"Breaking six armor points during telegraph cancels pending capture");
        Fixture(new Vector2(.8f,0),1,true);Until(()=>enemy.attackPlayer.CurrentAttack,"Existing AI approaches / chooses the configured grab");Hit(6);
        Check(!enemy.attackPlayer.CurrentAttack && enemy.AI.Cooldown("Primary")>=2.49f,"AI armor-break interruption consumes normal 2.5-second grab cooldown");
    }
    static void Lunge()
    {
        foreach(int facing in new[]{1,-1}){
            Fixture(new Vector2(2.4f*facing,1.2f),facing);Start();Step(25);
            Check(!grab.DirectionLocked && enemy.transform.position==Vector3.zero,"Early telegraph faces target without launching");
            Step();var expected=new Vector2(2.4f*facing,1.2f).normalized;
            Check(grab.DirectionLocked && Vector2.Distance(grab.LockedGrabDirection,expected)<.0001f && enemy.attackPlayer.Facing==facing,"Frame 26 locks target XY direction and facing");
            player.motor.ResetForStage(new Vector2(-5*facing,-2));Step(4);
            Check(grab.LungeActive && !grab.GrabActive && Vector2.Distance(enemy.transform.position,expected*(8f/60))<.0001f,"Frame 30 begins 8u/s committed launch before grab activates");
            Check(grab.grabLungeSpeed>enemy.motor.moveSpeed*5,"Lunge is substantially faster than normal approach");
            Step(18);Check(Vector2.Distance(enemy.transform.position,expected*2.4f)<.001f && grab.LockedGrabDirection==expected && enemy.motor.Height==0,"18 authored frames travel 2.4 units on ground XY without homing or jump height");
            Check(!grab.LungeActive && !grab.GrabActive && enemy.attackPlayer.CurrentFrame==48,"Authored lunge end enters 42-frame miss recovery");
        }
        Fixture(new Vector2(0,1.8f));Start();Step(26);Check(grab.LockedGrabDirection==Vector2.up,"Target directly above commits along walking depth, preserving stable sprite facing");
        Until(()=>player.IsGrabbed,"Traveling grab volume catches a player on a pure depth lunge");
        Check(!grab.LungeActive && (Vector2)player.transform.position==grab.AnchorPosition,"Depth capture stops movement and immediately attaches target");
        var heldPosition=enemy.transform.position;Step(10);Check(enemy.transform.position==heldPosition,"Successful hold does not keep lunging / dragging the target");
        Fixture(new Vector2(2,0));Start();Step(10);player.motor.ResetForStage(new Vector2(-2,1));Step(16);
        Check(enemy.attackPlayer.Facing==-1 && grab.LockedGrabDirection.x<0 && grab.LockedGrabDirection.y>0,"Early telegraph may update facing before the late commitment event");
        Fixture(new Vector2(5,0));Start();Step(30);var position=enemy.transform.position;int frame=enemy.attackPlayer.CurrentFrame;enemy.attackPlayer.Freeze(4);Step(4);
        Check(enemy.transform.position==position && enemy.attackPlayer.CurrentFrame==frame,"Hitstop cannot repeat a frame's committed movement");Step();Check(enemy.transform.position.x>position.x,"Lunge resumes once hitstop ends");
        Fixture(new Vector2(5,0));grab.grabLungeDistance=.4f;Start();Step(34);
        Check(Mathf.Abs(grab.LungeDistanceTraveled-.4f)<.0001f && !grab.LungeActive && enemy.attackPlayer.CurrentFrame>=48,"Distance cap stops committed travel and jumps to miss recovery");
        Fixture(new Vector2(5,0));grab.grabAcceleration=24;Start();Step(30);
        Check(enemy.transform.position.x>0 && enemy.transform.position.x<8f/60,"Optional acceleration ramps launch speed through combat frames");
        Fixture(new Vector2(5,0));Start();Step(26);player.motor.ResetForStage(new Vector2(1,2));Step(22);
        Check(enemy.transform.position.x>1 && !player.IsGrabbed && Mathf.Abs(enemy.transform.position.y)<.001f,"Sidestep permits a committed overshoot without steering to the new lane");
        Fixture(new Vector2(5,0));Start();Step(32);Hit(5);
        Check(armor.ArmorRemaining==1 && grab.LungeActive && enemy.reaction.State==EnemyReaction.Normal,"Armor absorbs five hits while the lunge continues");
        Hit();position=enemy.transform.position;Step(10);Check(armor.ArmorBroken && !grab.LungeActive && !grab.GrabActive && enemy.transform.position==position,"Armor break cancels lunge movement / grab and enters stagger");
        Fixture(new Vector2(4,0));var wall=Keep(new GameObject("Grappler lunge test wall"));wall.transform.position=new Vector2(.8f,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,4);wall.AddComponent<CombatWall>().allowsBounce=false;
        Start();Step(35);Check(grab.StoppedByWall && !grab.LungeActive && !grab.GrabActive && enemy.transform.position.x<.46f && enemy.attackPlayer.CurrentFrame>=48,"Solid CombatWall blocks the body, disables grab and starts miss/crash recovery");
        Fixture(new Vector2(2.5f,0));Start();Step(20);Capture("Telegraph");Step(11);Capture("Lunge");Until(()=>player.IsGrabbed,"Preview capture reaches held sequence");Capture("Grabbed");
    }
    static void Capture(string phase)
    {
        var go=new GameObject("Grappler validation camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=2.3f;
        camera.transform.position=new Vector3(1,1,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.08f,.12f);
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;var old=RenderTexture.active;
        try{camera.Render();RenderTexture.active=target;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();Directory.CreateDirectory("Documentation/GrapplerPreview");File.WriteAllBytes("Documentation/GrapplerPreview/"+phase+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);}
        finally{RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(go);}
    }
}
