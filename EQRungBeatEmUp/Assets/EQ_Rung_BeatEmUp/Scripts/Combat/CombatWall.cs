using UnityEngine;

namespace BeatEmUp
{
    // Put this on a wall collider or its parent. The collider occupies ground/lane XY.
    // Height is independent of lane, so a wall blocks characters at every jump height.
    [DisallowMultipleComponent]
    public sealed class CombatWall : MonoBehaviour
    {
        public bool allowsBounce = true;
        public bool debugDraw = true;
        private void OnDrawGizmos()
        {
            if (!debugDraw) return;
            Gizmos.color = allowsBounce ? Color.magenta : Color.gray;
            foreach (var collider in GetComponentsInChildren<Collider2D>())
                if (!collider.isTrigger) Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
