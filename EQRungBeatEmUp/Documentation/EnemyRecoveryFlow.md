# Thai bad guy recovery after a juggle

The existing `EnemyHitReaction` state machine now drives the whole recovery.
It originally contained Normal, GroundHit, Launched, AirHit, Falling, Landing,
and Defeated. Knockdown, Downed and GetUp extend that same enum and component;
the original enum values remain stable.

Launched / AirHit -> Falling -> motor floor contact -> Knockdown -> Downed
-> GetUp -> Normal. Natural launcher falls and AirPunch3 finishers both use
this path. A negative vertical velocity only changes the airborne reaction
to Falling; it never grants normal AI permission.

## Existing art and timing

All references come from the current ThaiBadBoy art pack. No artwork, images,
or placeholder animation clips were created for this implementation.

| Phase | Existing asset | Duration at 60 FPS |
| --- | --- | --- |
| Airborne / falling | Knockdown frame 03 held | Until actual motor floor contact |
| Knockdown | ThaiBadBoy_Knockdown.anim, unchanged | 80 combat frames, approximately 1.33 seconds |
| Downed | Knockdown frame 06 held exactly | 45 combat frames, 0.75 seconds |
| GetUp | ThaiBadBoy_GetUp.anim, unchanged | 48 combat frames, 0.80 seconds |
| Normal | Existing Idle / Walk / EnemyPunch | Existing AI rules |

GetUp already existed, so a reversed Knockdown fallback was unnecessary.
The existing Knockdown clip includes its original final lying-down hold;
the configurable Downed delay follows the complete clip. Its nominal authored
timeline is 1.32 seconds; Unity reports a 1.33-second length including the
last 100-Hz sprite sample. The phase rounds that length up to whole combat
ticks. Neither animation's timing/data was changed.

`knockdownRecoveryDelayFrames` on EnemyHitReaction defaults to 45 and is
serialized on BadGuy.prefab. Zero skips the additional downed hold. Knockdown
and GetUp durations follow the assigned clips; editing the existing clip
timing also edits the corresponding gameplay phase duration.

`CharacterAnimation.SampleState` advances those non-looping controller states
from combat ticks. Its Animator speed stays zero while manually sampled;
each tick explicitly samples the appropriate normalized time. This keeps
sprite playback, hitstop and state timers together during slow rendered
frames. Downed and airborne sprites use a distinct pose override. Returning
to Normal releases that override, restores Animator playback, and plays Idle.

## AI, collision and damage

EnemyCombat already checks `reaction.CanAct` before chase, facing or attacks.
CanAct requires Normal, a living enemy, and a grounded motor. It remains false
through every airborne and recovery phase. Recovery also clears movement
input and locks the motor. Normal AI becomes eligible only after the complete
GetUp; it can act on the next combat tick.

The existing passive-training-dummy setting and target selection are preserved.
An enemy configured for active AI resumes chase/attack; a passive training
dummy returns to Idle. Enable active AI and assign its target to test normal
behavior. Recovery itself works in either mode.

This project's ground is the motor's separate Height coordinate, rather than
a physical floor collider. CharacterMotor simulates gravity; crossing Height
zero clamps Height and vertical velocity to zero and raises its existing
Landed event. EnemyHitReaction uses that event as the floor-contact trigger.
The character's XY lane position does not change on landing. Airborne recoil
and authored horizontal velocity are cleared at floor contact; the visual
stays at the motor ground line with the existing sprite pivot.

The existing Visual-child trigger BoxCollider2D and CombatHurtbox are retained.
They detect attacks; they do not support the character against a physics floor.
No collider architecture or shape was redesigned. Ground damage can still
reduce health while downed/getting up, but cannot replace recovery with an
ordinary stagger, launch the grounded recovering enemy, move it or reset its
phase timer. This does not add invulnerability.

CharacterHealth.Died immediately cancels recovery and enters Defeated, including
during hitstop or damage applied directly to health. Dead airborne enemies
continue falling and remain defeated on landing. No timer can resurrect them.
An explicit health Restore for training respawn clears stale reaction timers.
All phase/juggle counters belong to each EnemyHitReaction instance.

## Validation

Run `Beat Em Up > Validate enemy recovery (Play Mode)` in an idle Editor.
It uses temporary actual-prefab instances, suspends existing actors during
the test, and exits Play Mode without saving the scene. For automated runs,
use the same method in an isolated validation project.

160 automated checks passed for current asset references, natural launcher
falls, full ground-launch-jump-air combos facing both directions, exact phase
boundaries, held/sampled sprites, AI attack/chase resumption, hitstop, grounded
damage, configurable delays, death in every phase, three independent enemies,
slow rendered frames and ordinary ground stagger recovery. Results are in
`Documentation/EnemyRecoveryValidationResults.txt`. Visual sprite references
and GetUp's kneeling sample were checked through the actual prefab Animator;
subjective in-game timing remains a playtest judgment.

Existing combat regression suites also passed: 205 ground-punch assertions and
262 air-punch assertions. The air suite's former fixed 120-frame recovery wait
was updated to assert floor contact -> Knockdown and wait the full configured
Knockdown + Downed + GetUp duration. Its combat impact/cancel checks remain.

## Changed files

- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/EnemyHitReaction.cs`: phases, timers,
  floor-contact routing, held poses, death/restore handling.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/CharacterAnimation.cs`: held sprite
  and combat-timed state sampling with clean override release.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/CharacterMotor.cs`: clear grounded
  recoil/input/velocity through the existing motor.
- `Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab`: current clip/sprite references
  and 45-frame downed delay.
- `Assets/EQ_Rung_BeatEmUp/Animations/BadGuy.controller`: Knockdown, Downed and
  GetUp states added to its existing Base Layer. State transitions are driven
  by EnemyHitReaction; no automatic exit to Idle is added.
- `Assets/Editor/Combat/EnemyRecoveryValidation.cs` and `.meta`: playtest checks.
- `Assets/Editor/Combat/AirPunchPlaytestValidation.cs`: expected recovery flow
  and duration updated for the new grounded phases.
- This document and `EnemyRecoveryValidationResults.txt`: implementation report
  and verification results. `EnemyRecoveryRegressionResults.txt` summarizes
  the existing combat suites; `AirPunchPlaytestValidationResults.txt` records
  the updated air suite's individual assertions.
