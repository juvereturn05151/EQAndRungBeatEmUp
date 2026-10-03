# BlueShirtGuy defensive sprite grounding fix

## Cause

The defensive setup selected the original images under `Assets/ArtAssets/Characters/BlueShirtGuy/Animations/` instead of the already grounded gameplay copies under `Assets/EQ_Rung_BeatEmUp/Sprites/`. The pictures have matching 128×128 canvases, 100 PPU and soles at image row 119, but different imported pivots:

| Sprite set | Normalized pivot | Pivot pixels | Opaque sole edge relative to root |
|---|---|---|---|
| Gameplay Idle / grounded gameplay copies | (0.5, 0.0625) | (64,8) | Y = 0 |
| Source Idle / Attack1 copies mistakenly used by defenses | (0.5, 0.5) | (64,64) | Y = -0.56 |
| Generated Parry_01 / Parry_02 | (0.5, 0.0625), 160×128 canvas | (80,8) | Y = 0 |

The 56-pixel pivot difference caused the reused poses to drop by 0.56 world units. Parry mixed centered-pivot reused poses with correctly grounded generated poses, so its baseline could also jump during playback. This was an error in the earlier defensive state authoring setup.

## Fix

`PlayerDefense.asset` now references the existing foot-aligned gameplay sprites. No new artwork, image edits, pivot mutations, physics changes or runtime offset compensation were needed. Source-art imports remain intact; gameplay Idle and Walk are untouched.

Affected references:

- Guard: Attack1_02, Attack1_04; block recoil Attack1_05.
- Dodge: Attack1_09, Attack1_10, Attack1_11, Idle2_01.
- Parry: Attack1_02 and Attack1_07. Generated Parry_01 and Parry_02 were already correct and remain unchanged.
- The shared Idle2_01 return pose also corrects GetUp's final Idle hold.

`PlayerDefenseSetup.Existing()` now loads the grounded copies from `Assets/EQ_Rung_BeatEmUp/Sprites/`, preventing regeneration of the state asset from reintroducing the error.

## Runtime / frame-data inspection

Defenses use `PlayerDefenseData` sprite-and-duration holds, not offensive AttackData. Holds contain no movement or visual offsets. `CharacterAnimation.HoldSprite` changes only the SpriteRenderer sprite and animation control. `ComboController.Defense` injects no downward visual offset. `CharacterMotor` places its visual child at `(0, Height, 0)`, which is `(0,0,0)` while grounded. Animator root motion is disabled; Idle/Walk clips have sprite curves and no transform curves.

Dodge retains its intended movement direction, including lane movement if explicitly supplied. Validation uses horizontal/backward Dodge to distinguish actual movement from sprite sinking. Dodge remains 20 frames, invulnerable only on 4–9, with 1.2-unit horizontal travel in an unobstructed test arena. Guard/Parry timing, health, hitboxes, hurtboxes and state transitions were not changed by this fix.

## Validation

`DefenseGroundingValidation` runs the actual player prefab in Unity Play Mode. It reads opaque sole pixels from the imported sprite images and compares world-space foot positions against gameplay Idle, rather than comparing canvas rectangles alone. It checks every Idle/Walk sprite key and every defensive pose, then plays all Guard/Parry/Dodge frames from Idle and Walk, in both facing directions, over three repeated cycles. It includes Guard block recoil and all return-to-Idle transitions. Root Y and visual local Y must remain unchanged throughout, and Dodge travel must retain its original distance.

All 922 grounding assertions passed. The rendered pose strip was visually reviewed against a shared cyan ground line. The line exists only in the validation image, never in the production scene. Existing defensive gameplay validation is also rerun to cover immunity, guarding, parrying, state conflicts and input behavior.

The preview has fourteen panels, read left to right across the first row, then the second:

1. Idle
2. Walk
3. Guard frame 0
4. Guard frame 2
5. Guard block recoil (frame 8)
6. Guard hold restored (frame 18)
7. Parry frame 0
8. Parry frame 1
9. Parry frame 3
10. Parry frame 6
11. Dodge frame 0
12. Dodge frame 3
13. Dodge frame 10
14. Dodge frame 14 / Idle hold

No remaining baseline mismatches were found. Pose/art refinement remains optional; no frame requires manual alignment polish.

## Files changed in this fix

- `Assets/EQ_Rung_BeatEmUp/PlayerDefense.asset`: corrected eight unique sprite references, including repeated holds.
- `Assets/Editor/Combat/PlayerDefenseSetup.cs`: uses grounded gameplay assets when authoring defenses.
- `Assets/Editor/Combat/DefenseGroundingValidation.cs` and `.meta`: new regression test and rendered pose comparison.
- `Documentation/DefenseGrounding.md`, `DefenseGroundingValidationResults.txt`, `DefenseGroundingPreview.png`: report and validation artifacts.

Run **Beat Em Up → Validate defensive sprite grounding (Play Mode)** to repeat the grounding check.
