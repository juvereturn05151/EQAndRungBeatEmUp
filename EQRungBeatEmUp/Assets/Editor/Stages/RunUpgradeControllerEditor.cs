using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RunUpgradeController))]
public sealed class RunUpgradeControllerEditor : Editor
{
    private UpgradeDefinition grant;
    private int stage;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var controller = (RunUpgradeController)target;
        EditorGUILayout.HelpBox("In-run rewards: configure Reward After Clear on the selected stage in LevelDefinition. Tab shows the build; F9 opens upgrade debug. Cards accept mouse, 1/2/3, or gamepad D-pad + South.", MessageType.Info);
        if (!Application.isPlaying) return;
        controller.Initialize();
        if (GUILayout.Button("Force upgrade choice")) controller.DebugForceChoice();
        if (controller.IsChoosing && GUILayout.Button("Reroll (debug)")) controller.DebugReroll();
        grant = (UpgradeDefinition)EditorGUILayout.ObjectField("Specific upgrade", grant, typeof(UpgradeDefinition), false);
        if (grant && GUILayout.Button("Give specific upgrade")) controller.DebugGive(grant);
        if (GUILayout.Button("Clear current build")) controller.DebugClearBuild();
        if (GUILayout.Button("Print modifiers")) Debug.Log(controller.Build.DescribeModifiers());
        stage = EditorGUILayout.IntField("Stage index (zero-based)", stage);
        if (GUILayout.Button("Jump to reward (debug)")) controller.DebugJumpToReward(stage);
        if (controller.Build) foreach (var stack in controller.Build.Acquired) EditorGUILayout.LabelField(stack.upgrade.displayName, "x" + stack.count);
    }
}
