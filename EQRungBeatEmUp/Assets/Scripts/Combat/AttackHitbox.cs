using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    public sealed class AttackHitbox : MonoBehaviour
    {
        public CharacterMotor motor;
        public int team;
        public LayerMask hurtboxLayers = ~0;
        public bool debugDraw;
        private readonly HashSet<CharacterHealth> victims = new HashSet<CharacterHealth>();
        private AttackData current;
        private int facing;
        public void Begin(AttackData attack) { current = attack; facing = motor.Facing; victims.Clear(); }
        public void End() { current = null; victims.Clear(); }
        public Vector2 Center => (Vector2)motor.transform.position + new Vector2(current.hitboxPosition.x * facing, motor.Height + current.hitboxPosition.y);
        
        public void Sample()
        {
            if (!current) return;
            Physics2D.SyncTransforms();
            foreach (var collider in Physics2D.OverlapBoxAll(Center, current.hitboxSize, 0, hurtboxLayers))
            {
                var hurtbox = collider.GetComponent<CombatHurtbox>();
                if (!hurtbox || !hurtbox.motor || !hurtbox.health || hurtbox.team == team || victims.Contains(hurtbox.health)) continue;
                if (Mathf.Abs(hurtbox.motor.transform.position.y - motor.transform.position.y) > current.laneTolerance) continue;
                if (hurtbox.Receive(current, facing)) victims.Add(hurtbox.health);
            }
        }
        private void OnDrawGizmos()
        {
            if (!debugDraw || !current || !motor) return;
            Gizmos.color = Color.yellow; Gizmos.DrawWireCube(Center, current.hitboxSize);
        }
    }
}
