# Destructible environment props

## Assets and placement

- `Assets/EQ_Rung_BeatEmUp/Prefabs/Props/CardboardBox.prefab`: 2 hit points.
- `Assets/EQ_Rung_BeatEmUp/Prefabs/Props/CeramicDragonJar.prefab`: 3 hit points.
- `Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/Destructibles/`: twelve separate transparent 96 x 96 PNGs and four original synthesized material SFX WAVs.
- `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset`: four samples in `Stage01_EntranceGate`, at (-3.9, .35), (-1.6, -.35), (.9, .6), (3.4, -.25).

Select the ThaiHauntedHouse level asset, select Entrance Gate, expand Destructibles, and add an entry. Assign either prefab, set Position to the desired ground point, Health to 2 or 3, Hitbox Size to (.7, .65), and Hitbox Offset to (0, .32). Empty sprite fields inherit the prefab sprites. The existing stage editor previews prefab artwork and placement handles. The placement Health and hitbox settings override the prefab when the stage spawns it. The prop pivot sits eight pixels above the texture bottom, on the visible floor contact point. Textures use Point filtering, no mipmaps/compression, and 100 pixels/unit, matching character sprites.

Prefabs can also be dragged into a scene for standalone use. For automatic stage restart cleanup and reconstruction, author them in the level's Destructibles list, so the stage owns props, debris, and drops.

Inspector settings include Maximum Health, Use Hit Points, three state sprites, destruction-frame sprites and timing, debris sprites/prefabs/count/force/lifetime, Hit SFX, Destruction SFX, VFX, Volume, Hitstop Frames, Hit Flash Seconds, and Drop Prefab/Chance. Use Hit Points means one HP per accepted hit regardless of damage upgrades; leave it off for existing damage-based props and boss totems. Frame IDs and intentional repeat intervals use the existing AttackHitbox history. Launcher and air attacks damage props only when their hitboxes intersect; high attacks can pass above low props. Props never receive enemy launch/recoil logic.

Ground footprint is a small non-trigger BoxCollider2D using the existing CombatWall system, with wall bounce disabled. The taller root collider is a trigger for attacks. Both disable synchronously on breaking. The ground footprint blocks characters at any jump height, consistent with existing walls; players can move around its small lane depth.

Optional reward: assign any item/reward prefab to Drop Prefab, or add `PropHealthPickup` to your health-item prefab. It heals a nearby grounded living player who is missing health, once, by Heal Amount. It expires after Lifetime. Configure the item's own artwork. Drops default to 100% chance when assigned; sample props have no drop assigned. `onBroken` also supports existing reward hooks. Reset Prop / Respawn clears spawned debris and rewards before restoring state.

## Implementation

The existing DestructibleObject was extended, preserving its Configure/Receive/Respawn/SetMaximumHealth/Current/Broken API and legacy boss-totem damage behavior. AttackHitbox now includes props in ground-area candidate selection and uses the prop's small hitstop value. Prop hits never reset the player's combo timeline. SFX and VFX use AttackFeedback.PlayRemoteFeedback, including existing Punch1 impact VFX. No pooling service exists in this prop/feedback path; short-lived cosmetics follow the existing transient-object lifecycle.

PropDebris simulates separate ground XY and visual height, gravity, one small bounce, friction, spin, sorting, fade, and expiry. Broken artwork switches over configured destruction frames, while fragments scatter. Feedback respects the combat pause state. The destruction guard commits before callbacks, preventing duplicate debris, sounds, drops, or reentrant events.

`Assets/Editor/Stages/DestructiblePropSetup.cs` supplies the rebuild menu (Beat Em Up > Props > Build destructible props and Stage 1 samples). It refreshes only the four labelled samples and the two generated prefabs. Other placements remain intact. Rebuilding resets prefab configuration, so customize duplicates or avoid rebuilding after tuning.

`Assets/Editor/Stages/DestructiblePropValidation.cs` supplies focused Play Mode validation and the batch BuildAndValidate entry point. See `Documentation/DestructibleValidationResults.txt` for the checks actually executed. Validation runs from an isolated copy of the project; no open scene is saved or changed by the test runner.

## Art provenance

Original artwork was generated with the built-in image_gen tool. The supplied concept was used for visual identity, not imported as game art. Full generated masters, prompts, nearest-neighbor extraction script, audio generator, and asset preview are retained in `Tools/Destructibles/`. Atlas cells were extracted, aligned, reduced to the game pixel grid with nearest-neighbor sampling, and alpha normalized to crisp pixel edges. Jar atlas rows were split at y=576 to avoid fragments bleeding across cells. Only separate final PNGs are imported by the game.

Manual follow-up: subjective in-game timing/visual polish, speaker listening, online multiplayer client presentation, and controller playthrough are not established by automated checks. The existing online snapshot architecture is unchanged.
