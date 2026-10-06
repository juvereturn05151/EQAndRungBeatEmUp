using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ShadowDragonSetup
{
    public const string Root = "Assets/EQ_Rung_BeatEmUp/Skills/ShadowDragon";
    public const string Art = "Assets/ArtAssets/Characters/BlueShirtGuy/ShadowDragon";
    public const string SkillPath = Root + "/ShadowDragon.asset";
    public const string CastPath = Root + "/ShadowDragonCast.asset";
    public const string ProjectilePath = Root + "/ShadowDragonProjectile.prefab";
    static ShadowDragonSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/ShadowDragonSetup.request")) return;
        try { File.Delete("Temp/ShadowDragonSetup.request"); } catch (IOException) { return; }
        try { Build(); } catch (Exception ex) { Debug.LogException(ex); }
    }
    [MenuItem("Beat Em Up/Skills/Configure Shadow Dragon defaults")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        foreach (string name in new[] { "Cast", "Dragon" })
        for (int i = 1; i <= (name == "Cast" ? 4 : 6); i++)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "/" + name + "_" + i.ToString("00") + ".png");
            if (!importer) throw new Exception("Missing generated " + name + " drawing " + i);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = name == "Cast" ? new Vector2(.5f, .0625f) : new Vector2(.85f, .5f);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        Sprite[] casts = Enumerable.Range(1, 4).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Cast_" + i.ToString("00") + ".png")).ToArray();
        Sprite[] dragons = Enumerable.Range(1, 6).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Dragon_" + i.ToString("00") + ".png")).ToArray();
        var cast = LoadOrCreate<AttackData>(CastPath);
        cast.attackName = "Shadow Dragon Cast"; cast.domain = AttackDomain.Ground; cast.cooldownFrames = 0;
        cast.frames.Clear();
        for (int frame = 0; frame < 32; frame++)
        {
            var f = new AttackFrameData { sprite = casts[frame < 6 ? 0 : frame < 12 ? 1 : frame < 20 ? 2 : 3], movementInputScale = 0 };
            if (frame == 12) { f.events.Add("Swing"); f.events.Add("SpawnDragon"); }
            cast.frames.Add(f);
        }
        cast.artworkNotes = "Original generated four-pose pixel casting art. Frames 0–11 startup, named SpawnDragon event on frame 12, 13–31 recovery. No melee boxes or cancels. Dragon animation and collision live in the shared CombatProjectile.";
        cast.feedback = new AttackFeedbackData {
            swingSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/punch_long_whoosh_21.wav"), swingVolume = .65f,
            impactSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/block_large_71.wav"), impactVolume = .8f,
            impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Plain/CFXR4 Sword Hit PLAIN (Cross).prefab"), impactScale = .32f, impactLifetime = .6f
        };
        var go = new GameObject("ShadowDragonProjectile"); GameObject prefab;
        try
        {
            var shot = go.AddComponent<CombatProjectile>(); shot.visual = go.AddComponent<SpriteRenderer>(); shot.visual.sprite = dragons[0];
            shot.emergenceSprites = new[] { dragons[0] }; shot.flightSprites = dragons.Skip(1).Take(3).ToArray(); shot.dissipateSprites = dragons.Skip(4).ToArray();
            shot.spriteHoldFrames = 4; shot.emergenceHoldFrames = 4; shot.dissipateHoldFrames = 4;
            shot.speed = 7; shot.lifetimeFrames = 90; shot.collisionRadius = .3f; shot.maximumTargets = 0; shot.repeatHitFrames = 0;
            shot.feedbackAttack = cast;
            shot.hit = new AttackHitboxData { damage = 32, hitstunFrames = 32, hitstopFrames = 7, knockback = 4, laneTolerance = .55f, hitType = HitType.Launcher, canHitGrounded = true, canHitAirborne = true, forceAirborneTargetDownward = true, launchVelocity = new Vector2(4, 5.5f) };
            prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        var skill = LoadOrCreate<PlayerSkillData>(SkillPath); skill.cast = cast; skill.projectilePrefab = prefab.GetComponent<CombatProjectile>();
        skill.meterCost = 1; skill.displayName = "Shadow Dragon"; skill.releaseEvent = "SpawnDragon"; skill.spawnOffset = new Vector2(.8f,.75f);
        var player = PrefabUtility.LoadPrefabContents(ComboTrackingSetup.PlayerPath);
        try
        {
            var meter = player.GetComponent<PlayerMeter>(); if (!meter) meter = player.AddComponent<PlayerMeter>();
            meter.maxMeter = meter.startingMeter = 1; meter.gainOnHit = .06f; meter.gainOnParry = .4f; meter.multiHitBonus = 0; meter.launcherBonus = meter.airHitBonus = .02f;
            var controller = player.GetComponent<PlayerSkillController>(); if (!controller) controller = player.AddComponent<PlayerSkillController>();
            controller.equippedSkill = skill; PrefabUtility.SaveAsPrefabAsset(player, ComboTrackingSetup.PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        EditorUtility.SetDirty(cast); EditorUtility.SetDirty(skill); AssetDatabase.SaveAssets(); MultiplayerSetup.Build();
        Debug.Log("SHADOW DRAGON SETUP COMPLETE: full one-bar meter, I / right trigger, release frame 12, generated cast + six dragon sprites, shared piercing projectile.");
    }
    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
}
