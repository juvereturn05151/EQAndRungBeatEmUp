using System.Collections.Generic;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

// Editor preview only: never runs combat or changes stage flow.
[InitializeOnLoad]
public static class EncounterPreview
{
    static LevelDefinition previewLevel;
    static int previewStage, encounterIndex, waveIndex;
    static bool editHandles;
    static string previewFingerprint;
    static double nextRefresh;
    static readonly Color TriggerColor = new Color(1, .7f, .15f);

    static EncounterPreview()
    {
        SceneView.duringSceneGui += DrawScene;
        EditorApplication.playModeStateChanged += _ => Clear();
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Clear();
        Undo.undoRedoPerformed += SceneView.RepaintAll;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        EditorApplication.quitting += Clear;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += (_, __) => Clear();
        EditorApplication.update += RefreshScene;
    }

    public static void DrawInspector(LevelDefinition level, int stageIndex)
    {
        if (!level || stageIndex < 0 || stageIndex >= level.stages.Count) return;
        if (previewLevel && (previewLevel != level || previewStage != stageIndex)) Clear();
        var stage = level.stages[stageIndex];
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Encounter / Trigger Preview", EditorStyles.boldLabel);
        if (stage.IsSafeStage)
        {
            EditorGUILayout.HelpBox("Safe stages ignore encounters at runtime. No trigger preview is available.", MessageType.Info);
            if (previewLevel == level && GUILayout.Button("Clear Preview")) Clear();
            return;
        }
        if (stage.encounters.Count == 0)
        {
            EditorGUILayout.HelpBox("Add an encounter to this stage to preview its trigger and waves.", MessageType.Info);
            if (previewLevel == level && GUILayout.Button("Clear Preview")) Clear();
            return;
        }
        encounterIndex = Mathf.Clamp(encounterIndex, 0, stage.encounters.Count - 1);
        EditorGUI.BeginChangeCheck();
        int chosenEncounter = EditorGUILayout.Popup("Encounter", encounterIndex,
            stage.encounters.Select((e, i) => $"{i + 1}. {e.encounterId}").ToArray());
        if (chosenEncounter != encounterIndex) waveIndex = 0;
        encounterIndex = chosenEncounter;
        var encounter = stage.encounters[encounterIndex];
        waveIndex = Mathf.Clamp(waveIndex, 0, encounter.waves.Count);
        var names = new List<string> { "All waves (layout, not simultaneous spawns)" };
        names.AddRange(encounter.waves.Select((w, i) => $"{i + 1}. {w.waveId}"));
        waveIndex = EditorGUILayout.Popup("Wave", waveIndex, names.ToArray());
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUILayout.HelpBox(Activation(stage, encounterIndex), MessageType.Info);
        EditorGUILayout.HelpBox(encounter.trigger == EncounterTrigger.PlayerZone
            ? "TRIGGER ZONE = the orange rectangle on the floor. Walking into it activates this encounter, then Trigger Delay applies. Trigger Zone X/Y are its lower-left position; Width/Height are its size."
            : "This encounter does not use a trigger zone. The gray rectangle is the unused Trigger Zone setting. Only Trigger = PlayerZone makes entering that rectangle activate the encounter. Your current trigger settings are unchanged.", MessageType.Info);
        foreach (var wave in SelectedWaves(encounter))
        {
            EditorGUILayout.LabelField(wave.waveId + ": " + WaveActivation(wave), EditorStyles.wordWrappedLabel);
            foreach (var spawn in wave.enemySpawns)
                EditorGUILayout.LabelField($"    {(spawn.prefab ? spawn.prefab.name : "MISSING PREFAB")} × {Mathf.Max(1, spawn.count)}; every {Mathf.Max(0, spawn.interval):0.##}s" + (spawn.isBoss ? " [Boss]" : ""), EditorStyles.wordWrappedLabel);
        }
        bool active = previewLevel == level && previewStage == stageIndex;
        if (changed && previewLevel == level) { previewStage = stageIndex; previewFingerprint = null; SceneView.RepaintAll(); }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            if (GUILayout.Button("Preview Encounter"))
            {
                Begin(level, stageIndex, encounterIndex, waveIndex);
                Focus();
            }
            using (new EditorGUI.DisabledScope(!active))
            {
                if (GUILayout.Button("Focus Trigger / Spawns")) Focus();
                if (GUILayout.Button("Clear Preview")) Clear();
            }
        }
        using (new EditorGUI.DisabledScope(!active))
            if (GUILayout.Button("Focus Trigger Zone Rectangle")) FocusZone();
        using (new EditorGUI.DisabledScope(!active || Application.isPlaying))
            editHandles = EditorGUILayout.Toggle("Edit Zone / Spawn Handles", editHandles);
        EditorGUILayout.HelpBox("In Edit Mode, Preview Encounter temporarily shows this stage's art, props, player entry and enemy visuals in the Scene view. Original renderers are hidden only in the editor. Clear Preview restores them. No combat scripts run. All waves shows a layout, not spawn timing. Enable Gizmos for labels/handles. Handles edit the level asset with Undo; Clear does not undo those edits.", MessageType.Info);
    }

    static IEnumerable<WaveDefinition> SelectedWaves(EncounterDefinition encounter)
    {
        if (waveIndex == 0) return encounter.waves;
        return encounter.waves.Skip(waveIndex - 1).Take(1);
    }

    public static string Activation(StageSegmentDefinition stage, int index)
    {
        var e = stage.encounters[index];
        string condition;
        switch (e.trigger)
        {
            case EncounterTrigger.PlayerZone: condition = "First player entry into Trigger Zone (latched even if the player leaves)"; break;
            case EncounterTrigger.PreviousEncounterClear:
                int previous = e.requiredPreviousEncounter < 0 ? index - 1 : e.requiredPreviousEncounter;
                condition = previous >= 0 && previous < index ? "After " + stage.encounters[previous].encounterId + " clears" : "INVALID: needs an earlier encounter";
                break;
            case EncounterTrigger.Manual: condition = "After a manual encounter signal"; break;
            case EncounterTrigger.Time: condition = "From stage entry (stage elapsed time)"; break;
            default: condition = "On stage entry"; break;
        }
        return condition + $"; delay {Mathf.Max(0, e.triggerDelay):0.##}s. " + (e.lockStageUntilClear ? "Locks stage until clear." : "Does not lock stage.");
    }

    static string WaveActivation(WaveDefinition wave)
    {
        switch (wave.trigger)
        {
            case WaveTrigger.PreviousWaveClear: return $"{wave.spawnDelay:0.##}s after previous wave clears";
            case WaveTrigger.Manual: return $"manual wave signal AND encounter elapsed ≥ {wave.spawnDelay:0.##}s";
            default: return $"encounter elapsed ≥ {wave.spawnDelay:0.##}s";
        }
    }

    public static Vector2 SpawnPosition(StageSegmentDefinition stage, EnemySpawnDefinition spawn, int instance)
    {
        var p = spawn.spawnPoints.Count > 0 ? spawn.spawnPoints[instance % spawn.spawnPoints.Count] : stage.movementMax;
        return new Vector2(Mathf.Clamp(p.x, stage.movementMin.x, stage.movementMax.x), Mathf.Clamp(p.y, stage.movementMin.y, stage.movementMax.y));
    }

    public static void Clear()
    {
        EncounterScenePreview.Clear(); previewFingerprint = null;
        previewLevel = null; editHandles = false; SceneView.RepaintAll();
    }

    public static void Begin(LevelDefinition level, int stageIndex, int encounter, int wave = 0)
    {
        Clear();
        if (Application.isPlaying || !level || stageIndex < 0 || stageIndex >= level.stages.Count) return;
        previewLevel = level; previewStage = stageIndex; encounterIndex = encounter; waveIndex = wave;
        RefreshScene(true);
    }

    static void RefreshScene() => RefreshScene(false);
    static void RefreshScene(bool force)
    {
        if (!previewLevel) return;
        if (!TryGet(out var stage, out var encounter)) { Clear(); return; }
        if (!force && EditorApplication.timeSinceStartup < nextRefresh) return;
        nextRefresh = EditorApplication.timeSinceStartup + .2;
        string fingerprint = EditorJsonUtility.ToJson(previewLevel) + ":" + encounterIndex + ":" + waveIndex;
        if (!force && fingerprint == previewFingerprint) return;
        previewFingerprint = fingerprint;
        try { EncounterScenePreview.Show(previewLevel, stage, SelectedWaves(encounter)); }
        catch (System.Exception error) { Clear(); Debug.LogException(error); }
    }

    static bool TryGet(out StageSegmentDefinition stage, out EncounterDefinition encounter)
    {
        stage = null; encounter = null;
        if (!previewLevel || previewStage < 0 || previewStage >= previewLevel.stages.Count) return false;
        stage = previewLevel.stages[previewStage];
        if (stage.IsSafeStage || encounterIndex < 0 || encounterIndex >= stage.encounters.Count) return false;
        encounter = stage.encounters[encounterIndex]; return true;
    }

    static void Focus()
    {
        if (!TryGet(out var stage, out var encounter)) return;
        var bounds = encounter.trigger == EncounterTrigger.PlayerZone
            ? new Bounds(encounter.triggerZone.center, new Vector3(Mathf.Abs(encounter.triggerZone.width), Mathf.Abs(encounter.triggerZone.height), .1f))
            : new Bounds((stage.movementMin + stage.movementMax) * .5f, stage.movementMax - stage.movementMin);
        foreach (var wave in SelectedWaves(encounter)) foreach (var spawn in wave.enemySpawns)
            for (int i = 0; i < Mathf.Max(1, spawn.count); i++) bounds.Encapsulate(SpawnPosition(stage, spawn, i));
        if (EncounterScenePreview.IsActive)
        {
            bounds.Encapsulate(new Vector3(-stage.artWidth * .5f, stage.floorCenterY - stage.floorHeight * .5f));
            bounds.Encapsulate(new Vector3(stage.artWidth * .5f, stage.backgroundCenterY + stage.backgroundHeight * .5f));
        }
        bounds.Expand(.8f);
        var view = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView : EditorWindow.GetWindow<SceneView>();
        view.Frame(bounds, false); view.Repaint();
    }

    static void DrawRect(Rect rect, Color color)
    {
        var points = new[] { new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin), new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax) };
        Handles.DrawSolidRectangleWithOutline(points, new Color(color.r, color.g, color.b, .06f), color);
    }

    static void FocusZone()
    {
        if (!TryGet(out _, out var encounter)) return;
        var zone = encounter.triggerZone;
        var bounds = new Bounds(zone.center, new Vector3(Mathf.Abs(zone.width), Mathf.Abs(zone.height), .1f));
        bounds.Expand(.8f);
        var view = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView : EditorWindow.GetWindow<SceneView>();
        view.Frame(bounds, false); view.Repaint();
    }

    static void DrawTriggerZone(EncounterDefinition encounter)
    {
        bool active = encounter.trigger == EncounterTrigger.PlayerZone;
        var zone = encounter.triggerZone;
        Color color = active ? TriggerColor : new Color(.7f, .7f, .7f);
        var corners = new[] { new Vector3(zone.xMin, zone.yMin), new Vector3(zone.xMax, zone.yMin), new Vector3(zone.xMax, zone.yMax), new Vector3(zone.xMin, zone.yMax) };
        Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, active ? .25f : .12f), color);
        Handles.color = color;
        Handles.DrawAAPolyLine(4, new[] { corners[0], corners[1], corners[2], corners[3], corners[0] });
        var style = new GUIStyle(EditorStyles.helpBox) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = EditorGUIUtility.isProSkin
            ? (active ? new Color(1, .8f, .35f) : Color.white)
            : (active ? new Color(.55f, .24f, .02f) : new Color(.2f, .2f, .2f));
        Handles.Label(zone.center, active
            ? $"TRIGGER ZONE\nWalk inside to activate\nDelay: {encounter.triggerDelay:0.##}s"
            : $"UNUSED TRIGGER ZONE\nCurrent trigger: {encounter.trigger}\nPlayer entry here does not activate it", style);
    }

    static void DrawScene(SceneView view)
    {
        if (!TryGet(out var stage, out var encounter)) return;
        var oldColor = Handles.color;
        var oldDepth = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        DrawRect(new Rect(stage.movementMin, stage.movementMax - stage.movementMin), Color.green);
        DrawTriggerZone(encounter);
        Vector2 labelPoint = stage.playerEntryPoint;
        if (encounter.trigger == EncounterTrigger.PlayerZone)
        {
            labelPoint = new Vector2(encounter.triggerZone.xMin, encounter.triggerZone.yMax);
            if (editHandles && !Application.isPlaying) EditZone(encounter);
        }
        Handles.Label((Vector3)labelPoint + Vector3.up * .25f, $"{stage.stageName} / {encounter.encounterId}\n{Activation(stage, encounterIndex)}");
        var stacked = new Dictionary<Vector2, int>();
        foreach (var wave in SelectedWaves(encounter)) foreach (var spawn in wave.enemySpawns)
        {
            if (editHandles && !Application.isPlaying) EditSpawns(spawn);
            for (int i = 0; i < Mathf.Max(1, spawn.count); i++)
            {
                Vector2 p = SpawnPosition(stage, spawn, i);
                var raw = spawn.spawnPoints.Count > 0 ? spawn.spawnPoints[i % spawn.spawnPoints.Count] : stage.movementMax;
                Handles.color = spawn.prefab ? Color.cyan : Color.red;
                Handles.DrawWireDisc(p, Vector3.forward, .12f);
                if (raw != p)
                {
                    Handles.color = Color.red; Handles.DrawDottedLine(raw, p, 4);
                    Handles.DrawWireDisc(raw, Vector3.forward, .08f);
                }
                stacked.TryGetValue(p, out int row); stacked[p] = row + 1;
                Handles.Label((Vector3)p + Vector3.up * (.2f + row * .22f),
                    $"{wave.waveId}: {(spawn.prefab ? spawn.prefab.name : "MISSING PREFAB")} #{i + 1} (+{i * Mathf.Max(0, spawn.interval):0.##}s from wave start)" + (raw != p ? " [CLAMPED]" : "") + (spawn.isBoss ? " [Boss]" : ""));
            }
        }
        Handles.color = oldColor;
        Handles.zTest = oldDepth;
    }

    static void EditZone(EncounterDefinition encounter)
    {
        Handles.color = TriggerColor;
        var zone = encounter.triggerZone;
        EditorGUI.BeginChangeCheck();
        Vector3 center = Handles.PositionHandle(zone.center, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(previewLevel, "Move encounter trigger zone");
            zone.center = center; encounter.triggerZone = zone; EditorUtility.SetDirty(previewLevel);
        }
        EditorGUI.BeginChangeCheck();
        Vector3 corner = Handles.PositionHandle(new Vector3(zone.xMax, zone.yMax, 0), Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(previewLevel, "Resize encounter trigger zone");
            zone.width = Mathf.Max(.01f, corner.x - zone.xMin); zone.height = Mathf.Max(.01f, corner.y - zone.yMin);
            encounter.triggerZone = zone; EditorUtility.SetDirty(previewLevel);
        }
    }

    static void EditSpawns(EnemySpawnDefinition spawn)
    {
        Handles.color = Color.cyan;
        for (int i = 0; i < spawn.spawnPoints.Count; i++)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(spawn.spawnPoints[i], Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) continue;
            Undo.RecordObject(previewLevel, "Move encounter enemy spawn point");
            spawn.spawnPoints[i] = position; EditorUtility.SetDirty(previewLevel);
        }
    }
}
