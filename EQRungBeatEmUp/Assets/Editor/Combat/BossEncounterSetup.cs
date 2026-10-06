using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class BossEncounterSetup
{
    public const string Root = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/";
    public const string DataPath = Root + "BossEncounter.asset";
    public const string BossPath = Root + "Prefabs/WhiteGhostBoss.prefab";
    static BossEncounterSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/BossSetup.request")) return;
        File.Delete("Temp/BossSetup.request");
        try { Build(); File.WriteAllText("Temp/BossSetup.result", "PASS"); }
        catch (Exception e) { File.WriteAllText("Temp/BossSetup.result", e.ToString()); Debug.LogException(e); }
    }
    static Sprite[] Sprites(string folder) => AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/ArtAssets/Characters/Enemies/" + folder })
        .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".png") && !p.Contains("Sheet"))
        .OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s).ToArray();
    static AttackData Timeline(string name, string art, int startup, int active, int recovery, string signal, bool melee = false)
    {
        string path = Root + "Attacks/Boss_" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<AttackData>(path); if (existing) return existing;
        var data = ScriptableObject.CreateInstance<AttackData>(); data.attackName = "Boss " + name;
        var sprites = Sprites("WhiteGhostBoss/Animations/" + art);
        if (sprites.Length == 0) throw new InvalidOperationException("Missing boss art: " + art);
        data.activeEvent = signal; data.eventActiveFrames = active;
        var sound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/punch_long_whoosh_21.wav");
        data.feedback.swingSound = sound; data.feedback.telegraphSound = sound;
        for (int i = 0; i < startup + active + recovery; i++)
        {
            var frame = new AttackFrameData { sprite = sprites[Mathf.Min(sprites.Length - 1, i * sprites.Length / (startup + active + recovery))], movementInputScale = 0 };
            if (i == 0) frame.events.Add("Telegraph");
            if (i == startup) { frame.events.Add("Swing"); if (!string.IsNullOrEmpty(signal)) frame.events.Add(signal); }
            if (melee && i >= startup && i < startup + active) frame.hitboxes.Add(new AttackHitboxData { damage = 18, hitstunFrames = 20, knockback = 1.2f, offset = new Vector2(.65f, .55f), size = new Vector2(1.35f, 1.1f), laneTolerance = .5f, canBeParried = true });
            data.frames.Add(frame);
        }
        data.artworkNotes = startup + " startup / " + active + " active / " + recovery + " recovery. Uses shared AttackPlayer; edit with Attack Data Editor.";
        AssetDatabase.CreateAsset(data, path); return data;
    }
    static CombatProjectile Projectile(string name, string sourcePath, string art, Color color)
    {
        string path = Root + "Prefabs/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing.GetComponent<CombatProjectile>();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath); if (!source) throw new InvalidOperationException("Missing shared projectile prefab: " + sourcePath);
        var go = UnityEngine.Object.Instantiate(source); go.name = name;
        try
        {
            var p = go.GetComponent<CombatProjectile>(); p.flightSprites = Sprites("Effects/Animations/" + art);
            if (p.flightSprites.Length == 0) throw new InvalidOperationException("Missing projectile art: " + art);
            p.visual.sprite = p.flightSprites[0]; p.visual.color = color;
            if (p.trailVisuals != null) foreach (var trail in p.trailVisuals) if (trail) { trail.sprite = p.flightSprites[0]; trail.color = color; }
            p.hit.canBeParried = true; p.hit.unblockable = false; p.canBeDeflected = true; p.canHitOriginalOwner = true;
            p.hit.damage = p.groundWave ? 20 : 14;
            if (p.groundWave) { p.waveSize = new Vector2(1.5f, .7f); p.hit.hitType = HitType.Stun; p.hit.stunDurationFrames = 90; }
            return PrefabUtility.SaveAsPrefabAsset(go, path).GetComponent<CombatProjectile>();
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    static AttackData Feedback(string name, AttackData source)
    {
        string path = Root + "Attacks/Boss_" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<AttackData>(path); if (asset) return asset;
        asset = ScriptableObject.CreateInstance<AttackData>(); asset.attackName = "Boss " + name;
        var parry = AssetDatabase.LoadAssetAtPath<AttackData>(Root + "Attacks/Projectile_DeflectFeedback.asset");
        if (parry) EditorUtility.CopySerialized(parry, asset);
        asset.attackName = "Boss " + name;
        if (source && !asset.feedback.impactSound) asset.feedback.impactSound = source.feedback.swingSound;
        AssetDatabase.CreateAsset(asset, path); return asset;
    }
    static void EnsureFeedbackVisual(AttackData feedback, string art)
    {
        if (!feedback || feedback.feedback.impactPrefab) return;
        string path = Root + "Prefabs/Boss" + art + "Feedback.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var sprites = Sprites("Effects/Animations/" + art);
        if (sprites.Length == 0) throw new InvalidOperationException("Missing feedback art: " + art);
        if (!prefab)
        {
            string clipPath = Root + "Attacks/Boss" + art + "Feedback.anim";
            var clip = new AnimationClip { frameRate = 60, name = "Boss " + art };
            AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" },
                sprites.Select((s, i) => new ObjectReferenceKeyframe { time = i / 20f, value = s }).ToArray());
            AssetDatabase.CreateAsset(clip, clipPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Root + "Attacks/Boss" + art + "Feedback.controller");
            controller.AddMotion(clip);
            var go = new GameObject("Boss " + art + " feedback");
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprites[0]; renderer.sortingOrder = 150;
                go.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        feedback.feedback.impactPrefab = prefab; feedback.feedback.impactScale = 1.1f / sprites[0].bounds.size.x; feedback.feedback.impactLifetime = .4f;
        EditorUtility.SetDirty(feedback);
    }
    [MenuItem("Tools/Combat/Configure First Boss Encounter")]
    public static void Build()
    {
        if (Application.isPlaying) return;
        var data = AssetDatabase.LoadAssetAtPath<BossEncounterData>(DataPath);
        if (!data)
        {
            data = ScriptableObject.CreateInstance<BossEncounterData>();
            var book = Timeline("Book", "Attack_CurseWave", 30, 1, 24, "ThrowProjectile");
            var swipe = Timeline("Swipe", "Attack_Swipe", 24, 6, 30, null, true);
            var summon = Timeline("Summon", "Attack_Summon", 36, 1, 30, "BossSummon");
            var curse = Timeline("CurseWave", "Attack_CurseWave", 36, 8, 30, "SpawnScreamWave");
            var scream = AssetDatabase.LoadAssetAtPath<AttackData>(ScreamerSetup.AttackPath);
            if (scream) { curse.feedback.directionalWaveWarning = true; curse.feedback.areaRingSprite = scream.feedback.areaRingSprite; curse.feedback.warningColor = new Color(.6f, .2f, 1, .85f); EditorUtility.SetDirty(curse); }
            data.bookProjectile = Projectile("BossBookProjectile", ThrowerProjectileSetup.ProjectilePath, "Notebook", new Color(.7f, .4f, 1));
            data.curseWaveProjectile = Projectile("BossCurseWaveProjectile", ScreamerSetup.WavePath, "CurseWave", new Color(.6f, .2f, 1));
            data.phase1.Add(new BossActionChoice { action = BossAction.Book, attack = book });
            data.phase1.Add(new BossActionChoice { action = BossAction.Swipe, attack = swipe });
            data.phase1.Add(new BossActionChoice { action = BossAction.SummonRusher, attack = summon });
            foreach (var c in data.phase1) data.phase2.Add(new BossActionChoice { action = c.action, attack = c.attack });
            data.phase2.Add(new BossActionChoice { action = BossAction.CurseWave, attack = curse });
            data.phase2.Add(new BossActionChoice { action = BossAction.SummonStrongGhosts, attack = summon });
            string[] labels = { "Top Left", "Top Right", "Bottom Left", "Bottom Right", "Center" };
            Vector2[] points = { new Vector2(-2.15f,.45f), new Vector2(2.15f,.45f), new Vector2(-2.15f,-.25f), new Vector2(2.15f,-.25f), new Vector2(0,.1f) };
            for (int i = 0; i < points.Length; i++) data.warpPoints.Add(new BossWarpPoint { label = "Warp " + (i + 1) + " - " + labels[i], position = points[i] });
            data.minionSpawnPoints.AddRange(new[] { new Vector2(-2.7f,.45f), new Vector2(2.7f,.45f), new Vector2(-2.7f,-.2f), new Vector2(2.7f,-.2f) });
            data.rusherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Rusher.prefab");
            data.grapplerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/GrapplerBruiser.prefab");
            data.throwerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Thrower.prefab");
            data.warpOutFeedback = Feedback("WarpOutFeedback", book); data.warpInFeedback = Feedback("WarpInFeedback", book);
            data.phaseFeedback = Feedback("PhaseFeedback", summon); data.shieldHitFeedback = Feedback("ShieldHitFeedback", swipe);
            data.vulnerableFeedback = Feedback("VulnerableFeedback", swipe); data.totemBreakFeedback = Feedback("TotemBreakFeedback", swipe);
            AssetDatabase.CreateAsset(data, DataPath); EditorUtility.SetDirty(data);
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Root + "ThaiHauntedHouse.asset");
            var stage = level.stages.First(s => s.stageId == "Stage07_WhiteGhostBossChamber");
            Undo.RecordObject(level, "Configure first boss Totems");
            var totems = stage.destructibles.Where(p => p.kind == PropKind.CursedTotem).ToList();
            for (int i = 0; i < totems.Count && i < 4; i++) totems[i].position = points[i];
            stage.notes = "Break a nearby Totem: its expanding radial front must reach the boss to open a 420-frame damage window. Totems respawn by default. Defeat boss and remaining minions to unlock exit.";
            EditorUtility.SetDirty(level);
        }
        var boss = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var controller = boss.GetComponent<TotemBossController>(); if (!controller) controller = boss.AddComponent<TotemBossController>(); controller.data = data;
            var ranged = boss.GetComponent<EnemyProjectileAttack>(); if (!ranged) ranged = boss.AddComponent<EnemyProjectileAttack>();
            ranged.projectilePrefab = data.bookProjectile; ranged.lockAimAtAttackStart = true;
            PrefabUtility.SaveAsPrefabAsset(boss, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        EnsureFeedbackVisual(data.warpOutFeedback, "SummonSeal"); EnsureFeedbackVisual(data.warpInFeedback, "SummonSeal");
        EnsureFeedbackVisual(data.phaseFeedback, "CurseWave"); EnsureFeedbackVisual(data.shieldHitFeedback, "StunBurst");
        EnsureFeedbackVisual(data.vulnerableFeedback, "StunBurst"); EnsureFeedbackVisual(data.totemBreakFeedback, "SummonSeal");
        AssetDatabase.SaveAssets(); Debug.Log("BOSS SETUP COMPLETE: shared projectiles, 5 warp points, 4 radial Totems, two weighted phases.");
    }
}
