# Run art generation and export

Mode: built-in image generation, using each character's existing idle/walk/combat PNGs as identity and pixel-art references, transparent background enabled. Two separate outputs were selected. The production specifications below record the intended final generation prompts.

## Character 1 specification

Create an original game-ready pixel-art RUN cycle for the exact existing blue-shirt Thai beat-em-up character in the references. Preserve his athletic proportions, tan skin, short dark hair, face, blue patterned short-sleeve shirt, navy trousers, and gray/white sneakers. Match the reference pixel density, crisp dark outlines, limited shaded palette, lighting, and side/three-quarter camera perspective. Show eight sequential, full-body right-facing run poses in a uniform four-column, two-row grid. Make the action distinctly faster and more committed than walk: forward torso lean, strong opposing arm swings, full leg extension, knee-up passing poses and brief flight phases. Alternate leading legs across a complete seamless cycle. Keep character identity, clothing, scale, head height and ground baseline consistent, with clear separation between equal cells. Actual transparent background; no text, frame borders, ground painting, scenery, effects or extra characters. Leave generous transparent margins so limbs are not clipped.

Selected source: `exec-4d29ed2f-96c5-4ac4-a9b5-33f2c5138798.png`, retained as `BlueShirtGuy-generated.png` beside this file.

## Character 2 specification

Create an original game-ready pixel-art RUN cycle for the exact existing Character 2 in the references. Preserve his stocky/chubby tan proportions, large shaggy dark navy hair, orange/dark glasses and face, gray T-shirt, navy shorts, and blue sandals. Match the reference sprite's pixel-art outlines, palette, lighting and gameplay camera perspective. Eight sequential full-body right-facing poses in equal cells, four columns by two rows, forming a smooth complete run loop. Use a committed forward lean, energetic opposing arm swing, higher knees, extended steps and flight phases so this reads as run rather than walk. Alternate leading legs across the cycle. Maintain a consistent silhouette, clothing, scale and grounded baseline; generous transparent padding. Actual transparent background; no scene, floor, labels, borders, motion trails or other figures.

Selected source: `exec-cd0926f2-efa2-4f50-8ea0-4522c65ffbfa.png`, retained as `Character2-generated.png` beside this file.

## Packaging

`package.cjs` splits the equal cells, trims transparent padding, applies one shared nearest-neighbor scale per character based on the existing standing reference, and exports eight separate 160 x 144 PNGs with crisp alpha. All poses use a common feet baseline; flight poses lift three pixels. It does not repaint identity or replace existing sprites. `PlayerRunSetup` supplies 100 PPU, point filtering and the feet pivot. `preview.cjs` creates the contact sheet and 50 ms/frame loop preview.

Source masters remain outside Assets and are not referenced by runtime prefabs. Only the separate exported PNGs are imported as gameplay sprites.
