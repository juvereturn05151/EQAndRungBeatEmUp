using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatEmUp
{
    // Same private action-copy/device restriction strategy used by SessionInput.
    public sealed class CharacterSelectPlayerCursor : IDisposable
    {
        public InputDevice Device { get; }
        readonly InputActionAsset actions;
        readonly CharacterSelectManager manager;
        readonly int playerIndex, createdFrame;
        Vector2 direction;
        float repeatAt;
        public CharacterSelectPlayerCursor(InputActionAsset template, InputDevice device, int index, CharacterSelectManager screen)
        {
            Device = device; manager = screen; playerIndex = index; createdFrame = Time.frameCount;
            actions = UnityEngine.Object.Instantiate(template);
            actions.devices = new[] { device };
            actions.bindingMask = InputBinding.MaskByGroup(device is Keyboard ? "Keyboard&Mouse" : "Gamepad");
            var map = actions.FindActionMap("CharacterSelect", true);
            map.FindAction("Navigate", true).performed += Navigate;
            map.FindAction("Navigate", true).canceled += Stop;
            map.FindAction("Confirm", true).performed += Confirm;
            map.FindAction("Cancel", true).performed += Cancel;
            map.FindAction("Start", true).performed += Start;
            map.Enable();
        }
        void Navigate(InputAction.CallbackContext context)
        {
            var value = context.ReadValue<Vector2>();
            if(value.magnitude < .55f) { direction = Vector2.zero; return; }
            var next = Mathf.Abs(value.x) >= Mathf.Abs(value.y) ? new Vector2(Mathf.Sign(value.x), 0) : new Vector2(0, Mathf.Sign(value.y));
            if(next == direction) return;
            direction = next; manager.Navigate(playerIndex, direction); repeatAt = Time.unscaledTime + .28f;
        }
        void Stop(InputAction.CallbackContext context) => direction = Vector2.zero;
        void Confirm(InputAction.CallbackContext context) { if(Time.frameCount > createdFrame) manager.Confirm(playerIndex); }
        void Cancel(InputAction.CallbackContext context) { if(Time.frameCount > createdFrame) manager.Cancel(playerIndex); }
        void Start(InputAction.CallbackContext context) { if(Time.frameCount > createdFrame) manager.StartGame(playerIndex); }
        public void Tick()
        {
            if(direction != Vector2.zero && Time.unscaledTime >= repeatAt) { manager.Navigate(playerIndex, direction); repeatAt = Time.unscaledTime + .11f; }
        }
        public void Dispose() { actions.Disable(); UnityEngine.Object.Destroy(actions); }
    }
}
