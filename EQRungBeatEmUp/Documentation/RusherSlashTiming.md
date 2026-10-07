# Rusher slash timing

Open **Beat Em Up → Enemies → Edit Rusher slash frames** to edit the live Slash Combo in the existing Attack Data Editor. The shortcut opens the first impact frame and uses the Rusher prefab as the preview reference. The prefab's basic slash fallback is also tuned to the same swing timing.

Each swing uses these zero-based combat frames at 60 FPS:

| Phase | Frames | Poses |
| --- | --- | --- |
| Wind-up | 0–23 (24f / 0.4s) | Raised blade 0–9, braced blade 10–19, swing preparation 20–23 |
| Active hit | 24–27 (4f) | Fully extended blade, `Rusher_Attack_Slash1_04` |
| Recovery | 28–47 (20f) | Follow-through 28–35, lowered blade 36–47 |

Basic Slash: 48 total frames. Slash Combo: 96 total frames, with active ranges 24–27 and 72–75. Previously each swing took 40 frames and hit on frames 14–22, including the preparation pose. The longer wind-up and shorter impact align collision with the clearest visual cue.

Existing hitbox geometry, damage, hit IDs, hitstop, attack cooldowns, and player parry tuning are preserved. Press L without directional movement near impact to parry; holding Guard from the start of the wind-up becomes a normal block at contact.

Run **Beat Em Up → Validate parry stun and projectile deflection (Play Mode)** for frame alignment and actual Rusher hitbox checks in both facing directions. Results are recorded in `Documentation/ParryResponseValidationResults.txt`.
