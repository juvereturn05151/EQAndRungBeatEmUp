# Live stage editing

## Workflow

1. Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity`. Stop Play Mode and open the **Scene** tab.
2. Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset` in the Project window.
3. Click the desired row in **Stages — drag to reorder**. The temporary Scene preview changes immediately. The Scene banner identifies the selected stage. The numbering follows your existing asset order; this feature does not reorder your stages.
4. Expand **Selected stage** and edit its normal Inspector fields. Asset edits refresh the selected preview immediately, without pressing Play.
5. Use **Previous Stage**, **Next Stage**, **Preview Selected Stage**, **Refresh Preview**, or **Focus Selected Stage** as needed. Focus changes the Scene view, not the gameplay camera.
6. To inspect or drag one encounter's zone/spawns, choose it under **Encounter / Trigger Preview**, click **Preview Encounter**, then enable **Edit Zone / Spawn Handles**. Center moves the zone; upper-right resizes it; cyan handles move authored spawn points. These edits support Undo.
7. **Preview Selected Stage** returns to the complete stage layout. **Clear Scene Preview** restores the original scene display. Clearing does not undo edits you made to the level asset.
8. Press Play to test actual activation, AI, combat, reward interaction and progression. Editor selection does not change which stage the game starts in.

Alternatively, select the scene object with **StageFlowController**, choose its **Editor Stage** dropdown, and use the same preview controls. **Edit level / ordered stages** selects its assigned level asset for editing. The two Inspectors share one stage selection per level.

## Immediate feedback

| Inspector fields | Scene preview result |
| --- | --- |
| Background Sprite / Floor Sprite | Replaces the corresponding art plate |
| Art Width | Changes both plates' world width |
| Background Height / Background Center Y | Changes the upper plate's height / vertical placement |
| Floor Height / Floor Center Y | Changes the lower plate's height / vertical placement |
| Movement Min / Max | Updates green bounds and the clamped enemy spawn positions |
| Player Entry Point / Player Exit Point / Exit Radius | Updates entry/player visual and yellow exit marker |
| Encounters → Trigger / Trigger Zone / Trigger Delay | Updates zone and activation labels; orange means PlayerZone, gray means unused zone |
| Waves → Enemy Spawns → Prefab / Count / Spawn Points / Interval | Updates static enemy visuals and labeled spawn layout/timing offsets |
| Decorative Props / Destructibles | Updates authored visual placements; safe stages omit ignored destructibles |
| Reward After Clear / Chapel Spawn Point | Shows the chapel visual and magenta interaction marker for UpgradeChoice |
| Reward Choice Center / Spacing / Reward Interact Radius | Updates the three upgrade-choice and interaction markers |
| Recovery Point / Radius | Updates recovery marker on safe/heal stages |

Art heights and centers affect visuals only. Movement bounds, triggers, spawn scheduling and reward fields affect gameplay when you edit them. Change those deliberately. Temporary preview objects are not the authoring source; edit the level fields instead.

All encounters/waves are shown together in complete-stage mode to reveal placement. They are **not simultaneous runtime spawns**. Use single-encounter/wave preview to isolate overlapping layouts. Missing prefabs and positions clamped into movement bounds are marked. Enemy copies show a static source pose. Reward-choice markers show placement, not randomized upgrade offers.

This is a **Scene view** authoring preview, not a Game view camera simulation. Scene focus does not change camera size/offset. Prefab content edits outside the level Inspector may require **Refresh Preview**.

## Selection and refresh

Previously the ReorderableList selection, StageFlow Inspector selection and active encounter preview were independent. Clicking a stage row did not rebuild the scene preview, and rebuilding the Inspector lost its selected index. Consequently the scene could keep showing a previously previewed stage, including Stage 8. No hardcoded Stage 8 default was found.

`StageEditorSelection` now retains selection per level through Unity `SessionState`. Both Inspectors use it; selection remains through Inspector reconstruction and domain reload during the editor session. Unique stage IDs preserve the selected stage through reordering. New levels default to the first row. Duplicate/empty IDs fall back to a clamped saved index. This does not promise retention after restarting Unity.

Selecting a row rebuilds and focuses its whole-stage preview. Applying serialized Inspector edits refreshes immediately; Undo refreshes immediately; handle edits refresh on the next editor callback. A 0.2-second fingerprint check catches changes made elsewhere. Routine refreshes do not reframe the Scene view or replace editor selection with runtime `StageIndex`.

Preview copies contain only transforms and SpriteRenderers, are excluded from scene saves, and never run AI or combat scripts. Original scene renderers are hidden through editor-only Scene Visibility and restored on clear, preserving previously hidden objects. Scene changes, closing, Play Mode transitions, assembly reload and quitting clear temporary objects. On Inspector initialization or returning to Edit Mode, the selected stage can be recreated. Runtime bounds gizmos draw only in Play Mode so they cannot conflict with the selected editor preview.

## Changed scripts

- `Assets/Editor/Stages/StageEditorSelection.cs` (new): retained shared selection and preview controls.
- `Assets/Editor/Stages/LevelDefinitionEditor.cs`: row selection, immediate serialized-edit refresh and restored list index.
- `Assets/Editor/Stages/StageFlowControllerEditor.cs`: shared Editor Stage selection and controls.
- `Assets/Editor/Stages/EncounterPreview.cs`: full-stage overlays, safe-stage support, immediate refresh, reward/entry/exit markers and Undo.
- `Assets/Editor/Stages/EncounterScenePreview.cs`: render-only whole-stage and chapel placement preview.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/StageFlowController.cs`: suppress conflicting runtime gizmos outside Play Mode.

No stage asset, scene, art, movement bounds, encounter data or runtime progression was rewritten by this change.

## Validation

Unity 6000.4.6f1 compiled the changes in an isolated project copy. Twenty live-stage checks and 24 existing temporary-preview/second-encounter checks passed. These cover the actual stage-list callback, Stage 8 → Stage 1 switching, Inspector reconstruction, sprite/layout refresh, Stage 3 → Stage 5 switching, trigger data and spawn refresh, chapel placement, safe stages, Undo, reorder identity, runtime-index independence, restoration and scene-save safety. See `LiveStageEditorValidationResults.txt`.

The tests invoke selection/refresh and inspect generated preview objects; batch mode does not exercise Inspector drawing or mouse dragging of Scene handles. Use the workflow above for an interactive visual check in your open editor.

The existing full haunted-house Play Mode regression also passed 79 checks, covering encounters, timed/clear/zone/manual triggers, transitions, enemy combat, destructibles, boss/totems, recovery, exit gating and retries. See `LiveStageRuntimeValidationResults.txt`. Both validation runs exited successfully with no compiler errors. Your open project was not batch-launched or closed.
