# Hub interactable readability

The current scene was identified as `PlayerHub` from the Unity Editor scene-load log. All four existing interaction stations now have separate, original foreground artwork and reusable gameplay prefabs. No background panels were repainted or used as object sprites.

| Station prefab | Ground interaction point | Existing function |
| --- | --- | --- |
| CharacterWardrobe | (-7.6, 0) | Change Character menu |
| StatBlessingGong | (0, 0) | Upgrade Base Stats menu |
| SkillFlameShrine | (7.6, 0) | Upgrade Skill menu |
| WorldEntranceGate | (15.2, 0) | Enter World 1 menu / existing Begin Run confirmation |

Sprites: `Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/HubInteractables/`. Prefabs: `Assets/EQ_Rung_BeatEmUp/Prefabs/HubInteractables/`. These are nested gameplay prefab instances in the existing `Hub/SanctuaryEnvironment.prefab`; the PlayerHub scene's EditorOnly preview and runtime hub stage already reference this environment prefab, so both inherit the improvement without saving or replacing the current scene. The environment paintings remain scenery. The selectable objects are independent sprite renderers and colliders in front of the scenery.

Each original 160 x 208 transparent PNG uses Point filtering, 100 pixels/unit, no mipmaps or texture compression, a Full Rect mesh, and a ground pivot eight pixels above the bottom edge. Shapes range from about 1.1 to 1.76 world units high, compared with the roughly 1.28-unit player. Warm gold, teak, indigo/red cloth, purple stone, and left-side warm highlights match the sanctuary. Turquoise magic and edge contrast improve separation from orange lantern light. The body rests 0.24 units behind the interaction point on the walkable floor, bringing it away from the background architecture while leaving approach space.

`HubInteractionStation` supplies a CircleCollider2D interaction trigger synchronized with the existing 1.2-unit Hub radius, a separate small solid footprint using CombatWall (bounce off), a pulsing one-pixel turquoise silhouette outline, a subtle elliptical ground marker, and a floating diamond when an eligible grounded player is near. The physical object stays grounded; only the highlight and diamond animate. The shader samples sprite alpha to draw an actual contrasting border rather than tinting the artwork.

`PlayerHubController.Nearby` now resolves these actual gameplay objects, preserving the legacy definition-distance fallback when no station object exists. Input still flows through the existing Player/Interact action and StageFlowController.Interact, including controller/multiplayer ownership. E / L1 / LB opens the same existing station menu. A high-contrast, object-anchored prompt identifies the station and input when nearby; it hides while the menu is open or interaction is unavailable. HubInteractionStation.TryInteract also forwards to the existing hub controller, without binding another input handler. Range is evaluated in the motor's ground XY coordinates, matching the project's custom character movement; it does not depend on Rigidbody2D trigger callbacks. Existing menu actions still own purchases, character changes, and run confirmation.

The existing World 1 runic aura is retained and placed at the entrance base with reduced scale. Its old floating text is hidden in favor of the common nearby prompt. Rebuilding preserves station coordinates and existing hub progression; it does recreate the four generated station prefabs and replaces their instances in the environment.

Setup menu: Beat Em Up > Hub > Improve interactable readability. To reposition stations, edit PlayerHub.asset's Stations coordinates using the existing Hub authoring handles, not just the prefab instance transform. Ground markers, triggers, and artwork follow those positions together. Inspector controls expose body/outline/ground/icon references, station index/name, indicator height, cue color, and idle/nearby highlight strength.

Artwork was generated using the built-in image_gen tool, then each object was extracted into a separate PNG with nearest-neighbor sampling and crisp binary alpha. The generated master and packaging script are retained in `Tools/HubReadability/`. UI cue shapes are small deterministic pixel sprites. `Prompts.txt` records the final generation prompt.

Validation: `Documentation/HubReadabilityValidationResults.txt` records the checks actually run in an isolated Unity project copy. Preview images, if generated, are editor renderings of the actual PlayerHub prefab composition rather than a hand-composited scene. No open scene or permanent player save was overwritten by the validation runner.
