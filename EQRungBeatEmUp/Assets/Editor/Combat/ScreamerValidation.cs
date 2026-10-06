using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ScreamerValidation
{
    const string Pending = "BeatEmUp.ScreamerValidation", Request = "Temp/ScreamerValidation.request";
    static readonly List<string> results = new List<string>();
    static readonly List<Object> temporary = new List<Object>();
    static ComboController player;
    static EnemyCombat enemy;
    static AttackData attack;
    static CombatClock clock;
    static EnemyProjectileAttack Spawn => enemy.GetComponent<EnemyProjectileAttack>();
    static ScreamerValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Enemies/Validate Screamer (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static void Step(int frames = 1) { for (int i = 0; i < frames; i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    static void Until(Func<bool> ready, string label, int maximum = 400)
    { for (int i = 0; !ready() && i < maximum; i++) Step(); Check(ready(), label); }
    static void Clear()
    {
        foreach (var shot in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(shot.gameObject);
        foreach (var item in temporary.ToArray()) if (item) Object.DestroyImmediate(item);
        temporary.Clear();
    }
    static T Keep<T>(T item) where T : Object { temporary.Add(item); return item; }
    static void Fixture(Vector2 position, bool ai = false, int facing = 1)
    {
        Clear();
        player = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath))).GetComponent<ComboController>();
        player.GetComponent<PlayerCombatInput>().enabled = false;
        player.motor.ResetForStage(position); player.health.SafeStageProtection = false; player.health.Restore();
        enemy = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ScreamerSetup.PrefabPath))).GetComponent<EnemyCombat>();
        enemy.motor.ResetForStage(Vector2.zero); enemy.motor.Face(facing); enemy.reaction.health.Restore();
        enemy.reaction.health.SafeStageProtection = false;
        enemy.motor.arenaMin = player.motor.arenaMin = new Vector2(-20,-20); enemy.motor.arenaMax = player.motor.arenaMax = new Vector2(20,20);
        enemy.passiveTrainingDummy = false; enemy.target = player.transform;
        attack = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<AttackData>(ScreamerSetup.AttackPath)));
        enemy.attack = attack;
        if (ai)
        {
            var profile = Keep(Object.Instantiate(enemy.aiProfile)); enemy.aiProfile = profile;
            profile.attacks[0].attack = attack; enemy.RefreshAI();
        }
        else { enemy.aiProfile = null; enemy.attackCooldownFrames = 9999; }
        Physics2D.SyncTransforms();
    }
    static AttackFeedback Start()
    { Check(enemy.attackPlayer.Play(attack), "Frame-data scream starts"); return enemy.GetComponent<AttackFeedback>(); }
    static CombatProjectile Fire()
    {
        Until(() => Spawn.ProjectilesReleased == 1, "One wave releases on frame 36");
        return Spawn.LastProjectile;
    }
    static void Poll()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Request))
        { try { File.Delete(Request); } catch (IOException) { return; } Run(); return; }
        if (!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear();
        try
        {
            foreach (var flow in Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) flow.enabled = false;
            foreach (var framing in Object.FindObjectsByType<StageFraming>(FindObjectsSortMode.None)) framing.enabled = false;
            foreach (var motor in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) motor.gameObject.SetActive(false);
            foreach (var wall in Object.FindObjectsByType<CombatWall>(FindObjectsSortMode.None)) wall.gameObject.SetActive(false);
            clock = Object.FindFirstObjectByType<CombatClock>(); if (!clock) clock = Keep(new GameObject("Screamer validation clock")).AddComponent<CombatClock>();
            clock.enabled = false; clock.combatFPS = 60;
            Phases(); DirectionAndDepth(); Reactions(); StunStatus(); InterruptionsAndAI(); LimitsAndImmunity(); Regression();
            results.Add("NOTE: Separate online peers were not rerun. Existing sprite snapshots / catalog carry directional warning and all wave crescents.");
            Debug.Log("SCREAMER VALIDATION PASSED: " + results.Count + " results");
        }
        catch (Exception error) { results.Add("FAIL: " + error); Debug.LogException(error); }
        finally
        {
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/ScreamerValidationResults.txt",results);
            Clear(); EditorApplication.ExitPlaymode();
        }
    }
    static void Phases()
    {
        Fixture(new Vector2(3,0));
        Check(attack.FirstActiveFrame==36 && attack.ActiveFrames==8 && attack.LastActiveFrame==43 && attack.TotalFrames==74,"Preserved 36f telegraph, 8f scream, 30f recovery");
        Check(attack.frames.All(f=>f.hitboxes.Count==0) && !attack.feedback.areaWarning,"Every radial / actor-owned scream hitbox removed");
        Check(attack.frames.All(f=>!f.superArmor && !f.invulnerable && f.movementInputScale==0),"All phases remain stationary and interruptible");
        attack.feedback.telegraphSound=Keep(AudioClip.Create("Test warning",44100,1,44100,false));
        attack.feedback.screamSound=Keep(AudioClip.Create("Test scream",44100,1,44100,false));
        var feedback=Start();
        Check(feedback.WarningVisual && !feedback.WaveVisual && feedback.TelegraphCount==1,"Directional corridor warning at frame zero, no radial VFX");
        Check(feedback.WarningVisual.bounds.min.x>0 && Mathf.Abs(feedback.WarningVisual.bounds.size.y-1.2f)<.001f,"Warning lies entirely in front and communicates exact full depth");
        Check(feedback.LastSound && feedback.LastSound.clip==attack.feedback.telegraphSound,"Telegraph sound hook retained"); Capture("Telegraph");
        var position=enemy.transform.position; enemy.motor.MoveInput=Vector2.right; Step(35);
        Check(enemy.transform.position==position && Spawn.ProjectilesReleased==0 && player.health.Current==200,"Telegraph neither moves nor damages / spawns early");
        Step(); var shot=Spawn.LastProjectile;
        Check(enemy.attackPlayer.CurrentFrame==36 && shot && !feedback.WarningVisual && feedback.ScreamCount==1,"One wave replaces warning exactly on release frame 36");
        Check(feedback.LastSound && feedback.LastSound.clip==attack.feedback.screamSound,"Separate scream audio hook retained");
        Check(shot.groundWave && shot.Velocity==Vector2.right*6 && Mathf.Abs(shot.transform.position.x-Spawn.releaseOffset.x)<.001f,"Shared projectile uses forward ground rectangle at mouth offset");
        Check(shot.visual && shot.trailVisuals.Length==2 && shot.flightSprites.Length==6,"Three forward crescents reuse six original pixel wave frames"); Capture("Active");
        Step(8); Check(enemy.attackPlayer.CurrentFrame==44 && attack.Phase(44)=="Recovery" && shot && shot.transform.position.x>Spawn.releaseOffset.x,"Screamer enters recovery while its wave travels independently"); Capture("ActiveEnd");
        Until(()=>!enemy.attackPlayer.CurrentAttack,"Cast completes existing recovery");
        Check(Spawn.ProjectilesReleased==1,"One release per cast despite hitstop / recovery");
        AttackFeedback.PlayRemote(attack,Vector2.zero,1,false,"Telegraph",991);
        var sound=Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).First(s=>s.name=="Network combat sound" && s.clip==attack.feedback.telegraphSound);
        AttackFeedback.PlayRemote(attack,Vector2.zero,1,false,"Scream",991); Check(!sound.isPlaying,"Remote scream cancels telegraph audio");
        AttackFeedback.PlayRemote(attack,Vector2.zero,1,false,"Telegraph",992);
        sound=Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).First(s=>s.name=="Network combat sound" && s.clip==attack.feedback.telegraphSound && s.isPlaying);
        AttackFeedback.PlayRemote(attack,Vector2.zero,1,false,"StopArea",992); Check(!sound.isPlaying,"Remote interruption cancels warning audio");
    }
    static void DirectionAndDepth()
    {
        foreach(int facing in new[]{1,-1})
        foreach(var point in new[]{new Vector2(2.5f,0),new Vector2(-1,0),new Vector2(2.5f,.59f),new Vector2(2.5f,.61f),new Vector2(2.5f,-.61f),new Vector2(7,0),new Vector2(0,1)})
        {
            var actual=new Vector2(point.x*facing,point.y); Fixture(actual,false,facing); Start(); var shot=Fire();
            Check(shot.Velocity.x*facing>0 && shot.transform.localScale.x==facing,"Travel and all visual crescents mirror for facing "+facing);
            bool expected=point.x>0 && point.x<=6.6f && Mathf.Abs(point.y)<=.6f;
            Step(95); Check((player.health.Current<200)==expected,"Swept rectangular front/lane threat at "+actual+", facing "+facing+", hit="+expected);
            if(expected)Check(player.health.Current==186,"Duplicate overlap causes only one 14-damage hit");
        }
        Fixture(new Vector2(3,0)); Start(); Fire(); player.motor.ResetForStage(new Vector2(-1,0)); Step(90);
        Check(player.health.Current==200,"Moving behind during warning / flight avoids wave");
        Fixture(new Vector2(3,0)); Start(); Step(30); player.motor.ResetForStage(new Vector2(3,1)); Fire(); Step(90);
        Check(player.health.Current==200,"Sidestepping warning's depth avoids wave");
        Fixture(new Vector2(3,0)); var feedback=Start(); Object.DestroyImmediate(feedback); Fire(); Until(()=>player.State==CombatState.Stunned,"Projectile stun remains authoritative with telegraph VFX removed");
        Fixture(new Vector2(3,0)); Start(); Step(34); player.motor.ResetForStage(new Vector2(-3,0)); var shot2=Fire();
        Check(enemy.attackPlayer.Facing==1 && shot2.Velocity.x>0,"Committed facing does not retarget a player crossing behind just before release");
        Step(12); Check(shot2 && shot2.Velocity.y==0 && player.health.Current==200,"Wave never homes toward moving target"); Capture("FacingLocked");
    }
    static void Reactions()
    {
        for(int mode=0;mode<6;mode++)
        {
            Fixture(new Vector2(1.2f,0)); Start(); Fire();
            if(mode==1){player.RequestAttack();player.RequestAttack();}
            if(mode==2){player.motor.Launch(8,0);player.motor.Simulate(.1f);player.RequestAttack();}
            if(mode==3)player.Interrupt(90);
            if(mode==4)player.RequestJump();
            if(mode==5)player.RequestGuard(true);
            Step(); Check(player.State==CombatState.Stunned && !player.CurrentAttack && player.motor.MovementLocked,"Wave contact enters explicit Stunned status / cancels player case "+mode);
            Check(player.BufferedInput==CombatInput.None && !player.JumpBuffered,"Stun clears buffered combat inputs");
            if(mode==2 || mode==4)Check(player.motor.VerticalVelocity>0 && !player.motor.AirAttackControl,"Airborne stun preserves natural upward trajectory and releases attack control");
            int remaining=player.StunFramesRemaining;
            Check(remaining==90,"Scream applies 90-frame Stunned status separate from normal Hitstun");
            if(mode==0)Capture("Stunned");
            player.RequestAttack(); player.RequestLauncher(); player.RequestJump(); player.RequestDodge(); player.RequestGuard(true);
            Check(player.State==CombatState.Stunned && !player.CurrentAttack && player.BufferedInput==CombatInput.None && !player.JumpBuffered && !player.GuardHeld && !player.CanStartSkill,"Stunned player rejects attack, launcher, jump, dodge, guard and skill");
            Step(4); Check(player.StunFramesRemaining==remaining,"Hitstop pauses the stun countdown");
            Step(remaining-1); Check(player.State==CombatState.Stunned && player.StunFramesRemaining==1 && player.motor.MovementLocked,"Stun lasts its authored combat frames without Downed or GetUp");
            Step(); Check(player.StunFramesRemaining==0,"Stun countdown reaches zero");
            Until(()=>player.State==CombatState.Idle,"Stunned status returns neutral after natural landing");
            Check(!player.motor.MovementLocked && player.health.Current==186,"Player recovers control after exactly one wave hit");
            player.RequestAttack(); Check(player.CurrentAttack && player.State==CombatState.GroundAttack,"Player can attack again after stun recovery");
        }
        Fixture(new Vector2(1.2f,0)); var second=Keep(Object.Instantiate(player.gameObject)).GetComponent<ComboController>();
        second.motor.ResetForStage(new Vector2(-1.2f,0)); second.health.Restore(); Start(); Fire(); Step();
        Check(player.State==CombatState.Stunned && second.State==CombatState.Idle && second.health.Current==200,"Front player stunned; independent player behind remains safe");
    }
    static void StunStatus()
    {
        Action fixture=()=>{Fixture(new Vector2(3,0)); enemy.enabled=false;};
        fixture();
        var hurtbox=player.GetComponentInChildren<CombatHurtbox>();
        var hit=new AttackHitboxData{damage=1,hitType=HitType.Stun,stunDurationFrames=60,hitstopFrames=0,knockback=0,canHitAirborne=true};
        Check(hurtbox.Receive(hit,-1,enemy.motor) && player.IsStunned,"Any attack can apply reusable Stun reaction through hit data");
        var data=player.defenseData;
        Check(data.stunned.Length==5 && data.stunned.All(p=>p.sprite && p.frames==6) && data.stunVfx.Length==6 && data.stunVfx.All(s=>s),"Five 6-frame body poses and six overhead VFX frames configured");
        Check(player.motor.sprite.sprite==data.stunned[0].sprite && player.StunVisual.enabled && player.StunVisual.sprite==data.stunVfx[0],"Stun begins with dedicated dazed pose and overhead indicator");
        Step(6); Check(player.motor.sprite.sprite==data.stunned[1].sprite && player.StunVisual.sprite==data.stunVfx[1],"Body and VFX advance on authored combat-frame holds");
        Step(24); Check(player.motor.sprite.sprite==data.stunned[0].sprite && player.StunVisual.sprite==data.stunVfx[0],"Body / orbit loops repeat every 30 combat frames");
        var position=player.transform.position; player.motor.MoveInput=Vector2.one;
        int remaining=player.StunFramesRemaining;
        CombatClock.SetPaused(player.gameObject,true); Step(10);
        Check(player.StunFramesRemaining==remaining && player.motor.sprite.sprite==data.stunned[0].sprite,"Global pause freezes status and sprite loop");
        CombatClock.SetPaused(player.gameObject,false); Step(5);
        Check(player.transform.position==position && player.motor.IsGrounded,"Standing stun prevents horizontal / lane movement without a knockdown");
        hurtbox.Receive(new AttackHitboxData{damage=3,hitstunFrames=18,hitstopFrames=0},-1,enemy.motor);
        Check(player.health.Current==196 && player.IsStunned && player.StunFramesRemaining==25,"Stunned player stays vulnerable; normal hitstun cannot clear the status");
        hit.stunDurationFrames=120; hurtbox.Receive(hit,-1,enemy.motor);
        Check(player.StunFramesRemaining==120,"Repeated Stun refreshes remaining duration to the longer authored value");
        hit.stunDurationFrames=60; hurtbox.Receive(hit,-1,enemy.motor);
        Check(player.StunFramesRemaining==120,"Shorter repeated Stun cannot shorten existing status");
        Step(120); Check(player.State==CombatState.Idle && !player.StunVisual.enabled && player.animationDriver.animator.enabled,"Status expires cleanly, removes VFX and restores animator / normal control");
        hurtbox.Receive(new AttackHitboxData{damage=1,hitstunFrames=18,hitstopFrames=0},-1,enemy.motor);
        Check(player.State==CombatState.Hitstun && !player.IsStunned && !player.StunVisual.enabled,"Normal attacks still use the separate short Hitstun reaction");
        fixture(); hurtbox=player.GetComponentInChildren<CombatHurtbox>(); player.motor.Face(-1); hurtbox.Receive(hit,1,enemy.motor);
        Check(player.motor.sprite.flipX && player.StunVisual.flipX && player.StunVisual.sortingOrder==player.motor.sprite.sortingOrder+2,"Body and overhead effect mirror and sort with the owner's renderer");
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        Check(data.stunned.All(p=>catalog.SpriteId(p.sprite)>=0) && data.stunVfx.All(s=>catalog.SpriteId(s)>=0),"All stun body / VFX sprites registered for authoritative multiplayer snapshots");
        hurtbox.Receive(new AttackHitboxData{damage=1,hitType=HitType.KnockDown,hitstopFrames=0},-1,enemy.motor);
        Check(player.State==CombatState.KnockDown && !player.StunVisual.enabled,"A separate knockdown reaction overrides status and clears overhead effect");
        fixture(); hurtbox=player.GetComponentInChildren<CombatHurtbox>(); hurtbox.Receive(hit,-1,enemy.motor); player.health.Damage(500);
        Check(player.State==CombatState.Die && !player.StunVisual.enabled,"Lethal damage overrides Stunned and clears VFX");
        player.health.Restore(); Check(player.State==CombatState.Idle && !player.StunVisual.enabled && player.StunFramesRemaining==0,"Restore resets status and visual control");
        hurtbox.Receive(hit,-1,enemy.motor); player.enabled=false;
        Check(!player.StunVisual.enabled && !player.motor.MovementLocked,"Disabling an actor clears its owned VFX and movement lock");
        fixture(); player.defenseData=null; player.EnterStun(60); Step(60);
        Check(player.State==CombatState.Idle && !player.motor.MovementLocked,"Missing cosmetic data does not prevent status recovery");
    }
    static void InterruptionsAndAI()
    {
        Fixture(new Vector2(2,0),true); Until(()=>enemy.attackPlayer.CurrentAttack,"AI stops and starts telegraph when ready");
        var feedback=enemy.GetComponent<AttackFeedback>();
        enemy.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{damage=1,hitstunFrames=18,hitstopFrames=0},-1,player.motor);
        Check(!enemy.attackPlayer.CurrentAttack && !feedback.WarningVisual && Spawn.ProjectilesReleased==0,"Interrupting telegraph cancels directional warning and pending spawn");
        Check(enemy.AI.Cooldown(enemy.aiProfile.attacks[0].id)>2.4f,"Telegraph interruption consumes unchanged 2.5-second cooldown");
        Step(50); Check(player.health.Current==200 && Spawn.ProjectilesReleased==0,"No delayed wave leaks from cancelled telegraph");
        Fixture(new Vector2(-2,0),true); Until(()=>enemy.attackPlayer.CurrentAttack,"AI finds left-hand target");
        Check(enemy.motor.Facing==-1 && enemy.attackPlayer.Facing==-1 && enemy.motor.MoveInput==Vector2.zero,"AI faces left before committing and stops chase");
        player.motor.ResetForStage(new Vector2(2,0)); var shot=Fire(); Check(shot.Velocity.x<0,"AI maintains committed direction after target crosses");
        player.motor.ResetForStage(new Vector2(2,2)); Until(()=>!enemy.attackPlayer.CurrentAttack,"AI completes cast recovery");
        Check(enemy.AI.Cooldown(enemy.aiProfile.attacks[0].id)>2.4f,"Natural finish starts existing 120f plus .5s cooldown");
        Step(90); Check(!enemy.attackPlayer.CurrentAttack,"Cooldown prevents immediate repeat attack");
        Fixture(new Vector2(2,.7f),true); enemy.AI.ForceState("Attack"); Step(4); Check(!enemy.attackPlayer.CurrentAttack,"AI does not fire at a target outside configured wave depth");
        Fixture(new Vector2(4,0)); Start(); Fire(); Until(()=>enemy.attackPlayer.CurrentFrame==44,"Recovery interruption begins at frame 44");
        enemy.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{damage=1,hitstunFrames=18},-1,player.motor);
        Check(!enemy.attackPlayer.CurrentAttack && enemy.reaction.State==EnemyReaction.GroundHit,"Screamer recovery stays vulnerable");
    }
    static void LimitsAndImmunity()
    {
        Fixture(new Vector2(8,0)); Start(); var shot=Fire(); float start=shot.transform.position.x;
        shot.maximumTravelDistance=.3f; Step(3); Check(shot.Resolved && Mathf.Abs(shot.DistanceTraveled-.3f)<.0001f && Mathf.Abs(shot.transform.position.x-start-.3f)<.0001f,"Maximum distance caps travel precisely and expires wave");
        Fixture(new Vector2(8,0)); Start(); shot=Fire(); shot.maximumTravelDistance=0; shot.lifetimeFrames=3; Step(3); Check(!shot.Resolved,"Wave remains through configured lifetime frames"); Step(); Check(shot.Resolved,"Wave lifetime expires independently from caster recovery");
        Fixture(new Vector2(8,0)); Start(); shot=Fire(); var position=shot.transform.position; CombatClock.SetPaused(enemy.gameObject,true); Step(10);
        Check(shot.Age==0 && shot.transform.position==position,"Pause freezes wave movement, age and animation"); CombatClock.SetPaused(enemy.gameObject,false);
        Fixture(new Vector2(1.2f,0)); Start(); Step(31); player.RequestDodge(); Step(4);
        Check(player.DodgeInvulnerable,"Existing dodge is inside its authored immunity window"); Fire(); player.motor.ResetForStage(new Vector2(1.2f,0)); Step();
        Check(player.health.Current==200,"Existing dodge immunity rejects directional wave contact");
        Fixture(new Vector2(1.2f,0)); player.GetComponentInChildren<CombatHurtbox>().externalInvulnerable=true; Start(); Fire(); Step(70);
        Check(player.health.Current==200,"External immunity remains authoritative");
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        Check(catalog.SpriteId(attack.feedback.areaRingSprite)>=0 && Spawn.projectilePrefab.flightSprites.All(s=>catalog.SpriteId(s)>=0),"Directional warning and original wave art registered in multiplayer catalog");
        Check(catalog.AttackId(AssetDatabase.LoadAssetAtPath<AttackData>(ScreamerSetup.AttackPath))>=0,"Scream timeline remains in multiplayer attack catalog");
    }
    static void Regression()
    {
        var notebook=AssetDatabase.LoadAssetAtPath<GameObject>(ThrowerProjectileSetup.ProjectilePath).GetComponent<CombatProjectile>();
        Check(!notebook.groundWave && notebook.maximumTargets==1 && notebook.maximumTravelDistance==0,"Notebook retains existing aimed circular, single-target defaults");
        var thrower=AssetDatabase.LoadAssetAtPath<GameObject>(ThrowerProjectileSetup.ThrowerPath).GetComponent<EnemyProjectileAttack>();
        Check(!thrower.forwardOnly && thrower.releaseEvent=="ThrowProjectile","Thrower event and target-aimed spawn remain unchanged");
        foreach(int facing in new[]{1,-1})
        {
            Fixture(new Vector2(facing*3,.2f)); enemy.enabled=false;
            var brain=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ThrowerProjectileSetup.ThrowerPath))).GetComponent<EnemyCombat>();
            brain.aiProfile=null; brain.passiveTrainingDummy=false; brain.target=player.transform; brain.attackCooldownFrames=9999;
            brain.motor.ResetForStage(Vector2.zero); brain.motor.Face(facing);
            Check(brain.attackPlayer.Play(brain.attack),"Existing Thrower cast begins, facing "+facing);
            var ranged=brain.GetComponent<EnemyProjectileAttack>(); Step(17);
            Check(ranged.ProjectilesReleased==0,"Thrower still has 18-frame anticipation"); Step();
            Check(ranged.ProjectilesReleased==1 && ranged.LastProjectile && ranged.LastProjectile.Velocity.x*facing>0,"Thrower still fires one aimed notebook at frame 18");
            Until(()=>player.health.Current<200,"Existing aimed circle projectile still damages player");
            Check(player.health.Current==200-notebook.hit.damage,"Notebook retains its original damage amount");
        }
    }
    static void Capture(string phase)
    {
        var go=new GameObject("Screamer validation camera"); var camera=go.AddComponent<Camera>(); camera.enabled=false; camera.orthographic=true; camera.orthographicSize=2.8f;
        camera.transform.position=new Vector3(enemy.attackPlayer.Facing*2,1,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.08f,.12f);
        var target=new RenderTexture(1280,720,24); camera.targetTexture=target; var old=RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active=target; var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply(); Directory.CreateDirectory("Documentation/ScreamerPreview");
            File.WriteAllBytes("Documentation/ScreamerPreview/"+phase+".png",texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        }
        finally{RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(go);}
    }
}
