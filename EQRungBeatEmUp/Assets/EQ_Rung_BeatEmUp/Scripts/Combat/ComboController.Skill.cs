namespace BeatEmUp
{
    public sealed partial class ComboController
    {
        bool skillPlaying;
        public bool CanStartSkill => isActiveAndEnabled && !CombatClock.IsPaused && health && !health.IsDead &&
            motor && motor.IsGrounded && (State == CombatState.Idle || State == CombatState.Run) && !CurrentAttack && !IsDefenseState &&
            stun == 0 && cooldown == 0 && !attackPlayer.IsFrozen;
        public bool StartSkill(AttackData cast)
        {
            if (!CanStartSkill || !cast || cast.domain != AttackDomain.Ground || cast.requiresAirborne) return false;
            buffered = CombatInput.None; bufferFrames = jumpBuffer = 0; ComboIndex = nextIndex = 0;
            routeAir = false; bufferedAirDive = false;
            if (!attackPlayer.Play(cast)) return false;
            EndRun();
            skillPlaying = true;
            State = CombatState.GroundAttack; motor.MovementLocked = true; return true;
        }
    }
}
