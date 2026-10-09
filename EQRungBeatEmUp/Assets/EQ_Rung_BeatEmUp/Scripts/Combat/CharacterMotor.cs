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
        [Header("Environment collision (ground/lane XY)")]
        public LayerMask wallCollisionMask = ~0;
        public Vector2 wallCollisionSize = new Vector2(.5f, .3f);
        public Vector2 wallCollisionOffset;
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
        public float GroundSpeedOverride { get; set; }
        public int FrameOrder => 50;
        public event Action Landed;
        public event Action<CombatWall, Vector2, float> WallContact;
        public float HorizontalRecoil => recoil.x;
        private float airControlUsed;
        private Vector2 recoil;
        public void ResetForStage(Vector2 position)
        {
            GetComponent<ComboController>()?.ResetRunInput();
            GetComponent<ComboTracker>()?.ResetTracking();
            if (attackPlayer) attackPlayer.Stop();
            Height = VerticalVelocity = airControlUsed = 0; recoil = MoveInput = DefenseVelocity = Vector2.zero;
            AttackHorizontalVelocity = GravityOverride = 0; FrameGravityScale = 1;
            MovementLocked = AirAttackControl = SuspendFalling = false;
            transform.position = new Vector3(position.x, position.y, 0);
            if (visual) visual.localPosition = Vector3.zero;
        }
        public void Face(float direction)
        {
            if (Mathf.Abs(direction) > .01f) Facing = direction < 0 ? -1 : 1;
            if (sprite) sprite.flipX = Facing < 0;
        }
        // Capture alignment without the stage-reset side effects on combo statistics.
        public void SnapGrabToGround(Vector2 position)
        {
            GetComponent<ComboController>()?.ResetRunInput();
            Height=VerticalVelocity=airControlUsed=0; recoil=DefenseVelocity=Vector2.zero;
            AttackHorizontalVelocity=GravityOverride=0; FrameGravityScale=1; AirAttackControl=SuspendFalling=false;
            transform.position=new Vector3(position.x,position.y,0);
            if(visual) visual.localPosition=Vector3.zero;
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
            GroundSpeedOverride = 0;
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
            transform.position = ResolveWalls(transform.position, position, false);
        }
        public void SetVerticalVelocity(float velocity)
        {
            VerticalVelocity = velocity;
            if (velocity > 0) Height = Mathf.Max(.001f, Height);
        }
        // Authored arcs keep air height separate from walking-lane position.
        public void SetAuthoredHeight(float height)
        {
            Height = Mathf.Max(0, height); VerticalVelocity = 0;
            if (visual) visual.localPosition = new Vector3(0, Height, 0);
        }
        public void Simulate(float dt)
        {
            Vector2 movement = MovementLocked ? Vector2.zero : Vector2.ClampMagnitude(MoveInput, 1);
            var frame = attackPlayer ? attackPlayer.Frame : null;
            if (frame != null) movement *= Mathf.Clamp01(frame.movementInputScale);
            if (movement.x != 0 && (!attackPlayer || !attackPlayer.CurrentAttack)) Face(movement.x);
            float speed = GroundSpeedOverride > 0 && IsGrounded ? GroundSpeedOverride : moveSpeed;
            Vector3 position = transform.position + (Vector3)((movement * speed + recoil + new Vector2(AttackHorizontalVelocity, 0) + DefenseVelocity) * dt);
            position.x = Mathf.Clamp(position.x, arenaMin.x, arenaMax.x);
            position.y = Mathf.Clamp(position.y, arenaMin.y, arenaMax.y);
            recoil = Vector2.MoveTowards(recoil, Vector2.zero, 6 * dt);
            // Decay before reporting contact so a rebound assigned by the listener survives this tick.
            transform.position = ResolveWalls(transform.position, position, true);
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
        public Vector2 ProbeGroundMove(Vector2 displacement)
        {
            var destination=(Vector2)transform.position+displacement;
            destination=new Vector2(Mathf.Clamp(destination.x,arenaMin.x,arenaMax.x),Mathf.Clamp(destination.y,arenaMin.y,arenaMax.y));
            return (Vector2)ResolveWalls(transform.position,destination,false,true)-(Vector2)transform.position;
        }
        public void AddGroundKnockback(Vector2 velocity) { recoil=velocity; }
        private Vector3 ResolveWalls(Vector3 origin, Vector3 destination, bool reportContact,bool probe=false)
        {
            Vector2 delta = destination - origin;
            if (delta.sqrMagnitude < .0000001f) return destination;
            float distance = delta.magnitude;
            var hits = Physics2D.BoxCastAll((Vector2)origin + wallCollisionOffset,
                new Vector2(Mathf.Max(.01f, wallCollisionSize.x), Mathf.Max(.01f, wallCollisionSize.y)),
                0, delta / distance, distance, wallCollisionMask);
            RaycastHit2D nearest = default;
            CombatWall wall = null;
            foreach (var hit in hits)
            {
                if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform)) continue;
                var candidate = hit.collider.GetComponentInParent<CombatWall>();
                if (!candidate || !candidate.isActiveAndEnabled || Vector2.Dot(delta, hit.normal) >= 0) continue;
                if (wall && hit.distance >= nearest.distance) continue;
                nearest = hit; wall = candidate;
            }
            if (!wall) return destination;
            float incoming = delta.x;
            destination = origin + (Vector3)(delta / distance * Mathf.Max(0, nearest.distance - .001f));
            if (Mathf.Abs(nearest.normal.x) > .5f)
            {
                if(!probe)recoil.x = 0;
                if (reportContact) WallContact?.Invoke(wall, nearest.normal, incoming);
            }
            return destination;
        }
    }
}
