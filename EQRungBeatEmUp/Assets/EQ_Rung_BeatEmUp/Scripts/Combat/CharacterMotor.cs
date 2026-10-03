using System;
using UnityEngine;

namespace BeatEmUp
{
    // Ground XY is the walking lane; height is separate so jumping never changes lane.
    public sealed class CharacterMotor : MonoBehaviour, ICombatFrameListener
    {
        [Header("References")]
        public Transform visual;
        public SpriteRenderer sprite;
        [Header("Movement / normal jump")]
        [Min(0)] public float moveSpeed = 3.5f;
        [Min(.1f)] public float jumpForce = 7.5f;
        [Min(.1f)] public float gravity = 14;
        public Vector2 arenaMin = new Vector2(-6, -2);
        public Vector2 arenaMax = new Vector2(6, 1);
        [Header("Air attack control (bounded per jump)")]
        [Range(.05f, 1)] public float airAttackGravityScale = .2f;
        [Min(.1f)] public float airAttackMaxFallSpeed = 1.2f;
        [Min(0)] public float maximumAirControlTime = 1.1f;
        public float Height { get; private set; }
        public float VerticalVelocity { get; private set; }
        public bool IsGrounded => Height <= 0 && VerticalVelocity <= 0;
        public int Facing { get; private set; } = 1;
        public Vector2 MoveInput { get; set; }
        public bool MovementLocked { get; set; }
        public bool AirAttackControl { get; set; }
        public float GravityOverride { get; set; }
        public AttackPlayer attackPlayer;
        public float FrameGravityScale { get; set; } = 1;
        public bool SuspendFalling { get; set; }
        public float AttackHorizontalVelocity { get; set; }
        public Vector2 DefenseVelocity { get; set; }
        public int FrameOrder => 50;
        public event Action Landed;
        private float airControlUsed;
        private Vector2 recoil;
        public void Face(float direction)
        {
            if (Mathf.Abs(direction) > .01f) Facing = direction < 0 ? -1 : 1;
            if (sprite) sprite.flipX = Facing < 0;
        }
        public bool Jump()
        {
            if (!IsGrounded) return false;
            Launch(jumpForce, 0);
            return true;
        }
        public void Launch(float upward, float horizontal)
        {
            VerticalVelocity = Mathf.Max(.1f, upward);
            Height = Mathf.Max(Height, .001f);
            recoil.x = horizontal;
            airControlUsed = 0;
        }
        public void AddKnockback(float horizontal) { recoil.x = horizontal; }
        // End airborne recoil at floor contact without changing lane/collider setup.
        public void StopGroundedMotion()
        {
            if (!IsGrounded) return;
            recoil = Vector2.zero; MoveInput = Vector2.zero;
            AttackHorizontalVelocity = 0; VerticalVelocity = 0;
        }
        public void JuggleLift(float lift) { VerticalVelocity = Mathf.Max(VerticalVelocity, lift); }
        public void Fall(float speed) { VerticalVelocity = -Mathf.Abs(speed); }
        private void OnEnable()
        {
            CombatClock.Register(this);
            if (StageFraming.Active) StageFraming.Active.Register(this);
        }
        private void OnDisable() => CombatClock.Unregister(this);
        public void CombatFrame() { if (!attackPlayer || !attackPlayer.IsFrozen) Simulate(CombatClock.FrameSeconds); }
        public void MoveAttack(Vector2 displacement, int facing)
        {
            var position = transform.position;
            position.x = Mathf.Clamp(position.x + displacement.x * facing, arenaMin.x, arenaMax.x);
            position.y = Mathf.Clamp(position.y + displacement.y, arenaMin.y, arenaMax.y);
            transform.position = position;
        }
        public void SetVerticalVelocity(float velocity)
        {
            VerticalVelocity = velocity;
            if (velocity > 0) Height = Mathf.Max(.001f, Height);
        }
        public void Simulate(float dt)
        {
            Vector2 movement = MovementLocked ? Vector2.zero : Vector2.ClampMagnitude(MoveInput, 1);
            var frame = attackPlayer ? attackPlayer.Frame : null;
            if (frame != null) movement *= Mathf.Clamp01(frame.movementInputScale);
            if (movement.x != 0 && (!attackPlayer || !attackPlayer.CurrentAttack)) Face(movement.x);
            Vector3 position = transform.position + (Vector3)((movement * moveSpeed + recoil + new Vector2(AttackHorizontalVelocity, 0) + DefenseVelocity) * dt);
            position.x = Mathf.Clamp(position.x, arenaMin.x, arenaMax.x);
            position.y = Mathf.Clamp(position.y, arenaMin.y, arenaMax.y);
            transform.position = position;
            recoil = Vector2.MoveTowards(recoil, Vector2.zero, 6 * dt);
            if (!IsGrounded)
            {
                float g = GravityOverride > 0 ? GravityOverride : gravity;
                bool controlled = AirAttackControl && airControlUsed < maximumAirControlTime;
                if (controlled) { g *= airAttackGravityScale; airControlUsed += dt; }
                g *= FrameGravityScale;
                VerticalVelocity -= g * dt;
                if (SuspendFalling && VerticalVelocity < 0) VerticalVelocity = 0;
                if (controlled) VerticalVelocity = Mathf.Max(VerticalVelocity, -airAttackMaxFallSpeed);
                Height += VerticalVelocity * dt;
                if (Height <= 0)
                {
                    Height = 0; VerticalVelocity = 0; airControlUsed = 0;
                    Landed?.Invoke();
                }
            }
            if (visual) visual.localPosition = new Vector3(0, Height, 0);
            if (sprite) sprite.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100);
        }
    }
}
