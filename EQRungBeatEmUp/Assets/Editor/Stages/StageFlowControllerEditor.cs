using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageFlowController))]
public sealed class StageFlowControllerEditor : Editor
{
    private readonly EncounterPreview.InspectorSelection previewSelection = new EncounterPreview.InspectorSelection();
    private void OnEnable() { EditorApplication.delayCall += StartPreview; }
    private void StartPreview() { if (this && target) StageEditorSelection.EnsurePreview(((StageFlowController)target).level); }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var flow = (StageFlowController)target;
        if (flow.level && GUILayout.Button("Edit level / ordered stages")) { Selection.activeObject = flow.level; EditorGUIUtility.PingObject(flow.level); }
        if (flow.level && flow.level.stages.Count > 0)
        {
            var names = flow.level.stages.ConvertAll(s => s.stageName).ToArray();
            int previewStage = StageEditorSelection.GetIndex(flow.level);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup("Editor Stage", previewStage, names);
            if (EditorGUI.EndChangeCheck()) StageEditorSelection.Select(flow.level, selected);
            StageEditorSelection.DrawControls(flow.level);
            previewStage = StageEditorSelection.GetIndex(flow.level);
            EncounterPreview.DrawInspector(flow.level, previewStage, previewSelection);
        }
        EditorGUILayout.HelpBox("Play HauntedHouse.unity. Walk to the right exit after clearing required waves. E near the shrine heals. R retries after death. F8 opens stage/wave debug controls. Select this object for bounds, spawn, entry/exit and prop gizmos.", MessageType.Info);
        if (!Application.isPlaying || !flow.level) return;
        EditorGUILayout.LabelField("Current stage", flow.StageIndex + 1 + ": " + flow.CurrentStage?.stageName);
        EditorGUILayout.LabelField("Exit", flow.ExitUnlocked ? "Open" : "Locked");
        for (int i = 0; i < flow.level.stages.Count; i++) if (GUILayout.Button("Jump to " + (i + 1) + ": " + flow.level.stages[i].stageName)) flow.RestartAt(i);
        if (GUILayout.Button("Advance if unlocked")) flow.TryAdvance();
    }
}
