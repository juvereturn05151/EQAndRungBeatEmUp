using UnityEngine;

namespace BeatEmUp
{
    // Preserve each effect layer's offset relative to actors at the same ground depth.
    [DisallowMultipleComponent]
    public sealed class GroundSortedEffect : MonoBehaviour
    {
        SpriteRenderer[] renderers;
        int[] offsets;
        void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            offsets = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) offsets[i] = renderers[i].sortingOrder;
        }
        void OnEnable() => RefreshSorting();
        void LateUpdate() => RefreshSorting();
        public void RefreshSorting()
        {
            if (renderers == null) return;
            int groundOrder = Mathf.RoundToInt(-transform.position.y * 100);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i]) renderers[i].sortingOrder = groundOrder + offsets[i];
        }
    }
}
