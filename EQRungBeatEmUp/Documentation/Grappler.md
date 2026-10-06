# Grappler armor and grab

The existing GrapplerBruiser prefab now uses hit-count armor and a telegraphed grab, with the existing EnemyCombat/AI, EnemyHitReaction, AttackPlayer, CombatClock and player ComboController. It moves at 1.3 units/second and has 200 HP so normal six-hit tests do not kill it before armor breaks. The old heavy attack asset remains available but is no longer its primary choice.

## Armor

Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/GrapplerBruiser.prefab`, then **Hit Count Armor**:

| Setting | Default |
|---|---|
| Max Armor Hits | 6 |
| Armor Break Stun Frames | 60 |
| Armor Recovery Delay Frames | 300 (5 seconds at 60 FPS) |
| Restore Full Armor | true |
| Hitstop Frames | 2 |

Each accepted damaging combat hit consumes one point. Existing hit IDs/repeat-hit history prevent a sustained hitbox or extra hurtbox colliders from consuming extra points. Rejected, invulnerable, paused, or zero-damage hits do not consume armor. Damage and combo/meter tracking still apply. First five hits produce **Armor** outcomes, a gold body flash and a metallic hit effect/sound, without hitstun, recoil or action cancellation. The sixth produces **ArmorBreak**, a cyan flash, larger effect and a different heavy sound, stops the attack and enters the existing GroundHit stagger for 60 frames. This overrides per-frame super-armor on the break hit. Later hits use normal reactions.

The recovery delay restarts after each accepted damaging hit, including hits during vulnerability. Its combat-frame counter pauses with hitstop/global pause. Armor restores only after the delay, while alive, able to act and outside an attack. Restore Full Armor replenishes all six points; false adds one point per delay. The existing health Restore/reset replenishes armor immediately for a new life/stage. Armor is kept throughout grab startup; breaking it cancels the pending grab. Breaking armor during a hold also releases the victim, providing a simple foundation for future rescue mechanics.

Armor hit and break feedback are separate AttackData feedback assets, reused by the existing VFX/audio and multiplayer cue path. They use the existing parry impact prefab at different scales and `metal_punch_06.wav` / `metal_punch_finisher_07.wav`. The ordinary hit blink is stopped on armored contacts so the gold/cyan feedback remains visible.

## Frame-data grab

Open `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Attacks/Grappler_Grab.asset` in the **Frame Attack Editor**:

| Frames (zero-based) | Behavior |
|---|---|
| 0–29 | 30-frame stationary telegraph using existing Grab_Start sprites; feet plant, arms open, forward reach |
| 26 | LockGrabDirection captures the target XY position and normalized launch direction |
| 30–47 | 18-frame committed lunge; StartGrabLunge at 30 and movement scale 1 |
| 31–46 | 16 active frames with traveling Grab Hitboxes; EnableGrabHitbox at 31 |
| 47 | DisableGrabHitbox; last movement frame |
| 48–89 | StopGrabLunge and 42-frame vulnerable miss recovery |
| 90 | Miss finishes; normal AI cooldown/recovery applies |

Grab volumes are separate from damage hitboxes. The Frame Attack Editor displays **Dedicated Grab Hitboxes (ground plane)** and a **Grab volumes** timeline track. Default center is `(0.65, 0)`, full horizontal width 1.2 and full lane depth 0.8. Offset X advances the box center along the committed XY direction; offset Y adds a world lane offset. Width and depth remain a simple axis-aligned rectangle on the ground plane. Candidates behind the movement path are excluded. Depth is ±0.4 around the traveling box center. The volume is swept between combat-frame positions to avoid fast-motion tunneling. Edit all 16 active frames, using duplicate/copy/paste for consistent geometry. Grounded targets are enabled; airborne targets are disabled. If enabled, airborne capture snaps cleanly to the grounded anchor.

During early telegraph the sprite may face the player. LockGrabDirection at frame 26 stores TargetPositionAtCommit and the normalized ground-plane XY direction, then commits left/right sprite facing. Neither the direction nor facing tracks after commitment. Pure depth movement stays on the walking plane; character jump Height remains unchanged. Active grabs do not track or home. Sidestepping, moving behind/out of range and the existing dodge immunity avoid capture. Guard/parry do not block a valid grab; this is a capture volume rather than a normal hit.

The **Combat Grab Controller** component assigns the grab attack, successful branch, Grab Damage (0), Slam Hit (24 damage, KnockDown, 45 downed frames, 4 hitstop), Maximum Grab Frames watchdog (360), GrabAnchor and debug drawing. Hold/miss recovery and lunge lengths are displayed live in the existing AI Inspector and authored by frame data.

Tune **Grab Lunge Speed** (8 units/second), **Grab Lunge Distance** (2.5-unit cap), and **Grab Acceleration** (0 = instant speed; positive values ramp in units/second squared) on Combat Grab Controller. Walk speed is 1.3. The Frame Attack Editor exposes **Committed grab movement scale** per frame, and its timeline shows movement separately from grab volumes. The default 18 movement frames travel 2.4 units at 60 FPS; increasing speed or reducing the cap can end travel earlier. Resize the movement section and move the Start/StopGrabLunge and grab events to tune Grab Lunge Frames.

Capture stops movement immediately and branches into the hold. Distance exhaustion, duration end, or blocking environment collision stops movement, disables the grab and enters the frame-48 miss tail. CharacterMotor uses the existing CombatWall collision and arena bounds; walls must have a solid collider and CombatWall configuration, as for other combat movement. Wall contact has the same 42-frame recovery initially. If the player dodges after commitment, the Grappler can overshoot the original target position. Armor remains active throughout; breaking it immediately cancels the lunge and grab volume.

## Successful branch and Grabbed

Successful capture branches to `Grappler_HoldSlam.asset` through AttackPlayer; there is no coroutine timer:

| Frames | Behavior / events |
|---|---|
| 0 | DisableGrabHitbox, AttachGrabbedTarget, ApplyGrabDamage |
| 0–29 | 30-frame hold using existing Grab_Hold sprites |
| 30 | ThrowTarget then ReleaseTarget: release attachment and apply the separate slam hit through CombatHurtbox |
| 30–35 | Existing BodySlam sprites |
| 36–53 | Recovery sprites |
| 54 | Branch finishes; cooldown is attributed to the original grab choice |

Move ThrowTarget/ReleaseTarget together to change **Hold Frames**; resize/move the miss recovery tail to change **Grab Miss Recovery Frames**. The default AI cooldown is 90 attack frames + 1 second choice cooldown = 2.5 seconds, with 0.25 seconds additional AI recovery. Interrupted casts also consume cooldown, so an armor break cannot immediately restart a grab.

`CombatState.Grabbed` is appended to the existing state enum. Capture cancels current actions, clears combat/jump buffers, ends guard/parry and rejects movement, attack, launcher, jump, guard, parry, dodge and skill. New Input System bindings/device ownership remain active; the existing state checks reject gameplay actions. It records **GrabOwner** on the player and **CurrentGrabbedTarget** on the owner. A player cannot be acquired twice and an owner cannot hold two players. Other players retain normal control.

GrabAnchor is a direct child of GrapplerBruiser, authored at `(0.7, 0, 0)` for right facing. Runtime mirrors its X offset with the committed facing. The player stays at the resulting ground position every combat frame, without parenting the gameplay root or drifting through velocity/physics. Snap alignment does not reset combo statistics. Move this child to tune the hold spacing; its Y is the walking-lane offset.

Targets are rejected while already grabbed, knocked down/downed/getting up, dead, externally or frame-invulnerable, dodge-invulnerable, safe-stage protected, disabled, or marked **Ungrabbable** on ComboController. Airborne permission belongs to the active GrabHitboxData. Stunned players remain vulnerable to a grab.

ThrowTarget first clears both ownership links, then uses the existing KnockDown → Downed → GetUp → Idle flow. Normal damaging hits can still affect a held player; stronger reactions can detach them. Owner death, attack interruption, controller/actor disable, victim death/restore and the maximum-frame watchdog clear attachment. Missing/failed successful-branch data also releases safely. Grabbed is reusable with another actor carrying CombatGrabController; player code does not check for the Grappler class/name.

## Art status

Grappler telegraph, launch/rushing reach, hold, slam and recovery reuse existing project sprites; the lunge uses the forward-reaching Grab_Start poses. No replacement Grappler art was needed. PlayerDefense.asset now has a **Grabbed** pose list.

`Assets/ArtAssets/Characters/BlueShirtGuy/Animations/Grabbed/BlueShirtGuy_Grabbed_PLACEHOLDER.png` is explicitly a **temporary held-reference pose**, copied from BlueShirtGuy's existing raised-arm Parry_02 sprite with the existing size/pivot/filtering. It is not Idle and is not claimed as final grabbed art. Replace this sprite or assign a final grabbed sequence in PlayerDefense.asset; timing and gameplay need no rewrite.

## Unity test steps

1. Stop Play Mode. Let Unity compile. Defaults are already configured. **Beat Em Up > Enemies > Configure Grappler defaults** reapplies the above values and refreshes the multiplayer catalog; rerunning it resets tuning.
2. Run **Beat Em Up > Enemies > Validate Grappler (Play Mode)**. Read `Documentation/GrapplerValidationResults.txt`; Unity returns to Edit Mode. This tests armor, break/regeneration, both facings, lane/reach boundaries, telegraph/active/miss timing, dodge, attachment, input rejection, independent players, slam/get-up, invalid targets, interruption/death/restore/watchdog cleanup, catalog references and AI cooldown.
3. For a manual fight, add **GrapplerBruiser.prefab** to a combat encounter in the current Level Editor and simulate a combat stage. Keep EnemyCombat enabled, Passive Training Dummy off and GrapplerBruiserAIProfile assigned. Hub/safe-stage protection intentionally prevents damage/capture.
4. Enable the prefab/component **Debug Draw** and Scene-view Gizmos. Select the live enemy; the existing AI Inspector displays Armor remaining/max, active/broken, restoration timer, telegraph/current frame, grab-active flag, current target, anchor and hold/miss lengths. The cyan/red wire box shows grab reach/depth. The yellow line shows remaining committed travel. Live fields include captured target position, locked direction, lunge active, distance remaining and wall stop.
5. Land five distinct hits: HP falls, armor counts 5 → 1, gold flash/metallic feedback appears and startup continues. Land the sixth before frame 30: cyan break feedback, attack canceled, 60-frame stagger. Combo the vulnerable enemy; wait five combat seconds after the last hit outside an attack to observe regeneration.
6. Let a fresh grab telegraph. After frame 26 commits, sidestep outside ±0.4 of the traveling box lane, move behind, retreat beyond the traveling volume or dodge. Verify 16 active grab frames during the rush followed by long miss recovery and cooldown.
7. Stay in front on the same lane: Grabbed and held reference pose, exact mirrored anchor, 30-frame hold, one 24-damage slam, knockdown/get-up/control recovery. Repeat facing left. Try all action buttons during the hold; they must not execute.
8. Put a solid BoxCollider2D wall with CombatWall in the rush path. Let the lunge hit it; verify no penetration, no active grab, and the same 42-frame miss recovery. Move the player between lanes during early startup, then after commitment: early movement changes the chosen XY direction; movement after frame 26 must not bend the path. Test a target directly above/below on the walking plane and verify the Grappler stays grounded.
9. With a second local player, confirm only one target is held, the partner can act normally, and breaking the remaining armor during the hold releases the victim. Kill/disable the owner during a hold and confirm the player is released.

Multiplayer remains host authoritative. The existing state/sprite snapshots carry Grabbed poses, attachment positions and armor flash colors; accepted armor feedback gets catalog-backed cues. The catalog includes both grab attacks, armor feedback and held pose. Independent online peer transport is not launched by this validator.

## Files

Created: runtime `HitCountArmor.cs`, `CombatGrabController.cs`, `ComboController.Grab.cs`; editor `GrapplerSetup.cs`, `GrapplerValidation.cs`; attack assets `Grappler_Grab`, `Grappler_HoldSlam`, `Grappler_ArmorHitFeedback`, `Grappler_ArmorBreakFeedback`; the marked BlueShirtGuy grabbed placeholder; this guide/results, plus Unity meta files.

Modified: `AttackData.cs` (Grabbed enum, dedicated grab data/events/cooldown option), `CombatHurtbox.cs` (accepted armor path), `PlayerDefenseData.cs` (pose/outcomes), `ComboController.cs` / `.Defense.cs` (grab control/cleanup), `CharacterMotor.cs` (capture alignment), `AttackPlayer.cs` (committed facing and frame seek for early miss recovery), `EnemyCombat.cs` (interrupted/branch cooldown), `RunBuildState.cs` (accepted armor hits retain upgrade accounting), `MultiplayerSession.cs` (armor cues), `AttackDataEditorWindow.cs`, `AttackTimelineGUI.cs`, `EnemyAIProfileEditor.cs`, GrapplerBruiser prefab/profile, PlayerDefense.asset and MultiplayerCatalog.asset.

## Verified results

The Unity Play Mode validator passed 163 assertions for armor, capture/state cleanup, active-volume geometry, AI/cooldown, XY commitment, acceleration, overshoot, hitstop, distance limits and CombatWall blocking. See GrapplerValidationResults.txt and GrapplerPreview (Telegraph, Lunge, Grabbed). Regression validation also passed 272 player-defense checks and 233 Screamer checks. Independent online peer transport remains untested.
