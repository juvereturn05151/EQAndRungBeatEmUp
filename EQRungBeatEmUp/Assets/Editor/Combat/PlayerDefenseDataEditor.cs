using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerDefenseData))]
public sealed class PlayerDefenseDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("parryWindowFrames"), new GUIContent("Parry Active Frames"));
        var data = (PlayerDefenseData)target;
        int window = serializedObject.FindProperty("parryWindowFrames").intValue;
        EditorGUILayout.HelpBox("Fresh Guard is immediately active on frames 0–" + (Mathf.Max(1, window) - 1) + ". Holding Guard transitions automatically to normal Guard; it cannot refresh the window.", MessageType.Info);
        DrawTrack(window);
        EditorGUILayout.LabelField("Counter advantage (base)", data.CounterAdvantageFrames + " frames = enemy stun minus successful-parry recovery");
        DrawPropertiesExcluding(serializedObject, "m_Script", "parryWindowFrames", "parryFeedback");
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Successful parry VFX / SFX", EditorStyles.boldLabel);
        var feedback = serializedObject.FindProperty("parryFeedback");
        foreach (var field in new[] { "impactSound", "impactVolume", "impactPrefab", "impactScale", "impactLifetime" })
            EditorGUILayout.PropertyField(feedback.FindPropertyRelative(field));
        serializedObject.ApplyModifiedProperties();
    }
    static void DrawTrack(int window)
    {
        window = Mathf.Max(1, window); int count = Mathf.Min(32, Mathf.Max(12, window + 4));
        var rect = GUILayoutUtility.GetRect(1, 56, GUILayout.ExpandWidth(true));
        float width = rect.width / count;
        for (int frame = 0; frame < count; frame++)
        {
            var cell = new Rect(rect.x + frame * width, rect.y, width - 1, 25);
            EditorGUI.DrawRect(cell, frame < window ? new Color(.15f,.6f,.3f) : new Color(.15f,.35f,.6f));
            GUI.Label(cell, frame.ToString(), EditorStyles.whiteMiniLabel);
        }
        GUI.Label(new Rect(rect.x, rect.y + 28, rect.width, 24), "Green: Parry active     Blue: Normal Guard", EditorStyles.miniLabel);
    }
}

[CustomEditor(typeof(ComboController))]
public sealed class ComboControllerParryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var player = (ComboController)target;
        if (!Application.isPlaying) return;
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Live parry / guard", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Player state", player.State.ToString());
        EditorGUILayout.LabelField("Stun remaining", player.StunFramesRemaining + " combat frames");
        EditorGUILayout.LabelField("Parry frame (zero-based)", player.DefenseFrame + " / " + player.EffectiveParryWindow);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Parry active", player.ParryActive);
            EditorGUILayout.Toggle("Guard active", player.GuardActive);
        }
        EditorGUILayout.LabelField("Re-arm remaining", player.ParryRearmRemaining + " frames");
        EditorGUILayout.LabelField("Counter advantage", player.CounterAdvantageFrames + " frames");
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
