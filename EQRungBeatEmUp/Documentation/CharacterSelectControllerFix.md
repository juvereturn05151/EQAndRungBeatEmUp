# Character-select controller ownership

Previously `BeginLocal` automatically paired Player 1 to the keyboard whenever one was connected, even if a controller activated Single Player or Local Co-op. Single-player selection also rejected the controller Join action, leaving its D-pad/stick without a cursor.

Menu navigation now remembers the device producing navigation/confirmation and passes it to the local session before activating a menu button. The local lobby uses that device for Player 1, falling back to the available keyboard/controller if it has disconnected. In single-player selection, an unpaired device can claim the one Player 1 slot with a meaningful Navigate or Join action. Claiming changes the paired device and clears readiness instead of adding Player 2. The entering/joining press still cannot also confirm a character.

Local co-op keeps explicit independent device ownership: a new device joins another slot via A/Enter. Online device preference remains controlled by the existing online input option. Gameplay receives the same device selected in the lobby.

Extended `CharacterSelectValidationRun` uses virtual Input System devices to exercise controller entry with a keyboard connected, D-pad/stick character navigation, A/B, takeover, Start and pairing after spawning. The existing keyboard/co-op/menu integration coverage runs afterward. Run **Beat Em Up → Character Select → Validate selection and spawning (Play Mode)**; results are saved to `Documentation/CharacterSelectValidationResults.txt`.

Default EventSystem submit/move actions are disabled at initialization so the custom menu action cannot also activate a new page's focused button during the first input update. The validation launcher explicitly clears its completion flag before spawning a new run.

Final Unity integration result: **PASS**, including the new controller entry/takeover/spawn checks and the existing independent co-op and menu checks. Compilation succeeds with zero errors; the project still reports existing warnings.
