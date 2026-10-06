using UnityEngine;

namespace BeatEmUp
{
    // Status lives in the existing player state machine and uses its defense clock / pose control.
    public sealed partial class ComboController
    {
        public bool IsStunned => State == CombatState.Stunned;
        public SpriteRenderer StunVisual => stunVisual;
        private int statusStunDuration;
        private SpriteRenderer stunVisual;

        public void EnterStun(int frames)
        {
            if (!health || health.IsDead || IsKnockdownState) return;
            int duration = Mathf.Max(1, frames);
            if (IsStunned) duration = Mathf.Max(duration, StunFramesRemaining);
            else duration = Mathf.Max(duration, stun);
            GetComponent<ComboTracker>()?.EndCombo("Player stunned");
            guardHeld = parryArmed = false;
            BeginDefense(CombatState.Stunned);
            statusStunDuration = duration;
            motor.AddKnockback(0);
            motor.AttackHorizontalVelocity = 0;
            motor.AirAttackControl = false;
            motor.StopGroundedMotion();
            ShowStun();
        }

        private void ShowStun()
        {
            if (!defenseData || !motor || !motor.sprite) return;
            var pose = PlayerDefenseData.LoopPose(defenseData.stunned, DefenseFrame);
            if (pose) animationDriver.HoldSprite(motor.sprite, pose);
            else animationDriver.Play("GroundHit");
            var sprites = defenseData.stunVfx;
            if (sprites == null || sprites.Length == 0) return;
            if (!stunVisual)
            {
                var go = new GameObject("Player stun overhead effect");
                go.transform.SetParent(motor.sprite.transform, false);
                stunVisual = go.AddComponent<SpriteRenderer>();
            }
            stunVisual.enabled = true;
            stunVisual.sprite = sprites[DefenseFrame / Mathf.Max(1, defenseData.stunVfxHoldFrames) % sprites.Length];
            stunVisual.transform.localPosition = defenseData.stunVfxOffset;
            stunVisual.transform.localScale = Vector3.one * defenseData.stunVfxScale;
            stunVisual.sortingLayerID = motor.sprite.sortingLayerID;
            stunVisual.sortingOrder = motor.sprite.sortingOrder + 2;
            stunVisual.flipX = motor.Facing < 0;
        }
    }
}
