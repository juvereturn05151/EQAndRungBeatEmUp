# Cream capture animations

Cream and both Thai captors animate in `Story/05_CreamKidnapped.asset`: nervous idle, synchronized grab/startled reaction (0.6 seconds), restrained struggle/hold (0.75 seconds), and resisted escort (2.8 seconds). The front captor keeps facing Cream while stepping backwards. All three maintain their hand-contact spacing throughout the escort. Heroes arrive after the captors leave, as before.

## Artwork

Generated using the built-in imagegen tool with the existing Cream and ThaiBadBoy idle sprites as identity/style references. Both PNGs preserve the generated transparent alpha and contain 24 frames in six columns and four rows:

- `Assets/EQ_Rung_BeatEmUp/ArtAssets/Story/CreamAnimations/Cream_Capture.png`: startled, escorted walk, struggle, nervous idle.
- `Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Capture/ThaiBadBoy_Capture.png`: grab, forward escort, backward escort, stationary hold.

Unity uses point filtering, uncompressed textures, six named sprites per row and foot-baseline pivots. Character heights are matched to the existing scene artwork. Cream is placed slightly behind the captors to align their hands. The sheets are cinematic artwork; normal enemy combat retains its existing animations and hitboxes.

## Setup and playback

`Beat Em Up > Story > Repair Cream capture animations` imports/slices the two sheets and replaces only the capture portion of the timeline, preserving the subsequent hero dialogue and final events. `PrologueSetup.Build()` calls the same configuration so a rebuild preserves these animations.

`StorySpriteAnimation.Play()` resets elapsed time and immediately shows frame one. Events can author frame rate, hold the last frame of a one-shot pose and preserve facing during backward movement. Loops and movement pause together. Despawn/static pose clears playback; Cream's original sprite is restored for rescue and replay. Completion and skip use the timeline's existing cleanup.

The editor timeline preview supports the same frame rate, one-shot hold and preserved facing settings.

## Verification

Repair writes `Documentation/CreamCaptureValidationResults.txt` and three Unity-rendered previews to `Documentation/CreamCapturePreview/`. The playable prologue validation also checks live grabbing/escort frames, inward facing, synchronized pause, hand-contact spacing and cleanup before rescue.

## Final generation prompts

### Cream sheet

Use case: illustration-story. Production pixel-art sprite sheet for Unity beat-em-up, transparent RGBA. Use reference ONLY for Cream's exact identity and clothing: petite tan Thai girl, black ponytail with pink tie, pink short-sleeve T shirt, dark navy short skirt, white pink sneakers. Create ONLY Cream, no other people, props, text, grid lines or shadows. 1536x1024 canvas, strict 6 columns x 4 rows of equal 256x256 cells. All 24 complete full-body sprites centered at each cell x midpoint with sole baseline 232 pixels from cell top; consistent ~180pixel standing height, crisp pixel art same style and proportions as reference, facing RIGHT three quarter side view. Row1 six sequential capture reaction frames: notices pursuers, startled, recoils, raises arms outward at chest height, tries pulling arms inward, ends with arms stretched outward left and right restrained at wrist height. Row2 six loopable resisted escort walk frames: arms stretched sideways at chest height, hands held by offscreen captors, reluctant stumbling walk right with alternating feet, unhappy alarmed expression, bobbing ponytail; frame6 returns toward frame1. Row3 six loopable standing struggle frames: arms outward at chest height, restrained pulling shoulders left/right with planted feet, alarmed expression; never collapse or change outfit. Row4 six loopable normal nervous idle frames for this same girl pink shirt navy skirt sneakers, hands close to chest slight breathing, ponytail sway. No nudity, injuries or violence, cartoon story abduction acting only. Keep exactly same character identity in every frame. Each pose fully inside its cell, do not overlap cells. Actual transparent background.

### Thai captor sheet

Use case: illustration-story. Unity pixel-art sprite sheet transparent RGBA. Reference defines exact Thai technical-school delinquent identity: tan skin shaggy short black hair, black short sleeve technical school jacket silver badge and wallet chain, black dark trousers white grey sneakers. ONLY this male character repeated, no other figures, text, grid, props or background. Strict1536x1024 six columns four rows 256x256 cells. Each full-body figure centered x midpoint sole baseline232pixels from cell top, identical ~205pixel standing height, crisp retro pixel art matching reference. All poses facing RIGHT. Row1 six sequential grab frames: idle, arm lifts, leans and reaches toward RIGHT at waist to low chest height, hand opens, hand closes, firm extended grip. Reaching hand must be on right edge, wrist around155pixels down from cell top to align shorter captive. Row2 six loopable escort walking frames towards RIGHT, one arm extended RIGHT at waist/low chest, hand held closed gripping captive offscreen, constant arm contact height, alternating natural walking steps. Row3 six loopable BACKWARDS escort step frames, still facing RIGHT with arm extended RIGHT same low height, walking BACKWARDS to LEFT while keeping grip; alternate feet to make clear backwards stepping and slight lean. Row4 six loopable stationary hold frames facing RIGHT arm extended RIGHT at same low height, clenched gripping hand, braced stance subtly pulling, small breathing motion. Full body in every cell, exact uniform and identity throughout; no weapons, blood, shadows, text or grid. Actual transparent background. Row2 and3 grips must remain in same place to match offscreen captive hand at low chest height.
