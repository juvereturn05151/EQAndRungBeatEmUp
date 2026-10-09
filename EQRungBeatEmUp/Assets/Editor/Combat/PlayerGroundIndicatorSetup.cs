using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlayerGroundIndicatorSetup
{
    public const string ArtRoot = "Assets/EQ_Rung_BeatEmUp/ArtAssets/UI/PlayerGroundIndicators";
    public const string StylePath = "Assets/EQ_Rung_BeatEmUp/Resources/PlayerGroundIndicatorStyle.asset";
    public const string PrefabPath = "Assets/EQ_Rung_BeatEmUp/Prefabs/PlayerGroundIndicator.prefab";
    [MenuItem("Beat Em Up/Players/Build ground indicators")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Build ground indicators outside Play mode."); return; }
        Directory.CreateDirectory(ArtRoot); AssetDatabase.Refresh();
        var sprites = new Sprite[5];
        for (int i = 0; i < sprites.Length; i++) sprites[i] = WriteSprite(i == 0 ? "BlobShadow" : "PlayerRing" + i, i);
        var style = AssetDatabase.LoadAssetAtPath<PlayerGroundIndicatorStyle>(StylePath);
        if (!style) { style = ScriptableObject.CreateInstance<PlayerGroundIndicatorStyle>(); AssetDatabase.CreateAsset(style, StylePath); }
        style.shadowSprite = sprites[0]; style.ringSprites = sprites.Skip(1).ToArray(); EditorUtility.SetDirty(style);
        var root = new GameObject("Player Ground Indicator");
        try
        {
            var indicator = root.AddComponent<PlayerGroundIndicator>(); indicator.style = style;
            indicator.shadow = Renderer("Blob Shadow", root.transform); indicator.ring = Renderer("Player Color Ring", root.transform);
            indicator.Refresh(); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        var catalog = AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        var prefabs = AssetDatabase.FindAssets("t:PlayableCharacterData").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<PlayableCharacterData>).Where(c => c && c.prefab).Select(c => c.prefab)
            .Concat(catalog.selectionCharacters.Where(c => c && c.Prefab).Select(c => c.Prefab)).Append(catalog.playerPrefab).Distinct();
        foreach (var prefab in prefabs)
        {
            string path = AssetDatabase.GetAssetPath(prefab); var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var indicator = contents.GetComponentInChildren<PlayerGroundIndicator>(true);
                if (!indicator) indicator = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), contents.transform)).GetComponent<PlayerGroundIndicator>();
                indicator.motor = contents.GetComponent<CharacterMotor>(); if (!indicator.style) indicator.style = style;
                indicator.Refresh(); PrefabUtility.RecordPrefabInstancePropertyModifications(indicator);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        // Append the five sprites; retain every existing network asset identifier.
        catalog.sprites = catalog.sprites.Concat(sprites.Where(s => !catalog.sprites.Contains(s))).ToArray();
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); RefreshHash(catalog);
    }
    static SpriteRenderer Renderer(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.AddComponent<SpriteRenderer>();
    }
    static Sprite WriteSprite(string name, int thickness)
    {
        const int width = 64, height = 32;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            float dx = x + .5f - width * .5f, dy = y + .5f - height * .5f;
            float distance = dx * dx / (31f * 31f) + dy * dy / (15f * 15f);
            byte alpha = 0;
            if (thickness == 0)
                alpha = distance < .45f ? (byte)230 : distance < .65f ? (byte)190 : distance < .82f ? (byte)115 : distance < 1 ? (byte)45 : (byte)0;
            else
            {
                float rx = 31 - thickness, ry = 15 - thickness * .5f;
                float inner = dx * dx / (rx * rx) + dy * dy / (ry * ry);
                if (distance <= 1 && inner >= 1) alpha = 255;
            }
            pixels[y * width + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels); texture.Apply(); string path = ArtRoot + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100; importer.spritePivot = new Vector2(.5f, .5f);
        importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void RefreshHash(MultiplayerCatalog catalog)
    {
        var paths = AssetDatabase.GetDependencies(new[] { ComboTrackingSetup.PlayerPath, HauntedLevelBuilder.LevelPath }.Concat(catalog.characters.Select(AssetDatabase.GetAssetPath)).ToArray(), true).OrderBy(p => p, StringComparer.Ordinal);
        string contents = "GhostFairProtocol5|" + string.Join("|", paths.Select(p => p + ":" + AssetDatabase.GetAssetDependencyHash(p))) + "|" + string.Join("|", catalog.sprites.Select(s => AssetDatabase.GetAssetPath(s) + ":" + s.name));
        contents += "|" + string.Join("|", catalog.selectionCharacters.Where(c => c).Select(c => AssetDatabase.GetAssetPath(c) + ":" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(c))));
        contents += "|" + string.Join("|", Directory.GetFiles("Assets/EQ_Rung_BeatEmUp/Scripts", "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).Select(p => p + ":" + File.ReadAllText(p))) + "|" + File.ReadAllText("Packages/manifest.json");
        using (var sha = SHA256.Create()) catalog.contentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(contents))).Replace("-", "");
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
    }
}
