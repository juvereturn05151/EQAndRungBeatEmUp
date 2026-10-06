using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class HauntedStageFlowValidation
{
    private const string Pending = "BeatEmUp.HauntedFlowValidation";
    private static readonly List<string> results = new List<string>();
    private static StageFlowController flow;
    private static CombatClock clock;
    private static AttackData punch;
    private static int Entrance => flow.level.stages.FindIndex(s => s.stageId == "Stage01_EntranceGate");
    static HauntedStageFlowValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Stages/Validate full haunted-house flow (Play Mode)")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    private static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    private static void Step(int frames)
    {
        for (int i = 0; i < frames; i++) { clock.StepFrame(); flow.Tick(CombatClock.FrameSeconds); flow.framing.ApplyFraming(CombatClock.FrameSeconds); }
    }
    private static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false;
            // Scheduling/exit regression stays independent of randomized rewards.
            // RunUpgradeValidation exercises the authored reward flow separately.
            flow.level = UnityEngine.Object.Instantiate(flow.level);
            // This encounter fixture expects the original room sequence; sort only its disposable copy.
            flow.level.stages = flow.level.stages.OrderBy(s => s.stageId, StringComparer.Ordinal).ToList();
            foreach (var stage in flow.level.stages) stage.rewardAfterClear = StageReward.None;
            flow.enabled = false; flow.player.GetComponent<PlayerCombatInput>().enabled = false;
            punch = AssetDatabase.LoadAssetAtPath<AttackData>("Assets/EQ_Rung_BeatEmUp/Attacks/Punch1.asset");
            flow.EnterStage(Entrance); Step(25);
            Check(flow.StageIndex == Entrance && flow.background.sprite == flow.level.stages[Entrance].backgroundSprite && flow.floor.sprite == flow.level.stages[Entrance].floorSprite, "TEST1: stage1 loads correct separated art");
            Check(flow.LivingEnemies.Count() == 2 && !flow.ExitUnlocked && !flow.TryAdvance(), "TEST5: encounter locks exit while required enemies live");
            var prop = flow.Destructibles[0]; PauseEnemies();
            flow.player.GetComponent<CharacterHealth>().Restore(); flow.player.ResetForStage((Vector2)prop.transform.position - new Vector2(.55f, 0)); flow.player.Face(1);
            Check(flow.player.attackPlayer.Play(punch), "Player punch starts for repeated-hit validation"); Step(punch.FirstActiveFrame + 1);
            Check(prop.Current < prop.maximumHealth && prop.visual.sprite == prop.damagedSprite, "TEST6: real player attack damages prop and shows damaged sprite");
            float health = prop.Current; flow.player.GetComponent<AttackHitbox>().Sample(); Check(prop.Current == health, "Prop cannot be hit twice by repeated samples of the same hit ID");
            Step(65);
            HitProp(prop); Check(prop.IsBroken && !prop.GetComponent<BoxCollider2D>().enabled && prop.visual.sprite == prop.brokenSprite, "TEST6: prop breaks with disabled hurtbox and broken visual");
            foreach (var enemy in flow.LivingEnemies.ToArray()) Defeat(enemy);
            Step(25); Check(flow.LivingEnemies.Count() == 1 && !flow.ExitUnlocked, "TEST4: clear-based wave spawns only after previous enemies AND queued spawns clear");
            ClearRoom(); Leave(); Check(flow.StageIndex == Entrance + 1, "TEST2: stage1 completion and exit move to stage2");
            Check(flow.player.arenaMin == flow.CurrentStage.movementMin && flow.player.transform.position == (Vector3)flow.CurrentStage.playerEntryPoint, "Transition sets entry, bounds, grounded player and facing");
            Step(150); Check(!flow.LivingEnemies.Any() && !flow.ExitUnlocked, "TEST3: timed wave remains pending before three seconds, exit stays locked");
            Step(65); Check(flow.LivingEnemies.Count() == 3, "TEST3: timed wave spawns three Rushers after delay plus interval");
            Check(flow.LivingEnemies.First().GetComponent<EnemyCombat>().target == flow.player.transform, "Spawned AI targets player and receives lane bounds");
            var ai = flow.LivingEnemies.First().GetComponent<EnemyCombat>(); PauseEnemies();
            flow.player.ResetForStage(new Vector2(0, 0)); ai.motor.ResetForStage(new Vector2(-.6f, 0)); ai.motor.Face(1);
            float playerHealth = flow.player.GetComponent<CharacterHealth>().Current;
            Check(ai.attackPlayer.Play(ai.attack), "New enemy prefab can run its authored frame attack"); Step(80);
            Check(flow.player.GetComponent<CharacterHealth>().Current < playerHealth, "New enemy attack damages existing player through combat hurtbox");
            ClearRoom(); Leave();
            for (int stage = 2; stage <= 4; stage++)
            {
                Check(flow.StageIndex == Entrance + stage, "Ordered combat room " + (stage + 1)); Step(45);
                Check(flow.background.sprite == flow.CurrentStage.backgroundSprite && flow.floor.sprite == flow.CurrentStage.floorSprite, "Correct art in room " + (stage + 1)); Capture("Stage" + (stage + 1));
                ClearRoom(); Leave();
            }
            Check(flow.StageIndex == Entrance + 5 && flow.CurrentStage.safeRoom && !flow.LivingEnemies.Any(), "TEST7: shrine room has no enemies");
            flow.player.GetComponent<CharacterHealth>().Damage(50); flow.player.ResetForStage(flow.CurrentStage.recoveryPoint);
            Check(flow.Recover() && flow.player.GetComponent<CharacterHealth>().Current == flow.player.GetComponent<CharacterHealth>().maximumHealth, "TEST7: nearby shrine interaction restores health"); Capture("Stage6-Shrine"); Leave();
            Step(2); var boss = flow.LivingEnemies.Single(); var bossControl = boss.GetComponent<TotemBossController>(); var bossHurt = boss.GetComponentInChildren<CombatHurtbox>();
            Check(flow.StageIndex == Entrance + 6 && flow.RemainingTotems == 4 && bossControl.Invulnerable, "TEST8: boss spawns protected by four cursed totems");
            var hit = punch.frames.First(f => f.hitboxes.Count > 0).hitboxes[0]; float bossHealth = boss.Current;
            Check(!bossHurt.Receive(hit, 1, flow.player) && boss.Current == bossHealth && !flow.TryAdvance(), "TEST8: protected boss rejects combat damage and room cannot complete");
            Step(70); // Exercise a real warp before positioning the contact fixture.
            // This suite tests stage progression with many individual prop punches; the boss suite checks the exact default 420-frame window.
            bossControl.data = UnityEngine.Object.Instantiate(bossControl.data); bossControl.data.warpOutFrames = 10000;
            bossControl.data.vulnerabilityFrames = 10000; bossControl.data.totemMode = BossTotemMode.OneShot; bossControl.BindEncounter(); bossControl.ForceWarp();
            boss.GetComponent<CharacterMotor>().SnapGrabToGround(flow.Destructibles.First().transform.position);
            foreach (var totem in flow.Destructibles.ToArray()) while (!totem.IsBroken) HitProp(totem);
            Step(2); Check(flow.RemainingTotems == 0 && !bossControl.Invulnerable && !bossHurt.externalInvulnerable, "TEST8: a Totem break wave reaches the boss and opens its vulnerability overlay");
            Check(bossControl.WarpsPerformed > 0, "Boss telegraphs and performs an arena warp during the totem phase");
            Capture("Stage7-Boss"); Defeat(boss); Step(2); Check(flow.ExitUnlocked, "Boss defeat unlocks exit only after required encounter completes"); Leave();
            Check(flow.StageIndex == Entrance + 7 && !flow.LivingEnemies.Any(), "TEST9: boss clear leads to Escape Lane"); Capture("Stage8-Exit"); Leave();
            Check(flow.LevelCompleted, "TEST10: full eight-stage progression reaches level completion using actual attacks and exit movement");
            flow.Restart(true); Step(2); Check(flow.StageIndex == 0 && !flow.LevelCompleted, "Completed level can restart without stale waves or totems");
            ExtraTriggers();
            flow.RestartAt(3); Step(2); flow.player.GetComponent<CharacterHealth>().Damage(10000); Check(!flow.TryAdvance(), "Dead player cannot progress");
            flow.Restart(false); Step(2); Check(flow.StageIndex == 3 && !flow.player.GetComponent<CharacterHealth>().IsDead, "Death retry reloads current room with restored player");
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/HauntedStageFlowValidationResults.txt", results);
            Debug.Log("HAUNTED STAGE FLOW VALIDATION PASSED: " + results.Count);
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch (Exception ex)
        {
            results.Add("FAIL: " + ex); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/HauntedStageFlowValidationResults.txt", results); Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
        }
    }
    private static void PauseEnemies() { foreach (var h in flow.LivingEnemies) h.GetComponent<EnemyCombat>().enabled = false; }
    private static void HitProp(DestructibleObject prop)
    {
        PauseEnemies(); flow.player.GetComponent<CharacterHealth>().Restore();
        flow.player.ResetForStage((Vector2)prop.transform.position - new Vector2(.55f, 0)); flow.player.Face(1);
        Check(flow.player.attackPlayer.Play(punch), "Player punch starts for environmental object"); Step(65);
    }
    private static void Defeat(CharacterHealth enemy)
    {
        int attempts = 0;
        while (enemy && !enemy.IsDead && attempts++ < 30)
        {
            PauseEnemies(); flow.player.GetComponent<CharacterHealth>().Restore();
            flow.player.ResetForStage((Vector2)enemy.transform.position - new Vector2(.55f, 0)); flow.player.Face(1);
            if (!flow.player.attackPlayer.Play(punch)) throw new Exception("Player attack did not start"); Step(65);
        }
        Check(!enemy || enemy.IsDead, "Real player attacks defeat " + (enemy ? enemy.name : "enemy"));
    }
    private static void ClearRoom()
    {
        int frames = 0;
        while (!flow.ExitUnlocked && frames++ < 100)
        {
            Step(30); foreach (var enemy in flow.LivingEnemies.ToArray()) Defeat(enemy);
        }
        Check(flow.ExitUnlocked, "All required waves clear and exit unlocks in room " + (flow.StageIndex + 1));
    }
    private static void Leave() { flow.player.ResetForStage(flow.CurrentStage.playerExitPoint); Step(1); }
    private static void ExtraTriggers()
    {
        var original = flow.level; var copy = UnityEngine.Object.Instantiate(original);
        var prefab = original.stages[Entrance].encounters[0].waves[0].enemySpawns[0].prefab;
        var stage = copy.stages[Entrance]; stage.encounters.Clear(); stage.completionMode = StageCompletion.Event;
        var first = new EncounterDefinition { encounterId = "Manual", trigger = EncounterTrigger.Manual };
        var wave = new WaveDefinition { waveId = "ManualWave", trigger = WaveTrigger.Manual };
        wave.enemySpawns.Add(new EnemySpawnDefinition { prefab = prefab, count = 2, interval = 1 }); first.waves.Add(wave); stage.encounters.Add(first);
        var zone = new EncounterDefinition { encounterId = "Zone", trigger = EncounterTrigger.PlayerZone, triggerDelay = .5f, triggerZone = new Rect(-.2f, -.3f, .4f, .6f) }; zone.waves.Add(new WaveDefinition()); stage.encounters.Add(zone);
        var phase = new EncounterDefinition { encounterId = "Phase", trigger = EncounterTrigger.PreviousEncounterClear, requiredPreviousEncounter = 0 }; phase.waves.Add(new WaveDefinition()); stage.encounters.Add(phase);
        flow.level = copy; flow.RestartAt(Entrance); Step(2); Check(!flow.LivingEnemies.Any() && !flow.ExitUnlocked, "Manual encounters wait for named signal and event stage waits for event");
        flow.SignalEncounter("Manual"); Step(2); Check(!flow.LivingEnemies.Any(), "Manual wave independently waits for wave signal");
        flow.SignalWave("Manual", "ManualWave"); Step(2); Check(flow.LivingEnemies.Count() == 1, "Manual wave starts first interval spawn");
        flow.LivingEnemies.Single().Damage(10000); Step(2); Check(!flow.EncountersComplete, "Pending interval spawn prevents premature wave completion");
        Step(65); Check(flow.LivingEnemies.Count() == 1, "Second interval enemy spawns even if first enemy died early");
        flow.LivingEnemies.Single().Damage(10000); Step(2); flow.player.ResetForStage(Vector2.zero); Step(2);
        Check(!flow.EncountersComplete, "Zone trigger respects its authored delay");
        flow.player.ResetForStage(new Vector2(-2, 0)); Step(35);
        Check(flow.EncountersComplete && !flow.ExitUnlocked, "Zone and phase encounters complete, explicit stage event still required");
        flow.CompleteStageEvent(); Check(flow.ExitUnlocked, "Explicit event unlocks authored event stage");
        flow.level = original; UnityEngine.Object.DestroyImmediate(copy); flow.RestartAt(Entrance);
    }
    private static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var camera = flow.framing.GetComponent<Camera>(); var target = new RenderTexture(1280, 720, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); Directory.CreateDirectory("Documentation/HauntedStageFlowPreview"); File.WriteAllBytes("Documentation/HauntedStageFlowPreview/" + name + ".png", image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
}
