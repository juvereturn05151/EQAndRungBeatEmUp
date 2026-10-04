# Per-stage art layout

Open `Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity`. Select **Haunted House Stage Flow** and click **Edit level / ordered stages** (or select `Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/ThaiHauntedHouse.asset` directly).

Select **1. Player Hub**, expand **Selected stage**, and find **Stage Art Layout**:

| Field | Default | Effect |
|---|---:|---|
| Art Width | 7.6 | Shared horizontal width of this stage's two art sprites; Entrance Gate uses 12.8 to cover its extended movement bounds |
| Background Height | 1.6 | Rendered upper sprite height in world units |
| Background Center Y | 2.56 | Upper sprite center position in world space |
| Floor Height | 2.4 | Rendered lower sprite height in world units |
| Floor Center Y | 0.56 | Lower sprite center position in world space |

For a 50/50 example retaining the existing total height and bottom edge, use Background Height **2**, Background Center Y **2.36**, Floor Height **2**, Floor Center Y **0.36**. Every stage has its own values. Existing stages remain at the old defaults until you edit them.

Press Play to see the result. Editing the **level asset** during Play updates the active stage's artwork on the next rendered frame. These are persistent asset edits; stopping Play does not undo them. Save the project with **File > Save Project** after editing. Editing renderer Transforms directly is no longer the authoring workflow; the controller reapplies stage data each frame. Edit-mode scene art does not automatically preview asset edits; start Play to preview.

**Reset to Default Layout** restores just the four height/center values of the selected stage. Unity Undo supports Inspector changes and reset. It leaves sprites, width, movement bounds, stage routing, and rewards alone.

For centered pivots, a seamless join requires:

`Background Center Y - Background Height / 2 = Floor Center Y + Floor Height / 2`

The Inspector warns about a gap or overlap, but allows deliberate overlap. Heights must be positive; Inspector inputs have a 0.01 minimum and runtime scaling clamps nonpositive values. Scaling stretches the existing sliced artwork; it does not redraw or reslice it.

Movement Min/Max, entry/exit positions, camera framing, sorting, and sprite pivots are separate. Changing art does not adjust walkable lanes or camera framing automatically. Keeping total art height at 4 and its bottom at -0.64 fits the existing normal camera view. The camera's Stage Framing Background Fraction is not the per-stage art ratio control.

Validation menu: **Beat Em Up > Stages > Validate per-stage art layout (Play Mode)**. It verifies baseline and legacy defaults, per-stage independence, live edits, seam alignment, movement/safety preservation, transitions, and asset persistence using a disposable level copy.
