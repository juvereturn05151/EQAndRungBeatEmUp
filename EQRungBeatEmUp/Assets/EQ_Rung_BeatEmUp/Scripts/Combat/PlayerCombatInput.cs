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
        private RunUpgradeController runMenu;
        
        private InputAction move, attack, launcher, jump, guard, skill;
        // Match the existing directional dodge threshold; the motor normalizes diagonals.
        public static bool HasDefenseDirection(Vector2 direction) => direction.sqrMagnitude > .01f;
        private bool HubMenuOpen => GetComponent<MetaProgress>()?.OpenStation>=0 || runMenu && runMenu.enabled && runMenu.MenuOpen;
        
        private void Start() 
        { 
            runMenu=FindFirstObjectByType<RunUpgradeController>();
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
            skill = map.FindAction("Skill", true);

            move.performed += OnMove; 
            move.canceled += OnMove;
            attack.performed += OnAttack; 
            launcher.performed += OnLauncher; 
            jump.performed += OnJump;
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
            // Offensive buttons use callbacks; defense resolves the action's press/hold state below.
            if(move==null || !motor || guard==null) return;
            // Resolve after the Input System has processed all movement/button events.
            // A directional press must never enter Guard first, regardless of event order.
            var direction=move.ReadValue<Vector2>();
            bool blocked=CombatClock.IsPaused || HubMenuOpen;
            motor.MoveInput=blocked ? Vector2.zero : direction;
            bool directional=HasDefenseDirection(direction);
            combat.RequestRun(!blocked && guard.IsPressed() && directional,direction);
            combat.RequestGuard(!blocked && guard.IsPressed() && !directional);
            if(!blocked && guard.WasPressedThisFrame() && directional)
            { LastAction="Dash / Run"; }
            else if(!blocked && guard.IsPressed() && !directional) LastAction="Guard / Parry";
        }
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
            if (combat) combat.ResetRunInput();
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
            if (skill != null) skill.performed -= OnSkill;
        }
    }
}
