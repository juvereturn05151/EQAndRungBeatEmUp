using UnityEngine;

namespace BeatEmUp
{
    public sealed class CharacterAnimation : MonoBehaviour
    {
        public Animator animator;
        private string current;
        private bool attackOverride;
        public void SetAttackOverride(bool value)
        {
            attackOverride = value;
            if (animator) animator.enabled = !value;
            if (!value) current = null;
        }
        public void SetFrozen(bool value) { if (animator) animator.speed = value ? 0 : 1; }
        public void Play(string state, bool restart = false)
        {
            if (attackOverride || !animator || (!restart && current == state)) return;
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, hash)) return;
            current = state;
            animator.Play(hash, 0, 0);
        }
    }
}
