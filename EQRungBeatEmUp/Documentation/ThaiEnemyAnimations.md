# Thai enemy animation replacement

The tutorial Thai delinquent now uses its own controller in `Assets/EQ_Rung_BeatEmUp/Story/ThaiAnimations/ThaiDelinquent.controller`. All 21 states and the direct airborne, downed, stun, knockdown and get-up references use Thai character artwork. Existing Thai idle, walk, punch, hurt, knockdown and get-up sprites are retained. Punch hitboxes and frame timing remain intact.

New artwork: `Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Reactions/ThaiBadBoy_Reactions.png`.

Generated with the built-in imagegen tool using the existing Thai idle, hurt and knockdown sprites as character references. The sheet has 24 poses: six airborne-hit frames, six defeated/downed frames, six stunned frames and six recovery frames. Unity slices the transparent image directly, with point filtering, no texture compression, and 200 pixels per unit. The slicing rectangles accommodate the generated pose boundaries and align the ground pivots with the existing 128-pixel artwork.

Prompt: Create a production pixel-art sprite animation sheet matching the character identity and style of the three reference sprites: Thai male technical-school delinquent, shaggy black hair, tan skin, black short-sleeved shirt/jacket with silver emblem, dark trousers, wallet chain and white/gray sneakers. Use crisp chunky pixel clusters, a limited palette and a genuinely transparent background. Produce a 1536x1024 sheet with six columns and four rows: airborne torso-hit recoil, falling and settling onto the back, standing slumped dizzy sway without stars, and recovery into a defensive stance. Keep the same character, scale and right-facing view in every frame; no weapons, text, grid, scenery or shadows. Provide complete bodies and meaningful changes between frames. Engine-provided stun stars are retained.

Regeneration: `Beat Em Up > Story > Repair Thai delinquent animations`. Prologue setup calls the same configuration method, so rebuilding the prologue preserves the replacement artwork.

Validation: `Beat Em Up > Enemies > Validate Thai enemy attack positioning` checks positioning, runtime animation states and the new parry-stun sprites. Reference validation rejects any remaining Rusher artwork dependencies.
