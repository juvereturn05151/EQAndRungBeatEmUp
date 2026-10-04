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
        public Vector2 Center(AttackHitboxData box) => (Vector2)motor.transform.position + new Vector2(box.offset.x * facing, motor.Height + box.offset.y);
        public void Sample()
        {
            if (CombatClock.IsPaused || current == null || !motor) return;
            Physics2D.SyncTransforms();
            foreach (var box in current.hitboxes)
            {
                if (box == null) continue;
                if (!victims.TryGetValue(box.hitId, out var history)) { history = new Dictionary<CharacterHealth, int>(); victims.Add(box.hitId, history); }
                foreach (var collider in Physics2D.OverlapBoxAll(Center(box), box.size, 0, hurtboxLayers))
                {
                    var prop = collider.GetComponentInParent<DestructibleObject>();
                    if (prop && team == 0)
                    {
                        if (!props.TryGetValue(box.hitId, out var propHistory)) { propHistory = new Dictionary<DestructibleObject, int>(); props.Add(box.hitId, propHistory); }
                        if (propHistory.TryGetValue(prop, out int lastProp) && (box.repeatAfterFrames == 0 || frameNumber - lastProp < box.repeatAfterFrames)) continue;
                        var propHit = motor.GetComponent<RunBuildState>()?.ModifyHit(box, null) ?? box;
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
            foreach (var box in current.hitboxes) if (box != null) Gizmos.DrawWireCube(Center(box), box.size);
        }
    }
}
