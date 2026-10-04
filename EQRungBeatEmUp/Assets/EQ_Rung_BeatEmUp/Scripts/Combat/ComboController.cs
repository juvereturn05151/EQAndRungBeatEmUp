using UnityEngine;

namespace BeatEmUp
{
    [RequireComponent(typeof(AttackPlayer))]
    public sealed partial class ComboController : MonoBehaviour, ICombatFrameListener
    {
        public CharacterMotor motor;
        public CharacterHealth health;
        public CharacterAnimation animationDriver;
        public AttackHitbox hitbox;
        public AttackPlayer attackPlayer;
        public PlayerDefenseData defenseData;
        public AttackData[] groundCombo = new AttackData[3];
        public AttackData launcher;
        public AttackData[] airCombo = new AttackData[3];
        public AttackData airDive;
        public bool AirDiveUsed { get; private set; }
        public bool IsAirDiving => airDive && CurrentAttack == airDive;
        public RunBuildState Build => GetComponent<RunBuildState>();
        [Min(1)] public int launcherAfterGroundHit = 2;
        [Header("Combat frames")]
        [Min(1)] public int inputBufferFrames = 6;
        [Min(1)] public int jumpBufferFrames = 36;
        [Min(0)] public int comboResetFrames = 21;
        [Min(0)] public int finisherRecoveryFrames = 7;
        public CombatState State { get; private set; }
        public AttackData CurrentAttack => attackPlayer ? attackPlayer.CurrentAttack : null;
        public int ComboIndex { get; private set; }
        public CombatInput BufferedInput => bufferFrames > 0 ? buffered : CombatInput.None;
        public bool JumpBuffered => jumpBuffer > 0;
        public int FrameOrder => 30;
        private CombatInput buffered;
        private int bufferFrames, jumpBuffer, idleFrames, cooldown, stun, nextIndex, airAttacksUsed;
        private bool routeAir;
        private bool bufferedAirDive;
        private void Awake()
        {
            if (!attackPlayer)
            {
                attackPlayer = GetComponent<AttackPlayer>();
            }
        }

        private void OnEnable()
        {
            if (motor) { 
                motor.Landed += OnLanding; 
            }

            if (!attackPlayer) 
            { 
                attackPlayer = GetComponent<AttackPlayer>(); 
            }

            attackPlayer.Finished += Finish;
            
            if (health) 
            { 
                health.Died += EnterDie; 
                health.Restored += RestorePlayer; 
            }

            CombatClock.Register(this);
        }
        
        private void OnDisable()
        {
            CombatClock.Unregister(this);

            if (motor) 
            { 
                motor.Landed -= OnLanding; 
                motor.MovementLocked = false; 
            }
            
            if (attackPlayer) attackPlayer.Finished -= Finish;
            if (health) { health.Died -= EnterDie; health.Restored -= RestorePlayer; }
            ClearDefenseControl();
            ResetCombo();
        }

        public void RequestAttack() => Buffer(CombatInput.Attack);
        
        public void RequestLauncher() => Buffer(CombatInput.Launcher);
        
        public void RequestJump()
        {
            if (CombatClock.IsPaused || !health || health.IsDead || stun > 0 || IsDefenseState || IsAirDiving || !motor.IsGrounded) 
            { 
                return; 
            }

            jumpBuffer = jumpBufferFrames; 
            TryJump();
        }
        private void Buffer(CombatInput input)
        {
            if (CombatClock.IsPaused || !health || health.IsDead || stun > 0 || IsDefenseState || IsAirDiving) 
            { 
                return; 
            }

            bufferedAirDive = input == CombatInput.Launcher && !motor.IsGrounded;
            buffered = input; bufferFrames = inputBufferFrames; TryConsume();
        }
        public void Interrupt(int frames)
        {
            if (health.IsDead) 
            { 
                EnterDie(); 
                return; 
            }

            if (IsKnockdownState) 
            { 
                return; 
            }

            ClearDefenseControl(); 
            guardHeld = false;
            ResetCombo(); 
            stun = Mathf.Max(stun, frames); 
            State = CombatState.Hitstun;
            motor.MovementLocked = true; 
            animationDriver.Play("GroundHit", true);
        }
        public void ResetCombo()
        {
            if (attackPlayer) attackPlayer.Stop();
            ComboIndex = 0; nextIndex = 0; idleFrames = 0;
            buffered = CombatInput.None; bufferFrames = 0; jumpBuffer = 0;
            bufferedAirDive = false;
            if (motor) { motor.AirAttackControl = false; motor.MovementLocked = stun > 0; }
        }
        private void OnLanding()
        {
            airAttacksUsed = 0;
            AirDiveUsed = false;

            if (IsAirDiving && !health.IsDead && !IsDefenseState)
            {
                buffered = CombatInput.None; 
                bufferFrames = jumpBuffer = 0; 
                bufferedAirDive = false;
                attackPlayer.BeginLanding(); 
                State = CombatState.GroundAttack; 
                motor.MovementLocked = true; 
                Build?.DiveImpact(); 
                
                return;
            }

            if (health.IsDead || State == CombatState.Die) 
            { 
                DefenseLanded(); 
                return; 
            }

            if (IsKnockdownState) 
            { 
                DefenseLanded(); 
                return; 
            }

            ResetCombo(); State = stun > 0 ? CombatState.Hitstun : CombatState.Idle;
        }
        public void CombatFrame()
        {
            if (!motor || !health) 
            { 
                return; 
            }

            if (health.IsDead && State != CombatState.Die) 
            { 
                EnterDie(); 
            }

            if (attackPlayer.IsFrozen) 
            { 
                return; 
            }

            if (UpdateDefense()) 
            { 
                return; 
            }

            if (stun > 0) 
            { 
                stun--; motor.MovementLocked = true; State = CombatState.Hitstun; return; 
            }

            if (cooldown > 0) 
            { 
                cooldown--; 
            }

            if (!CurrentAttack && ++idleFrames > comboResetFrames) 
            { 
                nextIndex = 0; 
                ComboIndex = 0; 
            }

            TryJump(); 
            TryConsume();

            if (bufferFrames > 0) 
            { 
                bufferFrames--; 
            }

            if (jumpBuffer > 0) 
            { 
                jumpBuffer--; 
            }

            motor.MovementLocked = IsAirDiving || (CurrentAttack && motor.IsGrounded);

            if (!CurrentAttack)
            {
                State = motor.IsGrounded ? CombatState.Idle : CombatState.Jumping;
                animationDriver.Play(!motor.IsGrounded ? "Jumping" : motor.MoveInput.sqrMagnitude > .01f ? "Walk" : "Idle");
            }
        }
        private void TryJump()
        {
            if (jumpBuffer <= 0 || !motor.IsGrounded || stun > 0 || IsDefenseState || cooldown > 0 || attackPlayer.IsFrozen) 
            { 
                return; 
            }

            if (CurrentAttack && !attackPlayer.Frame.canCancelIntoJump) 
            { 
                return; 
            }
            
            ResetCombo(); 
            motor.MovementLocked = false; 
            motor.Jump(); 
            State = CombatState.Jumping;
            animationDriver.Play("Jumping", true);
        }
        private void TryConsume()
        {
            if (bufferFrames <= 0 || stun > 0 || IsDefenseState || cooldown > 0 || attackPlayer.IsFrozen) 
            { 
                return; 
            }
            
            bool air = !motor.IsGrounded;

            if (bufferedAirDive && !air) 
            { 
                bufferFrames = 0; 
                buffered = CombatInput.None; 
                bufferedAirDive = false; 
                return; 
            }

            if (air && buffered == CombatInput.Launcher)
            {
                if (!airDive || AirDiveUsed) 
                { 
                    bufferFrames = 0; 
                    buffered = CombatInput.None; 
                    return; 
                }

                if (CurrentAttack && (!routeAir || attackPlayer.Frame == null || (!attackPlayer.Frame.canCancelIntoAttack && !attackPlayer.Frame.canCancelIntoLauncher))) 
                { 
                    return; 
                }

                if (!attackPlayer.Play(airDive)) 
                { 
                    return; 
                }

                AirDiveUsed = true; 
                bufferedAirDive = false; 
                buffered = CombatInput.None; 
                bufferFrames = jumpBuffer = 0;
                ComboIndex = nextIndex = 0; 
                routeAir = true; 
                State = CombatState.AirAttack; 
                motor.MovementLocked = true; 
                
                return;
            }

            if (air && airAttacksUsed >= airCombo.Length) 
            { 
                bufferFrames = 0; 
                return; 
            }

            int index = CurrentAttack ? ComboIndex : nextIndex;

            if (CurrentAttack)
            {
                if (CurrentAttack.isLauncher || air != routeAir)
                {
                    return;
                }

                var frame = attackPlayer.Frame;

                if (frame == null || (buffered == CombatInput.Attack ? !frame.canCancelIntoAttack : !frame.canCancelIntoLauncher))
                {
                    return;
                }
            }
            else if (air != routeAir) 
            { 
                index = 0; 
            }
            
            AttackData attack = null;
            
            if (buffered == CombatInput.Launcher) 
            {
                if (!air && (!CurrentAttack || index == launcherAfterGroundHit)) 
                { 
                    attack = launcher; 
                }
            }
            else
            {
                var route = air ? airCombo : groundCombo;
                if (index >= 0 && index < route.Length) attack = route[index];
            }

            if (!attack || (attack.domain == AttackDomain.Air) != air || !attackPlayer.Play(attack)) 
            { 
                return; 
            }

            buffered = CombatInput.None; bufferFrames = 0;
            ComboIndex = index + 1; 
            routeAir = air; 
            idleFrames = 0;

            if (air) 
            { 
                airAttacksUsed++; 
            }

            State = attack.isLauncher ? CombatState.Launcher : air ? CombatState.AirAttack : CombatState.GroundAttack;
            motor.MovementLocked = motor.IsGrounded;
        }

        private void Finish(AttackData finished)
        {
            Build?.Notify(RunCombatEvent.ComboFinished);

            if (finished == airDive)
            {
                buffered = CombatInput.None; bufferFrames = jumpBuffer = 0; bufferedAirDive = false;
                ComboIndex = nextIndex = 0; cooldown = finished.cooldownFrames;
                motor.MovementLocked = false; motor.AirAttackControl = false;
                State = motor.IsGrounded ? CombatState.Idle : CombatState.Jumping; return;
            }
            bool terminal = finished.isLauncher || ComboIndex >= (routeAir ? airCombo.Length : groundCombo.Length);
            cooldown = finished.cooldownFrames + (terminal ? finisherRecoveryFrames : 0);
            // Ground chains continue only through authored cancel windows.
            // Preserve the existing between-attack continuation for air routes.
            bool resetRoute = terminal || (!routeAir && !finished.isLauncher);
            nextIndex = resetRoute ? 0 : ComboIndex; idleFrames = 0;
            
            if (resetRoute) 
            { 
                bufferFrames = 0; 
                buffered = CombatInput.None; 
                ComboIndex = 0; 
            }
        }
    }
}
