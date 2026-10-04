# Ground punch playtest values

These are editable starting values for the existing AttackData system. All frame numbers are zero-based logical combat frames at 60 FPS. Hitstop pauses the timeline, so a connected attack takes longer in real time than a whiff.

Current readability revision adds selective anticipation/contact/follow-through holds. Whiff durations are 0.433s, 0.450s and 0.633s. Cancel windows move with the poses; held frames do not repeat displacement. Damage, hitstun, hitstop, knockback and total forward step remain unchanged. See [Combat readability](CombatReadability.md) for sound/VFX assignments and the corresponding aerial timing changes.

| Asset | Total | Startup | Active | Recovery | Attack cancel | Launcher cancel | Damage | Hitstun | Hitstop | Knockback speed | Forward step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Punch1 | 26 | 0–6 | 7–12 | 13–25 | 13–20 | None | 8 | 18f | 3f | 1 | 0.08 |
| Punch2 | 27 | 0–6 | 7–12 | 13–26 | 13–22 | 13–22 | 9 | 20f | 4f | 1.4 | 0.12 |
| Punch3 | 38 | 0–10 | 11–18 | 19–37 | None | None | 14 | 24f | 6f | 5 | 0.24 |

Each attack uses Normal ground hits, zero launch velocity, and Hit ID 0 with no repeat. Every active frame carries the same impact settings so first contact always produces the intended push. Punch3 uses the existing GroundHit stagger and strong horizontal knockback.

Hitboxes follow the upper-body strike and exist only on active frames:

| Asset | Offset (X, Y) | Size (X, Y) |
| --- | --- | --- |
| Punch1 | (0.40, 0.81) | (0.69, 0.58) |
| Punch2 | (0.43, 0.78) | (0.78, 0.46) |
| Punch3 | (0.52, 0.68) | (0.90, 0.54) |

Forward steps are one-time frame displacements, mirrored by facing. Punch1 steps 0.04 on frames 3 and 7; Punch2 steps 0.05 on frame 3 and 0.07 on frame 7; Punch3 steps 0.08 on frames 5, 6 and 11. For comparison, ordinary movement at speed 3.5 covers about 0.058 units per combat frame.

## Existing artwork

The existing `BlueShirtGuy_Attack1_01` through `_12` drawings are reused. Drawing numbers below are filename suffixes, not combat frame numbers.

| Asset | Combat frames → drawing |
| --- | --- |
| Punch1 | 0–6 → 02; 7–12 → 03; 13–17 → 04; 18–20 → 07; 21–25 → 08 |
| Punch2 | 0–2 → 04; 3–6 → 05; 7–12 → 06; 13–19 → 07; 20–26 → 08 |
| Punch3 | 0–4 → 08; 5–10 → 09; 11–18 → 10; 19–28 → 11; 29–37 → 12 |

## Input and recovery

The player prefab uses the existing six-frame attack/launcher buffer. Requests wait for the appropriate Attack Cancel or Launcher Cancel permission. Buffer time pauses during hitstop. There is no hit-confirm flag, so whiffs can cancel during the same authored windows. With the slower startup, a second input pressed too early can expire before the new cancel window; press during the strike or repeat the input to chain.

Ground attacks that complete recovery return to neutral and clear an unused chain request. A new Attack then starts Punch1. This prevents missed cancel windows from chaining after the animation has ended or accidentally restarting Punch1. Existing air-route continuation is preserved. The prefab's extra finisher cooldown is zero because Punch3 already has 19 authored recovery frames.

## Editing and validation

Open **Tools → Combat → Attack Data Editor** and select Punch1, Punch2 or Punch3. Sprites, frame count, hitboxes, displacement, damage, hitstop, hitstun and separate cancel permissions remain editable in the assets.

Run **Beat Em Up → Validate ground punch playtest (Play Mode)** for the focused checks. It temporarily isolates actors in Play Mode, uses the real assets and logical clock, and exits Play Mode after testing. It does not rebuild or save the scene. Results are written to `Documentation/PunchPlaytestValidationResults.txt`.

The legacy **Validate frame combat** command has older fixture/timing assumptions; use the focused ground punch command for these playtest values.

The current readability revision passed all 291 focused assertions in Unity 6000.4.6f1, including buffering, full recovery, combo/launcher routes, mirrored hits, hitstop, unchanged displacement and damage, and a connected 8 + 9 + 14 damage string.
