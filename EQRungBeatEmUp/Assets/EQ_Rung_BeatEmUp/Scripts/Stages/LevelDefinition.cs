using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public enum StageCompletion { ReachExit, ClearEncounters, BossDefeated, Event }
    public enum EncounterTrigger { StageEnter, Time, PreviousEncounterClear, PlayerZone, Manual }
    public enum WaveTrigger { [InspectorName("Immediate")] EncounterStart, [InspectorName("After Delay")] Time, [InspectorName("After Previous Wave Cleared")] PreviousWaveClear, Manual }
    public enum PropKind { Normal, CursedTotem }
    public enum StageReward { None, UpgradeChoice, Heal }
    public enum StageType { Combat, Safe, Boss, Exit }
    public enum EncounterClearCondition { AllEnemiesDefeated, ManualSignal }
    public enum EncounterExitLock { BothSides, LeftOnly, RightOnly }

    [CreateAssetMenu(menuName = "Beat Em Up/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public string levelId = "ThaiHauntedHouse";
        public string levelName = "Thai Haunted House";
        public List<StageSegmentDefinition> stages = new List<StageSegmentDefinition>();
    }

    [Serializable]
    public sealed class StageSegmentDefinition
    {
        [Min(1), Tooltip("Room-wide active enemy limit, shared by waves and backup calls.")] public int maxActiveEnemies=12;
        public string stageId;
        public string stageName;
        public PlayerHubDefinition hub;
        public StageType stageType;
        public bool IsSafeStage => stageType == StageType.Safe || safeRoom;
        [Header("Stage Art Layout")]
        public Sprite backgroundSprite, floorSprite;
        [Min(1)] public float artWidth = 7.6f;
        [Min(.01f), Tooltip("Rendered background height in world units. Does not change movement bounds.")]
        public float backgroundHeight = 1.6f;
        [Tooltip("World Y of the background's centered sprite pivot.")]
        public float backgroundCenterY = 2.56f;
        [Min(.01f), Tooltip("Rendered floor height in world units. Does not change movement bounds.")]
        public float floorHeight = 2.4f;
        [Tooltip("World Y of the floor's centered sprite pivot.")]
        public float floorCenterY = .56f;
        [Header("Gameplay / Movement")]
        public Vector2 movementMin = new Vector2(-3.05f, -.4f);
        public Vector2 movementMax = new Vector2(3.05f, .65f);
        public Vector2 playerEntryPoint = new Vector2(-2.5f, 0);
        public Vector2 playerExitPoint = new Vector2(2.7f, 0);
        [Min(.1f)] public float exitRadius = .35f;
        public StageCompletion completionMode = StageCompletion.ClearEncounters;
        public StageReward rewardAfterClear;
        [Header("World Upgrade Reward")]
        public Vector2 chapelSpawnPoint = new Vector2(.9f, .1f);
        public Vector2 rewardChoiceCenter = new Vector2(0, -.1f);
        [Min(1.5f)] public float rewardChoiceSpacing = 1.8f;
        [Min(.2f)] public float rewardInteractRadius = .65f;
        [Range(0, 1), Tooltip("Fraction of effective maximum HP restored by a Heal reward.")]
        public float rewardHealFraction = .5f;
        [Tooltip("-1 uses the next ordered stage. A value equal to stage count ends the level.")]
        public int nextStageIndex = -1;
        public List<EncounterDefinition> encounters = new List<EncounterDefinition>();
        public List<DestructiblePlacement> destructibles = new List<DestructiblePlacement>();
        public List<StagePropPlacement> decorativeProps = new List<StagePropPlacement>();
        public bool safeRoom;
        public Vector2 recoveryPoint = new Vector2(0, .1f);
        [Min(.1f)] public float recoveryRadius = 1;
        public AudioClip ambience;
        [TextArea] public string notes;
    }

    [Serializable]
    public sealed class EncounterDefinition
    {
        public AttackCoordinationSettings attackCoordination=new AttackCoordinationSettings();
        public bool enabled = true;
        [Tooltip("Normally disabled encounters are skipped. Enable this only to deliberately block completion/dependencies while disabled.")]
        public bool disabledBlocksProgression;
        public bool restoreSceneObjectsOnEncounterEnd;
        public List<SceneObjectState> sceneObjectStates = new List<SceneObjectState>();
        [Min(1), Tooltip("Shared active-enemy limit for authored waves and reinforcement calls.")] public int maxActiveEnemies=8;
        public string encounterId = "Encounter";
        public EncounterTrigger trigger;
        [Min(0)] public float triggerDelay;
        [Tooltip("Earlier encounter index, or -1 for the immediately preceding encounter.")]
        public int requiredPreviousEncounter = -1;
        public Rect triggerZone = new Rect(-1, -.4f, 2, 1.05f);
        public bool lockStageUntilClear = true;
        public bool requiredForCompletion = true;
        [Header("Combat Encounter Zone (off preserves legacy spawn triggers)")]
        public bool useCombatBounds;
        public Rect combatBounds = new Rect(-3, -.4f, 6, 1.05f);
        public bool lockCamera = true;
        [Tooltip("Visible camera arena in world XY. A smaller arena centers the existing view; vertical composition / jump framing is preserved.")]
        public Rect cameraBounds = new Rect(-3.8f, -.64f, 7.6f, 4);
        public EncounterExitLock exitLock;
        public EncounterClearCondition clearCondition;
        [Tooltip("When disabled, a PlayerZone encounter re-arms after clearing and all players leave the trigger.")]
        public bool oneShot = true;
        public List<WaveDefinition> waves = new List<WaveDefinition>();
    }

    [Serializable]
    public sealed class WaveDefinition
    {
        public bool overrideAttackCoordination;
        public AttackCoordinationSettings attackCoordination=new AttackCoordinationSettings();
        public bool enabled = true;
        public List<SceneObjectState> sceneObjectStates = new List<SceneObjectState>();
        public string waveId = "Wave";
        public WaveTrigger trigger;
        [Tooltip("Time delay is measured from encounter start; clear-based delay starts when the previous wave clears.")]
        [Min(0)] public float spawnDelay;
        [InspectorName("Spawn Groups")] public List<EnemySpawnDefinition> enemySpawns = new List<EnemySpawnDefinition>();
    }

    [Serializable]
    public sealed class SceneObjectState
    {
        [Tooltip("Resolved through the scene Stage Flow object's bindings. Use the encounter editor to assign a Hierarchy object.")]
        public string bindingId;
        public bool apply = true;
        public bool active = true;
    }

    [Serializable]
    public sealed class EnemySpawnDefinition
    {
        public GameObject prefab;
        [Min(1)] public int count = 1;
        public List<Vector2> spawnPoints = new List<Vector2> { new Vector2(2, 0) };
        [Min(0)] public float interval;
        [Min(0)] public float spawnDelay;
        public bool isBoss;
    }

    [Serializable]
    public sealed class DestructiblePlacement
    {
        public string label = "Breakable prop";
        public GameObject prefab;
        public Sprite intactSprite, damagedSprite, brokenSprite;
        public Vector2 position;
        [Min(1)] public float health = 25;
        public Vector2 hitboxSize = new Vector2(.65f, .9f);
        public Vector2 hitboxOffset = new Vector2(0, .4f);
        public PropKind kind;
        [Min(0), Tooltip("Boss Totems only: 0 uses the Boss Encounter's default break-wave radius.")]
        public float totemBreakRadius;
        public GameObject dropPrefab;
    }

    [Serializable]
    public sealed class StagePropPlacement
    {
        public GameObject prefab;
        public Vector2 position;
    }
}
