# First boss encounter and Boss Editor

The existing WhiteGhostBoss prefab and Stage07_WhiteGhostBossChamber encounter now use `BossEncounter.asset`. Existing player combat, hurtboxes, health, reactions, AttackPlayer timelines, parry feedback, projectile deflection, Screamer ground-wave collision, and StageFlowController tracked reinforcement spawning are retained.

## Files created

- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/BossEncounterData.cs`: serialized boss configuration, phase actions and weights.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/BossTotem.cs`: adapts existing destructible props to the boss mechanic and optional respawn.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/TotemBreakWave.cs`: typed expanding radial wave, separate from directional projectiles.
- `Assets/Editor/Combat/BossEditorWindow.cs`: visual editor, controller Inspector entry, Scene View handles, Play Mode controls.
- `Assets/Editor/Combat/BossEncounterSetup.cs`: idempotent asset setup; preserves existing configured attack and projectile values.
- `Assets/Editor/Combat/BossEncounterValidation.cs`: Unity Play Mode checks and an isolated batch-mode entry point.
- `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/BossEncounter.asset`.
- Attack assets `Boss_Book`, `Boss_Swipe`, `Boss_Summon`, `Boss_CurseWave`, and six `Boss_*Feedback` assets in the existing HauntedHouse Attacks folder.
- `BossBookProjectile.prefab`, `BossCurseWaveProjectile.prefab`, and pixel effect prefabs / animation clips / controllers created by setup. These use existing art; no enemy prefab copies are introduced.
- This report and `Documentation/BossEncounterValidationResults.txt`.
- `Documentation/BossRegressionResults.txt` and `Documentation/BossReinforcementRegressionResults.txt`: additional regression outcomes.
- Unity `.meta` files accompany the new assets and scripts.

## Files modified

- `Scripts/Stages/TotemBossController.cs`: replaces the previous all-Totems-destroyed damage gate and periodic two-position teleport.
- `Scripts/Stages/DestructibleObject.cs`: reusable prop respawn method.
- `Scripts/Stages/LevelDefinition.cs`: optional per-Totem break-radius override.
- `Scripts/Stages/StageFlowController.Reinforcements.cs`: common capped spawn path for support and boss reinforcements; binds runtime Totems after the boss is tracked.
- `Scripts/Stages/StageFlowController.cs`: boss-clear progression no longer requires unused Totems to be destroyed.
- `Scripts/Combat/EnemyCombat.cs`: yields AI decisions to the configured boss controller.
- `Scripts/Combat/EnemyProjectileAttack.cs`: uses boss-selected timeline and optional locked aim; tracks boss projectiles for death cleanup.
- `Scripts/Combat/CombatProjectile.cs`: point-target initialization for locked aim, retaining existing movement and collision.
- `Scripts/Combat/CharacterHealth.cs`: encounter-owned damage protection covers direct damage as well as hurtbox hits.
- `Scripts/Combat/CombatHurtbox.cs`: shield-hit feedback when the boss rejects an incoming attack.
- `Levels/HauntedHouse/Prefabs/WhiteGhostBoss.prefab`: binds configuration and existing projectile attack adapter.
- `Levels/HauntedHouse/ThaiHauntedHouse.asset`: four corner Totem placements and updated encounter notes.
- `Assets/Editor/Stages/HauntedStageFlowValidation.cs`, `Assets/Editor/Stages/RunUpgradeValidation.cs`, and `Scripts/Multiplayer/MultiplayerSmokeTest.cs`: replace obsolete all-Totems-destroyed assertions with physical wave-contact fixtures.

Paths above are relative to `Assets/EQ_Rung_BeatEmUp` unless they start with `Assets`, `Documentation`, or `Scripts` (the latter denotes its Scripts subfolder). Pre-existing unrelated local changes are retained.

## Runtime architecture

1. **States:** Inactive → WarpOut → Warping → WarpIn → Arrival → SelectAction → Acting → Recovery → optional SmallMove → WarpOut. PhaseTransition is a short boundary between actions; Dead cancels the loop. The existing CombatClock ticks the controller, AttackPlayer plays attacks, CharacterMotor handles movement, and EnemyHitReaction handles hurt/death. EnemyCombat does not run its ordinary AI simultaneously.
2. **Phases:** two independently serialized weighted lists. Phase 1 contains Book, Swipe and SummonRusher. Phase 2 retains these and adds CurseWave and SummonStrongGhosts. Default threshold is `Current / EffectiveMaximum <= .5`. A latched flag prevents repeated transitions, even if HP later rises. The current action may finish before the transition pose; phase membership changes immediately. No HP restoration occurs.
3. **Weighted selection:** filter disabled, zero-weight, missing-timeline, missing-projectile, wrong-phase and capped-summon choices; sum the eligible weights; sample across their cumulative weight intervals. An empty pool produces recovery and another warp, without locking the fight. Each phase has its own weights.
4. **Warp positions:** an arbitrary-length list of enabled world XY points, initially four corner locations plus center. The previous index is omitted when repeat is disabled and another valid point exists. A single point remains usable. Offsets are optional. Destination clamping includes the boss ground footprint, and solid CombatWalls reject blocked points. Warp-out hides the sprite, relocation happens in Warping, and warp-in restores it. Warp timing and arrival/cooldown are combat frames. SmallMove has separate probability, distance and speed, remains bounded, and checks walls.
5. **Book:** the existing Notebook projectile runtime is configured as a purple haunted Book variant with six existing flight sprites. A 30-frame windup locks the target ground position and jump height at attack start, then `ThrowProjectile` releases one shot. The shared swept collision, damage, hitstun, knockback, lifetime, parry and deflection settings remain editable. Successful deflection reverses velocity and changes ownership/team. The boss health/hurtbox protection remains authoritative, including for a deflected Book.
6. **Swipe:** a shared AttackData timeline with 24 startup / 6 active / 30 recovery frames. Its editable hitboxes default to 18 damage, 20 hitstun and 1.2 knockback; they can be parried. Use the existing Attack Data Editor for sprite frames, timing and hitboxes. The normal parry damage negation and VFX/SFX remain intact.
7. **Rusher:** a 36-frame summon telegraph releases via `BossSummon`. Default count is one. Spawned Rushers are the existing prefab and belong to the boss's tracked encounter wave. Boss minion cap, encounter cap and stage cap all apply. Authored points are clamped and tested for living-player/enemy clearance and solid walls; missing points use the existing reinforcement candidates. No safe location means no spawn.
8. **Totems:** the four existing DestructibleObject props keep their sprites, HP, damage reception and collider. BossTotem listens to their Broken event, triggers feedback and starts an expanding break wave. They have no normal enemy AI. Default mode is **Respawn**, delay **600 frames**, respawn HP **20**. OneShot disables reuse.
9. **Wave contact:** the wave radius grows from zero to 1.25 world units over 45 combat frames by default. Each tick tests the boss's ground position against the radial front swept between the previous and current radius. The origin is included on the first tick. The typed wave must belong to that boss, be active and actually reach its ground position. A distant destruction does not call a global vulnerability switch. The displayed ring follows the collision radius. The wave is intentionally radial; Curse Wave is directional.
10. **Seven-second window:** a separate `VulnerabilityRemaining` overlay starts at **420 combat frames**. It is independent of the action state. The contact tick does not consume a frame. It counts combat ticks, including local hitstop, and pauses when CombatClock pauses. At 60 combat FPS this is seven seconds; changing the combat FPS changes the seconds represented by 420 frames. Additional waves default to Ignore; RefreshTimer is optional.
11. **Returning to protection:** the timer reaching zero reinstates hurtbox and direct-health protection and the shield tint. It does not restore HP, reset phase, cancel the current attack or trigger a special teleport.
12. **Phase 2 activation:** damage crossing the inclusive 50% threshold latches Phase2 once; phase feedback and a 45-frame transition pose follow at a safe action boundary. Vulnerability remains the same overlay in both phases.
13. **Curse Wave:** reuses `EnemyProjectileAttack` with `forwardOnly` and `CombatProjectile.groundWave`, exactly the Screamer forward-wave path. It has a directional corridor telegraph, locks facing, travels along X, and tests a swept rectangle on ground XY. Default full width/depth is 1.5 / .7. The player can sidestep it. Its existing six-frame CurseWave art replaces Screamer art. Default damage is 20 and player Stun is 90 frames, using `HitType.Stun` and the existing player reaction. It is never a radial AoE.
14. **Strong Ghosts:** default one GrapplerBruiser and one Thrower, using existing prefabs, profiles, grab/projectile components and reactions. Counts are independent. Random, GrapplerFirst or ThrowerFirst priority resolves scarce slots, and the shared spawn path checks the cap before every spawn. It does not exceed caps to fulfill both counts.
15. **Parry exception:** the existing `EnemyHitReaction.CanBeParryStunned` excludes actors with TotemBossController. That rule is retained. Parrying Swipe still protects the player and produces feedback, but does not enter the boss's normal enemy Stunned state or stop its attack.
16. **Death:** stops boss action selection, cancels AttackPlayer hitboxes/telegraphs, immediately deactivates tracked projectiles (including deflected ones), and stops Totem waves/respawns. EnemyHitReaction provides the existing death sequence. `onDefeated` fires once. Existing wave/encounter completion releases the camera and exit. Remaining minions default to **must be defeated**; enabling `despawnMinionsOnDeath` deactivates tracked boss minions instead. Unused Totems do not block progression.

## Boss Editor workflow

17. Open **Tools → Combat → Boss Editor**. The editor loads the configured boss asset and HauntedHouse level. Use **Preview boss arena in Scene View** to display the correct arena without changing the active scene's runtime actors. Clear the preview when finished. The controller Inspector also has an editor button.
18. Drag the five labeled cyan warp handles; disable individual points in Warp Points. Use Add Warp Point / Remove Last Warp Point, or the serialized list controls, to change the list. Drag purple Totem handles to move their level placements. Click a Totem marker or Select Totem button to choose it; in Play Mode this selects the actual prop. Its HP is shown. Each radius slider sets that Totem's own radius override; 0 in the Totem fields uses the shared default radius. Green handles edit minion spawn locations. Book spawn, Swipe volume, Curse width/depth and small-move range are drawn too.
19. Each PHASE section exposes action, Enabled, Weight and Attack. Change weights there; `0` excludes a choice. The phase threshold, vulnerability frames, additional-wave policy, counts, caps, offsets, timings, reuse mode and VFX/SFX hooks appear below. Open the Book / Curse projectile buttons for their shared projectile Inspector. **Open frame timeline / hitboxes** selects the attack in the existing Attack Data Editor.
20. In Play Mode use Force Warp, Force Book, Force Swipe, Force SummonRusher, Force Phase 2, Force CurseWave, Force SummonStrongGhosts, Make Boss Vulnerable/Invulnerable, Set Boss HP to 49%, and individual Simulate break buttons. Force actions honor availability, hitstop and current reaction state; unavailable requests produce a warning. Debug vulnerability deliberately bypasses the wave for testing and is compiled only in the editor. Phase 2 actions require Phase 2 first.
21. Runtime readout shows phase/state, HP, vulnerability remaining, warp index/count, current/previous action, alive/broken Totems, active boss minions and per-archetype encounter counts. Serialized weights remain visible above. Config asset edits apply to subsequent decisions/releases without reloading the stage. Active waves retain the radius/duration captured at creation; active timers retain their current frame count. Position and radius handles record Undo and mark their owning asset dirty. Save boss and level assets after tuning. Asset changes in Play Mode persist; ordinary runtime transform changes alone do not. Totem HP edits preserve its current health fraction, and placement/radius Undo also updates the live Totems.

## Exact Unity testing steps

22. Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity` in Unity 6000.4.6f1. Allow compilation. Run **Tools → Combat → Validate First Boss (Play Mode)**. The suite enters Play Mode, uses the actual Stage 7 encounter and cloned tuning data, steps CombatClock deterministically, writes `Documentation/BossEncounterValidationResults.txt`, and exits Play Mode. It tests real collision/defense, wave misses/contact, exact duration, phases, caps, death progression and serialization Undo/Redo/reload. Save any wanted runtime state before invoking the suite.

For interactive verification, enter Play Mode and jump to **White Ghost Boss Chamber** with the existing stage/encounter editor or play to it. Open Boss Editor and:

- Observe warp-out disappearance, warp-in feedback and all five destinations. Disable all but one point, test repeat behavior, then restore points. Move a point beyond bounds and confirm runtime clamping.
- Force each Phase 1 action. Parry Swipe; confirm damage negation without boss Stun. Parry a Book and observe reversed ownership; verify invulnerability still blocks boss damage. Temporarily disable deflection on the Book prefab and retest, then Undo.
- Position the boss outside a Totem radius and simulate that Totem break: it remains protected. Repeat with the boss near the expanding front: the readout starts at 420f, the boss remains active, and protection returns when it reaches zero. Test Ignore and RefreshTimer, OneShot and Respawn.
- Set HP to 49%; confirm one transition and all five Phase 2 actions. Force Curse Wave and test both standing in its corridor (Stunned) and sidestepping. Force Strong Ghosts; inspect actual Grappler / Thrower behavior. Set Max Boss Minions to one and test each priority.
- Kill the vulnerable boss while a Book or Curse Wave is active; verify attack cleanup. With remaining-minion cleanup disabled, defeat the minions to release the exit; with cleanup enabled, verify they deactivate. Confirm camera unlock and unused Totems do not hold the exit.
- In Edit Mode preview the arena. Drag every point type, resize the radius, change a weight and press Ctrl+Z/Ctrl+Y. Save and reload the level/config asset. Confirm placement and tuning persistence and inspect readability of pixel effects and sound cues.

Manual visual checks are separate from automated results; the automated suite does not claim that handle dragging or perceptual VFX quality was exercised by a human.

The dedicated boss suite passed **85 automated checks** in an isolated Unity batch-mode copy. See `BossEncounterValidationResults.txt` for individual assertions and `BossRegressionResults.txt` for broader regression outcomes. The older stage-flow and upgrade suites currently fail before reaching the boss because their early-stage fixtures do not drive the currently authored encounter/reward flow. Those runs are not counted as passing boss or full-game coverage.
