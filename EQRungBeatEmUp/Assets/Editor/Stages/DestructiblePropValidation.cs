using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DestructiblePropValidation
{
    const string Pending="BeatEmUp.DestructibleValidation";
    static readonly List<string> results=new List<string>();
    static IEnumerator tests;
    static CustomYieldInstruction waiting;
    static GameObject root;
    static bool batch;
    static DestructiblePropValidation() { EditorApplication.update+=Poll; }
    public static void BuildAndValidate() { DestructiblePropSetup.Build(); Run(); }
    [MenuItem("Beat Em Up/Props/Validate destructible props (Play Mode)")]
    public static void Run()
    {
        SessionState.SetBool(Pending,true); SessionState.SetBool(Pending+".Batch",Application.isBatchMode);
        EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling) return;
        const string request="Tools/EntranceRevision/combat.request";
        if(File.Exists(request) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isUpdating)
        {
            File.Delete(request); Run(); return;
        }
        if(tests==null)
        {
            if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying) return;
            SessionState.SetBool(Pending,false); batch=SessionState.GetBool(Pending+".Batch",false); results.Clear(); tests=Tests();
        }
        try
        {
            if(waiting!=null && waiting.keepWaiting) return;
            waiting=null;
            if(!tests.MoveNext()) { Finish(true); return; }
            waiting=tests.Current as CustomYieldInstruction;
        }
        catch(Exception e) { results.Add("FAIL: "+e); Debug.LogException(e); Finish(false); }
    }
    static void Finish(bool passed)
    {
        if(root) UnityEngine.Object.DestroyImmediate(root);
        Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/DestructibleValidationResults.txt",results);
        tests=null; waiting=null; Debug.Log("DESTRUCTIBLE VALIDATION "+(passed?"PASSED":"FAILED"));
        if(batch) EditorApplication.Exit(passed?0:1); else EditorApplication.ExitPlaymode();
    }
    static void Check(bool ok,string label) { if(!ok)throw new Exception(label); results.Add("PASS: "+label); }
    static DestructibleObject Prop(string name,Vector2 point)
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DestructiblePropSetup.Prefabs+name+".prefab"),point,Quaternion.identity,root.transform);
        return go.GetComponent<DestructibleObject>();
    }
    static IEnumerator Tests()
    {
        foreach(var go in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>!t.parent).Select(t=>t.gameObject).ToArray()) go.SetActive(false);
        root=new GameObject("Destructible validation fixture");
        var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab"),root.transform);
        actor.GetComponent<PlayerCombatInput>().enabled=false;
        var motor=actor.GetComponent<CharacterMotor>(); var sampler=actor.GetComponent<AttackHitbox>(); var player=actor.GetComponent<AttackPlayer>();
        var clock=UnityEngine.Object.FindFirstObjectByType<CombatClock>(FindObjectsInactive.Include);
        if(!clock) clock=root.AddComponent<CombatClock>();
        clock.enabled=false;
        motor.ResetForStage(Vector2.zero);
        var data=ScriptableObject.CreateInstance<AttackData>();
        var hit=new AttackHitboxData { damage=100,offset=new Vector2(.7f,.3f),size=new Vector2(.9f,.7f),laneTolerance=.5f,hitId=0 };
        data.frames.Add(new AttackFrameData { hitboxes=new List<AttackHitboxData>{hit} });
        foreach(var name in new[]{"CardboardBox","CeramicDragonJar",EntranceEnvironmentSetup.Fence,EntranceEnvironmentSetup.Motorcycle})
        {
            player.Stop();for(int i=0;i<10;i++){player.PrepareFrame();player.EndClockFrame();}motor.ResetForStage(Vector2.zero);
            var prop=Prop(name,new Vector2(.7f,0)); int hp=(int)prop.maximumHealth;
            Check(prop.Current==hp && prop.visual.sprite==prop.intactSprite,"Intact "+name+" starts with "+hp+" hit points");
            Check(prop.hitSfx && prop.destructionSfx && prop.hitVfx && prop.debrisSprites.All(s=>s),name+" SFX, reused VFX and debris references exist");
            Check(prop.intactSprite.pixelsPerUnit==100 && prop.intactSprite.texture.filterMode==FilterMode.Point,name+" uses character pixel density and point filtering");
            int breaks=0; prop.Broken+=_=>breaks++;
            Check(player.Play(data),"Existing AttackPlayer accepts prop test attack");
            sampler.SetFrame(data.frames[0],0,1); sampler.Sample(); sampler.Sample();
            Check(prop.Current==hp-1 && prop.visual.sprite==prop.damagedSprite,"Overlapping samples deal one HP and change "+name+" to damaged");
            Check(player.CurrentAttack==data && player.HitstopRemaining==prop.hitstopFrames,"Prop hit applies small hitstop without cancelling attack timeline");
            Check(prop.movementBlocker.enabled,"Damaged prop still blocks floor movement");
            sampler.End(); motor.ResetForStage(new Vector2(1.4f,0)); motor.Face(-1);
            sampler.Begin(data);sampler.SetFrame(data.frames[0],0,-1);sampler.Sample();
            Check(prop.Current==hp-2,"Attack from right damages "+name);
            while(!prop.IsBroken) { sampler.Begin(data); sampler.SetFrame(data.frames[0],0,-1); sampler.Sample(); }
            sampler.Begin(data);sampler.SetFrame(data.frames[0],0,-1);sampler.Sample();
            Check(prop.IsBroken && breaks==1 && !prop.GetComponent<BoxCollider2D>().enabled && !prop.movementBlocker.enabled,"Destruction commits once and disables both colliders");
            Check(root.GetComponentsInChildren<PropDebris>().Length==prop.debrisCount,"Break scatters configured number of fragments");
            prop.Respawn(hp);
            Check(!prop.IsBroken && prop.Current==hp && prop.visual.sprite==prop.intactSprite && prop.movementBlocker.enabled,"Respawn restores intact appearance, HP and blocker");
            Check(!root.GetComponentsInChildren<PropDebris>().Any(d=>d.gameObject.activeSelf),"Respawn removes previous debris");
            // Existing motor probe must stop at the footprint, then pass after destruction.
            // Start outside every footprint, including the wider fence and motorcycle.
            prop.transform.position=new Vector2(2,0);motor.ResetForStage(Vector2.zero);Physics2D.SyncTransforms();
            Check(motor.ProbeGroundMove(Vector2.right*3).x<2,"Existing ground movement is blocked by prop footprint");
            while(!prop.IsBroken)prop.Receive(hit,1,motor);
            Physics2D.SyncTransforms();Check(motor.ProbeGroundMove(Vector2.right*3).x>2.9f,"Destroyed prop permits movement");
            foreach(var debris in root.GetComponentsInChildren<PropDebris>())debris.Simulate(4);
            yield return new WaitForSecondsRealtime(.1f);
            Check(root.GetComponentsInChildren<PropDebris>().Length==0,"Debris expires after configured lifetime");
            UnityEngine.Object.DestroyImmediate(prop.gameObject);
        }
        var target=Prop("CeramicDragonJar",new Vector2(.7f,0)); motor.ResetForStage(Vector2.zero);
        foreach(var attack in actor.GetComponent<ComboController>().groundCombo.Concat(new[]{actor.GetComponent<ComboController>().launcher}).Concat(actor.GetComponent<ComboController>().airCombo))
        {
            var frame=attack.frames.First(f=>f.hitboxes.Count>0); var box=frame.hitboxes[0];
            target.Respawn(3);target.transform.position=new Vector3(box.offset.x,0,0);
            // Set jump height for actual authored air frames; ground coordinate/lane remains zero.
            motor.ResetForStage(Vector2.zero);
            if(attack.domain==AttackDomain.Air)motor.SetVerticalVelocity(1);
            sampler.Begin(attack);sampler.SetFrame(frame,attack.frames.IndexOf(frame),1);sampler.Sample();
            Check(target.Current==2,"Authored "+attack.name+" hits stationary prop through existing hitbox");
            Check(target.transform.position.y==0,"Launcher/air response never launches the prop");
        }
        target.Respawn(3);target.transform.position=new Vector2(.7f,0);motor.ResetForStage(Vector2.zero);
        var area=new AttackFrameData { hitboxes=new List<AttackHitboxData>{new AttackHitboxData { groundArea=true,offset=new Vector2(.7f,0),size=new Vector2(2,1),laneTolerance=.5f,damage=10 }} };
        sampler.Begin(data);sampler.SetFrame(area,0,1);sampler.Sample();sampler.Sample();
        Check(target.Current==2,"Ground-area attacks include props and deduplicate repeated samples");
        target.Respawn(3);motor.ResetForStage(new Vector2(0,1));
        Check(!target.Receive(hit,1,motor),"Other walking lanes cannot hit prop");
        target.useHitPoints=false;target.Respawn(25);motor.ResetForStage(Vector2.zero);
        Check(target.Receive(new AttackHitboxData { damage=5,laneTolerance=1 },1,motor) && target.Current==20,"Legacy damage-based totems preserve health semantics");
        target.useHitPoints=true;target.Respawn(3);
        var enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab"),new Vector2(.7f,0),Quaternion.identity,root.transform);
        enemy.GetComponent<EnemyCombat>().enabled=false;
        var enemyHealth=enemy.GetComponent<CharacterHealth>();float previous=enemyHealth.Current;
        sampler.Begin(data);sampler.SetFrame(data.frames[0],0,1);sampler.Sample();sampler.Sample();
        Check(target.Current==2 && enemyHealth.Current<previous,"One swing hits both prop and enemy without suppressing enemy combat");
        UnityEngine.Object.DestroyImmediate(enemy);
        var reward=new GameObject("Optional reward template");reward.transform.SetParent(root.transform);
        reward.AddComponent<PropHealthPickup>();reward.SetActive(false);
        target.dropPrefab=reward;target.dropChance=1;target.Respawn(1);
        int events=0;target.onBroken.AddListener(()=>{events++;target.Receive(hit,1,motor);});
        target.Receive(hit,1,motor);target.Receive(hit,1,motor);
        Check(events==1 && root.GetComponentsInChildren<PropHealthPickup>(true).Length==2,"Reentrant and repeated damage produce one event and one optional reward");
        target.Respawn(3);yield return new WaitForSecondsRealtime(.1f);
        Check(root.GetComponentsInChildren<PropHealthPickup>(true).Length==1,"Prop reset removes spawned optional reward");
        var flowObject=new GameObject("Stage flow fixture");flowObject.transform.SetParent(root.transform);
        var flow=flowObject.AddComponent<StageFlowController>();flow.level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);flow.player=motor;
        int index=flow.level.stages.FindIndex(s=>s.stageId=="Stage01_EntranceGate");flow.EnterStage(index);
        var samples=flow.Destructibles.Where(p=>p.useHitPoints).ToArray();
        int expected=flow.CurrentStage.destructibles.Count(p=>p.prefab && p.prefab.GetComponent<DestructibleObject>().useHitPoints);
        Check(samples.Length==expected && samples.Count(p=>p.name.StartsWith(EntranceEnvironmentSetup.Fence))==6 && samples.Count(p=>p.name.StartsWith(EntranceEnvironmentSetup.Motorcycle))==1,"Stage 1 spawns authored samples, six fences and one motorcycle through LevelDefinition");
        Check(samples.All(p=>p.transform.position.y>=flow.CurrentStage.movementMin.y && p.transform.position.y<=flow.CurrentStage.movementMax.y),"All sample ground pivots lie inside Stage 1 walkable lanes");
        foreach(var prop in samples){motor.ResetForStage(prop.transform.position);while(!prop.IsBroken)prop.Receive(hit,1,motor);}
        flow.RestartAt(index);
        Check(flow.Destructibles.Where(p=>p.useHitPoints).All(p=>!p.IsBroken&&p.Current==p.maximumHealth&&p.movementBlocker.enabled),"Restarting Stage 1 restores all sample props");
        UnityEngine.Object.DestroyImmediate(data);
    }
}
