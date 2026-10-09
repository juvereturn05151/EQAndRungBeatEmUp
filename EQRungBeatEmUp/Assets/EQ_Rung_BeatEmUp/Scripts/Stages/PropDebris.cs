using UnityEngine;
namespace BeatEmUp
{
    // Height is separate from the ground XY lane, as in CharacterMotor.
    public sealed class PropDebris : MonoBehaviour
    {
        Vector2 ground, velocity;
        float height = .25f, vertical, age, lifetime, spin;
        bool bounced;
        SpriteRenderer visual;
        public void Initialize(Vector2 force, float upwardForce, float duration)
        {
            ground = transform.position; velocity = force; vertical = upwardForce; lifetime = Mathf.Max(.05f, duration);
            spin = Random.Range(-260f, 260f); visual = GetComponentInChildren<SpriteRenderer>();
        }
        void Update() => Simulate(Time.deltaTime);
        public void Simulate(float dt)
        {
            if (CombatClock.IsPaused) return;
            age += dt;
            if (age >= lifetime) { Destroy(gameObject); return; }
            ground += velocity * dt;
            vertical -= 18 * dt; height += vertical * dt;
            if (height <= 0)
            {
                height = 0;
                if (!bounced && vertical < -1) { vertical = -vertical * .22f; bounced = true; velocity *= .35f; spin *= .25f; }
                else { vertical = 0; velocity = Vector2.MoveTowards(velocity, Vector2.zero, dt * 8); spin = 0; }
            }
            transform.position = new Vector3(ground.x, ground.y + height, 0);
            transform.Rotate(0, 0, spin * dt);
            if (visual)
            {
                visual.sortingOrder = Mathf.RoundToInt(-ground.y * 100) + 1;
                var color = visual.color; color.a = Mathf.Clamp01((lifetime - age) / .45f); visual.color = color;
            }
        }
    }
}
