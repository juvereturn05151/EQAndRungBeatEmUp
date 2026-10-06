using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ThrowerProjectileValidation
{
    const string Pending = "BeatEmUp.ThrowerProjectileValidation";
    const string PlayerPath = "Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab";
    static readonly List<string> results = new List<string>();
    static GameObject player, thrower;
    static CombatClock clock;
    static ThrowerProjectileValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate Thrower and corridor (Play Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string label) { if(!ok) throw new Exception(label); results.Add("PASS: "+label); }
    static void Step(int frames) { for(int i=0;i<frames;i++) clock.StepFrame(); }
    static CombatProjectile[] Shots() => UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None);
    static void Clear()
    {
        if(player) UnityEngine.Object.DestroyImmediate(player);
        if(thrower) UnityEngine.Object.DestroyImmediate(thrower);
        foreach(var p in Shots()) UnityEngine.Object.DestroyImmediate(p.gameObject);
    }
    static EnemyCombat Fixture(int facing=1, float distance=3, float lane=.2f)
    {
        Clear();
        player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        thrower=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ThrowerProjectileSetup.ThrowerPath));
        player.GetComponent<PlayerCombatInput>().enabled=false;
        player.GetComponent<CharacterMotor>().ResetForStage(new Vector2(facing*distance,lane));
        var health=player.GetComponent<CharacterHealth>(); health.maximumHealth=500; health.Restore();
        var brain=thrower.GetComponent<EnemyCombat>(); brain.aiProfile=null; // Preserve this suite's exact legacy windup timing; EnemyAIValidation covers profile decisions.
        brain.motor.ResetForStage(Vector2.zero); brain.target=player.transform;
        return brain;
    }
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); bool passed=false;
        try
        {
            clock=UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled=false;
            var flow=UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); flow.enabled=false;
            foreach(var actor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            var attack=AssetDatabase.LoadAssetAtPath<AttackData>(ThrowerProjectileSetup.AttackPath);
            Check(attack.TotalFrames==40 && attack.frames.All(f=>f.hitboxes.Count==0),"Existing 40-frame throw drawings retained; fake melee hitboxes removed");
            Check(attack.frames[18].sprite.name.EndsWith("_04") && attack.frames.SelectMany(f=>f.events).Count(e=>e=="ThrowProjectile")==1 && attack.frames[18].events.Contains("ThrowProjectile"),"One release event on extended-arm drawing at frame 18");
            foreach(int facing in new[]{1,-1})
            {
                var brain=Fixture(facing); var ranged=thrower.GetComponent<EnemyProjectileAttack>();
                Step(1); Check(brain.attackPlayer.CurrentAttack==attack,"AI begins ranged windup at useful range, facing "+facing);
                Step(17); Check(ranged.ProjectilesReleased==0,"No projectile during 18-frame anticipation");
                Step(1); var shot=Shots().Single();
                Check(ranged.ProjectilesReleased==1 && Mathf.Sign(shot.Velocity.x)==facing && shot.Velocity.y>0,"Release aims toward player X and walking lane");
                Check(Mathf.Abs(shot.transform.position.x-facing*ranged.releaseOffset.x)<.001f,"Shot appears at authored hand offset on release frame");
                Step(1);
                Check(Mathf.Abs(shot.transform.position.x-(facing*ranged.releaseOffset.x+shot.Velocity.x/60))<.001f,"Projectile movement begins on next logical combat tick");
                for(int i=0;i<70 && player.GetComponent<CharacterHealth>().Current==500;i++) Step(1);
                Check(player.GetComponent<CharacterHealth>().Current==496 && player.GetComponent<ComboController>().State==CombatState.Hitstun,"Notebook causes existing damage and player hit reaction");
                Check(shot.Resolved && !shot.gameObject.activeSelf && player.GetComponent<AttackPlayer>().HitstopRemaining==3,"Hit consumes projectile once and applies existing hitstop");
                Step(20); Check(player.GetComponent<CharacterHealth>().Current==496 && ranged.ProjectilesReleased==1,"No repeated collision damage or per-frame throwing");
                Step(80); Check(ranged.ProjectilesReleased==1,"Authored throw cooldown remains active");
                Step(100); Check(ranged.ProjectilesReleased>=2 && ranged.ProjectilesReleased<=3,"Thrower throws again after cooldown");
            }
            var outOfRange=Fixture(1,8,0); Step(1);
            Check(!outOfRange.attackPlayer.CurrentAttack && outOfRange.motor.MoveInput.x>0,"Beyond maximum range, Thrower approaches rather than throws");
            var wrongLane=Fixture(1,3,1); Step(1);
            Check(!wrongLane.attackPlayer.CurrentAttack && wrongLane.motor.MoveInput.y>0,"Thrower aligns walking lane before attack");
            var close=Fixture(1,.5f,0); Step(1);
            Check(!close.attackPlayer.CurrentAttack && close.motor.MoveInput.x<0,"Thrower backs away from point-blank range");
            var interrupted=Fixture(); Step(12);
            interrupted.reaction.Receive(new AttackHitboxData{damage=0,hitstunFrames=60},1); Step(20);
            Check(thrower.GetComponent<EnemyProjectileAttack>().ProjectilesReleased==0,"Interrupted windup cannot release a delayed projectile");
            var paused=Fixture(); Step(18); CombatClock.SetPaused(player,true); Step(30); CombatClock.SetPaused(player,false);
            Check(thrower.GetComponent<EnemyProjectileAttack>().ProjectilesReleased==0,"Paused combat does not advance release event");
            Step(1); var timeout=Shots().Single(); paused.enabled=false;
            player.transform.position=new Vector3(4,1.5f,0); Step(130);
            Check(timeout.Resolved && !timeout.gameObject.activeSelf,"Missed notebook despawns after combat-frame lifetime");
            var walls=Fixture(); var wall=new GameObject("Projectile test wall"); var collider=wall.AddComponent<BoxCollider2D>();
            wall.transform.position=new Vector3(1.5f,.75f,0); collider.size=new Vector2(.1f,2);
            try
            {
                Step(19); var wallShot=Shots().Single(); walls.enabled=false; Step(20);
                Check(wallShot.Resolved && player.GetComponent<CharacterHealth>().Current==500,"Swept collision stops notebook at solid wall");
            }
            finally { UnityEngine.Object.DestroyImmediate(wall); }
            var guarded=Fixture(); var combo=player.GetComponent<ComboController>(); combo.motor.Face(-1); combo.RequestGuard(true);
            Step(70); Check(combo.health.Current==500 && player.GetComponentInChildren<CombatHurtbox>().LastHitOutcome==CombatHitOutcome.Block,"Projectile uses existing guard instead of bypassing defense");
            Clear();
            // Exercise the authored PlayerZone encounter instead of rebuilding stage data.
            int index=flow.level.stages.FindIndex(s=>s.stageId.StartsWith("Stage02_BloodSheet",StringComparison.Ordinal));
            var stage=flow.level.stages[index]; flow.player.gameObject.SetActive(true); flow.player.GetComponent<PlayerCombatInput>().enabled=false;
            flow.EnterStage(index);
            Check(flow.background.sprite.name.EndsWith("_v2") && Mathf.Abs(flow.background.bounds.size.y-3.15f)<.001f && Mathf.Abs(flow.background.bounds.min.y-.86f)<.001f,"BloodSheetCorridor loads taller v2 art while preserving original world seam");
            Check(Mathf.Abs(flow.background.bounds.size.x-11.33f)<.001f && Mathf.Abs(flow.floor.bounds.size.y-1.8379375f)<.001f,"Background width and floor layout remain unchanged");
            var encounter=stage.encounters.First(e=>e.waves.Any(w=>w.enemySpawns.Any(s=>s.prefab && s.prefab.GetComponent<EnemyProjectileAttack>())));
            flow.player.ResetForStage(encounter.triggerZone.center); flow.player.GetComponent<CharacterHealth>().Restore();
            EnemyProjectileAttack actual=null;
            for(int i=0;i<40;i++)
            {
                flow.Tick(CombatClock.FrameSeconds);
                foreach(var e in flow.LivingEnemies.ToArray()) if(!e.GetComponent<EnemyProjectileAttack>()) e.gameObject.SetActive(false);
                actual=flow.LivingEnemies.Select(e=>e.GetComponent<EnemyProjectileAttack>()).FirstOrDefault(e=>e);
                Step(1);
            }
            Check(actual && actual.ProjectilesReleased>=1,"Entering authored corridor encounter spawns a functioning ranged Thrower");
            float before=flow.player.GetComponent<CharacterHealth>().Current;
            Step(80); Check(flow.player.GetComponent<CharacterHealth>().Current<before,"Real BloodSheetCorridor Thrower projectile hits assigned stage player");
            var remaining=Shots().FirstOrDefault();
            if(!remaining) { actual.GetComponent<AttackPlayer>().Stop(); actual.GetComponent<AttackPlayer>().Play(attack); Step(18); remaining=Shots().FirstOrDefault(); }
            Check(remaining && remaining.transform.parent==actual.transform.parent,"Stage room owns projectile independently of Thrower");
            flow.EnterStage(0);
            Check(!remaining.gameObject.activeInHierarchy,"Stage replacement disables old room and its projectiles");
            passed=true;
        }
        catch(Exception error) { results.Add("FAIL: "+error); Debug.LogException(error); }
        finally
        {
            Clear(); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/ThrowerProjectileValidationResults.txt",results);
            Debug.Log("THROWER VALIDATION "+(passed?"PASSED":"FAILED")+": "+results.Count+" checks");
            if(Application.isBatchMode) EditorApplication.Exit(passed?0:1); else EditorApplication.ExitPlaymode();
        }
    }
}
