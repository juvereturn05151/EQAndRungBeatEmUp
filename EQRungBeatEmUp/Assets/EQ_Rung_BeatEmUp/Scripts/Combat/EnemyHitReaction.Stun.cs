using UnityEngine;

namespace BeatEmUp
{
    // A status branch of the existing reaction state machine, using its clock and pose control.
    public sealed partial class EnemyHitReaction
    {
        [Header("Parry stun (combat frames / sprite timeline)")]
        public bool canBeParryStunned = true;
        public AttackData stunAnimation;
        public Sprite stunFallbackSprite;
        public Sprite[] stunVfx;
        [Min(1)] public int stunVfxHoldFrames = 5;
        public Vector2 stunVfxOffset = new Vector2(0, 1.2f);
        [Min(.01f)] public float stunVfxScale = 1;
        [Min(0)] public int stunRecoveryFrames;
        public bool stunDebug;
        public bool CanBeParryStunned => canBeParryStunned && !GetComponent<TotemBossController>();
        public bool IsStunState => State == EnemyReaction.Stunned || State == EnemyReaction.StunRecovery;
        public int StunRemaining => State == EnemyReaction.Stunned ? recovery : 0;
        public int StunDuration { get; private set; }
        public SpriteRenderer StunVisual => stunVisual;
        int stunFrame;
        long stunStartedTick;
        SpriteRenderer stunVisual;

        public bool EnterStun(int frames)
        {
            if (!health || health.IsDead || !CanBeParryStunned || IsRecovering) return false;
            if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>();
            int remaining = State == EnemyReaction.Stunned ? recovery : 0;
            GetComponent<EnemyCombat>()?.ReleaseCoordination("Stun");
            attackPlayer.Stop(); // Existing Interrupted listeners cancel lunge/leap/grab/telegraph.
            GetComponent<EnemyCombat>()?.AI?.CancelPendingAction();
            ClearBounceEligibility(); ResetComboResources(); animationDriver.ReleaseReactionControl();
            motor.SnapGrabToGround(transform.position);
            recovery = StunDuration = Mathf.Max(1, Mathf.Max(frames, remaining));
            State = EnemyReaction.Stunned; LastHitReaction = State.ToString(); stunFrame = 0;
            stunStartedTick = CombatClock.IsStepping ? CombatClock.CurrentTick : -1;
            LockStunMotion(); ShowStun(); return true;
        }
        void LockStunMotion() { LockMotion(); motor.StopGroundedMotion(); motor.AttackHorizontalVelocity = 0; }
        void ShowStun()
        {
            Sprite pose = stunAnimation && stunAnimation.frames.Count > 0 ? stunAnimation.frames[stunFrame % stunAnimation.frames.Count].sprite : stunFallbackSprite;
            if (!pose) pose = downedSprite ? downedSprite : airborneSprite;
            animationDriver.HoldSprite(motor.sprite, pose ? pose : motor.sprite.sprite);
            if (stunVfx == null || stunVfx.Length == 0) return;
            if (!stunVisual)
            {
                var go = new GameObject("Enemy stun overhead effect"); go.transform.SetParent(motor.sprite.transform, false);
                stunVisual = go.AddComponent<SpriteRenderer>();
            }
            stunVisual.enabled = State == EnemyReaction.Stunned;
            stunVisual.sprite = stunVfx[stunFrame / Mathf.Max(1, stunVfxHoldFrames) % stunVfx.Length];
            stunVisual.transform.localPosition = stunVfxOffset; stunVisual.transform.localScale = Vector3.one * stunVfxScale;
            stunVisual.sortingLayerID = motor.sprite.sortingLayerID; stunVisual.sortingOrder = motor.sprite.sortingOrder + 2;
        }
        void EndStunVisual() { if (stunVisual) stunVisual.enabled = false; }
        void TickStun()
        {
            LockStunMotion();
            if (CombatClock.IsStepping && stunStartedTick == CombatClock.CurrentTick) return;
            stunFrame++; recovery = Mathf.Max(0, recovery - 1); ShowStun();
            if (recovery > 0) return;
            EndStunVisual();
            if (State == EnemyReaction.Stunned && stunRecoveryFrames > 0)
            { State = EnemyReaction.StunRecovery; recovery = stunRecoveryFrames; return; }
            State = EnemyReaction.Normal; motor.MovementLocked = false;
            animationDriver.ReleaseReactionControl(); animationDriver.Play("Idle", true);
        }
        void OnGUI()
        {
            if (!stunDebug) return;
            var camera = Camera.main; if (!camera) return;
            var screen = camera.WorldToScreenPoint(motor.sprite.transform.position + Vector3.up * 1.5f);
            GUI.Label(new Rect(screen.x - 95, Screen.height - screen.y, 250, 60),
                $"{State}\nParry stun: {StunRemaining}/{StunDuration}f\nCan be parry stunned: {CanBeParryStunned}");
        }
    }
}
