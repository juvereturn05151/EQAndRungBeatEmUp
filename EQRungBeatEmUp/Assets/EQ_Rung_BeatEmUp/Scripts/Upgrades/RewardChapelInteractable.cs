using UnityEngine;

namespace BeatEmUp
{
    public sealed class RewardChapelInteractable : MonoBehaviour
    {
        public RewardSelectionController Owner { get; internal set; }
        public bool TryInteract() => Owner && Owner.State == WorldRewardState.RewardPending && Owner.Interact();
    }
}
