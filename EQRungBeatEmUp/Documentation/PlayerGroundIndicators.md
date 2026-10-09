# Blob shadow and player color rings

Every playable character has a reusable `PlayerGroundIndicator` prefab nested under the player root, alongside its airborne Visual transform. Character 2 inherits the indicator from the existing BlueShirtGuy base prefab. Existing scene prefab instances also inherit it; no scene placement or extra input setup is required.

The indicator uses `CharacterMotor.transform.position`, the game's ground/lane position. `CharacterMotor.Height` continues to move only the character's existing visual. Jumping, launched motion, authored aerial arcs and air attacks therefore leave the shadow and ring at the ground anchor. Normal movement and stage resets move the indicator with the player.

PlayerIdentity's zero-based lobby slot determines color: slot 0/P1 blue, slot 1/P2 red, slot 2/P3 green, slot 3/P4 yellow. Changing the character or its owner does not change its slot color. Solo scene players without a PlayerIdentity use P1. The character-selection borders, selection slot strips and multiplayer HUD use the same shared palette.

## Inspector configuration

Select `Assets/EQ_Rung_BeatEmUp/Resources/PlayerGroundIndicatorStyle.asset`:

| Setting | Default |
| --- | --- |
| Shadow Size | 0.68 × 0.24 world units |
| Ring Size | 0.9 × 0.32 world units |
| Shadow Opacity | 0.34, multiplied by the sprite's soft alpha bands |
| Ring Opacity | 0.65 |
| Ring Thickness Pixels | 2; available sizes 1–4 |
| Ground Offset | (0, 0.015) relative to the player's ground position |
| Player Colors | Blue, red, green, yellow in P1–P4 order |

Both ellipses use matching ground perspective. Five original transparent 64 × 32 PNGs provide a soft blob and four ring thicknesses. They use point filtering, no mipmaps, uncompressed import and 100 pixels per unit. The shared style references imported assets rather than creating textures at runtime. The reusable prefab has two SpriteRenderers and one cosmetic component, with no colliders or combat listeners. It uses shared sprite materials; refreshes create no GameObjects, sprites, textures or materials.

The shadow follows the character body's existing lane sorting, at body order minus one. The ring is another order behind it. Both use the body sorting layer and render above the existing floor plates (order -900). They are unaffected by character hit blink, facing flips or airborne sprite height.

For a new playable prefab, use **Beat Em Up → Players → Build ground indicators**. This updates all playable/selection prefabs, preserves existing indicators and style tuning, and appends the five sprite assets to the multiplayer catalog without renumbering existing sprites or attacks. The standalone prefab can also be nested beneath a character root, with its `Motor` reference assigned to that root's existing CharacterMotor. Keep it outside the airborne Visual subtree. Edit the shared style's palette to keep selection and gameplay consistent.

## Multiplayer

The existing authoritative player spawner assigns `PlayerIdentity.slot`. The host refreshes each indicator before capturing its normal SpriteState snapshot, including ground position, slot color, scale, sorting layer/order and an indexed imported sprite. Clients use the existing sprite-replica renderer and cleanup paths. They receive the host's colors directly; no new network messages, gameplay inputs or movement state were introduced. The updated catalog compatibility hash includes the indicator dependency assets, so the usual build-matching check covers the shared style and sprites.

## Files

Created:

- `Scripts/Combat/PlayerGroundIndicator.cs` and `PlayerGroundIndicatorStyle.cs`, under `Assets/EQ_Rung_BeatEmUp/`.
- `Assets/EQ_Rung_BeatEmUp/Prefabs/PlayerGroundIndicator.prefab`.
- `Assets/EQ_Rung_BeatEmUp/Resources/PlayerGroundIndicatorStyle.asset`.
- `Assets/EQ_Rung_BeatEmUp/ArtAssets/UI/PlayerGroundIndicators/BlobShadow.png` and `PlayerRing1.png` through `PlayerRing4.png`.
- `Assets/Editor/Combat/PlayerGroundIndicatorSetup.cs` and `PlayerGroundIndicatorValidation.cs`.

Modified:

- `Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab`: nested indicator inherited by Character 2.
- `Assets/EQ_Rung_BeatEmUp/Resources/MultiplayerCatalog.asset`: five appended sprites and updated compatibility hash.
- `Scripts/CharacterSelect/CharacterPortraitUI.cs`: shared slot palette.
- `Scripts/Multiplayer/MultiplayerMenu.cs`: same palette for player HUD labels.
- `Scripts/Multiplayer/MultiplayerSession.cs`: refresh ground indicators before sprite snapshot capture.

Existing controller, motor, input, hitbox, attack, health, combo and animation implementations were not changed.

## Verification

All 224 assertions passed in Unity 6000.4.6f1 Play Mode in an isolated project copy. The validation uses the actual selected-prefab spawner to create four players, both playable characters, all four slot colors, the existing combat clock, actual jump/aerial-attack/launch methods, and actual stage-entry/reset flow for all nine stages in the current level. It checks hierarchy, shared anchors, palette consistency, character changes, Inspector controls, disable/reenable and 600 refreshes without new renderer/object/material/texture instances or movement/state mutations.

Network checks capture actual authoritative snapshots, round-trip them through JSON, and apply them through the real client `ApplySnapshot` path. They verify all eight ground renderers' sprites, colors, ground positions and sorting, alongside independently elevated character bodies, repeat-snapshot reuse and normal replica cleanup. A live Relay connection or separate networked machines were not used.

Reports: `Documentation/PlayerGroundIndicatorValidationResults.txt` and `Documentation/PlayerGroundIndicatorAssetVerification.txt`. Preview: `Documentation/PlayerGroundIndicatorPreview/Stage1.png`, rendered by Unity with P2 and P3 above their grounded indicators.

To rerun: **Beat Em Up → Players → Validate ground indicators (Play Mode)**. Save your scene first; the validator creates an empty fixture scene, builds the assets, then writes its report.
