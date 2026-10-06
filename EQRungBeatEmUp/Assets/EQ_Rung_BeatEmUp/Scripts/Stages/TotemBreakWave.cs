using UnityEngine;
namespace BeatEmUp
{
    // A typed expanding radial front on the walking plane; never broadcasts vulnerability on prop destruction.
    public sealed class TotemBreakWave : MonoBehaviour, ICombatFrameListener
    {
        public BossTotem Source { get; private set; }
        public float Radius { get; private set; }
        public int Age { get; private set; }
        public float MaximumRadius => maximumRadius;
        public int Duration => duration;
        public int FrameOrder => 6;
        float previousRadius, maximumRadius;
        int duration;
        bool contacted;
        LineRenderer ring;
        Material material;
        public void Initialize(BossTotem source)
        {
            Source = source; maximumRadius = source.BreakRadius; duration = Mathf.Max(1, source.Data.breakWaveFrames);
            ring = gameObject.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
            material = new Material(Shader.Find("Sprites/Default")); ring.sharedMaterial = material;
            ring.widthMultiplier = .035f; ring.sortingOrder = 150; Draw();
        }
        void OnEnable() => CombatClock.Register(this);
        void OnDisable() => CombatClock.Unregister(this);
        void OnDestroy() { if (material) Destroy(material); }
        public bool CanReach(TotemBossController boss)
        {
            if (!Source || Source.Boss != boss || contacted || Age <= 0 || Age > duration || !isActiveAndEnabled) return false;
            float distance = Vector2.Distance(transform.position, boss.transform.position);
            // Sweeps the radius traversed this tick, including origin contact on the first tick.
            return distance <= Radius && (Age == 1 || distance >= previousRadius);
        }
        public void CombatFrame()
        {
            if (CombatClock.IsPaused || !Source) return;
            if (!Source.Boss || Source.Boss.State == BossEncounterState.Dead || Age >= duration) { gameObject.SetActive(false); Destroy(gameObject); return; }
            previousRadius = Radius; Age++; Radius = maximumRadius * Age / duration; Draw();
            if (CanReach(Source.Boss)) { Source.Boss.ReceiveTotemWave(this); contacted = true; }
        }
        void Draw()
        {
            if (!ring) return;
            for (int i = 0; i < ring.positionCount; i++) { float a = i * Mathf.PI * 2 / ring.positionCount; ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * Radius); }
            ring.startColor = ring.endColor = new Color(.65f, .35f, 1, 1 - Age / (float)duration * .65f);
        }
    }
}
