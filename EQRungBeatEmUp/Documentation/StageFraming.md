# Gameplay camera composition

The attached written request is the specification: approximately 40% background, 60% visible floor, and a standing character 25–28% of viewport height. The image is a composition reference; its differing 35/65 annotations and pixel-size labels were not used as gameplay constants.

## Inspected setup

`ComboDemo.unity` is the first enabled build scene and the existing combat scene. `Demo` and `SampleScene` are separate perspective/template scenes and were left alone. The gameplay Main Camera was orthographic, size 4, position (0, 1, -10), with a full-screen viewport and no follow component. No Cinemachine or Pixel Perfect Camera was present, and neither package is installed.

BlueShirtGuy and ThaiBadBoy both use 100 pixels/unit, point-filtered sprite imports, and root/visual scale (1,1,1). BlueShirtGuy's opaque idle art is 106 pixels high = 1.06 world units. The prior eight-unit camera height displayed it at 13.25% of viewport height (approximately 143 pixels at 1080p). CharacterMotor clamps ground XY separately from airborne Height. Both actors originally had arena bounds X [-6,6], Y [-2,1]. The existing floor is a flat one-unit Arena sprite scaled to (16,3,1), centered at Y -0.5, covering Y [-2,1].

## New settings

| Setting | Previous | New |
|---|---|---|
| Projection | Orthographic | Orthographic |
| Orthographic size | 4 | 2 at rest; expands upward for nearby airborne sprites |
| Camera position | (0,1,-10) | (0,1.36,-10) at rest; horizontal dead-zone tracking |
| Resting vertical range | [-3,5] | [-0.64,3.36] |
| Vertical center offset | 1 | 1.36 (+0.36) |
| Standing art height | 13.25% | 26.5%; ~286px at 1080p, ~239px at 900p, ~191px at 720p |
| Background/floor boundary | Floor top Y 1 | Y 1.76 = 40% down from viewport top |
| Floor visual range | [-2,1] | [-0.64,1.76], center Y 0.56, height 2.4 |
| Actual actor lanes | [-2,1] | [-0.4,0.65] |
| Stage horizontal bounds | [-6,6] | [-6,6], unchanged |
| Default feet Y=0 | 62.5% from top | 84% from top |
| Bottom grounding margin | Camera bottom below floor | 6% of viewport at lane minimum |
| Character root/visual scale | (1,1,1) | Unchanged |

The visual floor occupies the lower 60%. Actual feet travel through 1.05 world units within it; space above the top feet limit accommodates a complete standing body, and space below the bottom limit provides the screen margin. Characters cannot walk into the background or beyond the floor bottom. Player and enemy prefab defaults are preserved for other scenes; this level authors its own lane overrides. Newly spawned motors receive the same Y bounds from the active stage, preserving their X limits.

`StageFraming` is the single new component attached to the existing Main Camera because there was no existing camera controller. It exposes reference resolution, base size, vertical center, background fraction, lane bounds, horizontal dead zone, airborne top margin, nearby-actor distance and zoom-return speed. Reference resolution is an authoring label; runtime Camera aspect follows the actual output dimensions. The component keeps vertical world height fixed for all resolutions at rest. Wider screens show more horizontal world; narrower screens show less without squashing sprites. No forced 1920×1080 output or letterboxing is needed for the tested 4:3 view.

The camera follows horizontal stage travel with a one-unit dead zone. The existing flat placeholder floor expands horizontally as needed to avoid uncovered edges. No new stage artwork, colliders, or environment system was added. Scene-view-only gizmos show a white camera rectangle, red background boundary, green actual lane bounds, and cyan player ground line. They are absent from the rendered game previews.

## Air combat and pixel art

The base view is sized for standing gameplay. For nearby airborne fighters, the camera immediately makes enough room above their complete sprite bounds plus a 0.12-world-unit margin, keeping the bottom world edge anchored at -0.64. It restores size 2 at 0.8 size units per second after the actors descend. The actual Punch1 → Punch2 → Launcher → Jump → AirPunch1 → AirPunch2 → AirPunch3 route at the highest lane needed a peak size of 2.336375, center Y approximately 1.696375. Both full sprite canvases remained visible at every sampled tick, including at 16:9, ultrawide and 4:3. Floor/background proportions temporarily shift during this camera expansion, then return to 40/60.

No attack/hurtbox dimensions, movement speed, knockback, jump/launcher velocity, gravity, dodge distance, enemy range or frame data were changed. PPU, sprite imports, character scales, animation assets, health and combat behavior were preserved.

Pixel Perfect Camera was investigated and not added: enforcing integer texel-to-screen scaling at 1080p, 900p and 720p would change the requested vertical framing or require letterboxing, and would conflict with continuous air-combat zoom. Point filtering remains in use, with MSAA disabled on this camera. Pixel edges remain sharp, although sampling is not guaranteed to use uniform integer-sized screen pixels at every resolution/zoom. Strict integer pixel rendering would be a separate rendering choice with that composition tradeoff.

## Validation

Unity 6000.4.6f1 Play Mode tests load the authored gameplay scene and actual prefabs/assets in an isolated project. The framing suite checks standing scale, floor split, foot position, horizontal coverage, top/bottom lane clamping, horizontal tracking, runtime enemy spawning, and the complete real combat route. It tests 1920×1080, 1600×900, 1280×720, 2560×1080 and 1024×768 at rest, plus the full air combo at 16:9, 2560:1080 and 4:3. Tests render actual Camera output to PNGs for visual review.

All 734 framing checks passed, with no compile errors or runtime exceptions in the final run. The existing 188-check defensive suite also passed after this change, for 922 total checks in these final runs. Logs are `stage-framing-validation-captures.log` and `stage-framing-defense-regression.log` in the isolated Unity project at `C:/Users/drago/AppData/Local/Temp/EQRungKnockback-3c9ea195806942188306e67bd5d50bfb`.

Run **Beat Em Up → Validate stage framing (Play Mode)**. Results are saved in `Documentation/StageFramingValidationResults.txt`; actual game captures are in `Documentation/StageFramingPreview/`. The defensive suite disables stage framing for its isolated prefab-arena distance checks; scene-specific boundaries are tested by the framing suite.

## Files and scene objects changed

- `Assets/EQ_Rung_BeatEmUp/Scenes/ComboDemo.unity`: Main Camera size/position/MSAA and StageFraming component; existing walking arena placement/visual extent; BlueShirtGuy and BadGuy instance lane overrides.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/StageFraming.cs` and meta: new level framing component.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/CharacterMotor.cs`: register newly enabled motors with the active stage; existing movement and attack calculations unchanged.
- `Assets/Editor/Combat/StageFramingSetup.cs` and meta: scene authoring entry point.
- `Assets/Editor/Combat/StageFramingValidation.cs` and meta: actual scene/viewport/combat/render validation.
- `Assets/Editor/Combat/PlayerDefenseValidation.cs`: isolate prefab distance tests from level camera/lane configuration.
- This report, framing validation results and PNG game previews.

The existing stage still uses placeholder flat floor/background colors. The camera composition is implemented; decorative scenery is outside this framing change.
