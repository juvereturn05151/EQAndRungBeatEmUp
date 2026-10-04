using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public enum StageCompletion { ReachExit, ClearEncounters, BossDefeated, Event }
    public enum EncounterTrigger { StageEnter, Time, PreviousEncounterClear, PlayerZone, Manual }
    public enum WaveTrigger { EncounterStart, Time, PreviousWaveClear, Manual }
    public enum PropKind { Normal, CursedTotem }
    public enum StageReward { None, UpgradeChoice, Heal }
    public enum StageType { Combat, Safe, Boss, Exit }

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
        public string stageId;
        public string stageName;
        public StageType stageType;
        public bool IsSafeStage => stageType == StageType.Safe || safeRoom;
        [Header("Stage Art Layout")]
        public Sprite backgroundSprite, floorSprite;
        [Min(1)] public float artWidth = 7.2f;
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
        public string encounterId = "Encounter";
        public EncounterTrigger trigger;
        [Min(0)] public float triggerDelay;
        [Tooltip("Earlier encounter index, or -1 for the immediately preceding encounter.")]
        public int requiredPreviousEncounter = -1;
        public Rect triggerZone = new Rect(-1, -.4f, 2, 1.05f);
        public bool lockStageUntilClear = true;
        public bool requiredForCompletion = true;
        public List<WaveDefinition> waves = new List<WaveDefinition>();
    }

    [Serializable]
    public sealed class WaveDefinition
    {
        public string waveId = "Wave";
        public WaveTrigger trigger;
        [Tooltip("Time delay is measured from encounter start; clear-based delay starts when the previous wave clears.")]
        [Min(0)] public float spawnDelay;
        public List<EnemySpawnDefinition> enemySpawns = new List<EnemySpawnDefinition>();
    }

    [Serializable]
    public sealed class EnemySpawnDefinition
    {
        public GameObject prefab;
        [Min(1)] public int count = 1;
        public List<Vector2> spawnPoints = new List<Vector2> { new Vector2(2, 0) };
        [Min(0)] public float interval;
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
        public GameObject dropPrefab;
    }

    [Serializable]
    public sealed class StagePropPlacement
    {
        public GameObject prefab;
        public Vector2 position;
    }
}
