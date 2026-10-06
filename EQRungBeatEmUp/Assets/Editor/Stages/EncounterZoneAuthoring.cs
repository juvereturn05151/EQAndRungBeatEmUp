using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Extends the existing level inspector and preview. No scene objects are saved for zones.
[InitializeOnLoad]
public static class EncounterZoneAuthoring
{
    const string Pending = "BeatEmUp.SimulateEncounter";
    static EncounterZoneAuthoring() { EditorApplication.update += StartPendingSimulation; }

    public static void Draw(LevelDefinition level, int stageIndex, EncounterPreview.InspectorSelection selection)
    {
        var stage = level.stages[stageIndex];
        if (stage.IsSafeStage) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spawn Triggers", EditorStyles.boldLabel);
        foreach (var spawn in stage.encounters.Where(e => !e.useCombatBounds)) EditorGUILayout.LabelField(spawn.encounterId);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            if (GUILayout.Button("+ Add Spawn Trigger")) Add(level, stageIndex, selection, false);
        EditorGUILayout.LabelField("Encounter Zones", EditorStyles.boldLabel);
        foreach (var zone in stage.encounters.Where(e => e.useCombatBounds)) EditorGUILayout.LabelField(zone.encounterId);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            if (GUILayout.Button("+ Add Encounter Zone")) Add(level, stageIndex, selection, true);
        if (stage.encounters.Count == 0) return;
        selection.encounter = Mathf.Clamp(selection.encounter, 0, stage.encounters.Count - 1);
        var encounter = stage.encounters[selection.encounter];
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Duplicate Encounter Zone"))
                {
                    Undo.RecordObject(level, "Duplicate encounter");
                    var copy = JsonUtility.FromJson<EncounterDefinition>(JsonUtility.ToJson(encounter));
                    copy.encounterId = UniqueId(stage, encounter.encounterId + " Copy");
                    stage.encounters.Add(copy); selection.encounter = stage.encounters.Count - 1; Changed(level, stageIndex, selection);
                    return;
                }
                if (GUILayout.Button("Delete Encounter Zone"))
                {
                    Undo.RecordObject(level, "Delete encounter");
                    int removed = selection.encounter; stage.encounters.RemoveAt(removed);
                    foreach (var other in stage.encounters)
                    {
                        if (other.requiredPreviousEncounter == removed) { other.requiredPreviousEncounter = -1; Debug.LogWarning("Deleted encounter dependency: review " + other.encounterId); }
                        else if (other.requiredPreviousEncounter > removed) other.requiredPreviousEncounter--;
                    }
                    selection.encounter = Mathf.Clamp(removed, 0, stage.encounters.Count - 1);
                    EditorUtility.SetDirty(level); EncounterPreview.BeginStage(level, stageIndex); return;
                }
            }
            if (GUILayout.Button("+ Add Wave (after previous clears)"))
            {
                Undo.RecordObject(level, "Add encounter wave");
                encounter.waves.Add(new WaveDefinition { waveId = "Wave " + (encounter.waves.Count + 1), trigger = encounter.waves.Count == 0 ? WaveTrigger.EncounterStart : WaveTrigger.PreviousWaveClear });
                Changed(level, stageIndex, selection);
            }
            if (encounter.waves.Count > 0 && GUILayout.Button("+ Add Enemy Spawn / Point to Selected Wave"))
            {
                Undo.RecordObject(level, "Add enemy spawn point");
                var wave = encounter.waves[Mathf.Clamp(selection.wave - 1, 0, encounter.waves.Count - 1)];
                var bounds = encounter.useCombatBounds ? encounter.combatBounds : new Rect(stage.movementMin, stage.movementMax - stage.movementMin);
                wave.enemySpawns.Add(new EnemySpawnDefinition { spawnPoints = new System.Collections.Generic.List<Vector2> { bounds.center } });
                Changed(level, stageIndex, selection);
            }
            foreach (var wave in encounter.waves)
                foreach (var spawn in wave.enemySpawns)
                    if (GUILayout.Button($"+ Spawn Point: {wave.waveId} / {(spawn.prefab ? spawn.prefab.name : "assign prefab above")}"))
                    {
                        Undo.RecordObject(level, "Add encounter spawn point");
                        spawn.spawnPoints.Add(encounter.useCombatBounds ? encounter.combatBounds.center : (stage.movementMin + stage.movementMax) * .5f);
                        Changed(level, stageIndex, selection);
                    }
        }
        if (GUILayout.Button("Simulate Encounter")) Simulate(level, stageIndex, selection.encounter);
        EditorGUILayout.HelpBox("Select an encounter or wave to immediately show its bounds and handles. Drag the center to move, or edge/corner squares to resize. Numeric fields edit the same rectangles. Simulation starts the real selected encounter in Play Mode.", MessageType.Info);
        foreach (var zone in stage.encounters)
        {
            if (zone.triggerZone.width <= 0 || zone.triggerZone.height <= 0 || zone.combatBounds.width <= 0 || zone.combatBounds.height <= 0 || zone.cameraBounds.width <= 0 || zone.cameraBounds.height <= 0)
                EditorGUILayout.HelpBox(zone.encounterId + ": Trigger / Combat / Camera bounds have zero or negative size.", MessageType.Warning);
            if (zone.combatBounds.xMin < zone.cameraBounds.xMin || zone.combatBounds.xMax > zone.cameraBounds.xMax || zone.combatBounds.yMin < zone.cameraBounds.yMin || zone.combatBounds.yMax > zone.cameraBounds.yMax)
                EditorGUILayout.HelpBox(zone.encounterId + ": combat bounds extend outside camera bounds. Review the intended camera framing.", MessageType.Warning);
            if (!zone.useCombatBounds) continue;
            if (!zone.combatBounds.Overlaps(new Rect(stage.movementMin, stage.movementMax - stage.movementMin)))
                EditorGUILayout.HelpBox(zone.encounterId + ": combat bounds are outside the movement area.", MessageType.Error);
            if (!zone.combatBounds.Overlaps(zone.triggerZone))
                EditorGUILayout.HelpBox(zone.encounterId + ": trigger is outside combat bounds; players will be pulled into the arena on activation.", MessageType.Warning);
        }
    }
    static string UniqueId(StageSegmentDefinition stage, string stem)
    {
        string id = stem; int suffix = 2;
        while (stage.encounters.Any(e => e.encounterId == id)) id = stem + " " + suffix++;
        return id;
    }
    static void Add(LevelDefinition level, int stageIndex, EncounterPreview.InspectorSelection selection, bool combat)
    {
        Undo.RecordObject(level, combat ? "Add encounter zone" : "Add spawn trigger");
        var stage = level.stages[stageIndex];
        var center = (stage.movementMin + stage.movementMax) * .5f;
        var bounds = new Rect(stage.movementMin, stage.movementMax - stage.movementMin);
        var encounter = new EncounterDefinition {
            encounterId = UniqueId(stage, combat ? "Encounter Zone" : "Spawn Trigger"), trigger = EncounterTrigger.PlayerZone,
            triggerZone = new Rect(center - new Vector2(.25f, bounds.height * .5f), new Vector2(.5f, bounds.height)),
            useCombatBounds = combat, combatBounds = bounds,
            cameraBounds = new Rect(-stage.artWidth * .5f, stage.floorCenterY - stage.floorHeight * .5f, stage.artWidth, stage.backgroundCenterY + stage.backgroundHeight * .5f - (stage.floorCenterY - stage.floorHeight * .5f)),
            lockStageUntilClear = combat, requiredForCompletion = combat
        };
        encounter.waves.Add(new WaveDefinition { waveId = "Wave 1" });
        stage.encounters.Add(encounter); selection.encounter = stage.encounters.Count - 1; selection.wave = 0;
        Changed(level, stageIndex, selection);
    }
    static void Changed(LevelDefinition level, int stage, EncounterPreview.InspectorSelection selection)
    {
        EditorUtility.SetDirty(level); EncounterPreview.Begin(level, stage, selection.encounter, selection.wave); EncounterPreview.RefreshNow();
    }
    public static void Simulate(LevelDefinition level, int stage, int encounter)
    {
        if (MultiplayerSession.Active && !MultiplayerSession.Active.IsAuthority) { Debug.LogWarning("Only the host can simulate encounters."); return; }
        if (Application.isPlaying)
        {
            var flow = Object.FindObjectsByType<StageFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(f => f.level == level);
            if (flow) flow.SimulateEncounter(stage, encounter); else Debug.LogWarning("Open the playable stage scene to simulate this level.");
            return;
        }
        // Preserve the current scene and authoring selection; do not silently discard scene edits.
        if (!Object.FindObjectsByType<StageFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(f => f.level == level))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        }
        SessionState.SetString(Pending + ".level", AssetDatabase.GetAssetPath(level));
        SessionState.SetInt(Pending + ".stage", stage); SessionState.SetInt(Pending + ".encounter", encounter);
        SessionState.SetBool(Pending, true); EncounterPreview.Clear(); EditorApplication.EnterPlaymode();
    }
    static void StartPendingSimulation()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false);
        var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(SessionState.GetString(Pending + ".level", ""));
        Simulate(level, SessionState.GetInt(Pending + ".stage", 0), SessionState.GetInt(Pending + ".encounter", 0));
    }
}
