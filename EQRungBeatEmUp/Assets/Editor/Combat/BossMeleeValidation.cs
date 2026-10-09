using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BossMeleeValidation
{
    const string Pending = "BossMelee.Validation";
    static readonly List<string> results = new List<string>();
    static readonly List<Object> temporary = new List<Object>();
    static StageFlowController flow; static TotemBossController boss; static EnemyCombat enemy; static ComboController player;
    static CombatClock clock; static BossEncounterData data;
    static BossMeleeValidation() { EditorApplication.update += Poll; EditorApplication.update += FinishParryRegression; }
    [MenuItem("Tools/Combat/Validate Boss Telegraph Melee (Play Mode)")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BossMeleeSetup.Build(); EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    public static void RunParryRegression()
    {
        const string report = "Documentation/ParryResponseValidationResults.txt";
        if (File.Exists(report)) File.Delete(report);
        SessionState.SetBool(Pending + ".ParryRegression", true);
        ParryResponseValidation.Run();
    }
    public static void RunImpactTuning() { BossMeleeSetup.TuneImpact(); Run(); }
    static void FinishParryRegression()
    {
        const string report = "Documentation/ParryResponseValidationResults.txt";
        if (!SessionState.GetBool(Pending + ".ParryRegression", false) || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(report)) return;
        SessionState.SetBool(Pending + ".ParryRegression", false);
        if (Application.isBatchMode) EditorApplication.Exit(File.ReadAllText(report).Contains("FAIL:") ? 1 : 0);
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); results.Add("PASS: " + message); }
    static void Step(int frames = 1) { for (int i = 0; i < frames; i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    static void Until(Func<bool> predicate, int frames = 360) { for (int f = 0; f < frames && !predicate(); f++) Step(); Check(predicate(), "Expected boss transition completes within " + frames + " frames"); }
    static CombatHurtbox BossBox => boss.GetComponentInChildren<CombatHurtbox>();
    static CombatHurtbox PlayerBox => player.GetComponentInChildren<CombatHurtbox>();
    static BossActionChoice Melee => data.phase1.First(c => c.action == BossAction.TelegraphMelee);
    static void Fixture(int character = 0, bool close = true)
    {
        for (int i = 0; i < 30 && player.attackPlayer.IsFrozen; i++) Step();
        // Manual combat stepping stays inside one editor update, so Unity's timed
        // Destroy callbacks have not run. Clear prior cosmetic instances for captures.
        foreach (var item in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (item && (item.name == "Combat impact (temporary)" || item.name == "Network combat impact")) Object.DestroyImmediate(item.gameObject);
        flow.enabled = true; flow.EnterStage(flow.level.stages.FindIndex(s => s.stageId == "Stage07_WhiteGhostBossChamber"));
        player.GetComponent<PlayerCharacterLoadout>().Apply(AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath).CharacterAt(character));
        player.ResetCombo(); player.health.maximumHealth = 10000; player.health.Restore(); player.motor.ResetForStage(new Vector2(close ? .95f : 2.5f, 0)); player.motor.Face(-1);
        Step(2); flow.enabled = false; boss = flow.StageEnemies.Select(h => h.GetComponent<TotemBossController>()).First(b => b); enemy = boss.GetComponent<EnemyCombat>();
        data = Object.Instantiate(boss.data); temporary.Add(data); boss.data = data;
        data.moveChance = 0; data.warpOffset = 0; data.warpCooldownFrames = 10000;
        enemy.motor.ResetForStage(Vector2.zero); enemy.target = player.transform; boss.BindEncounter();
        boss.DebugVulnerable(); data.vulnerabilityFrames = 10000; boss.DebugVulnerable();
    }
    static void Begin() { Check(boss.ForceAction(BossAction.TelegraphMelee), "Close-range telegraph can start through existing boss action execution"); }
    static AttackHitboxData PlayerHit => player.groundCombo[0].frames.First(f => f.hitboxes.Count > 0).hitboxes[0].RuntimeCopy();
    static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear(); bool passed = false;
        try
        {
            flow = Object.FindFirstObjectByType<StageFlowController>(); clock = Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false;
            flow.level = Object.Instantiate(flow.level); temporary.Add(flow.level); player = flow.player.GetComponent<ComboController>();
            player.GetComponent<PlayerCombatInput>().enabled = false; player.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled = false;
            WeightedSelection(); foreach (int character in new[] { 0, 1 }) { HitInterrupt(character); ParryAndGuard(character); RetreatAndCommit(character); StrongImpact(character); }
            ShieldsPhasesAndTotems(); ExistingAbilities(); Multiplayer(); passed = true;
        }
        catch (Exception e) { results.Add("FAIL: " + e); Debug.LogException(e); }
        finally
        {
            foreach (var item in temporary) if (item) Object.DestroyImmediate(item); temporary.Clear();
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/BossMeleeValidationResults.txt", results);
            Debug.Log("BOSS MELEE VALIDATION " + (passed ? "PASSED" : "FAILED")); if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.ExitPlaymode();
        }
    }
    static BossAction[] Rolls(int count = 1000) => Enumerable.Range(0, count).Select(i => boss.ChooseAction((i + .5f) / count)?.action ?? (BossAction)(-1)).ToArray();
    static void WeightedSelection()
    {
        Fixture(); var melee = Melee.attack;
        Check(melee.TotalFrames == 112 && melee.FirstActiveFrame == 60 && melee.ActiveFrames == 10 && melee.LastActiveFrame == 69, "New frame timeline is 60 startup / 10 active / 42 recovery");
        Check(data.phase1.Any(c => c.action == BossAction.Swipe) && data.phase2.Any(c => c.action == BossAction.CurseWave) && data.phase2.Any(c => c.action == BossAction.SummonStrongGhosts), "Existing swipe, curse wave and strong-ghost abilities remain authored");
        var close = Rolls(); Check(close.Count(a => a == BossAction.TelegraphMelee) > 600 && close.Contains(BossAction.Book) && close.Contains(BossAction.SummonRusher) && close.Contains(BossAction.Teleport) && close.Contains(BossAction.Swipe), "Close weights prioritize new melee while all existing ability families remain selectable");
        player.motor.ResetForStage(new Vector2(2.5f, 0)); var far = Rolls();
        Check(!far.Contains(BossAction.TelegraphMelee) && far.Count(a => a == BossAction.Book) == 550 && far.Count(a => a == BossAction.SummonRusher) == 300 && far.Count(a => a == BossAction.Teleport) == 150, "Far eligible weights normalize to 55 projectile / 30 summon / 15 teleport");
        boss.ForcePhase2(); far = Rolls(); Check(far.Contains(BossAction.CurseWave) && far.Contains(BossAction.SummonStrongGhosts) && far.Count(a => a == BossAction.Book || a == BossAction.CurseWave) == 550, "Phase 2 retains both ranged/summon variants and shared distance proportions");
        foreach (var choice in data.phase2) choice.enabled = choice.action == BossAction.Book;
        Check(Rolls().All(a => a == BossAction.Book), "Remaining available weights renormalize to 100 percent");
        data.phase2.First(c => c.action == BossAction.Book).weight = 0; Check(boss.ChooseAction(.3f) == null, "No eligible action returns neutral recovery instead of forcing unavailable ability");
        Fixture(); Begin(); Check(boss.CooldownRemaining(Melee) == 150 && !boss.ActionAvailable(Melee), "Melee consumes its editable AttackData cooldown on commitment");
        var cooldown = boss.CooldownRemaining(Melee); CombatClock.SetPaused(boss, true); Step(10); CombatClock.SetPaused(boss, false); Check(boss.CooldownRemaining(Melee) == cooldown, "Pause does not consume action cooldown");
        enemy.attackPlayer.Freeze(4); Step(4); Check(boss.CooldownRemaining(Melee) == cooldown, "Hitstop does not consume action cooldown");
        player.motor.ResetForStage(new Vector2(.95f, 1)); Check(!boss.ActionAvailable(data.phase1.First(c => c.action == BossAction.Swipe)), "Wrong walking lane excludes physical attacks");
    }
    static void HitInterrupt(int character)
    {
        Fixture(character); Begin(); float hp = player.health.Current;
        Step(20); Check(enemy.attackPlayer.CurrentFrame == 20 && player.health.Current == hp && enemy.attackPlayer.Frame.hitboxes.Count == 0, "Character " + (character + 1) + " receives no startup damage");
        Check(boss.GetComponent<AttackFeedback>().WarningVisual && boss.GetComponent<AttackFeedback>().WarningVisual.enabled, "Startup displays existing ground-area telegraph and assigned cue"); Capture("Telegraph");
        var hit = PlayerHit; Check(BossBox.Receive(hit, 1, player.motor) && boss.State == BossEncounterState.Stagger && boss.StaggerRemaining == 30 && enemy.attackPlayer.CurrentAttack == null, "Character " + (character + 1) + " accepted startup hit cancels attack into 30-frame punish window");
        var pose = enemy.motor.sprite.sprite; Check(data.meleeInterrupt.hitReaction.frames.Any(f => f.sprite == pose), "Interrupted pose comes from authored existing-art reaction timeline"); Capture("Interrupted");
        BossBox.Receive(hit, 1, player.motor); Check(boss.StaggerRemaining == 30 && boss.PhysicalInterrupts == 1, "Repeated hits deal damage without restarting stagger");
        Step(29); Check(boss.State == BossEncounterState.Stagger && boss.StaggerRemaining == 1, "Hit stagger retains exact configured duration"); Step(); Check(boss.State == BossEncounterState.Recovery, "Hit stagger exits cleanly through existing boss recovery");
        enemy.attackPlayer.Stop(); player.motor.ResetForStage(new Vector2(.95f, 0));
        Until(() => boss.CooldownRemaining(Melee) == 0, 180); Begin(); BossBox.Receive(hit, 1, player.motor);
        Check(boss.State == BossEncounterState.Acting && enemy.attackPlayer.CurrentAttack == Melee.attack && boss.InterruptImmunityRemaining > 0, "Interrupt lockout prevents repeated startup stunlock while still accepting damage");
        Fixture(character); Begin(); enemy.attackPlayer.SeekFrame(data.meleeInterrupt.lastFrame + 1); BossBox.Receive(PlayerHit, 1, player.motor); Check(boss.State == BossEncounterState.Acting && enemy.attackPlayer.CurrentAttack, "Startup outside configured interrupt window is committed");
        player.motor.ResetForStage(new Vector2(-2, 0)); enemy.attackPlayer.SeekFrame(Melee.attack.FirstActiveFrame); float bossHp = enemy.reaction.health.Current; BossBox.Receive(PlayerHit, 1, player.motor);
        Check(enemy.reaction.health.Current < bossHp && boss.State == BossEncounterState.Acting && enemy.attackPlayer.CurrentAttack, "Active attack takes legitimate damage but normal hits cannot cancel it"); Capture("Active");
        enemy.attackPlayer.SeekFrame(Melee.attack.LastActiveFrame + 1); BossBox.Receive(PlayerHit, 1, player.motor); Check(boss.State == BossEncounterState.Acting && enemy.attackPlayer.CurrentAttack, "Recovery remains damageable without being reset by normal hits");
        Until(() => enemy.attackPlayer.CurrentAttack == null, 100); Step(); Check(boss.State == BossEncounterState.Recovery, "Committed attack finishes without stuck active/recovery state");
        Fixture(character); data.meleeInterrupt.hitTypes = BossInterruptHitTypes.KnockDown; Begin(); BossBox.Receive(PlayerHit, 1, player.motor);
        Check(boss.State == BossEncounterState.Acting, "Inspector interrupt hit-type conditions reject disallowed normal hits");
        Fixture(character); data.warpCooldownFrames = 30; player.motor.ResetForStage(new Vector2(.7f, 0)); Begin(); player.RequestAttack();
        Until(() => boss.State == BossEncounterState.Stagger, 30);
        Check(boss.PhysicalInterrupts == 1 && player.CurrentAttack, "Character " + (character + 1) + " real combo hitbox interrupts telegraph without cancelling player attack");
        Until(() => boss.State == BossEncounterState.Recovery, 80); Step(30);
        Check(boss.State == BossEncounterState.SelectAction, "Punish recovery returns naturally to weighted action selection");
        Step(); Check(boss.State != BossEncounterState.SelectAction && boss.State != BossEncounterState.Stagger, "Boss resumes an eligible ability rather than becoming stuck in recovery");
    }
    static void ParryAndGuard(int character)
    {
        Fixture(character); int parriesBefore = player.GetComponent<AttackFeedback>()?.ParryCount ?? 0; Begin(); enemy.attackPlayer.SeekFrame(Melee.attack.FirstActiveFrame - 1); player.RequestGuard(true); float hp = player.health.Current; Step();
        Check(player.State == CombatState.Parry && player.health.Current == hp && boss.State == BossEncounterState.Stagger && boss.StaggerRemaining == 60 && boss.PhysicalParries == 1 && !enemy.attackPlayer.CurrentAttack, "Character " + (character + 1) + " perfect parry negates damage and cancels into 60-frame boss stagger");
        Check(player.GetComponent<AttackFeedback>().ParryCount == parriesBefore + 1 && player.defenseData.parryFeedback.impactSound && player.defenseData.parryFeedback.impactPrefab, "Perfect parry reuses existing VFX/SFX and feedback event");
        int frozen = enemy.attackPlayer.HitstopRemaining; Check(frozen > 0 && frozen == player.attackPlayer.HitstopRemaining, "Both attacker and defender receive existing perfect-parry hitstop"); Capture("Parried");
        Step(frozen); Check(boss.StaggerRemaining == 60, "Perfect-parry freeze preserves full punish duration"); Step(59); Check(boss.StaggerRemaining == 1, "Parry punish timer cannot expire early"); Step(); Check(boss.State == BossEncounterState.Recovery, "Parry stagger exits through normal boss recovery");
        Fixture(character); Begin(); player.RequestGuard(true); Step(player.EffectiveParryWindow + 1); enemy.attackPlayer.SeekFrame(Melee.attack.FirstActiveFrame - 1); hp = player.health.Current; Step();
        Check(PlayerBox.LastHitOutcome == CombatHitOutcome.Block && boss.State == BossEncounterState.Acting && enemy.attackPlayer.CurrentAttack && boss.PhysicalParries == 0 && player.health.Current == hp, "Character " + (character + 1) + " ordinary guard blocks without interrupting boss");
        Fixture(character); Check(boss.ForceAction(BossAction.Swipe), "Original swipe stays available at close range"); enemy.attackPlayer.SeekFrame(enemy.attackPlayer.CurrentAttack.FirstActiveFrame - 1); player.RequestGuard(true); Step();
        Check(boss.State == BossEncounterState.Stagger && boss.PhysicalParries == 1, "Perfect parry also cancels original physical swipe");
        Fixture(character); boss.ForceAction(BossAction.Book); int frame = enemy.attackPlayer.CurrentFrame;
        Check(!boss.InterruptPhysicalFromParry() && enemy.attackPlayer.CurrentAttack && enemy.attackPlayer.CurrentFrame == frame, "Physical-parry hook cannot cancel supernatural projectile cast");
    }
    static void RetreatAndCommit(int character)
    {
        Fixture(character); Begin(); Step(12); player.motor.ResetForStage(new Vector2(-.95f, 0)); Step(4);
        Check(enemy.attackPlayer.Facing == 1 && enemy.motor.Facing == 1, "Character " + (character + 1) + " crossing behind after facing-lock cannot rotate committed swing");
        player.motor.ResetForStage(new Vector2(2.6f, 0)); float hp = player.health.Current; Until(() => enemy.attackPlayer.CurrentFrame > Melee.attack.LastActiveFrame, 80);
        Check(player.health.Current == hp && boss.State == BossEncounterState.Acting, "Retreat out of melee range avoids damage while boss completes commitment");
        Fixture(character); Begin(); enemy.attackPlayer.SeekFrame(Melee.attack.FirstActiveFrame - 5); player.motor.MoveInput = Vector2.right; Check(player.RequestDodge(), "Existing dodge accepts retreat input before its four-frame invulnerability startup"); hp = player.health.Current; Step(8);
        Check(player.health.Current == hp && boss.State == BossEncounterState.Acting, "Character " + (character + 1) + " existing dodge avoids committed physical hit");
        Fixture(character); Begin(); hp = player.health.Current; Until(() => player.health.Current < hp, 60);
        Check(player.health.Current <= hp - 36 && player.State == CombatState.Hitstun && boss.State == BossEncounterState.Acting, "Ignoring telegraph causes powerful physical hit and clear existing hit reaction");
    }
    static void ShieldsPhasesAndTotems()
    {
        Fixture(); boss.DebugInvulnerable(); Begin(); float hp = enemy.reaction.health.Current;
        Check(!BossBox.Receive(PlayerHit, 1, player.motor) && enemy.reaction.health.Current == hp && boss.State == BossEncounterState.Acting, "Totem shield still rejects hit-based startup interruption and damage");
        enemy.attackPlayer.SeekFrame(Melee.attack.FirstActiveFrame - 1); player.RequestGuard(true); Step();
        Check(boss.State == BossEncounterState.Stagger && boss.Invulnerable && enemy.reaction.health.BossDamageProtection && boss.VulnerabilityRemaining == 0 && !BossBox.Receive(PlayerHit, 1, player.motor), "Shielded physical parry cancels swing without opening damage gate or changing phase");
        Check(!boss.Phase2 && boss.PhaseTransitions == 0, "Parry alone cannot advance boss phase");
        Fixture(); var totem = boss.Totems[0]; enemy.motor.ResetForStage((Vector2)totem.transform.position + Vector2.right * .3f); boss.DebugInvulnerable(); data.vulnerabilityFrames = 420;
        totem.DebugBreak(player.motor); Check(boss.Invulnerable, "Totem break retains its expanding-front delay"); Until(() => !boss.Invulnerable, 60);
        Check(boss.VulnerabilityRemaining == 420, "Existing radial totem front opens original configurable vulnerability duration");
        Fixture(); Begin(); var thresholdHit = PlayerHit; thresholdHit.damage = enemy.reaction.health.Current * .55f;
        BossBox.Receive(thresholdHit, 1, player.motor); Check(boss.Phase2 && boss.PhaseTransitions == 1 && boss.State == BossEncounterState.Stagger, "Legitimate health threshold queues existing phase transition without discarding punish window");
        Step(30); Step(); Check(boss.State == BossEncounterState.PhaseTransition, "Phase transition begins after interrupted physical punish finishes");
        Step(data.phaseTransitionFrames); Check(boss.Phase2 && boss.PhaseTransitions == 1 && boss.State == BossEncounterState.SelectAction, "Existing phase transition completes exactly once");
        Fixture(); Begin(); boss.GetComponent<CharacterHealth>().Damage(100000); Check(boss.State == BossEncounterState.Dead && !enemy.attackPlayer.CurrentAttack, "Death cancels new melee and retains existing cleanup");
    }
    static void StrongImpact(int character)
    {
        foreach (int side in new[] { 1, -1 })
        {
            Fixture(character); player.motor.ResetForStage(new Vector2(side * .95f, 0)); player.motor.Face(-side); Begin();
            var feedback = boss.GetComponent<AttackFeedback>(); int shakes = flow.framing.ImpactShakeCount; float hp = player.health.Current;
            Step(Melee.attack.FirstActiveFrame - 1);
            Check(player.health.Current == hp && feedback.ImpactCount == 0 && !feedback.LastImpact && flow.framing.ImpactShakeCount == shakes, "Character " + (character + 1) + " side " + side + ": full one-second telegraph is harmless and has no early impact effect/shake");
            var soundsBeforeImpact = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).ToHashSet(); Step();
            Check(player.health.Current == hp - 36 && feedback.ImpactCount == 1 && feedback.LastImpact, "Sweep deals 36 damage and spawns shared pixel burst exactly on connection");
            Check(feedback.LastImpact.GetComponentInChildren<Animator>() && feedback.LastImpact.GetComponentInChildren<SpriteRenderer>().transform.position.y > .5f, "Shared animated impact burst is raised to torso height for physical swipe readability");
            Check(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(s => !soundsBeforeImpact.Contains(s) && s.clip == Melee.attack.feedback.impactSound), "Heavy impact sound starts on confirmed hit");
            Check(flow.framing.ImpactShakeCount == shakes + 1, "Small camera shake occurs on confirmed hit");
            Check(player.attackPlayer.HitstopRemaining == 10 && enemy.attackPlayer.HitstopRemaining == 10 && player.motor.HorizontalRecoil * side >= 4.8f, "Ten-frame impact freeze and strong recoil apply to both facing directions");
            Capture("HeavyImpact"); float contactX = player.motor.transform.position.x; Step(10);
            Check(Mathf.Abs(player.motor.transform.position.x - contactX) < .001f, "Hitstop holds displacement until impact freeze ends");
            Step(20);
            Check((player.motor.transform.position.x - contactX) * side > .9f && player.motor.transform.position.x >= player.motor.arenaMin.x && player.motor.transform.position.x <= player.motor.arenaMax.x, "Strong push moves player more than 0.9 units after freeze while preserving stage bounds");
            Check(player.health.Current == hp - 36 && feedback.ImpactCount == 1, "Ten active frames preserve one damage/impact event per swing");
        }
    }
    static void ExistingAbilities()
    {
        Fixture(0, false); int releases = boss.GetComponent<EnemyProjectileAttack>().ProjectilesReleased;
        Check(boss.ForceAction(BossAction.Book), "Existing Book projectile remains usable at range"); Until(() => boss.GetComponent<EnemyProjectileAttack>().ProjectilesReleased > releases);
        Check(Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).Any(p => p.name.Contains("BossBookProjectile")), "Existing projectile spawning path creates actual book projectile");
        Fixture(0, false); boss.ForcePhase2(); releases = boss.GetComponent<EnemyProjectileAttack>().ProjectilesReleased;
        Check(boss.ForceAction(BossAction.CurseWave), "Existing phase-2 Curse Wave remains usable"); Until(() => boss.GetComponent<EnemyProjectileAttack>().ProjectilesReleased > releases);
        Check(Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).Any(p => p.groundWave), "Existing curse wave keeps ground-wave projectile behavior");
        Fixture(0, false); Check(boss.ForceAction(BossAction.SummonRusher), "Existing Rusher summon remains usable"); Until(() => boss.ActiveMinions > 0);
        Check(boss.ActiveMinions == 1 && !boss.ActionAvailable(data.phase1.First(c => c.action == BossAction.SummonRusher)), "Existing minion spawning is tracked and respects shared summoning cooldown");
        data.maxBossMinions = boss.ActiveMinions; Step(300); Check(!boss.ActionAvailable(data.phase1.First(c => c.action == BossAction.SummonRusher)), "Boss minion cap still excludes summoning after cooldown expires");
        Fixture(0, false); boss.ForcePhase2(); Check(boss.ForceAction(BossAction.SummonStrongGhosts), "Existing phase-2 strong-ghost summon remains usable"); Until(() => boss.ActiveMinions > 0);
        Check(flow.StageEnemies.Any(h => h.name.Contains("Grappler")) && flow.StageEnemies.Any(h => h.name.Contains("Thrower")), "Original strong-ghost summoning still creates Grappler and Thrower through stage spawning");
        Fixture(); int warps = boss.WarpsPerformed, previous = boss.LastWarpIndex;
        Check(boss.ForceAction(BossAction.Teleport), "Weighted teleport invokes existing warp behavior"); Until(() => boss.WarpsPerformed > warps);
        Check(boss.LastWarpIndex != previous && boss.transform.position.x >= enemy.motor.arenaMin.x && boss.transform.position.x <= enemy.motor.arenaMax.x && boss.CooldownRemaining(data.phase1.First(c => c.action == BossAction.Teleport)) > 0, "Warp avoids previous point, respects arena and consumes teleport cooldown");
        Until(() => enemy.motor.sprite.enabled, 60); Check(enemy.motor.sprite.enabled, "Boss reappears through original warp-in sequence");
    }
    static void Multiplayer()
    {
        Fixture(); var other = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2.prefab")); temporary.Add(other);
        other.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled = false; other.GetComponent<PlayerCombatInput>().enabled = false;
        var first = player.gameObject.AddComponent<PlayerIdentity>(); temporary.Add(first); first.slot = 0;
        var second = other.AddComponent<PlayerIdentity>(); second.slot = 1; var motor = other.GetComponent<CharacterMotor>(); motor.ResetForStage(new Vector2(-.8f, 0));
        player.motor.ResetForStage(new Vector2(2.5f, 0)); Step(); Check(enemy.target == motor.transform, "Multiplayer boss targets nearest living player using existing roster");
        Begin(); enemy.attackPlayer.SeekFrame(20); Check(BossBox.Receive(PlayerHit, 1, motor) && boss.State == BossEncounterState.Stagger, "Second playable character/player can interrupt authoritative telegraph");
        var catalog = AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        Check(catalog.AttackId(Melee.attack) >= 0 && catalog.AttackId(data.meleeInterrupt.interruptFeedback) >= 0, "Melee and interrupt feedback have stable network catalog indices");
        var source = enemy.motor.sprite; var transmitted = new SpriteState { sprite = catalog.SpriteId(source.sprite), position = source.transform.position, color = source.color, order = source.sortingOrder };
        var received = JsonUtility.FromJson<SpriteState>(JsonUtility.ToJson(transmitted)); Check(catalog.SpriteAt(received.sprite) == source.sprite && received.position == source.transform.position, "Existing sprite snapshots carry reused boss reaction pose and position");
        var state = new EntityState { kind = "Boss", state = boss.State.ToString(), action = boss.Selected?.action.ToString() };
        Check(JsonUtility.FromJson<EntityState>(JsonUtility.ToJson(state)).state == "Stagger", "Existing entity snapshots represent new boss stagger without separate client AI");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Documentation/BossMeleePreview"); var camera = flow.framing.GetComponent<Camera>();
        flow.framing.followEnabled = false; flow.framing.ApplyFraming(0, true); var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target; var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply(); File.WriteAllBytes("Documentation/BossMeleePreview/" + name + ".png", texture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(texture); Object.DestroyImmediate(target);
    }
}
