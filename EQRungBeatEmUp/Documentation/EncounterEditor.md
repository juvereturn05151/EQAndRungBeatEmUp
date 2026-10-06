# Encounter editor: bounds, waves and scene objects

## Exact editing workflow

1. Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity`.
2. Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset`. Alternatively select **Haunted House Stage Flow** and click **Edit level / ordered stages**.
3. Select a stage in **Stages — drag to reorder**, then expand **Selected stage → Encounters**.
4. Expand an encounter or click its **Select** button. The Encounter dropdown below also selects it. Scene View immediately shows its trigger (yellow/orange), combat bounds (red), and camera bounds (blue), with its ID in the labels. Play Mode and Preview Encounter are unnecessary for the handles.
5. Leave **Edit Zone / Spawn Handles** checked. Drag a rectangle's center arrows to move it, an edge square to resize one axis, or a corner square to resize both. The X/Y/W/H fields and handles edit the same serialized Rect. Numeric edits can author unusual sizes; warnings do not block them. Handle resizing keeps sizes positive.
6. Use **Show Trigger Zone**, **Show Combat Bounds**, **Show Camera Bounds**, and **Show All Encounter Bounds** to control editor drawing. Only the selected encounter has editable rectangle handles. Disabled combat/camera settings remain visible in dim colors. These drawing toggles do not change gameplay.
7. Expand **Waves**, then expand or **Select** an individual wave. Each wave has a collapsible header and **Enabled** checkbox. Edit **Spawn Groups**, prefab, count, spawn points, delays and interval. Cyan Scene handles move the selected wave's spawn points. The Wave dropdown can display all waves as a layout.
8. Expand **Scene Object States** on the encounter. Increase the list size/use the list's add control. Every row is **Apply | Active | Object**. Drag an actual GameObject from the Hierarchy into the Object field; assets/prefabs are not scene targets. Check Active to enable it at encounter start, or uncheck Active to disable it. Uncheck Apply to ignore the entry.
9. Configure the same list on each wave. Wave entries override the encounter/earlier waves for their listed objects. Objects absent from a list retain their current state.
10. **Add Selected GameObject to Encounter** and the per-wave **Add Selected Object** buttons add selected Hierarchy objects. Lock the level Inspector while selecting Hierarchy objects if needed. Remove an entry with Unity's normal array/list controls. Duplicate encounter copies object settings and points to the same bound objects.
11. Check **Restore Scene Objects On Encounter End** to restore each affected object's activeSelf as it was before the encounter. Leave it unchecked to retain the last applied states. This also governs cleanup on stage change/controller disable. No scene objects are destroyed or recreated.
12. Use **Preview Encounter State** to inspect the encounter settings on the actual scene objects. **Preview [wave ID]** applies the encounter and all enabled waves through that wave, in order, so earlier overrides remain visible. These previews run outside Play Mode.
13. Click **Restore Preview** to restore the original scene states. Switching encounter/stage, selecting another unrelated object, entering Play Mode, saving/closing the scene, assembly reload and editor quit also restore preview. Changes to settings and Undo refresh an active object preview. Preview state is temporary; authoring settings remain saved.
14. Save **both the level asset and the scene**. The asset stores settings and binding IDs; the scene's Stage Flow component stores the actual GameObject references. A different scene using the same level needs bindings to its own objects. Missing bindings show a warning and are ignored safely at runtime.
15. Press Play, or **Simulate Encounter**, to run the real scheduler. F8 shows the existing encounter/wave debug controls. Simulation intentionally restarts the runtime stage; ordinary authoring edits keep the selected stage.

Do not deactivate Stage Flow itself, its ancestors, or objects required to keep combat processing alive. The editor warns about targets containing Stage Flow. SetActive uses normal Unity lifecycle callbacks; inactive parents still make an Active child invisible. Lists configure activeSelf, rather than forcing every ancestor active.

## Runtime rules

- Encounters and waves default to **Enabled**, including existing saved content.
- Disabled encounters never trigger or apply their object states. Normally they satisfy required progression and previous-encounter dependencies as a skipped encounter. **Disabled Blocks Progression** deliberately keeps that gate closed. This option only matters for required completion/dependencies.
- Disabled waves neither spawn nor apply object states. PreviousWaveClear finds the previous enabled wave. With no previous enabled wave, its delay starts at encounter start. An encounter with no enabled waves can clear normally; ManualSignal still needs its clear signal.
- Activation order: encounter object states → combat/camera lock → wave object states → enemy spawn. Each list affects only explicitly applied, resolved entries. Later entries win if the same object occurs more than once.
- Optional restoration captures all affected objects before encounter start, including objects first mentioned by later waves. Without restoration, the last applied states persist into subsequent encounters/stages.
- Existing trigger delays, manual signals, enemy caps, queued spawns, one-shot/rearm rules, exit locks and legacy unlocked spawn triggers remain in the original StageFlowController. Frame-based combat is unchanged. Configure Enabled flags before running a test; enabling already completed waves does not replay their spawns.
- Overlapping unlocked encounters can share targets. Their activation order determines the most recent state. Optional restoration returns an encounter's captured values; avoid conflicting restore lists on simultaneously active encounters.

## Verification

**Beat Em Up → Stages → Validate encounter editor and object states** runs editor and Play Mode checks. It uses a temporary scene/asset, verifies real scene save/reload, checkbox and rectangle Undo/Redo, preview/restore, Apply filtering, wave history, activation order, disabled-wave progression, optional runtime restoration, camera/combat locks, dependencies, and legacy spawns. Temporary fixtures are deleted afterwards. Results: `Documentation/EncounterEditorValidationResults.txt`.

**Beat Em Up → Stages → Validate combat encounter zones (Play Mode)** remains the regression suite for queued/delayed spawns, camera framing, clear signals, repeatable zones, crossing triggers, and two local players. Results: `Documentation/EncounterZoneValidationResults.txt`.

For a manual visual/input check: select an encounter, drag each center and each edge/corner, verify numeric values, press Ctrl+Z/Ctrl+Y, edit numbers, switch waves, inspect labels/colors, toggle all visibility settings, preview a wave, and restore. Automated checks validate data/runtime behavior; they do not simulate a user's mouse drag or judge label placement.

## Files for this change

Created (with Unity metadata):
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/StageFlowController.SceneObjects.cs`: scene bindings, state application/restoration and disabled progression helpers.
- `Assets/Editor/Stages/EncounterObjectAuthoring.cs`: actual scene-object assignment and temporary previews.
- `Assets/Editor/Stages/EncounterDefinitionDrawers.cs`: collapsible encounter/wave headers, Enabled and Select controls.
- `Assets/Editor/Stages/EncounterEditorValidation.cs`: editor/runtime validation.
- This guide and `Documentation/EncounterEditorValidationResults.txt`.

Modified:
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/LevelDefinition.cs`: enabled flags, activation lists and optional restore/block settings; existing fields retained.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/StageFlowController.cs`: hooks in the existing scheduler, enabled-wave skipping and progression integration.
- `Assets/Editor/Stages/EncounterPreview.cs`: immediate selection drawing, visibility controls, dim disabled bounds and center/edge/corner handles.
- `Assets/Editor/Stages/EncounterZoneAuthoring.cs`: warnings for all rectangles and updated guidance.
- `Assets/Editor/Stages/LevelDefinitionEditor.cs`: first enabled wave delay guidance.
- `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset`: serialized new fields with enabled defaults and empty object lists.
- `Documentation/ProjectEditorGuide.md`: revised encounter workflow.
- `Documentation/EncounterZoneValidationResults.txt`: refreshed regression results.

No artwork, enemy attacks, input actions or additional encounter state machine were created.
