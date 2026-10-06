using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EncounterZoneValidation
{
    const string Pending = "BeatEmUp.EncounterZoneValidation";
    const string Request = "Temp/EncounterZoneValidation.request";
    static readonly List<string> results = new List<string>();
    static StageFlowController flow;
    static EncounterZoneValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Stages/Validate combat encounter zones (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EncounterPreview.Clear(); EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        results.Clear();
        var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
        int selection = StageEditorSelection.GetIndex(asset); string before = EditorJsonUtility.ToJson(asset);
        EncounterPreview.BeginStage(asset, selection); EncounterPreview.RefreshNow();
        Check(StageEditorSelection.GetIndex(asset) == selection && EncounterScenePreview.IsActive, "Live preview refresh preserves selected stage");
        var preview = Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == "Encounter Scene Preview (temporary)");
        Check(preview.GetComponentsInChildren<EnemyCombat>(true).Length == 0 && preview.GetComponentsInChildren<Collider2D>(true).Length == 0, "Edit preview contains only render copies without combat or colliders");
        EncounterPreview.Clear(); Check(before == EditorJsonUtility.ToJson(asset), "Preview / clear does not alter authored level data");
        SessionState.SetString(Pending + ".preview", string.Join("\n", results));
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static void Step(float seconds = .02f) { flow.Tick(seconds); flow.framing.ApplyFraming(seconds, true); Check(string.IsNullOrEmpty(flow.Failure), "Runtime has no spawn / stage error"); }
    static void Defeat() { foreach (var enemy in flow.LivingEnemies.ToArray()) enemy.Damage(100000); }
    static void Place(Vector2 position) { flow.player.ResetForStage(position); }
    static void Poll()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(Request))
        { try { File.Delete(Request); } catch (IOException) { return; } Run(); return; }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false);
        results.Clear(); results.AddRange(SessionState.GetString(Pending + ".preview", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
        LevelDefinition original = null, copy = null;
        GameObject secondPlayer = null;
        try
        {
            flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>();
            flow.enabled = false; UnityEngine.Object.FindFirstObjectByType<CombatClock>().enabled = false;
            flow.player.GetComponent<PlayerCombatInput>().enabled = false;
            original = flow.level; copy = UnityEngine.Object.Instantiate(original); flow.level = copy;
            foreach (var stage in copy.stages) stage.rewardAfterClear = StageReward.None;
            int entrance = copy.stages.FindIndex(s => s.stageId == "Stage01_EntranceGate");
            var stage1 = copy.stages[entrance]; var a = stage1.encounters[0]; var b = stage1.encounters[1];
            Check(a.useCombatBounds && b.useCombatBounds && a.waves[0].enemySpawns[0].count == 2 && b.waves[0].enemySpawns.Sum(s => s.count) == 3, "Entrance Gate authors two independent zones with requested Rusher / Thrower counts");
            flow.EnterStage(entrance); Step();
            Check(!flow.LivingEnemies.Any() && !flow.ActiveCameraBounds.HasValue && flow.player.arenaMin == stage1.movementMin, "Traversal starts unlocked with no encounter enemies");
            Place(stage1.playerExitPoint); Step(); Check(!flow.ExitUnlocked && flow.StageIndex == entrance, "Exit cannot bypass untriggered required encounters");
            Place(a.triggerZone.center); Step(.3f);
            Check(flow.LivingEnemies.Count() == 2 && flow.ActiveCameraBounds == a.cameraBounds, "A entry activates camera lock and spawns two Rushers");
            Check(flow.player.arenaMin.x == a.combatBounds.xMin && flow.player.arenaMax.x == a.combatBounds.xMax, "A locks player progression to its combat area");
            flow.player.MoveInput = Vector2.right; flow.player.Simulate(10);
            Check(flow.player.transform.position.x <= a.combatBounds.xMax, "Running cannot pass the active right barrier");
            flow.player.MoveInput = Vector2.left; flow.player.Simulate(10);
            Check(flow.player.transform.position.x >= a.combatBounds.xMin, "Running cannot pass the active left barrier"); flow.player.MoveInput = Vector2.zero;
            var camera = flow.framing.GetComponent<Camera>(); float half = camera.orthographicSize * camera.aspect;
            Check(camera.transform.position.x - half >= a.cameraBounds.xMin - .01f && camera.transform.position.x + half <= a.cameraBounds.xMax + .01f, $"Existing safe-zone camera keeps horizontal viewport inside encounter bounds (X {camera.transform.position.x}, half {half}, rect {camera.rect}, aspect {camera.aspect}, screen {Screen.width}x{Screen.height})");
            Defeat(); Step(); Check(!flow.ActiveCameraBounds.HasValue && flow.player.arenaMax == stage1.movementMax && !flow.ExitUnlocked, "Clearing A restores traversal while B still gates stage exit");
            Place(b.triggerZone.center); Step(.3f); Check(flow.LivingEnemies.Count() == 3 && flow.ActiveEncounterName == b.encounterId, "B independently locks and spawns two Rushers plus one Thrower");
            Defeat(); Step(); Check(flow.ExitUnlocked && !flow.ActiveCameraBounds.HasValue, "Clearing B releases camera / progression and unlocks exit");
            Place(a.triggerZone.center); Step(); Check(!flow.LivingEnemies.Any(), "One-shot encounter does not respawn on revisiting its trigger");
            Place(stage1.playerExitPoint); Step(); Check(flow.StageIndex != entrance, "Player reaches exit and advances after both encounters clear");

            // Delayed next-wave fixture: pending counts must prevent premature clear.
            a.waves.Add(new WaveDefinition { waveId = "Delayed wave", trigger = WaveTrigger.PreviousWaveClear, spawnDelay = 1,
                enemySpawns = new List<EnemySpawnDefinition> { new EnemySpawnDefinition { prefab = a.waves[0].enemySpawns[0].prefab, count = 2, interval = .5f, spawnDelay = .2f, spawnPoints = new List<Vector2> { a.combatBounds.center } } } });
            flow.SimulateEncounter(entrance, 0); Step(.3f); Defeat(); Step();
            Check(flow.ActiveCameraBounds.HasValue && !flow.LivingEnemies.Any(), "Camera stays locked while next wave awaits previous-clear delay");
            Step(.9f); Check(!flow.LivingEnemies.Any(), "Clear-based wave respects one-second delay");
            Step(.15f); Check(!flow.LivingEnemies.Any(), "Enemy spawn delay applies after wave start");
            Step(.1f); Check(flow.LivingEnemies.Count() == 1, "Delayed wave spawns first enemy after group delay");
            Defeat(); Step(.1f); Check(flow.ActiveCameraBounds.HasValue, "Queued second enemy prevents encounter from clearing");
            Step(.4f); Check(flow.LivingEnemies.Count() == 1, "Per-enemy interval spawns queued second enemy"); Defeat(); Step();
            Check(!flow.ActiveCameraBounds.HasValue, "Final wave death releases arena");
            flow.SimulateEncounter(entrance, 1); Step(.3f);
            Check(flow.ActiveEncounterName == b.encounterId && flow.LivingEnemies.Count() == 3, "Simulation skips A and starts B directly using real runtime");
            Defeat(); Step();

            // Traversal compatibility uses the same scheduler with arena locking disabled.
            a.waves.RemoveAt(1); a.useCombatBounds = false; a.lockStageUntilClear = false; a.requiredForCompletion = false;
            stage1.encounters.RemoveAt(1); flow.EnterStage(entrance); Place(a.triggerZone.center); Step(.3f);
            Check(flow.LivingEnemies.Count() == 2 && !flow.ActiveCameraBounds.HasValue && flow.player.arenaMax == stage1.movementMax && flow.ExitUnlocked, "Simple Spawn Trigger spawns enemies without camera / progression / exit lock");
            a.useCombatBounds = true; a.lockStageUntilClear = true; a.requiredForCompletion = true; a.oneShot = false;
            flow.EnterStage(entrance); Place(a.triggerZone.center); Step(.3f); Defeat(); Step(); Place(stage1.playerEntryPoint); Step(); Place(a.triggerZone.center); Step(.3f);
            Check(flow.LivingEnemies.Count() == 2, "Repeatable encounter re-arms only after players leave its trigger"); Defeat(); Step();
            Place(stage1.playerEntryPoint); Step(); Check(flow.ExitUnlocked, "Completed repeatable encounter does not permanently gate stage exit when re-armed");
            a.oneShot = true; a.clearCondition = EncounterClearCondition.ManualSignal;
            flow.SimulateEncounter(entrance, 0); Step(.3f); Defeat(); Step(); Check(flow.ActiveCameraBounds.HasValue, "Manual clear condition retains arena after enemies die");
            flow.SignalEncounterClear(a.encounterId); Step(); Check(!flow.ActiveCameraBounds.HasValue, "Explicit clear signal releases manual encounter after all waves clear");
            a.clearCondition = EncounterClearCondition.AllEnemiesDefeated;
            a.exitLock = EncounterExitLock.RightOnly;
            flow.SimulateEncounter(entrance, 0); Step(.3f);
            Check(flow.player.arenaMin.x == stage1.movementMin.x && flow.player.arenaMax.x == a.combatBounds.xMax, "Right-only exit lock preserves left traversal bound"); Defeat(); Step();
            a.exitLock = EncounterExitLock.LeftOnly;
            flow.SimulateEncounter(entrance, 0); Step(.3f);
            Check(flow.player.arenaMin.x == a.combatBounds.xMin && flow.player.arenaMax.x == stage1.movementMax.x, "Left-only exit lock preserves right traversal bound"); Defeat(); Step();
            a.exitLock = EncounterExitLock.BothSides;
            flow.EnterStage(entrance); Place(new Vector2(a.triggerZone.xMax + .5f, a.triggerZone.center.y)); Step(.3f);
            Check(flow.ActiveEncounterName == a.encounterId, "Crossing entire trigger between ticks still activates encounter"); Defeat(); Step();

            // Any local player can trigger; both players are brought inside and constrained.
            if (!flow.player.GetComponent<PlayerIdentity>()) flow.player.gameObject.AddComponent<PlayerIdentity>();
            secondPlayer = UnityEngine.Object.Instantiate(flow.player.gameObject); secondPlayer.GetComponent<PlayerIdentity>().slot = 1;
            var second = secondPlayer.GetComponent<CharacterMotor>(); flow.EnterStage(entrance);
            second.ResetForStage(a.triggerZone.center); Step(.3f);
            Check(flow.ActiveEncounterName == a.encounterId && flow.Players.Count() == 2 && flow.Players.All(p => p.arenaMin.x == a.combatBounds.xMin && p.transform.position.x >= a.combatBounds.xMin && p.transform.position.x <= a.combatBounds.xMax), "Second local player activates encounter and both players stay in arena");
            results.Add("PASS: All combat encounter zone checks completed");
        }
        catch (Exception error) { results.Add("FAIL: " + error); Debug.LogException(error); }
        finally
        {
            if (secondPlayer) UnityEngine.Object.DestroyImmediate(secondPlayer);
            if (flow && original) flow.level = original;
            if (copy) UnityEngine.Object.DestroyImmediate(copy);
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/EncounterZoneValidationResults.txt", results);
            Debug.Log("ENCOUNTER ZONE VALIDATION: " + results.Last());
            if (Application.isBatchMode) EditorApplication.Exit(results.Any(r => r.StartsWith("FAIL:")) ? 1 : 0); else EditorApplication.ExitPlaymode();
        }
    }
}
