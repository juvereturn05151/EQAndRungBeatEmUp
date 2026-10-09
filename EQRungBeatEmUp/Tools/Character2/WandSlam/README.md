Character 2 aerial combo finisher art

Created with the built-in imagegen tool, using AirPunch3_02, AirPunch3_03,
AirPunch3_04 and Cast_04 as pose, identity and wand references.

Prompt: Preserve the stocky gray-shirt character, black hair and glasses, navy
shorts, blue sandals and pixel art style. Make three transparent right-facing
airborne poses: overhead brown-wand wind-up, forceful downward-right wand smash,
and matching low-wand follow-through. No opponent, ground, text or motion blur.

Generated.png preserves the tool output. Original/ preserves the replaced sprites.
LeftHandCorrection.png preserves the imagegen handedness edit. Correction prompt:
Change only the wind-up to raise the wand in the anatomical left, camera-near
foreground arm, matching the downward strike and follow-through. The rear right
arm is an empty balancing fist. Preserve right-facing orientation, identity,
legs, palette, scale, transparency, and three-cell layout. Do not mirror the body.
Only the corrected wind-up cell is packaged; the accepted strike and recovery
are retained from Generated.png.
package.py crops, uniformly scales with nearest-neighbor sampling, and anchors
the generated poses in the existing 256 by 160 canvases at the sandal baseline.
Preview.png includes the unchanged first pose and the three replacement poses.
Size correction: the three wand poses use a uniform source scale of 0.225,
25% larger than the initial 0.18. Compared against Idle_01, Punch1_03 and
AirPunch2_02 at the same pixels-per-unit and foot anchor in SizeComparison.png.
Scaling uses the generated source to keep pixel edges crisp and avoid repeatedly
resizing the packaged sprites. The 256 by 160 canvas and eight-pixel pivot remain.
Existing Unity GUIDs, point filtering, pivots, combat timing and hit reactions
are retained. The attack already sends airborne opponents downward.
