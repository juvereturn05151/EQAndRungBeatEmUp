using System.Collections.Generic;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Render-only copies: no prefab behaviours, colliders, AI, or ExecuteAlways callbacks.
public static class EncounterScenePreview
{
    static GameObject root;
    static readonly List<GameObject> hidden = new List<GameObject>();
    static Scene scene;
    public static bool IsActive => root;

    public static void Show(LevelDefinition level, StageSegmentDefinition stage, IEnumerable<WaveDefinition> waves, bool showRewards = false)
    {
        if (Application.isPlaying) return;
        Clear();
        scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        try
        {
            // SceneVisibility affects editor display only, not active/enabled flags or saved scene data.
            foreach (var sceneRoot in scene.GetRootGameObjects())
                foreach (var renderer in sceneRoot.GetComponentsInChildren<Renderer>(true))
                    if (!SceneVisibilityManager.instance.IsHidden(renderer.gameObject) && !hidden.Contains(renderer.gameObject))
                    {
                        hidden.Add(renderer.gameObject);
                        SceneVisibilityManager.instance.Hide(renderer.gameObject, false);
                    }
            root = NewObject("Encounter Scene Preview (temporary)", null);
            SceneManager.MoveGameObjectToScene(root, scene);
            Plate("Background", stage.backgroundSprite, stage.artWidth, stage.backgroundHeight, stage.backgroundCenterY, -1000);
            Plate("Floor", stage.floorSprite, stage.artWidth, stage.floorHeight, stage.floorCenterY, -900);
            foreach (var prop in stage.decorativeProps)
                if (prop.prefab) Visual(prop.prefab, prop.position, prop.prefab.name);
            foreach (var prop in stage.IsSafeStage ? Enumerable.Empty<DestructiblePlacement>() : stage.destructibles)
            {
                if (prop.prefab) Visual(prop.prefab, prop.position, prop.label);
                else if (prop.intactSprite)
                {
                    var go = NewObject(prop.label, root.transform); go.transform.position = prop.position;
                    var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = prop.intactSprite;
                    renderer.sortingOrder = Mathf.RoundToInt(-prop.position.y * 100);
                }
            }
            var flow = Object.FindObjectsByType<StageFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.gameObject.scene == scene && f.level == level);
            if (flow && flow.player) Visual(flow.player.gameObject, stage.playerEntryPoint, "Player entry preview");
            if (showRewards && stage.rewardAfterClear == StageReward.UpgradeChoice)
            {
                var chapel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/Rewards/RewardChapel.prefab");
                if (chapel) Visual(chapel, stage.chapelSpawnPoint, "Reward chapel (after clear)");
            }
            foreach (var wave in waves) foreach (var spawn in wave.enemySpawns)
                if (spawn.prefab) for (int i = 0; i < Mathf.Max(1, spawn.count); i++)
                    Visual(spawn.prefab, EncounterPreview.SpawnPosition(stage, spawn, i), $"{wave.waveId} / {spawn.prefab.name} #{i + 1}");
            SceneView.RepaintAll();
        }
        catch
        {
            Clear(); throw;
        }
    }

    static GameObject NewObject(string name, Transform parent)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        if (parent) go.transform.SetParent(parent, false);
        return go;
    }

    static void Plate(string name, Sprite sprite, float width, float height, float y, int order)
    {
        if (!sprite) return;
        var go = NewObject(name, root.transform);
        go.transform.position = new Vector3(0, y, 0);
        go.transform.localScale = new Vector3(Mathf.Max(.01f, width) / Mathf.Max(.0001f, sprite.bounds.size.x), Mathf.Max(.01f, height) / Mathf.Max(.0001f, sprite.bounds.size.y), 1);
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
    }

    static void Visual(GameObject source, Vector2 position, string name)
    {
        var go = CopyVisual(source.transform, root.transform);
        go.name = name; go.transform.position = position; go.transform.rotation = Quaternion.identity;
        foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.sortingOrder = Mathf.RoundToInt(-position.y * 100);
    }

    static GameObject CopyVisual(Transform source, Transform parent)
    {
        var go = NewObject(source.name, parent);
        go.transform.localPosition = source.localPosition;
        go.transform.localRotation = source.localRotation;
        go.transform.localScale = source.localScale;
        var sprite = source.GetComponent<SpriteRenderer>();
        if (sprite) EditorUtility.CopySerialized(sprite, go.AddComponent<SpriteRenderer>());
        foreach (Transform child in source) CopyVisual(child, go.transform);
        go.SetActive(source.gameObject.activeSelf);
        return go;
    }

    public static void Clear()
    {
        if (root) Object.DestroyImmediate(root);
        root = null;
        foreach (var go in hidden) if (go) SceneVisibilityManager.instance.Show(go, false);
        hidden.Clear(); SceneView.RepaintAll();
    }
}
