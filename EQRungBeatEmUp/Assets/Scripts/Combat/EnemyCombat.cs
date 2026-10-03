using UnityEngine;

namespace BeatEmUp
{
    public sealed class EnemyCombat : MonoBehaviour
    {
        public CharacterMotor motor;
        public EnemyHitReaction reaction;
        public CharacterAnimation animationDriver;
        public AttackHitbox hitbox;
        public Transform target;
        public AttackData attack;
        public bool passiveTrainingDummy = true;
        [Min(.1f)] public float attackRange = 1;
        [Min(0)] public float attackCooldown = 1;
        private bool attacking;
        private float elapsed, cooldown;
        private void Update()
        {
            cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
            if (!reaction.CanAct || passiveTrainingDummy || !target)
            {
                motor.MoveInput = Vector2.zero;
                if (attacking) { attacking = false; hitbox.End(); }
                if (reaction.CanAct) animationDriver.Play("Idle");
                return;
            }
            if (attacking)
            {
                float previous = elapsed; elapsed += Time.deltaTime;
                motor.MoveInput = Vector2.zero;
                if (previous < attack.startup + attack.activeDuration && elapsed >= attack.startup) hitbox.Sample();
                if (elapsed >= attack.Duration) { attacking = false; cooldown = attackCooldown; hitbox.End(); }
                return;
            }
            Vector2 delta = target.position - transform.position;
            motor.Face(delta.x);
            if (Mathf.Abs(delta.x) <= attackRange && Mathf.Abs(delta.y) < attack.laneTolerance)
            {
                motor.MoveInput = Vector2.zero;
                if (cooldown <= 0) { attacking = true; elapsed = 0; hitbox.Begin(attack); animationDriver.Play("Attack", true); }
                else animationDriver.Play("Idle");
            }
            else { motor.MoveInput = delta.normalized; animationDriver.Play("Walk"); }
        }
        private void OnDisable() { if (hitbox) hitbox.End(); if (motor) motor.MoveInput = Vector2.zero; }
    }
}
