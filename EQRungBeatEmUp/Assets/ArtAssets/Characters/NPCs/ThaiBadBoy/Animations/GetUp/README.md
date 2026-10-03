# GetUp

Five right-facing recovery poses: prone, pushing up, half kneeling, rising,
and standing. Frame 01 reuses the final Knockdown frame exactly; frame 05
reuses Idle frame 01 exactly. Three intermediate poses were drawn from the
existing Idle and Knockdown references using the built-in image_gen tool.

All gameplay frames are 160 x 128, with binary transparent alpha and the
original palette. Their ground contact is row 119, their pivot is
`(0.5, 0.0625)`, and their import settings use 100 PPU, point filtering,
no mipmaps and no compression. Motion is in place; use `flipX` for left facing.

`ThaiBadBoy_GetUp.anim` is a non-looping 0.80-second SpriteRenderer animation:
120 / 180 / 180 / 160 / 160 milliseconds. Its binding path is empty, matching
the other clips in this art pack. Assign it to the enemy recovery state on
the GameObject containing the SpriteRenderer. This asset addition does not
change combat logic or an existing Animator controller.

Individual PNGs drive the clip. The sheet/JSON are for export and review;
the `.aseprite` file contains five timed RGBA cels on a Character layer.
The GIF repeats with an extended standing pause for visual review only.

The generated source is `../../Source/GetUp.png`. The exact generation prompt
and reproducible export script are in `Tools/ThaiBadBoy/getup-prompt.txt` and
`Tools/ThaiBadBoy/package-getup.cjs` at the project root. Run the exporter
with Node.js and the `sharp` dependency available.

`GetUp_Validation.json` records dimensions, palette, alpha, grounding and
per-frame hashes. The first and last poses remain pixel-identical to their
existing reference frames.
