using UnityEngine;
using UnityEngine.Events;

namespace BeatEmUp
{
    [RequireComponent(typeof(EnemyCombat))]
    public sealed class TotemBossController : MonoBehaviour, ICombatFrameListener
    {
        public StageFlowController flow;
        [Min(1)] public float warpInterval = 6;
        public UnityEvent onVulnerable = new UnityEvent();
        public bool Invulnerable { get; private set; }
        public int WarpsPerformed { get; private set; }
        public int FrameOrder => 5;
        private EnemyCombat combat;
        private CombatHurtbox hurtbox;
        private float elapsed, warpTime = -1;
        private bool gateInitialized, wasProtected, moved;
        private void Awake() { combat = GetComponent<EnemyCombat>(); hurtbox = GetComponentInChildren<CombatHurtbox>(); }
        private void OnEnable() => CombatClock.Register(this);
        private void OnDisable() { CombatClock.Unregister(this); if (hurtbox) hurtbox.externalInvulnerable = false; }
        public void CombatFrame()
        {
            if (!flow || !combat || !hurtbox || hurtbox.health.IsDead) return;
            Invulnerable = flow.RemainingTotems > 0; hurtbox.externalInvulnerable = Invulnerable;
            if (gateInitialized && wasProtected && !Invulnerable) onVulnerable.Invoke();
            gateInitialized = true; wasProtected = Invulnerable;
            if (combat.motor.sprite) combat.motor.sprite.color = Invulnerable ? new Color(.72f, .55f, 1, .8f) : Color.white;
            if (combat.attackPlayer.IsFrozen) return;
            if (!combat.reaction.CanAct)
            {
                if (warpTime >= 0) { warpTime = -1; combat.enabled = true; }
                return;
            }
            if (warpTime >= 0)
            {
                warpTime += CombatClock.FrameSeconds;
                if (!moved && warpTime >= .4f)
                {
                    moved = true; WarpsPerformed++;
                    Vector2 target = new Vector2(combat.motor.transform.position.x < 0 ? 2.1f : -2.1f, Random.Range(combat.motor.arenaMin.y, combat.motor.arenaMax.y));
                    combat.motor.ResetForStage(target); combat.motor.MovementLocked = true;
                    combat.animationDriver.Play("Warp_Appear", true);
                }
                if (warpTime >= .8f) { warpTime = -1; combat.motor.MovementLocked = false; combat.enabled = true; }
                return;
            }
            elapsed += CombatClock.FrameSeconds;
            if (elapsed >= warpInterval && !combat.attackPlayer.CurrentAttack)
            {
                elapsed = 0; warpTime = 0; moved = false; combat.enabled = false;
                combat.motor.MovementLocked = true; combat.animationDriver.Play("Warp_Start", true);
            }
        }
    }
}
