# Player Hub sanctuary

Open `Assets/EQ_Rung_BeatEmUp/Scenes/MainMenu.unity`, choose a mode/character and start. The session now loads **PlayerHub.unity**. You enter a physical sanctuary, walk between its stations and interact to begin World 1. You can also open `PlayerHub.unity` directly for single-player authoring/playtesting.

## Layout and art

The Hub is 38 world units wide, approximately 5.3 reference gameplay screens. From left to right:

| Place | Ground position | Interaction |
| --- | --- | --- |
| Buddha / HubPlayerSpawnPoint | (-15.2, 0) | Initial spawn and every return |
| Character wardrobe / mirror | (-7.6, 0) | Change character |
| Training blessing pavilion | (0, 0) | Permanent base stats |
| Elephant skill shrine | (7.6, 0) | Equipped character's skill power |
| World 1 entrance | (15.2, 0) | Start the haunted-house run |

Five separate pixel-art plates are saved at `Assets/ArtAssets/Environments/PlayerSanctuary/HubPanel0.png` through `HubPanel4.png`. They were created with the built-in imagegen tool using the original Hub environment as the style reference. Their prompt set and source paths are in `PlayerSanctuaryArtPrompts.md`. Each plate scales uniformly; adjacent plates overlap by 0.5 world units with a left-edge blend material. The same blend material is applied to online replicas. No small scene is stretched over the full sanctuary.

`Assets/EQ_Rung_BeatEmUp/Hub/SanctuaryEnvironment.prefab` contains the modular artwork and named `HubPlayerSpawnPoint`. The Buddha is calm sanctuary imagery above the spawn: there are no hurtboxes, destructible components or enemy components on the environment. Existing Safe-stage protection rejects combat and direct damage. The Hub has no normal encounters, destructibles, combat camera lock or run-upgrade reward choices.

The existing StageFraming camera follows the player horizontally and clamps to [-19, 19]. Walkable lanes remain [-0.4, 0.65], with horizontal movement bounds [-18.5, 18.5]. Character scale and movement retain the shared player prefab settings.

## Interaction and character changes

Approach within 1.2 units of a station and press **E / gamepad Select**, using the existing New Input System interaction path. Station windows support mouse buttons, arrow keys / D-pad to select, Enter / A to confirm, and Escape / B or E / Select to close. Movement and combat input are suppressed while that player's station window is open.

The character station shows both portraits, names, skill names and the currently selected character. Selection immediately applies PlayableCharacterData to the shared player components, so sprites, animation controller, attacks, defense and skill update without restarting the Hub or duplicating scene-loading logic. The choice is saved and restored on later launches. Local co-op has separate slot profiles and windows; online players can modify only their own character and upgrades. The online host starts the party at the gate.

## Permanent progression and tuning

The reusable `MetaProfile` is separate from `RunBuildState`. Both characters share permanent base stats, while their skill levels are separate. All costs, limits and effects are in `Assets/EQ_Rung_BeatEmUp/Hub/PlayerHub.asset` (PlayerHubDefinition).

Prototype defaults:

| Upgrade | Effect per level |
| --- | --- |
| Max health | +10 maximum HP |
| Attack power | +5% outgoing damage |
| Defense | 2.5% damage reduction |
| Meter gain | +10% accepted-hit and parry gains |
| Equipped skill | +10% skill damage |

Each upgrade caps at level 10. Cost is 10 essence plus 10 per existing level. A new profile starts with 60 essence, allowing the first upgrades to be tried immediately. A cleared combat room awards 10 essence once per run. These economy values are provisional and editable in PlayerHub.asset.

Health adds to the existing effective maximum alongside run bonuses. Defense uses the shared health damage path. Attack/skill power use the accepted-hit path (and destructible hit path), without editing shared AttackData assets. Meter gain scales existing hit/parry gain; the one-bar cap and one-bar skill cost remain unchanged. Projectile skills retain their source-skill identity, so deflected ordinary enemy projectiles do not receive an equipped-skill bonus.

Profiles are versioned JSON in `Application.persistentDataPath/PlayerMeta0.json` through `PlayerMeta3.json`; online uses the local player's PlayerMeta0 file, and couch players use their slot number. Writes use a temporary file and atomic replacement with a `.bak` backup. Loading falls back to the backup if the primary is invalid. This is local device persistence, not UGS Cloud Save. Do not delete these files to reset a run.

The host validates station proximity, player ownership, character index, upgrade index, affordability and level cap. Clients provide their saved profile on joining, receive host-approved updates in snapshots, and save their own updated profile locally. This prototype trusts local save progression; it does not introduce a server-account economy or save anti-tampering system.

## Starting runs and returning

The gate opens a confirmation window; walking close to it alone cannot start a run. Begin Run enters the existing Entrance Gate stage, retaining the haunted-house stage order and combat/reward systems. PlayerHub.unity hosts the shared level/stage loader, so all characters use one loading path. Existing HauntedHouse.unity remains usable for regression and authoring.

On single-player death, or when all co-op players are defeated, a 1.8-second fade returns the party to stage 0 at the Buddha spawn, facing right. Living co-op teammates continue their run after an individual defeat. Health and meter restore; temporary run blessings, combo state, rewards, enemies and stage-owned projectiles clear. Permanent character selection, base stats, skill levels and essence remain.

The host can use **RETURN HUB** during a run. The end-of-run button also returns to the Hub. Direct scene play supports R after defeat/completion. These returns use the same Buddha point, never the last station visited. The network session stays connected during stage returns.

## Editing and testing

Select **Hub Editor Preview** in PlayerHub's hierarchy to see the cyan camera bounds, green movement area, spawn marker and yellow interaction radii. Scene handles edit the spawn/station coordinates in PlayerHub.asset, with Undo. The existing level editor also shows stage bounds. Edit each visual module through SanctuaryEnvironment.prefab. `Beat Em Up → Hub → Create sanctuary defaults` rebuilds default placement/art references; use it for setup, not after custom placement tuning.

Run **Beat Em Up → Hub → Validate sanctuary (Play Mode)**. It uses isolated temporary save files, opens PlayerHub and checks scene startup, both character choices, movement/camera, physical interaction, safety, independent permanent upgrades, affordability/caps, accepted-hit effects, persistence/backup recovery, World 1 entry and death/explicit returns. Reports are in `PlayerSanctuaryValidationResults.txt`; actual camera renders are in `PlayerSanctuaryPreview`.

For manual testing: walk from Buddha to the wardrobe, change to each character, visit the base-stat and skill stations, purchase upgrades, then use the gate. During combat verify HP/damage/meter changes, die, and confirm the Buddha return and cleared run build. Stop/relaunch and confirm the selected character and upgrades persist. Use a gamepad to check D-pad/A/B station navigation and Select interaction.

For online testing, build with the existing `MultiplayerValidation.BuildDevelopmentPlayer` editor utility, then run `Tools/Multiplayer/RunBossNetworkValidation.ps1 -Players 2 -Relay -MixedCharacters` (or 4). The development harness uses isolated saves and real Authentication/DTLS Relay. It exercises remote station input, character switching, permanent health purchases, client-side saving, guardian feedback and boss encounter replication. Peers require the matching protocol/content catalog (`GhostFair/4`). Cross-household input feel remains a manual test.

## Validation results

Validated in Unity 6000.4.6f1 using an isolated copy of this project and temporary meta saves:

- 52 sanctuary checks passed: scene/art, physical stations, both characters, camera bounds, safety, four permanent stats, skill power, affordability/caps, save/backup recovery, run entry and death/explicit returns.
- 167 run-upgrade regression checks passed with the current authored encounter zones and stage order. Existing AttackData assets remained unchanged.
- 581 character/combat regression checks passed for both characters, including defense, skill effects and boss vulnerability.
- Development Windows player built successfully. Actual 2-player direct connections and 2-player/4-player DTLS Relay runs passed remote character changes, permanent health purchases, snapshot replication, each client's local persistence, both guardians and the boss encounter.

Evidence: `PlayerSanctuaryValidationResults.txt`, `PlayerSanctuaryUpgradeRegressionResults.txt`, `PlayerSanctuaryCombatRegressionResults.txt`, `MultiplayerBossDirect2*Results.txt`, `MultiplayerBossRelay2*Results.txt` and `MultiplayerBossRelay4*Results.txt` beside this guide. Camera previews are in `PlayerSanctuaryPreview/`.

The automated Relay peers run as separate processes on this computer. Cross-household latency, manual controller feel and station UI appearance on target displays still need human playtesting. Permanent progression is currently local JSON rather than account-based Cloud Save.

## Files

Created: PlayerHub.unity; Hub definition/environment/blend material/shader; five art plates and import metadata; `Scripts/Hub/PlayerHubDefinition.cs`, `MetaProgress.cs`, `PlayerHubController.cs`, `HubLandmarks.cs`; `Scripts/Multiplayer/MultiplayerSession.Hub.cs`; editor `PlayerSanctuarySetup.cs` and `PlayerSanctuaryValidation.cs`; this guide, art prompts, preview renders and validation report.

Updated shared paths: StageFlowController, LevelDefinition, CharacterHealth, CombatHurtbox, AttackHitbox, PlayerMeter, PlayerCombatInput, PlayerSkillController, CombatProjectile, MultiplayerSession, WorldSnapshot, MultiplayerMenu, MultiplayerSmokeTest and BossNetworkValidation; editor MultiplayerSetup, MultiplayerValidation, PlayerHubValidation and RunUpgradeValidation; HauntedHouse.unity, ThaiHauntedHouse.asset, MultiplayerCatalog.asset and EditorBuildSettings. Existing combat mechanics and other stage authoring remain in the shared systems.
