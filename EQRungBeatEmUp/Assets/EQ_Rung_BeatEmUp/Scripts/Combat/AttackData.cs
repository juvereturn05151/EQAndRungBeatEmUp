using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public enum AttackDomain { Ground, Air }
    public enum CombatState { Idle, GroundAttack, Launcher, Jumping, AirAttack, Hitstun, Dodge, GuardEnter, GuardHold, Parry, KnockDown, Downed, GetUp, Die }
    public enum CombatInput { None, Attack, Launcher, Jump }
    public enum HitType { Normal, Launcher, AirFinisher, KnockDown }

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
        [Min(.01f)] public float laneTolerance = .55f;
        [Min(0)] public float damage = 10;
        [Min(0)] public int hitstunFrames = 18;
        [Min(0)] public int hitstopFrames = 5;
        public float knockback = .25f;
        [Tooltip("Launcher: X horizontal, Y upward. AirFinisher / Force Airborne Target Downward: negative Y sets downward speed; nonnegative Y uses the enemy's fallback fall speed. Normal horizontal recoil uses Knockback.")]
        public Vector2 launchVelocity = new Vector2(.7f, 8);
        public HitType hitType;
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
        [Min(0)] public float blockDamage;
        [Min(0)] public int blockstunFrames = 10;
    }

    [Serializable]
    public sealed class AttackFeedbackData
    {
        public AudioClip swingSound, impactSound;
        [Range(0, 1)] public float swingVolume = .35f, impactVolume = .55f;
        public GameObject impactPrefab;
        [Min(.01f)] public float impactScale = .15f;
        [Min(.05f)] public float impactLifetime = .45f;
    }

    [Serializable]
    public sealed class AttackFrameData
    {
        public Sprite sprite;
        public List<AttackHitboxData> hitboxes = new List<AttackHitboxData>();
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
        public int TotalFrames => frames.Count;
        public int FirstActiveFrame => frames.FindIndex(f => f != null && f.hitboxes.Count > 0);
        public int LastActiveFrame => frames.FindLastIndex(f => f != null && f.hitboxes.Count > 0);
        public int ActiveFrames => frames.FindAll(f => f != null && f.hitboxes.Count > 0).Count;
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
