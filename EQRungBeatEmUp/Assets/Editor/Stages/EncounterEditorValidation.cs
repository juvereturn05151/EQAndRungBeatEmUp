using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EncounterEditorValidation
{
    const string Request = "Temp/EncounterEditorValidation.request", Pending = "BeatEmUp.EncounterEditorValidation";
    const string FixtureScene = "Assets/__EncounterEditorValidation.unity", FixtureLevel = "Assets/__EncounterEditorValidation.asset";
    static readonly List<string> results = new List<string>();
    static EncounterEditorValidation() { EditorApplication.update += Poll; }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static SceneObjectState State(string id, bool active, bool apply = true) => new SceneObjectState { bindingId = id, active = active, apply = apply };
    [MenuItem("Beat Em Up/Stages/Validate encounter editor and object states")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        EncounterPreview.Clear(); results.Clear();
        var existingFlow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>();
        if (!existingFlow || !existingFlow.level) { Debug.LogError("Open HauntedHouse scene before validation."); return; }
        var originalScene = SceneManager.GetActiveScene(); Scene fixture = default;
        try
        {
            Check(existingFlow.level.stages.SelectMany(s => s.encounters).All(e => e.enabled && e.waves.All(w => w.enabled)), "Existing serialized encounters/waves retain enabled defaults");
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var flow = new GameObject("Fixture Stage Flow").AddComponent<StageFlowController>();
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var encounter = new EncounterDefinition { encounterId = "Fixture", waves = new List<WaveDefinition> { new WaveDefinition { waveId = "One" }, new WaveDefinition { waveId = "Two" } } };
            level.stages.Add(new StageSegmentDefinition { encounters = new List<EncounterDefinition> { encounter } }); flow.level = level;
            AssetDatabase.CreateAsset(level, FixtureLevel);
            var door = new GameObject("Door"); var ghost = new GameObject("Ghost"); ghost.SetActive(false); var untouched = new GameObject("Unlisted");
            string doorId = EncounterObjectAuthoring.Bind(flow, door), ghostId = EncounterObjectAuthoring.Bind(flow, ghost);
            Check(doorId == EncounterObjectAuthoring.Bind(flow, door), "Binding an existing target reuses its stable ID");
            encounter.sceneObjectStates.Add(State(doorId, false));
            encounter.waves[0].sceneObjectStates.Add(State(ghostId, true));
            encounter.waves[1].sceneObjectStates.Add(State(doorId, true));
            Selection.activeObject = level;
            EncounterObjectAuthoring.Show(level, 0, 0);
            Check(!door.activeSelf && !ghost.activeSelf && untouched.activeSelf, "Encounter preview changes only listed scene objects");
            EncounterObjectAuthoring.Show(level, 0, 0, 2);
            Check(door.activeSelf && ghost.activeSelf && untouched.activeSelf, "Wave preview reproduces earlier wave states and later overrides");
            EncounterObjectAuthoring.Restore();
            Check(door.activeSelf && !ghost.activeSelf && untouched.activeSelf, "Restore Preview restores each original activeSelf");
            encounter.waves[0].enabled = false;
            EncounterObjectAuthoring.Show(level, 0, 0, 2);
            Check(!ghost.activeSelf && door.activeSelf, "Disabled wave is ignored in preview");
            encounter.waves[0].enabled = true;
            encounter.waves[1].sceneObjectStates[0].apply = false;
            EncounterObjectAuthoring.Refresh();
            Check(!door.activeSelf && ghost.activeSelf, "Apply unchecked ignores an object override");
            EncounterObjectAuthoring.Restore();
            Undo.RecordObject(level, "Fixture object checkbox"); encounter.sceneObjectStates[0].active = true; EditorUtility.SetDirty(level); Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); Check(!level.stages[0].encounters[0].sceneObjectStates[0].active, "Object checkbox supports Undo");
            Undo.PerformRedo(); Check(level.stages[0].encounters[0].sceneObjectStates[0].active, "Object checkbox supports Redo");
            var flags = new SerializedObject(level);
            var encounterProperty = flags.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("encounters").GetArrayElementAtIndex(0);
            encounterProperty.FindPropertyRelative("enabled").boolValue = false;
            encounterProperty.FindPropertyRelative("waves").GetArrayElementAtIndex(0).FindPropertyRelative("enabled").boolValue = false;
            flags.ApplyModifiedProperties();
            Check(!level.stages[0].encounters[0].enabled && !level.stages[0].encounters[0].waves[0].enabled, "Encounter/wave header checkboxes edit enabled flags");
            Undo.PerformUndo(); Check(level.stages[0].encounters[0].enabled && level.stages[0].encounters[0].waves[0].enabled, "Encounter/wave enabled edits support Undo");
            Undo.PerformRedo(); Check(!level.stages[0].encounters[0].enabled && !level.stages[0].encounters[0].waves[0].enabled, "Encounter/wave enabled edits support Redo");
            level.stages[0].encounters[0].enabled = true; level.stages[0].encounters[0].waves[0].enabled = true;
            var rectProperty = new SerializedObject(level); var trigger = rectProperty.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("encounters").GetArrayElementAtIndex(0).FindPropertyRelative("triggerZone");
            Rect moved = new Rect(2, 3, 4, 5); trigger.rectValue = moved; rectProperty.ApplyModifiedProperties();
            Check(level.stages[0].encounters[0].triggerZone == moved, "Numeric bounds write the same Rect used by handles");
            Undo.PerformUndo(); Check(level.stages[0].encounters[0].triggerZone != moved, "Bounds numeric edit supports Undo");
            Undo.PerformRedo(); Check(level.stages[0].encounters[0].triggerZone == moved, "Bounds numeric edit supports Redo");
            level.stages[0].encounters[0].sceneObjectStates[0].active = false;
            EncounterObjectAuthoring.Show(level, 0, 0, 2); EditorSceneManager.SaveScene(fixture, FixtureScene);
            Check(!EncounterObjectAuthoring.IsPreviewing && door.activeSelf && !ghost.activeSelf, "Saving a scene restores temporary preview before serialization");
            EditorUtility.SetDirty(level); AssetDatabase.SaveAssets(); EncounterPreview.Clear();
            EditorSceneManager.CloseScene(fixture, true); fixture = EditorSceneManager.OpenScene(FixtureScene, OpenSceneMode.Additive);
            var loadedFlow = fixture.GetRootGameObjects().Select(g => g.GetComponent<StageFlowController>()).First(f => f);
            Check(loadedFlow.ResolveSceneObject(doorId)?.name == "Door" && loadedFlow.ResolveSceneObject(ghostId)?.name == "Ghost", "Actual scene bindings survive scene save/reload");
            loadedFlow.ApplySceneObjectStates(loadedFlow.level.stages[0].encounters[0].sceneObjectStates);
            Check(!loadedFlow.ResolveSceneObject(doorId).activeSelf, "Reloaded level state resolves the actual reloaded scene object");
            Check(StageEditorSelection.GetIndex(existingFlow.level) >= 0, "Authoring changes preserve existing stage selection");
        }
        catch (Exception error) { results.Add("FAIL: " + error); Debug.LogException(error); }
        finally
        {
            EncounterPreview.Clear(); if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
            SceneManager.SetActiveScene(originalScene); AssetDatabase.DeleteAsset(FixtureScene); AssetDatabase.DeleteAsset(FixtureLevel);
            Selection.activeObject = existingFlow.level;
        }
        if (results.Any(r => r.StartsWith("FAIL:"))) { Write(); return; }
        SessionState.SetString(Pending + ".results", string.Join("\n", results)); SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Write() { Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/EncounterEditorValidationResults.txt", results); Debug.Log("ENCOUNTER EDITOR VALIDATION: " + results.Last()); }
    static void Poll()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(Request)) { File.Delete(Request); Run(); return; }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear(); results.AddRange(SessionState.GetString(Pending + ".results", "").Split('\n'));
        var flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); LevelDefinition original = flow.level, copy = UnityEngine.Object.Instantiate(original);
        GameObject door = null, ghost = null, untouched = null;
        var bindings = flow.sceneObjectBindings;
        try
        {
            flow.enabled = false; UnityEngine.Object.FindFirstObjectByType<CombatClock>().enabled = false; flow.player.GetComponent<PlayerCombatInput>().enabled = false;
            flow.level = copy; copy.stages.Clear();
            var stage = JsonUtility.FromJson<StageSegmentDefinition>(JsonUtility.ToJson(original.stages.First(s => !s.IsSafeStage && s.encounters.Count > 0)));
            copy.stages.Add(stage); stage.rewardAfterClear = StageReward.None; stage.completionMode = StageCompletion.ClearEncounters;
            var prefab = original.stages.SelectMany(s => s.encounters).SelectMany(e => e.waves).SelectMany(w => w.enemySpawns).First(s => s.prefab && s.prefab.GetComponent<EnemyCombat>()).prefab;
            door = new GameObject("Door runtime"); ghost = new GameObject("Ghost runtime"); ghost.SetActive(false); untouched = new GameObject("Unlisted runtime");
            flow.sceneObjectBindings = new List<SceneObjectBinding> { new SceneObjectBinding { id = "door", target = door }, new SceneObjectBinding { id = "ghost", target = ghost } };
            var a = new EncounterDefinition { encounterId = "A", useCombatBounds = true, combatBounds = new Rect(stage.movementMin, stage.movementMax - stage.movementMin), cameraBounds = new Rect(-3.8f, -.64f, 7.6f, 4), restoreSceneObjectsOnEncounterEnd = true };
            var skipped = new WaveDefinition { enabled = false, sceneObjectStates = new List<SceneObjectState> { State("ghost", true) }, enemySpawns = new List<EnemySpawnDefinition> { new EnemySpawnDefinition { prefab = prefab, count = 99 } } };
            var one = new WaveDefinition { waveId = "One", trigger = WaveTrigger.PreviousWaveClear, enemySpawns = new List<EnemySpawnDefinition> { new EnemySpawnDefinition { prefab = prefab } } };
            var two = new WaveDefinition { waveId = "Two", trigger = WaveTrigger.PreviousWaveClear, sceneObjectStates = new List<SceneObjectState> { State("door", true), State("ghost", true) }, enemySpawns = new List<EnemySpawnDefinition> { new EnemySpawnDefinition { prefab = prefab } } };
            a.sceneObjectStates.Add(State("door", false)); a.waves.AddRange(new[] { skipped, one, new WaveDefinition { enabled = false }, two });
            stage.encounters = new List<EncounterDefinition> { a }; flow.EnterStage(0); flow.Tick(.02f);
            Check(flow.LivingEnemies.Count() == 1 && !door.activeSelf && !ghost.activeSelf && untouched.activeSelf, "Encounter states apply before spawning; disabled first wave is skipped");
            Check(flow.ActiveCameraBounds == a.cameraBounds && flow.player.arenaMin == a.combatBounds.min, "Existing camera and combat bounds lock during encounter");
            foreach (var h in flow.LivingEnemies.ToArray()) h.Damage(100000); flow.Tick(.02f);
            Check(flow.LivingEnemies.Count() == 1 && door.activeSelf && ghost.activeSelf, "Next enabled wave follows previous enabled clear and overrides encounter state");
            foreach (var h in flow.LivingEnemies.ToArray()) h.Damage(100000); flow.Tick(.02f);
            Check(flow.EncountersComplete && !flow.ActiveCameraBounds.HasValue && door.activeSelf && !ghost.activeSelf && untouched.activeSelf, "Completion unlocks arena and restores original encounter object states");
            a.restoreSceneObjectsOnEncounterEnd = false; flow.EnterStage(0); flow.Tick(.02f); foreach (var h in flow.LivingEnemies.ToArray()) h.Damage(100000); flow.Tick(.02f); foreach (var h in flow.LivingEnemies.ToArray()) h.Damage(100000); flow.Tick(.02f);
            Check(door.activeSelf && ghost.activeSelf, "Restore disabled leaves final wave states intact");
            a.enabled = false; ghost.SetActive(false); flow.EnterStage(0); flow.SignalEncounter("A"); flow.Tick(.02f);
            Check(!flow.LivingEnemies.Any() && !flow.ActiveCameraBounds.HasValue && flow.EncountersComplete && !ghost.activeSelf, "Disabled encounter never triggers or applies states and defaults to completed progression");
            a.disabledBlocksProgression = true; flow.EnterStage(0); flow.Tick(.02f); Check(!flow.EncountersComplete, "Explicit disabled blocking option retains required progression gate");
            a.disabledBlocksProgression = false;
            var b = new EncounterDefinition { encounterId = "B", trigger = EncounterTrigger.PreviousEncounterClear, requiredPreviousEncounter = 0, waves = new List<WaveDefinition> { new WaveDefinition { enemySpawns = new List<EnemySpawnDefinition> { new EnemySpawnDefinition { prefab = prefab } } } } };
            stage.encounters.Add(b); flow.EnterStage(0); flow.Tick(.02f); Check(flow.ActiveEncounterName == "B" && flow.LivingEnemies.Count() == 1, "Required previous disabled encounter defaults to skipped dependency");
            a.enabled = true; b.enabled = false; skipped.enabled = true; skipped.enemySpawns[0].count = 1; flow.EnterStage(0); flow.Tick(.02f);
            Check(flow.LivingEnemies.Count() == 1 && ghost.activeSelf, "Re-enabling encounter/wave restores spawning and object states");
            a.useCombatBounds = false; a.lockStageUntilClear = false; flow.EnterStage(0); flow.Tick(.02f);
            Check(flow.LivingEnemies.Count() == 1 && !flow.ActiveCameraBounds.HasValue && flow.player.arenaMax == stage.movementMax, "Legacy spawn trigger remains compatible without arena locks");
            foreach (var wave in a.waves) wave.enabled = false;
            flow.EnterStage(0); flow.Tick(.02f);
            Check(!flow.LivingEnemies.Any() && flow.EncountersComplete, "All disabled waves complete without spawning or deadlocking");
            stage.completionMode = StageCompletion.BossDefeated; skipped.enemySpawns[0].isBoss = true;
            a.enabled = false; flow.EnterStage(0); flow.Tick(.02f);
            Check(flow.CompletionSatisfied, "Disabled authored boss does not leave BossDefeated progression waiting for a spawn");
            results.Add("PASS: Encounter editor and object state checks completed");
        }
        catch (Exception error) { results.Add("FAIL: " + error); Debug.LogException(error); }
        finally
        {
            EncounterPreview.Clear(); flow.sceneObjectBindings = bindings; flow.level = original;
            if (door) UnityEngine.Object.DestroyImmediate(door); if (ghost) UnityEngine.Object.DestroyImmediate(ghost); if (untouched) UnityEngine.Object.DestroyImmediate(untouched);
            UnityEngine.Object.DestroyImmediate(copy); Write(); EditorApplication.ExitPlaymode();
        }
    }
}
