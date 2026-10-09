# Character 1 and Character 2: dash into run

Implemented in the shared player controller and existing character loadouts. Both prefabs already have their own run data; existing scene instances load the matching data through `PlayerCharacterLoadout`. No scene replacement is needed.

## Input and behavior

Hold a movement direction and Guard: keyboard WASD + L, or gamepad left stick + the current Guard binding (B / Circle, buttonEast). Either input order works. Guard alone retains stationary guard/parry. Run also supports lane movement and diagonal input using the existing motor's direction normalization.

The existing Dodge state is the game's dash. Its original sprite sequence, movement burst, and invulnerability run first. Continued direction + Guard cancels dash recovery into Run at frame 11. Releasing either input during Dash preserves the original full dash recovery and prevents the run transition.

While running, direction changes immediately update movement and facing without another dash. Releasing Guard returns to walk; releasing direction while Guard remains held enters stationary guard. Releasing both returns to idle. Run has no guard protection or dash invulnerability.

Attack, launcher, jump, skill, damage reactions, stage reset, and disabled/disconnected input clear the run speed override. Offensive attacks use the existing buffer, cancel windows, and attack player. Attacking interrupts the run; release and re-hold the run chord to start another dash after attacking. This avoids automatic repeated dashes during a combo.

## Tuning in the Inspector

Select `Characters/BlueShirtGuy_Run.asset` or `Characters/Character2/Character2_Run.asset`:

| Setting | Default | Meaning |
|---|---|---|
| Run Speed | 3.6 units/sec | Existing walk speed is 2: run is 1.8 times faster. |
| Dash To Run Frame | 11 | Fixed 60 Hz combat frame; about 0.183 seconds after dash starts. |
| Poses / Frames | 8 poses, 3 frames each | 0.4-second loop, 20 displayed poses per second. Hitstop freezes movement and pose phase together. |

Dash duration, burst speed, and invulnerability remain in each character's existing `PlayerDefenseData`: 20 frames total, speed 9 on frames 3–10, invulnerability on frames 4–9. Run's transition frame is clamped after the movement and invulnerability windows and no later than the effective dash duration, including existing upgrades. Change dash tuning in that defense asset rather than duplicating it in Run.

## Animation assets

Each character has eight original, transparent PNG run poses. Character 1 retains the blue shirt, navy pants, and sneakers; Character 2 retains the large dark hair, glasses, gray shirt, navy shorts, and sandals. Forward lean, stronger arm swings, raised knees, and airborne passing poses distinguish the run from walk.

- Character 1: `ArtAssets/Characters/BlueShirtGuy/Animations/Run/BlueShirtGuy_Run_01.png` through `_08.png`.
- Character 2: `ArtAssets/Characters/Character2/Run/Character2_Run_01.png` through `_08.png`.
- Loop clips: `Animations/BlueShirtGuy_Run.anim` and `Characters/Character2/Character2_Run.anim`, added as Run states to each existing locomotion controller.

Sprites use 100 PPU, point filtering, uncompressed textures, full-rect mesh, and a common feet pivot. The 160 x 144 canvas accommodates the forward lean without clipping. A single scale per character and common foot baseline keep the poses stable. Runtime playback uses the existing `CharacterAnimation.HoldSprite` and combat clock; the Animator clips also provide editor previews. The generated sheets are source masters only and are not used as game sprites.

Preview: [both run loops](PlayerRunPreview/RunLoops.gif), [pose contact sheet](PlayerRunPreview/RunContactSheet.png).

## Files created or modified

All asset paths below are under `Assets/EQ_Rung_BeatEmUp/` unless stated otherwise.

Created:

- `Scripts/Combat/PlayerRunData.cs` and `Scripts/Combat/ComboController.Run.cs`: tunable run data and shared run state/input/pose control.
- The 16 PNGs, two run data assets, two looping clips listed above, with Unity metadata.
- `Assets/Editor/Combat/PlayerRunSetup.cs`: imports/wires both characters while retaining existing catalog order and character selections.
- `Assets/Editor/Combat/PlayerRunValidation.cs`: automated Play Mode checks using real prefabs and virtual Input System devices.
- `Tools/PlayerRun/`: retained generated source masters, packaging, preview, and asset reference checks.
- This guide, art-generation specification, previews, and validation reports in `Documentation/`.

Modified:

- `Scripts/Combat/AttackData.cs`: appended Run enum value, preserving existing serialized state numbers.
- `CharacterMotor.cs`: ground-speed override through the same collision sweeps and stage clamps; reset cleanup.
- `ComboController.cs`, `ComboController.Defense.cs`, `ComboController.Skill.cs`: state transitions, dash recovery cancel, attack/reaction/skill cleanup.
- `PlayableCharacterData.cs`, `PlayerCharacterLoadout.cs`: character-specific run data selection and reset.
- `PlayerCombatInput.cs`: held direction + Guard detection without changing input bindings.
- `Scripts/Multiplayer/SessionInput.cs`, `MultiplayerSession.cs`: held run command, authority application, menu/disconnect/timeout cleanup.
- Both character definition assets, both prefabs, both locomotion controllers, and `Resources/MultiplayerCatalog.asset`: run references and appended network sprite IDs/content signature.

## Actual verification

Unity 6000.4.6f1 batch Play Mode ran against an isolated copy of the current project, leaving the open editor's current scene alone. **103 run/input/integration assertions passed**, including seven original defense regression suite invocations. Those suites recorded **260 additional existing assertions**. No test failure remains.

Both actual player prefabs were tested for dash-first timing, Guard alone, both keyboard input orders, gamepad input, release paths, left/right facing, reversal, faster movement, complete pose loops, hitstop, pause, attack and buffered combo, jump, hit reaction, reset, wall collision, arena boundaries, lane movement, camera follow, and catalog sprite resolution. Multiplayer input command holds/releases were tested locally. Existing dash, guard/parry, parry boundaries/rearming, parry counter compatibility, knockdown, death, and real-enemy attacks passed unchanged.

Reports: `PlayerRunValidationResults.txt`, `RunDefenseRegressionResults.txt`, `PlayerRunAssetVerification.txt`. The transferred assets and sprites were compared with the validated copy and their serialized GUID references checked. The exported pose sheet was visually inspected.

Physical controller play-feel, a complete manual Stage 1 playthrough, and a two-machine online session were not performed. Run-specific skill interruption uses the existing skill entry path but was not separately exercised in the run validation suite.

To rerun checks, use **Beat Em Up → Characters → Validate dash-to-run (Play Mode)** in a disposable project copy; it creates an empty validation scene. **Set up dash-to-run animations** rebuilds animation wiring and sprite import settings, preserving existing run speed/transition tuning.
