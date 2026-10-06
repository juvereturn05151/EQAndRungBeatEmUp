using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatEmUp
{
    [DisallowMultipleComponent]
    public sealed class ComboTracker : MonoBehaviour, ICombatFrameListener
    {
        [Min(.1f)] public float timeoutSeconds = .9f;
        public bool debugLog;
        public bool IsActive { get; private set; }
        public int HitCount { get; private set; }
        public float TotalDamage { get; private set; }
        public float RemainingSeconds { get; private set; }
        public CharacterHealth LastTarget { get; private set; }
        public int BestHitCount { get; private set; }
        public string LastEndReason { get; private set; }
        public event Action Changed, Ended, Reset;
        public event Action<CombatHurtbox, AttackHitboxData> AcceptedHit;
        public int FrameOrder => 80; // After hits, reactions and motion, before stage transition.
        readonly Dictionary<CharacterHealth, EnemyHitReaction> targets = new Dictionary<CharacterHealth, EnemyHitReaction>();
        CharacterHealth health;
        AttackPlayer playback;
        long lastHitTick = -1;
        void Awake()
        {
            if (!GetComponent<ComboUIController>()) gameObject.AddComponent<ComboUIController>();
        }
        void OnEnable()
        {
            health = GetComponent<CharacterHealth>(); playback = GetComponent<AttackPlayer>();
            if (health) { health.Damaged += Interrupted; health.Died += Died; health.Restored += ResetTracking; }
            CombatClock.Register(this);
        }
        void OnDisable()
        {
            CombatClock.Unregister(this);
            if (health) { health.Damaged -= Interrupted; health.Died -= Died; health.Restored -= ResetTracking; }
            ResetTracking();
        }
        void Interrupted() => EndCombo("Player interrupted");
        void Died() => EndCombo("Player defeated");
        public void RecordHit(CombatHurtbox target, float actualDamage, AttackHitboxData hit = null)
        {
            var ownHitbox = GetComponent<AttackHitbox>();
            if (!isActiveAndEnabled || CombatClock.IsPaused || !target || !target.health || !target.enemy ||
                !ownHitbox || target.team == ownHitbox.team || actualDamage <= 0 || (health && health.IsDead)) return;
            if (!IsActive)
            {
                HitCount = 0; TotalDamage = 0; targets.Clear(); LastEndReason = null; IsActive = true;
                if (debugLog) Debug.Log("Combo started", this);
            }
            targets[target.health] = target.enemy; LastTarget = target.health;
            HitCount++; TotalDamage += actualDamage; BestHitCount = Mathf.Max(BestHitCount, HitCount);
            RemainingSeconds = timeoutSeconds; lastHitTick = CombatClock.CurrentTick;
            AcceptedHit?.Invoke(target, hit);
            Changed?.Invoke();
        }
        public void CombatFrame()
        {
            if (!IsActive || CombatClock.IsPaused) return;
            bool validTarget = false, livingTarget = false;
            foreach (var pair in targets)
            {
                if (!pair.Key || !pair.Value || !pair.Key.gameObject.activeInHierarchy) continue;
                if (pair.Key.IsDead) continue;
                livingTarget = true;
                if (!pair.Value.CanAct) validTarget = true;
            }
            // Resolve death after the entire hit batch, so multi-target lethal hits accumulate together.
            if (!livingTarget) { EndCombo("Targets defeated or removed"); return; }
            if (!validTarget) { EndCombo("Targets recovered"); return; }
            if (playback && playback.IsFrozen) return;
            if (CombatClock.CurrentTick != lastHitTick) RemainingSeconds = Mathf.Max(0, RemainingSeconds - CombatClock.FrameSeconds);
            if (RemainingSeconds <= .00001f) EndCombo("Timeout");
        }
        public void EndCombo(string reason)
        {
            if (!IsActive) return;
            IsActive = false; RemainingSeconds = 0; LastEndReason = reason; targets.Clear();
            if (debugLog) Debug.Log($"Combo ended: {HitCount} hits / {TotalDamage:0.##} damage — {reason}", this);
            Ended?.Invoke();
        }
        public void ResetTracking()
        {
            IsActive = false; HitCount = 0; TotalDamage = RemainingSeconds = 0;
            LastTarget = null; LastEndReason = null; lastHitTick = -1; targets.Clear(); Reset?.Invoke();
        }
    }
}
