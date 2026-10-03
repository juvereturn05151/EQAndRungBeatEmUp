using UnityEngine;

namespace BeatEmUp
{
    public enum EnemyReaction { Normal, GroundHit, Launched, AirHit, Falling, Landing, Defeated }
    [RequireComponent(typeof(AttackPlayer))]
    public sealed class EnemyHitReaction : MonoBehaviour, ICombatFrameListener
    {
        public CharacterMotor motor;
        public CharacterHealth health;
        public CharacterAnimation animationDriver;
        [Min(0)] public float juggleHitLift = 2.2f;
        [Min(.1f)] public float juggleGravity = 9;
        [Min(1)] public int maximumJuggleFrames = 168;
        [Min(0)] public int maximumJuggleHits = 3;
        [Min(0)] public int landingRecoveryFrames = 21;
        [Min(.1f)] public float finisherFallSpeed = 3;
        public EnemyReaction State { get; private set; }
        public int JuggleHits { get; private set; }
        public int RecoveryFrames => recovery;
        public int FrameOrder => 40;
        public bool CanAct => State == EnemyReaction.Normal && !health.IsDead && motor.IsGrounded;
        public bool JuggleOpen => !motor.IsGrounded && !juggleClosed && juggleFrames < maximumJuggleFrames && JuggleHits < maximumJuggleHits;
        private int recovery, juggleFrames;
        private bool juggleClosed;
        private AttackPlayer attackPlayer;
        private void Awake() { attackPlayer = GetComponent<AttackPlayer>(); }
        private void OnEnable() { if (motor) motor.Landed += OnLanding; CombatClock.Register(this); }
        private void OnDisable() { CombatClock.Unregister(this); if (motor) { motor.Landed -= OnLanding; motor.GravityOverride = 0; } }
        public void Receive(AttackHitboxData hit, int facing)
        {
            if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>();
            attackPlayer.Stop();
            recovery = Mathf.Max(recovery, hit.hitstunFrames);
            if (hit.hitType == HitType.Launcher && motor.IsGrounded && !health.IsDead)
            {
                JuggleHits = 0; juggleFrames = 0; juggleClosed = false;
                motor.Launch(hit.launchVelocity.y, facing * hit.launchVelocity.x);
                motor.GravityOverride = juggleGravity; State = EnemyReaction.Launched;
            }
            else if (!motor.IsGrounded)
            {
                motor.AddKnockback(facing * hit.knockback);
                if (JuggleOpen && !health.IsDead)
                {
                    JuggleHits++;
                    if (hit.hitType == HitType.AirFinisher || JuggleHits >= maximumJuggleHits) CloseJuggle(true);
                    else motor.JuggleLift(juggleHitLift);
                }
                // Finishers also drive down enemies whose juggle window has closed.
                if (hit.hitType == HitType.AirFinisher && !health.IsDead)
                {
                    CloseJuggle(true);
                    if (hit.launchVelocity.y < 0) motor.Fall(-hit.launchVelocity.y);
                }
                State = juggleClosed ? EnemyReaction.Falling : EnemyReaction.AirHit;
            }
            else { motor.AddKnockback(facing * hit.knockback); State = EnemyReaction.GroundHit; }
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
            motor.GravityOverride = 0; juggleClosed = false; juggleFrames = 0; JuggleHits = 0;
            recovery = Mathf.Max(recovery, landingRecoveryFrames);
            State = health.IsDead ? EnemyReaction.Defeated : EnemyReaction.Landing;
            animationDriver.Play(AnimationState(), true);
        }
        public void CombatFrame()
        {
            if (!motor || !health || (attackPlayer && attackPlayer.IsFrozen)) return;
            if (recovery > 0) recovery--;
            if (!motor.IsGrounded)
            {
                juggleFrames++;
                if (juggleFrames >= maximumJuggleFrames || health.IsDead) CloseJuggle(false);
                if (motor.VerticalVelocity < 0 && recovery <= 0) State = health.IsDead ? EnemyReaction.Defeated : EnemyReaction.Falling;
            }
            else if (!health.IsDead && recovery <= 0) State = EnemyReaction.Normal;
            motor.MovementLocked = !CanAct || (attackPlayer && attackPlayer.CurrentAttack);
            if (State != EnemyReaction.Normal) animationDriver.Play(AnimationState());
        }
    }
}
