using System;
using System.Collections.Generic;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EncounterObjectAuthoring
{
    static readonly Dictionary<GameObject, bool> originals = new Dictionary<GameObject, bool>();
    static StageFlowController previewFlow;
    static EncounterDefinition previewEncounter;
    static int previewWave;
    static int previewStage, previewEncounterIndex;
    public static bool IsPreviewing => previewEncounter != null;
    static EncounterObjectAuthoring()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
        EditorApplication.playModeStateChanged += _ => Restore();
        EditorSceneManager.sceneSaving += (_, __) => Restore();
        EditorSceneManager.sceneClosing += (_, __) => Restore();
        Undo.undoRedoPerformed += Refresh;
        Selection.selectionChanged += () => { if (IsPreviewing && previewFlow && Selection.activeObject != previewFlow.level && Selection.activeGameObject != previewFlow.gameObject) Restore(); };
    }
    public static StageFlowController FindFlow(LevelDefinition level) => !level ? null : UnityEngine.Object.FindObjectsByType<StageFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(f => f.level == level && f.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(f))
        .OrderByDescending(f => f.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()).FirstOrDefault();

    public static string Bind(StageFlowController flow, GameObject target)
    {
        if (!flow || !target || EditorUtility.IsPersistent(target) || target.scene != flow.gameObject.scene) return "";
        var existing = flow.sceneObjectBindings.FirstOrDefault(b => b.target == target);
        if (existing != null) return existing.id;
        Undo.RecordObject(flow, "Bind encounter scene object");
        var binding = new SceneObjectBinding { id = Guid.NewGuid().ToString("N"), target = target };
        flow.sceneObjectBindings.Add(binding);
        PrefabUtility.RecordPrefabInstancePropertyModifications(flow);
        EditorUtility.SetDirty(flow); EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
        return binding.id;
    }

    public static void Show(LevelDefinition level, int stage, int encounter, int wave = 0)
    {
        if (Application.isPlaying) return;
        EncounterPreview.Clear();
        EncounterPreview.SelectEncounter(level, stage, encounter, wave);
        previewFlow = FindFlow(level);
        if (!previewFlow) return;
        previewStage = stage; previewEncounterIndex = encounter;
        previewEncounter = level.stages[stage].encounters[encounter]; previewWave = wave;
        Apply();
    }
    static void Apply()
    {
        previewFlow.ApplySceneObjectStates(previewEncounter.sceneObjectStates, originals);
        // Reproduce the wave history, so unlisted objects retain earlier wave overrides.
        for (int i = 0; i < Mathf.Min(previewWave, previewEncounter.waves.Count); i++)
            if (previewEncounter.waves[i].enabled) previewFlow.ApplySceneObjectStates(previewEncounter.waves[i].sceneObjectStates, originals);
        SceneView.RepaintAll();
    }
    public static void Refresh()
    {
        if (!IsPreviewing || !previewFlow) { Restore(); return; }
        StageFlowController.RestoreSceneObjectStates(originals);
        if (!previewFlow.level || previewStage >= previewFlow.level.stages.Count || previewEncounterIndex >= previewFlow.level.stages[previewStage].encounters.Count) { Restore(); return; }
        previewEncounter = previewFlow.level.stages[previewStage].encounters[previewEncounterIndex]; Apply();
    }
    public static void Restore()
    {
        StageFlowController.RestoreSceneObjectStates(originals);
        previewFlow = null; previewEncounter = null; previewWave = 0;
        SceneView.RepaintAll();
    }

    public static void Draw(LevelDefinition level, int stage, EncounterPreview.InspectorSelection selection)
    {
        var encounter = level.stages[stage].encounters[selection.encounter];
        var flow = FindFlow(level);
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Scene Object States", EditorStyles.boldLabel);
        if (!flow) { EditorGUILayout.HelpBox("Open the playable scene containing a Stage Flow controller assigned to this level. Scene bindings are saved on that controller; activation settings are saved in this level asset.", MessageType.Info); return; }
        EditorGUILayout.HelpBox("Expand Scene Object States on an encounter or wave above. Each row is Apply | Active | Hierarchy object. Save both the scene and level asset. Unlisted objects keep their current state.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Add Selected GameObject to Encounter")) Add(level, flow, encounter.sceneObjectStates);
            if (GUILayout.Button("Preview Encounter State")) Show(level, stage, selection.encounter);
            for (int i = 0; i < encounter.waves.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select " + encounter.waves[i].waveId)) { selection.wave = i + 1; EncounterPreview.SelectEncounter(level, stage, selection.encounter, i + 1); }
                    if (GUILayout.Button("Add Selected Object")) Add(level, flow, encounter.waves[i].sceneObjectStates);
                    if (GUILayout.Button("Preview " + encounter.waves[i].waveId)) { selection.wave = i + 1; Show(level, stage, selection.encounter, i + 1); }
                }
            }
            if (GUILayout.Button("Restore Preview")) { Restore(); EncounterPreview.RefreshNow(); }
        }
        foreach (var state in encounter.sceneObjectStates.Concat(encounter.waves.SelectMany(w => w.sceneObjectStates)))
        {
            var target = flow.ResolveSceneObject(state.bindingId);
            if (!target) EditorGUILayout.HelpBox("An object entry has no binding in this scene. Assign its Hierarchy object or remove the entry.", MessageType.Warning);
            else if (flow.transform.IsChildOf(target.transform)) EditorGUILayout.HelpBox("This target contains Stage Flow itself. Deactivating it stops encounter processing; use a child scenery object instead.", MessageType.Warning);
        }
    }
    static void Add(LevelDefinition level, StageFlowController flow, List<SceneObjectState> states)
    {
        foreach (var target in Selection.gameObjects)
        {
            string id = Bind(flow, target); if (string.IsNullOrEmpty(id)) continue;
            Undo.RecordObject(level, "Add encounter object state"); states.Add(new SceneObjectState { bindingId = id }); EditorUtility.SetDirty(level);
        }
        Refresh();
    }
}

[CustomPropertyDrawer(typeof(SceneObjectState))]
public sealed class SceneObjectStateDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 4;
    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(rect, label, property);
        rect = EditorGUI.IndentedRect(rect); int indent = EditorGUI.indentLevel; EditorGUI.indentLevel = 0;
        var flow = EncounterObjectAuthoring.FindFlow(property.serializedObject.targetObject as LevelDefinition);
        var id = property.FindPropertyRelative("bindingId");
        rect.height = EditorGUIUtility.singleLineHeight;
        var applyRect = new Rect(rect.x, rect.y, 54, rect.height);
        var activeRect = new Rect(rect.x + 56, rect.y, 58, rect.height);
        float labelWidth = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 36;
        EditorGUI.PropertyField(applyRect, property.FindPropertyRelative("apply"), new GUIContent("Apply"));
        EditorGUI.PropertyField(activeRect, property.FindPropertyRelative("active"), new GUIContent("Active"));
        EditorGUIUtility.labelWidth = labelWidth;
        using (new EditorGUI.DisabledScope(!flow))
        {
            EditorGUI.BeginChangeCheck();
            var target = (GameObject)EditorGUI.ObjectField(new Rect(rect.x + 118, rect.y, Mathf.Max(30, rect.width - 118), rect.height), flow ? flow.ResolveSceneObject(id.stringValue) : null, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) id.stringValue = target ? EncounterObjectAuthoring.Bind(flow, target) : "";
        }
        EditorGUI.EndProperty();
        EditorGUI.indentLevel = indent;
    }
}
