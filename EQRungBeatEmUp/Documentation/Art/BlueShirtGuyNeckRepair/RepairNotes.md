# BlueShirtGuy neck/collar seam repair

The user-highlighted gap was transparent pixels separating the head/collar from the torso in Attack1 frames 2–9 and 11. Frame 10 was already attached. The same defect existed in the editable Aseprite cels.

The repair fills only short transparent runs between the head and collar using adjacent original colors, quantized to the original sprite palette. Every originally opaque pixel is retained exactly. Frames 1, 10 and 12 are pixel-identical to the originals. Pose positions, dimensions, import GUIDs/settings, frame durations, combo identities, AttackData and AnimationClips are unchanged.

Updated deliverables: runtime `Sprites/BlueShirtGuy_Attack1_02.png` through `_11.png`, matching artwork PNGs, `BlueShirtGuy_Attack1.aseprite`, sheet and preview GIF in the existing Attack1 artwork folder. Backups are retained under `Original/` here. `PixelRepairResults.json` records each filled pixel. `NeckBeforeAfter.png` shows original on the left and repaired on the right.

The built-in ImageGen tool was used for a targeted collar repair reference. Its whole-character output was not substituted for the original artwork; the native source repair preserves the existing pixels and animation. Saved reference: `ImagegenNeckReference.png`.

## ImageGen prompt

Edit this exact BlueShirtGuy sprite. Repair ONLY the thin horizontal TRANSPARENT GAP through the base of his neck and blue shirt collar (native row35, around x49..73). Fill the missing one-pixel seam so the neck and collar connect naturally. Use adjacent existing skin shades/collar blue pixels and original palette. Do NOT move the head, change pose, proportions, face, hair, shirt, pants, shoes, arm positions, or redraw the character. Everything outside the tiny neck/collar seam must be unchanged. Same128x128 sprite crisp original pixel clusters, transparent background, no blur or antialiasing, no added outline, no text or effects. The head must be attached anatomically to the neck/shirt instead of floating above a horizontal cut. Output one corrected sprite with ample transparent margin, same original pixel scale and stance.
