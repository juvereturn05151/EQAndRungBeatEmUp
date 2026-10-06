using System.Collections.Generic;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AttackCoordinationSetup
{
    public const string Root="Assets/EQ_Rung_BeatEmUp/Levels/AttackCoordination";
    public const string LevelPath=Root+"/AttackCoordinationTest.asset";
    public const string ScenePath="Assets/EQ_Rung_BeatEmUp/Scenes/AttackCoordinationTest.unity";
    public const string Prefabs="Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/";
    static AttackCoordinationData Data(AttackTokenCategory categories,bool ignore=false)=>new AttackCoordinationData{
        requiresMeleeSlot=(categories & AttackTokenCategory.Melee)!=0,requiresRangedSlot=(categories & AttackTokenCategory.Ranged)!=0,
        requiresCrowdControlSlot=(categories & AttackTokenCategory.CrowdControl)!=0,requiresSupportSlot=(categories & AttackTokenCategory.Support)!=0,ignoreCoordinator=ignore};
    [MenuItem("Beat Em Up/Enemies/Attack Coordination/Install category defaults and create test encounter")]
    public static void Build()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach(string guid in AssetDatabase.FindAssets("t:EnemyAIProfile",new[]{"Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/AI"}))
        {
            var profile=AssetDatabase.LoadAssetAtPath<EnemyAIProfile>(AssetDatabase.GUIDToAssetPath(guid)); Undo.RecordObject(profile,"Configure attack token categories");
            foreach(var choice in profile.attacks)
            {
                var categories=choice.projectile ? AttackTokenCategory.Ranged : AttackTokenCategory.Melee;
                if(profile.name=="ScreamerAIProfile") categories=AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl;
                if(profile.name=="GrapplerBruiserAIProfile" || profile.name=="AmbusherAIProfile") categories=AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl;
                if(profile.name=="PrefectAIProfile" && choice.id=="CallBackup") categories=AttackTokenCategory.Support;
                choice.coordination=Data(categories);
            }
            EditorUtility.SetDirty(profile);
        }
        foreach(string guid in AssetDatabase.FindAssets("t:BossEncounterData"))
        {
            var data=AssetDatabase.LoadAssetAtPath<BossEncounterData>(AssetDatabase.GUIDToAssetPath(guid)); Undo.RecordObject(data,"Configure boss coordinator bypass");
            foreach(var choice in data.phase1.Concat(data.phase2)) choice.coordination=Data(choice.action==BossAction.Book ? AttackTokenCategory.Ranged : choice.action==BossAction.CurseWave ? AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl : choice.action==BossAction.Swipe ? AttackTokenCategory.Melee : AttackTokenCategory.Support,true);
            EditorUtility.SetDirty(data);
        }
        if(!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/EQ_Rung_BeatEmUp/Levels","AttackCoordination");
        var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
        if(!level) { level=ScriptableObject.CreateInstance<LevelDefinition>(); AssetDatabase.CreateAsset(level,LevelPath); }
        var original=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath).stages.First(s=>s.stageId=="Stage01_EntranceGate");
        var stage=JsonUtility.FromJson<StageSegmentDefinition>(JsonUtility.ToJson(original));
        stage.stageId="AttackCoordinationTest"; stage.stageName="Attack Coordination Test"; stage.artWidth=15;
        stage.movementMin=new Vector2(-6,-.6f); stage.movementMax=new Vector2(6,.8f); stage.playerEntryPoint=Vector2.zero;
        stage.completionMode=StageCompletion.Event; stage.rewardAfterClear=StageReward.None; stage.nextStageIndex=-1; stage.destructibles.Clear(); stage.decorativeProps.Clear();
        var wave=new WaveDefinition{waveId="Seven mixed enemies"};
        string[] names={"Rusher","Rusher","Thrower","Screamer","GrapplerBruiser","Ambusher","Prefect"};
        Vector2[] points={new Vector2(-1,.2f),new Vector2(1,-.2f),new Vector2(-3,.1f),new Vector2(3,-.1f),new Vector2(-2,.4f),new Vector2(2,.4f),new Vector2(5,0)};
        for(int i=0;i<names.Length;i++) wave.enemySpawns.Add(new EnemySpawnDefinition{prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+names[i]+".prefab"),spawnPoints=new List<Vector2>{points[i]}});
        stage.encounters=new List<EncounterDefinition>{new EncounterDefinition{encounterId="Mixed attack pressure",maxActiveEnemies=10,attackCoordination=new AttackCoordinationSettings{showDebug=true},waves=new List<WaveDefinition>{wave}}};
        level.levelId="AttackCoordinationTest"; level.levelName="Attack Coordination Test"; level.stages=new List<StageSegmentDefinition>{stage}; EditorUtility.SetDirty(level); AssetDatabase.SaveAssets();
        var scene=EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); var flow=Object.FindFirstObjectByType<StageFlowController>(); flow.level=level;
        ClearHubPreview();
        var hub=flow.GetComponent<PlayerHubController>(); if(hub) hub.enabled=false;
        flow.ApplyStageArt(stage); flow.player.ResetForStage(stage.playerEntryPoint); flow.player.arenaMin=stage.movementMin; flow.player.arenaMax=stage.movementMax;
        flow.framing.SetStageBounds(-7.5f,7.5f); flow.framing.ApplyFraming(0,true); EditorSceneManager.SaveScene(scene,ScenePath);
        MultiplayerSetup.Build(); Debug.Log("ATTACK COORDINATION SETUP COMPLETE");
    }
    [MenuItem("Beat Em Up/Enemies/Attack Coordination/Open test encounter")]
    public static void Open() { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }
    static void ClearHubPreview() { foreach(var marker in Object.FindObjectsByType<HubLandmarks>(FindObjectsSortMode.None)) if(marker.editorPreview) Object.DestroyImmediate(marker.gameObject); }
    public static void FinalizeFixtureAndBuild()
    { var scene=EditorSceneManager.OpenScene(ScenePath); ClearHubPreview(); EditorSceneManager.SaveScene(scene); MultiplayerValidation.BuildDevelopmentPlayer(); }
}
