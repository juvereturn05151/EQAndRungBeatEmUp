# Prologue Studio

Open **Beat Em Up → Story → Prologue sequence editor** in Unity. Work outside Play Mode.

## Edit and preview a sequence

1. Choose a chapter on the left. The editor uses the sequences already assigned to `Prologue.asset`.
2. Press **Play** to preview it, or drag the scrubber. Select a timeline clip or a numbered event on the right to edit it.
3. Edit the event's duration, actor name, destination, animation speed, camera, fade, or conversation. **Move** and **Spawn** targets appear as orange crosshairs in the preview: drag a crosshair to place the event.
4. Use **Add**, **Copy**, **Remove**, **Move up**, and **Move down** to author the sequence. Edits support Unity Undo/Redo.
5. Consecutive events with the same positive **Parallel group** run together. The next block begins when the longest event finishes. A group of `0` runs sequentially.
6. Edit dialogue through a Dialogue event's conversation picker and its **Lines / portraits / choices** section. This edits the shared dialogue database, including Thai and English text.
7. Press **Save assets** to write edits to disk. The runtime reads these same assets.

Use **Make opening placement instant** to set all leading Spawn and Face events to duration `0`. They then run together before the first rendered frame, so actors start at their authored positions without appearing in their default positions first. A positive Spawn or Face duration adds a pause after applying that event. Dialogue still waits for input even when its duration is zero.

**Duplicate sequence** creates a separate editable asset. **Assign to [chapter]** connects it to that chapter; a duplicate does not replace the original automatically. Keep sequence IDs unique because story save flags use them.

## Festival walking area

The left panel exposes **Map width**, **Lane bottom Y**, and **Lane top Y**. The current lane values are preserved at `-1.2` and `-0.2`.

Enable **Position / lane guides** to see the green walking band. Drag either green edge to change the lane. **Show whole stage** reveals the complete panorama, including both stores, and a dashed camera frame.

Map width controls both the panorama scale and horizontal movement limits. Players have a half-unit margin inside each side. Exploration completes after interacting with both stores; the old far-right goal is no longer used. Lane changes apply to the school fair; other prologue environments retain their existing limits.

## Festival store memories

During playable festival exploration, players must approach **ร้านไก่ป๊อป** on the left and **Pepsi** on the right and press the existing **Interact** action (E on keyboard, the bound shoulder button on gamepad). Either order works. The objective shows `0/2` and `1/2`; both conversations must finish before Chanai's scene starts. Passing the old destination marker does not advance this section. Skipping a conversation after interacting counts as visiting that store. Visits are saved immediately, so resuming the exploration checkpoint retains completed stores.

In Prologue Studio, expand **Festival stores** to edit the stall positions, width, and interaction radius. Positions are world coordinates; interaction takes place in front of each stall at the midpoint of the walking lane. The sprites sit behind the players. Select **Festival · Chicken Pop memory** or **Festival · Pepsi memory** in the chapter list to edit their sequences and bilingual dialogue. The preview focuses on the selected stall. **Save assets** persists edits. The two sprite references are also available in the definition Inspector.

The store installation adds missing conversations and sequences without resetting existing authored chapters. Build/reset retains assigned store sequences and loads the store dialogue from the seed database.

## Tutorial editor

Open **Beat Em Up → Story → Tutorial editor**, or click **Playable tutorial** in Prologue Studio. Select one of the nine practice steps to see its player, partner, and enemy placement. Step 9 previews all three enemies. Steps keep their existing gameplay objectives and order; this editor adjusts their layout.

Drag any of the four green edges to resize the walking area, the yellow line to change the movement goal, and the orange crosses to place the player, partner, and first enemy. Numeric fields on the right also edit those positions, wave spacing, camera position, and camera size. Enable **Show whole stage** to see more of the backdrop. Enable **Position / lane guides** for dragging.

The player and partner starts represent player slots; the preview uses EQ and Rung as examples. In single player the companion follows the leader during gameplay. Players and moving enemies share the tutorial bounds; passive practice dummies remain near their spawn. Spawns and the goal are clamped to the area, and the editor warns when authored positions are outside it. Leave enough space to walk in all four directions and fight.

Use **Save assets** to persist edits to `Prologue.asset`, and **Ctrl+Z** to undo. Changes take effect when the tutorial next starts (restart or retry it in Play Mode). These settings do not change the festival or later boss encounters. The preview displays layout only; combat and objective completion require Play Mode.

## Finish and skip

**On finish / skip** edits the sequence's final events. These apply instantly both when the sequence completes and when it is skipped, regardless of their duration. Use them for required flags, final positions, visibility, and camera framing. **End / skip** previews the resulting state.

## Preview limits

The preview draws the actual sprite assets and walk clip frames, camera framing, actor placement and facing, poses, fades, dialogue, and an illustrative VFX marker. It does not instantiate objects in your scene, play audio, advance story saves, or run gameplay. Combat, possession, awakening, particles, camera shake, and custom signals need a Play Mode check; their cues appear below the preview.

Dialogue waits for player input in the game. **Seconds per line** is only a preview estimate and does not change gameplay timing. Preview speed is also editor-only. Chapter defaults supply the starting cast and environment; add explicit **Spawn** events when you need an exact starting position or a custom actor. Use the exact actor keys from the story, such as `EQ`, `Rung`, `Prapot`, `Cream`, and `Chanai`.

The **Build or reset prologue assets** menu regenerates authored story sequences and dialogue from setup code. It can overwrite Studio edits. Use **Save assets**, not that reset command, for normal authoring.

Run **Beat Em Up → Story → Validate prologue editor** to check preview scheduling, backward scrubbing, parallel movement, dialogue, finish events, and Undo. Results are written to `Documentation/PrologueEditorValidationResults.txt`.
