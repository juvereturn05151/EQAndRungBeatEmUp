using UnityEngine;

namespace BeatEmUp
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CombatHurtbox : MonoBehaviour
    {
        public CharacterMotor motor;
        public CharacterHealth health;
        public EnemyHitReaction enemy;
        public ComboController player;
        public int team;
        public bool debugDraw;
        public bool Receive(AttackHitboxData hit, int facing)
        {
            if (!health || health.IsDead || !motor) return false;
            if (motor.IsGrounded ? !hit.canHitGrounded : !hit.canHitAirborne) return false;
            var frame = motor.attackPlayer ? motor.attackPlayer.Frame : null;
            if (frame != null && frame.invulnerable) return false;
            if (!health.Damage(hit.damage)) return false;
            if (frame == null || !frame.superArmor || health.IsDead)
            {
                if (enemy) enemy.Receive(hit, facing);
                if (player) { player.Interrupt(hit.hitstunFrames); motor.AddKnockback(facing * hit.knockback); }
            }
            return true;
        }
        private void OnDrawGizmos()
        {
            if (!debugDraw) return;
            var box = GetComponent<BoxCollider2D>();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.TransformPoint(box.offset), Vector3.Scale(box.size, transform.lossyScale));
        }
    }
}
