# World 1 Runic entrance

The Hub's existing gate artwork now has a persistent cyan **CFXR3 Magic Aura A (Runic)** and a compact **WORLD 1 / ENTRANCE** label. The existing nearby prompt, gate confirmation and `PlayerHubController.BeginRun` stage-loading path are unchanged. Walking through the aura does not start the game.

## Object and settings

The authored object is `SanctuaryEnvironment.prefab → World 1 Entrance Portal`. In PlayerHub Scene View it is inherited by `Hub Editor Preview`; in Play Mode it is inside the stage runtime's `Sanctuary Environment(Clone)`.

`HubWorldPortal.runicAuraReference` is assigned to:

`Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab`

A nested instance of that original prefab lives under the portal object. The original VFX asset is unchanged. The child is authored once and instantiated with the normal Hub environment; there is no timer or repeated effect spawning.

| Setting | Current value |
| --- | --- |
| Ground/interaction center | `PlayerHub.asset → Stations → Element 3`, `(15.2, 0)` |
| Interaction radius | `PlayerHub.asset → Interaction Radius`, `1.2` world units |
| VFX scale | `HubWorldPortal → Vfx Scale`, `(0.9, 0.9, 0.9)` |
| VFX center | `1.05` world units above the interaction point |
| VFX orientation | X rotation `90°`, bringing the original runic plane into the 2D camera plane |
| Particle sorting | `Default`, order `round(-groundY × 100) - 1`; initially `-1` |
| Label | Height `2.25`, `Default` order `200`, font size `48`, character size `0.035` |
| Availability | `Unlocked = true` |

Background panels remain at approximately `-900`; the portal is above them. Players retain existing lane sorting (`round(-playerY × 100)`): a player in front renders above the aura, while one behind follows ordinary depth ordering. The translucent ring has an open center, preserving player readability. The Hub's actual orthographic camera and approximately 1.06-unit character height were used to inspect scale; camera/player/environment settings were not replaced.

All particle systems retain their existing shapes/animation, loop continuously, prewarm and play on awake, with hierarchy scaling and no stop cleanup. Their instance colors are cyan for contrast against the warm Hub art. Collision/trigger modules are disabled. The prefab's optional CFX camera-shake/cleanup helpers and lights are disabled on the instance. The aura has no colliders, combat hitboxes, damage components or gameplay input handlers.

`Unlocked` controls aura visibility only. This is a visual hook for a future locked entrance, not a world progression/permission system; actual locked gameplay rules are intentionally not implemented.

## Reposition and tune in Unity

1. Open `Assets/EQ_Rung_BeatEmUp/Scenes/PlayerHub.unity`. Enable Scene Gizmos.
2. Expand **Hub Editor Preview → World 1 Entrance Portal**, select the portal and use its **Scene position handle**. This edits `PlayerHub.asset`'s station 3 and the Hub exit marker together, preserving alignment with the existing interaction check. Undo/Redo uses the normal Unity asset undo path.
3. Alternatively select `Assets/EQ_Rung_BeatEmUp/Hub/PlayerHub.asset` and edit **Stations → Element 3**. Both preview and runtime aura follow that point. The portal transform follows this data, so tune the position through the handle/data rather than dragging its VFX child.
4. Open `Assets/EQ_Rung_BeatEmUp/Hub/SanctuaryEnvironment.prefab` in Prefab Mode, select **World 1 Entrance Portal**, and edit **Vfx Scale**, **Visual Height**, **Label Height**, **Sorting Layer** or **Sorting Offset**. Save the prefab. Change the shared **Interaction Radius** on PlayerHub.asset to tune prompt reach.
5. The portal's **Runic Aura Reference** shows the original VFX asset; the nested aura child exposes the retained particle systems and renderer overrides.

The Scene View displays **WORLD 1 ENTRANCE** and its interaction bounds. The existing Hub landmark editor also labels the World 1 station explicitly. A future use of **Create sanctuary defaults** includes the same portal setup; running it is unnecessary for this change and resets other authored Hub defaults.

## Testing

1. Run **Beat Em Up → Hub → Validate sanctuary (Play Mode)**. It opens PlayerHub and uses a separate temporary save directory. Read `Documentation/PlayerSanctuaryValidationResults.txt`; it must end in `ALL PLAYER SANCTUARY CHECKS PASSED`.
2. Open MainMenu, enter Play Mode and start Single Player. Walk from the Buddha spawn toward the rightmost gate. Confirm the runes and label stand out before reaching interaction range.
3. Walk through/in front of/behind the effect along the allowed walking lanes. Confirm movement and health are unchanged and the player stays readable.
4. Approach within `1.2` units of `(15.2, 0)`. The existing **[E / Select] Enter World 1** prompt appears. Press Interact to open the existing gate panel, then confirm with **Enter / gamepad A**. Confirm `Stage01_EntranceGate` loads. Merely touching the effect must leave you in the Hub.
5. Remain in the Hub for at least twenty seconds. Confirm the same effect continues looping. In Play Mode the portal Inspector's `Unlocked` toggle hides/restores its aura without creating another object.
6. Stop Play Mode and inspect the World 1 Scene label and radius. Move the Scene handle, Undo/Redo and reload to verify authored placement persists.

The validator checks the VFX reference/origin, prompt-radius boundaries, loop/prewarm settings, twenty seconds of particle playback on the same instance, sorting above the background and relative to front/behind players, movement/health safety, visible label, availability toggle and the existing Hub entry/death/progression flow. Actual camera captures are in `Documentation/PlayerSanctuaryPreview/WorldPortalApproach.png`, `WorldPortalFront.png`, and `WorldPortalBehind.png`.

## Files

Created `Assets/EQ_Rung_BeatEmUp/Scripts/Hub/HubWorldPortal.cs` and `Assets/Editor/Stages/HubWorldPortalSetup.cs`, with metadata. Modified `SanctuaryEnvironment.prefab`, `PlayerSanctuarySetup.cs`, `PlayerSanctuaryValidation.cs`, the multiplayer catalog and validation report. The Hub scene inherits the updated environment prefab; existing environmental artwork, input and stage-loading code are retained.

Validation completed with Unity 6000.4.6f1: **67 Hub assertions passed**, the Windows development build succeeded, and a real two-process Authentication/DTLS Relay smoke test passed. The portal was visually inspected in actual camera captures from three units away and with the player in front/behind. Source runtime scripts and the environment prefab matched the validated isolated copy. The Relay test covers broader Hub/game integration; detailed portal assertions run in Play Mode.
