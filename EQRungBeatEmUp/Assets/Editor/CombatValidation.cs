using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Batch integration checks use real InputActions and paired virtual devices in Play Mode.
[InitializeOnLoad]
public static class CombatValidation
{
    private const string Pending = "BeatEmUp.ValidationPending";
    private static readonly List<string> results = new List<string>();
    private static GameObject playerObject, enemyObject;
    private static ComboController player;
    private static EnemyHitReaction enemy;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static Gamepad gamepad;
    static CombatValidation() { EditorApplication.update += Poll; }
    public static void BuildAndValidate()
    {
        CombatDemoBuilder.Build();
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    private static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false);
        try
        {
            foreach (var component in UnityEngine.Object.FindObjectsByType<ComboController>()) component.gameObject.SetActive(false);
            foreach (var component in UnityEngine.Object.FindObjectsByType<EnemyHitReaction>()) component.gameObject.SetActive(false);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); gamepad = InputSystem.AddDevice<Gamepad>();
            Run();
            File.WriteAllLines("CombatValidationResults.txt", results);
            Debug.Log("COMBAT VALIDATION PASSED: " + results.Count + " assertions");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception); File.WriteAllLines("CombatValidationResults.txt", results);
            Debug.LogException(exception); EditorApplication.Exit(1);
        }
    }
    private static void Reset(bool controller = false)
    {
        if (playerObject) UnityEngine.Object.DestroyImmediate(playerObject);
        if (enemyObject) UnityEngine.Object.DestroyImmediate(enemyObject);
        playerObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CombatDemoBuilder.Output + "/Prefabs/BlueShirtGuy.prefab"));
        enemyObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CombatDemoBuilder.Output + "/Prefabs/BadGuy.prefab"));
        player = playerObject.GetComponent<ComboController>(); enemy = enemyObject.GetComponent<EnemyHitReaction>();
        playerObject.transform.position = new Vector3(-.7f, 0, 0); enemyObject.transform.position = new Vector3(.15f, 0, 0);
        var pi = playerObject.GetComponent<PlayerInput>();
        if (controller) pi.SwitchCurrentControlScheme("Gamepad", gamepad);
        else pi.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        pi.SwitchCurrentActionMap("Player");
        playerObject.GetComponent<PlayerCombatInput>().SendMessage("Start");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
        Physics2D.SyncTransforms();
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label + $" | player {player.State} {player.CurrentAttack?.name} h={player.motor.Height:F2} | enemy {enemy.State} h={enemy.motor.Height:F2} HP={enemy.health.Current}");
        results.Add("PASS: " + label);
    }
    private static void KeyPress(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); InputSystem.Update();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
    }
    private static void PadPress(GamepadButton button)
    {
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); InputSystem.Update();
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
    }
    private static void Step(float seconds)
    {
        int frames = Mathf.CeilToInt(seconds / .005f);
        for (int i = 0; i < frames; i++)
        {
            player.Tick(.005f); enemy.Tick(.005f);
            player.motor.Simulate(.005f); enemy.motor.Simulate(.005f);
            player.animationDriver.animator.Update(.005f); enemy.animationDriver.animator.Update(.005f);
        }
    }
    private static void Route(bool pad)
    {
        Reset(pad);
        void Attack() { if (pad) PadPress(GamepadButton.West); else KeyPress(Key.J); }
        Attack(); Check(player.CurrentAttack == player.groundCombo[0], "Punch1 starts via " + (pad ? "gamepad" : "keyboard"));
        Step(.1f); Check(enemy.health.Current == 490, "Punch1 deals one hit");
        Attack(); Check(player.BufferedInput == CombatInput.Attack, "Early Attack buffered");
        Step(.09f); Check(player.CurrentAttack == player.groundCombo[1], "Buffered Punch2 starts in window");
        Step(.1f); Check(enemy.health.Current == 480, "Punch2 damage");
        if (pad) PadPress(GamepadButton.North); else KeyPress(Key.K);
        Check(player.BufferedInput == CombatInput.Launcher, "Early Launcher buffered");
        Step(.09f); Check(player.State == CombatState.Launcher, "Punch2 transitions to Launcher");
        Step(.1f); Check(enemy.State == EnemyReaction.Launched && enemy.motor.Height > 0, "Launcher sends enemy upward");
        if (!pad) Capture("Launcher");
        Check(player.motor.IsGrounded, "Launcher does not automatically jump");
        if (pad) PadPress(GamepadButton.South); else KeyPress(Key.Space);
        Check(player.JumpBuffered, "Manual jump buffered during launcher active frames");
        Step(.13f); Check(!player.motor.IsGrounded && player.State == CombatState.Jumping, "Manual launcher jump cancel");
        Check(!player.CurrentAttack, "No automatic air attack");
        Attack(); Check(player.CurrentAttack == player.airCombo[0], "AirPunch1 starts");
        Step(.1f); Check(enemy.health.Current == 460, "AirPunch1 hits launched enemy");
        Attack(); Step(.09f); Check(player.CurrentAttack == player.airCombo[1], "AirPunch2 buffered transition");
        Step(.1f); Check(enemy.health.Current == 450, "AirPunch2 hits");
        if (!pad) Capture("AirCombo");
        Attack(); Step(.09f); Check(player.CurrentAttack == player.airCombo[2], "AirPunch3 buffered transition");
        Step(.1f); Check(enemy.health.Current == 440, "AirPunch3 hits");
        Check(enemy.motor.VerticalVelocity < 0 && !enemy.JuggleOpen, "Air finisher closes juggle and falls");
        for (int i = 0; i < 8; i++) { Attack(); if (pad) PadPress(GamepadButton.North); else KeyPress(Key.K); Step(.05f); }
        Check(player.State != CombatState.GroundAttack && player.State != CombatState.Launcher, "Air mashing cannot start ground attacks or relaunch");
        Step(3); Check(player.motor.IsGrounded && enemy.motor.IsGrounded && enemy.CanAct, "Both land and enemy recovers");
        Check(enemy.JuggleHits == 0 && enemy.motor.GravityOverride == 0, "Landing resets juggle count and gravity");
    }
    private static void Run()
    {
        Reset();
        Check(UnityEngine.Object.FindObjectsByType<SpriteRenderer>().All(r => r.sprite), "Scene renderers have assigned sprites");
        Capture("Ground");
        var map = playerObject.GetComponent<PlayerInput>().actions.FindActionMap("Player", true);
        Check(new[] { "Move", "Attack", "Launcher", "Jump" }.All(a => map.FindAction(a) != null), "Existing Player map extended with Launcher");
        float x = player.motor.transform.position.x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); InputSystem.Update(); Step(.1f);
        Check(player.motor.transform.position.x > x + .2f, "Keyboard movement");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        KeyPress(Key.Space); Check(!player.motor.IsGrounded, "Normal keyboard jump"); Step(2);
        Check(player.motor.IsGrounded, "Normal jump lands");
        Reset(true); x = player.motor.transform.position.x;
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.right }); InputSystem.Update(); Step(.1f);
        Check(player.motor.transform.position.x > x + .2f, "Gamepad movement");
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
        PadPress(GamepadButton.South); Check(!player.motor.IsGrounded, "Normal gamepad jump"); Step(2);
        Check(player.motor.IsGrounded, "Normal gamepad jump lands");

        Reset(); KeyPress(Key.J); Step(.1f); Step(.06f);
        Check(enemy.health.Current == 490, "Repeated active-frame sampling damages once per swing");
        KeyPress(Key.J); Step(.03f); Step(.1f); KeyPress(Key.J); Step(.09f);
        Check(player.CurrentAttack == player.groundCombo[2], "Ground route reaches Punch3");
        Step(.1f); Check(enemy.health.Current == 470 && enemy.motor.IsGrounded, "Three ground hits stay grounded");
        Step(.4f); Check(!player.CurrentAttack && player.ComboIndex == 0, "Terminal ground combo resets");

        Route(false); Route(true);
        Reset(); KeyPress(Key.J); Step(.1f); player.Interrupt(.4f); KeyPress(Key.J); KeyPress(Key.K); KeyPress(Key.Space);
        Check(player.State == CombatState.Hitstun && !player.CurrentAttack && player.BufferedInput == CombatInput.None, "Hitstun clears combo and rejects input");
        Step(.5f); KeyPress(Key.J); Check(player.CurrentAttack == player.groundCombo[0], "Interrupted combo restarts at Punch1");
        Reset(); KeyPress(Key.J); Step(.9f); KeyPress(Key.J);
        Check(player.CurrentAttack == player.groundCombo[0], "Timeout resets combo");
        Reset(); KeyPress(Key.K); Step(.4f); KeyPress(Key.J); Step(.1f); KeyPress(Key.J); Step(.09f); Step(.3f);
        Check(player.State != CombatState.Launcher, "Expired Launcher buffer never launches later");
        Reset(); enemyObject.transform.position = new Vector3(.15f, 1, 0); KeyPress(Key.J); Step(.2f);
        Check(enemy.health.Current == 500, "Separate lane rejects visually overlapping targets");

        Reset(); enemy.Receive(player.launcher, 1); Step(.1f);
        for (int i = 0; i < 10; i++) enemy.Receive(player.airCombo[0], 1);
        Check(enemy.JuggleHits == enemy.maximumJuggleHits && !enemy.JuggleOpen && enemy.motor.VerticalVelocity < 0, "Maximum juggle hit count prevents infinite lift");
        Step(3); Check(enemy.CanAct, "Capped juggle lands and recovers");
        Reset(); enemy.maximumJuggleTime = .15f; enemy.Receive(player.launcher, 1); Step(.2f);
        float velocity = enemy.motor.VerticalVelocity; enemy.Receive(player.airCombo[0], 1);
        Check(!enemy.JuggleOpen && enemy.motor.GravityOverride == 0 && enemy.motor.VerticalVelocity == velocity, "Maximum juggle time restores gravity and rejects lift");
        Step(3); Check(enemy.CanAct, "Timed-out juggle lands");
        Reset(); KeyPress(Key.Space); KeyPress(Key.J); Step(.1f); KeyPress(Key.J); Step(.09f); Step(.1f); KeyPress(Key.J); Step(.09f); Step(.5f);
        KeyPress(Key.J); Check(!player.CurrentAttack, "Air route cannot restart before landing, even on misses");
        Step(3); KeyPress(Key.J); Check(player.CurrentAttack == player.groundCombo[0], "Landing permits next ground route");
        Reset(); for (int i = 0; i < 50; i++) { KeyPress(Key.J); KeyPress(Key.K); Step(.015f); }
        Step(1); Check(!player.CurrentAttack && player.motor.IsGrounded, "Rapid mixed input settles without stuck attacks");
        Reset(); enemy.health.Damage(500); enemy.Receive(player.launcher, 1); Step(.2f);
        Check(enemy.State == EnemyReaction.Defeated && !enemy.CanAct, "Defeated enemy cannot resume AI");
        var ai = enemyObject.GetComponent<EnemyCombat>();
        Reset(); ai = enemyObject.GetComponent<EnemyCombat>(); ai.passiveTrainingDummy = false; ai.target = playerObject.transform;
        enemyObject.transform.position = new Vector3(3, 0, 0); ai.SendMessage("Update");
        Check(enemy.motor.MoveInput.x < 0, "Active enemy AI chases player");
        enemy.Receive(player.launcher, 1); ai.SendMessage("Update");
        Check(enemy.motor.MoveInput == Vector2.zero, "Enemy AI pauses while launched");
        Reset(); ai = enemyObject.GetComponent<EnemyCombat>(); ai.passiveTrainingDummy = false; ai.target = playerObject.transform;
        ai.SendMessage("Update");
        var enemyHitbox = enemyObject.GetComponent<AttackHitbox>(); enemyHitbox.Sample(); enemyHitbox.Sample();
        Check(player.health.Current == player.health.maximumHealth - ai.attack.damage && player.State == CombatState.Hitstun,
            "Enemy attack damages player once and interrupts combat");
        Step(.5f); Check(player.State == CombatState.Idle, "Player recovers after enemy hitstun");
        foreach (string name in new[] { "GroundHit", "Launched", "AirHit", "Falling", "Landing" })
            Check(enemy.animationDriver.animator.HasState(0, Animator.StringToHash("Base Layer." + name)), "Enemy Animator hook " + name);
        foreach (string name in new[] { "Punch1", "Punch2", "Punch3", "Launcher", "AirPunch1", "AirPunch2", "AirPunch3" })
            Check(player.animationDriver.animator.HasState(0, Animator.StringToHash("Base Layer." + name)), "Animator hook " + name);
    }
    private static void Capture(string name)
    {
        var camera = Camera.main;
        var target = new RenderTexture(960, 540, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
        Directory.CreateDirectory("CombatValidationCaptures");
        File.WriteAllBytes("CombatValidationCaptures/" + name + ".png", texture.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(texture); target.Release(); UnityEngine.Object.DestroyImmediate(target);
    }
}
