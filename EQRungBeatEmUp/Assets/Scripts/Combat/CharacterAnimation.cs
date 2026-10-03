using UnityEngine;

namespace BeatEmUp
{
    public sealed class CharacterAnimation : MonoBehaviour
    {
        public Animator animator;
        private string current;
        public void Play(string state, bool restart = false)
        {
            if (!animator || (!restart && current == state)) return;
            int hash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, hash)) return;
            current = state;
            animator.Play(hash, 0, 0);
        }
    }
}
