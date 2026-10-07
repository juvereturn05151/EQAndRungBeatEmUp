# Haunted-house enemy art package

Complete prototype artwork derived from the supplied possessed-student roster. Created with the built-in ImageGen tool, then cropped, reduced to shared character palettes, aligned, and exported locally. Exact generation prompts and source provenance are in `Tools/HauntedEnemies/generation.json`.

Open `Gallery.html` to view every animation, flip facing, and download sheets or editable files. `RosterPreview.png` compares silhouettes and relative scale.

## Animation inventory

All seven characters include **Hurt_Light, Hurt_Heavy, Air_Hit, Knockdown, Downed, GetUp, Defeated**. The following states are additional to those seven reactions:

| Enemy | Core and unique animations | Total sequences / frames |
|---|---|---|
| Rusher | Idle, Walk, Sprint, Attack_Slash1, Attack_Slash2, Attack_Lunge, Recovery | 14 / 83 |
| GrapplerBruiser | Idle, Walk, Grab_Start, Grab_Hold, BodySlam, HeavyAttack, Recovery | 14 / 84 |
| Thrower | Idle, Walk, Throw_Notebook, Throw_Pens, Throw_Papers, Recovery, Alert | 14 / 84 |
| Screamer | Idle, Walk, Scream_Wave, Wail_Burst, Telegraph, Recovery, Stunned | 14 / 80 |
| Ambusher | Idle_Crouched, Crawl, Hide, PopOut, LegTrip, Walk, Recovery | 14 / 88 |
| Prefect | Idle, Walk, Whistle_Call, Talisman_Cast, BatonAttack, Command, Recovery | 14 / 83 |
| WhiteGhostBoss | Idle_Float, Glide, Invulnerable, Vulnerable, Stunned, Recovery_Rise, Warp_Start, Warp_Disappear, Warp_Appear, Attack_Swipe, Attack_CurseWave, Attack_Summon | 19 / 111 |
| Totems | Idle, Damaged, Destroyed | 3 / 18 |
| Effects | Notebook, Pen, ExamPaper, ScreamWave, StunBurst, CurseWave, TalismanSlip, SummonSeal | 8 / 48 |

Total: **114 sequences, 679 transparent frames**. Character artwork accounts for 103 sequences / 613 frames; support artwork accounts for 11 sequences / 66 frames. Sequences contain 4–8 poses, generally six, with timed holds. No required animation state is omitted. Facing is canonical right; use `SpriteRenderer.flipX` for left.

## Files and import

Each enemy has `Source/`, `Animations/<State>/`, `manifest.json`, and an overview sheet. Each animation folder contains individual numbered PNG frames, a `_Sheet.png`, Aseprite-compatible `_Sheet.json`, `_Preview.gif`, editable `.aseprite`, and Unity `.anim` clip. `Unity/` contains an art preview prefab and controller for each character.

Normal enemies, totems, and effects use 160 × 128 canvases. The boss uses 192 × 192. All gameplay frames use 100 PPU, Point filtering, no mipmaps, uncompressed textures, and binary transparent alpha. Character pixels end at row 119 (boss 183); pivot is the ground line at row 120 (boss 184), centered horizontally. Effect pivots are centered. Idle body heights are 100 pixels for Rusher/Thrower/Screamer, 116 for Bruiser, 70 for crouched Ambusher, 106 for Prefect, and 150 for Boss.

Individual frames are imported as single sprites with stable GUIDs. Sheets are review/export images; do not assign their unsliced textures to a gameplay clip. Clips bind a SpriteRenderer at their object root. In the preview prefab the Animator and SpriteRenderer both live on `Visual`. Movement is in place. Character position and facing belong to runtime movement code.

Idle, locomotion, downed, hiding, grab hold, and boss presence states loop. Attacks, reactions, get-up, defeat, and warps are one-shots. GIFs always repeat for review. Downed and GetUp begin with the last Knockdown pose; GetUp ends at Idle. JSON contains per-frame timing. Preview controllers expose runtime reaction aliases (GroundHit, AirHit, Launched, Falling, Landing); they have no automatic transitions.

## Reuse and integration boundaries

The existing ThaiBadBoy sprite importer and Hurt clip conventions were reused, together with the project's frame-based combat naming. Existing ThaiBadBoy artwork and player artwork were inspected for scale and retained. None of their character images were replaced or reused as these seven identities.

The package supplies artwork and preview objects. Enemy AI, hitboxes, AttackData timing/damage, grab pairing with the victim, projectile collision, summons, and four-totem invulnerability rules still require gameplay integration. Use attack frames with the existing AttackPlayer pipeline; keep clip and attack frame timing synchronized. A totem sprite can be instantiated four times around the boss arena.

## Validation and remaining polish

`ExportValidation.json` records canvas, palette, transparency, nonempty frame, unique-pose, and file checks. `ArtValidation.json` records bounds and hashes for all frames. `UnityImportValidationResults.txt` records actual Unity import, clip references, loop flags, and preview prefab checks. Rebuild previews with **Beat Em Up → Art → Build and validate haunted enemy previews**.

This is a complete mock-up set, suitable for prototyping. Before final production, hand-polish face/uniform details across frames, foot spacing, weapon arcs, large-body fall contacts, crawler transitions, and boss dissolve silhouettes. Runtime playtesting should refine anticipation, impact, endlag, grab alignment, and effect attachment points. Optional turn/spawn/guard animations are not included; facing uses flipX. There is no final combat playtest of these art preview prefabs.

## Re-export

Run `Tools/HauntedEnemies/package.cjs`, then `review.cjs`, with Node and Sharp available. Source sheets are preserved in each character's Source folder. Generation prompts retain original ImageGen output paths; the exporter can use the preserved source sheets when those original paths are unavailable. Do not delete `.meta` files: their GUIDs connect frames, clips, controllers, and prefabs.
