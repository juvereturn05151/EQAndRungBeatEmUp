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
        
        private InputAction move, attack, launcher, jump;
        
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

            move.performed += OnMove; 
            move.canceled += OnMove;
            attack.performed += OnAttack; 
            launcher.performed += OnLauncher; 
            jump.performed += OnJump;
            // PlayerInput owns map enabling, device pairing and per-player action copies.
        }

        private void OnMove(InputAction.CallbackContext context) 
        { 
            motor.MoveInput = context.ReadValue<Vector2>(); 
            LastAction = "Move"; 
        }
        
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
        }
    }
}
