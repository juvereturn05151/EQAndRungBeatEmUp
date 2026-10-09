using UnityEngine;
namespace BeatEmUp
{
    public sealed partial class ComboController
    {
        public PlayerRunData runData;
        public bool IsRunning=>State==CombatState.Run;
        public int RunFrame { get; private set; }
        public bool RunHeld=>runRequested;
        public int EffectiveDashToRunFrame=>defenseData&&runData ? Mathf.Clamp(runData.dashToRunFrame,
            Mathf.Max(defenseData.dodgeMoveLastFrame+1,defenseData.dodgeInvulnerableLastFrame+1),EffectiveDodgeFrames) : int.MaxValue;
        bool runRequested,runChordLatched;
        Vector2 runDirection;
        public void RequestRun(bool held,Vector2 direction)
        {
            runDirection=Vector2.ClampMagnitude(direction,1);
            runRequested=held&&PlayerCombatInput.HasDefenseDirection(runDirection);
            if(!runRequested)
            {
                runChordLatched=false;
                if(IsRunning) EndRun();
                return;
            }
            if(runChordLatched||CombatClock.IsPaused) return;
            if((State==CombatState.GuardEnter||State==CombatState.GuardHold)&&blockstun<=0) RequestGuard(false);
            // The same committed dash starts for both input orders: direction then Guard, or Guard then direction.
            if(RequestDodge()) { motor.Face(runDirection.x); runChordLatched=true; }
        }
        bool CanDashIntoRun=>runRequested&&runData&&runData.poses!=null&&runData.poses.Length>0&&
            motor.IsGrounded&&health&&!health.IsDead&&!CurrentAttack;
        void BeginRun()
        {
            ClearDefenseControl();guardHeld=parryArmed=false;DefenseFrame=blockstun=0;
            State=CombatState.Run;RunFrame=0;motor.MovementLocked=false;
            motor.MoveInput=runDirection;motor.GroundSpeedOverride=runData.runSpeed;motor.Face(runDirection.x);
            ShowRun();
        }
        void EndRun()
        {
            if(motor) motor.GroundSpeedOverride=0;RunFrame=0;
            if(!IsRunning) return;
            animationDriver.ReleaseReactionControl();motor.MovementLocked=false;
            State=motor.IsGrounded?CombatState.Idle:CombatState.Jumping;
            animationDriver.Play(!motor.IsGrounded?"Jumping":motor.MoveInput.sqrMagnitude>.01f?"Walk":"Idle",true);
        }
        bool UpdateRun()
        {
            if(!IsRunning)return false;
            if(!CanDashIntoRun||CurrentAttack){EndRun();return false;}
            motor.MoveInput=runDirection;motor.MovementLocked=false;motor.GroundSpeedOverride=runData.runSpeed;
            motor.Face(runDirection.x);ShowRun();RunFrame++;return true;
        }
        void ShowRun() => animationDriver.HoldSprite(motor.sprite,PlayerDefenseData.LoopPose(runData.poses,RunFrame));
        public void ResetRunInput()
        {
            runRequested=runChordLatched=false;runDirection=Vector2.zero;EndRun();
        }
    }
}
