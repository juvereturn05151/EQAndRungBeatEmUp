# GrayShirtGuy walk identity polish

This pass refines the already accepted walk artwork to match the current Idle model. It does not rebuild the gait, change frame order or retime the animation.

## Adjusted frames and visual changes

All twelve `Assets/ArtAssets/Characters/Character2/Walk/Walk_01.png` through `Walk_12.png` received an appearance cleanup. Their shared off-model features were heavier glasses with conspicuous white eyes, simplified face/chin shading, a different hair contour, flat shirt shading and less consistent clothing detail. No complete frame was close enough to leave entirely untouched, but the accepted pose sequence was used as the edit target throughout.

The cleaned faces use Idle's downward three-quarter angle, smaller rectangular glasses/eyes and clearer nose, cheek and chin. The hair uses swept dark clusters and texture closer to Idle. Shirts have heather-gray shading, soft folds and a relaxed volume; shorts have muted dark shading; sandals retain exposed-toe blue slide readability. Shoulders, arms and legs keep the stocky ordinary-person feel rather than an exaggerated fighter or cartoon model.

The first sheet was rejected because its face still drifted. An identity-matched contact-pose reference was then used to guide the full sheet. Only the last four drawings subsequently needed a small anatomical size correction; Walk_01–08 were preserved byte-for-byte during that final correction. All frames were reviewed together, with particular comparisons of Idle against 01, 06 and 12 and inspection of 03, 05 and 09.

## Motion, scale and integration preserved

- No frame order changed: Walk_01 through Walk_12, then the existing closing Walk_01.
- The accepted left → right → left leg sequence and opposing casual arm swing remain. Walk_05–07 retain the opposite/far-leg contact; this pass did not start another gait redesign.
- The walk clip is byte-for-byte unchanged, including its timestamps, sample rate, loop settings and original sprite GUID references.
- Idle_01–04 and all twelve Walk `.meta` files are byte-for-byte unchanged. Canvas is still 128×128, PPU 100 and feet pivot (64,8), normalized (0.5,0.0625).
- All feet remain at image baseline y=120. Body heights are 104–107 pixels versus Idle's 106; hair widths are 37–39 versus Idle's 37. These small pose differences do not require renderer scaling.
- No runtime scripts, player prefab/controller, movement speed, playback speed, AttackData, skill, combat, input or physics settings changed.

The multiplayer catalog's compatibility fingerprint was refreshed for the changed art. The existing editor validation helper now also captures Walk_12. Packaging/preview tooling and review artifacts were updated; these are outside gameplay code.

## Unity validation

307 existing checks passed in Unity 6000.4.6f1 using an isolated copy of the final assets. They cover the twelve imports/references, original clip settings, identical renderer scale, repeated frame sampling, four continuous chronological walking loops, movement and stopping back to Idle. Source fingerprints independently confirmed unchanged Idle, clip and metadata.

The final Unity renders of Idle, Walk_01, Walk_06 and Walk_12 were compared at the same camera and object scale. The identity/appearance judgment is visual; the automated checks establish timing, dimensions, references and control behavior.

Review artifacts in `Documentation/Character2WalkPreview`:

- `ModelConsistencyBeforeAfter.gif`: Idle, the accepted walk before this polish, and the new appearance at equal pixel scale.
- `IdentityContacts.png`: Idle with Walk_01, Walk_06 and Walk_12.
- `IdleAndWalk.png`: all twelve current sprites beside Idle.
- `Unity_Idle.png`, `Unity_Walk_01.png`, `Unity_Walk_06.png`, `Unity_Walk_12.png`: actual Unity renders.
- `Onion_04_05_06.png` and `Onion_10_11_12_01.png`: adjacent-pose comparisons.

## Preview in Unity

1. Let Unity finish importing the changed PNGs.
2. Open `Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2.prefab` in Prefab Mode. Select the visual child containing its Animator.
3. Open **Window → Animation → Animation**, choose **Character2_Idle**, and enable Preview. Keep the camera and Transform scale fixed.
4. Switch to **Character2_Walk**. Scrub 01, 06 and 12, then play several loops. Check the face, hair and shirt against Idle before switching back.
5. For gameplay, start MainMenu, select GrayShirtGuy, enter the Hub, hold movement input for several cycles, then release it. The same shared controller handles Idle → Walk → Idle.
6. Optional automated replay: save scene edits, then use **Beat Em Up → Characters → Validate corrected GrayShirtGuy walk**. It opens HauntedHouse and enters/exits Play Mode.

## Art sources

Built-in imagegen edit mode was used; the complete prompt set is in `Character2WalkModelConsistencyPrompts.md`. Accepted sources are `Tools/Character2/WalkModelConsistencySource.png` and `WalkModelConsistencyProportions.png`, with the matched contact reference in `WalkModelContactReference.png`. `WalkModelConsistencyBefore` preserves the accepted pre-polish sprites for comparison. Rejected generated sheets remain outside the project.

`prepare_walk_model_consistency.py preview` refreshes review artifacts without touching game sprites. The packaging tool keeps a fixed canvas and one uniform source scale; it preserves all existing `.meta` files and does not edit the animation clip.
