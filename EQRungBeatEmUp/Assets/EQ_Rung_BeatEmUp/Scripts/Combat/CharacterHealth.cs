using System;
using UnityEngine;

namespace BeatEmUp
{
    public sealed class CharacterHealth : MonoBehaviour
    {
        [Min(1)] public float maximumHealth = 200;
        public float Current { get; private set; }
        public float EffectiveMaximum => maximumHealth + (GetComponent<RunBuildState>()?.Value(RunModifier.MaximumHealthBonus) ?? 0) + (GetComponent<MetaProgress>()?.HealthBonus ?? 0);
        public bool IsDead => Current <= 0;
        // Set by the stage owner at entry; covers combat and direct hazard damage.
        public bool SafeStageProtection { get; set; }
        public bool BossDamageProtection { get; set; }
        // Prologue's short first boss encounter must never become a false victory.
        public float StoryMinimumHealth { get; set; }
        public event Action Damaged;
        public event Action Died;
        public event Action Restored;
        private void Awake() { Current = maximumHealth; }
        public bool Damage(float amount)
        {
            if (IsDead || SafeStageProtection || BossDamageProtection) return false;
            float previous = Current;
            Current = Mathf.Max(Mathf.Clamp(StoryMinimumHealth,0,Current), Current - Mathf.Max(0, amount)*(1-(GetComponent<MetaProgress>()?.DamageReduction ?? 0)));
            if (Current <= 0 && GetComponent<RunBuildState>()?.TrySaveLethalHit() == true) Current = 1;
            if (Current < previous) Damaged?.Invoke();
            if (IsDead) Died?.Invoke();
            return true;
        }
        public void Heal(float amount) { if (!IsDead) Current = Mathf.Min(EffectiveMaximum, Current + Mathf.Max(0, amount)); }
        public void ClampToMaximum() => Current = Mathf.Min(Current, EffectiveMaximum);
        public void Restore() { Current = EffectiveMaximum; Restored?.Invoke(); }
    }
}
