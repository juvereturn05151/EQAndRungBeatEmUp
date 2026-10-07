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
public static class Character2WalkValidation
{
    const string Pending="Character2.WalkValidation.Pending";
    static readonly List<string> results=new List<string>();
    static Character2WalkValidation() { EditorApplication.update+=Poll; }
    [MenuItem("Beat Em Up/Characters/Validate corrected GrayShirtGuy walk")]
    public static void Run()
    {
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
        if(SessionState.GetBool("Character2.WalkValidation.Finished",false) && !EditorApplication.isPlayingOrWillChangePlaymode && Application.isBatchMode)
        { EditorApplication.Exit(SessionState.GetInt("Character2.WalkValidation.ExitCode",1)); return; }
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); GameObject fixture=null; int code=0;
        try
        {
            foreach(var flow in Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) { flow.enabled=false; if(flow.GetComponent<PlayerHubController>()) flow.GetComponent<PlayerHubController>().enabled=false; }
            foreach(var actor in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            var clock=Object.FindFirstObjectByType<CombatClock>(); clock.enabled=false;
            var character=AssetDatabase.LoadAssetAtPath<PlayableCharacterData>(Character2Setup.DefinitionPath);
            fixture=Object.Instantiate(character.prefab);
            fixture.GetComponent<PlayerCombatInput>().enabled=false; fixture.GetComponent<PlayerInput>().enabled=false;
            var player=fixture.GetComponent<ComboController>(); var motor=player.motor; var animator=player.animationDriver.animator;
            motor.ResetForStage(Vector2.zero); motor.arenaMin=new Vector2(-100,-100); motor.arenaMax=new Vector2(100,100);
            var scale=motor.sprite.transform.lossyScale; float speed=motor.moveSpeed;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Character2WalkRepair.ClipPath);
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
            Check(keys.Length==13 && keys[0].value==keys[12].value,"Twelve poses close into Walk_01");
            var settings=AnimationUtility.GetAnimationClipSettings(clip);
            Check(clip.frameRate==60 && Mathf.Abs(keys[12].time-.6f)<.0001f && Mathf.Abs(settings.stopTime-.6f)<.0001f && settings.loopTime,"Existing 60 FPS, closing key and authored loop endpoint retained (Unity sampled length="+clip.length+")");
            Check(!AnimationUtility.GetCurveBindings(clip).Any(b=>b.propertyName.Contains("Scale")),"Walk does not animate Transform scale");
            var idleMetrics=Metrics(character.idlePose);
            for(int i=0;i<12;i++)
            {
                var sprite=(Sprite)keys[i].value;
                Check(sprite.name=="Walk_"+(i+1).ToString("00") && Mathf.Abs(keys[i].time-i*.05f)<.0001f,"Pose and unchanged timestamp "+sprite.name);
                Check(sprite.rect.size==new Vector2(128,128) && sprite.pixelsPerUnit==character.idlePose.pixelsPerUnit && sprite.pivot==new Vector2(64,8),"Canvas, PPU and feet pivot "+sprite.name);
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Check(importer.filterMode==FilterMode.Point && !importer.mipmapEnabled,"Crisp point import "+sprite.name);
                var metrics=Metrics(sprite);
                Check(Mathf.Abs(metrics.x-idleMetrics.x)<=2 && metrics.y==120,"Body height matches Idle within pose bob and grounded baseline "+sprite.name);
                Check(Mathf.Abs(metrics.z-idleMetrics.z)<=2,"Hair silhouette matches Idle width "+sprite.name+" ("+metrics.z+" vs "+idleMetrics.z+")");
            }
            motor.MoveInput=Vector2.zero; clock.StepFrame(); animator.Update(0);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Shared controller starts in Idle"); Capture(motor,"Idle");
            motor.MoveInput=Vector2.right; clock.StepFrame(); animator.Update(0);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") && animator.speed==1,"Shared movement enters Walk at original playback speed");
            for(int loop=0;loop<4;loop++) for(int i=0;i<12;i++)
            {
                animator.Play("Base Layer.Walk",0,(i*.05f+.025f)/animator.GetCurrentAnimatorStateInfo(0).length); animator.Update(0);
                Check(motor.sprite.sprite==keys[i].value && motor.sprite.transform.lossyScale==scale,"Repeated loop "+loop+" pose "+(i+1)+" at identical renderer scale (actual="+motor.sprite.sprite.name+")");
                if(loop==0 && (i==0 || i==4 || i==5 || i==11)) Capture(motor,"Walk_"+(i+1).ToString("00"));
            }
            player.animationDriver.Play("Walk",true); animator.Update(0);
            int previousPose=0,changes=0; var seen=new HashSet<int>();
            for(int tick=0;tick<144;tick++)
            {
                clock.StepFrame(); animator.Update(1f/60f);
                int pose=Array.FindIndex(keys.Take(12).ToArray(),k=>k.value==motor.sprite.sprite);
                Check(pose>=0 && motor.sprite.transform.lossyScale==scale,"Continuous walking retains corrected sprite and renderer scale at tick "+tick);
                seen.Add(pose);
                if(pose!=previousPose)
                {
                    Check(pose==(previousPose+1)%12,"Continuous playback advances in chronological gait order");
                    previousPose=pose;changes++;
                }
            }
            Check(seen.Count==12 && changes>=44,"Continuous walking traverses all twelve poses for four loops");
            Check(motor.transform.position.x>0 && motor.moveSpeed==speed,"Movement runs without speed changes");
            motor.MoveInput=Vector2.zero; clock.StepFrame(); animator.Update(0);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && motor.sprite.transform.lossyScale==scale && animator.speed==1,"Stopping returns to Idle with identical scale and speed");
            results.Add("ALL WALK CHECKS PASSED");
        }
        catch(Exception ex) { code=1; results.Add("FAIL: "+ex); Debug.LogException(ex); }
        finally
        {
            if(fixture) Object.DestroyImmediate(fixture);
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/Character2WalkValidationResults.txt",results);
            Debug.Log(string.Join("\n",results)); SessionState.SetInt("Character2.WalkValidation.ExitCode",code);
            SessionState.SetBool("Character2.WalkValidation.Finished",true); EditorApplication.ExitPlaymode();
        }
    }
    static Vector3Int Metrics(Sprite sprite)
    {
        var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            var pixels=image.GetPixels32(); int top=image.height,bottom=0,left=image.width,right=0;
            for(int y=0;y<image.height;y++) for(int x=0;x<image.width;x++)
                if(pixels[(image.height-1-y)*image.width+x].a>128) { top=Mathf.Min(top,y);bottom=Mathf.Max(bottom,y+1); }
            for(int y=top;y<Mathf.Min(top+26,image.height);y++) for(int x=0;x<image.width;x++)
            {
                var c=pixels[(image.height-1-y)*image.width+x];
                if(c.a>128 && c.r<60 && c.g<60 && c.b<95) { left=Mathf.Min(left,x);right=Mathf.Max(right,x+1); }
            }
            return new Vector3Int(bottom-top,bottom,right-left);
        }
        finally { Object.DestroyImmediate(image); }
    }
    static void Capture(CharacterMotor motor,string name)
    {
        var go=new GameObject("Walk comparison camera"); var camera=go.AddComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=.75f; camera.transform.position=motor.transform.position+new Vector3(0,.55f,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.07f,.09f);
        var target=new RenderTexture(512,512,24); camera.targetTexture=target; var previous=RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active=target; var image=new Texture2D(512,512,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply();
            Directory.CreateDirectory("Documentation/Character2WalkPreview"); File.WriteAllBytes("Documentation/Character2WalkPreview/Unity_"+name+".png",image.EncodeToPNG()); Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active=previous; camera.targetTexture=null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(go); }
    }
}
