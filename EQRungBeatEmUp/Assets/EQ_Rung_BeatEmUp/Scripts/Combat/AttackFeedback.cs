using UnityEngine;

namespace BeatEmUp
{
    // Uses the existing combat timeline and accepted-hit history, never Animation Events.
    [DisallowMultipleComponent]
    public sealed class AttackFeedback : MonoBehaviour
    {
        AttackPlayer player;
        AttackHitbox hitbox;
        AttackData lastAttack;
        int lastSwingFrame = -1, lastImpactFrame = -1;
        public int SwingCount { get; private set; }
        public int ImpactCount { get; private set; }
        public GameObject LastImpact { get; private set; }
        public AudioSource LastSound { get; private set; }
        void OnEnable()
        {
            player = GetComponent<AttackPlayer>(); hitbox = GetComponent<AttackHitbox>();
            player.FrameEvent += FrameEvent; hitbox.HitConfirmed += HitConfirmed; player.Finished += Finished; player.Started += Finished;
        }
        void OnDisable()
        {
            if (player) { player.FrameEvent -= FrameEvent; player.Finished -= Finished; player.Started -= Finished; }
            if (hitbox) hitbox.HitConfirmed -= HitConfirmed;
            ResetHistory();
        }
        void Finished(AttackData _) => ResetHistory();
        void ResetHistory() { lastAttack = null; lastSwingFrame = lastImpactFrame = -1; }
        void Prepare()
        {
            // Started resets repeated assets; this also handles an explicitly rewound timeline.
            if (lastAttack != player.CurrentAttack || player.CurrentFrame < Mathf.Max(lastSwingFrame, lastImpactFrame))
            { ResetHistory(); lastAttack = player.CurrentAttack; }
        }
        void FrameEvent(string signal)
        {
            if (signal != "Swing" || !player.CurrentAttack || CombatClock.IsPaused) return;
            Prepare();
            if (lastSwingFrame == player.CurrentFrame) return;
            lastSwingFrame = player.CurrentFrame;
            var data = player.CurrentAttack.feedback;
            if (data == null) return;
            SwingCount++; PlaySound(data.swingSound, data.swingVolume, player.motor.transform.position);
        }
        void HitConfirmed(Vector2 point, CombatHitOutcome outcome)
        {
            if (outcome != CombatHitOutcome.Hit || !player.CurrentAttack || CombatClock.IsPaused) return;
            Prepare();
            // Multiple enemies/colliders hit on one frame share one cue to avoid a loud visual pile-up.
            if (lastImpactFrame == player.CurrentFrame) return;
            lastImpactFrame = player.CurrentFrame;
            var data = player.CurrentAttack.feedback;
            if (data == null) return;
            ImpactCount++; PlaySound(data.impactSound, data.impactVolume, point);
            if (!data.impactPrefab) return;
            var container = new GameObject("Combat impact (temporary)"); container.SetActive(false);
            container.transform.position = new Vector3(point.x, point.y, -.1f);
            var effect = Instantiate(data.impactPrefab, container.transform);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.Euler(0, 0, player.Facing < 0 ? 180 : 0);
            effect.transform.localScale *= data.impactScale;
            // Configure before activation; disable library camera shake and scene lights per instance.
            foreach (var cfx in effect.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))
            {
                if (cfx.cameraShake != null) cfx.cameraShake.enabled = false;
                cfx.animatedLights = new CartoonFX.CFXR_Effect.AnimatedLight[0];
            }
            foreach (var light in effect.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main; main.loop = false; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                if (renderer)
                {
                    renderer.sortingLayerID = player.motor.sprite ? player.motor.sprite.sortingLayerID : 0;
                    renderer.sortingOrder = (player.motor.sprite ? player.motor.sprite.sortingOrder : 0) + 2;
                }
            }
            LastImpact = container; container.SetActive(true);
            Destroy(container, data.impactLifetime);
        }
        void PlaySound(AudioClip clip, float volume, Vector3 point)
        {
            if (!clip || volume <= 0) return;
            var sound = new GameObject("Combat sound (temporary)"); sound.transform.position = point;
            var source = sound.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 0; source.volume = Mathf.Clamp01(volume); source.clip = clip;
            LastSound = source; source.Play(); Destroy(sound, clip.length + .1f);
        }
    }
}
