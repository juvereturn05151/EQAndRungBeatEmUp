using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatEmUp
{
    [Flags] public enum PlayerButtons { None=0, Attack=1, Launcher=2, Jump=4, Dodge=8, Interact=16, Skill=32 }
    [Serializable] public sealed class PlayerCommand
    {
        public int sequence;
        public Vector2 move;
        public int buttons;
        public bool guard;
        public int choice=-1;
    }
    // Each source has a private action copy restricted to explicitly assigned devices.
    public sealed class SessionInput : IDisposable
    {
        public InputDevice Device { get; }
        public InputActionAsset Actions { get; }
        public MenuNavigationInput Menu { get; }
        readonly InputAction move, attack, launcher, jump, guard, interact, skill;
        int sequence;
        public SessionInput(InputActionAsset template,InputDevice device)
        {
            Device=device; Actions=UnityEngine.Object.Instantiate(template);
            if(device is Keyboard)
            {
                Actions.devices=Mouse.current!=null ? new InputDevice[]{device,Mouse.current} : new InputDevice[]{device};
                Actions.bindingMask=InputBinding.MaskByGroup("Keyboard&Mouse");
            }
            else { Actions.devices=new[]{device}; Actions.bindingMask=InputBinding.MaskByGroup("Gamepad"); }
            var map=Actions.FindActionMap("Player",true);
            move=map.FindAction("Move",true); attack=map.FindAction("Attack",true); launcher=map.FindAction("Launcher",true);
            jump=map.FindAction("Jump",true); guard=map.FindAction("Guard",true); interact=map.FindAction("Interact",true);
            skill=map.FindAction("Skill",true);
            map.Enable();
            Menu=new MenuNavigationInput(Actions,device,false);
        }
        public PlayerCommand Read()
        {
            PlayerButtons pressed=PlayerButtons.None;
            if(attack.WasPressedThisFrame()) pressed|=PlayerButtons.Attack;
            if(launcher.WasPressedThisFrame()) pressed|=PlayerButtons.Launcher;
            if(jump.WasPressedThisFrame()) pressed|=PlayerButtons.Jump;
            var direction=move.ReadValue<Vector2>();
            bool directional=PlayerCombatInput.HasDefenseDirection(direction);
            if(guard.WasPressedThisFrame() && directional) pressed|=PlayerButtons.Dodge;
            if(skill.WasPressedThisFrame()) pressed|=PlayerButtons.Skill;
            // The asset maps gamepad Y to both launcher and interact; co-op uses Select for interaction.
            if(Device is Keyboard && interact.WasPressedThisFrame()) pressed|=PlayerButtons.Interact;
            var command=new PlayerCommand{sequence=++sequence,move=direction,buttons=(int)pressed,guard=guard.IsPressed() && !directional};
            if(Device is Keyboard key)
            {
                if(key.digit1Key.wasPressedThisFrame) command.choice=0;
                if(key.digit2Key.wasPressedThisFrame) command.choice=1;
                if(key.digit3Key.wasPressedThisFrame) command.choice=2;
                // Preserve the established world-reward interaction key as well as the input asset binding.
                if(key.eKey.wasPressedThisFrame) command.buttons|=(int)PlayerButtons.Interact;
            }
            if(Device is Gamepad pad)
            {
                if(pad.selectButton.wasPressedThisFrame) command.buttons|=(int)PlayerButtons.Interact;
            }
            return command;
        }
        public void Dispose() { Menu.Dispose(); Actions.Disable(); UnityEngine.Object.Destroy(Actions); }
    }
}
