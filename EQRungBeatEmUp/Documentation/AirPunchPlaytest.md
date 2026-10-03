# Air punch playtest values

All frames are zero-based logical combat frames at 60 FPS. These are starting values editable in the existing Attack Data Editor. Hitstop pauses the logical attack timeline.

| Attack | Total | Startup | Active | Recovery | Attack Cancel | Damage | Hitstun | Hitstop | Horizontal knockback | Gravity Scale |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AirPunch1 | 13 | 0–2 | 3–5 | 6–12 | 5–10 | 7 | 17f | 3f | 0.2 | 0.85 |
| AirPunch2 | 14 | 0–2 | 3–5 | 6–13 | 5–11 | 8 | 19f | 4f | 0.25 | 0.85 |
| AirPunch3 | 20 | 0–4 | 5–8 | 9–19 | None | 13 | 24f | 6f | 1 | 1 |

All attacks hit airborne opponents only. Hit ID 0 is shared across active frames with no repeat, so each attack deals damage once. Launcher Cancel and Jump Cancel are off. AirPunch3 uses the existing AirFinisher type; the first two punches use Normal and the existing AirHit/juggle reaction.

## Motion and finisher

Falling suspension is off on every frame. No horizontal or vertical velocity overrides are authored, so existing vertical momentum continues. Movement Input Scale is 0.3 on all three air attacks: voluntary movement stays directional and active at 30% speed, avoiding full-speed overshoot while holding forward. Normal jumping remains full speed. AirPunch1 steps forward 0.04 units on frame 2 and 0.02 on frame 3; AirPunch2 steps 0.04 on frame 2. These steps mirror facing. The first two punches reduce gravity to 0.85; the finisher restores scale 1.

Airborne enemy reactions apply the hitbox's existing Knockback value. The first two punches stay light to retain range. The enemy's existing juggle lift is tuned to 0.6; its cap remains unchanged. See `LauncherPursuitTuning.md` for the complete launcher/jump tuning comparison.

For AirFinisher hitboxes, **Launch Velocity Y = -8** sets downward enemy speed to 8 units/second. Edit the negative value in Attack Data Editor to tune it. Zero retains the existing enemy fallback fall speed. Dedicated Launcher behavior remains unchanged. The finisher closes the existing juggle, selects Falling, and lets the existing Landing/recovery state finish. It also applies downward speed if the juggle was already closed. No bounce or new knockdown system was added.

The existing six-frame input buffer, per-jump attack limit, and landing cleanup are retained. AirPunch3 cannot restart the air string in the same jump. Landing stops an air attack and clears its sprite override and combo route.

## Artwork and hitboxes

Existing AirAttack1 drawings are held across logical frames:

| Attack | Combat frames → drawing filename suffix |
| --- | --- |
| AirPunch1 | 0–1 → 01; 2 → 02; 3–5 → 04; 6–8 → 05; 9–12 → 09 |
| AirPunch2 | 0–1 → 05; 2 → 06; 3–5 → 08; 6–13 → 09 |
| AirPunch3 | 0–2 → 10; 3–4 → 11; 5–6 → 12; 7–8 → 13; 9–13 → 14; 14–19 → 09 |

| Attack | Hitbox offset (X, Y) | Hitbox size (X, Y) |
| --- | --- | --- |
| AirPunch1 | (0.42, 0.82) | (0.65, 0.42) |
| AirPunch2 | (0.46, 0.79) | (0.74, 0.46) |
| AirPunch3 | (0.48, 0.48) | (0.82, 0.86) |

Offsets are relative to the motor's airborne height. The last hitbox extends lower to follow the downward punch drawings.

## Validation

Run **Beat Em Up → Validate air punch playtest (Play Mode)**. It uses the actual player/enemy prefabs, actual assets, moving airborne hurtboxes, and logical clock; it does not rebuild or save the scene. It tests full recovery, cancels, buffering, gravity, forward movement, finisher damage/velocity, landing cleanup, the per-jump limit, and editor authoring.

It also executes the complete **Punch1 → Punch2 → Launcher → manual Jump → AirPunch1 → AirPunch2 → AirPunch3** route facing right, facing left, and with two logical frames per rendered step. Results are written to `Documentation/AirPunchPlaytestValidationResults.txt`.
