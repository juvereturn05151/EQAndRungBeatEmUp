using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public static class BossMeleeAuthoring
{
    public static void Settings(BossEncounterData data)
    {
        EditorGUILayout.LabelField("Close-range telegraph / interrupts", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Attack timing and cooldown live in Attack Data. Interrupt frames are zero-based and inclusive. Parry cancels physical attacks without opening the totem shield. Available weights are normalized after range, phase, cooldown and summon-cap checks.", MessageType.Info);
        var serialized = new SerializedObject(data); serialized.Update();
        foreach (string field in new[] { "useDistanceWeights", "closeRange", "meleeLaneTolerance", "teleportCooldownFrames", "meleeInterrupt" }) EditorGUILayout.PropertyField(serialized.FindProperty(field), true);
        serialized.ApplyModifiedProperties();
    }
    public static void Phase(BossEncounterData data, string field)
    {
        var serialized = new SerializedObject(data); serialized.Update(); var choices = serialized.FindProperty(field);
        for (int i = 0; i < choices.arraySize; i++)
        {
            var row = choices.GetArrayElementAtIndex(i); EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (string property in new[] { "action", "enabled", "attack" }) EditorGUILayout.PropertyField(row.FindPropertyRelative(property));
            EditorGUILayout.PropertyField(row.FindPropertyRelative("closeRangeWeight"), new GUIContent("Close Range Weight (-1 = inherit)"));
            EditorGUILayout.PropertyField(row.FindPropertyRelative("weight"), new GUIContent("Far Range Weight"));
            var attack = row.FindPropertyRelative("attack").objectReferenceValue as AttackData;
            if (attack)
            {
                var timeline = new SerializedObject(attack); timeline.Update(); EditorGUILayout.PropertyField(timeline.FindProperty("cooldownFrames"), new GUIContent("Attack Cooldown Frames")); timeline.ApplyModifiedProperties();
                if (GUILayout.Button("Open frame timeline / hitboxes")) { Selection.activeObject = attack; EditorApplication.ExecuteMenuItem("Tools/Combat/Attack Data Editor"); }
            }
            EditorGUILayout.EndVertical();
        }
        serialized.ApplyModifiedProperties();
    }
    public static void Live(TotemBossController boss)
    {
        if (!Application.isPlaying || !boss || !boss.data) return;
        var choices = (boss.Phase2 ? boss.data.phase2 : boss.data.phase1).Where(boss.ActionAvailable).ToArray(); float total = choices.Sum(boss.EffectiveWeight);
        EditorGUILayout.LabelField("Live boss", boss.State + " / distance " + boss.TargetDistance.ToString("F2"));
        EditorGUILayout.LabelField("Stagger / interrupt protection", boss.StaggerRemaining + "f / " + boss.InterruptImmunityRemaining + "f");
        foreach (var choice in choices) EditorGUILayout.LabelField(choice.action.ToString(), (total > 0 ? 100 * boss.EffectiveWeight(choice) / total : 0).ToString("F1") + "% available");
    }
}
