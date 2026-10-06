using System;
using UnityEngine;
namespace BeatEmUp
{
    [Flags] public enum AttackTokenCategory { None=0, Melee=1, Ranged=2, CrowdControl=4, Support=8 }
    [Serializable] public sealed class AttackCoordinationData
    {
        public bool requiresMeleeSlot=true, requiresRangedSlot, requiresCrowdControlSlot, requiresSupportSlot;
        public float priorityModifier;
        public bool ignoreCoordinator;
        public AttackTokenCategory Categories => (requiresMeleeSlot ? AttackTokenCategory.Melee : 0) |
            (requiresRangedSlot ? AttackTokenCategory.Ranged : 0) | (requiresCrowdControlSlot ? AttackTokenCategory.CrowdControl : 0) |
            (requiresSupportSlot ? AttackTokenCategory.Support : 0);
    }
    [Serializable] public sealed class AttackCoordinationSettings
    {
        public bool useAttackCoordination=true;
        [Min(0)] public int maxMeleeAttackers=1, maxRangedAttackers=1, maxCrowdControlAttackers=1, maxSupportAttackers=1;
        [Min(0)] public int minimumGlobalAttackGapFrames=20;
        [Min(0)] public int crowdControlRecoveryGraceFrames=45;
        public bool pauseNewCrowdControlWhilePlayerDisabled=true;
        public bool pauseAllAttacksWhilePlayerDisabled;
        public bool allowBossToIgnoreCoordinator=true;
        [Min(1)] public int maximumTokenHoldFrames=900;
        [Min(0)] public float waitingPriorityBonus=.1f;
        [Min(0)] public float recentAttackPenalty=25;
        [Min(1)] public int recentAttackPenaltyFrames=180;
        [Min(0)] public float randomPriorityVariation=2;
        public bool showDebug, verboseDebug;
        public int Capacity(AttackTokenCategory category) => Mathf.Max(0,category==AttackTokenCategory.Melee ? maxMeleeAttackers :
            category==AttackTokenCategory.Ranged ? maxRangedAttackers : category==AttackTokenCategory.CrowdControl ? maxCrowdControlAttackers : maxSupportAttackers);
    }
}
