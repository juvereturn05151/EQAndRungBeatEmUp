using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeatEmUp
{
    // Plain runtime state, owned by EnemyCombat: one authoritative clock listener, no parallel brain.
    public sealed class EnemyAIController
    {
        readonly EnemyCombat brain;
        EnemyAIProfile profile;
        EnemyAIState state;
        readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
        float previousHp, elapsed, recovery;
        bool interrupted, finished, damaged, attempted;
        Vector2 strafe;
        public string StateName { get; private set; }="Uninitialized";
        public string LastTransition { get; private set; }="None";
        public EnemyAIAttackChoice Selected { get; private set; }
        public float StateSeconds => elapsed;
        public float RecoverySeconds => recovery;
        public float Cooldown(string id) => cooldowns.TryGetValue(id,out var value) ? value : 0;
        public void CancelPendingAction() { brain.ReleaseCoordination("AI cancelled"); Selected=null; attempted=finished=damaged=false; interrupted=true; }
        public EnemyAIController(EnemyCombat owner) { brain=owner; Refresh(); }
        public void Refresh()
        {
            brain.ReleaseCoordination("Profile refresh");
            if(brain.attackPlayer.CurrentAttack) brain.attackPlayer.Stop();
            profile=brain.aiProfile; cooldowns.Clear(); Selected=null; finished=attempted=interrupted=false; recovery=0;
            previousHp=brain.reaction && brain.reaction.health ? brain.reaction.health.Current : 0;
            Enter(profile ? profile.Find(profile.defaultState) : null,"Refresh");
        }
        void Enter(EnemyAIState next,string reason)
        {
            state=next; StateName=next!=null ? next.id : "Invalid profile / idle";
            elapsed=0; attempted=false; finished=false; strafe=Random.insideUnitCircle.normalized;
            LastTransition=reason+" → "+StateName;
        }
        public bool ForceState(string id)
        {
            if(!profile || !brain.reaction.CanAct || brain.attackPlayer.CurrentAttack) return false;
            var next=profile.Find(id); if(next==null || next.role!=EnemyAIStateRole.Normal) return false;
            Enter(next,"Inspector force"); return true;
        }
        public void AttackFinished(AttackData attack)
        {
            if(Selected==null || Selected.attack!=attack) return;
            cooldowns[Selected.id]=Mathf.Max(0,Selected.cooldown)+Mathf.Max(0,attack.cooldownFrames)*CombatClock.FrameSeconds;
            recovery=Mathf.Max(0,profile.recoveryTime); finished=true;
        }
        bool ValidTarget(Transform target) => target && target.gameObject.activeInHierarchy && (!target.GetComponent<CharacterHealth>() || !target.GetComponent<CharacterHealth>().IsDead);
        void Target()
        {
            if(!ValidTarget(brain.target) || Vector2.Distance(brain.target.position,brain.transform.position)>profile.loseTargetRange) brain.target=null;
            if(brain.target && profile.targetRule==EnemyAITargetRule.KeepUntilLost) return;
            var candidates=PlayerRoster.Players.Any() ? PlayerRoster.Living.Select(p=>p.Motor.transform) : Object.FindObjectsByType<ComboController>(FindObjectsSortMode.None).Where(p=>p.isActiveAndEnabled).Select(p=>p.transform);
            var closest=candidates.Where(ValidTarget).Where(t=>Vector2.Distance(t.position,brain.transform.position)<=profile.aggroRange).OrderBy(t=>(t.position-brain.transform.position).sqrMagnitude).FirstOrDefault();
            if(closest) brain.target=closest;
        }
        Vector2 Delta => brain.target ? (Vector2)(brain.target.position-brain.transform.position) : Vector2.zero;
        public bool Condition(EnemyAIRequirement requirement)
        {
            if(requirement==null) return false;
            bool target=ValidTarget(brain.target); var delta=Delta; float range=profile.useGroundPlaneRange ? delta.magnitude : Mathf.Abs(delta.x); bool result=false;
            switch(requirement.condition)
            {
                case EnemyAICondition.CallBackupReady: result=brain.GetComponent<PrefectSupport>()?.CallReady==true; break;
                case EnemyAICondition.EmergencyPushReady: result=brain.GetComponent<PrefectSupport>()?.PushReady==true; break;
                case EnemyAICondition.SupportRetreatNeeded: {var support=brain.GetComponent<PrefectSupport>();result=support && target && support.ThreatDistance<support.retreatDistance;break;}
                case EnemyAICondition.SupportHasSpace: {var support=brain.GetComponent<PrefectSupport>();result=support && target && support.ThreatDistance>=support.PreferredDistance;break;}
                case EnemyAICondition.Always: result=true; break;
                case EnemyAICondition.HasTarget: result=target; break;
                case EnemyAICondition.NoTarget: result=!target; break;
                case EnemyAICondition.InAggroRange: result=target && delta.magnitude<=profile.aggroRange; break;
                case EnemyAICondition.OutOfAggroRange: result=!target || delta.magnitude>profile.aggroRange; break;
                case EnemyAICondition.LaneAligned: result=target && Mathf.Abs(delta.y)<profile.laneTolerance; break;
                case EnemyAICondition.NotLaneAligned: result=target && Mathf.Abs(delta.y)>=profile.laneTolerance; break;
                case EnemyAICondition.InMeleeRange: result=target && range<=profile.meleeRange; break;
                case EnemyAICondition.InProjectileRange: result=target && range<=profile.projectileRange; break;
                case EnemyAICondition.TooClose: result=target && range<profile.preferredDistance; break;
                case EnemyAICondition.TooFar: result=target && range>profile.preferredDistance; break;
                case EnemyAICondition.CooldownReady: result=recovery<=0 && profile.attacks.Any(a=>a!=null && Eligible(a,false)); break;
                case EnemyAICondition.TookDamage: result=damaged; break;
                case EnemyAICondition.HpBelow: result=brain.reaction.health.Current/brain.reaction.health.EffectiveMaximum<=requirement.value; break;
                case EnemyAICondition.AttackFinished: result=finished; break;
                case EnemyAICondition.RecoveryFinished: result=recovery<=0; break;
                case EnemyAICondition.KnockedDown: result=brain.reaction.IsRecovering; break;
                case EnemyAICondition.Dead: result=brain.reaction.health.IsDead; break;
                case EnemyAICondition.RandomChance: result=Random.value<requirement.value; break;
                case EnemyAICondition.TimerFinished: result=elapsed>=Mathf.Max(0,state?.duration ?? 0); break;
            }
            return requirement.invert ? !result : result;
        }
        bool Requirements(List<EnemyAIRequirement> conditions) => conditions==null || conditions.All(Condition);
        bool Eligible(EnemyAIAttackChoice choice,bool prerequisites=true)
        {
            if(choice==null || !choice.attack || choice.attack.TotalFrames==0 || choice.weight<=0 || !ValidTarget(brain.target) || recovery>0 || Cooldown(choice.id)>0) return false;
            var support=brain.GetComponent<PrefectSupport>();if(support && !support.AllowsChoice(choice))return false;
            if(choice.attack.requiresAirborne && brain.motor.IsGrounded) return false;
            var delta=Delta;
            var first=choice.attack.FirstActiveFrame;
            if(first>=0 && choice.attack.frames[first].hitboxes.Exists(h=>h!=null && h.groundArea) &&
                !choice.attack.frames[first].hitboxes.Exists(h=>h!=null && h.groundArea && AttackHitbox.InGroundArea(h,(Vector2)brain.transform.position+new Vector2(h.offset.x*brain.motor.Facing,h.offset.y),brain.target.position))) return false;
            float distance=profile.useGroundPlaneRange ? delta.magnitude : Mathf.Abs(delta.x);
            return distance>=choice.minimumRange && distance<=choice.maximumRange && Mathf.Abs(delta.y)<choice.laneTolerance &&
                (!choice.projectile || brain.GetComponent<EnemyProjectileAttack>() && brain.GetComponent<EnemyProjectileAttack>().projectilePrefab) && (!prerequisites || Requirements(choice.prerequisites));
        }
        public IEnumerable<EnemyAIAttackChoice> AvailableAttacks() => profile ? profile.attacks.Where(a=>Eligible(a)) : Enumerable.Empty<EnemyAIAttackChoice>();
        void Attack()
        {
            if(attempted) return;
            var options=AvailableAttacks().Where(a=>state.action==EnemyAIAction.WeightedAttack || a.id==state.attackChoice && (state.action!=EnemyAIAction.ProjectileAttack || a.projectile)).ToArray();
            float total=options.Sum(a=>a.weight); if(total<=0) { brain.ReleaseCoordination("No eligible attack"); Selected=null; return; }
            float roll=Random.value*total; var selected=options.Last();
            foreach(var option in options) { roll-=option.weight; if(roll<=0) { selected=option; break; } }
            if(Selected!=null && options.Contains(Selected)) selected=Selected;
            Selected=selected;
            // Revalidate every frame, including the frame following arbitration, before telegraph starts.
            if(!Eligible(selected) || !ClearAttackPath()) { brain.ReleaseCoordination("Range / path invalid"); Selected=null; return; }
            float range=Delta.magnitude; float score=20/(1+range)+10*(1-Mathf.Clamp01(Mathf.Abs(Delta.y)/selected.laneTolerance));
            if(!brain.RequestCoordination(selected.attack,selected.coordination,score))
            { brain.motor.MoveInput=WaitingMovement(); return; }
            if(brain.attackPlayer.Play(selected.attack)) { brain.motor.MovementLocked=true; attempted=true; finished=false; brain.ConfirmCoordination(); }
            else brain.ReleaseCoordination("Play failed");
        }
        bool ClearAttackPath()=>!Physics2D.LinecastAll(brain.transform.position,brain.target.position,brain.motor.wallCollisionMask)
            .Any(h=>h.collider && !h.collider.isTrigger && h.collider.GetComponentInParent<CombatWall>()?.isActiveAndEnabled==true);
        Vector2 WaitingMovement()
        {
            float side=brain.GetInstanceID()%2==0 ? 1 : -1;
            // Keep ranged spacing; melee applicants slide to another lane rather than freezing.
            float horizontal=Selected.projectile ? Mathf.Abs(Delta.x)<profile.preferredDistance ? -Mathf.Sign(Delta.x)*.35f : Mathf.Abs(Delta.x)>Selected.maximumRange*.85f ? Mathf.Sign(Delta.x)*.25f : 0 : 0;
            float vertical=Delta.y>.15f ? .3f : Delta.y<-.15f ? -.3f : side*.3f;
            if(brain.transform.position.y>=brain.motor.arenaMax.y-.08f) vertical=-.3f;
            if(brain.transform.position.y<=brain.motor.arenaMin.y+.08f) vertical=.3f;
            return new Vector2(horizontal,vertical);
        }
        public void Tick()
        {
            if(profile!=brain.aiProfile) Refresh(); if(!profile) return;
            float dt=CombatClock.FrameSeconds;
            foreach(var key in cooldowns.Keys.ToArray()) cooldowns[key]=Mathf.Max(0,cooldowns[key]-dt);
            recovery=Mathf.Max(0,recovery-dt);
            if(recovery<=0 && !brain.attackPlayer.CurrentAttack && brain.coordinator?.Committed(brain)==true) brain.ReleaseCoordination("Recovery finished");
            damaged=brain.reaction.health.Current<previousHp; previousHp=brain.reaction.health.Current;
            if (brain.reaction.IsStunState)
            { CancelPendingAction(); StateName=brain.reaction.State.ToString(); brain.motor.MoveInput=Vector2.zero; return; }
            var grab=brain.GetComponent<CombatGrabController>();
            bool authoredLeap=grab && grab.LeapActive && brain.reaction.State==EnemyReaction.Normal && !brain.reaction.health.IsDead;
            if(!brain.reaction.CanAct && !authoredLeap)
            {
                brain.ReleaseCoordination("Combat interrupt"); Selected=null;
                var role=brain.reaction.health.IsDead ? EnemyAIStateRole.Dead : brain.reaction.State==EnemyReaction.GetUp ? EnemyAIStateRole.GetUp : brain.reaction.IsRecovering ? EnemyAIStateRole.Knockdown : EnemyAIStateRole.Hurt;
                var forced=profile.states.FirstOrDefault(s=>s!=null && s.role==role);
                if(!interrupted || state!=forced) Enter(forced,"Combat interrupt: "+role);
                StateName=forced!=null ? forced.id : role.ToString(); interrupted=true;
                brain.motor.MoveInput=Vector2.zero; if(brain.attackPlayer.CurrentAttack) brain.attackPlayer.Stop(); return;
            }
            if(interrupted) { interrupted=false; recovery=Mathf.Max(recovery,profile.recoveryTime); Enter(profile.Find(profile.defaultState),"Combat recovered"); }
            Target(); elapsed+=dt; brain.motor.MoveInput=Vector2.zero;
            if(!ValidTarget(brain.target)) { brain.ReleaseCoordination("Target invalid"); if(brain.attackPlayer.CurrentAttack) brain.attackPlayer.Stop(); }
            if(brain.passiveTrainingDummy || state==null) { brain.animationDriver.Play("Idle"); return; }
            if(state.role!=EnemyAIStateRole.Normal) { brain.animationDriver.Play("Idle"); return; }
            if(brain.attackPlayer.CurrentAttack && profile.commitToAttack) return;
            if(elapsed>=profile.reactionDelay)
            {
                foreach(var transition in state.transitions)
                {
                    var destination=transition!=null ? profile.Find(transition.targetState) : null;
                    if(destination==null || destination.role!=EnemyAIStateRole.Normal || !Requirements(transition.conditions)) continue;
                    if(brain.attackPlayer.CurrentAttack) brain.attackPlayer.Stop();
                    Enter(destination,"From "+state.id); break;
                }
            }
            if(elapsed<profile.reactionDelay) return;
            if(brain.target && (state.faceTarget || state.action==EnemyAIAction.FaceTarget)) brain.motor.Face(Delta.x);
            Vector2 movement=Vector2.zero;
            switch(state.action)
            {
                case EnemyAIAction.Approach: if(brain.target) movement=Delta.normalized; break;
                case EnemyAIAction.Retreat: if(brain.target) movement=brain.GetComponent<PrefectSupport>() ? brain.GetComponent<PrefectSupport>().RetreatDirection() : profile.useGroundPlaneRange ? -Delta.normalized : new Vector2(-Mathf.Sign(Delta.x),0); break;
                case EnemyAIAction.LaneAlign: if(brain.target) movement=new Vector2(0,Mathf.Sign(Delta.y)); break;
                case EnemyAIAction.Reposition: movement=strafe; break;
                case EnemyAIAction.UseAttack: case EnemyAIAction.WeightedAttack: case EnemyAIAction.ProjectileAttack: Attack(); movement=brain.motor.MoveInput; break;
            }
            if(!brain.attackPlayer.CurrentAttack && recovery<=0 && state.action!=EnemyAIAction.UseAttack && state.action!=EnemyAIAction.WeightedAttack && state.action!=EnemyAIAction.ProjectileAttack)
            { brain.ReleaseCoordination("Returned to movement state"); Selected=null; }
            brain.motor.MoveInput=Vector2.ClampMagnitude(movement*state.movementScale,1);
            if(!brain.attackPlayer.CurrentAttack) brain.animationDriver.Play(movement.sqrMagnitude>.01f ? "Walk" : "Idle");
        }
    }
}
