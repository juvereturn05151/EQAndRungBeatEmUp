using System.Linq;
using UnityEngine;
namespace BeatEmUp
{
    [DisallowMultipleComponent, RequireComponent(typeof(AttackPlayer))]
    public sealed partial class CombatGrabController : MonoBehaviour, ICombatFrameListener
    {
        public Transform grabAnchor;
        public AttackData grabAttack, successfulGrab;
        [Min(0)] public float grabDamage;
        public AttackHitboxData slamHit=new AttackHitboxData { damage=24,hitType=HitType.KnockDown,knockdownDurationFrames=45,hitstopFrames=4,knockback=0,canHitAirborne=true,unblockable=true };
        [Min(1)] public int maximumGrabFrames=360;
        public bool debugDraw;
        [Header("Committed lunge: combat frame movement multiplier lives on the attack")]
        [Min(0)] public float grabLungeSpeed=8, grabLungeDistance=2.5f, grabAcceleration;
        public Vector2 TargetPositionAtCommit { get; private set; }
        public Vector2 LockedGrabDirection { get; private set; }=Vector2.right;
        public bool DirectionLocked { get; private set; }
        public bool LungeActive { get; private set; }
        public float LungeDistanceTraveled { get; private set; }
        public float LungeDistanceRemaining => Mathf.Max(0,grabLungeDistance-LungeDistanceTraveled);
        public int GrabLungeFrames => grabAttack ? grabAttack.frames.Count(f=>f.grabLungeMovementScale>0) : 0;
        public bool StoppedByWall { get; private set; }
        public ComboController CurrentGrabbedTarget { get; private set; }
        public bool GrabActive { get; private set; }
        public bool SwitchingBranch { get; private set; }
        public int Captures { get; private set; }
        public bool OwnerAlive => health && !health.IsDead;
        public int FrameOrder => 60;
        public int HoldFrames => successfulGrab ? successfulGrab.frames.FindIndex(f=>f.events.Contains("ThrowTarget")) : 0;
        public int GrabMissRecoveryFrames { get { if(!grabAttack)return 0;int start=grabAttack.frames.FindIndex(f=>f.events.Contains("StopGrabLunge"));return grabAttack.TotalFrames-(start>=0 ? start : grabAttack.LastActiveFrame+1); } }
        public Vector2 AnchorPosition => (Vector2)motor.transform.position+new Vector2((grabAnchor ? grabAnchor.localPosition.x : .7f)*player.Facing,grabAnchor ? grabAnchor.localPosition.y : 0);
        AttackPlayer player; CharacterMotor motor; CharacterHealth health;
        int movedFrame=-1; float currentLungeSpeed;
        void Awake() { player=GetComponent<AttackPlayer>(); motor=GetComponent<CharacterMotor>(); health=GetComponent<CharacterHealth>(); }
        void OnEnable() { player.FrameEvent+=Signal; player.Interrupted+=Interrupted; player.Finished+=Finished; health.Died+=Abort; health.Restored+=Abort; CombatClock.Register(this); }
        void OnDisable() { CombatClock.Unregister(this); if(player) {player.FrameEvent-=Signal;player.Interrupted-=Interrupted;player.Finished-=Finished;} if(health){health.Died-=Abort;health.Restored-=Abort;} Abort(); }
        void Interrupted(AttackData attack) { if(!SwitchingBranch) Abort(); }
        void Finished(AttackData attack) { Abort(); }
        public void ForgetTarget(ComboController target) { if(CurrentGrabbedTarget==target) {target.health.Died-=VictimDied;CurrentGrabbedTarget=null;} }
        void VictimDied() { Abort();if(player)player.Stop(); }
        public void Abort() { GrabActive=LungeActive=LeapActive=false; if(motor) {motor.FrameGravityScale=1;motor.StopGroundedMotion();} var target=CurrentGrabbedTarget; CurrentGrabbedTarget=null; if(target) {target.health.Died-=VictimDied;target.ReleaseGrab(this);} }
        void Signal(string signal)
        {
            if(player.CurrentAttack!=grabAttack && player.CurrentAttack!=successfulGrab) return;
            LeapSignal(signal);
            if(signal=="Telegraph") {DirectionLocked=false;LungeActive=false;LeapActive=false;FaceStrikesApplied=0;LungeDistanceTraveled=0;movedFrame=-1;StoppedByWall=false;LockedGrabDirection=new Vector2(player.Facing,0);}
            if(signal=="LockGrabDirection") LockDirection();
            if(signal=="StartGrabLunge") {if(!DirectionLocked)LockDirection();LungeActive=true;currentLungeSpeed=grabAcceleration>0 ? 0 : grabLungeSpeed;}
            if(signal=="StopGrabLunge") {LungeActive=false;GrabActive=false;motor.StopGroundedMotion();}
            if(signal=="EnableGrabHitbox") GrabActive=true;
            if(signal=="DisableGrabHitbox") GrabActive=false;
            if(signal=="AttachGrabbedTarget" && CurrentGrabbedTarget) CurrentGrabbedTarget.motor.transform.position=AnchorPosition;
            if(signal=="ApplyGrabDamage" && CurrentGrabbedTarget) CurrentGrabbedTarget.health.Damage(grabDamage);
            if(signal=="ThrowTarget" && CurrentGrabbedTarget)
            {
                var target=CurrentGrabbedTarget; target.ReleaseGrab(this);
                var hurtbox=target.GetComponentInChildren<CombatHurtbox>();
                if(hurtbox && hurtbox.Receive(slamHit.RuntimeCopy(),player.Facing,motor)) { player.Freeze(slamHit.hitstopFrames); target.attackPlayer.Freeze(slamHit.hitstopFrames); }
            }
            if(signal=="ReleaseTarget") Abort();
        }
        Transform Target()
        {
            var brain=GetComponent<EnemyCombat>();if(brain && brain.target && brain.target.gameObject.activeInHierarchy)return brain.target;
            return FindObjectsByType<ComboController>(FindObjectsSortMode.None).Where(p=>p.health && !p.health.IsDead).OrderBy(p=>(p.transform.position-transform.position).sqrMagnitude).FirstOrDefault()?.transform;
        }
        void LockDirection()
        {
            var target=Target(); TargetPositionAtCommit=target ? (Vector2)target.position : (Vector2)transform.position+new Vector2(player.Facing,0);
            LockedTarget=target;
            var delta=TargetPositionAtCommit-(Vector2)transform.position;
            LockedGrabDirection=delta.sqrMagnitude>.0001f ? delta.normalized : new Vector2(player.Facing,0);
            if(Mathf.Abs(LockedGrabDirection.x)>.01f)player.CommitFacing(LockedGrabDirection.x<0 ? -1 : 1);
            DirectionLocked=true;
            if(leapEnabled)PrepareLeapPath();
        }
        void MissRecovery()
        {
            LungeActive=GrabActive=false;motor.StopGroundedMotion();
            int recovery=grabAttack.frames.FindIndex(f=>f.events.Contains("StopGrabLunge"));
            if(recovery>=0)player.SeekFrame(recovery,true);else player.Stop();
        }
        static bool SweptBox(Vector2 start,Vector2 end,Vector2 point,float width,float depth)
        {
            var delta=end-start;float near=0,far=1;
            for(int axis=0;axis<2;axis++) {
                float half=Mathf.Max(.005f,(axis==0 ? width : depth)*.5f),origin=start[axis]-point[axis],step=delta[axis];
                if(Mathf.Abs(step)<.00001f){if(Mathf.Abs(origin)>half)return false;continue;}
                float a=(-half-origin)/step,b=(half-origin)/step;if(a>b){float swap=a;a=b;b=swap;}
                near=Mathf.Max(near,a);far=Mathf.Min(far,b);if(near>far)return false;
            }return true;
        }
        public void CombatFrame()
        {
            if(!OwnerAlive || !player.CurrentAttack) { Abort(); return; }
            if(CurrentGrabbedTarget) { CurrentGrabbedTarget.motor.transform.position=AnchorPosition; return; }
            if(player.IsFrozen || player.CurrentAttack!=grabAttack) return;
            if(!DirectionLocked && player.CurrentFrame<grabAttack.FirstActiveFrame){var target=Target();if(target && Mathf.Abs(target.position.x-transform.position.x)>.01f)player.CommitFacing(target.position.x<transform.position.x ? -1 : 1);}
            Vector2 previous=transform.position;
            if(LeapActive && !MoveLeap())return;
            if(LungeActive && movedFrame!=player.CurrentFrame){
                movedFrame=player.CurrentFrame;currentLungeSpeed=grabAcceleration>0 ? Mathf.MoveTowards(currentLungeSpeed,grabLungeSpeed,grabAcceleration*CombatClock.FrameSeconds) : grabLungeSpeed;
                float length=Mathf.Min(LungeDistanceRemaining,currentLungeSpeed*player.Frame.grabLungeMovementScale*CombatClock.FrameSeconds);
                if(length>0){motor.MoveAttack(LockedGrabDirection*length,1);float moved=Vector2.Distance(previous,transform.position);LungeDistanceTraveled+=moved;
                    if(moved<length-.0001f){StoppedByWall=true;MissRecovery();return;}}
            }
            if(!GrabActive){if(LungeActive && LungeDistanceRemaining<=.0001f)MissRecovery();return;}
            foreach(var box in player.Frame.grabHitboxes)
            foreach(var target in FindObjectsByType<ComboController>(FindObjectsSortMode.None).OrderBy(p=>(p.transform.position-transform.position).sqrMagnitude))
            {
                var hurtbox=target.GetComponentInChildren<CombatHurtbox>();
                if(!hurtbox || hurtbox.team==player.hitbox.team || !target.CanBeGrabbed || (target.motor.IsGrounded ? !box.canGrabGrounded : !box.canGrabAirborne)) continue;
                if(leapEnabled && Mathf.Abs(motor.Height-target.motor.Height)>box.maximumHeightDifference)continue;
                var offset=LockedGrabDirection*box.offset.x+Vector2.up*box.offset.y;
                if(!SweptBox(previous+offset,(Vector2)transform.position+offset,target.transform.position,box.width,box.depth) || Vector2.Dot((Vector2)target.transform.position-previous,LockedGrabDirection)<=0) continue;
                if (box.canBeParried && target.ParryActive && !hurtbox.externalInvulnerable && hurtbox.isActiveAndEnabled && hurtbox.GetComponent<Collider2D>().enabled &&
                    target.TryDefense(new AttackHitboxData { damage=0, canBeParried=true }, player.Facing, motor) == CombatHitOutcome.Parry)
                { player.Freeze(target.defenseData.parryHitstopFrames); target.attackPlayer.Freeze(target.defenseData.parryHitstopFrames); return; }
                if(!target.TryEnterGrab(this)) continue;
                if(LeapActive)motor.SnapGrabToGround(transform.position);
                CurrentGrabbedTarget=target;target.health.Died+=VictimDied; Captures++; GrabActive=LungeActive=LeapActive=false;motor.StopGroundedMotion(); SwitchingBranch=true;
                bool started=false; try { started=player.Play(successfulGrab); } finally {SwitchingBranch=false;}
                if(!started) Abort(); return;
            }
            if(LungeActive && LungeDistanceRemaining<=.0001f)MissRecovery();
        }
        void OnDrawGizmos()
        {
            if(!debugDraw) return; var playback=player ? player : GetComponent<AttackPlayer>();
            if(!playback || !grabAttack || grabAttack.FirstActiveFrame<0) return;
            Gizmos.color=GrabActive ? Color.red : Color.cyan;
            var direction=DirectionLocked ? LockedGrabDirection : new Vector2(GetComponent<CharacterMotor>().Facing,0);
            foreach(var box in grabAttack.frames[grabAttack.FirstActiveFrame].grabHitboxes) Gizmos.DrawWireCube(transform.position+(Vector3)(direction*box.offset.x+Vector2.up*box.offset.y),new Vector3(box.width,box.depth,.01f));
            Gizmos.color=Color.yellow;Gizmos.DrawLine(transform.position,transform.position+(Vector3)(direction*LungeDistanceRemaining));
            DrawLeapGizmos();
        }
    }
}
