using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace BeatEmUp
{
    public sealed partial class StageFlowController : MonoBehaviour, ICombatFrameListener
    {
        public LevelDefinition level;
        public CharacterMotor player;
        public StageFraming framing;
        public SpriteRenderer background, floor;
        public AudioSource ambience;
        public bool showHud = true;
        public UnityEvent onLevelCompleted = new UnityEvent();
        public RunUpgradeController RunUpgrades => GetComponent<RunUpgradeController>();
        public RewardSelectionController WorldRewards => GetComponent<RewardSelectionController>();
        public CoopRewards CoopRewards => GetComponent<CoopRewards>();
        public IEnumerable<CharacterMotor> Players => PlayerRoster.Motors(player);
        public IEnumerable<CharacterMotor> LivingPlayers => Players.Where(p=>p && p.gameObject.activeInHierarchy && !p.GetComponent<CharacterHealth>().IsDead);
        public int StageIndex { get; private set; } = -1;
        public StageSegmentDefinition CurrentStage => level && StageIndex >= 0 && StageIndex < level.stages.Count ? level.stages[StageIndex] : !Application.isPlaying && level && level.stages.Count > 0 ? level.stages[0] : null;
        public float StageElapsed { get; private set; }
        public bool LevelCompleted { get; private set; }
        public string Failure { get; private set; }
        public int FrameOrder => 90;
        public IReadOnlyList<DestructibleObject> Destructibles => destructibles;
        public IEnumerable<CharacterHealth> StageEnemies => encounters.SelectMany(e => e.waves).SelectMany(w => w.enemies).Where(h => h);
        public IEnumerable<CharacterHealth> LivingEnemies => StageEnemies.Where(h => h.gameObject.activeInHierarchy && !h.IsDead);
        public int RemainingTotems => destructibles.Count(p => p && p.kind == PropKind.CursedTotem && !p.IsBroken);
        public bool EncountersComplete => encounters.All(e => !e.definition.requiredForCompletion || EncounterSatisfied(e));
        public string ActiveEncounterName => encounters.FirstOrDefault(e => e.definition.enabled && e.started && !e.completed)?.definition.encounterId;
        public Rect? ActiveCameraBounds => encounters.FirstOrDefault(e => e.definition.enabled && e.started && !e.completed && e.definition.useCombatBounds && e.definition.lockCamera)?.definition.cameraBounds;
        private bool HasAuthority => !MultiplayerSession.Active || MultiplayerSession.Active.IsAuthority;
        public bool ExitUnlocked => CompletionSatisfied &&
            (CurrentStage.rewardAfterClear != StageReward.UpgradeChoice || rewardedStages.Contains(RewardKey)) &&
            !(RunUpgrades && RunUpgrades.IsChoosing) && !(WorldRewards && WorldRewards.IsPending) && !(CoopRewards && CoopRewards.Pending);
        public bool CompletionSatisfied => CurrentStage != null && !LevelCompleted && string.IsNullOrEmpty(Failure) &&
            !encounters.Any(e => e.definition.enabled && e.started && !e.completed && e.definition.lockStageUntilClear) &&
            (CurrentStage.completionMode == StageCompletion.ReachExit && EncountersComplete ||
             CurrentStage.completionMode == StageCompletion.ClearEncounters && EncountersComplete ||
             CurrentStage.completionMode == StageCompletion.BossDefeated && EncountersComplete && (bossSpawned || BossRequirementSkipped) && !LivingEnemies.Any(h => h.GetComponent<TotemBossController>()) ||
             CurrentStage.completionMode == StageCompletion.Event && stageEventComplete);
        private sealed class Plan { public EnemySpawnDefinition definition; public int spawned; public float due; }
        private sealed class WaveState { public WaveDefinition definition; public bool started, completed, signalled; public float elapsed, clearedAt; public readonly List<Plan> plans = new List<Plan>(); public readonly List<CharacterHealth> enemies = new List<CharacterHealth>(); }
        private sealed class EncounterState { public EnemyAttackCoordinator coordinator; public EncounterDefinition definition; public bool started, completed, signalled, clearSignalled, clearedOnce, simulationSkipped; public float elapsed, eligibleAt = -1; public readonly List<WaveState> waves = new List<WaveState>(); public readonly Dictionary<GameObject, bool> objectOriginals = new Dictionary<GameObject, bool>(); }
        private readonly List<EncounterState> encounters = new List<EncounterState>();
        private readonly List<DestructibleObject> destructibles = new List<DestructibleObject>();
        private readonly Dictionary<CharacterMotor, Vector2> previousPlayerPositions = new Dictionary<CharacterMotor, Vector2>();
        private GameObject room;
        public Transform SpawnedAttackRoot => room ? room.transform : transform;
        private bool stageEventComplete, bossSpawned, debugVisible;
        MenuNavigationInput debugInput;
        readonly ImmediateMenuNavigation debugNavigation=new ImmediateMenuNavigation();
        void OnDestroy() { debugInput?.Dispose(); }
        private float transitionFlash;
        private readonly HashSet<string> rewardedStages = new HashSet<string>();
        private bool rewardStarted;
        private string RewardKey => string.IsNullOrEmpty(CurrentStage.stageId) ? StageIndex.ToString() : CurrentStage.stageId;
        private void OnEnable() => CombatClock.Register(this);
        private void OnDisable() { HideExitMarkers(); CombatClock.Unregister(this); foreach(var encounter in encounters) encounter.coordinator?.Clear(); RestoreActiveEncounterObjects(); foreach(var actor in Players) if(actor) actor.GetComponent<CharacterHealth>().SafeStageProtection = false; }
        private void Start() 
        { if(StageIndex>=0) return; if (level && player) Restart(true); else Failure = "Assign a LevelDefinition and player."; }
        // Stage asset edits during Play update the artwork without restarting encounters.
        private void LateUpdate() 
        {
            if (CurrentStage != null) 
            {
                ApplyStageArt(CurrentStage);
                if (Application.isPlaying && HasAuthority) ApplyEncounterLocks();
            } 
        }
        public void EnterStage(int index)
        {
            if (!HasAuthority) return;
            if (!level || !player || index < 0 || index >= level.stages.Count) 
            { 
                return;
            }
            
            HideExitMarkers();
            if (room) 
            { 
                room.SetActive(false); 
                Destroy(room); 
            }

            WorldRewards?.Cancel(); CoopRewards?.Cancel(); RunUpgrades?.CloseChoice();
            RestoreActiveEncounterObjects();
            StageIndex = index; StageElapsed = 0; LevelCompleted = false; Failure = null;
            stageEventComplete = bossSpawned = false; encounters.Clear(); destructibles.Clear();
            rewardStarted = false;
            RunUpgrades?.Initialize(); RunUpgrades?.Build?.ClearTransient();
            var stage = CurrentStage;
            if(stage.hub) stage.playerEntryPoint=stage.hub.spawn;
            player.GetComponent<CharacterHealth>().SafeStageProtection = stage.IsSafeStage;
            room = new GameObject("Stage runtime — " + stage.stageName); 
            room.transform.SetParent(transform, false);
            player.GetComponent<ComboController>()?.ResetCombo(); 
            player.ResetForStage(stage.playerEntryPoint); 
            player.Face(1);
            player.arenaMin = stage.movementMin; 
            player.arenaMax = stage.movementMax;
            foreach(var actor in Players.Where(p=>p!=player))
            {
                var identity=actor.GetComponent<PlayerIdentity>();
                actor.GetComponent<ComboController>()?.ResetCombo();
                actor.ResetForStage(stage.playerEntryPoint+Vector2.right*(identity ? identity.slot*.35f : .35f));
                actor.Face(1); actor.arenaMin=stage.movementMin; actor.arenaMax=stage.movementMax;
                actor.GetComponent<CharacterHealth>().SafeStageProtection=stage.IsSafeStage;
                actor.GetComponent<RunBuildState>()?.ClearTransient();
            }
            
            if (framing)
            {
                framing.SetEncounterBounds(null);
                framing.floor = null; framing.player = player; framing.bottomLane = stage.movementMin.y; framing.topLane = stage.movementMax.y;
                framing.SetStageBounds(-stage.artWidth * .5f, stage.artWidth * .5f);
                framing.transform.position = new Vector3(0, framing.verticalCenter, -10); framing.ApplyFraming(0, true);
            }

            ApplyStageArt(stage);
            if (!stage.backgroundSprite || !stage.floorSprite) 
            { 
                Failure = "Stage art is missing: " + stage.stageName; 
            }
            
            if (ambience) 
            { 
                ambience.Stop(); 
                ambience.clip = stage.ambience; 
                ambience.loop = true;
                if (stage.ambience) 
                {
                    ambience.Play();
                } 
            }

            foreach (var p in stage.decorativeProps)
            {
                if (p.prefab && (!stage.IsSafeStage || (!p.prefab.GetComponentInChildren<EnemyCombat>(true) && !p.prefab.GetComponentInChildren<EnemyHitReaction>(true) && !p.prefab.GetComponentInChildren<DestructibleObject>(true))))
                {
                    Instantiate(p.prefab, p.position, Quaternion.identity, room.transform);
                }
            }

            foreach (var p in stage.IsSafeStage ? Enumerable.Empty<DestructiblePlacement>() : stage.destructibles)
            {
                var go = p.prefab ? Instantiate(p.prefab, p.position, Quaternion.identity, room.transform) : new GameObject(p.label);
                go.transform.SetParent(room.transform); 
                go.transform.position = p.position;
                var prop = go.GetComponent<DestructibleObject>();
                if (!prop) 
                { 
                    prop = go.AddComponent<DestructibleObject>(); 
                }
                prop.Configure(p); destructibles.Add(prop);
            }
            foreach (var e in stage.IsSafeStage ? Enumerable.Empty<EncounterDefinition>() : stage.encounters)
            {
                var coordinatorObject=new GameObject("Attack coordinator — "+e.encounterId); coordinatorObject.transform.SetParent(room.transform,false);
                var coordinator=coordinatorObject.AddComponent<EnemyAttackCoordinator>(); coordinator.settings=e.attackCoordination ?? new AttackCoordinationSettings(); coordinator.encounterId=e.encounterId;
                var state = new EncounterState { definition = e, coordinator=coordinator };
                foreach (var w in e.waves) 
                { 
                    state.waves.Add(new WaveState { definition = w }); 
                }
                encounters.Add(state);
            }
            transitionFlash = .2f;
            previousPlayerPositions.Clear(); foreach (var actor in Players) previousPlayerPositions[actor] = actor.transform.position;
            CreateExitMarkers();
        }
        public void ApplyStageArt(StageSegmentDefinition stage)
        {
            if (stage == null) 
            { 
                return; 
            }

            if(background) background.enabled=!stage.hub;
            if(floor) floor.enabled=!stage.hub;
            if(stage.hub) { if(framing) framing.SetStageBounds(-stage.hub.width*.5f,stage.hub.width*.5f); return; }
            ApplyPlate(background, stage.backgroundSprite, stage.artWidth, stage.backgroundHeight, stage.backgroundCenterY, -1000);
            ApplyPlate(floor, stage.floorSprite, stage.artWidth, stage.floorHeight, stage.floorCenterY, -900);
            
            if (framing && stage == CurrentStage)
            {
                // Use actual renderer edges (including any authored pivot offset).
                float left = background && background.sprite ? background.bounds.min.x : -stage.artWidth * .5f;
                float right = background && background.sprite ? background.bounds.max.x : stage.artWidth * .5f;

                if (floor && floor.sprite) 
                { 
                    left = Mathf.Max(left, floor.bounds.min.x); 
                    right = Mathf.Min(right, floor.bounds.max.x); 
                }

                framing.SetStageBounds(left, right);
            }
        }
        private static void ApplyPlate(SpriteRenderer renderer, Sprite sprite, float width, float height, float y, int order)
        {
            if (!renderer) 
            { 
                return; 
            }

            renderer.sprite = sprite; 
            renderer.sortingOrder = order;
            renderer.transform.position = new Vector3(0, y, 0);
            if (sprite) renderer.transform.localScale = new Vector3(Mathf.Max(.01f, width) / Mathf.Max(.0001f, sprite.bounds.size.x), Mathf.Max(.01f, height) / Mathf.Max(.0001f, sprite.bounds.size.y), 1);
        }
        
        public void CombatFrame() => Tick(CombatClock.FrameSeconds);
        
        public void Tick(float dt)
        {
            if (!HasAuthority || CombatClock.IsPaused || CurrentStage == null || LevelCompleted || !player || !LivingPlayers.Any() || !string.IsNullOrEmpty(Failure)) return;
            if (WorldRewards && WorldRewards.IsPending) return;
            if (CoopRewards && CoopRewards.Pending) return;
            dt = Mathf.Max(0, dt); StageElapsed += dt;
            for (int i = 0; i < encounters.Count; i++)
            {
                var e = encounters[i];
                if (!e.definition.enabled) continue;
                if (e.completed)
                {
                    if (!e.simulationSkipped && !e.definition.oneShot && e.definition.trigger == EncounterTrigger.PlayerZone && !LivingPlayers.Any(p => e.definition.triggerZone.Contains(p.transform.position)))
                    {
                        e.coordinator?.Clear(); e.started = e.completed = e.signalled = e.clearSignalled = false; e.elapsed = 0; e.eligibleAt = -1;
                        foreach (var old in e.waves) foreach (var enemy in old.enemies) if (enemy) Destroy(enemy.gameObject);
                        e.waves.Clear(); foreach (var definition in e.definition.waves) e.waves.Add(new WaveState { definition = definition });
                    }
                    continue;
                }
                if (!e.started)
                {
                    // Combat zones run sequentially; ordinary traversal spawn triggers can overlap.
                    if (e.definition.useCombatBounds && encounters.Any(other => other != e && other.definition.enabled && other.started && !other.completed && other.definition.useCombatBounds)) continue;
                    int previous = e.definition.requiredPreviousEncounter < 0 ? i - 1 : e.definition.requiredPreviousEncounter;
                    bool eligible = e.definition.trigger == EncounterTrigger.StageEnter || e.definition.trigger == EncounterTrigger.Time ||
                        e.definition.trigger == EncounterTrigger.Manual && e.signalled ||
                        e.definition.trigger == EncounterTrigger.PlayerZone && LivingPlayers.Any(p=>ReachedTrigger(p, e.definition.triggerZone)) ||
                        e.definition.trigger == EncounterTrigger.PreviousEncounterClear && previous >= 0 && previous < i && EncounterDependencySatisfied(encounters[previous]);
                    // Entering a zone latches its event even if the player leaves during its delay.
                    if (!eligible && e.eligibleAt < 0) continue;
                    if (e.eligibleAt < 0) e.eligibleAt = e.definition.trigger == EncounterTrigger.Time || e.definition.trigger == EncounterTrigger.StageEnter ? 0 : StageElapsed;
                    if (StageElapsed - e.eligibleAt + .0001f < e.definition.triggerDelay) continue;
                    e.started = true;
                    BeginEncounterObjects(e);
                    ApplyEncounterLocks();
                }

                e.elapsed += dt;

                for (int w = 0; w < e.waves.Count; w++)
                {
                    var wave = e.waves[w]; if (!wave.definition.enabled || wave.completed) continue;
                    if (!wave.started)
                    {
                        int previousWave = w - 1;
                        while (previousWave >= 0 && !e.waves[previousWave].definition.enabled) previousWave--;
                        bool clear = previousWave < 0 || e.waves[previousWave].completed;
                        float from = wave.definition.trigger == WaveTrigger.PreviousWaveClear && clear && previousWave >= 0 ? e.waves[previousWave].clearedAt : 0;
                        bool eligible = wave.definition.trigger == WaveTrigger.EncounterStart || wave.definition.trigger == WaveTrigger.Time ||
                            wave.definition.trigger == WaveTrigger.Manual && wave.signalled || wave.definition.trigger == WaveTrigger.PreviousWaveClear && clear;

                        if (!eligible || e.elapsed - from + .0001f < wave.definition.spawnDelay)
                        {
                            continue;
                        }

                        wave.started = true;
                        e.coordinator.settings=(wave.definition.overrideAttackCoordination ? wave.definition.attackCoordination : e.definition.attackCoordination) ?? new AttackCoordinationSettings();
                        ApplySceneObjectStates(wave.definition.sceneObjectStates, e.objectOriginals);
                        foreach (var spawn in wave.definition.enemySpawns) 
                        { 
                            wave.plans.Add(new Plan { definition = spawn, due = Mathf.Max(0, spawn.spawnDelay) });
                        }
                    }

                    wave.elapsed += dt;

                    foreach (var plan in wave.plans) 
                    { 
                        while (plan.spawned < Mathf.Max(1, plan.definition.count) && wave.elapsed + .0001f >= plan.due)
                        {
                            if(AvailableSlots(e)<=0)break;
                            Spawn(plan, wave, e); if (!string.IsNullOrEmpty(Failure)) return;
                            plan.spawned++; plan.due += Mathf.Max(0, plan.definition.interval);
                        }
                    }
                    if (wave.plans.All(p => p.spawned >= Mathf.Max(1, p.definition.count)) && wave.enemies.All(h => !h || h.IsDead)) 
                    { 
                        wave.completed = true; 
                        wave.clearedAt = e.elapsed; 
                    }
                }
                e.completed = e.waves.All(w => !w.definition.enabled || w.completed) && (e.definition.clearCondition == EncounterClearCondition.AllEnemiesDefeated || e.clearSignalled);
                if (e.completed) { e.coordinator?.Clear(); e.clearedOnce = true; EndEncounterObjects(e); }
            }
            ApplyEncounterLocks();
            foreach (var actor in Players) previousPlayerPositions[actor] = actor.transform.position;
            bool atExit = LivingPlayers.Any(p=>p.IsGrounded && !p.attackPlayer.CurrentAttack && !p.MovementLocked && Vector2.Distance(p.transform.position, CurrentStage.playerExitPoint) <= CurrentStage.exitRadius);
            // The chapel follows encounter completion, before any exit guidance or travel.
            if (CompletionSatisfied && (CurrentStage.rewardAfterClear == StageReward.UpgradeChoice ||
                CurrentStage.completionMode != StageCompletion.ReachExit || atExit)) GrantStageReward();
            if (ExitUnlocked && atExit) TryAdvance();
            RefreshExitMarkers();
        }
        private void Spawn(Plan plan, WaveState wave, EncounterState encounter)
        {
            var definition = plan.definition;

            if (!definition.prefab || !definition.prefab.GetComponent<CharacterHealth>() || !definition.prefab.GetComponent<CharacterMotor>()) 
            { 
                Failure = "Wave needs a combat enemy prefab.";
                return; 
            }

            var points = definition.spawnPoints;
            Vector2 point = points.Count > 0 ? points[plan.spawned % points.Count] : CurrentStage.movementMax;
            GetEncounterMovement(encounter.definition, out var minimum, out var maximum, true);
            point = new Vector2(Mathf.Clamp(point.x, minimum.x, maximum.x), Mathf.Clamp(point.y, minimum.y, maximum.y));
            SpawnTrackedEnemy(definition.prefab,point,wave,encounter,definition.isBoss);
        }
        private bool ReachedTrigger(CharacterMotor actor, Rect trigger)
        {
            Vector2 current = actor.transform.position;
            if (trigger.Contains(current)) return true;
            if (!previousPlayerPositions.TryGetValue(actor, out var previous)) return false;
            // Slab intersection in ground XY also catches a dash that crosses a narrow trigger in one tick.
            Vector2 delta = current - previous;
            float enter = 0, leave = 1;
            for (int axis = 0; axis < 2; axis++)
            {
                float min = axis == 0 ? trigger.xMin : trigger.yMin, max = axis == 0 ? trigger.xMax : trigger.yMax;
                if (Mathf.Abs(delta[axis]) < .00001f) { if (previous[axis] < min || previous[axis] > max) return false; continue; }
                float first = (min - previous[axis]) / delta[axis], last = (max - previous[axis]) / delta[axis];
                enter = Mathf.Max(enter, Mathf.Min(first, last)); leave = Mathf.Min(leave, Mathf.Max(first, last));
                if (enter > leave) return false;
            }
            return true;
        }
        private void GetEncounterMovement(EncounterDefinition definition, out Vector2 minimum, out Vector2 maximum, bool enemy = false)
        {
            minimum = CurrentStage.movementMin; maximum = CurrentStage.movementMax;
            if (!definition.useCombatBounds || (!enemy && !definition.lockStageUntilClear)) return;
            var bounds = definition.combatBounds;
            if (enemy || definition.exitLock != EncounterExitLock.RightOnly) minimum.x = Mathf.Clamp(bounds.xMin, minimum.x, maximum.x);
            if (enemy || definition.exitLock != EncounterExitLock.LeftOnly) maximum.x = Mathf.Clamp(bounds.xMax, minimum.x, maximum.x);
            minimum.y = Mathf.Clamp(bounds.yMin, minimum.y, maximum.y);
            maximum.y = Mathf.Clamp(bounds.yMax, minimum.y, maximum.y);
        }
        private static void RestrictMotor(CharacterMotor motor, Vector2 minimum, Vector2 maximum)
        {
            if (!motor) return;
            motor.arenaMin = minimum; motor.arenaMax = maximum;
            var position = motor.transform.position;
            position.x = Mathf.Clamp(position.x, minimum.x, maximum.x);
            position.y = Mathf.Clamp(position.y, minimum.y, maximum.y);
            motor.transform.position = position;
        }
        private void ApplyEncounterLocks()
        {
            if (CurrentStage == null) return;
            var active = encounters.FirstOrDefault(e => e.definition.enabled && e.started && !e.completed && e.definition.useCombatBounds);
            Vector2 minimum = CurrentStage.movementMin, maximum = CurrentStage.movementMax;
            if (active != null) GetEncounterMovement(active.definition, out minimum, out maximum);
            foreach (var actor in Players) RestrictMotor(actor, minimum, maximum);
            foreach (var encounter in encounters)
            {
                var min = CurrentStage.movementMin; var max = CurrentStage.movementMax;
                if (encounter.started && !encounter.completed) GetEncounterMovement(encounter.definition, out min, out max, true);
                foreach (var wave in encounter.waves) foreach (var enemy in wave.enemies)
                    if (enemy && !enemy.IsDead) RestrictMotor(enemy.GetComponent<CharacterMotor>(), min, max);
            }
            if (framing) framing.SetEncounterBounds(ActiveCameraBounds);
        }
        public void SignalEncounterClear(string id)
        {
            if (!HasAuthority) return;
            var encounter = encounters.FirstOrDefault(e => e.definition.encounterId == id);
            if (encounter != null) encounter.clearSignalled = true;
        }
        // Debug shortcut uses the real runtime. Earlier encounters are marked complete only for this run.
        public void SimulateEncounter(int stageIndex, int encounterIndex)
        {
            if (!HasAuthority) return;
            RestartAt(stageIndex);
            if (encounterIndex < 0 || encounterIndex >= encounters.Count) return;
            for (int i = 0; i < encounterIndex; i++) encounters[i].completed = encounters[i].clearedOnce = encounters[i].simulationSkipped = true;
            var encounter = encounters[encounterIndex]; encounter.started = true;
            if (!encounter.definition.enabled) { encounter.started = false; return; }
            BeginEncounterObjects(encounter);
            foreach (var actor in Players) actor.ResetForStage(encounter.definition.triggerZone.center);
            ApplyEncounterLocks(); debugVisible = true;
            Tick(0);
        }
        public bool TryAdvance()
        {
            if (!HasAuthority) return false;
            if (!CompletionSatisfied || !player || !LivingPlayers.Any())
            { 
                return false; 
            }
            
            if (!GrantStageReward() || !ExitUnlocked)
            { 
                return false; 
            }
            HideExitMarkers();

            int next = CurrentStage.nextStageIndex < 0 ? StageIndex + 1 : CurrentStage.nextStageIndex;

            if (next >= level.stages.Count)
            {
                LevelCompleted = true;
                player.MoveInput = Vector2.zero;
                onLevelCompleted.Invoke();
            }
            else 
            { 
                EnterStage(next); 
            }
            return true;
        }
        public bool GrantStageReward()
        {
            if (!CompletionSatisfied || !player || !LivingPlayers.Any() || CombatClock.IsPaused) return false;
            if (rewardedStages.Contains(RewardKey)) return true;
            if (rewardStarted) return false;
            rewardStarted = true;
            if(CoopRewards) foreach(var actor in LivingPlayers) actor.GetComponent<RunBuildState>()?.Notify(RunCombatEvent.StageClear);
            else RunUpgrades?.Build?.Notify(RunCombatEvent.StageClear);
            string key = RewardKey;
            if (CurrentStage.rewardAfterClear == StageReward.UpgradeChoice)
            {
                if(CoopRewards)
                {
                    if(!CoopRewards.Begin(()=>rewardedStages.Add(key))) Failure="Co-op reward needs an upgrade pool and reachable chapel.";
                    return false;
                }
                if (!RunUpgrades || !RunUpgrades.pool) { Failure = "UpgradeChoice reward needs a RunUpgradeController with an UpgradePool."; return false; }
                var world = WorldRewards ? WorldRewards : gameObject.AddComponent<RewardSelectionController>();
                if (!world.BeginReward(() => rewardedStages.Add(key))) Failure = "No reachable chapel reward placement. Adjust reward points / geometry or assign the chapel prefab.";
                return false;
            }
            else if (CurrentStage.rewardAfterClear == StageReward.Heal)
                foreach(var actor in LivingPlayers) actor.GetComponent<CharacterHealth>().Heal(actor.GetComponent<CharacterHealth>().EffectiveMaximum * CurrentStage.rewardHealFraction);
            rewardedStages.Add(key); return true;
        }
        public void SignalEncounter(string id) 
        {
            foreach (var e in encounters) 
            {
                if (e.definition.encounterId == id)
                {
                    e.signalled = true;
                }        
            }
        }
        public void SignalWave(string encounter, string wave) { foreach (var e in encounters) if (e.definition.encounterId == encounter) foreach (var w in e.waves) if (w.definition.waveId == wave) w.signalled = true; }
        public void CompleteStageEvent() => stageEventComplete = true;
        public bool Recover()
        {
            if (CurrentStage == null || !CurrentStage.safeRoom || !player.IsGrounded || player.GetComponent<CharacterHealth>().IsDead || Vector2.Distance(player.transform.position, CurrentStage.recoveryPoint) > CurrentStage.recoveryRadius) return false;
            player.GetComponent<CharacterHealth>().Restore(); return true;
        }
        public void Restart(bool entireLevel)
        {
            if (!player) return;
            RunUpgrades?.CloseChoice();
            if (entireLevel) { rewardedStages.Clear(); RunUpgrades?.NewRun(); }
            player.ResetForStage(Vector2.zero); player.GetComponent<CharacterHealth>().Restore(); EnterStage(entireLevel ? 0 : Mathf.Max(0, StageIndex));
        }
        private void Update()
        {
            transitionFlash = Mathf.Max(0, transitionFlash - Time.deltaTime);
            if(MultiplayerSession.Active) return; // Each paired input source owns interaction/restart commands.
            if (CombatClock.IsPaused) return;
            if(debugInput==null && player) debugInput=new MenuNavigationInput(player.GetComponent<PlayerInput>().actions);
            if(Keyboard.current?.f8Key.wasPressedThisFrame==true) debugVisible=!debugVisible;
            if(debugVisible)
            {
                var input=debugInput?.Read() ?? default;
                if(input.Cancel) debugVisible=false; else debugNavigation.Read(input);
                return;
            }
            debugInput?.Read();
            if (Keyboard.current != null)
            {
                if ((Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) && (LevelCompleted || player && player.GetComponent<CharacterHealth>().IsDead))
                { var hub=GetComponent<PlayerHubController>(); if(hub) hub.ReturnToHub(); else Restart(LevelCompleted); }
            }
            if (player && player.GetComponent<PlayerInput>().actions.FindAction("Player/Interact",true).WasPressedThisFrame()) Interact();
            if(Gamepad.current?.buttonSouth.wasPressedThisFrame==true && (LevelCompleted || player && player.GetComponent<CharacterHealth>().IsDead))
            { var hub=GetComponent<PlayerHubController>(); if(hub) hub.ReturnToHub(); else Restart(LevelCompleted); }
        }
        public bool Interact()
        {
            var story=GetComponent<BeatEmUp.Story.PrologueDirector>();
            if(story && story.ActiveStory)return story.TryInteractFestivalStore(player);
            return BeatEmUp.Story.StoryNpcConversation.TryInteract(player) || (CurrentStage!=null && CurrentStage.hub ? GetComponent<PlayerHubController>().Interact(player) : WorldRewards && WorldRewards.IsPending ? WorldRewards.Interact() : Recover());
        }
        private void OnGUI()
        {
            DrawNextAreaEdgeArrow();
            if (!showHud || CurrentStage == null) return;
            string status = WorldRewards && WorldRewards.IsPending ? WorldRewards.State == WorldRewardState.RewardPending ? "Stage clear — approach the chapel and press E / L1 / LB" : "Walk to a blessing and press E / L1 / LB to choose" : RunUpgrades && RunUpgrades.IsChoosing ? "Choose an upgrade" : LevelCompleted ? "You escaped! R / Enter / A: restart level" : player.GetComponent<CharacterHealth>().IsDead ? "Defeated — R / Enter / A: retry room" : !string.IsNullOrEmpty(Failure) ? Failure : ExitUnlocked ? "Exit open — walk to the right-hand exit" : "Exit locked — finish the encounter";
            GUI.Box(new Rect(12, 12, 470, 80), $"{StageIndex + 1}/{level.stages.Count}  {CurrentStage.stageName}\nHP {player.GetComponent<CharacterHealth>().Current:0}  Enemies {LivingEnemies.Count()}  Totems {RemainingTotems}\n{status}");
            if (CurrentStage.safeRoom) GUI.Box(new Rect(12, 98, 470, 30), "Stand near the shrine: E / L1 / LB to recover");
            if (debugVisible)
            {
                debugNavigation.Begin();
                GUILayout.BeginArea(new Rect(12, 138, 320, 430), GUI.skin.box); GUILayout.Label("Stage flow debug (F8)");
                for (int i = 0; i < level.stages.Count; i++) if (debugNavigation.Button((i + 1) + ": " + level.stages[i].stageName)) RestartAt(i);
                if (debugNavigation.Button("Advance if unlocked")) TryAdvance();
                if (debugNavigation.Button("Complete stage event")) CompleteStageEvent();
                foreach (var e in encounters)
                {
                    GUILayout.Label(e.definition.encounterId + ": " + (e.completed ? "clear" : e.started ? e.definition.useCombatBounds ? "Combat Locked" : "active" : "waiting") + " / wave " + e.waves.Count(w => w.started) + "/" + e.waves.Count + " / enemies " + e.waves.Sum(w => w.enemies.Count(h => h && !h.IsDead)));
                    if (e.definition.clearCondition == EncounterClearCondition.ManualSignal && debugNavigation.Button("Clear signal " + e.definition.encounterId)) SignalEncounterClear(e.definition.encounterId);
                    if (debugNavigation.Button("Signal " + e.definition.encounterId)) SignalEncounter(e.definition.encounterId);
                    foreach (var w in e.waves)
                    {
                        GUILayout.Label(w.definition.waveId + ": " + (w.completed ? "clear" : w.started ? "active" : "waiting") + " / live " + w.enemies.Count(h => h && !h.IsDead) + " / pending " + w.plans.Sum(p => Mathf.Max(1, p.definition.count) - p.spawned));
                        if (w.definition.trigger == WaveTrigger.Manual && debugNavigation.Button("Signal " + w.definition.waveId)) SignalWave(e.definition.encounterId, w.definition.waveId);
                    }
                }
                GUILayout.EndArea();
                debugNavigation.End();
            }
            if (transitionFlash > 0) { var color = GUI.color; GUI.color = new Color(0, 0, 0, transitionFlash / .2f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = color; }
        }
        public void RestartAt(int index) { RunUpgrades?.CloseChoice(); player.ResetForStage(Vector2.zero); player.GetComponent<CharacterHealth>().Restore(); EnterStage(index); }
        private void OnDrawGizmosSelected()
        {
            // Edit-time gizmos come from the authoring preview's selected stage, not CurrentStage.
            if (!Application.isPlaying) return;
            if (CurrentStage == null) 
            { 
                return; 
            }

            var s = CurrentStage;
            Gizmos.color = Color.green; Gizmos.DrawWireCube((s.movementMin + s.movementMax) * .5f, s.movementMax - s.movementMin);
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(s.playerEntryPoint, .2f); Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(s.playerExitPoint, s.exitRadius);
            foreach (var e in s.encounters) foreach (var w in e.waves) foreach (var spawn in w.enemySpawns) foreach (var point in spawn.spawnPoints) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(point, .15f); }
            foreach (var e in s.encounters)
            {
                Gizmos.color = Color.yellow; Gizmos.DrawWireCube(e.triggerZone.center, e.triggerZone.size);
                if (!e.useCombatBounds) continue;
                Gizmos.color = Color.red; Gizmos.DrawWireCube(e.combatBounds.center, e.combatBounds.size);
                if (e.lockCamera) { Gizmos.color = Color.magenta; Gizmos.DrawWireCube(e.cameraBounds.center, e.cameraBounds.size); }
            }
            foreach (var p in s.destructibles) { Gizmos.color = Color.magenta; Gizmos.DrawWireCube(p.position + p.hitboxOffset, p.hitboxSize); }
        }
    }
}
