# AirCombo3 two-handed hammer-smash redraw

This report records the original redraw's 30-frame timing. The later [combat readability revision](CombatReadability.md) retains these drawings and extends the attack to 36 frames (active 10–17), with sound/VFX hooks. Use that guide for current timings.

AirCombo3 uses the existing `AirPunch3.asset` finisher. Its new dedicated sprites are under `Assets/ArtAssets/Characters/BlueShirtGuy/AirCombo3`. They replace the previous one-arm-looking attack drawings only in this finisher's frame data; shared AirAttack1 sprites, AirPunch1 and AirPunch2 remain intact.

The redraw follows the existing BlueShirtGuy references: short brown hair, blue patterned shirt, navy trousers, gray/white sneakers, dark pixel outlines and compact airborne proportions. Joined hands, the overhead silhouette, forward/downward shoulder drive and restrained blue-white motion accents make the finisher easier to read. The impact remains airborne; it has no baked floor contact or debris. Its feet trail the strike rather than delivering a kick.

| New drawing | Combat frames (zero-based) | Intent |
| --- | --- | --- |
| AirReady | 0–2 | Compact airborne setup |
| HandsTogether | 3–4 | Clasp hands and compress |
| OverheadWindup | 5–7 | Raise both hands overhead |
| SmashStart | 8–10 | Commit the joined hands downward/forward; active hitboxes |
| Impact | 11–13 | Strong double-hand follow-through; active hitboxes |
| Recovery | 14–29 | Retract hands into airborne balance |

Six drawings now replace the five unique drawings previously used by AirPunch3 (the old five included a shared recovery sprite). Combat frame count remains 30, preserving the slower 0.5-second whiff duration. Existing hitboxes, active frames 8–13, damage, hitstop, gravity, movement, combo permissions and bounce/knockdown behavior remain unchanged.

## Files

- `Source.png`: full-resolution generated 3×2 transparent sheet.
- `BlueShirtGuy_AirCombo3_*.png`: six individually imported 128×128 sprites, Point filtering, 100 pixels per unit, original bottom-oriented pivot `(0.5, 0.0625)`.
- `NativeSheet.png` and `Review.png`: native sheet and enlarged nearest-neighbor review.
- `AirCombo3_Preview.gif`: animated preview of the actual combat-frame pose holds, without hitstop. GIF timing rounds to its supported hundredths of a second.
- `Assets/EQ_Rung_BeatEmUp/Attacks/AirPunch3.asset`: new sprite references and artwork notes.
- `Tools/AirCombo3/prompt.txt`: final prompt; built-in image_gen edit mode.
- `Tools/AirCombo3/package.cjs` and `generation.json`: reproducible export/hookup workflow, source and frame registration. The export downsamples with nearest-neighbor and converts alpha to the existing crisp binary sprite convention.

Open Tools → Combat → Attack Data Editor and select AirPunch3 to review its timeline. The original AirAttack1 Aseprite source is preserved; it is not the source of these new drawings.

## Validation

Visually reviewed the full-resolution generated sheet and exported native sprite sheet. Unity 6000.4.6f1 in an isolated project passed all 347 air punch playtest assertions after importing the new sprite references. These cover complete ground/launcher/jump/air strings in both directions and slow rendering, damage-once behavior, gravity, hitstop, finisher downward velocity, floor bounce, final Knockdown/GetUp recovery, landing cleanup and authoring. Exported sprites retain transparent margins, native canvas dimensions and sprite registration.
