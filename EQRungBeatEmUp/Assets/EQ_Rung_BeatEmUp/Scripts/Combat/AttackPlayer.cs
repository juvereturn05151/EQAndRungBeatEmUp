using System;
using UnityEngine;

namespace BeatEmUp
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMotor), typeof(AttackHitbox), typeof(CharacterAnimation))]
    public sealed class AttackPlayer : MonoBehaviour, ICombatFrameListener
    {
        public CharacterMotor motor;
        public AttackHitbox hitbox;
        public CharacterAnimation animationDriver;
        public AttackData CurrentAttack { get; private set; }
        public int CurrentFrame { get; private set; } = -1;
        public int Facing { get; private set; } = 1;
        public AttackFrameData Frame => CurrentAttack && CurrentFrame >= 0 && CurrentFrame < CurrentAttack.frames.Count ? CurrentAttack.frames[CurrentFrame] : null;
        public int HitstopRemaining { get; private set; }
        public bool FrozenThisFrame { get; private set; }
        public bool IsFrozen => FrozenThisFrame || HitstopRemaining > 0;
        public int FrameOrder => 20;
        public event Action<AttackData> Finished;
        public event Action<AttackData> Started;
        public event Action<AttackData> Interrupted;
        public event Action<AttackData> Stopped;
        public event Action<string> FrameEvent;
        public event Action FrameApplied;
        private long startedOnTick = -1;
        private void Awake()
        {
            if (!motor) motor = GetComponent<CharacterMotor>();
            if (!hitbox) hitbox = GetComponent<AttackHitbox>();
            if (!animationDriver) animationDriver = GetComponent<CharacterAnimation>();
            motor.attackPlayer = this; hitbox.owner = this;
        }
        private void OnEnable() => CombatClock.Register(this);
        private void OnDisable() { Stop(); HitstopRemaining = 0; CombatClock.Unregister(this); if (animationDriver) animationDriver.SetFrozen(false); }
        public bool Play(AttackData attack)
        {
            if (CombatClock.IsPaused || !attack || attack.frames.Count == 0 || attack.frames[0] == null || IsFrozen) return false;
            if (attack.requiresAirborne && motor.IsGrounded) return false;
            var enemy = GetComponent<EnemyHitReaction>();
            if (enemy && (enemy.IsStunState || enemy.health && enemy.health.IsDead)) return false;
            var player = GetComponent<ComboController>();
            if (player && (player.IsDefenseState || (player.health && player.health.IsDead))) return false;
            Stop(); CurrentAttack = attack; CurrentFrame = 0; Facing = motor.Facing;
            startedOnTick = CombatClock.IsStepping ? CombatClock.CurrentTick : -1;
            hitbox.Begin(attack); animationDriver.SetAttackOverride(true);
            player?.Build?.AttackStarted(attack);
            // Attach only for configured attacks; existing scene/prefab actors need no rebuild.
            if (attack.feedback != null && (attack.feedback.areaWarning || attack.feedback.directionalWaveWarning || attack.feedback.telegraphSound || attack.feedback.screamSound || attack.feedback.swingPrefab || attack.feedback.swingSound || attack.feedback.impactSound || attack.feedback.impactPrefab || attack.feedback.diveStartPrefab || attack.feedback.landingPrefab || attack.feedback.landingSound)
                && !GetComponent<AttackFeedback>()) gameObject.AddComponent<AttackFeedback>();
            Started?.Invoke(attack);
            ApplyFrame(); return true;
        }
        public void Stop()
        {
            var interrupted = CurrentAttack;
            StopTimeline();
            if (interrupted) Interrupted?.Invoke(interrupted);
        }
        private void StopTimeline()
        {
            var stopped = CurrentAttack;
            GetComponent<RunBuildState>()?.AttackStopped();
            CurrentAttack = null; CurrentFrame = -1;
            if (hitbox) hitbox.End();
            if (motor) { motor.FrameGravityScale = 1; motor.SuspendFalling = false; motor.AttackHorizontalVelocity = 0; motor.AirAttackControl = false; }
            if (animationDriver) animationDriver.SetAttackOverride(false);
            if (stopped) Stopped?.Invoke(stopped);
        }
        public void Freeze(int frames)
        {
            HitstopRemaining = Mathf.Max(HitstopRemaining, frames);
            if (animationDriver) animationDriver.SetFrozen(IsFrozen);
        }
        public void PrepareFrame() { FrozenThisFrame = HitstopRemaining > 0; }
        public void EndClockFrame()
        {
            if (FrozenThisFrame) HitstopRemaining = Mathf.Max(0, HitstopRemaining - 1);
            FrozenThisFrame = false;
            if (animationDriver) animationDriver.SetFrozen(HitstopRemaining > 0);
        }
        public void CombatFrame()
        {
            if (IsFrozen || !CurrentAttack || startedOnTick == CombatClock.CurrentTick) return;
            if (!motor.IsGrounded && CurrentAttack.landingFrame > 0 && CurrentFrame + 1 >= CurrentAttack.landingFrame &&
                CurrentAttack.airborneHoldFrame >= 0 && CurrentAttack.airborneHoldFrame < CurrentAttack.landingFrame)
            {
                // A hold must not replay frame-entry movement, velocity or events.
                if (CurrentFrame != CurrentAttack.airborneHoldFrame) { CurrentFrame = CurrentAttack.airborneHoldFrame; ApplyFrame(); }
                else hitbox.Sample();
                return;
            }
            int increment = 1;
            var combo = GetComponent<ComboController>();
            if (combo && combo.IsAirDiving && motor.IsGrounded && CurrentFrame >= CurrentAttack.landingFrame + 2)
            {
                int reduction = Mathf.Clamp(Mathf.RoundToInt(combo.Build?.Value(RunModifier.DiveRecoveryReduction) ?? 0), 0, Mathf.Max(0, CurrentAttack.TotalFrames - CurrentAttack.landingFrame - 5));
                // Skip only the tail of recovery, preserving impact poses and at least two recovery frames.
                if (CurrentFrame + 1 >= CurrentAttack.TotalFrames - reduction) increment += reduction;
            }
            CurrentFrame += increment;
            if (CurrentFrame >= CurrentAttack.frames.Count)
            {
                var finished = CurrentAttack; StopTimeline(); Finished?.Invoke(finished); return;
            }
            ApplyFrame();
        }
        public bool BeginLanding()
        {
            if (!CurrentAttack || !motor.IsGrounded || CurrentAttack.landingFrame < 0 || CurrentAttack.landingFrame >= CurrentAttack.frames.Count) return false;
            return SeekFrame(CurrentAttack.landingFrame, true);
        }
        public void CommitFacing(int direction)
        {
            Facing=direction<0 ? -1 : 1; motor.Face(Facing);
            if(Frame!=null) hitbox.SetFrame(Frame,CurrentFrame,Facing);
        }
        public bool SeekFrame(int index, bool stopGroundedMotion=false)
        {
            if(!CurrentAttack || index<0 || index>=CurrentAttack.TotalFrames) return false;
            CurrentFrame=index; startedOnTick=CombatClock.IsStepping ? CombatClock.CurrentTick : -1;
            if(stopGroundedMotion) motor.StopGroundedMotion(); ApplyFrame(); return true;
        }
        private void ApplyFrame()
        {
            var frame = Frame;
            if (frame == null) { Stop(); Debug.LogError("Null attack frame", this); return; }
            if (motor.sprite) { motor.sprite.sprite = frame.sprite; motor.sprite.flipX = Facing < 0; }
            motor.MoveAttack(frame.movement, Facing);
            motor.AttackHorizontalVelocity = frame.setHorizontalVelocity ? frame.horizontalVelocity * Facing : 0;
            if (frame.setVerticalVelocity) motor.SetVerticalVelocity(frame.verticalVelocity);
            if (frame.verticalVelocityModifier != 0) motor.SetVerticalVelocity(motor.VerticalVelocity + frame.verticalVelocityModifier);
            motor.FrameGravityScale = frame.gravityScale; motor.SuspendFalling = frame.suspendFalling;
            FrameApplied?.Invoke();
            hitbox.SetFrame(frame, CurrentFrame, Facing);
            hitbox.Sample();
            foreach (string signal in frame.events) FrameEvent?.Invoke(signal);
        }
    }
}
