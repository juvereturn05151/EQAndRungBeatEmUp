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
        private readonly Dictionary<int, Dictionary<CharacterHealth, int>> victims = new Dictionary<int, Dictionary<CharacterHealth, int>>();
        private AttackFrameData current;
        private int facing, frameNumber;
        public void Begin(AttackData attack) { current = null; victims.Clear(); }
        public void End() { current = null; victims.Clear(); }
        public void SetFrame(AttackFrameData frame, int index, int direction) { current = frame; frameNumber = index; facing = direction; }
        public Vector2 Center(AttackHitboxData box) => (Vector2)motor.transform.position + new Vector2(box.offset.x * facing, motor.Height + box.offset.y);
        public void Sample()
        {
            if (current == null || !motor) return;
            Physics2D.SyncTransforms();
            foreach (var box in current.hitboxes)
            {
                if (box == null) continue;
                if (!victims.TryGetValue(box.hitId, out var history)) { history = new Dictionary<CharacterHealth, int>(); victims.Add(box.hitId, history); }
                foreach (var collider in Physics2D.OverlapBoxAll(Center(box), box.size, 0, hurtboxLayers))
                {
                    var hurtbox = collider.GetComponent<CombatHurtbox>();
                    if (!hurtbox || !hurtbox.motor || !hurtbox.health || hurtbox.team == team) continue;
                    if (history.TryGetValue(hurtbox.health, out int last) && (box.repeatAfterFrames == 0 || frameNumber - last < box.repeatAfterFrames)) continue;
                    if (Mathf.Abs(hurtbox.motor.transform.position.y - motor.transform.position.y) > box.laneTolerance) continue;
                    if (!hurtbox.Receive(box, facing, motor)) continue;
                    history[hurtbox.health] = frameNumber;
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
