using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public static class BossMeleeSetup
{
    public const string Root = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Attacks/";
    public const string MeleePath = Root + "Boss_TelegraphMelee.asset";
    const string Art = "Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/Enemies/WhiteGhostBoss/Animations/";
    static Sprite[] Poses(string name) => AssetDatabase.FindAssets("t:Sprite", new[] { Art + name }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("Sheet") && p.EndsWith(".png")).OrderBy(p => p, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
    static AttackData Create(string path, string name)
    {
        var data = AssetDatabase.LoadAssetAtPath<AttackData>(path); if (data) return data;
        data = ScriptableObject.CreateInstance<AttackData>(); data.attackName = name; AssetDatabase.CreateAsset(data, path); return data;
    }
    [MenuItem("Tools/Combat/Add Boss Telegraph Melee")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;
        var encounter = AssetDatabase.LoadAssetAtPath<BossEncounterData>(BossEncounterSetup.DataPath);
        var existing = AssetDatabase.LoadAssetAtPath<AttackData>(Root + "Boss_Swipe.asset");
        var attack = AssetDatabase.LoadAssetAtPath<AttackData>(MeleePath);
        if (!attack)
        {
            attack = Create(MeleePath, "White Ghost — Telegraph Sweep"); EditorUtility.CopySerialized(existing, attack);
            attack.attackName = "White Ghost — Telegraph Sweep"; attack.frames.Clear(); attack.cooldownFrames = 150; attack.cooldownOnInterrupt = true;
            AuthorSweep(attack);
            encounter.meleeInterrupt.lastFrame = 53;
            EditorUtility.SetDirty(attack);
        }
        var hitReaction = Reaction("Boss_MeleeHitStagger", 30, false); var parryReaction = Reaction("Boss_MeleeParried", 60, true);
        if (attack.name == "Boss_Swipe") { attack.name = "Boss_TelegraphMelee"; EditorUtility.SetDirty(attack); }
        var feedback = AssetDatabase.LoadAssetAtPath<AttackData>(Root + "Boss_MeleeInterruptFeedback.asset");
        if (!feedback) { feedback = Create(Root + "Boss_MeleeInterruptFeedback.asset", "Boss physical interrupt"); EditorUtility.CopySerialized(encounter.shieldHitFeedback, feedback); feedback.attackName = "Boss physical interrupt"; feedback.frames.Clear(); EditorUtility.SetDirty(feedback); }
        if (feedback.name == "Boss_ShieldHitFeedback") { feedback.name = "Boss_MeleeInterruptFeedback"; EditorUtility.SetDirty(feedback); }
        encounter.useDistanceWeights = true;
        encounter.meleeInterrupt.hitReaction = hitReaction; encounter.meleeInterrupt.parryReaction = parryReaction; encounter.meleeInterrupt.interruptFeedback = feedback;
        foreach (var phase in new[] { encounter.phase1, encounter.phase2 })
        {
            if (!phase.Any(c => c.action == BossAction.TelegraphMelee)) phase.Add(new BossActionChoice { action = BossAction.TelegraphMelee, attack = attack, weight = 0, closeRangeWeight = 65 });
            if (!phase.Any(c => c.action == BossAction.Teleport)) phase.Add(new BossActionChoice { action = BossAction.Teleport, weight = 15, closeRangeWeight = 10 });
            bool second = phase == encounter.phase2;
            foreach (var choice in phase)
            {
                if (choice.action == BossAction.Book) { choice.weight = second ? 35 : 55; choice.closeRangeWeight = second ? 10 : 15; }
                if (choice.action == BossAction.CurseWave) { choice.weight = 20; choice.closeRangeWeight = 5; }
                if (choice.action == BossAction.Swipe) { choice.weight = 0; choice.closeRangeWeight = 3; }
                if (choice.action == BossAction.SummonRusher) { choice.weight = second ? 10 : 30; choice.closeRangeWeight = second ? 4 : 10; }
                if (choice.action == BossAction.SummonStrongGhosts) { choice.weight = 20; choice.closeRangeWeight = 6; }
                // Existing frame-data fields supply cooldowns, including shared summon cooldown.
                if (choice.attack && choice.action != BossAction.TelegraphMelee && choice.attack.cooldownFrames == 0)
                { choice.attack.cooldownFrames = choice.action == BossAction.Swipe ? 120 : choice.action == BossAction.Book ? 90 : choice.action == BossAction.CurseWave ? 180 : 270; EditorUtility.SetDirty(choice.attack); }
            }
        }
        EditorUtility.SetDirty(encounter); AssetDatabase.SaveAssets();
        var catalog = AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        var additions = new[] { attack, hitReaction, parryReaction, feedback }; catalog.attacks = catalog.attacks.Concat(additions.Where(a => !catalog.attacks.Contains(a))).ToArray();
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); RefreshHash(catalog);
    }
    [MenuItem("Tools/Combat/Tune Boss Telegraph Impact")]
    public static void TuneImpact()
    {
        if (EditorApplication.isPlaying) return;
        var attack = AssetDatabase.LoadAssetAtPath<AttackData>(MeleePath);
        if (!attack) { Build(); return; }
        AuthorSweep(attack);
        var encounter = AssetDatabase.LoadAssetAtPath<BossEncounterData>(BossEncounterSetup.DataPath);
        encounter.meleeInterrupt.lastFrame = 53;
        EditorUtility.SetDirty(attack); EditorUtility.SetDirty(encounter); AssetDatabase.SaveAssets();
        RefreshHash(AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath));
    }
    static void AuthorSweep(AttackData attack)
    {
        var swipe = Poses("Attack_Swipe"); var recovery = Poses("Recovery_Rise");
        attack.frames.Clear();
        for (int frame = 0; frame < 112; frame++)
        {
            var pose = frame < 40 ? swipe[0] : frame < 60 ? swipe[1] : frame < 70 ? swipe[2 + Mathf.Min(1, (frame - 60) / 5)] : recovery[Mathf.Min(recovery.Length - 1, (frame - 70) * recovery.Length / 42)];
            var entry = new AttackFrameData { sprite = pose, movementInputScale = 0, superArmor = frame >= 60 && frame < 70 };
            if (frame == 0) entry.events.Add("Telegraph"); if (frame == 60) entry.events.Add("Swing");
            if (frame >= 60 && frame < 70) entry.hitboxes.Add(new AttackHitboxData { hitId = 0, repeatAfterFrames = 0, groundArea = true, offset = new Vector2(.7f, 0), size = new Vector2(2.1f, 1), laneTolerance = .5f, damage = 36, hitstunFrames = 30, hitstopFrames = 10, knockback = 5, canHitGrounded = true, canHitAirborne = false, canBeParried = true, blockstunFrames = 18 });
            attack.frames.Add(entry);
        }
        var existing = AssetDatabase.LoadAssetAtPath<AttackData>(Root + "Boss_Swipe.asset");
        attack.feedback.areaWarning = true; attack.feedback.directionalWaveWarning = false;
        attack.feedback.areaRingSprite = AssetDatabase.LoadAssetAtPath<PlayerGroundIndicatorStyle>(PlayerGroundIndicatorSetup.StylePath).RingSprite;
        attack.feedback.warningColor = new Color(1, .28f, .12f, .8f); attack.feedback.waveColor = new Color(1, .35f, .2f, .5f);
        attack.feedback.telegraphSound = existing.feedback.swingSound; attack.feedback.areaVolume = .45f;
        attack.feedback.impactPrefab = SweepImpactPrefab();
        attack.feedback.impactSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/face_hit_finisher_73.wav");
        attack.feedback.impactRotateWithFacing = false;
        attack.feedback.impactVolume = .9f; attack.feedback.impactScale = .9f; attack.feedback.impactLifetime = .45f;
        attack.feedback.impactShakeStrength = .06f; attack.feedback.impactShakeDuration = .16f;
        attack.artworkNotes = "60 startup / 10 active / 42 recovery, zero-based active 60–69. Longer authored wind-up from existing Attack_Swipe poses. 36 damage, 5 recoil, 10 hitstop; shared pixel burst, heavy hit sound and small shake on confirmed impact only.";
    }
    static GameObject SweepImpactPrefab()
    {
        const string path = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/BossSweepImpactFeedback.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab) return prefab;
        var root = new GameObject("BossSweepImpactFeedback");
        try
        {
            var effect = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/BossStunBurstFeedback.prefab"));
            effect.transform.SetParent(root.transform, false);
            // Ground-area hit points are at floor height. Keep the shared animated
            // burst intact, but present this physical impact near the player's torso.
            effect.transform.localPosition = new Vector3(0, .9f, 0);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static AttackData Reaction(string name, int frames, bool parry)
    {
        string path = Root + name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<AttackData>(path); if (existing) return existing;
        var data = Create(path, name); var hurt = Poses("Hurt_Heavy"); var stagger = Poses("Stunned");
        for (int i = 0; i < frames; i++) data.frames.Add(new AttackFrameData { sprite = i < (parry ? 12 : 8) ? hurt[Mathf.Min(hurt.Length - 1, i / 3)] : stagger[Mathf.Min(stagger.Length - 1, (i - 8) * stagger.Length / Mathf.Max(1, frames - 8))], movementInputScale = 0 });
        data.artworkNotes = "Cosmetic boss punish pose timeline using existing Hurt_Heavy / Stunned sprites; no separate enemy stun or knockdown system."; EditorUtility.SetDirty(data); return data;
    }
    static void RefreshHash(MultiplayerCatalog catalog)
    {
        var paths = AssetDatabase.GetDependencies(new[] { ComboTrackingSetup.PlayerPath, HauntedLevelBuilder.LevelPath }.Concat(catalog.characters.Select(AssetDatabase.GetAssetPath)).ToArray(), true).OrderBy(p => p, StringComparer.Ordinal);
        string contents = "GhostFairProtocol5|" + string.Join("|", paths.Select(p => p + ":" + AssetDatabase.GetAssetDependencyHash(p))) + "|" + string.Join("|", catalog.sprites.Select(s => AssetDatabase.GetAssetPath(s) + ":" + s.name));
        contents += "|" + string.Join("|", catalog.selectionCharacters.Where(c => c).Select(c => AssetDatabase.GetAssetPath(c) + ":" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(c))));
        contents += "|" + string.Join("|", Directory.GetFiles("Assets/EQ_Rung_BeatEmUp/Scripts", "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).Select(p => p + ":" + File.ReadAllText(p))) + "|" + File.ReadAllText("Packages/manifest.json");
        using (var sha = SHA256.Create()) catalog.contentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(contents))).Replace("-", ""); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
    }
}
