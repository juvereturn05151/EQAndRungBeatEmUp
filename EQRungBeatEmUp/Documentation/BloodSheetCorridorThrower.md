# Blood Sheet Corridor upward extension and notebook throw

## Background

The new `Stage02_BloodSheetCorridor_Background_v2.png` is 720×240 pixels, extending the original 720×160 plate upward by 80 rows. The new upper space adds dark timber ceiling framing, wires, supporting columns and restrained red/amber bulbs in the existing haunted fair palette. The original lower 160 rows are byte-for-byte identical after decoding, including the lower floor seam. The old PNG and its metadata remain available.

BloodSheetCorridor's level entry now references v2. Its existing Art Width **11.33** is unchanged. Background Height changes **2.1 → 3.15** and Background Center Y **1.91 → 2.435**: the bottom remains **Y=0.86**, and all extra height extends upward. Floor sprite, floor placement/size, movement bounds, encounter configuration, stage order and camera settings are unchanged. The small existing overlap with the floor is preserved.

The art preview prefab uses v2 at its native 100 PPU dimensions; its background center shifts 2.56 → 2.96 to preserve its own original floor seam. The manifest and new versioned Source/Preview composition images describe the expanded 720×480 background-plus-floor canvas. Legacy Source/Preview files remain intact.

To inspect: open `HauntedHouse.unity`, select `ThaiHauntedHouse.asset`, choose **Blood Sheet Corridoor** in your existing stage list and open Scene view. The live stage editor displays the taller art. Adjust the existing **Background Height / Background Center Y** fields together if you later want another composition; center = bottom seam + height / 2 keeps the seam fixed.

## Thrower

Previously `Thrower_Throw_Notebook.asset` used its throw drawings with a melee hitbox. There was no projectile implementation. It now retains its existing six drawings and 40-frame animation, clears the fake melee hitboxes, and releases a real notebook on **frame 18**, the first extended-arm release drawing (`_04`). The anticipation lasts 18 combat frames (0.30s at 60 FPS). A `Swing` event plays the existing library's `punch_long_whoosh_21.wav` at volume 0.28 on release.

`EnemyCombat` keeps its normal detection, lane alignment, animation and cooldown logic. The Thrower prefab's range is now **1.25–6 world units**, with lane tolerance **0.55**. Beyond maximum range it approaches; when too close it backs away; once aligned it stops and plays the throw. The new minimum-range field defaults to zero for other enemy types. Existing cooldown remains **110 enemy frames + 30 attack frames after recovery**, approximately 2.33s between completed throws before the next windup. This prevents per-frame projectile spam without changing other enemies' cooldown behavior.

`EnemyProjectileAttack` subscribes to the existing `AttackPlayer` events. `ThrowProjectile` releases only once per attack, only while the Thrower can act and has a living target. Interrupted windups release nothing. The projectile starts at facing-relative X=0.45 and height=0.78 above the enemy's lane. It aims toward the player's current root XY ground position at release, ignores jump height and keeps a constant release height above the walking plane; it does not home afterward. Thrower may still start and finish throws against airborne players. The notebook hits grounded players only; see `ThrowerGroundPlane.md` for the correction and tests.

`NotebookProjectile.prefab` reuses the six existing Effects/Notebook sprites. It moves at **5.5 units/second**, rotates through those drawings every **3 combat frames**, and has a swept collision radius of **0.14**. It deals **4 damage**, **14 hitstun frames**, **3 hitstop frames**, with normal hit/guard/parry/dodge behavior through `CombatHurtbox.Receive`. Walking-lane tolerance and actual hurtbox height are respected. Enemy-team hurtboxes are ignored. Swept circle queries prevent fast throws from jumping over a narrow target between frames; no second damage architecture or physics-dependent animation events are used.

Accepted damaging contact or a block consumes the projectile once. A valid parry can deflect it according to its configured deflection settings. Solid obstacles consume it too; misses expire after **120 combat frames**. Combat pause freezes projectile movement and lifetime. Stage-spawned projectiles belong to the runtime room rather than the individual Thrower, so defeating the enemy does not erase its airborne notebook and changing stages removes old-room projectiles. Rendering follows lane sorting. Existing player damage/hit-blink feedback supplies contact readability.

## Tune in Unity

1. Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Thrower.prefab`.
2. In **EnemyCombat**, tune **Attack Range**, **Minimum Attack Range**, **Lane Range**, or **Attack Cooldown Frames**. Stage flow supplies the player target when spawning it.
3. In **EnemyProjectileAttack**, tune **Release Offset**, or replace **Projectile Prefab**.
4. Select `NotebookProjectile.prefab` to tune **Speed**, **Lifetime Frames**, **Collision Radius**, **Hit**, or **Flight Sprites**. Damage/hit reactions use the normal combat hit settings.
5. Select `Thrower_Throw_Notebook.asset` to edit its 40-frame timeline. Keep exactly one **ThrowProjectile** event on the intended release drawing. The **Swing** event and **Feedback** settings control the release sound.
6. Use the existing **Preview Encounter** workflow to inspect the Thrower's stage placement. Press Play to see actual projectile travel; static encounter previews do not run AI or projectiles.

## Changed files

- New art: `Background/Stage02_BloodSheetCorridor_Background_v2.png`, versioned Source/Preview PNGs and metadata.
- Updated art pipeline: the corridor manifest and `Unity/Stage02_BloodSheetCorridor_ArtPreview.prefab`.
- Updated level asset: only the corridor background reference, height and center for this task.
- Updated Thrower gameplay prefab and `Thrower_Throw_Notebook.asset`.
- New `NotebookProjectile.prefab` and metadata.
- New runtime scripts: `EnemyProjectileAttack.cs`, `CombatProjectile.cs`; updated `EnemyCombat.cs` with optional minimum range.
- New editor scripts: `ThrowerProjectileSetup.cs` for repeatable configuration and `ThrowerProjectileValidation.cs` for focused tests, each with metadata. Setup writes only the named Thrower/projectile assets; it does not rebuild the level or scene.
- `Tools/HauntedStages/BloodSheetExtension/` records packaging, generation provenance, composed preview and exact-pixel validation. The package command is a one-time migration and refuses an already revised level reference.

Built-in imagegen was used, with the original background as the edit target. The exact final prompt is saved in `Tools/HauntedStages/BloodSheetExtension/Prompt.txt`; generated image path and packaging dimensions are recorded in `generation.json`. Packaging retained the original lower pixels independently of the generated edit.

## Validation

Unity 6000.4.6f1 compiled and ran the focused tests in an isolated project copy, without closing the user's editor. **36 checks passed**, covering retained drawings/release timing, AI range/lane behavior, left/right aiming, origin and movement timing, player damage/hitstun/hitstop, cooldown, interruption, pause, timeout, swept wall collision, guard, actual authored corridor encounter spawning/damage, art dimensions/seam and room cleanup. See `ThrowerProjectileValidationResults.txt`.

Pixel validation confirms every original lower pixel is unchanged; width stays 720 and 80 upper rows were added. The finished background was visually inspected. Subjective gameplay feel remains adjustable through the exposed prefab/AttackData fields.
