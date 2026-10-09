using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class BlackDragonFistValidation
{
    const string Pending="BlackDragonFist.Validation";
    static readonly List<string> results=new List<string>();
    static GameObject root;static ComboController player;static EnemyHitReaction enemy;static CombatClock clock;static StageFraming camera;
    static BlackDragonFistValidation(){EditorApplication.update+=Poll;}
    [MenuItem("Beat Em Up/Skills/Validate Black Dragon Fist impact (Play Mode)")]
    public static void BuildAndValidate()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        BlackDragonFistSetup.Build();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);results.Add("PASS: "+message);}
    static void Step(int count=1){for(int i=0;i<count;i++){Physics2D.SyncTransforms();clock.StepFrame();camera.ApplyFraming(CombatClock.FrameSeconds);}}
    static void Fixture(string name,int facing=1)
    {
        if(root)Object.DestroyImmediate(root);
        foreach(var visual in Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None))Object.DestroyImmediate(visual.gameObject);
        foreach(var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))if(source.gameObject.name.Contains("combat sound"))Object.DestroyImmediate(source.gameObject);
        root=new GameObject("Black Dragon Fist validation");
        var view=new GameObject("Camera");view.transform.SetParent(root.transform);view.AddComponent<Camera>();camera=view.AddComponent<StageFraming>();camera.followEnabled=false;camera.ApplyFraming(0,true);
        player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab"),root.transform).GetComponent<ComboController>();
        player.GetComponent<PlayerCombatInput>().enabled=false;player.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled=false;
        enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/"+name+".prefab"),root.transform).GetComponent<EnemyHitReaction>();
        foreach(var component in enemy.GetComponents<MonoBehaviour>())
            if(!(component is EnemyHitReaction||component is CharacterHealth||component is CharacterMotor||component is AttackPlayer||component is AttackHitbox||component is CharacterAnimation||component is HitCountArmor||component is HitBlinkEffect))component.enabled=false;
        player.motor.arenaMin=enemy.motor.arenaMin=new Vector2(-20,-2);player.motor.arenaMax=enemy.motor.arenaMax=new Vector2(20,2);
        player.motor.ResetForStage(Vector2.zero);player.motor.Face(facing);enemy.motor.ResetForStage(Vector2.right*2*facing);enemy.motor.Face(-facing);
        // Recovery checks require a surviving target; authored HP/damage assets remain untouched.
        enemy.health.maximumHealth=Mathf.Max(200,enemy.health.maximumHealth);
        player.health.Restore();enemy.health.Restore();clock=Object.FindFirstObjectByType<CombatClock>();clock.enabled=false;clock.combatFPS=60;
        Physics2D.SyncTransforms();
    }
    static CombatHurtbox Hurtbox=>enemy.GetComponentInChildren<CombatHurtbox>();
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        SessionState.SetBool(Pending,false);results.Clear();bool pass=false;
        try
        {
            foreach(var name in new[]{"Rusher","Thrower","Screamer","Ambusher","Prefect"})ImpactAndRecovery(name,1);
            ImpactAndRecovery("Rusher",-1);Resistance();OverridesAndNormals();pass=true;
        }
        catch(Exception e){results.Add("FAIL: "+e);Debug.LogException(e);}
        finally
        {
            if(root)Object.DestroyImmediate(root);Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/BlackDragonFistValidationResults.txt",results);
            Debug.Log("BLACK DRAGON FIST VALIDATION "+(pass?"PASSED":"FAILED"));if(Application.isBatchMode)EditorApplication.Exit(pass?0:1);else EditorApplication.ExitPlaymode();
        }
    }
    static void ImpactAndRecovery(string name,int facing)
    {
        Fixture(name,facing);var skill=player.GetComponent<PlayerSkillController>();
        Check(skill.RequestSkill(),name+" accepts Character 1 skill cast");
        for(int frame=0;frame<12;frame++)
        {
            if(skill.ProjectilesReleased!=0||camera.ImpactShakeCount!=0||player.attackPlayer.HitstopRemaining!=0)throw new Exception("Impact triggered during startup");Step();
        }
        Check(skill.ProjectilesReleased==1&&player.attackPlayer.CurrentFrame==12,"Original release frame 12 remains unchanged");
        var shot=skill.LastProjectile;
        Check(camera.ImpactShakeCount==0&&!Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(s=>s.clip==skill.equippedSkill.cast.feedback.impactSound),"Release/miss has no heavy impact shake or sound");
        for(int f=0;f<60&&shot.AcceptedHits==0;f++)Step();
        Check(shot.AcceptedHits==1&&Hurtbox.LastHitOutcome==CombatHitOutcome.Hit,name+" receives one real projectile collision, facing "+facing);
        Check((bool)typeof(HitBlinkEffect).GetField("blinking",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(enemy.GetComponent<HitBlinkEffect>()),"Existing hit blink starts on confirmed damage");
        Check(enemy.State==EnemyReaction.Falling&&!enemy.CanAct&&!enemy.JuggleOpen,name+" enters committed falling reaction with no juggle window");
        Check(player.attackPlayer.HitstopRemaining==11&&enemy.motor.attackPlayer.HitstopRemaining==11,"Attacker and target freeze for 11 frames exactly on collision");
        Check(camera.ImpactShakeCount==1&&camera.ImpactShakeActive,"One small camera impulse begins on accepted impact");
        var feedback=skill.equippedSkill.cast.feedback;
        Check(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(s=>s.clip==feedback.impactSound&&Mathf.Abs(s.volume-.9f)<.001f),"Assigned heavy finisher sound plays at impact volume");
        var impact=Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None).Single();
        Check(impact.GetComponentsInChildren<ParticleSystem>().Length>0&&Mathf.Abs(impact.transform.GetChild(0).localScale.x-.42f)<.001f,"Existing impact VFX spawns at stronger skill-specific scale");
        var position=enemy.transform.position;float height=enemy.motor.Height;var pose=enemy.motor.sprite.sprite;
        Step(11);Check(enemy.transform.position==position&&enemy.motor.Height==height&&enemy.motor.sprite.sprite==pose,"Impact freeze preserves target position, height and fall pose for all 11 frames");
        Step();Check((enemy.transform.position.x-position.x)*facing>0&&enemy.motor.Height>height,"Short lift and strong backward push begin after impact freeze");
        for(int f=0;f<120&&enemy.State!=EnemyReaction.Knockdown;f++)Step();
        Check(enemy.State==EnemyReaction.Knockdown&&enemy.motor.IsGrounded&&enemy.IsRecovering,"Landing enters existing Knockdown animation");
        Check((enemy.transform.position.x-position.x)*facing>.6f,"Enemy receives noticeable backward displacement without leaving lane");
        Step(enemy.KnockdownFrames);Check(enemy.State==EnemyReaction.Downed&&enemy.PhaseFramesRemaining==enemy.knockdownRecoveryDelayFrames,"Knockdown finishes into the enemy's normal configurable downed duration");
        var down=enemy.motor.sprite.sprite;Check(down==enemy.downedSprite,"Existing lying-down sprite is held on the floor");
        Step(enemy.knockdownRecoveryDelayFrames-1);Check(enemy.State==EnemyReaction.Downed&&!enemy.CanAct,"Enemy cannot stand or act before normal downed hold finishes");
        Step();Check(enemy.State==EnemyReaction.GetUp&&enemy.PhaseFramesRemaining==enemy.GetUpFrames,"Existing GetUp clip begins at the normal recovery boundary");
        Step(enemy.GetUpFrames);Check(enemy.State==EnemyReaction.Normal&&enemy.CanAct,"Enemy returns to normal only after existing GetUp completes");
        Check(shot.AcceptedHits==1&&camera.ImpactShakeCount==1,"One projectile never repeats damage, knockdown or impact feedback on same target");
        Check(!camera.ImpactShakeActive&&Mathf.Abs(camera.transform.position.x)<.0001f&&Mathf.Abs(camera.transform.position.y-camera.verticalCenter)<.0001f,"Shake expires without camera drift or vertical composition changes");
    }
    static void Resistance()
    {
        Fixture("GrapplerBruiser");var hit=player.GetComponent<PlayerSkillController>().equippedSkill.projectilePrefab.hit.RuntimeCopy();
        Check(Hurtbox.Receive(hit,1,player.motor)&&Hurtbox.LastHitOutcome==CombatHitOutcome.Armor,"Existing Grappler Bruiser armor absorbs skill reaction");
        Check(!enemy.IsRecovering&&enemy.motor.IsGrounded&&Hurtbox.LastHitstopFrames==enemy.GetComponent<HitCountArmor>().hitstopFrames,"Armor retains its own reaction and hitstop rules");
        Fixture("WhiteGhostBoss");var hp=enemy.health.Current;Hurtbox.externalInvulnerable=true;
        Check(!Hurtbox.Receive(hit,1,player.motor)&&enemy.health.Current==hp,"Existing boss shield rejects damage and knockdown");
        Hurtbox.externalInvulnerable=false;enemy.health.BossDamageProtection=false;
        Check(Hurtbox.Receive(hit,1,player.motor)&&enemy.State==EnemyReaction.GroundHit&&enemy.motor.IsGrounded,"Vulnerable boss keeps previous short hit reaction instead of forced knockdown");
        Fixture("Rusher");var armorCast=ScriptableObject.CreateInstance<AttackData>();armorCast.frames.Add(new AttackFrameData{sprite=enemy.motor.sprite.sprite,superArmor=true});enemy.motor.attackPlayer.Play(armorCast);
        Check(Hurtbox.Receive(hit,1,player.motor)&&enemy.State==EnemyReaction.Normal&&enemy.motor.IsGrounded,"Existing super-armor frame suppresses knockdown");enemy.motor.attackPlayer.Stop();Object.DestroyImmediate(armorCast);
    }
    static void OverridesAndNormals()
    {
        Fixture("Rusher");var hit=player.GetComponent<PlayerSkillController>().equippedSkill.projectilePrefab.hit.RuntimeCopy();hit.hitstopFrames=0;hit.knockdownDurationFrames=12;
        Hurtbox.Receive(hit,1,player.motor);for(int f=0;f<120&&enemy.State!=EnemyReaction.Downed;f++)Step();
        Check(enemy.PhaseFramesRemaining==12,"Existing hit-data knockdown duration supports an optional per-hit override");
        hit.hitType=HitType.Normal;Hurtbox.Receive(hit,1,player.motor);Check(enemy.State==EnemyReaction.Downed&&enemy.PhaseFramesRemaining==12,"Follow-up damage cannot replace downed recovery or instantly stand enemy");
        Step(12+enemy.GetUpFrames);Check(enemy.CanAct,"Override still uses normal GetUp behavior");
        Fixture("Rusher");enemy.motor.Launch(5,0);hit=player.GetComponent<PlayerSkillController>().equippedSkill.projectilePrefab.hit.RuntimeCopy();hit.hitstopFrames=0;Hurtbox.Receive(hit,1,player.motor);
        Check(enemy.State==EnemyReaction.Falling&&enemy.motor.VerticalVelocity<0,"Already-airborne enemy is driven downward into existing landing recovery");
        Fixture("Rusher");var punch=player.groundCombo[0];var normal=punch.frames.First(f=>f.hitboxes.Count>0).hitboxes[0];Hurtbox.Receive(normal,1,player.motor);
        Check(enemy.State==EnemyReaction.GroundHit&&enemy.motor.IsGrounded&&!enemy.IsRecovering,"Normal Punch1 retains ordinary grounded hit reaction");
        Check(normal.hitstopFrames==3&&punch.feedback.impactShakeStrength==0&&punch.feedback.impactShakeDuration==0,"Normal punch keeps low hitstop and no camera shake");
        Check(player.GetComponent<PlayerSkillController>().equippedSkill.projectilePrefab.hit.hitstopFrames>normal.hitstopFrames&&player.GetComponent<PlayerSkillController>().equippedSkill.cast.feedback.impactScale>punch.feedback.impactScale*2,"Skill impact clearly exceeds normal punch's hitstop and VFX scale");
        Fixture("Thrower");enemy.health.maximumHealth=25;enemy.health.Restore();
        hit=player.GetComponent<PlayerSkillController>().equippedSkill.projectilePrefab.hit.RuntimeCopy();Hurtbox.Receive(hit,1,player.motor);Step(120);
        Check(enemy.health.IsDead&&enemy.State==EnemyReaction.Defeated&&!enemy.CanAct,"Lethal skill damage retains existing defeat flow and never makes dead enemies GetUp");
        for(int i=0;i<3;i++)
        {
            Fixture("Rusher");var attack=player.groundCombo[i];
            var authoredHit=attack.frames.First(f=>f.hitboxes.Count>0).hitboxes[0];Hurtbox.Receive(authoredHit,1,player.motor);
            var expected=authoredHit.wallBounce?EnemyReaction.WallBounceEligible:EnemyReaction.GroundHit;
            Check(enemy.State==expected&&enemy.motor.IsGrounded&&attack.feedback.impactShakeStrength==0,"Punch "+(i+1)+" retains its authored reaction (including existing wall bounce) and no camera shake");
        }
        Fixture("Rusher");Hurtbox.Receive(player.launcher.frames.First(f=>f.hitboxes.Count>0).hitboxes[0],1,player.motor);
        Check(enemy.State==EnemyReaction.Launched&&enemy.JuggleOpen,"Existing launcher still opens its normal airborne juggle");
        camera.AddImpactShake(.05f,.14f);camera.ApplyFraming(0,true);
        Check(!camera.ImpactShakeActive&&Mathf.Abs(camera.transform.position.x)<.0001f,"Framing reset cancels shake without retaining its offset");
        camera.AddImpactShake(.05f,.14f);camera.transform.position=new Vector3(2,camera.verticalCenter,-10);camera.ApplyFraming(0,true);
        Check(!camera.ImpactShakeActive&&Mathf.Abs(camera.transform.position.x-2)<.0001f,"Stage-entry camera teleport is preserved when an impact shake resets");
    }
}
