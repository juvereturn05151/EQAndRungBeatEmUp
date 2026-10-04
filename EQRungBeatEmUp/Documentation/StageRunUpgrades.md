# Stage rewards and current-run upgrades

Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity` and press Play. The player starts in the outdoor Player Hub with an empty build. Walk to the right exit to enter Entrance Gate. Clearing the five haunted combat rooms (stage indices1–5, displayed rooms2–6) opens three upgrade cards. Pick one; it applies immediately and loads the next room. Recovery Shrine (index6, displayed room7) keeps the optional E / gamepad Select shrine interaction and grants a 50%-maximum-HP heal at its exit. The boss and Escape Lane grant no cards by default. The acquired build stays active through both rooms.

## Playing and authoring

Choose with the mouse, **1 / 2 / 3**, or **Left / Right + Enter**. On gamepad, use **D-pad Left / Right + South (A / Cross)**. Cards show a placeholder icon or an assigned sprite, name, description, rarity and current → next stack count. **Tab** or the **Current build** button opens the acquired-upgrade list; gamepad **Start** toggles that panel outside card selection.

While choosing, an owned CombatClock pause stops attacks, movement, hitstop, enemy AI, boss warps, stage scheduling and frame stepping. Time scale also pauses Animator/physics updates. Player combat requests are rejected and buffered inputs cleared. The pause releases when a valid selection applies, on run/room reset, or if the reward component is disabled. Invalid selection indices cannot dismiss the cards.

Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset`, click a stage, expand **Selected stage**, and change **Reward After Clear**:

- **None**: retain normal clear-and-walk-to-exit progression.
- **UpgradeChoice**: pause at clear, select one card, then advance immediately. A ReachExit stage triggers this at its exit.
- **Heal**: heal by **Reward Heal Fraction × Effective Maximum HP**, then follow normal exit progression. On the shrine's ReachExit stage this happens at the exit, not on entry.

Required encounters and the boss/totem completion rules still decide when a stage is clear. Each stage ID grants its reward once per run; retrying or jumping back to a rewarded room cannot farm extra cards. Use unique stage IDs. The reward enum can be extended for future reward types.

Definitions live under `Assets/EQ_Rung_BeatEmUp/Upgrades/`. Each UpgradeDefinition exposes ID, name, description, icon, rarity, tags, max stacks, weight, prerequisite definitions, incompatible definitions, and reusable modifier effects. Create another with **Create → Beat Em Up → Run Upgrade**, and add it to `HauntedUpgradePool.asset`. Fractions such as `0.2` mean +20% damage; frame/HP values use their literal units.

The pool defaults to **70 Common / 25 Rare / 5 Epic** tier weights. A tier is drawn first, then a weighted valid definition within that tier, so adding many Common assets does not distort the configured tier odds. Each draw excludes duplicate IDs, capped stacks, missing prerequisites and incompatibilities in either direction. Matching acquired tags gives a configurable 15% selection-weight bias; other builds remain available. If an authored pool has fewer than three eligible definitions, show only those valid cards. If it is fully exhausted, stage rewards fall back to a 25%-maximum-HP heal and do not lock progression. A missing pool produces a visible configuration error.

## First-pass upgrades

| Upgrade | Rarity / max stacks | Runtime effect per stack |
| --- | --- | --- |
| Heavy Hands | Common / 3 | Ground punch damage +20% |
| Combo Momentum | Rare / 2 | Damage +10% per combo step after the first |
| Finisher | Common / 3 | Final ground combo attack damage +30% |
| Juggle Master | Common / 3 | Damage against airborne targets +20% |
| Rising Force | Common / 3 | Launcher damage +25% |
| Air Finisher | Rare / 2 | Final air punch damage and downward slam speed +25% |
| Hard Head | Common / 3 | Diving headbutt damage +25% |
| Dive Shockwave | Epic / 1 | Connected dive causes an 8-damage nearby landing shockwave |
| Dive Reset | Rare / 1 | Dive landing recovery shortened by 3 frames |
| Perfect Timing | Rare / 2 | Parry window +2 frames |
| Counterattack | Rare / 1 | Next attack after parry deals +50% damage |
| Ghost Breaker | Common / 2 | Parry attacker stun +12 frames |
| Quick Step | Common / 2 | Dodge recovery shortened by 3 frames |
| Long Step | Common / 3 | Dodge distance +25% |
| Afterimage | Rare / 1 | Next attack after dodging through a hit deals +35% damage |
| Strong Guard | Common / 3 | Blockstun reduced by 25% |
| Iron Body | Common / 3 | Max HP +40; grant that much HP immediately |
| Second Wind | Epic / 1 | Survive one lethal hit at 1 HP during this run |

Damage bonuses add together before multiplying authored damage. Ground punch bonuses exclude Launcher and the airborne dive. Combo Momentum scales by authored combo step, not each collider/sample. Counterattack/Afterimage consume their bonus on the next successfully started attack, including a whiff; the bonus lasts for that attack and is not repeatedly consumed per victim. Temporary attack buffs clear at stage transitions.

Dive Shockwave requires a connected damaging headbutt. It has a compact 1.8 × 0.8 area with 0.45 lane tolerance, deduplicates victims, uses normal hurtbox/guard/invulnerability gates, and displays a short expanding ring. It does not bypass cursed-totem protection or damage totems through the area effect. Dive Reset retains impact and at least two recovery frames. Quick Step preserves the authored dodge movement and immunity windows. Strong Guard retains a minimum 10% blockstun scale. Iron Body increases effective runtime maximum HP without changing the serialized base `maximumHealth`. Second Wind remains consumed after healing or retrying a room.

## Architecture and reset

- **UpgradeDefinition / UpgradePool**: ScriptableObject authoring and constrained weighted choices.
- **RunBuildState**: player component created at runtime by the stage reward controller; tracks acquired definitions/stacks, tags, aggregated modifiers and consumed run resources.
- **RunUpgradeController**: three-card UI, current-build display, selection input, pause ownership and debug tools. It is attached to the existing **Haunted House Stage Flow** scene object with the pool assigned.
- **Runtime combat integration**: AttackHitbox makes a value copy of AttackHitboxData before applying modifiers. Base AttackData, defense data, sprites and prefabs are not modified by upgrade acquisition. AttackPlayer reads dive recovery reduction; ComboController defense reads effective timing/distance/blockstun; CharacterHealth reads effective maximum HP and the lethal-save resource.
- **Hooks**: RunBuildState exposes AttackHit, Parry, Block, DodgeSuccess, AirDiveImpact, ComboFinished, EnemyKilled and StageClear events. Reusable modifier types and dedicated hook methods implement effects without runtime switches on upgrade IDs.

`StageFlowController.Restart(true)` starts a fresh run: close cards, clear rewarded-stage history and all upgrades/modifiers/temporary buffs/consumed Second Wind uses, restore base health, and enter Player Hub. The completion-screen **R** uses this path. `Restart(false)` retries the current room while preserving the build and consumed resources. Debug stage jumps also preserve the build. Reloading the scene starts a fresh run. There is no permanent save, unlock, currency or next-run selection.

## Debug and validation

In the Editor or a development build, **F9** opens upgrade debug: force a choice, grant a specific definition, clear the build, print modifiers, jump to a stage and force a reward, or reroll pending cards. These actions also appear in the RunUpgradeController Inspector during Play Mode. Rerolls are debug only; normal card UI has none. Clearing the build intentionally resets its run resources.

**Beat Em Up → Upgrades → Set up stage run upgrades** creates missing assets and connects the scene. It preserves existing card/pool tuning and stage reward settings on rerun. **Validate stage run upgrades (Play Mode)** tests the hub and all eight haunted rooms and individual effects. The existing stage-flow regression uses a runtime clone with rewards set to None, so encounter/exit tests remain independent of randomized selection; the new suite tests actual authored rewards separately.

Before hub integration, Unity 6000.4.6f1 checks passed: **159 upgrade**, **101 combat**, **188 defense**, **56 dive**, **79 stage flow** — **583 total**. Checks include real runtime punch damage, connected dive plus landing shockwave, extended parry and punish, next-attack bonuses, dodge-through, max HP and lethal saves, shrine healing, all five reward selections, boss protection, room retry/new-run reset, prerequisites/incompatibilities, max stacks/unique cards, rarity distribution, keyboard/gamepad selection, and byte-for-byte unchanged base attack assets. Card UI uses the project's existing IMGUI approach; visual layout and mouse feel should be reviewed in the live Game view during balancing.

Results are `Documentation/RunUpgradeValidationResults.txt` and `RunUpgrade*RegressionResults.txt`. New scripts are under `Assets/EQ_Rung_BeatEmUp/Scripts/Upgrades/`; setup, Inspector and validation tools are under `Assets/Editor/Stages/RunUpgrade*.cs`. Other changed files are LevelDefinition, StageFlowController, HauntedHouse.unity, ThaiHauntedHouse.asset, the combat clock/input/hit/health/attack/defense integration points, and the existing stage regression fixture. Existing stage artwork, area bounds, enemy spawns and previous combat tuning were preserved.
