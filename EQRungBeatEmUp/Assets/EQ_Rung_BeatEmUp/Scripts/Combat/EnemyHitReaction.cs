using UnityEngine;

namespace BeatEmUp
{
    // Keep the existing enum values stable for serialized/debug references.
    public enum EnemyReaction { Normal, GroundHit, Launched, AirHit, Falling, Landing, Defeated, Knockdown, Downed, GetUp, GroundBounceEligible, GroundBouncing, WallBounceEligible, WallBouncing }
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
        [Header("Per-combo bounce resource caps")]
        [Min(0)] public int maxGroundBounces = 1;
        [Min(0)] public int maxWallBounces = 1;
        public int GroundBouncesUsed { get; private set; }
        public int WallBouncesUsed { get; private set; }
        public bool GroundBounceEligible { get; private set; }
        public bool WallBounceEligible { get; private set; }
        public string LastHitReaction { get; private set; } = "None";
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
        private AttackHitboxData bounceHit;
        private int bounceFacing, bounceRecoveryDelayFrames;
        private bool bouncedAirborne;
        private void Awake() { attackPlayer = GetComponent<AttackPlayer>(); }
        private void OnEnable()
        {
            if (motor) { motor.Landed += OnLanding; motor.WallContact += OnWallContact; }
            if (health) { health.Died += OnDeath; health.Restored += OnRestore; }
            CombatClock.Register(this);
        }
        private void OnDisable()
        {
            CombatClock.Unregister(this);
            if (health) { health.Died -= OnDeath; health.Restored -= OnRestore; }
            if (motor) { motor.Landed -= OnLanding; motor.WallContact -= OnWallContact; motor.GravityOverride = 0; }
            ClearBounceEligibility();
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
            ClearBounceEligibility();
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
                if ((hit.hitType == HitType.AirFinisher || hit.forceAirborneTargetDownward) && !health.IsDead)
                {
                    CloseJuggle(true);
                    if (hit.launchVelocity.y < 0) motor.Fall(-hit.launchVelocity.y);
                }
                State = juggleClosed ? EnemyReaction.Falling : EnemyReaction.AirHit;
            }
            else { motor.AddKnockback(facing * hit.knockback); State = EnemyReaction.GroundHit; }
            // Ground bounce wins when both properties are authored on an airborne hit.
            // Snapshot tunables at impact; later frame edits cannot alter a pending reaction.
            if (!health.IsDead)
            {
                GroundBounceEligible = hit.groundBounce && !motor.IsGrounded && GroundBouncesUsed < Mathf.Min(maxGroundBounces, hit.maximumGroundBounces);
                WallBounceEligible = !GroundBounceEligible && hit.wallBounce && WallBouncesUsed < Mathf.Min(maxWallBounces, hit.maximumWallBounces);
                if (GroundBounceEligible || WallBounceEligible)
                {
                    bounceHit = new AttackHitboxData {
                        groundBounceForce = hit.groundBounceForce, groundBounceGravity = hit.groundBounceGravity,
                        groundBounceRecoveryFrames = hit.groundBounceRecoveryFrames,
                        wallBounceHorizontalForce = hit.wallBounceHorizontalForce, wallBounceVerticalForce = hit.wallBounceVerticalForce,
                        wallBounceHitstunFrames = hit.wallBounceHitstunFrames
                    };
                    bounceFacing = facing;
                    State = GroundBounceEligible ? EnemyReaction.GroundBounceEligible : EnemyReaction.WallBounceEligible;
                }
            }
            if (health.IsDead) { CloseJuggle(true); State = EnemyReaction.Defeated; }
            LastHitReaction = State.ToString();
            LockMotion(); ShowReaction(true);
        }
        private string AnimationState() => State == EnemyReaction.WallBounceEligible && motor.IsGrounded ? "GroundHit" : State.ToString();
        public void InterruptFromParry(int frames)
        {
            if (health.IsDead || IsRecovering) return;
            ClearBounceEligibility();
            attackPlayer.Stop(); animationDriver.ReleaseReactionControl();
            recovery = Mathf.Max(recovery, frames);
            State = motor.IsGrounded ? EnemyReaction.GroundHit : juggleClosed ? EnemyReaction.Falling : EnemyReaction.AirHit;
            LockMotion(); ShowReaction(true);
        }
        private void CloseJuggle(bool forceFall)
        {
            juggleClosed = true; motor.GravityOverride = 0;
            if (forceFall && !motor.IsGrounded) motor.Fall(finisherFallSpeed);
        }
        private void OnLanding()
        {
            if (!health.IsDead && GroundBounceEligible)
            {
                var hit = bounceHit;
                ClearBounceEligibility(); GroundBouncesUsed++;
                StartBounce(EnemyReaction.GroundBouncing, bounceFacing * hit.groundBounceForce.x,
                    hit.groundBounceForce.y, hit.groundBounceGravity, hit.groundBounceRecoveryFrames);
                return; // Floor contact was consumed: do not start landing recovery this tick.
            }
            bool wasJuggled = State == EnemyReaction.Launched || State == EnemyReaction.AirHit || State == EnemyReaction.Falling ||
                State == EnemyReaction.GroundBounceEligible || State == EnemyReaction.WallBounceEligible ||
                State == EnemyReaction.GroundBouncing || State == EnemyReaction.WallBouncing;
            ClearBounceEligibility();
            motor.GravityOverride = 0; juggleClosed = false; juggleFrames = 0; JuggleHits = 0;
            if (health.IsDead) { OnDeath(); motor.StopGroundedMotion(); return; }
            if (wasJuggled || bouncedAirborne)
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
        private void ClearBounceEligibility()
        {
            GroundBounceEligible = WallBounceEligible = false;
            bounceHit = null;
        }
        private void OnWallContact(CombatWall wall, Vector2 normal, float incoming)
        {
            if (!WallBounceEligible || recovery <= 0 || health.IsDead || !wall.allowsBounce ||
                Mathf.Abs(normal.x) < .5f || incoming * normal.x >= 0) return;
            var hit = bounceHit;
            ClearBounceEligibility(); WallBouncesUsed++;
            StartBounce(EnemyReaction.WallBouncing, -Mathf.Sign(incoming) * hit.wallBounceHorizontalForce,
                hit.wallBounceVerticalForce, juggleGravity, hit.wallBounceHitstunFrames);
        }
        private void StartBounce(EnemyReaction state, float horizontal, float upward, float gravity, int frames)
        {
            attackPlayer.Stop();
            motor.Launch(Mathf.Max(.1f, upward), horizontal);
            motor.GravityOverride = Mathf.Max(.1f, gravity);
            juggleClosed = false; juggleFrames = JuggleHits = 0;
            recovery = Mathf.Max(0, frames);
            bounceRecoveryDelayFrames = state == EnemyReaction.GroundBouncing ? Mathf.Max(0, frames) : knockdownRecoveryDelayFrames;
            bouncedAirborne = true; State = state; LastHitReaction = state.ToString();
            LockMotion(); ShowReaction(true);
        }
        private void ResetComboResources()
        {
            ClearBounceEligibility(); GroundBouncesUsed = WallBouncesUsed = 0;
            bouncedAirborne = false; bounceRecoveryDelayFrames = 0;
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
            else if (State != EnemyReaction.Defeated && (!motor.IsGrounded || State == EnemyReaction.Launched || State == EnemyReaction.AirHit || State == EnemyReaction.Falling))
                animationDriver.HoldSprite(motor.sprite, airborneSprite);
            else animationDriver.Play(AnimationState(), restart);
        }
        private void OnDeath()
        {
            if (State == EnemyReaction.Defeated) return;
            if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>();
            attackPlayer.Stop(); CloseJuggle(true); recovery = phaseFrames = 0;
            ClearBounceEligibility();
            State = EnemyReaction.Defeated; LastHitReaction = State.ToString(); LockMotion();
            motor.StopGroundedMotion();
            animationDriver.ReleaseReactionControl(); ShowReaction(true);
        }
        private void OnRestore()
        {
            attackPlayer.Stop(); recovery = phaseFrames = juggleFrames = JuggleHits = 0; juggleClosed = false;
            ResetComboResources(); LastHitReaction = "None";
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
                    if (State == EnemyReaction.Knockdown) BeginPhase(EnemyReaction.Downed, Mathf.Max(0, bouncedAirborne ? bounceRecoveryDelayFrames : knockdownRecoveryDelayFrames));
                    else if (State == EnemyReaction.Downed) BeginPhase(EnemyReaction.GetUp, GetUpFrames);
                    else
                    {
                        State = EnemyReaction.Normal; motor.MovementLocked = false;
                        ResetComboResources();
                        animationDriver.ReleaseReactionControl(); animationDriver.Play("Idle", true);
                    }
                }
                return;
            }
            if (recovery > 0) recovery--;
            if (WallBounceEligible && recovery <= 0) ClearBounceEligibility();
            if (!motor.IsGrounded)
            {
                juggleFrames++;
                if (juggleFrames >= maximumJuggleFrames || health.IsDead) CloseJuggle(false);
                if (motor.VerticalVelocity < 0 && recovery <= 0 && !GroundBounceEligible && !WallBounceEligible) State = EnemyReaction.Falling;
            }
            else if (!health.IsDead && recovery <= 0) { State = EnemyReaction.Normal; ResetComboResources(); }
            motor.MovementLocked = !CanAct || (attackPlayer && attackPlayer.CurrentAttack);
            if (State != EnemyReaction.Normal) ShowReaction();
        }
    }
}
