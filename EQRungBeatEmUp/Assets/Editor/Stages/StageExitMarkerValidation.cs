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
public static class StageExitMarkerValidation
{
    const string Pending="StageExitMarker.Validation";
    static readonly List<string> results=new List<string>();
    static StageFlowController flow;
    static int entrance;
    static StageSegmentDefinition stage;
    static StageExitMarkerValidation(){EditorApplication.update+=Poll;}
    [MenuItem("Beat Em Up/Stages/Validate existing next-area reward order (Play Mode)")]
    public static void Run()
    {
        SessionState.SetString(Pending+".preview","");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    [MenuItem("Beat Em Up/Stages/Validate Stage 1 next-area guidance (Play Mode)")]
    public static void BuildAndValidate()
    {
        StageExitMarkerSetup.Build();EncounterPreview.Clear();EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
        int index=level.stages.FindIndex(s=>s.stageId=="Stage01_EntranceGate");
        string before=EditorJsonUtility.ToJson(level);
        EncounterPreview.BeginStage(level,index);EncounterPreview.RefreshNow();
        var preview=Resources.FindObjectsOfTypeAll<GameObject>().First(g=>g.name=="Encounter Scene Preview (temporary)");
        results.Clear();Check(preview.GetComponentsInChildren<StageExitMarker>(true).Length==0&&preview.GetComponentsInChildren<Collider2D>(true).Length==0,"Scene marker preview is render-only, with no gameplay scripts or triggers");
        Check(preview.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.gameObject.name=="Animated chevrons")==3,"All three Stage 1 route markers preview as separate sprites");
        EncounterPreview.Clear();Check(before==EditorJsonUtility.ToJson(level),"Preview and clear preserve authored stage data");
        SessionState.SetString(Pending+".preview",string.Join("\n",results));SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string label){if(!value)throw new Exception(label);results.Add("PASS: "+label);}
    static void Tick(float dt=.05f)
    {
        flow.Tick(dt);flow.framing.ApplyFraming(dt);
        foreach(var marker in flow.ExitMarkers)if(marker)marker.RefreshVisual();
        if(!string.IsNullOrEmpty(flow.Failure))throw new Exception(flow.Failure);
    }
    static void Defeat(){foreach(var enemy in flow.LivingEnemies.ToArray())enemy.Damage(100000);Tick();}
    static void Walk(Vector2 destination,Func<bool> stop=null)
    {
        for(int i=0;i<600;i++)
        {
            if(stop!=null&&stop())return;
            var delta=destination-(Vector2)flow.player.transform.position;
            if(delta.magnitude<.03f)return;
            flow.player.MoveInput=Vector2.ClampMagnitude(delta/(flow.player.moveSpeed*.05f),1);flow.player.Simulate(.05f);Tick();
        }
        throw new Exception("Walk could not reach "+destination+" from "+flow.player.transform.position+"; movement locked="+flow.player.MovementLocked);
    }
    static void Visible(string id)
    {
        Check(flow.ActiveNextArea?.markerId==id,"Route selects "+id);
        Check(flow.ExitMarkers.Count(m=>m.Visible)==1&&flow.ExitMarkers.Single(m=>m.Visible).Definition.markerId==id,"Exactly the relevant world marker is visible");
    }
    static void Hidden(string why)
    {
        flow.RefreshExitMarkers();foreach(var marker in flow.ExitMarkers)marker.RefreshVisual();
        Check(flow.ActiveNextArea==null&&flow.ExitMarkers.All(m=>!m.Visible)&&flow.CaptureNextAreaMarker()==null,why+": world marker, NEXT and edge guidance are hidden");
    }
    static void Poll()
    {
        const string request="Temp/StageRewardOrderValidation.request";
        if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
        {File.Delete(request);Run();return;}
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        SessionState.SetBool(Pending,false);results.Clear();results.AddRange(SessionState.GetString(Pending+".preview","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries));bool passed=false;
        try
        {
            flow=Object.FindFirstObjectByType<StageFlowController>();Object.FindFirstObjectByType<CombatClock>().enabled=false;
            flow.player.GetComponent<PlayerCombatInput>().enabled=false;flow.level=Object.Instantiate(flow.level);
            entrance=flow.level.stages.FindIndex(s=>s.stageId=="Stage01_EntranceGate");stage=flow.level.stages[entrance];
            // Exercise every guidance state on the runtime clone, preserving the authored route.
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(StageExitMarkerSetup.Prefab);
            stage.nextAreaMarkers=new List<NextAreaMarkerDefinition>
            {
                new NextAreaMarkerDefinition{markerId="Entrance approach",markerPrefab=prefab,target=NextAreaTarget.EncounterEntry,targetEncounterId=stage.encounters[0].encounterId,showAfter=NextAreaShowAfter.Immediately},
                new NextAreaMarkerDefinition{markerId="After first fight",markerPrefab=prefab,target=NextAreaTarget.EncounterEntry,targetEncounterId=stage.encounters[1].encounterId,showAfter=NextAreaShowAfter.EncounterComplete,afterEncounterId=stage.encounters[0].encounterId},
                new NextAreaMarkerDefinition{markerId="Stage 1 exit",markerPrefab=prefab,target=NextAreaTarget.StageExit,showAfter=NextAreaShowAfter.SequenceComplete}
            };
            FullRoute();AdditionalConditions();passed=true;
        }
        catch(Exception e){results.Add("FAIL: "+e);Debug.LogException(e);}
        finally
        {
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/StageExitMarkerValidationResults.txt",results);
            Debug.Log("STAGE EXIT MARKER VALIDATION "+(passed?"PASSED":"FAILED"));
            if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);else EditorApplication.ExitPlaymode();
        }
    }
    static void FullRoute()
    {
        flow.EnterStage(entrance);Tick();
        Check(stage.nextAreaMarkers.Count==3,"Stage 1 has initial approach, between-encounter and final exit markers");
        Check(flow.ExitMarkers.All(m=>m.GetComponentsInChildren<Collider2D>(true).Length==0),"Marker prefab cannot block movement or create a new transition");
        Visible("Entrance approach");
        var first=stage.encounters[0];var second=stage.encounters[1];
        Walk(new Vector2(flow.player.transform.position.x,stage.movementMin.y+.2f));
        Walk(new Vector2(first.triggerZone.center.x,stage.movementMin.y+.2f),()=>flow.ActiveEncounterName==first.encounterId);Tick(1);
        Hidden("First encounter active");Check(!flow.TryAdvance(),"Existing progression rejects advance during first fight");
        Check(flow.player.arenaMax.x==first.combatBounds.xMax,"Original first arena movement lock is retained");
        Defeat();Visible("After first fight");
        var marker=flow.ExitMarkers.Single(m=>m.Visible);
        Check((marker.transform.position-(Vector3)second.triggerZone.center).sqrMagnitude<.0001f,"Between-fight marker matches the actual second encounter trigger center");
        Check(flow.player.arenaMax==stage.movementMax&&!flow.ExitUnlocked,"First clear unlocks traversal, while second encounter still gates final exit");
        var view=flow.framing.GetComponent<Camera>();var saved=view.transform.position;var savedRect=view.rect;
        view.rect=new Rect(.1f,0,.8f,1);view.transform.position=new Vector3(-10,view.transform.position.y,-10);
        Check(StageExitMarker.TryEdgePosition(view,marker.GuidancePosition,out var edge,out float angle)&&Mathf.Abs(angle+90)<10,"Off-screen target to the right produces a right-pointing screen-edge arrow");
        Check(view.pixelRect.Contains(new Vector2(edge.x,Screen.height-edge.y)),"Edge guidance remains within the letterboxed camera viewport");
        view.transform.position=new Vector3(10,view.transform.position.y,-10);
        Check(StageExitMarker.TryEdgePosition(view,marker.GuidancePosition,out _,out angle)&&Mathf.Abs(Mathf.Abs(angle)-90)<10,"Off-screen target to the left produces a left-pointing arrow");
        view.transform.position=new Vector3(marker.GuidancePosition.x,marker.GuidancePosition.y,-10);
        Check(!StageExitMarker.TryEdgePosition(view,marker.GuidancePosition,out _,out _),"Edge arrow disappears once the world marker is visible");
        view.rect=savedRect;view.transform.position=saved;
        float far=marker.Proximity;flow.player.ResetForStage(second.triggerZone.center-Vector2.right*.6f);Tick();
        Check(marker.Visible&&marker.Proximity>far,"Approaching the next encounter increases marker brightness/pulse proximity");
        Render("BetweenEncounters",marker);
        Walk(second.triggerZone.center,()=>flow.ActiveEncounterName==second.encounterId);Tick(2);
        Hidden("Second encounter active");Check(flow.LivingEnemies.Count()==second.waves.SelectMany(w=>w.enemySpawns).Sum(s=>s.count),"Second encounter keeps its authored enemy groups");
        Defeat();
        Check(flow.WorldRewards&&flow.WorldRewards.IsPending&&flow.WorldRewards.Chapel,"Final enemy defeat immediately spawns the chapel before visiting the exit");
        Check(Vector2.Distance(flow.player.transform.position,stage.playerExitPoint)>stage.exitRadius,"Chapel appears while the player remains away from the exit");
        Hidden("Final fight cleared, chapel pending");
        Check(!flow.ExitUnlocked&&!flow.TryAdvance(),"Exit stays locked until the chapel reward is resolved");
        Check(flow.StageIndex==entrance,"Combat clear does not teleport or automatically advance the player");
        var chapel=flow.WorldRewards.Chapel;Tick();Tick();
        Check(flow.WorldRewards.Chapel==chapel,"Waiting after combat does not duplicate the chapel");
        flow.player.ResetForStage(flow.WorldRewards.Chapel.transform.position);Check(flow.WorldRewards.Interact(),"Player approaches and interacts with the existing reward chapel");Tick();Hidden("Reward choosing");
        var choice=flow.WorldRewards.ChoiceObjects.First();flow.player.ResetForStage(choice.transform.position);
        Check(flow.WorldRewards.Interact(),"Player approaches and selects an existing world upgrade");Tick();
        Visible("Stage 1 exit");
        marker=flow.ExitMarkers.Single(m=>m.Visible);
        Check((marker.transform.position-(Vector3)stage.playerExitPoint).sqrMagnitude<.0001f,"After the reward, the final marker identifies the actual stage transition radius center");
        Render("StageExit",marker);
        var oldMarkers=flow.ExitMarkers.ToArray();
        // Clear breakable route obstacles so this checks progression independently of prop layout.
        var propHit=new AttackHitboxData{damage=100000,laneTolerance=100};
        foreach(var prop in flow.Destructibles)
            for(int hit=0;hit<1000&&!prop.IsBroken;hit++)prop.Receive(propHit,1,flow.player);
        Check(flow.Destructibles.All(p=>p.IsBroken),"Breakable route obstacles cleared before testing the exit transition");
        Walk(new Vector2(flow.player.transform.position.x,.15f));
        Walk(new Vector2(stage.playerExitPoint.x,.15f),()=>flow.StageIndex!=entrance);
        Check(flow.StageIndex==entrance+1,"Physically reaching exit continues to the original next stage");
        Check(oldMarkers.All(m=>!m.Visible&&!m.gameObject.activeInHierarchy),"Transition immediately disables all old marker visuals");
        Check(flow.ActiveNextArea==null,"Stage 1 guidance does not leak into the next stage");
        flow.RestartAt(entrance);Tick();Visible("Entrance approach");
        Check(!flow.EncounterIsComplete(first.encounterId)&&flow.ExitMarkers.Count==3,"Restart recreates marker route and original encounter state");
        var catalog=Resources.Load<MultiplayerCatalog>("MultiplayerCatalog");
        Check(flow.ExitMarkers.All(m=>m.arrowFrames.All(s=>catalog.SpriteId(s)>=0)&&catalog.SpriteId(m.nextLabel.sprite)>=0&&catalog.SpriteId(m.ground.sprite)>=0),"Marker art resolves in multiplayer world sprite catalog");
        var state=new NextAreaMarkerState{id="test",position=Vector3.one,showEdgeArrow=true,arrowSprite=3,labelSprite=4};
        var copy=JsonUtility.FromJson<NextAreaMarkerState>(JsonUtility.ToJson(state));
        Check(copy.id==state.id&&copy.position==state.position&&copy.showEdgeArrow&&copy.labelSprite==4,"Authority guidance snapshot preserves target and HUD sprite references");
    }
    static void AdditionalConditions()
    {
        stage.rewardAfterClear=StageReward.None;var a=stage.encounters[0];var b=stage.encounters[1];
        a.waves.Add(new WaveDefinition {waveId="Delayed marker test",trigger=WaveTrigger.PreviousWaveClear,spawnDelay=1,enemySpawns=new List<EnemySpawnDefinition>{new EnemySpawnDefinition{prefab=a.waves[0].enemySpawns[0].prefab,count=2,interval=.5f,spawnDelay=.2f,spawnPoints=new List<Vector2>{a.combatBounds.center}}}});
        flow.EnterStage(entrance);flow.player.ResetForStage(a.triggerZone.center);Tick(1);Defeat();Hidden("Delayed next wave queued");
        Tick(.8f);Hidden("Next wave delay still pending");Tick(.3f);Tick(.2f);Defeat();Hidden("Second enemy spawn still queued");Tick(.6f);Defeat();Visible("After first fight");
        a.waves.RemoveAt(a.waves.Count-1);
        a.clearCondition=EncounterClearCondition.ManualSignal;flow.EnterStage(entrance);flow.player.ResetForStage(a.triggerZone.center);Tick(1);Defeat();Hidden("Manual encounter clear not signalled");
        flow.SignalEncounterClear(a.encounterId);Tick();Visible("After first fight");a.clearCondition=EncounterClearCondition.AllEnemiesDefeated;
        b.triggerDelay=.5f;flow.player.ResetForStage(b.triggerZone.center);Tick();Hidden("Next trigger entry latched during delay");b.triggerDelay=0;
        flow.EnterStage(entrance);CombatClock.SetPaused(flow,true);Hidden("Combat paused");CombatClock.SetPaused(flow,false);Tick();Visible("Entrance approach");
        var health=flow.player.GetComponent<CharacterHealth>();
        for(int i=0;i<5&&!health.IsDead;i++)health.Damage(100000);
        Check(health.IsDead,"Defeat fixture accounts for any existing lethal-save upgrade");
        Hidden("All players defeated");flow.RestartAt(entrance);Tick();
        var definition=stage.nextAreaMarkers[0];definition.showAfter=NextAreaShowAfter.ControlledByScript;
        flow.EnterStage(entrance);Tick();Hidden("Script-controlled marker defaults hidden");flow.SetNextAreaMarkerVisible(definition.markerId,true);Visible("Entrance approach");
        flow.player.ResetForStage(a.triggerZone.center);Tick(1);Hidden("Script-visible marker cannot override active arena");
        definition.showAfter=NextAreaShowAfter.Immediately;
        a.enabled=false;b.enabled=false;
        stage.nextAreaMarkers[2].showAfter=NextAreaShowAfter.Immediately;flow.EnterStage(entrance);Tick();Visible("Stage 1 exit");
        Check(!flow.LivingEnemies.Any()&&flow.ExitUnlocked,"No-combat area shows immediate marker while using existing exit eligibility");
        var final=stage.nextAreaMarkers[2];final.markerOffset=new Vector3(.1f,.15f,0);flow.RefreshExitMarkers();var marker=flow.ExitMarkers.Single(m=>m.Definition==final);marker.RefreshVisual();
        Check((marker.transform.position-((Vector3)stage.playerExitPoint+final.markerOffset)).sqrMagnitude<.0001f,"Linked marker offset preserves transition position");
        final.useTransitionPosition=false;final.exitPosition=new Vector3(3,.2f,0);marker.RefreshVisual();
        Check(marker.transform.position==final.exitPosition+final.markerOffset,"Independent marker position is editable without moving gameplay trigger");
        flow.enabled=false;Check(flow.ExitMarkers.All(m=>!m.Visible),"Disabling stage flow immediately hides markers");
    }
    static void Render(string name,StageExitMarker marker)
    {
        foreach(var enemy in flow.StageEnemies.Where(h=>h.IsDead))
        {
            var animator=enemy.GetComponent<CharacterAnimation>()?.animator;
            if(animator&&animator.enabled)animator.Update(.5f);
        }
        var camera=flow.framing.GetComponent<Camera>();var oldTarget=camera.targetTexture;var oldPosition=camera.transform.position;var oldRect=camera.rect;float oldAspect=camera.aspect;
        var target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=1280f/720;
        float halfWidth=camera.orthographicSize*camera.aspect;
        camera.transform.position=new Vector3(Mathf.Clamp(marker.transform.position.x-1.1f,-stage.artWidth*.5f+halfWidth,stage.artWidth*.5f-halfWidth),oldPosition.y,-10);
        camera.Render();var oldActive=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        Directory.CreateDirectory("Documentation/StageExitMarkerPreview");File.WriteAllBytes("Documentation/StageExitMarkerPreview/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=oldActive;camera.targetTexture=oldTarget;camera.transform.position=oldPosition;camera.rect=oldRect;camera.aspect=oldAspect;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);
        Check(true,"Rendered actual Stage 1 world marker: "+name);
    }
}
