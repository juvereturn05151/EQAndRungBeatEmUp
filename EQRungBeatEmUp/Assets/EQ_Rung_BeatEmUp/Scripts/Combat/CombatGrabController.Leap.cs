using System;
using UnityEngine;
namespace BeatEmUp
{
    public sealed partial class CombatGrabController
    {
        [Header("Optional committed airborne grab")]
        public bool leapEnabled;
        [Min(0)] public float leapHorizontalSpeed=10, leapDistance=4, leapHeight=1.6f;
        [Min(1)] public int leapDurationFrames=30;
        [Header("Held face-strike sequence (authored FaceStrike / RepeatFaceAttacks events)")]
        [Min(1)] public int faceAttackCount=3;
        public AttackHitboxData faceHit=new AttackHitboxData{damage=8,hitstunFrames=0,hitstopFrames=2,knockback=0,unblockable=true,canBeParried=false};
        public Sprite heldImpactPose;
        [Min(1)] public int heldImpactPoseFrames=4;
        public bool LeapActive { get; private set; }
        public Transform LockedTarget { get; private set; }
        public Vector2 LeapOrigin { get; private set; }
        public Vector2 LeapDestination { get; private set; }
        public int FaceStrikesApplied { get; private set; }
        public int StrikeFeedbackCount { get; private set; }
        public event Action<AttackData,Vector2,string> HeldStrikeFeedback;
        int leapStartFrame;
        void PrepareLeapPath()
        {
            LeapOrigin=transform.position;
            float duration=leapDurationFrames*CombatClock.FrameSeconds;
            float distance=Mathf.Min(Vector2.Distance(LeapOrigin,TargetPositionAtCommit),leapDistance,leapHorizontalSpeed*duration);
            LeapDestination=LeapOrigin+LockedGrabDirection*distance;
        }
        void LeapSignal(string signal)
        {
            if(signal=="StartGrabLeap" && leapEnabled){
                if(!DirectionLocked)LockDirection();
                PrepareLeapPath();leapStartFrame=player.CurrentFrame;LeapActive=true;
                motor.FrameGravityScale=0;motor.SetAuthoredHeight(.001f);
            }
            if(signal=="LandGrabLeap" && leapEnabled){LeapActive=false;motor.SetAuthoredHeight(0);motor.FrameGravityScale=1;motor.StopGroundedMotion();GrabActive=false;}
            if(signal=="FaceStrike" && CurrentGrabbedTarget && FaceStrikesApplied<faceAttackCount){
                var victim=CurrentGrabbedTarget;
                var hurtbox=victim.GetComponentInChildren<CombatHurtbox>();
                if(hurtbox && hurtbox.Receive(faceHit.RuntimeCopy(),player.Facing,motor)){
                    FaceStrikesApplied++;StrikeFeedbackCount++;
                    var point=(Vector2)victim.motor.sprite.transform.position+Vector2.up*.95f;
                    AttackFeedback.PlayRemote(successfulGrab,point,player.Facing,true);
                    HeldStrikeFeedback?.Invoke(successfulGrab,point,"FaceStrike"+FaceStrikesApplied);
                    if(victim.IsGrabbed)victim.ShowGrabImpact(heldImpactPose,heldImpactPoseFrames);
                    player.Freeze(faceHit.hitstopFrames);victim.attackPlayer.Freeze(faceHit.hitstopFrames);
                    if(victim.health.IsDead){Abort();player.Stop();}
                }else {Abort();player.Stop();}
            }
            // A frame-data loop, not a timer: repeat the impact/recoil section until the configured count.
            if(signal=="RepeatFaceAttacks" && CurrentGrabbedTarget && FaceStrikesApplied<faceAttackCount){
                int impact=successfulGrab.frames.FindIndex(f=>f.events.Contains("FaceStrike"));
                if(impact>=0)player.SeekFrame(Mathf.Max(0,impact-1));
            }
        }
        bool MoveLeap()
        {
            int elapsed=player.CurrentFrame-leapStartFrame+1;
            float t=Mathf.Clamp01((float)elapsed/leapDurationFrames);
            Vector2 requested=Vector2.Lerp(LeapOrigin,LeapDestination,t);
            Vector2 origin=transform.position;float distance=Vector2.Distance(origin,requested);
            motor.MoveAttack(requested-origin,1);
            LungeDistanceTraveled=Vector2.Distance(LeapOrigin,transform.position);
            if(Vector2.Distance(origin,transform.position)<distance-.0001f){
                StoppedByWall=true;LeapActive=GrabActive=false;motor.SetAuthoredHeight(0);motor.FrameGravityScale=1;
                int landing=grabAttack.frames.FindIndex(f=>f.events.Contains("LandGrabLeap"));
                if(landing>=0)player.SeekFrame(landing,true);else player.Stop();return false;
            }
            motor.FrameGravityScale=0;motor.SetAuthoredHeight(4*leapHeight*t*(1-t));
            if(elapsed>=leapDurationFrames)LeapActive=false;
            return true;
        }
        void DrawLeapGizmos()
        {
            if(!leapEnabled || !DirectionLocked)return;
            Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(LeapDestination,.12f);
            Vector3 previous=LeapOrigin;
            for(int i=1;i<=24;i++){float t=i/24f;Vector3 next=(Vector3)Vector2.Lerp(LeapOrigin,LeapDestination,t)+Vector3.up*(4*leapHeight*t*(1-t));Gizmos.DrawLine(previous,next);previous=next;}
        }
    }
}
