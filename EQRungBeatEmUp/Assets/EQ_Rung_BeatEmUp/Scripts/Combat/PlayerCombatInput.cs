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
        
        private InputAction move, attack, launcher, jump, guard, dodge;
        
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

            move.performed += OnMove; 
            move.canceled += OnMove;
            attack.performed += OnAttack; 
            launcher.performed += OnLauncher; 
            jump.performed += OnJump;
            guard.performed += OnGuard;
            guard.canceled += OnGuardReleased;
            dodge.performed += OnDodge;
            // PlayerInput owns map enabling, device pairing and per-player action copies.
        }

        private void OnMove(InputAction.CallbackContext context)
        { 
            motor.MoveInput = context.ReadValue<Vector2>(); 
            LastAction = "Move"; 
        }
        private void Update()
        {
            // Preserve intended movement after a recovery clears motor input.
            // Buttons remain edge-triggered through action callbacks.
            if (move != null && motor) motor.MoveInput = move.ReadValue<Vector2>();
        }
        private void OnGuard(InputAction.CallbackContext context) { LastAction = "Guard / Parry"; combat.RequestGuard(true); }
        private void OnGuardReleased(InputAction.CallbackContext context) { combat.RequestGuard(false); }
        private void OnDodge(InputAction.CallbackContext context) { LastAction = "Dodge"; combat.RequestDodge(); }
        
        private void OnAttack(InputAction.CallbackContext context) 
        { 
            LastAction = "Attack"; 
            combat.RequestAttack(); 
        }
        
        private void OnLauncher(InputAction.CallbackContext context) 
        { 
            LastAction = "Launcher"; 
            combat.RequestLauncher(); 
        }
        
        private void OnJump(InputAction.CallbackContext context) 
        { 
            LastAction = "Jump"; 
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
        }
    }
}
