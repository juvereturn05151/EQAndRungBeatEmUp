using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageFlowController))]
public sealed class StageFlowControllerEditor : Editor
{
    private int previewStage;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var flow = (StageFlowController)target;
        if (flow.level && GUILayout.Button("Edit level / ordered stages")) { Selection.activeObject = flow.level; EditorGUIUtility.PingObject(flow.level); }
        if (flow.level && flow.level.stages.Count > 0)
        {
            var names = flow.level.stages.ConvertAll(s => s.stageName).ToArray();
            previewStage = Mathf.Clamp(previewStage, 0, names.Length - 1);
            previewStage = EditorGUILayout.Popup("Preview Stage", previewStage, names);
            EncounterPreview.DrawInspector(flow.level, previewStage);
        }
        EditorGUILayout.HelpBox("Play HauntedHouse.unity. Walk to the right exit after clearing required waves. E near the shrine heals. R retries after death. F8 opens stage/wave debug controls. Select this object for bounds, spawn, entry/exit and prop gizmos.", MessageType.Info);
        if (!Application.isPlaying || !flow.level) return;
        EditorGUILayout.LabelField("Current stage", flow.StageIndex + 1 + ": " + flow.CurrentStage?.stageName);
        EditorGUILayout.LabelField("Exit", flow.ExitUnlocked ? "Open" : "Locked");
        for (int i = 0; i < flow.level.stages.Count; i++) if (GUILayout.Button("Jump to " + (i + 1) + ": " + flow.level.stages[i].stageName)) flow.RestartAt(i);
        if (GUILayout.Button("Advance if unlocked")) flow.TryAdvance();
    }
}
