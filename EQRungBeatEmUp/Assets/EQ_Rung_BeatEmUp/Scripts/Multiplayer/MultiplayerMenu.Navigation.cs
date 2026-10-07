using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BeatEmUp
{
    public sealed partial class MultiplayerMenu
    {
        MenuNavigationInput menuInput;
        readonly ImmediateMenuNavigation debugNavigation=new ImmediateMenuNavigation();
        string focusName;
        int gameMenuSlot=-1;
        readonly List<Selectable> menuSelectables=new List<Selectable>();
        readonly List<Button> gameMenuButtons=new List<Button>();
        readonly Dictionary<int,List<Button>> rewardButtons=new Dictionary<int,List<Button>>();
        readonly Dictionary<int,int> rewardFocus=new Dictionary<int,int>();
        readonly Dictionary<Selectable,Image> focusFrames=new Dictionary<Selectable,Image>();
        public bool MenuOpen => session && session.LocalMenuOpen;
        void InitializeNavigation() => menuInput=new MenuNavigationInput(session.catalog.playerPrefab.GetComponent<PlayerInput>().actions);
        void DisposeNavigation() { menuInput?.Dispose(); CombatClock.SetPaused(this,false); }
        void RememberFocus() { if(EventSystem.current && EventSystem.current.currentSelectedGameObject) focusName=EventSystem.current.currentSelectedGameObject.name; }
        Image FocusFrame(Selectable control)
        {
            var frame=new GameObject("Button focus",typeof(RectTransform),typeof(Image)); frame.transform.SetParent(control.transform,false);
            var rect=frame.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
            var image=frame.GetComponent<Image>(); image.sprite=Resources.Load<Sprite>("CharacterSelect/PixelFrame"); image.type=UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier=.25f; image.color=new Color(1,.82f,.3f); image.raycastTarget=false; image.enabled=false; return image;
        }
        void FinishNavigation()
        {
            menuSelectables.Clear(); focusFrames.Clear();
            var controls=page.GetComponentsInChildren<Selectable>().Where(s=>s.isActiveAndEnabled && s.interactable).ToArray();
            foreach(var control in controls)
            {
                control.navigation=new Navigation {mode=Navigation.Mode.None}; focusFrames[control]=FocusFrame(control);
            }
            if(!session.InGame) menuSelectables.AddRange(controls.OrderByDescending(s=>s.transform.position.y).ThenBy(s=>s.transform.position.x));
            else if(session.LocalMenuOpen) menuSelectables.AddRange(gameMenuButtons.Where(b=>b && b.interactable));
            else if(session.Latest!=null && (session.Latest.gameOver || session.Latest.completed)) menuSelectables.AddRange(controls);
            if(EventSystem.current)
            {
                var chosen=menuSelectables.FirstOrDefault(s=>s.name==focusName) ?? menuSelectables.FirstOrDefault();
                EventSystem.current.SetSelectedGameObject(chosen ? chosen.gameObject : null);
            }
            DrawFocus();
        }
        void DrawFocus()
        {
            var current=EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            foreach(var pair in focusFrames) if(pair.Key && pair.Value) pair.Value.enabled=pair.Key.gameObject==current;
            foreach(var pair in rewardButtons)
            {
                int index=rewardFocus.TryGetValue(pair.Key,out int value) ? Mathf.Clamp(value,0,pair.Value.Count-1) : 0;
                for(int i=0;i<pair.Value.Count;i++) if(focusFrames.TryGetValue(pair.Value[i],out var frame) && frame) frame.enabled=i==index;
            }
        }
        void MoveFocus(Vector2 direction)
        {
            if(menuSelectables.Count==0 || !EventSystem.current) return;
            var current=menuSelectables.FirstOrDefault(s=>s.gameObject==EventSystem.current.currentSelectedGameObject);
            if(!current) { EventSystem.current.SetSelectedGameObject(menuSelectables[0].gameObject); return; }
            Vector2 origin=((RectTransform)current.transform).TransformPoint(((RectTransform)current.transform).rect.center);
            Selectable best=null; float score=float.PositiveInfinity;
            foreach(var candidate in menuSelectables.Where(s=>s && s!=current && s.interactable))
            {
                var rect=(RectTransform)candidate.transform; Vector2 delta=(Vector2)rect.TransformPoint(rect.rect.center)-origin;
                float forward=Vector2.Dot(delta,direction); if(forward<=.01f) continue;
                float value=delta.sqrMagnitude/forward; if(value<score) {score=value;best=candidate;}
            }
            if(!best) best=menuSelectables.Where(s=>s && s!=current && s.interactable).OrderBy(s=>Vector2.Dot((Vector2)s.transform.position,direction)).FirstOrDefault();
            if(best) EventSystem.current.SetSelectedGameObject(best.gameObject);
        }
        void SubmitFocus()
        {
            if(!EventSystem.current) return;
            var selected=EventSystem.current.currentSelectedGameObject;
            if(!selected) return;
            var button=selected.GetComponent<Button>();
            if(button && button.interactable) button.onClick.Invoke();
            else if(selected.GetComponent<InputField>()) Show("code");
        }
        public void SetGameMenu(bool open,int slot=-1)
        {
            if(!session.InGame) return;
            session.LocalMenuOpen=open;
            gameMenuSlot=open ? slot : -1;
            CombatClock.SetPaused(this,open && session.Mode!=SessionMode.Online);
            focusName=null; lastSignature=null; Refresh();
        }
        void UpdateNavigation()
        {
            if(menuInput==null) return;
            var input=menuInput.Read();
            if(debugVisible)
            {
                if(input.Cancel) debugVisible=false;
                else debugNavigation.Read(input);
                return;
            }
            if(session.InLobby) return; // Device-owned character cursors handle their own screen.
            if(!session.InGame)
            {
                CombatClock.SetPaused(this,false);
                if(EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>()?.isFocused==true)
                {
                    if(input.Cancel || input.Confirm) { EventSystem.current.currentSelectedGameObject.GetComponent<InputField>().DeactivateInputField(); }
                    return;
                }
                if(input.Cancel) { if(screen=="online") session.CancelConnection(); Show(screen=="main" ? "main" : screen=="code" ? "online" : screen=="online" ? "modes" : "main"); }
                else { if(input.Navigate!=Vector2.zero) MoveFocus(input.Navigate); if(input.Confirm) SubmitFocus(); }
                if(Keyboard.current?.tabKey.wasPressedThisFrame==true && menuSelectables.Count>0 && EventSystem.current)
                { int index=menuSelectables.FindIndex(s=>s.gameObject==EventSystem.current.currentSelectedGameObject); EventSystem.current.SetSelectedGameObject(menuSelectables[(index+1)%menuSelectables.Count].gameObject); }
                DrawFocus(); return;
            }
            var world=session.Latest; if(world==null) return;
            var own=world.players.FirstOrDefault(p=>session.IsLocalOwner(p.owner)); if(own==null) return;
            input=session.LocalInput(session.LocalMenuOpen && gameMenuSlot>=0 ? gameMenuSlot : own.slot)?.Menu.Read() ?? input;
            if(!session.LocalMenuOpen)
            {
                foreach(var player in world.players.Where(p=>p.choosing && session.IsLocalOwner(p.owner)))
                {
                    var state=session.LocalInput(player.slot)?.Menu.Read() ?? default;
                    int count=player.choices.Length+1;
                    int index=rewardFocus.TryGetValue(player.slot,out int old) ? old : 0;
                    if(state.Navigate!=Vector2.zero) index=(index+(state.Navigate.x>0 || state.Navigate.y<0 ? 1 : -1)+count)%count;
                    rewardFocus[player.slot]=index;
                    if(state.Cancel) session.ChooseLocal(player.slot,-2);
                    else if(state.Confirm) session.ChooseLocal(player.slot,index==player.choices.Length ? -2 : index);
                }
                if(world.players.Any(p=>(p.hubStation>=0 || p.choosing) && session.IsLocalOwner(p.owner))) { DrawFocus(); return; }
                foreach(var player in world.players.Where(p=>session.IsLocalOwner(p.owner)))
                {
                    var source=session.LocalInput(player.slot);
                    var request=source?.Menu.Read() ?? default;
                    if((request.Start && source?.Device is Gamepad) || request.Cancel) { SetGameMenu(true,player.slot); return; }
                }
                if(!world.gameOver && !world.completed) { DrawFocus(); return; }
            }
            else if(input.Cancel || (input.Start && session.LocalInput(gameMenuSlot>=0 ? gameMenuSlot : own.slot)?.Device is Gamepad)) { SetGameMenu(false); return; }
            if(input.Navigate!=Vector2.zero) MoveFocus(input.Navigate);
            if(input.Confirm) SubmitFocus();
            DrawFocus();
        }
        void CodeEntry()
        {
            Label("ROOM CODE: "+joinCode,new Rect(100,280,1100,70),42);
            const string letters="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            for(int i=0;i<letters.Length;i++)
            {
                char letter=letters[i];
                Button(letter.ToString(),new Rect(100+(i%6)*100,390+(i/6)*75,90,65),()=>{ if(joinCode.Length<12) {joinCode+=letter;lastSignature=null;Refresh();} });
            }
            Button("DELETE LAST",new Rect(850,390,600,80),()=>{if(joinCode.Length>0)joinCode=joinCode.Substring(0,joinCode.Length-1);lastSignature=null;Refresh();});
            Button("DONE",new Rect(850,500,600,80),()=>Show("online"),red);
            Button("BACK",new Rect(850,610,600,80),()=>Show("online"));
            Label("Arrows / WASD / stick / D-pad: move\nEnter / A: press button\nEscape / B: back",new Rect(850,735,750,160),28);
        }
        void GameMenu()
        {
            gameMenuButtons.Clear();
            Panel("Game menu shade",new Rect(0,0,1920,1080),new Color(0,0,0,.86f));
            Label(session.Mode==SessionMode.Online ? "GAME MENU  (ONLINE RUN CONTINUES)" : "PAUSED",new Rect(520,260,1000,80),44);
            gameMenuButtons.Add(Button("RESUME",new Rect(650,390,620,80),()=>SetGameMenu(false),red));
            if(session.IsAuthority && session.Latest.stage>0) gameMenuButtons.Add(Button("RETURN TO HUB",new Rect(650,500,620,80),()=>{SetGameMenu(false);session.ReturnToHub();}));
            gameMenuButtons.Add(Button("MAIN MENU",new Rect(650,610,620,80),()=>{SetGameMenu(false);screen="main";session.LeaveToMenu();}));
            Label("Arrows / WASD / stick / D-pad: move    Enter / A: confirm\nEscape / B / Start: resume",new Rect(450,760,1100,90),26);
        }
    }
}
