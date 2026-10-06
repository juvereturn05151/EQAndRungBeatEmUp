# Character 2: GrayShirtGuy

**Current implementation:** see [GrayShirtGuy presentation update](Character2Polish.md). The twelve-frame walk, scalp-snake Punch 3 and Wand Barrier replace the art and elephant concept described in the original implementation record below.

## Assets and changed files

The second character is `Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2.prefab`, a **variant of BlueShirtGuy**. Its definition is `Character2.asset` in the same folder. `Characters/BlueShirtGuy.asset` registers the original character.

Created runtime scripts: `Scripts/Combat/PlayableCharacterData.cs` and `PlayerCharacterLoadout.cs`. These paths are beneath `Assets/EQ_Rung_BeatEmUp`.

Created editor scripts: `Assets/Editor/Combat/Character2Setup.cs` and `Character2Validation.cs`. Created art preparation utility: `Tools/Character2/prepare_art.py`.

Modified shared combat files: `PlayerSkillData.cs`, `PlayerSkillController.cs`; editor `PlayerSkillDataEditor.cs`; multiplayer `WorldSnapshot.cs`, `PlayerIdentity.cs`, `MultiplayerCatalog.cs`, `MultiplayerSession.cs`, `MultiplayerMenu.cs`, `BossNetworkValidation.cs`; editor `MultiplayerSetup.cs`; `BlueShirtGuy.prefab`, `Resources/MultiplayerCatalog.asset`; test runner `Tools/Multiplayer/RunBossNetworkValidation.ps1`.

Character assets include eight independent attacks: `Character2_Punch1`, `Punch2`, `Punch3`, `Launcher`, `AirPunch1`, `AirPunch2`, `AirPunch3`, `AirDive` (all `.asset`, all names have the `Character2_` prefix). They preserve BlueShirtGuy's logical frames, movement, hitboxes, damage, cancel windows and bounce properties while replacing sprites. Additional assets are `Character2_Defense.asset`, `Character2_GuardianCast.asset`, `LanternGuardian.asset`, `LanternGuardianFeedback.asset`, `SpiritBlastFeedback.asset`, locomotion controller/clips and two animated VFX prefabs/controllers/clips.

## Art and idle

`Assets/ArtAssets/Characters/Character2` contains 64 independent 128×128 transparent character frames, four guardian frames, four blast frames, a trimmed warning ring, source sheets, native preview sheet, supplied concept reference and extraction manifest. Point filtering, uncompressed textures, 100 PPU and feet pivot `(0.5, 0.0625)` match BlueShirtGuy.

The poses cover Idle, Walk, all eight attacks, Jump, Dodge, Guard, Parry, Grabbed, Hit, Stun, Knockdown, GetUp, Cast and Die. Gray tee, navy shorts, blue slides, stocky silhouette, dark hair and glasses carry across the sets. Idle cycles four relaxed poses at 12 logical frames each (48-frame loop), with hanging arms, weight shift and a sandal forward. Walk cycles four stride poses at six frames each. Combat still disables locomotion Animator control and uses AttackData sprites.

Art was created with the built-in imagegen tool from the supplied concept and existing BlueShirtGuy sprites. The original supernatural guardian is a mint/violet spectral elephant with a lantern heart. See `Character2ArtPrompts.md` for the prompt set and saved source paths. Audio currently uses existing project whoosh/impact hooks; replace those clips in the feedback assets for a distinct guardian sound.

## Shared gameplay and selection

`PlayerCharacterLoadout` applies the selected definition before the normal controllers initialize. Both characters use the same CharacterMotor, ComboController, AttackPlayer, AttackHitbox, CombatHurtbox, defense, grab, bounce, health, meter and input components. There is no separate Character 2 combat state machine.

Choose **CHARACTER: GrayShirtGuy** on the multiplayer mode page before Single Player or creating/joining a room. In a lobby each locally owned slot has a CHARACTER button; this also supports local co-op with different characters. Changing a lobby character clears that slot's Ready status. The host validates ownership and character index and instantiates its selected prefab. Reliable snapshots carry character identity and the shared catalog includes both sets of sprites/attacks. Clients retain the existing cosmetic replica architecture; the host handles damage and meter. Both peers need the new matching build/content hash (protocol `GhostFair/4`).

Meter uses the existing one-bar system: starts full, normal accepted hits give 0.06, successful parry gives 0.4, launcher/air bonuses use existing settings, and the maximum clamps to one. The guardian costs one bar and obeys existing skill state restrictions. These are tunable on PlayerMeter; no extra resource system was introduced.

## Lantern Guardian skill

The shared skill controller supports projectile and area delivery. BlueShirtGuy retains the projectile path. GrayShirtGuy plays `Character2_GuardianCast.asset` through AttackPlayer, while ordinary area hitboxes perform damage:

| Event | Zero-based logical frame | Human frame number |
| --- | --- | --- |
| Guardian appears (`SummonGuardian`) | 16 | 17 |
| Radial blast (`SpiritBlast`) | 24 | 25 |
| Damage active | 24–27 | 25–28 |
| Recovery | 28–47 | 29–48 |

The cast lasts 48 logical frames at the existing 60Hz combat clock; hitstop pauses that clock. Guardian and blast creation are explicit data events, not elapsed render-time timers. VFX use timed cleanup and reliable network feedback cues.

Defaults: walking-plane ellipse radius X **2.25**, depth radius Y **1.2**, damage **32**, hitstun **32 frames**, hitstop **7 frames**, outward knockback **5**, four active frames, 20 recovery frames, one-bar cost. The area uses shared hit ID 901 with no repeating hits: one accepted hit per target per cast, including multiple surrounding targets. Each target's walking-plane direction from the caster determines its recoil, independent of facing. Depth and airborne/grounded rules remain in the usual sampler. Boss damage still passes through the Totem vulnerability gate; the guardian cannot unlock or bypass it.

Select `LanternGuardian.asset` to tune cost and feedback, plus the inspector's radius, depth, damage, hitstun, hitstop and outward knockback controls. These record Undo and update all active area boxes. Select the cast frame data and open the existing Frame Attack Editor to tune active/recovery frames, sprites, hitboxes and the two event locations. Preserve one occurrence of each event and a shared hit ID unless repeat hits are intended. Feedback assets tune animation prefab, sound, scale and lifetime. Character2_Defense controls defensive pose holds and timings. Ordinary attacks are separate copies, so their future tuning is independent.

## Exact Unity testing steps

1. Let Unity 6000.4.6f1 import the new files. Open `Assets/EQ_Rung_BeatEmUp/Scenes/MainMenu.unity` and press Play.
2. Open the play mode selection page, cycle CHARACTER to GrayShirtGuy and start Single Player. Confirm casual Idle, walking and left/right flips. For co-op, create a local lobby, join another device, choose a character per slot, mark each Ready and start.
3. Use the existing Controls panel/key bindings for punches, launcher, jump, guard, dodge and skill. Chain Punch1→Punch2→Punch3; chain Punch1→Punch2→Launcher→Jump→AirPunch1→AirPunch2→AirPunch3. Test Punch3 near a wall and the airborne finisher above a launched target. AirDive retains its existing input.
4. Hold guard through an attack, parry within the initial window, dodge during its invulnerable frames, and let enemies cause Stun, knockdown and Grappler grabs. Confirm own reaction sprites and recovery.
5. Use skill **I / gamepad right trigger** with a full meter. Confirm casting poses, elephant manifestation, ring/blast, multi-target hit and outward recoil. Test facing both ways. Whiff to empty meter, try again, then refill with hits/parry. Confirm the HUD never exceeds one bar.
6. In the boss chamber, cast while the boss is shielded: no boss HP loss. Break a nearby Totem so its wave actually touches the boss, then cast during the window: damage is accepted. Confirm effects disappear after casting.
7. Stop Play. Run **Beat Em Up → Characters → Validate Character 2 (Play Mode)**. It opens HauntedHouse and runs fixtures, writes `Documentation/Character2ValidationResults.txt`, then exits Play. This exercises instantiation/data, movement/facing, combo routes, physical bounces, defense/reactions, meter, both skill paths, area hit history/recoil/depth, boss protection and VFX expiry. It does not replace human animation-feel review.
8. For online testing, build with `MultiplayerValidation.BuildDevelopmentPlayer` (the existing editor build utility), then run `Tools/Multiplayer/RunBossNetworkValidation.ps1 -Players 2 -Relay -MixedCharacters` (or `-Players 4`). Separate processes authenticate and connect through real DTLS Relay, select mixed characters and verify host loadouts, client identity, guardian/blast cues and the boss encounter replication. For a household-to-household test, run the same build on two computers, host Online, share the displayed room code, join, choose characters, Ready and start.

Validation completed on 2026-10-06: the Unity development player compiled successfully; **581 character assertions passed** in `Character2ValidationResults.txt`; real **two-player and four-player mixed-character Relay runs passed** in `MultiplayerBossRelay2*Results.txt` and `MultiplayerBossRelay4*Results.txt`, including both guardian effects on every client and the boss encounter replication checks. The projectile regression also passed for BlueShirtGuy. The automated Relay tests use separate processes on this machine. Cross-household latency/input feel and manual controller/visual review remain separate checks.

