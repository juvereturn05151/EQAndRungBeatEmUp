Character 2's wand magic now uses a taller dome (1.2 x 1.05 visual scale) centered 0.95 units above his feet, rather than the flattened 1.84 x 0.8 shell.

The shell uses 42% opacity and the pulse uses 45%. Both render behind the caster, following the same ground-depth sorting as actors through GroundSortedEffect. The warning and active area rings remain aligned with the existing damage ellipse, with reduced opacity so they do not compete with the dome or wand.

Skill damage, hit area, meter cost, timing and knockback are unchanged. Character 2's previously enlarged cast poses remain intact.

Use Beat Em Up > Characters > Polish wand barrier presentation to reapply these prefab settings. The Character 2 setup tools use the same defaults. Presentation validation covers both facing directions, three ground depths and effect cleanup. Unity-rendered previews are in Character2PolishPreview/WandBarrier_Right.png and WandBarrier_Left.png.
