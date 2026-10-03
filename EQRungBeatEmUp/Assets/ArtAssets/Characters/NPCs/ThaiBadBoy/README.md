# ThaiBadBoy

Thai street-thug enemy art pack. Identity follows the supplied character reference: swept black hair, mustache/goatee, tattooed neck and forearms, oversized dark graphic shirt, cargo pants, sling bag, jewelry, hip chain, bandana, and black/off-white sneakers. BlueShirtGuy Idle2/Walk2 informed gameplay scale and pixel shading only.

## Deliverables

- `Concept/ThaiBadBoy_Concept.png`: front three-quarter, side, back, and three expressions.
- `Animations/Idle`: 8 frames, 1.12-second breathing/weight-shift loop.
- `Animations/Walk`: 8 frames, 0.76-second in-place walk loop.
- `Animations/Attack1`: 8 frames, 0.79-second jab/cross combo.
- `Animations/Hurt`: 4 frames, 0.40-second recoil/recovery.
- `Animations/Knockdown`: 6 frames, 1.32-second fall/defeat, including a final hold.

Every animation includes individual PNGs, a four-column PNG sheet, Aseprite JSON frame/timing data, an editable timed `.aseprite` file, a transparent GIF preview, and a Unity `.anim` clip. Aseprite files contain a single editable Character pixel layer, frame durations, animation tag, and shared palette; these are not anatomical rigs. GIF previews repeat for review; only Idle and Walk loop in the Unity clips.

## Unity setup

Frames are **160 x 128**, right-facing, with standing height approximately **106 pixels**, matching the reference character's gameplay height. The wider canvas accommodates horizontal knockdown poses. Ground contact is row 119 from the top; the custom pivot is `(0.5, 0.0625)`, on the ground line immediately below the foot pixels. All frames use 100 pixels per unit, point filtering, no mipmaps, and no compression. PNG alpha is binary and the shared opaque palette contains at most 48 colors. Sheets have Unity slice metadata as well as Aseprite JSON.

Use either the individual sprites or the sliced sheet. The supplied clips reference individual PNGs and animate the SpriteRenderer on the same GameObject as the Animator (empty binding path). Assign these clips to the enemy's Animator controller and use SpriteRenderer.flipX for left facing. Movement is in place; move the gameplay object separately.

Attack1 strike poses are frame 03 (0.20–0.26 s) and frame 06 (0.48–0.55 s). These are suggested windows for gameplay integration, not configured hitboxes or events. Knockdown is non-looping and ends prone.

## Production and validation

Artwork was generated using the built-in image_gen tool, then processed in Aseprite into native-size pixel frames with silhouette isolation, nearest sampling, a shared palette, binary transparency, and aligned grounding. `Source/` retains the generated large sheets; use `Animations/` for gameplay.

Rebuild tools and prompts are in `Tools/ThaiBadBoy/` at the project root. `export.lua` runs in Aseprite batch mode; `package.cjs` writes Unity metadata/clips and checks frame size, unique frames, alpha, palette limits, and canvas bounds. The exporter currently uses absolute paths for this checkout.

The artwork and file exports have been inspected and validated. The enemy has **not been placed or playtested in a Unity scene**; controller transitions, movement speed, collision/hitboxes, damage events, and combat timing remain gameplay integration work.
