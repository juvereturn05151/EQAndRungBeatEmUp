using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MultiplayerSetup
{
    public const string MenuPath="Assets/EQ_Rung_BeatEmUp/Scenes/MainMenu.unity";
    public const string CatalogPath="Assets/EQ_Rung_BeatEmUp/Resources/MultiplayerCatalog.asset";
    [MenuItem("Beat Em Up/Multiplayer/Refresh multiplayer asset catalog")]
    public static void Build()
    {
        if(EditorApplication.isPlaying) { Debug.LogWarning("Refresh the multiplayer catalog outside Play mode."); return; }
        Directory.CreateDirectory("Assets/EQ_Rung_BeatEmUp/Resources"); AssetDatabase.Refresh();
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(CatalogPath);
        if(!catalog) { catalog=ScriptableObject.CreateInstance<MultiplayerCatalog>(); AssetDatabase.CreateAsset(catalog,CatalogPath); }
        catalog.playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath);
        catalog.characters=AssetDatabase.FindAssets("t:PlayableCharacterData").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<PlayableCharacterData>).OrderBy(c=>c.sortOrder).ThenBy(c=>c.characterId,StringComparer.Ordinal).ToArray();
        catalog.level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
        catalog.sprites=AssetDatabase.FindAssets("t:Sprite").Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p=>p,StringComparer.Ordinal)
            .SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal)).ToArray();
        catalog.attacks=AssetDatabase.FindAssets("t:AttackData").Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<AttackData>).ToArray();
        catalog.menuBackground=catalog.level.stages[0].backgroundSprite;
        // Reject builds whose art indexing, player configuration or stage definitions do not match.
        var paths=AssetDatabase.GetDependencies(new[]{ComboTrackingSetup.PlayerPath,HauntedLevelBuilder.LevelPath}.Concat(catalog.characters.Select(AssetDatabase.GetAssetPath)).ToArray(),true).OrderBy(p=>p,StringComparer.Ordinal);
        string contents="GhostFairProtocol5|"+string.Join("|",paths.Select(p=>p+":"+AssetDatabase.GetAssetDependencyHash(p)))+"|"+string.Join("|",catalog.sprites.Select(s=>AssetDatabase.GetAssetPath(s)+":"+s.name));
        contents+="|"+string.Join("|",catalog.selectionCharacters.Where(c=>c).Select(c=>AssetDatabase.GetAssetPath(c)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(c))));
        contents+="|"+string.Join("|",Directory.GetFiles("Assets/EQ_Rung_BeatEmUp/Scripts","*.cs",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p+":"+File.ReadAllText(p)))+"|"+File.ReadAllText("Packages/manifest.json");
        using(var hash=SHA256.Create()) catalog.contentHash=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(contents))).Replace("-","");
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        if(!File.Exists(MenuPath))
        {
            var previous=SceneManagerSetup();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Menu Camera"); camera.tag="MainCamera"; var view=camera.AddComponent<Camera>(); view.backgroundColor=new Color(.02f,.025f,.04f); view.clearFlags=CameraClearFlags.SolidColor; camera.AddComponent<AudioListener>();
            var go=new GameObject("Main Menu"); go.AddComponent<MainMenuBootstrap>().catalog=catalog;
            EditorSceneManager.SaveScene(scene,MenuPath);
            if(!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=MenuPath).ToList(); scenes.Insert(0,new EditorBuildSettingsScene(MenuPath,true)); EditorBuildSettings.scenes=scenes.ToArray();
        Debug.Log("MULTIPLAYER SETUP: "+catalog.sprites.Length+" sprites, "+catalog.attacks.Length+" attacks; main menu first in build.");
    }
    static SceneSetup[] SceneManagerSetup() => EditorSceneManager.GetSceneManagerSetup();
    [MenuItem("Beat Em Up/Multiplayer/Open main menu")]
    public static void Open()
    {
        if(EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if(!File.Exists(MenuPath)) Build(); EditorSceneManager.OpenScene(MenuPath);
    }
}
