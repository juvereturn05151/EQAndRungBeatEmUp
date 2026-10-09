using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DestructiblePropSetup
{
    public const string Art = "Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/Destructibles/";
    public const string Prefabs = "Assets/EQ_Rung_BeatEmUp/Prefabs/Props/";
    const string Request = "Tools/Destructibles/setup.request";
    static DestructiblePropSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Build(); File.WriteAllText("Tools/Destructibles/setup.result", "PASS: prefabs and Stage 1 placements created"); }
        catch(Exception e) { File.WriteAllText("Tools/Destructibles/setup.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Beat Em Up/Props/Build destructible props and Stage 1 samples")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach (var path in Directory.GetFiles(Art, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = path.Contains("Debris") ? new Vector2(.5f,.5f) : new Vector2(.5f,8f/96);
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        Directory.CreateDirectory(Prefabs);
        var box = BuildProp("CardboardBox",2); var jar = BuildProp("CeramicDragonJar",3);
        var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
        var stage = level.stages.First(s=>s.stageId=="Stage01_EntranceGate");
        Undo.RecordObject(level,"Place destructible samples");
        stage.destructibles.RemoveAll(p=>p.label.StartsWith("Destructible sample:"));
        Add(stage,box,"Box near entry",new Vector2(-3.9f,.35f),2);
        Add(stage,jar,"Jar lower lane",new Vector2(-1.6f,-.35f),3);
        Add(stage,box,"Box upper lane",new Vector2(.9f,.6f),2);
        Add(stage,jar,"Jar near exit",new Vector2(3.4f,-.25f),3);
        EditorUtility.SetDirty(level); AssetDatabase.SaveAssets();
    }
    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art+name+".png");
    static GameObject BuildProp(string name,int hp)
    {
        var go = new GameObject(name); var prop=go.AddComponent<DestructibleObject>();
        var visual=new GameObject("Visual"); visual.transform.SetParent(go.transform,false);
        prop.visual=visual.AddComponent<SpriteRenderer>();
        prop.intactSprite=Sprite(name+"_Intact"); prop.damagedSprite=Sprite(name+"_Damaged"); prop.brokenSprite=Sprite(name+"_Destroyed");
        prop.visual.sprite=prop.intactSprite; prop.maximumHealth=hp; prop.useHitPoints=true;
        prop.debrisSprites=Enumerable.Range(1,3).Select(i=>Sprite(name+"_Debris"+i)).ToArray();
        prop.destructionSprites=new[]{prop.damagedSprite,prop.brokenSprite};
        prop.debrisForce=name=="CardboardBox"?1.25f:1.7f; prop.debrisLifetime=3;
        string sound=name=="CardboardBox"?"Cardboard":"Ceramic";
        prop.hitSfx=AssetDatabase.LoadAssetAtPath<AudioClip>(Art+sound+"Hit.wav");
        prop.destructionSfx=AssetDatabase.LoadAssetAtPath<AudioClip>(Art+sound+"Break.wav");
        var attack=AssetDatabase.LoadAssetAtPath<AttackData>("Assets/EQ_Rung_BeatEmUp/Attacks/Punch1.asset");
        prop.hitVfx=prop.destructionVfx=attack ? attack.feedback.impactPrefab : null;
        var hurt=go.GetComponent<BoxCollider2D>(); hurt.isTrigger=true; hurt.size=new Vector2(.7f,.65f); hurt.offset=new Vector2(0,.32f);
        var footprint=new GameObject("Ground footprint"); footprint.transform.SetParent(go.transform,false);
        prop.movementBlocker=footprint.AddComponent<BoxCollider2D>(); prop.movementBlocker.size=new Vector2(.55f,.22f);
        var wall=footprint.AddComponent<CombatWall>(); wall.allowsBounce=false; wall.debugDraw=false;
        var asset=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+name+".prefab"); UnityEngine.Object.DestroyImmediate(go); return asset;
    }
    static void Add(StageSegmentDefinition stage,GameObject prefab,string label,Vector2 point,int hp)
    {
        var prop=prefab.GetComponent<DestructibleObject>(); var hurt=prefab.GetComponent<BoxCollider2D>();
        stage.destructibles.Add(new DestructiblePlacement { label="Destructible sample: "+label,prefab=prefab,position=point,health=hp,
            intactSprite=prop.intactSprite,damagedSprite=prop.damagedSprite,brokenSprite=prop.brokenSprite,hitboxSize=hurt.size,hitboxOffset=hurt.offset });
    }
}
