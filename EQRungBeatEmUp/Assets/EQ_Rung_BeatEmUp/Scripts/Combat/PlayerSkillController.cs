using UnityEngine;

namespace BeatEmUp
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerMeter), typeof(ComboController))]
    public sealed class PlayerSkillController : MonoBehaviour
    {
        public PlayerSkillData equippedSkill;
        public int ProjectilesReleased { get; private set; }
        public CombatProjectile LastProjectile { get; private set; }
        public int AreaReleases { get; private set; }
        public int GuardiansManifested { get; private set; }
        ComboController combat;
        PlayerMeter meter;
        PlayerSkillData casting;
        bool released, guardianReleased;
        void OnEnable()
        {
            combat = GetComponent<ComboController>(); meter = GetComponent<PlayerMeter>();
            combat.attackPlayer.FrameEvent += Signal; combat.attackPlayer.Stopped += Stopped;
        }
        void OnDisable()
        {
            if (combat && combat.attackPlayer) { combat.attackPlayer.FrameEvent -= Signal; combat.attackPlayer.Stopped -= Stopped; }
            casting = null;
        }
        public bool RequestSkill() => RequestSkill(equippedSkill);
        public bool RequestSkill(PlayerSkillData skill)
        {
            var session = MultiplayerSession.Active;
            if (!isActiveAndEnabled || (session && !session.IsAuthority) || !skill || !skill.cast || (skill.delivery == PlayerSkillDelivery.Projectile && !skill.projectilePrefab) ||
                !combat.CanStartSkill || !meter.CanSpend(skill.meterCost)) return false;
            // Set before Play: frame zero can legitimately contain a release event.
            casting = skill; released = guardianReleased = false;
            if (!combat.StartSkill(skill.cast)) { casting = null; return false; }
            meter.TrySpend(skill.meterCost); return true;
        }
        void Stopped(AttackData _) { casting = null; }
        void Signal(string signal)
        {
            if (!casting || combat.CurrentAttack != casting.cast || CombatClock.IsPaused) return;
            if (casting.delivery == PlayerSkillDelivery.Area && !guardianReleased && signal == casting.guardianEvent)
            {
                guardianReleased = true; GuardiansManifested++;
                Feedback(casting.guardianFeedback);
            }
            if (released || signal != casting.releaseEvent) return;
            released = true;
            if (casting.delivery == PlayerSkillDelivery.Area)
            {
                AreaReleases++; Feedback(casting.releaseFeedback);
                return; // Existing AttackHitbox samples the cast's active area frames.
            }
            var source = combat.motor;
            int facing = combat.attackPlayer.Facing;
            var origin = (Vector2)source.transform.position + Vector2.right * (casting.spawnOffset.x * facing);
            // Stage-owned parent allows existing room cleanup; no projectile simulation on clients.
            var flow = FindFirstObjectByType<StageFlowController>();
            LastProjectile = Instantiate(casting.projectilePrefab, flow && flow.isActiveAndEnabled ? flow.SpawnedAttackRoot : transform.parent);
            LastProjectile.skillSource=casting;
            LastProjectile.InitializeForward(source, combat.hitbox.team, origin, source.Height + casting.spawnOffset.y, facing);
            ProjectilesReleased++;
        }
        void Feedback(AttackData effect)
        {
            if (!effect) return;
            var point = (Vector2)combat.motor.transform.position;
            AttackFeedback.PlayRemote(effect, point, combat.attackPlayer.Facing, true);
            MultiplayerSession.Active?.QueueEncounterFeedback(effect, point, combat.attackPlayer.Facing, GetInstanceID());
        }
    }
}
