using UnityEngine;

namespace BeatEmUp
{
    // Small runtime feedback for the upgrade's existing hurtbox-based area hit.
    public sealed class DiveShockwavePulse : MonoBehaviour
    {
        private LineRenderer ring;
        private Material material;
        private float age;
        private void Awake()
        {
            ring = gameObject.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 32;
            material = new Material(Shader.Find("Sprites/Default")); ring.sharedMaterial = material;
            ring.widthMultiplier = .035f; ring.sortingOrder = 150;
        }
        private void Update()
        {
            age += Time.deltaTime; float t = Mathf.Clamp01(age / .25f);
            for (int i = 0; i < 32; i++) { float angle = i * Mathf.PI * 2 / 32; ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * .9f, Mathf.Sin(angle) * .2f) * Mathf.Lerp(.2f, 1, t)); }
            ring.startColor = ring.endColor = new Color(.55f, .9f, 1, 1 - t);
            if (t >= 1) Destroy(gameObject);
        }
        private void OnDestroy() { if (material) Destroy(material); }
    }
}
