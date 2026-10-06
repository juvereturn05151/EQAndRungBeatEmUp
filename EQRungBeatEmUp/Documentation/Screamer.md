# Screamer directional scream wave

The scream is now a **forward-moving rectangular wave**, released from the Screamer's mouth. There are no radial scream hitboxes and no expanding ring. Moving behind the Screamer or outside the wave's walking-lane depth avoids damage.

The existing AttackPlayer, enemy AI, EnemyProjectileAttack, CombatProjectile and feedback/audio hooks are used. The player's existing state machine now includes a distinct Stunned status with dedicated dizzy poses and overhead stars; ordinary Hitstun remains separate. Frame data still controls the entire cast. The shared projectile performs authoritative swept ground-plane collision through CombatHurtbox.Receive; no circular query is used for this wave.

## Defaults at 60 combat FPS

| Setting | Value | Edit |
|---|---|---|
| Telegraph | Frames 0–35, 36 frames | Screamer_Scream_Wave.asset |
| Scream pose | Frames 36–43, 8 frames | Attack asset |
| Recovery | Frames 44–73, 30 frames | Attack asset |
| Release | SpawnScreamWave at frame 36 | Attack frame events |
| Spawn offset | Forward X 0.65, mouth height 0.65 | EnemyProjectileAttack on Screamer prefab |
| Wave speed | 6 units/second | ScreamWaveProjectile.prefab / CombatProjectile |
| Wave lifetime | 60 combat frames | Projectile prefab |
| Wave width | 1.2 units along horizontal travel | Wave Size X |
| Wave depth | Full 1.2 units; ±0.6 from launch lane | Wave Size Y |
| Maximum travel distance | 5 units from spawn center | Maximum Travel Distance |
| Damage | 14, once per player per wave | Projectile / Hit |
| Reaction | Immediate Stunned status; interrupts attacks and locks actions/movement | Projectile / Hit Type = Stun |
| Stun duration | 90 combat frames (1.5s), paused during hitstop | Stun Duration Frames |
| Airborne reaction | Natural jump/fall continues; no forced downward slam | Force Airborne Target Downward = off |
| Hitstop | 4 frames | Projectile / Hit |
| Piercing | All opposing players; once each | Maximum Targets = 0, Repeat Hit Frames = 0 |
| Cooldown | 120 frames + AI choice 0.5s = 2.5s | Attack asset + AI profile |
| AI additional recovery | 0.15s | AI profile |
| AI cast distance | Within 2.5 horizontal units and 0.6 lane depth | Primary attack choice |
| Armor / invulnerability | None | Existing frames |
| Guard response | Unblockable; dodge immunity still applies | Projectile / Hit |

Distance limits the wave center; its forward edge extends half the configured width beyond that center. Lifetime and distance whichever expires first end the wave. Its own hitstop pauses travel/age, and global pause freezes it. The released wave can continue during the Screamer's recovery. Interrupting startup cancels the pending release; interrupting after release does not recall a traveling wave.

## Fair direction and readable visuals

The AI faces the target immediately before starting the attack. AttackPlayer captures that facing **at the beginning of telegraph** and the committed profile prevents further target-facing updates during the cast. Locking it early makes the full warning readable; crossing behind before frame 36 does not change the shot. There is no homing or lane tracking after launch. One facing sign mirrors spawn X, travel, the warning arrow and the complete crescent arrangement.

The warning is an orange rectangular ground corridor with a forward arrow, entirely in front of the source. Its length describes the complete projected travel threat and its depth matches Wave Size Y. Frame 36 removes the warning and releases three red forward-facing crescents. These animate using the existing six ScreamWave sprites, travel together, and resize to the gameplay width/depth. No mirrored crescent travels behind the source and no radial boundary remains. The wave disappears when its travel/lifetime ends.

Collision samples a swept rectangle between successive ground positions to prevent tunneling. Horizontal width and walking-lane depth use actor ground positions; jumping does not incorrectly change the lane. Valid contacts enter Stunned, clear attacks and buffered inputs, and prevent attacks, skills, jump, guard and dodge until recovery. Airborne contacts preserve natural vertical movement and landing; landing does not clear remaining stun. Repeated Stun preserves the longer remaining duration. Normal hits still damage the player and do not clear the status. Surviving scream hits do not enter KnockDown, Downed or GetUp; lethal damage still follows the existing death rules. Once-per-health history prevents multiple hurtbox colliders or repeated overlap from causing duplicate damage. Team, immunity, pause, safe-stage protection and existing defense rules remain in the shared damage path. The default wave ignores scenery and expires by distance/lifetime. See [PlayerStun.md](PlayerStun.md) for animation/VFX authoring and status behavior.

## Editing in Unity

1. Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Screamer.prefab`. **EnemyProjectileAttack** exposes Release Offset, Release Event, Forward Only and Projectile Prefab.
2. Select `Prefabs/ScreamWaveProjectile.prefab` in that same folder. **CombatProjectile** exposes Speed, Lifetime Frames, Wave Size, Maximum Travel Distance, Hit data, repeat/piercing rules and the original sprite sequence. Wave Size Y is the **full** depth, not a radius. Collision Radius belongs to other projectile modes and is ignored when Ground Wave is enabled.
3. Open `Attacks/Screamer_Scream_Wave.asset` in the existing Frame Attack Editor. Frame 0 has Telegraph; frame 36 has Scream and SpawnScreamWave. Move the release event to change timing. **Active Event / Event Active Frames** describe the eight-frame scream pose for timeline phase counters without introducing melee hitboxes. Update those counters if changing the scream phase.
4. Expand Feedback to edit Directional Wave Warning, warning color/arrow sprite, Telegraph Sound, Scream Sound and volume. Separate voice hooks remain unassigned because the supplied library contains impacts/whooshes rather than a suitable scream recording. Assign clips here. Frame events play each sound once and cancellation stops charging audio.
5. The existing Enemy AI Editor edits ScreamerAIProfile, its committed cast, attack eligibility, depth and cooldown. Keep AI depth at half Wave Size Y if you want eligibility to match the collision band.
6. **Beat Em Up > Enemies > Configure Screamer defaults** rebuilds this configuration and refreshes the existing multiplayer catalog. Defaults are already applied. Running it again resets these tuning values.

## Tests

Outside Play Mode, use **Beat Em Up > Enemies > Validate Screamer (Play Mode)**. The existing validation menu checks directional geometry in both facings, lane boundaries, behind safety, movement/limits, commit direction, interruptions, startup/scream/recovery, immediate ground/air stun, stun duration and input locks, once-only damage, immunity, pause, cooldown, audio cues and catalog references. It writes `Documentation/ScreamerValidationResults.txt` and renders updated `Documentation/ScreamerPreview` captures, including `Stunned.png`.

Verified in Unity 6000.4.6f1: **233 scream/projectile/status checks and 272 existing player-defense checks passed**. These cover stun duration, action locks, recovery, preserved airborne movement, guarded hits, dedicated animation/VFX, vulnerability and cleanup, plus real Thrower release and aimed notebook damage in both facings. Current results are recorded in `ScreamerValidationResults.txt` and `PlayerDefenseValidationResults.txt`. Native artwork and the in-game Stunned capture were visually inspected.

For a manual fight, add the existing Screamer prefab to a combat encounter with the current level editor and simulate the encounter. Keep its AI profile assigned and Passive Training Dummy off. Test in a combat stage; the hub/shrine's existing safety intentionally prevents damage. Approach on the same lane, observe the forward warning, move behind or sidestep before firing, then repeat standing in the traveling wave's path. Verify 14 damage and Stunned → Idle, with standing dizzy poses and overhead stars for 90 combat frames. Try a jump, attack, timed dodge and interrupting the telegraph. Facing left must show the same mirrored behavior.

The existing catalog includes the warning texture and crescent sprites. Hosts own damage; clients display authoritative sprite snapshots and warning/scream/cancellation audio cues. Separate online peer transport is not rerun by this suite.

## Changed files

Runtime: `CombatProjectile.cs` adds the optional swept ground wave and size-driven crescent layout; `EnemyProjectileAttack.cs` adds named release/forward-only spawning; `AttackFeedback.cs` adds the directional warning; `AttackData.cs` adds event-based phase counters and the warning option; `AttackPlayer.cs`, `EnemyCombat.cs`, and `MultiplayerSession.cs` recognize the new warning for attachment/cancellation/cooldown. Existing aimed notebook and player dragon modes keep their defaults.

Authoring: `ScreamerSetup.cs`, `ScreamerValidation.cs`, `AttackDataEditorWindow.cs`, new `CombatProjectileEditor.cs` with explicit Wave Speed / Lifetime / Width / Depth controls, Screamer attack/profile/prefab, and refreshed MultiplayerCatalog. New assets: `ScreamWaveProjectile.prefab` and `ScreamDirectionWarning.png`, with Unity meta files. The old ScreamAreaRing texture is no longer referenced by the Screamer; it is retained as an unused asset. This guide, ProjectEditorGuide and the validation/previews describe the directional implementation.
