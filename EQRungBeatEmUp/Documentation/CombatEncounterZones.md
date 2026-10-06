# Combat encounter zones in the current level editor

The existing `LevelDefinition` inspector, `StageFlowController` inspector, wave scheduler and live Scene preview are extended in place. Zones are serialized `EncounterDefinition` entries in each stage's existing `encounters` list. No separate encounter scenes or saved preview objects are required. Existing entries default to `useCombatBounds = false` and retain their previous spawn behavior.

## Author a zone

1. Select the level asset (or click **Edit level / ordered stages** on Haunted House Stage Flow).
2. Select a stage in the existing stage list.
3. Click **+ Add Encounter Zone** in the Encounter Zones section. **+ Add Spawn Trigger** creates an unlocked traversal spawn instead.
4. Expand the selected stage's **Encounters** entry to configure name (`encounterId`), trigger, combat and camera bounds, exit lock behavior, one-shot and clear condition.
5. Click **Preview Encounter** and enable **Edit Zone / Spawn Handles**. Orange handles move/resize the trigger; red handles move the combat rectangle and resize its two opposite corners; magenta handles edit camera bounds. Cyan handles move spawn positions. **Focus Encounter** frames the arena. Asset edits support Undo and refresh the same selected stage.
6. **+ Add Wave** adds a clear-based subsequent wave (the first wave is Immediate). **+ Add Enemy Spawn / Point to Selected Wave** creates an enemy group; assign its prefab in the Encounters list above. The per-group **+ Spawn Point** buttons add positions you can move visually. An **All waves** selection adds new groups to Wave 1; selecting a wave targets that wave.

Wave settings reuse the existing data: Immediate, After Delay, After Previous Wave Cleared, or Manual trigger; wave delay; enemy prefab and count; group spawn delay; per-enemy interval; and a list of positions reused cyclically when count exceeds positions. Clear-based wave delay starts when the previous wave clears. Time-based wave delay starts at encounter activation. Enemy group delay starts when its wave starts. Counts and queued spawns must finish before a wave can clear.

## Locks and completion

Any living local player entering or crossing the trigger activates the encounter. Ground XY is used even during jumps. Only one combat zone runs at a time; ordinary traversal triggers can overlap combat scheduling.

Combat zones constrain the existing `CharacterMotor.arenaMin/arenaMax`; players outside the arena are brought inside on activation. BothSides, LeftOnly, or RightOnly selects horizontal barriers. Lane Y is restricted to combat bounds. Encounter enemies use the complete combat bounds, with spawn positions clamped to the stage/arena intersection. The red lock markers in Scene View represent these temporary movement restrictions; they are not saved wall colliders.

The existing `StageFraming` safe-zone camera accepts a temporary encounter override. It follows inside the authored horizontal camera area, clamps to art edges and pillarboxes narrow arenas using its existing viewport system. Clearing removes the override and restores normal stage following. The existing vertical/jump composition is retained; vertical camera center is clamped when the view fits, or centered in a shorter authored rectangle. Author camera bounds tall enough for the desired jump view.

Default completion requires every wave and queued enemy spawn to finish and all encounter enemies to die. Optional ManualSignal also requires `SignalEncounterClear(encounterId)` after the waves clear; its debug button is available in the F8 panel. Destroyed enemies count as removed; inactive living enemies still prevent completion. OneShot prevents revisits from respawning enemies. When disabled, PlayerZone re-arms only after all living players leave the trigger. The first clear satisfies its required stage-completion obligation; an active repeat still locks progression.

Required encounters gate ReachExit as well as ClearEncounters stages, preventing an untriggered required arena from being skipped at the stage exit. Optional unlocked Spawn Triggers do not gate the exit by default. Existing safe stage rules remain unchanged.

## Preview and simulation

**Preview Encounter** reuses render-only live Scene preview. It shows artwork, trigger, combat/camera rectangles, barriers and spawn markers without running AI or combat. **Clear Preview** restores original editor visibility; it does not undo authored data edits. Duplicate creates independent wave/spawn data with a unique encounter name. Delete supports Undo and adjusts explicit previous-encounter indices; review warned dependencies when their referenced encounter was removed.

**Simulate Encounter** uses the real Play Mode runtime. In Edit Mode it opens the playable HauntedHouse scene if needed (Unity's normal save prompt protects scene edits), then starts at the selected stage/encounter after domain reload. In Play Mode it restarts that selected stage directly. Earlier encounters are skipped for this debug run only. Later encounters remain available for continued traversal. Stop Play Mode to end simulation. F8 shows encounter state, waves started/total, living enemies and manual signals. The authoring stage selection persists across previews and Play Mode.

Online decisions and enemy spawning are guarded by the existing `MultiplayerSession.IsAuthority`. The host snapshot sends the active encounter name and camera override to clients; clients use visual replicas and never run encounter spawning. Player restrictions and deaths are already part of the authoritative world simulation. No separate enemy network-spawn system is introduced.

## Entrance Gate test asset

Stage01 Entrance Gate now authors traversal → Encounter 01 (two Rushers) → traversal → Encounter 02 (two Rushers and one Thrower) → exit. Trigger, combat/camera bounds and positions are asset data. Exit was moved beyond Encounter 02; its existing upgrade reward is retained. Other stages and existing traversal spawn data remain intact.

## Validation

Run **Beat Em Up → Stages → Validate combat encounter zones (Play Mode)**. It validates render-only preview/selection persistence and uses a disposable level copy for traversal, required exit gating, two arenas, player/camera bounds, clear/unlock/advance, delayed waves and queued enemies, direct simulation, unlocked spawns, repeatable/manual clear settings, side-specific locks, fast trigger crossing and two local players. Results are saved in `Documentation/EncounterZoneValidationResults.txt`. It defeats fixture enemies through `CharacterHealth.Damage`; it checks scheduling and locks rather than manual fighting or weapon balance. Online peer behavior still warrants a separate host/client playtest.

## Files

- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/LevelDefinition.cs`: additional zone / spawn data and wave trigger labels.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/StageFlowController.cs`: trigger crossing, arena lifecycle, motor limits, completion and direct simulation.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/StageFraming.cs`: temporary camera override within existing follow system.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Multiplayer/WorldSnapshot.cs` and `MultiplayerSession.cs`: authoritative camera/encounter snapshot fields.
- `Assets/Editor/Stages/EncounterPreview.cs`: arena, barrier and camera handles/labels, matching clamped spawn preview.
- `Assets/Editor/Stages/EncounterZoneAuthoring.cs` (+ Unity meta): current-inspector authoring controls and Play Mode shortcut.
- `Assets/Editor/Stages/EncounterZoneValidation.cs` (+ Unity meta): Unity runtime / preview validation.
- `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset`: Entrance Gate sample.
- This guide and `Documentation/EncounterZoneValidationResults.txt`.
