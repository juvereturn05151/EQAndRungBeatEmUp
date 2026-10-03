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
        [Tooltip("Different teams can damage one another.")]
        public int team;
        public bool debugDraw;
        public bool Receive(AttackData attack, int facing)
        {
            if (!health || health.IsDead || !motor || (!motor.IsGrounded && !attack.canHitAirborne)) return false;
            if (!health.Damage(attack.damage)) return false;
            if (enemy) enemy.Receive(attack, facing);
            if (player) { player.Interrupt(attack.hitstun); motor.AddKnockback(facing * attack.knockback); }
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
