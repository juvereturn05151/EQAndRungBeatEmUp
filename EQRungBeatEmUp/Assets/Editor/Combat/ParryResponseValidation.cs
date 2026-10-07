using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ParryResponseValidation
{
    const string Pending="BeatEmUp.ParryResponseValidation", Request="Temp/ParryResponseValidation.request";
    static readonly List<string> results=new List<string>();
    static readonly List<Object> temporary=new List<Object>();
    static ComboController player; static EnemyCombat enemy; static CombatClock clock;
    static ParryResponseValidation(){EditorApplication.update+=Poll;}
    [MenuItem("Beat Em Up/Validate parry stun and projectile deflection (Play Mode)")]
    public static void Run(){if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Poll()
    {
        if(!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/RusherSlash.open-request"))
        { File.Delete("Temp/RusherSlash.open-request"); AttackDataEditorWindow.OpenRusherSlash(); }
        if(!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Request)){File.Delete(Request);Run();return;}
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        SessionState.SetBool(Pending,false);results.Clear();
        try
        {
            foreach(var flow in Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None))flow.enabled=false;
            foreach(var actor in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None))actor.gameObject.SetActive(false);
            clock=Object.FindFirstObjectByType<CombatClock>();
            if(!clock) clock=new GameObject("Parry validation clock").AddComponent<CombatClock>();
            clock.enabled=false;
            foreach(var name in new[]{"Rusher","Thrower","Screamer","GrapplerBruiser","Ambusher","Prefect"})Melee(name);
            RusherSlashTiming();
            Boss();Grab("GrapplerBruiser");Grab("Ambusher");Push();Projectiles();ActualThrower();Status();
            results.Add("PASS: All parry response checks completed");
        }
        catch(Exception error){results.Add("FAIL: "+error);Debug.LogException(error);}
        finally{Clear();Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/ParryResponseValidationResults.txt",results);Debug.Log("PARRY RESPONSE VALIDATION: "+results.Last()+" ("+results.Count+")");EditorApplication.ExitPlaymode();}
    }
    static T Keep<T>(T value)where T:Object{temporary.Add(value);return value;}
    static void Clear(){foreach(var value in temporary.ToArray())if(value)Object.DestroyImmediate(value);temporary.Clear();}
    static void Check(bool pass,string label){if(!pass)throw new Exception(label);results.Add("PASS: "+label);}
    static void Step(int frames=1){for(int i=0;i<frames;i++){Physics2D.SyncTransforms();clock.StepFrame();}}
    static void Fixture(string name="Rusher",int facing=1)
    {
        Clear();
        player=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath))).GetComponent<ComboController>();
        player.GetComponent<PlayerCombatInput>().enabled=false; player.health.SafeStageProtection=false;player.health.maximumHealth=200;player.health.Restore();
        player.motor.ResetForStage(Vector2.zero);player.motor.Face(facing);
        var meter=player.GetComponent<PlayerMeter>();meter.startingMeter=0;meter.ResetMeter();
        enemy=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ParryResponseSetup.Root+"/Prefabs/"+name+".prefab"))).GetComponent<EnemyCombat>();
        enemy.enabled=false;enemy.reaction.health.SafeStageProtection=false;enemy.reaction.health.maximumHealth=200;enemy.reaction.health.Restore();
        enemy.motor.ResetForStage(new Vector2(.8f*facing,0));enemy.motor.Face(-facing);enemy.target=player.transform;
        enemy.motor.arenaMin=player.motor.arenaMin=Vector2.one*-20;enemy.motor.arenaMax=player.motor.arenaMax=Vector2.one*20;
    }
    static AttackHitboxData Hit()=>new AttackHitboxData{damage=5,hitstopFrames=0,hitstunFrames=18,knockback=0,canHitAirborne=true};
    static CombatHurtbox PlayerBox=>player.GetComponentInChildren<CombatHurtbox>();
    static AttackData Dummy()
    {
        var attack=Keep(ScriptableObject.CreateInstance<AttackData>());
        for(int i=0;i<120;i++)attack.frames.Add(new AttackFrameData{sprite=enemy.motor.sprite.sprite,movementInputScale=0});return attack;
    }
    static void RusherSlashTiming()
    {
        foreach(var path in new[]{"Attacks/Rusher_Attack_Slash1.asset","AI/Rusher_SlashCombo.asset"})
        {
            var attack=AssetDatabase.LoadAssetAtPath<AttackData>(ParryResponseSetup.Root+"/"+path);
            int swings=path.Contains("Combo") ? 2 : 1;
            Check(attack.TotalFrames==48*swings && attack.FirstActiveFrame==24,"Rusher slash has a 24-frame readable wind-up: "+attack.name);
            for(int i=0;i<attack.TotalFrames;i++)
            {
                bool active=i%48>=24 && i%48<=27;
                Check((attack.frames[i].hitboxes.Count>0)==active,"Rusher slash active-frame alignment "+attack.name+" / "+i);
                if(active) Check(attack.frames[i].sprite.name.Contains("Slash1_04") && attack.frames[i].hitboxes.All(h=>h.canBeParried && !h.unblockable && h.damage==4),"Extended blade pose carries the original parryable hit: "+i);
            }
            foreach(int facing in new[]{1,-1})
            {
                Fixture(facing:facing); enemy.attackPlayer.Play(attack); float hp=player.health.Current;
                Step(14); Check(player.health.Current==hp && enemy.attackPlayer.CurrentFrame<24,"Original frame14 impact is now a harmless telegraph");
                player.RequestGuard(true); Step(11);
                Check(player.health.Current==hp && PlayerBox.LastHitOutcome==CombatHitOutcome.Parry && enemy.reaction.State==EnemyReaction.Stunned && !enemy.attackPlayer.CurrentAttack,"Guard on the wind-up parries the real slash hitbox in facing "+facing);
                Fixture(facing:facing); player.RequestGuard(true); enemy.attackPlayer.Play(attack); Step(25);
                Check(player.health.Current==200 && PlayerBox.LastHitOutcome==CombatHitOutcome.Block,"Guard held from slash start becomes normal block at impact");
                Fixture(facing:facing); enemy.attackPlayer.Play(attack); hp=player.health.Current;
                Step(23); Check(player.health.Current==hp,"No slash hit lands before frame24");
                Step(); Check(player.health.Current==hp-4 && PlayerBox.LastHitOutcome==CombatHitOutcome.Hit,"Unguarded slash lands the unchanged four damage at frame24");
            }
        }
    }
    static void Melee(string name)
    {
        Fixture(name);var reaction=enemy.reaction;var attack=Dummy();enemy.attackPlayer.Play(attack);player.RequestGuard(true);
        Check(PlayerBox.Receive(Hit(),-1,enemy.motor)&&PlayerBox.LastHitOutcome==CombatHitOutcome.Parry&&player.health.Current==200,name+": melee parry negates damage");
        Check(reaction.State==EnemyReaction.Stunned&&reaction.StunRemaining==90&&!enemy.attackPlayer.CurrentAttack&&!reaction.CanAct,name+": same Stunned state interrupts attack for90f");
        Check(reaction.stunAnimation&&reaction.stunAnimation.frames.All(f=>f.sprite)&&reaction.StunVisual&&reaction.StunVisual.enabled&&!enemy.animationDriver.animator.enabled,name+": own sprite timeline and overhead VFX replace Walk/Attack");
        Check(!enemy.attackPlayer.Play(attack),name+": offensive playback cannot restart during stun");
        Check(Mathf.Approximately(player.GetComponent<PlayerMeter>().CurrentMeter,.4f),name+": successful melee parry gives existing significant meter gain");
        var armor=enemy.GetComponent<HitCountArmor>();if(armor)
        {
            Check(armor.ArmorRemaining==6,name+": parry bypasses armor without consuming six-hit resource");
            float hp=reaction.health.Current;enemy.GetComponentInChildren<CombatHurtbox>().Receive(Hit(),1,player.motor);
            Check(reaction.health.Current==hp-5&&armor.ArmorRemaining==6&&reaction.State==EnemyReaction.Stunned,name+": punish damage works while armor is suspended during stun");
        }
        var position=enemy.transform.position;Step(89);Check(reaction.StunRemaining==1&&enemy.transform.position==position,name+": cannot move and remains stunned through89 frames");
        Capture(name);
        Step();Check(reaction.CanAct&&reaction.State==EnemyReaction.Normal&&!reaction.StunVisual.enabled,name+": recovers exactly at90f and hides VFX");
        if(armor)Check(armor.ArmorRemaining==6,name+": armor count preserved after stun");
    }
    static void Boss()
    {
        Fixture("WhiteGhostBoss");var boss=enemy.GetComponent<TotemBossController>();boss.enabled=false;enemy.GetComponentInChildren<CombatHurtbox>().externalInvulnerable=false;
        var attack=Dummy();enemy.attackPlayer.Play(attack);player.RequestGuard(true);
        Check(PlayerBox.Receive(Hit(),-1,enemy.motor)&&player.health.Current==200&&PlayerBox.LastHitOutcome==CombatHitOutcome.Parry,"Boss: parryable attack still gives damage-free successful parry");
        Check(!enemy.reaction.CanBeParryStunned&&!enemy.reaction.IsStunState&&enemy.attackPlayer.CurrentAttack==attack,"Boss: classified controller prevents generic stun and attack interruption");
        Check(player.GetComponent<AttackFeedback>().ParryCount==1&&Mathf.Approximately(player.GetComponent<PlayerMeter>().CurrentMeter,.4f),"Boss: parry feedback and meter remain intact");
    }
    static void Grab(string name)
    {
        Fixture(name);enemy.motor.ResetForStage(Vector2.zero);player.motor.ResetForStage(new Vector2(name=="Ambusher"?2.4f:2,0));player.motor.Face(-1);enemy.motor.Face(1);
        var grab=enemy.GetComponent<CombatGrabController>();enemy.attackPlayer.Play(grab.grabAttack);
        int active=grab.grabAttack.FirstActiveFrame;Step(active-1);player.RequestGuard(true);
        for(int i=0;i<7&&!enemy.reaction.IsStunState&&!player.IsGrabbed;i++)Step();
        Check(enemy.reaction.State==EnemyReaction.Stunned&&!player.IsGrabbed&&player.health.Current==200,name+": real active grab is parried after readable telegraph");
        Check(!grab.LungeActive&&!grab.LeapActive&&!grab.GrabActive&&!grab.CurrentGrabbedTarget&&!enemy.attackPlayer.CurrentAttack&&enemy.motor.IsGrounded,name+": lunge/leap/hitbox/attachment are all cleaned up");
        var position=enemy.transform.position;Step(20);Check(enemy.transform.position==position&&enemy.reaction.StunRemaining>0,name+": cancelled grab motion does not drift during stun");
    }
    static void Push()
    {
        Fixture("Prefect");var support=enemy.GetComponent<PrefectSupport>();enemy.motor.ResetForStage(Vector2.zero);enemy.motor.Face(1);player.motor.ResetForStage(new Vector2(.7f,0));player.motor.Face(-1);
        enemy.attackPlayer.Play(support.pushAttack);player.RequestGuard(true);Step(6);
        Check(enemy.reaction.State==EnemyReaction.Stunned&&!enemy.attackPlayer.CurrentAttack&&player.health.Current==200,"Prefect: actual frame6 Push is parried and cancelled");
    }
    static CombatProjectile Shot()
    {
        var shot=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ParryResponseSetup.Root+"/Prefabs/NotebookProjectile.prefab"))).GetComponent<CombatProjectile>();
        shot.collideWithScenery=false;shot.InitializeForward(enemy.motor,enemy.hitbox.team,new Vector2(.25f*player.motor.Facing,0),.65f,-player.motor.Facing);return shot;
    }
    static void Projectiles()
    {
        foreach(int facing in new[]{1,-1})
        {
            Fixture("Thrower",facing);var attack=Dummy();enemy.attackPlayer.Play(attack);player.RequestGuard(true);var shot=Shot();Vector2 incoming=shot.Velocity;int originalLifetime=shot.lifetimeFrames;Step();
            Check(shot.DeflectionCount==1&&!shot.Resolved&&PlayerBox.LastHitOutcome==CombatHitOutcome.Parry&&player.health.Current==200,"Notebook "+facing+": swept active parry reflects without damage");
            Check(shot.Owner==player.motor&&shot.Faction==PlayerBox.team&&Vector2.Distance(shot.Velocity,-incoming*1.2f)<.001f&&shot.visual.flipX==(facing<0),"Notebook "+facing+": ownership/team, velocity and visual orientation reverse");
            Check(enemy.reaction.State==EnemyReaction.Normal&&enemy.attackPlayer.CurrentAttack==attack&&!enemy.attackPlayer.IsFrozen,"Notebook "+facing+": shooter is neither stunned, cancelled nor hitstopped by remote parry");
            Check(shot.Age==1&&shot.lifetimeFrames==originalLifetime&&!shot.Deflect(player.motor),"Notebook "+facing+": preserves age/lifetime and rejects second deflection");
            Check(Mathf.Approximately(player.GetComponent<PlayerMeter>().CurrentMeter,.4f),"Notebook "+facing+": projectile deflection gives successful-parry meter once");
            float hp=enemy.reaction.health.Current;for(int i=0;i<40&&shot&&shot.isActiveAndEnabled;i++)Step();
            Check(enemy.reaction.health.Current==hp-shot.hit.damage&&enemy.reaction.State==EnemyReaction.GroundHit&&player.health.Current==200,"Notebook "+facing+": reflected damage hits original Thrower with normal reaction and never hits parrier");
        }
        Fixture("Thrower");player.RequestGuard(true);Step(player.EffectiveParryWindow);var blocked=Shot();Step();Check(blocked.Resolved&&PlayerBox.LastHitOutcome==CombatHitOutcome.Block&&blocked.DeflectionCount==0,"Outside active window projectile blocks instead of deflecting");
        Fixture("Thrower");player.RequestGuard(true);var optedOut=Shot();optedOut.hit=optedOut.hit.RuntimeCopy();optedOut.hit.canBeParried=false;Step();Check(optedOut.DeflectionCount==0&&PlayerBox.LastHitOutcome==CombatHitOutcome.Block,"Projectile parry opt-out retains normal Guard behavior");
        Fixture("Thrower");player.RequestGuard(true);var notReflectable=Shot();notReflectable.canBeDeflected=false;Step();Check(notReflectable.Resolved&&notReflectable.DeflectionCount==0&&enemy.reaction.State==EnemyReaction.Normal,"Parryable non-deflectable projectile is neutralized without stunning shooter");
        Fixture("Thrower");player.RequestGuard(true);var tuned=Shot();tuned.deflectDamageMultiplier=2;tuned.deflectHitstunFrames=27;tuned.deflectKnockback=0;Step();float before=enemy.reaction.health.Current;for(int i=0;i<40&&tuned&&tuned.isActiveAndEnabled;i++)Step();Check(enemy.reaction.health.Current==before-tuned.hit.damage*2&&enemy.reaction.RecoveryFrames>=26,"Reflected damage/hitstun tunables reach normal accepted-hit path");
        Fixture("Thrower");player.RequestGuard(true);var lifetime=Shot();lifetime.lifetimeFrames=3;Step();enemy.motor.ResetForStage(new Vector2(10,0));Step(10);Check(!lifetime||!lifetime.isActiveAndEnabled,"Reflected projectile expires with finite original lifetime");
        var wave=AssetDatabase.LoadAssetAtPath<GameObject>(ParryResponseSetup.Root+"/Prefabs/ScreamWaveProjectile.prefab").GetComponent<CombatProjectile>();Check(!wave.hit.canBeParried&&!wave.canBeDeflected,"Screamer forward wave explicitly opts out of parry and reflection");
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);Check(catalog.AttackId(AssetDatabase.LoadAssetAtPath<AttackData>(ParryResponseSetup.DeflectPath))>=0&&catalog.SpriteId(enemy.reaction.stunVfx[0])>=0,"Multiplayer catalog contains deflection cue and shared stun sprites");
    }
    static void Status()
    {
        Fixture();enemy.reaction.stunRecoveryFrames=5;enemy.reaction.EnterStun(3);enemy.attackPlayer.Freeze(4);Step(4);Check(enemy.reaction.StunRemaining==3,"Combat hitstop freezes stun duration");Step(3);Check(enemy.reaction.State==EnemyReaction.StunRecovery&&!enemy.reaction.CanAct&&!enemy.reaction.StunVisual.enabled,"Optional recovery remains action-locked after status expires");Step(5);Check(enemy.reaction.CanAct,"Optional recovery returns to normal after configured frames");
        Fixture();enemy.reaction.EnterStun(90);enemy.reaction.health.Damage(10000);Check(enemy.reaction.State==EnemyReaction.Defeated&&!enemy.reaction.StunVisual.enabled,"Death overrides stun and removes indicator");enemy.reaction.health.Restore();Check(enemy.reaction.State==EnemyReaction.Normal&&enemy.reaction.CanAct,"Health restore clears status lock");
        Fixture("Prefect");enemy.enabled=true;enemy.RefreshAI();enemy.reaction.EnterStun(5);Step();Check(enemy.AI.StateName=="Stunned"&&enemy.AI.Selected==null,"AI exposes Stunned and clears incompatible pending action");Step(8);Check(!enemy.reaction.IsStunState&&enemy.AI.StateName!="Stunned","Existing AI resumes its normal graph after stun");
    }
    static void ActualThrower()
    {
        Fixture("Thrower");enemy.motor.ResetForStage(new Vector2(3,0));enemy.enabled=true;enemy.passiveTrainingDummy=false;enemy.RefreshAI();enemy.AI.ForceState("Attack");
        var launcher=enemy.GetComponent<EnemyProjectileAttack>();for(int i=0;i<100&&!launcher.LastProjectile;i++)Step();
        var shot=launcher.LastProjectile;if(shot)temporary.Add(shot.gameObject);
        Check(shot&&launcher.ProjectilesReleased==1&&shot.Owner==enemy.motor,"Real Thrower AI/frame event releases an enemy-owned notebook");
        for(int i=0;i<80&&shot&&shot.transform.position.x>.65f;i++)Step();player.RequestGuard(true);
        for(int i=0;i<8&&shot&&shot.DeflectionCount==0;i++)Step();
        Check(shot&&shot.DeflectionCount==1&&enemy.reaction.State==EnemyReaction.Normal,"Real released notebook reflects during fresh guard without stunning Thrower");
        float hp=enemy.reaction.health.Current;for(int i=0;i<80&&shot&&shot.isActiveAndEnabled;i++)Step();
        Check(enemy.reaction.health.Current<hp&&player.health.Current==200,"Real reflected Thrower notebook returns to hit its original owner");
        Fixture("Thrower");enemy.motor.ResetForStage(new Vector2(2,0));
        var peer=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath))).GetComponent<ComboController>();peer.GetComponent<PlayerCombatInput>().enabled=false;peer.health.SafeStageProtection=false;peer.health.Restore();peer.motor.ResetForStage(new Vector2(.9f,0));float peerHp=peer.health.Current;
        player.RequestGuard(true);var friendly=Shot();Step();float enemyHp=enemy.reaction.health.Current;for(int i=0;i<50&&friendly&&friendly.isActiveAndEnabled;i++)Step();
        Check(peer.health.Current==peerHp&&player.health.Current==200&&enemy.reaction.health.Current<enemyHp,"Reflected player faction skips another local player and damages enemy beyond them");
        Fixture("Thrower");player.RequestGuard(true);var protectedOwner=Shot();protectedOwner.canHitOriginalOwner=false;Step(25);
        Check(enemy.reaction.health.Current==200&&protectedOwner.DeflectionCount==1,"Explicit original-owner immunity prevents reflected self-hit");
    }
    static void Capture(string name)
    {
        var go=new GameObject("Parry stun review camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.8f;camera.transform.position=new Vector3(0,1,-10);var rt=new RenderTexture(900,600,24);camera.targetTexture=rt;var previous=RenderTexture.active;
        try{camera.Render();RenderTexture.active=rt;var texture=new Texture2D(900,600,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,900,600),0,0);texture.Apply();Directory.CreateDirectory("Documentation/ParryResponsePreview");File.WriteAllBytes("Documentation/ParryResponsePreview/"+name+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
}
