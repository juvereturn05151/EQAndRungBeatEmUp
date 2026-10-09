using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BeatEmUp
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class DestructibleObject : MonoBehaviour
    {
        public SpriteRenderer visual;
        public Sprite intactSprite, damagedSprite, brokenSprite;
        [Min(1)] public float maximumHealth = 25;
        [Tooltip("One HP per accepted attack hit. Off preserves legacy damage-based props/totems.")]
        public bool useHitPoints;
        public PropKind kind;
        [Min(0)] public float totemBreakRadius;
        [Header("Ground footprint (existing CharacterMotor wall collision)")]
        public BoxCollider2D movementBlocker;
        [Header("Break animation and debris")]
        public Sprite[] destructionSprites = new Sprite[0];
        public Sprite[] debrisSprites = new Sprite[0];
        public GameObject[] debrisPrefabs = new GameObject[0];
        [Min(.01f)] public float destructionFrameSeconds = .06f;
        [Min(0)] public int debrisCount = 9;
        [Min(0)] public float debrisForce = 1.8f;
        [Min(.05f)] public float debrisLifetime = 3;
        [Header("Feedback")]
        public AudioClip hitSfx, destructionSfx;
        public GameObject hitVfx, destructionVfx;
        [Range(0, 1)] public float volume = .65f;
        [Min(0)] public int hitstopFrames = 3;
        [Min(0)] public float hitFlashSeconds = .055f;
        [Header("Optional pickup / reward")]
        public GameObject dropPrefab;
        [Range(0, 1)] public float dropChance = 1;
        public UnityEvent onBroken = new UnityEvent();
        public float Current { get; private set; }
        public bool IsBroken => Current <= 0;
        public int LastHitstopFrames { get; private set; }
        public event Action<DestructibleObject> Broken;
        readonly List<GameObject> spawned = new List<GameObject>();
        float shake, flash, brokenTime;
        Vector3 rest;
        Color tint = Color.white;
        bool breakTriggered;
        BoxCollider2D hurtbox;
        void Awake()
        {
            hurtbox = GetComponent<BoxCollider2D>();
            if (!visual) visual = GetComponentInChildren<SpriteRenderer>();
            if (visual) { rest = visual.transform.localPosition; tint = visual.color; }
            Current = maximumHealth;
            ResetVisual();
        }
        public void Configure(DestructiblePlacement placement)
        {
            maximumHealth = Mathf.Max(1, placement.health); kind = placement.kind; totemBreakRadius = placement.totemBreakRadius;
            intactSprite = placement.intactSprite ? placement.intactSprite : intactSprite;
            damagedSprite = placement.damagedSprite ? placement.damagedSprite : damagedSprite;
            brokenSprite = placement.brokenSprite ? placement.brokenSprite : brokenSprite;
            dropPrefab = placement.dropPrefab ? placement.dropPrefab : dropPrefab;
            if (!visual) visual = GetComponentInChildren<SpriteRenderer>();
            if (!visual) { var child = new GameObject("Visual"); child.transform.SetParent(transform, false); visual = child.AddComponent<SpriteRenderer>(); }
            rest = visual.transform.localPosition; tint = visual.color;
            hurtbox = GetComponent<BoxCollider2D>(); hurtbox.isTrigger = true;
            hurtbox.size = placement.hitboxSize; hurtbox.offset = placement.hitboxOffset;
            Respawn(maximumHealth);
        }
        public bool Receive(AttackHitboxData hit, int facing, CharacterMotor attacker)
        {
            if (CombatClock.IsPaused || !isActiveAndEnabled || IsBroken || breakTriggered || hit == null || !attacker || hit.damage <= 0 ||
                Mathf.Abs(attacker.transform.position.y - transform.position.y) > hit.laneTolerance) return false;
            LastHitstopFrames = useHitPoints ? hitstopFrames : hit.hitstopFrames;
            Current = Mathf.Max(0, Current - (useHitPoints ? 1 : hit.damage));
            shake = .16f; flash = hitFlashSeconds;
            if (IsBroken)
            {
                // Commit before callbacks so reentrant damage cannot repeat drops / break effects.
                breakTriggered = true; hurtbox.enabled = false;
                if (movementBlocker) movementBlocker.enabled = false;
                shake = 0; brokenTime = 0;
                if (visual) { visual.transform.localPosition = rest; visual.sprite = destructionSprites.Length > 0 ? destructionSprites[0] : brokenSprite; visual.enabled = visual.sprite; }
                Feedback(destructionSfx, destructionVfx, facing);
                for (int i = 0; i < debrisCount; i++) SpawnDebris(facing, i);
                if (dropPrefab && UnityEngine.Random.value <= dropChance) Track(Instantiate(dropPrefab, transform.position, Quaternion.identity, transform.parent));
                onBroken.Invoke(); Broken?.Invoke(this);
            }
            else
            {
                if (visual && damagedSprite) visual.sprite = damagedSprite;
                Feedback(hitSfx, hitVfx, facing);
            }
            return true;
        }
        void Track(GameObject go) { spawned.RemoveAll(item => !item); spawned.Add(go); }
        void Feedback(AudioClip clip, GameObject effect, int facing)
        {
            // Same SFX/VFX path used by attack and boss encounter feedback.
            if (!clip && !effect) return;
            var data = new AttackFeedbackData { impactSound = clip, impactVolume = volume, impactPrefab = effect,
                impactScale = .12f, impactLifetime = .55f, impactRotateWithFacing = false };
            AttackFeedback.PlayRemoteFeedback(data, (Vector2)transform.position + Vector2.up * .3f, facing, true);
        }
        void SpawnDebris(int facing, int index)
        {
            GameObject go;
            if (debrisPrefabs.Length > 0 && debrisPrefabs[index % debrisPrefabs.Length])
                go = Instantiate(debrisPrefabs[index % debrisPrefabs.Length], transform.position, Quaternion.identity, transform.parent);
            else
            {
                if (debrisSprites.Length == 0 || !debrisSprites[index % debrisSprites.Length]) return;
                go = new GameObject("Prop fragment"); go.transform.SetParent(transform.parent, false); go.transform.position = transform.position;
                go.AddComponent<SpriteRenderer>().sprite = debrisSprites[index % debrisSprites.Length];
            }
            var debris = go.GetComponent<PropDebris>(); if (!debris) debris = go.AddComponent<PropDebris>();
            debris.Initialize(new Vector2(UnityEngine.Random.Range(-.7f, .7f) + facing * .5f, UnityEngine.Random.Range(-.3f, .3f)) * debrisForce,
                UnityEngine.Random.Range(1.5f, 3.5f) * debrisForce, debrisLifetime);
            Track(go);
        }
        public void Respawn(float hp)
        {
            foreach (var go in spawned) if (go) { go.SetActive(false); Destroy(go); }
            spawned.Clear(); maximumHealth = Mathf.Max(1, hp); Current = maximumHealth;
            breakTriggered = false; shake = flash = brokenTime = 0;
            if (!hurtbox) hurtbox = GetComponent<BoxCollider2D>();
            hurtbox.enabled = true;
            if (movementBlocker) movementBlocker.enabled = true;
            ResetVisual();
        }
        [ContextMenu("Reset prop")] public void ResetProp() => Respawn(maximumHealth);
        void ResetVisual()
        {
            if (!visual) return;
            visual.transform.localPosition = rest; visual.sprite = intactSprite; visual.color = tint; visual.enabled = true;
            visual.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100);
        }
        public void SetMaximumHealth(float hp)
        {
            float fraction = Current / Mathf.Max(1, maximumHealth);
            maximumHealth = Mathf.Max(1, hp); Current = maximumHealth * fraction;
        }
        void Update()
        {
            if (CombatClock.IsPaused || !visual) return;
            float dt = Time.deltaTime;
            shake = Mathf.Max(0, shake - dt); flash = Mathf.Max(0, flash - dt);
            visual.transform.localPosition = rest + Vector3.right * (shake > 0 ? Mathf.Sin(shake * 180) * .035f : 0);
            visual.color = flash > 0 ? Color.white * 2 : tint;
            visual.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100);
            if (!IsBroken) return;
            brokenTime += dt;
            int frame = Mathf.FloorToInt(brokenTime / Mathf.Max(.01f, destructionFrameSeconds));
            visual.sprite = frame < destructionSprites.Length ? destructionSprites[frame] : brokenSprite;
            visual.enabled = visual.sprite && (!useHitPoints || brokenTime < debrisLifetime);
        }
    }
}
