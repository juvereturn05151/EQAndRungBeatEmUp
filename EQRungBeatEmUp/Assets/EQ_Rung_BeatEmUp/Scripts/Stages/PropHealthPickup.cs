using UnityEngine;
namespace BeatEmUp
{
    public sealed class PropHealthPickup : MonoBehaviour
    {
        [Min(1)] public float healAmount = 25;
        [Min(.1f)] public float collectRadius = .4f;
        [Min(1)] public float lifetime = 20;
        float age;
        void Update()
        {
            if (CombatClock.IsPaused) return;
            age += Time.deltaTime;
            if (age >= lifetime) { Destroy(gameObject); return; }
            foreach (var player in FindObjectsByType<ComboController>(FindObjectsSortMode.None))
            {
                if (!player.motor || !player.motor.IsGrounded || !player.health || player.health.IsDead || player.health.Current >= player.health.EffectiveMaximum) continue;
                if (Vector2.Distance(transform.position, player.transform.position) > collectRadius) continue;
                player.health.Heal(healAmount); gameObject.SetActive(false); Destroy(gameObject); return;
            }
        }
    }
}
