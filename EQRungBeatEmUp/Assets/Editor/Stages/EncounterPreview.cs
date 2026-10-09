using System.Collections.Generic;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

// Editor preview only: never runs combat or changes stage flow.
[InitializeOnLoad]
public static class EncounterPreview
{
    public sealed class InspectorSelection
    {
        public LevelDefinition level;
        public int stage = -1, encounter, wave;
        public bool shown;
    }
    static LevelDefinition previewLevel;
    static int previewStage, encounterIndex, waveIndex;
    static bool editHandles = true;
    static bool showAllBounds, showTrigger = true, showCombat = true, showCamera = true;
    static LevelDefinition requestedLevel;
    static int requestedStage, requestedEncounter, requestedWave;
    public static void RequestSelection(LevelDefinition level, int stage, int encounter, int wave)
    { requestedLevel = level; requestedStage = stage; requestedEncounter = encounter; requestedWave = wave; }
    static bool stageMode;
    static LevelDefinition resumeLevel;
    static string previewFingerprint;
    static double nextRefresh;
    static readonly Color TriggerColor = new Color(1, .7f, .15f);

    static EncounterPreview()
    {
        SceneView.duringSceneGui += DrawScene;
        EditorApplication.playModeStateChanged += state => {
            if (previewLevel) resumeLevel = previewLevel;
            Clear();
            if (state == PlayModeStateChange.EnteredEditMode && resumeLevel)
                EditorApplication.delayCall += () => { if (resumeLevel) StageEditorSelection.EnsurePreview(resumeLevel); };
        };
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Clear();
        Undo.undoRedoPerformed += RefreshNow;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        EditorApplication.quitting += Clear;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += (_, __) => Clear();
        EditorApplication.update += RefreshScene;
    }

    public static void DrawInspector(LevelDefinition level, int stageIndex, InspectorSelection selection)
    {
        if (!level || stageIndex < 0 || stageIndex >= level.stages.Count) return;
        PrepareSelection(level, stageIndex, selection);
        if (requestedLevel == level && requestedStage == stageIndex)
        { selection.encounter = requestedEncounter; selection.wave = requestedWave; requestedLevel = null; selection.shown = false; PrepareSelection(level, stageIndex, selection); }
        if (!Application.isPlaying && !selection.shown) { SelectEncounter(level, stageIndex, selection.encounter, selection.wave); selection.shown = true; }
        var stage = level.stages[stageIndex];
        EncounterZoneAuthoring.Draw(level, stageIndex, selection);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Encounter / Trigger Preview", EditorStyles.boldLabel);
        if (stage.IsSafeStage)
        {
            EditorGUILayout.HelpBox("Safe stages ignore encounters at runtime. No trigger preview is available.", MessageType.Info);
            return;
        }
        if (stage.encounters.Count == 0)
        {
            EditorGUILayout.HelpBox("Add an encounter to this stage to preview its trigger and waves.", MessageType.Info);
            return;
        }
        EditorGUI.BeginChangeCheck();
        int chosenEncounter = EditorGUILayout.Popup("Encounter", selection.encounter,
            stage.encounters.Select((e, i) => $"{i + 1}. {e.encounterId}" + (e.enabled ? "" : " [Disabled]")).ToArray());
        if (chosenEncounter != selection.encounter) selection.wave = 0;
        selection.encounter = chosenEncounter;
        var encounter = stage.encounters[selection.encounter];
        var coordinationObject=new SerializedObject(level); coordinationObject.Update();
        var coordination=coordinationObject.FindProperty("stages").GetArrayElementAtIndex(stageIndex).FindPropertyRelative("encounters").GetArrayElementAtIndex(selection.encounter).FindPropertyRelative("attackCoordination");
        coordination.isExpanded=true;
        EditorGUILayout.PropertyField(coordination,new GUIContent("Attack Coordination"),true);
        coordinationObject.ApplyModifiedProperties();
        selection.wave = Mathf.Clamp(selection.wave, 0, encounter.waves.Count);
        var names = new List<string> { "All waves (layout, not simultaneous spawns)" };
        names.AddRange(encounter.waves.Select((w, i) => $"{i + 1}. {w.waveId}" + (w.enabled ? "" : " [Disabled]")));
        selection.wave = EditorGUILayout.Popup("Wave", selection.wave, names.ToArray());
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUILayout.HelpBox(Activation(stage, selection.encounter), MessageType.Info);
        EditorGUILayout.HelpBox(encounter.trigger == EncounterTrigger.PlayerZone
            ? "TRIGGER ZONE = the orange rectangle on the floor. Walking into it activates this encounter, then Trigger Delay applies. Trigger Zone X/Y are its lower-left position; Width/Height are its size."
            : "This encounter does not use a trigger zone. The gray rectangle is the unused Trigger Zone setting. Only Trigger = PlayerZone makes entering that rectangle activate the encounter. Your current trigger settings are unchanged.", MessageType.Info);
        foreach (var wave in SelectedWaves(encounter, selection.wave))
        {
            EditorGUILayout.LabelField(wave.waveId + ": " + WaveActivation(wave), EditorStyles.wordWrappedLabel);
            foreach (var spawn in wave.enemySpawns)
                EditorGUILayout.LabelField($"    {(spawn.prefab ? spawn.prefab.name : "MISSING PREFAB")} × {Mathf.Max(1, spawn.count)}; every {Mathf.Max(0, spawn.interval):0.##}s" + (spawn.isBoss ? " [Boss]" : ""), EditorStyles.wordWrappedLabel);
        }
        bool sameContext = previewLevel == level && previewStage == stageIndex;
        if (changed && sameContext)
        {
            EncounterObjectAuthoring.Restore();
            SelectEncounter(level, stageIndex, selection.encounter, selection.wave); RefreshNow();
        }
        bool active = !stageMode && previewLevel == level && previewStage == stageIndex && encounterIndex == selection.encounter && waveIndex == selection.wave;
        if (TryGet(out var showingStage, out var showingEncounter))
            EditorGUILayout.LabelField("Showing", stageMode ? showingStage.stageName + " / Whole stage" : $"{showingStage.stageName} / {encounterIndex + 1}. {showingEncounter.encounterId}", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            if (GUILayout.Button("Preview Encounter"))
            {
                Begin(level, stageIndex, selection.encounter, selection.wave);
                Focus();
            }
            using (new EditorGUI.DisabledScope(!active))
            {
                if (GUILayout.Button("Focus Encounter")) FocusZone();
                if (GUILayout.Button("Clear Preview")) Clear();
            }
        }
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            for (int i = 0; i < stage.encounters.Count; i++)
                if (GUILayout.Button($"Preview {i + 1}. {stage.encounters[i].encounterId}"))
                {
                    selection.encounter = i; selection.wave = 0;
                    Begin(level, stageIndex, i); Focus();
                }
        using (new EditorGUI.DisabledScope(!active))
            if (GUILayout.Button("Focus Trigger Zone Rectangle")) FocusZone();
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(!active || Application.isPlaying))
            editHandles = EditorGUILayout.Toggle("Edit Zone / Spawn Handles", editHandles);
        showAllBounds = EditorGUILayout.Toggle("Show All Encounter Bounds", showAllBounds);
        showTrigger = EditorGUILayout.Toggle("Show Trigger Zone", showTrigger);
        showCombat = EditorGUILayout.Toggle("Show Combat Bounds", showCombat);
        showCamera = EditorGUILayout.Toggle("Show Camera Bounds", showCamera);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        EncounterObjectAuthoring.Draw(level, stageIndex, selection);
        EditorGUILayout.HelpBox("In Edit Mode, Preview Encounter temporarily shows this stage's art, props, player entry and enemy visuals in the Scene view. Original renderers are hidden only in the editor. Clear Preview restores them. No combat scripts run. All waves shows a layout, not spawn timing. Labels/handles draw over the artwork. Handles edit the level asset with Undo; Clear does not undo those edits.", MessageType.Info);
    }

    public static void PrepareSelection(LevelDefinition level, int stageIndex, InspectorSelection selection)
    {
        if (!level || stageIndex < 0 || stageIndex >= level.stages.Count) return;
        if (selection.level != level || selection.stage != stageIndex)
        {
            // Reset only this inspector's encounter controls; keep the shared stage preview intact.
            selection.level = level; selection.stage = stageIndex; selection.encounter = selection.wave = 0;
            selection.shown = false;
        }
        var stage = level.stages[stageIndex];
        selection.encounter = Mathf.Clamp(selection.encounter, 0, stage.encounters.Count - 1);
        selection.wave = stage.encounters.Count == 0 ? 0 : Mathf.Clamp(selection.wave, 0, stage.encounters[selection.encounter].waves.Count);
    }

    static IEnumerable<WaveDefinition> SelectedWaves(EncounterDefinition encounter, int selection = -1)
    {
        int wave = selection < 0 ? waveIndex : selection;
        if (wave == 0) return encounter.waves;
        return encounter.waves.Skip(wave - 1).Take(1);
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
        var zone = stage.encounters.FirstOrDefault(e => e.waves.Any(w => w.enemySpawns.Contains(spawn)));
        var min = stage.movementMin; var max = stage.movementMax;
        if (zone != null && zone.useCombatBounds)
        {
            min = Vector2.Max(min, zone.combatBounds.min); max = Vector2.Min(max, zone.combatBounds.max);
            max = Vector2.Max(min, max);
        }
        return new Vector2(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y));
    }

    public static void Clear()
    {
        EncounterObjectAuthoring.Restore();
        EncounterScenePreview.Clear(); previewFingerprint = null;
        previewLevel = null; SceneView.RepaintAll();
    }

    public static void SelectEncounter(LevelDefinition level, int stage, int encounter, int wave)
    {
        if (Application.isPlaying || !level || stage < 0 || stage >= level.stages.Count || level.stages[stage].encounters.Count == 0) return;
        bool different = previewLevel != level || previewStage != stage || encounterIndex != encounter || waveIndex != wave;
        if (different) EncounterObjectAuthoring.Restore();
        previewLevel = level; previewStage = stage; encounterIndex = Mathf.Clamp(encounter, 0, level.stages[stage].encounters.Count - 1);
        waveIndex = wave; stageMode = false; StageEditorSelection.Remember(level, stage); SceneView.RepaintAll();
    }

    public static bool IsPreviewing(LevelDefinition level, int stage) => previewLevel == level && previewStage == stage && (EncounterScenePreview.IsActive || EncounterObjectAuthoring.IsPreviewing);
    public static void BeginStage(LevelDefinition level, int index)
    {
        Clear();
        if (Application.isPlaying || !level || index < 0 || index >= level.stages.Count) return;
        StageEditorSelection.Remember(level, index);
        previewLevel = level; previewStage = index; stageMode = true;
        RefreshScene(true);
    }
    public static void RefreshNow()
    {
        if (!Application.isPlaying) RefreshScene(true);
        SceneView.RepaintAll();
    }
    public static void FocusPreview() => Focus();

    public static void Begin(LevelDefinition level, int stageIndex, int encounter, int wave = 0)
    {
        Clear();
        if (Application.isPlaying || !level || stageIndex < 0 || stageIndex >= level.stages.Count) return;
        StageEditorSelection.Remember(level, stageIndex);
        previewLevel = level; previewStage = stageIndex; encounterIndex = encounter; waveIndex = wave; stageMode = false;
        RefreshScene(true);
    }

    static void RefreshScene() => RefreshScene(false);
    static void RefreshScene(bool force)
    {
        if (!previewLevel) return;
        previewStage = StageEditorSelection.GetIndex(previewLevel);
        if (!TryGet(out var stage, out var encounter)) { Clear(); return; }
        if (!force && EditorApplication.timeSinceStartup < nextRefresh) return;
        nextRefresh = EditorApplication.timeSinceStartup + .2;
        string fingerprint = EditorJsonUtility.ToJson(previewLevel) + ":" + previewStage + ":" + stageMode + ":" + encounterIndex + ":" + waveIndex;
        if (!force && fingerprint == previewFingerprint) return;
        previewFingerprint = fingerprint;
        if (EncounterObjectAuthoring.IsPreviewing) { EncounterObjectAuthoring.Refresh(); return; }
        try { EncounterScenePreview.Show(previewLevel, stage, stageMode ? StageWaves(stage) : SelectedWaves(encounter), stageMode); }
        catch (System.Exception error) { Clear(); Debug.LogException(error); }
    }

    static IEnumerable<WaveDefinition> StageWaves(StageSegmentDefinition stage) => stage.IsSafeStage
        ? Enumerable.Empty<WaveDefinition>() : stage.encounters.SelectMany(e => e.waves);

    static bool TryGet(out StageSegmentDefinition stage, out EncounterDefinition encounter)
    {
        stage = null; encounter = null;
        if (!previewLevel || previewStage < 0 || previewStage >= previewLevel.stages.Count) return false;
        stage = previewLevel.stages[previewStage];
        if (stageMode) return true;
        if (stage.IsSafeStage || encounterIndex < 0 || encounterIndex >= stage.encounters.Count) return false;
        encounter = stage.encounters[encounterIndex]; return true;
    }

    static void Focus()
    {
        if (!TryGet(out var stage, out var encounter)) return;
        var bounds = !stageMode && encounter.trigger == EncounterTrigger.PlayerZone
            ? new Bounds(encounter.triggerZone.center, new Vector3(Mathf.Abs(encounter.triggerZone.width), Mathf.Abs(encounter.triggerZone.height), .1f))
            : new Bounds((stage.movementMin + stage.movementMax) * .5f, stage.movementMax - stage.movementMin);
        foreach (var wave in stageMode ? StageWaves(stage) : SelectedWaves(encounter)) foreach (var spawn in wave.enemySpawns)
            for (int i = 0; i < Mathf.Max(1, spawn.count); i++) bounds.Encapsulate(SpawnPosition(stage, spawn, i));
        if (EncounterScenePreview.IsActive)
        {
            bounds.Encapsulate(new Vector3(-stage.artWidth * .5f, stage.floorCenterY - stage.floorHeight * .5f));
            bounds.Encapsulate(new Vector3(stage.artWidth * .5f, stage.backgroundCenterY + stage.backgroundHeight * .5f));
        }
        if (stageMode && !stage.IsSafeStage)
            foreach (var e in stage.encounters) if (e.trigger == EncounterTrigger.PlayerZone)
            {
                bounds.Encapsulate(new Vector3(e.triggerZone.xMin, e.triggerZone.yMin));
                bounds.Encapsulate(new Vector3(e.triggerZone.xMax, e.triggerZone.yMax));
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
        if (!TryGet(out _, out var encounter) || stageMode) return;
        var zone = encounter.triggerZone;
        if (encounter.useCombatBounds) zone = encounter.combatBounds;
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
            ? $"{encounter.encounterId}\nTRIGGER ZONE — walk inside\nDelay: {encounter.triggerDelay:0.##}s"
            : $"{encounter.encounterId}\nUNUSED ZONE — {encounter.trigger}", style);
    }

    static void DrawScene(SceneView view)
    {
        if (!TryGet(out var stage, out var encounter)) return;
        Handles.BeginGUI();
        GUI.Label(new Rect(12, 42, Mathf.Min(560, view.position.width - 24), 48), stageMode
            ? $"LIVE STAGE PREVIEW: {previewStage + 1}. {stage.stageName}\nEntry, exit, movement, encounters, spawns and reward markers"
            : $"PREVIEW: {stage.stageName}\nEncounter {encounterIndex + 1}: {encounter.encounterId}", EditorStyles.helpBox);
        Handles.EndGUI();
        var oldColor = Handles.color;
        var oldDepth = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        DrawRect(new Rect(stage.movementMin, stage.movementMax - stage.movementMin), Color.green);
        DrawStageMarkers(stage);
        StageExitMarkerAuthoring.Draw(previewLevel, stage);
        if (stageMode || showAllBounds)
        {
            var stacked = new Dictionary<Vector2, int>();
            if (!stage.IsSafeStage)
                for (int i = 0; i < stage.encounters.Count; i++) DrawEncounter(stage, stage.encounters[i], i, stageMode, stacked);
        }
        else DrawEncounter(stage, encounter, encounterIndex, false);
        Handles.color = oldColor;
        Handles.zTest = oldDepth;
    }

    static void DrawStageMarkers(StageSegmentDefinition stage)
    {
        Handles.color = Color.cyan; Handles.DrawWireDisc(stage.playerEntryPoint, Vector3.forward, .2f);
        Handles.Label(stage.playerEntryPoint, "PLAYER ENTRY");
        Handles.color = Color.yellow; Handles.DrawWireDisc(stage.playerExitPoint, Vector3.forward, stage.exitRadius);
        Handles.Label(stage.playerExitPoint, "STAGE EXIT");
        if (stage.IsSafeStage || stage.rewardAfterClear == StageReward.Heal)
        {
            Handles.color = Color.green; Handles.DrawWireDisc(stage.recoveryPoint, Vector3.forward, stage.recoveryRadius);
            Handles.Label(stage.recoveryPoint, "RECOVERY POINT");
        }
        if (stage.rewardAfterClear != StageReward.UpgradeChoice) return;
        Handles.color = Color.magenta; Handles.DrawWireDisc(stage.chapelSpawnPoint, Vector3.forward, stage.rewardInteractRadius);
        Handles.Label(stage.chapelSpawnPoint, "REWARD CHAPEL (after clear)");
        for (int i = 0; i < 3; i++)
        {
            var p = stage.rewardChoiceCenter + Vector2.right * ((i - 1) * stage.rewardChoiceSpacing);
            Handles.DrawWireDisc(p, Vector3.forward, stage.rewardInteractRadius);
            Handles.Label(p, $"UPGRADE CHOICE {i + 1} (after chapel)");
        }
    }

    static void DrawEncounter(StageSegmentDefinition stage, EncounterDefinition encounter, int index, bool allWaves, Dictionary<Vector2, int> stacked = null)
    {
        if (showTrigger) DrawTriggerZone(encounter);
        bool selected = !stageMode && index == encounterIndex;
        if (showCombat)
        {
            DrawRect(encounter.combatBounds, encounter.useCombatBounds ? Color.red : new Color(1, .4f, .4f, .35f));
            Handles.Label(new Vector3(encounter.combatBounds.xMin, encounter.combatBounds.yMax), "COMBAT BOUNDS / " + encounter.encounterId + (encounter.useCombatBounds ? "" : " (unused)"));
        }
        if (showCamera)
        {
            DrawRect(encounter.cameraBounds, encounter.lockCamera && encounter.useCombatBounds ? new Color(.2f, .6f, 1) : new Color(.4f, .6f, 1, .35f));
            Handles.Label(new Vector3(encounter.cameraBounds.xMin, encounter.cameraBounds.yMax), "CAMERA BOUNDS / " + encounter.encounterId + (encounter.lockCamera && encounter.useCombatBounds ? "" : " (unused)"));
        }
        if (editHandles && selected && !Application.isPlaying)
        {
            if (showTrigger) EditZone(encounter);
            if (showCombat) EditBounds(encounter, false);
            if (showCamera) EditBounds(encounter, true);
        }
        if (encounter.useCombatBounds)
        {
            if (encounter.lockStageUntilClear)
            {
                Handles.color = Color.red;
                if (encounter.exitLock != EncounterExitLock.RightOnly) Barrier(encounter.combatBounds.xMin, encounter.combatBounds);
                if (encounter.exitLock != EncounterExitLock.LeftOnly) Barrier(encounter.combatBounds.xMax, encounter.combatBounds);
            }
        }
        Vector2 labelPoint = stage.playerEntryPoint;
        if (encounter.trigger == EncounterTrigger.PlayerZone)
        {
            labelPoint = new Vector2(encounter.triggerZone.xMin, encounter.triggerZone.yMax);
        }
        Handles.Label((Vector3)labelPoint + Vector3.up * .25f, $"{stage.stageName} / {encounter.encounterId}\n{Activation(stage, index)}");
        if (stacked == null) stacked = new Dictionary<Vector2, int>();
        if (!stageMode && !selected) return;
        foreach (var wave in allWaves ? encounter.waves : SelectedWaves(encounter)) foreach (var spawn in wave.enemySpawns)
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
                    $"{encounter.encounterId} / {wave.waveId}: {(spawn.prefab ? spawn.prefab.name : "MISSING PREFAB")} #{i + 1} (+{i * Mathf.Max(0, spawn.interval):0.##}s from wave start)" + (raw != p ? " [CLAMPED]" : "") + (spawn.isBoss ? " [Boss]" : ""));
            }
        }
    }

    static void EditZone(EncounterDefinition encounter)
    {
        Handles.color = TriggerColor;
        var zone = EditRectangle(encounter.triggerZone, "Edit encounter trigger zone");
        if (zone != encounter.triggerZone) { encounter.triggerZone = zone; BoundsChanged(); }
    }

    static void Barrier(float x, Rect bounds)
    {
        Handles.DrawAAPolyLine(5, new Vector3(x, bounds.yMin), new Vector3(x, bounds.yMax));
        Handles.Label(new Vector3(x, bounds.yMin), "TEMPORARY LOCK");
    }
    static void EditBounds(EncounterDefinition encounter, bool camera)
    {
        var bounds = camera ? encounter.cameraBounds : encounter.combatBounds;
        Handles.color = camera ? new Color(.2f, .6f, 1) : Color.red;
        var changed = EditRectangle(bounds, camera ? "Edit encounter camera bounds" : "Edit encounter combat bounds");
        if (changed == bounds) return;
        if (camera) encounter.cameraBounds = changed; else encounter.combatBounds = changed;
        BoundsChanged();
    }
    static Rect EditRectangle(Rect bounds, string undo)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 center = Handles.PositionHandle(bounds.center, Quaternion.identity);
        if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(previewLevel, undo); bounds.center = center; return bounds; }
        // Four corners plus four edge midpoints. Edge handles affect only their own axis.
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
        {
            if (x == 0 && y == 0) continue;
            Vector3 point = bounds.center + new Vector2(x * bounds.width * .5f, y * bounds.height * .5f);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(point, HandleUtility.GetHandleSize(point) * .055f, Vector3.zero, Handles.RectangleHandleCap);
            if (!EditorGUI.EndChangeCheck()) continue;
            Undo.RecordObject(previewLevel, undo);
            float minX = bounds.xMin, maxX = bounds.xMax, minY = bounds.yMin, maxY = bounds.yMax;
            if (x < 0) minX = Mathf.Min(moved.x, maxX - .01f); else if (x > 0) maxX = Mathf.Max(moved.x, minX + .01f);
            if (y < 0) minY = Mathf.Min(moved.y, maxY - .01f); else if (y > 0) maxY = Mathf.Max(moved.y, minY + .01f);
            bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
        return bounds;
    }
    static void BoundsChanged()
    {
        EditorUtility.SetDirty(previewLevel); EditorApplication.delayCall += RefreshNow;
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
            EditorApplication.delayCall += RefreshNow;
        }
    }
}
