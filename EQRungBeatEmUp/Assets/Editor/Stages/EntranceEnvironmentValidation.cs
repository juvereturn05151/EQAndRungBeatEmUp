using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EntranceEnvironmentValidation
{
    [MenuItem("Beat Em Up/Stages/Validate extended Entrance Gate coverage")]
    public static void Run()
    {
        var results=new List<string>(); var scene=EditorSceneManager.NewPreviewScene();
        RenderTexture render=null; var previous=RenderTexture.active;
        try
        {
            var stage=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath).stages.First(s=>s.stageId=="Stage01_EntranceGate");
            var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
            var background=Plate(scene,"Background",stage.backgroundSprite,stage.artWidth,stage.backgroundHeight,stage.backgroundCenterY,-1000);
            var floor=Plate(scene,"Floor",stage.floorSprite,stage.artWidth,stage.floorHeight,stage.floorCenterY,-900);
            if(background.bounds.max.y<7.3f || floor.bounds.min.y> -2.7f || floor.bounds.max.y<background.bounds.min.y)
                throw new Exception("Extended art does not cover both directions or the layer seam.");
            results.Add("PASS: Background covers up to "+background.bounds.max.y+"; floor down to "+floor.bounds.min.y+"; original width remains "+stage.artWidth+".");
            var props=new List<GameObject>();
            foreach(var placement in stage.destructibles)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(placement.prefab,scene); go.transform.position=placement.position; props.Add(go);
                if(placement.label.StartsWith("Entrance breakable:"))
                {
                    var prop=go.GetComponent<DestructibleObject>();
                    if(!prop.useHitPoints || !prop.intactSprite || !prop.damagedSprite || !prop.brokenSprite || !prop.movementBlocker || !prop.hitSfx || !prop.destructionSfx)
                        throw new Exception("Missing breakable prop setup: "+placement.label);
                    var states=new[]{prop.intactSprite,prop.damagedSprite,prop.brokenSprite}.Concat(prop.debrisSprites);
                    if(states.Any(s=>!catalog.sprites.Contains(s)))throw new Exception("Missing online sprite catalog entry.");
                    results.Add("PASS: "+placement.label+" has intact/damaged/broken art, debris, SFX, attack trigger, ground blocker and multiplayer sprite entries.");
                }
            }
            var cameraObject=new GameObject("Entrance preview camera"); SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>(); camera.scene=scene; camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.magenta;
            render=new RenderTexture(960,540,24); camera.targetTexture=render;
            Directory.CreateDirectory("Documentation/EntranceRevisionPreview");
            Capture(camera,render,0,-.64f,3.36f,"Ground",results);
            Capture(camera,render,0,-.64f,6.3f,"Jump",results);
            // Show the complete expanded art while keeping horizontal view inside the plate.
            camera.rect=new Rect(.14f,0,.72f,1);
            Capture(camera,render,0,-2.6f,7.1f,"FullArt",results,false);
            camera.rect=new Rect(0,0,1,1);
            foreach(var prop in props) if(prop.name.StartsWith(EntranceEnvironmentSetup.Fence)||prop.name.StartsWith(EntranceEnvironmentSetup.Motorcycle))prop.SetActive(false);
            Capture(camera,render,0,-.64f,6.3f,"CleanBackground",results);
            results.Add("ALL ENTRANCE ART AND PROP AUTHORING CHECKS PASSED");
        }
        catch(Exception e){results.Add("FAIL: "+e);Debug.LogException(e);}
        finally
        {
            RenderTexture.active=previous;if(render){render.Release();UnityEngine.Object.DestroyImmediate(render);}
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllLines("Documentation/EntranceEnvironmentValidationResults.txt",results);Debug.Log(string.Join("\n",results));
        }
    }
    static SpriteRenderer Plate(Scene scene,string name,Sprite sprite,float width,float height,float center,int order)
    {
        var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene);
        var r=go.AddComponent<SpriteRenderer>(); r.sprite=sprite;r.sortingOrder=order;
        go.transform.position=new Vector3(0,center,0);go.transform.localScale=new Vector3(width/sprite.bounds.size.x,height/sprite.bounds.size.y,1);return r;
    }
    static void Capture(Camera camera,RenderTexture render,float x,float bottom,float top,string name,List<string> results,bool check=true)
    {
        camera.orthographicSize=(top-bottom)*.5f;camera.transform.position=new Vector3(x,(top+bottom)*.5f,-10);
        RenderTexture.active=render;GL.Clear(true,true,Color.black);
        camera.Render();RenderTexture.active=render;
        var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        try
        {
            image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();
            if(check && image.GetPixels32().Any(p=>p.r>250&&p.g<5&&p.b>250))throw new Exception("Exposed clear background in "+name);
            File.WriteAllBytes("Documentation/EntranceRevisionPreview/"+name+".png",image.EncodeToPNG());
            results.Add("PASS: "+name+" preview captured"+(check?" without exposed clear background.":"."));
        }
        finally{UnityEngine.Object.DestroyImmediate(image);}
    }
}
