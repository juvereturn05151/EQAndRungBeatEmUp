#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeatEmUp
{
    // Opt-in integration checks exercise real actions, UI, persistent session and gameplay spawning.
    public sealed class CharacterSelectValidationRun : MonoBehaviour
    {
        public static bool Finished, Passed;
        readonly List<string> results = new List<string>();
        readonly List<InputDevice> devices = new List<InputDevice>();
        MultiplayerSession session;
        Keyboard keyboard;
        Gamepad pad;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        void Check(bool condition, string label) { results.Add((condition ? "PASS: " : "FAIL: ") + label); Passed &= condition; }
        IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null; yield return null;
        }
        IEnumerator PadPress(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button)); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState()); yield return null; yield return null;
        }
        IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while(!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Check(condition(),"Scene load completed within timeout");
        }
        string Focus => EventSystem.current?.currentSelectedGameObject?.name;
        IEnumerator ValidateMenu()
        {
            yield return null; yield return null;
            Check(Focus=="NEW GAME","Main menu starts with visible New Game focus");
            yield return KeyPress(Key.DownArrow);
            Check(Focus=="HOW TO PLAY","Keyboard arrows navigate buttons");
            yield return KeyPress(Key.Enter);
            Check(Focus=="BACK","Enter opens help without a mouse (focus: "+Focus+")");
            Check(FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t=>t.text.Contains("Interact: L1 / LB")),"How to Play displays L1 / LB for controller interaction");
            yield return KeyPress(Key.Escape);
            Check(Focus=="NEW GAME","Escape returns to the main menu");
            yield return PadPress(GamepadButton.DpadDown); if(Focus=="CONTINUE GAME")yield return PadPress(GamepadButton.DpadDown); yield return PadPress(GamepadButton.DpadDown);
            Check(Focus=="OPTIONS","Controller D-pad navigates the main menu");
            yield return PadPress(GamepadButton.South);
            Check(Focus=="−","Controller A opens options");
            float volume=AudioListener.volume;
            yield return PadPress(GamepadButton.South);
            Check(Focus=="−" && AudioListener.volume<=volume,"Focus survives options rebuilding after activation");
            AudioListener.volume=volume;
            yield return PadPress(GamepadButton.East);
            Check(Focus=="NEW GAME","Controller B returns to the main menu");
            yield return KeyPress(Key.Enter); yield return KeyPress(Key.DownArrow); yield return KeyPress(Key.DownArrow); yield return KeyPress(Key.Enter);
            Check(Focus=="CREATE LOBBY","Online page navigable without connecting");
            // Select the keypad through a real button, then exercise all-device action navigation.
            var keypad=FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.name=="ENTER CODE WITH BUTTONS");
            Check(keypad,"Controller room-code keypad entry available");
            if(keypad) { EventSystem.current.SetSelectedGameObject(keypad.gameObject); yield return PadPress(GamepadButton.South); }
            Check(Focus=="A","Room-code keypad starts on A");
            yield return PadPress(GamepadButton.South);
            Check(Focus=="A" && FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t=>t.text=="ROOM CODE: A"),"A enters a room-code character and retains focus");
            InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=Vector2.right}); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState()); yield return null; yield return null;
            Check(Focus=="B","Analog stick navigates keypad");
            yield return PadPress(GamepadButton.East); yield return PadPress(GamepadButton.East); yield return PadPress(GamepadButton.East);
            Check(Focus=="NEW GAME","Back traverses keypad, online and modes to main");
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject); Finished=false; Passed=true;
            background=InputSystem.settings.backgroundBehavior; editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>(); devices.Add(keyboard);
            pad=InputSystem.AddDevice<Gamepad>(); devices.Add(pad);
            session=MultiplayerSession.Active;
            Check(session && session.catalog.characters.Length>=2,"Existing persistent session and two real character prefabs available");
            if(!session || session.catalog.characters.Length<2) { Finish(); yield break; }
            yield return ValidateMenu();
            // Controller activates New Game and Offline Play with a keyboard present.
            yield return PadPress(GamepadButton.South); yield return PadPress(GamepadButton.South);
            yield return null; yield return null;
            var solo=FindFirstObjectByType<CharacterSelectManager>();
            Check(solo && session.Mode==SessionMode.Local && session.Lobby.slots.Count==1 && session.Lobby.slots[0].device==pad,
                "Controller opening OFFLINE PLAY owns P1 even with a keyboard connected");
            Check(!session.Lobby.slots[0].ready,"Opening controller press does not also confirm the character");
            // Preserve coverage of the internal single-slot device handoff without a menu entry.
            session.LeaveToMenu(); yield return null;
            session.PreferredLocalDevice=pad; session.BeginLocal(true); yield return null; yield return null;
            // Story restart is covered by StoryMenuValidation; these checks exercise selection and combat.
            session.NewGameRequested=false;
            int soloCharacter=session.Lobby.slots[0].character;
            yield return PadPress(GamepadButton.DpadRight);
            Check(session.Lobby.slots[0].character!=soloCharacter,"Single-player controller D-pad selects a character");
            soloCharacter=session.Lobby.slots[0].character;
            InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=Vector2.left}); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState()); yield return null; yield return null;
            Check(session.Lobby.slots[0].character!=soloCharacter,"Single-player controller stick selects a character");
            yield return PadPress(GamepadButton.South); Check(session.Lobby.slots[0].ready,"Controller A confirms single-player selection");
            yield return PadPress(GamepadButton.East); Check(!session.Lobby.slots[0].ready,"Controller B unlocks single-player selection");
            session.UseSingleSelectionDevice(keyboard); yield return null; yield return null;
            yield return PadPress(GamepadButton.DpadRight);
            Check(session.Lobby.slots.Count==1 && session.Lobby.slots[0].device==pad,"Unpaired controller navigation takes over P1 without creating P2");
            session.UseSingleSelectionDevice(keyboard); yield return null; yield return null;
            yield return PadPress(GamepadButton.South);
            Check(session.Lobby.slots.Count==1 && session.Lobby.slots[0].device==pad && !session.Lobby.slots[0].ready,
                "Controller join claims a keyboard-owned single slot without confirming on the same press");
            yield return PadPress(GamepadButton.South);
            Check(session.Lobby.slots[0].ready && session.CanStart,"Claimed controller can confirm and enable Start");
            yield return PadPress(GamepadButton.Start);
            yield return WaitFor(()=>session.InGame && PlayerRoster.Players.Count()==1);
            Check(session.LocalInput(0)?.Device==pad,"Controller Start launches the selected solo character with the same paired controller");
            session.LeaveToMenu(); yield return WaitFor(()=>!session.InGame && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu");
            // The remainder intentionally exercises keyboard P1 plus controller P2.
            session.PreferredLocalDevice=keyboard;
            session.BeginLocal(); yield return null; yield return null;
            var ui=FindFirstObjectByType<CharacterSelectManager>();
            Check(ui,"Open Character Select from existing local mode");
            if(!ui) { Finish(); yield break; }
            Canvas.ForceUpdateCanvases();
            var previewCorners=new Vector3[4]; ui.LargePreview.rectTransform.GetWorldCorners(previewCorners);
            Check(ui.transform.lossyScale.x>0 && ui.LargePreview.rectTransform.rect.width>0 && previewCorners[2].x>previewCorners[0].x && previewCorners[2].x<=Screen.width,"Canvas preview has visible size and lies inside the game screen");
            Check(session.Lobby.slots.Count==1 && !session.Lobby.slots[0].ready,"P1 joins selecting; not auto-confirmed");
            int original=session.Lobby.slots[0].character;
            yield return KeyPress(Key.RightArrow);
            int moved=session.Lobby.slots[0].character;
            Check(moved!=original,"P1 navigation action changes character");
            var selected=session.catalog.SelectionAt(moved);
            Check(ui.CharacterName.text==selected.DisplayName && ui.ArchetypeText.text==selected.Archetype && ui.LargePreview.sprite==selected.Preview,"Preview, name and archetype update from definition");
            Check(ui.PowerBar.Segments.Count(s=>s.color==ui.PowerBar.FillColor)==selected.Power,"Stat segments update immediately");
            Check(ui.RosterContainer.GetComponentsInChildren<CharacterPortraitUI>().Any(p=>p.PlayerMarkers[0].enabled),"P1 portrait highlight and marker visible");
            yield return KeyPress(Key.Enter);
            Check(session.Lobby.slots[0].ready && ui.Player1Slot.StateLabel.text=="READY","Confirm action shows READY");
            yield return KeyPress(Key.LeftArrow);
            Check(session.Lobby.slots[0].character==moved,"Confirmed selection ignores navigation");
            yield return KeyPress(Key.Escape);
            Check(!session.Lobby.slots[0].ready,"Cancel unlocks selection");
            yield return PadPress(GamepadButton.South);
            Check(session.Lobby.slots.Count==2 && !session.Lobby.slots[1].ready,"P2 Join action adds separate selecting cursor without also confirming");
            int p1=session.Lobby.slots[0].character;
            int p2=session.Lobby.slots[1].character;
            yield return PadPress(GamepadButton.DpadRight);
            Check(session.Lobby.slots[1].character!=p2 && session.Lobby.slots[0].character==p1,"P2 gamepad navigation is independent of P1 keyboard");
            session.ConfigureSelection(2,true,session.catalog.gameplayScene);
            session.SelectCharacter(0,0); session.SelectCharacter(1,0);
            session.Ready(0); session.Ready(1);
            Check(session.CanStart,"Duplicate characters allowed: both can confirm");
            session.Ready(0); session.Ready(1);
            session.SelectCharacter(1,1); session.ConfigureSelection(2,false,session.catalog.gameplayScene);
            session.SelectCharacter(1,0);
            Check(session.Lobby.slots[1].character==1,"Duplicate characters disabled: occupied selection rejected");
            var locked=session.catalog.SelectionAt(1); bool unlocked=locked.IsUnlocked; locked.IsUnlocked=false;
            session.Ready(1); Check(!session.Lobby.slots[1].ready,"Locked character cannot confirm"); locked.IsUnlocked=unlocked;
            session.Ready(0); Check(!session.CanStart,"Start unavailable until every joined player is ready");
            session.Ready(1); Check(session.CanStart && ui.StartPrompt.interactable,"Start available after both required players confirm");
            for(int i=0;i<2;i++) { var extra=InputSystem.AddDevice<Gamepad>(); devices.Add(extra); session.JoinDevice(extra); }
            Check(session.Lobby.slots.Count==4,"Four local players supported");
            Check(session.Lobby.slots.Where(s=>s.character>=0).Select(s=>s.character).Distinct().Count()==session.Lobby.slots.Count(s=>s.character>=0),"Joining with duplicates disabled never assigns an occupied fighter");
            Check(!session.CanStart,"Unconfirmed third/fourth players block Start");
            session.ConfigureSelection(2,true,session.catalog.gameplayScene);
            session.Ready(2); session.Ready(3);
            Check(session.CanStart && ui.Player4Slot.StateLabel.text=="READY","All four slots render and become ready");
            session.Ready(0); ui.Select(0,0); ui.Confirm(0);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Documentation/CharacterSelectPreview.png")); yield return null; yield return null;
            session.RemoveSelectionDevice(devices[2]); session.RemoveSelectionDevice(devices[3]);
            var expected=session.Lobby.slots.Select(s=>session.catalog.SelectionAt(s.character).Prefab).ToArray();
            var wireState=JsonUtility.FromJson<LobbyState>(JsonUtility.ToJson(session.Lobby));
            Check(wireState.requiredPlayerCount==2 && wireState.allowDuplicateCharacters && wireState.slots.All(s=>s.ready) && wireState.slots.Select(s=>s.character).SequenceEqual(session.Lobby.slots.Select(s=>s.character)),"Network lobby payload preserves selections, readiness and party rules");
            yield return KeyPress(Key.Space); yield return WaitFor(()=>session.InGame && session.Flow && PlayerRoster.Players.Count()==2);
            var players=PlayerRoster.Players.OrderBy(p=>p.slot).ToArray();
            Check(players.Length==2,"Gameplay spawns two distinct player instances");
            for(int i=0;i<players.Length;i++)
            {
                var slot=session.Lobby.slots.Find(s=>s.slot==players[i].slot);
                Check(players[i].GetComponent<PlayerCharacterLoadout>().character==session.catalog.CharacterAt(slot.character),"P"+(i+1)+" uses selected gameplay loadout");
                Check(players[i].SpawnedPrefab==expected[i],"P"+(i+1)+" instantiated the exact selected prefab after scene loading");
                Check(PlayerCharacterSelection.From(slot,session.catalog).CharacterId==session.catalog.CharacterAt(slot.character).characterId,"P"+(i+1)+" stable character ID survives scene loading");
            }
            Check(expected.Length==2 && expected[0]!=expected[1],"P1/P2 selected distinct actual prefabs");
            var defense=players[0].GetComponent<ComboController>();
            var clock=FindFirstObjectByType<CombatClock>();
            Vector2 spawn=players[0].transform.position;
            foreach(var direction in new[]{Vector2.left,Vector2.right,Vector2.up,Vector2.down})
            {
                players[0].Health.Restore(); players[0].Motor.ResetForStage(spawn);
                // Authority must enforce priority even if a command contains both defense requests.
                session.ApplyCommand(players[0].slot,new PlayerCommand {move=direction,guard=true,buttons=(int)PlayerButtons.Dodge});
                Check(defense.State==CombatState.Dodge && !defense.GuardHeld && defense.ParryRearmRemaining==0,"Authority prioritizes directional Dodge over Guard: "+direction);
                for(int frame=0;frame<4;frame++) clock.StepFrame();
                Check(Vector2.Dot(players[0].Motor.DefenseVelocity.normalized,direction)>.99f,"Authority uses the command's current movement direction: "+direction);
            }
            players[0].Health.Restore(); players[0].Motor.ResetForStage(spawn);
            session.ApplyCommand(players[0].slot,new PlayerCommand {guard=true});
            Check(defense.GuardActive,"Authority enters Guard for stationary shared-button input");
            session.ApplyCommand(players[0].slot,new PlayerCommand());
            var menu=FindFirstObjectByType<MultiplayerMenu>();
            yield return PadPress(GamepadButton.East);
            Check(!menu.MenuOpen && !CombatClock.IsPaused,"Controller B during gameplay does not open the pause menu or pause combat");
            yield return KeyPress(Key.Escape);
            Check(menu.MenuOpen && CombatClock.IsPaused && Focus=="RESUME","Escape opens gameplay menu and pauses local combat");
            yield return PadPress(GamepadButton.South);
            Check(menu.MenuOpen,"P2 confirm cannot activate P1's gameplay menu");
            yield return KeyPress(Key.Enter);
            Check(!menu.MenuOpen && !CombatClock.IsPaused,"Enter resumes local play");
            yield return KeyPress(Key.Space);
            Check(!menu.MenuOpen && !players[0].Motor.IsGrounded,"Space remains the gameplay jump button");
            players[0].Motor.ResetForStage(players[0].transform.position);
            yield return KeyPress(Key.Escape);
            Check(menu.MenuOpen,"Escape opens the gameplay menu again");
            yield return KeyPress(Key.Escape);
            Check(!menu.MenuOpen,"Escape closes gameplay menu");
            yield return PadPress(GamepadButton.Start);
            Check(menu.MenuOpen && Focus=="RESUME","P2 can open the gameplay menu with controller Start");
            yield return PadPress(GamepadButton.East);
            Check(!menu.MenuOpen,"The controller that opened the gameplay menu can close it with B");
            var hub=session.Flow.GetComponent<PlayerHubController>();
            var meta=players[1].GetComponent<MetaProgress>();
            players[1].Motor.ResetForStage(hub.definition.stations[2]);
            meta.OpenStation=2;
            yield return new WaitForSecondsRealtime(.2f);
            yield return PadPress(GamepadButton.DpadDown);
            yield return PadPress(GamepadButton.South);
            Check(meta.OpenStation==-1,"P2 navigates to hub Close and confirms with A");
            players[1].Motor.ResetForStage(hub.definition.stations[0]);
            yield return PadPress(GamepadButton.Select);
            Check(meta.OpenStation==-1,"Select no longer interacts with a nearby hub station");
            yield return PadPress(GamepadButton.LeftShoulder);
            Check(meta.OpenStation==0 && !menu.MenuOpen,"L1 / LB opens the paired player's hub station without pausing");
            yield return PadPress(GamepadButton.LeftShoulder);
            Check(meta.OpenStation==-1,"A new L1 / LB press closes the hub station");
            yield return PadPress(GamepadButton.LeftShoulder);
            yield return new WaitForSecondsRealtime(.2f);
            yield return PadPress(GamepadButton.East);
            Check(meta.OpenStation==-1 && !menu.MenuOpen,"Controller B closes a hub panel without opening the gameplay menu");
            var rewards=session.Flow.CoopRewards;
            bool offered=rewards.Begin(null); Check(offered,"Co-op blessing reward available for navigation checks");
            if(offered)
            {
                foreach(var actor in players) { actor.Motor.ResetForStage(rewards.Chapel.position); rewards.Interact(actor); }
                yield return new WaitForSecondsRealtime(.2f);
                var first=rewards.selections.Find(s=>s.player==players[0]);
                var second=rewards.selections.Find(s=>s.player==players[1]);
                Check(first.opened && second.opened,"Both players open independent blessing menus");
                yield return PadPress(GamepadButton.DpadDown);
                Check(!first.done && !second.done,"D-pad changes focus without immediately purchasing a blessing");
                yield return PadPress(GamepadButton.East);
                Check(first.opened && !second.opened && !second.done,"P2 cancel closes only P2's blessing menu and retains the reward");
                yield return PadPress(GamepadButton.LeftShoulder); yield return new WaitForSecondsRealtime(.2f);
                Check(second.opened && !second.done,"L1 / LB reopens the paired player's blessing menu");
                var expectedBlessing=second.choices[Mathf.Min(1,second.choices.Count-1)];
                yield return PadPress(GamepadButton.South);
                Check(second.done && !first.done && players[1].GetComponent<RunBuildState>().Stacks(expectedBlessing)>0,"P2 A selects the focused blessing without changing P1");
                rewards.Cancel();
            }
            // Exercise the standalone IMGUI menus with the persistent session's updates suspended.
            menu.enabled=false; session.enabled=false;
            var upgrades=session.Flow.RunUpgrades;
            upgrades.enabled=true; yield return null; yield return null;
            Check(upgrades.Offer(),"Standalone upgrade-card menu opens");
            if(upgrades.IsChoosing)
            {
                var chosen=upgrades.Choices[Mathf.Min(1,upgrades.Choices.Count-1)];
                int stacks=upgrades.Build.Stacks(chosen);
                yield return PadPress(GamepadButton.DpadRight);
                yield return PadPress(GamepadButton.South);
                Check(!upgrades.IsChoosing && upgrades.Build.Stacks(chosen)==stacks+1,"Standalone controller navigation chooses the focused upgrade card");
            }
            yield return KeyPress(Key.Tab);
            Check(upgrades.MenuOpen,"Keyboard Tab opens the standalone current-build menu");
            yield return KeyPress(Key.Escape);
            Check(!upgrades.MenuOpen,"Keyboard Escape closes the standalone current-build menu");
            yield return PadPress(GamepadButton.Start);
            Check(upgrades.MenuOpen,"Controller Start opens the standalone current-build menu");
            yield return PadPress(GamepadButton.East);
            Check(!upgrades.MenuOpen,"Controller B closes the standalone current-build menu");
            upgrades.enabled=false;
            session.enabled=true; menu.enabled=true;
            session.LeaveToMenu(); yield return WaitFor(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu");
            session.PreferredLocalDevice=keyboard;
            session.BeginLocal(); yield return null; yield return null;
            Check(session.Lobby.slots.Count==1 && !session.Lobby.slots[0].ready,"Re-entry clears previous party and ready state");
            original=session.Lobby.slots[0].character;
            yield return KeyPress(Key.RightArrow);
            Check(session.Lobby.slots[0].character!=original,"Re-entry handles navigation once; no stale action listeners");
            yield return KeyPress(Key.Escape);
            Check(!session.InLobby && !session.InGame,"Cancel from an unconfirmed P1 returns to the menu");
            session.BeginLocal(true); yield return null;
            Check(session.InLobby && !session.InGame && session.Lobby.requiredPlayerCount==1 && !session.Lobby.slots[0].ready,"Single player also enters selection before gameplay");
            session.LeaveToMenu(); yield return null;
            Finish();
        }
        void Finish()
        {
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/CharacterSelectValidationResults.txt",results.Concat(new[]{Passed ? "RESULT: PASS" : "RESULT: FAIL"}));
            Finished=true;
        }
        void OnDestroy()
        {
            foreach(var device in devices) if(device.added) InputSystem.RemoveDevice(device);
            InputSystem.settings.backgroundBehavior=background; InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
        }
    }
}
#endif
