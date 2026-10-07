# UI navigation

All player-facing menus support keyboard and controller buttons. Mouse input remains available.

| Action | Keyboard | Controller |
| --- | --- | --- |
| Move focus | Arrows or WASD; Tab on front menus | D-pad or left stick |
| Confirm focused button | Enter | A / Cross |
| Back or close | Escape | B / Circle |
| Open gameplay menu / resume | Escape | Start / Options, or B |
| Open a hub station / reward chapel | E | Select / View |
| Current build in standalone stage scenes | Tab | Start / Options |

The front menus display a gold selection frame, preserve focus when options refresh, and skip disabled controls. Online room codes can be entered using the on-screen letter/number keypad.

Local gameplay pauses while the game menu is open. Online gameplay continues, with the local player's combat input suppressed. Any local player can open the gameplay menu; the device that opened it controls it. Hub and blessing menus use each player's paired device. Selecting another blessing with the D-pad no longer immediately acquires it: press Confirm to choose. Back closes that player's blessing menu and leaves the reward available at the chapel.

Standalone upgrade cards support the same navigation controls. The current-build window scrolls with Up/Down and closes with Confirm, Back, Tab, or Start. Standalone defeat/completion accepts Enter or A to retry/return to the hub.

Development panels opened with F8 (stages), F9 (upgrades), or F10 (multiplayer) also support directional button focus, Confirm, and Back. Direct-IP text entry in the development panel still uses a keyboard.

Validation: **Beat Em Up → Character Select → Validate selection and spawning (Play Mode)** now also checks UI navigation using synthetic keyboard/controller input. Results are saved to `Documentation/CharacterSelectValidationResults.txt`. Tests restore the previous saved editor scene setup and input settings.
