using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class StageArtLayoutValidation
{
    const string Pending = "BeatEmUp.StageArtLayoutValidation";
    const string TemporaryAsset = "Assets/StageArtLayoutValidationTemporary.asset";
    static readonly List<string> results = new List<string>();
    static LevelDefinition original, copy;
    static int phase;
    static StageArtLayoutValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Stages/Validate per-stage art layout (Play Mode)")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath);
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
    static void Plate(SpriteRenderer renderer, float height, float y, string label)
    {
        Check(Near(renderer.bounds.size.y, height) && Near(renderer.bounds.center.y, y), label);
    }
    static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>();
            if (phase == 0)
            {
                results.Clear(); original = flow.level;
                Check(flow.StageIndex == 0, "Scene starts in first hub");
                Check(original.stages.All(s => Near(s.backgroundHeight, 1.6f) && Near(s.backgroundCenterY, 2.56f) && Near(s.floorHeight, 2.4f) && Near(s.floorCenterY, .56f)), "All nine existing stages retain baseline layout");
                var fresh = new StageSegmentDefinition();
                Check(Near(fresh.backgroundHeight, 1.6f) && Near(fresh.floorHeight, 2.4f), "New stages receive baseline defaults");
                // Load an actual legacy YAML asset without any of the four new serialized fields.
                var legacy = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(HauntedLevelBuilder.LevelPath), @"^    (backgroundHeight|backgroundCenterY|floorHeight|floorCenterY):[^\r\n]+\r?\n", "", System.Text.RegularExpressions.RegexOptions.Multiline);
                File.WriteAllText(TemporaryAsset, legacy); AssetDatabase.ImportAsset(TemporaryAsset, ImportAssetOptions.ForceSynchronousImport);
                var loadedLegacy = AssetDatabase.LoadAssetAtPath<LevelDefinition>(TemporaryAsset);
                Check(loadedLegacy.stages.All(s => Near(s.backgroundHeight, 1.6f) && Near(s.backgroundCenterY, 2.56f) && Near(s.floorHeight, 2.4f) && Near(s.floorCenterY, .56f)), "Legacy assets with missing layout fields load baseline defaults");
                AssetDatabase.DeleteAsset(TemporaryAsset);
                copy = UnityEngine.Object.Instantiate(original); flow.level = copy;
                flow.GetComponent<RunUpgradeController>().enabled = false;
                UnityEngine.Object.FindFirstObjectByType<CombatClock>().enabled = false;
                var hub = copy.stages[0]; var min = hub.movementMin; var max = hub.movementMax;
                hub.backgroundHeight = 2; hub.backgroundCenterY = 2.36f; hub.floorHeight = 2; hub.floorCenterY = .36f;
                flow.EnterStage(0);
                Plate(flow.background, 2, 2.36f, "Hub uses authored background dimensions at entry");
                Plate(flow.floor, 2, .36f, "Hub uses authored floor dimensions at entry");
                Check(Near(flow.background.bounds.min.y, flow.floor.bounds.max.y), "50/50 layout meets at seam");
                Check(flow.player.arenaMin == min && flow.player.arenaMax == max && flow.player.GetComponent<CharacterHealth>().SafeStageProtection, "Art changes preserve movement bounds and hub safety");
                var serialized = new SerializedObject(copy); var stage = serialized.FindProperty("stages").GetArrayElementAtIndex(0);
                Check(stage.FindPropertyRelative("backgroundHeight") != null && stage.FindPropertyRelative("floorCenterY") != null, "Fields available to Unity stage Inspector");
                hub.backgroundHeight = 1.8f; hub.backgroundCenterY = 2.46f; hub.floorHeight = 2.2f; hub.floorCenterY = .46f;
                phase = 1; return; // Give MonoBehaviour.LateUpdate a real rendered frame.
            }
            Plate(flow.background, 1.8f, 2.46f, "Live asset edits update background without restarting stage");
            Plate(flow.floor, 2.2f, .46f, "Live asset edits update floor without restarting stage");
            Check(flow.StageIndex == 0 && !flow.LivingEnemies.Any(), "Live preview does not advance stage or spawn enemies");
            flow.EnterStage(1);
            Plate(flow.background, 1.6f, 2.56f, "Next stage independently retains default background");
            Plate(flow.floor, 2.4f, .56f, "Next stage independently retains default floor");
            flow.EnterStage(0);
            Plate(flow.floor, 2.2f, .46f, "Returning to hub reapplies its custom layout");
            // Persist a disposable asset and reimport it, without touching the authored level.
            var saved = UnityEngine.Object.Instantiate(copy); AssetDatabase.CreateAsset(saved, TemporaryAsset); AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TemporaryAsset, ImportAssetOptions.ForceSynchronousImport);
            var persisted = AssetDatabase.LoadAssetAtPath<LevelDefinition>(TemporaryAsset);
            Check(Near(persisted.stages[0].backgroundHeight, 1.8f) && Near(persisted.stages[0].floorCenterY, .46f) && Near(persisted.stages[1].floorHeight, 2.4f), "Custom per-stage values survive asset save/reimport");
            flow.level = original; flow.EnterStage(0); UnityEngine.Object.DestroyImmediate(copy); copy = null;
            AssetDatabase.DeleteAsset(TemporaryAsset); Finish(0);
        }
        catch (Exception ex)
        {
            results.Add("FAIL: " + ex); Debug.LogException(ex);
            var flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); if (flow && original) flow.level = original;
            if (copy) UnityEngine.Object.DestroyImmediate(copy);
            AssetDatabase.DeleteAsset(TemporaryAsset); Finish(1);
        }
    }
    static void Finish(int code)
    {
        phase = 0; SessionState.SetBool(Pending, false);
        Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/StageArtLayoutValidationResults.txt", results);
        Debug.Log("STAGE ART LAYOUT VALIDATION " + (code == 0 ? "PASSED" : "FAILED") + ": " + results.Count);
        if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }
}
