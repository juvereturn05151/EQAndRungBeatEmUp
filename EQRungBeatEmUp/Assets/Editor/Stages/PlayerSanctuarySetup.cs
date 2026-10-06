using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PlayerSanctuarySetup
{
    public const string Root="Assets/EQ_Rung_BeatEmUp/Hub";
    public const string Art="Assets/ArtAssets/Environments/PlayerSanctuary";
    public const string ScenePath="Assets/EQ_Rung_BeatEmUp/Scenes/PlayerHub.unity";
    public const string DefinitionPath=Root+"/PlayerHub.asset";
    public const string EnvironmentPath=Root+"/SanctuaryEnvironment.prefab";
    [MenuItem("Beat Em Up/Hub/Create sanctuary defaults")]
    public static void Build()
    {
        if(EditorApplication.isPlaying) return;
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        var definition=AssetDatabase.LoadAssetAtPath<PlayerHubDefinition>(DefinitionPath);
        if(!definition) { definition=ScriptableObject.CreateInstance<PlayerHubDefinition>(); AssetDatabase.CreateAsset(definition,DefinitionPath); }
        definition.spawn=new Vector2(-15.2f,0); definition.stations=new[]{new Vector2(-7.6f,0),Vector2.zero,new Vector2(7.6f,0),new Vector2(15.2f,0)};
        definition.panels=Enumerable.Range(0,5).Select(i=>Import(Art+"/HubPanel"+i+".png")).ToArray();
        var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/HubPanelBlend.mat");
        if(!material) { material=new Material(Shader.Find("BeatEmUp/Hub Panel Blend")); AssetDatabase.CreateAsset(material,Root+"/HubPanelBlend.mat"); }
        material.SetFloat("_LeftBlend",.5f/(definition.width/5+.5f)); EditorUtility.SetDirty(material);
        definition.panelBlendMaterial=material;
        var root=new GameObject("Sanctuary Environment"); root.AddComponent<HubLandmarks>().definition=definition;
        try
        {
            for(int i=0;i<5;i++)
            {
                var go=new GameObject(new[]{"Buddha Sanctuary","Character Wardrobe","Training Blessing Pavilion","Skill Shrine","World 1 Entrance"}[i]); go.transform.SetParent(root.transform);
                var renderer=go.AddComponent<SpriteRenderer>(); renderer.sprite=definition.panels[i]; renderer.sortingOrder=-900+i;
                if(i>0) renderer.sharedMaterial=material;
                float scale=(definition.width/5+.5f)/renderer.sprite.bounds.size.x; go.transform.localScale=new Vector3(scale,scale,1);
                go.transform.position=new Vector3(-definition.width*.5f+(i+.5f)*definition.width/5,-.8f+renderer.sprite.bounds.size.y*scale*.5f,0);
            }
            var spawn=new GameObject("HubPlayerSpawnPoint"); spawn.transform.SetParent(root.transform); spawn.transform.position=definition.spawn;
            PrefabUtility.SaveAsPrefabAsset(root,EnvironmentPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath); var hub=level.stages[0];
        if(hub.stageId!=PlayerHubSetup.HubId) throw new Exception("Expected existing Hub as first stage; preserve level stage order.");
        hub.hub=definition; hub.artWidth=definition.width; hub.playerEntryPoint=definition.spawn; hub.playerExitPoint=definition.stations[3];
        hub.movementMin=new Vector2(-definition.width*.5f+.5f,-.4f); hub.movementMax=new Vector2(definition.width*.5f-.5f,.65f);
        hub.completionMode=StageCompletion.Event; hub.rewardAfterClear=StageReward.None; hub.stageType=StageType.Safe;
        hub.decorativeProps.RemoveAll(p=>p.prefab && (p.prefab.GetComponent<HubLandmarks>() || p.prefab.name=="Sanctuary Environment"));
        hub.decorativeProps.Add(new StagePropPlacement{prefab=AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath),position=Vector2.zero});
        hub.notes="Walkable sanctuary: Buddha spawn, character wardrobe, permanent base-stat pavilion, equipped-skill shrine, World 1 gate. E / Select interacts; gate starts run explicitly. Five distinct environment modules; no enemies/destructibles.";
        EditorUtility.SetDirty(level); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
        var scene=EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        ConfigureScene(level,definition); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.SaveScene(scene,ScenePath);
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath); catalog.gameplayScene="PlayerHub"; EditorUtility.SetDirty(catalog);
        var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToList(); scenes.Add(new EditorBuildSettingsScene(ScenePath,true)); EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets(); HubWorldPortalSetup.Build(); Debug.Log("PLAYER SANCTUARY SETUP COMPLETE: 38-unit Hub, four interactions, permanent progression and Buddha spawn.");
    }
    static Sprite Import(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); if(!importer) throw new Exception("Missing panel: "+path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single; importer.spritePixelsPerUnit=100;
        importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.mipmapEnabled=false; importer.maxTextureSize=4096;
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void ConfigureScene(LevelDefinition level,PlayerHubDefinition definition)
    {
        var flow=UnityEngine.Object.FindFirstObjectByType<StageFlowController>();
        var controller=flow.GetComponent<PlayerHubController>() ?? flow.gameObject.AddComponent<PlayerHubController>(); controller.definition=definition;
        foreach(var old in UnityEngine.Object.FindObjectsByType<HubLandmarks>(FindObjectsSortMode.None)) if(old.editorPreview) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var preview=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath));
        preview.name="Hub Editor Preview"; preview.tag="EditorOnly"; preview.GetComponent<HubLandmarks>().editorPreview=true;
        flow.ApplyStageArt(level.stages[0]); flow.player.ResetForStage(definition.spawn); flow.player.Face(1);
        flow.player.arenaMin=level.stages[0].movementMin; flow.player.arenaMax=level.stages[0].movementMax;
        flow.framing.player=flow.player; flow.framing.SetStageBounds(-definition.width*.5f,definition.width*.5f); flow.framing.ApplyFraming(0,true);
        EditorUtility.SetDirty(flow); EditorUtility.SetDirty(flow.player); EditorUtility.SetDirty(flow.framing);
    }
}
[CustomEditor(typeof(HubLandmarks))]
public sealed class HubLandmarksEditor : Editor
{
    void OnSceneGUI()
    {
        var marker=(HubLandmarks)target; var definition=marker.definition; if(!definition) return;
        Handles.Label(definition.spawn,"HubPlayerSpawnPoint →");
        EditorGUI.BeginChangeCheck(); var spawn=(Vector2)Handles.PositionHandle(definition.spawn,Quaternion.identity);
        if(EditorGUI.EndChangeCheck()) { Undo.RecordObject(definition,"Move Hub spawn"); definition.spawn=spawn; Save(definition); }
        string[] names={"Character","Base Stats","Skill Shrine","WORLD 1 ENTRANCE"};
        for(int i=0;i<definition.stations.Length;i++)
        {
            Handles.Label(definition.stations[i],names[i]); EditorGUI.BeginChangeCheck(); var point=(Vector2)Handles.PositionHandle(definition.stations[i],Quaternion.identity);
            if(EditorGUI.EndChangeCheck()) { Undo.RecordObject(definition,"Move Hub station"); definition.stations[i]=point; Save(definition); }
        }
    }
    static void Save(PlayerHubDefinition definition)
    {
        EditorUtility.SetDirty(definition); var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
        Undo.RecordObject(level,"Update Hub spawn and gate");
        level.stages[0].playerEntryPoint=definition.spawn; level.stages[0].playerExitPoint=definition.stations[3]; EditorUtility.SetDirty(level);
    }
}
