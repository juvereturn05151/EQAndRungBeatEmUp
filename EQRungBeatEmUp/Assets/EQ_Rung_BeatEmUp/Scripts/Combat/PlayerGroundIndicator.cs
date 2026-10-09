using UnityEngine;

namespace BeatEmUp
{
    // A sibling of the airborne Visual transform. Cosmetic only: no combat listener,
    // colliders, per-frame spawning, private materials or independent network state.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PlayerGroundIndicator : MonoBehaviour
    {
        public CharacterMotor motor;
        public PlayerGroundIndicatorStyle style;
        public SpriteRenderer shadow;
        public SpriteRenderer ring;
        [Range(0, 3), Tooltip("Editor/single-player fallback only. PlayerIdentity.slot always takes precedence.")]
        public int previewPlayerIndex;
        PlayerIdentity identity;
        public int PlayerIndex => identity ? Mathf.Clamp(identity.slot, 0, 3) : previewPlayerIndex;
        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (!motor) motor = GetComponentInParent<CharacterMotor>();
            if (!identity && motor) identity = motor.GetComponent<PlayerIdentity>();
            var settings = style ? style : PlayerGroundIndicatorStyle.Shared;
            if (!settings || !shadow || !ring) return;
            if (motor)
            {
                transform.position = motor.transform.position + (Vector3)settings.groundOffset;
                transform.rotation = Quaternion.identity;
            }
            else transform.localPosition = settings.groundOffset;
            shadow.sprite = settings.shadowSprite;
            ring.sprite = settings.RingSprite;
            SetSize(shadow, settings.shadowSize);
            SetSize(ring, settings.ringSize);
            shadow.color = new Color(.08f, .065f, .09f, settings.shadowOpacity);
            var color = settings.ColorForSlot(PlayerIndex); color.a *= settings.ringOpacity; ring.color = color;
            // Follow the body's existing lane sort; never use airborne visual Y.
            int order = motor ? Mathf.RoundToInt(-motor.transform.position.y * 100) : 0;
            int layer = motor && motor.sprite ? motor.sprite.sortingLayerID : shadow.sortingLayerID;
            shadow.sortingLayerID = ring.sortingLayerID = layer;
            shadow.sortingOrder = order - 1; ring.sortingOrder = order - 2;
            shadow.enabled = ring.enabled = enabled;
        }
        static void SetSize(SpriteRenderer renderer, Vector2 size)
        {
            if (!renderer.sprite) return;
            var bounds = renderer.sprite.bounds.size;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localScale = new Vector3(Mathf.Max(.01f, size.x) / bounds.x, Mathf.Max(.01f, size.y) / bounds.y, 1);
        }
        void OnDisable() { if (shadow) shadow.enabled = false; if (ring) ring.enabled = false; }
    }
}
