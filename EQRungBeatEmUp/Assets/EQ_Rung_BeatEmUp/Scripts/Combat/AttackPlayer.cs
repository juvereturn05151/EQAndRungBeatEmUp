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
        public event Action<string> FrameEvent;
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
            if (!attack || attack.frames.Count == 0 || attack.frames[0] == null || IsFrozen) return false;
            var player = GetComponent<ComboController>();
            if (player && (player.IsDefenseState || (player.health && player.health.IsDead))) return false;
            Stop(); CurrentAttack = attack; CurrentFrame = 0; Facing = motor.Facing;
            startedOnTick = CombatClock.IsStepping ? CombatClock.CurrentTick : -1;
            hitbox.Begin(attack); animationDriver.SetAttackOverride(true);
            ApplyFrame(); return true;
        }
        public void Stop()
        {
            CurrentAttack = null; CurrentFrame = -1;
            if (hitbox) hitbox.End();
            if (motor) { motor.FrameGravityScale = 1; motor.SuspendFalling = false; motor.AttackHorizontalVelocity = 0; motor.AirAttackControl = false; }
            if (animationDriver) animationDriver.SetAttackOverride(false);
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
            CurrentFrame++;
            if (CurrentFrame >= CurrentAttack.frames.Count)
            {
                var finished = CurrentAttack; Stop(); Finished?.Invoke(finished); return;
            }
            ApplyFrame();
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
            hitbox.SetFrame(frame, CurrentFrame, Facing);
            hitbox.Sample();
            foreach (string signal in frame.events) FrameEvent?.Invoke(signal);
        }
    }
}
