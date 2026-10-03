using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public enum AttackDomain { Ground, Air }
    public enum CombatState { Idle, GroundAttack, Launcher, Jumping, AirAttack, Hitstun }
    public enum CombatInput { None, Attack, Launcher, Jump }
    public enum HitType { Normal, Launcher, AirFinisher }

    [Serializable]
    public sealed class AttackHitboxData
    {
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
        [Tooltip("Launcher: X horizontal, Y upward. AirFinisher: negative Y sets downward speed; 0 uses the enemy's fallback fall speed.")]
        public Vector2 launchVelocity = new Vector2(.7f, 8);
        public HitType hitType;
        public bool canHitGrounded = true;
        public bool canHitAirborne;
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
        [TextArea] public string artworkNotes;
        public List<AttackFrameData> frames = new List<AttackFrameData>();
        public int TotalFrames => frames.Count;
        public int FirstActiveFrame => frames.FindIndex(f => f != null && f.hitboxes.Count > 0);
        public int LastActiveFrame => frames.FindLastIndex(f => f != null && f.hitboxes.Count > 0);
        public int ActiveFrames => frames.FindAll(f => f != null && f.hitboxes.Count > 0).Count;
        private void OnValidate()
        {
            foreach (var frame in frames)
                if (frame != null)
                    foreach (var box in frame.hitboxes)
                        if (box != null) box.size = new Vector2(Mathf.Max(.01f, box.size.x), Mathf.Max(.01f, box.size.y));
        }
    }
}
