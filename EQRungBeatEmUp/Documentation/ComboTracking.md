# Landed-hit combos

BlueShirtGuy now has a `ComboTracker` and `ComboUIController` on its prefab. Existing scene players with a ComboController also receive the components at runtime if missing. The existing attack-button chain, frame data, hit registration, attack movement and enemy reaction logic remain the combat authority.

## Counting rules

- The successful `CombatHurtbox.Receive` damage path notifies the attacker's tracker after damage and reaction resolution. Counts use actual HP lost, including damage modifiers and clamping lethal overkill.
- Each accepted hit against an opposing enemy adds one hit and its damage. Whiffs, invulnerability, blocks, parries, zero damage, props and friendly targets do not count.
- Existing hit IDs suppress duplicate contacts from lingering hitbox frames. The tracker does not create a second hit-registration system.
- One player sequence can include multiple enemies. Their accepted hits accumulate together; defeating one target does not end the combo while another participant remains in a valid reaction state.
- Punches, launcher, air attacks, dive and upgrade damage resolved through the same hurtbox path share the tracker. Starting or finishing an attack animation does not itself start or end a combo.

## Continuing and ending

Every qualifying hit starts or refreshes a **0.9-second** maximum follow-up window. The timer uses the 60 Hz combat clock, pauses with combat pause, and does not consume attacker hitstop. It is a gap between hits, not a maximum total combo duration.

The combo ends on the earliest of:

- The follow-up timer expires.
- All living hit targets recover to `EnemyHitReaction.CanAct` (normal, grounded, able to act).
- All hit targets die, disappear or become inactive. Finalization happens after the frame's hit batch, so simultaneous lethal hits can all contribute.
- The player takes damage, dies or is interrupted, including interruption without damage from a parry.

Air hitstun, launch, falling, bounce, knockdown and get-up remain valid reaction states until recovery or timeout. This does not grant permission to hit otherwise invulnerable or unhittable targets. Ground/air eligibility remains defined by the attack data.

Final results remain available on the tracker until a new combo or reset. Stage positioning/reset, health restore and player disable clear the counter and HUD. `BestHitCount` is a debug best for that player object's lifetime, not saved progression.

## Editing in Unity

1. In Project, open `Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab`.
2. Select the prefab root and expand **Combo Tracker**:
   - **Timeout Seconds**: maximum gap between qualifying hits (default 0.9).
   - **Debug Log**: print combo starts and ends with their reason.
3. Expand **Combo UI Controller**:
   - **Final Hold Seconds**: hold final count/damage before fading (0.8).
   - **Fade Seconds**: fade duration (0.35).
   - **Hit Pop Seconds**: duration of the subtle 8% pop (0.12; zero disables it).
   - **Top Right Inset**: X/Y distance from the upper-right edge (24/24 reference pixels). Increase X to move left; increase Y to move down.
   - **Hit Color**: normal count color. Six or more hits receive orange/red emphasis.
4. Press Play and land hits. The upper-right screen-space panel shows large `N HITS` and smaller `DAMAGE`. It starts hidden, updates on hits and holds/fades on combo end. It does not intercept pointer input. Reference resolution is 1920×1080 with automatic canvas scaling.
5. Select the live player in Hierarchy and inspect **Combo Tracker** to see active state, hits/damage, remaining seconds, last target, best hits and last end reason. **Reset combo display** clears the current result for testing. Play-mode changes are temporary; edit the prefab outside Play to persist tuning.

## Validation

Validated in Unity 6000.4.6f1 using an isolated project copy, preserving the open editor and current stage data:

- Ground punch playtest: **293 assertions**. Actual Punch1 → Punch2 → Punch3 reports **3 hits / 31 damage**.
- Air punch playtest: **393 assertions**. Actual Punch1 → Punch2 → Launcher → Jump → AirCombo1 → AirCombo2 → AirCombo3 remains one six-hit combo, facing both directions and with slow rendering.
- Combo tracking/HUD: **25 checks** covering whiffs, duplicate active frames, hitstop, timeout, recovery, interruption, pause, lethal overkill, multiple targets, stage reset, actual dive follow-up and HUD hold/fade. The qualifying dive follow-up reports **2 hits / 22 damage**.

Total: **711 passed checks**. Results are in `Documentation/ComboTrackingValidationResults.txt`, `PunchPlaytestValidationResults.txt` and `AirPunchPlaytestValidationResults.txt`. `ComboTrackingPreview/ComboHUD.png` is a captured HUD preview with test actors and scenery hidden for clarity.

Run `Beat Em Up > Validate combo tracking and HUD (Play Mode)` for the focused validation. It opens the haunted-house scene, enters Play, creates temporary test actors and exits Play afterward; use outside an active play session and save any scene edits first. Existing ground/air validation menu entries now assert combo counts too.

## Implementation files

- New runtime scripts: `ComboTracker.cs`, `ComboUIController.cs` under `Assets/EQ_Rung_BeatEmUp/Scripts/Combat`.
- Runtime integration: `CombatHurtbox.cs` (accepted damage), `ComboController.cs` (legacy-player setup and interruption), `CharacterMotor.cs` (stage reset).
- Prefab: `Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab` (serialized tracker/UI settings).
- Editor: `Assets/Editor/Combat/ComboTrackerEditor.cs`, `ComboTrackingValidation.cs`, `ComboTrackingSetup.cs` (prefab setup utility); extended `PunchPlaytestValidation.cs` and `AirPunchPlaytestValidation.cs`.

No attack timing, damage balance, stage art, movement bounds or encounter data changes are part of this combo task.
