using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds only art preview artifacts. Does not alter existing combat prefabs or scenes.
public static class HauntedEnemyArtBuilder
{
    private const string Root = "Assets/ArtAssets/Characters/Enemies";
    [Serializable] private sealed class Manifest { public string id; public List<Sequence> animations; }
    [Serializable] private sealed class Sequence { public string name; public int frameCount; public bool loop; public List<Frame> frames; }
    [Serializable] private sealed class Frame { public string file; }
    private static readonly string[] Characters = { "Rusher", "GrapplerBruiser", "Thrower", "Screamer", "Ambusher", "Prefect", "WhiteGhostBoss" };
    [MenuItem("Beat Em Up/Art/Build and validate haunted enemy previews")]
    public static void BuildAndValidate()
    {
        if (EditorApplication.isPlaying) return;
        var results = new List<string>();
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int frames = 0, clips = 0;
            foreach (string folder in Characters.Concat(new[] { "Totems", "Effects" }))
            {
                string basePath = Root + "/" + folder;
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(basePath + "/manifest.json"));
                var motions = new Dictionary<string, AnimationClip>();
                foreach (var animation in manifest.animations)
                {
                    string prefix = basePath + "/Animations/" + animation.name + "/";
                    foreach (var frame in animation.frames)
                    {
                        string assetPath = prefix + frame.file;
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                        if (!sprite || importer == null || sprite.pixelsPerUnit != 100 || importer.filterMode != FilterMode.Point ||
                            importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
                            throw new InvalidOperationException("Invalid sprite import: " + assetPath);
                        frames++;
                    }
                    string clipPath = prefix + folder + "_" + animation.name + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (!clip || clip.length <= 0 || clip.isLooping != animation.loop) throw new InvalidOperationException("Invalid clip: " + clipPath);
                    var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    if (bindings.Length != 1 || bindings[0].path != "" || bindings[0].type != typeof(SpriteRenderer))
                        throw new InvalidOperationException("Invalid sprite binding: " + clipPath);
                    if (AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]).Any(k => !k.value))
                        throw new InvalidOperationException("Missing frame reference: " + clipPath);
                    motions[animation.name] = clip; clips++;
                }
                results.Add("PASS: " + folder + " imports " + manifest.animations.Count + " clips and " + manifest.animations.Sum(a => a.frameCount) + " sprite frames");
                if (!Characters.Contains(folder)) continue;
                string unityPath = basePath + "/Unity";
                if (!AssetDatabase.IsValidFolder(unityPath)) AssetDatabase.CreateFolder(basePath, "Unity");
                string controllerPath = unityPath + "/" + folder + "_ArtPreview.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                foreach (var child in machine.states) machine.RemoveState(child.state);
                var states = new Dictionary<string, AnimatorState>();
                foreach (var pair in motions) { var state = machine.AddState(pair.Key); state.motion = pair.Value; states[pair.Key] = state; }
                string idle = folder == "Ambusher" ? "Idle_Crouched" : folder == "WhiteGhostBoss" ? "Idle_Float" : "Idle";
                foreach (var pair in new Dictionary<string, string> {
                    { "Idle", idle }, { "Walk", folder == "WhiteGhostBoss" ? "Glide" : "Walk" },
                    { "GroundHit", "Hurt_Light" }, { "AirHit", "Air_Hit" }, { "Launched", "Air_Hit" },
                    { "Falling", "Air_Hit" }, { "Landing", "Knockdown" }
                }) if (!states.ContainsKey(pair.Key)) { var state = machine.AddState(pair.Key); state.motion = motions[pair.Value]; states[pair.Key] = state; }
                machine.defaultState = states[idle]; EditorUtility.SetDirty(controller);
                var preview = new GameObject(folder + " (art preview)");
                try
                {
                    var visual = new GameObject("Visual"); visual.transform.SetParent(preview.transform, false);
                    var renderer = visual.AddComponent<SpriteRenderer>();
                    var sequence = manifest.animations.First(a => a.name == idle);
                    renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "/Animations/" + idle + "/" + sequence.frames[0].file);
                    visual.AddComponent<Animator>().runtimeAnimatorController = controller;
                    string prefabPath = unityPath + "/" + folder + "_ArtPreview.prefab";
                    PrefabUtility.SaveAsPrefabAsset(preview, prefabPath);
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (!saved || !saved.GetComponentInChildren<SpriteRenderer>().sprite || !saved.GetComponentInChildren<Animator>().runtimeAnimatorController)
                        throw new InvalidOperationException("Invalid preview prefab: " + prefabPath);
                    results.Add("PASS: " + folder + " art preview prefab/controller with runtime reaction aliases");
                }
                finally { UnityEngine.Object.DestroyImmediate(preview); }
            }
            AssetDatabase.SaveAssets();
            results.Add("PASS: TOTAL " + frames + " sprites / " + clips + " clips, point filter, 100 PPU, uncompressed, complete SpriteRenderer references");
            File.WriteAllLines(Root + "/UnityImportValidationResults.txt", results);
            Debug.Log("HAUNTED ENEMY ART VALIDATION PASSED: " + frames + " sprites / " + clips + " clips");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception); File.WriteAllLines(Root + "/UnityImportValidationResults.txt", results); Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
