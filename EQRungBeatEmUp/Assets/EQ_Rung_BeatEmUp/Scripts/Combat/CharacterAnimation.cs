using UnityEngine;

namespace BeatEmUp
{
    public sealed class CharacterAnimation : MonoBehaviour
    {
        public Animator animator;
        private string current;
        private bool attackOverride;
        private bool poseOverride, frameDriven, frozen;
        private void ApplyControl()
        {
            if (!animator) return;
            animator.enabled = !attackOverride && !poseOverride;
            animator.speed = frozen || frameDriven ? 0 : 1;
        }
        public void SetAttackOverride(bool value)
        {
            attackOverride = value;
            ApplyControl();
            if (!value) current = null;
        }
        public void SetFrozen(bool value) { frozen = value; ApplyControl(); }
        // Reactions use the combat clock so hitstop and slow rendered frames do
        // not let the visual recovery finish before the gameplay recovery.
        public void SampleState(string state, float normalizedTime)
        {
            if (attackOverride || !animator) return;
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, hash)) return;
            poseOverride = false; frameDriven = true; current = state;
            ApplyControl();
            animator.Play(hash, 0, Mathf.Clamp01(normalizedTime));
            animator.Update(0);
        }
        public void HoldSprite(SpriteRenderer renderer, Sprite pose)
        {
            if (attackOverride || !renderer || !pose) return;
            poseOverride = true; frameDriven = false; current = null;
            ApplyControl(); renderer.sprite = pose;
        }
        public void ReleaseReactionControl()
        {
            poseOverride = false; frameDriven = false; current = null;
            ApplyControl();
        }
        public void Play(string state, bool restart = false)
        {
            if (attackOverride || !animator || (!restart && current == state && !frameDriven && !poseOverride)) return;
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, hash)) return;
            poseOverride = false; frameDriven = false; ApplyControl();
            current = state;
            animator.Play(hash, 0, 0);
        }
    }
}
