using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatEmUp
{
    [RequireComponent(typeof(PlayerInput))]
    [DefaultExecutionOrder(-30)]
    public sealed class PlayerCombatInput : MonoBehaviour
    {
        [SerializeField]
        private ComboController combat;

        [SerializeField]
        private CharacterMotor motor;

        public string LastAction { get; private set; } = "None";
        
        private PlayerInput playerInput;
        
        private InputAction move, attack, launcher, jump, guard, dodge, skill;
        private bool HubMenuOpen => GetComponent<MetaProgress>()?.OpenStation>=0;
        
        private void Start() 
        { 
            Bind(); 
        }

        private void OnEnable() 
        { 
            if (playerInput) 
            { 
                Bind(); 
            } 
        }
        
        private void Bind()
        {
            Unbind();
            playerInput = GetComponent<PlayerInput>();
            var map = playerInput.actions.FindActionMap("Player", true);

            move = map.FindAction("Move", true); 
            attack = map.FindAction("Attack", true);
            launcher = map.FindAction("Launcher", true); 
            jump = map.FindAction("Jump", true);
            guard = map.FindAction("Guard", true);
            dodge = map.FindAction("Dodge", true);
            skill = map.FindAction("Skill", true);

            move.performed += OnMove; 
            move.canceled += OnMove;
            attack.performed += OnAttack; 
            launcher.performed += OnLauncher; 
            jump.performed += OnJump;
            guard.performed += OnGuard;
            guard.canceled += OnGuardReleased;
            dodge.performed += OnDodge;
            skill.performed += OnSkill;
            // PlayerInput owns map enabling, device pairing and per-player action copies.
        }

        private void OnMove(InputAction.CallbackContext context)
        { 
            motor.MoveInput = CombatClock.IsPaused || HubMenuOpen ? Vector2.zero : context.ReadValue<Vector2>();
            LastAction = "Move"; 
        }
        private void Update()
        {
            // Preserve intended movement after a recovery clears motor input.
            // Buttons remain edge-triggered through action callbacks.
            if (move != null && motor) motor.MoveInput = CombatClock.IsPaused || HubMenuOpen ? Vector2.zero : move.ReadValue<Vector2>();
        }
        private void OnGuard(InputAction.CallbackContext context) { if(HubMenuOpen) return; LastAction = "Guard / Parry"; combat.RequestGuard(true); }
        private void OnGuardReleased(InputAction.CallbackContext context) { combat.RequestGuard(false); }
        private void OnDodge(InputAction.CallbackContext context) { if(HubMenuOpen) return; LastAction = "Dodge"; combat.RequestDodge(); }
        private void OnSkill(InputAction.CallbackContext context) { if(HubMenuOpen) return; LastAction = "Skill"; combat.GetComponent<PlayerSkillController>()?.RequestSkill(); }
        
        private void OnAttack(InputAction.CallbackContext context) 
        { 
            LastAction = "Attack";
            if(HubMenuOpen) return;
            combat.RequestAttack(); 
        }
        
        private void OnLauncher(InputAction.CallbackContext context) 
        { 
            LastAction = "Launcher";
            if(HubMenuOpen) return;
            combat.RequestLauncher(); 
        }
        
        private void OnJump(InputAction.CallbackContext context) 
        { 
            LastAction = "Jump";
            if(HubMenuOpen) return;
            combat.RequestJump(); 
        }
        
        private void OnDisable() 
        { 
            Unbind();
            if (combat) combat.RequestGuard(false);

            if (motor)
            {
                motor.MoveInput = Vector2.zero;
            } 
        }
        
        private void Unbind()
        {
            if (move != null) 
            { 
                move.performed -= OnMove; 
                move.canceled -= OnMove; 
            }

            if (attack != null) attack.performed -= OnAttack;
            if (launcher != null) launcher.performed -= OnLauncher;
            if (jump != null) jump.performed -= OnJump;
            if (guard != null) { guard.performed -= OnGuard; guard.canceled -= OnGuardReleased; }
            if (dodge != null) dodge.performed -= OnDodge;
            if (skill != null) skill.performed -= OnSkill;
        }
    }
}
