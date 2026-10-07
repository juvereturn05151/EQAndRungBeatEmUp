# Character Select

Open `Assets/EQ_Rung_BeatEmUp/Scenes/MainMenu.unity` and press Play. Choose PLAY, then SINGLE PLAYER or LOCAL CO-OP. Online CREATE/JOIN uses the same select screen after the existing Relay lobby connects. Single player now enters selection before starting.

The screen prefab is `Assets/EQ_Rung_BeatEmUp/Resources/CharacterSelect/CharacterSelectCanvas.prefab`. Its `CharacterSelectManager` exposes the roster, required player count (1–4), duplicate policy, gameplay scene, every UI reference, and audio clips. The default gameplay scene remains the project's existing `PlayerHub`, which continues into the haunted-house run. Set `GameplayScene` to an enabled scene with the existing `StageFlowController` when changing this flow.

## Add a character

1. Create **Beat Em Up > Playable Character** combat data, or use an existing asset. Give it a unique stable `characterId`, its existing controller/animation/attack loadout, and its playable prefab. Do not replace the shared character controllers.
2. Create **Beat Em Up > Character Select > Character Definition**. Link `GameplayCharacter` to that combat asset; use the same ID in `CharacterId` and set `DisplayName`.
3. Assign `CharacterPrefab` to the actual playable prefab. Leaving it empty uses `GameplayCharacter.prefab`. The prefab must retain the components required by the existing session: PlayerInput, PlayerCombatInput, ComboController, CharacterMotor, health, ComboTracker and ComboUIController. Build new playable prefabs from an existing character prefab.
4. Assign `PortraitSprite` and `LargePreviewSprite`. Empty artwork fields fall back to the combat data. Import pixel art with **Point** filtering, no mipmaps and no compression. Optional `CharacterSelectAnimation` is an ordered list of preview frames. BlueShirtGuy uses the existing Idle2 frames, preserving its current design.
5. Set Power, Speed, Defense and Technique from 0–5, plus Archetype and Description. These bars describe the character; gameplay balance remains in the existing combat data. Set `IsUnlocked` false for a locked fighter. An entry without gameplay data/prefab is a locked placeholder and cannot confirm.
6. Run **Beat Em Up > Character Select > Create or refresh screen**. It discovers definition assets, registers their combat data in `MultiplayerCatalog`, populates the screen roster, and refreshes the network content hash. Add/remove/reorder the `Characters` array on the canvas prefab to curate the visible roster. Keep multiplayer catalog ordering identical in all builds.

`CharacterPortraitUI.prefab` includes portrait crop, hollow selection frame, independent P1–P4 marker/frame pairs, READY and a locked overlay. Multiple players choosing the same fighter remain visible. The grid derives its column count from roster size and scrolls as navigation reaches additional rows.

Refreshing preserves the canvas inspector settings and edited layout. **Rebuild generated visual layout** explicitly restores the supplied canvas/portrait layout and defaults. Character definition edits are preserved by either command.

## Controls and party rules

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Join / Confirm | Enter | A / South |
| Navigate | WASD / arrows | D-pad / left stick |
| Cancel / Back | Escape | B / East |
| Start | Space | Start |

Local mode reuses the existing initial keyboard/gamepad join. Additional unassigned gamepads join with A; the join press does not also confirm. One keyboard owns one player. Every joined device gets a private copy of the `CharacterSelect` map in the existing input asset, restricted to that device. No PlayerInputManager exists in this project's lobby; selection uses the existing session's device assignments.

Confirm locks navigation and shows READY. Cancel unlocks a ready player. Cancel again leaves the screen for P1/online, or removes another local player. Device disconnect removes that local slot. Start requires at least `RequiredPlayerCount` and **every joined player** ready. Single-player mode always requires one. Only the online host starts the run.

With `AllowDuplicateCharacters` false, new joins receive a free unlocked fighter, navigation skips occupied choices, and the session refuses confirmation/start with conflicts. If the roster has no free fighter, that slot shows **NO FREE FIGHTER** and cannot confirm. A party larger than the unlocked roster cannot start with duplicates disabled.

## Persistence, spawning and online

`MultiplayerSession` already survives scene loads. Its existing `LobbyState.slots` stores each player's character index, ready flag, device and owner. `PlayerCharacterSelection.From` exposes a presentation snapshot including stable character ID; it is not a second mutable session. The authority validates locks, duplicate conflicts and readiness. The existing lobby messages synchronize the same state online, including required count and duplicate policy.

The existing `MultiplayerSession.SpawnPlayer` calls `GameplayPlayerSpawner.ResolvePrefab` for each lobby slot, instantiates **that selected definition's prefab**, and applies the existing `PlayerCharacterLoadout`. Gameplay input, identity, rewards, hub preferences, camera framing and replication remain in the existing session. No new spawning singleton or networking lifecycle was added.

The input map is owned by the select screen only. Cursors/actions/listeners are disposed when its page closes, so combat actions and menu navigation resume normally. Network protocol/content hashes were refreshed; build all participants from the same project version. Relay service setup is unchanged.

## Validation

Run **Beat Em Up > Character Select > Validate selection and spawning (Play Mode)** from a saved scene. The opt-in integration test uses virtual keyboard/gamepads to exercise navigation, visuals/stat values, confirmation locking, cancellation, independent P2 input, duplicate policies, locked characters, four slots, Start gating, actual gameplay scene loading, selected loadouts/prefabs and clean menu re-entry. It restores the previous editor scene setup and input settings.

Results: `Documentation/CharacterSelectValidationResults.txt`. Rendered sample: `Documentation/CharacterSelectPreview.png`. Local tests do not prove an Internet Relay connection; check host/client behavior with two matching builds and the project's existing multiplayer smoke-test tools.

Architecture touched: MultiplayerMenu, MultiplayerSession, MultiplayerCatalog, LobbyState, PlayerIdentity (spawn-source inspection), the existing input asset, catalog hashing, and the existing single-player smoke test (now explicitly confirms before Start). MainMenu and gameplay scenes retain their original architecture.
