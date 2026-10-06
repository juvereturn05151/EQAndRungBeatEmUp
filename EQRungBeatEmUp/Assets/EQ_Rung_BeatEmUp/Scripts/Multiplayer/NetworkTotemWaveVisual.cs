using UnityEngine;

namespace BeatEmUp
{
    // Cosmetic only: the host's TotemBreakWave owns collision and vulnerability.
    public sealed class NetworkTotemWaveVisual : MonoBehaviour
    {
        LineRenderer ring;
        Material material;
        float radius, targetRadius;
        Color color;
        void Awake()
        {
            ring = gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
            material = new Material(Shader.Find("Sprites/Default")); ring.sharedMaterial = material;
            ring.widthMultiplier = .035f; ring.sortingOrder = 150;
        }
        public void Apply(TotemWaveState state, bool snap)
        {
            transform.position = state.position;
            targetRadius = state.radius;
            if (snap) radius = targetRadius;
            color = new Color(.65f, .35f, 1, 1 - Mathf.Clamp01(state.age / (float)Mathf.Max(1, state.duration)) * .65f);
            Draw();
        }
        void Update()
        {
            radius = Mathf.Lerp(radius, targetRadius, 1 - Mathf.Exp(-30 * Time.unscaledDeltaTime));
            Draw();
        }
        void Draw()
        {
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            ring.startColor = ring.endColor = color;
        }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
