using System;
using UnityEngine;

namespace BeatEmUp
{
    // Extends the player's existing state machine, not a second component.
    public sealed partial class ComboController
    {
        public int DefenseFrame { get; private set; }
        public int BlockstunFrames => blockstun;
        public bool GuardHeld => guardHeld;
        public bool IsKnockdownState => State == CombatState.KnockDown || State == CombatState.Downed || State == CombatState.GetUp;
        public bool IsDefenseState => (int)State >= (int)CombatState.Dodge;
        public bool DodgeInvulnerable => defenseData && State == CombatState.Dodge && DefenseFrame >= defenseData.dodgeInvulnerableFirstFrame && DefenseFrame <= defenseData.dodgeInvulnerableLastFrame;
        public event Action<DefenseFeedback> DefenseImpact;
        private bool guardHeld, parryArmed;
        private int blockstun;
        private Vector2 dodgeDirection;
        private long defenseStartedTick = -1;
        private bool CanStartDefense => !CombatClock.IsPaused && defenseData && health && !health.IsDead && motor.IsGrounded && !CurrentAttack && stun <= 0 && cooldown <= 0 && !attackPlayer.IsFrozen;
        public int EffectiveParryWindow => defenseData ? Mathf.Max(1, defenseData.parryWindowFrames + Mathf.RoundToInt(Build?.Value(RunModifier.ParryWindowBonus) ?? 0)) : 0;
        public int EffectiveDodgeFrames => defenseData ? Mathf.Max(defenseData.dodgeMoveLastFrame + 1, defenseData.dodgeInvulnerableLastFrame + 1, defenseData.dodgeTotalFrames - Mathf.RoundToInt(Build?.Value(RunModifier.DodgeRecoveryReduction) ?? 0)) : 0;

        public bool RequestDodge()
        {
            if (!CanStartDefense || IsDefenseState) return false;
            dodgeDirection = motor.MoveInput.sqrMagnitude > .01f ? motor.MoveInput.normalized : new Vector2(-motor.Facing, 0);
            guardHeld = false; BeginDefense(CombatState.Dodge); motor.StopGroundedMotion(); Build?.DodgeStarted();
            ShowDefense(); return true;
        }
        public void RequestGuard(bool held)
        {
            if (!held)
            {
                guardHeld = false; parryArmed = false;
                if ((State == CombatState.GuardEnter || State == CombatState.GuardHold) && blockstun <= 0) EndDefense();
                return;
            }
            if (guardHeld || !CanStartDefense || (IsDefenseState && State != CombatState.GuardEnter && State != CombatState.GuardHold)) return;
            guardHeld = true;
            if (blockstun > 0) return;
            BeginGuard(true);
        }
        private void BeginGuard(bool freshPress)
        {
            BeginDefense(freshPress ? CombatState.GuardEnter : CombatState.GuardHold);
            parryArmed = freshPress;
            if (!freshPress) DefenseFrame = EffectiveParryWindow;
            motor.StopGroundedMotion(); ShowDefense();
        }
        private void BeginDefense(CombatState state)
        {
            ResetCombo(); ClearDefenseControl(); stun = cooldown = blockstun = 0;
            State = state; DefenseFrame = 0;
            defenseStartedTick = CombatClock.IsStepping ? CombatClock.CurrentTick : -1;
            motor.MovementLocked = true;
        }
        private void ClearDefenseControl()
        {
            if (motor) motor.DefenseVelocity = Vector2.zero;
            if (animationDriver) animationDriver.ReleaseReactionControl();
        }
        private void EndDefense()
        {
            ClearDefenseControl(); State = motor.IsGrounded ? CombatState.Idle : CombatState.Jumping;
            motor.MovementLocked = false; DefenseFrame = blockstun = 0; parryArmed = false;
            animationDriver.Play(motor.IsGrounded ? "Idle" : "Jumping", true);
        }
        public void PrepareDefenseFrame()
        {
            if (!IsDefenseState || !defenseData || attackPlayer.IsFrozen || defenseStartedTick == CombatClock.CurrentTick) return;
            if ((State == CombatState.KnockDown || State == CombatState.Die) && !motor.IsGrounded) return;
            // Evaluated before offensive hitboxes, so immunity and parry windows
            // correspond to the same logical frame as the incoming attack.
            if (State != CombatState.Downed && State != CombatState.Die || DefenseFrame < StateDuration()) DefenseFrame++;
            if (blockstun > 0) blockstun--;
            if (State == CombatState.GuardEnter && DefenseFrame >= EffectiveParryWindow) { State = CombatState.GuardHold; parryArmed = false; }
            if (State == CombatState.Dodge && DefenseFrame >= EffectiveDodgeFrames) EndDefense();
        }
        public CombatHitOutcome TryDefense(AttackHitboxData hit, int facing, CharacterMotor attacker)
        {
            if (!defenseData || health.IsDead || DodgeInvulnerable || IsKnockdownState || State == CombatState.Die) return CombatHitOutcome.None;
            if (State != CombatState.GuardEnter && State != CombatState.GuardHold) return CombatHitOutcome.None;
            float direction = attacker ? attacker.transform.position.x - motor.transform.position.x : -facing;
            if (Mathf.Abs(direction) < .001f) direction = -facing;
            if (hit.unblockable || direction * motor.Facing < 0) return CombatHitOutcome.None;
            if (parryArmed && DefenseFrame < EffectiveParryWindow && blockstun == 0)
            {
                BeginDefense(CombatState.Parry); parryArmed = false;
                if (attacker)
                {
                    var reaction = attacker.GetComponent<EnemyHitReaction>();
                    int punish = defenseData.parryAttackerStunFrames + Mathf.RoundToInt(Build?.Value(RunModifier.ParryStunBonus) ?? 0);
                    if (reaction) reaction.InterruptFromParry(punish);
                    else { var otherPlayer = attacker.GetComponent<ComboController>(); if (otherPlayer) otherPlayer.Interrupt(punish); }
                }
                Build?.OnParry();
                ShowDefense(); DefenseImpact?.Invoke(DefenseFeedback.Parry); return CombatHitOutcome.Parry;
            }
            parryArmed = false; State = CombatState.GuardHold;
            blockstun = Mathf.Max(blockstun, Mathf.RoundToInt(hit.blockstunFrames * Mathf.Clamp(1 - (Build?.Value(RunModifier.BlockstunReduction) ?? 0), .1f, 1)));
            if (hit.blockDamage > 0) health.Damage(hit.blockDamage);
            if (!health.IsDead) { ShowDefense(); Build?.OnBlock(); DefenseImpact?.Invoke(DefenseFeedback.Block); }
            return CombatHitOutcome.Block;
        }
        public void ReceiveHit(AttackHitboxData hit, int facing)
        {
            if (health.IsDead) { EnterDie(); return; }
            if (IsKnockdownState) return;
            if (hit.hitType == HitType.Launcher || hit.hitType == HitType.AirFinisher || hit.hitType == HitType.KnockDown)
            {
                guardHeld = false; BeginDefense(CombatState.KnockDown);
                motor.AddKnockback(facing * hit.knockback);
                if (hit.hitType == HitType.Launcher) motor.Launch(hit.launchVelocity.y, facing * hit.launchVelocity.x);
                else if (hit.hitType == HitType.AirFinisher && !motor.IsGrounded) motor.Fall(hit.launchVelocity.y < 0 ? -hit.launchVelocity.y : 3);
                ShowDefense(); return;
            }
            Interrupt(hit.hitstunFrames); motor.AddKnockback(facing * hit.knockback);
        }
        private void DefenseLanded()
        {
            motor.StopGroundedMotion(); DefenseFrame = 0;
            defenseStartedTick = CombatClock.IsStepping ? CombatClock.CurrentTick : -1;
            ShowDefense();
        }
        private void EnterDie()
        {
            if (State == CombatState.Die) return;
            bool alreadyDown = IsKnockdownState && motor.IsGrounded;
            guardHeld = parryArmed = false; BeginDefense(CombatState.Die);
            if (alreadyDown && defenseData) DefenseFrame = defenseData.dieFrames;
            if (motor.IsGrounded) motor.StopGroundedMotion(); else motor.Fall(3);
            ShowDefense();
        }
        private void RestorePlayer()
        {
            AirDiveUsed = false;
            guardHeld = parryArmed = false; stun = cooldown = blockstun = 0;
            ResetCombo(); EndDefense(); motor.StopGroundedMotion();
        }
        private int StateDuration()
        {
            switch (State)
            {
                case CombatState.Dodge: return EffectiveDodgeFrames;
                case CombatState.Parry: return defenseData.parryRecoveryFrames;
                case CombatState.KnockDown: return defenseData.knockdownFrames;
                case CombatState.Downed: return defenseData.downedFrames;
                case CombatState.GetUp: return defenseData.getUpFrames;
                case CombatState.Die: return defenseData.dieFrames;
                default: return int.MaxValue;
            }
        }
        private bool UpdateDefense()
        {
            if (!IsDefenseState) return false;
            motor.MovementLocked = true; motor.DefenseVelocity = Vector2.zero;
            if (!defenseData) return true;
            if (State == CombatState.Dodge)
            {
                if (DefenseFrame >= defenseData.dodgeMoveFirstFrame && DefenseFrame <= defenseData.dodgeMoveLastFrame) motor.DefenseVelocity = dodgeDirection * defenseData.dodgeSpeed * (1 + (Build?.Value(RunModifier.DodgeDistanceBonus) ?? 0));
            }
            else if ((State == CombatState.GuardEnter || State == CombatState.GuardHold) && !guardHeld && blockstun <= 0) { EndDefense(); return true; }
            else if (State != CombatState.Die && State != CombatState.GuardEnter && State != CombatState.GuardHold && DefenseFrame >= StateDuration() && motor.IsGrounded)
            {
                if (State == CombatState.KnockDown) { BeginDefense(CombatState.Downed); motor.StopGroundedMotion(); }
                else if (State == CombatState.Downed) BeginDefense(CombatState.GetUp);
                else { EndDefense(); if (guardHeld) BeginGuard(false); return true; }
            }
            if (State == CombatState.Downed || State == CombatState.GetUp || State == CombatState.GuardEnter || State == CombatState.GuardHold) motor.StopGroundedMotion();
            ShowDefense(); return true;
        }
        private void ShowDefense()
        {
            if (!defenseData) return;
            CharacterPoseHold[] poses;
            switch (State)
            {
                case CombatState.Dodge: poses = defenseData.dodge; break;
                case CombatState.GuardEnter:
                case CombatState.GuardHold: poses = defenseData.guard; break;
                case CombatState.Parry: poses = defenseData.parry; break;
                case CombatState.GetUp: poses = defenseData.getUp; break;
                case CombatState.Die: poses = defenseData.die; break;
                default: poses = defenseData.knockdown; break;
            }
            int frame = State == CombatState.Downed ? int.MaxValue : DefenseFrame;
            if ((State == CombatState.KnockDown || State == CombatState.Die) && !motor.IsGrounded) frame = 5;
            var pose = blockstun > 0 && State == CombatState.GuardHold ? defenseData.blockPose : PlayerDefenseData.Pose(poses, frame);
            animationDriver.HoldSprite(motor.sprite, pose);
        }
    }
}
