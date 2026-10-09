using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CombatFeedbackValidation
{
    const string Pending = "BeatEmUp.CombatFeedbackValidation";
    const string Root = "Assets/EQ_Rung_BeatEmUp/";
    static readonly List<string> results = new List<string>();
    static GameObject player, enemy;
    static CombatClock clock;
    static CombatFeedbackValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate combo sound and effects (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string label) { if(!ok) throw new Exception(label); results.Add("PASS: "+label); }
    static void Step(int frames) { for(int i=0;i<frames;i++) clock.StepFrame(); }
    static void Clear()
    {
        if(player) UnityEngine.Object.DestroyImmediate(player);
        if(enemy) UnityEngine.Object.DestroyImmediate(enemy);
        foreach(var go in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t=>!t.parent && (t.name=="Combat sound (temporary)" || t.name=="Combat impact (temporary)")).Select(t=>t.gameObject).ToArray())
            UnityEngine.Object.DestroyImmediate(go);
    }
    static AttackPlayer Fixture(AttackData attack,int facing,bool contact)
    {
        Clear();
        player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/BlueShirtGuy.prefab"));
        enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/BadGuy.prefab"));
        player.GetComponent<PlayerCombatInput>().enabled=false; enemy.GetComponent<EnemyCombat>().enabled=false;
        var motor=player.GetComponent<CharacterMotor>(); var target=enemy.GetComponent<CharacterMotor>();
        motor.ResetForStage(Vector2.zero); motor.Face(facing); target.ResetForStage(new Vector2(facing*(contact?.85f:4),0));
        enemy.GetComponent<CharacterHealth>().maximumHealth=500; enemy.GetComponent<CharacterHealth>().Restore();
        if(attack.domain==AttackDomain.Air) { motor.Launch(4,0); target.Launch(4,0); motor.Simulate(.1f); target.Simulate(.1f); }
        return player.GetComponent<AttackPlayer>();
    }
    static void Poll()
    {
        const string request="Temp/CombatFeedbackValidation.request";
        if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
        { File.Delete(request); Run(); return; }
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); bool passed=false;
        try
        {
            foreach(var actor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach(var flow in UnityEngine.Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) flow.enabled=false;
            clock=UnityEngine.Object.FindFirstObjectByType<CombatClock>();
            if(!clock) clock=new GameObject("Feedback validation clock").AddComponent<CombatClock>();
            clock.enabled=false;
            foreach(string name in new[]{"Punch1","Punch2","Punch3","AirPunch1","AirPunch2","AirPunch3","Character2_AirPunch3"})
            {
                var attack=AssetDatabase.LoadAssetAtPath<AttackData>(Root+(name.StartsWith("Character2_")?"Characters/Character2/":"Attacks/")+name+".asset"); var data=attack.feedback;
                Check(data!=null && data.swingSound && data.impactSound && data.impactPrefab,name+" resolves both library sounds and VFX prefab");
                Check(attack.frames.SelectMany(f=>f.events).Count(e=>e=="Swing")==1 && attack.frames[attack.FirstActiveFrame].events.Contains("Swing"),name+" swing is authored exactly once on first active frame");
                foreach(int facing in new[]{1,-1})
                {
                    var playback=Fixture(attack,facing,true); Check(playback.Play(attack),name+" begins");
                    var feedback=player.GetComponent<AttackFeedback>(); Step(attack.FirstActiveFrame-1);
                    Check(feedback.SwingCount==0 && feedback.ImpactCount==0,name+" anticipation has no early cues");
                    Step(1);
                    Check(feedback.SwingCount==1 && feedback.ImpactCount==1 && feedback.LastImpact,name+" confirmed hit and swing occur on actual first active frame, facing "+facing);
                    var sounds=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                    Check(sounds.Any(s=>s.clip==data.swingSound && s.volume==data.swingVolume) && sounds.Any(s=>s.clip==data.impactSound && s.volume==data.impactVolume),name+" audio sources use selected clips and volumes");
                    var particles=feedback.LastImpact.GetComponentsInChildren<ParticleSystem>();
                    Check(particles.Length>0 && particles.All(p=>!p.main.loop && p.main.scalingMode==ParticleSystemScalingMode.Hierarchy),name+" library particles are finite and scale with effect");
                    Check(feedback.LastImpact.GetComponentsInChildren<Light>().All(l=>!l.enabled) && feedback.LastImpact.GetComponentsInChildren<CartoonFX.CFXR_Effect>().All(c=>c.cameraShake==null || !c.cameraShake.enabled),name+" instance disables lighting and camera shake");
                    var visual=player.GetComponent<CharacterMotor>().sprite;
                    Check(particles.All(p=>p.GetComponent<ParticleSystemRenderer>().sortingLayerID==visual.sortingLayerID && p.GetComponent<ParticleSystemRenderer>().sortingOrder==visual.sortingOrder+2),name+" VFX render just above player sprite");
                    player.GetComponent<AttackHitbox>().Sample(); Step(playback.HitstopRemaining);
                    Check(feedback.SwingCount==1 && feedback.ImpactCount==1,name+" repeated samples and hitstop do not duplicate feedback");
                    if(facing==1 && name.EndsWith("3")) Capture(name,feedback.LastImpact);
                }
                var whiff=Fixture(attack,1,false); whiff.Play(attack); Step(attack.FirstActiveFrame);
                var whiffFeedback=player.GetComponent<AttackFeedback>();
                Check(whiffFeedback.SwingCount==1 && whiffFeedback.ImpactCount==0 && !whiffFeedback.LastImpact,name+" whiff has swing but no false impact");
                whiff.Stop(); whiff.Play(attack); Step(attack.FirstActiveFrame);
                Check(whiffFeedback.SwingCount==2,name+" interrupted same-asset restart resets feedback history");
                CombatClock.SetPaused(player,true); int before=whiffFeedback.SwingCount; Step(10); CombatClock.SetPaused(player,false);
                Check(whiffFeedback.SwingCount==before,name+" paused combat emits no extra feedback");
            }
            var p1=AssetDatabase.LoadAssetAtPath<AttackData>(Root+"Attacks/Punch1.asset");
            var rejected=Fixture(p1,1,true); enemy.GetComponentInChildren<CombatHurtbox>().externalInvulnerable=true;
            rejected.Play(p1); Step(p1.FirstActiveFrame);
            Check(player.GetComponent<AttackFeedback>().ImpactCount==0,"Invulnerable/rejected hits do not create impact cues");
            // Multi-target hits still damage each target while sharing one feedback burst per frame.
            var multi=Fixture(p1,1,true); var extra=UnityEngine.Object.Instantiate(enemy);
            try
            {
                multi.Play(p1); Step(p1.FirstActiveFrame);
                Check(enemy.GetComponent<CharacterHealth>().Current<500 && extra.GetComponent<CharacterHealth>().Current<500 && player.GetComponent<AttackFeedback>().ImpactCount==1,"Two targets on one frame share one impact without losing damage");
            }
            finally { UnityEngine.Object.DestroyImmediate(extra); }
            passed=true;
        }
        catch(Exception error) { results.Add("FAIL: "+error); Debug.LogException(error); }
        finally
        {
            Clear(); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/CombatFeedbackValidationResults.txt",results);
            Debug.Log("COMBAT FEEDBACK "+(passed?"PASSED":"FAILED")+": "+results.Count(r=>r.StartsWith("PASS:"))+" checks");
            if(Application.isBatchMode) EditorApplication.Exit(passed?0:1); else EditorApplication.ExitPlaymode();
        }
    }
    static void Capture(string name,GameObject effect)
    {
        var transforms=player.GetComponentsInChildren<Transform>(true).Concat(enemy.GetComponentsInChildren<Transform>(true)).Concat(effect.GetComponentsInChildren<Transform>(true)).ToArray();
        var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        foreach(var t in transforms)t.gameObject.layer=31;
        foreach(var p in effect.GetComponentsInChildren<ParticleSystem>()) p.Simulate(.06f,false,true);
        var cameraObject=new GameObject("Feedback validation camera"); var camera=cameraObject.AddComponent<Camera>();
        camera.cullingMask=1<<31;
        camera.orthographic=true; camera.orthographicSize=1.25f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.07f,.09f,.13f);
        camera.transform.position=new Vector3(.45f,player.GetComponent<CharacterMotor>().Height+.45f,-10);
        var target=new RenderTexture(720,480,24); var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            var image=new Texture2D(720,480,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,720,480),0,0); image.Apply();
            Directory.CreateDirectory("Documentation/CombatReadabilityPreview"); File.WriteAllBytes("Documentation/CombatReadabilityPreview/"+name+".png",image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            var bounds=new Bounds(effect.transform.position,Vector3.zero);
            foreach(var renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>()) bounds.Encapsulate(renderer.bounds);
            results.Add("INFO: "+name+" sampled VFX bounds: "+bounds.size);
        }
        finally { for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i]; RenderTexture.active=previous; camera.targetTexture=null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject); }
    }
}
