# Attack Data Editor

Open **Tools > Combat > Attack Data Editor**. You can also double-click an AttackData asset or use **Open Attack Data Editor timeline** in its Inspector. Select an asset in the top field, drag one into the field, or enable **Follow Project selection**.

The window edits the existing runtime AttackData asset directly. Runtime combat scripts, frame definitions, and current attack content are preserved. All authoring code lives under Assets/Editor.

## Author an attack

1. Choose an existing attack or click **New Attack**, then **Add** combat frames.
2. Click or drag on the timeline to scrub. Frame numbers start at zero.
3. Assign a sprite in the selected frame Inspector. For a hold, Shift-select a range and click **Hold current sprite across range**, or use the bulk Sprite field and **Set sprite**.
4. Select an impact frame and click **Add hitbox**. Drag its yellow center to move it; drag an edge or corner handle to resize it. Damage, hitstun, hitstop, and other box properties remain available numerically in the right panel.
5. Select recovery frames, choose the bulk Attack/Launcher/Jump cancel permissions, and click **Apply cancel permissions to range**.
6. Step through frames or play the preview. Click **Save**, then test the asset in gameplay.

## Timeline controls

| Control | Action |
| --- | --- |
| Click / drag | Select a frame / scrub the playhead |
| Shift-click / Shift-drag | Select a contiguous range |
| Range fields | Set the inclusive first and last selected frames |
| Alt-drag selected range | Move complete frames to the yellow insertion boundary |
| Horizontal scrollbar | Browse long attacks |
| Timeline zoom slider / Ctrl-wheel | Change frame-cell width |
| Ctrl+C / Ctrl+V | Copy the selected range / insert pasted frames before the current frame |
| Delete | Remove the selected range |
| Left / Right arrow | Step frames |
| Space | Play / pause |

Keyboard authoring shortcuts apply when the timeline has focus and a text field is not being edited. Clicking another panel releases timeline focus. Unity's normal Undo/Redo remains available.

Each moved, copied, or duplicated frame contains its entire sprite, hitboxes, movement, velocity/gravity, cancels, defense, and events. Duplicated hitbox lists are independent. Clipboard sprite references use asset GUID and local file ID so copies remain usable across script reloads.

Tracks show Sprite, Hitboxes (with count), Movement, Velocity/gravity, Attack cancel, Launcher cancel, Jump cancel, Invulnerability, Super armor, and Events. Markers derive from the actual frame data. Track definitions are collected in AttackTimelineGUI.Tracks for future extension.

## Sprite and bulk editing

Drag a sprite onto a Sprite-track cell. One sprite dropped inside the selected range fills that range; several sprites fill consecutive existing frames in drag order, starting at the drop position. Texture assets can supply their imported Sprite subassets. Add frames first if the sequence needs more room.

Bulk buttons affect only the selected range. **Enable hitbox** fills empty frames with independent copies of the current selected hitbox, or a default box if none is selected; existing authored boxes are preserved. **Disable hitboxes** removes all boxes on the range. Movement buttons set or clear each frame's movement vector. Applying cancel permissions writes all three chosen checkbox values to each frame.

**Add** appends an empty frame. **Insert** places an empty frame before the current frame. **Duplicate** inserts a copy of the selected range after that range. **Duplicate previous** inserts the preceding frame's data at the current frame. **Paste** inserts clipboard frames before the current frame. Deleting every frame leaves an empty attack that can be authored again.

## Preview

Yellow identifies the selected hitbox; other hitboxes are orange, the hurtbox is cyan, and the white cross marks the pivot. Select a box in the preview or the Inspector dropdown. Numeric edits and visual edits use the same data.

Use the preview zoom slider or mouse wheel to fit large boxes; middle-drag pans. **Reset** restores the view. **Facing Left/Right** mirrors the sprite, forward movement, and hitbox visualization while preserving one canonical attack definition. Onion skin displays translucent neighboring sprites. Movement path displays accumulated authored movement and the current movement direction.

The hurtbox reference defaults to the existing BlueShirtGuy prefab and can be changed to another character reference. This reference is used only for the visual overlay.

Transport includes first, previous, Play/Pause, next, last, looping, and 0.25x/0.5x/1x/2x speeds. FPS comes from the open scene's CombatClock, with a 60 FPS fallback. Numeric and bulk editing pause during playback; clicking the timeline or manipulating preview geometry pauses playback. The current frame's tracks and cancel status update during preview.

Preview shows sprites and authored movement; it does not simulate enemy hits, runtime input buffering, velocities, gravity, or gameplay physics. Test those behaviors in Play Mode.

## Saving and derived timing

An asterisk beside the attack name indicates unsaved asset changes. **Save** writes the current asset. Changes are recorded with Unity Undo, marked dirty, and saved before a script reload or editor shutdown. The current attack, playhead, and range survive Unity window serialization. The preview uses an isolated render scene and does not alter the game scene.

Total frames, first/last active frame, startup, active count, and recovery are derived from the existing frame timeline. No second timing model is stored.

## Files and verification

| Script | Responsibility |
| --- | --- |
| Assets/Editor/Combat/AttackDataEditorWindow.cs | Window, selection, transport, Inspector, bulk controls, persistence |
| Assets/Editor/Combat/AttackTimelineGUI.cs | Tracks, ruler, playhead, selection, zoom/scroll, sprite drops, frame moves |
| Assets/Editor/Combat/AttackPreviewGUI.cs | Sprite rendering, overlays, mirroring, hitbox handles, onion skin, movement path |
| Assets/Editor/Combat/AttackFrameAuthoring.cs | Undo-aware frame operations, deep copies, clipboard, range edits, saving |
| Assets/Editor/Combat/AttackEditorValidation.cs | Isolated EditMode authoring checks |
| Assets/Editor/AttackDataEditor.cs | Existing Inspector with a button to open the timeline |

Validated in Unity 6000.4.6f1: 32 assertions covering deep copies, range changes, Undo/Redo, sprite clipboard resolution, complete-frame moves, timeline tracks, mirrored geometry, window serialization, saving, and preservation of existing attack assets. Native Unity checks confirmed sprite rendering, frame selection, hitbox dragging/resizing, and Undo restoration. See AttackEditorValidationResults.txt for assertion results.
