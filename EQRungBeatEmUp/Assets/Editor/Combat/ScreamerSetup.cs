using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ScreamerSetup
{
    public const string AttackPath = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Attacks/Screamer_Scream_Wave.asset";
    public const string ProfilePath = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/AI/ScreamerAIProfile.asset";
    public const string PrefabPath = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Screamer.prefab";
    public const string WavePath = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/ScreamWaveProjectile.prefab";
    const string WarningPath = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Attacks/ScreamDirectionWarning.png";
    static ScreamerSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/ScreamerSetup.request")) return;
        try { File.Delete("Temp/ScreamerSetup.request"); } catch (IOException) { return; }
        Configure();
    }
    static Sprite[] Sprites(string phase) => AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/ArtAssets/Characters/Enemies/Screamer/Animations/" + phase })
        .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("Sheet")).OrderBy(p => p)
        .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s).ToArray();
    [MenuItem("Beat Em Up/Enemies/Configure Screamer defaults")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) return;
        var attack = AssetDatabase.LoadAssetAtPath<AttackData>(AttackPath);
        var profile = AssetDatabase.LoadAssetAtPath<EnemyAIProfile>(ProfilePath);
        if (!attack || !profile) { Debug.LogError("Existing Screamer assets missing"); return; }
        if (!File.Exists(WarningPath))
        {
            var texture = new Texture2D(64, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 64; x++)
            {
                bool outline = x < 1 || x >= 63 || y < 1 || y >= 15;
                bool arrow = x >= 51 && x <= 60 && Mathf.Abs(Mathf.Abs(y - 7.5f) - (60 - x) * .65f) < .9f;
                texture.SetPixel(x, y, outline || arrow ? Color.white : Color.clear);
            }
            texture.Apply(); File.WriteAllBytes(WarningPath, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(WarningPath);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(WarningPath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
        var telegraph = Sprites("Telegraph"); var active = Sprites("Scream_Wave"); var recover = Sprites("Recovery");
        if (telegraph.Length == 0 || active.Length == 0 || recover.Length == 0) throw new System.InvalidOperationException("Screamer phase art missing");
        Undo.RecordObjects(new Object[] { attack, profile }, "Configure readable Screamer");
        attack.attackName = "Screamer Scream Wave"; attack.cooldownFrames = 120; attack.frames.Clear();
        attack.activeEvent = "SpawnScreamWave"; attack.eventActiveFrames = 8;
        attack.feedback.areaWarning = false; attack.feedback.directionalWaveWarning = true;
        attack.feedback.areaRingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WarningPath);
        attack.feedback.areaWaveSprites = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/ArtAssets/Characters/Enemies/Effects/Animations/ScreamWave" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("Sheet")).OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s).ToArray();
        attack.artworkNotes = "36f directional telegraph / 8f scream / 30f vulnerable recovery. SpawnScreamWave at frame 36 releases one forward-only swept ground rectangle from the mouth, facing locked at attack start. No radial or actor-owned melee hitboxes. Separate telegraph / scream voice hooks remain swappable.";
        for (int i = 0; i < 74; i++)
        {
            var sprites = i < 36 ? telegraph : i < 44 ? active : recover;
            int start = i < 36 ? 0 : i < 44 ? 36 : 44, length = i < 36 ? 36 : i < 44 ? 8 : 30;
            var frame = new AttackFrameData { sprite = sprites[Mathf.Min(sprites.Length - 1, (i - start) * sprites.Length / length)], movementInputScale = 0 };
            if (i == 0) frame.events.Add("Telegraph");
            if (i == 36) { frame.events.Add("Scream"); frame.events.Add("SpawnScreamWave"); }
            attack.frames.Add(frame);
        }
        var go = new GameObject("ScreamWaveProjectile"); GameObject wavePrefab;
        try
        {
            var wave = go.AddComponent<CombatProjectile>(); wave.groundWave = true; wave.waveSize = new Vector2(1.2f, 1.2f);
            wave.speed = 6; wave.lifetimeFrames = 60; wave.maximumTravelDistance = 5;
            wave.maximumTargets = 0; wave.repeatHitFrames = 0; wave.collideWithScenery = false;
            wave.flightSprites = attack.feedback.areaWaveSprites; wave.spriteHoldFrames = 3; wave.feedbackAttack = attack;
            if (wave.flightSprites.Length == 0) throw new System.InvalidOperationException("Scream wave art missing");
            wave.hit = new AttackHitboxData { damage = 14, hitType = HitType.Stun, stunDurationFrames = 90,
                launchVelocity = Vector2.zero, forceAirborneTargetDownward = false,
                canHitGrounded = true, canHitAirborne = false, hitstopFrames = 4, knockback = 0, unblockable = true, laneTolerance = .6f };
            var renderers = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var child = new GameObject("Forward sound crescent " + i); child.transform.SetParent(go.transform, false);
                child.transform.localPosition = new Vector3(.35f - i * .35f, 0, 0);
                var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = wave.flightSprites[0];
                renderer.color = new Color(1, .35f, .45f, 1 - i * .23f);
                var bounds = renderer.sprite.bounds.size;
                child.transform.localScale = new Vector3(.45f / bounds.x, wave.waveSize.y / bounds.y, 1);
                renderers[i] = renderer;
            }
            wave.visual = renderers[0]; wave.trailVisuals = renderers.Skip(1).ToArray();
            wavePrefab = PrefabUtility.SaveAsPrefabAsset(go, WavePath);
        }
        finally { Object.DestroyImmediate(go); }
        var screamer = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var spawn = screamer.GetComponent<EnemyProjectileAttack>(); if (!spawn) spawn = screamer.AddComponent<EnemyProjectileAttack>();
            spawn.projectilePrefab = wavePrefab.GetComponent<CombatProjectile>(); spawn.releaseEvent = "SpawnScreamWave";
            spawn.forwardOnly = true; spawn.releaseOffset = new Vector2(.65f, .65f);
            PrefabUtility.SaveAsPrefabAsset(screamer, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(screamer); }
        profile.meleeRange = 2.5f; profile.laneTolerance = .6f; profile.commitToAttack = true;
        profile.recoveryTime = .15f;
        foreach (var choice in profile.attacks.Where(a => a != null && a.attack == attack))
        { choice.maximumRange = 2.5f; choice.laneTolerance = .6f; choice.cooldown = .5f; choice.projectile = true; }
        EditorUtility.SetDirty(attack); EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets(); MultiplayerSetup.Build();
        Debug.Log("SCREAMER SETUP COMPLETE: directional wave; release frame 36; speed 6, full depth 1.2, distance 5; unchanged 36 / 8 / 30 cast and 2.5s cooldown.");
    }
}
