# GrayShirtGuy walk correction

The walk artwork now alternates light foreground/left contact → darker far/right contact → foreground/left contact. Gameplay code, movement speed, combat data, input, physics, Idle, snake attack and barrier super are unchanged.

## Work preserved on continuation

At the continuation request, all twelve PNGs had already been replaced in `Assets/ArtAssets/Characters/Character2/Walk`, keeping their existing `.meta` GUIDs. The accepted opposite-contact reference showed the light foreground leg trailing left and the darker far leg leading right. Walk_05–07 already used that convention. Editor changes redirected both Character2 setup helpers to the moved Walk folder, and a focused repair/validation helper and comparison images already existed. The walk clip had not been changed in the source project.

The accepted leg sequence was preserved, including Walk_05–07's opposite contact. Walk_10 was refined while retaining its left-leading silhouette. Unity's rendered comparison then exposed wider hair silhouettes in all twelve poses; their heads were narrowed to match Idle without redesigning the accepted gait. Finally, only Walk_09–12's shortened shins were corrected. Walk_01–08 were preserved byte-for-byte during that last correction. No poses were reordered to conceal errors, and no frame was individually fitted or zoomed. A standalone refinement that made the legs too thin was rejected.

## Cause and import settings

Idle and the old Walk already used 128×128, 100 PPU and the same feet pivot. The visible change came from inconsistent drawn proportions and a pipeline that centered the changing feet bounding box rather than the torso/hip axis. Reordering the previous drawings also repeated the same leading leg.

The replacement artwork uses one scale across the sheet, a stable shirt/hip axis, full 128×128 output canvases and a common bottom contact at image y=120. Imports use 100 PPU, Point filtering, no mipmaps, uncompressed RGBA and Full Rect sprites. The normalized custom pivot is (0.5, 0.0625), or (64,8) pixels from the bottom-left. Renderer scale is unchanged.

Idle alpha bounds are (36,14)–(94,120), 106 pixels high. Important replacement bounds, in top-left image coordinates:

| Pose | Bounds | Height |
|---|---|---:|
| Walk_01 | (31,15)–(102,120) | 105 |
| Walk_05 | (27,13)–(110,120) | 107 |
| Walk_06 | (27,13)–(106,120) | 107 |
| Walk_10 | (27,14)–(101,120) | 106 |
| Walk_12 | (28,14)–(101,120) | 106 |

Wider bounds come from stride and arm swing. They are not a larger torso or renderer. Dark hair-silhouette widths in the upper 26 pixels are 35–36 versus Idle's 37. All twelve body heights are 105–107 versus Idle's 106: only one pixel above or below Idle. The original Idle is retained verbatim. Adjacent frames and onion skins were checked at the same pixel scale, especially 04–06 and 10–12–01; the loop has small pose-dependent bob, not scaling.

## Frame-by-frame sequence

| Frame | Leg/arm reading |
|---|---|
| 01 | Light near/left foot forward; far/right foot behind; near arm back |
| 02 | Weight transfers onto the near/left contact |
| 03 | Dark far/right foot lifts and passes; near foot supports |
| 04 | Close passing pose before the opposite contact |
| 05 | Dark far/right leg advances right; light near/left leg trails left; near arm advances |
| 06 | Opposite/right contact; far foot ahead, light near foot clearly behind |
| 07 | Far/right contact takes weight; near arm remains forward |
| 08 | Light near/left foot lifts to pass while the far foot supports |
| 09 | Close passing pose; arms return toward the near-leading swing |
| 10 | Light near/left leg advances right; darker far leg trails left |
| 11 | Approaches near/left contact |
| 12 | Near/left contact transitions into 01 with the same leg identity and arm swing |

Walk_05 previously repeated the near-leg-forward part. Its replacement reverses the leg overlap/shading and arm swing; it is not a reordered copy of the left contact. Walk_06 is the accepted opposite contact in the playable cycle.

## Animation data and validation

The existing locomotion Animator is retained. Combat continues to use its existing frame-data sprite system. `Character2_Walk.anim` already referenced Walk_01 through Walk_12 and closing Walk_01 by preserved GUIDs. No source clip edit was ultimately necessary: its serialized data is unchanged.

The twelve sprite keys remain at 0.00, 0.05, …, 0.55 seconds, with closing Walk_01 at 0.60, sample rate 60 and authored stop time 0.60. Unity's runtime sampled clip length can include the last sprite sample; validation checks the authored settings and timestamps instead of changing them. The editor repair helper restores clip settings after assigning keys because Unity otherwise extends the serialized stop time by one sample.

`Character2WalkValidation` runs in Unity Play Mode using the actual shared Character2 prefab, controller, motor and combat clock. It checks imports, GUID-based references, chronological keys, unchanged timing, repeated same-scale samples, continuous walking for four loops and Idle → Walk → Idle. Equal-camera renders are saved as `Character2WalkPreview/Unity_*.png`. Results are in `Character2WalkValidationResults.txt`.

Final result: 307 checks passed in Unity 6000.4.6f1 using an isolated copy of this project's final assets. All twelve original sprite GUIDs were preserved, all twelve PNG drawings are distinct, and the source walk clip is unchanged. The final Unity renders were inspected alongside Idle at the same camera and renderer scale.

## Preview in Unity

1. Let Unity finish importing. Open `Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2.prefab` in Prefab Mode.
2. Select its visual SpriteRenderer/Animator child. Open **Window → Animation → Animation** and select **Character2_Walk**.
3. Enable Preview and play. Scrub Walk_04–06, then Walk_10–12–01. The sprite order remains 01 through 12.
4. Compare **Character2_Idle** and **Character2_Walk** without changing the object or camera scale.
5. For gameplay, start MainMenu, choose GrayShirtGuy, enter the Hub, walk continuously with movement input, then release it. Confirm the far-leg contact appears each cycle and the character keeps the same physical scale.
6. Optional automated check: **Beat Em Up → Characters → Validate corrected GrayShirtGuy walk**. This opens HauntedHouse and enters/exits Play Mode; save any scene edits first.

To edit individual artwork later, use the PNGs in the Walk folder. Keep their `.meta` files. If references/imports need repair, use **Beat Em Up → Characters → Apply corrected GrayShirtGuy walk** outside Play Mode. There is no need to rebuild the entire character.

## References and cleanup

`Tools/Character2/WalkCorrectedSource.png` supplies the final head-corrected sheet; `WalkLowerLegCorrectedSource.png` supplies only the final four poses at the same uniform scale. `Walk10CorrectedSource.png` preserves the earlier accepted gait reference, and `WalkRepairBefore` preserves the original twelve PNGs for comparison. Rejected sheets remain outside the Unity project. Final contact-sheet, animated loop and onion-skin previews are useful review artifacts, not imported gameplay textures. `prepare_walk_repair.py preview` refreshes review images without rewriting any sprite. Legacy walk extraction is blocked to avoid restoring the repeated-leg cycle.

The art was made with the built-in imagegen tool. Exact prompts and rejected iterations are recorded in `Character2WalkArtPrompts.md`.
