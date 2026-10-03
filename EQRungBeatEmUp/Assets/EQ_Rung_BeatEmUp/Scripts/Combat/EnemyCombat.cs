using UnityEngine;

namespace BeatEmUp
{
    [RequireComponent(typeof(AttackPlayer))]
    public sealed class EnemyCombat : MonoBehaviour, ICombatFrameListener
    {
        public CharacterMotor motor;
        public EnemyHitReaction reaction;
        public CharacterAnimation animationDriver;
        public AttackHitbox hitbox;
        public AttackPlayer attackPlayer;
        public Transform target;
        public AttackData attack;
        public bool passiveTrainingDummy = true;
        [Min(.1f)] public float attackRange = 1;
        [Min(.01f)] public float laneRange = .55f;
        [Min(0)] public int attackCooldownFrames = 60;
        private int cooldown;
        public int FrameOrder => 10;
        private void Awake() { if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>(); }
        private void OnEnable()
        {
            if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>();
            attackPlayer.Finished += OnFinished; CombatClock.Register(this);
        }
        private void OnFinished(AttackData finished) { cooldown = attackCooldownFrames + finished.cooldownFrames; }
        public void CombatFrame()
        {
            if (!motor || !reaction || attackPlayer.IsFrozen) return;
            if (cooldown > 0) cooldown--;
            if (!reaction.CanAct || passiveTrainingDummy || !target)
            {
                motor.MoveInput = Vector2.zero;
                if (attackPlayer.CurrentAttack) attackPlayer.Stop();
                if (reaction.CanAct) animationDriver.Play("Idle");
                return;
            }
            if (attackPlayer.CurrentAttack) { motor.MoveInput = Vector2.zero; return; }
            Vector2 delta = target.position - transform.position;
            motor.Face(delta.x);
            if (Mathf.Abs(delta.x) <= attackRange && Mathf.Abs(delta.y) < laneRange)
            {
                motor.MoveInput = Vector2.zero;
                if (cooldown <= 0 && attackPlayer.Play(attack)) motor.MovementLocked = true;
                else animationDriver.Play("Idle");
            }
            else { motor.MoveInput = delta.normalized; animationDriver.Play("Walk"); }
        }
        private void OnDisable()
        {
            CombatClock.Unregister(this);
            if (attackPlayer) { attackPlayer.Finished -= OnFinished; attackPlayer.Stop(); }
            if (motor) motor.MoveInput = Vector2.zero;
        }
    }
}
