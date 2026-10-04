using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorldRewardSetup
{
    public const string ArtPath = "Assets/ArtAssets/Props/RewardChapel/RewardChapel.png";
    public const string ChapelPath = "Assets/EQ_Rung_BeatEmUp/Prefabs/Rewards/RewardChapel.prefab";
    public const string ChoicePath = "Assets/EQ_Rung_BeatEmUp/Prefabs/Rewards/RewardChoice.prefab";
    public static void Attach(StageFlowController flow)
    {
        var world = flow.GetComponent<RewardSelectionController>(); if (!world) world = flow.gameObject.AddComponent<RewardSelectionController>();
        if (!world.chapelPrefab) world.chapelPrefab = AssetDatabase.LoadAssetAtPath<RewardChapelInteractable>(ChapelPath);
        if (!world.choicePrefab) world.choicePrefab = AssetDatabase.LoadAssetAtPath<RewardChoiceWorldObject>(ChoicePath);
        EditorUtility.SetDirty(world);
    }
    [MenuItem("Beat Em Up/Upgrades/Set up world chapel rewards")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Path.GetDirectoryName(ChapelPath)); AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 512; importer.spritePivot = new Vector2(.5f, 0);
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, 0); importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048; importer.SaveAndReimport();
        var chapel = AssetDatabase.LoadAssetAtPath<RewardChapelInteractable>(ChapelPath);
        if (!chapel)
        {
            var root = new GameObject("Reward Chapel"); var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            float scale = 2 / renderer.sprite.bounds.size.y; root.transform.localScale = Vector3.one * scale; root.AddComponent<RewardChapelInteractable>();
            PrefabUtility.SaveAsPrefabAsset(root, ChapelPath); Object.DestroyImmediate(root); chapel = AssetDatabase.LoadAssetAtPath<RewardChapelInteractable>(ChapelPath);
        }
        var choice = AssetDatabase.LoadAssetAtPath<RewardChoiceWorldObject>(ChoicePath);
        if (!choice) { var root = new GameObject("Reward Choice"); root.AddComponent<RewardChoiceWorldObject>(); PrefabUtility.SaveAsPrefabAsset(root, ChoicePath); Object.DestroyImmediate(root); choice = AssetDatabase.LoadAssetAtPath<RewardChoiceWorldObject>(ChoicePath); }
        var scene = EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); var flow = Object.FindFirstObjectByType<StageFlowController>();
        Attach(flow); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("WORLD REWARD SETUP COMPLETE: chapel and choice prefabs assigned; existing stage data preserved.");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
