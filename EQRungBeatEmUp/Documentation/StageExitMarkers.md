# Next-area and stage-exit guidance

Stage 1 (Entrance Gate) is configured with three independent route cues:

| Cue | Show after | Existing destination |
|---|---|---|
| Entrance approach | Immediately during initial traversal | Encounter 01 trigger center, offset down 0.4 units into the open lower lane. |
| After first fight | Encounter 01 complete | Encounter 02 trigger center. |
| Stage 1 exit | Current stage sequence complete and ExitUnlocked | Existing playerExitPoint, inside its existing exitRadius. |

Only the first eligible cue in route order is shown. Active encounters, unfinished wave/spawn queues, manual clear requirements, reward selection, pause, defeat and stage failure suppress guidance. Entering an encounter's existing trigger hides its approach cue as soon as the scheduler latches entry, including an authored trigger delay. Entering the existing stage exit radius hides the final cue; the original reward and stage-transition code continues unchanged. Clearing combat does not advance or move players.

The reusable `Assets/EQ_Rung_BeatEmUp/Prefabs/StageExitMarker.prefab` contains three separate SpriteRenderers: animated downward chevrons, a small ground bracket, and bitmap NEXT text. Original sprites are in `ArtAssets/UI/ExitMarkers/`. They have transparent backgrounds, dark outlines, amber/bone colors, 100 PPU, point filtering and no texture compression. Three chevron frames cycle every eight combat frames; the arrow bounces by at most a few pixels. Approaching within Near Distance strengthens the pulse and brightness. The prefab has no collider or transition behavior.

If the cue is outside the gameplay camera's viewport, a single small arrow and NEXT label appear at that viewport's edge. It points toward the marker, respects camera letterboxing and disappears when the marker's arrow position enters the view. Multiplayer uses the authoritative cue snapshot and existing world sprite replication, with guidance drawn through the session HUD on clients and hosts.

## Configure without code

1. Select `Levels/HauntedHouse/ThaiHauntedHouse.asset`, then select **Entrance Gate** in the existing ordered-stage editor. The Stage Flow inspector also provides **Edit level / ordered stages**.
2. Expand **Selected stage → Next Area Markers**. Add an element for each traversal section. Assign a unique Marker ID and the StageExitMarker prefab; enable it.
3. Choose **Stage Exit** to link to the existing exit point, or **Encounter Entry** and enter the ID of an existing enabled PlayerZone encounter. Encounter Entry never creates a new trigger.
4. **Use Transition Position** follows the actual exit or trigger center automatically. The button re-enables that link. Marker Offset adjusts only the artwork. Disable the link to edit a separate Vector3 Exit Position.
5. Choose **Immediately**, **Encounter Complete** (supply the completed encounter ID), **Sequence Complete** (the existing stage completion condition), or **Controlled By Script**. Every option still respects combat/reward locks and actual exit eligibility.
6. Toggle **Show Edge Arrow** and **Marker Preview**, and tune **Near Distance**. The prefab exposes the chevron frames, Frame Hold and Bounce Height.

The existing whole-stage and encounter previews show marker sprite copies, chevron gizmos, the real transition circle/trigger rectangle, and a dotted connection. Drag the marker with the normal Scene View position handle. With automatic positioning enabled, dragging edits Marker Offset and leaves the gameplay transition in place. Without it, dragging edits Exit Position. Changes use Undo and persist in the level asset. Preview copies have no gameplay scripts/colliders and are not saved in the scene.

Script-controlled visibility uses `StageFlowController.SetNextAreaMarkerVisible(markerId, visible)`. This controls the cue only; it cannot complete encounters or override an arena/reward lock. Script visibility resets on stage entry/restart. **Sequence Complete** reads the current stage's existing CompletionSatisfied state, including Event completion via CompleteStageEvent; it does not introduce a second sequence controller.

For new levels, add marker elements through this editor and reuse the prefab. No marker scene placement or manual collider setup is needed. The existing room runtime owns cleanup. Disabled/missing PlayerZone destinations are reported in the level inspector. Reordering marker elements changes presentation priority, not encounter order.

## Implementation files

Created runtime files in `Scripts/Stages/`: `NextAreaMarkerDefinition.cs`, `StageExitMarker.cs`, `StageFlowController.ExitMarkers.cs`. Added route definitions to `LevelDefinition.cs` and small create/refresh/hide hooks to `StageFlowController.cs`. The existing progression predicates and TryAdvance logic remain in place.

Created editor files in `Assets/Editor/Stages/`: `NextAreaMarkerDrawer.cs`, `StageExitMarkerAuthoring.cs`, `StageExitMarkerSetup.cs`, `StageExitMarkerValidation.cs`. Extended `LevelDefinitionEditor`, `EncounterPreview` and `EncounterScenePreview` for configuration, validation, gizmos and render-only previews.

Extended `WorldSnapshot.cs` and `MultiplayerSession.cs` with authoritative guidance state and session HUD drawing. Appended the five new sprites to `Resources/MultiplayerCatalog.asset` without changing existing sprite IDs or character selections. Updated the level asset only with marker definitions. `Tools/ExitMarkers/art.cjs` retains the deterministic pixel-art source.

## Verification

**66 assertions passed in Unity 6000.4.6f1**, with results in `StageExitMarkerValidationResults.txt`. The Unity batch Play Mode harness runs in an isolated copy of the current project, using the actual HauntedHouse scene, Stage 1 asset, player motor, enemy spawn groups, reward chapel/upgrade choices and transition code. It physically moves the player with the existing motor around intact props, triggers both encounter zones, defeats their real spawned enemies through the existing health API, visits/selects a reward and walks into the existing next-stage exit. Enemy deaths are injected for deterministic progression checks; this is not a manual combat playthrough.

It also checks delayed waves and queued spawns, manual encounter clear, trigger delay, script control, immediately visible no-combat exits, all-player defeat, pause, offsets, restart/disable cleanup, render-only editor preview preservation, off-screen left/right direction math and viewport bounds, world-visible arrow suppression, proximity feedback, multiplayer art catalog IDs and guidance snapshot serialization. These are local tests; a two-machine network session and physical-controller playthrough were not performed.

Actual camera renders of the Stage 1 world cue are saved in `StageExitMarkerPreview/BetweenEncounters.png` and `StageExitMarkerPreview/StageExit.png`. These camera renders show world sprites; edge-arrow HUD placement is covered by the projection checks.

To rerun in a disposable project copy, use **Beat Em Up → Stages → Validate Stage 1 next-area guidance (Play Mode)**. It opens HauntedHouse and uses a temporary in-memory copy of level data for test variants. **Set up Stage 1 next-area markers** recreates the marker prefab/import settings and adds missing named Stage 1 cues while preserving existing marker positions/conditions.
