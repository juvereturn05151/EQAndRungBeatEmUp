using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class HauntedStageArtBuilder
{
    private const string Root = "Assets/ArtAssets/Environments/HauntedHouse";
    private static readonly string[] Stages = { "Stage01_EntranceGate", "Stage02_BloodSheetCorridor", "Stage03_FakeMorgue", "Stage04_ServiceCorridor", "Stage05_HauntedMaze", "Stage06_RecoveryShrine", "Stage07_WhiteGhostBossChamber", "Stage08_EscapeLane" };

    [MenuItem("Beat Em Up/Art/Build and validate haunted stage previews")]
    public static void BuildAndValidate()
    {
        var results = new List<string>();
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string stage in Stages)
            {
                var preview = new GameObject(stage + " (art preview)");
                try
                {
                    foreach (string layer in new[] { "Background", "Floor" })
                    {
                        string path = Root + "/" + stage + "/" + layer + "/" + stage + "_" + layer + ".png";
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        int expectedHeight = layer == "Background" ? 160 : 240;
                        if (!sprite || importer == null || sprite.rect.width != 720 || sprite.rect.height != expectedHeight ||
                            sprite.pixelsPerUnit != 100 || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                            importer.textureCompression != TextureImporterCompression.Uncompressed || importer.spritePivot != new Vector2(.5f, .5f))
                            throw new InvalidOperationException("Invalid stage sprite: " + path);
                        var child = new GameObject(layer); child.transform.SetParent(preview.transform, false);
                        child.transform.localPosition = new Vector3(0, layer == "Background" ? 2.56f : .56f, 0);
                        var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                        renderer.sortingOrder = layer == "Background" ? -100 : -90;
                    }
                    var background = preview.transform.Find("Background").GetComponent<SpriteRenderer>();
                    var floor = preview.transform.Find("Floor").GetComponent<SpriteRenderer>();
                    if (Mathf.Abs(background.bounds.min.y - floor.bounds.max.y) > .0001f || Mathf.Abs(floor.bounds.min.y + .64f) > .0001f || Mathf.Abs(background.bounds.max.y - 3.36f) > .0001f)
                        throw new InvalidOperationException("Stage plate seam/framing mismatch: " + stage);
                    string dir = Root + "/" + stage + "/Unity";
                    if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root + "/" + stage, "Unity");
                    string prefab = dir + "/" + stage + "_ArtPreview.prefab";
                    PrefabUtility.SaveAsPrefabAsset(preview, prefab);
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                    if (!saved || saved.GetComponentsInChildren<SpriteRenderer>().Length != 2) throw new InvalidOperationException("Invalid stage prefab: " + stage);
                    results.Add("PASS: " + stage + " separate background/floor, 100 PPU, Point, uncompressed, centered pivots, exact seam and 40/60 framing, preview prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(preview); }
            }
            AssetDatabase.SaveAssets();
            results.Add("PASS: TOTAL 8 stages / 16 gameplay plates / 8 art preview prefabs");
            File.WriteAllLines(Root + "/UnityImportValidationResults.txt", results);
            Debug.Log("HAUNTED STAGE ART VALIDATION PASSED");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception); File.WriteAllLines(Root + "/UnityImportValidationResults.txt", results); Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
