# Physical chapel upgrade rewards

Open HauntedHouse.unity after Unity recompiles. Clear a stage whose **Reward After Clear** is **UpgradeChoice**:

1. Stage clear spawns one blessing chapel. No upgrade popup appears and gameplay is not paused.
2. Walk into range and press **E / gamepad Select**. Merely standing nearby does nothing.
3. The chapel gives way to three floating world cards on pedestals. Each displays its upgrade name, short description, rarity color/label, and build tag.
4. Walk to one card and press **E / gamepad Select** again. Only that upgrade is acquired through the existing RunBuildState.
5. All reward objects disappear. The exit unlocks; walk to the exit to continue. Selection does not force an immediate transition.

Normal reward selection keeps the frame clock, time scale, movement, and camera active. Activating the chapel also preserves a held movement input. Stage scheduling waits while the reward is pending, cleared enemies are deactivated, and the player is protected from damage during the sequence. Paused/airborne/attacking/locked/dead characters cannot interact. Remote index selection and number-key shortcuts cannot bypass proximity for world choices.

Stage order, movement bounds, art heights, and existing sprites are retained. In the currently authored asset, Entrance Gate precedes Player Hub; this implementation preserves that order rather than resetting it. None and Heal rewards retain their previous behavior. Safe stages can also grant world rewards when explicitly configured; they are not forced to do so.

## Authoring

Select **Haunted House Stage Flow > Edit level / ordered stages**, select a stage, and expand **Selected stage > World Upgrade Reward**:

- **Chapel Spawn Point**: preferred ground position, default (0.9, 0.1).
- **Reward Choice Center**: preferred center of the three choices, default (0, -0.1).
- **Reward Choice Spacing**: default 1.8 world units.
- **Reward Interact Radius**: default 0.65 world units.

The system samples a reachable ground grid inside the stage's walkable bounds and horizontal art coverage. Solid CombatWall geometry limits reachability; destructible trigger props exclude individual spawn positions but do not incorrectly block walking paths. Placement relocates to the nearest valid point, keeps the chapel away from the player, and spaces choices at least 1.5 units apart. If the stage is too cramped for the choices, chapel activation remains pending for retry rather than granting or skipping a reward. A stage without any valid chapel position reports a configuration error instead of silently advancing.

The scene's **Reward Selection Controller** references:

- `Assets/EQ_Rung_BeatEmUp/Prefabs/Rewards/RewardChapel.prefab`: non-hostile interactable, no blocking collider.
- `Assets/EQ_Rung_BeatEmUp/Prefabs/Rewards/RewardChoice.prefab`: world card actor; runtime constructs its simple pedestal/panel/text children.

**Beat Em Up > Upgrades > Set up world chapel rewards** creates missing prefabs and assigns missing scene references. Existing prefab tuning and level data are preserved. RunUpgradeSetup and HauntedLevelBuilder also attach existing reward prefab references when authoring scenes.

The existing pool generation, prerequisites, incompatibilities, stack caps, synergy weighting, and modifier application remain in use. Normal draws contain three distinct choices. If fewer than three definitions remain eligible, valid options repeat to fill three physical slots; one selection still grants only one stack. A completely exhausted pool still spawns a chapel and requires interaction, then uses the existing 25%-maximum-HP healing fallback. Missing configuration does not auto-skip a reward.

Each stage ID grants one reward per run. Retrying a rewarded room cannot farm upgrades. A pending reward is cancelled cleanly on stage changes/retries/new runs; existing build persistence/reset rules remain intact. Legacy paused card presentation remains available only through explicit upgrade debug/test tools; normal stage rewards always use the physical flow.

## Art and files

`Assets/ArtAssets/Props/RewardChapel/RewardChapel.png` is a new transparent sprite generated with the built-in image_gen tool using the imagegen skill and the user's Thai spirit-house reference. It retains the red roof, white pedestal, gold ornament, stairs and chapel opening with a warm blessing glow. The prompt is saved in `Tools/WorldRewards/ChapelPrompt.txt`. Existing environment and character art was not regenerated.

Created: RewardSelectionController.cs, RewardChapelInteractable.cs, RewardChoiceWorldObject.cs, WorldRewardSetup.cs, WorldRewardValidation.cs, the two reward prefabs, chapel art/import metadata, this guide, the saved prompt, validation reports and rendered previews.

Updated: StageFlowController.cs (pending progression and E/Select routing), LevelDefinition.cs (per-stage placement), RunUpgradeController.cs (unpaused physical choice mode), RunUpgradeSetup.cs and HauntedLevelBuilder.cs (scene wiring), RunUpgradeValidation.cs and HauntedStageFlowValidation.cs (current flow and reordered-stage test fixtures), HauntedHouse.unity (one added controller), ThaiHauntedHouse.asset (placement defaults only), and StageRunUpgrades.md.

Validation menu: **Beat Em Up > Upgrades > Validate chapel world rewards (Play Mode)**. Reports and camera-rendered previews are under `Documentation/WorldReward*`. The encounter regression canonicalizes its disposable level copy to the original order; the reward regression tests the actual authored order and routes.

Unity 6000.4.6f1 validation passed **39 world reward**, **167 upgrade/build**, and **79 stage-flow** checks (**285 total**) in the isolated test project. Coverage includes all five combat rewards, real keyboard/gamepad interaction, retained movement input, reachable wall relocation, no automatic popup/selection, one-time application, retries, authored routes, safe-stage compatibility, exhausted-pool fallback, and unchanged base AttackData files. Chapel and card previews were rendered and inspected.
