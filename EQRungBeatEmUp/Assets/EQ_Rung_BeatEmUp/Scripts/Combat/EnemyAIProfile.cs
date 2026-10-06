using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    public enum EnemyAIAction { Idle, Approach, Retreat, LaneAlign, FaceTarget, Wait, Reposition, WeightedAttack, UseAttack, ProjectileAttack }
    public enum EnemyAICondition { Always, HasTarget, NoTarget, InAggroRange, OutOfAggroRange, LaneAligned, NotLaneAligned, InMeleeRange, InProjectileRange, TooClose, TooFar, CooldownReady, TookDamage, HpBelow, AttackFinished, RecoveryFinished, KnockedDown, Dead, RandomChance, TimerFinished, CallBackupReady, EmergencyPushReady, SupportRetreatNeeded, SupportHasSpace }
    public enum EnemyAITargetRule { NearestLiving, KeepUntilLost }
    public enum EnemyAIStateRole { Normal, Hurt, Knockdown, GetUp, Dead }
    [Serializable] public sealed class EnemyAIRequirement
    {
        public EnemyAICondition condition;
        [Tooltip("HP fraction or random probability (0–1).")][Range(0,1)] public float value=.5f;
        public bool invert;
    }
    [Serializable] public sealed class EnemyAITransition
    {
        public string targetState="Idle";
        [Tooltip("All conditions must pass. First matching transition wins; empty means unconditional.")]
        public List<EnemyAIRequirement> conditions=new List<EnemyAIRequirement>();
    }
    [Serializable] public sealed class EnemyAIAttackChoice
    {
        public string id="Attack";
        public AttackData attack;
        public bool projectile;
        public AttackCoordinationData coordination=new AttackCoordinationData();
        [Min(0)] public float minimumRange;
        [Min(.01f)] public float maximumRange=1;
        [Min(.01f)] public float laneTolerance=.55f;
        [Min(0)] public float cooldown=1;
        [Min(0)] public float weight=1;
        public List<EnemyAIRequirement> prerequisites=new List<EnemyAIRequirement>();
    }
    [Serializable] public sealed class EnemyAIState
    {
        public string id="Idle";
        public EnemyAIStateRole role;
        public EnemyAIAction action;
        [Min(0)] public float duration=.25f;
        [Min(0)] public float movementScale=1;
        public bool faceTarget=true;
        [Tooltip("Choice ID for UseAttack / ProjectileAttack. WeightedAttack uses the whole choice list.")]
        public string attackChoice;
        public List<EnemyAITransition> transitions=new List<EnemyAITransition>();
    }
    [CreateAssetMenu(menuName="Beat Em Up/Enemy AI Profile")]
    public sealed class EnemyAIProfile : ScriptableObject
    {
        public string defaultState="Idle";
        [Min(.01f)] public float aggroRange=8;
        [Min(.01f)] public float loseTargetRange=10;
        public EnemyAITargetRule targetRule;
        [Min(.01f)] public float meleeRange=1;
        [Tooltip("Use XY ground distance for range and retreat checks instead of horizontal distance.")] public bool useGroundPlaneRange;
        [Min(.01f)] public float projectileRange=6;
        [Min(0)] public float preferredDistance=.75f;
        [Min(.01f)] public float laneTolerance=.55f;
        [Min(0)] public float reactionDelay=.1f;
        [Min(0)] public float recoveryTime=.15f;
        [Tooltip("When enabled, an attack finishes its authored timeline before normal transitions are evaluated. Damage always interrupts it.")]
        public bool commitToAttack=true;
        public List<EnemyAIState> states=new List<EnemyAIState>();
        public List<EnemyAIAttackChoice> attacks=new List<EnemyAIAttackChoice>();
        public EnemyAIState Find(string id) => states?.FirstOrDefault(s=>s!=null && s.id==id);
        public IEnumerable<string> Validate()
        {
            if(Find(defaultState)==null) yield return "Missing default state: "+defaultState;
            else if(Find(defaultState).role!=EnemyAIStateRole.Normal) yield return "Default state must have the Normal role.";
            if(loseTargetRange<aggroRange) yield return "Lose target range should be at least aggro range.";
            foreach(var group in states.Where(s=>s!=null).GroupBy(s=>s.id)) if(string.IsNullOrWhiteSpace(group.Key) || group.Count()>1) yield return "Empty or duplicate state ID: "+group.Key;
            foreach(var group in attacks.Where(a=>a!=null).GroupBy(a=>a.id)) if(string.IsNullOrWhiteSpace(group.Key) || group.Count()>1) yield return "Empty or duplicate attack choice ID: "+group.Key;
            foreach(var s in states.Where(s=>s!=null))
            {
                if(s.role==EnemyAIStateRole.Normal && s.transitions.Count==0) yield return s.id+": no transitions (terminal / stationary state).";
                foreach(var t in s.transitions)
                {
                    if(t==null || Find(t.targetState)==null) yield return s.id+": transition has a missing destination.";
                    else if(Find(t.targetState).role!=EnemyAIStateRole.Normal) yield return s.id+": combat interrupt destinations are selected by the reaction system, not normal transitions.";
                }
                if((s.action==EnemyAIAction.UseAttack || s.action==EnemyAIAction.ProjectileAttack) && !attacks.Any(a=>a!=null && a.id==s.attackChoice)) yield return s.id+": missing attack choice.";
                if(s.action==EnemyAIAction.WeightedAttack && attacks.Count==0) yield return s.id+": no weighted attack choices.";
            }
            foreach(var a in attacks.Where(a=>a!=null))
            {
                if(!a.attack || a.attack.TotalFrames==0) yield return a.id+": missing or empty AttackData.";
                if(a.minimumRange>a.maximumRange) yield return a.id+": minimum range exceeds maximum.";
                if(a.projectile && a.attack && !a.attack.frames.Any(f=>f!=null && (f.events.Contains("ThrowProjectile") || f.events.Contains("SpawnScreamWave")))) yield return a.id+": projectile attack has no ThrowProjectile / SpawnScreamWave release event.";
            }
        }
    }
}
