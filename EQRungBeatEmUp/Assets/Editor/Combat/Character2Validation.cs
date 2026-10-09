using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Character2Validation
{
    const string Pending="Character2.Validation";
    static readonly List<string> results=new List<string>();
    static readonly List<GameObject> fixtures=new List<GameObject>();
    static ComboController player;
    static CombatClock clock;
    static PlayableCharacterData character,blue;
    static bool cleaning,cleanupReady,failed,presentationOnly;
    static Character2Validation() { EditorApplication.update+=Poll; }
    public static void RunPresentation()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        SessionState.SetBool("Character2.PresentationOnly",true);
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    [MenuItem("Beat Em Up/Characters/Validate Character 2 (Play Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        SessionState.SetBool("Character2.PresentationOnly",false);
        EditorSceneManager.OpenScene("Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity");
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition,string message)
    {
        if(!condition) throw new Exception(message);
        results.Add("PASS: "+message);
    }
    static void Poll()
    {
        if(SessionState.GetBool("Character2.Finished",false) && !EditorApplication.isPlayingOrWillChangePlaymode && Application.isBatchMode) { EditorApplication.Exit(SessionState.GetInt("Character2.ExitCode",1)); return; }
        if(cleaning && cleanupReady && EditorApplication.isPlaying)
        {
            cleaning=false;
            try
            {
                var remaining=Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None).Where(v=>v.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.sprite && AssetDatabase.GetAssetPath(r.sprite).StartsWith(Character2Setup.Art))).ToArray();
                Check(remaining.Length==0,"Barrier shell and pulse effects expire without leaked objects (remaining="+remaining.Length+", timeScale="+Time.timeScale+", scaledTime="+Time.time+")");
            }
            catch(Exception ex) { failed=true; results.Add("FAIL: "+ex); }
            Finish(); return;
        }
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); failed=false;
        try
        {
            foreach(var flow in Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) { flow.enabled=false; if(flow.GetComponent<PlayerHubController>()) flow.GetComponent<PlayerHubController>().enabled=false; }
            foreach(var actor in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach(var instance in Object.FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) instance.enabled=false;
            clock=Object.FindFirstObjectByType<CombatClock>();
            character=AssetDatabase.LoadAssetAtPath<PlayableCharacterData>(Character2Setup.DefinitionPath);
            blue=AssetDatabase.LoadAssetAtPath<PlayableCharacterData>(Character2Setup.Root+"/BlueShirtGuy.asset");
            presentationOnly=SessionState.GetBool("Character2.PresentationOnly",false);
            SessionState.SetBool("Character2.PresentationOnly",false);
            if(!presentationOnly) { Assets(); Routes(); Bounces(); Defense(); BlueSkill(); Area(); BossGate(); }
            Presentation();
            cleaning=true; cleanupReady=false;
            clock.StartCoroutine(WaitForEffects());
        }
        catch(Exception ex) { failed=true; results.Add("FAIL: "+ex); Debug.LogException(ex); Finish(); }
    }
    static System.Collections.IEnumerator WaitForEffects()
    {
        yield return new WaitForSeconds(1.2f);
        yield return null;
        cleanupReady=true;
    }
    static void Finish()
    {
        foreach(var go in fixtures) if(go) Object.DestroyImmediate(go); fixtures.Clear();
        results.Add(failed ? "CHARACTER 2 VALIDATION FAILED" : presentationOnly ? "ALL WAND BARRIER PRESENTATION CHECKS PASSED" : "ALL CHARACTER 2 CHECKS PASSED");
        Directory.CreateDirectory("Documentation"); File.WriteAllLines(presentationOnly ? "Documentation/WandBarrierPresentationValidationResults.txt" : "Documentation/Character2ValidationResults.txt",results);
        Debug.Log(string.Join("\n",results));
        SessionState.SetInt("Character2.ExitCode",failed ? 1 : 0); SessionState.SetBool("Character2.Finished",true);
        EditorApplication.ExitPlaymode();
    }
    static void Step(int frames=1) { for(int i=0;i<frames;i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    static void Presentation()
    {
        Check(character.skill.cast.feedback.waveColor.a<=.22f,"Area pulse cue stays subtle alongside the dome");
        Fixture(); var skill=player.GetComponent<PlayerSkillController>();
        Check(!character.skill.guardianFeedback.feedback.impactRotateWithFacing && !character.skill.releaseFeedback.feedback.impactRotateWithFacing,"Surrounding barrier feedback stays upright independently of facing");
        foreach(int facing in new[]{1,-1})
        {
            foreach(var visual in Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None)) Object.DestroyImmediate(visual.gameObject);
            Fixture(); skill=player.GetComponent<PlayerSkillController>(); player.motor.Face(facing);
            Check(skill.RequestSkill(),"Presentation cast starts facing "+facing);
            Step(24);
            var effects=Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None);
            Check(effects.Length==2,"Only barrier shell and pulse spawn for one cast");
            foreach(var visual in effects)
            {
                var sorted=visual.GetComponentInChildren<GroundSortedEffect>();
                Check(sorted,"Wand magic follows ground depth");
                foreach(float y in new[]{-2f,0f,2f})
                {
                    visual.transform.position=new Vector3(0,y,0); sorted.RefreshSorting();
                    Check(visual.GetComponentsInChildren<SpriteRenderer>().All(r=>r.sortingOrder<Mathf.RoundToInt(-y*100) && r.color.a<=.45f),"Magic stays translucent and behind caster at depth "+y);
                }
                visual.transform.position=player.transform.position; sorted.RefreshSorting();
            }
            Check(effects.All(v=>v.transform.position==player.transform.position && v.transform.GetChild(0).localRotation==Quaternion.identity),"Both effects originate at caster and remain upright facing "+facing);
            foreach(var visual in effects) foreach(var animator in visual.GetComponentsInChildren<Animator>()) animator.Update(.25f);
            Capture("WandBarrier_"+(facing>0 ? "Right" : "Left"));
        }
        foreach(var visual in Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None)) Object.DestroyImmediate(visual.gameObject);
        if(presentationOnly) return;
        Fixture(); player.attackPlayer.Play(character.groundCombo[2]); Step(character.groundCombo[2].FirstActiveFrame);
        Capture("HeadSnake_Impact");
    }
    static void Capture(string name)
    {
        var go=new GameObject("Presentation capture camera"); var camera=go.AddComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=1.65f; camera.transform.position=player.transform.position+new Vector3(0,.65f,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.07f,.09f);
        var texture=new RenderTexture(800,450,24); camera.targetTexture=texture;
        var previous=RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active=texture;
            var image=new Texture2D(800,450,TextureFormat.RGBA32,false); image.ReadPixels(new Rect(0,0,800,450),0,0); image.Apply();
            Directory.CreateDirectory("Documentation/Character2PolishPreview"); File.WriteAllBytes("Documentation/Character2PolishPreview/"+name+".png",image.EncodeToPNG()); Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active=previous; camera.targetTexture=null; texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(go); }
    }
    static void Until(Func<bool> condition,int limit=150)
    {
        for(int i=0;i<limit && !condition();i++) Step();
        Check(condition(),"Combat frame condition reached");
    }
    static void Fixture()
    {
        foreach(var old in fixtures) if(old) Object.DestroyImmediate(old); fixtures.Clear();
        var go=Object.Instantiate(character.prefab); fixtures.Add(go);
        go.GetComponent<PlayerCombatInput>().enabled=false; go.GetComponent<PlayerInput>().enabled=false;
        player=go.GetComponent<ComboController>(); player.health.maximumHealth=10000; player.health.Restore();
        player.motor.ResetForStage(Vector2.zero); player.motor.arenaMin=new Vector2(-20,-20); player.motor.arenaMax=new Vector2(20,20);
    }
    static EnemyHitReaction Enemy(Vector2 position)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab")); fixtures.Add(go);
        var ai=go.GetComponent<EnemyCombat>(); if(ai) ai.enabled=false;
        var enemy=go.GetComponent<EnemyHitReaction>(); enemy.health.maximumHealth=10000; enemy.health.Restore();
        enemy.motor.ResetForStage(position); enemy.motor.arenaMin=new Vector2(-20,-20); enemy.motor.arenaMax=new Vector2(20,20); return enemy;
    }
    static void Assets()
    {
        Check(character && character.prefab && blue && blue.prefab,"Both character definitions have reusable prefabs");
        Check(PrefabUtility.GetPrefabAssetType(character.prefab)==PrefabAssetType.Variant,"Character 2 is a BlueShirtGuy prefab variant");
        Fixture(); Check(player.GetComponent<PlayerCharacterLoadout>().character==character,"Character 2 instantiates through shared data loadout");
        Check(player.motor.moveSpeed==blue.prefab.GetComponent<CharacterMotor>().moveSpeed && player.motor.sprite.transform.localScale==blue.prefab.GetComponent<CharacterMotor>().sprite.transform.localScale,"Movement speed and sprite transform scale match BlueShirtGuy");
        Check(character.idlePose.rect.size==new Vector2(128,128) && character.idlePose.pixelsPerUnit==100,"Sprites use BlueShirtGuy 128px canvas and 100 PPU");
        Check(AssetDatabase.GetAssetPath(character.idlePose).EndsWith("Idle_01.png"),"Casual walking-like idle is the default pose");
        var controller=(UnityEditor.Animations.AnimatorController)character.locomotion;
        foreach(string state in new[]{"Idle","Walk","Jumping","GroundHit"}) Check(controller.layers[0].stateMachine.states.Any(s=>s.state.name==state),"Locomotion state available: "+state);
        Check(AnimationUtility.GetAnimationClipSettings((AnimationClip)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Idle").state.motion).loopTime,"Four-pose subtle idle loops");
        var walk=(AnimationClip)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Walk").state.motion;
        var walkKeys=AnimationUtility.GetObjectReferenceCurve(walk,AnimationUtility.GetObjectReferenceCurveBindings(walk).Single());
        Check(walkKeys.Length==13 && walkKeys.Take(12).Select(k=>k.value).Distinct().Count()==12 && walkKeys[0].value==walkKeys[12].value,"Twelve distinct walk poses loop through contact and passing drawings");
        Check(Mathf.Abs(walkKeys[12].time-.6f)<.0001f,"Walk has a 36 combat-frame casual stride cycle");
        Check(character.skill.displayName=="Wand Barrier" && character.skill.meterCost==1 && character.skill.delivery==PlayerSkillDelivery.Area,"Wand Barrier retains shared one-bar area delivery");
        Check(character.skill.guardianEvent=="RaiseBarrier" && character.skill.releaseEvent=="BarrierPulse","Barrier shell and pulse use cast timeline events");
        Check(character.skill.guardianFeedback.feedback.impactPrefab.name=="WandBarrier" && character.skill.releaseFeedback.feedback.impactPrefab.name=="BarrierPulse","Active skill effects replace the elephant with surrounding barrier and pulse");
        Check(character.groundCombo[2].frames[character.groundCombo[2].FirstActiveFrame].sprite.name=="Punch3_03","Snake extension drawing coincides with Punch 3 active frames");
        player.motor.Face(-1); Check(player.motor.sprite.flipX,"Facing left mirrors shared renderer"); player.motor.Face(1); Check(!player.motor.sprite.flipX,"Facing right restores renderer");
        var own=character.groundCombo.Concat(new[]{character.launcher}).Concat(character.airCombo).Concat(new[]{character.airDive}).ToArray();
        var originals=blue.groundCombo.Concat(new[]{blue.launcher}).Concat(blue.airCombo).Concat(new[]{blue.airDive}).ToArray();
        for(int attack=0;attack<own.Length;attack++)
        {
            Check(own[attack]!=originals[attack] && own[attack].TotalFrames==originals[attack].TotalFrames,"Independent data with matching logical timing: "+own[attack].name);
            for(int frame=0;frame<own[attack].TotalFrames;frame++)
            {
                var a=own[attack].frames[frame]; var b=originals[attack].frames[frame];
                Check(a.sprite && AssetDatabase.GetAssetPath(a.sprite).StartsWith(Character2Setup.Art),"Own sprite: "+own[attack].name+" frame "+frame);
                Check(a.movement==b.movement && a.hitboxes.Count==b.hitboxes.Count && a.canCancelIntoAttack==b.canCancelIntoAttack && a.canCancelIntoLauncher==b.canCancelIntoLauncher && a.canCancelIntoJump==b.canCancelIntoJump,"Shared movement/hitbox/cancel values: "+own[attack].name+" frame "+frame);
                for(int hit=0;hit<a.hitboxes.Count;hit++)
                {
                    var expected=b.hitboxes[hit].RuntimeCopy();
                    if(attack==2) { expected.offset=new Vector2(.53f,.88f); expected.size=new Vector2(.9f,.42f); }
                    Check(JsonUtility.ToJson(a.hitboxes[hit])==JsonUtility.ToJson(expected),"Preserved hit/bounce data with authored snake geometry: "+own[attack].name+" frame "+frame);
                }
            }
        }
        Check(character.defense!=blue.defense && character.skill!=blue.skill,"Independent defense and skill assets");
        Check(player.GetComponent<PlayerMeter>().CurrentMeter==1 && player.GetComponent<PlayerMeter>().MaxMeter==1,"Shared meter starts at one full bar");
        var catalog=Resources.Load<MultiplayerCatalog>("MultiplayerCatalog");
        Check(catalog.characters.Contains(character) && catalog.characters.Contains(blue),"Both characters are selectable in multiplayer catalog");
        Check(own.All(a=>catalog.AttackId(a)>=0) && catalog.AttackId(character.skill.cast)>=0,"Character attack data is included in network catalog");
    }
    static void Route(AttackData attack,Action input)
    {
        for(int i=0;i<150 && player.CurrentAttack!=attack;i++) { input(); Step(); }
        Check(player.CurrentAttack==attack,"Shared input/combo route enters "+attack.name);
    }
    static void Routes()
    {
        Fixture(); var enemy=Enemy(new Vector2(.65f,0));
        Route(character.groundCombo[0],player.RequestAttack); Route(character.groundCombo[1],player.RequestAttack); Route(character.groundCombo[2],player.RequestAttack);
        Step(70); Check(enemy.health.Current<enemy.health.EffectiveMaximum,"Ground combo lands real hitbox damage");
        Fixture(); enemy=Enemy(new Vector2(.65f,0));
        Route(character.groundCombo[0],player.RequestAttack); Route(character.groundCombo[1],player.RequestAttack); Route(character.launcher,player.RequestLauncher);
        Until(()=>!enemy.motor.IsGrounded); Check(enemy.State==EnemyReaction.Launched || enemy.State==EnemyReaction.AirHit,"Launcher opens shared airborne target reaction");
        player.RequestJump(); Until(()=>!player.motor.IsGrounded); Check(player.motor.Height>0,"Jump buffer executes through shared motor");
        Route(character.airCombo[0],player.RequestAttack); Route(character.airCombo[1],player.RequestAttack); Route(character.airCombo[2],player.RequestAttack);
        Check(player.animationDriver.animator.enabled==false && player.motor.sprite.sprite==player.attackPlayer.Frame.sprite,"Air combat sprites are controlled by frame data, not Animator");
        Fixture(); player.motor.MoveInput=Vector2.right; Step(12); Check(player.motor.transform.position.x>.1f,"Shared movement walks Character 2");
        var meter=player.GetComponent<PlayerMeter>(); meter.TrySpend(1); Enemy(new Vector2(.65f,0)); player.motor.ResetForStage(Vector2.zero);
        Route(character.groundCombo[0],player.RequestAttack); Step(30); Check(meter.CurrentMeter>0,"Accepted normal hits refill shared meter"); meter.Add(100); Check(meter.CurrentMeter==1,"Meter clamps to one bar");
    }
    static AttackHitboxData Incoming(HitType type=HitType.Normal) => new AttackHitboxData{damage=10,hitstopFrames=0,hitstunFrames=5,hitType=type,canHitGrounded=true,canHitAirborne=true,knockback=0};
    static void Bounces()
    {
        Fixture(); var enemy=Enemy(new Vector2(.65f,0));
        var wall=new GameObject("Character 2 wall bounce fixture"); fixtures.Add(wall);
        wall.transform.position=new Vector3(1.3f,0,0); wall.AddComponent<CombatWall>();
        wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,4);
        var hit=character.groundCombo[2].frames.SelectMany(f=>f.hitboxes).First(h=>h.wallBounce);
        enemy.Receive(hit,1); Until(()=>enemy.WallBouncesUsed==1);
        Check(enemy.motor.VerticalVelocity>0,"Character 2 Punch3 authored hit bounces target off physical wall");
        Fixture(); enemy=Enemy(new Vector2(.65f,0)); enemy.motor.Launch(3,0); Step(2);
        hit=character.airCombo[2].frames.SelectMany(f=>f.hitboxes).First(h=>h.groundBounce);
        enemy.Receive(hit,1); Until(()=>enemy.GroundBouncesUsed==1);
        Check(enemy.motor.VerticalVelocity>0,"Character 2 AirPunch3 authored hit bounces target on floor contact");
    }
    static void BlueSkill()
    {
        Fixture(); var go=Object.Instantiate(blue.prefab); fixtures.Add(go);
        go.GetComponent<PlayerCombatInput>().enabled=false; go.GetComponent<PlayerInput>().enabled=false;
        go.GetComponent<CharacterMotor>().ResetForStage(new Vector2(-8,0));
        var skill=go.GetComponent<PlayerSkillController>();
        Check(skill.equippedSkill==blue.skill && skill.RequestSkill(),"BlueShirtGuy retains original equipped projectile skill");
        Step(100); Check(skill.ProjectilesReleased==1 && skill.AreaReleases==0,"Shared skill extension preserves Shadow Dragon projectile release");
    }
    static void Defense()
    {
        Fixture(); var enemy=Enemy(new Vector2(.7f,0)); var hurt=player.GetComponentInChildren<CombatHurtbox>();
        var meter=player.GetComponent<PlayerMeter>(); meter.TrySpend(1);
        player.motor.Face(1); player.RequestGuard(true);
        Check(hurt.Receive(Incoming(),-1,enemy.motor) && hurt.LastHitOutcome==CombatHitOutcome.Parry,"Character 2 successfully parries through shared defense");
        Check(meter.CurrentMeter>=.4f,"Successful parry grants significant shared meter gain");
        Fixture(); enemy=Enemy(new Vector2(.7f,0)); hurt=player.GetComponentInChildren<CombatHurtbox>();
        player.RequestGuard(true); Step(player.defenseData.parryWindowFrames+1);
        Check(hurt.Receive(Incoming(),-1,enemy.motor) && hurt.LastHitOutcome==CombatHitOutcome.Block,"Held guard blocks after parry window");
        Fixture(); Check(player.RequestDodge(),"Dodge uses existing state machine"); Step(player.defenseData.dodgeInvulnerableFirstFrame);
        Check(player.DodgeInvulnerable && player.motor.sprite.sprite!=blue.idlePose,"Dodge invulnerability uses Character 2 pose");
        Fixture(); player.ReceiveHit(Incoming(HitType.Stun),-1); Check(player.IsStunned,"Shared Stunned state supports Character 2");
        Check(character.defense.stunned.Any(p=>p.sprite==player.motor.sprite.sprite),"Stun displays own reaction art");
        Fixture(); player.ReceiveHit(Incoming(HitType.KnockDown),-1); Check(player.IsKnockdownState,"Shared knockdown starts");
        Step(player.defenseData.knockdownFrames+player.defenseData.downedFrames+player.defenseData.getUpFrames+3);
        Check(!player.IsKnockdownState,"Knockdown and own GetUp poses recover");
        Fixture(); var grabGo=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/GrapplerBruiser.prefab")); fixtures.Add(grabGo);
        grabGo.GetComponent<EnemyCombat>().enabled=false; var grab=grabGo.GetComponent<CombatGrabController>();
        Check(player.TryEnterGrab(grab) && player.IsGrabbed,"Existing Grappler can place Character 2 in Grabbed state");
        Check(character.defense.grabbed.Any(p=>p.sprite==player.motor.sprite.sprite),"Grabbed displays own pose"); player.ReleaseGrab(grab);
        Check(!player.IsGrabbed,"Shared grab release restores control");
    }
    static void Area()
    {
        Fixture(); var skill=player.GetComponent<PlayerSkillController>(); var meter=player.GetComponent<PlayerMeter>();
        var left=Enemy(new Vector2(-.7f,0)); var right=Enemy(new Vector2(.7f,0)); var upper=Enemy(new Vector2(0,.6f)); var invalid=Enemy(new Vector2(0,2));
        Check(skill.RequestSkill() && meter.CurrentMeter==0,"Wand Barrier consumes exactly one bar");
        Check(!skill.RequestSkill(),"Skill cannot restart while casting / without meter");
        Check(player.CurrentAttack==character.skill.cast,"Character-specific skill resolves through equipped data");
        Step(16); Check(skill.GuardiansManifested==1,"Barrier shell appears on zero-based combat frame 16");
        Check(Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None).Any(),"Wand barrier visual is instantiated");
        Step(8); Check(skill.AreaReleases==1,"Barrier pulse releases on zero-based combat frame 24");
        Check(left.health.Current==9968 && right.health.Current==9968 && upper.health.Current==9968,"One activation damages multiple surrounding enemies");
        Check(invalid.health.Current==10000,"Invalid walking depth remains outside area");
        var positions=new[]{left.motor.transform.position,right.motor.transform.position,upper.motor.transform.position};
        Step(10);
        Check(left.motor.transform.position.x<positions[0].x && right.motor.transform.position.x>positions[1].x && upper.motor.transform.position.y>positions[2].y,"Outward recoil pushes left/right/depth targets away independently");
        Step(70); Check(left.health.Current==9968 && right.health.Current==9968 && upper.health.Current==9968,"All active frames share once-per-target accepted-hit history");
        Check(!skill.RequestSkill(),"Insufficient remaining meter blocks second activation");
        Fixture(); player.motor.Face(-1); var target=Enemy(new Vector2(.7f,0)); skill=player.GetComponent<PlayerSkillController>();
        Check(skill.RequestSkill(),"Wand Barrier can cast facing left"); Step(80);
        Check(target.health.Current==9968 && target.motor.transform.position.x>.7f,"Left facing does not reverse outward area knockback");
        Check(skill.ProjectilesReleased==0,"Area skill reuses melee area sampler without spawning a projectile");
    }
    static void BossGate()
    {
        Fixture(); var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/WhiteGhostBoss.prefab")); fixtures.Add(go);
        var boss=go.GetComponent<TotemBossController>(); var ai=go.GetComponent<EnemyCombat>(); ai.enabled=false;
        var data=Object.Instantiate(boss.data); data.warpOutFrames=10000; boss.data=data;
        ai.motor.ResetForStage(new Vector2(.7f,0)); var skill=player.GetComponent<PlayerSkillController>(); float hp=ai.reaction.health.Current;
        Check(skill.RequestSkill(),"Wand Barrier begins near shielded boss"); Step(80);
        Check(ai.reaction.health.Current==hp && boss.Invulnerable,"Wand Barrier respects physical Totem vulnerability gate");
        boss.DebugVulnerable(); player.GetComponent<PlayerMeter>().Add(1); player.ResetCombo();
        Check(skill.RequestSkill(),"Wand Barrier begins during boss vulnerability"); Step(80);
        Check(ai.reaction.health.Current==hp-32,"Vulnerable boss accepts ordinary area damage once");
        Object.DestroyImmediate(data);
    }
}



