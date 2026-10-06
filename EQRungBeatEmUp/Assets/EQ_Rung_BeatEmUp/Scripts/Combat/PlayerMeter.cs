using System;
using UnityEngine;

namespace BeatEmUp
{
    [DisallowMultipleComponent, RequireComponent(typeof(ComboTracker), typeof(ComboController))]
    public sealed class PlayerMeter : MonoBehaviour
    {
        [Min(.01f)] public float maxMeter = 1;
        [Min(0)] public float startingMeter = 1;
        [Min(0)] public float gainOnHit = .06f;
        [Min(0), Tooltip("Additional gain for subsequent hits in the current combo.")] public float multiHitBonus;
        [Min(0)] public float launcherBonus = .02f, airHitBonus = .02f;
        [Min(0)] public float gainOnParry = .4f;
        public float CurrentMeter { get; private set; }
        public float MaxMeter => Mathf.Max(.01f, maxMeter);
        public float Normalized => Mathf.Clamp01(CurrentMeter / MaxMeter);
        public event Action Changed;
        ComboTracker tracker;
        ComboController combat;
        CharacterHealth health;
        void Awake() => ResetMeter();
        void OnEnable()
        {
            tracker = GetComponent<ComboTracker>(); combat = GetComponent<ComboController>(); health = GetComponent<CharacterHealth>();
            tracker.AcceptedHit += Hit; combat.DefenseImpact += Defense;
            if (health) health.Restored += ResetMeter;
        }
        void OnDisable()
        {
            if (tracker) tracker.AcceptedHit -= Hit;
            if (combat) combat.DefenseImpact -= Defense;
            if (health) health.Restored -= ResetMeter;
        }
        void Hit(CombatHurtbox target, AttackHitboxData hit)
        {
            float gain = gainOnHit + (tracker.HitCount > 1 ? multiHitBonus : 0);
            if (hit != null && hit.hitType == HitType.Launcher) gain += launcherBonus;
            if (combat.motor && !combat.motor.IsGrounded) gain += airHitBonus;
            Add(gain*(GetComponent<MetaProgress>()?.MeterMultiplier ?? 1));
        }
        void Defense(DefenseFeedback outcome) { if (outcome == DefenseFeedback.Parry) Add(gainOnParry*(GetComponent<MetaProgress>()?.MeterMultiplier ?? 1)); }
        public void ResetMeter() { CurrentMeter = Mathf.Clamp(startingMeter, 0, MaxMeter); Changed?.Invoke(); }
        public void Add(float amount)
        {
            if (CombatClock.IsPaused || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0 || (health && health.IsDead)) return;
            CurrentMeter = Mathf.Clamp(CurrentMeter + amount, 0, MaxMeter); Changed?.Invoke();
        }
        public bool CanSpend(float cost) => !float.IsNaN(cost) && !float.IsInfinity(cost) && cost >= 0 && CurrentMeter >= cost;
        public bool TrySpend(float cost)
        {
            if (CombatClock.IsPaused || !CanSpend(cost)) return false;
            CurrentMeter = Mathf.Clamp(CurrentMeter - cost, 0, MaxMeter); Changed?.Invoke(); return true;
        }
    }
}
