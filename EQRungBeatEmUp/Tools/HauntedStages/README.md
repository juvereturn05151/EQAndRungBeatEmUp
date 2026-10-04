# Haunted stage art export

The built-in ImageGen tool drew one complete plate per stage. `generation.json` stores each exact prompt, generated file provenance, and inspected source wall/floor boundary. `Reference/` preserves the supplied poster and written brief. Full source images also live under each stage's Source folder.

`package.cjs` requires Node.js and Sharp. Run from the project root:

```
node Tools/HauntedStages/package.cjs
```

This exports 16 gameplay PNG sprites, eight assembled previews, manifests, the gallery, and the comparison overview. Image processing only splits, aligns, and samples the already drawn art; it does not draw replacement scenery. Existing .meta files retain their GUIDs. The preserved Source images allow export if the original Codex generated-image files have moved.

The Unity editor entry **Beat Em Up → Art → Build and validate haunted stage previews** creates eight art-only prefabs and reports sprite settings, references, centered pivots, bounds, and seam checks. It never modifies the existing demo scene, camera, collision, combat scripts, or actor assets.
