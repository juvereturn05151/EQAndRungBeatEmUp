using UnityEngine;

namespace BeatEmUp
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealth))]
    public sealed class HitBlinkEffect : MonoBehaviour
    {
        [Header("Hit Blink")]
        [SerializeField, Min(0)] private float hitBlinkDuration = .35f;
        [SerializeField, Min(.001f)] private float hitBlinkInterval = .06f;
        [SerializeField] private SpriteRenderer[] blinkRenderers;
        [SerializeField] private bool blinkOnHit = true;

        private CharacterHealth health;
        private SpriteRenderer[] activeRenderers;
        private bool[] originalVisibility;
        private float elapsed;
        private bool blinking;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            // Use the motor's body renderer as a fallback, not every child effect.
            if (blinkRenderers == null || blinkRenderers.Length == 0)
            {
                var motor = GetComponent<CharacterMotor>();
                if (motor && motor.sprite) blinkRenderers = new[] { motor.sprite };
            }
        }

        private void OnEnable()
        {
            if (!health) health = GetComponent<CharacterHealth>();
            health.Damaged += PlayBlink;
            health.Died += StopBlink;
            health.Restored += StopBlink;
        }

        public void PlayBlink()
        {
            // One timer per character. A new hit restores and restarts that timer.
            StopBlink();
            if (!isActiveAndEnabled || !blinkOnHit || hitBlinkDuration <= 0 ||
                !health || health.IsDead || blinkRenderers == null || blinkRenderers.Length == 0) return;
            activeRenderers = (SpriteRenderer[])blinkRenderers.Clone();
            originalVisibility = new bool[activeRenderers.Length];
            for (int i = 0; i < activeRenderers.Length; i++)
                originalVisibility[i] = activeRenderers[i] && activeRenderers[i].enabled;
            elapsed = 0;
            blinking = true;
        }

        private void Update()
        {
            if (!blinking) return;
            elapsed += Time.unscaledDeltaTime;
            if (!blinkOnHit || elapsed >= hitBlinkDuration || (health && health.IsDead))
            {
                StopBlink();
                return;
            }
            bool visible = Mathf.FloorToInt(elapsed / Mathf.Max(.001f, hitBlinkInterval)) % 2 == 0;
            for (int i = 0; i < activeRenderers.Length; i++)
                if (activeRenderers[i]) activeRenderers[i].enabled = originalVisibility[i] && visible;
        }

        public void StopBlink()
        {
            if (activeRenderers != null)
                for (int i = 0; i < activeRenderers.Length; i++)
                    if (activeRenderers[i]) activeRenderers[i].enabled = originalVisibility[i];
            activeRenderers = null;
            originalVisibility = null;
            elapsed = 0;
            blinking = false;
        }

        private void OnDisable()
        {
            if (health)
            {
                health.Damaged -= PlayBlink;
                health.Died -= StopBlink;
                health.Restored -= StopBlink;
            }
            StopBlink();
        }

        private void OnDestroy() { StopBlink(); }
    }
}
