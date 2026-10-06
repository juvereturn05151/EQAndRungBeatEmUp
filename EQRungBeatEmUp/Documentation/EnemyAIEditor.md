# Enemy AI Editor

Open **Beat Em Up → Enemies → Enemy AI Editor**. This is a ScriptableObject state-list editor that drives the existing `EnemyCombat`, `CharacterMotor`, `AttackPlayer` and projectile components. It does not replace damage, knockdown, the combat clock or network authority.

## Edit an existing enemy

1. In the Project window, open `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/AI/`.
2. Select `RusherAIProfile` or `ThrowerAIProfile`. The custom Inspector shows detection/movement tuning, expanded states, transitions, attack choices and validation. Click **Open Enemy AI Editor** for a larger scrolling window.
3. Expand a state such as **Approach**. Select its **Action** and tune its movement scale, facing and duration.
4. Expand its transitions. **Go to** selects the destination state. Expand **Conditions**, change Size or use its add control, then choose condition types. All conditions in one transition must pass. Transitions are tested from top to bottom; use the arrows to change priority.
5. Add/remove states and transitions with their buttons. State IDs and attack choice IDs must be unique. Renaming an ID requires updating references to that ID; validation reports broken links. Set **Default state** to a normal state that exists.
6. Expand **Attack choices**. Assign an existing AttackData asset and tune minimum/maximum horizontal range, lane tolerance, cooldown, weight and optional prerequisites. Drag entries to reorder them. Weight zero disables a choice.
7. Select the enemy prefab and inspect **EnemyCombat → Ai Profile** to confirm the assignment. Prefab Mode edits apply to future stage spawns. Scene-instance changes only apply to that instance unless you apply the prefab override.
8. Press Play, select the spawned enemy and watch **Live AI**. The editor window can keep a specific enemy selected for debug while you edit its profile.

The original `EnemyCombat` attack/range/cooldown fields are hidden while a profile is assigned because the profile owns those decisions. Remove the profile assignment to use the original AI. **Passive Training Dummy** still disables normal decisions and attacks.

## Create or duplicate behavior

- Right-click in Project → **Create → Beat Em Up → Enemy AI Profile**, or click **Create new AI profile** in the editor window.
- Add an Idle state and choose it as Default state; add other states/transitions/actions as needed. An empty profile safely idles and warns about its missing default.
- Duplicate an example asset with **Ctrl+D** to get a useful starting point. Select an enemy in the window, choose the duplicate profile, then click **Assign profile to selected enemy** outside Play mode. Inspector assignment works too.
- Standard serialized edits support Undo. Changes to a shared profile affect every enemy using it. Duplicate it first when you want a variation for only one enemy type.

Example: a cowardly Rusher can use a `Retreat` state with Action `Retreat`, and an Approach transition to Retreat conditioned on **HP Below** with Value `0.3`. Its Retreat state can transition back on **Timer Finished**, with Duration `1`. No code is needed to compose this behavior.

## Actions and conditions

Actions include Idle, Approach, Retreat, LaneAlign, FaceTarget, Wait, Reposition, WeightedAttack, UseAttack and ProjectileAttack. Reposition chooses a direction when the state is entered. UseAttack and ProjectileAttack reference one choice ID; WeightedAttack considers all eligible choices. ProjectileAttack requires a choice marked Projectile, an `EnemyProjectileAttack` component with a projectile prefab, and a `ThrowProjectile` frame event in its AttackData.

Conditions include target presence/aggro, lane alignment, melee/projectile range, too close/far relative to Preferred Distance, cooldown readiness, damage, HP fraction, attack completion, recovery completion, knockdown/death, random chance and state timer completion. **Invert** negates a condition. HP/Random use Value as a 0–1 fraction. Random Chance rolls each time that transition is evaluated, not once for the entire state. Melee/projectile conditions check horizontal distance; add Lane Aligned when needed.

All durations are **seconds on the existing 60 FPS combat clock**. Hitstop pauses AI decisions and timers. Reaction Delay applies after entering a state before normal action/transition evaluation. Recovery Time is a global post-attack pause. Each attack additionally has its own choice cooldown, plus its AttackData cooldown converted from frames to seconds. Commit To Attack keeps the authored attack timeline intact before normal transitions run; damage still interrupts it. Disabling commitment allows authored transitions to cancel attacks.

## Live debug and safe testing

The spawned enemy Inspector and pinned editor window show current AI state, target, chosen AttackData, existing combat reaction, state elapsed time, recovery timer, per-choice cooldowns, available choices and last transition. Scene-view gizmos on the selected enemy show its aggro radius, state label and target line.

- **Force state (safe neutral only)** works when the enemy can act and is not in an attack. It cannot bypass death, hurt or knockdown.
- **Refresh AI (reset timers)** cancels the current attack and restarts the profile from its default state. Use after changing state structure/IDs during Play mode. Ordinary numeric tuning is read directly by the running AI.
- **Print current AI state** writes a diagnostic to Console.
- **Validate profile** reports missing references/defaults, duplicate IDs, unusable interrupt destinations, missing projectile events and invalid attack ranges.
- **Attack range preview** evaluates hypothetical horizontal distance and lane difference without spawning objects. It explicitly previews range only; live cooldown/prerequisite eligibility appears on the running enemy.

Hurt, Knockdown, GetUp and Dead **roles** are combat-owned interrupts. You can name/display these states in the profile, but their actions and normal transitions do not execute while the existing reaction system owns the enemy. Downed, launches and bounce phases remain visible in the Combat Reaction field. This prevents profile edits from skipping recovery or reviving dead enemies. Once recovery finishes, the AI resumes at its default normal state. A missing interrupt state uses a safe debug label.

## Examples and integration

Assigned example profiles: **Rusher, Thrower, GrapplerBruiser, Screamer, Ambusher and Prefect**. The latter four reuse their current primary attacks and can be customized with more choices. Thrower maintains distance, aligns lanes, throws using its existing release event, recovers/repositions and repeats.

Rusher chooses between two new AttackData variants using existing slash sprites: **SprintAttack** adds forward displacement during anticipation; **SlashCombo** repeats the existing slash cycle with distinct hit IDs for its second strike. Original attack assets and art are preserved. Timing/hitboxes can be edited in the existing Frame Attack editor. These are gameplay variants using existing art, not new character animations.

Boss AI remains unchanged. A normal profile can be assigned manually, but totem invulnerability/warp timing stay in `TotemBossController`; this first editor does not expose boss-specific warp actions or arbitrary code hooks.

Enemy AI runs through the same `EnemyCombat` clock listener. Online clients continue to use authoritative visual replicas and do not run independent AI. Nearest Living and Keep Until Lost target rules work with the existing multiplayer roster and offline player. Stage spawners already instantiate enemy prefabs, so profile assignments automatically apply to encounters without editing stage data.

**Beat Em Up → Enemies → Create missing AI examples and assign enemy prefabs** creates missing examples and fills empty prefab assignments; it preserves edited profiles and custom assignments. After changing AI assets or scripts, run **Beat Em Up → Multiplayer → Refresh multiplayer asset catalog** before distributing a new network build.

## Files and checks

New runtime scripts: `EnemyAIProfile.cs` (editable state/transition/choice data and validation), `EnemyAIController.cs` (per-enemy runtime state). `EnemyCombat.cs` delegates to profiles when assigned; `EnemyProjectileAttack.cs` accepts the selected profile projectile attack. New editor scripts: `EnemyAIProfileEditor.cs` (Inspector/window/live debug), `EnemyAISetup.cs` and `EnemyAIValidation.cs`. Six enemy prefabs gain profile references; assets and the two Rusher attack variants are in the AI folder. The multiplayer asset catalog is refreshed.

Run **Beat Em Up → Enemies → Validate AI profiles (Play Mode)** from a saved test scene. It creates temporary test actors and resets the test scene's gameplay state; use it for testing, not during a run you want to retain. Results are in `Documentation/EnemyAIValidationResults.txt`. Checks cover actual profile assignments/inspectors, tuning changes, movement/lane decisions, weighted attacks and cooldowns, custom duplicated behavior, invalid defaults, hurt/death safety, real projectile damage and legacy fallback. Combat/recovery/projectile regression results and the multiplayer full-run check are recorded separately. Tests run in an isolated project copy so the open working editor and unsaved scene placement are preserved.

Final results: **36 AI checks**, **164 enemy recovery checks**, **36 projectile checks**, **293 ground attack checks** and **393 air attack checks** passed. The local four-player full run, party defeat/retry, and separate network host/client full run passed; evidence is in `MultiplayerLocal4Results.txt` and `EnemyAIOnline2/`. The Windows development build succeeded. Full-run smoke tests accelerate encounter clearing with fixture damage and positioning; the AI tests independently verify real selected attacks/projectile damage. Relay/internet connectivity was not retested for this editor task.

The recovery regression now uses authored attack/cancel timing rather than old hard-coded combo frames. The party-defeat fixture accounts for the existing once-per-run lethal-hit protection upgrade before checking all players dead. These are test updates; player combat and upgrade behavior were not changed.
