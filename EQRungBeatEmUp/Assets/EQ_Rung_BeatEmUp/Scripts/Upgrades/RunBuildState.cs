using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    [RequireComponent(typeof(ComboController), typeof(CharacterHealth))]
    public sealed class RunBuildState : MonoBehaviour
    {
        [Serializable] public sealed class Stack { public UpgradeDefinition upgrade; public int count; }
        [SerializeField] private List<Stack> acquired = new List<Stack>();
        public IReadOnlyList<Stack> Acquired => acquired;
        public IReadOnlyDictionary<RunModifier, float> Modifiers => modifiers;
        public event Action Changed;
        public event Action<RunCombatEvent> CombatEvent;
        public int LethalSavesUsed { get; private set; }
        private readonly Dictionary<RunModifier, float> modifiers = new Dictionary<RunModifier, float>();
        private readonly HashSet<string> tags = new HashSet<string>();
        private ComboController player;
        private CharacterHealth health;
        private float pendingAttackBonus, activeAttackBonus;
        private bool dodgeRewarded, diveConnected;
        private AttackData activeAttack;
        private void Awake() { player = GetComponent<ComboController>(); health = GetComponent<CharacterHealth>(); Rebuild(); }
        public float Value(RunModifier modifier) => modifiers.TryGetValue(modifier, out float value) ? value : 0;
        public bool HasTag(string tag) => tags.Contains(tag);
        public int Stacks(UpgradeDefinition upgrade) => upgrade ? acquired.Where(s => s.upgrade && s.upgrade.id == upgrade.id).Sum(s => s.count) : 0;
        public bool CanAcquire(UpgradeDefinition upgrade) => upgrade && !string.IsNullOrEmpty(upgrade.id) && Stacks(upgrade) < Mathf.Max(1, upgrade.maxStacks) &&
            upgrade.prerequisites.All(u => u && Stacks(u) > 0) &&
            !acquired.Any(s => s.upgrade && s.count > 0 && (upgrade.incompatibleUpgrades.Any(u => u && u.id == s.upgrade.id) || s.upgrade.incompatibleUpgrades.Any(u => u && u.id == upgrade.id)));
        public bool Acquire(UpgradeDefinition upgrade)
        {
            if (!CanAcquire(upgrade)) return false;
            float oldHealthBonus = Value(RunModifier.MaximumHealthBonus);
            var stack = acquired.Find(s => s.upgrade && s.upgrade.id == upgrade.id);
            if (stack == null) { stack = new Stack { upgrade = upgrade }; acquired.Add(stack); }
            stack.count++; Rebuild();
            health.Heal(Mathf.Max(0, Value(RunModifier.MaximumHealthBonus) - oldHealthBonus)); Changed?.Invoke(); return true;
        }
        private void Rebuild()
        {
            modifiers.Clear(); tags.Clear();
            foreach (var stack in acquired.Where(s => s.upgrade && s.count > 0))
            {
                foreach (string tag in stack.upgrade.tags) tags.Add(tag);
                foreach (var effect in stack.upgrade.effects) modifiers[effect.modifier] = Value(effect.modifier) + effect.amount * stack.count;
            }
        }
        public void ResetRun()
        {
            acquired.Clear(); modifiers.Clear(); tags.Clear(); LethalSavesUsed = 0;
            ClearTransient(); health.ClampToMaximum(); Changed?.Invoke();
        }
        public void ClearTransient() { pendingAttackBonus = activeAttackBonus = 0; activeAttack = null; diveConnected = dodgeRewarded = false; }
        public void AttackStarted(AttackData attack)
        {
            activeAttack = attack; activeAttackBonus = pendingAttackBonus; pendingAttackBonus = 0;
            if (attack == player.airDive) diveConnected = false;
        }
        public void AttackStopped() { activeAttack = null; activeAttackBonus = 0; }
        public AttackHitboxData ModifyHit(AttackHitboxData authored, CharacterMotor target)
        {
            // Copy every authored property; shared ScriptableObject data is never edited.
            var hit = authored.RuntimeCopy();
            var attack = player.CurrentAttack;
            if (!attack) return hit;
            float bonus = activeAttack == attack ? activeAttackBonus : 0;
            if (attack.domain == AttackDomain.Ground && !attack.isLauncher) bonus += Value(RunModifier.GroundDamage);
            bonus += Value(RunModifier.ComboDamagePerStep) * Mathf.Max(0, player.ComboIndex - 1);
            if (player.groundCombo.Length > 0 && attack == player.groundCombo[player.groundCombo.Length - 1]) bonus += Value(RunModifier.GroundFinisherDamage);
            if (target && !target.IsGrounded) bonus += Value(RunModifier.AirborneTargetDamage);
            if (attack.isLauncher) bonus += Value(RunModifier.LauncherDamage);
            if (attack == player.airDive) bonus += Value(RunModifier.DiveDamage);
            if (player.airCombo.Length > 0 && attack == player.airCombo[player.airCombo.Length - 1])
            {
                bonus += Value(RunModifier.AirFinisherDamage);
                hit.launchVelocity.y = -(hit.launchVelocity.y < 0 ? -hit.launchVelocity.y : 3) * (1 + Value(RunModifier.AirFinisherFallSpeed));
            }
            hit.damage *= Mathf.Max(0, 1 + bonus); return hit;
        }
        public void AttackHit(CharacterHealth victim, CombatHitOutcome outcome)
        {
            if (outcome != CombatHitOutcome.Hit && outcome != CombatHitOutcome.Armor && outcome != CombatHitOutcome.ArmorBreak) return;
            if (player.IsAirDiving) diveConnected = true;
            CombatEvent?.Invoke(RunCombatEvent.AttackHit);
            if (victim && victim.IsDead) CombatEvent?.Invoke(RunCombatEvent.EnemyKilled);
        }
        public void OnParry() { pendingAttackBonus = Mathf.Max(pendingAttackBonus, Value(RunModifier.ParryAttackBonus)); CombatEvent?.Invoke(RunCombatEvent.Parry); }
        public void OnBlock() => CombatEvent?.Invoke(RunCombatEvent.Block);
        public void DodgeStarted() => dodgeRewarded = false;
        public void DodgeSucceeded()
        {
            if (dodgeRewarded) return; dodgeRewarded = true;
            pendingAttackBonus = Mathf.Max(pendingAttackBonus, Value(RunModifier.DodgeAttackBonus)); CombatEvent?.Invoke(RunCombatEvent.DodgeSuccess);
        }
        public void DiveImpact()
        {
            CombatEvent?.Invoke(RunCombatEvent.AirDiveImpact);
            float damage = Value(RunModifier.DiveShockwaveDamage);
            if (!diveConnected || damage <= 0) return;
            diveConnected = false;
            var pulse = new GameObject("Dive shockwave"); pulse.transform.position = player.transform.position; pulse.AddComponent<DiveShockwavePulse>();
            var hit = new AttackHitboxData { damage = damage, knockback = .5f, hitstunFrames = 12, hitstopFrames = 3, canHitAirborne = true, laneTolerance = .45f };
            var victims = new HashSet<CharacterHealth>();
            Physics2D.SyncTransforms();
            foreach (var collider in Physics2D.OverlapBoxAll(player.transform.position + new Vector3(0, .35f), new Vector2(1.8f, .8f), 0))
            {
                var hurt = collider.GetComponent<CombatHurtbox>();
                if (!hurt || hurt.team == player.hitbox.team || !hurt.health || !victims.Add(hurt.health) || Mathf.Abs(hurt.motor.transform.position.y - player.transform.position.y) > hit.laneTolerance) continue;
                if (hurt.Receive(hit, player.motor.Facing, player.motor)) { player.attackPlayer.Freeze(hurt.LastHitstopFrames); hurt.motor.attackPlayer?.Freeze(hurt.LastHitstopFrames); AttackHit(hurt.health, hurt.LastHitOutcome); }
            }
        }
        public bool TrySaveLethalHit()
        {
            if (LethalSavesUsed >= Mathf.FloorToInt(Value(RunModifier.LethalSaves))) return false;
            LethalSavesUsed++; return true;
        }
        public void Notify(RunCombatEvent combatEvent) => CombatEvent?.Invoke(combatEvent);
        public string DescribeModifiers() => string.Join("\n", modifiers.OrderBy(p => p.Key).Select(p => p.Key + ": " + p.Value.ToString("0.##")));
    }
    public enum RunCombatEvent { AttackHit, Parry, Block, DodgeSuccess, AirDiveImpact, ComboFinished, EnemyKilled, StageClear }
}
