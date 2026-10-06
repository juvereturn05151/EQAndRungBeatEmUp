using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ThrowerGroundPlaneValidation
{
    const string Pending="Thrower.GroundPlaneValidation";
    static readonly List<string> results=new List<string>();
    static readonly List<Object> fixtures=new List<Object>();
    static CombatClock clock; static ComboController player; static EnemyCombat enemy; static EnemyAttackCoordinator coordinator;
    static EnemyProjectileAttack Launcher=>enemy.GetComponent<EnemyProjectileAttack>();
    static ThrowerGroundPlaneValidation()=>EditorApplication.update+=Poll;
    [MenuItem("Beat Em Up/Enemies/Validate Thrower ground-plane flight (Play Mode)")]
    public static void Run()
    { if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode(); }
    static T Keep<T>(T value) where T:Object { fixtures.Add(value); return value; }
    static void Check(bool condition,string description) { if(!condition) throw new Exception(description); results.Add("PASS: "+description); }
    static void Tick(int frames=1) { for(int i=0;i<frames;i++) clock.StepFrame(); }
    static void Until(Func<bool> condition,string description,int limit=240)
    { int count=0; while(!condition() && count++<limit) Tick(); Check(condition(),description); }
    static void Clear()
    { foreach(var p in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(p.gameObject); foreach(var value in fixtures) if(value) Object.DestroyImmediate(value); fixtures.Clear(); }
    static void Fixture(string kind="Thrower",float x=3,float lane=0)
    {
        Clear();
        player=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath))).GetComponent<ComboController>();
        player.GetComponent<PlayerCombatInput>().enabled=false; player.health.SafeStageProtection=false; player.health.Restore(); player.motor.ResetForStage(new Vector2(x,lane));
        enemy=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AttackCoordinationSetup.Prefabs+kind+".prefab"))).GetComponent<EnemyCombat>();
        enemy.motor.ResetForStage(Vector2.zero); enemy.target=player.transform; enemy.passiveTrainingDummy=false;
        var profile=Keep(Object.Instantiate(enemy.aiProfile)); profile.reactionDelay=0; profile.attacks.ForEach(a=>a.cooldown=10); enemy.aiProfile=profile; enemy.RefreshAI();
        coordinator=Keep(new GameObject("Ground flight coordination fixture")).AddComponent<EnemyAttackCoordinator>(); enemy.coordinator=coordinator;
    }
    static void Airborne(float upward=7.5f) { player.RequestJump(); player.motor.Launch(upward,0); player.motor.SuspendFalling=true; }
    static CombatProjectile Fire()
    { Until(()=>Launcher.ProjectilesReleased==1,"Existing frame event releases projectile against current target"); return Launcher.LastProjectile; }
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); bool passed=false;
        try
        {
            foreach(var f in Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) { f.enabled=false; if(f.GetComponent<PlayerHubController>()) f.GetComponent<PlayerHubController>().enabled=false; }
            foreach(var m in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) m.gameObject.SetActive(false);
            foreach(var w in Object.FindObjectsByType<CombatWall>(FindObjectsSortMode.None)) w.gameObject.SetActive(false);
            clock=Object.FindFirstObjectByType<CombatClock>(); clock.enabled=false;
            var book=AssetDatabase.LoadAssetAtPath<GameObject>(ThrowerProjectileSetup.ProjectilePath).GetComponent<CombatProjectile>();
            Check(book.groundPlaneFlight && book.hit.canHitGrounded && !book.hit.canHitAirborne,"Notebook is fixed-height flight with grounded-only hit permission");
            foreach(bool airborne in new[]{false,true})
            {
                Fixture(); if(airborne) Airborne();
                Until(()=>enemy.attackPlayer.CurrentAttack,"Thrower starts coordinated telegraph with "+(airborne ? "airborne" : "grounded")+" target");
                Check(coordinator.Owns(enemy) && coordinator.Occupied(AttackTokenCategory.Ranged)==1,"Book telegraph holds its ordinary Ranged token");
                var shot=Fire(); float height=shot.FlightHeight; Vector2 velocity=shot.Velocity; float hp=player.health.Current;
                Vector2 aim=((Vector2)player.motor.transform.position-((Vector2)enemy.motor.transform.position+Vector2.right*Launcher.releaseOffset.x*enemy.attackPlayer.Facing)).normalized*shot.speed;
                Check((velocity-aim).sqrMagnitude<.000001f,"Book trajectory uses walking-lane coordinates only");
                bool fixedHeight=true;
                for(int i=0;i<80;i++) { Tick(); if(shot && !shot.Resolved) fixedHeight &= Mathf.Abs(shot.FlightHeight-height)<.0001f; }
                Check(fixedHeight,"Book keeps constant flight height throughout travel");
                Check(airborne ? !player.motor.IsGrounded && player.health.Current==hp : player.health.Current<hp,airborne ? "Book passes beneath airborne player without damage" : "Book hits grounded player normally");
                Check(!coordinator.Owns(enemy),"Ranged reservation releases after existing throw/recovery");
            }
            Fixture(); Until(()=>enemy.attackPlayer.CurrentAttack,"Grounded target begins windup"); var attack=enemy.attackPlayer.CurrentAttack;
            Airborne(); Tick(3); Check(enemy.attackPlayer.CurrentAttack==attack && coordinator.Owns(enemy),"Jump during telegraph neither cancels throw nor releases Ranged early");
            var jumpingShot=Fire(); Check(jumpingShot.groundPlaneFlight && Mathf.Abs(jumpingShot.FlightHeight-Launcher.releaseOffset.y)<.0001f,"Book still spawns at unchanged release height after telegraph jump");
            Fixture(); Airborne(); var landingShot=Fire(); Tick(3); player.ResetCombo(); player.motor.SnapGrabToGround(new Vector2(3,0)); float landingHp=player.health.Current;
            Until(()=>player.health.Current<landingHp,"Landing before arrival allows normal book collision and damage");
            Fixture(lane:.1f); Airborne(); Until(()=>enemy.attackPlayer.CurrentAttack,"Airborne depth-target begins throw");
            player.transform.position=new Vector2(3,.2f); var depthShot=Fire();
            Vector2 expected=((Vector2)player.motor.transform.position-((Vector2)enemy.motor.transform.position+Vector2.right*Launcher.releaseOffset.x)).normalized*depthShot.speed;
            Check((depthShot.Velocity-expected).sqrMagnitude<.000001f,"Aiming uses root ground X/depth Y after airborne depth movement");
            float depthHeight=depthShot.FlightHeight; var direction=depthShot.Velocity; player.transform.position=new Vector2(3,-.2f); Tick(5);
            Check(depthShot.Velocity==direction && Mathf.Abs(depthShot.FlightHeight-depthHeight)<.0001f,"Launched book does not home toward later depth or jump-height changes");
            Fixture(); Airborne(.1f); var lowShot=Fire(); float lowHp=player.health.Current; Tick(80);
            Check(!player.motor.IsGrounded && player.motor.Height<.02f && player.health.Current==lowHp,"Airborne hit filter rejects book even when low airborne hurtbox overlaps its flight");
            Fixture(); var parried=Fire(); CombatClock.Unregister(enemy); player.motor.Face(-1);
            var box=player.GetComponentInChildren<CombatHurtbox>();
            // Place the emitted book just outside the grounded hurtbox, then use the actual parry collision.
            parried.InitializeForward(enemy.motor,enemy.hitbox.team,new Vector2(player.transform.position.x-.55f,0),Launcher.releaseOffset.y,1);
            player.RequestGuard(true); Tick(2);
            Check(parried.DeflectionCount==1 && parried.Owner==player.motor && parried.Velocity.x<0 && player.health.Current==player.health.EffectiveMaximum,"Valid grounded parry deflects existing book and negates damage");
            float reflectedHeight=parried.FlightHeight; Tick(4); Check(Mathf.Abs(parried.FlightHeight-reflectedHeight)<.0001f,"Deflected book retains ground-plane height");
            Fixture("Screamer"); Airborne(.1f); var wave=Fire(); float waveHp=player.health.Current;
            Check(wave.groundWave && wave.hit.canHitGrounded && !wave.hit.canHitAirborne,"Screamer still casts forward grounded-only wave against airborne target");
            Tick(90); Check(player.health.Current==waveHp && !player.IsStunned,"Jump avoids Screamer wave damage and Stun");
            Fixture("Screamer"); var groundedWave=Fire(); Until(()=>player.IsStunned,"Screamer wave still Stuns grounded player");
            results.Add("ALL THROWER GROUND-PLANE CHECKS PASSED"); passed=true;
        }
        catch(Exception ex) { results.Add("FAIL: "+ex); Debug.LogException(ex); }
        Clear(); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/ThrowerGroundPlaneValidationResults.txt",results); Debug.Log(string.Join("\n",results));
        if(Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.ExitPlaymode();
    }
}
