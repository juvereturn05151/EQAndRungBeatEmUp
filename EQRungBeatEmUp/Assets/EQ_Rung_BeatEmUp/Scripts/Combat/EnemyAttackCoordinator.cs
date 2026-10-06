using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BeatEmUp
{
    // A request remains a candidate, never a turn in a queue. AI revalidates before consuming a grant.
    public sealed class EnemyAttackCoordinator : MonoBehaviour, ICombatFrameListener
    {
        public AttackCoordinationSettings settings=new AttackCoordinationSettings();
        public string encounterId;
        public int FrameOrder=>4;
        public EnemyCombat LastAttacker { get; private set; }
        public sealed class Request
        {
            public EnemyCombat owner; public Transform target; public AttackData attack; public AttackCoordinationData data;
            public long waitingSince, refreshedAt, grantedAt; public float positionScore, variation;
            public AttackTokenCategory categories; public bool granted, committed; public string denial;
        }
        sealed class TargetState { public bool disabled; public long graceUntil; }
        readonly Dictionary<EnemyCombat,Request> requests=new Dictionary<EnemyCombat,Request>();
        readonly Dictionary<Transform,TargetState> targets=new Dictionary<Transform,TargetState>();
        readonly Dictionary<EnemyCombat,long> recent=new Dictionary<EnemyCombat,long>();
        readonly Dictionary<EnemyCombat,long> waitingHistory=new Dictionary<EnemyCombat,long>();
        long nextCommit;
        public IEnumerable<Request> Requests=>requests.Values;
        public int Occupied(AttackTokenCategory category)=>requests.Values.Count(r=>r.granted && (r.categories & category)!=0);
        public bool Owns(EnemyCombat owner)=>owner && requests.TryGetValue(owner,out var r) && r.granted;
        public bool Committed(EnemyCombat owner)=>owner && requests.TryGetValue(owner,out var r) && r.committed;
        public string Describe(EnemyCombat owner)=>owner && requests.TryGetValue(owner,out var r) ? r.granted ? "["+r.categories+"]" : "[WAITING "+r.categories+"] "+Score(r).ToString("F1")+" · "+r.denial : "No reservation";
        bool Authority=>!MultiplayerSession.Active || MultiplayerSession.Active.IsAuthority;
        void OnEnable()=>CombatClock.Register(this);
        void OnDisable() { CombatClock.Unregister(this); Clear(); }
        public static bool ValidTarget(Transform target)=>target && target.gameObject.activeInHierarchy && target.GetComponent<CharacterHealth>() && !target.GetComponent<CharacterHealth>().IsDead;
        public static bool DisabledTarget(Transform target)
        { var player=target ? target.GetComponent<ComboController>() : null; return player && (player.IsStunned || player.IsKnockdownState || player.State==CombatState.Grabbed); }
        bool Bypass(EnemyCombat owner,AttackCoordinationData data)=>!settings.useAttackCoordination || data==null ||
            (data.ignoreCoordinator && (!owner.GetComponent<TotemBossController>() || settings.allowBossToIgnoreCoordinator)) || data.Categories==AttackTokenCategory.None;
        public bool RequestAttack(EnemyCombat owner,Transform target,AttackData attack,AttackCoordinationData data,float positionScore)
        {
            if(!Authority || !owner || !owner.isActiveAndEnabled || !owner.reaction || owner.reaction.State!=EnemyReaction.Normal || owner.reaction.health.IsDead || !ValidTarget(target) || !attack) { Release(owner,"Invalid request"); return false; }
            if(Bypass(owner,data)) { Release(owner,"Bypass"); return true; }
            long now=CombatClock.CurrentTick;
            if(requests.TryGetValue(owner,out var r) && (r.target!=target || r.attack!=attack || r.categories!=data.Categories)) { Release(owner,"Choice / target changed"); r=null; }
            if(r==null)
            {
                if(!waitingHistory.TryGetValue(owner,out long since)) waitingHistory[owner]=since=now;
                r=new Request{owner=owner,target=target,attack=attack,data=data,categories=data.Categories,waitingSince=since,variation=Random.Range(0,settings.randomPriorityVariation)};
                requests.Add(owner,r); Log(owner.name+" requested "+data.Categories);
                if(!targets.ContainsKey(target)) targets.Add(target,new TargetState());
            }
            r.refreshedAt=now; r.positionScore=positionScore;
            return r.granted;
        }
        public void Confirm(EnemyCombat owner)
        {
            if(!requests.TryGetValue(owner,out var r) || !r.granted) return;
            r.committed=true; LastAttacker=owner; recent[owner]=CombatClock.CurrentTick;
            waitingHistory.Remove(owner);
        }
        public void Release(EnemyCombat owner,string reason="Finished / cancelled")
        {
            if(ReferenceEquals(owner,null) || !requests.TryGetValue(owner,out var r)) return;
            requests.Remove(owner); if(r.granted) Log((owner ? owner.name : "Despawned enemy")+" released "+r.categories+" · "+reason);
        }
        public void Clear()
        {
            foreach(var request in requests.Values.Where(r=>r.granted).ToArray()) if(request.owner) request.owner.CancelCoordinationAttack();
            requests.Clear(); targets.Clear(); recent.Clear(); waitingHistory.Clear(); LastAttacker=null; nextCommit=0;
        }
        public float Score(Request r)
        {
            float score=(CombatClock.CurrentTick-r.waitingSince)*settings.waitingPriorityBonus+r.positionScore+r.data.priorityModifier+r.variation+10;
            if(recent.TryGetValue(r.owner,out long frame)) score-=settings.recentAttackPenalty*Mathf.Clamp01(1-(CombatClock.CurrentTick-frame)/(float)Mathf.Max(1,settings.recentAttackPenaltyFrames));
            if(r.owner==LastAttacker && requests.Values.Any(other=>other.owner!=r.owner && !other.granted)) score-=settings.recentAttackPenalty;
            return score;
        }
        string Blocked(Request r)
        {
            bool disabled=DisabledTarget(r.target);
            if(disabled && settings.pauseAllAttacksWhilePlayerDisabled) return "Target incapacitated";
            if((r.categories & AttackTokenCategory.CrowdControl)!=0)
            {
                if(disabled && settings.pauseNewCrowdControlWhilePlayerDisabled) return "Target incapacitated";
                if(targets.TryGetValue(r.target,out var state) && CombatClock.CurrentTick<state.graceUntil) return "CC recovery grace";
            }
            foreach(var category in Categories)
                if((r.categories & category)!=0 && Occupied(category)>=settings.Capacity(category))
                    return category+" occupied: "+string.Join(", ",requests.Values.Where(x=>x.granted && (x.categories & category)!=0).Select(x=>x.owner.name));
            return null;
        }
        public static readonly AttackTokenCategory[] Categories={AttackTokenCategory.Melee,AttackTokenCategory.Ranged,AttackTokenCategory.CrowdControl,AttackTokenCategory.Support};
        public void CombatFrame()
        {
            if(!Authority) { Clear(); return; }
            if(targets.Count>0 && !targets.Keys.Any(ValidTarget)) { Clear(); return; }
            foreach(var target in targets.Keys.ToArray())
            {
                if(!ValidTarget(target)) { targets.Remove(target); continue; }
                var state=targets[target]; bool disabled=DisabledTarget(target);
                if(state.disabled && !disabled) state.graceUntil=CombatClock.CurrentTick+Mathf.Max(0,settings.crowdControlRecoveryGraceFrames);
                state.disabled=disabled;
            }
            foreach(var r in requests.Values.ToArray())
            {
                if(!r.owner || !r.owner.isActiveAndEnabled || !ValidTarget(r.target) || r.owner.reaction.health.IsDead || r.owner.reaction.State!=EnemyReaction.Normal)
                { if(r.owner && r.granted) r.owner.CancelCoordinationAttack(); Release(r.owner,"Owner / target interrupted"); continue; }
                long age=CombatClock.CurrentTick-r.grantedAt;
                if(r.granted && age>=Mathf.Max(1,settings.maximumTokenHoldFrames))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("Attack token watchdog released "+r.owner.name+" in "+encounterId,r.owner);
#endif
                    r.owner.CancelCoordinationAttack(); Release(r.owner,"Watchdog"); continue;
                }
                if(r.granted && (!r.committed && age>2 || r.committed && !r.owner.CoordinationAttackActive)) Release(r.owner,"Attack no longer active");
                else if(!r.granted && CombatClock.CurrentTick-r.refreshedAt>2) Release(r.owner,"Candidate no longer eligible");
            }
            foreach(var r in requests.Values.Where(r=>!r.granted).OrderByDescending(Score).ThenBy(r=>r.owner.GetInstanceID()).ToArray())
            {
                var denial=Blocked(r);
                if(denial==null && CombatClock.CurrentTick<nextCommit) denial="Global commitment gap";
                if(denial!=null) { if(r.denial!=denial) Log(r.owner.name+" denied: "+denial); r.denial=denial; continue; }
                r.granted=true; r.grantedAt=CombatClock.CurrentTick; r.denial=null;
                nextCommit=CombatClock.CurrentTick+Mathf.Max(0,settings.minimumGlobalAttackGapFrames);
                Log(r.owner.name+" approved "+r.categories);
            }
        }
        void Log(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(settings.verboseDebug) Debug.Log("Attack coordinator "+encounterId+": "+message,this);
#endif
        }
        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!settings.showDebug) return;
            string text="ATTACK COORDINATOR · "+encounterId;
            foreach(var category in Categories) text+="\n"+category+": "+Occupied(category)+" / "+settings.Capacity(category)+"  "+string.Join(", ",requests.Values.Where(r=>r.granted && (r.categories & category)!=0).Select(r=>r.owner.name));
            foreach(var r in requests.Values.Where(r=>!r.granted).OrderByDescending(Score)) text+="\nWaiting: "+r.owner.name+" · "+Score(r).ToString("F1")+" · "+r.denial;
            GUI.Box(new Rect(Screen.width-440,105,430,130+requests.Values.Count(r=>!r.granted)*22),text);
#endif
        }
    }
}
