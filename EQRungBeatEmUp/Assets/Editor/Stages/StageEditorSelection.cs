using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

// One authoring selection per level; never reads or writes runtime StageIndex.
public static class StageEditorSelection
{
    static string Key(LevelDefinition level)
    {
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level));
        return "BeatEmUp.StageSelection." + (string.IsNullOrEmpty(guid) ? level.GetInstanceID().ToString() : guid);
    }
    public static int GetIndex(LevelDefinition level)
    {
        if (!level || level.stages.Count == 0) return -1;
        string key = Key(level), id = SessionState.GetString(key + ".id", "");
        int index = SessionState.GetInt(key + ".index", 0);
        if (!string.IsNullOrEmpty(id) && level.stages.Count(s => s.stageId == id) == 1)
            index = level.stages.FindIndex(s => s.stageId == id);
        return Mathf.Clamp(index, 0, level.stages.Count - 1);
    }
    public static void Remember(LevelDefinition level, int index)
    {
        if (!level || index < 0 || index >= level.stages.Count) return;
        string key = Key(level);
        SessionState.SetInt(key + ".index", index);
        SessionState.SetString(key + ".id", level.stages[index].stageId ?? "");
    }
    public static void Select(LevelDefinition level, int index)
    {
        if (!level || index < 0 || index >= level.stages.Count) return;
        Remember(level, index);
        if (!Application.isPlaying) { EncounterPreview.BeginStage(level, index); EncounterPreview.FocusPreview(); }
    }
    public static void EnsurePreview(LevelDefinition level)
    {
        if (Application.isPlaying || !level || GetIndex(level) < 0) return;
        if (!EncounterPreview.IsPreviewing(level, GetIndex(level)))
        {
            EncounterPreview.BeginStage(level, GetIndex(level)); EncounterPreview.FocusPreview();
        }
    }
    public static void DrawControls(LevelDefinition level)
    {
        int index = GetIndex(level);
        if (index < 0) return;
        EditorGUILayout.HelpBox(Application.isPlaying
            ? "Editor selection is independent of the active runtime stage. Scene preview resumes after leaving Play Mode."
            : "Live Scene preview follows the selected stage. Asset edits and Undo refresh immediately. Preview objects never run gameplay scripts. Selection survives Inspector recreation and domain reload; runtime stage flow remains separate.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview Selected Stage")) Select(level, index);
                if (GUILayout.Button("Refresh Preview")) { EnsurePreview(level); EncounterPreview.RefreshNow(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(index == 0)) if (GUILayout.Button("Previous Stage")) Select(level, index - 1);
                using (new EditorGUI.DisabledScope(index == level.stages.Count - 1)) if (GUILayout.Button("Next Stage")) Select(level, index + 1);
                if (GUILayout.Button("Focus Selected Stage")) { EnsurePreview(level); EncounterPreview.FocusPreview(); }
            }
            if (GUILayout.Button("Clear Scene Preview")) EncounterPreview.Clear();
        }
    }
}
