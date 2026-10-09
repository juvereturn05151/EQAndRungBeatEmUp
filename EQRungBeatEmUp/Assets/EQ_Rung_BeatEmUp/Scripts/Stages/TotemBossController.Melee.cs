using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    public sealed partial class TotemBossController
    {
        readonly Dictionary<AttackData, int> actionCooldowns = new Dictionary<AttackData, int>();
        readonly List<AttackData> cooldownKeys = new List<AttackData>();
        int teleportCooldown, summonCooldown, interruptImmunity, staggerDuration, staggerFrame;
        long staggerStartedTick;
        bool physicalRecovery, facingLocked;
        AttackData staggerReaction;
        public int InterruptImmunityRemaining => interruptImmunity;
        public int StaggerRemaining => State == BossEncounterState.Stagger ? remaining : 0;
        public int PhysicalInterrupts { get; private set; }
        public int PhysicalParries { get; private set; }
        public float TargetDistance => combat && combat.target ? Vector2.Distance(transform.position, combat.target.position) : float.PositiveInfinity;
        static bool IsPhysical(BossActionChoice choice) => choice != null && (choice.action == BossAction.Swipe || choice.action == BossAction.TelegraphMelee);
        static bool IsSummon(BossActionChoice choice) => choice != null && (choice.action == BossAction.SummonRusher || choice.action == BossAction.SummonStrongGhosts);
        public float EffectiveWeight(BossActionChoice choice)
        {
            if (choice == null) return 0;
            return Mathf.Max(0, data && data.useDistanceWeights && TargetDistance <= data.closeRange && choice.closeRangeWeight >= 0 ? choice.closeRangeWeight : choice.weight);
        }
        public int CooldownRemaining(BossActionChoice choice)
        {
            if (choice == null) return 0;
            if (choice.action == BossAction.Teleport) return teleportCooldown;
            int cooldown = choice.attack && actionCooldowns.TryGetValue(choice.attack, out int value) ? value : 0;
            return IsSummon(choice) ? Mathf.Max(cooldown, summonCooldown) : cooldown;
        }
        void ConsumeCooldown(BossActionChoice choice)
        {
            if (choice.action == BossAction.Teleport) { teleportCooldown = Mathf.Max(0, data.teleportCooldownFrames); return; }
            int duration = Mathf.Max(0, choice.attack.cooldownFrames);
            actionCooldowns[choice.attack] = duration;
            if (IsSummon(choice)) summonCooldown = Mathf.Max(summonCooldown, duration);
        }
        void TickActionCooldowns()
        {
            cooldownKeys.Clear(); cooldownKeys.AddRange(actionCooldowns.Keys);
            foreach (var key in cooldownKeys) actionCooldowns[key] = Mathf.Max(0, actionCooldowns[key] - 1);
            teleportCooldown = Mathf.Max(0, teleportCooldown - 1); summonCooldown = Mathf.Max(0, summonCooldown - 1);
            interruptImmunity = Mathf.Max(0, interruptImmunity - 1);
        }
        void TrackMeleeFacing()
        {
            if (State != BossEncounterState.Acting || Selected?.action != BossAction.TelegraphMelee || !combat.attackPlayer.CurrentAttack || facingLocked) return;
            int lockFrame = Mathf.Min(Mathf.Max(0, data.meleeInterrupt.facingLockFrame), Mathf.Max(0, Selected.attack.FirstActiveFrame));
            if (combat.attackPlayer.CurrentFrame >= lockFrame) { facingLocked = true; return; }
            if (combat.target) combat.attackPlayer.CommitFacing(combat.target.position.x < transform.position.x ? -1 : 1);
        }
        // Called only after the existing shield, defense and damage gates accept a hit.
        // A physical commitment/punish window consumes ordinary reaction handling so
        // hits cannot stop active frames or reset the stagger timer indefinitely.
        public bool HandlePhysicalHit(AttackHitboxData hit, CharacterMotor attacker, CombatProjectile projectile)
        {
            if (!OwnsAI || health.IsDead || !physicalRecovery) return false;
            var options = data.meleeInterrupt;
            int frame = combat.attackPlayer.CurrentFrame;
            if (State == BossEncounterState.Acting && Selected?.action == BossAction.TelegraphMelee && !Invulnerable && interruptImmunity == 0 &&
                frame >= options.firstFrame && frame <= options.lastFrame && frame < Selected.attack.FirstActiveFrame &&
                hit.damage >= options.minimumDamage && attacker && attacker.GetComponent<ComboController>() &&
                (projectile == null || options.allowProjectileHits) && (((int)options.hitTypes & (1 << (int)hit.hitType)) != 0))
                InterruptPhysical(false);
            return true;
        }
        public bool InterruptPhysicalFromParry()
        {
            if (!OwnsAI || health.IsDead || State != BossEncounterState.Acting || !IsPhysical(Selected) || !combat.attackPlayer.CurrentAttack) return false;
            // A parry cancels the physical move even while shielded. It never opens
            // the totem damage gate or changes vulnerability/phase state.
            InterruptPhysical(true); return true;
        }
        void InterruptPhysical(bool parried)
        {
            PreviousAction = Selected; Selected = null;
            combat.attackPlayer.Stop(); combat.ReleaseCoordination("Boss physical interrupt");
            combat.motor.StopGroundedMotion(); combat.animationDriver.ReleaseReactionControl();
            var options = data.meleeInterrupt;
            staggerReaction = parried ? options.parryReaction : options.hitReaction;
            staggerDuration = Mathf.Max(1, parried ? options.parryStaggerFrames : options.hitStaggerFrames);
            staggerFrame = 0; staggerStartedTick = CombatClock.CurrentTick;
            interruptImmunity = Mathf.Max(interruptImmunity, options.interruptImmunityFrames);
            physicalRecovery = true; Enter(BossEncounterState.Stagger, staggerDuration);
            if (parried) PhysicalParries++; else PhysicalInterrupts++;
            Feedback(options.interruptFeedback); ShowPhysicalStagger();
        }
        void ShowPhysicalStagger()
        {
            if (staggerReaction && staggerReaction.TotalFrames > 0)
            {
                int frame = Mathf.Min(staggerReaction.TotalFrames - 1, staggerFrame * staggerReaction.TotalFrames / Mathf.Max(1, staggerDuration));
                combat.animationDriver.HoldSprite(combat.motor.sprite, staggerReaction.frames[frame].sprite);
            }
            else combat.animationDriver.SampleState("Hurt_Heavy", staggerFrame / (float)Mathf.Max(1, staggerDuration));
        }
        void TickPhysicalStagger()
        {
            combat.motor.StopGroundedMotion();
            if (CombatClock.IsStepping && staggerStartedTick == CombatClock.CurrentTick) return;
            staggerFrame++; remaining = Mathf.Max(0, remaining - 1); ShowPhysicalStagger();
            if (remaining > 0) return;
            combat.animationDriver.ReleaseReactionControl(); combat.animationDriver.Play("Recovery_Rise", true);
            Enter(BossEncounterState.Recovery, data.warpCooldownFrames);
        }
        void ResetMeleeRuntime()
        {
            actionCooldowns.Clear(); cooldownKeys.Clear(); teleportCooldown = summonCooldown = interruptImmunity = 0;
            physicalRecovery = facingLocked = false; staggerReaction = null;
            if (combat && combat.animationDriver) combat.animationDriver.ReleaseReactionControl();
        }
    }
}
