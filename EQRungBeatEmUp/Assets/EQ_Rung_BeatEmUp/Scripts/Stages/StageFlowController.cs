using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace BeatEmUp
{
    public sealed class StageFlowController : MonoBehaviour, ICombatFrameListener
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
        public bool EncountersComplete => encounters.All(e => !e.definition.requiredForCompletion || e.completed);
        public bool ExitUnlocked => CompletionSatisfied && !(RunUpgrades && RunUpgrades.IsChoosing) && !(WorldRewards && WorldRewards.IsPending);
        public bool CompletionSatisfied => CurrentStage != null && !LevelCompleted && string.IsNullOrEmpty(Failure) &&
            !encounters.Any(e => e.started && !e.completed && e.definition.lockStageUntilClear) &&
            (CurrentStage.completionMode == StageCompletion.ReachExit ||
             CurrentStage.completionMode == StageCompletion.ClearEncounters && EncountersComplete ||
             CurrentStage.completionMode == StageCompletion.BossDefeated && EncountersComplete && bossSpawned && !LivingEnemies.Any(h => h.GetComponent<TotemBossController>()) && RemainingTotems == 0 ||
             CurrentStage.completionMode == StageCompletion.Event && stageEventComplete);
        private sealed class Plan { public EnemySpawnDefinition definition; public int spawned; public float due; }
        private sealed class WaveState { public WaveDefinition definition; public bool started, completed, signalled; public float elapsed, clearedAt; public readonly List<Plan> plans = new List<Plan>(); public readonly List<CharacterHealth> enemies = new List<CharacterHealth>(); }
        private sealed class EncounterState { public EncounterDefinition definition; public bool started, completed, signalled; public float elapsed, eligibleAt = -1; public readonly List<WaveState> waves = new List<WaveState>(); }
        private readonly List<EncounterState> encounters = new List<EncounterState>();
        private readonly List<DestructibleObject> destructibles = new List<DestructibleObject>();
        private GameObject room;
        private bool stageEventComplete, bossSpawned, debugVisible;
        private float transitionFlash;
        private readonly HashSet<string> rewardedStages = new HashSet<string>();
        private bool rewardStarted;
        private string RewardKey => string.IsNullOrEmpty(CurrentStage.stageId) ? StageIndex.ToString() : CurrentStage.stageId;
        private void OnEnable() => CombatClock.Register(this);
        private void OnDisable() { CombatClock.Unregister(this); if (player) player.GetComponent<CharacterHealth>().SafeStageProtection = false; }
        private void Start() 
        { if (level && player) Restart(true); else Failure = "Assign a LevelDefinition and player."; }
        // Stage asset edits during Play update the artwork without restarting encounters.
        private void LateUpdate() 
        {
            if (CurrentStage != null) 
            {
                ApplyStageArt(CurrentStage);
            } 
        }
        public void EnterStage(int index)
        {
            if (!level || !player || index < 0 || index >= level.stages.Count) 
            { 
                return;
            }
            
            if (room) 
            { 
                room.SetActive(false); 
                Destroy(room); 
            }

            WorldRewards?.Cancel(); RunUpgrades?.CloseChoice();
            StageIndex = index; StageElapsed = 0; LevelCompleted = false; Failure = null;
            stageEventComplete = bossSpawned = false; encounters.Clear(); destructibles.Clear();
            rewardStarted = false;
            RunUpgrades?.Initialize(); RunUpgrades?.Build?.ClearTransient();
            var stage = CurrentStage;
            player.GetComponent<CharacterHealth>().SafeStageProtection = stage.IsSafeStage;
            room = new GameObject("Stage runtime — " + stage.stageName); 
            room.transform.SetParent(transform, false);
            player.GetComponent<ComboController>()?.ResetCombo(); 
            player.ResetForStage(stage.playerEntryPoint); 
            player.Face(1);
            player.arenaMin = stage.movementMin; 
            player.arenaMax = stage.movementMax;
            
            if (framing)
            {
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
                var state = new EncounterState { definition = e };
                foreach (var w in e.waves) 
                { 
                    state.waves.Add(new WaveState { definition = w }); 
                }
                encounters.Add(state);
            }
            transitionFlash = .2f;
        }
        public void ApplyStageArt(StageSegmentDefinition stage)
        {
            if (stage == null) 
            { 
                return; 
            }

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
            if (CombatClock.IsPaused || CurrentStage == null || LevelCompleted || !player || player.GetComponent<CharacterHealth>().IsDead || !string.IsNullOrEmpty(Failure)) return;
            if (WorldRewards && WorldRewards.IsPending) return;
            dt = Mathf.Max(0, dt); StageElapsed += dt;
            for (int i = 0; i < encounters.Count; i++)
            {
                var e = encounters[i]; if (e.completed) continue;
                if (!e.started)
                {
                    int previous = e.definition.requiredPreviousEncounter < 0 ? i - 1 : e.definition.requiredPreviousEncounter;
                    bool eligible = e.definition.trigger == EncounterTrigger.StageEnter || e.definition.trigger == EncounterTrigger.Time ||
                        e.definition.trigger == EncounterTrigger.Manual && e.signalled ||
                        e.definition.trigger == EncounterTrigger.PlayerZone && e.definition.triggerZone.Contains(player.transform.position) ||
                        e.definition.trigger == EncounterTrigger.PreviousEncounterClear && previous >= 0 && previous < i && encounters[previous].completed;
                    // Entering a zone latches its event even if the player leaves during its delay.
                    if (!eligible && e.eligibleAt < 0) continue;
                    if (e.eligibleAt < 0) e.eligibleAt = e.definition.trigger == EncounterTrigger.Time || e.definition.trigger == EncounterTrigger.StageEnter ? 0 : StageElapsed;
                    if (StageElapsed - e.eligibleAt + .0001f < e.definition.triggerDelay) continue;
                    e.started = true;
                }

                e.elapsed += dt;

                for (int w = 0; w < e.waves.Count; w++)
                {
                    var wave = e.waves[w]; if (wave.completed) continue;
                    if (!wave.started)
                    {
                        bool clear = w > 0 && e.waves[w - 1].completed;
                        float from = wave.definition.trigger == WaveTrigger.PreviousWaveClear && clear ? e.waves[w - 1].clearedAt : 0;
                        bool eligible = wave.definition.trigger == WaveTrigger.EncounterStart || wave.definition.trigger == WaveTrigger.Time ||
                            wave.definition.trigger == WaveTrigger.Manual && wave.signalled || wave.definition.trigger == WaveTrigger.PreviousWaveClear && clear;

                        if (!eligible || e.elapsed - from + .0001f < wave.definition.spawnDelay)
                        {
                            continue;
                        }

                        wave.started = true;
                        foreach (var spawn in wave.definition.enemySpawns) 
                        { 
                            wave.plans.Add(new Plan { definition = spawn }); 
                        }
                    }

                    wave.elapsed += dt;

                    foreach (var plan in wave.plans) 
                    { 
                        while (plan.spawned < Mathf.Max(1, plan.definition.count) && wave.elapsed + .0001f >= plan.due)
                        {
                            Spawn(plan, wave); plan.spawned++; plan.due += Mathf.Max(0, plan.definition.interval);
                        }
                    }
                    if (wave.plans.All(p => p.spawned >= Mathf.Max(1, p.definition.count)) && wave.enemies.All(h => !h || h.IsDead)) 
                    { 
                        wave.completed = true; 
                        wave.clearedAt = e.elapsed; 
                    }
                }
                e.completed = e.waves.All(w => w.completed);
            }
            bool atExit = player.IsGrounded && !player.attackPlayer.CurrentAttack && !player.MovementLocked && Vector2.Distance(player.transform.position, CurrentStage.playerExitPoint) <= CurrentStage.exitRadius;
            if (CompletionSatisfied && (CurrentStage.completionMode != StageCompletion.ReachExit || atExit)) GrantStageReward();
            if (ExitUnlocked && atExit) TryAdvance();
        }
        private void Spawn(Plan plan, WaveState wave)
        {
            var definition = plan.definition;

            if (!definition.prefab || !definition.prefab.GetComponent<CharacterHealth>() || !definition.prefab.GetComponent<CharacterMotor>()) 
            { 
                Failure = "Wave needs a combat enemy prefab.";
                return; 
            }

            var points = definition.spawnPoints;
            Vector2 point = points.Count > 0 ? points[plan.spawned % points.Count] : CurrentStage.movementMax;
            point = new Vector2(Mathf.Clamp(point.x, CurrentStage.movementMin.x, CurrentStage.movementMax.x), Mathf.Clamp(point.y, CurrentStage.movementMin.y, CurrentStage.movementMax.y));
            var go = Instantiate(definition.prefab, point, Quaternion.identity, room.transform);
            var motor = go.GetComponent<CharacterMotor>(); motor.arenaMin = CurrentStage.movementMin; motor.arenaMax = CurrentStage.movementMax;
            var combat = go.GetComponent<EnemyCombat>(); if (combat) { combat.target = player.transform; combat.passiveTrainingDummy = false; }
            var boss = go.GetComponent<TotemBossController>();

            if (definition.isBoss && !boss) 
            { 
                boss = go.AddComponent<TotemBossController>(); 
            }
            if (boss) 
            { 
                boss.flow = this; 
                bossSpawned = true; 
            }

            wave.enemies.Add(go.GetComponent<CharacterHealth>());
        }
        public bool TryAdvance()
        {
            if (!ExitUnlocked || !player || player.GetComponent<CharacterHealth>().IsDead) 
            { 
                return false; 
            }
            
            if (!GrantStageReward()) 
            { 
                return false; 
            }

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
            if (!CompletionSatisfied || !player || player.GetComponent<CharacterHealth>().IsDead || CombatClock.IsPaused) return false;
            if (rewardedStages.Contains(RewardKey)) return true;
            if (rewardStarted) return false;
            rewardStarted = true; RunUpgrades?.Build?.Notify(RunCombatEvent.StageClear);
            string key = RewardKey;
            if (CurrentStage.rewardAfterClear == StageReward.UpgradeChoice)
            {
                if (!RunUpgrades || !RunUpgrades.pool) { Failure = "UpgradeChoice reward needs a RunUpgradeController with an UpgradePool."; return false; }
                var world = WorldRewards ? WorldRewards : gameObject.AddComponent<RewardSelectionController>();
                if (!world.BeginReward(() => rewardedStages.Add(key))) Failure = "No reachable chapel reward placement. Adjust reward points / geometry or assign the chapel prefab.";
                return false;
            }
            else if (CurrentStage.rewardAfterClear == StageReward.Heal)
                player.GetComponent<CharacterHealth>().Heal(player.GetComponent<CharacterHealth>().EffectiveMaximum * CurrentStage.rewardHealFraction);
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
            if (CombatClock.IsPaused) return;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame) Interact();
                if (Keyboard.current.rKey.wasPressedThisFrame && (LevelCompleted || player && player.GetComponent<CharacterHealth>().IsDead)) Restart(LevelCompleted);
                if (Keyboard.current.f8Key.wasPressedThisFrame) debugVisible = !debugVisible;
            }
            if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame) Interact();
        }
        public bool Interact() => WorldRewards && WorldRewards.IsPending ? WorldRewards.Interact() : Recover();
        private void OnGUI()
        {
            if (!showHud || CurrentStage == null) return;
            string status = WorldRewards && WorldRewards.IsPending ? WorldRewards.State == WorldRewardState.RewardPending ? "Stage clear — approach the chapel and press E / Select" : "Walk to a blessing and press E / Select to choose" : RunUpgrades && RunUpgrades.IsChoosing ? "Choose an upgrade" : LevelCompleted ? "You escaped! R: restart level" : player.GetComponent<CharacterHealth>().IsDead ? "Defeated — R: retry room" : !string.IsNullOrEmpty(Failure) ? Failure : ExitUnlocked ? "Exit open — walk to the right-hand exit" : "Exit locked — finish the encounter";
            GUI.Box(new Rect(12, 12, 470, 80), $"{StageIndex + 1}/{level.stages.Count}  {CurrentStage.stageName}\nHP {player.GetComponent<CharacterHealth>().Current:0}  Enemies {LivingEnemies.Count()}  Totems {RemainingTotems}\n{status}");
            if (CurrentStage.safeRoom) GUI.Box(new Rect(12, 98, 470, 30), "Stand near the shrine: E / gamepad Select to recover");
            if (debugVisible)
            {
                GUILayout.BeginArea(new Rect(12, 138, 320, 430), GUI.skin.box); GUILayout.Label("Stage flow debug (F8)");
                for (int i = 0; i < level.stages.Count; i++) if (GUILayout.Button((i + 1) + ": " + level.stages[i].stageName)) RestartAt(i);
                if (GUILayout.Button("Advance if unlocked")) TryAdvance();
                if (GUILayout.Button("Complete stage event")) CompleteStageEvent();
                foreach (var e in encounters)
                {
                    GUILayout.Label(e.definition.encounterId + ": " + (e.completed ? "clear" : e.started ? "active" : "waiting"));
                    if (GUILayout.Button("Signal " + e.definition.encounterId)) SignalEncounter(e.definition.encounterId);
                    foreach (var w in e.waves)
                    {
                        GUILayout.Label(w.definition.waveId + ": " + (w.completed ? "clear" : w.started ? "active" : "waiting") + " / live " + w.enemies.Count(h => h && !h.IsDead) + " / pending " + w.plans.Sum(p => Mathf.Max(1, p.definition.count) - p.spawned));
                        if (w.definition.trigger == WaveTrigger.Manual && GUILayout.Button("Signal " + w.definition.waveId)) SignalWave(e.definition.encounterId, w.definition.waveId);
                    }
                }
                GUILayout.EndArea();
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
            foreach (var p in s.destructibles) { Gizmos.color = Color.magenta; Gizmos.DrawWireCube(p.position + p.hitboxOffset, p.hitboxSize); }
        }
    }
}
