using UnityEngine;

namespace BeatEmUp
{
    // Keep the existing enum values stable for serialized/debug references.
    public enum EnemyReaction { Normal, GroundHit, Launched, AirHit, Falling, Landing, Defeated, Knockdown, Downed, GetUp }
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
        [Header("Grounded recovery (existing animation assets)")]
        public AnimationClip knockdownClip;
        public AnimationClip getUpClip;
        public Sprite airborneSprite;
        public Sprite downedSprite;
        [Min(0)] public int knockdownRecoveryDelayFrames = 45;
        [Min(.1f)] public float finisherFallSpeed = 3;
        public EnemyReaction State { get; private set; }
        public int JuggleHits { get; private set; }
        public int RecoveryFrames => recovery;
        public int PhaseFramesRemaining => phaseFrames;
        public int KnockdownFrames => ClipFrames(knockdownClip);
        public int GetUpFrames => ClipFrames(getUpClip);
        public bool IsRecovering => State == EnemyReaction.Knockdown || State == EnemyReaction.Downed || State == EnemyReaction.GetUp;
        public int FrameOrder => 40;
        public bool CanAct => State == EnemyReaction.Normal && !health.IsDead && motor.IsGrounded;
        public bool JuggleOpen => !motor.IsGrounded && !juggleClosed && juggleFrames < maximumJuggleFrames && JuggleHits < maximumJuggleHits;
        private int recovery, juggleFrames, phaseFrames, phaseLength;
        private long phaseStartedTick = -1;
        private bool juggleClosed;
        private AttackPlayer attackPlayer;
        private void Awake() { attackPlayer = GetComponent<AttackPlayer>(); }
        private void OnEnable()
        {
            if (motor) motor.Landed += OnLanding;
            if (health) { health.Died += OnDeath; health.Restored += OnRestore; }
            CombatClock.Register(this);
        }
        private void OnDisable()
        {
            CombatClock.Unregister(this);
            if (health) { health.Died -= OnDeath; health.Restored -= OnRestore; }
            if (motor) { motor.Landed -= OnLanding; motor.GravityOverride = 0; }
            if (animationDriver) animationDriver.ReleaseReactionControl();
        }
        private static int ClipFrames(AnimationClip clip) => Mathf.Max(1, Mathf.CeilToInt((clip ? clip.length : 0) / CombatClock.FrameSeconds - .0001f));
        public void Receive(AttackHitboxData hit, int facing)
        {
            if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>();
            if (health.IsDead) { OnDeath(); return; }
            // Damage remains possible while downed/recovering, but a grounded
            // hit cannot silently replace the recovery with GroundHit/Normal.
            if (IsRecovering) { LockMotion(); return; }
            attackPlayer.Stop();
            animationDriver.ReleaseReactionControl();
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
            LockMotion(); ShowReaction(true);
        }
        private string AnimationState() => State == EnemyReaction.Defeated ? "Defeated" : State.ToString();
        private void CloseJuggle(bool forceFall)
        {
            juggleClosed = true; motor.GravityOverride = 0;
            if (forceFall && !motor.IsGrounded) motor.Fall(finisherFallSpeed);
        }
        private void OnLanding()
        {
            bool wasJuggled = State == EnemyReaction.Launched || State == EnemyReaction.AirHit || State == EnemyReaction.Falling;
            motor.GravityOverride = 0; juggleClosed = false; juggleFrames = 0; JuggleHits = 0;
            if (health.IsDead) { OnDeath(); motor.StopGroundedMotion(); return; }
            if (wasJuggled)
            {
                attackPlayer.Stop(); recovery = 0; motor.StopGroundedMotion();
                BeginPhase(EnemyReaction.Knockdown, KnockdownFrames);
            }
            else
            {
                recovery = Mathf.Max(recovery, landingRecoveryFrames);
                State = EnemyReaction.Landing; LockMotion(); ShowReaction(true);
            }
        }
        private void LockMotion()
        {
            motor.MovementLocked = true; motor.MoveInput = Vector2.zero;
            if (IsRecovering) motor.StopGroundedMotion();
        }
        private void BeginPhase(EnemyReaction state, int frames)
        {
            State = state; phaseFrames = phaseLength = frames;
            phaseStartedTick = CombatClock.IsStepping ? CombatClock.CurrentTick : -1;
            LockMotion(); ShowReaction(true);
            if (state == EnemyReaction.Downed && frames == 0) BeginPhase(EnemyReaction.GetUp, GetUpFrames);
        }
        private void ShowReaction(bool restart = false)
        {
            if (State == EnemyReaction.Downed) animationDriver.HoldSprite(motor.sprite, downedSprite);
            else if (State == EnemyReaction.Knockdown || State == EnemyReaction.GetUp)
                animationDriver.SampleState(State.ToString(), 1f - (float)phaseFrames / Mathf.Max(1, phaseLength));
            else if (State == EnemyReaction.Launched || State == EnemyReaction.AirHit || State == EnemyReaction.Falling)
                animationDriver.HoldSprite(motor.sprite, airborneSprite);
            else animationDriver.Play(AnimationState(), restart);
        }
        private void OnDeath()
        {
            if (State == EnemyReaction.Defeated) return;
            if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>();
            attackPlayer.Stop(); CloseJuggle(true); recovery = phaseFrames = 0;
            State = EnemyReaction.Defeated; LockMotion();
            motor.StopGroundedMotion();
            animationDriver.ReleaseReactionControl(); ShowReaction(true);
        }
        private void OnRestore()
        {
            attackPlayer.Stop(); recovery = phaseFrames = juggleFrames = JuggleHits = 0; juggleClosed = false;
            motor.GravityOverride = 0; State = EnemyReaction.Normal;
            motor.MovementLocked = !motor.IsGrounded; motor.MoveInput = Vector2.zero;
            animationDriver.ReleaseReactionControl(); animationDriver.Play("Idle", true);
        }
        public void CombatFrame()
        {
            if (!motor || !health) return;
            if (health.IsDead) { OnDeath(); return; }
            if (attackPlayer && attackPlayer.IsFrozen) return;
            if (IsRecovering)
            {
                LockMotion();
                if (phaseStartedTick == CombatClock.CurrentTick && CombatClock.IsStepping) return;
                phaseFrames = Mathf.Max(0, phaseFrames - 1);
                ShowReaction();
                if (phaseFrames == 0)
                {
                    if (State == EnemyReaction.Knockdown) BeginPhase(EnemyReaction.Downed, Mathf.Max(0, knockdownRecoveryDelayFrames));
                    else if (State == EnemyReaction.Downed) BeginPhase(EnemyReaction.GetUp, GetUpFrames);
                    else
                    {
                        State = EnemyReaction.Normal; motor.MovementLocked = false;
                        animationDriver.ReleaseReactionControl(); animationDriver.Play("Idle", true);
                    }
                }
                return;
            }
            if (recovery > 0) recovery--;
            if (!motor.IsGrounded)
            {
                juggleFrames++;
                if (juggleFrames >= maximumJuggleFrames || health.IsDead) CloseJuggle(false);
                if (motor.VerticalVelocity < 0 && recovery <= 0) State = health.IsDead ? EnemyReaction.Defeated : EnemyReaction.Falling;
            }
            else if (!health.IsDead && recovery <= 0) State = EnemyReaction.Normal;
            motor.MovementLocked = !CanAct || (attackPlayer && attackPlayer.CurrentAttack);
            if (State != EnemyReaction.Normal) ShowReaction();
        }
    }
}
