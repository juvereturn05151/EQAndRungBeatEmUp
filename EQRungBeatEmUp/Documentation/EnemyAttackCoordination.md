# Enemy attack coordination

Enemies continue using their existing movement, target selection, weighted attack choices, telegraphs, frame events and reactions. An encounter coordinator limits **committed attacks**, not the number of enemies moving around the arena. This is priority arbitration rather than a turn order.

## Runtime architecture and timing

StageFlow creates one `EnemyAttackCoordinator` under each encounter's stage runtime. Authored spawns, Prefect backup and boss summons receive that same coordinator through `SpawnTrackedEnemy`. Replacing a stage disables and destroys this runtime; no token data enters permanent saves or run blessings. Standalone enemies without a coordinator retain their existing AI.

AI first selects an eligible weighted attack. It submits a request containing owner, target, AttackData, category requirements and position score. The request is retained while the enemy remains a candidate. On the next combat tick, the coordinator ranks candidates and reserves all required categories atomically. AI rechecks target, range, prerequisites, cooldown, reaction and walls before starting the original AttackPlayer timeline. Failed validation or playback releases the reservation.

The coordinator runs at order 4, before boss decisions (5) and normal enemy AI (10), on the existing CombatClock. Gaps, waiting age, CC grace, recent penalties and watchdogs use combat ticks. No coroutine/wall-clock timer controls permissions. Individual attack frames and animation assets have not been rebalanced.

Default slots are Melee 1, Ranged 1, CrowdControl 1, Support 1. A 20-frame **global gap between grants** spaces new commitments across categories. The gap starts at approval, not at projectile destruction or enemy cooldown expiry. Different categories can still overlap during longer committed attacks. A zero gap allows simultaneous independent categories. Capacities of zero disable that category.

## Atomic categories and enemy configuration

| Enemy action | Required categories |
| --- | --- |
| Rusher slash combo / sprint | Melee |
| Thrower notebook | Ranged |
| Screamer forward wave | Ranged + CrowdControl |
| Grappler grab | Melee + CrowdControl |
| Ambusher leap grab | Melee + CrowdControl |
| Prefect Call Backup | Support |
| Prefect Push | Melee |
| Boss actions | Ignore Coordinator enabled |

Requirements live in each `EnemyAIAttackChoice.coordination`, not enemy-name checks inside the coordinator. The installation utility assigns the existing profiles' initial category mappings; later tuning is data-driven. Legacy EnemyCombat also has `legacyCoordination` for attacks without an AI profile.

The boss uses its own state machine and action coordination data. Its actions default to Ignore Coordinator and do not occupy minion slots. Encounter **Allow Boss To Ignore Coordinator** permits that bypass by default. Turn it off to deliberately coordinate the boss using its action categories. Boss minions always receive ordinary encounter coordination unless their own action data is explicitly configured to bypass it.

Denied melee enemies move between depth lanes and seek alignment; denied projectile enemies also maintain preferred horizontal spacing. Approach, retreat, normal lane alignment and Prefect escape behavior require no reservation. Pending eligibility does not freeze the actor or change telegraph lengths. Candidates can leave and return as positions change.

## Priority, starvation and repeats

Priority combines waiting age × Waiting Priority Bonus, a distance score, lane alignment, a ready-action bonus, per-action Priority Modifier and a small random variation fixed for that request. Only eligible actions enter arbitration. Recent Attack Penalty decays over Recent Attack Penalty Frames. LastAttacker receives an additional penalty while another candidate is waiting.

Waiting history survives temporary loss of range/movement states and resets on a successful commitment or encounter reset. This gives long-waiting enemies increasing opportunity. The last attacker is penalized, never forbidden: a lone eligible enemy continues attacking. Capacity-blocked candidates cannot partially claim another category, and a blocked high-priority candidate does not prevent an independent eligible action from being approved.

## Recovery, interruptions and watchdog

Reservations cover the entire authored attack timeline, including recovery, followed by the AI profile's existing recovery delay. They are released before the following attack opportunity. Thrower/Screamer projectile lifetimes do not extend the owner's reservation. Grappler's successful-grab branch retains its original reservation across the internal timeline switch; grab hold/release remains part of the commitment.

Stop/cancel, hit reaction, armor break, parry Stun, death, disable, despawn, invalid target and refresh release tokens. Stage/encounter reset and coordinator disable cancel granted attacks and clear pending requests, targets and recent history. When all tracked targets die, the runtime clears; in co-op, invalid-target attacks cancel while attacks against living teammates remain eligible.

Unconsumed grants expire after two additional combat ticks. Candidates not refreshed for more than two ticks are dropped. A configurable Maximum Token Hold Frames (default 900) cancels an attack and releases its reservation if it exceeds the failsafe. Editor/development builds log one watchdog warning; ordinary production logs remain quiet. This hard limit should stay above the longest authored attack plus recovery and expected hitstop.

## CC chain protection and multiplayer

Pause New Crowd Control While Player Disabled defaults true. Stunned, Grabbed, KnockDown, Downed and GetUp targets cannot receive a new CC commitment. The coordinator tracks transitions separately for every targeted player. After recovery it blocks CC for 45 combat frames by default. Optional Pause All Attacks While Player Disabled also pauses new Melee/Ranged/Support commitments. These policies affect new grants, not projectiles or attacks already in flight.

The host owns coordination, AI and damage in online co-op. Clients continue receiving the existing authoritative snapshots and attack feedback; they do not arbitrate tokens or run normal enemy AI. Local co-op uses the same target-aware API. Encounter capacities are shared across its players, while CC-disabled/grace checks use the request's actual target.

## Editor controls

1. Open the existing **Encounter Editor** or the LevelDefinition inspector and select an encounter. Its **Attack Coordination** section exposes enabled state, four simultaneous counts, global gap, CC grace, disabled-player policies, boss bypass, watchdog, priority values and debug toggles. Serialized edits support Undo/Redo and asset persistence.
2. Open **Beat Em Up → Enemies → Enemy AI Editor**, select a profile and expand an attack choice. **Coordination** shows the four category checkboxes, Priority Modifier and Ignore Coordinator. Live EnemyCombat inspection reports held/waiting tokens alongside existing state/cooldown information.
3. Expand a wave to enable **Override Attack Coordination** and edit its settings. The most recently started wave selects the shared encounter's policy. A wave without an override restores encounter defaults. For overlapping waves, this policy also applies to surviving enemies and reinforcements from earlier waves. Lowering capacity does not retroactively cancel existing attacks; new grants wait for occupancy to fall.
4. Enable **Show Debug** for the runtime occupancy/waiting-score panel and Scene View labels over all enemies. Green labels indicate granted categories; yellow labels indicate waiting requests. Enable Gizmos in Scene View. Enable **Verbose Debug** for request/approval/changed-denial/release logs. Runtime panels and logs compile only in Editor/development builds; Scene labels are editor-only.

The installation menu **Beat Em Up → Enemies → Attack Coordination → Install category defaults and create test encounter** restores the initial category mappings and rebuilds the test fixture. It is a setup command; do not use it after custom category/test-fixture tuning unless you intend to restore those defaults. Existing encounter intensity settings and attack frames are preserved.

## Exact Unity test steps

1. Open `Assets/EQ_Rung_BeatEmUp/Scenes/AttackCoordinationTest.unity`, then press Play. The fixture contains two Rushers and one each of Thrower, Screamer, Grappler, Ambusher and Prefect. Its debug display is enabled. Move around using existing controls and observe attacks and waiting movement.
2. Inspect **Stage runtime → Attack coordinator — Mixed attack pressure**. Confirm each occupancy stays within 1/1, and Screamer/Grappler/Ambusher claim both required categories before telegraph. Watch independent attacks overlap after the 20-frame grant gap.
3. Parry a grab, interrupt Ambusher anticipation and break Grappler armor. Reservations should release with the reaction. Miss a grab and observe release after its existing recovery. Kill/disable an attacking enemy and confirm its slots free.
4. Let the player be stunned, grabbed and knocked down. Confirm another CC action does not start while disabled or during the configured recovery grace. Test against two players to confirm protection follows the targeted player.
5. Stop Play. In the Encounter Editor, set Melee to 2 and Ranged to 2 while retaining CC at 1. Undo/Redo, save/reopen the scene, then Play again. Repeat with coordination disabled, gap 0/20 and a wave override. The new intensity should use the saved settings.
6. Run **Beat Em Up → Enemies → Attack Coordination → Validate tokens and mixed encounter (Play Mode)**. This uses controlled fixtures and actual AI/frame events to verify atomic reservations, caps, gap boundaries, priority/repeats, disabled-player/grace behavior, target-aware co-op, interrupts, watchdog, emitted projectiles, missed grabs, boss bypass, reinforcement ownership, mixed combat, Undo/Redo and reload persistence. The result is `Documentation/AttackCoordinationValidationResults.txt`.
7. Open the regular boss encounter. Boss Book/Swipe/Curse Wave remain independent of regular slots; summoned Rusher/Grappler/Thrower use the encounter coordinator. Returning to PlayerHub or restarting must leave no reservations from the previous room.
8. For online testing build matching development players with `MultiplayerValidation.BuildDevelopmentPlayer`, then run `Tools/Multiplayer/RunBossNetworkValidation.ps1 -Players 4 -Relay -MixedCharacters`. Use matching regenerated MultiplayerCatalog content on every peer. Cross-machine latency/feel remains a manual playtest.

## Files

Created runtime: `Scripts/Combat/AttackCoordinationData.cs`, `EnemyAttackCoordinator.cs`.

Created editor: `Editor/Combat/AttackCoordinationEditor.cs`, `AttackCoordinationSetup.cs`, `AttackCoordinationValidation.cs`.

Created fixture: `Levels/AttackCoordination/AttackCoordinationTest.asset`, `Scenes/AttackCoordinationTest.unity`, associated metadata and validation reports.

Modified runtime: `EnemyAIProfile`, `EnemyAIController`, `EnemyCombat`, `EnemyHitReaction` and its Stun partial; `LevelDefinition`, `StageFlowController`, its Reinforcements partial; `BossEncounterData`, `TotemBossController`; the opt-in development `BossNetworkValidation` harness.

Modified editor/data: `EnemyAIProfileEditor`, `EncounterPreview`, `EnemyAIValidation`; six enemy AI profile assets, BossEncounter.asset and MultiplayerCatalog.asset. The AI validator now accepts Screamer's existing SpawnScreamWave event, and its cooldown regression uses actual attack completion rather than manually reporting completion after cancellation. Attack animations/timelines and existing authored stage order/geometry are preserved.

## Validation evidence

Unity 6000.4.6f1, using an isolated copy of the project:

- 362 focused checks passed, including a live mixed encounter, atomic category leases under live data edits, exact global-gap/CC-grace boundaries, wave override/restoration, interruption/expiry cleanup, target-aware co-op, editor Undo/Redo and asset reload.
- 36 shared AI regression checks passed.
- 163 Grappler and 131 Ambusher regression checks passed, preserving original grabs, telegraphs, armor, hit reactions and recovery.
- Windows development player built successfully. Real two-player and four-player DTLS Relay validation passed host-owned boss/minion coordination, category caps across combat, client replication, exact vulnerability timing, death and encounter cleanup, alongside the existing Hub/character/skill tests.

Reports: `AttackCoordinationValidationResults.txt`, `AttackCoordinationAIRegressionResults.txt`, `AttackCoordinationGrapplerRegressionResults.txt`, `AttackCoordinationAmbusherRegressionResults.txt`, `MultiplayerBossRelay2*Results.txt` and `MultiplayerBossRelay4*Results.txt`.

Relay peers ran as separate player processes on this computer. Cross-machine latency/feel and manual visual review of Scene labels and debug panels remain playtesting steps; automated editor checks cover serialized controls, debug descriptions, Undo/Redo and persistence.
