using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerHubSetup
{
    public const string HubId = "Stage00_PlayerHub";
    public const string ArtPath = "Assets/ArtAssets/Environments/HauntedHouse/Stage00_PlayerHub/Stage00_PlayerHub.png";
    public static void Configure(LevelDefinition level)
    {
        ImportArt();
        if (!level.stages.Any(s => s.stageId == HubId))
        {
            // Preserve explicit routes, including an explicit end-of-level index.
            foreach (var stage in level.stages) if (stage.nextStageIndex >= 0) stage.nextStageIndex++;
            level.stages.Insert(0, new StageSegmentDefinition {
                stageId = HubId, stageName = "Player Hub", stageType = StageType.Safe,
                backgroundSprite = Pose("PlayerHub_Background"), floorSprite = Pose("PlayerHub_Floor"),
                artWidth = 7.6f, movementMin = new Vector2(-3.05f, -.4f), movementMax = new Vector2(3.05f, .65f),
                playerEntryPoint = new Vector2(-2.5f, 0), playerExitPoint = new Vector2(2.7f, 0),
                completionMode = StageCompletion.ReachExit, rewardAfterClear = StageReward.None,
                notes = "Warm outdoor camper hub. No encounters, damage, hazards or combat props. Walk right to enter Entrance Gate. DecorativeProps can host future camp/NPC interactions; safeRoom can enable the existing recovery interaction later. No run-build selection here."
            });
        }
        foreach (var stage in level.stages)
        {
            if (stage.safeRoom) stage.stageType = StageType.Safe;
            else if (stage.completionMode == StageCompletion.BossDefeated) stage.stageType = StageType.Boss;
            else if (stage.stageId == "Stage08_EscapeLane") stage.stageType = StageType.Exit;
        }
        EditorUtility.SetDirty(level);
    }
    private static Sprite Pose(string name) => AssetDatabase.LoadAllAssetsAtPath(ArtPath).OfType<Sprite>().Single(s => s.name == name);
    private static void ImportArt()
    {
        if (!File.Exists(ArtPath)) throw new Exception("Copy the supplied warm camper artwork to " + ArtPath);
        AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceSynchronousImport);
        if (AssetDatabase.LoadAllAssetsAtPath(ArtPath).OfType<Sprite>().Any(s => s.name == "PlayerHub_Background")) return;
        var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int floorHeight = Mathf.RoundToInt(height * .6f);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048; importer.mipmapEnabled = false;
#pragma warning disable 0618
        importer.spritesheet = new[] {
            new SpriteMetaData { name = "PlayerHub_Background", rect = new Rect(0, floorHeight, width, height - floorHeight), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) },
            new SpriteMetaData { name = "PlayerHub_Floor", rect = new Rect(0, 0, width, floorHeight), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) }
        };
#pragma warning restore 0618
        importer.SaveAndReimport();
    }
    [MenuItem("Beat Em Up/Stages/Add outdoor safe hub first")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
        Configure(level); AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        var flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); var hub = level.stages[0];
        flow.ApplyStageArt(hub);
        EditorUtility.SetDirty(flow.background); EditorUtility.SetDirty(flow.floor);
        flow.player.transform.position = hub.playerEntryPoint;
        flow.player.arenaMin = hub.movementMin; flow.player.arenaMax = hub.movementMax; EditorUtility.SetDirty(flow.player);
        if (flow.framing) { flow.framing.bottomLane = hub.movementMin.y; flow.framing.topLane = hub.movementMax.y; flow.framing.SetStageBounds(-hub.artWidth * .5f, hub.artWidth * .5f); flow.framing.ApplyFraming(0, true); EditorUtility.SetDirty(flow.framing); }
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("PLAYER HUB SETUP COMPLETE: safe outdoor hub first, followed by all eight haunted-house stages.");
    }
}
