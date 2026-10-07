# BlueShirtGuy grounded launcher refinement

The existing eight Launch1 poses now show neutral, load, deepest compression, explosive extension, maximum impact, follow-through, recovery and neutral. The first and last sprites exactly match BlueShirtGuy_Idle2_01. Both soles remain at their original image rows 119/118 throughout. Runtime and art PNGs keep their existing GUIDs, point filtering, 128x128 canvas, 100 pixels/unit and bottom pivot (64,8).

Launcher.asset remains 26 combat frames with the original active window 6-12. The sprite holds are now:

| Pose | Combat frames |
|---|---|
| Neutral | 0-1 |
| Anticipation | 2-3 |
| Deepest compression | 4-5 |
| Explosive extension | 6-7 |
| Maximum impact | 8-12 |
| Follow-through | 13-16 |
| Recovery | 17-21 |
| Exact Idle return | 22-25 |

The existing Launcher.anim uses matching pose boundaries. Damage, hitboxes, enemy launch force, total duration, movement and explicit jump-cancel rules were preserved; the move itself gives the player no jump impulse. Player-requested jump follow-ups still exit this attack through the existing cancel system.

Existing Launch1 PNGs, sheet, GIF and editable Aseprite were replaced in place. The built-in imagegen edit used the original sheet and Idle references; generation prompts are in GenerationPrompts.txt. Original source/art and attack/clip snapshots are under Original for recovery.

Validation: Beat Em Up > Validate grounded launcher and airborne follow-up (Play Mode). LauncherAnimationValidationResults.txt checks canvas, pivots, both soles, exact Idle endpoints, visual alignment with the unchanged hit window, and actual grounded enemy-launch behavior in both facings. The same runner verifies existing jump/air-dive follow-ups.
