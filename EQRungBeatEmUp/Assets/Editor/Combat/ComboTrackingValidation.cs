using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ComboTrackingValidation
{
    const string Pending = "BeatEmUp.ComboTrackingValidation";
    static readonly List<string> results = new List<string>();
    static GameObject player, enemy;
    static ComboController control;
    static ComboTracker tracker;
    static ComboUIController ui;
    static CombatClock clock;
    static CombatHurtbox target;
    static ComboTrackingValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate combo tracking and HUD (Play Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string label) { if(!ok) throw new Exception(label); results.Add("PASS: "+label); }
    static void Step(int frames) { for(int i=0;i<frames;i++) clock.StepFrame(); }
    static void Clear()
    {
        if(player) UnityEngine.Object.DestroyImmediate(player); if(enemy) UnityEngine.Object.DestroyImmediate(enemy);
    }
    static void Fixture(float distance=.85f)
    {
        Clear();
        player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath));
        enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab"));
        player.GetComponent<PlayerCombatInput>().enabled=false; enemy.GetComponent<EnemyCombat>().enabled=false;
        control=player.GetComponent<ComboController>(); tracker=player.GetComponent<ComboTracker>(); ui=player.GetComponent<ComboUIController>();
        control.motor.ResetForStage(Vector2.zero); enemy.GetComponent<CharacterMotor>().ResetForStage(new Vector2(distance,0));
        target=enemy.GetComponentInChildren<CombatHurtbox>(); target.health.maximumHealth=500; target.health.Restore();
    }
    static bool Hit(float damage=8,int stun=100) => target.Receive(new AttackHitboxData {damage=damage,hitstunFrames=stun,hitstopFrames=0,knockback=0},1,control.motor);
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); bool passed=false;
        try
        {
            foreach(var actor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach(var flow in UnityEngine.Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) flow.enabled=false;
            clock=UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled=false;
            Fixture(4); Check(!tracker.IsActive && tracker.HitCount==0 && ui.Group.alpha==0,"HUD starts hidden with empty combo");
            control.RequestAttack(); Step(35); Check(!tracker.IsActive && tracker.HitCount==0,"Whiffed attack never counts a hit");
            Fixture(); control.RequestAttack(); Step(control.groundCombo[0].FirstActiveFrame);
            Check(tracker.IsActive && tracker.HitCount==1 && tracker.TotalDamage==8,"Real collision starts a one-hit damage combo");
            float timer=tracker.RemainingSeconds;
            control.hitbox.Sample(); Step(3);
            Check(tracker.HitCount==1 && tracker.RemainingSeconds==timer,"Lingering hitboxes and hitstop do not count again or consume timeout");
            Step(30); Check(!tracker.IsActive && tracker.LastEndReason=="Targets recovered","Target neutral recovery ends combo before maximum timeout");
            Check(ui.HitText.text=="1 HIT" && ui.DamageText.text=="8 DAMAGE" && ui.Group.alpha==1,"Ended combo retains its final HUD result");
            ui.AdvanceDisplay(ui.finalHoldSeconds+.1f); Check(ui.Group.alpha>0 && ui.Group.alpha<1,"Final result fades after configured hold");
            ui.AdvanceDisplay(ui.fadeSeconds); Check(ui.Group.alpha==0,"HUD hides after fade");
            Fixture(); Hit(); Step(54); Check(!tracker.IsActive && tracker.LastEndReason=="Timeout","0.9-second timeout ends a target that remains stunned");
            Fixture(); Hit(); Step(30); Hit(9); Step(30);
            Check(tracker.IsActive && tracker.HitCount==2 && tracker.TotalDamage==17,"Another accepted hit refreshes timer and accumulates damage");
            CombatClock.SetPaused(player,true); timer=tracker.RemainingSeconds; Step(100); ui.AdvanceDisplay(10); CombatClock.SetPaused(player,false);
            Check(tracker.RemainingSeconds==timer && ui.Group.alpha==1,"Combat pause freezes counter timer and display");
            tracker.ResetTracking(); Check(tracker.HitCount==0 && ui.Group.alpha==0,"Explicit reset clears active result and HUD");
            Fixture(); target.health.maximumHealth=5; target.health.Restore(); Hit(20); Step(1);
            Check(!tracker.IsActive && tracker.HitCount==1 && tracker.TotalDamage==5,"Lethal hit finalizes with actual HP lost, not overkill");
            Check(ui.DamageText.text=="5 DAMAGE","Lethal result remains readable in HUD");
            Fixture(); target.externalInvulnerable=true; Hit(); Check(tracker.HitCount==0,"Rejected/invulnerable damage does not count");
            target.externalInvulnerable=false; Hit(0); Check(tracker.HitCount==0,"Zero damage does not count");
            Hit(); control.health.Damage(1); Check(!tracker.IsActive && tracker.LastEndReason=="Player interrupted","Player damage drops active sequence");
            Fixture(); Hit(); control.Interrupt(10);
            Check(!tracker.IsActive && tracker.LastEndReason=="Player interrupted","Direct interruption/parry drops active sequence even without damage");
            Fixture(); Hit(); control.motor.ResetForStage(Vector2.zero);
            Check(!tracker.IsActive && tracker.HitCount==0 && ui.Group.alpha==0,"Stage positioning reset clears combo and HUD");
            Fixture(); Hit(); var other=UnityEngine.Object.Instantiate(enemy);
            try
            {
                var otherTarget=other.GetComponentInChildren<CombatHurtbox>();
                otherTarget.Receive(new AttackHitboxData{damage=9,hitstunFrames=100},1,control.motor); Step(1);
                Check(tracker.IsActive && tracker.HitCount==2 && tracker.TotalDamage==17,"Multiple enemies participate in one player sequence");
                otherTarget.health.Damage(1000); Step(1);
                Check(tracker.IsActive,"One defeated target does not end remaining valid multi-target combo");
                target.health.Damage(1000); Step(1); Check(!tracker.IsActive,"Last target death ends multi-target combo");
            }
            finally { UnityEngine.Object.DestroyImmediate(other); }
            // Real dive follow-up collision: seed a qualifying airborne hit, then use the existing dive input.
            Fixture(1); control.RequestJump(); Step(12); target.motor.Launch(4,0); target.motor.Simulate(.1f);
            var seed=new AttackHitboxData{damage=8,hitstunFrames=100,canHitAirborne=true,knockback=0};
            target.Receive(seed,1,control.motor); control.RequestLauncher();
            for(int i=0;i<40 && tracker.HitCount<2;i++) Step(1);
            Check(tracker.HitCount==2 && tracker.TotalDamage==22,"Existing 14-damage dive continues qualifying airborne hit sequence (hits="+tracker.HitCount+", damage="+tracker.TotalDamage+", end="+tracker.LastEndReason+")");
            Check(ui.HitText.text=="2 HITS" && ui.DamageText.text=="22 DAMAGE","Dive updates combo UI from actual accepted hit");
            Fixture(); Hit(12); Hit(13); Hit(17); Hit(10); Hit(8); Hit(9);
            Capture(); Check(ui.Group.blocksRaycasts==false && !ui.HitText.raycastTarget && !ui.DamageText.raycastTarget,"Screen HUD never blocks gameplay input");
            passed=true;
        }
        catch(Exception error) { results.Add("FAIL: "+error); Debug.LogException(error); }
        finally
        {
            Clear(); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/ComboTrackingValidationResults.txt",results);
            Debug.Log("COMBO TRACKING "+(passed?"PASSED":"FAILED")+": "+results.Count+" checks");
            if(Application.isBatchMode) EditorApplication.Exit(passed?0:1); else EditorApplication.ExitPlaymode();
        }
    }
    static void Capture()
    {
        var canvas=ui.Group.GetComponentInParent<Canvas>();
        // Isolate the HUD and test actors; this is a UI preview, not a stage-art framing test.
        var scenery=UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Where(r=>r.enabled && !r.transform.IsChildOf(player.transform) && !r.transform.IsChildOf(enemy.transform)).ToArray();
        foreach(var renderer in scenery) renderer.enabled=false;
        var go=new GameObject("Combo HUD validation camera"); var camera=go.AddComponent<Camera>();
        camera.transform.position=new Vector3(0,0,-10); camera.orthographic=true; camera.orthographicSize=2.2f;
        camera.backgroundColor=new Color(.08f,.1f,.14f); camera.clearFlags=CameraClearFlags.SolidColor;
        var targetTexture=new RenderTexture(1280,720,24); var old=RenderTexture.active;
        try
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            camera.targetTexture=targetTexture; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=targetTexture;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            Directory.CreateDirectory("Documentation/ComboTrackingPreview"); File.WriteAllBytes("Documentation/ComboTrackingPreview/ComboHUD.png",image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally { foreach(var renderer in scenery) if(renderer) renderer.enabled=true; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; camera.targetTexture=null; RenderTexture.active=old; targetTexture.Release(); UnityEngine.Object.DestroyImmediate(targetTexture); UnityEngine.Object.DestroyImmediate(go); }
    }
}
