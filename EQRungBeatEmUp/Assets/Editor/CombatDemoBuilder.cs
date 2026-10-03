using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class CombatDemoBuilder
{
    public const string Output = "Assets/CombatDemo";
    public const string ScenePath = "Assets/Scenes/ComboDemo.unity";
    private const string Blue = "Assets/ArtAssets/Characters/BlueShirtGuy/Animations/";
    private const string Bad = "Assets/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/";

    [MenuItem("Beat Em Up/Create or rebuild combo demo")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static void Build()
    {
        Directory.CreateDirectory(Output + "/Animations");
        Directory.CreateDirectory(Output + "/Attacks");
        Directory.CreateDirectory(Output + "/Prefabs");
        Directory.CreateDirectory(Output + "/Sprites");
        AssetDatabase.Refresh();
        var idle = BlueClip("Idle", "Idle/BlueShirtGuy_Idle2_", .8f, true);
        var walk = BlueClip("Walk", "Walk/BlueShirtGuy_Walk2_", .65f, true);
        var punch = BlueClip("Punch", "Attack1/BlueShirtGuy_Attack1_", .43f, false);
        var launchClip = BlueClip("Launcher", "Launch1/BlueShirtGuy_Launch1_", .43f, false);
        var air = BlueClip("AirPunch", "AirAttack1/BlueShirtGuy_AirAttack1_", .43f, false);
        var playerController = Controller("BlueShirtGuy", new[] { "Idle", "Walk", "Punch1", "Punch2", "Punch3", "Launcher", "Jumping", "AirPunch1", "AirPunch2", "AirPunch3", "GroundHit" },
            new[] { idle, walk, punch, punch, punch, launchClip, idle, air, air, air, idle });
        AnimationClip EnemyClip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Bad + name + "/ThaiBadBoy_" + name + ".anim");
        var enemyController = Controller("BadGuy", new[] { "Idle", "Walk", "Attack", "GroundHit", "Launched", "AirHit", "Falling", "Landing", "Defeated" },
            new[] { EnemyClip("Idle"), EnemyClip("Walk"), EnemyClip("Attack1"), EnemyClip("Hurt"), EnemyClip("Hurt"), EnemyClip("Hurt"), EnemyClip("Knockdown"), EnemyClip("Hurt"), EnemyClip("Knockdown") });
        var ground = new AttackData[3]; var airborne = new AttackData[3];
        for (int i = 0; i < 3; i++)
        {
            ground[i] = Attack("Punch" + (i + 1));
            airborne[i] = Attack("AirPunch" + (i + 1));
            airborne[i].domain = AttackDomain.Air; airborne[i].canHitAirborne = true;
            airborne[i].endsJuggle = i == 2; airborne[i].knockback = 0;
            EditorUtility.SetDirty(airborne[i]);
        }
        var launcher = Attack("Launcher"); launcher.canLaunch = true;
        launcher.knockback = 0; launcher.hitstun = .4f;
        EditorUtility.SetDirty(launcher);
        var enemyAttack = Attack("EnemyPunch"); enemyAttack.damage = 8; enemyAttack.animationState = "Attack";
        enemyAttack.startup = .2f; enemyAttack.activeDuration = .08f; enemyAttack.recovery = .51f;
        EditorUtility.SetDirty(enemyAttack);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 4;
        camera.transform.position = new Vector3(0, 1, -10); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .1f, .14f); cameraObject.AddComponent<AudioListener>();
        var floor = new GameObject("Walking arena (XY lane, height is separate)");
        var floorSprite = floor.AddComponent<SpriteRenderer>(); floorSprite.sprite = FloorSprite();
        floorSprite.color = new Color(.19f, .22f, .26f); floorSprite.sortingOrder = -1000;
        floor.transform.position = new Vector3(0, -.5f, 1); floor.transform.localScale = new Vector3(16, 3, 1);

        var player = Character("BlueShirtGuy", playerController,
            AssetDatabase.LoadAssetAtPath<Sprite>(Output + "/Sprites/BlueShirtGuy_Idle2_01.png"), 0);
        var combo = player.AddComponent<ComboController>(); var playerMotor = player.GetComponent<CharacterMotor>();
        combo.motor = playerMotor; combo.health = player.GetComponent<CharacterHealth>();
        combo.animationDriver = player.GetComponent<CharacterAnimation>(); combo.hitbox = player.GetComponent<AttackHitbox>();
        combo.groundCombo = ground; combo.airCombo = airborne; combo.launcher = launcher;
        var playerInput = player.AddComponent<PlayerInput>();
        playerInput.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        playerInput.defaultActionMap = "Player"; playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        var adapter = player.AddComponent<PlayerCombatInput>();
        var adapterProperties = new SerializedObject(adapter);
        adapterProperties.FindProperty("combat").objectReferenceValue = combo;
        adapterProperties.FindProperty("motor").objectReferenceValue = playerMotor;
        adapterProperties.ApplyModifiedPropertiesWithoutUndo();
        player.GetComponentInChildren<CombatHurtbox>().player = combo;
        player.transform.position = new Vector3(-.7f, 0, 0);
        PrefabUtility.SaveAsPrefabAssetAndConnect(player, Output + "/Prefabs/BlueShirtGuy.prefab", InteractionMode.AutomatedAction);

        var enemy = Character("BadGuy", enemyController, AssetDatabase.LoadAssetAtPath<Sprite>(Bad + "Idle/ThaiBadBoy_Idle_01.png"), 1);
        enemy.GetComponent<CharacterHealth>().maximumHealth = 500;
        var reaction = enemy.AddComponent<EnemyHitReaction>(); reaction.motor = enemy.GetComponent<CharacterMotor>();
        reaction.health = enemy.GetComponent<CharacterHealth>(); reaction.animationDriver = enemy.GetComponent<CharacterAnimation>();
        enemy.GetComponentInChildren<CombatHurtbox>().enemy = reaction;
        var ai = enemy.AddComponent<EnemyCombat>(); ai.motor = reaction.motor; ai.reaction = reaction;
        ai.animationDriver = reaction.animationDriver; ai.attack = enemyAttack; ai.hitbox = enemy.GetComponent<AttackHitbox>();
        enemy.transform.position = new Vector3(.15f, 0, 0);
        reaction.motor.Face(-1);
        PrefabUtility.SaveAsPrefabAssetAndConnect(enemy, Output + "/Prefabs/BadGuy.prefab", InteractionMode.AutomatedAction);
        ai.target = player.transform;
        var debug = new GameObject("Combat Debug (optional)").AddComponent<CombatDebugOverlay>();
        debug.player = combo; debug.input = adapter; debug.enemy = reaction; debug.showDebug = true;
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Combo demo built: " + ScenePath);
    }

    private static GameObject Character(string name, RuntimeAnimatorController controller, Sprite sprite, int team)
    {
        var root = new GameObject(name);
        var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        var animator = visual.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
        var motor = root.AddComponent<CharacterMotor>(); motor.visual = visual.transform; motor.sprite = renderer;
        var driver = root.AddComponent<CharacterAnimation>(); driver.animator = animator;
        var health = root.AddComponent<CharacterHealth>();
        var hitbox = root.AddComponent<AttackHitbox>(); hitbox.motor = motor; hitbox.team = team;
        var collider = visual.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.offset = new Vector2(0, .55f); collider.size = new Vector2(.55f, 1.05f);
        var hurtbox = visual.AddComponent<CombatHurtbox>(); hurtbox.motor = motor; hurtbox.health = health; hurtbox.team = team;
        return root;
    }

    private static AttackData Attack(string name)
    {
        string path = Output + "/Attacks/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        if (existing) return existing;
        var data = ScriptableObject.CreateInstance<AttackData>(); data.animationState = name;
        data.hitboxPosition = new Vector2(.55f, .55f); data.hitboxSize = new Vector2(1, 1.1f);
        data.knockback = .25f;
        AssetDatabase.CreateAsset(data, path); return data;
    }

    private static AnimatorController Controller(string name, string[] states, AnimationClip[] clips)
    {
        string path = Output + "/Animations/" + name + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        for (int i = 0; i < states.Length; i++)
        {
            var state = machine.states.FirstOrDefault(s => s.state.name == states[i]).state;
            if (!state) state = machine.AddState(states[i], new Vector3(250 + (i % 3) * 220, (i / 3) * 80, 0));
            state.motion = clips[i];
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller); return controller;
    }

    private static AnimationClip BlueClip(string name, string prefix, float duration, bool loop)
    {
        var paths = Directory.GetFiles(Blue + Path.GetDirectoryName(prefix), Path.GetFileName(prefix) + "*.png")
            .Where(p => int.TryParse(Path.GetFileNameWithoutExtension(p).Split('_').Last(), out _)).OrderBy(p => p).ToArray();
        if (paths.Length == 0) throw new InvalidOperationException("No animation frames for " + prefix);
        var keys = new ObjectReferenceKeyframe[paths.Length + 1];
        for (int i = 0; i < paths.Length; i++)
        {
            string target = Output + "/Sprites/" + Path.GetFileName(paths[i]);
            if (!File.Exists(target)) File.Copy(paths[i], target);
            AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.spritePivot = new Vector2(.5f, .0625f);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, .0625f);
            importer.SetTextureSettings(settings); importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            keys[i] = new ObjectReferenceKeyframe { time = i * duration / paths.Length, value = AssetDatabase.LoadAssetAtPath<Sprite>(target) };
        }
        keys[paths.Length] = new ObjectReferenceKeyframe { time = duration, value = keys[paths.Length - 1].value };
        string clipPath = Output + "/Animations/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
        return clip;
    }

    private static Sprite FloorSprite()
    {
        string path = Output + "/Sprites/Arena.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, Color.white); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.spritePixelsPerUnit = 1; importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
