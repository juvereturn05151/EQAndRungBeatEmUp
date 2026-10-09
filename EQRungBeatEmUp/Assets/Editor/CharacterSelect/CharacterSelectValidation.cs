using System.IO;
using System;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class CharacterSelectValidation
{
    const string Pending="GhostFair.CharacterSelectValidation";
    static bool running;
    [Serializable] sealed class SavedScenes { public string[] paths; public bool[] loaded, active; }
    static CharacterSelectValidation() { EditorApplication.update+=Poll; EditorApplication.playModeStateChanged+=StateChanged; }
    static void StateChanged(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode) return;
        string saved=SessionState.GetString(Pending+".Scenes",""); if(string.IsNullOrEmpty(saved)) return;
        SessionState.EraseString(Pending+".Scenes");
        var scenes=UnityEngine.JsonUtility.FromJson<SavedScenes>(saved);
        EditorApplication.delayCall+=()=>EditorSceneManager.RestoreSceneManagerSetup(scenes.paths.Select((path,i)=>new SceneSetup {path=path,isLoaded=scenes.loaded[i],isActive=scenes.active[i]}).ToArray());
    }
    [MenuItem("Beat Em Up/Character Select/Validate selection and spawning (Play Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if(EditorSceneManager.GetActiveScene().isDirty) { UnityEngine.Debug.LogWarning("Save the current scene before running selection validation."); return; }
        var previous=EditorSceneManager.GetSceneManagerSetup();
        SessionState.SetString(Pending+".Scenes",UnityEngine.JsonUtility.ToJson(new SavedScenes {paths=previous.Select(s=>s.path).ToArray(),loaded=previous.Select(s=>s.isLoaded).ToArray(),active=previous.Select(s=>s.isActive).ToArray()}));
        EditorSceneManager.OpenScene(MultiplayerSetup.MenuPath);
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(File.Exists("Temp/CharacterSelect.validate-request") && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        { File.Delete("Temp/CharacterSelect.validate-request"); Run(); }
        if(EditorApplication.isPlaying && SessionState.GetBool(Pending,false) && MultiplayerSession.Active)
        { SessionState.SetBool(Pending,false); CharacterSelectValidationRun.Finished=false; CharacterSelectValidationRun.Passed=false; running=true; new UnityEngine.GameObject("Character Select integration validation").AddComponent<CharacterSelectValidationRun>(); }
        if(running && CharacterSelectValidationRun.Finished)
        { running=false; EditorApplication.ExitPlaymode(); }
    }
}
