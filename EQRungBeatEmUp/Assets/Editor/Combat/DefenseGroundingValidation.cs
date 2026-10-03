using System;
using System.Collections.Generic;
using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class DefenseGroundingValidation
{
    const string Pending = "BeatEmUp.DefenseGroundingValidation";
    static readonly List<string> results = new List<string>();
    static readonly Dictionary<Sprite,float> baselines = new Dictionary<Sprite,float>();
    static ComboController player;
    static CombatClock clock;
    static Camera preview;
    static Texture2D contactSheet;
    static int panel;
    static float reference;
    static DefenseGroundingValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate defensive sprite grounding (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool valid,string label) { if(!valid) throw new Exception(label); results.Add("PASS: "+label); }
    static float Baseline(Sprite sprite)
    {
        if(baselines.TryGetValue(sprite,out float value)) return value;
        var texture=new Texture2D(2,2); texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        var pixels=texture.GetPixels32(); int low=int.MaxValue;
        for(int y=(int)sprite.rect.y;y<sprite.rect.yMax;y++)
            for(int x=(int)sprite.rect.x;x<sprite.rect.xMax;x++)
                if(pixels[y*texture.width+x].a>=128) low=Mathf.Min(low,y);
        value=(low-sprite.rect.y-sprite.pivot.y)/sprite.pixelsPerUnit;
        UnityEngine.Object.DestroyImmediate(texture); baselines[sprite]=value; return value;
    }
    static void Grounded(string label)
    {
        var renderer=player.motor.sprite;
        float feet=renderer.transform.TransformPoint(new Vector3(0,Baseline(renderer.sprite),0)).y;
        Check(Mathf.Abs(feet-player.transform.position.y-reference)<.00001f && player.motor.visual.localPosition==Vector3.zero && Mathf.Abs(player.transform.position.y)<.00001f,label+" feet match Idle; root and visual Y unchanged");
    }
    static void Step() { clock.StepFrame(); if(player.animationDriver.animator.enabled) player.animationDriver.animator.Update(0); }
    static void Poll()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        SessionState.SetBool(Pending,false); results.Clear(); baselines.Clear(); panel=0;
        try
        {
            foreach(var stage in UnityEngine.Object.FindObjectsByType<StageFraming>(FindObjectsSortMode.None)) stage.enabled=false;
            foreach(var motor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) motor.gameObject.SetActive(false);
            foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled=false;
            player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab")).GetComponent<ComboController>();
            player.GetComponent<PlayerCombatInput>().enabled=false; player.transform.position=Vector3.zero;
            clock=UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled=false;
            player.animationDriver.Play("Idle",true); player.animationDriver.animator.Update(0); reference=Baseline(player.motor.sprite.sprite);
            Check(Mathf.Abs(reference)<.00001f,"Gameplay Idle opaque sole edge is grounded at root Y");
            foreach(var clip in player.animationDriver.animator.runtimeAnimatorController.animationClips)
            {
                if(clip.name!="Idle"&&clip.name!="Walk")continue;
                Check(AnimationUtility.GetCurveBindings(clip).Length==0,clip.name+" has no transform/root-motion curves");
                foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,binding))
                        if(key.value is Sprite sprite) Check(Mathf.Abs(Baseline(sprite)-reference)<.00001f,clip.name+" "+sprite.name+" baseline unchanged");
            }
            Check(!player.animationDriver.animator.applyRootMotion,"Player root motion remains disabled");
            var data=player.defenseData;
            foreach(var holds in new[]{data.guard,data.dodge,data.parry}) foreach(var hold in holds)
                Check(hold.sprite && hold.sprite.pixelsPerUnit==100 && Mathf.Abs(Baseline(hold.sprite)-reference)<.00001f,hold.sprite.name+" authored hold uses the Idle baseline and 100 PPU");
            Check(Mathf.Abs(Baseline(data.blockPose)-reference)<.00001f,"Guard block recoil baseline matches Idle");
            preview=new GameObject("Grounding validation camera").AddComponent<Camera>(); preview.orthographic=true; preview.orthographicSize=.8f;
            preview.clearFlags=CameraClearFlags.SolidColor; preview.backgroundColor=new Color(.12f,.14f,.17f); preview.allowMSAA=false;
            contactSheet=new Texture2D(1792,512,TextureFormat.RGB24,false);
            foreach(var source in new[]{"Idle","Walk"})
            {
                player.animationDriver.Play(source,true); player.animationDriver.animator.Update(0); Grounded(source); Capture(source);
            }
            foreach(int facing in new[]{1,-1}) foreach(var source in new[]{"Idle","Walk"}) for(int repeat=0;repeat<3;repeat++) foreach(var action in new[]{"Guard","Parry","Dodge"})
            {
                player.motor.Face(facing); player.motor.MoveInput=Vector2.zero;
                player.animationDriver.Play(source,true); player.animationDriver.animator.Update(0); Grounded(source+" before "+action);
                float startX=player.transform.position.x;
                if(action=="Dodge") player.RequestDodge();
                else { player.RequestGuard(true); if(action=="Parry") player.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{canHitGrounded=true,damage=10},-facing); }
                int frames=action=="Guard"?35:action=="Dodge"?20:8; Sprite last=null;
                for(int frame=0;frame<frames;frame++)
                {
                    if(action=="Guard"&&frame==8) player.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{canHitGrounded=true,blockstunFrames=10},-facing);
                    Grounded(source+" -> "+action+" facing "+facing+" repeat "+repeat+" frame "+frame);
                    if(facing==1&&source=="Idle"&&repeat==0&&last!=player.motor.sprite.sprite) { Capture(action+" frame "+frame); last=player.motor.sprite.sprite; }
                    Step();
                }
                player.RequestGuard(false); player.animationDriver.animator.Update(0);
                Check(player.State==CombatState.Idle,action+" returns to Idle"); Grounded(action+" -> Idle");
                if(action=="Dodge") Check(Mathf.Abs(player.transform.position.x-startX+facing*1.2f)<.0001f,"Dodge retains its existing 1.2-unit horizontal travel");
                // Reset only horizontal position between repeated sequences.
                player.transform.position=new Vector3(0,player.transform.position.y,0);
            }
            Directory.CreateDirectory("Documentation"); contactSheet.Apply();
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null) File.WriteAllBytes("Documentation/DefenseGroundingPreview.png",contactSheet.EncodeToPNG());
            File.WriteAllLines("Documentation/DefenseGroundingValidationResults.txt",results);
            Debug.Log("DEFENSE GROUNDING VALIDATION PASSED: "+results.FindAll(line=>line.StartsWith("PASS:")).Count+" checks");
            if(Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch(Exception ex)
        {
            results.Add("FAIL: "+ex);Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/DefenseGroundingValidationResults.txt",results);Debug.LogException(ex);
            if(Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
        }
    }
    static void Capture(string label)
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null||panel>=14)return;
        preview.transform.position=new Vector3(player.transform.position.x,.6f,-10);
        var target=new RenderTexture(256,256,24); var active=RenderTexture.active;
        preview.targetTexture=target;preview.Render();RenderTexture.active=target;
        int x=panel%7*256,y=(1-panel/7)*256;
        contactSheet.ReadPixels(new Rect(0,0,256,256),x,y);
        // Test-artifact reference line only; never placed in the gameplay scene.
        int groundY=y+32;
        for(int px=0;px<256;px++) if(px<85||px>170) contactSheet.SetPixel(x+px,groundY,Color.cyan);
        results.Add("PREVIEW panel "+(panel+1)+": "+label);panel++;
        preview.targetTexture=null;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);
    }
}
