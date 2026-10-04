using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerHubValidation
{
    const string Pending = "BeatEmUp.PlayerHubValidation";
    static readonly List<string> results = new List<string>();
    static PlayerHubValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Stages/Validate outdoor safe hub (Play Mode)")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); var clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>();
            Check(flow.StageIndex == 0 && flow.CurrentStage.stageId == PlayerHubSetup.HubId, "TEST1: actual scene Start spawns in Player Hub at index0");
            flow.enabled = false; clock.enabled = false; flow.Restart(true);
            var player = flow.player.GetComponent<ComboController>(); player.GetComponent<PlayerCombatInput>().enabled = false;
            Check(flow.level.stages.Count == 9 && flow.level.stages.Select(s => s.stageId).SequenceEqual(new[] { PlayerHubSetup.HubId, "Stage01_EntranceGate", "Stage02_BloodSheetCorridor", "Stage03_FakeMorgue", "Stage04_ServiceCorridor", "Stage05_HauntedMaze", "Stage06_RecoveryShrine", "Stage07_WhiteGhostBossChamber", "Stage08_EscapeLane" }), "Nine stages retain haunted-house order after the hub");
            var hub = flow.CurrentStage;
            Check(hub.stageType == StageType.Safe && hub.IsSafeStage && hub.rewardAfterClear == StageReward.None && hub.completionMode == StageCompletion.ReachExit && hub.encounters.Count == 0 && hub.destructibles.Count == 0, "Hub authored as Safe / ReachExit / no reward / no encounters or destructibles");
            Check(flow.background.sprite == hub.backgroundSprite && flow.floor.sprite == hub.floorSprite, "TEST2: correct imported hub background and floor are assigned");
            Check(hub.backgroundSprite.texture == hub.floorSprite.texture && hub.backgroundSprite.rect.yMin == hub.floorSprite.rect.yMax && hub.backgroundSprite.rect.height + hub.floorSprite.rect.height == hub.backgroundSprite.texture.height, "Source artwork sections meet exactly without duplicated or missing rows");
            Check(flow.player.transform.position == (Vector3)hub.playerEntryPoint && flow.player.arenaMin == hub.movementMin && flow.player.arenaMax == hub.movementMax, "Player spawns at authored hub entry with correct movement bounds");
            Check(flow.framing.bottomLane == hub.movementMin.y && flow.framing.topLane == hub.movementMax.y && flow.framing.GetComponent<Camera>().orthographic, "Hub reuses authored lane/camera framing");
            Capture(flow);
            float x = player.transform.position.x; player.motor.MoveInput = Vector2.right;
            for (int i = 0; i < 12; i++) { clock.StepFrame(); flow.Tick(CombatClock.FrameSeconds); }
            Check(player.transform.position.x > x + .5f && !player.motor.MovementLocked, "TEST3: player walks normally in the safe hub"); player.motor.MoveInput = Vector2.zero;
            for (int i = 0; i < 240; i++) { clock.StepFrame(); flow.Tick(CombatClock.FrameSeconds); }
            Check(!flow.LivingEnemies.Any() && flow.Destructibles.Count == 0 && flow.ExitUnlocked && !flow.RunUpgrades.IsChoosing && string.IsNullOrEmpty(flow.Failure), "TEST4: no enemy waves, combat locks, rewards or failures after waiting");
            float hp = player.health.Current; var state = player.State;
            Check(!player.health.Damage(10000) && player.health.Current == hp, "TEST5: direct environmental/lethal damage cannot harm a safe-stage player");
            var hit = new AttackHitboxData { damage = 10000, hitType = HitType.Launcher, unblockable = true };
            Check(!player.GetComponentInChildren<CombatHurtbox>().Receive(hit, -1) && player.health.Current == hp && player.State == state && player.motor.IsGrounded, "Safe stage rejects enemy hit before guard, knockback, launch or hitstop");
            var secondWind = flow.RunUpgrades.pool.upgrades.Single(u => u.id == "SecondWind"); flow.RunUpgrades.Build.Acquire(secondWind); player.health.Damage(10000);
            Check(flow.RunUpgrades.Build.LethalSavesUsed == 0, "Safe-stage protection does not consume Second Wind"); flow.RunUpgrades.Build.ResetRun();
            player.RequestAttack(); Check(player.CurrentAttack == player.groundCombo[0], "Optional practice attacks still work without enemies or damage pressure"); player.ResetCombo();
            var serialized = new SerializedObject(flow.level);
            Check(serialized.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("stageType").enumValueIndex == (int)StageType.Safe, "TEST8: hub and editable stage type are present in the authoring list");
            var original = flow.level; var copy = UnityEngine.Object.Instantiate(original);
            var enemy = copy.stages[1].encounters[0].waves[0].enemySpawns[0].prefab;
            copy.stages[0].encounters.Add(copy.stages[1].encounters[0]); copy.stages[0].destructibles.Add(copy.stages[1].destructibles[0]); copy.stages[0].decorativeProps.Add(new StagePropPlacement { prefab = enemy });
            flow.level = copy; flow.EnterStage(0);
            for (int i = 0; i < 120; i++) { clock.StepFrame(); flow.Tick(CombatClock.FrameSeconds); }
            Check(!flow.LivingEnemies.Any() && !UnityEngine.Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Any(e => e.isActiveAndEnabled) && flow.Destructibles.Count == 0 && flow.ExitUnlocked, "Safe flag suppresses mistakenly authored encounters, enemy decorations and destructibles");
            flow.level = original; flow.EnterStage(0); UnityEngine.Object.DestroyImmediate(copy);
            player.motor.ResetForStage(hub.playerExitPoint); clock.StepFrame(); flow.Tick(CombatClock.FrameSeconds);
            Check(flow.StageIndex == 1 && flow.CurrentStage.stageId == "Stage01_EntranceGate" && !player.health.SafeStageProtection && !flow.RunUpgrades.IsChoosing, "TEST6: walking to hub exit enters Entrance Gate without a reward choice");
            Check(player.health.Damage(10) && player.health.Current == hp - 10, "Damage protection is removed immediately upon entering combat");
            for (int i = 0; i < 25; i++) { clock.StepFrame(); flow.Tick(CombatClock.FrameSeconds); }
            Check(flow.LivingEnemies.Count() == 2 && !flow.ExitUnlocked && flow.CurrentStage.rewardAfterClear == StageReward.UpgradeChoice, "TEST7: Entrance Gate retains its normal first wave, lock and upgrade reward");
            copy = UnityEngine.Object.Instantiate(original); copy.stages.RemoveAt(0); copy.stages[0].nextStageIndex = 1; copy.stages[7].nextStageIndex = 8;
            PlayerHubSetup.Configure(copy); Check(copy.stages[1].nextStageIndex == 2 && copy.stages[8].nextStageIndex == 9, "Hub insertion remaps explicit next-stage and end-of-level indices");
            string snapshot = JsonUtility.ToJson(copy); PlayerHubSetup.Configure(copy); Check(JsonUtility.ToJson(copy) == snapshot && copy.stages.Count == 9, "Setup is idempotent and preserves existing authored settings"); UnityEngine.Object.DestroyImmediate(copy);
            flow.Restart(true); Check(flow.StageIndex == 0 && player.health.SafeStageProtection && flow.RunUpgrades.Build.Acquired.Count == 0, "New run returns to the protected hub with an empty build");
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/PlayerHubValidationResults.txt", results); Debug.Log("PLAYER HUB VALIDATION PASSED: " + results.Count);
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch (Exception ex) { results.Add("FAIL: " + ex); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/PlayerHubValidationResults.txt", results); Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode(); }
    }
    static void Capture(StageFlowController flow)
    {
        var camera = flow.framing.GetComponent<Camera>(); var render = new RenderTexture(1280, 720, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        try
        {
            camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            Directory.CreateDirectory("Documentation/PlayerHubPreview"); File.WriteAllBytes("Documentation/PlayerHubPreview/PlayerHub.png", image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; render.Release(); UnityEngine.Object.DestroyImmediate(render); }
    }
}
