# Stage camera safe-frame follow

Open HauntedHouse.unity. Select **Main Camera > Stage Framing > Camera tracking**:

- **Follow Enabled**: enable horizontal player tracking.
- **Follow Smooth Time**: horizontal damping in seconds; default 0.12. Zero follows immediately.
- **Left Safe Margin / Right Safe Margin**: fractions of the gameplay viewport reserved at each edge; defaults 0.25. The middle 50% is the safe zone. Larger margins start follow earlier. Each side is limited to 0.45.
- **Show Gizmos**: yellow outline shows the safe frame; white shows the camera view.

The camera remains still while the player's sprite fits inside the safe frame. Beyond it, horizontal movement eases toward the nearest safe position. Fast movement or teleports are corrected immediately only as needed to keep the sprite inside the actual view. Stage edges take precedence over safe margins, so the player may be closer to the edge at the end of a room.

StageFlowController refreshes **Stage Left / Stage Right** from the intersection of the active background and floor renderer edges. Do not manually edit these runtime-managed values. Per-stage **Art Width** in ThaiHauntedHouse.asset controls horizontal coverage. Movement bounds, encounter locks, rewards, and exits remain separate and unchanged. Transitions reset damping and install the next stage's art limits. Live stage art edits update these limits too.

If an ultrawide viewport or existing airborne zoom would show more horizontal space than the artwork covers, the camera uses a centered narrower viewport (pillarboxing). This preserves vertical size and avoids rendering blank world beyond the art edges. Small lane movement does not pan vertically; existing airborne zoom and anchored bottom framing are preserved.

The current stage widths are 7.6 for the hub and ordinary rooms, giving the 1.28-unit player sprite padding at movement limits ±3.05. Entrance Gate uses 12.8 to cover its existing maximum X of 5.73. These changes scale the supplied sprites horizontally; textures, slicing, and the four height/center layout values are unchanged.

**Authoring limitation:** a camera clamped to art cannot display actors walking outside that art. The stage Inspector warns when horizontal movement limits exceed Art Width. Leave room for half the player's sprite width as well. Entrance Gate still has its pre-existing maximum lane Y of 4.75, above the current composition. This horizontal camera update does not change that authored vertical bound or add vertical lane tracking.

Standalone scenes using StageFraming without StageFlowController also get safe-frame follow. Their stage clamping remains disabled unless explicitly configured; the placeholder floor continues to expand. The old serialized Horizontal Dead Zone is retained for compatibility but replaced by viewport margins.

Validation menu: **Beat Em Up > Stages > Validate camera safe-frame follow (Play Mode)**. It checks both directions, smoothing, stationary motion, sprite visibility, art limits, follow disable, hub/combat/boss stages, encounter locks, transitions, narrow/ultrawide output, and airborne zoom.

Unity 6000.4.6f1 validation in the isolated test project passed 35 camera follow checks, 79 stage-flow checks, 21 hub checks, and 16 art-layout checks (151 total). The older standalone framing suite passed another 734 checks after restoring its expected Background Fraction 0.4 and Top Lane 0.65 in the isolated ComboDemo test fixture; the actual project's ComboDemo was not changed. Reports are `Documentation/StageCamera*Results.txt`.

Modified files:

- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/StageFraming.cs`: safe frame, smoothing, sprite visibility guard, art clamping, viewport handling, gizmos.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/StageFlowController.cs`: per-stage limits and transition reset; removed full-art-width dead-zone override.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Stages/LevelDefinition.cs`: new stage art width default 7.6.
- `Assets/Editor/Stages/LevelDefinitionEditor.cs`: movement/art mismatch warnings.
- `Assets/Editor/Stages/PlayerHubSetup.cs`: new hub width default and bounds-aware setup.
- `Assets/Editor/Stages/HauntedLevelBuilder.cs`: removed obsolete dead-zone override.
- `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity`: serialized follow controls and matching hub width preview.
- `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset`: 7.6 art widths; Entrance Gate 12.8.
- `Documentation/StageArtLayout.md`: current width defaults.

Added this guide, `Assets/Editor/Stages/StageCameraFollowValidation.cs` and its metadata, and the validation reports.
