using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Validate imported art in a disposable preview scene without changing the user's scene.
[InitializeOnLoad]
public static class HubSkyExtensionValidation
{
    const string Request = "Temp/HubSkyExtensionValidation.request";
    const string Output = "Documentation/HubSkyExtensionPreview";
    static HubSkyExtensionValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Run();
    }
    [MenuItem("Beat Em Up/Hub/Validate upward art coverage")]
    public static void Run()
    {
        var results = new List<string>();
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture render = null;
        var previous = RenderTexture.active;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSanctuarySetup.EnvironmentPath);
            var environment = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var definition = AssetDatabase.LoadAssetAtPath<PlayerHubDefinition>(PlayerSanctuarySetup.DefinitionPath);
            var panels = environment.GetComponentsInChildren<SpriteRenderer>().Where(r => definition.panels.Contains(r.sprite)).OrderBy(r => r.transform.position.x).ToArray();
            if (panels.Length != 5) throw new Exception("Expected five environment panels.");
            foreach (var panel in panels)
            {
                if (panel.sprite.texture.width != 1672 || panel.sprite.texture.height != 1672 || panel.sprite.texture.filterMode != FilterMode.Point)
                    throw new Exception("Unexpected import dimensions/filter: " + panel.name);
                if (Mathf.Abs(panel.bounds.min.y + .8f) > .003f || panel.bounds.max.y < 7.29f)
                    throw new Exception("Ground anchor or upper coverage changed: " + panel.name + " " + panel.bounds);
                results.Add("PASS: " + panel.name + " imported at 1672x1672; ground -0.8, canopy 7.3; point filtering.");
            }
            var cameraObject = new GameObject("Hub coverage preview camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene;
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            render = new RenderTexture(960, 540, 24); camera.targetTexture = render;
            Directory.CreateDirectory(Output);
            // Test a view expanded to 6.3, well above the normal 3.36 camera top.
            // Cover all stations, seams, and both stage edges at the enlarged view.
            const float bottom = -.64f, top = 6.3f;
            camera.orthographicSize = (top - bottom) * .5f;
            float halfWidth = camera.orthographicSize * 960f / 540f;
            var positions = new float[] { -19 + halfWidth, -11.4f, -7.6f, -3.8f, 0, 3.8f, 7.6f, 11.4f, 19 - halfWidth };
            for (int i = 0; i < positions.Length; i++)
            {
                float x = Mathf.Clamp(positions[i], -19 + halfWidth, 19 - halfWidth);
                camera.transform.position = new Vector3(x, (top + bottom) * .5f, -10);
                camera.Render(); RenderTexture.active = render;
                var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                    if (image.GetPixels32().Any(p => p.r > 250 && p.g < 5 && p.b > 250))
                        throw new Exception("Empty camera background at view " + i);
                    File.WriteAllBytes(Output + "/JumpCoverage" + i + ".png", image.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                results.Add("PASS: Enlarged jump view " + i + " at x=" + x + " has no exposed clear background, including seams/edges.");
            }
            results.Add("ALL HUB SKY COVERAGE CHECKS PASSED");
        }
        catch (Exception error) { results.Add("FAIL: " + error); Debug.LogException(error); }
        finally
        {
            RenderTexture.active = previous;
            if (render) { render.Release(); UnityEngine.Object.DestroyImmediate(render); }
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllLines("Documentation/HubSkyExtensionValidationResults.txt", results);
            Debug.Log(string.Join("\n", results));
        }
    }
}
