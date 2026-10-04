using BeatEmUp;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(LevelDefinition))]
public sealed class LevelDefinitionEditor : Editor
{
    private ReorderableList stages;
    private readonly EncounterPreview.InspectorSelection previewSelection = new EncounterPreview.InspectorSelection();
    private void OnEnable()
    {
        stages = new ReorderableList(serializedObject, serializedObject.FindProperty("stages"), true, true, true, true);
        stages.index = StageEditorSelection.GetIndex((LevelDefinition)target);
        stages.onSelectCallback = list => {
            serializedObject.ApplyModifiedProperties();
            StageEditorSelection.Select((LevelDefinition)target, list.index);
        };
        stages.onReorderCallback = list => {
            serializedObject.ApplyModifiedProperties();
            var level = (LevelDefinition)target;
            StageEditorSelection.Select(level, StageEditorSelection.GetIndex(level));
        };
        EditorApplication.delayCall += StartPreview;
        stages.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Stages — drag to reorder");
        stages.drawElementCallback = (rect, index, active, focused) => {
            var item = stages.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.LabelField(rect, (index + 1) + ". " + item.FindPropertyRelative("stageName").stringValue);
        };
    }
    private void StartPreview() { if (this && target) StageEditorSelection.EnsurePreview((LevelDefinition)target); }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        stages.index = StageEditorSelection.GetIndex((LevelDefinition)target);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelName"));
        stages.DoLayoutList();
        if (stages.index >= 0 && stages.index < stages.serializedProperty.arraySize)
        {
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(stages.serializedProperty.GetArrayElementAtIndex(stages.index), new GUIContent("Selected stage"), true);
            var selected = stages.serializedProperty.GetArrayElementAtIndex(stages.index);
            if (GUILayout.Button("Reset to Default Layout"))
            {
                selected.FindPropertyRelative("backgroundHeight").floatValue = 1.6f;
                selected.FindPropertyRelative("backgroundCenterY").floatValue = 2.56f;
                selected.FindPropertyRelative("floorHeight").floatValue = 2.4f;
                selected.FindPropertyRelative("floorCenterY").floatValue = .56f;
            }
            float seamGap = selected.FindPropertyRelative("backgroundCenterY").floatValue - selected.FindPropertyRelative("backgroundHeight").floatValue * .5f
                - (selected.FindPropertyRelative("floorCenterY").floatValue + selected.FindPropertyRelative("floorHeight").floatValue * .5f);
            if (Mathf.Abs(seamGap) > .01f)
                EditorGUILayout.HelpBox($"Art seam: {Mathf.Abs(seamGap):0.###} world units of {(seamGap > 0 ? "gap" : "overlap")}. Match Background Center Y − Background Height / 2 to Floor Center Y + Floor Height / 2. This assumes centered sprite pivots.", MessageType.Warning);
            EditorGUILayout.HelpBox("Art heights and center Y values affect visuals only. Changes refresh the selected stage immediately in Edit Mode. Asset edits persist; temporary preview transforms do not. Reset restores only the four height/center values.", MessageType.Info);
        }
        if (serializedObject.ApplyModifiedProperties() && !Application.isPlaying)
        {
            StageEditorSelection.EnsurePreview((LevelDefinition)target);
            EncounterPreview.RefreshNow();
        }
        EditorGUILayout.HelpBox("Select a stage above. Expand encounters → waves → enemy spawns to set prefab, count, positions and interval. Destructibles support a prefab or sprites plus HP and hitbox. -1 nextStageIndex follows list order. PreviousEncounterClear references an earlier index. IDs are used by manual/event signals.", MessageType.Info);
        var level = (LevelDefinition)target;
        StageEditorSelection.DrawControls(level);
        stages.index = StageEditorSelection.GetIndex(level);
        EncounterPreview.DrawInspector(level, stages.index, previewSelection);
        for (int i = 0; i < level.stages.Count; i++)
        {
            var stage = level.stages[i];
            if (stage.IsSafeStage && (stage.encounters.Count > 0 || stage.destructibles.Count > 0)) EditorGUILayout.HelpBox($"Stage {i + 1}: Safe stages ignore encounters and destructibles. Remove these unused placements.", MessageType.Warning);
            if (stage.IsSafeStage && stage.completionMode == StageCompletion.BossDefeated) EditorGUILayout.HelpBox($"Stage {i + 1}: Safe stages cannot spawn a boss. Use ReachExit or Event completion.", MessageType.Error);
            if (!stage.backgroundSprite || !stage.floorSprite) EditorGUILayout.HelpBox($"Stage {i + 1}: assign both art sprites.", MessageType.Error);
            if (stage.movementMin.x < -stage.artWidth * .5f || stage.movementMax.x > stage.artWidth * .5f)
                EditorGUILayout.HelpBox($"Stage {i + 1}: horizontal movement bounds extend beyond the art. Increase Art Width to cover the bounds plus character sprite padding; camera clamping cannot display a player outside the artwork.", MessageType.Warning);
            if (stage.movementMax.y > Mathf.Max(stage.backgroundCenterY + stage.backgroundHeight * .5f, stage.floorCenterY + stage.floorHeight * .5f))
                EditorGUILayout.HelpBox($"Stage {i + 1}: Movement Max Y extends above the artwork. Horizontal camera follow does not change this lane bound or vertically track grounded movement.", MessageType.Warning);
            if (stage.movementMax.x <= stage.movementMin.x || stage.movementMax.y <= stage.movementMin.y) EditorGUILayout.HelpBox($"Stage {i + 1}: movement bounds are reversed/empty.", MessageType.Error);
            var bounds = new Rect(stage.movementMin, stage.movementMax - stage.movementMin);
            if (!bounds.Contains(stage.playerEntryPoint) || !bounds.Contains(stage.playerExitPoint)) EditorGUILayout.HelpBox($"Stage {i + 1}: entry/exit must be inside movement bounds.", MessageType.Warning);
            if (stage.nextStageIndex == i || stage.nextStageIndex < -1 || stage.nextStageIndex > level.stages.Count) EditorGUILayout.HelpBox($"Stage {i + 1}: invalid next-stage index.", MessageType.Error);
            if (stage.safeRoom && stage.encounters.Count > 0) EditorGUILayout.HelpBox($"Stage {i + 1}: safe room contains encounters.", MessageType.Warning);
            for (int e = 0; e < stage.encounters.Count; e++)
            {
                var encounter = stage.encounters[e];
                if (encounter.trigger == EncounterTrigger.PreviousEncounterClear && (e == 0 || encounter.requiredPreviousEncounter >= e)) EditorGUILayout.HelpBox($"Stage {i + 1}: clear-based encounter needs an earlier encounter.", MessageType.Error);
                for (int w = 0; w < encounter.waves.Count; w++)
                {
                    var wave = encounter.waves[w];
                    if (w == 0 && wave.trigger == WaveTrigger.PreviousWaveClear) EditorGUILayout.HelpBox($"Stage {i + 1}: first wave cannot wait for a previous wave.", MessageType.Error);
                    foreach (var spawn in wave.enemySpawns) if (!spawn.prefab || !spawn.prefab.GetComponent<CharacterHealth>() || !spawn.prefab.GetComponent<EnemyCombat>()) EditorGUILayout.HelpBox($"Stage {i + 1}: spawn needs a combat enemy prefab.", MessageType.Error);
                }
            }
        }
        if (GUILayout.Button("Open playable haunted-house scene")) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        if (GUILayout.Button("Select stage flow documentation")) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>("Documentation/HauntedStageFlow.md"));
    }
}
