using System;
using UnityEngine;

namespace BeatEmUp
{
    public sealed class CharacterHealth : MonoBehaviour
    {
        [Min(1)] public float maximumHealth = 200;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0;
        public event Action Died;
        private void Awake() { Current = maximumHealth; }
        public bool Damage(float amount)
        {
            if (IsDead) return false;
            Current = Mathf.Max(0, Current - Mathf.Max(0, amount));
            if (IsDead) Died?.Invoke();
            return true;
        }
        public void Restore() { Current = maximumHealth; }
    }
}
