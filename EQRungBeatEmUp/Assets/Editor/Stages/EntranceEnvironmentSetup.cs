using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EntranceEnvironmentSetup
{
    const string Request="Tools/EntranceRevision/setup.request";
    const string Environment="Assets/EQ_Rung_BeatEmUp/ArtAssets/Environments/HauntedHouse/Stage01_EntranceGate/";
    public const string Fence="EntranceFence", Motorcycle="EntranceMotorcycle";
    static EntranceEnvironmentSetup() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        const string validate="Tools/EntranceRevision/validate.request";
        if(File.Exists(validate)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode)
        { File.Delete(validate); EntranceEnvironmentValidation.Run(); }
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Build(); EntranceEnvironmentValidation.Run(); File.WriteAllText("Tools/EntranceRevision/setup.result","PASS: entrance artwork, destructible props and catalog configured."); }
        catch(Exception e) { File.WriteAllText("Tools/EntranceRevision/setup.result",e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Beat Em Up/Stages/Configure extended Entrance Gate art and props")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var background=Import(Environment+"Background/Stage01_EntranceGate_Background.png",false);
        var floor=Import(Environment+"Floor/Stage01_EntranceGate_Floor.png",false);
        foreach(var name in new[]{Fence,Motorcycle})
            foreach(var state in new[]{"Intact","Damaged","Destroyed","Debris1","Debris2","Debris3"})
                Import(DestructiblePropSetup.Art+name+"_"+state+".png",true);
        var fence=Prop(Fence,3,new Vector2(1.5f,.95f),new Vector2(1.4f,.18f));
        var motorcycle=Prop(Motorcycle,6,new Vector2(1.8f,1.12f),new Vector2(1.5f,.3f));
        var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
        var stage=level.stages.First(s=>s.stageId=="Stage01_EntranceGate");
        Undo.RecordObject(level,"Extend entrance and place breakable steel props");
        stage.backgroundSprite=background; stage.floorSprite=floor;
        stage.backgroundHeight=6.4f; stage.backgroundCenterY=4.12786875f;
        stage.floorHeight=3.675875f; stage.floorCenterY=-.90006875f;
        stage.destructibles.RemoveAll(p=>p.label.StartsWith("Entrance breakable:"));
        foreach(float x in new[]{-5.45f,-3.8f,-2.15f,2.15f,3.8f,5.45f})
            Place(stage,fence,"Fence "+x,new Vector2(x,.6f),3);
        Place(stage,motorcycle,"Parked motorcycle",new Vector2(4.65f,-.05f),6);
        EditorUtility.SetDirty(level); AssetDatabase.SaveAssets();
        // Keep the standalone art-preview prefab consistent with runtime placement.
        var path=Environment+"Unity/Stage01_EntranceGate_ArtPreview.prefab";
        var preview=PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach(var r in preview.GetComponentsInChildren<SpriteRenderer>())
            {
                bool bg=r.name=="Background"; r.sprite=bg?background:floor;
                float height=bg?stage.backgroundHeight:stage.floorHeight;
                r.transform.localPosition=new Vector3(0,bg?stage.backgroundCenterY:stage.floorCenterY,0);
                r.transform.localScale=new Vector3(stage.artWidth/r.sprite.bounds.size.x,height/r.sprite.bounds.size.y,1);
            }
            PrefabUtility.SaveAsPrefabAsset(preview,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(preview); }
        // Rebuild the existing catalog/hash so online replicas can render every new state.
        MultiplayerSetup.Build(); AssetDatabase.SaveAssets();
    }
    static Sprite Import(string path,bool prop)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(!importer) throw new Exception("Missing entrance art: "+path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.GetSourceTextureWidthAndHeight(out int width,out int height);
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Custom;
        settings.spritePivot=new Vector2(.5f,prop&&!path.Contains("Debris")?8f/height:.5f);
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite Art(string name,string state)=>AssetDatabase.LoadAssetAtPath<Sprite>(DestructiblePropSetup.Art+name+"_"+state+".png");
    static GameObject Prop(string name,int health,Vector2 hurtSize,Vector2 footprintSize)
    {
        var root=new GameObject(name);
        try
        {
            var prop=root.AddComponent<DestructibleObject>();
            var child=new GameObject("Visual"); child.transform.SetParent(root.transform,false);
            prop.visual=child.AddComponent<SpriteRenderer>(); prop.intactSprite=Art(name,"Intact");
            prop.damagedSprite=Art(name,"Damaged"); prop.brokenSprite=Art(name,"Destroyed");
            prop.visual.sprite=prop.intactSprite; prop.maximumHealth=health; prop.useHitPoints=true;
            prop.destructionSprites=new[]{prop.damagedSprite,prop.brokenSprite};
            prop.debrisSprites=Enumerable.Range(1,3).Select(i=>Art(name,"Debris"+i)).ToArray();
            prop.debrisCount=name==Fence?8:12; prop.debrisForce=1.4f; prop.debrisLifetime=3;
            prop.hitSfx=AssetDatabase.LoadAssetAtPath<AudioClip>(DestructiblePropSetup.Art+"MetalHit.wav");
            prop.destructionSfx=AssetDatabase.LoadAssetAtPath<AudioClip>(DestructiblePropSetup.Art+"MetalBreak.wav");
            var punch=AssetDatabase.LoadAssetAtPath<AttackData>("Assets/EQ_Rung_BeatEmUp/Attacks/Punch1.asset");
            prop.hitVfx=prop.destructionVfx=punch.feedback.impactPrefab;
            var hurt=root.GetComponent<BoxCollider2D>(); hurt.isTrigger=true; hurt.size=hurtSize; hurt.offset=new Vector2(0,hurtSize.y*.5f);
            var footprint=new GameObject("Ground footprint"); footprint.transform.SetParent(root.transform,false);
            prop.movementBlocker=footprint.AddComponent<BoxCollider2D>(); prop.movementBlocker.size=footprintSize;
            var wall=footprint.AddComponent<CombatWall>(); wall.allowsBounce=false; wall.debugDraw=false;
            return PrefabUtility.SaveAsPrefabAsset(root,DestructiblePropSetup.Prefabs+name+".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void Place(StageSegmentDefinition stage,GameObject prefab,string label,Vector2 position,int health)
    {
        var hurt=prefab.GetComponent<BoxCollider2D>();
        stage.destructibles.Add(new DestructiblePlacement{label="Entrance breakable: "+label,prefab=prefab,position=position,health=health,hitboxSize=hurt.size,hitboxOffset=hurt.offset});
    }
}
