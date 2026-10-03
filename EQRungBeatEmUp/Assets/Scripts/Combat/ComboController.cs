using UnityEngine;

namespace BeatEmUp
{
    [DefaultExecutionOrder(-10)]
    public sealed class ComboController : MonoBehaviour
    {
        [Header("Existing character components")]
        public CharacterMotor motor;
        public CharacterHealth health;
        public CharacterAnimation animationDriver;
        public AttackHitbox hitbox;
        [Header("Routes: exactly three attacks each")]
        public AttackData[] groundCombo = new AttackData[3];
        public AttackData launcher;
        public AttackData[] airCombo = new AttackData[3];
        [Tooltip("Launcher branches after this many ground punches (2 gives Punch1 -> Punch2 -> Launcher).")]
        [Min(1)] public int launcherAfterGroundHit = 2;
        [Header("Buffer / reset")]
        [Min(.01f)] public float inputBufferDuration = .35f;
        [Min(.01f)] public float jumpBufferDuration = .6f;
        [Min(0)] public float comboResetTime = .35f;
        [Tooltip("Extra cooldown after a terminal attack; mashing cannot restart immediately.")]
        [Min(0)] public float finisherRecovery = .12f;
        public CombatState State { get; private set; }
        public AttackData CurrentAttack { get; private set; }
        public int ComboIndex { get; private set; }
        public CombatInput BufferedInput => bufferTime > 0 ? buffered : CombatInput.None;
        public bool JumpBuffered => jumpBuffer > 0;
        private CombatInput buffered;
        private float bufferTime, jumpBuffer, elapsed, idleTime, cooldown, stun;
        private int nextIndex, airAttacksUsed;
        private bool routeAir;
        
        private void OnEnable() 
        { 
            if (motor) motor.Landed += OnLanding; 
        }
        
        private void OnDisable()
        {
            if (motor) { motor.Landed -= OnLanding; motor.AirAttackControl = false; motor.MovementLocked = false; }
            ResetCombo();
        }

        public void RequestAttack() 
        { 
            Debug.Log("RequestAttack called");
            Buffer(CombatInput.Attack); 
        }

        public void RequestLauncher() 
        { 
            Buffer(CombatInput.Launcher);
        }

        public void RequestJump()
        {
            if (!health || health.IsDead || stun > 0 || !motor.IsGrounded) return;
            jumpBuffer = jumpBufferDuration;
            TryJump();
        }

        private void Buffer(CombatInput input)
        {
            if (!health || health.IsDead || stun > 0) return;
            // One pending request, never an unbounded queue from mashing.
            buffered = input; bufferTime = inputBufferDuration;
            TryConsume();
        }

        public void Interrupt(float duration)
        {
            ResetCombo(); stun = Mathf.Max(stun, duration); State = CombatState.Hitstun;
            animationDriver.Play("GroundHit", true);
        }

        public void ResetCombo()
        {
            CurrentAttack = null; ComboIndex = 0; nextIndex = 0; elapsed = 0; idleTime = 0;
            buffered = CombatInput.None; bufferTime = 0; jumpBuffer = 0;
            if (hitbox) hitbox.End();
            if (motor) motor.AirAttackControl = false;
        }

        private void OnLanding() { ResetCombo(); airAttacksUsed = 0; State = stun > 0 ? CombatState.Hitstun : CombatState.Idle; }
        
        private void Update() { Tick(Time.deltaTime); }
        
        public void Tick(float dt)
        {
            if (!motor || !health) return;
            cooldown = Mathf.Max(0, cooldown - dt);
            if (health.IsDead)
            {
                ResetCombo(); motor.MovementLocked = true; State = CombatState.Hitstun;
                animationDriver.Play("GroundHit"); return;
            }
            if (stun > 0)
            {
                stun = Mathf.Max(0, stun - dt); motor.MovementLocked = true; State = CombatState.Hitstun; return;
            }
            if (CurrentAttack)
            {
                float previous = elapsed; elapsed += dt;
                if (previous < CurrentAttack.startup + CurrentAttack.activeDuration && elapsed >= CurrentAttack.startup) hitbox.Sample();
                TryJump();
                TryConsume();
                if (CurrentAttack && elapsed >= CurrentAttack.Duration) Finish();
            }
            else
            {
                idleTime += dt;
                if (idleTime > comboResetTime) { nextIndex = 0; ComboIndex = 0; }
                TryJump(); TryConsume();
            }
            bufferTime = Mathf.Max(0, bufferTime - dt);
            jumpBuffer = Mathf.Max(0, jumpBuffer - dt);
            motor.MovementLocked = CurrentAttack && motor.IsGrounded;
            motor.AirAttackControl = CurrentAttack && CurrentAttack.domain == AttackDomain.Air;
            if (!CurrentAttack)
            {
                State = motor.IsGrounded ? CombatState.Idle : CombatState.Jumping;
                animationDriver.Play(!motor.IsGrounded ? "Jumping" : motor.MoveInput.sqrMagnitude > .01f ? "Walk" : "Idle");
            }
        }

        private void TryJump()
        {
            if (jumpBuffer <= 0 || !motor.IsGrounded || stun > 0 || cooldown > 0) return;
            if (CurrentAttack && (!CurrentAttack.canLaunch || elapsed < CurrentAttack.startup + CurrentAttack.activeDuration)) return;
            ResetCombo(); motor.Jump(); State = CombatState.Jumping;
            animationDriver.Play("Jumping", true);
        }

        private void TryConsume()
        {
            if (bufferTime <= 0 || stun > 0 || cooldown > 0) return;
            bool air = !motor.IsGrounded;
            if (air && (buffered == CombatInput.Launcher || airAttacksUsed >= airCombo.Length)) { bufferTime = 0; return; }
            int index = CurrentAttack ? ComboIndex : nextIndex;
            if (CurrentAttack)
            {
                if (CurrentAttack.canLaunch || air != routeAir || index >= (air ? airCombo.Length : groundCombo.Length)) return;
                if (elapsed < CurrentAttack.comboWindowOpen || elapsed > CurrentAttack.WindowEnd) return;
            }
            else if (air != routeAir) index = 0;
            AttackData attack = null;
            if (buffered == CombatInput.Launcher)
            {
                if (!air && index == launcherAfterGroundHit) attack = launcher;
            }
            else
            {
                AttackData[] route = air ? airCombo : groundCombo;
                if (index >= 0 && index < route.Length) attack = route[index];
            }
            if (!attack || (attack.domain == AttackDomain.Air) != air) return;
            buffered = CombatInput.None; bufferTime = 0;
            StartAttack(attack, index + 1, air);
        }

        private void StartAttack(AttackData attack, int index, bool air)
        {
            CurrentAttack = attack; 
            ComboIndex = index; 
            routeAir = air; 
            elapsed = 0; 
            idleTime = 0;
            
            if (air) airAttacksUsed++;

            State = attack.canLaunch ? CombatState.Launcher : air ? CombatState.AirAttack : CombatState.GroundAttack;
            hitbox.Begin(attack); animationDriver.Play(attack.animationState, true);
        }

        private void Finish()
        {
            bool terminal = CurrentAttack.canLaunch || ComboIndex >= (routeAir ? airCombo.Length : groundCombo.Length);
            float delay = CurrentAttack.cooldown + (terminal ? finisherRecovery : 0);
            int index = ComboIndex;
            CurrentAttack = null; hitbox.End(); motor.AirAttackControl = false; idleTime = 0;
            nextIndex = terminal ? 0 : index;
            cooldown = delay;
            if (terminal) { bufferTime = 0; buffered = CombatInput.None; ComboIndex = 0; }
        }
    }
}
