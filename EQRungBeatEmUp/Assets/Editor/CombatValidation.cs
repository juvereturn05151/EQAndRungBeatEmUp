using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class CombatValidation
{
    private const string Pending = "BeatEmUp.FrameValidation";
    private static readonly List<string> results = new List<string>();
    private static GameObject playerObject, enemyObject;
    private static ComboController player;
    private static EnemyHitReaction enemy;
    private static CombatClock clock;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static Gamepad gamepad;
    static CombatValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate frame combat (Play Mode)")]
    public static void BuildAndValidate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CombatDemoBuilder.Build();
        EditorSceneManager.OpenScene(CombatDemoBuilder.ScenePath);
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    private static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        bool previousRunInBackground = InputSystem.runInBackground;
        try
        {
            foreach (var component in UnityEngine.Object.FindObjectsByType<ComboController>()) component.gameObject.SetActive(false);
            foreach (var component in UnityEngine.Object.FindObjectsByType<EnemyHitReaction>()) component.gameObject.SetActive(false);
            clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); gamepad = InputSystem.AddDevice<Gamepad>();
            Run(); File.WriteAllLines("CombatValidationResults.txt", results);
            Debug.Log("FRAME COMBAT VALIDATION PASSED: " + results.Count + " assertions");
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch (Exception exception)
        {
            results.Add("FAIL: " + exception); File.WriteAllLines("CombatValidationResults.txt", results); Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
        }
        finally
        {
            if (playerObject) UnityEngine.Object.DestroyImmediate(playerObject);
            if (enemyObject) UnityEngine.Object.DestroyImmediate(enemyObject);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            InputSystem.runInBackground = previousRunInBackground;
        }
    }
    private static void Reset(bool pad = false)
    {
        if (playerObject) UnityEngine.Object.DestroyImmediate(playerObject);
        if (enemyObject) UnityEngine.Object.DestroyImmediate(enemyObject);
        playerObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CombatDemoBuilder.Output + "/Prefabs/BlueShirtGuy.prefab"));
        enemyObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CombatDemoBuilder.Output + "/Prefabs/BadGuy.prefab"));
        player = playerObject.GetComponent<ComboController>(); enemy = enemyObject.GetComponent<EnemyHitReaction>();
        playerObject.transform.position = new Vector3(-.7f, 0, 0); enemyObject.transform.position = new Vector3(.15f, 0, 0);
        var pi = playerObject.GetComponent<PlayerInput>();
        if (pad) pi.SwitchCurrentControlScheme("Gamepad", gamepad); else pi.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        pi.SwitchCurrentActionMap("Player"); playerObject.GetComponent<PlayerCombatInput>().SendMessage("Start");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
        clock.combatFPS = 60; Physics2D.SyncTransforms();
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label + $" | attack={player.CurrentAttack?.name} frame={player.attackPlayer.CurrentFrame} hp={enemy.health.Current} enemy={enemy.State} h={enemy.motor.Height:F2} playerH={player.motor.Height:F2}");
        results.Add("PASS: " + label);
    }
    private static void Key(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); InputSystem.Update();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
    }
    private static void Pad(GamepadButton button)
    {
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); InputSystem.Update();
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
    }
    private static void Step(int frames) { for (int i = 0; i < frames; i++) clock.StepFrame(); }
    private static void Until(Func<bool> condition, int limit = 240) { for (int i = 0; i < limit && !condition(); i++) Step(1); if (!condition()) throw new Exception("Timed out waiting for route"); }
    private static AttackData TestAttack(int count, Action<AttackFrameData, int> configure = null)
    {
        var attack = ScriptableObject.CreateInstance<AttackData>();
        for (int i = 0; i < count; i++)
        {
            var frame = new AttackFrameData { sprite = player.groundCombo[0].frames[i % player.groundCombo[0].TotalFrames].sprite };
            configure?.Invoke(frame, i); attack.frames.Add(frame);
        }
        return attack;
    }
    private static void Route(bool pad)
    {
        Reset(pad);
        void Attack() { if (pad) Pad(GamepadButton.West); else Key(UnityEngine.InputSystem.Key.J); }
        Attack(); Check(player.CurrentAttack == player.groundCombo[0] && player.attackPlayer.CurrentFrame == 0, "Frame 0 starts on manual input " + pad);
        Step(3); Attack(); Check(player.BufferedInput == CombatInput.Attack && player.ComboIndex == 1, "Attack buffers before cancel");
        Until(() => player.CurrentAttack == player.groundCombo[1]); Check(player.attackPlayer.CurrentFrame == 0, "Buffered input starts Punch2 at frame 0");
        Step(3); Attack(); Until(() => player.CurrentAttack == player.groundCombo[2]); Step(45);
        Check(enemy.health.Current == 470 && enemy.motor.IsGrounded, "Punch1 -> Punch2 -> Punch3 deals exactly three grounded hits " + pad);
        Check(!player.CurrentAttack && player.ComboIndex == 0, "Terminal ground route clears");
        Reset(pad); Attack(); Step(3); Attack(); Until(() => player.CurrentAttack == player.groundCombo[1]); Step(3);
        if (pad) Pad(GamepadButton.North); else Key(UnityEngine.InputSystem.Key.K);
        Check(player.BufferedInput == CombatInput.Launcher, "Launcher buffers before cancel");
        Until(() => player.CurrentAttack == player.launcher); Until(() => !enemy.motor.IsGrounded);
        Check(player.motor.IsGrounded && enemy.State == EnemyReaction.Launched, "Launcher launches enemy without auto jump");
        Step(8); Check(player.CurrentAttack == player.launcher && player.motor.IsGrounded, "Launcher does not auto attack or jump");
        if (pad) Pad(GamepadButton.South); else Key(UnityEngine.InputSystem.Key.Space);
        Until(() => !player.motor.IsGrounded); Attack();
        Check(player.CurrentAttack == player.airCombo[0], "Manual jump then AirPunch1");
        Step(3); Attack(); Until(() => player.CurrentAttack == player.airCombo[1]);
        Step(3); Attack(); Until(() => player.CurrentAttack == player.airCombo[2]); Step(45);
        Check(enemy.health.Current == 440 && !enemy.JuggleOpen, "Three manual air punches hit launched enemy and end juggle " + pad);
        Step(180); Check(enemy.CanAct && player.motor.IsGrounded, "Enemy lands and recovers; player lands");
    }
    private static void Run()
    {
        Route(false); Route(true);
        Reset(); Key(UnityEngine.InputSystem.Key.J); Step(6);
        Check(enemy.health.Current == 490 && player.attackPlayer.CurrentFrame == 6, "First active frame deals one hit");
        int frozenFrame = player.attackPlayer.CurrentFrame, stun = enemy.RecoveryFrames;
        var sprite = player.motor.sprite.sprite; var position = enemy.motor.transform.position;
        Step(5); Check(player.attackPlayer.CurrentFrame == frozenFrame && player.motor.sprite.sprite == sprite && enemy.RecoveryFrames == stun && enemy.motor.transform.position == position, "Five hitstop frames freeze timeline, sprite, victim movement and hitstun");
        Step(8); Check(enemy.health.Current == 490, "Shared hit ID prevents repeated damage across active frames");
        Reset(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.J)); InputSystem.Update(); Step(120);
        Check(enemy.health.Current == 490 && !player.CurrentAttack, "Holding Attack produces only one manual attack");
        Reset(); for (int i = 0; i < 100; i++) { Key(UnityEngine.InputSystem.Key.J); Step(1); } Step(120);
        Check(!player.CurrentAttack && player.motor.IsGrounded, "Rapid mashing settles without an unbounded input queue");
        Reset(); Key(UnityEngine.InputSystem.Key.J); Step(2); player.Interrupt(14); Key(UnityEngine.InputSystem.Key.J);
        Check(!player.CurrentAttack && player.BufferedInput == CombatInput.None && player.State == CombatState.Hitstun, "Interruption clears attack hitboxes and input");
        Step(15); Key(UnityEngine.InputSystem.Key.J); Check(player.CurrentAttack == player.groundCombo[0], "Recovery restarts at Punch1");
        Reset(); Key(UnityEngine.InputSystem.Key.J); Step(100); Key(UnityEngine.InputSystem.Key.J);
        Check(player.CurrentAttack == player.groundCombo[0], "Missing combo continuation timeout resets route");
        Reset(); Key(UnityEngine.InputSystem.Key.K); Step(30); Key(UnityEngine.InputSystem.Key.J); Step(40);
        Check(enemy.motor.IsGrounded, "Expired Launcher request cannot trigger later");
        Reset(); enemyObject.transform.position = new Vector3(.15f, 1, 0); Key(UnityEngine.InputSystem.Key.J); Step(40);
        Check(enemy.health.Current == 500, "Lane depth rejects a different walking lane");
        Reset(); player.motor.Face(-1); enemyObject.transform.position = new Vector3(-1.55f, 0, 0); Key(UnityEngine.InputSystem.Key.J); Step(15);
        Check(enemy.health.Current == 490 && player.motor.sprite.flipX && enemy.motor.transform.position.x < -1.55f, "Facing left mirrors sprite, hitbox and knockback");
        Reset(); var move = TestAttack(5, (f, i) => f.movement = new Vector2(.1f, 0));
        player.motor.Face(-1); float x = playerObject.transform.position.x; player.attackPlayer.Play(move); Step(4);
        Check(Mathf.Abs(playerObject.transform.position.x - x + .5f) < .001f, "Per-frame displacement applies once and mirrors left");
        player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(move);
        Reset(); var timing = TestAttack(60); player.attackPlayer.Play(timing);
        foreach (float dt in new[] { .007f, .011f, .032f, .1f, .05f }) clock.Advance(dt);
        Check(player.attackPlayer.CurrentFrame == 12, "Variable rendered delta advances exactly 12 combat frames in 0.2 seconds");
        player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(timing);
        Reset(); var cancels = TestAttack(8, (f, i) => f.canCancelIntoAttack = i == 4);
        var original = player.groundCombo[0]; player.groundCombo[0] = cancels;
        Key(UnityEngine.InputSystem.Key.J); Step(3); Key(UnityEngine.InputSystem.Key.J); Step(1);
        Check(player.CurrentAttack == player.groundCombo[1], "Input on frame 3 consumes precisely on legal frame 4");
        player.groundCombo[0] = original; UnityEngine.Object.DestroyImmediate(cancels);
        Reset(); var missed = TestAttack(30, (f, i) => f.canCancelIntoAttack = i == 4);
        player.groundCombo[0] = missed; Key(UnityEngine.InputSystem.Key.J); Step(5); Key(UnityEngine.InputSystem.Key.J); Step(22);
        Check(player.CurrentAttack == missed && player.BufferedInput == CombatInput.None, "Input after final cancel window expires without chaining");
        player.groundCombo[0] = original; player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(missed);
        Reset(); var multihit = TestAttack(3, (f, i) => { f.hitboxes.Add(new AttackHitboxData { hitId = 0, hitstopFrames = 0 }); f.hitboxes.Add(new AttackHitboxData { hitId = 1, hitstopFrames = 0 }); });
        player.attackPlayer.Play(multihit); Step(2); Check(enemy.health.Current == 480, "Two explicit hit IDs deal two hits, never one hit per frame");
        player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(multihit);
        Reset(); var invulnerable = TestAttack(5, (f, i) => f.invulnerable = true); player.attackPlayer.Play(invulnerable);
        Check(!player.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData(), -1), "Frame invulnerability rejects damage");
        player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(invulnerable);
        Reset(); var armor = TestAttack(5, (f, i) => f.superArmor = true); player.attackPlayer.Play(armor);
        player.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData(), -1);
        Check(player.CurrentAttack == armor && player.health.Current == 190, "Super armor receives damage without interruption");
        player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(armor);
        Reset(); var ai = enemyObject.GetComponent<EnemyCombat>(); ai.passiveTrainingDummy = false; ai.target = playerObject.transform;
        Step(35); Check(player.health.Current == 192 && player.State == CombatState.Hitstun, "Enemy uses frame AttackPlayer and interrupts player once");
        Check(!player.animationDriver.animator.HasState(0, Animator.StringToHash("Base Layer.Punch1")) && !enemy.animationDriver.animator.HasState(0, Animator.StringToHash("Base Layer.Attack")), "Attack states removed from both runtime Animator Controllers");
        Reset(); var lowFPS = TestAttack(30, (f, i) => { if (i == 6) f.hitboxes.Add(new AttackHitboxData { hitstopFrames = 0 }); });
        player.attackPlayer.Play(lowFPS); clock.Advance(.5f);
        Check(enemy.health.Current == 490 && !player.CurrentAttack, "One slow rendered frame still executes an intermediate one-frame hitbox");
        UnityEngine.Object.DestroyImmediate(lowFPS);
        Reset(); var fpsAttack = TestAttack(90); player.attackPlayer.Play(fpsAttack); clock.combatFPS = 30; clock.Advance(1f);
        Check(player.attackPlayer.CurrentFrame == 30, "Configurable 30 FPS clock advances 30 frames per second");
        player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(fpsAttack);
        Reset(); var signals = TestAttack(4, (f, i) => { f.events.Add("frame"); f.setVerticalVelocity = i == 0; f.verticalVelocity = 3; f.suspendFalling = true; f.gravityScale = 0; });
        int count = 0; Action<string> callback = signal => count++; player.attackPlayer.FrameEvent += callback;
        player.attackPlayer.Play(signals); var directSprite = player.motor.sprite.sprite; Step(3);
        Check(count == 4 && player.motor.Height > 0 && player.motor.VerticalVelocity == 3, "Frame events fire once and authored velocity/gravity drive movement");
        Check(!player.animationDriver.animator.enabled && player.motor.sprite.sprite == signals.frames[3].sprite, "AttackPlayer owns the exact frame sprite while Animator is disabled");
        player.attackPlayer.Stop(); player.attackPlayer.FrameEvent -= callback; UnityEngine.Object.DestroyImmediate(signals);
        Reset(); Key(UnityEngine.InputSystem.Key.J); Step(6); Key(UnityEngine.InputSystem.Key.J); Step(5);
        Check(player.BufferedInput == CombatInput.Attack && player.attackPlayer.CurrentFrame == 6, "Input buffers during hitstop without aging or advancing the frame");
        Until(() => player.CurrentAttack == player.groundCombo[1]); Check(player.ComboIndex == 2, "Hitstop-buffered attack consumes at the next legal cancel");
        Reset(); var repeat = TestAttack(6, (f, i) => f.hitboxes.Add(new AttackHitboxData { hitstopFrames = 0, repeatAfterFrames = 2 }));
        player.attackPlayer.Play(repeat); Step(5); Check(enemy.health.Current == 470, "Explicit repeat interval allows intentional hits at frames 0, 2 and 4");
        player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(repeat);
        Reset(); enemy.Receive(player.launcher.frames[6].hitboxes[0], 1);
        for (int i = 0; i < 10; i++) enemy.Receive(player.airCombo[0].frames[6].hitboxes[0], 1);
        Check(enemy.JuggleHits == enemy.maximumJuggleHits && !enemy.JuggleOpen && enemy.motor.VerticalVelocity < 0, "Juggle cap prevents unlimited lift");
        Step(180); Check(enemy.CanAct, "Capped juggle lands and recovers");
        var editorAsset = UnityEngine.Object.Instantiate(player.groundCombo[0]);
        AssetDatabase.CreateAsset(editorAsset, "Assets/FrameEditorValidation.asset");
        var editor = UnityEditor.Editor.CreateEditor(editorAsset, typeof(AttackDataEditor));
        var selected = typeof(AttackDataEditor).GetField("selectedFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var edit = typeof(AttackDataEditor).GetMethod("EditFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        selected.SetValue(editor, 6); int total = editorAsset.TotalFrames; edit.Invoke(editor, new object[] { "Duplicate" });
        Check(editorAsset.TotalFrames == total + 1 && editorAsset.frames[7].sprite == editorAsset.frames[6].sprite && !ReferenceEquals(editorAsset.frames[7].hitboxes[0], editorAsset.frames[6].hitboxes[0]), "Editor duplicates sprites and deeply copies per-frame hitboxes");
        selected.SetValue(editor, 7); edit.Invoke(editor, new object[] { "Delete" }); Check(editorAsset.TotalFrames == total, "Editor deletes selected frame");
        selected.SetValue(editor, 6); edit.Invoke(editor, new object[] { "Insert" }); Check(editorAsset.frames[6].sprite == null && editorAsset.TotalFrames == total + 1, "Editor inserts an empty frame at selection");
        selected.SetValue(editor, 7); edit.Invoke(editor, new object[] { "Previous" }); Check(editorAsset.frames[7].sprite == null && editorAsset.TotalFrames == total + 2, "Editor duplicates previous frame at selection");
        edit.Invoke(editor, new object[] { "Add" }); Check(editorAsset.TotalFrames == total + 3, "Editor adds frame at end");
        EditorUtility.SetDirty(editorAsset); AssetDatabase.SaveAssets();
        Check(File.ReadAllText("Assets/FrameEditorValidation.asset").Contains("hitstopFrames:"), "Frame and hitbox authoring saves to the Unity asset");
        UnityEngine.Object.DestroyImmediate(editor); AssetDatabase.DeleteAsset("Assets/FrameEditorValidation.asset");
        foreach (var attack in player.groundCombo.Concat(player.airCombo).Append(player.launcher))
            Check(attack.frames.Count > 0 && attack.frames.All(f => f.sprite), "Every frame has existing artwork: " + attack.name);
    }
}
