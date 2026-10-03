# Launcher pursuit tuning

The complete route was tested with the actual player and enemy prefabs and logical 60 FPS clock. The earlier checks used immediate inputs and no forward movement; the new timing matrix also delays the pursuit and holds forward to expose overshoot.

## Before and after

| Setting | Before | After | Reason |
| --- | --- | --- | --- |
| Enemy launcher vertical velocity | 8 | 6.2 | A 22.5% reduction brings the launched target into natural jump reach. |
| Enemy launcher horizontal velocity | 0.7 | 0.35 | Reduce separation at the start of pursuit. |
| Player jump velocity | 7.5 | 7.5 | Preserve normal traversal. |
| Player base gravity | 14 | 14 | Preserve the natural jump arc. |
| Enemy airborne/juggle gravity | 9 | 9 | Lowering velocity already produces the target apex; gravity stays active. |
| Launcher Jump Cancel | 13–25 | 8–25 | Open two logical frames after first impact on frame 6, after hitstop. Manual Jump still required. |
| AirPunch1 forward displacement | 0 | 0.06 total | Subtle chase step: 0.04 on frame 2, 0.02 on frame 3. |
| AirPunch1 horizontal/vertical velocity overrides | None | None | Preserve momentum; no extra upward boost is needed. |
| AirPunch1 enemy horizontal knockback | 0.35 | 0.2 | Retain comfortable reach for the next hit. |
| AirPunch2 forward displacement | 0.04 on frame 2 | Same | Already provides a small positioning correction. |
| AirPunch2 horizontal/vertical velocity overrides | None | None | Avoid artificial drift or upward separation. |
| AirPunch2 enemy horizontal knockback | 0.5 | 0.25 | Keep the target close for the finisher. |
| Enemy air-hit lift | 2.2 | 0.6 | Small recovery from descent rather than a renewed upward launch. Existing upward velocity is preserved if already higher. |
| AirPunch1/2 Gravity Scale | 0.65 | 0.85 | Effective player gravity rises from 9.1 to 11.9; the lowered target no longer needs prolonged player float. |
| AirPunch3 Gravity Scale | 1 | 1 | Normal gravity during the finish. |
| Movement Input Scale, all air punches | 1 | 0.3 | Keep forward control and movement while preventing full walking speed from carrying the player past the enemy. |
| AirPunch1 Attack Cancel | 5–10 | 5–10 | Existing final-active/early-recovery window is already forgiving. |
| AirPunch2 Attack Cancel | 5–11 | 5–11 | Existing window already reaches the finisher smoothly. |
| Attack/launcher input buffer | 6f | 6f | Existing buffer handles early requests and pauses during hitstop. |
| Air hitstun | 17 / 19 / 24f | Same | Enough stun after positioning is corrected. |
| Air hitstop | 3 / 4 / 6f | Same | Retain escalating impact. |
| Finisher downward enemy velocity | -8 | -8 | The existing AirFinisher now connects reliably; downward force is already strong. |

AirPunch frame counts, damage, sprite timing, hitbox sizes, the per-jump attack limit and landing cleanup remain unchanged. There is no tracking, snapping or homing. The new frame-level Movement Input Scale only multiplies voluntary movement; it does not change recoil or authored velocity. Its default is 1, so other attacks retain their previous movement.

## Measurements and iterations

Measured natural jump apex: **1.948 units before and after**. Enemy launch apex: **3.490 → 2.085 units**, reducing the ratio from **179.2% → 107.1%**.

The matrix varies initial spacing (0.8 and 1.0), facing left/right, jump input delays (0/4/8/12 live frames after launch), first air attack delays (0/4/8 frames after jumping), cancel rhythm (frames 5/8/10), and either no forward input or forward input held throughout pursuit. Hitstop is respected when counting live input delays.

| Iteration | Connected full routes |
| --- | --- |
| Original settings | 100 / 288 |
| Lower launch, earlier jump cancel, reduced lift, matched arcs | 184 / 288; all 144 stationary variants passed, but forward movement still overshot |
| Add air-frame movement input scale | 288 / 288 |
| Final two-frame pursuit opening | 288 / 288 |

For an immediate, forward-held route at initial spacing 0.8, enemy position relative to the player was:

| Moment | Horizontal distance in front | Enemy height minus player height |
| --- | --- | --- |
| AirPunch1 begins | 0.825 | +0.078 |
| AirPunch1 impact | 0.733 | +0.035 |
| AirPunch2 impact | 0.628 | -0.042 |
| AirPunch3 impact | 0.531 | -0.127 |

This places the first target slightly above/in front, keeps the arcs close for the second hit, and puts the enemy below the finishing strike. Both actors land and recover. The maximum heights during a connected route are lower than the unopposed launch apex because the finisher intentionally cuts the enemy's ascent short.

The matrix measures connectivity and position using the actual combat simulation. Subjective controller feel still needs human playtesting; the measurements are not a claim that automated tests can judge it.

## Editing and repeatable checks

Open Attack Data Editor to tune Launcher velocity/cancels and AirPunch movement, Movement Input Scale, gravity, knockback or cancels. The enemy's small air-hit lift remains exposed on BadGuy's EnemyHitReaction component.

Run **Beat Em Up → Validate launcher pursuit (Play Mode)** to repeat the 288 routes, apex measurements, launcher whiff check, ordinary forward jump check and standalone air attack check. It also verifies landing and enemy recovery. Tests temporarily isolate actors in Play Mode and do not save the scene.

The original and final samples are preserved in `LauncherPursuitBefore.csv` and `LauncherPursuitAfter.csv`; the repeatable validator writes `PursuitTuningMatrix.csv`. Summary results are in `LauncherPursuitValidationResults.txt`.
