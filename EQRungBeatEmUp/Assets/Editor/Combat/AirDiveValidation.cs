using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class AirDiveValidation
{
    const string Pending = "BeatEmUp.AirDiveValidation";
    static readonly List<string> results = new List<string>();
    static ComboController player;
    static EnemyHitReaction enemy;
    static CombatClock clock;
    static Keyboard keyboard;
    static Gamepad gamepad;
    static Mouse mouse;
    static GameObject po, eo;
    static AirDiveValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate airborne headbutt dive (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(CombatDemoBuilder.ScenePath); SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    [MenuItem("Beat Em Up/Combat/Validate air dive in current scene (Play Mode)")]
    public static void RunExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static void Step(int frames) { for (int i = 0; i < frames; i++) clock.StepFrame(); }
    static void Fixture()
    {
        if (po) UnityEngine.Object.DestroyImmediate(po); if (eo) UnityEngine.Object.DestroyImmediate(eo);
        po = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab"));
        eo = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab"));
        player = po.GetComponent<ComboController>(); enemy = eo.GetComponent<EnemyHitReaction>();
        po.GetComponent<PlayerCombatInput>().enabled = false; eo.GetComponent<EnemyCombat>().enabled = false;
        player.motor.arenaMin = enemy.motor.arenaMin = new Vector2(-10, -2); player.motor.arenaMax = enemy.motor.arenaMax = new Vector2(10, 2);
        player.motor.ResetForStage(Vector2.zero); enemy.motor.ResetForStage(new Vector2(8, 0));
        enemy.health.maximumHealth = 500; enemy.health.Restore(); player.health.Restore(); Physics2D.SyncTransforms();
    }
    static void Jump(int frames = 12) { player.RequestJump(); Step(frames); Check(!player.motor.IsGrounded, "Normal jump is airborne"); }
    static void FinishDive()
    {
        int frames = 0; while (!player.motor.IsGrounded && frames++ < 180) Step(1);
        Check(player.motor.IsGrounded && player.State == CombatState.GroundAttack && player.CurrentAttack == player.airDive && player.attackPlayer.CurrentFrame == player.airDive.landingFrame, "Ground contact enters grounded impact/recovery at the landing frame");
        player.RequestLauncher(); player.RequestAttack(); player.RequestJump();
        Check(player.CurrentAttack == player.airDive && player.motor.MovementLocked && player.BufferedInput == CombatInput.None && !player.JumpBuffered, "Landing recovery rejects attack, launcher and jump cancels");
        Step(player.airDive.TotalFrames - player.airDive.landingFrame);
        Check(!player.CurrentAttack && player.State == CombatState.Idle && !player.motor.MovementLocked && player.motor.FrameGravityScale == 1 && player.motor.AttackHorizontalVelocity == 0, "Recovery ends in neutral with normal physics restored");
    }
    static void DiveFeedbackChecks()
    {
        foreach (var path in new[] { AirDiveSetup.AssetPath, "Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2_AirDive.asset" })
        foreach (int facing in new[] { -1, 1 })
        {
            Fixture(); player.airDive = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            var cue = player.airDive.feedback;
            Check(cue.swingSound && cue.impactSound && cue.landingSound && cue.diveStartPrefab && cue.impactPrefab && cue.landingPrefab, path + ": all dive VFX/SFX assigned");
            player.motor.Face(facing); player.motor.Launch(24, 0); Step(25); player.RequestLauncher();
            Step(player.airDive.frames.FindIndex(f => f.events.Contains("DiveWhoosh")));
            var fx = player.GetComponent<AttackFeedback>();
            Check(fx && fx.DiveStartCount == 1 && fx.LandingCount == 0 && fx.LastSound.clip == cue.swingSound && fx.LastImpact,
                "Dive start emits one visual/whoosh before landing, facing " + facing);
            Check(fx.LastImpact.transform.position.y > player.motor.transform.position.y + 1, "Dive burst follows airborne visual height");
            if (path == AirDiveSetup.AssetPath && facing == 1) Preview(fx.LastImpact, "DiveStart");
            Step(20); Check(fx.DiveStartCount == 1 && fx.LandingCount == 0, "Held airborne frame does not repeat effects or land early");
            int guard = 0; while (!player.motor.IsGrounded && guard++ < 180) Step(1);
            Check(fx.LandingCount == 1 && fx.LastSound.clip == cue.landingSound && fx.LastImpact, "Whiff still emits one landing visual/thud");
            Check(Mathf.Abs(fx.LastImpact.transform.position.y - player.motor.transform.position.y) < .01f &&
                fx.LastImpact.transform.GetChild(0).localRotation == Quaternion.identity, "Landing effect stays upright at floor");
            if (path == AirDiveSetup.AssetPath && facing == 1) Preview(fx.LastImpact, "DiveLanding");
            Step(30); Check(fx.LandingCount == 1 && fx.DiveStartCount == 1, "Recovery does not repeat dive cues");
            player.RequestJump(); Step(12); player.RequestLauncher(); Step(6); player.Interrupt(10); Step(40);
            Check(fx.DiveStartCount == 2 && fx.LandingCount == 1, "Interrupted dive has no false landing cue");
            AttackFeedback.PlayRemote(player.airDive, Vector2.zero, facing, false, "DiveWhoosh");
            AttackFeedback.PlayRemote(player.airDive, Vector2.zero, facing, false, "DiveLanding");
            Check(UnityEngine.Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None).Length >= 2, "Client dive cues reconstruct temporary VFX");
        }
    }
    [MenuItem("Beat Em Up/Validate grounded launcher and airborne follow-up (Play Mode)")]
    public static void RunLauncher() => Run();
    static void LauncherFeedbackChecks()
    {
        int firstResult = results.Count;
        foreach (var path in new[] { "Assets/EQ_Rung_BeatEmUp/Attacks/Launcher.asset", "Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2_Launcher.asset" })
        foreach (int facing in new[] { -1, 1 })
        {
            Fixture(); player.launcher = AssetDatabase.LoadAssetAtPath<AttackData>(path); player.motor.Face(facing);
            var cue = player.launcher.feedback;
            Check(cue.swingSound && cue.impactSound && cue.swingPrefab && cue.impactPrefab, path + ": launcher VFX/SFX assigned");
            Check(player.launcher.frames[6].events.Contains("Swing") &&
                player.launcher.frames.Count(f => f.events.Contains("Swing")) == 1, "One launcher sweep event at the first active frame");
            player.RequestLauncher(); Step(5);
            var fx = player.GetComponent<AttackFeedback>();
            Check(fx.SwingCount == 0 && fx.ImpactCount == 0, "Anticipation has no premature launcher cue");
            Step(1);
            Check(fx.SwingCount == 1 && fx.LastSwing && fx.LastSound.clip == cue.swingSound,
                "Missed launcher emits one sweep and whoosh");
            var expected = (Vector2)player.motor.transform.position + new Vector2(cue.swingOffset.x * facing, cue.swingOffset.y);
            Check(Vector2.Distance(fx.LastSwing.transform.position, expected) < .01f &&
                fx.LastSwing.transform.localScale.x == facing, "Sweep offset/mirroring follows committed facing " + facing);
            if (path.EndsWith("/Launcher.asset") && facing == 1) Preview(fx.LastSwing, "LauncherSwing");
            Step(40); Check(fx.SwingCount == 1 && fx.ImpactCount == 0 && player.motor.IsGrounded, "Recovery adds no duplicate or false-hit cues");
            Fixture(); player.launcher = AssetDatabase.LoadAssetAtPath<AttackData>(path); player.motor.Face(facing);
            enemy.motor.ResetForStage(new Vector2(.8f * facing, 0)); enemy.health.SafeStageProtection = false;
            int impactSounds = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.clip == cue.impactSound);
            player.RequestLauncher(); Step(8); fx = player.GetComponent<AttackFeedback>();
            // Hit sampling precedes Swing events on the same frame; both sounds must exist.
            Check(fx.SwingCount == 1 && fx.ImpactCount == 1 && fx.LastImpact && fx.LastImpact != fx.LastSwing &&
                UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.clip == cue.impactSound) == impactSounds + 1,
                "Accepted launcher contact emits one separate impact visual/sound alongside the sweep");
            Fixture(); player.launcher = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            player.RequestLauncher(); Step(3); fx = player.GetComponent<AttackFeedback>(); player.Interrupt(10); Step(15);
            Check(fx.SwingCount == 0 && fx.ImpactCount == 0, "Interrupted startup has no delayed launcher cues");
            var origin = new Vector2(11 * facing, -1);
            AttackFeedback.PlayRemote(player.launcher, origin, facing, false, "Swing");
            var remote = UnityEngine.Object.FindObjectsByType<NetworkFeedbackVisual>(FindObjectsSortMode.None)
                .FirstOrDefault(v => Vector2.Distance(v.transform.position, origin + new Vector2(cue.swingOffset.x * facing, cue.swingOffset.y)) < .01f);
            Check(remote && remote.transform.localScale.x == facing, "Client reconstructs launcher sweep position and facing");
        }
        File.WriteAllLines("Documentation/LauncherFeedbackValidationResults.txt", results.Skip(firstResult));
    }
    static void GroundedLauncherChecks()
    {
        int firstResult = results.Count;
        var idlePath = "Assets/EQ_Rung_BeatEmUp/Sprites/BlueShirtGuy_Idle2_01.png";
        var idle = new Texture2D(2, 2); idle.LoadImage(File.ReadAllBytes(idlePath));
        try
        {
            for (int i = 1; i <= 8; i++)
            {
                var path = "Assets/EQ_Rung_BeatEmUp/Sprites/BlueShirtGuy_Launch1_" + i.ToString("00") + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Check(sprite && sprite.rect.size == new Vector2(128, 128) && sprite.pixelsPerUnit == 100 &&
                    sprite.pivot == new Vector2(64, 8), "Launcher pose " + i + ": original canvas, scale and ground pivot");
                var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(path));
                try
                {
                    var pixels = texture.GetPixels32(); int minY = 128;
                    int leftY = 128, rightY = 128;
                    for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                        if (pixels[y * 128 + x].a > 0)
                        {
                            minY = Mathf.Min(minY, y);
                            if (x < 64) leftY = Mathf.Min(leftY, y); else rightY = Mathf.Min(rightY, y);
                        }
                    Check(minY == 8 && leftY <= 10 && rightY <= 10, "Launcher pose " + i + ": both shoes contact consistent floor; no airborne pixels");
                    if (i == 1 || i == 8) Check(pixels.SequenceEqual(idle.GetPixels32()), "Launcher endpoint " + i + ": pixel-exact Idle return");
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(idle); }
        foreach (int facing in new[] { -1, 1 })
        {
            Fixture(); player.motor.Face(facing); enemy.motor.ResetForStage(new Vector2(.8f * facing, 0));
            player.health.SafeStageProtection = enemy.health.SafeStageProtection = false;
            Check(player.launcher.TotalFrames == 26 && player.launcher.FirstActiveFrame == 6 && player.launcher.LastActiveFrame == 12,
                "Launcher retains 26 frames and original 6-12 hit window");
            var impact = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EQ_Rung_BeatEmUp/Sprites/BlueShirtGuy_Launch1_05.png");
            Check(player.launcher.frames.Skip(8).Take(5).All(f => f.sprite == impact), "Maximum extension matches active impact frames 8-12");
            player.RequestLauncher(); bool grounded = true, launchedEnemy = false;
            for (int i = 0; i < 70; i++)
            {
                Step(1); grounded &= player.motor.IsGrounded && player.motor.Height == 0;
                launchedEnemy |= enemy.motor.Height > 0 || enemy.motor.VerticalVelocity > 0;
            }
            Check(grounded && launchedEnemy && !player.CurrentAttack, "Grounded launcher launches enemy and recovers without lifting player, facing " + facing);
        }
        File.WriteAllLines("Documentation/LauncherAnimationValidationResults.txt", results.Skip(firstResult));
    }
    // Isolated offscreen render for reviewing the existing library effects at gameplay scale.
    static void Preview(GameObject effect, string name)
    {
        var transforms = effect.GetComponentsInChildren<Transform>();
        var layers = transforms.Select(t => t.gameObject.layer).ToArray();
        var cameraObject = new GameObject("Dive feedback preview camera");
        var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
        var render = new RenderTexture(512, 512, 24);
        var previous = RenderTexture.active;
        try
        {
            foreach (var t in transforms) t.gameObject.layer = 31;
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>())
                particles.Simulate(.12f, false, true);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.2f;
            camera.transform.position = effect.transform.position + new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .065f, .09f); camera.targetTexture = render;
            camera.Render(); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply();
            File.WriteAllBytes("Temp/" + name + "FeedbackPreview.png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(texture);
            render.Release(); UnityEngine.Object.DestroyImmediate(render);
        }
    }
    static void Poll()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/AirDiveExistingValidation.request"))
        { File.Delete("Temp/AirDiveExistingValidation.request"); RunExisting(); return; }
        if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/AirDiveValidation.request"))
        { File.Delete("Temp/AirDiveValidation.request"); Run(); return; }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        var background = InputSystem.settings.backgroundBehavior;
        var editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        try
        {
            foreach (var motor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) motor.gameObject.SetActive(false);
            foreach (var flow in UnityEngine.Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) flow.enabled = false;
            foreach (var framing in UnityEngine.Object.FindObjectsByType<StageFraming>(FindObjectsSortMode.None)) framing.enabled = false;
            foreach (var wall in UnityEngine.Object.FindObjectsByType<CombatWall>(FindObjectsSortMode.None)) wall.gameObject.SetActive(false);
            clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; keyboard = InputSystem.AddDevice<Keyboard>(); gamepad = InputSystem.AddDevice<Gamepad>();
            GroundedLauncherChecks(); LauncherFeedbackChecks(); DiveFeedbackChecks();
            Fixture(); Check(player.airDive && player.airDive.TotalFrames == 28 && player.airDive.FirstActiveFrame == 9 && player.airDive.LastActiveFrame == 16, "Authored 28-frame dive with eight active frames");
            Check(!player.attackPlayer.Play(player.airDive), "TEST5: direct grounded Play cannot activate airborne-only dive");
            player.RequestLauncher(); Check(player.CurrentAttack == player.launcher && !player.IsAirDiving, "Grounded Launcher keeps existing launcher behavior");
            Fixture(); Jump(); float x = player.transform.position.x, h = player.motor.Height; player.RequestLauncher();
            Check(player.IsAirDiving && player.AirDiveUsed, "TEST1: airborne Launcher starts headbutt dive"); Step(8);
            Check(player.transform.position.x > x + .2f && player.motor.Height < h && player.motor.VerticalVelocity < 0, "Committed travel moves forward and down through motor velocity");
            for (int i = 0; i < 10; i++) player.RequestLauncher(); Check(player.attackPlayer.CurrentFrame == 8, "TEST7: repeated Launcher presses do not restart dive timeline");
            FinishDive(); Check(!player.AirDiveUsed, "Landing replenishes per-airborne-sequence dive use");
            Fixture(); Jump(); enemy.motor.ResetForStage(new Vector2(1, 0)); float hp = enemy.health.Current; player.RequestLauncher(); Step(11);
            Check(enemy.health.Current == hp - 14 && player.attackPlayer.IsFrozen, "TEST2: grounded enemy takes one14-damage head hit with hitstop");
            var hitFx = player.GetComponent<AttackFeedback>();
            Check(hitFx.ImpactCount == 1 && hitFx.LastSound.clip == player.airDive.feedback.impactSound && hitFx.LastImpact,
                "Accepted enemy hit emits one configured hit visual/sound during hitstop");
            FinishDive();
            Fixture(); Jump(); enemy.motor.ResetForStage(new Vector2(1, 0)); enemy.motor.Launch(4, 0); enemy.motor.Simulate(.1f); hp = enemy.health.Current;
            player.RequestLauncher(); Step(30); Check(enemy.health.Current == hp - 14 && !enemy.JuggleOpen, "TEST3: airborne enemy receives finisher hit and descends toward knockdown");
            if (!player.motor.IsGrounded) FinishDive(); else Step(30);
            Fixture(); player.motor.Face(-1); Jump(); x = player.transform.position.x; player.RequestLauncher(); Step(9);
            Check(player.transform.position.x < x && player.motor.sprite.flipX, "Facing left mirrors diagonal travel and pose"); FinishDive();
            Fixture(); player.motor.Launch(24, 0); Step(25); player.RequestLauncher(); Step(20);
            Check(!player.motor.IsGrounded && player.attackPlayer.CurrentFrame == player.airDive.airborneHoldFrame && player.IsAirDiving, "High dive holds active travel pose instead of showing midair impact/recovery"); FinishDive();
            Fixture(); Jump(); player.RequestLauncher(); Step(6); player.Interrupt(10);
            Check(!player.CurrentAttack && player.AirDiveUsed && player.motor.FrameGravityScale == 1 && player.motor.AttackHorizontalVelocity == 0, "Interruption clears dive physics but does not refresh airborne use");
            Step(12); player.RequestLauncher(); Check(!player.IsAirDiving, "Interrupted dive cannot repeat before floor contact");
            Fixture(); Jump(); player.RequestAttack(); int cancel = player.airCombo[0].frames.FindIndex(f => f.canCancelIntoAttack); Step(cancel); player.RequestLauncher();
            Check(player.IsAirDiving, "Air punch can choose dive at an authored attack-cancel window"); FinishDive();
            Fixture(); player.RequestLauncher(); Step(player.launcher.frames.FindIndex(f => f.canCancelIntoJump)); player.RequestJump(); Step(12); player.RequestLauncher();
            Check(player.IsAirDiving, "Manual jump out of grounded Launcher can choose the airborne headbutt"); FinishDive();
            var dive = player.airDive;
            foreach (var frame in dive.frames.Skip(dive.FirstActiveFrame).Take(dive.ActiveFrames)) Check(frame.hitboxes.Count == 1 && frame.hitboxes[0].offset.x > .3f && frame.hitboxes[0].offset.y < .4f && frame.hitboxes[0].size.x <= .5f, "TEST8: compact active box stays on low front/head, not trailing legs");
            foreach (var sprite in dive.frames.Select(f => f.sprite).Distinct()) Check(sprite && sprite.rect.width == 160 && sprite.rect.height == 128 && sprite.pixelsPerUnit == 100 && sprite.pivot == new Vector2(80, 8), "TEST9: pose canvas, scale and ground pivot match existing sprites");
            mouse = InputSystem.AddDevice<Mouse>();
            Fixture(); po.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse); po.GetComponent<PlayerCombatInput>().enabled = true; po.GetComponent<PlayerCombatInput>().SendMessage("Start"); Step(1);
            Jump(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K)); InputSystem.Update(); Check(player.IsAirDiving, "Actual keyboard Launcher action invokes dive in air");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            Fixture(); po.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Gamepad", gamepad); po.GetComponent<PlayerCombatInput>().enabled = true; po.GetComponent<PlayerCombatInput>().SendMessage("Start"); Step(1);
            Jump(); InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North)); InputSystem.Update(); Check(player.IsAirDiving, "Actual gamepad north Launcher action invokes dive in air");
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/AirDiveValidationResults.txt", results); Debug.Log("AIR DIVE VALIDATION PASSED: " + results.Count);
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch (Exception ex) { results.Add("FAIL: " + ex); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/AirDiveValidationResults.txt", results); Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode(); }
        finally
        {
            if (po) UnityEngine.Object.DestroyImmediate(po); if (eo) UnityEngine.Object.DestroyImmediate(eo);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior;
        }
    }
}

