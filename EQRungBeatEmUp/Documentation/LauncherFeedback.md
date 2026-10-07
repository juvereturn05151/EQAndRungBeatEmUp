# Launcher feedback

Both character launcher attack assets use the existing Swing event at combat frame 6 (first active frame). One upward sweep and punch whoosh plays even on a miss; accepted enemy hits add a stronger impact spark and finisher body-hit sound through the existing hit-confirmed event. Held active frames and recovery do not replay the sweep. Interrupted startup does not play it.

Feedback fields in the Frame Attack Editor / Inspector:
- Swing Prefab, Offset, Rotation, Mirror With Facing, Scale and Lifetime control the uppercut sweep.
- Swing Sound/Volume control the whoosh.
- Impact Prefab/Scale/Lifetime and Sound/Volume control confirmed contact.

Existing Cartoon FX Plain Spiral/Cross and Deadly Kombat punch-whoosh/body-hit assets are reused. Effects disable library shake/lights, use non-looping particles and expire automatically. The sweep mirrors horizontally so it remains upward in either facing. Online clients reconstruct the same cosmetic cue through the existing Swing/confirmed-hit feedback snapshots; no network protocol changes.

No animation pose, damage, hitbox, launch-force, grounded physics or recovery changes.

Validation results: LauncherFeedbackValidationResults.txt. Run Beat Em Up > Validate grounded launcher and airborne follow-up (Play Mode).
