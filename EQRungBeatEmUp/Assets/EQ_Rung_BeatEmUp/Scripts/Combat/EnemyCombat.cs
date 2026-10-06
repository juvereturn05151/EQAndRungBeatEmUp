using UnityEngine;
using System.Linq;

namespace BeatEmUp
{
    public enum EnemyRole { Generic, Rusher }
    [RequireComponent(typeof(AttackPlayer))]
    public sealed class EnemyCombat : MonoBehaviour, ICombatFrameListener
    {
        public EnemyRole role;
        public CharacterMotor motor;
        public EnemyHitReaction reaction;
        public CharacterAnimation animationDriver;
        public AttackHitbox hitbox;
        public AttackPlayer attackPlayer;
        public Transform target;
        public AttackData attack;
        [Tooltip("Optional editable behavior. None preserves the original AI.")]
        public EnemyAIProfile aiProfile;
        public EnemyAIController AI { get; private set; }
        public EnemyAttackCoordinator coordinator;
        public AttackCoordinationData legacyCoordination=new AttackCoordinationData();
        public bool CoordinationAttackActive => attackPlayer && attackPlayer.CurrentAttack || (AI?.RecoverySeconds ?? 0)>0 || GetComponent<TotemBossController>()?.State==BossEncounterState.Recovery;
        public void CancelCoordinationAttack() { attackPlayer.Stop(); AI?.CancelPendingAction(); }
        public bool RequestCoordination(AttackData selected,AttackCoordinationData data,float positionScore=0)
            => !coordinator || coordinator.RequestAttack(this,target,selected,data,positionScore);
        public void ConfirmCoordination()=>coordinator?.Confirm(this);
        public void ReleaseCoordination(string reason="Cancelled") { coordinator?.Release(this,reason); }
        public void RefreshAI() { ReleaseCoordination("Refresh"); if(attackPlayer && attackPlayer.CurrentAttack) attackPlayer.Stop(); AI=new EnemyAIController(this); }
        public bool passiveTrainingDummy = true;
        [Min(.1f)] public float attackRange = 1;
        [Min(0)] public float minimumAttackRange;
        [Min(.01f)] public float laneRange = .55f;
        [Min(0)] public int attackCooldownFrames = 60;
        private int cooldown;
        public int FrameOrder => 10;
        private void Awake() { if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>(); }
        private void OnEnable()
        {
            if (!attackPlayer) attackPlayer = GetComponent<AttackPlayer>();
            attackPlayer.Finished += OnFinished; CombatClock.Register(this);
            attackPlayer.Interrupted += OnInterrupted;
            if(reaction && reaction.health) reaction.health.Died+=ReleaseOnDeath;
        }
        void ReleaseOnDeath()=>ReleaseCoordination("Death");
        private void OnInterrupted(AttackData interrupted)
        {
            // An interrupted area telegraph consumes its cooldown too; ordinary attacks retain their existing rules.
            var grab = GetComponent<CombatGrabController>();
            if (grab && grab.SwitchingBranch) return;
            ReleaseCoordination("Interrupted");
            if (interrupted.cooldownOnInterrupt || interrupted.feedback != null && (interrupted.feedback.areaWarning || interrupted.feedback.directionalWaveWarning)) OnFinished(interrupted);
            AI?.CancelPendingAction();
        }
        private void OnFinished(AttackData finished)
        {
            var grab=GetComponent<CombatGrabController>();
            if(grab && finished==grab.successfulGrab) finished=grab.grabAttack;
            if(aiProfile) AI?.AttackFinished(finished); else cooldown=attackCooldownFrames+finished.cooldownFrames;
        }
        public void CombatFrame()
        {
            if (GetComponent<TotemBossController>()?.OwnsAI == true) return;
            if (!motor || !reaction || attackPlayer.IsFrozen) return;
            if(aiProfile) { if(AI==null) RefreshAI(); AI.Tick(); return; }
            if(!attackPlayer.CurrentAttack && coordinator?.Committed(this)==true) ReleaseCoordination("Recovery finished");
            if(PlayerRoster.Players.Any()) target=PlayerRoster.Nearest(transform.position)?.transform;
            if (cooldown > 0) cooldown--;
            if (!reaction.CanAct || passiveTrainingDummy || !target)
            {
                ReleaseCoordination("Cannot act / target invalid"); motor.MoveInput = Vector2.zero;
                if (attackPlayer.CurrentAttack) attackPlayer.Stop();
                if (reaction.CanAct) animationDriver.Play("Idle");
                return;
            }
            if (attackPlayer.CurrentAttack) { motor.MoveInput = Vector2.zero; return; }
            Vector2 delta = target.position - transform.position;
            motor.Face(delta.x);
            if (Mathf.Abs(delta.x) < minimumAttackRange)
            {
                motor.MoveInput = new Vector2(-Mathf.Sign(delta.x), 0);
                animationDriver.Play("Walk"); return;
            }
            if (Mathf.Abs(delta.x) <= attackRange && Mathf.Abs(delta.y) < laneRange)
            {
                motor.MoveInput = Vector2.zero;
                if (cooldown <= 0 && RequestCoordination(attack,legacyCoordination,10))
                { if(attackPlayer.Play(attack)) { motor.MovementLocked=true; ConfirmCoordination(); } else ReleaseCoordination("Play failed"); }
                else { motor.MoveInput=new Vector2(0,transform.position.y<target.position.y ? -.35f : .35f); animationDriver.Play("Walk"); }
            }
            else { motor.MoveInput = delta.normalized; animationDriver.Play("Walk"); }
        }
        private void OnDisable()
        {
            ReleaseCoordination("Disabled / despawned");
            if(reaction && reaction.health) reaction.health.Died-=ReleaseOnDeath;
            CombatClock.Unregister(this);
            if (attackPlayer) { attackPlayer.Finished -= OnFinished; attackPlayer.Interrupted -= OnInterrupted; attackPlayer.Stop(); }
            if (motor) motor.MoveInput = Vector2.zero;
            AI=null;
        }
    }
}
