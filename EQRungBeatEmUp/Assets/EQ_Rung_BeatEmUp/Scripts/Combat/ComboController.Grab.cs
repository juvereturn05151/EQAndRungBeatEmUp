using UnityEngine;
namespace BeatEmUp
{
    public sealed partial class ComboController
    {
        public bool ungrabbable;
        public bool IsGrabbed => State==CombatState.Grabbed;
        public CombatGrabController GrabOwner { get; private set; }
        Sprite grabImpactPose; int grabImpactFrames;
        public void ShowGrabImpact(Sprite pose,int frames) { grabImpactPose=pose;grabImpactFrames=Mathf.Max(1,frames);ShowGrabbed(); }
        public bool CanBeGrabbed => isActiveAndEnabled && health && !health.IsDead && !health.SafeStageProtection && !ungrabbable && !IsGrabbed && !IsKnockdownState && State!=CombatState.Die && !DodgeInvulnerable && !(attackPlayer.Frame?.invulnerable ?? false);
        public bool TryEnterGrab(CombatGrabController owner)
        {
            var hurtbox=GetComponentInChildren<CombatHurtbox>();
            if(!owner || !owner.isActiveAndEnabled || !owner.OwnerAlive || owner.CurrentGrabbedTarget || !CanBeGrabbed || !hurtbox || !hurtbox.isActiveAndEnabled || !hurtbox.GetComponent<Collider2D>().enabled || hurtbox.externalInvulnerable) return false;
            GetComponent<ComboTracker>()?.EndCombo("Player grabbed"); guardHeld=parryArmed=false;
            BeginDefense(CombatState.Grabbed); GrabOwner=owner;
            motor.SnapGrabToGround(owner.AnchorPosition); motor.MovementLocked=true; ShowGrabbed(); return true;
        }
        internal void DetachGrabOwner() { var owner=GrabOwner; GrabOwner=null;grabImpactPose=null;grabImpactFrames=0; if(owner) owner.ForgetTarget(this); }
        public void ReleaseGrab(CombatGrabController owner) { if(GrabOwner!=owner) return; EndDefense(); }
        void UpdateGrabbed()
        {
            if(!GrabOwner || !GrabOwner.isActiveAndEnabled || !GrabOwner.OwnerAlive || DefenseFrame>=GrabOwner.maximumGrabFrames) { EndDefense(); return; }
            motor.MovementLocked=true; motor.AddKnockback(0); motor.AttackHorizontalVelocity=0; motor.DefenseVelocity=Vector2.zero;
            motor.transform.position=GrabOwner.AnchorPosition; ShowGrabbed();
            if(grabImpactFrames>0)grabImpactFrames--;
        }
        void ShowGrabbed() { if(grabImpactFrames>0 && grabImpactPose)animationDriver.HoldSprite(motor.sprite,grabImpactPose);else if(defenseData) animationDriver.HoldSprite(motor.sprite,PlayerDefenseData.LoopPose(defenseData.grabbed,DefenseFrame)); }
    }
}
