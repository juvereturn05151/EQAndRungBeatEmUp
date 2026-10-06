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
        // Optional encounter-owned gate (for example, a boss protected by totems).
        public bool externalInvulnerable;
        public CombatHitOutcome LastHitOutcome { get; private set; }
        public int LastHitstopFrames { get; private set; }
        public bool Receive(AttackHitboxData hit, int facing, CharacterMotor attacker = null, CombatProjectile projectile = null)
        {
            LastHitOutcome = CombatHitOutcome.None; LastHitstopFrames = hit.hitstopFrames;
            if (CombatClock.IsPaused || !health || health.IsDead || health.SafeStageProtection || !motor) return false;
            if (externalInvulnerable) { motor.GetComponent<TotemBossController>()?.ShieldHit(); return false; }
            if (motor.IsGrounded ? !hit.canHitGrounded : !hit.canHitAirborne) return false;
            var frame = motor.attackPlayer ? motor.attackPlayer.Frame : null;
            if (frame != null && frame.invulnerable) return false;
            if (player && player.DodgeInvulnerable) { player.Build?.DodgeSucceeded(); return false; }
            if (player)
            {
                var defense = player.TryDefense(hit, facing, attacker, projectile);
                if (defense == CombatHitOutcome.Block || defense == CombatHitOutcome.Parry)
                {
                    LastHitOutcome = defense;
                    LastHitstopFrames = defense == CombatHitOutcome.Parry ? player.defenseData.parryHitstopFrames : player.defenseData.guardHitstopFrames;
                    return true;
                }
            }
            float previousHealth = health.Current;
            var sourceMeta=attacker ? attacker.GetComponent<MetaProgress>() : null;
            var sourceSkill=attacker ? attacker.GetComponent<PlayerSkillController>()?.equippedSkill : null;
            bool isSkill=sourceSkill && (projectile ? projectile.skillSource==sourceSkill : attacker.attackPlayer.CurrentAttack==sourceSkill.cast);
            if (!health.Damage(hit.damage*(sourceMeta ? sourceMeta.DamageMultiplier(isSkill) : 1))) return false;
            LastHitOutcome = CombatHitOutcome.Hit;
            var armor = enemy ? enemy.GetComponent<HitCountArmor>() : null;
            bool armored = armor && armor.isActiveAndEnabled && !health.IsDead && armor.Absorb(hit);
            if (armored) { LastHitOutcome = armor.LastOutcome; LastHitstopFrames = armor.hitstopFrames; }
            if (!armored && (frame == null || !frame.superArmor || health.IsDead))
            {
                if (enemy) enemy.Receive(hit, facing);
                if (player) player.ReceiveHit(hit, facing);
                if(hit.outwardGroundKnockback && attacker && !health.IsDead && hit.hitType==HitType.Normal)
                {
                    var delta=(Vector2)motor.transform.position-(Vector2)attacker.transform.position;
                    motor.AddGroundKnockback((delta.sqrMagnitude>.0001f ? delta.normalized : new Vector2(facing,0))*hit.knockback);
                }
            }
            if (attacker && health.Current < previousHealth)
                attacker.GetComponent<ComboTracker>()?.RecordHit(this, previousHealth - health.Current, hit);
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
