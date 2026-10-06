using UnityEngine;

namespace BeatEmUp
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyCombat))]
    public sealed class EnemyProjectileAttack : MonoBehaviour
    {
        public CombatProjectile projectilePrefab;
        [Tooltip("X follows facing; Y is height above the walking lane.")]
        public Vector2 releaseOffset = new Vector2(.45f, .78f);
        public string releaseEvent = "ThrowProjectile";
        public bool lockAimAtAttackStart;
        Vector2 lockedAim;
        float lockedHeight;
        [Tooltip("Uses attack-start facing; no target tracking or homing.")] public bool forwardOnly;
        public CombatProjectile LastProjectile { get; private set; }
        public int ProjectilesReleased { get; private set; }
        EnemyCombat brain;
        AttackPlayer playback;
        bool released;
        void OnEnable()
        {
            brain = GetComponent<EnemyCombat>(); playback = GetComponent<AttackPlayer>();
            playback.FrameEvent += Signal; playback.Started += Started;
        }
        void OnDisable()
        {
            if (playback) { playback.FrameEvent -= Signal; playback.Started -= Started; }
        }
        void Started(AttackData _)
        {
            released = false;
            if (brain.target) { lockedAim = brain.target.position; lockedHeight = brain.target.GetComponent<CharacterMotor>()?.Height ?? 0; }
        }
        void Signal(string signal)
        {
            if (signal != releaseEvent || released || CombatClock.IsPaused || !brain.enabled || brain.passiveTrainingDummy ||
                !brain.target || !brain.reaction.CanAct || !projectilePrefab) return;
            var boss = GetComponent<TotemBossController>();
            if (boss && boss.OwnsAI)
            {
                if (boss.State != BossEncounterState.Acting || boss.Selected == null || playback.CurrentAttack != boss.Selected.attack ||
                    boss.Selected.action != BossAction.Book && boss.Selected.action != BossAction.CurseWave) return;
            }
            else if(brain.aiProfile ? brain.AI?.Selected==null || !brain.AI.Selected.projectile || playback.CurrentAttack!=brain.AI.Selected.attack : playback.CurrentAttack!=brain.attack) return;
            var health = brain.target.GetComponent<CharacterHealth>();
            if (health && health.IsDead) return;
            released = true;
            Vector2 origin = (Vector2)brain.motor.transform.position + Vector2.right * releaseOffset.x * playback.Facing;
            // The room owns projectiles, not the enemy: stage replacement cleans them up.
            var shot = Instantiate(projectilePrefab, brain.transform.parent);
            LastProjectile = shot;
            boss?.TrackProjectile(shot);
            if (forwardOnly)
            {
                shot.InitializeForward(brain.motor, brain.hitbox.team, origin, brain.motor.Height + releaseOffset.y, playback.Facing);
                // Mirror the whole visual arrangement, including its trailing crescents.
                if (shot.visual) shot.visual.flipX = false;
                shot.transform.localScale = new Vector3(playback.Facing, 1, 1);
            }
            else if (lockAimAtAttackStart) shot.InitializeAtPoint(brain.motor, brain.hitbox.team, origin, brain.motor.Height + releaseOffset.y, lockedAim, lockedHeight);
            else shot.Initialize(brain.motor, brain.hitbox.team, origin, brain.motor.Height + releaseOffset.y, brain.target);
            ProjectilesReleased++;
        }
    }
}
