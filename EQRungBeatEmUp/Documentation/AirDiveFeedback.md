# Diving attack feedback

Both BlueShirtGuy and Character 2 use their existing Jump + Launcher attack timeline:

- `DiveWhoosh` (frame 5): short burst at the airborne character height and kick whoosh.
- Accepted enemy hit: existing confirmed-hit pipeline plays a hit spark and body-hit sound, once per accepted hit frame.
- `DiveLanding` (frame 14, reached on ground contact): ground-impact burst at ground position and heavy landing thud, including missed attacks.

The existing AttackFeedback component handles these cues. Multiplayer authority forwards the same named events through existing reliable feedback snapshots. No new input bindings, damage, hitboxes, movement, recovery, or attack timing changes.

Tune each attack asset's Feedback section in the Frame Attack Editor or Inspector. Dive Start Prefab/Scale/Lifetime and Swing Sound/Volume control takeoff; Landing Prefab/Scale/Lifetime/Sound/Volume control landing; Impact fields control enemy contact. Uses existing Cartoon FX and Deadly Kombat library assets. Instances disable library camera shake/lights, use non-looping particles, and expire automatically.

Validation: Beat Em Up > Validate airborne headbutt dive (Play Mode). Checks both assets/facings, one-shot cues during airborne hold and recovery, landing on a miss, interruption, accepted enemy hit, client reconstruction, and existing movement/input/damage regressions. Results: Documentation/AirDiveValidationResults.txt.
