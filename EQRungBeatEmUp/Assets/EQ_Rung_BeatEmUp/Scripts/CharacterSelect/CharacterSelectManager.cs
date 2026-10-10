using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BeatEmUp
{
    public sealed class CharacterSelectManager : MonoBehaviour
    {
        public CharacterDefinition[] Characters;
        [Range(1,4)] public int RequiredPlayerCount = 1;
        public bool AllowDuplicateCharacters = true;
        public string GameplayScene = "HauntedHouse";
        public Image LargePreview;
        public Text CharacterName, ArchetypeText, DescriptionText, RoomText;
        public CharacterStatBar PowerBar, SpeedBar, DefenseBar, TechniqueBar;
        public RectTransform RosterContainer;
        public CharacterPortraitUI PortraitPrefab;
        public CharacterSelectPlayerSlot Player1Slot, Player2Slot, Player3Slot, Player4Slot;
        public Button ConfirmPrompt, CancelPrompt, StartPrompt;
        public AudioClip NavigateSFX, ConfirmSFX, CancelSFX;
        public AudioSource Audio;
        public Image Background;
        public Sprite BackgroundSprite;
        MultiplayerSession session;
        InputActionAsset joinActions;
        readonly Dictionary<int,CharacterSelectPlayerCursor> cursors = new Dictionary<int,CharacterSelectPlayerCursor>();
        readonly List<CharacterPortraitUI> portraits = new List<CharacterPortraitUI>();
        readonly Dictionary<int,int> lockedHover = new Dictionary<int,int>();
        readonly Dictionary<int,bool> lastReady = new Dictionary<int,bool>();
        int focusPlayer, previewIndex = -2;
        int OwnedPlayer => session.Lobby.slots.FirstOrDefault(s => session.IsLocalOwner(s.owner))?.slot ?? 0;
        float animationAt;
        int animationFrame;
        public void Bind(MultiplayerSession existing)
        {
            // Nested under the existing menu canvas: clear any editor-driven standalone canvas transform.
            var rect = (RectTransform)transform;
            rect.localScale = Vector3.one; rect.anchorMin = rect.anchorMax = new Vector2(0,1);
            rect.pivot = new Vector2(0,1); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1920,1080);
            session = existing;
            focusPlayer = OwnedPlayer;
            session.CharacterSelectInputActive = true;
            if(Characters == null || Characters.Length == 0) Characters = session.catalog.selectionCharacters;
            session.ConfigureSelection(RequiredPlayerCount, AllowDuplicateCharacters, GameplayScene);
            if(BackgroundSprite) Background.sprite = BackgroundSprite;
            if(session.Mode == SessionMode.Local) ConfigureCoopLayout();
            var grid = RosterContainer.GetComponent<GridLayoutGroup>();
            grid.constraintCount = Mathf.Max(1, Mathf.Min(6, Mathf.CeilToInt(Mathf.Sqrt(Characters.Length * 1.8f))));
            for(int i = 0; i < Characters.Length; i++)
            {
                var portrait = Instantiate(PortraitPrefab, RosterContainer); portrait.SetCharacter(Characters[i]);
                int index = i;
                portrait.GetComponent<Button>().onClick.AddListener(() => Select(OwnedPlayer,index));
                portraits.Add(portrait);
            }
            ConfirmPrompt.onClick.AddListener(() => Confirm(OwnedPlayer));
            CancelPrompt.onClick.AddListener(() => Cancel(OwnedPlayer));
            StartPrompt.onClick.AddListener(() => StartGame(OwnedPlayer));
            session.Changed += Render;
            joinActions = Instantiate(session.catalog.playerPrefab.GetComponent<PlayerInput>().actions);
            var map = joinActions.FindActionMap("CharacterSelect",true);
            var join = map.FindAction("Join",true); join.performed += Join;
            join.Enable();
            // An unpaired controller may claim the single-player slot using its stick/D-pad.
            var navigate=map.FindAction("Navigate",true); navigate.performed+=ClaimSingleDevice; navigate.Enable();
            InputSystem.onDeviceChange += DeviceChanged;
            Render();
        }
        void Join(InputAction.CallbackContext context)
        {
            if(!session || !session.InLobby || session.Mode == SessionMode.Online) return;
            session.JoinDevice(context.control.device);
        }
        void ConfigureCoopLayout()
        {
            // The local party uses four independent cards instead of one shared preview.
            var previewElements = new Component[] { LargePreview, CharacterName, ArchetypeText, DescriptionText,
                PowerBar, SpeedBar, DefenseBar, TechniqueBar };
            foreach(var element in previewElements) element.gameObject.SetActive(false);
            string[] decorations = { "Preview shadow", "Preview corners", "Name slash 0", "Name slash 1",
                "Name slash 2", "Name slash 3", "POWER", "SPEED", "DEFENSE", "TECHNIQUE" };
            foreach(string name in decorations)
            {
                var element = transform.Find(name); if(element) element.gameObject.SetActive(false);
            }
            MoveElement("GHOST FAIR", new Rect(60, 25, 600, 52));
            MoveElement("CHOOSE YOUR FIGHTER", new Rect(60, 80, 1500, 78));
            MoveElement("THE SCHOOL GATES ARE OPEN.", new Rect(62, 155, 900, 32));
            CharacterSelectPlayerSlot.SetRect(RoomText.rectTransform, new Rect(60, 195, 1805, 35));
            MoveElement("Roster shadow", new Rect(0, 795, 1920, 285));
            CharacterSelectPlayerSlot.SetRect((RectTransform)RosterContainer.parent, new Rect(60, 805, 1805, 156));
            CharacterSelectPlayerSlot.SetRect(RosterContainer, new Rect(10, 0, 1785, 156));
            var slots = new[] { Player1Slot, Player2Slot, Player3Slot, Player4Slot };
            for(int i = 0; i < slots.Length; i++) slots[i].ConfigureCoopCard(i);
            MoveElement("ENTER / A: JOIN + CONFIRM     ESC / B: CANCEL", new Rect(60, 965, 1805, 34));
            CharacterSelectPlayerSlot.SetRect(ConfirmPrompt.GetComponent<RectTransform>(), new Rect(60, 1012, 365, 52));
            CharacterSelectPlayerSlot.SetRect(CancelPrompt.GetComponent<RectTransform>(), new Rect(440, 1012, 300, 52));
            CharacterSelectPlayerSlot.SetRect(StartPrompt.GetComponent<RectTransform>(), new Rect(1390, 1012, 475, 52));
        }
        void MoveElement(string name, Rect area)
        {
            // Some UI object names contain '/', which Transform.Find treats as a hierarchy path.
            var element = transform.Cast<Transform>().FirstOrDefault(child => child.name == name);
            if(element) CharacterSelectPlayerSlot.SetRect((RectTransform)element, area);
        }
        void ClaimSingleDevice(InputAction.CallbackContext context)
        {
            if(session && context.ReadValue<Vector2>().magnitude>=.55f)
                session.UseSingleSelectionDevice(context.control.device);
        }
        void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if(change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed) session?.RemoveSelectionDevice(device);
        }
        int CatalogIndex(int rosterIndex) => rosterIndex >= 0 && rosterIndex < Characters.Length && Characters[rosterIndex]
            ? System.Array.IndexOf(session.catalog.characters, Characters[rosterIndex].GameplayCharacter) : -1;
        int RosterIndex(int catalogIndex) => System.Array.FindIndex(Characters, c => c && c.GameplayCharacter == session.catalog.CharacterAt(catalogIndex));
        public void Select(int playerIndex, int rosterIndex)
        {
            var slot = session.Lobby.slots.Find(s => s.slot == playerIndex && session.IsLocalOwner(s.owner));
            if(slot == null || slot.ready) return;
            focusPlayer = playerIndex;
            int index = CatalogIndex(rosterIndex);
            if(index < 0) { lockedHover[playerIndex] = rosterIndex; Render(); return; }
            lockedHover.Remove(playerIndex);
            session.SelectCharacter(playerIndex,index);
            portraits[rosterIndex].Punch(false); Play(NavigateSFX);
            Canvas.ForceUpdateCanvases();
            var viewport = RosterContainer.parent.GetComponent<RectTransform>();
            var grid = RosterContainer.GetComponent<GridLayoutGroup>();
            float rowTop = (rosterIndex / grid.constraintCount) * (grid.cellSize.y + grid.spacing.y);
            var position = RosterContainer.anchoredPosition;
            if(rowTop < position.y) position.y = rowTop;
            else if(rowTop + grid.cellSize.y > position.y + viewport.rect.height) position.y = rowTop + grid.cellSize.y - viewport.rect.height;
            position.y = Mathf.Clamp(position.y,0,Mathf.Max(0,RosterContainer.rect.height-viewport.rect.height));
            RosterContainer.anchoredPosition = position;
        }
        public void Navigate(int playerIndex, Vector2 direction)
        {
            var slot = session.Lobby.slots.Find(s => s.slot == playerIndex && session.IsLocalOwner(s.owner));
            if(slot == null || slot.ready || Characters.Length == 0) return;
            int current = lockedHover.TryGetValue(playerIndex,out int hovered) ? hovered : Mathf.Max(0,RosterIndex(slot.character));
            int columns = RosterContainer.GetComponent<GridLayoutGroup>().constraintCount;
            int next;
            if(direction.x != 0) next = (current + (direction.x > 0 ? 1 : -1) + Characters.Length) % Characters.Length;
            else
            {
                int rows = Mathf.CeilToInt(Characters.Length / (float)columns);
                int row = (current / columns + (direction.y > 0 ? -1 : 1) + rows) % rows;
                next = Mathf.Min(row * columns + current % columns, Characters.Length - 1);
            }
            if(!session.Lobby.allowDuplicateCharacters)
            {
                for(int attempts=0; attempts<Characters.Length; attempts++)
                {
                    int candidate=CatalogIndex(next);
                    if(candidate<0 || !session.Lobby.slots.Any(s=>s.slot!=playerIndex && s.character==candidate)) break;
                    next=(next + (direction.x<0 || direction.y>0 ? -1 : 1) + Characters.Length) % Characters.Length;
                }
            }
            Select(playerIndex,next);
        }
        public void Confirm(int index)
        {
            var slot = session.Lobby.slots.Find(s => s.slot == index && session.IsLocalOwner(s.owner));
            if(slot == null || slot.ready || lockedHover.ContainsKey(index)) return;
            focusPlayer = index; session.Ready(index);
            if(slot.ready)
            {
                int roster = RosterIndex(slot.character); if(roster >= 0) portraits[roster].Punch(true);
                Play(ConfirmSFX); var character = session.catalog.SelectionAt(slot.character); if(character) Play(character.ConfirmVoice);
            }
        }
        public void Cancel(int index)
        {
            var slot = session.Lobby.slots.Find(s => s.slot == index && session.IsLocalOwner(s.owner));
            if(slot == null) return;
            Play(CancelSFX);
            if(slot.ready) session.Ready(index);
            else if(index == 0 || session.Mode == SessionMode.Online) session.LeaveToMenu();
            else session.RemoveSelectionDevice(slot.device);
        }
        public void StartGame(int index)
        {
            if(session.Lobby.slots.Any(s => s.slot == index && session.IsLocalOwner(s.owner)) && session.CanStart) session.StartGame();
        }
        public void Render()
        {
            if(!session || !session.InLobby) return;
            foreach(int index in lockedHover.Keys.ToArray()) if(!session.Lobby.slots.Any(s=>s.slot==index)) lockedHover.Remove(index);
            foreach(var slot in session.Lobby.slots.Where(s => session.IsLocalOwner(s.owner)))
                if(slot.device == null) slot.device = session.PreferredOnlineDevice ?? (InputDevice)Keyboard.current ?? Gamepad.current;
            foreach(var pair in cursors.ToArray())
            {
                if(!session.Lobby.slots.Any(s => s.slot == pair.Key && s.device == pair.Value.Device)) { pair.Value.Dispose(); cursors.Remove(pair.Key); }
            }
            foreach(var slot in session.Lobby.slots.Where(s => session.IsLocalOwner(s.owner)))
            {
                var device = slot.device ?? session.PreferredOnlineDevice ?? (InputDevice)Keyboard.current ?? Gamepad.current;
                if(device != null && !cursors.ContainsKey(slot.slot)) cursors.Add(slot.slot,new CharacterSelectPlayerCursor(session.catalog.playerPrefab.GetComponent<PlayerInput>().actions,device,slot.slot,this));
            }
            var slots = new[] { Player1Slot, Player2Slot, Player3Slot, Player4Slot };
            for(int i = 0; i < slots.Length; i++)
            {
                bool visible = session.Mode != SessionMode.Single || i == 0;
                slots[i].gameObject.SetActive(visible);
                if(visible)
                {
                    var slot = session.Lobby.slots.Find(s => s.slot == i);
                    slots[i].Render(i,slot);
                    if(session.Mode == SessionMode.Local)
                    {
                        int roster = slot == null ? -1 : lockedHover.TryGetValue(i,out int hover) ? hover : RosterIndex(slot.character);
                        slots[i].RenderCharacter(slot, roster >= 0 && roster < Characters.Length ? Characters[roster] : null);
                    }
                }
            }
            var displaySlots = session.Lobby.slots.Select(s => new LobbySlot { slot=s.slot, ready=s.ready,
                character=lockedHover.TryGetValue(s.slot,out int hover) ? hover : RosterIndex(s.character) }).ToList();
            for(int i = 0; i < portraits.Count; i++) portraits[i].Render(displaySlots,i);
            foreach(var slot in session.Lobby.slots)
            {
                if(session.Mode==SessionMode.Online && session.IsLocalOwner(slot.owner) && slot.ready && lastReady.TryGetValue(slot.slot,out bool wasReady) && !wasReady)
                {
                    int roster=RosterIndex(slot.character); if(roster>=0) portraits[roster].Punch(true);
                    Play(ConfirmSFX); var definition=session.catalog.SelectionAt(slot.character); if(definition) Play(definition.ConfirmVoice);
                }
                lastReady[slot.slot]=slot.ready;
            }
            var focused = session.Lobby.slots.Find(s => s.slot == focusPlayer) ?? session.Lobby.slots.FirstOrDefault();
            ShowPreview(focused == null ? 0 : lockedHover.TryGetValue(focused.slot,out int preview) ? preview : RosterIndex(focused.character));
            StartPrompt.interactable = session.CanStart;
            StartPrompt.GetComponentInChildren<Text>().text = session.IsAuthority ? session.CanStart ? "START  /  SPACE" : "WAITING FOR READY" : "WAITING FOR HOST";
            var p1 = session.Lobby.slots.Find(s => s.slot == OwnedPlayer && session.IsLocalOwner(s.owner));
            ConfirmPrompt.interactable = p1 != null && !p1.ready && !lockedHover.ContainsKey(p1.slot) && session.CanConfirm(p1);
            RoomText.text = session.Mode == SessionMode.Online ? "ONLINE  /  ROOM " + session.Lobby.code : "OFFLINE  /  " + session.Lobby.slots.Count + " PLAYERS  /  NEED " + session.Lobby.requiredPlayerCount;
        }
        void ShowPreview(int index)
        {
            if(index < 0 || index >= Characters.Length) return;
            var character = Characters[index]; if(!character) return;
            bool changed = previewIndex != index; previewIndex = index;
            LargePreview.sprite = character.Preview; LargePreview.enabled = LargePreview.sprite;
            CharacterName.text = character.DisplayName; ArchetypeText.text = character.Archetype;
            DescriptionText.text = character.IsUnlocked ? character.Description : "LOCKED";
            PowerBar.SetValue(character.Power); SpeedBar.SetValue(character.Speed); DefenseBar.SetValue(character.Defense); TechniqueBar.SetValue(character.Technique);
            if(changed) { animationFrame = 0; animationAt = Time.unscaledTime; Play(character.CharacterSelectVoice); }
        }
        void Play(AudioClip clip) { if(Audio && clip) Audio.PlayOneShot(clip); }
        void Update()
        {
            foreach(var cursor in cursors.Values.ToArray()) cursor.Tick();
            if(previewIndex < 0 || previewIndex >= Characters.Length) return;
            var character = Characters[previewIndex];
            if(character && character.CharacterSelectAnimation != null && character.CharacterSelectAnimation.Length > 0 && Time.unscaledTime >= animationAt)
            { LargePreview.sprite = character.CharacterSelectAnimation[animationFrame++ % character.CharacterSelectAnimation.Length]; animationAt = Time.unscaledTime + .13f; }
        }
        void OnDisable()
        {
            if(session) { session.Changed -= Render; session.CharacterSelectInputActive = false; }
            InputSystem.onDeviceChange -= DeviceChanged;
            foreach(var cursor in cursors.Values) cursor.Dispose(); cursors.Clear();
            if(joinActions) { joinActions.Disable(); Destroy(joinActions); joinActions = null; }
        }
    }
}
