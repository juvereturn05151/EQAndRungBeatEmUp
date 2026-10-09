# White Ghost: telegraph melee and distance preferences

The White Ghost boss has an additional close-range sweep. The original swipe, book projectiles, phase-two curse wave, Rusher and strong-ghost summons, warp sequence, totem protection and phase transitions remain in use. Boss HP, existing attack timelines, player combos and ordinary enemy reactions were preserved.

## Playing against the sweep

The boss crouches into her existing swipe wind-up, plays a quiet warning cue and draws an orange outline around the threatened ground area. She follows the nearest living player's side during the first 12 frames, then commits her facing direction. Retreating does not redirect or cancel the swing.

While her totem shield is open, a valid player hit during displayed frames 1–54 cancels startup and produces a 30-frame stagger. Later startup, active and recovery hits still deal legitimate damage but do not cancel the committed move. Repeated hits do not restart the punish timer. A 180-frame interrupt lockout prevents repeated hit-based startup interruptions. Perfect parries remain a timing-based escape.

A perfect parry cancels both the new sweep and the original physical swipe into a 60-frame stagger. It uses the existing player's parry freeze, sound, VFX and meter reward. Normal guard follows the existing block rules and leaves the attack running. Projectiles retain their existing deflection behavior; supernatural casts and warps cannot be cancelled by the physical-parry hook.

Parrying a shielded physical move cancels the attack but **does not open the totem damage gate**. Damaging startup interruption and punish damage still require legitimate vulnerability. Phase changes triggered by real damage wait for the physical punish window to finish.

## Tuning in Unity

Open **Beat Em Up > Enemies > Enemy AI Editor** and select the White Ghost boss or `BossEncounter.asset`. The existing **Boss Editor** exposes the same settings. Phase rows show close/far weights and the existing Attack Data cooldown field; the timeline button opens the existing Attack Data Editor. Live controls show eligible normalized percentages, distance, stagger and interrupt protection.

| Setting | Default |
|---|---:|
| Close ground distance | 1.4 units |
| Melee lane tolerance | 0.55 units |
| Startup / active / recovery | 60 / 10 / 42 frames |
| Hit-based interrupt window | Zero-based 0–53, inclusive |
| Facing lock | Zero-based frame 12 |
| Hit stagger / parry stagger | 30 / 60 frames |
| Hit-interrupt protection | 180 frames |
| Sweep cooldown | 150 frames / 2.5 seconds |
| Sweep damage / hitstop / knockback | 36 / 10 frames / 5 |
| Additional boss recovery | Existing `warpCooldownFrames`, 30 frames |
| Teleport cooldown | 180 frames |
| Old swipe / book / curse / summon cooldown | 120 / 90 / 180 / 270 frames |

Interrupt settings also expose minimum damage, allowed hit types and whether player projectiles qualify. Reaction timelines and interrupt feedback are asset references. Timing, hitboxes, guard/parry eligibility, damage and impact feedback remain in Attack Data. Timers use the existing combat clock and pause during hitstop.

The sweep now telegraphs for one second. A confirmed hit plays the existing pixel-art boss burst at scale 0.9, the heavier finisher impact sound at volume 0.9, a ten-frame freeze and a small 0.06-unit camera shake lasting 0.16 seconds. Its recoil uses the existing movement and collision system. Impact cues start only when damage connects.

The **Tools > Combat > Tune Boss Telegraph Impact** menu reapplies these sweep defaults and the extended interrupt window. Use Attack Data and the boss editors for custom tuning; the tuning menu rebuilds the sweep timeline.

## Weighted selection

Only enabled abilities with a valid target, phase, positive weight, available cooldown and appropriate assets enter the selection pool. Physical attacks require close range and lane alignment. Summons require free boss/encounter/stage capacity and run through the existing tracked spawning, clearance and wall checks. Both summon choices share their cooldown, so switching summon types cannot bypass it.

| Ability | Phase 1 close / far | Phase 2 close / far |
|---|---:|---:|
| New telegraph sweep | 65 / 0 | 65 / 0 |
| Original swipe | 3 / 0 | 3 / 0 |
| Book projectile | 15 / 55 | 10 / 35 |
| Curse wave | — | 5 / 20 |
| Summon Rusher | 10 / 30 | 4 / 10 |
| Summon strong ghosts | — | 6 / 20 |
| Teleport | 10 / 15 | 10 / 15 |

These are relative weights, normalized after eligibility checks. The old swipe adds three close-range weight units so it remains part of the fight. With every ability eligible, the new sweep has about 63% close-range preference; far-range family weights are 55% projectiles, 30% summons and 15% teleport. Cooldown/cap filtering naturally changes those percentages.

After an action and its recovery, the boss selects another weighted ability instead of always teleporting. Selecting teleport invokes the original optional small reposition and warp-out/warp-in sequence. She does not pursue distant players to force melee. Disabling `useDistanceWeights` restores the legacy automatic post-action warp flow.

## Assets and integration

New assets under `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Attacks/`:

- `Boss_TelegraphMelee.asset`: sweep timeline and existing ground-area telegraph/impact feedback.
- `Boss_MeleeHitStagger.asset`: existing Hurt_Heavy and Stunned poses over the hit punish window.
- `Boss_MeleeParried.asset`: longer authored parry reaction using the same existing pixel art.
- `Boss_MeleeInterruptFeedback.asset`: shared boss impact effect and sound.
- `../Prefabs/BossSweepImpactFeedback.prefab`: the existing animated pixel burst raised toward torso height for confirmed sweep impacts.

No new bitmap art was required. Existing Attack_Swipe, Recovery_Rise, Hurt_Heavy and Stunned sprites were re-timed into separate readable sequences. The existing boss prefab already references the encounter data; the boss chamber uses it automatically. No scene or player-prefab replacement is needed.

Runtime changes: `BossEncounterData.cs`, `TotemBossController.cs`, new `TotemBossController.Melee.cs`, and small boss-specific hooks in `CombatHurtbox.cs` and `ComboController.Defense.cs`. Editor changes: `BossEditorWindow.cs`, `EnemyAIProfileEditor.cs`, and new `BossMeleeAuthoring.cs`, `BossMeleeSetup.cs`, `BossMeleeValidation.cs`. The encounter asset adds choices/settings; four old attack assets gain cooldowns; the multiplayer catalog appends four attack IDs without reordering existing IDs.

The setup menu configures the initial weights and creates missing assets; it does not regenerate existing sweep/reaction timelines. Use the editors for subsequent balancing. Rerunning setup reapplies the initial legacy-action weights.

## Verification

`BossMeleeValidationResults.txt` records 183 passing Unity Play Mode checks in an isolated copy of the project and the actual haunted-house boss chamber. It covers both playable characters, actual combo hitboxes, startup/active/recovery timing, lockout, guard/parry/dodge/retreat, damage gates, totem waves, phase/death handling, weighted filtering, original projectile and summon execution, spawn caps and warps. The stronger sweep is also checked on both sides with both characters: no startup damage or early impact cues, 36 damage once, confirmed-hit VFX/SFX/shake, ten-frame hitstop, torso placement, and over 0.9 units of push within 20 moving frames after freeze. `BossMeleePreview/` contains camera renders from that stage.

Local multiplayer checks use two player identities and nearest-player targeting. Catalog registration and sprite/entity snapshot serialization were checked. A live two-machine Relay session was not tested.

`BossParryRegressionResults.txt` records 265 passing checks from the existing ordinary-enemy parry, grab, projectile-deflection and status regression suite. `BossMeleeAssetVerification.txt` confirms that original attack timelines, scene data, the boss prefab and existing catalog IDs were preserved. The validation menu opens the boss scene and enters Play Mode; save current scene edits before using it. It also runs the setup menu, so prefer the supplied reports when preserving custom weight tuning.
