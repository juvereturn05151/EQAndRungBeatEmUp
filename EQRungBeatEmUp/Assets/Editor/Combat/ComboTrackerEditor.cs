using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ComboTracker))]
public sealed class ComboTrackerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        var combo = (ComboTracker)target;
        EditorGUILayout.LabelField("Active", combo.IsActive.ToString());
        EditorGUILayout.LabelField("Hits / damage", combo.HitCount + " / " + combo.TotalDamage.ToString("0.##"));
        EditorGUILayout.LabelField("Remaining seconds", combo.RemainingSeconds.ToString("0.00"));
        EditorGUILayout.LabelField("Best hits", combo.BestHitCount.ToString());
        EditorGUILayout.LabelField("Last end", combo.LastEndReason ?? "—");
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Last target", combo.LastTarget, typeof(CharacterHealth), true);
        if (GUILayout.Button("Reset combo display")) combo.ResetTracking();
        Repaint();
    }
}
