using System.Linq;
using UnityEngine;
namespace BeatEmUp
{
    // Trait / frame-event adapter for EnemyCombat's existing editable AI, not a second brain.
    [DisallowMultipleComponent,RequireComponent(typeof(EnemyCombat))]
    public sealed class PrefectSupport : MonoBehaviour
    {
        public GameObject rusherPrefab;
        public AttackData callBackup,pushAttack;
        public StageFlowController flow;
        [Min(1)] public int rushersPerCall=2;
        [Min(0)] public float retreatDistance=3, emergencyPushDistance=1.05f,minimumCallDistance=3.2f;
        [Min(0)] public float spawnPlayerClearance=1.25f,spawnEnemyClearance=1.25f;
        public bool debugDraw=true;
        EnemyCombat brain;
        public Vector2 SelectedRetreatDirection { get; private set; }
        public int LastCallSpawned { get; private set; }
        public int CallsActivated { get; private set; }
        public float PreferredDistance=>brain.aiProfile ? brain.aiProfile.preferredDistance : 3.5f;
        public float ThreatDistance=>brain.target ? Vector2.Distance(transform.position,brain.target.position) : float.PositiveInfinity;
        public StageFlowController Flow=>flow ? flow : flow=FindFirstObjectByType<StageFlowController>();
        public int EnemySlots=>Flow ? Flow.ReinforcementSlots(brain) : 0;
        public int ActiveEnemyCount=>Flow ? Flow.EncounterEnemyCount(brain) : 0;
        public int ActiveRusherCount=>Flow ? Flow.EncounterRusherCount(brain) : 0;
        public float CallCooldown=>brain.AI?.Cooldown("CallBackup") ?? 0;
        public float PushCooldown=>brain.AI?.Cooldown("Push") ?? 0;
        public bool CallReady=>brain.target && !brain.attackPlayer.CurrentAttack && brain.reaction.CanAct && rusherPrefab && rusherPrefab.GetComponent<EnemyCombat>()?.role==EnemyRole.Rusher && InChoiceRange(callBackup) && ThreatDistance>=minimumCallDistance && EnemySlots>0 && CallCooldown<=0 && (brain.AI?.RecoverySeconds ?? 0)<=0;
        public bool PushReady=>brain.target && !brain.attackPlayer.CurrentAttack && brain.reaction.CanAct && InChoiceRange(pushAttack) && ThreatDistance<=emergencyPushDistance && PushCooldown<=0 && (brain.AI?.RecoverySeconds ?? 0)<=0;
        bool InChoiceRange(AttackData attack){var choice=brain.aiProfile ? brain.aiProfile.attacks.FirstOrDefault(c=>c.attack==attack) : null;return choice!=null && ThreatDistance>=choice.minimumRange && ThreatDistance<=choice.maximumRange && Mathf.Abs(brain.target.position.y-transform.position.y)<choice.laneTolerance;}
        void Awake(){brain=GetComponent<EnemyCombat>();}
        void OnEnable(){if(!brain)brain=GetComponent<EnemyCombat>();brain.attackPlayer.FrameEvent+=FrameEvent;brain.attackPlayer.Started+=Started;}
        void OnDisable(){brain.attackPlayer.FrameEvent-=FrameEvent;brain.attackPlayer.Started-=Started;}
        void Started(AttackData attack){if(attack==callBackup)LastCallSpawned=0;}
        void FrameEvent(string signal)
        {
            if(signal!="CallBackup" || brain.attackPlayer.CurrentAttack!=callBackup || !Flow)return;
            CallsActivated++;LastCallSpawned=Flow.RequestRusherBackup(brain,rusherPrefab,rushersPerCall,spawnPlayerClearance,spawnEnemyClearance);
        }
        public bool AllowsChoice(EnemyAIAttackChoice choice)=>choice.attack==callBackup ? CallReady : choice.attack==pushAttack ? PushReady : true;
        public Vector2 RetreatDirection()
        {
            if(!brain.target)return SelectedRetreatDirection=Vector2.zero;
            Vector2 origin=transform.position,target=brain.target.position,away=(origin-target).normalized;
            float best=ThreatDistance;Vector2 selected=Vector2.zero;
            foreach(float degrees in new[]{0f,45,-45,90,-90,135,-135,180}){
                float rad=degrees*Mathf.Deg2Rad;var direction=new Vector2(away.x*Mathf.Cos(rad)-away.y*Mathf.Sin(rad),away.x*Mathf.Sin(rad)+away.y*Mathf.Cos(rad));
                var step=brain.motor.ProbeGroundMove(direction*.5f);
                if(step.magnitude<.15f)continue;
                float distance=Vector2.Distance(origin+step,target);
                if(distance>best+.001f){best=distance;selected=step.normalized;}
            }return SelectedRetreatDirection=selected;
        }
        void OnDrawGizmos()
        {
            if(!debugDraw)return;var b=brain ? brain : GetComponent<EnemyCombat>();
            Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,b&&b.aiProfile ? b.aiProfile.preferredDistance : 3.5f);
            Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(transform.position,retreatDistance);
            Gizmos.color=Color.red;Gizmos.DrawWireSphere(transform.position,emergencyPushDistance);
            Gizmos.DrawLine(transform.position,transform.position+(Vector3)SelectedRetreatDirection);
            if(Application.isPlaying&&Flow&&b){Gizmos.color=Color.green;foreach(var point in Flow.BackupSpawnCandidates(b))Gizmos.DrawWireSphere(point,.15f);}
        }
    }
}
