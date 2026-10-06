# GrayShirtGuy presentation update

GrayShirtGuy retains the BlueShirtGuy prefab variant and shared movement, combo, meter, skill, hit, defense, Stun, knockdown and grab systems. His identity and selection/save ID remain `gray-shirt`. Existing asset GUIDs were preserved through the super asset renames.

## Inspection and art

Before editing, the existing native sheet, BlueShirtGuy sprite/import settings, locomotion clips, eight AttackData assets, defense poses, character loadout, shared combo/skill/hitbox/feedback systems and original skill were inspected. Original GrayShirtGuy had 64 pose assets, a four-frame walk and an elephant manifestation followed by a shared ground-area attack.

All 64 original character pose slots were redrawn: Idle, Walk, Punch1–3, Launcher, Jump, AirPunch1–3, AirDive, Dodge, Guard, Parry, Grabbed, Hit, Stun, Knockdown, GetUp, Cast and Die. The new walk replaces four drawings with twelve, adding eight pose assets: **72 character pose assets total**. The two existing parry pose slots share one polished drawing, retaining their separate authored holds. Gray shirt, stocky build, black hair/glasses, navy shorts and blue sandals remain consistent.

Textures retain transparent backgrounds, point filtering, uncompressed import, 100 PPU and a feet pivot eight pixels above the canvas bottom. Idle/walk use 128×128 cells. Combat/reaction/cast use 256×160 transparent cells to accommodate extended limbs, wand and snake without shrinking the body. Wider canvases change available art space, not collision or character scale. Upright body height remains approximately 106 pixels.

Walk has twelve distinct drawings held three combat frames each, a **36-frame / 0.6-second loop**, with contact, weight shift, passing, advancing foot and opposite-step poses. Foot anchors and one uniform extraction scale preserve body size; head/arm movement and leg crossings are drawn into the frames. Existing movement speed is unchanged.

Art was generated using the built-in imagegen tool, then sliced and normalized with the project art packaging utility. The generated sources, extraction manifests and prompts are retained. See `Character2PolishArtPrompts.md`; original drawings and elephant effects are archived outside Unity's Assets tree in `Tools/Character2/Archive`.

## Punch 3: scalp snake

`Character2_Punch3.asset` now plays anticipation → snake emergence → extended scalp snake → retraction. The snake is part of the character drawing, naturally mirrors with the shared renderer and follows its motor. There is no snake projectile, summoned actor, extra damage component or new combo state.

The extended snake drawing is displayed throughout existing active frames. Startup, active/recovery duration, damage, hit ID, once-per-target history, movement, combo cancels and wall bounce are retained. The hitbox changes to offset **(0.53, 0.88)** and size **(0.9, 0.42)**, aligning the existing forward reach with head-height impact. Only Character 2's ground Punch 3 changes; Punch1/Punch2 and all airborne attack mechanics retain their data.

Tune sprites, frame holds, hitbox, release sound, damage and bounce in the existing Frame Attack Editor for `Character2_Punch3.asset`. Enable **AttackHitbox > Debug Draw** in Play Mode to inspect the contact volume.

## Wand Barrier

`WandBarrier.asset` remains a shared `PlayerSkillData` area skill costing exactly **one full bar**. `Character2_WandBarrierCast.asset` supplies six drawings: prepare/reach, reveal wand, raise wand, cast, barrier activation and recovery. The skill uses its existing 48 combat-frame duration:

| Zero-based frame | Behavior |
| --- | --- |
| 0–15 | Reveal/raise the wand; shared warning ellipse surrounds caster |
| 16 | `RaiseBarrier`: instantiate surrounding barrier shell |
| 24 | `BarrierPulse`: instantiate outward pulse |
| 24–27 | Existing area hitboxes damage surrounding targets |
| 28–47 | Recovery; VFX fade and expire |

The eight-frame shell is centered on the caster, scaled to surround the area, rendered behind him and tinted to preserve face/wand readability. Four pulse frames expand from his position. The shared pixel warning/active ring depicts the exact area ellipse. These visuals are cosmetics; damage still comes from the existing AttackHitbox sampler.

Preserved defaults: radius X **2.25**, walking-depth radius Y **1.2**, damage **32**, hitstun **32 frames**, hitstop **7 frames**, outward recoil **5**, shared hit ID **901**, one accepted hit per target per cast. Multiple nearby enemies can be hit; targets beyond the walking-depth boundary remain excluded. Boss shield/vulnerability checks still apply. A barrier visual does not add defensive invulnerability or absorb damage.

The shared feedback data gains one option, **Impact Rotate With Facing**, enabled by default for existing effects. Both barrier feedback assets disable it, so left-facing casts stay upright. This fixes the original effect orientation issue without a character-specific runtime controller.

Tune cost and radius/damage/recoil through `WandBarrier.asset`'s existing skill Inspector. Tune frames/events/area hitboxes through its cast asset. Tune VFX opacity, sprite animation, offset and scale on `WandBarrier.prefab` and `BarrierPulse.prefab`; tune sound and cleanup lifetime on the corresponding feedback assets. Radius edits update collision/area indicators; adjust shell scale separately when changing radius substantially. Normal multiplayer catalogs must be rebuilt after art/data changes.

## Created, modified and replaced files

All paths below are relative to the project root.

Created:

- `Assets/Editor/Combat/Character2PolishSetup.cs` and metadata: focused, repeatable migration; preserves current gameplay tuning.
- `Tools/Character2/prepare_polish.py`: generated sheet packaging and manifests.
- Character art: `Walk_05.png`–`Walk_12.png`, `Barrier_01.png`–`Barrier_08.png`, `Pulse_01.png`–`Pulse_04.png`, polished/walk/barrier source sheets and review sheets, corresponding metadata and extraction manifests under `Assets/ArtAssets/Characters/Character2`.
- This guide, prompt record and `Documentation/Character2PolishPreview` Unity captures/walk preview.
- `Tools/Character2/Archive`: recoverable original art and retired effect sprites.

Modified:

- All original character pose PNGs and their import metadata; `Character2_Walk.anim`; `Character2_Punch3.asset`.
- `Assets/Editor/Combat/Character2Setup.cs`: future defaults produce the twelve-frame walk and wand skill.
- `Assets/Editor/Combat/Character2Validation.cs`: verifies preserved mechanics, snake geometry, twelve walk frames and upright centered VFX; captures Unity previews.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Combat/AttackData.cs` and `AttackFeedback.cs`: generic authored feedback orientation option.
- `Assets/EQ_Rung_BeatEmUp/Scripts/Multiplayer/BossNetworkValidation.cs`: online fixture recognizes the replacement VFX names.
- `Assets/EQ_Rung_BeatEmUp/Resources/MultiplayerCatalog.asset`: updated content hash/sprite and feedback references.
- Character documentation and validation report.

Renamed and replaced, preserving GUIDs:

- `LanternGuardian.asset` → `WandBarrier.asset`.
- `Character2_GuardianCast.asset` → `Character2_WandBarrierCast.asset`.
- `LanternGuardian.prefab`, `.controller`, `LanternGuardianFeedback.asset` → `WandBarrier` equivalents.
- `SpiritBlast.prefab`, `.controller`, `SpiritBlastFeedback.asset` → `BarrierPulse` equivalents.
- `Character2_LanternGuardian.anim` → `Character2_WandBarrier.anim`; `Character2_SpiritBlast.anim` → `Character2_BarrierPulse.anim`.
- `Guardian_01`–`04`, `Guardian_Source`, old `Wave_01`–`04` and `WarningRing` PNGs were removed from active Unity assets and archived. The active skill contains no elephant.

## Exact Unity testing steps

1. Let Unity 6000.4.6f1 finish importing/compiling. The migration has already been applied; do not run **Create Character 2 defaults** to test, because it resets authored tuning.
2. Save your scene. Run **Beat Em Up → Characters → Validate Character 2 (Play Mode)**. Read `Documentation/Character2ValidationResults.txt`; it must end with `ALL CHARACTER 2 CHECKS PASSED`. The validator opens HauntedHouse and exits Play Mode after fixtures and VFX cleanup.
3. Open `Assets/EQ_Rung_BeatEmUp/Scenes/MainMenu.unity`, enter Play Mode, choose **GrayShirtGuy** and start Single Player. Verify the casual idle and readable glasses/hair/outfit. In the Hub, change between BlueShirtGuy and GrayShirtGuy and confirm normal selection.
4. Walk continuously left/right and along depth. Inspect planted sandals, intermediate passing poses, alternating arms and restrained bob. Open `Character2_Walk.anim` in Animation preview to step through all twelve drawings.
5. Chain Punch1 → Punch2 → Punch3 near a normal enemy. Verify scalp snake emerges at the final attack, contacts on the extended drawing, retracts and retains normal damage/recovery. Face left and repeat; inspect Debug Draw. Repeat near a physical wall to verify the existing wall bounce.
6. Test launcher/air combo, dodge, guard/parry, hit, Stun, knockdown/get-up and a Grappler grab; confirm the own-character reaction art and ordinary shared state behavior.
7. With one full meter bar, use the existing skill input (**I / gamepad right trigger**). Verify wand reveal, casting pose, surrounding shell and outward pulse. The meter empties once and a second cast is denied until refilled.
8. Place enemies left, right and in an adjacent valid depth lane. Cast and verify each takes one hit and recoils away. Place another beyond the configured depth radius; verify no hit. Repeat while facing left; the barrier must stay upright and centered.
9. Cast near the shielded boss, then during a physical Totem-wave vulnerability window. Verify shielded HP is protected and vulnerable HP accepts normal skill damage. Confirm all VFX disappear after the move.
10. Use the rebuilt development player for a mixed-character co-op session. Select GrayShirtGuy locally/on a Relay client; verify the host-approved identity, own sprites, one-bar cast and both barrier effects. Both peers need the same updated build/content hash.

The built-in validator covers mechanics and asset wiring. Unity screenshots and native art were visually inspected; final subjective walk feel can be adjusted through clip holds without changing movement/combat systems.

## Recorded validation

Unity 6000.4.6f1 passed **594 character assertions**, covering both character loadouts, original combo/air routes, snake geometry and timing, wall/ground bounces, defense/reaction states, meter, multi-target area damage/recoil/depth, boss protection, twelve-frame walking, upright centered effects and cleanup. The updated Windows development player built successfully. A real two-process Authentication/DTLS Relay session with mixed characters passed, including client reconstruction of `WandBarrier` and `BarrierPulse`, selection/Hub persistence and boss/minion coordination. The shared online test retains legacy internal phase/counter names such as `Character2Guardian`; its visual checks now require the new barrier prefab names.

Reports: `Character2ValidationResults.txt`, `MultiplayerBossRelay2HostResults.txt`, `MultiplayerBossRelay2Client1Results.txt`. Unity previews are `Character2PolishPreview/HeadSnake_Impact.png`, `WandBarrier_Right.png`, `WandBarrier_Left.png`; `WalkLoop.gif` shows the native twelve-pose cycle. Source runtime scripts and Character 2 assets were hash-compared with the validated isolated copy, with zero mismatches.
