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
public static class WorldRewardValidation
{
    const string Pending = "BeatEmUp.WorldRewardValidation";
    static readonly List<string> results = new List<string>();
    static StageFlowController flow;
    static CombatClock clock;
    static RunUpgradeController upgrades;
    static RewardSelectionController world;
    static WorldRewardValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Upgrades/Validate chapel world rewards (Play Mode)")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static void Step(int frames = 1) { for (int i = 0; i < frames; i++) { clock.StepFrame(); flow.Tick(CombatClock.FrameSeconds); } }
    static void ClearRoom()
    {
        for (int i = 0; i < 800 && !world.IsPending; i++)
        {
            foreach (var enemy in flow.LivingEnemies.ToArray()) { enemy.GetComponent<EnemyCombat>().enabled = false; enemy.Damage(10000); }
            Step();
        }
        Check(world.State == WorldRewardState.RewardPending, "Combat clear enters RewardPending");
    }
    static void Move(Vector2 position) { flow.player.GetComponent<ComboController>().ResetCombo(); flow.player.ResetForStage(position); }
    static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear(); LevelDefinition original = null, copy = null;
        try
        {
            flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); upgrades = flow.RunUpgrades; world = flow.WorldRewards;
            flow.enabled = false; clock.enabled = false; flow.player.GetComponent<PlayerCombatInput>().enabled = false;
            Check(world && world.chapelPrefab && world.choicePrefab, "Playable scene assigns chapel and world choice prefabs");
            Check(world.chapelPrefab.GetComponent<SpriteRenderer>().sprite.texture.format != TextureFormat.RGB24, "Generated chapel art has alpha-capable texture");
            original = flow.level; copy = UnityEngine.Object.Instantiate(original); flow.level = copy; flow.Restart(true);
            Check(!world.IsPending && !world.Chapel, "Stage entry does not force a reward chapel");
            int entrance = copy.stages.FindIndex(s => s.stageId == "Stage01_EntranceGate");
            int hub = copy.stages.FindIndex(s => s.stageId == "Stage00_PlayerHub");
            if (hub >= 0) { flow.EnterStage(hub); int next = flow.CurrentStage.nextStageIndex < 0 ? hub + 1 : flow.CurrentStage.nextStageIndex; Move(flow.CurrentStage.playerExitPoint); Step(); Check(flow.StageIndex == next, "Optional hub preserves its authored next-stage route"); }
            flow.EnterStage(entrance);
            ClearRoom(); int stage = flow.StageIndex; var chapel = world.Chapel;
            Check(chapel && !upgrades.IsChoosing && world.ChoiceObjects.Count == 0, "TEST1/2: clear spawns chapel without opening upgrade popup or cards");
            Check(Time.timeScale == 1 && !CombatClock.IsPaused && !flow.player.MovementLocked, "Reward pending preserves game time and player controls");
            Check(Vector2.Distance(chapel.transform.position, flow.player.transform.position) >= 1.4f, "Chapel does not spawn on player");
            Check(chapel.transform.position.x >= flow.CurrentStage.movementMin.x && chapel.transform.position.x <= flow.CurrentStage.movementMax.x, "Chapel stays inside stage walking bounds");
            Check(!flow.ExitUnlocked && !flow.TryAdvance(), "Unresolved reward locks progression");
            Step(500); Check(flow.StageIndex == stage && world.Chapel == chapel && !upgrades.IsChoosing && world.ChoiceObjects.Count == 0, "TEST7: waiting does not auto-activate, duplicate chapel, or skip reward");
            Check(!flow.Interact(), "Out-of-range chapel interaction rejected");
            Move(chapel.transform.position); Step(10); Check(!upgrades.IsChoosing, "Standing at chapel never auto-triggers selection");
            var hp = flow.player.GetComponent<CharacterHealth>(); Check(!hp.Damage(10000), "Cleared reward area protects player from damage");
            flow.player.MoveInput = Vector2.right;
            Check(chapel.TryInteract() && world.State == WorldRewardState.Choosing && upgrades.IsWorldChoosing, "TEST3: Interact on chapel begins world choice selection");
            Check(flow.player.MoveInput == Vector2.right, "Chapel activation preserves a held movement input"); flow.player.MoveInput = Vector2.zero;
            Check(world.ChoiceObjects.Count == 3 && upgrades.Choices.Count == 3, "Three physical choices appear");
            Check(!CombatClock.IsPaused && Time.timeScale == 1, "World choices do not pause movement or time");
            Check(world.ChoiceObjects.All(c => c.GetComponentsInChildren<TextMesh>().Length == 3 && c.Upgrade && c.Upgrade.displayName.Length > 0), "All cards display name, description, rarity and build tags");
            var positions = world.ChoiceObjects.Select(c => (Vector2)c.transform.position).ToArray();
            Check(Vector2.Distance(positions[0], positions[1]) >= 1.5f && Vector2.Distance(positions[1], positions[2]) >= 1.5f && Vector2.Distance(positions[0], positions[2]) >= 1.5f, "Cards have distinct non-overlapping positions");
            Check(!upgrades.Choose(0) && !upgrades.Choose(-1) && !upgrades.Choose(99), "Remote and invalid selections cannot bypass physical interaction");
            var choice = world.ChoiceObjects[1]; var definition = choice.Upgrade; Move(choice.transform.position);
            float oldX = flow.player.transform.position.x; flow.player.MoveInput = Vector2.right; Step(2); flow.player.MoveInput = Vector2.zero;
            Check(flow.player.transform.position.x > oldX, "Player can walk during world selection");
            Capture("WorldChoices");
            Check(choice.TryInteract() && upgrades.Build.Stacks(definition) == 1, "TEST4: nearby Interact applies selected upgrade to existing run build");
            Check(world.State == WorldRewardState.Resolved && world.ChoiceObjects.Count == 0 && !world.Chapel && !upgrades.IsChoosing, "TEST5: chosen and unchosen reward objects are removed cleanly");
            Check(!choice.TryInteract() && !upgrades.Choose(1) && upgrades.Build.Acquired.Count == 1, "One reward only; duplicate selection rejected");
            Check(flow.StageIndex == stage && flow.ExitUnlocked, "TEST6: selection unlocks exit without an unsolicited transition");
            Move(flow.CurrentStage.playerExitPoint); Step(); Check(flow.StageIndex == stage + 1 && upgrades.Build.Stacks(definition) == 1, "Walking to exit continues progression and retains upgrade");
            // Future safe-stage rewards use the same flow, independent of stage IDs.
            flow.Restart(true); int safeStage = copy.stages.FindIndex(s => s.IsSafeStage);
            copy.stages[safeStage].rewardAfterClear = StageReward.UpgradeChoice; copy.stages[safeStage].completionMode = StageCompletion.Event;
            flow.EnterStage(safeStage); flow.CompleteStageEvent(); Step(); Check(world.IsPending && !flow.LivingEnemies.Any(), "Safe stages support explicitly authored chapel rewards");
            Capture("ChapelPending");
            // Verify actual E / gamepad Select input routes through stage interaction.
            InputTests();
            flow.Restart(false); Check(!world.IsPending && world.ChoiceObjects.Count == 0 && !upgrades.IsChoosing && !CombatClock.IsPaused, "Room retry removes pending reward objects and does not leak pause");
            flow.CompleteStageEvent(); Step(); Check(!world.IsPending && flow.ExitUnlocked, "Retry cannot farm an already selected stage reward");
            flow.Restart(true); flow.EnterStage(safeStage); flow.CompleteStageEvent(); Step(); Move(world.Chapel.transform.position); Check(flow.Interact(), "New run can spawn and activate a fresh chapel");
            flow.EnterStage(entrance); Check(!world.IsPending && !world.Chapel && world.ChoiceObjects.Count == 0 && !upgrades.IsChoosing, "Stage change cancels unresolved world reward safely");
            PlacementAndExhaustion(safeStage);
            flow.level = original; flow.Restart(true); Finish(0);
        }
        catch (Exception ex) { results.Add("FAIL: " + ex); Debug.LogException(ex); world?.Cancel(); if (flow && original) flow.level = original; Finish(1); }
        finally { if (copy) UnityEngine.Object.DestroyImmediate(copy); }
    }
    static void PlacementAndExhaustion(int safeStage)
    {
        var wall = new GameObject("Reward placement test wall"); wall.transform.position = flow.level.stages[safeStage].chapelSpawnPoint;
        wall.AddComponent<CombatWall>(); wall.AddComponent<BoxCollider2D>().size = new Vector2(.65f, 3);
        try
        {
            flow.Restart(true); flow.EnterStage(safeStage); flow.CompleteStageEvent(); Step();
            Check(world.IsPending && world.Chapel.transform.position.x < wall.transform.position.x - .4f, "Blocked chapel anchor relocates onto player's reachable side of wall");
            Check(!Physics2D.OverlapBoxAll(world.Chapel.transform.position, flow.player.wallCollisionSize, 0).Any(c => c.GetComponent<CombatWall>()), "Relocated chapel does not overlap solid wall geometry");
        }
        finally { world.Cancel(); UnityEngine.Object.DestroyImmediate(wall); Physics2D.SyncTransforms(); }
        var originalPool = upgrades.pool; var empty = ScriptableObject.CreateInstance<UpgradePool>(); upgrades.pool = empty;
        try
        {
            flow.Restart(true); flow.EnterStage(safeStage); flow.CompleteStageEvent(); Step();
            Check(world.IsPending && !flow.ExitUnlocked, "Exhausted pool still waits for chapel interaction");
            Move(world.Chapel.transform.position); Check(flow.Interact() && !world.IsPending && flow.ExitUnlocked && !CombatClock.IsPaused, "Exhausted pool's chapel interaction grants fallback and unlocks progression");
        }
        finally { upgrades.pool = originalPool; UnityEngine.Object.DestroyImmediate(empty); }
    }
    static void InputTests()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>(); var pad = InputSystem.AddDevice<Gamepad>();
        var background = InputSystem.settings.backgroundBehavior; var editor = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        try
        {
            Move(world.Chapel.transform.position); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update(); flow.SendMessage("Update");
            Check(world.State == WorldRewardState.Choosing && world.ChoiceObjects.Count == 3, "Keyboard E activates chapel through actual input routing");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1)); InputSystem.Update(); upgrades.SendMessage("Update");
            Check(upgrades.IsWorldChoosing && world.IsPending, "Number-key menu selection does not bypass world interaction");
            Move(world.ChoiceObjects[2].transform.position); var definition = world.ChoiceObjects[2].Upgrade;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Select)); InputSystem.Update(); flow.SendMessage("Update");
            Check(!world.IsPending && upgrades.Build.Stacks(definition) == 1, "Gamepad Select chooses nearby physical reward through actual input routing");
        }
        finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(pad); InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editor; }
    }
    static void Capture(string name)
    {
        var camera = flow.framing.GetComponent<Camera>(); var target = new RenderTexture(1280,720,24); var previous = camera.targetTexture; var active = RenderTexture.active;
        try { camera.targetTexture = target; flow.framing.ApplyFraming(0, true); camera.Render(); RenderTexture.active = target; var image = new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply(); Directory.CreateDirectory("Documentation/WorldRewardPreview"); File.WriteAllBytes("Documentation/WorldRewardPreview/" + name + ".png",image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
    static void Finish(int code)
    {
        Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/WorldRewardValidationResults.txt", results);
        Debug.Log("WORLD REWARD VALIDATION " + (code == 0 ? "PASSED" : "FAILED") + ": " + results.Count);
        if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }
}
