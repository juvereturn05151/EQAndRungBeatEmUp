using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CombatWallSetup
{
    [MenuItem("Beat Em Up/Add sample combat walls to demo")]
    public static void AddToDemo()
    {
        if (EditorApplication.isPlaying) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(CombatDemoBuilder.ScenePath);
        if (Object.FindObjectsByType<CombatWall>().Length > 0) return;
        // Physical colliders at the world arena ends, independent of camera framing.
        Add("Combat Wall Left", -6.1f);
        Add("Combat Wall Right", 6.1f);
        EditorSceneManager.SaveScene(scene);
    }
    private static void Add(string name, float x)
    {
        var root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Add sample combat wall");
        root.transform.position = new Vector3(x, -.5f, 0);
        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(.2f, 4);
        root.AddComponent<CombatWall>();
    }
}
