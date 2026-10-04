using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public enum UpgradeRarity { Common, Rare, Epic }
    public enum RunModifier
    {
        GroundDamage, ComboDamagePerStep, GroundFinisherDamage, AirborneTargetDamage,
        AirFinisherDamage, AirFinisherFallSpeed, LauncherDamage, DiveDamage,
        DiveShockwaveDamage, DiveRecoveryReduction, ParryWindowBonus, ParryAttackBonus,
        ParryStunBonus, DodgeRecoveryReduction, DodgeDistanceBonus, DodgeAttackBonus,
        BlockstunReduction, MaximumHealthBonus, LethalSaves
    }
    [Serializable]
    public sealed class UpgradeEffect
    {
        public RunModifier modifier;
        public float amount;
    }
    [CreateAssetMenu(menuName = "Beat Em Up/Run Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public UpgradeRarity rarity;
        public List<string> tags = new List<string>();
        [Min(1)] public int maxStacks = 1;
        [Min(0)] public float weight = 1;
        public List<UpgradeDefinition> prerequisites = new List<UpgradeDefinition>();
        public List<UpgradeDefinition> incompatibleUpgrades = new List<UpgradeDefinition>();
        public List<UpgradeEffect> effects = new List<UpgradeEffect>();
    }
}
