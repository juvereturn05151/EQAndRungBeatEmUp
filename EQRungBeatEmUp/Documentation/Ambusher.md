# Ambusher leap / face grab

Ambusher.prefab now uses the existing EnemyCombat, EnemyAIProfile, AttackPlayer, CombatClock, CharacterMotor and reusable CombatGrabController / ComboController.Grabbed. Its old LegTrip asset remains available. It has no hit-count armor or frame super-armor: ordinary hits interrupt startup and held sequences.

## Authored timing (zero-based combat frames)

Open `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Attacks/Ambusher_LeapGrab.asset` in the existing Frame Attack Editor.

| Frames | Behavior |
|---|---|
| 0–19 | Stationary crouch / deep crouch telegraph; early facing may adjust |
| 17 | LockGrabDirection stores the explicit target Transform, target ground XY and normalized direction |
| 20 | StartGrabLeap begins the committed arc |
| 20–49 | Takeoff, rise, airborne, descent/reach; ground XY and separate air Height progress across 30 frames |
| 45–49 | EnableGrabHitbox and dedicated traveling grab boxes, only during final descent |
| 50 | DisableGrabHitbox / LandGrabLeap |
| 50–79 | 30-frame vulnerable miss recovery |
| 80 | Miss ends; normal AI cooldown applies |

No target updates change the trajectory after frame17. Sideways, diagonal and pure-depth leaps use XY; Z stays on the game plane. Height is a separate visual/air value, as with existing jumps. Arc height is `4 * LeapHeight * t * (1 - t)`. The root travels linearly from origin toward the locked position, limited by configured distance and speed over the duration. The existing motor's wall/boundary resolution is applied each step. Hitstop freezes both parts. Ordinary airborne interruption restores normal falling rather than leaving the authored arc active.

## Prefab tuning

Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Ambusher.prefab`, then Combat Grab Controller:

| Setting | Default |
|---|---|
| Leap Enabled | true |
| Leap Horizontal Speed | 10 units/sec (maximum travel speed) |
| Leap Distance | 4 units (cap) |
| Leap Height | 1.6 units |
| Leap Duration Frames | 30 |
| Face Attack Count | 3 |
| Face Hit Damage | 8 per strike |
| Face Hit Hitstop Frames | 2 |
| Grab Damage | 0 |
| Slam Hit Damage | 0, KnockDown reaction |
| Slam Hit Knockdown Duration Frames | 45 |
| Maximum Grab Frames | 360 watchdog |

Actual travel speed is locked target distance divided by duration when below the speed cap. Tune duration and move the LandGrabLeap / active-box frames together when changing the airborne section. Default steering is zero; no homing correction is implemented. Miss Recovery Frames are authored by the length of the landing/recovery tail, displayed in the AI Inspector.

Grab boxes default to offset0.2 along locked direction, full width0.9, full lane depth0.7 (±0.35) and maximum air-height difference0.75. Grounded targets are permitted; airborne targets default off. The existing swept rectangle prevents fast-motion tunneling. The final descent/height gate prevents capturing a grounded player high above them. Edit all five active frames when changing box dimensions. Offset Y is a world-lane offset. No damage hitbox substitutes for capture.

The AI profile exposes **Use Ground Plane Range**, enabled only for Ambusher initially. Minimum leap range1.25, maximum4, preferred distance1.25, lane tolerance4, walk speed2.6. Too close retreats, too far approaches, medium distance may use the leap. Range checks use XY magnitude so direct depth targets work. The existing AI remains committed through its authored airborne attack while damage still interrupts it. Leap AttackData cooldown120 frames plus Primary choice cooldown1 second =3 seconds at60 FPS, including interrupted casts, with0.25 seconds additional AI recovery.

## Successful capture / face attacks

Capture immediately stops the leap and disables its grab, grounds the owner and reuses **CombatState.Grabbed**. The victim snaps to the mirrored direct-child GrabAnchor at `(0.55,-0.25,0)` (slightly lower lane offset aligns the held head with the crouched punching pose) and follows it every combat frame. Move that child to adjust holding spacing. The player cannot move, attack, launch, jump, guard, parry, dodge or cast a skill. Input bindings remain active; state rules reject actions. One owner holds one player, one player has one owner; partners keep control.

Open `Ambusher_GrabSuccess.asset`:

| Frames | Behavior |
|---|---|
| 0 | DisableGrabHitbox, AttachGrabbedTarget, ApplyGrabDamage (0 default) |
| 0–5 | Hold / strike windup |
| 6 | FaceStrike: separate damage, impact sprite, VFX/SFX and hitstop |
| 7–15 | Impact return / held recoil |
| 16 | RepeatFaceAttacks: if fewer than Face Attack Count were applied, seek frame5 to repeat the authored impact/return section |
| 18 | ThrowTarget and ReleaseTarget: clear ownership and apply existing KnockDown, with zero additional damage by default |
| 21–37 | Enemy recovery |
| 38 | Successful branch ends; cooldown attributed to original leap choice |

Default three impacts each deal8 damage (24 total); no damage-over-time timer or coroutine. Adjust **Face Attack Count** on the controller. Counts1 and4 are also validated. Move FaceStrike and RepeatFaceAttacks events to tune the loop; impact timing remains synchronized to the selected sprite. Each impact uses the success AttackData's existing parry impact prefab at scale0.1 and metal_punch_06 sound, plus2 combat-frame hitstop. The player briefly shows an existing Stunned hurt pose, then returns to the existing Grabbed pose. The impact frame can be reassigned on the controller.

Release uses the current KnockDown → Downed → GetUp → normal-control flow. Death during a face strike stops the sequence, detaches and uses the normal Die state. Enemy interruption/death/disable, player restore/death, invalid branch and watchdog also clear ownership. A partner's ordinary hit can interrupt this unarmored owner and release the victim, preserving future rescue support.

## Art

Created twelve transparent128x128 sprites in `Assets/ArtAssets/Characters/Enemies/Ambusher/Animations/LeapGrab`: Telegraph, DeepCrouch, Takeoff, Rising, Airborne, DescendingReach, Hold, Windup, FaceImpact, ReturnHold, Release and Landing. They were generated with the built-in imagegen tool using the existing PopOut sheet as the identity/style reference. Imported point-filtered at100 PPU with consistent foot pivot. The existing Ambusher sprites are preserved. `Source/LeapGrab/GeneratedSheet.png` contains the source; `Tools/Ambusher/Prompts.md` records the exact prompt, and `export-sprites.cjs` reproduces the cell exports.

The BlueShirtGuy grabbed pose remains the existing explicitly marked **BlueShirtGuy_Grabbed_PLACEHOLDER** from the reusable grab implementation. Face impacts reuse an existing Stunned pose. Replace either cosmetic assignment later without changing gameplay; no new BlueShirtGuy art is claimed.

## Unity tests

1. Stop Play Mode and let scripts compile. Defaults are already applied. **Beat Em Up → Enemies → Configure Ambusher defaults** resets all settings above and refreshes the existing multiplayer catalog.
2. Run **Beat Em Up → Enemies → Validate Ambusher (Play Mode)**. Unity returns to Edit Mode and writes `Documentation/AmbusherValidationResults.txt`. Captured renderings are in `Documentation/AmbusherPreview`.
3. Add Ambusher.prefab to a combat encounter in the existing Level Editor and simulate a combat stage. Keep EnemyCombat enabled, Passive Training Dummy off, and AmbusherAIProfile assigned. Safe hub protection intentionally prevents combat/capture.
4. Stand at medium distance. Observe the crouch warning, locked direction near its end, airborne arc and descending grab attempt. Repeat left/right, diagonally and directly above/below on the walking plane.
5. Move during early telegraph to change its commitment. Move after frame17, sidestep beyond the traveling box depth, dodge during descent, move behind or retreat out of reach. Verify the leap stays committed, lands and has30 frames of miss recovery plus cooldown.
6. Hit the Ambusher during telegraph: it enters normal hitstun and cancels the attack. Hit during a hold to release the player. It has no Grappler armor.
7. Let it capture you: verify mirrored anchor, all action buttons blocked, three separate8-damage impacts with feedback, release/knockdown/get-up and restored control. Change Face Attack Count to1 or4 and check damage matches.
8. Place a solid collider with CombatWall in its path. Verify no clipping, leap abort/landing and full miss recovery. Repeat near arena bounds.
9. Kill/disable the Ambusher during a hold; kill the player during a face strike; confirm attachment clears. With a second local player verify only one victim is held and the partner retains control.
10. Enable Debug Draw / Scene Gizmos and select the live enemy. Existing AI Inspector shows state/frame, target, committed XY, leap direction/destination, grab-active/volume, held target and strike count. Yellow gizmos show the arc and destination.

Multiplayer keeps existing host authority. Sprite/state/position snapshots carry arc, held poses and attachment. Held-strike feedback uses explicit per-strike catalog-backed cues. Separate online peers are not launched by these validators.

## File inventory

Created: runtime `CombatGrabController.Leap.cs`; editor `AmbusherSetup.cs`, `AmbusherValidation.cs`; `Ambusher_LeapGrab.asset`, `Ambusher_GrabSuccess.asset`; twelve LeapGrab sprite PNGs and generated source sheet; Tools/Ambusher export and prompt files; this guide, validation results/previews and Unity meta files.

Modified: `CombatGrabController.cs` (optional leap / height-aware capture / locked player), `ComboController.Grab.cs` (temporary held impact pose), `CharacterMotor.cs` (authored air height), `AttackData.cs` (grab-height tolerance), `EnemyAIProfile.cs`, `EnemyAIController.cs` (optional XY ranges and committed airborne attacks), `EnemyAIProfileEditor.cs` (range option / leap debug), `MultiplayerSession.cs` (held strike cues), Ambusher prefab/profile and existing MultiplayerCatalog.asset. ProjectEditorGuide.md lists the new tuning/menu entries.

## Validation results

Unity Play Mode validation passed 131 Ambusher assertions. Shared-system regressions passed 163 Grappler, 272 player-defense and 233 Screamer assertions. These include interruptibility, locked XY travel/height, descent-only capture, wall stops, dodge/sidestep, ownership/action gates, configured1/3/4 strike counts, synchronized damage/feedback/hitstop, owner/victim death, partner control and AI cooldown. Separate online peers remain untested.
