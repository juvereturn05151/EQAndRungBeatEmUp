using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public enum AttackDomain { Ground, Air }
    public enum CombatState { Idle, GroundAttack, Launcher, Jumping, AirAttack, Hitstun, Dodge, GuardEnter, GuardHold, Parry, KnockDown, Downed, GetUp, Die, Stunned, Grabbed }
    public enum CombatInput { None, Attack, Launcher, Jump }
    public enum HitType { Normal, Launcher, AirFinisher, KnockDown, Stun }

    [Serializable]
    public sealed class AttackHitboxData
    {
        public AttackHitboxData RuntimeCopy() => (AttackHitboxData)MemberwiseClone();
        [Tooltip("Same ID across frames shares hit history. Different IDs allow intentional multiple hits.")]
        public int hitId;
        [Min(0), Tooltip("0 = once per attack and hit ID. Otherwise minimum combat frames between hits.")]
        public int repeatAfterFrames;
        public Vector2 offset = new Vector2(.55f, .55f);
        public Vector2 size = new Vector2(1, 1.1f);
        [Tooltip("Ground-plane elliptical area. Size is full width/depth; Offset is ground XY. Jump height does not change range. Uses the same accepted-hit history as other hitboxes.")]
        public bool groundArea;
        [Min(.01f)] public float laneTolerance = .55f;
        [Min(0)] public float damage = 10;
        [Min(0)] public int hitstunFrames = 18;
        [Min(0)] public int hitstopFrames = 5;
        public float knockback = .25f;
        [Tooltip("For normal hits, recoil on the XY walking plane away from the attacker.")] public bool outwardGroundKnockback;
        [Tooltip("Launcher: X horizontal, Y upward. AirFinisher / Force Airborne Target Downward: negative Y sets downward speed; nonnegative Y uses the enemy's fallback fall speed. Normal horizontal recoil uses Knockback.")]
        public Vector2 launchVelocity = new Vector2(.7f, 8);
        public HitType hitType;
        [Min(1), Tooltip("Stun status duration in combat frames. Separate from normal Hitstun Frames; used when Hit Type is Stun.")]
        public int stunDurationFrames = 90;
        [Min(-1), Tooltip("Player downed hold after landing / knockdown animation. -1 uses Player Defense Data.")]
        public int knockdownDurationFrames = -1;
        [Header("Downward slam (airborne targets)")]
        public bool forceAirborneTargetDownward;
        [Header("Ground bounce")]
        public bool groundBounce;
        [Tooltip("X follows attack facing; Y is height velocity, not walking lane.")]
        public Vector2 groundBounceForce = new Vector2(.6f, 4.5f);
        [Min(.1f)] public float groundBounceGravity = 18;
        [Min(0), Tooltip("Bounce hitstun and downed delay after the final landing's Knockdown animation; then GetUp returns the enemy to neutral.")]
        public int groundBounceRecoveryFrames = 21;
        [Min(0)] public int maximumGroundBounces = 1;
        [Header("Wall bounce")]
        public bool wallBounce;
        [Min(0)] public float wallBounceHorizontalForce = 4;
        [Min(.1f)] public float wallBounceVerticalForce = 4;
        [Min(0)] public int wallBounceHitstunFrames = 24;
        [Min(0)] public int maximumWallBounces = 1;
        public bool canHitGrounded = true;
        public bool canHitAirborne;
        [Header("Guard response")]
        public bool unblockable;
        [Tooltip("Blockable attacks may opt out of parry while retaining normal guard/chip rules. Unblockable attacks bypass both.")]
        public bool canBeParried = true;
        [Min(0)] public float blockDamage;
        [Min(0)] public int blockstunFrames = 10;
    }

    [Serializable]
    public sealed class AttackFeedbackData
    {
        [Header("Area warning / active wave (uses first / last hitbox frames)")]
        public bool areaWarning;
        [Tooltip("Forward corridor telegraph from EnemyProjectileAttack; projectile owns active collision and visuals.")]
        public bool directionalWaveWarning;
        [Tooltip("Optional full ring sprite. Empty uses a point-filtered pixel ring. Cosmetic only.")]
        public Sprite areaRingSprite;
        [Tooltip("Optional existing pixel wave sequence, mirrored around the area origin. Runs over the active combat frames.")]
        public Sprite[] areaWaveSprites = new Sprite[0];
        public Color warningColor = new Color(1, .35f, .1f, .8f);
        public Color waveColor = new Color(1, .2f, .3f, .9f);
        public AudioClip telegraphSound, screamSound;
        [Range(0, 1)] public float areaVolume = .6f;
        public AudioClip swingSound, impactSound;
        [Range(0, 1)] public float swingVolume = .35f, impactVolume = .55f;
        public GameObject impactPrefab;
        [Tooltip("Rotate the effect 180 degrees when facing left. Disable for upright, surrounding area effects.")]
        public bool impactRotateWithFacing = true;
        [Min(.01f)] public float impactScale = .15f;
        [Min(.05f)] public float impactLifetime = .45f;
    }

    [Serializable]
    public sealed class GrabHitboxData
    {
        [Tooltip("Active front-facing parry interrupts the grab; normal Guard does not block grabs.")]
        public bool canBeParried = true;
        [Tooltip("Ground-plane offset. X mirrors with committed facing; Y is walking lane.")]
        public Vector2 offset = new Vector2(.65f, 0);
        [Min(.01f)] public float width = 1.2f, depth = .8f;
        public bool canGrabGrounded = true, canGrabAirborne;
        [Min(0)] public float maximumHeightDifference = .75f;
    }

    [Serializable]
    public sealed class AttackFrameData
    {
        public Sprite sprite;
        public List<AttackHitboxData> hitboxes = new List<AttackHitboxData>();
        public List<GrabHitboxData> grabHitboxes = new List<GrabHitboxData>();
        [Min(0), Tooltip("Committed ground-plane grab movement multiplier. 0 = stationary; CombatGrabController supplies speed and locked direction.")]
        public float grabLungeMovementScale;
        [Tooltip("Displacement once on entry: X forward, Y walking lane. Mirrored by facing.")]
        public Vector2 movement;
        [Range(0, 1), Tooltip("Scale voluntary movement input during this frame. 1 preserves full movement; does not change recoil or authored velocity.")]
        public float movementInputScale = 1;
        public bool setHorizontalVelocity;
        public float horizontalVelocity;
        public bool setVerticalVelocity;
        public float verticalVelocity;
        public float verticalVelocityModifier;
        [Min(0)] public float gravityScale = 1;
        public bool suspendFalling;
        public bool canCancelIntoAttack;
        public bool canCancelIntoLauncher;
        public bool canCancelIntoJump;
        public bool invulnerable;
        public bool superArmor;
        [Tooltip("Optional named signals raised once on entry; never Animation Events.")]
        public List<string> events = new List<string>();
    }

    [CreateAssetMenu(menuName = "Beat Em Up/Frame Attack")]
    public sealed class AttackData : ScriptableObject
    {
        public string attackName = "New Attack";
        public AttackDomain domain;
        public bool isLauncher;
        [Min(0)] public int cooldownFrames;
        [Header("Optional airborne landing transition")]
        public bool requiresAirborne;
        [Tooltip("-1 disables landing branching. Ground contact jumps here; the remaining frames are landing recovery.")]
        public int landingFrame = -1;
        [Tooltip("Hold this earlier travel frame while airborne at Landing Frame. -1 disables the hold.")]
        public int airborneHoldFrame = -1;
        [TextArea] public string artworkNotes;
        [Header("Combat feedback (Swing frame event + confirmed hits)")]
        public AttackFeedbackData feedback = new AttackFeedbackData();
        public List<AttackFrameData> frames = new List<AttackFrameData>();
        [Header("Optional projectile phase display")]
        [Tooltip("Named release event used for the active phase; it does not add melee collision.")]
        public string activeEvent;
        [Min(0)] public int eventActiveFrames;
        public bool cooldownOnInterrupt;
        public int TotalFrames => frames.Count;
        public int FirstActiveFrame => !string.IsNullOrEmpty(activeEvent) && eventActiveFrames > 0 ? frames.FindIndex(f => f != null && f.events.Contains(activeEvent)) : frames.FindIndex(f => f != null && f.hitboxes.Count > 0);
        public int LastActiveFrame => !string.IsNullOrEmpty(activeEvent) && eventActiveFrames > 0 && FirstActiveFrame >= 0 ? Mathf.Min(frames.Count - 1, FirstActiveFrame + eventActiveFrames - 1) : frames.FindLastIndex(f => f != null && f.hitboxes.Count > 0);
        public int ActiveFrames => !string.IsNullOrEmpty(activeEvent) && eventActiveFrames > 0 && FirstActiveFrame >= 0 ? LastActiveFrame - FirstActiveFrame + 1 : frames.FindAll(f => f != null && f.hitboxes.Count > 0).Count;
        public string Phase(int frame) => frame < FirstActiveFrame ? "Telegraph" : frame <= LastActiveFrame ? "Scream Active" : "Recovery";
        private void OnValidate()
        {
            foreach (var frame in frames) 
            {
                if (frame != null) 
                {
                    foreach (var box in frame.hitboxes) 
                    {
                        if (box != null) box.size = new Vector2(Mathf.Max(.01f, box.size.x), Mathf.Max(.01f, box.size.y));
                    }
                }
            }
                
        }
    }
}
