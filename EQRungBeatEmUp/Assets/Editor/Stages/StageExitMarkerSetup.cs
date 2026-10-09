using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public static class StageExitMarkerSetup
{
    public const string Art = "Assets/EQ_Rung_BeatEmUp/ArtAssets/UI/ExitMarkers/";
    public const string Prefab = "Assets/EQ_Rung_BeatEmUp/Prefabs/StageExitMarker.prefab";
    [MenuItem("Beat Em Up/Stages/Set up Stage 1 next-area markers")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var arrows = Enumerable.Range(1,3).Select(n=>Import(Art+"ExitChevron_"+n.ToString("00")+".png")).ToArray();
        var ground=Import(Art+"ExitGround.png");var label=Import(Art+"ExitNEXT.png");
        var root=new GameObject("Stage Exit Marker");
        try
        {
            var marker=root.AddComponent<StageExitMarker>(); marker.arrowFrames=arrows;
            marker.arrow=Renderer(root,"Animated chevrons",arrows[0],new Vector3(0,.75f,0),600);
            marker.ground=Renderer(root,"Ground destination",ground,Vector3.zero,598);
            marker.nextLabel=Renderer(root,"NEXT",label,new Vector3(0,1.04f,0),601);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
        var stage=level.stages.First(s=>s.stageId=="Stage01_EntranceGate");
        bool newApproach=!stage.nextAreaMarkers.Any(m=>m.markerId=="Entrance approach");
        Add(stage,prefab,"Entrance approach",NextAreaTarget.EncounterEntry,stage.encounters[0].encounterId,NextAreaShowAfter.Immediately,"");
        Add(stage,prefab,"After first fight",NextAreaTarget.EncounterEntry,stage.encounters[1].encounterId,NextAreaShowAfter.EncounterComplete,stage.encounters[0].encounterId);
        Add(stage,prefab,"Stage 1 exit",NextAreaTarget.StageExit,"",NextAreaShowAfter.SequenceComplete,"");
        // Keep the entry cue inside the real trigger's open lower lane, clear of its authored box.
        var approach=stage.nextAreaMarkers.First(m=>m.markerId=="Entrance approach");
        if(newApproach)approach.markerOffset=new Vector3(0,-.4f,0);
        EditorUtility.SetDirty(level); AssetDatabase.SaveAssets();
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        catalog.sprites=catalog.sprites.Concat(arrows).Concat(new[]{ground,label}).Distinct().ToArray();
        using(var sha=SHA256.Create())catalog.contentHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
            "ExitMarkers1|"+AssetDatabase.GetAssetDependencyHash(HauntedLevelBuilder.LevelPath)+"|"+string.Join("|",catalog.sprites.Select(s=>s?AssetDatabase.GetAssetPath(s)+s.name:"null"))))).Replace("-","").ToLowerInvariant();
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
    }
    static void Add(StageSegmentDefinition stage,GameObject prefab,string id,NextAreaTarget target,string destination,NextAreaShowAfter after,string source)
    {
        if(stage.nextAreaMarkers.Any(m=>m.markerId==id))return;
        stage.nextAreaMarkers.Add(new NextAreaMarkerDefinition {markerId=id,markerPrefab=prefab,target=target,targetEncounterId=destination,showAfter=after,afterEncounterId=source});
    }
    static SpriteRenderer Renderer(GameObject root,string name,Sprite sprite,Vector3 position,int order)
    {
        var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=position;
        var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=order;return renderer;
    }
    static Sprite Import(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
