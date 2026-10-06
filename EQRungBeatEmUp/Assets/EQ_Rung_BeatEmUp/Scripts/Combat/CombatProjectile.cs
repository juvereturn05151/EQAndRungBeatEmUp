using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    // Swept queries use the same CombatHurtbox.Receive path as frame hitboxes.
    public sealed partial class CombatProjectile : MonoBehaviour, ICombatFrameListener
    {
        [Min(.1f)] public float speed = 5.5f;
        [Min(1)] public int lifetimeFrames = 120;
        [Min(.01f)] public float collisionRadius = .14f;
        public LayerMask collisionLayers = ~0;
        [Tooltip("Aim in walking-lane XY and keep the release height constant. Ignores the target's jump height; retains normal circular projectile collision.")]
        public bool groundPlaneFlight;
        public Vector2 GroundPosition => groundPosition;
        public float FlightHeight => height;
        [Header("Optional directional ground wave")]
        [Tooltip("Uses a swept rectangle on ground X / walking-lane Y, independent of jump height. No circular query.")]
        public bool groundWave;
        [Tooltip("Full width along travel X and full walking-lane depth Y.")]
        public Vector2 waveSize = new Vector2(1.2f, 1.2f);
        [Min(0), Tooltip("0 disables distance limit. Measured from spawn, in world units.")] public float maximumTravelDistance;
        public SpriteRenderer[] trailVisuals;
        public AttackHitboxData hit = new AttackHitboxData { damage = 4, hitstunFrames = 14, hitstopFrames = 3, knockback = 1, canHitAirborne = true };
        public SpriteRenderer visual;
        [Header("Parry deflection (parry permission lives in Hit)")]
        public bool canBeDeflected;
        [Min(0)] public int maxDeflections = 1;
        public bool canHitOriginalOwner = true;
        [Min(0)] public float deflectDamageMultiplier = 1, deflectSpeedMultiplier = 1.2f;
        [Min(0)] public int deflectHitstunFrames = 18;
        public float deflectKnockback = 1;
        public AttackData deflectFeedback;
        public bool debugDraw;
        public CharacterMotor Owner => owner;
        public PlayerSkillData skillSource;
        public int Faction => team;
        public int DeflectionCount { get; private set; }
        public bool CanDeflect => initialized && hit.canBeParried && !hit.unblockable && canBeDeflected && DeflectionCount < maxDeflections && !Resolved;
        public Sprite[] flightSprites;
        [Min(1)] public int spriteHoldFrames = 3;
        public Sprite[] emergenceSprites, dissipateSprites;
        [Min(1)] public int emergenceHoldFrames = 3, dissipateHoldFrames = 3;
        [Min(0), Tooltip("0 = unlimited targets. 1 preserves notebook behavior.")] public int maximumTargets = 1;
        [Min(0), Tooltip("0 = once per target per projectile; otherwise minimum frames between intentional repeated hits.")] public int repeatHitFrames;
        public bool collideWithScenery = true;
        public AttackData feedbackAttack;
        public static event Action<CombatProjectile, Vector2> Impact;
        public static event Action<CombatProjectile, Vector2> Deflected;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEvents() { Impact = null; Deflected = null; }
        public int AcceptedHits { get; private set; }
        public int Age => age;
        public float DistanceTraveled { get; private set; }
        public Vector2 Velocity { get; private set; }
        public bool Resolved { get; private set; }
        CharacterMotor owner;
        CharacterMotor originalOwner;
        Color flightColor;
        int deflectFlash;
        Vector2 groundPosition;
        float height, heightVelocity;
        int team, facing, age;
        bool initialized;
        Vector2 launchGround;
        float launchSourceX;
        int endAge, freezeFrames;
        readonly Dictionary<CharacterHealth, int> hitHistory = new Dictionary<CharacterHealth, int>();
        public int FrameOrder => 60;
        void OnEnable() => CombatClock.Register(this);
        void OnDisable() => CombatClock.Unregister(this);
        public void Initialize(CharacterMotor source, int sourceTeam, Vector2 origin, float releaseHeight, Transform target)
        {
            var targetMotor = target.GetComponentInParent<CharacterMotor>();
            InitializeAtPoint(source, sourceTeam, origin, releaseHeight, targetMotor ? (Vector2)targetMotor.transform.position : (Vector2)target.position, targetMotor ? targetMotor.Height : 0);
        }
        public void InitializeAtPoint(CharacterMotor source, int sourceTeam, Vector2 origin, float releaseHeight, Vector2 aimPoint, float aimHeight)
        {
            owner = source; team = sourceTeam; groundPosition = origin; height = releaseHeight; age = 0;
            Vector2 delta = aimPoint - origin;
            Velocity = (delta.sqrMagnitude > .0001f ? delta.normalized : Vector2.right * source.Facing) * speed;
            facing = Velocity.x < 0 ? -1 : 1;
            float flightTime = Mathf.Max(.05f, delta.magnitude / speed);
            heightVelocity = groundPlaneFlight ? 0 : (aimHeight + .65f - height) / flightTime;
            Begin();
            if (visual) visual.flipX = facing < 0;
        }
        public void InitializeForward(CharacterMotor source, int sourceTeam, Vector2 origin, float releaseHeight, int direction)
        {
            owner = source; team = sourceTeam; groundPosition = origin; height = releaseHeight; heightVelocity = 0;
            facing = direction < 0 ? -1 : 1; Velocity = Vector2.right * facing * speed; Begin();
            if (visual) visual.flipX = facing < 0;
        }
        void Begin()
        {
            originalOwner = owner; DeflectionCount = 0; deflectFlash = 0; flightColor = visual ? visual.color : Color.white;
            age = endAge = freezeFrames = AcceptedHits = 0; hitHistory.Clear();
            launchGround = groundPosition; launchSourceX = owner.transform.position.x; DistanceTraveled = 0;
            initialized = true; Resolved = false; Place(); Animate();
        }
        void Animate()
        {
            if (!visual) return;
            if (Resolved && dissipateSprites != null && dissipateSprites.Length > 0)
                visual.sprite = dissipateSprites[Mathf.Min(dissipateSprites.Length - 1, endAge / Mathf.Max(1, dissipateHoldFrames))];
            else if (emergenceSprites != null && age < emergenceSprites.Length * Mathf.Max(1, emergenceHoldFrames))
                visual.sprite = emergenceSprites[age / Mathf.Max(1, emergenceHoldFrames)];
            else if (flightSprites != null && flightSprites.Length > 0)
                visual.sprite = flightSprites[(age / Mathf.Max(1, spriteHoldFrames)) % flightSprites.Length];
            if (trailVisuals != null) foreach (var trail in trailVisuals) if (trail) trail.sprite = visual.sprite;
            if (groundWave)
            {
                int count = 1 + (trailVisuals?.Length ?? 0);
                FitWaveVisual(visual, 0, count);
                if (trailVisuals != null) for (int i = 0; i < trailVisuals.Length; i++) FitWaveVisual(trailVisuals[i], i + 1, count);
            }
        }
        void FitWaveVisual(SpriteRenderer renderer, int index, int count)
        {
            if (!renderer || !renderer.sprite || renderer.transform == transform) return;
            var bounds = renderer.sprite.bounds.size;
            renderer.transform.localPosition = new Vector3(waveSize.x * (.5f - (index + .5f) / count) * .875f, 0, 0);
            renderer.transform.localScale = new Vector3(waveSize.x / count * 1.125f / bounds.x, waveSize.y / bounds.y, 1);
        }
        void Place()
        {
            transform.position = new Vector3(groundPosition.x, groundPosition.y + height, 0);
            if (visual) visual.sortingOrder = Mathf.RoundToInt(-groundPosition.y * 100) + 2;
            if (trailVisuals != null) foreach (var trail in trailVisuals) if (trail) trail.sortingOrder = visual ? visual.sortingOrder : 2;
        }
        public void CombatFrame()
        {
            if (!initialized || CombatClock.IsPaused) return;
            if (Resolved)
            {
                if (++endAge >= (dissipateSprites?.Length ?? 0) * Mathf.Max(1, dissipateHoldFrames)) DestroyNow();
                else Animate();
                return;
            }
            if (freezeFrames > 0) { freezeFrames--; return; }
            if (deflectFlash > 0 && --deflectFlash == 0 && visual) visual.color = flightColor;
            if (++age > lifetimeFrames || !owner || height < 0) { Despawn(); return; }
            var previous = (Vector2)transform.position;
            var previousGround = groundPosition;
            var movement = Velocity * CombatClock.FrameSeconds;
            if (maximumTravelDistance > 0) movement = Vector2.ClampMagnitude(movement, Mathf.Max(0, maximumTravelDistance - DistanceTraveled));
            groundPosition += movement; DistanceTraveled += movement.magnitude;
            height += heightVelocity * CombatClock.FrameSeconds; Place();
            Animate();
            Physics2D.SyncTransforms();
            if (groundWave)
            {
                SampleGroundWave(previousGround);
                if (!Resolved && maximumTravelDistance > 0 && DistanceTraveled >= maximumTravelDistance - .00001f) Despawn();
                return;
            }
            Vector2 travel = (Vector2)transform.position - previous;
            foreach (var contact in Physics2D.CircleCastAll(previous, collisionRadius, travel.normalized, travel.magnitude, collisionLayers))
            {
                var collider = contact.collider;
                if (!collider || collider.transform.IsChildOf(transform) || collider.transform.IsChildOf(owner.transform) ||
                    !canHitOriginalOwner && DeflectionCount > 0 && originalOwner && collider.transform.IsChildOf(originalOwner.transform)) continue;
                var hurtbox = collider.GetComponent<CombatHurtbox>();
                if (hurtbox)
                {
                    if (hurtbox.team == team || !hurtbox.motor || !hurtbox.health || Mathf.Abs(hurtbox.motor.transform.position.y - groundPosition.y) > hit.laneTolerance) continue;
                    if (hitHistory.TryGetValue(hurtbox.health, out int lastHit) && (repeatHitFrames == 0 || age - lastHit < repeatHitFrames)) continue;
                    int beforeDeflections = DeflectionCount;
                    if (!hurtbox.Receive(ImpactHit(), facing, owner, this)) continue;
                    if (hurtbox.LastHitOutcome == CombatHitOutcome.Parry && DeflectionCount != beforeDeflections)
                    { hurtbox.motor.attackPlayer?.Freeze(hurtbox.LastHitstopFrames); freezeFrames = hurtbox.LastHitstopFrames; return; }
                    hitHistory[hurtbox.health] = age; AcceptedHits++;
                    hurtbox.motor.attackPlayer?.Freeze(hurtbox.LastHitstopFrames);
                    if (hurtbox.LastHitOutcome != CombatHitOutcome.Parry) owner.attackPlayer?.Freeze(hurtbox.LastHitstopFrames);
                    freezeFrames = Mathf.Max(freezeFrames, hurtbox.LastHitstopFrames);
                    if (hurtbox.LastHitOutcome == CombatHitOutcome.Hit && feedbackAttack)
                    {
                        AttackFeedback.PlayRemote(feedbackAttack, contact.point, facing, true);
                        Impact?.Invoke(this, contact.point);
                    }
                    if (hurtbox.LastHitOutcome != CombatHitOutcome.Hit || maximumTargets > 0 && AcceptedHits >= maximumTargets) { Despawn(); return; }
                    continue;
                }
                if (collideWithScenery && !collider.isTrigger && !collider.GetComponentInParent<CharacterMotor>()) { Despawn(); return; }
            }
        }
        void SampleGroundWave(Vector2 previous)
        {
            float halfWidth = Mathf.Max(.01f, waveSize.x) * .5f, halfDepth = Mathf.Max(.01f, waveSize.y) * .5f;
            float left = Mathf.Min(previous.x, groundPosition.x) - halfWidth, right = Mathf.Max(previous.x, groundPosition.x) + halfWidth;
            foreach (var hurtbox in FindObjectsByType<CombatHurtbox>(FindObjectsSortMode.None))
            {
                if (!hurtbox || !hurtbox.isActiveAndEnabled || hurtbox.team == team || !hurtbox.motor || !hurtbox.health || hurtbox.motor == owner) continue;
                if (!canHitOriginalOwner && DeflectionCount > 0 && hurtbox.motor == originalOwner) continue;
                var collider = hurtbox.GetComponent<Collider2D>();
                if (!collider || !collider.enabled || (collisionLayers.value & (1 << hurtbox.gameObject.layer)) == 0) continue;
                var point = (Vector2)hurtbox.motor.transform.position;
                // Never threaten the launch source's rear, even with an oversized authored volume.
                if ((point.x - launchSourceX) * facing <= 0 || point.x < left || point.x > right || Mathf.Abs(point.y - launchGround.y) > halfDepth) continue;
                if (hitHistory.TryGetValue(hurtbox.health, out int lastHit) && (repeatHitFrames == 0 || age - lastHit < repeatHitFrames)) continue;
                var incoming = ImpactHit(); incoming.laneTolerance = halfDepth;
                int beforeDeflections = DeflectionCount;
                if (!hurtbox.Receive(incoming, facing, owner, this)) continue;
                if (hurtbox.LastHitOutcome == CombatHitOutcome.Parry && DeflectionCount != beforeDeflections)
                { hurtbox.motor.attackPlayer?.Freeze(hurtbox.LastHitstopFrames); freezeFrames = hurtbox.LastHitstopFrames; return; }
                hitHistory[hurtbox.health] = age; AcceptedHits++;
                hurtbox.motor.attackPlayer?.Freeze(hurtbox.LastHitstopFrames); if (hurtbox.LastHitOutcome != CombatHitOutcome.Parry) owner.attackPlayer?.Freeze(hurtbox.LastHitstopFrames);
                freezeFrames = Mathf.Max(freezeFrames, hurtbox.LastHitstopFrames);
                if (hurtbox.LastHitOutcome == CombatHitOutcome.Hit && feedbackAttack)
                { AttackFeedback.PlayRemote(feedbackAttack, (Vector2)hurtbox.motor.transform.position + Vector2.up * .6f, facing, true); Impact?.Invoke(this, point); }
                if (maximumTargets > 0 && AcceptedHits >= maximumTargets) { Despawn(); return; }
            }
        }
        void Despawn()
        {
            Resolved = true; endAge = 0;
            if (dissipateSprites != null && dissipateSprites.Length > 0) { Animate(); return; }
            DestroyNow();
        }
        void DestroyNow()
        {
            initialized = false;
            // Deactivate immediately so repeated ticks/colliders cannot damage twice.
            gameObject.SetActive(false); Destroy(gameObject);
        }
    }
}
