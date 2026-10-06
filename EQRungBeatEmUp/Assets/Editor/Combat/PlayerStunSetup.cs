using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerStunSetup
{
    public const string ArtPath = "Assets/ArtAssets/Characters/BlueShirtGuy/Stun";
    public const string DefensePath = "Assets/EQ_Rung_BeatEmUp/PlayerDefense.asset";
    static PlayerStunSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/PlayerStunSetup.request")) return;
        try { File.Delete("Temp/PlayerStunSetup.request"); } catch (IOException) { return; }
        Configure();
    }
    [MenuItem("Beat Em Up/Configure player stun artwork")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) return;
        AssetDatabase.Refresh();
        foreach (var path in Directory.GetFiles(ArtPath, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.spritePixelsPerUnit = 100;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = path.Contains("StunVfx") ? new Vector2(.5f,.5f) : new Vector2(.5f,.0625f);
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        var data = AssetDatabase.LoadAssetAtPath<PlayerDefenseData>(DefensePath);
        if (!data) throw new System.InvalidOperationException("Player Defense Data missing");
        data.stunned = Enumerable.Range(1,5).Select(i => new CharacterPoseHold {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "/Stun_" + i.ToString("00") + ".png"), frames = 6 }).ToArray();
        data.stunVfx = Enumerable.Range(1,6).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "/StunVfx_" + i.ToString("00") + ".png")).ToArray();
        if (data.stunned.Any(p => !p.sprite) || data.stunVfx.Any(s => !s)) throw new System.InvalidOperationException("Stun art incomplete");
        data.stunVfxHoldFrames = 5; data.stunVfxOffset = new Vector2(0,1.2f); data.stunVfxScale = 1;
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
        ScreamerSetup.Configure();
        Debug.Log("PLAYER STUN CONFIGURED: 90f status, 5 body poses / 6 overhead VFX frames; multiplayer catalog refreshed");
    }
}
