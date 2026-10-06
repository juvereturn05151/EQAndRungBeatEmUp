# Outdoor Player Hub

This documents the earlier short outdoor Hub. The current walkable sanctuary, progression stations, persistence and death returns are documented in [PlayerSanctuary.md](PlayerSanctuary.md). Its new PlayerHub scene replaces this version's walk-to-exit behavior.

Start `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity`. The run now begins in **Player Hub**, stage index **0**, displayed as **1/9**. Walk to the right exit at `(2.7, 0)` to enter Entrance Gate. No upgrade cards or combat locks appear in the hub. New runs return here with an empty build.

Order: Player Hub → Entrance Gate → Blood Sheet Corridor → Fake Morgue → Service Corridor & Stair → Haunted Maze → Recovery Shrine → White Ghost Boss Chamber → Escape Lane. Existing stage IDs remain unchanged; the original eight stages shift forward one index. Explicit next-stage routes are remapped when the hub is first inserted. Combat-room rewards, shrine healing and cursed-totem boss protection remain in place.

## Art and authoring

The supplied warm outdoor camper PNG is copied byte-for-byte to `Assets/ArtAssets/Environments/HauntedHouse/Stage00_PlayerHub/Stage00_PlayerHub.png`. Unity imports two sprite sections from that same texture: **PlayerHub_Background** uses the upper 40%, and **PlayerHub_Floor** uses the lower 60%. Sections meet on the same source row without repainting, gaps or overlapping rows. They use the existing stage renderer/camera framing, point filtering, 100 PPU and uncompressed import. The existing scene displays hub artwork in edit mode as well as at runtime. An actual camera render is saved at `Documentation/PlayerHubPreview/PlayerHub.png`.

Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset`, select **1. Player Hub**, and expand **Selected stage**. Edit its sprites, art width, movement bounds, entry/exit, Stage Type, completion mode, reward and decorative placements. Defaults: bounds `(-3.05, -0.4)` to `(3.05, 0.65)`, entry `(-2.5, 0)`, exit `(2.7, 0)`, radius `0.35`, **Stage Type = Safe**, **Completion Mode = ReachExit**, **Reward After Clear = None**.

## Safe behavior and future hooks

The **Stage Art Layout** section now exposes per-stage **Background Height**, **Background Center Y**, **Floor Height**, and **Floor Center Y**. Edit the level asset to persist changes, including during Play for live preview. Defaults remain 1.6 / 2.56 / 2.4 / 0.56. See [StageArtLayout.md](StageArtLayout.md) for a 50/50 example, seam guidance, and reset instructions. Editing these values does not modify movement bounds or camera framing.

Stage types are **Combat, Safe, Boss, Exit**. Safe stages ignore authored encounters, enemy combat/reaction prefabs in decorative placements, and destructibles. The Inspector warns about such unused placements. Safe stages also protect the player at both CharacterHealth.Damage and CombatHurtbox.Receive: enemy hits, scripted/environmental damage, guard chip, lethal saves, launch, knockback and hit reactions cannot harm the player. Entering a combat stage removes that protection immediately.

Recovery Shrine is also marked Safe. The legacy **Safe Room** flag continues to enable its existing E / gamepad Select recovery interaction and also implies safe protection. Player Hub uses Safe stage type with Safe Room unchecked, so a shrine interaction is not forced into the camper area. Movement, jumping and optional practice attacks remain available.

DecorativeProps can host future NPC/camp/interactable prefabs. The existing recovery point, reward configuration and Event completion mode provide hooks for future interactions. No shop, NPC dialogue, checkpoint persistence, run setup selection or additional upgrade system was introduced.

**Beat Em Up → Stages → Add outdoor safe hub first** sets up missing artwork slicing and inserts the hub once while preserving existing stage data. **Validate outdoor safe hub (Play Mode)** checks startup, artwork sections, camera/bounds, movement, no enemies, direct/combat damage protection, no Second Wind consumption, practice attacks, editor data, suppression of accidentally authored combat placements, exit transition, restored combat damage, original first wave, explicit-route remapping and idempotent setup.

Validation in Unity 6000.4.6f1 passed **21 hub**, **161 upgrade**, **79 stage flow**, **101 combat**, **188 defense** checks — **550 total**. The stage regression starts at Entrance Gate by ID; the upgrade regression walks from the hub first and still tests all five upgrade choices through the boss and escape. All reports are `Documentation/PlayerHub*Results.txt`.

Created: hub PNG/import metadata, `Assets/Editor/Stages/PlayerHubSetup.cs`, `PlayerHubValidation.cs`, this guide and camera preview. Updated: LevelDefinition (stage types), StageFlowController (safe placement/protection rules), CharacterHealth and CombatHurtbox (no-harm gates), LevelDefinitionEditor, HauntedLevelBuilder, RunUpgradeSetup, stage/upgrade regression fixtures, ThaiHauntedHouse.asset, HauntedHouse.unity and the existing flow/upgrade guides. Existing haunted-stage art, area tuning, encounter waves, rewards and combat assets were retained.
