# Playable haunted-house level and authoring

Open **Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity** and press Play. This scene is first in Build Settings. The original ComboDemo remains available as the combat test scene. The outdoor Player Hub and all eight haunted areas run inside the same scene; completed rooms swap their background/floor, dispose of old encounter objects, move the existing player to the next entry, and update movement bounds. The level ends after leaving Escape Lane.

## Playing

The existing controls are preserved: WASD/arrows to move, J/Enter or left mouse for attack, K for launcher, Space to jump, L to guard, Left Alt to dodge. Gamepad combat bindings remain unchanged. When the HUD says the exit is open, walk to the right-hand exit at approximately `(2.7, 0)`. Locked encounters keep progression disabled; movement always stays within the authored room bounds.

In Recovery Shrine, stand near the center shrine and press **E** or gamepad **Select** to refill health. No enemies spawn there. Healing is optional: walk to the exit when ready. The boss stays protected while any of the four cursed totems remain; punch the totems, then defeat the vulnerable ghost and leave. The boss periodically telegraphs a warp and reappears elsewhere in the arena. **R** retries the current room after death or restarts the level after completion.

**F8** opens the debug panel: jump to any room, inspect encounter/wave state, see live/pending counts, signal manual encounters/waves, complete a stage event, or advance if the real unlock condition permits. Stage jumping intentionally restores the player and resets that room. It is a development shortcut, not normal progression.

## Default level

| Room | Encounters / progression |
|---|---|
| Player Hub | Safe starting area; no enemies, damage or reward; walk to the right exit |
| Entrance Gate | 2 Rushers on entry, then 1 Rusher after the first wave clears |
| Blood Sheet Corridor | 3 Rushers after 3 seconds, then 1 Thrower after clear |
| Fake Morgue | 2 Rushers, 1 Thrower, 1 Screamer |
| Service Corridor & Stair | 2 Rushers, 1 Bruiser |
| Haunted Maze | 2 Rushers, 1 Ambusher, 1 Prefect; then 1 Thrower and 1 Screamer |
| Recovery Shrine | No waves; optional recovery interaction; reach exit |
| White Ghost Boss Chamber | 1 boss and 4 cursed totems; destroy totems, defeat boss, reach exit |
| Escape Lane | No waves; reach final exit to finish |

The first five rooms also contain two normal breakable haunted-house set pieces. They reuse the existing totem artwork as prototype scenery, with the **Normal** special type, and do not affect boss protection. Replace their sprites/prefabs in the level Inspector with crates, carts, jars, or other final prop art. Cursed totems are a separate authored **CursedTotem** special type in the boss room. Art assets themselves were retained.

## Authoring without code

Select **Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset**, or select **Haunted House Stage Flow** in the scene and click **Edit level / ordered stages**. The custom Inspector provides a draggable stage list and the selected stage's fields. Expand the selected stage, encounters, waves, and enemy spawns.

For each stage, assign Background and Floor sprites, art width, movement bounds, entry/exit coordinates and exit radius, completion mode, encounters, destructibles, decorative prefab placements, optional ambience AudioClip, and notes. `nextStageIndex = -1` follows the next item in list order. An explicit index routes elsewhere; an index equal to stage count ends the level. Reordering stages does not automatically repair explicit next-stage indices or prior-encounter indices: review those when reordering.

Completion modes are **ReachExit**, **ClearEncounters**, **BossDefeated**, and **Event**. Completion enables the exit; the player still walks to it. ReachExit stages can contain optional encounters; an active encounter with `lockStageUntilClear` still blocks progression. ClearEncounters waits for every `requiredForCompletion` encounter, including encounters that have not triggered yet. BossDefeated additionally requires a boss to have spawned, no living tracked boss, and zero intact cursed totems. Event requires `CompleteStageEvent()` and still respects active encounter locks.

### Encounters and waves

Encounter triggers:

- **StageEnter**: starts on entry, optionally delayed by `triggerDelay`.
- **Time**: starts after `triggerDelay` seconds from room entry.
- **PreviousEncounterClear**: starts after an earlier encounter completes; `-1` references the immediately preceding encounter. Delay starts when that completion becomes eligible.
- **PlayerZone**: activates after the player enters the authored XY Rect, optionally delayed. No trigger collider is required.
- **Manual**: waits for `SignalEncounter(encounterId)`, optionally delayed.

An encounter owns a list of waves. **EncounterStart** and **Time** waves use delays from encounter start. **PreviousWaveClear** waves wait for the immediately previous wave to finish, then apply their delay. **Manual** waves wait for `SignalWave(encounterId, waveId)`; their configured delay is measured from encounter start. Use unique encounter IDs within a room and unique wave IDs within an encounter.

Each enemy spawn group specifies a combat prefab, count, XY spawn points, and interval. Points cycle for repeated enemies and are clamped to the room bounds. Groups in one wave can run concurrently. A wave is clear only when every scheduled spawn has happened and every tracked enemy is dead/removed; defeating the first enemy cannot skip later interval spawns. An encounter clears after all its waves clear. The scheduler runs on CombatClock so frame stepping and low render rates preserve event timing.

Use the seven prefabs under **Levels/HauntedHouse/Prefabs**. They extend the existing BadGuy combat structure with each generated enemy's Animator/reaction sprites and a prototype primary move under **Levels/HauntedHouse/Attacks**. Spawned EnemyCombat targets the current player. Existing player and BadGuy assets, combo attacks, and reaction rules are retained. These archetypes use the existing chase/attack AI and one primary move each; bespoke projectile trajectories, paired victim grab animations, and support AI are future encounter polish. AttackData remains editable in the existing Frame Attack editor.

### Destructibles

Add a placement in a stage's Destructibles list. Choose a prefab or provide intact/damaged/broken sprites directly, position, HP, collider size/offset, and Normal/CursedTotem type. Runtime objects use trigger hurtboxes; they do not add solid movement obstacles unless your prefab separately includes a CombatWall. After damage, props shake and use the damaged sprite. At zero HP they disable the hit collider, display broken art, invoke `onBroken`, optionally spawn `dropPrefab`, and notify listeners. Drop prefabs inherit the room parent and are cleaned up at transitions. The hook does not automatically implement pickup gameplay.

Player-team attacks damage props through the normal AttackHitbox overlap and lane-tolerance checks. Damage is deduplicated per attack/hit ID, including multi-collider prefabs, and respects authored repeat-after-frame intervals. Enemy-team attacks do not break props. Boss gating uses the same spawned destructibles, and rejects hits through the normal CombatHurtbox while totems remain. It does not change CharacterHealth's generic Damage API.

## Camera, art, and transition details

Two SpriteRenderers display the authored plates at sorting orders -100/-90. At default art width 7.2, scale is 1 and the seam stays at Y 1.76, matching the existing 40% background / 60% floor framing. Floor is not assigned to `StageFraming.floor`, preventing the placeholder-resizing behavior from stretching it. The controller updates StageFraming's lanes and resets its horizontal position for each room, with a room-wide dead zone; air-combat upward expansion remains active. The controller authors the existing motor's XY bounds rather than adding a navigation framework.

Room switches reset attack/air movement state and show a brief dark overlay as the art changes. Health persists between ordinary rooms. Death retry and debug stage jumps restore it. Player movement, defense, combo, jump, juggle, bounce, knockdown/get-up and frame-clock behavior are reused.

The plates are fixed room segments, not seamless scenery tiles. At the default 16:9 rest framing they cover the camera. Unusually wide aspect ratios or extended airborne framing can expose clear-color borders; larger stages should supply wider/taller artwork rather than stretch a single room plate. Changing `artWidth` currently scales the two environment plates horizontally; character sprites retain scale 1. Select the Stage Flow scene object for entry, exit, bounds, spawn and prop gizmos. In edit mode it previews the first stage's gizmos; running rooms display their current data.

## Tools, files, and validation

- **LevelDefinition.cs**: ordered level/stage, encounter, wave, spawn, prop and destructible data.
- **StageFlowController.cs**: room swapping, scheduling, exit locking, recovery, restart, events, HUD and debug.
- **DestructibleObject.cs**: health, damage/break visuals, shake and drop/event hooks.
- **TotemBossController.cs**: protection condition and telegraphed warps.
- **LevelDefinitionEditor.cs / StageFlowControllerEditor.cs**: authoring list, validation warnings, data selection and runtime shortcuts.
- **HauntedLevelBuilder.cs**: creates the default level, scene, seven enemy prefabs and frame attacks; adds the scene first in Build Settings. Re-running preserves existing authored level, prefabs, attacks and scene.
- **HauntedStageFlowValidation.cs**: deterministic real-combat full-level route and trigger edge cases.
- Existing **AttackHitbox.cs**, **CombatHurtbox.cs**, and **CharacterMotor.cs** have small opt-in integration additions for props, boss gating, and room resets.

Create another level with **Create → Beat Em Up → Level Definition**, then assign it to the Stage Flow controller. Run **Beat Em Up → Stages → Validate full haunted-house flow (Play Mode)** for the integration suite. Results are in `Documentation/HauntedStageFlowValidationResults.txt`; actual Camera captures are under `Documentation/HauntedStageFlowPreview`. Regression suites separately check the existing combat/defense pipeline. Full-flow tests pause ordinary enemy AI during deterministic player attacks, then separately prove the new enemy attacks can damage the player; this verifies integration and progression, not final encounter difficulty balance.

The custom Inspector warns about missing art/enemy references, reversed bounds, unreachable entry/exit points, invalid next-stage indices, and impossible previous-wave/encounter dependencies. Runtime missing enemy/art configuration blocks completion with a visible error instead of silently skipping the encounter. Audio hooks, decorative prefabs, and break/drop events are optional; no new audio track or final prop artwork is included.

Final validation in Unity 6000.4.6f1 passed **79 stage-flow checks, 101 combat checks, and 188 defense checks (368 total)**. The existing combat suite's outdated fixed Punch1 damage/hitstop/frame assumptions were changed to use its authored data, and the buffer-expiry fixture now requests launcher during a noncancelable attack so it remains valid with the project's existing standalone launcher behavior. Attack settings and standalone-launcher gameplay were preserved. Regression reports are `HauntedCombatRegressionResults.txt` and `HauntedDefenseRegressionResults.txt` beside the full-flow report.
