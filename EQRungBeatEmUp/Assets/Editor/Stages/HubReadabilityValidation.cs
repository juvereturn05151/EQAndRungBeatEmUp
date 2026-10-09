using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HubReadabilityValidation
{
    const string Pending="HubReadability.Validation";
    static List<string> results=new List<string>();
    static HubReadabilityValidation(){EditorApplication.update+=Poll;}
    public static void BuildAndValidate()
    {
        HubReadabilitySetup.Build();
        EditorSceneManager.OpenScene(PlayerSanctuarySetup.ScenePath);
        MetaSave.DirectoryOverride=Path.GetFullPath("Temp/HubReadabilitySaves");
        SessionState.SetString(Pending+".Saves",MetaSave.DirectoryOverride);
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS: "+label);}
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling) return;
        var flow=Object.FindFirstObjectByType<StageFlowController>();
        if(!flow||flow.StageIndex!=0||!flow.player.GetComponent<MetaProgress>())return;
        SessionState.SetBool(Pending,false);MetaSave.DirectoryOverride=SessionState.GetString(Pending+".Saves",null);
        results.Clear();bool passed=false;
        try
        {
            var hub=flow.GetComponent<PlayerHubController>();var player=flow.player;var meta=player.GetComponent<MetaProgress>();
            meta.Initialize(hub.definition,new MetaProfile{characterId="blue-shirt",essence=1000},0,false);
            foreach(var input in Object.FindObjectsByType<PlayerCombatInput>())input.enabled=false;
            foreach(var clock in Object.FindObjectsByType<CombatClock>())clock.enabled=false;
            var stations=Object.FindObjectsByType<HubInteractionStation>();Check(stations.Length==4,"Current PlayerHub spawns four separate foreground station objects");
            foreach(var station in stations.OrderBy(s=>s.stationIndex))
            {
                int i=station.stationIndex;player.ResetForStage(station.GroundPosition+Vector2.left*2);meta.OpenStation=-1;
                station.RefreshPresentation();Check(!station.HasNearbyPlayer&&!station.indicator.enabled,"Station "+i+" hides nearby indicator out of range");
                Check(!station.TryInteract(player),"Station "+i+" rejects interaction outside range");
                player.ResetForStage(station.GroundPosition+Vector2.down*.2f);station.RefreshPresentation();
                Check(station.HasNearbyPlayer&&station.indicator.enabled,"Station "+i+" highlights and shows icon on approach");
                Check(station.GetComponent<CircleCollider2D>().isTrigger && Mathf.Approximately(station.GetComponent<CircleCollider2D>().radius,hub.definition.interactionRadius),"Station "+i+" trigger matches existing interaction radius");
                Check(station.body.sprite&&station.body.sprite.texture.filterMode==FilterMode.Point&&station.body.sprite.pixelsPerUnit==100,"Station "+i+" separate sprite matches character pixel density");
                Check(station.outline.sharedMaterial.shader.name=="BeatEmUp/Interactable Pixel Outline"&&station.outline.sharedMaterial.shader.isSupported,"Station "+i+" has supported dedicated pixel-outline material");
                Check(station.body.sortingOrder>-900&&station.body.transform.localPosition.y==.24f,"Station "+i+" renders in foreground above painted panels with grounded pivot");
                Physics2D.SyncTransforms();Check(player.ProbeGroundMove(Vector2.up*.2f).y>.19f,"Station "+i+" interaction point remains naturally approachable");
                Check(station.TryInteract(player)&&meta.OpenStation==i,"Station "+i+" opens existing hub menu through PlayerHubController");
                station.RefreshPresentation();Check(!station.indicator.enabled,"Station "+i+" hides prompt indicator while menu is open");
                hub.Execute(player,HubAction.Close,0);
                player.SetVerticalVelocity(2);Check(!station.TryInteract(player),"Station "+i+" rejects interaction while airborne");player.ResetForStage(station.GroundPosition);
            }
            var action=player.GetComponent<PlayerInput>().actions.FindAction("Player/Interact",true);
            Check(action.bindings.Any(b=>b.path.Contains("<Keyboard>/e"))&&action.bindings.Any(b=>b.path.Contains("leftShoulder")),"Existing E and controller shoulder interaction bindings remain intact");
            player.ResetForStage(hub.definition.stations[1]);Check(flow.Interact()==true&&meta.OpenStation==1,"Existing StageFlow interaction route opens foreground base-stat station");
            Check(hub.Execute(player,HubAction.Stat,0)&&meta.profile.healthLevel==1,"Gong still applies existing base-stat upgrade");hub.Execute(player,HubAction.Close,0);
            player.ResetForStage(hub.definition.stations[2]);hub.Interact(player);Check(hub.Execute(player,HubAction.Skill,0)&&meta.profile.blueSkillLevel==1,"Flame shrine still upgrades equipped skill");hub.Execute(player,HubAction.Close,0);
            player.ResetForStage(hub.definition.stations[3]);hub.Interact(player);Check(hub.Execute(player,HubAction.EnterWorld,0)&&flow.CurrentStage.stageId=="Stage01_EntranceGate","Readable entrance still starts World 1 through existing hub function");
            flow.Restart(true);Check(Object.FindObjectsByType<HubInteractionStation>().Length==4,"Hub restart restores four separate stations without duplicate gameplay objects");
            passed=true;
        }
        catch(Exception e){results.Add("FAIL: "+e);Debug.LogException(e);}
        Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/HubReadabilityValidationResults.txt",results);
        Debug.Log("HUB READABILITY VALIDATION "+(passed?"PASSED":"FAILED"));
        if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);else EditorApplication.ExitPlaymode();
    }
    public static void RenderPreviews()
    {
        EditorSceneManager.OpenScene(PlayerSanctuarySetup.ScenePath);
        var flow=Object.FindFirstObjectByType<StageFlowController>();var definition=flow.GetComponent<PlayerHubController>().definition;
        var camera=Camera.main;Directory.CreateDirectory("Documentation/HubReadabilityPreview");
        var player=flow.player;player.GetComponent<CharacterAnimation>().enabled=false;
        for(int i=0;i<4;i++)
        {
            var point=definition.stations[i];player.transform.position=new Vector3(point.x-.9f,point.y-.2f,0);
            camera.transform.position=new Vector3(point.x,1.35f,-10);camera.orthographic=true;camera.orthographicSize=2.8f;
            var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();
            var before=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Documentation/HubReadabilityPreview/"+HubReadabilitySetup.Names[i]+".png",image.EncodeToPNG());
            RenderTexture.active=before;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);
        }
        if(Application.isBatchMode)EditorApplication.Exit(0);
    }
}

