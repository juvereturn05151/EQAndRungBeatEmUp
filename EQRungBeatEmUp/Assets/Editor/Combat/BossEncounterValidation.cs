using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BossEncounterValidation
{
    const string Pending = "FirstBossValidation.Pending";
    static readonly List<string> results = new List<string>();
    static readonly List<Object> temporary = new List<Object>();
    static CombatClock clock;
    static StageFlowController flow;
    static TotemBossController boss;
    static EnemyCombat enemy;
    static ComboController player;
    static BossEncounterData data;
    static BossEncounterValidation() { EditorApplication.update += Poll; }
    [MenuItem("Tools/Combat/Validate First Boss (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Application.isBatchMode) { BossEncounterSetup.Build(); UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity"); }
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        if (Application.isBatchMode && SessionState.GetBool("FirstBossValidation.Finished", false) && !EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.Exit(SessionState.GetInt("FirstBossValidation.ExitCode", 1)); return; }
        if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/BossValidation.request")) { File.Delete("Temp/BossValidation.request"); Run(); }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            clock = Object.FindFirstObjectByType<CombatClock>(); flow = Object.FindFirstObjectByType<StageFlowController>();
            if (!clock || !flow) throw new Exception("Open the HauntedHouse scene before running validation.");
            clock.enabled = false; player = flow.player.GetComponent<ComboController>(); player.GetComponent<PlayerCombatInput>().enabled = false;
            TestWarpAndPool(); TestTotems(); TestAttacks(); TestSummonsAndDeath(); TestEditor();
            results.Add("ALL AUTOMATED BOSS CHECKS PASSED");
            SessionState.SetInt("FirstBossValidation.ExitCode", 0);
        }
        catch (Exception e) { results.Add("FAIL: " + e); Debug.LogException(e); }
        finally
        {
            foreach (var item in temporary) if (item) Object.DestroyImmediate(item); temporary.Clear();
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/BossEncounterValidationResults.txt", results);
            Debug.Log(string.Join("\n", results)); EditorApplication.ExitPlaymode();
            SessionState.SetBool("FirstBossValidation.Finished", true);
        }
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static void Step(int frames = 1) { for (int i = 0; i < frames; i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    static void Until(Func<bool> predicate, int limit = 300) { for (int i = 0; i < limit && !predicate(); i++) Step(); if (!predicate()) throw new Exception("Timed out waiting for boss action."); }
    static void Fixture()
    {
        // EnterStage owns all previous actors, attacks, totems, and encounter tracking.
        flow.enabled = true; int stage = flow.level.stages.FindIndex(s => s.stageId == "Stage07_WhiteGhostBossChamber"); flow.EnterStage(stage);
        player.ResetCombo(); player.health.maximumHealth = 10000; player.health.Restore(); player.motor.ResetForStage(new Vector2(-2.7f,0));
        Step(2); flow.enabled = false;
        boss = flow.StageEnemies.Select(h => h.GetComponent<TotemBossController>()).First(b => b); enemy = boss.GetComponent<EnemyCombat>();
        data = Object.Instantiate(boss.data); temporary.Add(data); boss.data = data; boss.BindEncounter();
        data.moveChance = 0; data.warpOffset = 0; data.warpCooldownFrames = 10000;
        enemy.motor.ResetForStage(Vector2.zero); enemy.target = player.transform;
    }
    static void TestWarpAndPool()
    {
        Fixture(); Check(data.warpPoints.Count == 5 && data.warpPoints.Count(p => p.enabled) == 5, "5 enabled corner / center warp points");
        Check(boss.Invulnerable && !enemy.reaction.health.Damage(1), "Boss starts invulnerable, including direct health damage");
        var box = boss.GetComponentInChildren<CombatHurtbox>(); float hp = box.health.Current;
        Check(!box.Receive(new AttackHitboxData { damage = 10 },1,player.motor) && box.health.Current == hp, "Shield rejects normal hurtbox damage without hit reaction");
        for (int i = 0; i < 12; i++)
        {
            int previous = boss.LastWarpIndex, warps = boss.WarpsPerformed; boss.ForceWarp(); Until(() => boss.WarpsPerformed > warps);
            Check(previous < 0 || previous != boss.LastWarpIndex, "No repeated warp index " + i);
            var p = (Vector2)boss.transform.position; Check(p.x >= enemy.motor.arenaMin.x && p.x <= enemy.motor.arenaMax.x && p.y >= enemy.motor.arenaMin.y && p.y <= enemy.motor.arenaMax.y, "Warp remains in combat bounds " + i);
        }
        Check(data.warpOutFeedback && data.warpInFeedback && enemy.motor.sprite.enabled, "Warp feedback assigned and boss reappears");
        foreach (var p in data.warpPoints) p.enabled = false; data.warpPoints[2].enabled = true;
        int before = boss.WarpsPerformed; boss.ForceWarp(); Until(() => boss.WarpsPerformed > before); Check(boss.LastWarpIndex == 2, "Single valid warp point remains usable with repeat disabled");
        data.canRepeatWarpPoint = true; before = boss.WarpsPerformed; boss.ForceWarp(); Until(() => boss.WarpsPerformed > before); Check(boss.LastWarpIndex == 2, "Configured same-point repetition works");
        Check(data.phase1.Select(c => c.action).SequenceEqual(new[] { BossAction.Book, BossAction.Swipe, BossAction.SummonRusher }), "Phase 1 contains Book, Swipe and Rusher");
        var selected = Enumerable.Range(0,100).Select(i => boss.ChooseAction(i / 100f)?.action).ToArray(); Check(selected.Contains(BossAction.Book) && selected.Contains(BossAction.Swipe) && selected.Contains(BossAction.SummonRusher), "Weighted random pool can select all three actions");
        data.phase1[0].weight = 0; data.phase1[1].enabled = false; Check(boss.ChooseAction(.2f)?.action == BossAction.SummonRusher, "Disabled / zero-weight actions excluded");
    }
    static void BreakAt(Vector2 position)
    {
        enemy.motor.SnapGrabToGround(position); boss.DebugInvulnerable(); boss.Totems[0].DebugBreak(player.motor);
    }
    static void TestTotems()
    {
        Fixture(); data.warpOutFrames = 10000; boss.ForceWarp();
        Check(boss.Totems.Count == 4 && boss.Totems.All(t => t.Prop.GetComponent<BoxCollider2D>().enabled), "Four damageable Totem hurt volumes");
        var totem = boss.Totems[0]; var position = (Vector2)totem.transform.position;
        totem.Prop.totemBreakRadius = .9f; Check(Mathf.Approximately(totem.BreakRadius,.9f), "Per-Totem radius override is used by its wave"); totem.Prop.totemBreakRadius = 0;
        float initialMaximum = totem.Prop.maximumHealth; totem.Prop.SetMaximumHealth(initialMaximum * 2);
        Check(totem.Prop.Current == initialMaximum * 2, "Live Totem HP tuning preserves health fraction"); totem.Prop.SetMaximumHealth(initialMaximum);
        float hp = totem.Prop.Current; totem.Prop.Receive(new AttackHitboxData { damage = 1, laneTolerance = 100 },1,player.motor);
        Check(totem.Prop.Current == hp - 1 && !totem.Prop.IsBroken, "Totem takes nonlethal damage");
        BreakAt(position + Vector2.right * (data.breakWaveRadius + .5f)); Step(data.breakWaveFrames + 1);
        Check(totem.Prop.IsBroken && !totem.Prop.GetComponent<BoxCollider2D>().enabled, "Totem destruction disables its damage collider");
        Check(boss.Invulnerable, "Boss outside radial break wave remains invulnerable");
        totem.Prop.Respawn(20); BreakAt(position + Vector2.right * .5f);
        Check(boss.Invulnerable, "Totem destruction does not immediately unlock boss");
        Until(() => !boss.Invulnerable, data.breakWaveFrames + 1);
        Check(boss.VulnerabilityRemaining == 420, "Physical expanding front opens exactly configured 420-frame window");
        CombatClock.SetPaused(boss, true); try { Step(10); Check(boss.VulnerabilityRemaining == 420, "Paused combat does not consume vulnerability frames"); } finally { CombatClock.SetPaused(boss, false); }
        var oldState = boss.State; data.warpOutFrames = 24; boss.ForceWarp(); Step(419); Check(!boss.Invulnerable && boss.VulnerabilityRemaining == 1, "Vulnerability remains through 419 elapsed combat frames");
        Step(); Check(boss.Invulnerable && boss.VulnerabilityRemaining == 0 && enemy.reaction.health.BossDamageProtection, "Returns to invulnerability exactly at 420 frames");
        Check(boss.State != BossEncounterState.Inactive && boss.State != BossEncounterState.Dead && boss.State != oldState, "Boss loop continues during vulnerability");
        Fixture(); data.warpOutFrames = 10000; boss.ForceWarp(); totem = boss.Totems[0]; position = totem.transform.position; enemy.motor.SnapGrabToGround(position);
        boss.DebugVulnerable(); Step(20); int remaining = boss.VulnerabilityRemaining; totem.DebugBreak(player.motor); Step();
        Check(boss.VulnerabilityRemaining == remaining - 1, "Repeated wave Ignore does not refresh timer");
        data.additionalWave = AdditionalTotemWave.RefreshTimer; totem.Prop.Respawn(20); enemy.motor.SnapGrabToGround(position); totem.DebugBreak(player.motor); Step();
        Check(boss.VulnerabilityRemaining == data.vulnerabilityFrames, "Repeated wave RefreshTimer restores full frame duration");
        data.totemRespawnFrames = 3; totem.Prop.Respawn(20); totem.DebugBreak(player.motor); Step(3);
        Check(!totem.Prop.IsBroken && totem.Prop.Current == data.totemRespawnHP, "Respawn mode restores configured HP after configured frames");
        data.totemMode = BossTotemMode.OneShot; totem.Prop.Respawn(20); totem.DebugBreak(player.motor); Step(5);
        Check(totem.Prop.IsBroken && totem.RespawnRemaining == 0, "One Shot mode never schedules respawn");
    }
    static CombatProjectile Fire(BossAction action)
    {
        var ranged = boss.GetComponent<EnemyProjectileAttack>(); int before = ranged.ProjectilesReleased;
        Check(boss.ForceAction(action), "Force " + action + " accepts valid action"); Until(() => ranged.ProjectilesReleased > before); return ranged.LastProjectile;
    }
    static void TestAttacks()
    {
        Fixture(); player.motor.ResetForStage(new Vector2(2,0)); var shot = Fire(BossAction.Book);
        Check(!shot.groundWave && shot.flightSprites.Length == 6 && shot.Velocity.x > 0, "Book uses shared swept projectile and existing six-frame possessed-book art");
        Step(30); Check(player.health.Current < player.health.EffectiveMaximum, "Book damages through player combat hurtbox");
        Fixture(); player.motor.ResetForStage(new Vector2(2,0)); player.motor.Face(-1); shot = Fire(BossAction.Book);
        // Contact through the same defense entry point, so no fragile timing or camera dependence.
        player.RequestGuard(true); var playerBox = player.GetComponentInChildren<CombatHurtbox>();
        Check(playerBox.Receive(shot.hit,1,enemy.motor,shot) && playerBox.LastHitOutcome == CombatHitOutcome.Parry && shot.DeflectionCount == 1 && shot.Faction == playerBox.team && shot.Velocity.x < 0, "Parried Book reverses and becomes player-owned");
        float bossHP = enemy.reaction.health.Current; Step(50); Check(enemy.reaction.health.Current == bossHP, "Deflected Book cannot bypass boss invulnerability");
        Fixture(); player.motor.ResetForStage(new Vector2(.8f,0)); player.motor.Face(-1);
        Check(boss.ForceAction(BossAction.Swipe), "Swipe uses AttackPlayer timeline");
        var swipe = boss.Selected.attack; Step(swipe.FirstActiveFrame - 1); player.RequestGuard(true); Step();
        Check(player.GetComponentInChildren<CombatHurtbox>().LastHitOutcome == CombatHitOutcome.Parry && player.health.Current == player.health.EffectiveMaximum, "Actual Swipe active hitbox is parryable and negates damage");
        Check(!enemy.reaction.CanBeParryStunned && !enemy.reaction.IsStunState && enemy.attackPlayer.CurrentAttack == swipe, "Boss exception prevents generic parry Stun and interruption");
        Fixture(); boss.DebugVulnerable(); float max = enemy.reaction.health.EffectiveMaximum; enemy.reaction.health.Damage(max * .5f);
        Check(boss.Phase2 && boss.PhaseTransitions == 1, "Phase 2 activates exactly at <=50% HP");
        enemy.reaction.health.Damage(1); Step(3); Check(boss.PhaseTransitions == 1, "Phase transition occurs once without restoring HP");
        Check(data.phase2.Count == 5 && data.phase1.All(c => data.phase2.Any(p => p.action == c.action)), "Phase 2 retains original three attacks and adds two");
        player.motor.ResetForStage(new Vector2(2,0)); shot = Fire(BossAction.CurseWave);
        Check(shot.groundWave && shot.Velocity.y == 0 && shot.waveSize == data.curseWaveProjectile.waveSize && shot.hit.hitType == HitType.Stun, "Curse uses Screamer shared forward ground rectangle with configurable Stun");
        Check(boss.GetComponent<AttackFeedback>() && data.phase2.First(c => c.action == BossAction.CurseWave).attack.feedback.directionalWaveWarning, "Curse reuses directional corridor telegraph");
        Step(30); Check(player.State == CombatState.Stunned, "Curse Wave puts player in existing Stunned state");
        Fixture(); boss.ForcePhase2(); player.motor.ResetForStage(new Vector2(2,.55f)); shot = Fire(BossAction.CurseWave); Step(40);
        Check(player.health.Current == player.health.EffectiveMaximum, "Player can sidestep Curse by leaving its walking-lane depth");
    }
    static void TestSummonsAndDeath()
    {
        Fixture(); int count = boss.ActiveMinions; Check(boss.ForceAction(BossAction.SummonRusher), "Rusher summon starts"); Until(() => boss.ActiveMinions > count);
        Check(boss.ActiveMinions == 1 && flow.EncounterRusherCount(enemy) == 1, "Default summon creates one existing Rusher tracked by encounter");
        Check(flow.LivingEnemies.All(h => h == enemy.reaction.health || Vector2.Distance(h.transform.position,player.transform.position) >= data.playerSpawnClearance), "Summons respect player clearance");
        Fixture(); boss.ForcePhase2(); Check(boss.ForceAction(BossAction.SummonStrongGhosts), "Strong Ghost action starts"); Until(() => boss.ActiveMinions == 2);
        var strong = flow.LivingEnemies.Where(h => h != enemy.reaction.health).Select(h => h.GetComponent<EnemyCombat>()).ToArray();
        Check(strong.Any(c => c.aiProfile == data.grapplerPrefab.GetComponent<EnemyCombat>().aiProfile && c.GetComponent<CombatGrabController>()), "Strong summon reuses Grappler implementation");
        Check(strong.Any(c => c.aiProfile == data.throwerPrefab.GetComponent<EnemyCombat>().aiProfile && c.GetComponent<EnemyProjectileAttack>()), "Strong summon reuses Thrower implementation");
        Fixture(); boss.ForcePhase2(); data.maxBossMinions = 1; data.strongGhostPriority = StrongGhostPriority.ThrowerFirst;
        Check(boss.ForceAction(BossAction.SummonStrongGhosts), "Strong summon with one minion slot starts"); Until(() => boss.ActiveMinions > 0);
        Check(boss.ActiveMinions == 1 && flow.LivingEnemies.Any(h => h.GetComponent<EnemyCombat>()?.aiProfile == data.throwerPrefab.GetComponent<EnemyCombat>().aiProfile), "One-slot priority chooses Thrower without exceeding cap");
        Check(!boss.ActionAvailable(data.phase2.First(c => c.action == BossAction.SummonStrongGhosts)), "Summon unavailable when boss minion cap is full");
        var extra = flow.RequestBossMinions(enemy, data.rusherPrefab, 100, null, .1f, .1f);
        Check(flow.EncounterEnemyCount(enemy) <= flow.CurrentStage.encounters[0].maxActiveEnemies && flow.LivingEnemies.Count() <= flow.CurrentStage.maxActiveEnemies, "Shared spawning respects encounter and stage enemy caps");
        Fixture(); player.motor.ResetForStage(new Vector2(2,0)); var shot = Fire(BossAction.Book); boss.DebugVulnerable(); enemy.reaction.health.Damage(100000);
        Check(boss.State == BossEncounterState.Dead && !enemy.attackPlayer.CurrentAttack && !shot.gameObject.activeSelf, "Boss death stops AI, cancels attack and immediately deactivates owned projectiles");
        Step(3); Check(flow.Destructibles.Any(t => !t.IsBroken), "Boss can die while unused Totems remain");
        flow.enabled = true; Step(3); Check(flow.EncountersComplete && flow.ExitUnlocked && !flow.ActiveCameraBounds.HasValue, "Tracked boss death completes encounter and unlocks exit / camera without requiring all Totems broken");
    }
    static void TestEditor()
    {
        var asset = AssetDatabase.LoadAssetAtPath<BossEncounterData>(BossEncounterSetup.DataPath);
        var copy = Object.Instantiate(asset); temporary.Add(copy); float weight = copy.phase1[0].weight; int frames = copy.vulnerabilityFrames;
        Undo.RecordObject(copy, "Boss validation tuning"); copy.phase1[0].weight = 9; copy.vulnerabilityFrames = 111; Undo.FlushUndoRecordObjects();
        Undo.PerformUndo(); Check(copy.phase1[0].weight == weight && copy.vulnerabilityFrames == frames, "Editor serialized phase weights / vulnerability Undo works");
        Undo.PerformRedo(); Check(copy.phase1[0].weight == 9 && copy.vulnerabilityFrames == 111, "Editor tuning Redo works");
        var path = "Assets/BossValidationTemporary.asset";
        try
        {
            var saved = Object.Instantiate(asset); AssetDatabase.CreateAsset(saved,path); saved.vulnerabilityFrames = 421; EditorUtility.SetDirty(saved); AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); Check(AssetDatabase.LoadAssetAtPath<BossEncounterData>(path).vulnerabilityFrames == 421, "Serialized boss values persist after asset reload");
        }
        finally { AssetDatabase.DeleteAsset(path); }
        Check(File.Exists("Assets/Editor/Combat/BossEditorWindow.cs") && asset.warpPoints.Count == 5 && asset.breakWaveRadius > 0, "Visual editor and authorable warp / Totem radius data installed");
        results.Add("MANUAL: Verify Scene View dragging, radius slider, preview art, Add/Remove list controls and perceptual VFX/SFX quality. See Documentation/FirstBossEncounter.md.");
    }
}
