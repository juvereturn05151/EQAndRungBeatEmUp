# BlueShirtGuy defense and reactions

## Runtime architecture

The existing `ComboController` remains the player state machine. Its new partial source file extends that same component; it does not add a second state machine. The combat clock still runs at 60 logical frames per second. `PlayerDefense.asset` authors defensive/reaction timings and held sprite poses separately from offensive `AttackData`.

Reused systems: CombatClock ordering and manual stepping, CharacterMotor movement/facing/height/ground detection, CharacterAnimation's sprite override, AttackPlayer's hitstop and attack cancellation, CombatHurtbox's incoming-hit gate, CharacterHealth's death/restore events, EnemyHitReaction's existing recovery timer, enemy AttackHitbox sampling, PlayerInput and PlayerCombatInput. Defensive states use sprite holds rather than Animator transitions. Existing attacks and combo routes retain their offensive data.

## Input

| Action | Keyboard | Gamepad | Behavior |
|---|---|---|---|
| Guard / Parry | L without movement | Left shoulder without stick movement | Press opens a fresh parry window; hold continues Guard; release ends Guard after any remaining blockstun. |
| Dodge | Direction + L | Stick direction + left shoulder | One dodge per button press, with priority over Guard. Holding cannot repeat it. |

The existing `Guard` input action owns both behaviors. Separate Dodge and Parry actions are absent. Standalone input resolves defense after all input events, using the current Move action and the Guard action's press/hold state. Paired multiplayer input sends the same decision through the existing player command; a Dodge command always suppresses Guard on the authority. There are no keyboard-specific defense checks.

Movement above the existing 0.1 magnitude threshold selects Dodge on a new defense-button press. Diagonals retain the existing normalized eight-direction movement and the same total travel distance. Releasing movement while holding the defense button requests Guard once the current dodge/recovery permits it. Adding movement while holding the button releases Guard and never creates another Dodge; release and press the button again to Dodge.

## Gameplay timing

All indices are zero-based logical frames. Hitstop pauses the actor's state timer and movement.

| State / result | Implemented timing |
|---|---|
| Dodge | 20f: startup 0–2, movement 3–10, recovery 11–19. |
| Dodge invulnerability | Only 4–9 inclusive. Startup and recovery receive ordinary hits. |
| Dodge movement | Configurable speed 9 units/s, eight moving ticks = 1.2 units. Normalized current movement input, or backward from facing when input is absent. Uses motor velocity and arena bounds. |
| Guard entry / hold | Entry poses hold 2f and 3f; last pose holds indefinitely. No animation restart each tick. |
| Parry window | Uses the authored `parryWindowFrames` (currently 12f). Held Guard after that only blocks. The input change leaves this tuning unchanged. |
| Guard block | Default zero damage, 10f blockstun and 2f hitstop. Per-hitbox `blockDamage`, `blockstunFrames` and `unblockable` are editable in Attack Data Editor. |
| Successful Parry | Zero damage, no blockstun, 8f player recovery, 24f attacker interruption, 6f hitstop on both actors. |
| Parry re-arm | Full active window plus 6 additional combat frames. Repressing during this delay immediately Guards without reopening parry. |
| KnockDown | 18f ground fall/impact, 45f Downed, 24f GetUp, then neutral. Airborne launcher reactions hold the falling pose until actual motor landing, then begin the 18f ground sequence. |
| Die | 18f fall/impact, then final pose indefinitely. Death while already down keeps the defeated pose. Airborne death falls to the floor first. |

Guard and Parry require a valid incoming attack from the front, based on attacker position and player facing. Behind attacks and explicitly unblockable hits go through normally. Releasing/repressing Guard during blockstun does not reopen a parry window. Successful Parry interrupts the attack already being sampled and uses existing enemy hitstun recovery without dealing artificial damage. If Guard is still held after parry recovery, it resumes ordinary Guard without rearming Parry.

Block uses a distinct recoil sprite and lighter hitstop; Parry uses deflection poses and stronger hitstop. The `DefenseImpact` event now connects successful parries to the existing AttackFeedback renderer, using a distinct existing flash/sparks prefab and block sound. The defense Inspector includes a visual parry/Guard frame track and editable re-arm / feedback data. See [Parry.md](Parry.md) for current tuning and test steps.

KnockDown is entered by an explicit `HitType.KnockDown`, a Launcher, or an AirFinisher. Defense/attack/jump requests are rejected while knocked down, downed or getting up. Normal follow-up hits can damage the player but cannot force an incompatible state. Health reaching zero overrides every state, including frozen reactions. Dead players cannot move, attack, defend or recover automatically. Only an explicit existing `CharacterHealth.Restore()` can reset the player for respawn/restart.

## Art inventory and pose holds

Existing BlueShirtGuy Idle2, Attack1, Walk, Jump, Launch1 and AirAttack1 art was inspected first. No dedicated defense, player KnockDown, GetUp or Die art existed. Existing Idle2 and Attack1 drawings are reused where their silhouettes fit.

| Animation | New drawings | Sprite holds |
|---|---|---|
| Dodge | None; reuse Attack1_09, _10, _11 and Idle2_01. | 09: 0–2; 10: 3–5 and 6–9; 11: 10–13; Idle: 14–19. |
| Guard | None; reuse Attack1_02, _04 and block recoil _05. | 02: 0–1; 04: 2 onward; 05 during blockstun. |
| Parry | Two missing interception/deflection drawings: Parry_01 and _02. | Attack02 1f, Parry01 2f, Parry02 3f, Attack07 2f. |
| KnockDown | Four missing losing-balance, falling, impact and prone drawings: KnockDown_01–04. | 4f, 5f, 4f, 5f; prone holds through Downed. |
| GetUp | None; reuse the four KnockDown poses in reverse and Idle. | KD04 6f, KD03 6f, KD02 6f, KD01 5f, Idle 1f. |
| Die | None; reuse KnockDown_01–04. | 3f, 5f, 4f, 6f; KD04 remains indefinitely. |

New drawings use transparent backgrounds, the existing sprite palette, binary alpha, point filtering, 100 pixels/unit and the existing (.5, .0625) grounding pivot. Their compatible canvas is 160×128 to accommodate prone bodies without shrinking the character; standing scale remains approximately 106 pixels. Existing artwork remains at its original resolution. Pixel outlines, blue/white shirt print, dark trousers, shoes, face and hair are preserved as prototype reference-based artwork.

The built-in ImageGen tool generated only the missing poses from the inspected BlueShirtGuy references. Prompts are preserved in `Tools/BlueShirtDefense/prompts.txt`; `export-sprites.cjs` performs deterministic crop, nearest-neighbor native-scale export and mapping to the existing palette. Generated source sheets and the visually reviewed native export are in `Assets/ArtAssets/Characters/BlueShirtGuy/Source/Defense/`.

## Modified / added files

Under `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/`:

- Added `PlayerDefenseData.cs` and `ComboController.Defense.cs`, with Unity metas.
- Modified `ComboController.cs`, `CharacterMotor.cs`, `CombatClock.cs`, `CombatHurtbox.cs`, `AttackHitbox.cs`, `AttackPlayer.cs`, `AttackData.cs`, `EnemyHitReaction.cs`, `PlayerCombatInput.cs`, `CombatDebugOverlay.cs`.

Assets and authoring:

- `Assets/EQ_Rung_BeatEmUp/PlayerDefense.asset` and meta; reference assigned on `Prefabs/BlueShirtGuy.prefab`.
- `Assets/EQ_Rung_BeatEmUp/Input/PlayerInput_Actions.inputactions`.
- `Assets/Editor/Combat/AttackDataEditorWindow.cs`; new `PlayerDefenseSetup.cs` and `PlayerDefenseValidation.cs`, with metas.
- `Assets/ArtAssets/Characters/BlueShirtGuy/Animations/Parry/BlueShirtGuy_Parry_01.png` and `_02.png`, with metas.
- `Assets/ArtAssets/Characters/BlueShirtGuy/Animations/KnockDown/BlueShirtGuy_KnockDown_01.png` through `_04.png`, with metas.
- New art folders and their Unity metas, plus `Source/Defense/Parry.png`, `KnockDown.png`, `NativeReview.png` and metas.
- `Tools/BlueShirtDefense/export-sprites.cjs`, `prompts.txt`; this report and validation result files.

## Validation and remaining limitations

Unity 6000.4.6f1 Play Mode validation uses copies of the actual prefab, authored defense data, sprite assets, input actions and installed Input System package in an isolated project. The defensive suite executes 188 checks, including every Dodge tick, vulnerable/immune hit frames, hitstop, normalized movement, front/behind/unblockable hits, early/timely/late Guard, block damage and blockstun configuration, held-input edge behavior, keyboard and gamepad callbacks, deflection/prone/final sprite holds, launch-to-ground recovery, and death from eight different states. Actual enemy AttackPlayer/AttackHitbox sampling verifies Block/Parry outcomes and safe attacker interruption; the test removes startup from a copy of the enemy attack only to align the active collision deterministically.

The original defense implementation passed 815 checks: player defense 188, ground punch regression 205, air punch regression 262, enemy recovery regression 160. The later parry improvement passed the updated 272-check player defense suite; those original other regression suites were not rerun for the parry change. Current outcomes are in `PlayerDefenseValidationResults.txt` and `Parry.md`.

Run the suite in the project via **Beat Em Up → Validate player defense (Play Mode)**. Detailed outcomes are in `Documentation/PlayerDefenseValidationResults.txt`. Regression results are recorded alongside this report.

Artwork and GetUp are deliberately first-pass mock-ups; GetUp reverses KnockDown poses rather than adding new drawings. Automated Play Mode checks verify mechanics and exact timings, but subjective controller feel still needs human playtesting. The existing enemy prefab remains a passive training dummy; enable its existing EnemyCombat behavior when testing autonomous attacks. No new game-over screen or respawn workflow is introduced.
