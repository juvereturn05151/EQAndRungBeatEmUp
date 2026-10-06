using UnityEngine;

namespace BeatEmUp
{
    public enum PlayerSkillDelivery { Projectile, Area }
    [CreateAssetMenu(menuName = "Beat Em Up/Player Skill")]
    public sealed class PlayerSkillData : ScriptableObject
    {
        public string displayName = "Shadow Dragon";
        [Min(0)] public float meterCost = 1;
        public AttackData cast;
        public string releaseEvent = "SpawnDragon";
        public CombatProjectile projectilePrefab;
        public PlayerSkillDelivery delivery;
        [Header("Area skill cosmetics (collision is authored in Cast AttackData)")]
        public string guardianEvent = "SummonGuardian";
        public AttackData guardianFeedback, releaseFeedback;
        [Tooltip("X follows facing; Y is height above the player's ground lane.")]
        public Vector2 spawnOffset = new Vector2(.8f, .75f);
    }
}
