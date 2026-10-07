using UnityEngine;

namespace BeatEmUp
{
    // IMGUI debug panels retain mouse support while sharing the game's button controls.
    public sealed class ImmediateMenuNavigation
    {
        int focus, count, drawn;
        bool submit;
        public void Read(MenuNavigationState input)
        {
            if(count>0 && input.Navigate!=Vector2.zero)
                focus=(focus+(input.Navigate.x>0 || input.Navigate.y<0 ? 1 : count-1))%count;
            submit|=input.Confirm;
        }
        public void Begin() { drawn=0; }
        public bool Button(string label)
        {
            if(!GUI.enabled) return GUILayout.Button(label);
            int index=drawn++;
            bool clicked=GUILayout.Button((focus==index ? "► " : "")+label);
            bool pressed=submit && focus==index && Event.current.type==EventType.Repaint;
            if(pressed) submit=false;
            return clicked || pressed;
        }
        public void End() { count=drawn; focus=Mathf.Clamp(focus,0,Mathf.Max(0,count-1)); if(Event.current.type==EventType.Repaint) submit=false; }
    }
}
