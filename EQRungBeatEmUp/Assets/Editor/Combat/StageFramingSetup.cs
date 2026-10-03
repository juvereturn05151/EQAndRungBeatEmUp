using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageFramingSetup
{
    public const string ScenePath = "Assets/EQ_Rung_BeatEmUp/Scenes/ComboDemo.unity";
    // Batch authoring entry point: touches only the existing gameplay scene.
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var camera = Object.FindFirstObjectByType<Camera>();
        var framing = camera.GetComponent<StageFraming>();
        if (!framing) framing = camera.gameObject.AddComponent<StageFraming>();
        framing.player = Object.FindFirstObjectByType<ComboController>().motor;
        framing.floor = GameObject.Find("Walking arena (XY lane, height is separate)").GetComponent<SpriteRenderer>();
        camera.allowMSAA = false;
        foreach (var motor in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None))
        {
            framing.ApplyLaneBounds(motor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
        }
        framing.ApplyFraming(0, true);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("STAGE FRAMING CONFIGURED: size 2, center Y 1.36, lanes -0.4..0.65");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
