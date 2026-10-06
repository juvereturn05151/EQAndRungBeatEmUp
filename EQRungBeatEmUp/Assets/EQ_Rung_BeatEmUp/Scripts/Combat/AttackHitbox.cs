using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public sealed class AttackHitbox : MonoBehaviour
    {
        public CharacterMotor motor;
        public AttackPlayer owner;
        public int team;
        public LayerMask hurtboxLayers = ~0;
        public bool debugDraw;
        public event System.Action<Vector2, CombatHitOutcome> HitConfirmed;
        private readonly Dictionary<int, Dictionary<CharacterHealth, int>> victims = new Dictionary<int, Dictionary<CharacterHealth, int>>();
        private readonly Dictionary<int, Dictionary<DestructibleObject, int>> props = new Dictionary<int, Dictionary<DestructibleObject, int>>();
        private AttackFrameData current;
        private int facing, frameNumber;
        public void Begin(AttackData attack) { current = null; victims.Clear(); props.Clear(); }
        public void End() { current = null; victims.Clear(); props.Clear(); }
        public void SetFrame(AttackFrameData frame, int index, int direction) { current = frame; frameNumber = index; facing = direction; }
        public Vector2 Center(AttackHitboxData box) => (Vector2)motor.transform.position + new Vector2(box.offset.x * facing, (box.groundArea ? 0 : motor.Height) + box.offset.y);
        public static bool InGroundArea(AttackHitboxData box, Vector2 center, Vector2 groundPosition)
        {
            var delta = groundPosition - center;
            float x = delta.x / Mathf.Max(.005f, box.size.x * .5f), y = delta.y / Mathf.Max(.005f, box.size.y * .5f);
            return x * x + y * y <= 1 && Mathf.Abs(delta.y) <= box.laneTolerance;
        }
        Collider2D[] Candidates(AttackHitboxData box)
        {
            if (!box.groundArea) return Physics2D.OverlapBoxAll(Center(box), box.size, 0, hurtboxLayers);
            // Airborne colliders follow sprite height. Select by motor ground coordinates instead,
            // then pass through precisely the same defense, damage and deduplication path below.
            var colliders = new List<Collider2D>();
            foreach (var hurtbox in FindObjectsByType<CombatHurtbox>(FindObjectsSortMode.None))
            {
                if (!hurtbox.isActiveAndEnabled || !hurtbox.motor || !InGroundArea(box, Center(box), hurtbox.motor.transform.position)) continue;
                var collider = hurtbox.GetComponent<Collider2D>();
                if (collider && collider.enabled && (hurtboxLayers.value & (1 << collider.gameObject.layer)) != 0) colliders.Add(collider);
            }
            return colliders.ToArray();
        }
        public void Sample()
        {
            if (CombatClock.IsPaused || current == null || !motor) return;
            Physics2D.SyncTransforms();
            foreach (var box in current.hitboxes)
            {
                if (box == null) continue;
                if (!victims.TryGetValue(box.hitId, out var history)) { history = new Dictionary<CharacterHealth, int>(); victims.Add(box.hitId, history); }
                foreach (var collider in Candidates(box))
                {
                    var prop = collider.GetComponentInParent<DestructibleObject>();
                    if (prop && team == 0)
                    {
                        if (!props.TryGetValue(box.hitId, out var propHistory)) { propHistory = new Dictionary<DestructibleObject, int>(); props.Add(box.hitId, propHistory); }
                        if (propHistory.TryGetValue(prop, out int lastProp) && (box.repeatAfterFrames == 0 || frameNumber - lastProp < box.repeatAfterFrames)) continue;
                        var propHit = motor.GetComponent<RunBuildState>()?.ModifyHit(box, null) ?? box;
                        var meta=motor.GetComponent<MetaProgress>();
                        if(meta) { propHit=propHit.RuntimeCopy(); propHit.damage*=meta.DamageMultiplier(owner && owner.CurrentAttack==motor.GetComponent<PlayerSkillController>()?.equippedSkill?.cast); }
                        if (prop.Receive(propHit, facing, motor))
                        {
                            propHistory[prop] = frameNumber;
                            HitConfirmed?.Invoke(Center(box), CombatHitOutcome.Hit);
                            if (owner) owner.Freeze(propHit.hitstopFrames);
                        }
                        continue;
                    }
                    var hurtbox = collider.GetComponent<CombatHurtbox>();
                    if (!hurtbox || !hurtbox.motor || !hurtbox.health || hurtbox.team == team) continue;
                    if (history.TryGetValue(hurtbox.health, out int last) && (box.repeatAfterFrames == 0 || frameNumber - last < box.repeatAfterFrames)) continue;
                    if (Mathf.Abs(hurtbox.motor.transform.position.y - motor.transform.position.y) > box.laneTolerance) continue;
                    var build = motor.GetComponent<RunBuildState>();
                    var runtimeHit = build ? build.ModifyHit(box, hurtbox.motor) : box;
                    if (!hurtbox.Receive(runtimeHit, facing, motor)) continue;
                    history[hurtbox.health] = frameNumber;
                    HitConfirmed?.Invoke(collider.ClosestPoint(Center(box)), hurtbox.LastHitOutcome);
                    build?.AttackHit(hurtbox.health, hurtbox.LastHitOutcome);
                    if (owner) owner.Freeze(hurtbox.LastHitstopFrames);
                    if (hurtbox.motor.attackPlayer) hurtbox.motor.attackPlayer.Freeze(hurtbox.LastHitstopFrames);
                    // A parry can stop the attacker during this very sample.
                    if (owner && !owner.CurrentAttack) return;
                }
            }
        }
        private void OnDrawGizmos()
        {
            if (!debugDraw || current == null || !motor) return;
            Gizmos.color = Color.yellow;
            var boxes = current.hitboxes;
            if (owner && owner.CurrentAttack && owner.CurrentAttack.feedback != null && owner.CurrentAttack.feedback.areaWarning && boxes.Count == 0 && owner.CurrentFrame < owner.CurrentAttack.FirstActiveFrame)
            { boxes = owner.CurrentAttack.frames[owner.CurrentAttack.FirstActiveFrame].hitboxes; Gizmos.color = new Color(1, .4f, 0); }
            foreach (var box in boxes) if (box != null)
            {
                if (!box.groundArea) { Gizmos.DrawWireCube(Center(box), box.size); continue; }
                var center = Center(box);
                for (int i = 0; i < 48; i++)
                {
                    float a = i * Mathf.PI / 24, b = (i + 1) * Mathf.PI / 24;
                    Gizmos.DrawLine(center + new Vector2(Mathf.Cos(a) * box.size.x / 2, Mathf.Sin(a) * box.size.y / 2), center + new Vector2(Mathf.Cos(b) * box.size.x / 2, Mathf.Sin(b) * box.size.y / 2));
                }
            }
        }
    }
}
