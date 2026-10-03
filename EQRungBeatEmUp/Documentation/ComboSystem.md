# Combo and air-juggle system

## What was in this checkout

Unity 6000.4.6f1, Input System 1.19.0, `activeInputHandler: 1` (New Input System only). The existing `InputSystem_Actions.inputactions` asset contained a Player map with Move, Attack and Jump, plus a UI map and keyboard/gamepad control schemes.

There were no gameplay scripts, character prefabs, PlayerInput components, health, damage or movement systems. SampleScene and Demo contained only scene infrastructure. BlueShirtGuy had single-state idle, walk, attack and air-attack controllers; their motion GUIDs did not resolve to clips in Assets. There was no object named BadGuy; the available enemy artwork and functioning animation clips were ThaiBadBoy. The new BadGuy uses that art.

## Files added

- `Assets/Scripts/Combat/AttackData.cs`: Inspector-editable ScriptableObject attack definitions.
- `CharacterMotor.cs`: walking on an XY lane, independent visual jump height, knockback and bounded air control.
- `ComboController.cs`: ground/air routes, launcher branch, input buffers, states and interruption.
- `PlayerCombatInput.cs`: InputAction callbacks on the character's PlayerInput action copy.
- `AttackHitbox.cs`, `CombatHurtbox.cs`: reusable overlap hitboxes; one health target can be hit once per swing, even with multiple hurtboxes; friendly fire and separate lanes rejected.
- `CharacterHealth.cs`: shared player/enemy health and death signal.
- `EnemyHitReaction.cs`: launch, finite juggle, normal gravity restoration and landing recovery.
- `EnemyCombat.cs`: optional ground chase/attack AI that pauses during reactions.
- `CharacterAnimation.cs`: named Animator-state hooks; combat timing independent of animation lengths.
- `CombatDebugOverlay.cs`: optional runtime state, height, buffer, last action, health and juggle display.
- `Assets/Editor/CombatDemoBuilder.cs`: `Beat Em Up > Create or rebuild combo demo`.
- `Assets/Editor/CombatValidation.cs`: batch Play Mode integration checks using paired virtual keyboard/mouse and gamepad devices.
- `Assets/Scenes/ComboDemo.unity`: ready-to-play arena, BlueShirtGuy, BadGuy and optional debug overlay.
- `Assets/CombatDemo/Prefabs/BlueShirtGuy.prefab`, `BadGuy.prefab`.
- `Assets/CombatDemo/Attacks/`: Punch1–3, Launcher, AirPunch1–3 and EnemyPunch assets.
- `Assets/CombatDemo/Animations/`: two multi-state controllers and five player clips built from existing PNG frames.
- `Assets/CombatDemo/Sprites/`: sprite-imported copies of the player PNGs and a simple arena sprite. Original artwork and original controllers remain available.
- Unity `.meta` files for the new assets; this document and validation results.

## Existing files changed

- `Assets/InputSystem_Actions.inputactions`: added Launcher, J for Attack, K and gamepad North for Launcher. Existing actions, IDs, Move/Jump/Attack bindings, control schemes and UI map retained. Existing Attack aliases include Enter and left mouse. The unused Interact action also has a North binding; no Interact handler is installed in this demo. Rebind Interact if you enable it in gameplay later.
- `ProjectSettings/EditorBuildSettings.asset`: added ComboDemo as the first enabled scene; SampleScene retained.

No package changes were needed. Package manifest/lock edits already present before this work were preserved.

## PlayerInput and Animator setup

The new BlueShirtGuy prefab has one PlayerInput referencing the existing input asset, default action map Player, and notification behavior InvokeCSharpEvents. PlayerCombatInput subscribes to Move performed/canceled and Attack/Launcher/Jump performed. PlayerInput owns map activation, control scheme switching and device pairing. There is no direct keyboard polling or legacy Input API.

BlueShirtGuy's new controller has Idle, Walk, Punch1, Punch2, Punch3, Launcher, Jumping, AirPunch1, AirPunch2, AirPunch3 and GroundHit. BadGuy's controller has Idle, Walk, Attack, GroundHit, Launched, AirHit, Falling, Landing and Defeated. Components explicitly play named states, with no exit-time transitions to interfere with configured combat timing. The state name on an AttackData asset can be changed in the Inspector. No animation events are required.

**Replace these placeholder motions later:** Punch2/3 currently reuse the ground Punch clip. AirPunch2/3 reuse AirPunch. Jumping and player GroundHit use Idle. Enemy Launched/AirHit/Landing reuse Hurt; Falling/Defeated use Knockdown. Assign final clips to the corresponding states in the generated controllers. Launcher already uses the supplied Launch1 frame art. Do not rebuild the demo after manually assigning final Animator motions: the builder restores the default motion assignments.

## Inspector tuning

Select the assets in `Assets/CombatDemo/Attacks` to tune each attack:

- Damage (default 10), hitstun (.3 seconds), knockback (.25), hitbox offset (.55, .55), size (1, 1.1) and lane tolerance (.55).
- Startup (.09), active duration (.12), recovery (.22), combo window opening (.18), window duration (.27, capped at attack end), optional cooldown.
- Ground/Air domain, Can Launch, Can Hit Airborne and Ends Juggle. AirPunch3 closes the juggle.
- Launcher force/horizontal force overrides: zero uses the victim's settings; default Launcher assets use zero so tuning BadGuy works directly.

Select BlueShirtGuy:

- ComboController: input buffer .35 seconds, jump buffer .6 seconds, combo reset time .35 seconds after attack recovery, finisher recovery .12 seconds, launcher branch after 2 punches. Ground/air routes are Inspector arrays; add attack assets and states to extend them. Recovery values are per AttackData.
- CharacterMotor: speed 3.5, manual jump force 7.5, normal gravity 14, arena bounds; air attack gravity scale .2, max fall speed 1.2, maximum air control time 1.1 seconds per jump. Gravity is always positive; normal gravity resumes at attack end, interruption, or air-control budget exhaustion.

Select BadGuy:

- EnemyHitReaction: launch force 8, horizontal force .7, juggle lift 2.2, juggle gravity 9, maximum juggle time 2.8 seconds, maximum lift hits 3, landing recovery .35 seconds, finisher fall speed 3.
- EnemyCombat: Passive Training Dummy defaults on for combo practice. Turn it off to enable chase and punches. The scene assigns BlueShirtGuy as Target. When placing a BadGuy prefab yourself, assign its Target.
- CharacterHealth: demo enemy health 500; player health 200. Stop/restart Play Mode to reset defeated characters.

The motor intentionally models arcade height separately from ground XY. Its child Visual owns the Animator, SpriteRenderer and hurtbox; its root owns gameplay components. There are no rigidbody collisions between actors and no platform physics in this lane arena. Moving to a platformer/3D world requires adapting CharacterMotor; combat and input components can remain separate.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move on ground lane | WASD / arrows | Left stick |
| Attack | J (Enter / left mouse also retained) | West / Square / X on Xbox |
| Launcher | K | North / Triangle / Y on Xbox |
| Jump | Space | South / Cross / A on Xbox |

## Test the complete combo in Unity

1. Wait for Unity to import and compile. Open `Assets/Scenes/ComboDemo.unity`, then enter Play Mode and focus the Game view. Both characters start within punch range on the same lane.
2. Tap J three times, around .2–.3 seconds apart. Confirm Punch1 → Punch2 → Punch3 and three grounded damage reactions. A slightly early tap is buffered. The finisher ends the chain.
3. Tap J, J, K. Confirm Punch1 → Punch2 → Launcher and BadGuy rising while BlueShirtGuy stays grounded.
4. Manually press Space during or shortly after Launcher. An early Space press is buffered until the launcher's active frames finish. BlueShirtGuy must jump only because you pressed Jump.
5. After BlueShirtGuy leaves the ground, tap J three times, around .2–.3 seconds apart. Confirm AirPunch1 → AirPunch2 → AirPunch3. BadGuy receives short lift from the first two hits, then falls after the finisher. No air attack happens automatically.
6. Confirm both land, the air route resets, and BadGuy returns to normal after landing recovery. Repeat with West, West, North, South, West, West, West on a gamepad.
7. Test a normal Space/South jump followed by the three air punches without first launching an enemy. Missed air punches still consume the three-attack budget for that jump.
8. Press the next J/K slightly before a cancel window, then test waiting past reset time. Mash J/K/Space: no stacked animations, midair ground punches, midair relaunch or endless air-chain restart should occur.
9. Turn off Passive Training Dummy to test enemy chase, attack, player damage, interruption and recovery. Re-enter Play Mode if health runs out.
10. Toggle Combat Debug > Show Debug off to hide the overlay. Enable AttackHitbox > Debug Draw and Visual > CombatHurtbox > Debug Draw to display hitboxes/hurtboxes with Scene/Game Gizmos enabled. The overlay shows state, height, combo index, current attack, buffered request, buffered Jump, last Input Action and enemy juggle state.

## Validation

The batch runner compiles under the project's exact Unity version, builds the scene and enters Play Mode. It pairs virtual devices through PlayerInput, queues real Input System state events, and advances the same combat/motor methods used by Update. Checks cover both full keyboard/gamepad routes, ground combo, movement, normal jumping, once-per-swing damage, lane filtering, early/expired buffers, manual jumping, no automatic air attacks, landing, interruption, mashing, juggle hit/time caps, defeat, enemy chase/attack and required Animator hooks.

See `Documentation/CombatValidationResults.txt` for the recorded assertions. Rendered captures show the arena and launched/air-combo poses. These automated device tests do not replace physical controller testing or subjective timing/feel tuning in the Game view.

To rerun safely, use a separate copy of the project and invoke Unity in batch mode with `-executeMethod CombatValidation.BuildAndValidate`. The runner rebuilds/opens its demo and exits Unity after writing results; do not invoke it in an editor session with unsaved work.
