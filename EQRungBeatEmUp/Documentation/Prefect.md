# Prefect support / Rusher backup

The existing Prefect prefab now uses the same EnemyCombat / editable EnemyAIProfile / AttackPlayer / CombatClock as other enemies. PrefectSupport is a trait and frame-event adapter, not a second AI or spawner. Backup uses StageFlowController's same enemy instantiation and wave tracking as authored encounters. Prefect has 45 HP, no armor and low defensive damage; her threat grows while she survives.

## States and priority

Normal AI states: **Wait → CallBackup → Recovery → Wait**, and **Wait → Push → Retreat → Wait**. The existing Hurt, Knockdown, GetUp and Dead roles still override normal decisions.

1. Handle death / hit reaction / knockdown.
2. At emergency range with push cooldown ready and a valid lane, use Push.
3. If closer than Retreat Distance, retreat.
4. With call cooldown ready, valid Rusher prefab, enough spawn slots and sufficient player distance, call backup.
5. Otherwise wait at a useful distance.

Nearest-living targeting identifies the immediate threat. XY range checks include depth. A target close in another lane causes retreat rather than trapping the AI in an unusable push state. Actions commit to their frame timelines; ordinary player hits can interrupt startup. Completing or interrupting either attack consumes its own cooldown.

## Call timing and art

Open `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Attacks/Prefect_CallBackup.asset` in the Frame Attack Editor:

| Combat frames (zero-based) | Behavior |
|---|---|
| 0–11 | Stop / raised-hand authority pose, existing Command frame 5 |
| 12–23 | Existing Whistle_Call warning poses |
| 24 | **CallBackup** event requests Rushers immediately |
| 25–41 | Call tail / Recovery poses |
| 42 | Attack finishes |

Default **Rushers Per Call = 2**. Move CallBackup to tune the spawn frame; edit sprites/tail to tune warning and recovery. No coroutine or queued callback survives interruption. A hit before frame24 cancels the call and spawns nothing. A hit after activation cannot undo already-spawned Rushers. Backup cooldown is360 AttackData frames +2 seconds AI choice cooldown = **8 seconds at60 FPS**, including interrupted calls. Profile recovery adds0.25 seconds.

The assigned Rusher prefab is `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Rusher.prefab`. EnemyCombat.Role is marked Rusher on that existing prefab. The authoritative reinforcement API rejects any other role, including a mistakenly assigned Ambusher/Prefect/etc. No duplicate Rusher logic is created; spawned Rushers receive normal AI, target and encounter movement bounds.

## Shared encounter spawning and caps

There previously was no active-enemy cap. The existing LevelDefinition now exposes:

- Stage / room **Max Active Enemies**, default12.
- Each encounter **Max Active Enemies**, default8.

Available slots are the smaller remaining room/encounter capacity. Both authored wave plans and Prefect backup use these limits. Authored plans wait until a slot opens; they retain pending count/delay state. Backup spawns only the number of slots available right now: one free slot produces one Rusher, zero produces none. Multiple Prefects share the same live counts, checked after each spawn. There is no delayed backup queue that can later overflow the cap.

Backup requires the caller to be an alive, tracked member of a started, unfinished encounter/wave in a combat stage. A prefab placed outside tracked encounters has no backup slots; add it through the Level Editor's enemy spawn list. Safe stages and non-authoritative clients cannot summon.

StageFlow prefers authored spawn points from the caller's encounter, preferring positions far from the caller and projected into valid current combat/room bounds. If those are occupied/blocked, it tries perimeter entrance positions. Every candidate must fit the Rusher motor footprint inside bounds, remain at least1.25 units from every living player and1.25 from living enemies (including Prefect), and avoid solid CombatWall geometry. If no valid point exists, it spawns fewer or none. No optional delayed spawn marker was added. Rushers become members of the caller's original wave and room runtime hierarchy, so they count toward HUD totals, locks, completion and room cleanup. Killing Prefect leaves live backup to defeat; killing the remaining Rushers clears normally.

## Retreat and tuning

Select Prefect.prefab's EnemyCombat and click **Open Enemy AI Editor**, with the Prefect assigned in the Enemy field. The existing AI Inspector/window now has direct **Prefect support tuning** controls. You can also edit PrefectSupport / CharacterMotor / PrefectAIProfile components/assets directly.

| Setting | Default / location |
|---|---|
| Preferred Distance | 3.5, AI profile |
| Retreat Distance | 3, PrefectSupport |
| Emergency Push Distance | 1.05, PrefectSupport (must also fit Push choice range/lane) |
| Minimum Call Distance | 3.2, PrefectSupport (also respects CallBackup choice range) |
| Retreat Speed | 2.8 units/sec, CharacterMotor.Move Speed |
| Rushers Per Call | 2, PrefectSupport |
| Player / Enemy spawn clearance | 1.25 /1.25, PrefectSupport |
| Call Backup cooldown | 360 frames on CallBackup +2s profile choice |
| Push cooldown | 90 frames on PushAttack +0.5s profile choice |

Retreat starts below3 and continues until3.5, giving hysteresis rather than constant direction changes. Direction starts away from the nearest threat on XY. The motor probes candidate directions rotated0, ±45, ±90, ±135 and180 degrees against the same CombatWall casts and arena bounds used for real movement. It selects a free direction that increases distance. Tangent/lane escape can avoid a blocked ideal direction; when no increasing-distance route exists, she waits/can push rather than clipping outside the arena. Retreat stops once sufficient space exists.

## Defensive push

Open `Prefect_PushAttack.asset`:

| Frames | Behavior |
|---|---|
| 0–5 | Readable baton windup |
| 6–8 | Forward short-range push hitbox, shared hit ID0 |
| 9–27 | Recovery |
| 28 | Finish, retreat /2-second cooldown |

Defaults: damage4, normal hitstun18 frames, hitstop3 frames, knockback6 units/sec. It does **not** knock down. Existing baton art communicates a forceful defensive strike. Box offset `(0.55,0.5)`, size `(1,0.9)` and lane tolerance0.45 define local front reach/depth; mirrored by committed facing. It only hits overlapping valid player hurtboxes, with the normal accepted-hit history (one hit per cast), guard/parry/dodge rules and existing VFX/SFX. It never pushes all room players globally.

**Outward Ground Knockback** is a new optional normal-hit flag in the existing Frame Attack Editor. Accepted hits compute normalized XY from Prefect to victim and apply Knockback through CharacterMotor's existing recoil; Height remains separate. All existing attacks retain their previous horizontal recoil unless this flag is enabled. Enemy armor / invulnerability / defense still use the existing accepted-hit path. Change **Push Knockback** directly in the AI tuning panel to update all active push hitboxes, or edit Knockback and the outward flag in the attack editor. Damage, Hitstun Frames, Hitstop Frames, geometry and recovery remain frame-data settings.

## Artwork and feedback

No new or replacement PNGs were necessary. Existing Prefect **Command**, **Whistle_Call**, **BatonAttack** and **Recovery** frames are configured into the new call/push attacks. Existing idle/walk/hurt/knockdown art is preserved. Push uses the existing impact prefab at scale0.1 and metal_punch_06 sound. There is no dedicated whistle audio clip assigned; the existing Telegraph Sound field can accept one. Call warning readability comes from the hand-up/whistle poses.

## Debug / multiplayer

Enable PrefectSupport.Debug Draw and Scene Gizmos. The AI Inspector shows state/frame/target, preferred/retreat/emergency ranges, call/push readiness and cooldown, last call spawn count, current encounter/Rusher totals, available slots and selected retreat direction. Gizmos show ranges, chosen escape and candidate spawn points. The usual attack-hitbox gizmos show push range/depth.

StageFlow remains host authoritative. Backup is tracked as ordinary enemies, so the existing enemy prefab/position/sprite snapshots replicate it. Call/push attacks are catalog-backed. Independent online peers were not launched by these validators.

## Exact Unity testing steps

1. Stop Play Mode and allow scripts to compile. Defaults are already configured. **Beat Em Up → Enemies → Configure Prefect defaults** reapplies tuning, marks the existing Rusher role and refreshes the catalog; rerunning resets tuning.
2. Run **Beat Em Up → Enemies → Validate Prefect (Play Mode)**. Unity returns to Edit Mode and writes `Documentation/PrefectValidationResults.txt`; rendered previews are in `Documentation/PrefectPreview`.
3. In the current Level Editor, add Prefect.prefab to a combat encounter's enemy spawn list and assign sensible entrance spawn points. Set stage/encounter Max Active Enemies. Simulate that encounter. Keep EnemyCombat enabled, Passive Training Dummy off, and PrefectAIProfile assigned.
4. Stay beyond3.2 units: observe raised-hand/whistle warning, two Rushers appearing only at frame24, recovery and8-second cooldown. Watch real encounter/Rusher counts and confirm new enemies use Rusher AI.
5. Reach and hit Prefect before frame24: verify normal hitstun, no spawned Rushers later and cooldown. Kill her after a completed call: encounter remains locked until backup is defeated, then clears normally.
6. Approach within3: verify retreat on both horizontal and depth axes. At3.5 she stops fleeing. Chase toward a room edge / solid CombatWall and verify a valid tangent escape when available and no boundary clipping.
7. Enter push range on the same lane: windup6 frames, one4-damage hit, outward displacement, short Hitstun and recovery without Knockdown. Repeat facing left and at a slight depth offset. Guard/parry/dodge use normal defenses. A player well outside the local box should remain unaffected.
8. Set encounter cap to caller count +1: the call produces only one Rusher. Set it to caller count: no backup call is eligible. Add two Prefects and verify simultaneous casts cannot exceed room/encounter caps. Put a CombatWall/players at entrance points and confirm invalid positions are skipped.
9. With multiple players, approach with the nearest player and confirm she retreats from the immediate threat; other players are pushed only when their hurtbox overlaps the short-range push.

## File inventory

Created: runtime `PrefectSupport.cs`, `StageFlowController.Reinforcements.cs`; editor `PrefectSetup.cs`, `PrefectValidation.cs`; attacks `Prefect_CallBackup.asset`, `Prefect_PushAttack.asset`; this guide, validation results/previews and Unity meta files.

Modified: `StageFlowController.cs` (shared instantiation/caps), `LevelDefinition.cs` (room/encounter caps), `EnemyCombat.cs` (explicit Rusher role), `EnemyAIProfile.cs` / `EnemyAIController.cs` (support conditions / retreat adapter), `CharacterMotor.cs` (side-effect-free movement probe / XY recoil), `AttackData.cs` / `CombatHurtbox.cs` (optional outward normal hit), `EnemyAIProfileEditor.cs` (support tuning/debug), `AttackDataEditorWindow.cs` (outward flag), Prefect prefab/profile, existing Rusher prefab, ThaiHauntedHouse.asset (serialized cap defaults), MultiplayerCatalog.asset and ProjectEditorGuide.md. Existing sprite PNGs were not modified.

## Verified results

Unity Play Mode validation passed105 Prefect assertions, including real wave tracking/completion, typed Rusher spawning, single/full/shared caps, pending authored waves, combat-zone bounds, frame timing/interruption, safe placement, normal push defenses, outward recoil, XY retreat/blocked routes, support priority and separate cooldowns. Shared regressions passed272 player-defense,131 Ambusher,233 Screamer and163 Grappler assertions. Separate online peer transport remains untested.
