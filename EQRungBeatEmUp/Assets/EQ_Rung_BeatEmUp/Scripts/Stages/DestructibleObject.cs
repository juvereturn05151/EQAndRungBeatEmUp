using System;
using UnityEngine;
using UnityEngine.Events;

namespace BeatEmUp
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class DestructibleObject : MonoBehaviour
    {
        public SpriteRenderer visual;
        public Sprite intactSprite, damagedSprite, brokenSprite;
        [Min(1)] public float maximumHealth = 25;
        public PropKind kind;
        public GameObject dropPrefab;
        public UnityEvent onBroken = new UnityEvent();
        public float Current { get; private set; }
        public bool IsBroken => Current <= 0;
        public event Action<DestructibleObject> Broken;
        private float shake;
        private Vector3 rest;
        private void Awake() { Current = maximumHealth; if (visual) rest = visual.transform.localPosition; }
        public void Configure(DestructiblePlacement placement)
        {
            maximumHealth = Mathf.Max(1, placement.health); Current = maximumHealth; kind = placement.kind;
            intactSprite = placement.intactSprite ? placement.intactSprite : intactSprite;
            damagedSprite = placement.damagedSprite ? placement.damagedSprite : damagedSprite;
            brokenSprite = placement.brokenSprite ? placement.brokenSprite : brokenSprite;
            dropPrefab = placement.dropPrefab ? placement.dropPrefab : dropPrefab;
            if (!visual) visual = GetComponentInChildren<SpriteRenderer>();
            if (!visual) { var child = new GameObject("Visual"); child.transform.SetParent(transform, false); visual = child.AddComponent<SpriteRenderer>(); }
            rest = visual.transform.localPosition; visual.sprite = intactSprite;
            visual.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100);
            var box = GetComponent<BoxCollider2D>(); box.isTrigger = true;
            box.size = placement.hitboxSize; box.offset = placement.hitboxOffset; box.enabled = true;
        }
        public bool Receive(AttackHitboxData hit, int facing, CharacterMotor attacker)
        {
            if (IsBroken || !attacker || hit.damage <= 0 || Mathf.Abs(attacker.transform.position.y - transform.position.y) > hit.laneTolerance) return false;
            Current = Mathf.Max(0, Current - hit.damage); shake = .16f;
            if (IsBroken)
            {
                GetComponent<BoxCollider2D>().enabled = false; shake = 0;
                if (visual) { visual.transform.localPosition = rest; visual.sprite = brokenSprite; visual.enabled = brokenSprite; }
                if (dropPrefab) Instantiate(dropPrefab, transform.position, Quaternion.identity, transform.parent);
                onBroken.Invoke(); Broken?.Invoke(this);
            }
            else if (visual && damagedSprite) visual.sprite = damagedSprite;
            return true;
        }
        private void Update()
        {
            if (!visual || shake <= 0) return;
            shake = Mathf.Max(0, shake - Time.deltaTime);
            visual.transform.localPosition = rest + Vector3.right * (shake > 0 ? Mathf.Sin(shake * 180) * .035f : 0);
        }
    }
}
