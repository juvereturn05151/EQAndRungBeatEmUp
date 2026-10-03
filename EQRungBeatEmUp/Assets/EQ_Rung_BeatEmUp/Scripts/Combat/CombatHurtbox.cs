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
        public CombatHitOutcome LastHitOutcome { get; private set; }
        public int LastHitstopFrames { get; private set; }
        public bool Receive(AttackHitboxData hit, int facing, CharacterMotor attacker = null)
        {
            LastHitOutcome = CombatHitOutcome.None; LastHitstopFrames = hit.hitstopFrames;
            if (!health || health.IsDead || !motor) return false;
            if (motor.IsGrounded ? !hit.canHitGrounded : !hit.canHitAirborne) return false;
            var frame = motor.attackPlayer ? motor.attackPlayer.Frame : null;
            if (frame != null && frame.invulnerable) return false;
            if (player && player.DodgeInvulnerable) return false;
            if (player)
            {
                var defense = player.TryDefense(hit, facing, attacker);
                if (defense == CombatHitOutcome.Block || defense == CombatHitOutcome.Parry)
                {
                    LastHitOutcome = defense;
                    LastHitstopFrames = defense == CombatHitOutcome.Parry ? player.defenseData.parryHitstopFrames : player.defenseData.guardHitstopFrames;
                    return true;
                }
            }
            if (!health.Damage(hit.damage)) return false;
            LastHitOutcome = CombatHitOutcome.Hit;
            if (frame == null || !frame.superArmor || health.IsDead)
            {
                if (enemy) enemy.Receive(hit, facing);
                if (player) player.ReceiveHit(hit, facing);
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
