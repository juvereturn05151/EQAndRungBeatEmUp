using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyAIValidation
{
    const string Pending="BeatEmUp.EnemyAIValidation";
    static readonly List<string> results=new List<string>();
    static GameObject player,enemy;
    static EnemyCombat brain;
    static EnemyAIProfile profile;
    static CombatClock clock;
    static EnemyAIValidation() { EditorApplication.update+=Poll; }
    [MenuItem("Beat Em Up/Enemies/Validate AI profiles (Play Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition,string message) { if(!condition) throw new Exception(message); results.Add("PASS: "+message); }
    static void Step(int frames) { for(int i=0;i<frames;i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    static void Clear() { if(enemy) UnityEngine.Object.DestroyImmediate(enemy); if(player) UnityEngine.Object.DestroyImmediate(player); if(profile) UnityEngine.Object.DestroyImmediate(profile); }
    static void Fixture(string type="Rusher",float x=2,float lane=0)
    {
        Clear();
        player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath));
        player.GetComponent<PlayerCombatInput>().enabled=false; player.GetComponent<CharacterHealth>().maximumHealth=2000; player.GetComponent<CharacterHealth>().Restore();
        player.GetComponent<CharacterMotor>().ResetForStage(new Vector2(x,lane));
        enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ThrowerProjectileSetup.Root+"Prefabs/"+type+".prefab"));
        brain=enemy.GetComponent<EnemyCombat>(); brain.motor.ResetForStage(Vector2.zero); brain.target=player.transform;
        profile=UnityEngine.Object.Instantiate(brain.aiProfile); brain.aiProfile=profile; profile.reactionDelay=0; brain.RefreshAI();
    }
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); bool passed=false;
        try
        {
            foreach(var flow in UnityEngine.Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) flow.enabled=false;
            foreach(var motor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) motor.gameObject.SetActive(false);
            foreach(var framing in UnityEngine.Object.FindObjectsByType<StageFraming>(FindObjectsSortMode.None)) framing.enabled=false;
            clock=UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled=false;
            foreach(var path in AssetDatabase.FindAssets("t:EnemyAIProfile",new[]{EnemyAISetup.Root.TrimEnd('/')}))
            {
                var asset=AssetDatabase.LoadAssetAtPath<EnemyAIProfile>(AssetDatabase.GUIDToAssetPath(path));
                Check(!asset.Validate().Any(),asset.name+" has valid states, destinations and attack references");
                var editor=Editor.CreateEditor(asset); Check(editor is EnemyAIProfileEditor,"AI profile opens in the editable custom inspector"); UnityEngine.Object.DestroyImmediate(editor);
            }
            Fixture(); profile.aggroRange=.5f; profile.loseTargetRange=.6f; brain.RefreshAI(); Step(2);
            Check(brain.target==null && brain.motor.MoveInput==Vector2.zero,"Small aggro range prevents detection and movement");
            profile.aggroRange=8; profile.loseTargetRange=10; Step(3);
            Check(brain.target==player.transform && brain.AI.StateName=="Approach" && brain.motor.MoveInput.x>0,"Edited aggro range acquires target and profile drives approach");
            Fixture(x:.7f,lane:.9f); Step(3); Check(brain.AI.StateName=="LaneAlign" && brain.motor.MoveInput.y>0,"Profile uses lane-align state before attacking");
            Fixture(x:.65f); profile.attacks[0].weight=1; profile.attacks[1].weight=0; Step(3);
            Check(brain.attackPlayer.CurrentAttack==profile.attacks[0].attack,"Weighted selection excludes zero-weight sprint and uses slash combo");
            Check(brain.AI.Selected.id=="SlashCombo","Runtime debug exposes chosen attack ID");
            player.GetComponent<CharacterMotor>().ResetForStage(new Vector2(-.65f,0)); Step(1);
            Check(brain.motor.Facing==1 && brain.attackPlayer.Facing==1,"Committed attack preserves facing when the target crosses behind");
            player.GetComponent<CharacterMotor>().ResetForStage(new Vector2(.65f,0));
            Step(profile.attacks[0].attack.TotalFrames+20);
            Check(brain.AI.Cooldown("SlashCombo")>0,"Finishing AttackData starts per-choice cooldown");
            Check(brain.AI.StateName!="Attack" || !brain.attackPlayer.CurrentAttack,"Cooldown prevents immediate repeated attack");
            Check(!brain.AI.ForceState("missing"),"Missing forced state is rejected safely");
            Fixture(x:.65f); profile.attacks[0].weight=0; profile.attacks[1].weight=1; Step(3);
            Check(brain.attackPlayer.CurrentAttack==profile.attacks[1].attack,"Rusher can select sprint as a distinct AttackData choice");
            Fixture(x:.65f); profile.attacks.ForEach(a=>a.cooldown=5); Step(3); var selected=brain.AI.Selected; int completionGuard=0; while(brain.attackPlayer.CurrentAttack && completionGuard++<500) Step(1);
            Check(brain.AI.Cooldown(selected.id)>=5,"Edited cooldown is honored");
            Fixture(); profile.defaultState="missing"; brain.RefreshAI(); Step(3); Check(brain.motor.MoveInput==Vector2.zero,"Missing default state idles safely and validates"); Check(profile.Validate().Any(),"Invalid default produces validation warning");
            Fixture(); profile.states.Add(new EnemyAIState{id="CustomWait",action=EnemyAIAction.Wait,duration=1}); profile.defaultState="CustomWait"; brain.RefreshAI(); Step(10);
            Check(brain.AI.StateName=="CustomWait" && brain.motor.MoveInput==Vector2.zero,"Duplicated profile can add a new behavior without code changes");
            profile.states.Add(new EnemyAIState{id="Look",action=EnemyAIAction.FaceTarget,faceTarget=false}); profile.defaultState="Look";
            player.GetComponent<CharacterMotor>().ResetForStage(new Vector2(-2,0)); brain.RefreshAI(); Step(1);
            Check(brain.motor.Facing==-1 && brain.motor.MoveInput==Vector2.zero,"Explicit FaceTarget action works without movement or automatic facing");
            Fixture(x:.65f); Step(3); brain.reaction.Receive(new AttackHitboxData{damage=1,hitstunFrames=30,knockback=0},1); Step(1);
            Check(brain.AI.StateName=="Hurt" && !brain.attackPlayer.CurrentAttack,"Hurt interrupts profile actions through the existing reaction system");
            Check(!brain.AI.ForceState("Attack"),"Force-state cannot bypass hurt safety"); Step(35); Check(brain.reaction.CanAct,"Existing reaction recovery returns control to AI");
            brain.reaction.health.Damage(10000); Step(1); Check(brain.AI.StateName=="Dead" && brain.motor.MoveInput==Vector2.zero,"Dead enemies cannot move or attack");
            Fixture("Thrower",2); Step(3); Check(brain.AI.Selected!=null && brain.AI.Selected.projectile,"Thrower selects projectile through its profile");
            float hp=player.GetComponent<CharacterHealth>().Current; Step(180);
            Check(brain.GetComponent<EnemyProjectileAttack>().ProjectilesReleased>0,"ThrowProjectile frame event releases a real projectile");
            Check(player.GetComponent<CharacterHealth>().Current<hp,"Profile-driven projectile applies host combat damage");
            Fixture("Thrower",.7f); Step(3); Check(brain.AI.StateName=="Retreat" && brain.motor.MoveInput.x<0,"Thrower retreats when target is too close");
            Fixture(); brain.aiProfile=null; brain.attackRange=.1f; brain.CombatFrame(); Check(brain.motor.MoveInput.x>0,"Unassigned enemies retain legacy movement behavior");
            passed=true;
        }
        catch(Exception exception) { results.Add("FAIL: "+exception); Debug.LogException(exception); }
        finally { Clear(); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/EnemyAIValidationResults.txt",results); }
        Debug.Log("ENEMY AI VALIDATION "+(passed ? "PASSED" : "FAILED")+": "+results.Count);
        if(Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.ExitPlaymode();
    }
}
