# Air punch playtest values

All frames are zero-based logical combat frames at 60 FPS. These are starting values editable in the existing Attack Data Editor. Hitstop pauses the logical attack timeline.

Current readability revision adds selective anticipation/contact/follow-through holds. Whiff durations are 0.383s, 0.400s and 0.600s. Cancel windows move with the poses; held frames do not repeat one-time displacement. Damage, hitstun, hitstop, gravity, recoil and finisher/bounce settings are preserved. See [Combat readability](CombatReadability.md) for the new library feedback and ground timing changes.

| Attack | Total | Startup | Active | Recovery | Attack Cancel | Damage | Hitstun | Hitstop | Horizontal knockback | Gravity Scale |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AirPunch1 | 23 | 0–5 | 6–10 | 11–22 | 10–19 | 7 | 17f | 3f | 0.2 | 0.85 |
| AirPunch2 | 24 | 0–5 | 6–10 | 11–23 | 10–20 | 8 | 19f | 4f | 0.25 | 0.85 |
| AirPunch3 | 36 | 0–9 | 10–17 | 18–35 | None | 13 | 24f | 6f | 1 | 1 |

All attacks hit airborne opponents only. Hit ID 0 is shared across active frames with no repeat, so each attack deals damage once. Launcher Cancel and Jump Cancel are off. AirPunch3 uses the existing AirFinisher type; the first two punches use Normal and the existing AirHit/juggle reaction.

## Motion and finisher

Falling suspension is off on every frame. No horizontal or vertical velocity overrides are authored, so existing vertical momentum continues. Movement Input Scale is 0.3 on all three air attacks: voluntary movement stays directional and active at 30% speed, avoiding full-speed overshoot while holding forward. Normal jumping remains full speed. AirPunch1 steps forward 0.04 units on frame 3 and 0.02 on frame 6; AirPunch2 steps 0.04 on frame 3. These steps mirror facing. The first two punches reduce gravity to 0.85; the finisher restores scale 1.

Airborne enemy reactions apply the hitbox's existing Knockback value. The first two punches stay light to retain range. The enemy's existing juggle lift is tuned to 0.6; its cap remains unchanged. See `LauncherPursuitTuning.md` for the complete launcher/jump tuning comparison.

For AirFinisher hitboxes, **Launch Velocity Y = -8** sets downward enemy speed to 8 units/second. Edit the negative value in Attack Data Editor to tune it. Zero retains the existing enemy fallback fall speed. Dedicated Launcher behavior remains unchanged. The finisher closes the existing juggle and drives the enemy downward, including if the juggle was already closed. The current authored ground bounce is retained: one rebound, followed by final landing into Knockdown and GetUp recovery.

The existing six-frame input buffer, per-jump attack limit, and landing cleanup are retained. AirPunch3 cannot restart the air string in the same jump. Landing stops an air attack and clears its sprite override and combo route.

## Artwork and hitboxes

Existing AirAttack1 drawings are held across logical frames:

| Attack | Combat frames → drawing filename suffix |
| --- | --- |
| AirPunch1 | 0–2 → 01; 3–5 → 02; 6–10 → 04; 11–16 → 05; 17–22 → 09 |
| AirPunch2 | 0–2 → 05; 3–5 → 06; 6–10 → 08; 11–23 → 09 |
| AirPunch3 | Dedicated redraw: AirReady 0–2; HandsTogether 3–4; OverheadWindup 5–9; SmashStart 10–12; Impact 13–17; Recovery 18–35 |

AirPunch3 uses the dedicated six-pose redraw from `AirCombo3Redraw.md` and the AirCombo3 asset folder. The current timing extends its windup/impact/recovery holds without changing the drawings; it is now 36 frames with active frames 10–17.

| Attack | Hitbox offset (X, Y) | Hitbox size (X, Y) |
| --- | --- | --- |
| AirPunch1 | (0.42, 0.82) | (0.65, 0.42) |
| AirPunch2 | (0.46, 0.79) | (0.74, 0.46) |
| AirPunch3 | (0.48, 0.48) | (0.82, 0.86) |

Offsets are relative to the motor's airborne height. The last hitbox extends lower to follow the downward punch drawings.

## Validation

Run **Beat Em Up → Validate air punch playtest (Play Mode)**. It uses the actual player/enemy prefabs, actual assets, moving airborne hurtboxes, and logical clock; it does not rebuild or save the scene. It tests full recovery, cancels, buffering, gravity, forward movement, finisher damage/velocity, landing cleanup, the per-jump limit, and editor authoring.

It also executes the complete **Punch1 → Punch2 → Launcher → manual Jump → AirPunch1 → AirPunch2 → AirPunch3** route facing right, facing left, and with two logical frames per rendered step. Results are written to `Documentation/AirPunchPlaytestValidationResults.txt`.

The current readability revision passed all 387 assertions in Unity 6000.4.6f1, including those full routes with the slower ground punches, unchanged damage, gravity, hitstop and finisher settings, single-hit history across longer active windows, bounce/knockdown recovery, landing cleanup, and buffer/cancel boundaries.
