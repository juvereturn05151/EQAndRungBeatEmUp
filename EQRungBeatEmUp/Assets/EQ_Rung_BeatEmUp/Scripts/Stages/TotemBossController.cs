using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
namespace BeatEmUp
{
    public enum BossEncounterState { Inactive, WarpOut, Warping, WarpIn, Arrival, SelectAction, Acting, Recovery, SmallMove, PhaseTransition, Dead }
    [RequireComponent(typeof(EnemyCombat))]
    public sealed class TotemBossController : MonoBehaviour, ICombatFrameListener
    {
        public StageFlowController flow;
        public BossEncounterData data;
        public UnityEvent onVulnerable = new UnityEvent(), onPhase2 = new UnityEvent(), onDefeated = new UnityEvent();
        public BossEncounterState State { get; private set; }
        public BossActionChoice Selected { get; private set; }
        public BossActionChoice PreviousAction { get; private set; }
        public bool Invulnerable => VulnerabilityRemaining <= 0;
        public int VulnerabilityRemaining { get; private set; }
        public bool Phase2 { get; private set; }
        public int PhaseTransitions { get; private set; }
        public int WarpsPerformed { get; private set; }
        public int LastWarpIndex { get; private set; } = -1;
        public bool OwnsAI => isActiveAndEnabled && data;
        public int FrameOrder => 5;
        public int ActiveMinions => minions.Count(h => h && h.gameObject.activeInHierarchy && !h.IsDead);
        public IReadOnlyList<BossTotem> Totems => totems;
        readonly List<CharacterHealth> minions = new List<CharacterHealth>();
        readonly List<BossTotem> totems = new List<BossTotem>();
        readonly List<CombatProjectile> projectiles = new List<CombatProjectile>();
        EnemyCombat combat; CombatHurtbox hurtbox; CharacterHealth health; EnemyProjectileAttack ranged;
        int remaining, shieldFlash;
        bool pendingPhase, summonReleased;
        long vulnerabilityTick = -1;
        Vector2 moveTarget;
        void Awake()
        {
            combat = GetComponent<EnemyCombat>(); health = GetComponent<CharacterHealth>();
            hurtbox = GetComponentInChildren<CombatHurtbox>(); ranged = GetComponent<EnemyProjectileAttack>();
        }
        void OnEnable()
        {
            CombatClock.Register(this);
            if (health) { health.Died += Die; health.Damaged += CheckPhase; }
            if (combat.attackPlayer) combat.attackPlayer.FrameEvent += Signal;
            ApplyGate();
        }
        void OnDisable()
        {
            CombatClock.Unregister(this);
            if (health) { health.Died -= Die; health.Damaged -= CheckPhase; health.BossDamageProtection = false; }
            if (combat && combat.attackPlayer) { combat.attackPlayer.FrameEvent -= Signal; combat.attackPlayer.Stop(); }
            if (hurtbox) hurtbox.externalInvulnerable = false;
            CleanupAttacks();
        }
        public void BindEncounter()
        {
            if (!flow || !data) return;
            foreach (var prop in flow.Destructibles.Where(p => p && p.kind == PropKind.CursedTotem))
            {
                var totem = prop.GetComponent<BossTotem>(); if (!totem) totem = prop.gameObject.AddComponent<BossTotem>();
                totem.Bind(this, data); if (!totems.Contains(totem)) totems.Add(totem);
            }
            ApplyGate();
        }
        void ApplyGate()
        {
            bool protect = OwnsAI && State != BossEncounterState.Dead && Invulnerable;
            if (health) health.BossDamageProtection = protect;
            if (hurtbox) hurtbox.externalInvulnerable = protect;
        }
        void CheckPhase()
        {
            if (!data || Phase2 || health.IsDead || health.Current / health.EffectiveMaximum > data.phase2HealthThreshold) return;
            Phase2 = true; pendingPhase = true; PhaseTransitions++; onPhase2.Invoke();
        }
        public bool ReceiveTotemWave(TotemBreakWave wave)
        {
            if (!OwnsAI || !wave || !wave.CanReach(this) || health.IsDead) return false;
            if (!Invulnerable && data.additionalWave == AdditionalTotemWave.Ignore) return false;
            VulnerabilityRemaining = Mathf.Max(1, data.vulnerabilityFrames); vulnerabilityTick = CombatClock.CurrentTick;
            ApplyGate(); Feedback(data.vulnerableFeedback); onVulnerable.Invoke(); return true;
        }
        public void ShieldHit() { if (!data) return; shieldFlash = 6; Feedback(data.shieldHitFeedback); }
        void Feedback(AttackData effect)
        {
            if (!effect) return;
            AttackFeedback.PlayRemote(effect, transform.position, combat.motor.Facing, true);
            MultiplayerSession.Active?.QueueEncounterFeedback(effect, transform.position, combat.motor.Facing, GetInstanceID());
        }
        void Enter(BossEncounterState next, int frames = 0)
        {
            if(next==BossEncounterState.PhaseTransition || next==BossEncounterState.WarpOut) { combat.ReleaseCoordination("Boss transition"); Selected=null; }
            State = next; remaining = Mathf.Max(0, frames); combat.motor.MoveInput = Vector2.zero; combat.motor.MovementLocked = true;
            if (next == BossEncounterState.WarpOut) { Feedback(data.warpOutFeedback); combat.animationDriver.Play("Warp_Start", true); }
            if (next == BossEncounterState.WarpIn) { Show(true); Feedback(data.warpInFeedback); combat.animationDriver.Play("Warp_Appear", true); }
            if (next == BossEncounterState.PhaseTransition) { Feedback(data.phaseFeedback); combat.animationDriver.Play("Attack_Summon", true); }
        }
        void Show(bool visible) { if (combat.motor.sprite) combat.motor.sprite.enabled = visible; }
        public Vector2 ClampPoint(Vector2 point)
        {
            var motor = combat.motor; var half = motor.wallCollisionSize * .5f;
            return new Vector2(Mathf.Clamp(point.x, motor.arenaMin.x + half.x, motor.arenaMax.x - half.x), Mathf.Clamp(point.y, motor.arenaMin.y + half.y, motor.arenaMax.y - half.y));
        }
        bool ClearPoint(Vector2 point) => !Physics2D.OverlapBoxAll(point + combat.motor.wallCollisionOffset, combat.motor.wallCollisionSize, 0, combat.motor.wallCollisionMask)
            .Any(c => !c.isTrigger && c.GetComponentInParent<CombatWall>()?.isActiveAndEnabled == true);
        void Warp()
        {
            var points = data.warpPoints.Select((p, i) => new { p, i }).Where(x => x.p != null && x.p.enabled && ClearPoint(ClampPoint(x.p.position))).ToList();
            if (!data.canRepeatWarpPoint && points.Count > 1) points.RemoveAll(x => x.i == LastWarpIndex);
            if (points.Count > 0)
            {
                var choice = points[Random.Range(0, points.Count)]; LastWarpIndex = choice.i;
                var point = ClampPoint(choice.p.position + Random.insideUnitCircle * data.warpOffset);
                if (!ClearPoint(point)) point = ClampPoint(choice.p.position);
                combat.motor.SnapGrabToGround(point); WarpsPerformed++;
            }
            Enter(BossEncounterState.WarpIn, data.warpInFrames);
        }
        public bool ActionAvailable(BossActionChoice choice)
        {
            if (choice == null || !choice.enabled || choice.weight <= 0 || !choice.attack || choice.attack.TotalFrames == 0 || !combat.target) return false;
            switch (choice.action)
            {
                case BossAction.Book: return ranged && data.bookProjectile;
                case BossAction.CurseWave: return Phase2 && ranged && data.curseWaveProjectile && data.curseWaveProjectile.groundWave;
                case BossAction.SummonRusher: return Slots > 0 && data.rusherCount > 0 && ValidMinion(data.rusherPrefab) && data.rusherPrefab.GetComponent<EnemyCombat>().role == EnemyRole.Rusher;
                case BossAction.SummonStrongGhosts: return Phase2 && Slots > 0 && (data.grapplerCount > 0 && ValidMinion(data.grapplerPrefab) || data.throwerCount > 0 && ValidMinion(data.throwerPrefab));
                default: return true;
            }
        }
        static bool ValidMinion(GameObject prefab) => prefab && prefab.GetComponent<EnemyCombat>() && prefab.GetComponent<CharacterHealth>() && prefab.GetComponent<CharacterMotor>();
        public BossActionChoice ChooseAction(float unitRoll)
        {
            var pool = (Phase2 ? data.phase2 : data.phase1).Where(ActionAvailable).ToList();
            float roll = Mathf.Clamp01(unitRoll) * pool.Sum(p => p.weight);
            foreach (var choice in pool) { roll -= choice.weight; if (roll < 0) return choice; }
            return pool.LastOrDefault();
        }
        int Slots => flow ? Mathf.Max(0, Mathf.Min(data.maxBossMinions - ActiveMinions, flow.ReinforcementSlots(combat))) : 0;
        bool BeginAction(BossActionChoice choice)
        {
            if (!ActionAvailable(choice)) return false;
            if(!combat.RequestCoordination(choice.attack,choice.coordination,20)) return false;
            Selected = choice; summonReleased = false; combat.motor.Face(combat.target.position.x - transform.position.x);
            combat.motor.StopGroundedMotion();
            if (ranged)
            {
                ranged.projectilePrefab = choice.action == BossAction.CurseWave ? data.curseWaveProjectile : data.bookProjectile;
                ranged.releaseOffset = choice.action == BossAction.CurseWave ? data.curseSpawnOffset : data.bookSpawnOffset;
                ranged.forwardOnly = choice.action == BossAction.CurseWave;
                ranged.releaseEvent = choice.action == BossAction.CurseWave ? "SpawnScreamWave" : "ThrowProjectile";
                ranged.lockAimAtAttackStart = true;
            }
            Enter(BossEncounterState.Acting);
            if (combat.attackPlayer.Play(choice.attack)) { combat.ConfirmCoordination(); return true; }
            combat.ReleaseCoordination("Boss play failed");
            Enter(BossEncounterState.Recovery, data.warpCooldownFrames); return false;
        }
        public void TrackProjectile(CombatProjectile shot) { if (shot) projectiles.Add(shot); }
        void Signal(string signal)
        {
            if (signal != "BossSummon" || summonReleased || State != BossEncounterState.Acting || Selected == null || health.IsDead) return;
            summonReleased = true;
            if (Selected.action == BossAction.SummonRusher) Summon(data.rusherPrefab, data.rusherCount);
            if (Selected.action == BossAction.SummonStrongGhosts)
            {
                bool first = data.strongGhostPriority == StrongGhostPriority.GrapplerFirst || data.strongGhostPriority == StrongGhostPriority.Random && Random.value < .5f;
                if (first) { Summon(data.grapplerPrefab, data.grapplerCount); Summon(data.throwerPrefab, data.throwerCount); }
                else { Summon(data.throwerPrefab, data.throwerCount); Summon(data.grapplerPrefab, data.grapplerCount); }
            }
        }
        void Summon(GameObject prefab, int count)
        {
            if (!flow || !prefab || Slots <= 0) return;
            minions.RemoveAll(h => !h || h.IsDead || !h.gameObject.activeInHierarchy);
            foreach (var spawned in flow.RequestBossMinions(combat, prefab, Mathf.Min(count, Slots), data.minionSpawnPoints, data.playerSpawnClearance, data.enemySpawnClearance)) minions.Add(spawned);
        }
        void SmallMove()
        {
            if (data.moveSpeed <= 0 || data.moveDistance <= 0 || Random.value >= data.moveChance) { Enter(BossEncounterState.WarpOut, data.warpOutFrames); return; }
            moveTarget = ClampPoint((Vector2)transform.position + Random.insideUnitCircle.normalized * data.moveDistance);
            Enter(BossEncounterState.SmallMove, Mathf.CeilToInt(data.moveDistance / data.moveSpeed / CombatClock.FrameSeconds)); combat.animationDriver.Play("Glide", true);
        }
        public void CombatFrame()
        {
            if (!data || CombatClock.IsPaused || MultiplayerSession.Active && !MultiplayerSession.Active.IsAuthority) return;
            if (health.IsDead) { Die(); return; }
            if (VulnerabilityRemaining > 0 && vulnerabilityTick != CombatClock.CurrentTick) { VulnerabilityRemaining--; ApplyGate(); }
            if (combat.motor.sprite) combat.motor.sprite.color = shieldFlash > 0 ? Color.white : Invulnerable ? data.shieldColor : data.vulnerableColor;
            shieldFlash = Mathf.Max(0, shieldFlash - 1);
            CheckPhase(); var target = PlayerRoster.Nearest(transform.position); if (target) combat.target = target.transform;
            if (combat.attackPlayer.IsFrozen || !combat.reaction.CanAct) return;
            combat.motor.MovementLocked = true; combat.motor.MoveInput = Vector2.zero;
            if (!combat.target || combat.target.GetComponent<CharacterHealth>()?.IsDead == true) { combat.motor.MoveInput = Vector2.zero; return; }
            if (pendingPhase && State != BossEncounterState.Acting && State != BossEncounterState.WarpOut && State != BossEncounterState.Warping && State != BossEncounterState.WarpIn)
            { pendingPhase = false; Show(true); Enter(BossEncounterState.PhaseTransition, data.phaseTransitionFrames); return; }
            switch (State)
            {
                case BossEncounterState.Inactive: BindEncounter(); Enter(BossEncounterState.WarpOut, data.warpOutFrames); break;
                case BossEncounterState.WarpOut: if (--remaining <= 0) { Show(false); Enter(BossEncounterState.Warping); } break;
                case BossEncounterState.Warping: Warp(); break;
                case BossEncounterState.WarpIn: if (--remaining <= 0) Enter(BossEncounterState.Arrival, data.arrivalFrames); break;
                case BossEncounterState.Arrival: if (--remaining <= 0) Enter(BossEncounterState.SelectAction); break;
                case BossEncounterState.SelectAction:
                    var candidate=ActionAvailable(Selected) ? Selected : ChooseAction(Random.value); Selected=candidate;
                    if(candidate==null) Enter(BossEncounterState.Recovery, Mathf.Max(1,data.warpCooldownFrames));
                    else BeginAction(candidate);
                    break;
                case BossEncounterState.Acting: if (!combat.attackPlayer.CurrentAttack) { PreviousAction = Selected; Selected = null; Enter(BossEncounterState.Recovery, data.warpCooldownFrames); combat.animationDriver.Play("Recovery_Rise", true); } break;
                case BossEncounterState.Recovery: if (--remaining <= 0) { combat.ReleaseCoordination("Boss recovery finished"); SmallMove(); } break;
                case BossEncounterState.SmallMove:
                    var next = Vector2.MoveTowards(transform.position, moveTarget, data.moveSpeed * CombatClock.FrameSeconds);
                    if (ClearPoint(next)) combat.motor.SnapGrabToGround(next);
                    if (--remaining <= 0 || Vector2.Distance(transform.position, moveTarget) < .01f) Enter(BossEncounterState.WarpOut, data.warpOutFrames);
                    break;
                case BossEncounterState.PhaseTransition: if (--remaining <= 0) Enter(BossEncounterState.SelectAction); break;
            }
        }
        void CleanupAttacks()
        {
            foreach (var shot in projectiles) if (shot) { shot.gameObject.SetActive(false); Destroy(shot.gameObject); }
            projectiles.Clear();
        }
        void Die()
        {
            if (State == BossEncounterState.Dead) return;
            State = BossEncounterState.Dead; Selected = null; pendingPhase = false; VulnerabilityRemaining = 0;
            combat.attackPlayer.Stop(); combat.motor.MoveInput = Vector2.zero; Show(true); ApplyGate(); CleanupAttacks();
            foreach (var totem in totems) if (totem) totem.StopEncounter();
            if (data && data.despawnMinionsOnDeath) foreach (var minion in minions) if (minion) minion.gameObject.SetActive(false);
            onDefeated.Invoke();
        }
#if UNITY_EDITOR
        public bool ForceAction(BossAction action)
        {
            if (!Application.isPlaying || !data || health.IsDead || !combat.reaction.CanAct || combat.attackPlayer.IsFrozen) return false;
            var choice = (Phase2 ? data.phase2 : data.phase1).FirstOrDefault(c => c.action == action && ActionAvailable(c));
            if (choice == null) return false;
            combat.attackPlayer.Stop(); Show(true); return BeginAction(choice);
        }
        public void ForceWarp() { if (Application.isPlaying && OwnsAI && !health.IsDead && combat.reaction.CanAct) { combat.attackPlayer.Stop(); Enter(BossEncounterState.WarpOut, data.warpOutFrames); } }
        public void ForcePhase2() { if (Application.isPlaying && OwnsAI && !health.IsDead && !Phase2) { Phase2 = true; pendingPhase = true; PhaseTransitions++; onPhase2.Invoke(); } }
        public void DebugVulnerable() { if (Application.isPlaying && OwnsAI && !health.IsDead) { VulnerabilityRemaining = data.vulnerabilityFrames; vulnerabilityTick = CombatClock.CurrentTick; ApplyGate(); } }
        public void DebugInvulnerable() { VulnerabilityRemaining = 0; ApplyGate(); }
#endif
    }
}
