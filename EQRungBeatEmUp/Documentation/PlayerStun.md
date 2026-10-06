# Player Stunned status

The Screamer's directional wave now applies **Hit Type = Stun**, with **Stun Duration Frames = 90** (1.5 seconds at 60 combat FPS, excluding hitstop/pause). Damage, wave geometry, telegraph, committed direction, recovery and cooldown are unchanged.

`CombatState.Stunned` belongs to the existing ComboController state machine. Normal attacks retain the separate `CombatState.Hitstun` and Hitstun Frames. The new enum members are appended so existing serialized reaction/state values retain their meaning. There is no attacker-type check, extra player state component, or disabled GameObject/input map. Any authored melee hitbox or projectile can select Stun and set its duration.

Stun interrupts the current action/combo, clears buffered attack/jump inputs, ends guard/parry, locks movement, and rejects attack, launcher, jump, guard, dodge and skill requests. The player remains vulnerable. Ordinary hits still damage the player but do not clear the stronger status. Another Stun refreshes to the longer of its duration and the remaining status; it cannot shorten the status. A launcher/knockdown or lethal hit overrides Stunned and clears the overhead effect. Existing downed/get-up immunity/reaction rules are retained.

Grounded players stand and wobble. Air actions are canceled while natural gravity and vertical movement continue. Landing preserves the remaining status time; the counter does not restart. If time expires before landing, action lock remains until grounded recovery. Hitstop and global pause freeze the pose/VFX/status clock. Restore, death, overrides and actor disable clean up the owned VFX and animation control.

## Artwork and frame data

The five new BlueShirtGuy poses use the existing Idle2 sprite sheet as reference, with the original blue patterned short sleeve shirt, face, dark trousers, sneakers, proportions and right-facing perspective. Native sprites are 128 × 128 with 100 pixels/unit, point filtering, no compression/mipmaps and the same foot pivot (0.5, 0.0625). Feet align at pixel 120; first pose is 106 pixels tall. Body colors map to the existing Idle2 palette.

The separate six-frame overhead effect contains gold stars orbiting a subtle mint spectral ring. Native VFX frames are 64 × 32, center pivot, 100 pixels/unit. Runtime attaches the effect to the player's existing visual transform, follows jump height, mirrors with facing and sorts two orders above the player. It has no collider or damage authority.

Both loops are authored in `Assets/EQ_Rung_BeatEmUp/PlayerDefense.asset`: **Stunned** contains five poses, six combat frames per pose; **Stun Vfx** contains six frames, five frames per hold. Both loops repeat every 30 combat frames. CharacterAnimation.HoldSprite gives the combat clock ownership of body playback; recovery restores the normal Animator. Stun Vfx Offset and Scale remain editable.

Assets: `Assets/ArtAssets/Characters/BlueShirtGuy/Stun`. Source generated sheets and native review strips are in `Source`. Built-in imagegen created the art; [Prompts.md](../Tools/PlayerStun/Prompts.md) records both complete prompts. [export-sprites.cjs](../Tools/PlayerStun/export-sprites.cjs) reproducibly crops, nearest-scales, maps body colors and aligns feet.

## Editing and validation

- Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/ScreamWaveProjectile.prefab`, expand **Hit**, set **Hit Type = Stun** and tune **Stun Duration Frames**. Suggested range: 60–120. Hitstun Frames continues to control ordinary Normal reactions.
- The existing Frame Attack Editor also exposes Stun Duration Frames in each hitbox. No offensive hitboxes are added to the stun pose data.
- Select PlayerDefense.asset to tune pose holds and VFX. In Play Mode, the ComboController Inspector shows Player state and remaining stun frames.
- **Beat Em Up > Configure player stun artwork** imports/assigns the generated poses/VFX, applies the 90-frame Screamer defaults, and rebuilds the multiplayer catalog. This resets these defaults; manual tuning is otherwise preserved.
- **Beat Em Up > Enemies > Validate Screamer (Play Mode)** exercises wave geometry, status entry, duration, animation/VFX loops, rejected actions, standing/airborne recovery, repeat-stun policy, normal-hit vulnerability, override/death/restore/disable cleanup, pause, missing cosmetic data, catalog membership and existing aimed Thrower behavior. Results: ScreamerValidationResults.txt; in-game capture: ScreamerPreview/Stunned.png.

Existing multiplayer ownership remains unchanged: the host processes damage/state and rejects gameplay commands through the same ComboController methods. Sprite snapshots already carry all enabled body/VFX renderers and the state's name; the catalog includes the new sprites. Each player's effect is owned by its own visual transform. Separate online peer transport is not rerun by this suite.

Verified in Unity 6000.4.6f1: 233 scream/projectile/status checks and 272 existing player-defense checks passed. Native body/VFX strips and the actual in-game Stunned capture were visually inspected.
