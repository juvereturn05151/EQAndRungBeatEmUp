using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HauntedLevelBuilder
{
    public const string Root = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse";
    public const string ScenePath = "Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity";
    public const string LevelPath = Root + "/ThaiHauntedHouse.asset";
    private const string Art = "Assets/ArtAssets/Characters/Enemies/";
    private const string StageArt = "Assets/ArtAssets/Environments/HauntedHouse/";
    private static readonly string[] Enemies = { "Rusher", "GrapplerBruiser", "Thrower", "Screamer", "Ambusher", "Prefect", "WhiteGhostBoss" };
    private static readonly string[] Attacks = { "Attack_Slash1", "HeavyAttack", "Throw_Notebook", "Scream_Wave", "LegTrip", "BatonAttack", "Attack_Swipe" };
    private static readonly string[] Stages = { "Stage01_EntranceGate", "Stage02_BloodSheetCorridor", "Stage03_FakeMorgue", "Stage04_ServiceCorridor", "Stage05_HauntedMaze", "Stage06_RecoveryShrine", "Stage07_WhiteGhostBossChamber", "Stage08_EscapeLane" };
    private static readonly string[] Names = { "Entrance Gate", "Blood Sheet Corridor", "Fake Morgue", "Service Corridor & Stair", "Haunted Maze", "Recovery Shrine / Safe Room", "White Ghost Boss Chamber", "Escape Lane (Exit)" };
    private static T Load<T>(string path) where T : UnityEngine.Object { var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (!asset) throw new Exception("Missing asset: " + path); return asset; }
    private static Sprite Pose(string enemy, string state, int frame = 1) => Load<Sprite>(Art + enemy + "/Animations/" + state + "/" + enemy + "_" + state + "_" + frame.ToString("00") + ".png");
    private static AnimationClip Clip(string enemy, string state) => Load<AnimationClip>(Art + enemy + "/Animations/" + state + "/" + enemy + "_" + state + ".anim");
    [MenuItem("Beat Em Up/Stages/Create haunted-house playable level")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Root + "/Prefabs"); Directory.CreateDirectory(Root + "/Attacks"); AssetDatabase.Refresh();
        var enemies = new Dictionary<string, GameObject>();
        for (int i = 0; i < Enemies.Length; i++) enemies[Enemies[i]] = BuildEnemy(i);
        var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
        if (!level)
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>(); AssetDatabase.CreateAsset(level, LevelPath);
            for (int i = 0; i < Stages.Length; i++)
            {
                string id = Stages[i];
                var stage = new StageSegmentDefinition { stageId = id, stageName = Names[i], stageType = i == 5 ? StageType.Safe : i == 6 ? StageType.Boss : i == 7 ? StageType.Exit : StageType.Combat, backgroundSprite = Load<Sprite>(StageArt + id + "/Background/" + id + "_Background.png"), floorSprite = Load<Sprite>(StageArt + id + "/Floor/" + id + "_Floor.png") };
                level.stages.Add(stage);
            }
            AddEncounter(level.stages[0], Wave(enemies, ("Rusher", 2)), Wave(enemies, ("Rusher", 1)));
            var timed = Wave(enemies, ("Rusher", 3)); timed.trigger = WaveTrigger.Time; timed.spawnDelay = 3;
            AddEncounter(level.stages[1], timed, Wave(enemies, ("Thrower", 1)));
            AddEncounter(level.stages[2], Wave(enemies, ("Rusher", 2), ("Thrower", 1), ("Screamer", 1)));
            AddEncounter(level.stages[3], Wave(enemies, ("Rusher", 2), ("GrapplerBruiser", 1)));
            var lurking = Wave(enemies, ("Ambusher", 1), ("Prefect", 1), ("Rusher", 2));
            AddEncounter(level.stages[4], lurking, Wave(enemies, ("Thrower", 1), ("Screamer", 1)));
            level.stages[5].safeRoom = true; level.stages[5].completionMode = StageCompletion.ReachExit;
            var boss = Wave(enemies, ("WhiteGhostBoss", 1)); boss.enemySpawns[0].isBoss = true;
            AddEncounter(level.stages[6], boss); level.stages[6].completionMode = StageCompletion.BossDefeated;
            level.stages[7].completionMode = StageCompletion.ReachExit;
            for (int i = 0; i < 5; i++)
            {
                level.stages[i].destructibles.Add(Prop("Breakable haunted prop", new Vector2(-1.45f, -.25f), PropKind.Normal, 15));
                level.stages[i].destructibles.Add(Prop("Breakable haunted prop", new Vector2(1.45f, .4f), PropKind.Normal, 15));
            }
            foreach (var point in new[] { new Vector2(-1.6f, -.25f), new Vector2(-.65f, .45f), new Vector2(.65f, .45f), new Vector2(1.6f, -.25f) }) level.stages[6].destructibles.Add(Prop("Cursed totem", point, PropKind.CursedTotem, 20));
            level.stages[5].notes = "No enemies. E / gamepad Select near shrine restores health; walk right when ready.";
            level.stages[6].notes = "Four cursed totems protect the boss. Break all, defeat the vulnerable boss, then leave right.";
            EditorUtility.SetDirty(level);
        }
        // Preserve edited authored data on subsequent builder runs.
        if (File.Exists(PlayerHubSetup.ArtPath)) PlayerHubSetup.Configure(level);
        if (!File.Exists(ScenePath)) BuildScene(level);
        var existing = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList(); existing.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = existing.ToArray();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("HAUNTED LEVEL BUILT: " + LevelPath + " / " + ScenePath);
    }
    private static GameObject BuildEnemy(int index)
    {
        string enemy = Enemies[index], path = Root + "/Prefabs/" + enemy + ".prefab";
        if (File.Exists(path)) return Load<GameObject>(path);
        var attack = BuildAttack(enemy, Attacks[index], index == 2 ? 1.7f : index == 3 ? 1.5f : 1);
        var root = PrefabUtility.LoadPrefabContents("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab");
        try
        {
            root.name = enemy;
            var motor = root.GetComponent<CharacterMotor>(); var health = root.GetComponent<CharacterHealth>();
            var combat = root.GetComponent<EnemyCombat>(); var reaction = root.GetComponent<EnemyHitReaction>();
            var animation = root.GetComponent<CharacterAnimation>();
            var visualAnimator = motor.visual.GetComponent<Animator>();
            visualAnimator.runtimeAnimatorController = Load<RuntimeAnimatorController>(Art + enemy + "/Unity/" + enemy + "_ArtPreview.controller"); animation.animator = visualAnimator;
            string idle = index == 4 ? "Idle_Crouched" : index == 6 ? "Idle_Float" : "Idle";
            motor.sprite.sprite = Pose(enemy, idle); motor.moveSpeed = index == 0 ? 2.3f : index == 4 ? 1.1f : 1.6f;
            health.maximumHealth = index == 6 ? 120 : index == 1 ? 45 : index == 5 ? 30 : 25;
            reaction.knockdownClip = Clip(enemy, "Knockdown"); reaction.getUpClip = Clip(enemy, "GetUp");
            reaction.airborneSprite = Pose(enemy, "Air_Hit"); reaction.downedSprite = Pose(enemy, "Downed");
            combat.passiveTrainingDummy = false; combat.target = null; combat.attack = attack; combat.attackRange = index == 2 ? 1.7f : index == 3 ? 1.5f : .85f; combat.attackCooldownFrames = 110;
            var hurt = root.GetComponentInChildren<CombatHurtbox>(); hurt.externalInvulnerable = false;
            var box = hurt.GetComponent<BoxCollider2D>(); float h = index == 4 ? .65f : index == 6 ? 1.5f : 1.05f;
            box.size = new Vector2(index == 1 ? .65f : .45f, h); box.offset = new Vector2(0, h * .5f);
            if (index == 6) root.AddComponent<TotemBossController>();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static AttackData BuildAttack(string enemy, string state, float range)
    {
        string path = Root + "/Attacks/" + enemy + "_" + state + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<AttackData>(path); if (asset) return asset;
        var clip = Clip(enemy, state); var keys = AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip)[0]);
        asset = ScriptableObject.CreateInstance<AttackData>(); asset.attackName = enemy + " " + state; asset.cooldownFrames = 30;
        asset.artworkNotes = "Prototype primary move built from the supplied animation timing. Edit hitboxes/timing in Frame Attack editor; existing player frame data is unchanged.";
        int count = Mathf.Max(1, Mathf.CeilToInt(clip.length * 60));
        for (int f = 0; f < count; f++)
        {
            var frame = new AttackFrameData { sprite = keys.Last(k => k.time <= f / 60f + .00001f).value as Sprite, movementInputScale = 0 };
            if (f >= count * .35f && f <= count * .55f) frame.hitboxes.Add(new AttackHitboxData { damage = enemy == "WhiteGhostBoss" ? 7 : 4, hitstunFrames = 14, hitstopFrames = 3, offset = new Vector2(range * .55f, .5f), size = new Vector2(range, 1), laneTolerance = .3f, knockback = .3f, canHitGrounded = true, canHitAirborne = true });
            asset.frames.Add(frame);
        }
        AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static WaveDefinition Wave(Dictionary<string, GameObject> enemies, params (string enemy, int count)[] spawns)
    {
        var wave = new WaveDefinition(); int i = 0;
        foreach (var spawn in spawns) { wave.enemySpawns.Add(new EnemySpawnDefinition { prefab = enemies[spawn.enemy], count = spawn.count, interval = .25f, spawnPoints = new List<Vector2> { new Vector2(1.6f + i * .2f, -.1f), new Vector2(2.5f, .4f) } }); i++; }
        return wave;
    }
    private static void AddEncounter(StageSegmentDefinition stage, params WaveDefinition[] waves)
    {
        var encounter = new EncounterDefinition { encounterId = stage.stageId + "_Encounter" };
        for (int i = 0; i < waves.Length; i++) { waves[i].waveId = "Wave" + (i + 1); if (i > 0) waves[i].trigger = WaveTrigger.PreviousWaveClear; encounter.waves.Add(waves[i]); }
        stage.encounters.Add(encounter);
    }
    private static DestructiblePlacement Prop(string label, Vector2 point, PropKind kind, float hp) => new DestructiblePlacement { label = label, position = point, health = hp, kind = kind, intactSprite = Pose("Totems", "Idle"), damagedSprite = Pose("Totems", "Damaged"), brokenSprite = Pose("Totems", "Destroyed", 6) };
    private static void BuildScene(LevelDefinition level)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Combat Clock (60 FPS)").AddComponent<CombatClock>();
        var view = new GameObject("Main Camera"); view.tag = "MainCamera"; var camera = view.AddComponent<Camera>(); view.AddComponent<AudioListener>();
        camera.orthographic = true; camera.orthographicSize = 2; camera.backgroundColor = new Color(.045f, .035f, .06f); camera.allowMSAA = false;
        view.transform.position = new Vector3(0, 1.36f, -10); var framing = view.AddComponent<StageFraming>(); framing.showGizmos = true;
        var player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab")); player.name = "Player";
        var flowObject = new GameObject("Haunted House Stage Flow"); var flow = flowObject.AddComponent<StageFlowController>(); flow.level = level; flow.player = player.GetComponent<CharacterMotor>(); flow.framing = framing;
        framing.player = flow.player; framing.floor = null;
        var bg = new GameObject("Stage Background"); var floor = new GameObject("Stage Floor");
        flow.background = bg.AddComponent<SpriteRenderer>(); flow.floor = floor.AddComponent<SpriteRenderer>(); flow.ambience = flowObject.AddComponent<AudioSource>(); flow.ambience.playOnAwake = false;
        var stage = level.stages[0]; player.transform.position = stage.playerEntryPoint; flow.player.arenaMin = stage.movementMin; flow.player.arenaMax = stage.movementMax;
        flow.ApplyStageArt(stage);
        WorldRewardSetup.Attach(flow);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }
}
