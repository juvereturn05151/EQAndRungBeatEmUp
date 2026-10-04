# Frame combat authoring and migration report

The attack timeline now follows `AttackData.frames -> AttackPlayer -> SpriteRenderer / hitboxes / movement / cancels`. Each list entry is one combat frame, numbered from zero. There is no seconds-based attack duration, startup timer, cancel timer, Animator attack playback, normalized-time hitbox check, or Animation Event timing.

## What changed

Created runtime scripts: `Assets/Scripts/Combat/AttackPlayer.cs`, `CombatClock.cs`. Created editor script: `Assets/Editor/AttackDataEditor.cs`, with their Unity `.meta` files.

Modified runtime scripts: `AttackData.cs`, `AttackHitbox.cs`, `CharacterAnimation.cs`, `CharacterMotor.cs`, `CombatHurtbox.cs`, `ComboController.cs`, `EnemyHitReaction.cs`, `EnemyCombat.cs`, `CombatDebugOverlay.cs`. Modified editor scripts: `CombatDemoBuilder.cs`, `CombatValidation.cs`.

Migrated the two prefabs in `Assets/EQ_Rung_BeatEmUp/Prefabs`, the two controllers in `Assets/EQ_Rung_BeatEmUp/Animations`, eight attack assets in `Assets/EQ_Rung_BeatEmUp/Attacks`, and `Assets/Scenes/ComboDemo.unity`. The scene now contains a **Combat Clock** object with **Combat FPS = 60**. Existing asset GUIDs and prefab file IDs were retained.

The one-time, idempotent conversion is recorded in `Documentation/Tools/MigrateFrameCombat.py`. It reads the old asset timing, rounds it to integer frames at 60 FPS, and writes the frame lists. It is a migration tool, not a second runtime timing system. Do not use it to author new attacks.

Preserved the New Input System asset and the user's private serialized input references. `Move`, `Attack`, `Launcher`, and `Jump` still flow through `PlayerCombatInput`. `CharacterHealth` and the existing separate `BoxCollider2D` hurtboxes remain. Walking uses root X/Y; jump height remains a separate value applied to the Visual child.

## Attack library

| Asset | Frames | Active frames (inclusive) | Artwork |
| --- | ---: | --- | --- |
| Punch1 / Punch2 / Punch3 | 26 each | 6-12 | Existing Attack1 sprites |
| Launcher | 26 | 6-12 | Existing Launch1 sprites |
| AirPunch1 / AirPunch2 / AirPunch3 | 26 each | 6-12 | Existing AirAttack1 sprites |
| EnemyPunch | 48 | 12-16 | Existing ThaiBadBoy Attack1 sprites |

Punch1-3 share the available punch artwork; AirPunch1-3 share the available air artwork. Their assets remain independent and can receive distinct sprites later. No artwork was generated. Multiple combat frames deliberately repeat a sprite. All existing attack frames have sprite references.

The player controller retains Idle, Walk, Jumping, and GroundHit. The enemy controller retains locomotion and hit/launch/landing/death reactions. Punch1-3, Launcher, AirPunch1-3, and the enemy Attack state were removed from those runtime controllers. During attack playback the Animator is disabled so it cannot overwrite the frame's sprite; it resumes for locomotion/reactions after playback. Source artwork controllers and old clip assets were left available but do not drive attacks.

## Frame and hitbox data

`AttackFrameData` contains a sprite, a list of zero or more `AttackHitboxData`, entry movement, optional horizontal and vertical velocities, vertical velocity modifier, gravity scale, suspend-falling flag, three cancel permissions, invulnerability, super armor, and optional named entry signals. Signals are C# `AttackPlayer.FrameEvent` callbacks, not Animation Events. Subscribe from a separate effects/audio component if needed.

Movement is applied once when entering the frame. `movement.x` is forward displacement and mirrors when facing left; `movement.y` moves in the walking lane. Horizontal velocity is in world units per second and is mirrored by facing; vertical velocity operates on jump height. Gravity scale and suspend-falling apply while that frame is current. The clock internally supplies elapsed seconds to the existing motor physics; attack authoring and combat deadlines are integer frames.

Each hitbox has local offset X/Y, width/height, lane tolerance, damage, hitstun frames, hitstop frames, knockback, launch velocity X/Y, hit type, grounded/airborne eligibility, hit ID, and optional repeat interval. Offset Y is relative to the character's feet plus jump height; lane tolerance compares the two root walking-lane Y values.

Author all sprites, offsets, movement, and knockback for facing right. Playback locks facing for an attack, flips the sprite, mirrors hitbox X and forward movement, and mirrors outgoing knockback/launch X. Turning while idle affects the next attack.

The same hit ID across frames normally damages an enemy once for the whole attack instance. Different IDs permit deliberate multiple hits. A positive `repeatAfterFrames` explicitly allows a repeat after that many attack frames. Hit history resets when a new attack starts. Adjacent active frames each supply their own hitbox list; previous-frame boxes are replaced, rather than kept alive accidentally.

Hitstop freezes both characters' frame playback, movement, reaction animation, and hitstun countdown for the specified number of combat ticks. Buffered input can still be received during the freeze; it waits until playback resumes. Invulnerability rejects damage. Super armor accepts damage and hitstop without interrupting the attack, unless the hit kills the character.

Hurtboxes remain separate. Frame defense permissions are read through the current frame; future per-frame hurtbox shapes can be added alongside those fields without changing attack timing.

## Edit sprites and hitboxes

1. Open **Beat Em Up > Edit Punch1 frame data**, or select an asset in `Assets/EQ_Rung_BeatEmUp/Attacks`.
2. Use **Previous**, **Next**, or **Combat frame (zero based)** to select a frame.
3. Drag a Sprite asset into that frame's **Sprite** slot. Repeating the same sprite across several frames is intentional. Empty frames show an artwork warning.
4. Expand **Hitboxes**. Set its count to zero, one, or more. Expand an element to edit its properties.
5. Enable **Scene view preview**, then click **Focus preview in Scene view**. Switch to the Scene tab.
6. The frame sprite is displayed with yellow attack boxes and a cyan hurtbox. Drag a yellow center handle to move a box; drag its edges to resize it. Changes write back to this frame in the selected asset and support Undo.
7. Optionally assign BlueShirtGuy's **CharacterMotor** to **Preview relative to character** to position the preview at that character. Without a reference, preview uses the origin and the demo's default hurtbox size. The reference character's actual sprite is not changed by the editor preview.
8. Enable **Preview facing left** to inspect mirrored geometry. Edits still save in canonical right-facing coordinates.
9. Save the project after editing. Enable `debugDraw` on runtime `AttackHitbox` / `CombatHurtbox` to see yellow/cyan Gizmos during Play Mode.

The Scene view preview is temporary, is excluded from the Game camera, and is removed when its Inspector closes. The move and resize handles were also exercised in the main editor, then undone to preserve the authored values. **Preview Attack** loops the sprite and hitbox preview at the scene combat FPS. It is visual authoring playback; it does not simulate combat hits, attack movement, or AI.

## Frame editing buttons

- **Add Frame**: append an empty frame and select it.
- **Insert**: insert an empty frame before the selected frame.
- **Duplicate**: insert a deep copy immediately after the selection and select it. Hitboxes are independent copies.
- **Duplicate Previous Frame**: insert a copy of the preceding frame at the selection.
- **Delete**: remove the selected frame. Following frames shift their indices.

Sprites and hitbox settings belong to each frame, so inserting a frame naturally shifts all later timing. The Inspector derives total frames, first/last active frame, startup, active-frame count, and remaining recovery from the list. Gaps between active frames are possible; active count is the number of frames containing boxes, not the entire span.

## Cancels and buffering

Tick the appropriate **Can Cancel Into Attack**, **Can Cancel Into Launcher**, or **Can Cancel Into Jump** on individual frames. The combo controller never checks a separate cancel-time interval.

The ground route remains Punch1 -> Punch2 -> Punch3. Launcher can branch after two ground punches. Launcher recovery frames allow a manual Jump; the launcher never jumps automatically. After a manual jump, manual Attack presses advance AirPunch1 -> AirPunch2 -> AirPunch3. AirPunch3 closes the juggle. Landing resets the air route; its three attacks cannot restart indefinitely before landing.

The demo's ordinary attacks allow Attack/Launcher cancels beginning at frame 11. Launcher permits Jump from frame 13. Route rules still determine whether a permitted cancel has a valid destination: enabling Launcher cancel on Punch1 does not bypass the configured two-punch route requirement.

`ComboController` exposes input buffer (21f), jump buffer (36f), combo reset (21f), and terminal recovery (7f). There is one pending attack/launcher request, preventing an unbounded mash queue; the newest request replaces it. Jump has its own buffer. A press on frame 3 can wait for a legal cancel on frame 4. An expired request never activates later. Buffers and recovery pause during hitstop. Holding a button does not create new requests.

## Combat FPS and a new attack

Select **Combat Clock** in ComboDemo and edit **Combat FPS**. All characters share that clock. A missing clock is automatically created at 60 FPS at runtime; other scenes can have an explicit CombatClock object too. Use only one active clock per scene. Attack assets store frame lists, rather than competing per-attack FPS values. Raising combat FPS speeds up the same authored frame sequence; it does not resample the sprites.

Create an asset through **Assets > Create > Beat Em Up > Frame Attack**. Give it a name and ground/air domain. Add frames, assign existing sprites, add hitboxes only on desired frames, and enable specific cancel permissions. Configure hit type and launch velocity on a launcher hitbox. The asset's **Is Launcher** flag selects the launcher route's state/terminal behavior; the hitbox's **Hit Type** controls what happens to the target. Add optional movement and defense properties.

Assign the asset to `ComboController.groundCombo`, `airCombo`, or `launcher` to use it in the player's configured routes, or to `EnemyCombat.attack`. A separate ability can call `AttackPlayer.Play(asset)` directly; it should still obey the character's state/route rules. AttackPlayer does not contain Punch1-specific behavior. No Animator state or clip is required.

## Validation and exact Unity test steps

### Ground and wall bounces

Bounce properties belong to `AttackHitboxData` on the active attack frame. Copying frames/hitboxes also copies their bounce settings. Tune them in the attack inspector or Attack Data Editor timeline; timing continues to use CombatClock and pauses during hitstop.

- **AirPunch3** active hitboxes enable **Force Airborne Target Downward** and **Ground Bounce**. Existing **Launch Velocity Y = -8** supplies slam speed; **Knockback** supplies horizontal recoil. Any airborne hit type can use the downward flag. A negative Launch Velocity Y is the downward speed; a nonnegative value uses the enemy's Finisher Fall Speed.
- **Ground Bounce Force** defaults to X = 0.6 (mirrored by attack facing), Y = 4.5 (upward height velocity). **Ground Bounce Gravity = 18** controls the rebound arc, and **Ground Bounce Recovery Frames = 21** controls bounce hitstun and the Downed delay after the final landing's Knockdown animation. GetUp then returns the enemy to neutral. Floor contact uses the existing motor `Height = 0` ground plane, independent of walking lane Y. Only a pending marked hit consumes that contact and launches upward; the final landing and ordinary falling use Knockdown/Downed/GetUp.
- **Punch3** active hitboxes enable **Wall Bounce**, retaining strong **Knockback = 5**. The rebound defaults to horizontal force 4 away from the wall, upward height force 4, and 24 combat frames of hitstun. Wall-bounce eligibility expires when the hit's recovery reaches zero; a new hit or parry replaces pending eligibility.
- **Maximum Ground Bounces / Maximum Wall Bounces** on each hitbox and **Max Ground Bounces / Max Wall Bounces** on each enemy both default to 1. The smaller limit wins. Usage persists across follow-up hits, relaunches, and landing recovery. Fully grounded neutral or health restore resets both counters. A rebound opens a fresh, bounded juggle window using the existing juggle duration/hit caps.

**CombatWall** marks a solid `Collider2D` (or a parent containing wall colliders). Turn off **Allows Bounce** for a wall that blocks movement without allowing rebound. Triggers, disabled components, ordinary unmarked colliders, and arena/screen bounds do not trigger wall bounce. CharacterMotor sweeps its **Wall Collision Size / Offset** through ground/lane XY using **Wall Collision Mask**, preventing fast knockback from tunneling through marked walls. Walls span jump heights because airborne height is separate from lane position. Keep wall colliders within reach of the motor's arena bounds.

ComboDemo contains two sample wall colliders at the world arena ends. **Beat Em Up > Add sample combat walls to demo** can recreate them in a demo with no CombatWalls. Enable Scene gizmos to see magenta bounce walls and gray walls with bouncing disabled. **Combat Debug > Show Debug** displays state, eligibility, usage, horizontal recoil, height velocity, and the last hit reaction.

Priority is death/recovery protection, incoming hit, ground-bounce eligibility (when both bounce types are marked on an airborne hit), wall eligibility, falling, then final landing. Ground contact that activates a bounce does not also start landing recovery. After the rebound, floor contact enters Knockdown immediately, then Downed and GetUp. Grounded follow-up hits cannot replace that recovery with a hurt reaction. Wall-bounce landings and ordinary unmarked airborne reactions also use Knockdown/Downed/GetUp; wall bounce uses the enemy's normal downed delay.

Use **Beat Em Up > Validate combat bounces (Play Mode)** for both input devices, the two authored routes, real wall contact in both directions, high-speed sweeps, hitstop, combo caps/reset, normal movement/falling, trigger rejection, opted-out walls, and death/restore. It writes `BounceCombatValidationResults.txt` at the project root. Save unrelated scene work first: this command opens ComboDemo and runs temporary Play Mode fixtures. Updated air playtest and enemy recovery checks cover the new bounce route alongside existing recovery behavior.

Bounce implementation validated in Unity 6000.4.6f1 in the isolated validation project: **699 assertions passed** across bounce combat (59), air punch playtest (271), enemy recovery (164), and ground punch playtest (205). Current reports are `Documentation/BounceCombatValidationResults.txt`, `AirPunchPlaytestValidationResults.txt`, `EnemyRecoveryValidationResults.txt`, and `PunchPlaytestValidationResults.txt`. Ground and air routes were checked with keyboard/gamepad, both attack directions, and slow rendered frames; recovery and authored hitstop remain on the shared combat clock.

Historical migration validation ran in Unity 6000.4.6f1 Play Mode in an isolated copy at `E:/EQRungBeatEmUp/FrameCombatValidation`, using paired virtual keyboard/mouse and gamepad devices. **64 assertions passed**, including both manual routes, buffering, expired and missed cancel windows, holding/mashing, mirroring, lane rejection, once-per-ID and intentional repeated hits, air juggling/landing, interruption, hitstop, armor/invulnerability, exact sprite ownership, slow/variable rendered deltas, configurable FPS, entry events/movement, and editor add/insert/duplicate/delete/save behavior. Historical results are in `Documentation/FrameCombatValidationResults.txt`; that older suite still has fixed expectations for pre-playtest damage/timing and is not the current bounce validation command.

1. Open `Assets/Scenes/ComboDemo.unity`. Confirm the Console has no compile errors, the Combat Clock has 60 FPS, and BlueShirtGuy and BadGuy each have AttackPlayer.
2. Enter Play Mode and click the Game view. Move with WASD; jump with Space. Confirm the original walking/jumping feel and lane movement.
3. Tap J three times, with each next press shortly before or during the cancel window. Confirm Punch1 -> Punch2 -> Punch3 and one hit per move. The debug overlay shows the current attack frame and hitstop count.
4. Restart Play Mode. Tap J, J, K. Confirm BadGuy launches while BlueShirtGuy stays grounded. Then press Space manually and J, J, J to perform the three air punches. Confirm BadGuy falls, lands, and recovers.
5. Repeat using gamepad West for Attack, North for Launcher, South for Jump, and the left stick for movement.
6. Hold J: only one attack should start. Mash J: the bounded buffer should still settle after release. Wait beyond combo reset before pressing again: the next route starts at Punch1.
7. Face left, approach BadGuy from the right, and attack. Confirm the sprite and hitbox mirror. Move BadGuy to a different walking lane and verify that lane tolerance rejects hits.
8. Select an attack asset in Edit Mode. Scrub an active frame (Punch1 frame 6), enable Scene preview, move/resize a box, Undo, and change a sprite. Check that only the selected frame changes, then save.
9. Set a hitbox's Hitstop Frames to 5. Watch the debug frame stay fixed through impact. Set Hitstun Frames to a different integer and check the enemy recovery. Restore your intended tuning after experimentation.
10. To test incoming interruption, disable BadGuy's **Passive Training Dummy** option. Its EnemyPunch uses the same frame runtime and should interrupt the player unless the current frame has armor/invulnerability.
11. Test with low rendered FPS (for example target 15 FPS) and normal/high FPS. Intermediate combat frames must still execute and damage must not multiply. Configure Combat FPS independently of render FPS.
12. For the full repeatable suite, use **Beat Em Up > Validate frame combat (Play Mode)**. It refreshes prefab references/removes attack states, opens ComboDemo, temporarily creates virtual devices, runs the suite, writes `CombatValidationResults.txt` at the project root, and exits Play Mode. Save unrelated scene work first because the suite opens ComboDemo. The isolated batch run exits the validation editor, not the user's editor.

The editor also offers **Refresh frame combat setup** to reconnect the existing prefabs and remove attack states from their controllers. It preserves scenes and existing attack-frame tuning rather than recreating the demo.
