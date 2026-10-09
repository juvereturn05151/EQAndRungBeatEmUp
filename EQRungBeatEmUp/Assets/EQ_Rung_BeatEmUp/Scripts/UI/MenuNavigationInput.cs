using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatEmUp
{
    public struct MenuNavigationState
    {
        public Vector2 Navigate;
        public bool Confirm, Cancel, Start;
    }
    // Reuses the project's menu action map and the session's explicit device pairing.
    public sealed class MenuNavigationInput : IDisposable
    {
        readonly InputActionAsset actions;
        readonly InputActionMap map;
        readonly bool ownsAsset;
        public InputDevice LastDevice { get; private set; }
        Vector2 direction;
        bool moved, confirm, cancel, start;
        float repeatAt;
        int readFrame=-1, createdFrame;
        MenuNavigationState cached;
        public MenuNavigationInput(InputActionAsset template, InputDevice device=null, bool clone=true)
        {
            ownsAsset=clone; actions=clone ? UnityEngine.Object.Instantiate(template) : template;
            if(clone && device!=null) { actions.devices=new[]{device}; actions.bindingMask=InputBinding.MaskByGroup(device is Keyboard ? "Keyboard&Mouse" : "Gamepad"); }
            createdFrame=Time.frameCount;
            map=actions.FindActionMap("CharacterSelect",true);
            map.FindAction("Navigate",true).performed+=Move;
            map.FindAction("Navigate",true).canceled+=Stop;
            map.FindAction("Confirm",true).performed+=Confirm;
            map.FindAction("Cancel",true).performed+=Cancel;
            map.FindAction("Start",true).performed+=Start;
            map.Enable();
        }
        void Move(InputAction.CallbackContext context)
        {
            var value=context.ReadValue<Vector2>();
            if(value.magnitude<.55f) { direction=Vector2.zero; return; }
            var next=Mathf.Abs(value.x)>=Mathf.Abs(value.y) ? new Vector2(Mathf.Sign(value.x),0) : new Vector2(0,Mathf.Sign(value.y));
            if(next==direction) return;
            LastDevice=context.control.device;
            direction=next; moved=true; repeatAt=Time.unscaledTime+.28f;
        }
        void Stop(InputAction.CallbackContext context) => direction=Vector2.zero;
        void Confirm(InputAction.CallbackContext context) { if(Time.frameCount>createdFrame) { LastDevice=context.control.device; confirm=true; } }
        void Cancel(InputAction.CallbackContext context) { if(Time.frameCount>createdFrame) { LastDevice=context.control.device; cancel=true; } }
        void Start(InputAction.CallbackContext context) { if(Time.frameCount>createdFrame) { LastDevice=context.control.device; start=true; } }
        public MenuNavigationState Read()
        {
            if(readFrame==Time.frameCount) return cached;
            readFrame=Time.frameCount;
            bool repeat=direction!=Vector2.zero && Time.unscaledTime>=repeatAt;
            cached=new MenuNavigationState {Navigate=moved || repeat ? direction : Vector2.zero,Confirm=confirm,Cancel=cancel,Start=start};
            if(repeat) repeatAt=Time.unscaledTime+.11f;
            moved=confirm=cancel=start=false;
            return cached;
        }
        public void Dispose()
        {
            map.Disable(); map.FindAction("Navigate").performed-=Move; map.FindAction("Navigate").canceled-=Stop;
            map.FindAction("Confirm").performed-=Confirm; map.FindAction("Cancel").performed-=Cancel; map.FindAction("Start").performed-=Start;
            if(ownsAsset) UnityEngine.Object.Destroy(actions);
        }
    }
}
