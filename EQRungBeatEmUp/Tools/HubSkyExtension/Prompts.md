# Hub upward scenery extension

Generated with the built-in ImageGen tool. Each original HubPanel0.png through HubPanel4.png was supplied separately as its edit target.

Prompt for each panel:

Use case: precise-object-edit. Asset: Unity pixel art battle hub environment panel. Edit target: attached image. Extend this artwork upward by adding 731 pixels of scenery above its existing top edge, producing a 1672 x 1672 square image. The entire original 1672 x 941 image must occupy the bottom 941 pixels at exactly its original scale and position, with original ground, shrine, buildings, lights, colors and details preserved. Only outpaint ABOVE: continue the large banyan tree trunks, dense canopy and branches naturally upward, with matching purple-orange sunset sky and pixel cloud shapes in openings. Preserve crisp detailed pixel-art texture, same pixel scale, lighting and palette. Do not shrink, stretch, zoom out, repaint the original lower scene or add characters, text, borders or new landmarks. Fill every pixel fully opaque. The added upper region is playable-camera headroom during jumps.

The package script copies the original lower 941 rows unchanged beneath the generated upper 731 rows. It verifies exact equality of the lower RGB pixels. Original images are retained in Originals/. Import pivots keep the original center at the existing prefab transform; scale, walkway, station placement, GUIDs and multiplayer sprite references remain intact.
