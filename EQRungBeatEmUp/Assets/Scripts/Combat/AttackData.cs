using UnityEngine;

namespace BeatEmUp
{
    public enum AttackDomain { Ground, Air }
    public enum CombatState { Idle, GroundAttack, Launcher, Jumping, AirAttack, Hitstun }
    public enum CombatInput { None, Attack, Launcher, Jump }

    [CreateAssetMenu(menuName = "Beat Em Up/Attack")]
    public sealed class AttackData : ScriptableObject
    {
        [Header("Identity / animation")]
        public string animationState = "Punch1";
        public AttackDomain domain;
        public bool canLaunch;
        public bool canHitAirborne;
        public bool endsJuggle;
        
        [Header("Damage / reaction")]
        [Min(0)] public float damage = 10;
        [Min(0)] public float hitstun = .3f;
        [Min(0)] public float knockback = .35f;
        [Tooltip("Zero uses the victim's launch setting.")]
        [Min(0)] public float launchForce;
        [Min(0)] public float launchHorizontalForce;
        [Header("Hitbox (relative to feet, mirrored by facing)")]
        public Vector2 hitboxPosition = new Vector2(.65f, .8f);
        public Vector2 hitboxSize = new Vector2(1.2f, 1.4f);
        [Min(.01f)] public float laneTolerance = .55f;
        [Header("Timing in seconds, independent of clip length")]
        [Min(0)] public float startup = .09f;
        [Min(.01f)] public float activeDuration = .12f;
        [Min(0)] public float recovery = .22f;
        [Min(0)] public float comboWindowOpen = .18f;
        [Min(.01f)] public float comboInputWindow = .27f;
        [Tooltip("Optional extra time after recovery before the next attack.")]
        [Min(0)] public float cooldown;
        public float Duration => startup + activeDuration + recovery;
        public float WindowEnd => Mathf.Min(Duration, comboWindowOpen + comboInputWindow);
        private void OnValidate()
        {
            hitboxSize = new Vector2(Mathf.Max(.01f, hitboxSize.x), Mathf.Max(.01f, hitboxSize.y));
            comboWindowOpen = Mathf.Clamp(comboWindowOpen, 0, Duration);
        }
    }
}
