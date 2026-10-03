using UnityEngine;

namespace BeatEmUp
{
    public enum EnemyReaction { Normal, GroundHit, Launched, AirHit, Falling, Landing, Defeated }
    [DefaultExecutionOrder(-10)]
    public sealed class EnemyHitReaction : MonoBehaviour
    {
        public CharacterMotor motor;
        public CharacterHealth health;
        public CharacterAnimation animationDriver;
        [Header("Launch / juggle")]
        [Min(.1f)] public float launchForce = 8;
        [Min(0)] public float launchHorizontalForce = .7f;
        [Min(0)] public float juggleHitLift = 2.2f;
        [Min(.1f)] public float juggleGravity = 9;
        [Min(.1f)] public float maximumJuggleTime = 2.8f;
        [Min(0)] public int maximumJuggleHits = 3;
        [Min(0)] public float landingRecovery = .35f;
        [Min(.1f)] public float finisherFallSpeed = 3;
        public EnemyReaction State { get; private set; }
        public int JuggleHits { get; private set; }
        public bool CanAct => State == EnemyReaction.Normal && !health.IsDead && motor.IsGrounded;
        public bool JuggleOpen => !motor.IsGrounded && !juggleClosed && juggleTime < maximumJuggleTime && JuggleHits < maximumJuggleHits;
        private float recovery, juggleTime;
        private bool juggleClosed;
        private void OnEnable() { if (motor) motor.Landed += OnLanding; }
        private void OnDisable() { if (motor) { motor.Landed -= OnLanding; motor.GravityOverride = 0; } }
        public void Receive(AttackData attack, int facing)
        {
            recovery = Mathf.Max(recovery, attack.hitstun);
            if (attack.canLaunch && motor.IsGrounded && !health.IsDead)
            {
                JuggleHits = 0; juggleTime = 0; juggleClosed = false;
                motor.Launch(attack.launchForce > 0 ? attack.launchForce : launchForce,
                    facing * (attack.launchHorizontalForce > 0 ? attack.launchHorizontalForce : launchHorizontalForce));
                motor.GravityOverride = juggleGravity; State = EnemyReaction.Launched;
            }
            else if (!motor.IsGrounded)
            {
                if (JuggleOpen && !health.IsDead)
                {
                    JuggleHits++;
                    if (attack.endsJuggle || JuggleHits >= maximumJuggleHits) CloseJuggle(true);
                    else motor.JuggleLift(juggleHitLift);
                }
                State = juggleClosed ? EnemyReaction.Falling : EnemyReaction.AirHit;
            }
            else { motor.AddKnockback(facing * attack.knockback); State = EnemyReaction.GroundHit; }
            if (health.IsDead) { CloseJuggle(true); State = EnemyReaction.Defeated; }
            motor.MovementLocked = true; animationDriver.Play(AnimationState(), true);
        }
        private string AnimationState() => State == EnemyReaction.Defeated ? "Defeated" : State.ToString();
        private void CloseJuggle(bool forceFall)
        {
            juggleClosed = true; motor.GravityOverride = 0;
            if (forceFall && !motor.IsGrounded) motor.Fall(finisherFallSpeed);
        }
        private void OnLanding()
        {
            motor.GravityOverride = 0; juggleClosed = false; juggleTime = 0; JuggleHits = 0;
            recovery = Mathf.Max(recovery, landingRecovery);
            State = health.IsDead ? EnemyReaction.Defeated : EnemyReaction.Landing;
            animationDriver.Play(AnimationState(), true);
        }
        private void Update() { Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            recovery = Mathf.Max(0, recovery - dt);
            if (!motor.IsGrounded)
            {
                juggleTime += dt;
                if (juggleTime >= maximumJuggleTime || health.IsDead) CloseJuggle(false);
                if (motor.VerticalVelocity < 0 && recovery <= 0) State = health.IsDead ? EnemyReaction.Defeated : EnemyReaction.Falling;
            }
            else if (!health.IsDead && recovery <= 0) State = EnemyReaction.Normal;
            motor.MovementLocked = !CanAct;
            if (State != EnemyReaction.Normal) animationDriver.Play(AnimationState());
        }
    }
}
