# Combo readability and library feedback

BlueShirtGuy keeps the same six moves, sprites, combo routes, six-frame input buffer, damage, hitstun, hitstop, displacement, recoil, airborne physics and bounce behavior. AirCombo1/2/3 are the existing `AirPunch1/2/3.asset` assets.

## Timing

All ranges are inclusive, zero-based combat frames at 60 FPS. Whiff duration excludes hitstop; connected attacks pause for their existing hitstop.

| Move | Total before → now | Startup | Active | Recovery | Attack cancel | Whiff duration |
| --- | --- | --- | --- | --- | --- | --- |
| Punch1 | 23 → 26 | 0–6 | 7–12 | 13–25 | 13–20 | 0.433s |
| Punch2 | 24 → 27 | 0–6 | 7–12 | 13–26 | 13–22 | 0.450s |
| Punch3 | 33 → 38 | 0–10 | 11–18 | 19–37 | None | 0.633s |
| AirCombo1 | 20 → 23 | 0–5 | 6–10 | 11–22 | 10–19 | 0.383s |
| AirCombo2 | 21 → 24 | 0–5 | 6–10 | 11–23 | 10–20 | 0.400s |
| AirCombo3 | 30 → 36 | 0–9 | 10–17 | 18–35 | None | 0.600s |

Light/middle attacks gain one anticipation hold, one contact hold and one follow-through hold. Punch3 gains two anticipation and two contact holds plus one follow-through; AirCombo3 gains two of each. This emphasizes readable poses rather than uniformly scaling the entire animation. Extra frames do not repeat one-time movement. Cancel windows move with the poses and preserve the existing six-frame buffer; Punch2's separate Launcher cancel is 13–22. AirCombo1/2 can still cancel on their final active frame. Finishers do not chain back into the first move.

AirCombo3's dedicated drawings now hold AirReady 0–2, HandsTogether 3–4, OverheadWindup 5–9, SmashStart 10–12, Impact 13–17 and Recovery 18–35. No artwork was replaced or regenerated.

## Sound and VFX assignments

Sounds come from `Assets/Deadly Kombat Free version/`; names below omit `.wav`. VFX come from `Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/`; names omit `.prefab`.

| Move | Swing | Confirmed impact | VFX | Scale / lifetime |
| --- | --- | --- | --- | --- |
| Punch1 | punch_short_whoosh_16 | body_hit_small_11 | CFXR Hit D 3D (Yellow) | 0.12 / 0.40s |
| Punch2 | punch_short_whoosh_30 | body_hit_large_32 | CFXR Hit A (Red) | 0.14 / 0.40s |
| Punch3 | punch_long_whoosh_21 | body_hit_finisher_23 | CFXR Hit A (Red) | 0.20 / 0.50s |
| AirCombo1 | punch_short_whoosh_16 | face_hit_small_13 | CFXR Hit D 3D (Yellow) | 0.12 / 0.40s |
| AirCombo2 | punch_short_whoosh_30 | body_hit_large_44 | CFXR Hit A (Red) | 0.14 / 0.40s |
| AirCombo3 | punch_long_whoosh_30 | body_hit_finisher_52 | CFXR Impact Glowing HDR (Blue) | 0.18 / 0.50s |

Light/middle swing volume is 0.32 and impact volume 0.50; finishers use 0.42 and 0.65. Playback is 2D at the clip's original pitch. Finishers use a larger short burst and stronger impact clip, while preserving their existing six-frame hitstop. Instance lights and Cartoon FX camera shake are disabled, so the bursts do not wash out the stage or interfere with camera follow. Original library prefabs and sound files remain unchanged.

## Existing hooks

- One `Swing` frame event on the first active frame uses the existing `AttackPlayer.FrameEvent`. It plays on a miss too.
- `AttackHitbox.HitConfirmed` is raised only after a hurtbox/destructible accepts the hit and hit history is recorded. `AttackFeedback` plays impact audio/VFX only for `CombatHitOutcome.Hit`; misses, immunity, blocks and parries do not play this damage-impact cue.
- The hit-confirm location determines effect position, so an opponent contacted later in the active window receives its cue at that actual contact frame.
- Existing hit-ID history prevents repeated damage and repeated feedback. Multiple accepted targets on the same combat frame share one cue to avoid stacking volume and bursts; all targets still receive their own damage.
- `AttackPlayer.Started` resets helper history, including interrupted/repeated plays of the same asset. Hitstop does not replay frame-entry events.
- `AttackFeedback` attaches at runtime only when a configured attack starts. Existing scenes and player prefabs work without rebuilding. Other attacks without feedback remain silent.
- Audio and impact objects clean themselves up after their clip/lifetime, independently of attacker recovery. Particle renderers use the player's sorting layer and order +2.

## Tune it in Unity

1. Select a Punch or AirPunch asset under `Assets/EQ_Rung_BeatEmUp/Attacks`.
2. Expand **Feedback** in its existing AttackData Inspector (also available in **Tools → Combat → Attack Data Editor**).
3. Change **Swing Sound**, **Impact Sound**, their volumes, **Impact Prefab**, **Impact Scale**, or **Impact Lifetime**.
4. In the frame list, keep exactly one `Swing` under **Events** at the strike's first active pose. Moving it changes swing timing; hit impacts remain automatic on accepted collision.
5. Play and test both contact and whiff. To change pose durations, use the existing frame editor; keep movement on its original entry frame and preserve hit IDs/cancel permissions.

## Validation and files

Unity 6000.4.6f1 validation ran in the isolated `FrameCombatValidation` project without closing the user's open editor:

- Ground punch playtest: 291 assertions passed, including buffer boundaries, recovery, launcher permissions, mirrored hitboxes, once-only damage and the complete ground damage string.
- Air punch playtest: 387 assertions passed, including full ground → launcher → jump → three-air-hit routes facing both directions and at slower rendered steps, landing cleanup, juggle/bounce reactions and unchanged physics/damage.
- Combat feedback: 128 checks passed for all six loaded library assignments, frame-entry timing, mirrored contacts, AudioSource clips/volumes, finite particle configuration/sorting, disabled light/shake, hitstop deduplication, whiffs, same-asset restarts, pause, rejected hits and multi-target deduplication.
- Rendered Punch3 and AirCombo3 contact snapshots were visually inspected: bursts remain localized and character poses remain readable. Batch checks verify playback configuration and hooks, not subjective audio quality or perceived responsiveness; tune volumes/feel through normal Play Mode as needed.

Run **Beat Em Up → Validate combo sound and effects (Play Mode)** to repeat the feedback check. Like the existing combat validations, it temporarily isolates actors in Play Mode and exits after testing; it does not save/rebuild the scene. Results: `CombatFeedbackValidationResults.txt`, `PunchPlaytestValidationResults.txt`, `AirPunchPlaytestValidationResults.txt`. Images: `CombatReadabilityPreview/Punch3.png` and `AirPunch3.png`.

Changed assets: the six Punch/AirPunch AttackData files. Changed scripts: `AttackData.cs`, `AttackPlayer.cs`, `AttackHitbox.cs`, `AttackDataEditor.cs`, `AttackDataEditorWindow.cs`, and the existing two combo playtests. New scripts: `AttackFeedback.cs` and `CombatFeedbackValidation.cs`, each with metadata. `Tools/CombatReadability/` records the one-time retiming/test migration; it refuses retiming assets with different frame counts. No scene, player prefab, source animation drawing, sound file or library effect prefab was rewritten.
