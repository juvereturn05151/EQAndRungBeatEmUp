using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp.Story;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CreamCaptureAnimationSetup
{
    public const string CreamArt = "Assets/EQ_Rung_BeatEmUp/ArtAssets/Story/CreamAnimations/Cream_Capture.png";
    public const string CaptorArt = "Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Capture/ThaiBadBoy_Capture.png";
    static CreamCaptureAnimationSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/CreamCapture.setup-request"))return;
        File.Delete("Temp/CreamCapture.setup-request");
        try { Repair(); }
        catch(Exception error) { Directory.CreateDirectory("Documentation");File.WriteAllText("Documentation/CreamCaptureValidationResults.txt","FAIL: "+error);Debug.LogException(error); }
    }
    [MenuItem("Beat Em Up/Story/Repair Cream capture animations")]
    public static void Repair()
    {
        var definition=AssetDatabase.LoadAssetAtPath<PrologueDefinition>(PrologueSetup.Root+"/Prologue.asset");
        Configure(definition);AssetDatabase.SaveAssets();Validate(definition);RenderReview(definition);
        Debug.Log("Cream and Thai captor capture animations installed and validated.");
    }
    public static void Configure(PrologueDefinition definition)
    {
        if(!definition || !definition.kidnapping)throw new Exception("Cream capture timeline is missing.");
        var cream=Import(CreamArt,"Cream",new[]{"Startled","Escort","Struggle","Nervous"},255,new Vector2(.5f,.023f),new[]{254,503,753,1009});
        var captor=Import(CaptorArt,"ThaiCaptor",new[]{"Grab","Escort","Backstep","Hold"},230,new Vector2(.4f,.03f),new[]{247,503,758,1014});
        const string walkFolder="Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Walk";
        var walk=Directory.GetFiles(walkFolder,"ThaiBadBoy_Walk_*.png").Where(p=>!p.EndsWith("_Sheet.png")).OrderBy(p=>p).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).ToArray();
        var idle=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Idle/ThaiBadBoy_Idle_01.png");
        var events=definition.kidnapping.events;
        int begin=events.FindIndex(e=>e.action==StoryAction.Spawn && e.target=="Cream");
        if(begin<0)throw new Exception("Cream's capture spawn is missing.");
        int end=events.FindIndex(begin,e=>e.action==StoryAction.Despawn && e.target=="Cream");
        if(end<begin)throw new Exception("Capture scene boundaries are missing.");
        var replacement=new[]{
            Spawn("Cream",.3f,.12f,definition.cream),Face("Cream",1),Pose("Cream",cream[3],6),Wait(.8f),
            Spawn("Captor1",-3.1f,0,idle),Spawn("Captor2",3.1f,0,idle),
            Pose("Captor1",walk,12),Pose("Captor2",walk,12),
            Move("Captor1",-.54f,0,1.2f,1),Move("Captor2",1.21f,0,1.2f,1),
            Face("Captor1",1),Face("Captor2",-1),
            Pose("Cream",cream[0],10,true),Pose("Captor1",captor[0],10,true),Pose("Captor2",captor[0],10,true),Wait(.6f),
            Pose("Cream",cream[2],8),Pose("Captor1",captor[3],8),Pose("Captor2",captor[3],8),Wait(.75f),
            Pose("Cream",cream[1],8),Pose("Captor1",captor[1],8),Pose("Captor2",captor[2],8),
            Move("Captor1",4.96f,0,2.8f,2,true),Move("Cream",5.8f,.12f,2.8f,2,true),Move("Captor2",6.71f,0,2.8f,2,true)
        };
        events.RemoveRange(begin,end-begin);events.InsertRange(begin,replacement);
        EditorUtility.SetDirty(definition.kidnapping);
    }
    static CutsceneEvent Spawn(string actor,float x,float y,Sprite sprite)=>new CutsceneEvent{action=StoryAction.Spawn,target=actor,position=new Vector3(x,y),sprite=sprite,duration=0};
    static CutsceneEvent Face(string actor,float direction)=>new CutsceneEvent{action=StoryAction.Face,target=actor,amount=direction,duration=0};
    static CutsceneEvent Pose(string actor,Sprite[] frames,float speed,bool hold=false)=>new CutsceneEvent{action=StoryAction.Pose,target=actor,frames=frames,framesPerSecond=speed,holdLastFrame=hold,duration=0};
    static CutsceneEvent Wait(float seconds)=>new CutsceneEvent{action=StoryAction.Wait,duration=seconds};
    static CutsceneEvent Move(string actor,float x,float y,float seconds,int group,bool preserve=false)=>new CutsceneEvent{action=StoryAction.Move,target=actor,position=new Vector3(x,y),duration=seconds,parallelGroup=group,preserveFacing=preserve};
    static Sprite[][] Import(string path,string prefix,string[] rows,float ppu,Vector2 pivot,int[] baselines)
    {
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=ppu;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
        var slices=new List<SpriteMetaData>();
        for(int row=0;row<4;row++)for(int column=0;column<6;column++)
            slices.Add(new SpriteMetaData{name=prefix+"_"+rows[row]+"_"+(column+1).ToString("00"),rect=new Rect(column*256,1024-(row+1)*256,256,256),alignment=9,pivot=new Vector2(pivot.x,((row+1)*256-baselines[row])/256f)});
        importer.spritesheet=slices.ToArray();importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        var result=rows.Select(row=>sprites.Where(s=>s.name.StartsWith(prefix+"_"+row+"_")).OrderBy(s=>s.name).ToArray()).ToArray();
        if(result.Any(row=>row.Length!=6))throw new Exception("Missing capture frames: "+path);
        return result;
    }
    static void Validate(PrologueDefinition definition)
    {
        var results=new List<string>();
        Action<bool,string> check=(condition,message)=>{if(!condition)throw new Exception(message);results.Add("PASS: "+message);};
        var sequence=definition.kidnapping;
        foreach(string actor in new[]{"Cream","Captor1","Captor2"})
        {
            var poses=sequence.events.Where(e=>e.action==StoryAction.Pose && e.target==actor).ToArray();
            check(poses.Length==4,actor+" has approach, grab reaction, struggle/hold and escort animations.");
            check(poses.All(e=>e.frames.Length>=6 && e.frames.All(s=>s)),actor+" has complete sprite references.");
        }
        var moves=sequence.events.Where(e=>e.action==StoryAction.Move && e.parallelGroup==2).ToArray();
        check(moves.Length==3 && moves.All(e=>e.preserveFacing && e.duration==2.8f),"All three actors escort together while preserving inward facing.");
        var schedule=PrologueSequencePreview.Schedule(sequence,definition.dialogue,3);
        var start=schedule.First(s=>sequence.events[s.index].frames?.FirstOrDefault()?.name.StartsWith("Cream_Startled_")==true).start;
        var escort=schedule.First(s=>sequence.events[s.index].action==StoryAction.Move && sequence.events[s.index].parallelGroup==2).start;
        foreach(float time in new[]{start+.5f,escort+.1f,escort+1.4f})
        {
            var frame=PrologueSequencePreview.Evaluate(definition,sequence,time);
            check(frame.actors["Captor2"].flip && !frame.actors["Captor1"].flip,"Captors keep facing inward at "+time.ToString("F2")+"s.");
            check(frame.actors["Cream"].visible && !frame.actors["EQ"].visible && !frame.actors["Rung"].visible,"Cream's capture remains visible with heroes absent.");
        }
        var frames=sequence.events.First(e=>e.frames?.FirstOrDefault()?.name.StartsWith("Cream_Startled_")==true).frames;
        var go=new GameObject("Capture frame validation");
        try
        {
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=definition.cream;
            var animation=go.AddComponent<StorySpriteAnimation>();animation.Play(frames,10,true);
            check(renderer.sprite==frames[0],"Capture starts immediately at its first frame.");
            animation.Advance(.31f);check(renderer.sprite==frames[3],"Capture advances at authored frame rate.");
            animation.Advance(2);check(renderer.sprite==frames[5],"One-shot grabbing holds its final frame.");
            animation.Play(frames,10);check(renderer.sprite==frames[0],"Switching animations resets their clocks.");
            animation.Advance(.71f);check(renderer.sprite==frames[1],"Escort and struggle animations loop.");
            animation.Stop();check(renderer.sprite==definition.cream && animation.frames==null,"Stopping capture restores Cream's original pose for rescue/replay.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        var final=PrologueSequencePreview.Evaluate(definition,sequence,PrologueSequencePreview.Length(schedule)+1);
        check(new[]{"Cream","Captor1","Captor2"}.All(a=>!final.actors[a].visible),"Completion/skip final events remove Cream and both captors.");
        Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/CreamCaptureValidationResults.txt",results.Concat(new[]{"RESULT: PASS"}));
    }
    static void RenderReview(PrologueDefinition definition)
    {
        string output="Documentation/CreamCapturePreview";Directory.CreateDirectory(output);
        var sequence=definition.kidnapping;var schedule=PrologueSequencePreview.Schedule(sequence,definition.dialogue,3);
        float grab=schedule.First(s=>sequence.events[s.index].frames?.FirstOrDefault()?.name.StartsWith("Cream_Startled_")==true).start;
        float escort=schedule.First(s=>sequence.events[s.index].action==StoryAction.Move && sequence.events[s.index].parallelGroup==2).start;
        foreach(var sample in new[]{("Grab",grab+.5f),("Struggle",grab+.95f),("Escort",escort+.3f)})
        {
            var frame=PrologueSequencePreview.Evaluate(definition,sequence,sample.Item2);
            var root=new GameObject("Cream capture review");var camera=root.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=.9f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.10f,.13f);
            camera.cullingMask=1<<31;camera.transform.position=new Vector3(frame.actors["Cream"].position.x,.55f,-10);
            var target=new RenderTexture(960,540,24);var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                foreach(string name in new[]{"Cream","Captor1","Captor2"})
                {
                    var actor=frame.actors[name];var go=new GameObject(name);go.layer=31;go.transform.SetParent(root.transform);go.transform.position=actor.position;
                    var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=actor.sprite;renderer.flipX=actor.flip;renderer.sortingOrder=name=="Cream" ? 0 : 1;
                }
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image=new Texture2D(960,540,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
                File.WriteAllBytes(output+"/"+sample.Item1+".png",image.EncodeToPNG());
            }
            finally { RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
