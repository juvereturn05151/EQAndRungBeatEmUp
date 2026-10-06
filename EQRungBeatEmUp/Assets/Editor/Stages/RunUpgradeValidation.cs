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
public static class RunUpgradeValidation
{
    const string Pending = "BeatEmUp.RunUpgradeValidation";
    static bool AutoExit => Application.isBatchMode || Environment.GetCommandLineArgs().Contains("-runUpgradeValidationAutoExit");
    static readonly List<string> results = new List<string>();
    static StageFlowController flow;
    static RunUpgradeController rewards;
    static RunBuildState build;
    static ComboController player;
    static CombatClock clock;
    static GameObject target;
    static bool capturing;
    static double captureDue;
    static RunUpgradeValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Upgrades/Validate stage run upgrades (Play Mode)")]
    public static void Run()
    {
        if (!AutoExit && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity"); SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition, string description) { if (!condition) throw new Exception(description); results.Add("PASS: " + description); }
    static void Near(float actual, float expected, string description) => Check(Mathf.Abs(actual - expected) < .001f, description + " (" + actual + ")");
    static void Step(int count = 1) { for (int i = 0; i < count; i++) { clock.StepFrame(); flow.Tick(CombatClock.FrameSeconds); } }
    static UpgradeDefinition Card(string id) => rewards.pool.upgrades.Single(u => u.id == id);
    static void Give(string id) => Check(build.Acquire(Card(id)), "Acquire " + id);
    static void Neutral()
    {
        rewards.CloseChoice(); build.ResetRun(); player.health.Restore(); player.ResetCombo(); player.motor.ResetForStage(Vector2.zero); player.motor.Face(1);
        player.motor.arenaMin = new Vector2(-10, -2); player.motor.arenaMax = new Vector2(10, 2);
        foreach (var h in flow.LivingEnemies) { h.GetComponent<EnemyCombat>().enabled = false; h.gameObject.SetActive(false); }
        foreach (var prop in flow.Destructibles) if (prop) prop.gameObject.SetActive(false);
        if (target) UnityEngine.Object.DestroyImmediate(target);
        target = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab"));
        target.GetComponent<EnemyCombat>().enabled = false; target.GetComponent<CharacterMotor>().ResetForStage(new Vector2(.7f, 0));
        target.GetComponent<CharacterHealth>().maximumHealth = 500; target.GetComponent<CharacterHealth>().Restore();
    }
    static AttackHitboxData BaseHit(AttackData attack) => attack.frames.First(f => f.hitboxes.Count > 0).hitboxes[0];
    static float TunedDamage(AttackData attack, bool airborne = false)
    {
        player.attackPlayer.Stop(); player.health.Restore();
        Check(player.attackPlayer.Play(attack), "Play attack for runtime calculation: " + attack.attackName);
        var motor = target.GetComponent<CharacterMotor>(); motor.ResetForStage(new Vector2(.7f, 0)); if (airborne) motor.Launch(4, 0);
        return build.ModifyHit(BaseHit(attack), motor).damage;
    }
    static int Stage(string id) => flow.level.stages.FindIndex(s => s.stageId == id || s.stageId?.Replace("Corridoor","Corridor")==id);
    static void LeaveHub() { if(flow.CurrentStage.hub) flow.GetComponent<PlayerHubController>().BeginRun(); else if (flow.CurrentStage.IsSafeStage) { player.motor.ResetForStage(flow.CurrentStage.playerExitPoint); Step(); } Check(flow.CurrentStage.stageId == "Stage01_EntranceGate", "Run reaches first combat stage without forced cards"); }
    static void ClearCombatRoom()
    {
        int guard = 0;
        while (!rewards.IsChoosing && !flow.CompletionSatisfied && guard++ < 700)
        {
            foreach (var h in flow.LivingEnemies.ToArray()) { h.GetComponent<EnemyCombat>().enabled = false; h.Damage(10000); }
            Step();
            if(string.IsNullOrEmpty(flow.ActiveEncounterName))
                foreach(var encounter in flow.CurrentStage.encounters.Where(e=>e.enabled && e.trigger==EncounterTrigger.PlayerZone))
                { player.motor.ResetForStage(encounter.triggerZone.center); Step(); if(!string.IsNullOrEmpty(flow.ActiveEncounterName) || flow.CompletionSatisfied) break; }
        }
        if(flow.CompletionSatisfied && flow.CurrentStage.completionMode==StageCompletion.ReachExit && !flow.WorldRewards.IsPending)
        { player.motor.ResetForStage(flow.CurrentStage.playerExitPoint); Step(); }
        Check(guard < 700 && flow.WorldRewards && flow.WorldRewards.State == WorldRewardState.RewardPending && !rewards.IsChoosing, "Stage " + (flow.StageIndex + 1) + " clear spawns chapel without cards; failure: " + flow.Failure);
        player.motor.ResetForStage(flow.WorldRewards.Chapel.transform.position);
        Check(flow.Interact() && rewards.IsWorldChoosing, "Chapel interaction opens physical current-run choices");
    }
    static void Pick(string preferred)
    {
        // Deterministic debug reroll is used only to select a named test upgrade;
        // normal gameplay does not expose reroll.
        int guard = 0; while (!rewards.Choices.Any(u => u.id == preferred) && guard++ < 1000) rewards.DebugReroll();
        Check(guard < 1000, "Named test card becomes available: " + preferred);
        int index = rewards.Choices.ToList().FindIndex(u => u.id == preferred);
        player.motor.ResetForStage(flow.WorldRewards.ChoiceObjects[index].transform.position);
        Check(flow.Interact(), "World choice applies immediately: " + preferred);
        player.motor.ResetForStage(flow.CurrentStage.playerExitPoint); Step();
        while (flow.CurrentStage.IsSafeStage && flow.CurrentStage.rewardAfterClear == StageReward.None && !flow.LevelCompleted) { player.motor.ResetForStage(flow.CurrentStage.playerExitPoint); Step(); }
    }
    static void Poll()
    {
        if (capturing)
        {
            if (EditorApplication.timeSinceStartup < captureDue) return;
            capturing = false; rewards.CloseChoice();
            Debug.Log("RUN UPGRADE VALIDATION PASSED: " + results.Count);
            if (AutoExit) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode(); return;
        }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            flow = UnityEngine.Object.FindFirstObjectByType<StageFlowController>(); flow.enabled = false;
            rewards = flow.RunUpgrades; rewards.Initialize(); build = rewards.Build; player = flow.player.GetComponent<ComboController>();
            player.GetComponent<PlayerCombatInput>().enabled = false; clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false;
            flow.Restart(true); Check(build.Acquired.Count == 0 && build.Modifiers.Count == 0, "TEST1: new run starts with base stats and no upgrades");
            Check(rewards.pool.upgrades.Count == 18 && rewards.pool.upgrades.All(u => u && u.effects.Count > 0), "18 data-driven upgrade assets load with effects");
            string[] attackPaths = AssetDatabase.FindAssets("t:AttackData", new[] { "Assets/EQ_Rung_BeatEmUp/Attacks" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var snapshots = attackPaths.ToDictionary(path => path, File.ReadAllText);
            LeaveHub(); ClearCombatRoom(); Check(rewards.Choices.Count == 3 && rewards.Choices.Distinct().Count() == 3 && !flow.ExitUnlocked && !flow.TryAdvance(), "TEST2: three unique cards block progression until selection");
            long tick = clock.FrameNumber; float elapsed = flow.StageElapsed; var position = player.transform.position;
            player.ResetCombo(); player.motor.MoveInput = Vector2.right; Step(20); player.motor.MoveInput = Vector2.zero;
            Check(clock.FrameNumber > tick && flow.StageElapsed == elapsed && player.transform.position.x > position.x && !CombatClock.IsPaused, "World reward preserves player movement and frame clock while stopping stage encounter scheduling");
            Pick("HeavyHands"); Check(flow.StageIndex == Stage("Stage02_BloodSheetCorridor") && !rewards.IsChoosing && !CombatClock.IsPaused && Time.timeScale == 1, "Physical selection unlocks exit and walking there advances to Stage2");
            Neutral(); Give("HeavyHands"); var enemyHealth = target.GetComponent<CharacterHealth>(); float hp = enemyHealth.Current;
            player.RequestAttack(); Step(player.groundCombo[0].FirstActiveFrame + 1);
            Near(hp - enemyHealth.Current, BaseHit(player.groundCombo[0]).damage * 1.2f, "TEST3: actual Stage2 punch hit receives runtime +20% ground damage");
            player.ResetCombo(); if (target) UnityEngine.Object.DestroyImmediate(target); flow.Restart(true); LeaveHub();
            ClearCombatRoom(); Pick("HeavyHands"); ClearCombatRoom(); Pick("HardHead");
            Check(flow.StageIndex == Stage("Stage03_FakeMorgue") && build.Stacks(Card("HeavyHands")) == 1 && build.Stacks(Card("HardHead")) == 1, "TEST4/5: consecutive stages add persistent brawler and dive upgrades");
            flow.player.Launch(4, 0); player.attackPlayer.Play(player.airDive);
            Near(build.ModifyHit(BaseHit(player.airDive), null).damage, 17.5f, "TEST5: Hard Head immediately raises14 base dive damage to17.5");
            player.ResetCombo(); flow.player.ResetForStage(flow.CurrentStage.playerEntryPoint);
            ClearCombatRoom(); Pick("PerfectTiming"); Check(player.EffectiveParryWindow == player.defenseData.parryWindowFrames + 2, "TEST6: selected parry upgrade immediately extends window");
            ClearCombatRoom(); Pick("IronBody"); player.health.Damage(180); ClearCombatRoom(); Pick("LongStep");
            Check(flow.StageIndex == Stage("Stage06_RecoveryShrine") && !rewards.IsChoosing && !flow.LivingEnemies.Any(), "TEST7: shrine has no enemies and no forced cards");
            float max = player.health.EffectiveMaximum; player.motor.ResetForStage(flow.CurrentStage.playerExitPoint); Step();
            Check(flow.StageIndex == Stage("Stage08_EscapeLane") && !rewards.IsChoosing, "Heal reward follows authored shrine exit without upgrade UI"); Near(player.health.Current, Mathf.Min(max, max - 180 + max * .5f), "Shrine exit Heal uses configured fraction of upgraded maximum HP");
            flow.EnterStage(Stage("Stage07_WhiteGhostBossChamber")); Step(2); Check(build.Acquired.Count == 5 && build.Value(RunModifier.DiveDamage) == .25f, "TEST8: all five acquired upgrades persist into boss");
            var boss = flow.LivingEnemies.Single(); var hurt = boss.GetComponentInChildren<CombatHurtbox>(); hp = boss.Current;
            player.motor.Launch(4, 0); player.attackPlayer.Play(player.airDive);
            Check(!hurt.Receive(build.ModifyHit(BaseHit(player.airDive), boss.GetComponent<CharacterMotor>()), 1, player.motor) && boss.Current == hp && flow.RemainingTotems == 4, "Upgraded headbutt cannot bypass cursed-totem boss protection");
            boss.GetComponent<CharacterMotor>().SnapGrabToGround(flow.Destructibles.First().transform.position);
            foreach (var prop in flow.Destructibles.ToArray()) prop.Receive(new AttackHitboxData { damage = 10000, laneTolerance = 100 }, 1, player.motor);
            Step(2); Check(hurt.Receive(build.ModifyHit(BaseHit(player.airDive), boss.GetComponent<CharacterMotor>()), 1, player.motor), "Upgraded hit works after a Totem's radial wave physically reaches the boss");
            boss.Damage(10000); player.ResetCombo(); player.motor.ResetForStage(flow.CurrentStage.playerEntryPoint); Step(2); Check(flow.ExitUnlocked, "Boss clear waits for exit without offering cards");
            player.motor.ResetForStage(flow.CurrentStage.playerExitPoint); Step(); Check(flow.LevelCompleted && build.Acquired.Count == 5, "Final authored boss exit preserves build at completion"); flow.EnterStage(Stage("Stage08_EscapeLane"));
            player.motor.ResetForStage(flow.CurrentStage.playerExitPoint); Step(); Check(build.Acquired.Count == 5, "Escape Lane preserves acquired build in authored progression");
            flow.Restart(true); Check(build.Acquired.Count == 0 && player.health.EffectiveMaximum == player.health.maximumHealth, "TEST9: new run clears upgrades and restores base max HP");
            EffectTests(); ChoiceTests(); InputTests();
            foreach (var path in attackPaths) Check(File.ReadAllText(path) == snapshots[path], "Base AttackData file unchanged: " + Path.GetFileName(path));
            if (target) UnityEngine.Object.DestroyImmediate(target);
            flow.Restart(true); rewards.Offer();
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/RunUpgradeValidationResults.txt", results);
            capturing = true; captureDue = EditorApplication.timeSinceStartup + 2;
        }
        catch (Exception ex)
        {
            rewards?.CloseChoice(); results.Add("FAIL: " + ex); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/RunUpgradeValidationResults.txt", results); Debug.LogException(ex);
            if (AutoExit) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
        }
    }
    static void EffectTests()
    {
        // Hold stage scheduling independent of the modifier fixtures.
        flow.EnterStage(Stage("Stage07_WhiteGhostBossChamber"));
        Neutral(); Give("Finisher"); Near(TunedDamage(player.groundCombo[2]), BaseHit(player.groundCombo[2]).damage * 1.3f, "Ground finisher modifier");
        Neutral(); Give("HigherLauncherDamage"); Near(TunedDamage(player.launcher), BaseHit(player.launcher).damage * 1.25f, "Launcher modifier");
        Neutral(); Give("JuggleMaster"); Near(TunedDamage(player.groundCombo[0], true), BaseHit(player.groundCombo[0]).damage * 1.2f, "Airborne-target modifier");
        Neutral(); Give("AirFinisher"); player.motor.Launch(4, 0); Near(TunedDamage(player.airCombo[2], true), BaseHit(player.airCombo[2]).damage * 1.25f, "Air finisher damage modifier");
        Check(build.ModifyHit(BaseHit(player.airCombo[2]), target.GetComponent<CharacterMotor>()).launchVelocity.y < BaseHit(player.airCombo[2]).launchVelocity.y, "Air finisher strengthens downward slam");
        Neutral(); Give("ComboMomentum"); target.GetComponent<CharacterMotor>().ResetForStage(new Vector2(8, 0)); player.RequestAttack(); Step(player.groundCombo[0].frames.FindIndex(f => f.canCancelIntoAttack)); player.RequestAttack();
        Check(player.CurrentAttack == player.groundCombo[1], "Combo momentum fixture reaches second punch"); Near(build.ModifyHit(BaseHit(player.groundCombo[1]), null).damage, BaseHit(player.groundCombo[1]).damage * 1.1f, "Combo momentum adds damage per authored combo step");
        Neutral(); Give("PerfectTiming"); Give("GhostBreaker"); Give("Counterattack");
        player.RequestGuard(true); Step(player.defenseData.parryWindowFrames + 1);
        var incoming = new AttackHitboxData { damage = 10 }; var playerHurt = player.GetComponentInChildren<CombatHurtbox>();
        Check(playerHurt.Receive(incoming, -1, target.GetComponent<CharacterMotor>()) && playerHurt.LastHitOutcome == CombatHitOutcome.Parry, "Extended parry accepts a hit outside the base window");
        Check(target.GetComponent<EnemyHitReaction>().RecoveryFrames == player.defenseData.parryAttackerStunFrames + 12, "Ghost Breaker extends attacker punish stun");
        player.RequestGuard(false); Step(player.defenseData.parryRecoveryFrames + 2); player.RequestAttack();
        Check(player.CurrentAttack == player.groundCombo[0], "Parry recovery permits next punch: " + player.State + " / frozen:" + player.attackPlayer.IsFrozen);
        Near(build.ModifyHit(BaseHit(player.groundCombo[0]), null).damage, BaseHit(player.groundCombo[0]).damage * 1.5f, "Counterattack buffs the next attack after actual parry");
        player.attackPlayer.Stop(); player.attackPlayer.Play(player.groundCombo[0]); Near(build.ModifyHit(BaseHit(player.groundCombo[0]), null).damage, BaseHit(player.groundCombo[0]).damage, "Counterattack bonus is consumed once, not every following attack");
        Neutral(); Give("QuickStep"); Give("LongStep"); Give("Afterimage"); float x = player.transform.position.x; Check(player.RequestDodge(), "Upgraded dodge starts"); Step(5);
        hpBeforeDodge = player.health.Current;
        Check(!playerHurt.Receive(incoming, -1, target.GetComponent<CharacterMotor>()) && player.health.Current == hpBeforeDodge, "Dodge through hit is detected without damage");
        Step(12); Check(player.State == CombatState.Idle && player.EffectiveDodgeFrames == 17, "Quick Step shortens only dodge recovery"); Near(Mathf.Abs(player.transform.position.x - x), 1.5f, "Long Step increases motor dodge distance25%");
        player.RequestAttack(); Near(build.ModifyHit(BaseHit(player.groundCombo[0]), null).damage, BaseHit(player.groundCombo[0]).damage * 1.35f, "Afterimage buffs next attack after actual dodge-through");
        Neutral(); Give("StrongGuard"); player.RequestGuard(true); Step(player.EffectiveParryWindow + 1); playerHurt.Receive(new AttackHitboxData { damage = 10, blockstunFrames = 20 }, -1, target.GetComponent<CharacterMotor>());
        Check(player.BlockstunFrames == 15, "Strong Guard reduces incoming blockstun25%");
        Neutral(); Give("IronBody"); Near(player.health.EffectiveMaximum, player.health.maximumHealth + 40, "Iron Body adds runtime max HP"); Near(player.health.Current, player.health.EffectiveMaximum, "Iron Body immediately grants added HP");
        Give("SecondWind"); player.health.Damage(10000); Check(!player.health.IsDead && player.health.Current == 1 && build.LethalSavesUsed == 1, "Second Wind prevents first lethal hit"); player.health.Restore(); player.health.Damage(10000); Check(player.health.IsDead, "Second Wind does not refresh on shrine/retry health restoration");
        Neutral(); Give("DiveReset"); player.motor.Launch(4, 0); player.attackPlayer.Play(player.airDive); int guard = 0; while (!player.motor.IsGrounded && guard++ < 100) Step();
        Step(6); Check(player.IsAirDiving && player.motor.MovementLocked, "Dive Reset still retains punishable grounded recovery"); Step(); Check(!player.CurrentAttack, "Dive Reset shortens landing recovery by exactly3 frames");
        Neutral(); Give("DiveShockwave"); player.motor.Launch(4, 0); player.attackPlayer.Play(player.airDive); var victim = target.GetComponent<CharacterHealth>(); float hp = victim.Current;
        build.AttackHit(victim, CombatHitOutcome.Hit); player.motor.ResetForStage(Vector2.zero); build.DiveImpact(); Near(hp - victim.Current, 8, "Connected dive shockwave uses normal hurtbox damage gates");
        hp = victim.Current; build.DiveImpact(); Near(victim.Current, hp, "Shockwave cannot repeat without another connected dive");
        Neutral(); Give("DiveShockwave"); target.GetComponent<CharacterMotor>().ResetForStage(new Vector2(1, 0)); victim = target.GetComponent<CharacterHealth>(); hp = victim.Current;
        player.RequestJump(); Step(12); player.RequestLauncher(); guard = 0; while (player.CurrentAttack && guard++ < 120) Step();
        Near(hp - victim.Current, 22, "Actual connected dive automatically triggers14-damage head hit plus8-damage landing shockwave");
        Check(build.Acquired.Count == 1, "Effect fixtures do not leak stacks between resets");
    }
    static float hpBeforeDodge;
    static void ChoiceTests()
    {
        Neutral(); Give("HeavyHands"); Give("HeavyHands"); Give("HeavyHands"); Check(!build.Acquire(Card("HeavyHands")), "Max stack count blocks extra acquisition");
        for (int i = 0; i < 50; i++) { var cards = rewards.pool.Generate(build, new System.Random(i)); Check(cards.Count == 3 && cards.Select(u => u.id).Distinct().Count() == 3 && cards.All(u => u.id != "HeavyHands"), "Draw" + i + " respects stacks and unique card IDs"); }
        var a = ScriptableObject.CreateInstance<UpgradeDefinition>(); a.id = "A"; a.displayName = "A"; a.maxStacks = 1;
        var b = ScriptableObject.CreateInstance<UpgradeDefinition>(); b.id = "B"; b.prerequisites.Add(a);
        var c = ScriptableObject.CreateInstance<UpgradeDefinition>(); c.id = "C"; c.incompatibleUpgrades.Add(a);
        build.ResetRun(); Check(!build.CanAcquire(b) && build.CanAcquire(c), "Prerequisites exclude unavailable upgrade"); build.Acquire(a); Check(build.CanAcquire(b) && !build.CanAcquire(c), "Acquisition unlocks prerequisite and excludes incompatible card");
        build.ResetRun(); build.Acquire(c); Check(!build.CanAcquire(a), "Incompatibility is enforced in both directions");
        build.ResetRun(); UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(c);
        var random = new System.Random(77); int common = 0, rare = 0, epic = 0;
        for (int i = 0; i < 10000; i++) { var card = rewards.pool.Generate(build, random, 1)[0]; if (card.rarity == UpgradeRarity.Common) common++; else if (card.rarity == UpgradeRarity.Rare) rare++; else epic++; }
        Check(common > 6700 && common < 7300 && rare > 2200 && rare < 2800 && epic > 350 && epic < 650, "Configured70/25/5 rarity tiers produce expected weighted distribution");
        rewards.pool.upgrades.ForEach(u => { for (int i = 0; i < u.maxStacks; i++) build.Acquire(u); });
        Check(rewards.pool.Generate(build, new System.Random(1)).Count == 0 && !rewards.Offer(), "Exhausted pool returns no invalid/maxed cards and cannot lock pause");
        flow.Restart(true); build.Acquire(Card("HardHead")); flow.Restart(false); Check(build.Stacks(Card("HardHead")) == 1, "Room retry preserves current run build");
    }
    static void InputTests()
    {
        Neutral(); var keyboard = InputSystem.AddDevice<Keyboard>(); var gamepad = InputSystem.AddDevice<Gamepad>();
        var background = InputSystem.settings.backgroundBehavior; var editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        try
        {
            rewards.Offer(); var choice = rewards.Choices[1]; InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2)); InputSystem.Update(); rewards.SendMessage("Update");
            Check(!rewards.IsChoosing && build.Stacks(choice) == 1, "Keyboard2 selects second visible card through UI input");
            rewards.Offer(); choice = rewards.Choices[0]; InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); InputSystem.Update(); rewards.SendMessage("Update");
            Check(!rewards.IsChoosing && build.Stacks(choice) > 0, "Gamepad South selects focused card through UI input");
            rewards.Offer(); Check(!rewards.Choose(-1) && !rewards.Choose(99) && rewards.IsChoosing, "Invalid/double selections cannot skip pause or acquire a card"); rewards.CloseChoice();
        }
        finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(gamepad); InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior; }
    }
}
