using System;
using System.Collections.Generic;
using UnityEngine;
namespace BeatEmUp
{
    public enum BossAction { Book, Swipe, SummonRusher, CurseWave, SummonStrongGhosts, TelegraphMelee, Teleport }
    [Flags] public enum BossInterruptHitTypes { Normal = 1, Launcher = 2, AirFinisher = 4, KnockDown = 8, Stun = 16, All = 31 }
    [Serializable] public sealed class BossMeleeInterruptData
    {
        [Tooltip("Zero-based inclusive attack frames. Defaults represent displayed frames 1–30.")]
        [Min(0)] public int firstFrame = 0, lastFrame = 29;
        [Min(1)] public int hitStaggerFrames = 30, parryStaggerFrames = 60;
        [Min(0)] public int interruptImmunityFrames = 180;
        [Min(0)] public float minimumDamage = 1;
        public BossInterruptHitTypes hitTypes = BossInterruptHitTypes.All;
        public bool allowProjectileHits = true;
        [Min(0), Tooltip("Tracking stops at this zero-based frame, or the first active frame if earlier.")]
        public int facingLockFrame = 12;
        public AttackData hitReaction, parryReaction, interruptFeedback;
    }
    public enum AdditionalTotemWave { Ignore, RefreshTimer }
    public enum BossTotemMode { OneShot, Respawn }
    public enum StrongGhostPriority { Random, GrapplerFirst, ThrowerFirst }
    [Serializable] public sealed class BossWarpPoint { public string label; public bool enabled = true; public Vector2 position; }
    [Serializable] public sealed class BossActionChoice
    {
        public BossAction action; public bool enabled = true;
        public AttackCoordinationData coordination=new AttackCoordinationData{ignoreCoordinator=true};
        [Min(0), Tooltip("Far-range weight; also the legacy/default weight.")] public float weight = 1;
        [Min(-1), Tooltip("Close-range weight. -1 inherits Weight.")] public float closeRangeWeight = -1;
        public AttackData attack;
    }
    [CreateAssetMenu(menuName = "Beat Em Up/Boss Encounter")]
    public sealed class BossEncounterData : ScriptableObject
    {
        [Header("World-space arena positions")]
        public List<BossWarpPoint> warpPoints = new List<BossWarpPoint>();
        public List<Vector2> minionSpawnPoints = new List<Vector2>();
        [Min(0)] public int warpOutFrames = 24, warpInFrames = 24, arrivalFrames = 12, warpCooldownFrames = 30;
        public bool canRepeatWarpPoint;
        [Min(0)] public float warpOffset = .08f;
        [Range(0, 1)] public float moveChance = .25f;
        [Min(0)] public float moveDistance = .25f, moveSpeed = .8f;
        [Header("Phases / frame timelines")]
        [Range(.01f, 1)] public float phase2HealthThreshold = .5f;
        [Min(1)] public int phaseTransitionFrames = 45;
        public List<BossActionChoice> phase1 = new List<BossActionChoice>(), phase2 = new List<BossActionChoice>();
        [Header("Distance preference (ground plane; no player pursuit)")]
        public bool useDistanceWeights = true;
        [Min(.1f)] public float closeRange = 1.4f;
        [Min(.01f)] public float meleeLaneTolerance = .55f;
        [Min(0)] public int teleportCooldownFrames = 180;
        public BossMeleeInterruptData meleeInterrupt = new BossMeleeInterruptData();
        public CombatProjectile bookProjectile, curseWaveProjectile;
        public Vector2 bookSpawnOffset = new Vector2(.45f, .78f), curseSpawnOffset = new Vector2(.65f, .65f);
        [Header("Totems / vulnerability overlay")]
        [Min(1)] public int vulnerabilityFrames = 420;
        public AdditionalTotemWave additionalWave;
        public BossTotemMode totemMode = BossTotemMode.Respawn;
        [Min(.01f)] public float breakWaveRadius = 1.25f;
        [Min(1)] public int breakWaveFrames = 45, totemRespawnFrames = 600;
        [Min(1)] public float totemRespawnHP = 20;
        [Header("Tracked reinforcements (always respect encounter and stage caps)")]
        public GameObject rusherPrefab, grapplerPrefab, throwerPrefab;
        [Min(0)] public int rusherCount = 1, grapplerCount = 1, throwerCount = 1, maxBossMinions = 6;
        [Min(.1f)] public float playerSpawnClearance = 1.25f, enemySpawnClearance = .65f;
        public StrongGhostPriority strongGhostPriority;
        public bool despawnMinionsOnDeath;
        [Header("Shared VFX / SFX feedback hooks")]
        public AttackData warpOutFeedback, warpInFeedback, phaseFeedback, shieldHitFeedback, vulnerableFeedback, totemBreakFeedback;
        public Color shieldColor = new Color(.65f, .4f, 1, .8f), vulnerableColor = new Color(1, .85f, .65f, 1);
    }
}
