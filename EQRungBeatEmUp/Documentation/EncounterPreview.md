# Encounter trigger authoring preview

1. Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity` and the Scene view.
2. Select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset` in the Project window.
3. Select a stage in its ordered stage list. Expand **Selected stage → Encounters → Waves → Enemy Spawns** to edit the existing fields.
4. In Edit Mode, below the stage fields, find **Encounter / Trigger Preview**. Choose an encounter and either a single wave or all waves, then click **Preview Encounter**. The Scene view temporarily displays the selected stage's background, floor, props, player entry visual and configured enemy visuals, and focuses the layout.
5. Green shows movement bounds; an orange filled rectangle with a thick border and **TRIGGER ZONE — Walk inside to activate** label shows an active PlayerZone. A gray rectangle labeled **UNUSED TRIGGER ZONE** means the current trigger type does not use that rectangle. Cyan circles show actual enemy spawn positions. Labels identify stage/encounter, activation condition, enemy prefab, wave, instance, stagger delay, and boss status. Red marks missing prefabs or authored points outside movement bounds, with dotted lines to their clamped runtime positions. Preview overlays remain visible even with the Scene view Gizmos toggle off, and draw over the stage artwork.
6. Click **Focus Trigger / Spawns** to reframe. Click **Clear Preview** to remove temporary visuals and restore the original scene display.

Use **Focus Trigger Zone Rectangle** to zoom directly to the rectangle. A trigger zone means a rectangle the player's ground position must enter to activate a PlayerZone encounter. Trigger Zone X/Y locate its lower-left corner; Width/Height determine its size. Other encounter triggers ignore this rectangle. The current stage asset's encounters use StageEnter, so they start from stage entry and their zone rectangles are gray; previewing does not change that setting.

The same controls are available on the scene's **StageFlowController** Inspector, with a **Preview Stage** dropdown. Choose the stage and press Preview Encounter to display it temporarily; this does not enter the stage through the runtime flow. Preview creation is disabled in Play Mode.

## Editing in the Scene view

In Edit Mode, enable **Edit Zone / Spawn Handles** after starting a preview. The center handle moves a PlayerZone rectangle; its upper-right handle resizes it with a minimum width/height of 0.01. Spawn handles move the authored points. All handles work in the XY plane (Z is ignored), edit the selected level asset, mark it dirty, and support Undo/Redo. Multiple enemies can reuse the same point; only one authored handle exists for that point per spawn entry.

Clear Preview restores the original scene display but does not undo authored edits. Use Undo to reverse an edit. Asset changes persist when saved. Editing the asset, choosing another encounter/wave, dragging handles or using Undo updates the preview automatically. Switching the inspected stage/level, changing or closing the active scene, entering/leaving Play Mode, domain reload or quitting clears the preview.

## Activation and spawn interpretation

- **PlayerZone:** first entry latches the encounter, then Trigger Delay applies even if the player leaves the zone.
- **StageEnter / Time:** Trigger Delay is measured from stage entry.
- **PreviousEncounterClear:** waits for the referenced earlier encounter (or immediately previous encounter when index is -1), then Trigger Delay applies.
- **Manual:** waits for a manual encounter signal, then Trigger Delay applies.
- **EncounterStart / Time waves:** Spawn Delay is measured from encounter start.
- **PreviousWaveClear waves:** Spawn Delay is measured from the previous wave's clear.
- **Manual waves:** require both a wave signal and the encounter elapsed time reaching Spawn Delay.
- Spawn points cycle when Count exceeds the number of points. With no points, the fallback is Movement Max. Actual positions are clamped to the movement rectangle, matching the current runtime implementation.

All-waves mode shows the configured layout together, not a promise that all enemies appear simultaneously. The inspector lists each wave's activation condition; scene labels show stagger offsets from that wave's start. Safe stages suppress encounters and have no preview. The temporary scene emulates the visual game layout, not AI, collisions, combat, trigger events, or clear conditions. Enemy and prop visual copies contain only transforms and SpriteRenderers, with no gameplay scripts or colliders. They display the source's current/default pose, not running animations.

Original scene renderers are temporarily hidden using editor-only Scene Visibility. Their active/enabled flags, transforms and saved data are preserved, including objects already hidden before preview. Temporary objects use HideAndDontSave and are destroyed on Clear. This preview is for the Scene view; the Game view camera and original objects remain unchanged. Player entry visual is included when the active scene has a StageFlowController using this level and an assigned player. Preview creation does not overwrite any scene or stage asset.

## Validation

Compiled in Unity 6000.4.6f1 using an isolated project copy. The original nine editor smoke checks covered empty-point fallback, out-of-bounds clamping, in-bounds placement, cyclic point reuse, preserving authored coordinates, previous-encounter resolution, invalid dependency reporting, latched zone description, and read-only evaluation/clear of the real level asset. The temporary scene extension passed 15 additional checks covering visual object creation, save-exclusion flags, artwork sizing, clamped enemy placement, absence of gameplay scripts/colliders and callbacks, unchanged active/enabled flags, hiding/restoring original visibility (including previously hidden objects), destruction on clear, unchanged level data/transforms, and clean scene state before/after preview. Interactive Scene view rendering and handle dragging still require a manual check in the editor; batch mode does not exercise GUI interaction.
